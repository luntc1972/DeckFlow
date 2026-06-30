# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- ✅ **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) — see `.planning/milestones/v1.4-ROADMAP.md`
- ✅ **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** — Phases 28-33 (shipped 2026-06-10) — see `.planning/milestones/v1.5-ROADMAP.md`
- ✅ **v1.6 Content KB Retrieval Fix + Value Re-Validation** — Phases 34-40 (shipped 2026-06-12) — see `.planning/milestones/v1.6-ROADMAP.md`
- ✅ **v1.7 Local Harvest & Publish Studio** — Phases 41-50 (shipped 2026-06-17) — see `.planning/milestones/v1.7-ROADMAP.md`
- ✅ **Cycle 8 — Hardening & Backlog Burn-down** — Phases 51-54 (shipped 2026-06-17, `2026.06.4`) — see `.planning/milestones/cycle8-ROADMAP.md`
- ✅ **Cycle 9 — Content Pipeline & Publish-Tracking** — Phases 55-58 (shipped 2026-06-19, `2026.06.5`) — see `.planning/milestones/cycle9-ROADMAP.md`
- ✅ **Cycle 10 — Studio Automation, Sync & Polish** — Phases 59-63 (shipped 2026-06-21, `2026.06.6`) — see `.planning/milestones/cycle10-ROADMAP.md`
- ✅ **Cycle 11 — Security, Visibility Control & Creator-Lens** — Phases 64-69 (shipped 2026-06-25, `2026.06.8`) — see `.planning/milestones/cycle11-ROADMAP.md`
- ✅ **Cycle 12 — Manabase Accuracy, Command-Zone Awareness & Cross-Tool Persistence** — Phases 70-74 + flag-key namespacing (shipped 2026-06-27, `2026.06.9`)
- 🚧 **Cycle 13 — Deck Evaluation & Creator Output** — Phases 75-78 (in progress)

## Phases

### Cycle 13 — Deck Evaluation & Creator Output (Phases 75-78)

**Milestone goal:** Extend the paste-artifact engine into deck evaluation (bracket classification + balancer, multi-axis score) and creator output (auto-refreshing primer), plus surface the manabase tap analysis DeckFlow already computes — closing the top uncontested gaps from the 2026-06-27 commander-feature-wants research.

**Granularity:** coarse — 4 phases is the natural minimum. Each phase maps to one complete, independently-verifiable capability. Tap Analyzer is the additive quick-win with zero dependencies; Bracket is the headline differentiator and the gate for the Score's Power axis; Score folds into the existing analysis artifact with no new tool tile; Primer closes the creator lane and is independent of all other phases.

**Coverage:** 17/17 requirements mapped — BRACKET-01..05, SCORE-01..04, PRIMER-01..04, TAP-01..04.

- [x] **Phase 75: Tap Analyzer Surface** — Surface untapped-source frequency and turn-1 untapped availability from the existing manabase simulator, behind a flag seeded OFF (completed 2026-06-28)
- [x] **Phase 76: Bracket Classifier + Balancer** — Auto-classify decks into the official 5-tier Commander bracket; generate a "cuts to target bracket" paste artifact; migrate versioned Game Changers data out of `.cs` literals (completed 2026-06-28)
- [x] **Phase 77: Multi-Axis Deck Score** — Four-axis Power/Speed/Control/Consistency score (0-5 coarse bands) folded into the existing deck-analysis paste packet across all three prompt variants (completed 2026-06-29)
- [x] **Phase 78: Auto-Refreshing Primer** — Stale-flag detection on the Deck Primer when the source deck changes; golden tests lock the staleness equivalence relation (completed 2026-06-30)

## Phase Details

### Phase 75: Tap Analyzer Surface

**Goal**: The manabase report and paste artifact expose the untapped-source and turn-1 metrics the CastabilitySimulator already computes inside its Monte-Carlo loop but has never surfaced.
**Depends on**: Nothing (entirely additive to the existing manabase path; no dependency on any other Cycle 13 feature)
**Requirements**: TAP-01, TAP-02, TAP-03, TAP-04
**Success Criteria** (what must be TRUE):
  1. The manabase on-page report and paste artifact both show untapped-source frequency — the count and fraction of mana sources that enter untapped — for the deck overall and broken out per color, with a single source of truth for each number (no two contradictory untapped figures in one report).
  2. The manabase on-page report and paste artifact both show turn-1 untapped availability — the probability of having at least one untapped mana source on turn 1 — as a discrete labeled metric that does not contradict the simulator's own cast-rate figures.
  3. Both metrics are derived from counters accumulated inside the existing 20k-trial simulation loop with no second pass and no new simulation; the only code changes to `CastabilitySimulator` are additive `{ get; init; }` fields on `CardCastability`/`ManabaseReport` with safe defaults, so existing tests and zip round-trips are byte-compatible.
  4. With `analysis.manabase.tap-analyzer` seeded OFF, the manabase page and paste artifact are byte-identical to the pre-Cycle-13 output; the flag is registered in `FeatureFlagCatalog` with a description and seeded OFF in both SQLite and Postgres; web-page changes carry xUnit tests, theme verification, and mobile verification.
