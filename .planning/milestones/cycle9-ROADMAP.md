# Cycle 9 — Content Pipeline & Publish-Tracking (ARCHIVED)

**Status:** ✅ SHIPPED 2026-06-19 · Tag `2026.06.5` · Phases 55-58 · 11 plans
**Theme:** Wire the Studio/Content-KB pipeline end-to-end — publish-state tracking across Studio + site admin, per-video status, block/add, distill-quality rework — then dogfood it on real content.

## Phases

### Phase 55: Publish-State Foundation — COMPLETE (2026-06-18)
**Goal:** The system records when content was pushed to production and derives one authoritative status per entry.
**Requirements:** PUB-01, PUB-02
**Note:** Production-push timestamp is a NEW `pushed_to_prod_utc` column (existing `published_utc` = video's YouTube date, part of the byte-stable seed contract).
**Plans (2/2):**
- 55-01 — PUB-01: `pushed_to_prod_utc` column, idempotent dual-dialect migration, stamped by both publish paths
- 55-02 — PUB-02: pure `PublishStateDeriver` in Core (four states) + unit tests

### Phase 56: Studio Surfaces — COMPLETE (2026-06-18)
**Goal:** Studio operator sees each video's pipeline status at browse time, multi-selects harvest, blocks/unblocks, adds a single video — no CLI.
**Requirements:** BROWSE-01, BROWSE-02, BROWSE-03, REM-01, REM-02, ADD-01, PUB-03
**Plans (4/4):**
- 56-01 — BROWSE-02: 6-state VideoStatus (Approved/Published) + resolver + tests
- 56-02 — PUB-03: register PublishStateDeriver (DI) + Review column + Publish summary
- 56-03 — REM-02: Blocked.razor list/unblock page + NavMenu
- 56-04 — REM-01/ADD-01/BROWSE-01/03: Harvest block action + status badges + paste feedback

### Phase 57: Admin Surface + Distill Quality — COMPLETE (2026-06-18)
**Goal:** Site admin sees publish-state for every KB entry; new distills produce measurably better content.
**Requirements:** SITE-01, DIST-01
**Plans (2/2):**
- 57-01 — SITE-01: publish-state column on `/Admin/ContentKb` (deriver DI + view column + CSS + tests)
- 57-02 — DIST-01: rework four distill system prompts (paste-ready summary, on-topic clips, tag parsimony); JSON contract unchanged
**Verification:** auto-verifiable PASS; SC2 quality inspection fulfilled by Phase 58 dogfood.

### Phase 58: Dogfood — COMPLETE (2026-06-19)
**Goal:** A real in-cycle harvest + distill run proves the pipeline works end-to-end with accurate publish-state in both Studio and site admin.
**Requirements:** DOGFOOD-01
**Plans (3/3):** 58-01 scaffolding (SELECT-only queries + baseline) · 58-02 operator runbook (real paid harvest+distill → publish → two-surface confirm) · 58-03 no-regression + verdict.
**Result:** All 4 SCs PASS. SC1 distill `e3qGnuupp8U` higher-quality (tags 3 vs 12). SC3 $0 ≤ $15 cap. SC4 prod additive 108→109, nothing flipped. SC2 Published both surfaces **after a fix** — dogfood exposed that DirectPush stamped `pushed_to_prod_utc` but never set `is_visible`, leaving Studio stuck Pushed-hidden while prod /Admin showed Published. Fix `4cb333e`: keyed `SetVisibilityAsync` + DirectPush publishes visible (prod-then-local). Codex-reviewed (1 HIGH + 1 MED fixed); secured (T-58-09, 9/9 SECURED).

## Milestone Summary

**Decisions:**
- `pushed_to_prod_utc` is a separate column from `published_utc` (protect the seed contract).
- DirectPush publishes its rows **visible** immediately (operator decision, SC2 fix); harvest/seed-load stay ships-dark.
- Claude codes / Codex reviews (gpt-5.4 low) for the SC2 fix (temp project rule, expires 2026-06-24).

**Issues found + fixed in-cycle:**
- DirectPush publish-visible gap (SC2) — found by dogfood, fixed + reviewed + secured.
- Prod harvest `42883` (F-51-PG-01) — confirmed stale (pre-deploy errors); fix live since 2026-06-17 21:19Z on `d0bb913`.

**Tech debt / carry-forward:**
- Prod harvest green-run not yet observed since the F-51-PG-01 deploy (awaiting scheduled/manual run).
- `e3qGnuupp8U` durability: in prod DB but not the git seed — a future reset+reseed would omit it until a full git-Publish runs.
- Backlog seeded: Studio "Pull from Prod" (prod→local sync); Validate-KB-value A/B gating experiment.

**Validation:** Per-phase verified + secured (55 secured, 56 verified 7/7, 57 verified + SC2→58, 58 all SCs PASS + 9/9 SECURED). No separate milestone audit — per-phase coverage.
