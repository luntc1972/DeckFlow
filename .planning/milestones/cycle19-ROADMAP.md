# Cycle 19 — Cut Lab Upgrade Hardening (SHIPPED 2026-07-24, `2026.07.9`)

Phases 108-111. Merged to main + released 2026.07.9. Feature gated behind `tool.cut-lab.enabled` (ships OFF).
Verification: gsd-verifier 6/6 (CLUP-09/10/19/20); SOLID review SHIP; 4 WCAG a11y defects caught by the gate + fixed.

## Phase list

- [x] **Phase 108: Server-Authored Cut Lab UI Patch Contract** - Replace client-side domain re-derivation in `cut-lab.ts` with patch DTOs returned by Cut Lab mutation endpoints. ✅ 2026-07-23
- [x] **Phase 109: What-If Service Consolidation** - Move preview and commit behavior into one service shared by JSON and no-JS paths. ✅ 2026-07-23
- [x] **Phase 110: Cut Lab Navigation and Pool Discovery** - Add Cut-Lab-scoped anchors, sticky mobile jump navigation, lock-pool filtering/search, collapsible sections, package assignment help, and text-first card disclosures. ✅ 2026-07-24
- [x] **Phase 110.1: Cut Lab Combo Intelligence** (INSERTED 2026-07-23) - Surface complete-combo and near-combo context in Structural findings, grouped by variant, reusing the Phase 110 disclosure component.
- [x] **Phase 111: Cut Lab Upgrade Regression Gate** - Verify card-pill locking, Structural evidence behavior, all-theme readability, screenshot-based UI evidence, and full Cut Lab suites across the upgraded surfaces. ✅ 2026-07-24 (gsd-verifier 6/6; a11y defects caught+fixed)

## Phase Details

### Phase 108: Server-Authored Cut Lab UI Patch Contract
**Goal**: Cut Lab's live UI renders server-authored state after every mutation instead of rebuilding C# domain rules in TypeScript.
**Depends on**: Cycle 18 Cut Lab shipped archive; current structural card-pill fix on `main`
**Requirements**: CLUP-01, CLUP-02, CLUP-03
**Success Criteria**:
  1. JSON decide/adjust/what-if mutations return a typed patch DTO with serialized state, count, cards remaining, export eligibility, proposal/finding rows, and what-if options.
  2. `cut-lab.ts` stops computing quantity legality, accepted-cut summaries, and export eligibility independently where the patch supplies those values.
  3. JSON and no-JS round trips produce matching visible state for counts, export eligibility, and floor/quantity warnings.

### Phase 109: What-If Service Consolidation
**Goal**: One service owns Cut Lab what-if preview and commit rules for both transport paths.
**Depends on**: Phase 108
**Requirements**: CLUP-04, CLUP-05
**Success Criteria**:
  1. `ICutLabWhatifService` exposes preview and commit operations consumed by the API controller and no-JS controller action.
  2. Commit remains atomic and preview remains non-destructive.
  3. Commander locks, quantity legality, floor warnings, and swap eligibility are covered by shared tests rather than duplicate controller assertions.

### Phase 110: Cut Lab Navigation and Pool Discovery
**Goal**: Users can quickly find, inspect, and move through Cut Lab cards and sections without losing the existing lock/package workflow.
**Depends on**: Phase 108
**Requirements**: CLUP-06, CLUP-07, CLUP-08, CLUP-11, CLUP-12, CLUP-13, CLUP-14, CLUP-15, CLUP-16
**Success Criteria**:
  1. Process, Decide, Goals, Export, and other primary Cut Lab sections have stable anchors plus compact jump controls patterned after Manabase.
  2. Existing submit-driven workflow tabs still submit when they need server work; JS enhancement only scrolls when it is safe to do so.
  3. The main Lock your pool table can filter by locked/all/unlocked and search by card name without changing lock/package state.
  4. Primary Cut Lab sections can collapse/expand and remember state in browser local storage per deck/page.
  5. Package assignment has concise helper copy explaining named groups and how cards remain in the pool.
  6. Card oracle text is shown through reusable text-first disclosures in lock pool rows and under Structural evidence chips; text is primary, imagery is optional/enhancement only.
  7. Classic, Nyx, and Commander Table mobile screenshots show no text overlap or unreadable control states.
**Plans**: 6 plans
Plans:
- [x] 110-01-PLAN.md — Card-text view-model lookup (CardTextByCardName), fed to disclosures
- [x] 110-02-PLAN.md — Section ids + collapsible sections with localStorage persistence
- [x] 110-03-PLAN.md — Sticky mobile jump navigation + sticky-bar collision + safe scroll/focus
- [x] 110-04-PLAN.md — Lock-your-pool filter (all/locked/unlocked) + card-name search
- [x] 110-05-PLAN.md — Text-first card disclosures (pool rows + evidence chips) + package help copy
- [x] 110-06-PLAN.md — Cross-theme mobile screenshot verification (Classic/Nyx/Commander Table)

