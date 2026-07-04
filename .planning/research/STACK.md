# Stack Research

**Domain:** Cycle 14 — Deeper Deck Evaluation (Interaction & Answers Audit, Win-Condition & Combo Map, Opening-Hand / Mulligan Evaluator) layered on the existing ASP.NET 10 + Razor MTG paste-artifact engine
**Researched:** 2026-06-30
**Confidence:** HIGH (verified directly against the live codebase — classifier, aggregator, Commander Spellbook service, Monte-Carlo sim, Hypergeometric helper, and Scryfall DTOs all read in `deckflow-cycle14`)

---

## Verdict

**ZERO new dependencies. Zero new NuGet packages, zero new npm packages.**

All three Cycle 14 features are fully covered by data and engines already in the codebase. Every input — Scryfall card data (oracle_text, type_line, **keywords**, mana_cost, produced_mana, color_identity), Commander Spellbook combo results, and the seeded Monte-Carlo `CastabilitySimulator` with a built-in London mulligan — is already hydrated and flowing through `DeckFlow.Core` / `DeckFlow.Web`. The genuinely-needed additions are **in-codebase C# code** (new pure classifier predicates, new projection records, one new metric pass), not packages.

This honors the project's no-new-deps rule (CLAUDE.md: "No new packages... without asking the user first"; project CLAUDE.md pins the stack) with no exception required. Do **not** add a stats library, a local combo database, an MTG rules engine, or any package this milestone.

---

## Feature-by-Feature Coverage (the load-bearing answer)

### Feature 1 — Interaction & Answers Audit — COVERED by existing card data + classifier

**Already present (verified):**
- `DeckStatClassifier` (`DeckFlow.Core/Analysis/DeckStatClassifier.cs`) already has `IsInteractionCard`, `IsBoardWipeCard`, `IsCounterspellCard`, `IsRecursionCard`, `IsTutorCard` — all pure functions over Scryfall `type_line` + `oracle_text`.
- `DeckStatSummary` (`DeckStatAggregator.cs`) already exposes `Interaction`, `Wipes`, `Counters`, `Recursion`, `Tutors` tallies.
- The source fields are already hydrated: `DeckStatCardInput` carries `TypeLine`, `OracleText`, `ManaCost`; the `ScryfallCard` DTO (`Services/Scryfall/ScryfallDtos.cs`) deserializes **`keywords`** (e.g. "Ward", "Hexproof", "Flash"), `produced_mana`, and `color_identity`.

**In-codebase additions needed (NOT packages):**
- Finer interaction categories the current summary lumps together — **targeted/spot removal** vs board wipes (have) vs counterspells (have) vs **stax** vs **protection / answers-to-removal**. These are NEW pure predicates in `DeckStatClassifier` (e.g. `IsTargetedRemovalCard`, `IsStaxCard`, `IsProtectionCard`) + NEW init-property tally fields on `DeckStatSummary`, plus a coverage-gap rollup ("no enchantment removal", "0 graveyard hate", etc.).

**Why no package:** the inputs (oracle text, type line, keywords) are already fetched. Categorization is the same string/keyword-heuristic technique the file already uses. No library can supply judgment the oracle text already encodes.

### Feature 2 — Win-Condition & Combo Map — COVERED by existing Commander Spellbook integration

**Already present (verified):**
- `ICommanderSpellbookService.FindCombosAsync` (`Services/CommanderSpellbookService.cs`) returns `IncludedCombos` — each `SpellbookCombo(CardNames, Results, Instructions, Popularity, ManaValueNeeded)` — and `AlmostIncludedCombos` (exactly one-card-away). `ManaValueNeeded` and `Popularity` are already parsed (defensive `TryGetInt32` → null-graceful).
- Result is already cached 30 min in `IMemoryCache`, keyed by commander + main list, and consumed by `DeckAnalysisPacketService`, the bracket classifier, and `MultiAxisScorer`.
- `DeckStatClassifier.IsClosingPowerCard` already flags non-combo win lines ("you win the game", extra turns, overrun/Craterhoof, combat-damage draw engines).

