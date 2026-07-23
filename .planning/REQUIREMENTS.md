# Requirements: Cycle 19 — Cut Lab Upgrade Hardening

**Defined:** 2026-07-23
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip, without the user reformatting anything.

## Cycle 19 Requirements

### Server-Authored Cut Lab State

- [ ] **CLUP-01**: Cut Lab JSON mutation endpoints return a server-authored UI patch containing serialized state, current count, cards remaining, export eligibility, proposal rows, structural finding rows, and what-if option data.
- [ ] **CLUP-02**: `cut-lab.ts` renders returned patch data instead of recomputing domain rules that already exist in C#.
- [ ] **CLUP-03**: Quantity legality, accepted cuts, current counts, and export readiness display identically after JSON mutations and full no-JS server round trips.

### What-If Service Consolidation

- [ ] **CLUP-04**: What-if preview and commit logic live behind one `ICutLabWhatifService` path used by the JSON API and no-JS `/cut-lab/whatif` action.
- [ ] **CLUP-05**: Preview remains non-destructive, commit remains atomic, and both paths enforce the same commander-lock, quantity, and floor-warning rules.

### Navigation, Pool Discovery, and Card Context

- [ ] **CLUP-06**: On mobile, Cut Lab exposes a sticky step/jump affordance that scrolls users to Process, Decide, Goals, and Export sections without hiding primary tables.
- [ ] **CLUP-07**: The jump behavior is progressive enhancement only; existing server-submit step buttons and no-JS fallback continue to work.
- [ ] **CLUP-08**: Mobile navigation is verified across Classic, Nyx, and Commander Table themes without text overlap or unreadable pill/button states.
- [ ] **CLUP-11**: The main "Lock your pool" table supports locked-state filtering so users can quickly show all, locked, or unlocked cards without changing card state.
- [ ] **CLUP-12**: The main "Lock your pool" table supports card search by card name, preserving package assignment and lock controls for matching rows.
- [ ] **CLUP-13**: Cut Lab sections can collapse and expand, with collapsed state remembered in browser local storage per deck/page.
- [ ] **CLUP-14**: Cut Lab exposes compact in-page section anchors with mobile sticky jump behavior patterned after the Manabase page.
- [ ] **CLUP-15**: Package assignment includes a short static help block and one-line inline helper text near the package select explaining how named groups work.
- [ ] **CLUP-16**: Card oracle context is available through a reusable text-first per-card disclosure, starting in lock pool rows and reused for structural/combo evidence where card context is available.
- [ ] **CLUP-17**: Structural/combo findings distinguish complete combo membership from near-combo missing-partner state, including weak-floor cases where combo context explains why cards are protected or missing.

### Regression Preservation

- [ ] **CLUP-09**: Role-group and Structural card evidence pills continue to lock/unlock canonical pool cards, while unmatched Structural evidence remains inert.
- [ ] **CLUP-10**: Cut Lab full-suite verification covers xUnit, Vitest, TypeScript compile, and focused browser smoke for the changed surfaces, including pool filters/search, collapse state, anchors, oracle disclosures, combo labels, package helper copy, and theme readability.

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
| CLUP-17 | Phase 110 | Pending |
| CLUP-09 | Phase 111 | Pending |
| CLUP-10 | Phase 111 | Pending |

**Coverage:**
- Cycle 19 requirements: 17 total
- Mapped to phases: 17
- Unmapped: 0

---
*Requirements defined: 2026-07-23*
*Last updated: 2026-07-23 after Foreman-approved UI discovery requirements*
