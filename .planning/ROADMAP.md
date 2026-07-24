# Roadmap: DeckFlow

## Milestones

- [ACTIVE] **Cycle 19 - Cut Lab Upgrade Hardening** - Phases 108-111 (started 2026-07-23) - promotes `.planning/milestones/ws-cut-lab-2026-07-23/BACKLOG-cut-lab-followups-2026-07-22.md`
- [SHIPPED] **Cycle 16 - Content-KB Prod<->Git<->Studio Sync Hardening** - Phases 88-93 (shipped 2026-07-11, `2026.07.3`) - see `.planning/milestones/cycle16-ROADMAP.md`
- â **2026.07.2 Cycle 15 â Cleanup, Refactor & Visual Polish** â Phases 82â87 (shipped 2026-07-05) â see .planning/milestones/2026.07.2-ROADMAP.md
- â **Cycle 14 â Deeper Deck Evaluation** â Phases 79-81 (shipped 2026-07-03, `2026.07.1`) â see `.planning/milestones/cycle14-ROADMAP.md`
- â **Cycle 13 â Deck Evaluation & Creator Output** â Phases 75-78 (shipped 2026-06-30, `2026.06.10`) â see `.planning/milestones/cycle13-ROADMAP.md`
- â **Cycle 12 â Manabase Accuracy, Command-Zone Awareness & Cross-Tool Persistence** â Phases 70-74 + flag-key namespacing (shipped 2026-06-27, `2026.06.9`)
- â **Cycle 11 â Security, Visibility Control & Creator-Lens** â Phases 64-69 (shipped 2026-06-25, `2026.06.8`) â see `.planning/milestones/cycle11-ROADMAP.md`
- â **Cycle 10 â Studio Automation, Sync & Polish** â Phases 59-63 (shipped 2026-06-21, `2026.06.6`) â see `.planning/milestones/cycle10-ROADMAP.md`
- â **Cycle 9 â Content Pipeline & Publish-Tracking** â Phases 55-58 (shipped 2026-06-19, `2026.06.5`) â see `.planning/milestones/cycle9-ROADMAP.md`
- â **Cycle 8 â Hardening & Backlog Burn-down** â Phases 51-54 (shipped 2026-06-17, `2026.06.4`) â see `.planning/milestones/cycle8-ROADMAP.md`
- â **v1.7 Local Harvest & Publish Studio** â Phases 41-50 (shipped 2026-06-17) â see `.planning/milestones/v1.7-ROADMAP.md`
- â **v1.6 Content KB Retrieval Fix + Value Re-Validation** â Phases 34-40 (shipped 2026-06-12) â see `.planning/milestones/v1.6-ROADMAP.md`
- â **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** â Phases 28-33 (shipped 2026-06-10) â see `.planning/milestones/v1.5-ROADMAP.md`
- â **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** â Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) â see `.planning/milestones/v1.4-ROADMAP.md`
- â **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** â Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) â see `.planning/milestones/v1.3-ROADMAP.md`
- â **v1.2 Multi-AI Prompts** â Phases 9-10 (shipped 2026-05-13) â see `.planning/milestones/v1.2-ROADMAP.md`
- â **v1.1 Admin Console** â Phases 6-8 (shipped 2026-05-08)
- â **v1.0 Polish & Quality** â Phases 1-5 (shipped 2026-05-02) â see `.planning/milestones/v1.0-ROADMAP.md`

## Phases

**Phase Numbering:**
- Integer phases (108, 109, ...): Planned Cycle 19 milestone work
- Decimal phases (108.1, 108.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order. Numbering continues after shipped Cut Lab phases 101-107.

### Cycle 19: Cut Lab Upgrade Hardening

- [x] **Phase 108: Server-Authored Cut Lab UI Patch Contract** - Replace client-side domain re-derivation in `cut-lab.ts` with patch DTOs returned by Cut Lab mutation endpoints. ✅ 2026-07-23
- [x] **Phase 109: What-If Service Consolidation** - Move preview and commit behavior into one service shared by JSON and no-JS paths. ✅ 2026-07-23
- [x] **Phase 110: Cut Lab Navigation and Pool Discovery** - Add Cut-Lab-scoped anchors, sticky mobile jump navigation, lock-pool filtering/search, collapsible sections, package assignment help, and text-first card disclosures. ✅ 2026-07-24
- [x] **Phase 110.1: Cut Lab Combo Intelligence** (INSERTED 2026-07-23) - Surface complete-combo and near-combo context in Structural findings, grouped by variant, reusing the Phase 110 disclosure component.
- [x] **Phase 111: Cut Lab Upgrade Regression Gate** - Verify card-pill locking, Structural evidence behavior, all-theme readability, screenshot-based UI evidence, and full Cut Lab suites across the upgraded surfaces. ✅ 2026-07-24 (gsd-verifier 6/6; a11y defects caught+fixed)

<details>
<summary>Cycle 16 (Phases 88-93) - SHIPPED 2026-07-11 (2026.07.3)</summary>

- [x] Phase 88 - Index-Row Integrity Hotfix
- [x] Phase 89 - Content-Hash Foundation
- [x] Phase 90 - DirectPush Correctness + Seed Sync (flag sync.directpush-gitbody)
- [x] Phase 91 - Reconcile + Seed Lifecycle (flag sync.reconcile)
- [x] Phase 92 - Pull Hardening
- [x] Phase 93 - Round-Trip Integration Test

Full details: .planning/milestones/cycle16-ROADMAP.md

</details>

<details>
<summary>â 2026.07.2 Cycle 15 (Phases 82â87) â SHIPPED 2026-07-05</summary>

- [x] Phase 82 â Refactor-Review Sweep & UI Baseline Audit (completed 2026-07-04)
- [x] Phase 83 â Packet-Service SRP Split (completed 2026-07-04)
- [x] Phase 84 â Theme Semantic-Token Migration (completed 2026-07-05)
- [x] Phase 85 â `chatgpt-*` Naming Cleanup (completed 2026-07-05)
- [x] Phase 86 â UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout (completed 2026-07-05)
- [x] Phase 87 â Creator-Source Model Hardening (completed 2026-07-05)

</details>

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
| 82. Refactor-Review Sweep & UI Baseline Audit | 2026.07.2 | 3/3 | Complete | 2026-07-04 |
| 83. Packet-Service SRP Split | 2026.07.2 | 7/7 | Complete | 2026-07-04 |
| 84. Theme Semantic-Token Migration | 2026.07.2 | 2/2 | Complete | 2026-07-05 |
| 85. `chatgpt-*` Naming Cleanup | 2026.07.2 | 5/5 | Complete | 2026-07-05 |
| 86. UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout | 2026.07.2 | 5/5 | Complete | 2026-07-05 |
| 87. Creator-Source Model Hardening | 2026.07.2 | 1/1 | Complete | 2026-07-05 |
| 88. Index-Row Integrity Hotfix | Cycle 16 | 3/3 | Complete | 2026-07-06 |
| 89. Content-Hash Foundation | Cycle 16 | 6/6 | Complete   | 2026-07-07 |
| 90. DirectPush Correctness + Seed Sync | Cycle 16 | 7/7 | Complete   | 2026-07-08 |
| 91. Reconcile + Seed Lifecycle | Cycle 16 | 9/9 | Complete | 2026-07-09 |
| 92. Pull Hardening | Cycle 16 | 2/2 | Complete | 2026-07-10 |
| 93. Round-Trip Integration Test | Cycle 16 | 3/3 | Complete   | 2026-07-11 |

---

## Carry-forward backlog (not in Cycle 16)

- Scheduled/bulk harvest (AUTO-03/04)
- SEO/growth lane (SEO-01..05)
- Matchup / meta-threat read (deferred â deepens cedh-meta-gap, a separate lane)
- **ADMIN-01** â `/Admin/Flags` sortable by on/off (enabled) state (descoped from Cycle 15, user decision 2026-07-05; view-only, no flag semantics change)
- Manabase engine refactor (CastabilitySimulator / ManabaseAnalyzer / ManabaseClassifier SRP split) â deferred out of Cycle 15: behavior-critical Monte-Carlo + Karsten scoring, no byte-identical gate, just heavily worked in Cycles 12/14. Needs a numeric-parity harness built FIRST. Candidate for a dedicated future refactor cycle.
- **KB "commander advice" content class for filtered videos** â the distill classifier filters out videos that lack actionable deckbuilding decisions (slot/cut/synergy on a real list), discarding them entirely. But many are still valuable *general commander advice*: meta/format philosophy, budget-building mindset, card evaluations. Give these a distinct KB content type/home instead of dropping them, so they can be surfaced (and pasted into ChatGPT) as advice rather than deckbuilding lessons. Needs: a second classifier verdict ("advice" vs "filtered"), its own artifact shape/prompt, and a browse surface. Observed 2026-07-04 re-distill filtered 3 such videos: `D5XXv7BzmZw` (The Midrange-ification of Commander â format meta essay), `GGoQxBP3DcE` (budget-deck pep talk / "Rock Lee of Commander"), `s_B1wCIWGR0` (Top 10 Lands for EDH â card eval + pricing).
- ~~**Manabase research-gap closure**~~ ✅ SHIPPED 2026-07-13 (plans 01-10 live in prod `61595280`; flags `restricted-lands`/`ritual-land-credit` seeded OFF awaiting flip). Continuation backlog: `.planning/captures/manabase-backlog-2026-07-13.md`.
- **Manabase backlog (post gap-closure)** — flag flips (ritual-credit ready; restricted-lands needs golden diff), MBGAP-09 cEDH castability surface (own phase, D-02), Tier-3 minors (MBGAP-06/07/08/10), UX LOW 8-10, 3 refactor follow-ups. Details: `.planning/captures/manabase-backlog-2026-07-13.md`.
- **SYNC-F1** â Retire DirectPush entirely (fold into Publish) â this cycle makes the two paths consistent; retirement is a later-cycle decision.
- **SYNC-F2** â Scheduled/automatic reconcile runs (this cycle ships operator-triggered reconcile only).
