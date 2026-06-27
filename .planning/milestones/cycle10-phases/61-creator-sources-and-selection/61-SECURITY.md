---
phase: 61
slug: creator-sources-and-selection
status: verified
threats_open: 0
asvs_level: 1
created: 2026-06-21
---

# Phase 61 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
> Creator Sources & Selection (SRC-01/02, HSEL-01/02/03). Register authored at plan time
> across the four 61-0x PLANs; all mitigations verified in the implemented code.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Operator → local stores | Operator-entered creator display name / channel ref / skip reason persisted in content-kb.db | Non-sensitive text (no secrets) |
| Operator → YouTube browse | Saved/selected channel ref drives the existing channel/playlist browse | Channel URL/handle/id (same as today's pasted URL) |
| Studio page → Serilog log | Catch blocks on the new pages + the browse skip-load | Generic operator-safe copy only; never ex.Message / DB path (D-07) |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-61-01 | Tampering | CreatorSourceStore / SkippedVideoStore SQL | mitigate | All SQL parameterized via Dapper (no string concat — verified); idempotent `EnsureSchemaAsync` with schema guards | closed |
| T-61-02 | Information disclosure | Both new stores | mitigate | Column sets are non-sensitive by construction (display name / channel ref / video id / reason / timestamps); no secrets persisted | closed |
| T-61-03 | Integrity (HSEL-02 invariant) | SkippedVideoStore | mitigate | Writes ONLY `skipped_videos` (verified — no other table referenced); never blocked_videos or any artifact. Core test pre-seeds blocked_videos + artifact sentinel and asserts both byte-identical after skip | closed |
| T-61-04 | Tampering | Harvest creator dropdown | mitigate | Dropdown selection sets `_channelInput`, which flows through the SAME existing `BrowseChannelAsync` validation path as a pasted URL — no new bypass | closed |
| T-61-05 | Denial of service | Creator list size | accept | Local single-operator tool; an oversized self-curated list is operator-self-inflicted and out of scope | closed |
| T-61-06 | Integrity (skip ≠ block) | Harvest Skip action | mitigate | `SkipVideoAsync` calls only `SkippedStore.AddSkipAsync` (verified — no Block/maintenance/Delete call); bUnit asserts the Block path/store is untouched by a Skip | closed |
| T-61-07 | Tampering | Show-all toggle | mitigate | Existing per-status row guards unchanged (Block disabled on already-Blocked); Show-all only reveals rows, grants no new action; canonical visible projection still gates harvesting | closed |
| T-61-08 | Integrity | Skipped page un-skip | mitigate | `UnskipAsync` calls only `RemoveSkipAsync` (deletes the skipped_videos row only); never artifacts, blocked_videos, or content_site_index | closed |
| T-61-09 | Spoofing / EoP | Skipped / Creators pages | accept | No new surface — same local single-operator Studio access model as the existing Blocked page | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| R-61-01 | T-61-05 | Creator-list DoS is operator-self-inflicted on a local-only tool; no remote attacker surface | Chris Lunt | 2026-06-21 |
| R-61-02 | T-61-09 | Studio is a local single-operator console; no new auth surface beyond the existing pages | Chris Lunt | 2026-06-21 |

*Accepted risks do not resurface in future audit runs.*

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-21 | 9 | 9 | 0 | Claude (inline, from PLAN threat registers + code verification; Codex code-review found+fixed the D-07 skip-load db-path leak) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter
