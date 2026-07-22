# Roadmap: Cut Lab (Workstream: cut-lab)

## Overview

Cycle 18 ships a deterministic decision-support loop that takes a builder from an oversized 101-150 card Commander pool to a validated, exported 100-card deck: capture the pool and the builder's own intent, protect what must never be cut, expose the pool's structural composition, guide iterative cuts with measurable (never judgmental) tradeoffs, let the builder pin turn-based goals and test what-if swaps against a reused simulation engine, and finish with a builder-compatible export. Every phase reuses existing DeckFlow engines (parsing, role/category inference, Monte Carlo mulligan/castability/plan-presence simulation, bracket rules, diff/export) — new effort is comparison rules, structural detection, and interaction design, not new simulation math.

## Phases

**Phase Numbering:**
- Cycle 18 (Cut Lab) starts at **Phase 101** — phases 94-100 are reserved by the concurrent Cycle 17 (Creator-Style) milestone in a separate worktree.
- Integer phases (101, 102, ...): Planned milestone work.
- Decimal phases (101.1, 101.2, ...) would be urgent insertions (none at roadmap creation).

- [x] **Phase 101: Intake & Protection Foundation** - Oversized pool intake, intent capture, and full card/package/role locking (completed 2026-07-19)
- [x] **Phase 102: Structural Analysis & Role Floors** - Functional slot competition, structural findings, and configurable role floors (completed 2026-07-19)
- [x] **Phase 103: Simulation Engine & Guided Cut Rounds** - Metrics recalculation engine plus the obvious-structural-preference cut loop with measurable tradeoffs (completed 2026-07-20)
- [x] **Phase 104: Goals & What-If Scenarios** - Turn-based goal definitions, saved scenarios, and instant what-if swap recalculation (completed 2026-07-21)
- [ ] **Phase 105: Builder-Compatible Export** - Validated final-list and add/cut patch export to Moxfield/Archidekt formats

## Phase Details

### Phase 101: Intake & Protection Foundation
**Goal**: A builder can bring an oversized Commander pool into Cut Lab, declare their build intent, and lock everything that must never be cut before any cutting logic runs.
**Depends on**: Nothing (first phase; reuses existing deck-input surfaces and parser)
**Requirements**: INTAKE-01, INTAKE-02, INTAKE-03, LOCK-01, LOCK-02, LOCK-03
**Success Criteria** (what must be TRUE):
  1. User can submit a 101-150 card pool plus commander via the standard Moxfield/Archidekt URL or text-paste surfaces and see it parsed with a card count and format-legality summary
  2. User can declare primary plan, optional secondary plan, target bracket, and desired play experience, and the declaration persists with the working session
  3. A pool that is already at or below 100 cards, or exceeds the supported cap, produces a clear, actionable message instead of a broken workflow
  4. User can lock individual cards, group cards into named packages and lock/unlock a package as a unit, and bulk-lock an entire role group (e.g. all lands) in one action; the commander is always auto-locked and cannot be unlocked
**Plans**: 4 plans
- [x] 101-01-PLAN.md — Register Cut Lab tool (ToolRegistry, DeckPageTab, feature flag OFF both dialects, tile icon)
- [x] 101-02-PLAN.md — Lock domain model, 101-150 pool validator, commander/package/land lock rules
- [x] 101-03-PLAN.md — State serializer, page service (load/validate/resolve/legality/auto-lock), flag-gated controller
- [x] 101-04-PLAN.md — Cut Lab Razor page, lock interactions TS, CSS, e2e smoke
**UI hint**: yes

### Phase 102: Structural Analysis & Role Floors
**Goal**: A builder sees exactly how their pool is structurally composed — and what floors protect it — before any cut is ever proposed.
**Depends on**: Phase 101 (needs the parsed pool and declared intent)
**Requirements**: SLOT-01, SLOT-02, FLOOR-01, FLOOR-02
**Success Criteria** (what must be TRUE):
  1. User sees pool cards grouped by functional slot competition — cards competing for the same role — using existing role/category inference, with no new classification model introduced
  2. User sees structural findings with supporting evidence: curve congestion, stranded subthemes, redundant finishers, weak floor cases, and enabler-starved cards
  3. User sees default structural role floors (lands, ramp, draw, interaction, protection, engines, payoffs, win conditions) derived from their declared bracket and plan
  4. User can adjust any role floor, and no later cut suggestion may silently break a floor — breaking one always carries an explicit warning
