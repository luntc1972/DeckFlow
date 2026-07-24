# Requirements: Cycle 20 — Personal Tools

**Defined:** 2026-07-24
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip, without the user reformatting anything.
**Design spec:** `docs/research/personal-tools-admin-reframe-design.md` (approved 2026-07-24) is authoritative for this milestone.

## Cycle 20 Requirements

### Cycle 17 Code Port

- [ ] **PORT-01**: Cycle 17's Core engine (Phases 94–98 — profile records and store, measured extraction, stated-rules extraction, profile fusion, card-grounding guard) is present on `feat/personal-tools` and the solution builds with no new errors or warnings.
- [ ] **PORT-02**: Creator-style Web services (`Services/CreatorStyle/*`), the creator-style seed loader, and their DI registrations are ported and resolve at application startup.
- [ ] **PORT-03**: Cycle 17's shared-infrastructure refactors are re-derived against current `main` — neutral `ScryfallCollectionResolver`, `ScryfallLimits.CollectionBatchSize`, shared `CachedNameResolution`, and a dedicated `archidekt` resilience pipeline — with the manabase and Scryfall test suites still green.
- [ ] **PORT-04**: Ported Core and Web test suites pass, with Phase 100's public-surface tests (feature-flag lockstep, `ToolRegistry` counts, route-gate coverage, sitemap assertions) removed rather than carried forward.

### Admin Personal-Tools Surface

- [ ] **PTOOL-01**: Creator-style is reachable only at `/Admin/CreatorStyle`, and an unauthenticated request is refused by the existing BasicAuth branch.
- [ ] **PTOOL-02**: Phase 100's public plumbing is absent — no `tool.creator-style.enabled` flag, no `ToolRegistry` entry, no sitemap or `SeoPaths` entry, no public help topic, and no `PacketSessionCache` bypass-list entry.
- [ ] **PTOOL-03**: The `/Admin` landing page lists a personal-tools section linking both personal tools.
- [ ] **PTOOL-04**: Deck Tendencies is reachable at `/Admin/CreatorProfile` and is linked from that same section.

### Real Data

- [ ] **PSEED-01**: A hand-authored stated-rules seed is committed at `content-kb/seed/creator-stated-rules.json`, with every rule marked `Provenance = "hand-authored"` so a later re-distill supersedes rather than duplicates it.
- [ ] **PSEED-02**: The `creator-style-import-stated` CLI command loads that seed into `content_stated_rules` and the rules read back intact.
- [ ] **PSEED-03**: `fuse-profile` produces `FusedTarget[]` plus a conflict ledger that reproduces the P89/P90 prototype verdicts, including the board-wipe "agreement, not hypocrisy" result.
- [ ] **PSEED-04**: The operator run exports populated `creator-style-profiles.json` and `creator-deck-cache.json`, and both are committed to the repository.
- [ ] **PSEED-05**: `/Admin/CreatorStyle` renders a real critique of a submitted deck against the seeded profile, not the empty-store state.

## Out of Scope

| Feature | Reason |
|---------|--------|
| Public launch of creator-style | The 2026-07-19 legal review turned creator-crawl off as a public feature. No flag, no tool tile, no sitemap entry, no help topic. This is the defining constraint of the milestone, not a deferral. |
| Rebasing `plan/cycle-17-creator-style` | 777 commits behind `main` with a −57,732-line planning-doc diff that would conflict against Cycle 18/19 archival. Code is ported forward; the branch is preserved untouched at origin as the historical record. |
| Installing the distill toolchain or re-distilling the 85-video corpus | `yt-dlp`/`ffmpeg`/`whisper` are absent from PATH; installing them is a new system dependency plus an unbounded transcription run, when the needed rules already exist in `docs/research/p89-p90-prototype-snail.md`. |
| Postgres migration of the creator-style stores | They bind to the local `content-kb.db`; production hydrates from git-shipped seeds, sufficient for a single-operator tool. |
| Pet-card detection | Spec superseded pending the EDHREC integration under consideration for a later cycle. |
| Crawling from production | The crawl runs locally only and Render reads seeds, keeping the 512 MB tier and request timeouts out of scope. |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| PORT-01 | 112 | Pending |
| PORT-02 | 112 | Pending |
| PORT-03 | 113 | Pending |
| PORT-04 | 114 | Pending |
| PTOOL-01 | 114 | Pending |
| PTOOL-02 | 114 | Pending |
| PTOOL-03 | 114 | Pending |
| PTOOL-04 | 114 | Pending |
| PSEED-01 | 115 | Pending |
| PSEED-02 | 115 | Pending |
| PSEED-03 | 115 | Pending |
| PSEED-04 | 115 | Pending |
| PSEED-05 | 115 | Pending |

**Coverage:**
- Cycle 20 requirements: 13 total
- Mapped to phases: 13 (Phases 112-115)
- Unmapped: 0

---
*Requirements defined: 2026-07-24*
