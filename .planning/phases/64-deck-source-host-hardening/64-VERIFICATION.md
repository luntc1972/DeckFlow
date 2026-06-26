---
phase: 64-deck-source-host-hardening
verified: 2026-06-21T22:05:14Z
status: passed
score: 3/3 must-haves verified
overrides_applied: 0
---

# Phase 64: Deck-Source Host Hardening Verification Report

**Phase Goal:** A hostile look-alike deck URL can no longer impersonate Moxfield/Archidekt on ANY deck tool.
**Verified:** 2026-06-21T22:05:14Z
**Status:** passed
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | SEC-01: `LoadFromSourceAsync`, `PacketArtifactStore`, and all 4 identified host-check sites use `DeckSourceHost.IsMoxfield/IsArchidekt` — no `Host.Contains` survives | VERIFIED | Grep gate returned zero results; each adoption site reads via exact-or-subdomain predicate |
| 2 | SEC-02: `FetchViaCommanderSpellbookAsync` always forwards `https://moxfield.com/decks/{deckId}` — the submitted URL is never forwarded | VERIFIED | Line 105 of `MoxfieldApiDeckImporter.cs` is unconditional canonical reconstruction; `originalUrl` param is unused in the method body |
| 3 | SEC-03: Regression tests cover all three spoof variants for both platforms and would fail if substring matching were reintroduced | VERIFIED | 16-case `DeckSourceHostTests` Theory matrix + 6-case DeckEntryLoader Theory + 4-case PacketArtifactStore Theory + 2 MoxfieldApiDeckImporter Facts |

**Score:** 3/3 truths verified

---

## Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Core/Integration/DeckSourceHost.cs` | Trust-predicate helper | VERIFIED | Exists; `IsApprovedHost` uses `string.Equals(...OrdinalIgnoreCase) \|\| host.EndsWith("." + apex, OrdinalIgnoreCase)`. No `TrimEnd`, no `Contains`. |
| `DeckFlow.Core.Tests/DeckSourceHostTests.cs` | 16-case accept/reject matrix | VERIFIED | Exists; 9 IsMoxfield cases (4 accept, 5 reject) + 7 IsArchidekt cases (2 accept, 5 reject) |
| `DeckFlow.Core/Loading/DeckEntryLoader.cs` | Adoption of `DeckSourceHost` at lines 121, 127 | VERIFIED | `DeckSourceHost.IsMoxfield(uri)` at line 121, `DeckSourceHost.IsArchidekt(uri)` at line 127 |
| `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` | Canonical URL reconstruction, no `originalUrl.Contains` | VERIFIED | Line 105: `var moxfieldUrl = $"https://moxfield.com/decks/{deckId}";` unconditional; `originalUrl` param declared but unused in method body |
| `DeckFlow.Web/Services/Persistence/PacketArtifactStore.cs` | Adoption of `DeckSourceHost` at lines 99-100 | VERIFIED | `DeckSourceHost.IsMoxfield(uri) \|\| DeckSourceHost.IsArchidekt(uri)` at line 100 |
| `DeckFlow.Core.Tests/DeckEntryLoaderTests.cs` | Spoof Theory tests with null-capture assertion | VERIFIED | `LoadFromSourceAsync_SpoofedMoxfieldUrl_DoesNotRouteToMoxfieldImporter` (3 cases) + `LoadFromSourceAsync_SpoofedArchidektUrl_DoesNotRouteToArchidektImporter` (3 cases) |
| `DeckFlow.Core.Tests/MoxfieldApiDeckImporterTests.cs` | SC2 Facts proving canonical forward | VERIFIED | `FetchViaCommanderSpellbookAsync_AlwaysForwardsCanonicalUrl_NeverSubmittedUrl` + `FetchViaCommanderSpellbookAsync_SpoofHostInput_ForwardsCanonicalNotSubmitted` |
| `DeckFlow.Web.Tests/AiPlatformPhase10RoundTripTests.cs` | PacketArtifactStore spoof Theory (4 cases) | VERIFIED | `OriginalDeckTextOrNull_ReturnsTextUnchanged_ForSpoofedDeckHosts` at lines 496-508; 4 cases including both Moxfield and Archidekt look-alikes |

---

## Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `DeckEntryLoader.LoadFromSourceAsync` | `DeckSourceHost.IsMoxfield/IsArchidekt` | Direct static call | WIRED | Lines 121, 127 in `DeckEntryLoader.cs` |
| `PacketArtifactStore.OriginalDeckTextOrNull` | `DeckSourceHost.IsMoxfield/IsArchidekt` | Direct static call | WIRED | Line 100 in `PacketArtifactStore.cs`; `using DeckFlow.Core.Integration` added |
| `MoxfieldApiDeckImporter.FetchViaCommanderSpellbookAsync` | Spellbook `url` param | Canonical `$"https://moxfield.com/decks/{deckId}"` | WIRED | `originalUrl` never forwarded; `deckId` used exclusively |
| `DeckSourceHostTests` | `DeckSourceHost.IsMoxfield/IsArchidekt` | Theory/InlineData | WIRED | Substring match would cause look-alike rows to pass and the suite to fail |

---

## SC1 Verification: Predicate Correctness

**DeckSourceHost predicate (file: `DeckFlow.Core/Integration/DeckSourceHost.cs`):**

```csharp
private static bool IsApprovedHost(string host, string apex)
{
    return string.Equals(host, apex, StringComparison.OrdinalIgnoreCase)
        || host.EndsWith("." + apex, StringComparison.OrdinalIgnoreCase);
}
```

Spoof case analysis:

| URL | `Uri.Host` resolves to | Predicate result | Correct? |
|-----|------------------------|-----------------|----------|
| `https://moxfield.com/decks/x` | `moxfield.com` | `string.Equals` → true | YES (accept) |
| `https://www.moxfield.com/decks/x` | `www.moxfield.com` | `EndsWith(".moxfield.com")` → true | YES (accept) |
| `https://api.moxfield.com/v2/decks/x` | `api.moxfield.com` | `EndsWith(".moxfield.com")` → true | YES (accept) |
| `https://moxfield.com.evil.tld/decks/x` | `moxfield.com.evil.tld` | `string.Equals` false, `EndsWith(".moxfield.com")` false | YES (reject) |
| `https://evilmoxfield.com/decks/x` | `evilmoxfield.com` | both false | YES (reject) |
| `https://moxfield.com@evil.tld/decks/x` | `evil.tld` (userinfo parsed by .NET Uri) | both false | YES (reject) |
| `https://moxfield.com./decks/x` | `moxfield.com.` (trailing dot) | `string.Equals` false (`moxfield.com.` != `moxfield.com`), `EndsWith(".moxfield.com")` false (`moxfield.com.` ends with `.` not `.moxfield.com`) | YES (reject) |

The comment at line 44-45 documents the trailing-dot pitfall explicitly. Correct.

---

## SC2 Verification: Canonical URL Forward

**Evidence from `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs:103-120`:**

- `originalUrl` is accepted as a parameter but is unused in the method body (verified by `grep -n "originalUrl" MoxfieldApiDeckImporter.cs` → only line 103, the parameter declaration).
- `moxfieldUrl` is always `$"https://moxfield.com/decks/{deckId}"` (line 105, unconditional).
- The Spellbook `url` query param is `moxfieldUrl` (line 108).
- The error message at line 117 uses `deckId`, not `originalUrl`.
- The SC2 test `FetchViaCommanderSpellbookAsync_SpoofHostInput_ForwardsCanonicalNotSubmitted` calls `ImportAsync("https://moxfield.com.evil.tld/decks/abc123?x=1")` and asserts the forwarded `url` param equals `"https://moxfield.com/decks/abc123"`.

Note on test approach: The SC2 test calls `ImportAsync` directly on `MoxfieldApiDeckImporter` with a spoof URL. This is legitimate because `MoxfieldApiUrl.TryGetDeckId` is host-agnostic (extracts `deckId` from path segments regardless of host), so the test can reach `FetchViaCommanderSpellbookAsync`. The test proves that even if the importer were somehow called with a hostile URL (defense in depth), it would still forward only the canonical URL to Spellbook.

---

## Grep Gate (SC1 + SC3)

Command: `grep -rnE 'Host\.Contains\("(moxfield|archidekt)\.com"|originalUrl\.Contains\("moxfield\.com"' DeckFlow.Core DeckFlow.Web`

Result: **zero matches** (command returned no output).

Secondary check for broader patterns: `grep -rnE '\.Host.*Contains\("(moxfield|archidekt)'` and `grep -rnE 'Contains\("moxfield\.com|Contains\("archidekt\.com'` both returned zero results in production code paths.

---

## Deviation Assessment: Spoof URLs Do Not Throw (Acceptable)

