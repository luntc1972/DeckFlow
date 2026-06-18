# Milestone Archive: Cycle 8 — Hardening & Backlog Burn-down

**Shipped:** 2026-06-17
**CalVer release:** `2026.06.4`
**Phases:** 51–54
**Theme:** Close accumulated debt (v1.7 deferred operator-UAT, architecture-review backlog, feature debt) before the next feature cycle. No net-new user features.

## Stats

- **Phases:** 4 (51–54)
- **Plans:** 11 (51: 4, 52: 1, 53: 4, 54: 2)
- **Commits:** 46 (range `39e74d55`..`8dbdedb`, all 2026-06-17)
- **Diff:** 184 files changed, +7,627 / −1,656
- **Audit:** none run — closed as tech-debt; every phase individually verified (P51 PASS, P52 PASS, P53 PASS 8/8 + SECURED 4/4, P54 PASS-with-notes)

## Key Accomplishments

1. **Verified the shipped v1.7 Studio/publish pipeline end-to-end** — non-prod operator-UAT smokes (P51: Studio runtime render, `/Admin/Harvest` no-jump grid, re-distill/cap/cancel, Review/Publish git+LF) plus a **live prod publish run** (P52): DirectPush SCP'd artifacts to the Render `/data` disk and ran a content-columns-only Postgres upsert, proving `is_visible`/`is_evergreen`/`is_hidden`/`approval_status` on all 86 pre-existing rows were preserved (identical fingerprints) while 8 new rows landed in default `pending`/not-visible state.
2. **Fixed F-51-PG-01** — `AddDeckIdsAsync` compared a TEXT `last_checked_utc` against a `timestamptz`-bound param → Npgsql `42883` on Postgres (SQLite tolerated it). Dialect-guarded `::timestamptz` cast (PG-only, no migration); PG parity 19/19, SQLite 20/20. Surfaced by the `DECKFLOW_POSTGRES_TESTS=1` gate (HARD-03).
3. **Burned down the Phase 39 architecture backlog (ARCH-01/02)** — split the `CategoryKnowledgeRepository` god-file (1272→274 LOC facade + Schema/DeckQueue/CardCategory collaborators), extracted `Program.cs` DI into `AddDeckFlowXxx()` extensions (553→354 LOC) + finished `Services/` concern-foldering (Scryfall/Persistence/Content), relocated the deck-stat classifiers into `DeckFlow.Core.Analysis`, and removed the `Feedback*` layering leak from the Core `IRelationalDialect`. Zero user-visible change; verifier PASS 8/8; security audit SECURED 4/4. Dropped Finding C (already addressed by the Core orchestrator slices); deferred the full dialect-branch collapse pending Postgres DDL parity tests.
4. **Resolved feature debt (FEAT-01/02)** — captured the `SpellbookCombo` ranking fields (`popularity`/`manaValueNeeded`/`uses`) the parser previously dropped and priority-ranked Deck Primer combos (popularity DESC, manaValueNeeded ASC); verified Gemini artifacts fit the ~30,000-char paste ceiling across all 4 workflows (analysis 24,994 / comparison 23,830 / meta-gap 18,026 / primer 5,553) with the `DECKFLOW_GEMINI_ENABLED` flag staying default-off.
5. **Merged v1.7 to `main` and confirmed Render deploys from `main` (OPS-01)** — first CalVer cycle.

## Requirements Coverage

8/8 requirements satisfied (FEAT-01 PASS-WITH-NOTES). See `cycle8-REQUIREMENTS.md`.

