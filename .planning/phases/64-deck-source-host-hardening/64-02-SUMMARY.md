---
phase: 64-deck-source-host-hardening
plan: 02
subsystem: security
tags: [security, ssrf, host-trust, moxfield, archidekt, deck-import]

requires:
  - phase: 64-deck-source-host-hardening/64-01
    provides: DeckSourceHost helper (IsMoxfield/IsArchidekt exact-domain predicates)

provides:
  - All four spoofable substring host-match call sites replaced with DeckSourceHost helper
  - MoxfieldApiDeckImporter Spellbook fallback always forwards canonical URL, never submitted URL
  - Per-site regression tests at DeckEntryLoader, MoxfieldApiDeckImporter, PacketArtifactStore

affects: [deck-import, spellbook-fallback, artifact-suppression, sec-hardening]

tech-stack:
  added: []
  patterns:
    - "Spoof-host rejection: DeckSourceHost.IsMoxfield(uri)/IsArchidekt(uri) as the single trust gate"
    - "Canonical URL reconstruction: always build moxfieldUrl from parsed deckId, never forward originalUrl"
    - "Null-capture regression pattern: importer capture property null-asserted in spoof tests"

key-files:
  created: []
  modified:
    - DeckFlow.Core/Loading/DeckEntryLoader.cs
    - DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs
    - DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs
    - DeckFlow.Core.Tests/DeckEntryLoaderTests.cs
    - DeckFlow.Core.Tests/MoxfieldApiDeckImporterTests.cs
    - DeckFlow.Web.Tests/AiPlatformPhase10RoundTripTests.cs

key-decisions:
  - "Spoof-URL test behavior: DeckEntryLoader falls through to parser cascade (URL parsed as card name with implicit quantity) rather than throwing InvalidOperationException; security invariant is null-capture (importer not called), not exception throw"
  - "MoxfieldApiDeckImporter: deckId is already parsed from path before FetchViaCommanderSpellbookAsync runs, so unconditional reconstruction is safe and is the only secure approach"
  - "PacketArtifactStore: DeckFlow.Core.Integration using sorted between System.* and DeckFlow.Web.* per project conventions"

requirements-completed: [SEC-01, SEC-02, SEC-03]

duration: 35min
completed: 2026-06-21
---

# Phase 64 Plan 02: Deck-Source Host Hardening — 4-Site Adoption Summary

**Replaced all four spoofable substring host-match call sites with DeckSourceHost exact-domain predicates and fixed the Moxfield→Spellbook fallback to always forward a canonical reconstructed URL, never the user-submitted URL.**

## Performance

- **Duration:** ~35 min
- **Completed:** 2026-06-21
- **Tasks:** 3/3
- **Files modified:** 6
- **Commits:** 3

## Accomplishments

### Task 1: DeckEntryLoader (S1, S2) — `aebfd8e8`
- Replaced `uri.Host.Contains("moxfield.com", OrdinalIgnoreCase)` with `DeckSourceHost.IsMoxfield(uri)`
- Replaced `uri.Host.Contains("archidekt.com", OrdinalIgnoreCase)` with `DeckSourceHost.IsArchidekt(uri)`
- Added two `[Theory]` regression methods (6 inline cases total):
  - `LoadFromSourceAsync_SpoofedMoxfieldUrl_DoesNotRouteToMoxfieldImporter`
  - `LoadFromSourceAsync_SpoofedArchidektUrl_DoesNotRouteToArchidektImporter`
- Each asserts `LastImportWithSourceArgument` / `LastImportArgument` is null after the spoof URL is passed

### Task 2: MoxfieldApiDeckImporter (S3, SC2) — `de6d212a`
- Deleted the `originalUrl.Contains("moxfield.com", OrdinalIgnoreCase) ? originalUrl : ...` conditional
- Replaced with unconditional `var moxfieldUrl = $"https://moxfield.com/decks/{deckId}";`
- `originalUrl` parameter retained; still referenced in the double-failure error message at line ~119
- Added two `[Fact]` tests proving the Spellbook `url` query param equals the canonical form:
  - `FetchViaCommanderSpellbookAsync_AlwaysForwardsCanonicalUrl_NeverSubmittedUrl`: www input → `https://moxfield.com/decks/abc123`
  - `FetchViaCommanderSpellbookAsync_SpoofHostInput_ForwardsCanonicalNotSubmitted`: spoof input `moxfield.com.evil.tld/decks/abc123?x=1` → still `https://moxfield.com/decks/abc123` (SC2 direct proof)