**In-codebase additions needed (NOT packages):**
- A projection record (e.g. `WinConditionMap`) + a Core/Web method composing: combo list → **redundancy** (combos sharing pieces; near-combo count as backup lines) + **assembly-turn read** (derive from `ManaValueNeeded` already captured, optionally cross-referenced with the castability sim's per-card effective turn) + closing-power cards as alternative win lines. Then three paste sections + a view readout.

**Why no package:** Commander Spellbook IS the combo authority and is already wired with Polly resilience + caching + graceful null-on-failure. No local combo DB or rules engine needed — the "deeper use" is reading fields (`ManaValueNeeded`, `Popularity`, near-combos) the parser already extracts but the analysis prompt does not yet surface as a map.

### Feature 3 — Opening-Hand / Mulligan Evaluator — COVERED by existing Monte-Carlo + Hypergeometric

**Already present (verified):**
- `CastabilitySimulator` (`DeckFlow.Core/Manabase/CastabilitySimulator.cs`) runs a seeded London mulligan every trial (`LondonMulligan`): land-count keep bands that widen with avg MV, the Commander free first mulligan, an optional color-aware keep gate (`ColorKeepSatisfied`), and London choose-and-bottom. 20k trials per spell.
- `Hypergeometric.AtLeast(population, successes, draws, atLeast)` (`Manabase/Hypergeometric.cs`) computes closed-form "P(≥N lands in the opening 7)" in log-space (overflow-safe to 512 cards).
- Per-card mana cost / color identity for the color & curve read already live in `CardFact` / `SpellRequirement` (`Pips` by `ManaColor`, `ManaValue`).

**In-codebase additions needed (NOT packages):**
- A deck-level **keepable-hand probability** metric + a `KeepableHandSummary` record. Two viable in-codebase routes (a roadmap design choice, not a stack choice):
  - **(a) Closed-form** via the existing `Hypergeometric` for the land-count keep band — cheap, deterministic, RAM-friendly (fits the 512 MB Render cap).
  - **(b) New deck-level sim pass** reusing the simulator's existing mulligan logic to emit a keep% over one trial set (not per-spell), plus a color/curve read off the kept hands.
- Today the sim emits only per-spell `CastPercent` + `Turn1UntappedTrials`; the gap is an **exposed deck-level metric**, not a missing engine.

**Why no package:** the mulligan logic and the hypergeometric math already exist and are unit-tested (see `ColorAwareMulliganTests`, `CastabilitySimulatorTests`). A probability/stats NuGet would duplicate `Hypergeometric.cs`.

---

## Recommended Stack (all already in place — no version changes)

### Core Technologies

| Technology | Version | Purpose | Why It Already Covers Cycle 14 |
|------------|---------|---------|--------------------------------|
| .NET / C# | net10.0 / C# 12 | All server logic | Pure-CPU classifiers + sim live in `DeckFlow.Core`; new predicates/records/metrics are more of the same. No runtime change. |
| Scryfall card data (`ScryfallCard` DTO → `CardFact` / `DeckStatCardInput`) | live API | oracle_text, type_line, **keywords**, mana_cost, produced_mana, color_identity, layout, power | Already deserialized (incl. `keywords`) and mapped. Interaction categorization reads these same fields — no new fetch surface. |
| `ICommanderSpellbookService` (RestSharp 114 + Polly 8) | existing | Combo enumeration via `backend.commanderspellbook.com/find-my-combos` | Returns `IncludedCombos` (+`Popularity`,`ManaValueNeeded`) and `AlmostIncludedCombos`. Combo map is a projection over data already fetched + cached. |
| `CastabilitySimulator` (Monte-Carlo, seeded) | existing | Opening-hand modeling | Plays a full London mulligan per trial. Mulligan evaluator is a readout off this exact loop. |
| `Hypergeometric` (hand-rolled, log-space) | existing | Closed-form opening-hand land/color probability | `AtLeast` already gives P(≥N lands). Lean, deterministic land-count keep dimension. |
| `DeckStatClassifier` / `DeckStatAggregator` | existing | Card-signal predicates + role tallies | Interaction audit = more predicates + more `DeckStatSummary` fields here. |
| `MultiAxisScorer` (Cycle 13) | existing | Consumes the same combo + stat signals | Confirms the plumbing the three features need is already assembled. New sections sit beside it. |

### Supporting Libraries (already present — use, do not add)

| Library | Version | Purpose | Use For |
|---------|---------|---------|---------|
| Polly | 8.x | `"spellbook"` named resilience pipeline | Wraps the combo POST; combo-map reuses unchanged |
| Microsoft.Extensions.Caching.Memory | built-in | 30-min combo + session cache | Combo map rides the existing cache |
| `IFeatureFlagCache` (`Services/FeatureFlags/`) | in-repo | Flag gating | Each feature gets its own `analysis.*` flag, seeded OFF, byte-identical when off — same pattern as `analysis.multi-axis-score` |
| Prompt-variant registries (ChatGpt/Claude/Gemini) | in-repo | Per-AI artifact rendering | New paste sections render in all three variants WITHOUT a shared helper (ADR-0001) |

### Development Tools (no change)

| Tool | Purpose | Notes |
|------|---------|-------|
| xUnit 2.9.x | Core + Web tests | New classifier predicates + the mulligan metric are pure functions — high-value golden tests in `DeckFlow.Core.Tests` |
| Vitest + Playwright | TS unit + e2e | Only if a view readout adds client TS; otherwise N/A |
| `dotnet build` clean + push-and-watch CI | Gate | VSTest unreliable in WSL (per constraints) |

---

## Installation

```bash
# Nothing to install. No NuGet, npm, or other package is added this milestone.
# All capabilities already restore with the existing solution.
dotnet build DeckFlow.sln
```

---

## In-Codebase Additions Required (summary — code, not dependencies)

| Addition | Location | Kind |
|----------|----------|------|
| New interaction sub-category predicates (targeted removal, stax, protection) | `DeckFlow.Core/Analysis/DeckStatClassifier.cs` | Pure static methods |
| New tally fields + coverage-gap rollup | `DeckFlow.Core/Analysis/DeckStatAggregator.cs` (`DeckStatSummary`) | Record init-fields + counting |
| Win-condition / combo-map projection (redundancy + assembly-turn) | new Core record + Web composition over `CommanderSpellbookResult` | Projection over fetched data |
| Keepable-hand metric (`KeepableHandSummary`) | Core: reuse `Hypergeometric` and/or a deck-level sim pass | New metric off existing math |
| 3 flag keys (`analysis.*`), seeded OFF | `Services/FeatureFlags` + flag seed | Config rows |
| 3 paste-artifact sections per AI + view readouts | prompt variant registries + Razor views | Rendering (no shared helper, ADR-0001) |
| (Optional, in-codebase) curated stax/protection name or keyword list | static JSON like existing `DeckFlow.Web/Data/bracket-data.json` | Versioned data file, NOT a package |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| MathNet.Numerics / Accord.Statistics (or any stats/probability NuGet) | Duplicates the already hand-rolled, overflow-safe `Hypergeometric` (log-factorial) and the seeded Monte-Carlo sim. New dep, RAM cost, zero new capability. | `DeckFlow.Core/Manabase/Hypergeometric.cs` + `CastabilitySimulator` |
| A local combo database / bundled combo dataset | Commander Spellbook is the live authority, already integrated with Polly + 30-min cache + graceful null-on-failure. A bundled DB goes stale and re-solves a solved problem. | Existing `ICommanderSpellbookService` (`IncludedCombos` + `AlmostIncludedCombos` + `ManaValueNeeded`) |
| An MTG rules engine | Massive dependency to replace deterministic substring/field heuristics the classifier already uses. | Existing `DeckStatClassifier` predicates over Scryfall fields |
| An NLP / semantic oracle-text parser to classify interaction | Over-engineered, non-deterministic, heavy; the existing classifier is deterministic + testable. | New pure predicates over existing oracle_text + **keywords** |
| A shared cross-AI prompt helper for the new sections | Violates ADR-0001 (prompt variants intentionally decoupled). | Hand-render in each ChatGpt/Claude/Gemini variant |
| `Microsoft.Extensions.Http.Resilience` standard handler | Project constraint forbids it; existing RestSharp + direct Polly v8 is the pattern. | Existing `ResiliencePipelineProvider<string>` named pipelines |
| An ORM (EF Core, Dapper) for any new field | Ruled out by project patterns; `RelationalDatabaseConnection` + raw SQL suffices. New metrics here are compute-only and need no persistence at all. | Compute at request time; persist nothing new |

---

## Genuinely-Needed Addition? — None at the dependency level

No capability among the three features fails to be covered by a current in-codebase source:

- Interaction categorization → Scryfall `oracle_text` + `type_line` + `keywords` (already hydrated into `DeckStatCardInput`).
- Combo enumeration + redundancy + assembly turn → Commander Spellbook `IncludedCombos`/`AlmostIncludedCombos`/`ManaValueNeeded` (already fetched + cached).
- Mulligan keep probability + color/curve read → `CastabilitySimulator` London mulligan + `Hypergeometric` + existing per-card color/MV data.

The only honest caveat is a **methodology pitfall, not a stack gap:** stax/protection detection by oracle-text heuristics is brittle (the existing classifier already shows this — see the `IsTutorCard` "nonland card" carve-out gymnastics needed to avoid a false land-fetch match). The correct mitigation is an **in-codebase** curated keyword/name list as a versioned static data file (mirroring `DeckFlow.Web/Data/bracket-data.json`), not a new dependency. Flag this for requirements/roadmap as a classification-accuracy risk to design golden tests around — see PITFALLS.

---

## Version Compatibility

| Component | State | Notes |
|-----------|-------|-------|
| .NET 10 / RestSharp 114 / Polly 8 / Npgsql 10 / Microsoft.Data.Sqlite 10 | Pinned, unchanged | No bumps required; Cycle 14 adds no package surface |
| Scryfall `keywords` field | Confirmed present in `ScryfallCard` DTO | Available today for interaction keyword tags (Ward/Hexproof/Flash) |
| Commander Spellbook `manaValueNeeded` / `popularity` | Confirmed parsed into `SpellbookCombo` | Available today for assembly-turn / redundancy read |

---

## Sources

- `DeckFlow.Core/Analysis/DeckStatClassifier.cs`, `DeckStatAggregator.cs` — existing interaction/role predicates + `DeckStatSummary` fields incl. `Interaction`/`Wipes`/`Counters`/`Tutors`/`Recursion` (HIGH, read directly)
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` — `SpellbookCombo`/`SpellbookAlmostCombo` shape incl. `Popularity`/`ManaValueNeeded`, near-combo (one-card-away) extraction, 30-min cache (HIGH, read directly)
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — `LondonMulligan` keep bands + Commander free-mull + color-aware keep + per-trial bottoming; emits `CastPercent` + `Turn1UntappedTrials` (HIGH, read directly)
- `DeckFlow.Core/Manabase/Hypergeometric.cs` — closed-form `AtLeast` land-count probability, log-space (HIGH, read directly)
- `DeckFlow.Core/Manabase/ManabaseModels.cs` — `SpellRequirement.Pips` by `ManaColor`, `CardCastability`, `ManabaseDeck` (HIGH, read directly)
- `DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs` — confirms `keywords`, `oracle_text`, `type_line`, `produced_mana`, `color_identity`, `mana_cost` hydrated (HIGH, read directly)
- `DeckFlow.Core/Analysis/MultiAxisScorer.cs`, `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — confirms stats + combos + score signal plumbing already assembled and flag-gated via `IFeatureFlagCache` (HIGH, read directly)
- `DeckFlow.Web/Data/bracket-data.json` — established in-repo static-data-file pattern for curated lists (HIGH, located)
- Confirmed no stats/math NuGet present (grep of all `.csproj`) — `Hypergeometric` is hand-rolled (HIGH)
- CLAUDE.md "Dependency additions" + project CLAUDE.md pinned-stack constraints — no-new-deps rule (HIGH)

---
*Stack research for: DeckFlow Cycle 14 — Deeper Deck Evaluation (interaction audit, combo map, mulligan evaluator)*
*Researched: 2026-06-30*
