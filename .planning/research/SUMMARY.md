# Project Research Summary

**Project:** DeckFlow — Cycle 14 "Deeper Deck Evaluation"
**Domain:** MTG Commander / cEDH deck-evaluation (paste-to-AI artifact engine), brownfield extension of ASP.NET 10 + Razor
**Researched:** 2026-06-30
**Confidence:** HIGH (all four research streams verified directly against the live `deckflow-cycle14` worktree — classifier, aggregator, Commander Spellbook service, Monte-Carlo sim, Hypergeometric, prompt variants, flag store, and DTOs read with file:line citations)

## Executive Summary

Cycle 14 adds three flag-gated, read-only deck-evaluation dimensions — (1) an **interaction & answers audit**, (2) a **win-condition & combo map**, and (3) an **opening-hand / mulligan evaluator** — onto DeckFlow's already-shipped analysis engine. The dominant finding across all four research streams is consensus: **this milestone requires zero new dependencies.** Every input the three features need is already hydrated and flowing — Scryfall card data (oracle_text, type_line, `keywords`, mana_cost, produced_mana, color_identity), Commander Spellbook combo results (including the captured-but-unused `ManaValueNeeded` / `Popularity`), and a seeded Monte-Carlo `CastabilitySimulator` that already runs a London-mulligan keep decision every trial. The real work is in-codebase C# (new pure classifier predicates, new projection records, additive sim counters, one new metric pass) plus paste-artifact rendering — not packages.

The recommended approach is to follow three proven in-repo grooves rather than invent anything: **Pattern 1** (Phase-77 precedent) — new `DeckStatClassifier` predicate + additive `{ get; init; }` `DeckStatSummary` field tallied in the existing `Compute` loop; **Pattern 2** (multi-axis-score precedent, ADR-0001) — pre-built block text threaded as a trailing optional `string?` param hand-rendered into all three decoupled prompt variants with no shared helper; and **Pattern 3** (TAP-02 precedent) — an additive sim counter observed inside the existing per-trial loop, aggregated like `ComputeTapAnalysis`. Each feature is a paste-artifact section plus a view readout, gated by its own `analysis.*` flag seeded OFF and byte-identical when off.

The key risk is **correctness, not infrastructure**: these features turn heuristics into authoritative-looking numbers. The milestone-wide invariant is that every artifact must paste into ChatGPT/Claude/Gemini and produce a useful answer in one round-trip — so a subtly-wrong count ("7 removal, 3 combos, 62% keepable") is worse than no feature. Mitigation is uniform across the three: frame all outputs as heuristic first-pass reads the AI re-checks, show the cards/bands behind every number, reuse the single existing sim/combo pass (no second sim, no second fetch), and keep all pure logic in `DeckFlow.Core` where the test gate is reliable. Two roadmap-level decisions remain open (mulligan routing and `manaValueNeeded` capture — see Implications) plus a build-order divergence between the Features and Architecture streams.

## Key Findings

### Recommended Stack

**Zero new NuGet, zero new npm — verified against every `.csproj`.** All three features are fully covered by data and engines already in `DeckFlow.Core` / `DeckFlow.Web`. The genuinely-needed additions are pure C# (predicates, records, a metric pass) and paste rendering, not dependencies. This honors the project's no-new-deps rule with no exception required. Explicitly rejected: a stats/probability NuGet (would duplicate the hand-rolled, overflow-safe `Hypergeometric`), a local combo DB (Commander Spellbook is the live authority, already Polly-wrapped + 30-min cached), an MTG rules engine, an NLP oracle-text parser, and any shared cross-AI prompt helper (ADR-0001 forbids it).

**Core technologies (all already in place — no version changes):**
- **.NET 10 / C# 12 + `DeckFlow.Core`** — pure-CPU classifiers, aggregator, and Monte-Carlo sim; new predicates/records/metrics are more of the same.
- **Scryfall `ScryfallCard` DTO -> `DeckStatCardInput`** — `oracle_text`, `type_line`, **`keywords`** (Ward/Hexproof/Flash), `mana_cost`, `produced_mana`, `color_identity` already deserialized and mapped; interaction audit reads these with zero new fetch.
- **`ICommanderSpellbookService` (RestSharp 114 + Polly 8)** — returns `IncludedCombos` (+ `Popularity`, `ManaValueNeeded`) and `AlmostIncludedCombos`; combo map is a projection over data already fetched and 30-min cached.
- **`CastabilitySimulator` (seeded Monte-Carlo) + `Hypergeometric` (log-space closed-form)** — the sim already plays a full London mulligan per trial; `Hypergeometric.AtLeast` already gives P(>=N lands). The mulligan evaluator is a readout, not new compute.

