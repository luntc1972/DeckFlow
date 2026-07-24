# Requirements: Cycle 19 — Cut Lab Upgrade Hardening (SHIPPED)

**Shipped:** 2026-07-24 (release `2026.07.9`, merged to main). All requirements satisfied.


**Defined:** 2026-07-23
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip, without the user reformatting anything.

## Cycle 19 Requirements

### Server-Authored Cut Lab State

- [x] **CLUP-01**: Cut Lab JSON mutation endpoints return a server-authored UI patch containing serialized state, current count, cards remaining, export eligibility, proposal rows, structural finding rows, and what-if option data.
- [x] **CLUP-02**: `cut-lab.ts` renders returned patch data instead of recomputing domain rules that already exist in C#.
- [x] **CLUP-03**: Quantity legality, accepted cuts, current counts, and export readiness display identically after JSON mutations and full no-JS server round trips.

### What-If Service Consolidation

- [x] **CLUP-04**: What-if preview and commit logic live behind one `ICutLabWhatifService` path used by the JSON API and no-JS `/cut-lab/whatif` action.
- [x] **CLUP-05**: Preview remains non-destructive, commit remains atomic, and both paths enforce the same commander-lock, quantity, and floor-warning rules.

### Navigation, Pool Discovery, and Card Context

- [x] **CLUP-06**: On mobile, Cut Lab exposes a sticky step/jump affordance that scrolls users to Process, Decide, Goals, and Export sections without hiding primary tables.
- [x] **CLUP-07**: The jump behavior is progressive enhancement only; existing server-submit step buttons and no-JS fallback continue to work.
- [x] **CLUP-08**: Mobile navigation is verified across Classic, Nyx, and Commander Table themes without text overlap or unreadable pill/button states.
- [x] **CLUP-11**: The main "Lock your pool" table supports locked-state filtering so users can quickly show all, locked, or unlocked cards without changing card state.
- [x] **CLUP-12**: The main "Lock your pool" table supports card search by card name, preserving package assignment and lock controls for matching rows.
- [x] **CLUP-13**: Cut Lab sections can collapse and expand, with collapsed state remembered in browser local storage per deck/page.
- [x] **CLUP-14**: Cut Lab exposes compact in-page section anchors with mobile sticky jump behavior patterned after the Manabase page.
- [x] **CLUP-15**: Package assignment includes a short static help block and one-line inline helper text near the package select explaining how named groups work.
- [x] **CLUP-16**: Card oracle context is available through a reusable text-first per-card disclosure, starting in lock pool rows and reused for structural/combo evidence where card context is available; placement must be validated against the Cycle 19 UI-audit recommendation that oracle text should be visible before optional card imagery.
- [x] **CLUP-17**: Structural/combo findings distinguish complete combo membership from near-combo missing-partner state, including weak-floor cases where combo context explains why cards are protected or missing.
- [x] **CLUP-18**: Structural findings identify when evidence cards are part of a combo, show the relevant combo role/context, and preserve lock/unlock behavior for matched card evidence chips.
- [x] **CLUP-19**: Cut Lab has a dedicated theme-readability regression check for all supported themes covering Lock All role pills, role/card chips, package chips, sticky status, warning/finding panels, selects, inputs, and primary buttons.
- [x] **CLUP-20**: Cut Lab UI verification includes representative desktop/mobile screenshots for Classic, Nyx, and Commander Table, with explicit pass/fail notes for usability, understandability, aesthetic hierarchy, and readability.

### Regression Preservation

- [x] **CLUP-09**: Role-group and Structural card evidence pills continue to lock/unlock canonical pool cards, while unmatched Structural evidence remains inert.
- [x] **CLUP-10**: Cut Lab full-suite verification covers xUnit, Vitest, TypeScript compile, and focused browser smoke for the changed surfaces, including pool filters/search, collapse state, anchors, oracle disclosures, combo labels, package helper copy, theme readability, and the Cycle 19 UI-audit screenshot/contrast evidence.

## Out of Scope

| Feature | Reason |
|---------|--------|
| Arbitrary nonbasic card additions | Needs Scryfall resolution and broader re-analysis; not part of the hardening backlog. |
| Reopening shipped Cycle 18 phase archives | Cycle 18 is complete; this milestone consumes its follow-up backlog as new active work. |
| Cycle 17 Creator-Style work | Separate worktree and milestone lane. |
| Cut Lab public go-live flag flip | Operator UAT/release task, not implementation scope. |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CLUP-01 | Phase 108 | Pending |
| CLUP-02 | Phase 108 | Pending |
| CLUP-03 | Phase 108 | Pending |
| CLUP-04 | Phase 109 | Pending |
| CLUP-05 | Phase 109 | Pending |
| CLUP-06 | Phase 110 | Pending |
| CLUP-07 | Phase 110 | Pending |
| CLUP-08 | Phase 110 | Pending |
| CLUP-11 | Phase 110 | Pending |
| CLUP-12 | Phase 110 | Pending |
| CLUP-13 | Phase 110 | Pending |
| CLUP-14 | Phase 110 | Pending |
| CLUP-15 | Phase 110 | Pending |
| CLUP-16 | Phase 110 | Pending |
| CLUP-17 | Phase 110.1 | Pending |
| CLUP-18 | Phase 110.1 | Pending |
| CLUP-19 | Phase 111 | Pending |
| CLUP-20 | Phase 111 | Pending |
| CLUP-09 | Phase 111 | Pending |
| CLUP-10 | Phase 111 | Pending |

**Coverage:**
- Cycle 19 requirements: 20 total
- Mapped to phases: 20
- Unmapped: 0

---
*Requirements defined: 2026-07-23*
*Last updated: 2026-07-23 after Cut Lab UI/theme audit findings were added*