| Requirement | Phase | Status |
|-------------|-------|--------|
| HARD-01 | 51 | SATISFIED — web + Studio smokes PASS; 2 P45 sub-smokes + real Publish-commit waived-with-reason |
| HARD-02 | 52 | SATISFIED — live prod publish run end-to-end; admin/content preserved, 8 inserted, artifacts on `/data` |
| HARD-03 | 51 | SATISFIED — PG-gated suite found F-51-PG-01, fixed same-session `c4b625e`; 19/19 PG + 20/20 SQLite |
| ARCH-01 | 53 | COMPLETE — `Services/` foldering + Program.cs DI extract + Feedback dialect-leak removal |
| ARCH-02 | 53 | COMPLETE — CategoryKnowledgeRepository split + deck-stat classifiers → Core |
| FEAT-01 | 54 | SATISFIED (PASS-WITH-NOTES) — Gemini artifacts within paste ceiling; flag default-off; operator live-paste carry-forward |
| FEAT-02 | 54 | SATISFIED — combo ranking fields captured + priority-rank in Deck Primer |
| OPS-01 | 51 | SATISFIED — v1.7 merged to main, Render deploys from main, Cycle 8 branch base confirmed |

## Phase Details

### Phase 51: Verify v1.7 on main + non-prod UAT (HARD-01, HARD-03, OPS-01) — COMPLETE 2026-06-17
4/4 plans. Web smoke (no-scroll-jump grid) + Studio smokes (runtime render, re-distill/cap/cancel, Review/Publish git+LF) + Postgres-gated parity suite + Render deploy-branch flip → main. F-51-PG-01 surfaced by the PG gate, fixed same-session (`c4b625e`). See `51-VERIFICATION.md` (passed). 2 P45 sub-smokes + real Publish-commit waived-with-reason.

### Phase 52: Live prod-publish verification (HARD-02) — COMPLETE 2026-06-17
1/1 plan (operator-gated; AI read-only-SELECT only). Operator DirectPush (new=8/updated=0) ran SCP→`/data` + content-columns-only upsert; admin_fingerprint over 86 pre-existing rows identical before/after, 8 new rows `pending`/not-visible. Live run, not waived. See `52-VERIFICATION.md` (passed). **Security follow-up owed: finish `deckflow_admin` credential rotation/deletion** (prod admin password was exposed in-session → rotated to `deckflow_28g4_user`; old-account deletion owed by operator).

### Phase 53: Architecture backlog burn-down (ARCH-01, ARCH-02) — COMPLETE 2026-06-17
4/4 plans (W1 53-01/03/04, W2 53-02). Facade-then-extract split of CategoryKnowledgeRepository; Program.cs DI extraction + Services foldering; deck-stat classifiers → Core + 64 tests; Feedback* removed from Core dialect → Web `FeedbackDialect`. The DI ValidateOnBuild smoke test caught a latent missing `IFeatureFlagCache` registration. Build 0err, Core 447/447, Web 633/644 (11 PG-skip). Verifier PASS 8/8 (`53-VERIFICATION.md`); SECURED 4/4 (`53-SECURITY.md`). Full dialect-branch collapse deferred (PG DDL parity prereq).

### Phase 54: Feature debt (FEAT-01, FEAT-02) — COMPLETE 2026-06-17
2/2 plans. SpellbookCombo ranking fields captured + Deck Primer priority-rank (popularity DESC / manaValueNeeded ASC, tolerant parse). Gemini artifact sizes measured vs ~30k ceiling across all 4 workflows — all under; flag stays default-off. FEAT-02 PASS, FEAT-01 PASS-WITH-NOTES. See `54-VERIFICATION.md`. Carry-forward: operator live Gemini paste before flipping `DECKFLOW_GEMINI_ENABLED` in prod (analysis = least headroom); F-54-FEAT01-01 analysis-variant truncation risk deferred.

## Carry-Forward / Deferred

- **`deckflow_admin` credential deletion** (P52 security follow-up — owed by operator; password already rotated).
- **Operator live Gemini paste** before flipping `DECKFLOW_GEMINI_ENABLED` in prod (P54).
- **Full dual-dialect branch collapse** (51 `IsPostgres`/`IsSqlite` branches) — gated on adding a Postgres DDL parity test.
- **Cycle 9 scope:** Studio/content-pipeline expansion, SEO/growth/ops (per REQUIREMENTS Out-of-Scope), plus SEED-001 (KB add/remove + publish-tracking).
