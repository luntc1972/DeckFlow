# Requirements: Cycle 18 — Cut Lab + Goal-Based Consistency Lab

**Defined:** 2026-07-19
**Source:** `research/2026-07-18-commander-feature-priorities.md` + user-approved scope (2026-07-18 handoff)

## Cycle 18 Requirements

### Intake (INTAKE)

- [ ] **INTAKE-01**: User can submit an oversized Commander pool (101–150 cards plus commander) through the standard deck-input surfaces (Moxfield/Archidekt URL or text paste) and see it parsed with card count and format-legality summary
- [ ] **INTAKE-02**: User can declare deck intent — primary plan, optional secondary plan, target bracket, and desired play experience — and the declaration persists with the working session
- [ ] **INTAKE-03**: User submitting a pool that is already ≤100 cards or above the supported cap receives a clear, actionable message instead of a broken workflow

### Protection (LOCK)

- [ ] **LOCK-01**: User can lock individual cards so no cut round may propose them; the commander is always auto-locked
- [ ] **LOCK-02**: User can group cards into named packages and lock/unlock a package as a unit
- [ ] **LOCK-03**: User can bulk-lock a whole role group (e.g., all lands) in one action

### Structural Analysis (SLOT)

- [x] **SLOT-01**: User sees pool cards grouped by functional slot competition — cards competing for the same role — using existing role/category inference
- [x] **SLOT-02**: User sees structural findings with supporting evidence: curve congestion, stranded subthemes, redundant finishers, weak floor cases, and enabler-starved cards

### Role Floors (FLOOR)

- [x] **FLOOR-01**: User sees default structural role floors (lands, ramp, draw, interaction, protection, engines, payoffs, win conditions) derived from declared bracket and plan
- [x] **FLOOR-02**: User can adjust each role floor, and no cut suggestion may silently break a floor — breaking one always carries an explicit warning

### Cut Rounds (CUT)

- [ ] **CUT-01**: User works through cut rounds in a fixed order: obvious cuts → structural choices → preference calls
- [ ] **CUT-02**: Every proposed cut shows its measurable consequences (tradeoff deltas) before the user decides; the tool never labels a card objectively worse
- [ ] **CUT-03**: User can accept, reject, or defer each proposed cut individually, with a running cards-remaining-to-100 count always visible

### Goals & Scenarios (GOAL)

- [ ] **GOAL-01**: User can define turn-based goals (e.g., cast commander by turn 3, see ramp by turn 2, hold interaction by turn 2, engine + payoff by turn 6)
- [ ] **GOAL-02**: User can save and reload named scenarios capturing goals, locks, and deck intent
- [ ] **GOAL-03**: User can run a what-if swap (replace card A with card B) and immediately see all goal and consistency metrics recalculated

### Simulation (SIM)

- [ ] **SIM-01**: After every accepted cut or swap, the working list's metrics recalculate: commander-on-time probability, keepable-hand rate, mana/color reliability, early-interaction availability, plan presence, per-goal category-by-turn probability, and flood/screw/curve risk
- [ ] **SIM-02**: User can view a before/after comparison between the original pool baseline and the current working list

### Export (EXPORT)

- [ ] **EXPORT-01**: User can export the finished 100-card list in Moxfield- and Archidekt-compatible text formats
- [ ] **EXPORT-02**: User can export an add/cut patch describing exactly which cards to remove/add relative to their original builder list
- [ ] **EXPORT-03**: The finished list is validated before export: exactly 100 cards, color-identity legal, and Commander-banlist clean

### Copy-Count Editing (EDIT) — Cycle 18 addendum (added 2026-07-22)

- [ ] **EDIT-01**: User can cut or add copies of a legal-multiple card in the working list (basics + the "any number" cards) one copy at a time, instead of the whole entry all-or-nothing
- [ ] **EDIT-02**: User can add brand-new basic lands (the five basics, Snow-Covered variants, and Wastes) that were not in the imported pool, resolved from known constants with no card lookup
- [ ] **EDIT-03**: Copy edits enforce Commander singleton legality (quantity > 1 only for basics and the recognized any-number cards) and let the working list reach exactly 100 by tuning counts, not only by whole-entry cuts

## Future Requirements (deferred)

- Add arbitrary new nonbasic cards to the working list (needs Scryfall name-resolution + full role/floor/sim/color/banlist re-analysis) — the larger half of "full editor", deferred from EDIT scope
- Undersized-pool intake (accept a paste/URL under 101 cards and build up to 100) — changes the INTAKE 101–150 oversized premise

- Deck Experiment Journal (game observations on Deck History) — next in research sequence
- Pod Fit / Rule Zero Passport — compatibility matrix across decks
- Optional AI explanation layer over deterministic results

## Out of Scope

| Item | Reason |
|------|--------|
| AI-generated cut decisions | Product stance: deterministic tradeoffs only, user decides |
| Complete 100-card deck generation | Crowded market; research anti-feature |
| Collection management / ownership | ManaBox territory; separate future feature |
| Universal 1–10 power scoring | Research anti-feature |
| Collaborative review, Pilot Trainer | Lower priority (3/5), large effort |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| INTAKE-01 | Phase 101 | Pending |
| INTAKE-02 | Phase 101 | Pending |
| INTAKE-03 | Phase 101 | Pending |
| LOCK-01 | Phase 101 | Pending |
| LOCK-02 | Phase 101 | Pending |
| LOCK-03 | Phase 101 | Pending |
| SLOT-01 | Phase 102 | Complete |
| SLOT-02 | Phase 102 | Complete |
| FLOOR-01 | Phase 102 | Complete |
| FLOOR-02 | Phase 102 | Complete |
| CUT-01 | Phase 103 | Pending |
| CUT-02 | Phase 103 | Pending |
| CUT-03 | Phase 103 | Pending |
| SIM-01 | Phase 103 | Pending |
| SIM-02 | Phase 103 | Pending |
| GOAL-01 | Phase 104 | Pending |
| GOAL-02 | Phase 104 | Pending |
| GOAL-03 | Phase 104 | Pending |
| EXPORT-01 | Phase 105 | Pending |
| EXPORT-02 | Phase 105 | Pending |
| EXPORT-03 | Phase 105 | Pending |
| EDIT-01 | Phase 106 | Pending |
| EDIT-02 | Phase 106 | Pending |
| EDIT-03 | Phase 106 | Pending |

**Coverage:** 24/24 requirements mapped to a phase (21 v1 + 3 EDIT addendum). Phase 107 is cleanup/tech-debt (no new requirements). No orphans.