**Plans**: 5 plans
- [x] 102-01-PLAN.md — Floor domain layer: RoleFloors state extension, serializer clamp, CutLabFloorRules (Phase 103 contract), CutLabFloorDefaults + CalculateTargetRamp promotion
- [x] 102-02-PLAN.md — Pure analysis rules: CutLabRoleAssigner (8-role assignment) + CutLabStructuralFindings (5 detectors with degradation flags)
- [x] 102-03-PLAN.md — Page-service orchestration: classification I/O (fail-open, batched), stages A-F wiring, view model extension, PoolStatusText cleanup
- [x] 102-04-PLAN.md — UI: three Razor sections, floor editor TS, multi-role pool table, CSS, Vitest/e2e fixture updates
- [x] 102-05-PLAN.md — E2e structure spec, floor-persistence round-trip proof, theme×viewport screenshots, full phase test gate
**UI hint**: yes

### Phase 103: Simulation Engine & Guided Cut Rounds
**Goal**: A builder works through cuts in a fixed, evidence-backed order and sees the measurable consequence of every proposed cut — the tool never tells them a card is objectively worse.
**Depends on**: Phase 101 (locked pool), Phase 102 (structural findings drive round ordering and floor warnings)
**Requirements**: CUT-01, CUT-02, CUT-03, SIM-01, SIM-02
**Success Criteria** (what must be TRUE):
  1. After every accepted cut or swap, the working list's metrics recalculate — commander-on-time probability, keepable-hand rate, mana/color reliability, early-interaction availability, plan presence, per-goal category-by-turn probability, and flood/screw/curve risk — by reusing the existing Monte Carlo, mulligan, castability, and plan-presence engines (no new simulation math)
  2. User works through cut rounds in a fixed order: obvious cuts, then structural choices, then preference calls
  3. Every proposed cut shows its measurable tradeoff deltas before the user decides, and the tool never labels a card objectively worse
  4. User can accept, reject, or defer each proposed cut individually, with a running cards-remaining-to-100 count always visible
  5. User can view a before/after comparison between the original pool baseline and the current working list at any point in the process
**Plans**: 10 plans
- [x] 103-01-PLAN.md — Metric contract (7 families) + D-08 determinism guard + D-11 timing spike
- [x] 103-02-PLAN.md — CutLabState extension: decision history (D-16) + baseline snapshot (D-12) + immutable-Pool derived-working-list helper (HIGH-1) + bounded serializer
- [x] 103-03-PLAN.md — Resolved-card cache (D-09 gap) + delta cache (D-10), dedicated bounded instances + DI
- [x] 103-04-PLAN.md — Cut round engine over the derived working list: CUT-01 fixed order, D-04 loop-around, Pitfall-3 finding tally
- [x] 103-05-PLAN.md — Trials-override parameterization (HIGH-3) + simulation service: 7-family projection (SIM-01) + baseline builder, cached + noise-floored
- [x] 103-06-PLAN.md — Shared CutLabAnalysisContextBuilder (HIGH-2) + intake: cache/baseline + initial round plan + server-computed initial deltas (HIGH-4)
- [x] 103-07-PLAN.md — Shared decision applier + POST /api/cut-lab/decide (JSON) + no-JS /cut-lab/decide form fallback (HIGH-5), context rebuild (HIGH-2) + FLOOR-02 gate
- [x] 103-08-PLAN.md — ViewModel + Razor: Cut rounds / Cuts made / Compare sections with ready-made deltas + real no-JS decision forms
- [x] 103-09-PLAN.md — cut-lab.ts decision-form submit interception (fetch/patch) + site-common.css layout + Vitest
- [x] 103-10-PLAN.md — e2e rounds/decision/restore/compare + no-JS fallback + full-suite gate + human verify
**UI hint**: yes

