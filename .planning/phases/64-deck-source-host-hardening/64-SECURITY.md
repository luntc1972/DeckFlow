---
phase: 64
slug: deck-source-host-hardening
status: secured
asvs_level: 1
block_on: high
audited: 2026-06-21
open_high_threats: 0
---

# Phase 64 — Security Audit (Deck-Source Host Hardening)

> Retroactive verification that the PLAN.md threat-model mitigations exist in the shipped code.
> ASVS L1, block on `high`. Audited 2026-06-21 (inline main-thread audit — subagent dispatch was 529-overloaded; same Claude rigor, evidence below is file:line + test verified).

## Verdict

**status: secured** — all 3 high-severity threats MITIGATED, **0 open**.

## Threat-by-Threat

| Threat | STRIDE | Status | Evidence (file:line + test) | Residual risk |
|--------|--------|--------|------------------------------|----------------|
| **T-64-01** Look-alike host impersonates Moxfield/Archidekt via substring match (`moxfield.com.evil.tld`, `evilmoxfield.com`, `moxfield.com@evil.tld`) | Spoofing | **MITIGATED** | Predicate `DeckSourceHost.cs` (`IsApprovedHost`): `string.Equals(host, apex, OrdinalIgnoreCase) || host.EndsWith("." + apex, OrdinalIgnoreCase)` — no substring, no `TrimEnd('.')`. Adopted at all 4 sites: `DeckEntryLoader.cs:121` (IsMoxfield), `:127` (IsArchidekt); `PacketArtifactStore.cs:100` (`IsMoxfield‖IsArchidekt`, behind http/https scheme guard); `MoxfieldApiDeckImporter` host routing via loader. `Uri.Host` resolves `moxfield.com@evil.tld` → `evil.tld` (userinfo stripped) → rejected. Trailing-dot FQDN `moxfield.com.` rejected (no trim). **Grep gate clean:** `grep -rnE 'Host\.Contains\("(moxfield\|archidekt)\.com"\|originalUrl\.Contains\("moxfield\.com"' DeckFlow.Core DeckFlow.Web` → 0 hits. Tests: `DeckSourceHostTests.cs:18-21,32-35` (spoof rows → false, both platforms); `DeckEntryLoaderTests.cs:124,143` (spoof URL → importer NOT called, both platforms). | Approved-subdomain matching intentionally accepts any `*.moxfield.com` / `*.archidekt.com` host (incl. multi-level). **Accepted** — only the real registrable domain can serve those subdomains. |
| **T-64-02** User-submitted URL forwarded downstream to Commander Spellbook (SSRF / request-forgery via attacker-controlled URL) | Elevation of Privilege | **MITIGATED** | `MoxfieldApiDeckImporter.cs:105` — `var moxfieldUrl = $"https://moxfield.com/decks/{deckId}";` (unconditional canonical reconstruction from the already-parsed `deckId`); `:108` forwards only `moxfieldUrl` as the `url` query param. The `originalUrl.Contains(...)` conditional is deleted; `originalUrl` no longer reaches any outbound request (param appears only in the method signature line 103 — see note below). Test: `MoxfieldApiDeckImporterTests.cs:18` (www input → forwarded url == canonical) + `:64` `FetchViaCommanderSpellbookAsync_SpoofHostInput_ForwardsCanonicalNotSubmitted` (hostile `moxfield.com.evil.tld/decks/abc123?x=1` → forwarded url == exactly `https://moxfield.com/decks/abc123`). `deckId` is the validated path segment from `MoxfieldApiUrl.TryGetDeckId` (no `/`,`?`,`#` injection into the canonical URL). | None. |
| **T-64-01-REG** A future edit reverts any of S1/S2/S3/S4 to substring/`Contains` host trust | Tampering | **MITIGATED** | `DeckSourceHostTests.cs` 16-case `[Theory]` accept/reject matrix is the standalone guard — look-alike rows (`evilmoxfield.com`, `moxfield.com.evil.tld`, etc.) assert `false`; a `.Contains` revert would make them `true` and fail the suite. Reinforced per-site: `DeckEntryLoaderTests.cs:124,143`, `MoxfieldApiDeckImporterTests.cs:18,64`, `AiPlatformPhase10RoundTripTests.cs:501` (`OriginalDeckTextOrNull_ReturnsTextUnchanged_ForSpoofedDeckHosts`, 4 spoof inputs both platforms). | None. |

## Adjacent-Threat Sanity Pass

- **`DeckEntryLoader.LoadAsync` `DeckInputKind.PublicUrl` path** routes by explicit `DeckPlatform` enum, not by URL host content, so it does not consult `DeckSourceHost`. The phase verifier (64-VERIFICATION.md) flagged this; **accepted non-issue** — the platform is chosen by the caller, not inferred from an attacker-controlled host string, so the spoof vector does not apply.
- No other deck-source host-trust-by-string path found in `DeckFlow.Core` / `DeckFlow.Web` (grep gate covers both projects).

## Non-Security Note (informational, not a finding)

- `MoxfieldApiDeckImporter.FetchViaCommanderSpellbookAsync(string originalUrl, ...)` — `originalUrl` is now an unused parameter (the double-failure error message at `:117` uses `deckId`, not `originalUrl`). Harmless dead parameter; could be removed in a future cleanup. No security impact (the whole point is that the submitted URL is never used downstream).

## Cross-Context

- Phase 65 (DATA — prod content artifact reconcile) handles a separate prod data-consistency gap; out of scope for this audit.

**Build/test reality:** build 0 errors; executor reported Core 612/612, Web 677/677 (+11 PG-skip). Codex code review (gpt-5.4, 2026-06-21) returned 0 HIGH/MED/LOW on this diff.
