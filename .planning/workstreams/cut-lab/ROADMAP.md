# Roadmap: Cut Lab (Workstream: cut-lab)

## Overview

Cycle 18 ships a deterministic decision-support loop that takes a builder from an oversized 101-150 card Commander pool to a validated, exported 100-card deck: capture the pool and the builder's own intent, protect what must never be cut, expose the pool's structural composition, guide iterative cuts with measurable (never judgmental) tradeoffs, let the builder pin turn-based goals and test what-if swaps against a reused simulation engine, and finish with a builder-compatible export. Every phase reuses existing DeckFlow engines (parsing, role/category inference, Monte Carlo mulligan/castability/plan-presence simulation, bracket rules, diff/export) — new effort is comparison rules, structural detection, and interaction design, not new simulation math.

## Phases

**Phase Numbering:**
- Cycle 18 (Cut Lab) starts at **Phase 101** — phases 94-100 are reserved by the concurrent Cycle 17 (Creator-Style) milestone in a separate worktree.
- Integer phases (101, 102, ...): Planned milestone work.
- Decimal phases (101.1, 101.2, ...) would be urgent insertions (none at roadmap creation).

- [x] **Phase 101: Intake & Protection Foundation** - Oversized pool intake, intent capture, and full card/package/role locking (completed 2026-07-19)
- [ ] **Phase 102: Structural Analysis & Role Floors** - Functional slot competition, structural findings, and configurable role floors
- [ ] **Phase 103: Simulation Engine & Guided Cut Rounds** - Metrics recalculation engine plus the obvious-structural-preference cut loop with measurable tradeoffs
- [ ] **Phase 104: Goals & What-If Scenarios** - Turn-based goal definitions, saved scenarios, and instant what-if swap recalculation
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
- [ ] 102-03-PLAN.md — Page-service orchestration: classification I/O (fail-open, batched), stages A-F wiring, view model extension, PoolStatusText cleanup
- [ ] 102-04-PLAN.md — UI: three Razor sections, floor editor TS, multi-role pool table, CSS, Vitest/e2e fixture updates
- [ ] 102-05-PLAN.md — E2e structure spec, floor-persistence round-trip proof, theme×viewport screenshots, full phase test gate
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
**Plans**: TBD
**UI hint**: yes

### Phase 104: Goals & What-If Scenarios
**Goal**: A builder can pin the deck to their own turn-based goals, save that configuration, and instantly see the consequence of any hypothetical swap.
**Depends on**: Phase 103 (reuses its simulation/metrics engine and working-list state), Phase 101 (locks and declared intent feed saved scenarios)
**Requirements**: GOAL-01, GOAL-02, GOAL-03
**Success Criteria** (what must be TRUE):
  1. User can define turn-based goals (e.g. cast commander by turn 3, see ramp by turn 2, hold interaction by turn 2, engine and payoff by turn 6)
  2. User can save and reload named scenarios that capture goals, locks, and deck intent together
  3. User can run a what-if swap (replace card A with card B) and immediately see all goal and consistency metrics recalculated using the Phase 103 engine
**Plans**: TBD
**UI hint**: yes

### Phase 105: Builder-Compatible Export
**Goal**: A builder walks away with a validated, finished 100-card list and a patch they can paste straight back into the builder they started from.
**Depends on**: Phase 103 (produces the working list), Phase 104 (produces the goal-satisfying final state)
**Requirements**: EXPORT-01, EXPORT-02, EXPORT-03
**Success Criteria** (what must be TRUE):
  1. User can export the finished 100-card list in Moxfield-compatible and Archidekt-compatible text formats
  2. User can export an add/cut patch describing exactly which cards to remove and add relative to their original builder list
  3. The finished list is validated before export — exactly 100 cards, color-identity legal, and Commander-banlist clean — reusing existing diff/export and banlist infrastructure
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 101 -> 102 -> 103 -> 104 -> 105

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 101. Intake & Protection Foundation | 4/4 | Complete   | 2026-07-19 |
| 102. Structural Analysis & Role Floors | 2/5 | In Progress|  |
| 103. Simulation Engine & Guided Cut Rounds | 0/TBD | Not started | - |
| 104. Goals & What-If Scenarios | 0/TBD | Not started | - |
| 105. Builder-Compatible Export | 0/TBD | Not started | - |