### Expected Features

Three read-only paste-artifact sections + view readouts, each layered on an already-shipped engine piece. The highest-value output of each is the *synthesis*, not the raw count.

**Must have (table stakes, all P1 this cycle):**
- **Bucketed interaction counts** (targeted removal / counters / wipes / protection / stax) — a single "interaction: 14" is uninformative; players think in buckets.
- **Interaction gap-flags** ("0 counterspells", "no catch-all removal", "no graveyard hate") — the whole point of an audit is finding holes; the single highest-value output.
- **Win lines named + grouped** (combo / value / commander-damage) + **combo redundancy count** ("3 ways to assemble") — single-combo fragility is the #1 cEDH concern.
- **Keepable-hand %** + color/curve read — lowest cost, high value; the sim already makes the keep decision, so surface the rate rather than rebuild it.
- **Compact, labeled section per feature, rendered in all 3 AI variants, flag-gated OFF, graceful degradation** when Spellbook is unavailable or no win line is detectable.

**Should have (differentiators):**
- **Coverage-gap synthesis** ("can't answer resolved creatures; no graveyard hate") — no incumbent (EDHREC/Moxfield/Archidekt) tells you what your deck *can't* deal with.
- **"How this deck wins" narrative** (win line + redundancy + tutorability + assembly band) — cedh-decklist-database does this by hand; nobody auto-generates it.
- **Assembly-turn band** off MV + tutor density (always a band — "T2-4 with a tutor" — never a hard number).
- **Protection as its own bucket** (resilience != removal).

**Defer (next cycle / out of scope):**
- Assembly-band sharpened by captured `manaValueNeeded` (pending the parser-capture decision — see below).
- Cross-wiring all three into the multi-axis score narrative.
- **Anti-features to refuse:** exhaustive stax classification (text-heuristic noise -> coarse low/med/high only), hard assembly-turn numbers, per-card "is this good?" grading, mulligan *decisions* / play-vs-draw advisor, win-rate %, live game tracker, "fix my interaction" auto-suggestions (belongs to the AI round-trip).

### Architecture Approach

The single most important structural fact: **`/deck-analysis` (`DeckAnalysisPacketService`) and `/manabase` (`ManabaseAnalysisService`) are two independent pipelines.** The packet service never builds a `ManabaseDeck` and never calls the Monte-Carlo simulator — the sim lives only behind `ManabaseController`. Features 1 and 2 slot cleanly into the deck-analysis score-block groove; Feature 3's sim lives in the *other* pipeline, which is what makes it the highest-lift and forces the routing decision. All new pure logic stays in `DeckFlow.Core`; Web holds only block-text rendering + flag gates + hydration.

**Major components (existing attach points):**
1. **`DeckStatClassifier` / `DeckStatAggregator` / `DeckStatSummary`** — pure role predicates + the single `Compute` loop; Feature 1 adds predicates + additive `{ get; init; }` tallies here (Pattern 1, Phase-77 precedent).
2. **`DeckAnalysisPacketService` + `AnalysisPromptVariantRegistry` + 3 decoupled `*AnalysisPromptVariant.cs`** — block text built once, threaded as trailing optional `string?` params, hand-rendered into ChatGpt/Claude/Gemini (Pattern 2, ADR-0001); Features 1 & 2 both add a block here. The combo fetch (`comboTask`) is already wired and widened — reuse `comboResult`, never double-fetch.
3. **`CastabilitySimulator` + `ManabaseAnalyzer` + `ManabaseReport`** — additive counter in the existing per-trial loop, aggregated like `ComputeTapAnalysis` (Pattern 3, TAP-02 precedent); Feature 3's keepable-hand metric.
4. **`FeatureFlagStore` / `IFeatureFlagCache`** — three `analysis.*` flags seeded OFF in BOTH dialects; gate via `Snapshot().TryGetValue(key, out var on) && on` (never `IsEnabled`, which defaults missing->true).

### Critical Pitfalls