**Plans**: 4 plans (waves 0-3)
- [x] 75-01-PLAN.md — Tap-analysis contracts + RED test suite (wave 0)
- [x] 75-02-PLAN.md — Core computation: turn-1 counter + ComputeTapAnalysis + paste block (wave 1)
- [x] 75-03-PLAN.md — Flag registration + service/controller wiring (wave 2)
- [x] 75-04-PLAN.md — Tap card view + CSS + theme/mobile verify (wave 3)
**UI hint**: yes

### Phase 76: Bracket Classifier + Balancer

**Goal**: Users can auto-classify their deck into the official 5-tier Commander bracket and download a paste artifact that frames the floor violations and starter cuts needed to reach a chosen target bracket.
**Depends on**: Nothing new (new standalone surface; ships before Phase 77 so `GameChangerCatalog` is available to the Power axis)
**Requirements**: BRACKET-01, BRACKET-02, BRACKET-03, BRACKET-04, BRACKET-05
**Success Criteria** (what must be TRUE):
  1. User pastes or URL-imports a deck and sees its official bracket tier (B1-B5) with the specific reasons that determined it — which Game Changers were detected, whether a two-card infinite combo was found via Commander Spellbook, whether mass-land-denial or extra-turn chains are present; tutor count is not a bracket gate.
  2. The Game Changers list lives in a versioned seed file (stamped with an effective-date, loaded at startup into `IMemoryCache`) and not in any `.cs` literal; the existing `DeckFlow.Web/Models/CommanderBracketCatalog.cs` hardcoded bracket data is migrated into the new Core model; every bracket classification artifact is stamped with the list's effective-date so a stale list degrades gracefully rather than misclassifying silently.
  3. User selects a target bracket and gets a paste artifact listing the specific floor violations (the cards/combos that exceed the target tier) plus a starter set of suggested cuts framed for AI refinement in one round-trip; a null or unavailable Commander Spellbook response is disclosed as "combo detection unavailable" rather than silently treated as zero combos.
  4. Bracket classification and balancer output render in all three prompt variants (ChatGpt/Claude/Gemini) with no shared helper per ADR-0001; a parity test asserts both the classification block and the balancer block appear in all three variants.
  5. The entire bracket surface is behind `tool.bracket.enabled` seeded OFF (flag OFF = prod byte-identical on deploy); the flag is registered in the tool registry with a tile entry, nav link, help topic reference, and admin warning when disabled; web-page changes carry xUnit tests, theme verification, and mobile verification.
**Plans**: 6 plans (waves 0-5)
- [x] 76-01-PLAN.md — Core models + bracket-data.json seed + BracketClassifier + unit tests (wave 0)
- [x] 76-02-PLAN.md — GameChangerCatalog service + CommanderBracketCatalog migration + flag wiring (wave 1)
- [x] 76-03-PLAN.md — 3 decoupled prompt variants + registry + parity test (wave 2)
- [x] 76-04-PLAN.md — BracketClassificationService orchestration + combo-null handling (wave 3)
- [x] 76-05-PLAN.md — BracketController + view + CSS + tool-registry tile + render test (wave 4)
- [x] 76-06-PLAN.md — Theme + mobile human-verify checkpoint (wave 5)
**UI hint**: yes

### Phase 77: Multi-Axis Deck Score

**Goal**: Every deck analysis paste artifact includes a four-axis Power/Speed/Control/Consistency score block that replaces single-number scoring, delivered across all three prompt variants with no new tool tile.
**Depends on**: Phase 76 (`GameChangerCatalog` produced there provides the Game Changers count that feeds the Power axis)
**Requirements**: SCORE-01, SCORE-02, SCORE-03, SCORE-04
**Success Criteria** (what must be TRUE):
  1. Every deck analysis paste artifact contains a multi-axis score block showing Power, Speed, Control, and Consistency each as a coarse 0-5 labeled band (e.g., "Speed: 4 — High") with no false-decimal precision.
  2. Each axis shows inline the specific signals that produced its band (e.g., "Speed 4 — avg MV 1.8, 12 fast-mana sources"); Speed and Consistency derive from signals DeckFlow already computes (avg MV, ramp/draw counts, combo density, tutor count); Power derives from Game Changers count + combo density + fast mana; Control derives from a new interaction/removal classifier over deck categories.
  3. The score block cross-checks against the deck's bracket tier for consistency (a golden test asserts a known cEDH deck scores higher Power/Speed than a known battlecruiser deck) and discloses the signals and any coverage gaps in the artifact so the AI can flag mismatches.
  4. All three prompt variants (ChatGpt/Claude/Gemini) contain the score block, each hand-edited per ADR-0001 with no shared helper; a parity test asserts all three variants received the score addition; the score surface is the existing `/deck-analysis` page with no new tool tile.