### Phase 104: Goals & What-If Scenarios
**Goal**: A builder can pin the deck to their own turn-based goals, save that configuration, and instantly see the consequence of any hypothetical swap.
**Depends on**: Phase 103 (reuses its simulation/metrics engine and working-list state), Phase 101 (locks and declared intent feed saved scenarios)
**Requirements**: GOAL-01, GOAL-02, GOAL-03
**Success Criteria** (what must be TRUE):
  1. User can define turn-based goals (e.g. cast commander by turn 3, see ramp by turn 2, hold interaction by turn 2, engine and payoff by turn 6)
  2. User can save and reload named scenarios that capture goals, locks, and deck intent together
  3. User can run a what-if swap (replace card A with card B) and immediately see all goal and consistency metrics recalculated using the Phase 103 engine
**Plans**: 6 plans
- [x] 104-01-PLAN.md — GOAL-01 backend: goal domain + serializer clamp + engine threading + Pitfall-1 fix
- [x] 104-02-PLAN.md — GOAL-01 UI: goals editor + per-goal results (view model, Razor, TS snapshot)
- [x] 104-03-PLAN.md — GOAL-03 backend: whatif preview + atomic commit endpoints + whatif-swap round key
- [x] 104-04-PLAN.md — GOAL-02 scenarios: localStorage store + Scenarios panel (JS-only, noscript)
- [x] 104-05-PLAN.md — GOAL-03 UI: swap pickers + preview/keep/discard + no-JS whatif form
- [x] 104-06-PLAN.md — E2e scenarios + whatif specs, full-suite gate, theme/viewport screenshots, human verify
**UI hint**: yes

### Phase 105: Builder-Compatible Export
**Goal**: A builder walks away with a validated, finished 100-card list and a patch they can paste straight back into the builder they started from.
**Depends on**: Phase 103 (produces the working list), Phase 104 (produces the goal-satisfying final state)
**Requirements**: EXPORT-01, EXPORT-02, EXPORT-03
**Success Criteria** (what must be TRUE):
  1. User can export the finished 100-card list in Moxfield-compatible and Archidekt-compatible text formats
  2. User can export an add/cut patch describing exactly which cards to remove and add relative to their original builder list
  3. The finished list is validated before export — exactly 100 cards, color-identity legal, and Commander-banlist clean — reusing existing diff/export and banlist infrastructure
**Plans**: 5 plans
- [x] 105-01-PLAN.md — Capture-once OriginalEntries baseline on CutLabState (+ serializer clamp, survives scenario reload)
- [x] 105-02-PLAN.md — Thread color_identity through ScryfallCardData + mapper (no new Scryfall call)
- [x] 105-03-PLAN.md — CutLabExportComposer (Core): both-dialect full list + CUT/ADD patch + validation summary, unit-tested
- [x] 105-04-PLAN.md — Wire-up: export service, /cut-lab/export action, Export panel + step tab (gated at 100), copy + CSS
- [x] 105-05-PLAN.md — e2e export spec, full-suite gate, theme/viewport screenshots, human verify
  - Wave-4 e2e surfaced a real defect: JS-cutting a multi-copy entry to reach 100 overshot (whole-entry cut) and the Export tab never unlocked. Fixed in this phase (engine overshoot filter + applier guard + JS tab-wire + atomic what-if keep). Partial-copy tuning promoted from backlog to Phase 106.

