# Roadmap: Cycle 21 — Commander-Aware Role Floors

**Workstream:** `cutlab-role-floors` (branch `research/cutlab-role-floors`, isolated worktree)
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip, without the user reformatting anything.

This is a small, two-phase milestone. Phase 2 is explicitly conditional on Phase 1's findings.

## Phases

- [ ] **Phase 1: Role-Floor Divergence Research** - Re-derive per-commander role-floor divergence using the real `DeckStatClassifier`/`PlanRoleClassifier` classifiers against a defensible commander sample, apply an explicit statistical bar, and publish a go/no-go findings doc.
- [ ] **Phase 2: Commander-Aware Floor Defaults (CONDITIONAL on Phase 1 = go)** - Extend `CutLabFloorDefaults` with a commander-specific priority-chain layer for the roles Phase 1 found real signal for, preserving existing bracket+plan fallback behavior everywhere else.

## Phase Details

### Phase 1: Role-Floor Divergence Research
**Goal**: We know, with a defined and applied statistical bar, whether any of the five Cut Lab role floors (interaction, protection, engines, payoffs, win conditions) diverge meaningfully by commander — and that answer is backed by the real production classifiers, not the throwaway Python reimplementation from the prior ad hoc session.
**Depends on**: Nothing (first phase; extends the already-shipped Phase 102 bracket+plan role-floor work, which it does not modify)
**Requirements**: RFLR-01, RFLR-02, RFLR-03, RFLR-04
**Success Criteria** (what must be TRUE):
  1. A reproducible research harness (a throwaway `DeckFlow.CLI` command runner) calls the real `DeckStatClassifier`/`PlanRoleClassifier`/`CutLabRoleAssigner` types from `DeckFlow.Core`/`DeckFlow.Web` directly, classifying oracle-text-only (no Archidekt category tags, avoiding tag-circularity), against decks reconstructed from the Postgres corpus via `CategoryKnowledgeRepository.GetCategoryDeckMembershipForCommanderAsync` (public passthrough to `CardCategoryRepository`'s implementation; `card_category_observations` joined through `sources`/`deck_queue`) — no reimplemented classification logic — and this is stated and verifiable by inspection of the harness code.
  2. Per-commander role classification is produced for a defensible sample wider than the prior session's 4 commanders (Sokka, Edgar Markov, Krenko, Atraxa), with an explicit minimum-deck-count threshold stated and enforced before a commander is included.
  3. An explicit statistical bar (minimum sample size per commander and an effect-size/spread threshold, e.g. ratio or z-score vs. corpus-wide mean) is stated in writing and applied uniformly to all five roles — not eyeballed.
  4. A committed findings document (e.g. `.planning/workstreams/cutlab-role-floors/phases/01-research/RESEARCH-FINDINGS.md`) reports, per commander and per role, the count/spread data, which roles (if any) clear the statistical bar, and ends with an explicit "go" or "no-go" line naming exactly which roles (if any) are in scope for Phase 2.
  5. No production code path (`DeckFlow.Web` controllers/views/services consumed at runtime) is modified by this phase — the harness is additive/throwaway and does not ship.
**Plans**: TBD

### Phase 2: Commander-Aware Floor Defaults (CONDITIONAL on Phase 1 = go)
**Goal**: For any role Phase 1 found real per-commander signal for, Cut Lab's floor defaults reflect that commander's own corpus data via a priority chain, exactly mirroring the pattern already proven for lands (`ManabaseBaselineProvider` → `CedhLandBaselineProvider` → fallback) and ramp/draw in `CutLabFloorDefaults.cs` — while every commander and role without qualifying signal keeps today's bracket+plan-derived floor unchanged.
**Depends on**: Phase 1 — this phase is gated. It is only planned in detail (and only executed) if Phase 1's `RESEARCH-FINDINGS.md` records a "go" recommendation. If Phase 1 returns "no-go" for all five roles, this phase is descoped to a no-op closeout (documenting the negative result) and the milestone ends at Phase 1. Plan scope below assumes "go" and will be narrowed to whichever roles actually cleared the bar.
**Requirements**: RFLR-05, RFLR-06, RFLR-07, RFLR-08
**Success Criteria** (what must be TRUE, assuming Phase 1 = go):
  1. For each role Phase 1 flagged as real signal, `CutLabFloorDefaults` resolves that role's default floor through a priority chain — commander-specific corpus data first, falling back to the existing bracket+plan-derived value — following the same `IManabaseBaselineProvider`/`ICedhLandBaselineProvider`-style pattern already used for lands.
  2. A commander with insufficient corpus data (below the Phase 1 statistical bar) or a role that did not clear the bar in Phase 1 produces byte-identical floor defaults to current shipped behavior — no regression to the bracket+plan fallback path.
  3. `DeckFlow.Core.Tests` (and/or `DeckFlow.Web.Tests`, matching where the new logic lives) has unit coverage for the new priority-chain resolution, including the commander-hit path, the fallback path, and the "role not in scope" path.
  4. The Cut Lab UI shows, per role floor, whether the displayed value is commander-specific or bracket+plan-derived — reusing the existing lands "Source" text pattern (e.g. "Default for B4: 34") rather than inventing a new label style — so the user is never left guessing which values came from their commander and which came from the bracket.
**Plans**: TBD

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Role-Floor Divergence Research | 0/TBD | Not started | - |
| 2. Commander-Aware Floor Defaults (conditional) | 0/TBD | Not started (gated on Phase 1) | - |

---

## Traceability Check

| Requirement | Phase | Status |
|-------------|-------|--------|
| RFLR-01 | Phase 1 | Pending |
| RFLR-02 | Phase 1 | Pending |
| RFLR-03 | Phase 1 | Pending |
| RFLR-04 | Phase 1 | Pending |
| RFLR-05 | Phase 2 (conditional) | Pending |
| RFLR-06 | Phase 2 (conditional) | Pending |
| RFLR-07 | Phase 2 (conditional) | Pending |
| RFLR-08 | Phase 2 (conditional) | Pending |

**Coverage:** 8/8 v1 requirements mapped. No orphans, no duplicates.

---
*Roadmap created: 2026-07-26*
*Granularity: coarse (2 phases) — matches the milestone's intentionally small, research-gated scope.*