**Plans**: 6 plans (waves 0-4)
- [x] 77-01-PLAN.md — Core signal predicates + DeckStatSummary fields + tests (wave 0)
- [x] 77-02-PLAN.md — MultiAxisScore records + MultiAxisScorer + golden tests (wave 1)
- [x] 77-03-PLAN.md — 3 decoupled prompt variants + scoreBlockText + parity test (wave 0)
- [x] 77-04-PLAN.md — Flag + packet-service integration + ScoreJson round-trip (wave 2)
- [x] 77-05-PLAN.md — Score view block + site-common.css + render test (wave 3)
- [x] 77-06-PLAN.md — README + theme/mobile human-verify checkpoint (wave 4)
**UI hint**: no

### Phase 78: Auto-Refreshing Primer

**Goal**: Users see a clear staleness indicator when the Deck Primer they generated no longer matches the current deck, protecting the creator artifact's correctness.
**Depends on**: Nothing (independent of all other Cycle 13 features; core primitive already in `DeckPrimerPacketService.TryComputeCacheKeyAsync` and `DiffEngine`)
**Requirements**: PRIMER-01, PRIMER-02, PRIMER-03, PRIMER-04
**Success Criteria** (what must be TRUE):
  1. When a user revisits the Deck Primer page for a deck whose contents have changed since the primer was generated, a visible "Deck changed — regenerate?" banner appears naming the count of changed cards.
  2. The stale-detection key is a canonical card-name + quantity multiset hash: reordering cards or swapping printings does NOT set the stale flag; adding or removing a card or changing a card's quantity DOES set the stale flag.
  3. The stale flag never triggers an automatic primer rebuild and never triggers an upstream re-fetch on its own; regeneration remains the existing explicit user action, and a stale primer stays visible until the user chooses to regenerate it.
  4. Golden tests cover both directions of the staleness equivalence relation: reorder/printing-swap inputs return "fresh"; card-add/remove/quantity-change inputs return "stale"; tests are committed in the same change as the implementation.
**Plans**: 3 plans (waves 1-3)
- [x] 78-01-PLAN.md — Staleness evaluator + deck-only hash + DiffEngine count + flag seed + golden tests (wave 1)
- [x] 78-02-PLAN.md — Staleness DTO/view-model props + zip hash persist/restore-record + resume-without-rebuild controller activation + integration tests (wave 2)
- [x] 78-03-PLAN.md — Stale banner + site-common.css modifier + render test + README + theme/mobile checkpoint (wave 3)
**UI hint**: yes

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 75. Tap Analyzer Surface | 4/4 | Complete   | 2026-06-28 |
| 76. Bracket Classifier + Balancer | 6/6 | Complete   | 2026-06-28 |
| 77. Multi-Axis Deck Score | 6/6 | Complete   | 2026-06-29 |
| 78. Auto-Refreshing Primer | 3/3 | Complete   | 2026-06-30 |

**Phase ordering rationale:**

- **75 first**: Zero dependencies on other Cycle 13 features; lowest-risk change (additive counters in an existing loop); delivers an immediate visible win on the manabase page. Validates the additive-field discipline before the heavier bracket work.
- **76 second**: The headline differentiator. Also produces `GameChangerCatalog` — a hard prerequisite for the Power axis in Phase 77. Concentrates the domain-accuracy risk (Game Changers versioning, gating mechanic detection, Spellbook null handling) in one isolated phase.
- **77 third**: Depends on `GameChangerCatalog` from Phase 76. Speed and Consistency axes reuse ~80% of existing signals; Control and Power build on bracket infrastructure. Folding into the existing analysis packet enriches every deck analysis without a new tool tile.
- **78 last**: Fully independent of the other three phases. Its core primitive already exists in the codebase; the new work is thin (staleness evaluator, snapshot store, stale banner). DeckFlow's clearest creator-lane differentiator ships once the evaluation features have demonstrated the paste-artifact thesis.

---

*v1.0 shipped 2026-05-02 | v1.1 shipped 2026-05-08 | v1.2 shipped 2026-05-13 | v1.3 shipped 2026-05-23 | v1.4 shipped 2026-06-03 | v1.5 shipped 2026-06-10 | v1.6 shipped 2026-06-12 | v1.7 shipped 2026-06-17 | Cycle 8 shipped 2026-06-17 | Cycle 9 shipped 2026-06-19 | Cycle 10 shipped 2026-06-21 (`2026.06.6`) | Cycle 11 shipped 2026-06-25 (`2026.06.8`) | Cycle 12 shipped 2026-06-27 (`2026.06.9`)*