### Phase 110.1: Cut Lab Combo Intelligence (INSERTED 2026-07-23)
**Goal**: Structural findings explain combo membership — complete, near-complete, and variant alternatives — so users understand why cards are protected.
**Depends on**: Phase 110
**Requirements**: CLUP-17, CLUP-18
**Success Criteria**:
  1. Structural/combo findings distinguish complete combo membership from near-combo missing-partner state, including weak-floor cases where combo context explains why cards are protected.
  2. Matched Structural evidence chips carry a combo state badge and expose combo role/context inside the Phase 110 disclosure, keeping the canonical lock/unlock behavior unchanged.
  3. Near-combos that differ only by an interchangeable card are grouped into one finding listing the alternatives rather than one finding per variant.
  4. Research records the Commander Spellbook API's template-slot (`requires`) and variant shapes so candidate matching can be scoped as follow-on work.

**Plans:** 3 plans (3 waves)

Plans:
- [x] 110.1-01-PLAN.md — Card→combo lookup replaces name-only ComboNames; record Spellbook requires/of shape (SC-4) [Wave 1]
- [x] 110.1-02-PLAN.md — ComboProtected finding kind, variant grouping, weak-floor cross-ref, tally exclusion, per-card combo-state map on view-model + patch DTO [Wave 2]
- [x] 110.1-03-PLAN.md — Chip combo badge + disclosure combo context + theme-readable CSS + what-if badge round-trip [Wave 3]

**Split rationale**: Inserted during Phase 110 discuss-phase (2026-07-23). Phase 110 is view-layer only (Razor, TypeScript, `site-common.css`, one view-model dictionary); the combo work is data-layer (Spellbook parsing, a new finding kind, a changed evidence record, and a ripple into the Phase 108 patch DTO). Different risk profiles, different regression gates.

### Phase 111: Cut Lab Upgrade Regression Gate
**Goal**: Prove the hardening did not regress shipped Cut Lab flows or the newly fixed card-pill locking behavior.
**Depends on**: Phases 108-110.1
**Requirements**: CLUP-09, CLUP-10, CLUP-19, CLUP-20
**Success Criteria**:
  1. Role-group and Structural evidence card pills lock/unlock canonical pool cards; unmatched Structural evidence is inert.
  2. Pool filters/search, collapse state, anchors, oracle disclosures, combo labels, package helper copy, and theme readability are covered by focused browser or unit smoke as appropriate.
  3. Full relevant xUnit, Vitest, TypeScript compile, and focused browser smoke gates pass.
  4. A Cut-Lab-specific all-theme readability check covers Lock All role pills, role/card chips, package chips, sticky status, warning/finding panels, selects, inputs, and primary buttons.
  5. Representative Classic, Nyx, and Commander Table desktop/mobile screenshots are captured and reviewed for usability, understandability, aesthetic hierarchy, and readability.
  6. Findings from verification are either fixed or explicitly recorded as deferred with rationale.
**Plans**: 4 plans
Plans:
- [x] 111-01-PLAN.md — CLUP-09 locking & Structural evidence regression (xUnit + Vitest + e2e) [Wave 1]
- [x] 111-02-PLAN.md — CLUP-10 full-suite smoke coverage matrix + canonical gate command list [Wave 1]
- [x] 111-03-PLAN.md — CLUP-19 all-theme Cut Lab readability spec + WCAG contrast helper [Wave 1]
- [x] 111-04-PLAN.md — CLUP-20 desktop/mobile screenshots + reviewed UI evidence + findings ledger [Wave 2]

## Progress

**Execution Order:**
Phases execute in numeric order: 108 -> 109 -> 110 -> 110.1 -> 111

**Phase 111 wave note (MED-1):** Plans 111-01/02/03 are Wave 1, but the three that launch the
e2e server (111-01 Task 3, 111-02 Task 3, 111-03 Task 2) MUST run SEQUENTIALLY, not truly
concurrently — `scripts/run-web-test.sh` runs `fuser -k 5173/tcp` on start and would kill a
sibling plan's server. Each plan's verify reuses an already-running :5173 server to make this
safe. 111-04 is Wave 2 (depends on 01/02/03).

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 108. Server-Authored Cut Lab UI Patch Contract | Cycle 19 | 3/3 | Complete   | 2026-07-23 |
| 109. What-If Service Consolidation | Cycle 19 | 2/2 | Complete   | 2026-07-23 |
| 110. Cut Lab Navigation and Pool Discovery | Cycle 19 | 6/6 | Complete   | 2026-07-24 |
| 110.1. Cut Lab Combo Intelligence | Cycle 19 | 3/3 | Complete   | 2026-07-24 |
| 111. Cut Lab Upgrade Regression Gate | Cycle 19 | 4/4 | Complete | 2026-07-24 |