### Phase 106: Partial-Quantity Tuning & Add Basics
**Goal**: Within a Cut Lab session, the builder fine-tunes copy counts — cut or add copies of basics and other legal-multiple cards, and add brand-new basic lands — to land on exactly 100, lifting the whole-entry all-or-nothing limitation from Phase 105.
**Depends on**: Phase 103 (working list + Derive), Phase 105 (OriginalEntries baseline + quantity-aware export patch)
**Requirements**: EDIT-01, EDIT-02, EDIT-03
**Design (approved 2026-07-22 — "Approach B", inline UI):**
- New `CutLabState.QuantityAdjustments`: signed per-name copy deltas (+ added-basic flag), serializer-bounded. `CutLabWorkingList.Derive` gains a second pass — apply whole-entry `Decisions` (unchanged), then apply adjustments (clamp each entry ≥ 0, materialize added basics as land entries). All consumers read the derived list.
- No Scryfall resolution: basics (5 + Snow-Covered + Wastes) have known color identity + land type as constants; copy-deltas touch already-resolved pool cards only.
- UI is INLINE in the Decide workspace: +/- steppers on basic / legal-multiple rows + an "add basic land" control; posts adjustments, sticky count + validation update. No new step tab.
- Legality enforced: quantity > 1 only for basics + the any-number cards (Persistent Petitioners, Dragon's Approach, Relentless Rats, Rat Colony, Shadowborn Apostle, Slime Against Humanity, Templar Knights, Nazgûl, Seven Dwarves); everything else capped at 1.
- Export unchanged: `DiffEngine` already diffs quantities vs the OriginalEntries baseline, so add/cut-copy flows through both dialects for free.
**Success Criteria** (what must be TRUE):
  1. User can cut/add copies of a legal-multiple card one at a time (trim/pad `35 Island`)
  2. User can add new basic lands not in the imported pool, resolved from constants
  3. Singleton legality is enforced and the working list can reach exactly 100 by tuning counts
**Out of scope (deferred)**: arbitrary new nonbasic cards (needs Scryfall resolution + re-analysis); undersized-pool intake.

### Phase 107: Cut Lab Tech-Debt Cleanup
**Goal**: Retire the tracked Cut Lab cleanup/tech-debt identified across Cycle 18. No new user requirements — quality only.
**Depends on**: Phases 101–106 (touches their surfaces)
**Cleanup items:**
  1. Dead `_spellbook` + `_categoryKnowledge` fields in `CutLabPageService` (test-only DI-probe, unused) — remove or justify (deferred from 103 C5 + 104 /simplify)
  2. Pool-status chip: two sites disagree (total vs non-commander count) — reconcile
  3. Dark-theme delta contrast: only Nyx has `--cutlab-delta-up/down` overrides; other dark guild themes inherit sub-AA global success/danger — add 2 overrides each (token seam exists)
  4. 101-VERIFICATION open items: validator xmldoc garble; Manabase castability-copy leaking onto Cut Lab; Nyx-mobile badge overlap; Lock-all-lands contrast; mobile pool-row "Package assignment" label truncation
  5. 104-simplify notes: cacheKey→data-attr, route path-base safety, shared pluralizer (server + JS)
  6. Decide: structural-analysis table isn't live-patched on JS decide (server-render refresh only) — live-patch or keep documented
**Success Criteria**: each item fixed or explicitly closed-with-reason; full suite + e2e green; no behavior regression.

## Progress

**Execution Order:**
Phases execute in numeric order: 101 -> 102 -> 103 -> 104 -> 105 -> 106 -> 107

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 101. Intake & Protection Foundation | 4/4 | Complete   | 2026-07-19 |
| 102. Structural Analysis & Role Floors | 5/5 | Complete    | 2026-07-19 |
| 103. Simulation Engine & Guided Cut Rounds | 10/10 | Complete | 2026-07-20 |
| 104. Goals & What-If Scenarios | 6/6 | Complete | 2026-07-21 |
| 105. Builder-Compatible Export | 5/5 | Complete | 2026-07-22 |
| 106. Partial-Quantity Tuning & Add Basics | 3/5 | In Progress | - |
| 107. Cut Lab Tech-Debt Cleanup | 0/? | Not started | - |

## Backlog / Future

### Partial-Copy Cuts (arose from the Phase 105 export fix)
**Status**: PROMOTED to Phase 106 (2026-07-22) — scoped as "Approach B" quantity-adjustment layer + add-basics, see the Phase 106 entry above. This note kept for the origin rationale.
**Why**: The Cut Lab decision model is name-keyed with no per-copy quantity, so `CutLabWorkingList.Derive` cuts a whole entry (all copies of a name) on accept. A multi-copy entry (e.g. `35 Island`) is therefore all-or-nothing: it can only be cut when its full quantity fits the remaining budget. Phase 105 shipped the contained "Option A" fix — the cut engine excludes any entry whose `Quantity > cardsRemainingToTarget` from proposals (with an applier defense-in-depth guard) so the working list converges on exactly 100. Consequence/limitation: a large basic-land stack can't be trimmed a few copies at a time near the target; basics near 100 are effectively uncuttable.
**Scope if built (Option B)**: add per-copy cut support — `CutLabDecision.Quantity`, quantity-subtracting `Derive`, serializer bounds, per-copy apply/restore, per-copy proposals/target-decrement, quantity-aware what-if + deltas + export, and UI to cut N of a stack ("cut 1 of 35"). ~9 source files + broad tests across both projects; touches the P103 state contract and P104 what-if/restore invariants — treat as its own phase, not a bug fix.