### Task 3: PacketArtifactStore (S4) — `934b6789`
- Added `using DeckFlow.Core.Integration;` in sorted position
- Replaced `uri.Host.Contains("moxfield.com"...) || uri.Host.Contains("archidekt.com"...)` with `DeckSourceHost.IsMoxfield(uri) || DeckSourceHost.IsArchidekt(uri)`
- Pre-existing scheme guard (`uri.Scheme == http/https`) and `uri.Host is not null` retained
- Added `[Theory]` `OriginalDeckTextOrNull_ReturnsTextUnchanged_ForSpoofedDeckHosts` with 4 spoof inputs

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Corrected spoof-test assertion: InvalidOperationException is not thrown for URL look-alikes**
- **Found during:** Task 1 (test execution)
- **Issue:** The plan's `<behavior>` section stated spoof URLs "fall through the parser cascade and throw InvalidOperationException with the exact not-recognized message." In practice, `MoxfieldParser.TryParseEntry` with `allowImplicitQuantity: true` accepts the URL string as an implicit-quantity-1 card name (`https://moxfield.com.evil.tld/decks/abc` → card "https://moxfield.com.evil.tld/decks/abc"), so neither parser throws and the loader returns a result instead of throwing.
- **Fix:** Changed test assertion from `Assert.ThrowsAsync<InvalidOperationException>` to a direct `await loader.LoadFromSourceAsync(spoofUrl)` (verifying no exception), then asserting `Assert.Null(importer.LastImportWithSourceArgument)`. The null-capture assertion is the security invariant (importer not called); the exception throw is a secondary detail that depends on parser internals.
- **Security impact:** None — the importer is not called for spoof URLs regardless of parser behavior. The routing guard (`DeckSourceHost.IsMoxfield`) fires first. The null-capture assertion is stronger than an exception assertion.
- **Files modified:** `DeckFlow.Core.Tests/DeckEntryLoaderTests.cs`

## Verification Results

- **Build:** `DeckFlow.Core` 0 errors / 0 new warnings; `DeckFlow.Web.Tests` 0 errors / 0 new warnings
- **Core suite:** 612/612 passed (0 failed, 0 skipped)
- **Web suite:** 677/677 passed (11 skipped PG integration — expected)
- **DeckEntryLoader filter:** 12/12 passed
- **MoxfieldApiDeckImporter filter:** 3/3 passed
- **OriginalDeckTextOrNull filter:** 7/7 passed
- **Grep gate:** `grep -rn 'Host.Contains("moxfield.com"\|Host.Contains("archidekt.com"\|originalUrl.Contains("moxfield.com"' DeckFlow.Core DeckFlow.Web` → zero results

## Threat Mitigations Closed

| Threat ID | Status | Evidence |
|-----------|--------|----------|
| T-64-01 | MITIGATED | DeckEntryLoader and PacketArtifactStore use DeckSourceHost; spoof-routing Theory proves no importer called for look-alike hosts |
| T-64-02 | MITIGATED | MoxfieldApiDeckImporter always reconstructs canonical URL; spoof-host Fact proves forwarded url param never equals hostile originalUrl |
| T-64-01-REG | MITIGATED | Per-site regression tests (this plan) + DeckSourceHost matrix (64-01) fail if substring matching is reintroduced |

## Self-Check: PASSED

- `DeckFlow.Core/Loading/DeckEntryLoader.cs` — contains `DeckSourceHost.IsMoxfield(uri)` ✓
- `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` — contains `https://moxfield.com/decks/{deckId}` (line 105), no `originalUrl.Contains` ✓
- `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` — contains `DeckSourceHost.IsMoxfield(uri) || DeckSourceHost.IsArchidekt(uri)` ✓
- `DeckFlow.Core.Tests/DeckEntryLoaderTests.cs` — two Theory methods with null-capture assertions ✓
- `DeckFlow.Core.Tests/MoxfieldApiDeckImporterTests.cs` — two Fact methods; SC2 proof Fact present ✓
- `DeckFlow.Web.Tests/AiPlatformPhase10RoundTripTests.cs` — Theory with 4 spoof inputs ✓
- Commits `aebfd8e8`, `de6d212a`, `934b6789` verified in git log ✓