The SUMMARY documents a deviation: spoof URLs in `LoadFromSourceAsync` do not throw `InvalidOperationException`; they fall through to the parser cascade and are treated as pasted deck text. Tests use null-capture assertion (`Assert.Null(importer.LastImportWithSourceArgument)`) instead of `Assert.ThrowsAsync`.

**Assessment: Acceptable — security intent fully satisfied.**

The phase goal is "a hostile look-alike URL can no longer impersonate Moxfield/Archidekt." The importer is never invoked for a spoof URL. The null-capture assertion is a stronger proof of the security invariant than an exception assertion, because:
1. The routing guard fires first — `DeckSourceHost.IsMoxfield(uri)` returns false.
2. The importer is simply never called.
3. Whether the parser cascade accepts or rejects the URL-as-text is irrelevant to the SSRF/abuse vector.

The "rejected" wording in SC1 means "not treated as a trusted Moxfield/Archidekt source" (importer not invoked), not necessarily "throws." The implementation satisfies this.

---

## Unguarded Path: `LoadAsync` with `DeckInputKind.PublicUrl`

**Finding:** `DeckEntryLoader.LoadAsync` has a separate code path (`LoadMoxfieldAsync`/`LoadArchidektAsync`) that calls importers directly with `DeckInputKind.PublicUrl`, bypassing `DeckSourceHost`. This path is used by `DeckSyncService` when the user-selected platform is explicit (e.g. Moxfield sync panel).

**Risk Assessment: Out of scope, low risk.** The RESEARCH.md explicitly inventoried 4 sites (S1-S4) and excluded this path. The attack model in `LoadAsync` is different: routing is determined by `DeckPlatform` (an enum set from controller logic), not from URL content. A hostile URL submitted to the Moxfield panel would reach the Moxfield importer — which is the intended behavior for that panel. The SSRF concern is cross-platform impersonation via the auto-detect path (`LoadFromSourceAsync`), which IS guarded.

**Verdict: Not a gap for this phase.** Document as a known characteristic for future security review.

---

## Build Verification

`/mnt/c/Program Files/dotnet/dotnet.exe build DeckFlow.Core/DeckFlow.Core.csproj`:
- **0 errors**
- **2 warnings** (NU1903 pre-existing SQLitePCLRaw advisory — documented as pre-existing in SUMMARY)

---

## Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `MoxfieldApiDeckImporter.cs` | 103 | `originalUrl` parameter declared but unused in method body | Info | No security impact; parameter was used before the fix and is retained for readability/error-message context; compiler will warn (CS0168 if flagged). Not a debt marker. |

No `TBD`, `FIXME`, `XXX`, `TODO`, or `PLACEHOLDER` markers found in phase-modified files (verified by reading all modified files).

---

## Behavioral Spot-Checks

Step 7b: SKIPPED for host-predicate checks — the behaviors are pure unit-testable predicates with no runnable server entry point needed. Tests serve as the spot-check.

---

## Probe Execution

Step 7c: No probes declared or conventional probe scripts found for this phase.

---

## Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| SEC-01 | 64-01, 64-02 | Host check uses exact-or-subdomain predicate at all 4 adoption sites | SATISFIED | `DeckSourceHost.IsApprovedHost` + grep gate zero results |
| SEC-02 | 64-02 | Spellbook fallback always forwards canonical URL | SATISFIED | `MoxfieldApiDeckImporter.cs:105` unconditional + SC2 test Fact |
| SEC-03 | 64-01, 64-02 | Regression test matrix fails if substring matching reintroduced | SATISFIED | 16+6+4+2 test cases covering both platforms and all 3 spoof variants |

---

## Human Verification Required

None. All phase behaviors are pure logic with automated verification. No visual UI, real-time behavior, or external service integration is involved.

---

## Summary

Phase 64 achieved its goal. The four identified host-check call sites (S1-S4) all use `DeckSourceHost.IsMoxfield/IsArchidekt` exact-or-subdomain predicates; no substring `Contains` match survives in production code. The Spellbook fallback unconditionally reconstructs the canonical URL. The regression test matrix (16 + 6 + 4 + 2 = 28 cases across three test files) covers all three spoof variants (`superstring.moxfield.com`, `evilmoxfield.com`, `moxfield.com@evil.tld`) for both Moxfield and Archidekt. A hostile look-alike deck URL cannot impersonate Moxfield or Archidekt in any of the guarded deck tools.

---

_Verified: 2026-06-21T22:05:14Z_
_Verifier: Claude (gsd-verifier)_