1. **Heuristic substring classification presented as authoritative** (Pitfall 1, Phase 79) — `Contains("destroy target")` mis-reads pseudo-removal, modal/MDFC cards, board wipes, self-target effects. Avoid: frame as "automated first-pass read — verify against the cards," show the card list behind every count, add a "possible/review" confidence tier, extend the shared `DeckStatClassifier` (don't fork a second `Contains` chain).
2. **Over-claiming combos from Commander Spellbook** (Pitfall 2, Phase 80) — "in deck" != reachable; almost-combos conflated with win lines; unranked truncation at 20. Avoid: wire `ManaValueNeeded`/`Popularity` into ranking + assembly-turn read, keep included vs almost-included strictly separated, distinguish API-null ("unavailable") from "no win conditions."
3. **Over-promising subjective mulligan-keep heuristics** (Pitfall 3, Phase 81) — a second, divergent keep rule would contradict the manabase tool's own model. Avoid: reuse the sim's existing `LondonMulligan` + `ColorKeepCap` as the single source of "keepable," state the criterion narrowly, surface a band not a false-precision %.
4. **Cross-cutting per-phase gate conditions (Pitfalls 4-8, every phase):**
   - **Flag-OFF byte-identity** must hold for the page **AND all 3 paste artifacts AND the zip** (AISEL-04 / `ResultContractTests`), seeded OFF in both SQLite + Postgres SQL — copy the Phase-77 contiguous-suppressible-block pattern, add a per-surface parity test.
   - **ADR-0001 variant parity** — hand-edit all three variants (the Gemini omission is the classic miss), no shared helper, add a 3-platform parity test; don't let `/simplify` "fix" the intentional duplication.
   - **WSL/CI test-masking** — a green-looking local `dotnet test` masked Cycle 13's 2 CI failures; treat CI as authoritative (push-and-watch), build the test projects, run targeted `--filter`.
   - **No second sim / no extra fetch** — thread the single existing 20k-trial pass and the cached `CommanderSpellbookResult`; a second pass is a real latency/RAM hit on the 512MB tier.
   - **Format-gate carve-outs** — never let an editor re-indent raw-string prompt literals (changes the bytes shipped to the AI) or convert `{ get; init; }`->get-only (breaks STJ on combo records); touch only intended lines.

## Implications for Roadmap

Based on combined research, three feature-phases (the streams reference them as Phases 79 / 80 / 81), each a paste-artifact section + view readout, each its own `analysis.*` flag seeded OFF. **There is a build-order divergence the roadmapper must resolve** (see below), and **two unresolved design decisions that requirements/roadmap must settle before the affected phase is planned.**

### Phase A: Interaction & Answers Audit (flag `analysis.interaction-audit`)
**Rationale:** Lowest risk and strongest precedent — purely additive over the most-exercised groove (Pattern 1, Phase 77, exact same files: `DeckStatClassifier` + `DeckStatSummary` + `DeckStatAggregator`), no external call, raw inputs (`Interaction`/`Counters`/`Wipes`) already exist. Establishes the repeatable "new block param through 3 variants + new flag" recipe end-to-end. (Architecture recommends this **first**; Features ranks it P1 but second-by-risk — see divergence.)
**Delivers:** Bucketed interaction counts (targeted removal / counters / wipes / protection / stax) + gap-flags synthesis + a per-AI paste section + view readout.
**Addresses:** Bucketed counts, gap flags, protection-as-own-bucket, coverage-gap synthesis (FEATURES P1/P2).
**Avoids:** Pitfall 1 — must show card lists + "verify" framing + a confidence tier; extend the shared classifier, not a forked `Contains` chain.

### Phase B: Win-Condition & Combo Map (flag `analysis.win-con-map`)
**Rationale:** Reuses Phase A's block recipe and the **already-wired, already-widened** combo fetch; independent of Phase A's output. Medium risk is only combo-null handling (precedent exists).
**Delivers:** Win lines named + grouped, combo redundancy count, assembly-turn **band**, "how this deck wins" narrative; reuses `comboResult` (no second fetch) + `IsClosingPowerCard`.
**Uses:** `ICommanderSpellbookService` `IncludedCombos`/`AlmostIncludedCombos` + `Popularity`/`ManaValueNeeded` (currently captured-but-unused).
**Avoids:** Pitfall 2 — rank by `ManaValueNeeded`/`Popularity`, separate included vs almost, disclose null as "unavailable" not "no win conditions," never a hard turn number.

### Phase C: Opening-Hand / Mulligan Evaluator (flag TBD)
**Rationale:** Additive-sim-field half is trivial (TAP-02 clone, Pattern 3), but routing is the milestone's real architectural choice and depends on no other feature's output. Sequence **last** so the cross-pipeline decision is made deliberately.
**Delivers:** Keepable-hand % + color/curve read as a consistency signal (band, not advice), surfaced from the single existing London-mulligan sim pass.
**Implements:** Additive `KeepableHandTrials` (+ optional color-screw counter) on `CardCastability`, aggregated like `ComputeTapAnalysis`.
**Avoids:** Pitfalls 3 & 7 — reuse the sim's `LondonMulligan`/`ColorKeepCap` (one keep rule), no second `Simulate` pass.

### Phase Ordering Rationale

- **Build-order divergence to resolve:** **Architecture recommends interaction-first** (strongest precedent, establishes the block recipe, lowest risk, fastest to green). **Features recommends mulligan-first by risk** (the sim already makes the keep decision internally, so it is the lowest-*compute*-risk readout). Both agree the mulligan *routing* is the hardest open question. Recommendation for the roadmapper: do **interaction-first** to lock the repeatable block-through-3-variants recipe on the safest surface, then win-con (reuses the recipe + wired combo fetch), then mulligan last so its routing decision is made deliberately — but the divergence is explicitly flagged for the roadmapper to settle.
- Features A & B are mutually independent and both slot into the deck-analysis score-block groove; doing A first de-risks B's plumbing. No feature consumes another's output, so all three can be parallelized at the Core layer if needed.
- All three are gated OFF and byte-identical, so order does not affect the OFF-state contract.

### Two Unresolved Roadmap Decisions (requirements/roadmap must settle)

1. **Mulligan routing — 3a vs 3b (affects Phase C):**
   - **3a (cheap, recommended first cut):** surface the mulligan metric on **`/manabase`** (`Manabase.cshtml` + its paste artifact), mirroring TAP-01/02. The sim, classify, flag plumbing, and view all already exist there — Pattern-3-only, fraction of the risk, promotable later.
   - **3b (expensive):** surface it as a discrete metric inside **`/deck-analysis`**. Matches the milestone framing ("a discrete deck-eval metric") but requires bridging `ManabaseClassifier` (text->`ManabaseDeck`) + the 20k-trial sim into `DeckAnalysisPacketService` (which today does neither) — a new dependency, new Scryfall-fact dependency, and heavier per-request cost on the 512MB Render tier.
   - *Trade-off:* 3a delivers the metric cheaply and safely but on a different page than the rest of Cycle 14; 3b puts it where the milestone implies but adds per-request sim cost and the cross-pipeline bridge. **Decide before Phase C is planned.**

2. **`manaValueNeeded` capture — MV-guessed vs Spellbook-grounded (affects Phase B):**
   - **MV-guessed (no parser change):** derive the assembly-turn band from combined piece MV + tutor/ramp density. Ships sooner, no parser touch.
   - **Spellbook-grounded (small parser change first):** capture the already-parsed-but-dropped `SpellbookCombo.ManaValueNeeded` so the band is grounded in Spellbook's needed mana rather than guessed. The field is parsed and discarded today (known backlog: SpellbookCombo ranking fields); wiring it is low-cost and also feeds combo ranking (Pitfall 2).
   - *Trade-off:* Spellbook-grounded sharpens both the assembly band and the combo ranking for a small parser change; MV-guessed avoids the parser touch but leaves the documented follow-up open and the band weaker. **Decide before Phase B is planned** (the ranking wiring in Pitfall 2 leans toward capturing it).

### Research Flags

Phases likely needing a planning-time decision (not deeper external research — the codebase is fully mapped):
- **Phase C (mulligan):** needs the **3a-vs-3b routing decision** resolved before planning; it is the milestone's one genuine cross-pipeline architectural choice.
- **Phase B (combo map):** needs the **`manaValueNeeded` capture decision** (parser change yes/no) resolved before planning; affects both assembly band and ranking.

Phases with standard, well-documented in-repo patterns (skip research-phase):
- **Phase A (interaction):** exact Phase-77 precedent, same files, purely additive.
- **Phase B (combo map):** block recipe from A + already-wired combo fetch; only combo-null handling has nuance (precedent exists).
- **Phase C additive-sim half:** TAP-02 clone; only the routing wrapper is open.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Zero-new-deps verified against every `.csproj`; every input source read directly (classifier, Spellbook service, sim, Hypergeometric, Scryfall DTOs incl. `keywords`). |
| Features | HIGH | Interaction taxonomy + win-line archetypes + mulligan keep heuristics grounded in current Commander/cEDH sources; engine dependencies verified in-worktree. |
| Architecture | HIGH | Every integration point read with file:line citations; the two-pipeline boundary and three reuse patterns (Phase 77 / ADR-0001 / TAP-02) confirmed in source. |
| Pitfalls | HIGH | Grounded in this repo's source + tests (`AnalysisScorePromptParityTests`, `ResultContractTests`, seed-consistency tests, `CarveOutGuardTests`) and the Cycle-13 CI-masking precedent. |

**Overall confidence:** HIGH — this is a brownfield extension fully mapped against the live worktree; the only open items are two design decisions and one build-order preference, all surfaced explicitly for the roadmapper.

### Gaps to Address

- **Mulligan routing (3a vs 3b):** the milestone framing leans 3b ("discrete deck-eval metric") but 3a is far lower risk on the 512MB tier; resolve at requirements/roadmap before Phase C planning.
- **`manaValueNeeded` capture:** decide MV-guessed vs Spellbook-grounded before Phase B; capturing the field is low-cost and also satisfies the ranking pitfall, but it is a (small) parser change to scope.
- **Build-order divergence:** Features (mulligan-first by risk) vs Architecture (interaction-first by groove) — roadmapper picks the sequence; recommendation is interaction -> win-con -> mulligan.
- **Stax/protection classification accuracy:** text heuristics are brittle; mitigate with a curated in-repo static keyword/name list (mirroring `bracket-data.json`, NOT a package) and golden tests, and keep stax a coarse low/med/high presence read.

## Sources

### Primary (HIGH confidence — live worktree, read directly)
- `DeckFlow.Core/Analysis/{DeckStatClassifier,DeckStatAggregator,MultiAxisScorer,MultiAxisScore}.cs` — existing predicates + `DeckStatSummary` additive-field pattern (Phase 77).
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` — `SpellbookCombo` shape incl. `Popularity`/`ManaValueNeeded` (parsed, unused), almost-combo extraction, `MaxIncluded`=20, null-graceful, 30-min cache.
- `DeckFlow.Core/Manabase/{CastabilitySimulator,Hypergeometric,ManabaseModels,ManabaseAnalyzer}.cs` — London mulligan + `ColorKeepCap`, closed-form `AtLeast`, TAP-02 `Turn1UntappedTrials` additive-counter precedent.
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` + `PromptBuilders/Analysis/*` — score-block groove, trailing-optional-param threading, `comboTask` reuse, `Snapshot().TryGetValue` flag gate.
- `DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs` — confirms `keywords`/`oracle_text`/`type_line`/`produced_mana`/`color_identity`/`mana_cost` hydrated.
- `DeckFlow.Web/Services/FeatureFlags/*`, `FeatureFlagStore` dual-dialect seed SQL; `docs/decisions/0001-prompt-variants-decoupled.md` (ADR-0001).
- Repo tests: `AnalysisScorePromptParityTests`, `ResultContractTests`, `ToolFlagSeedConsistencyTests` / `ToolFlagPostgresSeedTests`, `CarveOutGuardTests`, `Integration/PostgresFactAttribute` (WSL masking).

### Secondary (MEDIUM-HIGH — domain grounding)
- Commander's Herald (counterspell/removal taxonomy), EDHREC cEDH stax guide, TCGplayer cEDH intro — interaction buckets.
- Draftsim cEDH win-conditions, Learn cEDH "How Many Combos Are Too Many", Laboratory Maniacs — win-line archetypes + redundancy.
- Draftsim / MTG EDH / EDHREC mulligan guides — keepable-hand heuristics.

### Tertiary (project context)
- `.planning/PROJECT.md` (Cycle 14 scope, byte-identical-OFF, Gemini paste ceiling), `CLAUDE.md` + global instructions (no-new-deps, VSTest-unreliable-in-WSL, format-gate carve-outs, 512MB/256mb caps), MEMORY (SpellbookCombo ranking-fields follow-up, Cycle-13 2-CI-failures).

---
*Research completed: 2026-06-30*
*Ready for roadmap: yes*
