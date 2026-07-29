# Requirements: Cycle 21 — Discriminating Cut Proposals

**Defined:** 2026-07-26
**Re-planned:** 2026-07-26 (scope widened from role floors only; see PROJECT.md Decisions Log)
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip, without the user reformatting anything.

## Milestone Thesis

Cut Lab measures the deck well but barely compares cards. Floor accuracy improves the *guardrails*
("do not cut below here"); only a discriminating finding improves the *ranking* ("cut this one
first"). This cycle does both, and is explicit about which requirement serves which.

## Cycle 21 Requirements

### Interaction Taxonomy Split (ISPL)

- [ ] **ISPL-01**: `CutLabRoleAssigner` emits `interaction-targeted` and `interaction-mass` as distinct role keys in place of the single merged `interaction` key, sourced from the `DeckStatClassifier.IsTargetedRemovalCard` / `IsBoardWipeCard` calls that are already invoked separately at `CutLabRoleAssigner.cs:142-143`.
- [ ] **ISPL-02**: Every floor-key consumer handles both new keys — `CutLabFloorRules.RoleKeys`, `CutLabFloorDefaults`, `CutLabStructuralFindings.WeakFloorRoleOrder`, `CutLabCutRoundEngine.LockedOvershootRoleOrder`, `CutLabRoleAssigner.RoleDisplayLabels` — and a persisted `CutLabState` carrying the legacy `interaction` key restores without data loss or unhandled exception.
- [ ] **ISPL-03**: Bracket-derived default floors are defined for both new roles, and at every bracket their sum is greater than or equal to today's merged `interaction` floor, so splitting the role cannot silently weaken the guardrail.

### Role-Floor Divergence Research (RFLR)

- [ ] **RFLR-01**: Per-commander role classification is reproduced using the real production classifiers (`DeckStatClassifier`, `PlanRoleClassifier`, `CutLabRoleAssigner`) against the Postgres corpus — not a reimplementation — for a defensible commander sample with a stated, enforced minimum deck count.
- [ ] **RFLR-02**: An explicit statistical bar (minimum deduped deck count per commander, effect-size/spread threshold) is defined in writing and applied uniformly to every role, separating real per-commander divergence from corpus noise.
- [ ] **RFLR-03**: A committed findings document reports, per commander and per role, the count/spread data and which roles clear the bar — over the post-ISPL role taxonomy, so the roles measured are the roles that ship.
- [ ] **RFLR-04**: The findings document ends with an explicit go/no-go recommendation naming exactly which roles are in scope for Phase 3.
- [ ] **RFLR-09**: The harness has **no code path that can emit a findings artifact without querying the corpus** — the orphaned `WriteSyntheticVerificationOutputs` / `BuildSyntheticCommander` fixture writer is deleted, not merely left uncalled. Every findings artifact carries run provenance (database host, commanders enumerated, raw and deduped deck counts, run timestamp, harness commit SHA), and the run exits non-zero when zero commanders qualify.

### Commander-Aware Floor Defaults (RFLR) — conditional on RFLR-04 = go

- [ ] **RFLR-05**: For each role Phase 2 flagged as real signal, `CutLabFloorDefaults` resolves that role's effective default as `max(bracket-and-plan derived, commander-derived)`, so commander-specific corpus data may only raise a floor and never lower one; both numbers are retained for display. (amended 2026-07-28 by Phase 3 D-04 from a priority chain to a max; see 03-CONTEXT.md D-04 for the measured payoffs 124-of-124 evidence)
- [ ] **RFLR-06**: A commander below the statistical bar, or a role that did not clear it, produces byte-identical floor defaults to current shipped behavior.
- [ ] **RFLR-07**: The new `max(bracket, commander)` resolution has unit coverage for the commander-hit path, the fallback path, and the role-not-in-scope path. (wording aligned 2026-07-28 with RFLR-05's D-04 amendment; scope unchanged)
- [ ] **RFLR-08**: The Cut Lab role-floor UI shows **both numbers side by side** for every role — the bracket-derived floor and the commander-derived floor — clearly labeled, with the commander floor shown regardless of bracket. Roles or commanders without commander-specific data show the bracket value and an explicit empty marker for the commander column, never a silently substituted number.

### Functional-Twins Detector (TWIN)

- [ ] **TWIN-01**: A new `CutLabFindingKind` groups unlocked, non-commander pool cards by (role ∩ mana-value bucket ∩ primary card type) and raises a finding when a group holds 3 or more members.
- [ ] **TWIN-02**: The new kind is **discriminating** — it is deliberately NOT added to `CutLabCutRoundEngine.ExcludedFindingKindsFromTally`, so it contributes to the round-1/round-2 finding tally and can change which card is proposed next.
- [ ] **TWIN-03**: Evidence within a twin group is ordered highest mana value first, matching the community heuristic that the costlier of two functionally equivalent cards is the cut.
- [ ] **TWIN-04**: Locked cards and the commander are excluded from twin groups; a group member that is also combo-protected is still listed, so the two findings compose rather than one suppressing the other.

### Archidekt Bracket Capture (BRKT) — parallel, non-gating

- [ ] **BRKT-01**: The category harvest parses the bracket field from the Archidekt deck payload already being fetched — no additional request per deck.
- [ ] **BRKT-02**: Bracket is persisted as a nullable column so existing harvested rows are unaffected and no backfill is required for the schema change to land.
- [ ] **BRKT-03**: A deck harvested before this change is distinguishable from a deck harvested after it whose bracket was genuinely absent, so a later commander × bracket analysis can tell "not captured" from "not declared."

## Out of Scope

| Feature | Reason |
|---------|--------|
| Commander × bracket floor derivation | Bracket capture (BRKT) lands this cycle, but backfill latency means the commander floor stays bracket-agnostic in Cycle 21. Joint derivation is a later refinement. |
| Land/ramp/draw floor logic changes | Already commander-aware via the existing priority chain |
| Bracket+plan fallback behavior changes | Preserved as-is; commander data is an additive layer |
| Bracket legality / Game Changers panel | Real gap, deliberately deferred — thematically separate from cut-proposal quality |
| EDHREC statistical substrate (C20-03) | Long-term Cycle 20 track; see the corpus-ownership note in PROJECT.md before starting it |
| Partial-copy cuts, add-non-basic cards | Backlogged; low need against cost |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| ISPL-01 | Phase 1 | Pending |
| ISPL-02 | Phase 1 | Pending |
| ISPL-03 | Phase 1 | Pending |
| RFLR-01 | Phase 2 | Pending |
| RFLR-02 | Phase 2 | Pending |
| RFLR-03 | Phase 2 | Pending |
| RFLR-04 | Phase 2 | Pending |
| RFLR-09 | Phase 2 | Pending |
| RFLR-05 | Phase 3 (conditional) | Pending |
| RFLR-06 | Phase 3 (conditional) | Pending |
| RFLR-07 | Phase 3 (conditional) | Pending |
| RFLR-08 | Phase 3 (conditional) | Pending |
| TWIN-01 | Phase 4 | Pending |
| TWIN-02 | Phase 4 | Pending |
| TWIN-03 | Phase 4 | Pending |
| TWIN-04 | Phase 4 | Pending |
| BRKT-01 | Phase 5 | Pending |
| BRKT-02 | Phase 5 | Pending |
| BRKT-03 | Phase 5 | Pending |

**Coverage:**
- Cycle 21 requirements: 19 total
- Mapped to phases: 19
- Unmapped: 0

---
*Requirements defined: 2026-07-26*
*Last updated: 2026-07-26 (re-plan)*
