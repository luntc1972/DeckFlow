# Requirements Archive: Cycle 8 — Hardening & Backlog Burn-down

**Milestone:** Cycle 8 — Hardening & Backlog Burn-down
**CalVer release:** `2026.06.4`
**Defined:** 2026-06-17 · **Shipped:** 2026-06-17
**Theme:** Close accumulated debt (v1.7 deferred operator-UAT, architecture-review backlog, feature debt) before the next feature cycle. No net-new user features.
**Outcome:** 8/8 requirements satisfied (FEAT-01 PASS-WITH-NOTES). Closed as tech-debt (no milestone audit; every phase individually verified).

## Requirements

### Deferred operator-UAT closure

- [x] **HARD-01**: Deferred non-prod operator-UAT smokes pass and are recorded — Studio runtime render (P41), `/Admin/Harvest` lazy-grid no-scroll-jump (P44), re-distill E2E + harvest-cap-persist + cancel-on-circuit-drop (P45), Review queue + Publish git commit (P46). *(SATISFIED — Phase 51; 2 P45 sub-smokes + real Publish-commit waived-with-reason.)*
- [x] **HARD-02**: Direct prod publish path exercised live end-to-end (P47) — SCP artifacts to Render `/data` + content-columns-only Postgres upsert, confirming `is_visible`/`is_evergreen` on pre-existing rows preserved. *(SATISFIED — Phase 52; live run, not waived. 86 pre-existing rows preserved, 8 inserted.)*
- [x] **HARD-03**: Postgres-gated test suite passes (`DECKFLOW_POSTGRES_TESTS=1`), confirming Phase 49 Dapper type-handler parity on prod Postgres. *(SATISFIED — Phase 51; found+fixed F-51-PG-01, 19/19 PG.)*

### Architecture-review backlog (B–K, from 39-AUDIT)

- [x] **ARCH-01**: `Services/` foldering and dual-dialect cleanup completed, or each finding explicitly descoped with a recorded reason. Pure structure/refactor — behavior unchanged, guarded by existing tests. *(COMPLETE — Phase 53; Program.cs DI extract + Services foldering + Feedback dialect-leak removal. Full dialect collapse deferred with reason.)*
- [x] **ARCH-02**: Remaining god-class / SRP-split findings re-scoped against post-Dapper code and the still-relevant ones addressed; obsolete ones closed. *(COMPLETE — Phase 53; CategoryKnowledgeRepository split + deck-stat classifiers → Core. Finding C dropped.)*

### Feature debt

- [x] **FEAT-01**: Gemini paste-limit path unblocked (`DECKFLOW_GEMINI_ENABLED`) and verified — artifacts generate and paste within Gemini's limits across analysis/comparison/meta-gap/primer. *(SATISFIED, PASS-WITH-NOTES — Phase 54; all 4 workflows <30k chars; flag stays default-off; operator live-paste carry-forward.)*
- [x] **FEAT-02**: `SpellbookCombo` ranking fields (`manaValueNeeded`, `popularity`, `uses`) captured by the parser and used to priority-rank combos in the Deck Primer (PRM-08). *(SATISFIED — Phase 54; popularity DESC / manaValueNeeded ASC, tolerant parse.)*

### Ops

- [x] **OPS-01**: `v1.7` merged to `main` and tagged with its CalVer; Cycle 8 work branches off the v1.7-inclusive `main`. *(SATISFIED — Phase 51; Render deploys from main, tree-identical to v1.7 squash.)*

## Out of Scope (deferred to Cycle 9+)

- **Studio / content-pipeline expansion:** more creator sources, better distill quality, automate harvest→distill→review→publish.
- **SEO / growth / ops:** Search Console + Bing submission + backlinks, analytics + monitoring, performance / Core Web Vitals, on-site SEO + structured-data expansion.
- **SEED-001** — KB add/remove + publish-tracking (planted for Cycle 9).
