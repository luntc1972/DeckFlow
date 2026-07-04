# Architecture Research

**Domain:** Brownfield integration — 3 deck-eval read dimensions into DeckFlow (ASP.NET 10 + Razor; `DeckFlow.Core` pure-domain lib + `DeckFlow.Web` services/controllers/views)
**Researched:** 2026-06-30
**Confidence:** HIGH (every integration point read from the live `deckflow-cycle14` worktree; file paths + line numbers cited)

> Cycle 14 features: (1) Interaction & answers audit, (2) Win-condition & combo map, (3) Opening-hand / mulligan evaluator. All build on infrastructure shipped in Cycle 13 (`2026.06.10`): `DeckStatClassifier`/`MultiAxisScorer` (Phase 77), `CommanderSpellbookService`, the `CastabilitySimulator` Tap Analyzer (Phase 75).

## System Overview — where the 3 features attach

```
┌──────────────────────────────────────────────────────────────────────────┐
│  DeckFlow.Web (MVC)                                                        │
│  ┌──────────────────────┐   ┌───────────────────────┐                     │
│  │ DeckPacketController  │   │ ManabaseController     │  (SEPARATE tools)  │
│  └──────────┬───────────┘   └──────────┬────────────┘                     │
│             │                          │                                   │
│  ┌──────────▼───────────────┐  ┌───────▼──────────────────┐               │
│  │ DeckAnalysisPacketService │  │ ManabaseAnalysisService  │               │
│  │  - combo fetch (widened)  │  │  - classify -> sim       │               │
│  │  - score-block groove     │  │  - flag: tap-analyzer    │               │
│  │  - flag snapshot gates    │  └───────┬──────────────────┘               │
│  └──────────┬───────────────┘          │                                  │
│             │ Build(...) + new block-text params                          │
│  ┌──────────▼───────────────────────────────────────────┐                │
│  │ AnalysisPromptVariantRegistry  -> 3 DECOUPLED variants │  (ADR-0001)    │
│  │   ChatGpt / Claude / Gemini AnalysisPromptVariant.cs   │                │
│  └───────────────────────────────────────────────────────┘                │
├──────────────────────────────────────────────────────────────────────────┤
│  DeckFlow.Core (pure CPU domain — no HTTP/DI)                              │
│  ┌─────────────────────┐ ┌──────────────────────┐ ┌────────────────────┐  │
│  │ Analysis/            │ │ Manabase/             │ │ Models/             │ │
│  │  DeckStatClassifier  │ │  CastabilitySimulator │ │  DeckEntry          │ │
│  │  DeckStatAggregator  │ │  ManabaseAnalyzer     │ │                     │ │
│  │  MultiAxisScorer     │ │  CardCastability      │ │                     │ │
│  └─────────────────────┘ └──────────────────────┘ └────────────────────┘  │
├──────────────────────────────────────────────────────────────────────────┤
│  External: Commander Spellbook (combo) · Scryfall (card data) · Postgres  │
│            FeatureFlagStore (seeded OFF) + FeatureFlagCache (Snapshot)     │
└──────────────────────────────────────────────────────────────────────────┘
```

**The single most important structural fact:** `/deck-analysis` (`DeckAnalysisPacketService`) and `/manabase` (`ManabaseAnalysisService`) are **two independent pipelines**. The packet service never builds a `ManabaseDeck` and never calls the Monte-Carlo simulator (verified — zero `ManabaseDeck`/`ManabaseAnalyzer` references in `DeckAnalysisPacketService.cs`; manabase classify→sim lives only behind `ManabaseController`). This boundary is what makes **Feature 3 (mulligan evaluator) the highest-lift** of the three: the sim it wants to reuse lives behind the manabase tool, not the deck-analysis tool.

## Component Responsibilities (existing — the attach points)

| Component | Responsibility | File (cite) |
|-----------|----------------|-------------|
| `DeckStatClassifier` | Pure static role predicates (`IsRampCard`, `IsInteractionCard`, `IsBoardWipeCard`, `IsCounterspellCard`, `IsTutorCard`, `IsClosingPowerCard`…) | `DeckFlow.Core/Analysis/DeckStatClassifier.cs` |
| `DeckStatSummary` / `DeckStatAggregator` | Immutable tally record + the `Compute(...)` loop that calls every predicate | `DeckFlow.Core/Analysis/DeckStatAggregator.cs` |
| `MultiAxisScorer` / `DeckMultiAxisScore` | Pure transform: `DeckStatSummary` + bracket signals → 4 bands + rationale | `DeckFlow.Core/Analysis/MultiAxisScorer.cs`, `MultiAxisScore.cs` |
| `DeckAnalysisPacketService` | Orchestrates load→Scryfall→combo→score→prompt; owns flag gates + block-text builders | `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` |
| `IAnalysisPromptVariant` + registry | 3 decoupled platform variants; each `Build(...)` hand-renders blocks | `DeckFlow.Web/Services/PromptBuilders/Analysis/*` |
| `CommanderSpellbookService` | `FindCombosAsync` → `CommanderSpellbookResult`; combos already carry `Popularity`, `ManaValueNeeded` | `DeckFlow.Web/Services/CommanderSpellbookService.cs` |
| `CastabilitySimulator` | Seeded Monte-Carlo per-spell; single pass already emits `Turn1UntappedTrials` | `DeckFlow.Core/Manabase/CastabilitySimulator.cs` |
| `CardCastability` / `ManabaseTapAnalysis` | Per-spell result record + the deck-level aggregate (TAP-02 precedent) | `DeckFlow.Core/Manabase/ManabaseModels.cs` |
| `FeatureFlagStore` / `IFeatureFlagCache` | Seeds keys OFF (Pg + SQLite SQL); `Snapshot().TryGetValue` is the byte-identical-OFF gate | `DeckFlow.Web/Services/FeatureFlags/*` |

## The 3 established integration patterns to reuse

### Pattern 1: New Core signal predicate + additive `{ get; init; }` summary field (Phase 77 precedent)

**What:** Cycle-13 Phase 77 added **4** predicates to `DeckStatClassifier` (`IsTutorCard`, `IsFastManaCard`, `IsRampOrDrawUnderThreeMv`, `IsCounterspellCard`) and **4 additive `{ get; init; }` fields** to `DeckStatSummary` (`Tutors`, `FastMana`, `RampDrawUnderThreeMv`, `Counters` — `DeckStatAggregator.cs:41-52`). Each predicate is tallied inside the single `DeckStatAggregator.Compute` loop (`:175-193`) and the new fields set in the object initializer (`:210-215`).

**When to use:** Feature 1 (interaction audit) — add `IsRemovalCard`/`IsStaxCard`/`IsProtectionCard`/… predicates + additive `SpotRemoval`/`Stax`/`Protection`/… fields the SAME way. `Counters`, `Wipes`, `Interaction` already exist and are directly reusable.

**Trade-offs:** Additive fields use the object-initializer (not the positional ctor), so existing `Compute` callers and `System.Text.Json` round-trips are unaffected — exactly why the pattern is low-risk. (CLAUDE.md carve-out: keep them `{ get; init; }`, never `required`, never get-only — STJ silently drops get-only positional members.)

**Example (the groove to copy):**
```csharp
// DeckStatSummary — append, never reorder the positional ctor
public int SpotRemoval { get; init; }
public int Stax { get; init; }
// DeckStatAggregator.Compute — one more if() in the existing foreach
if (DeckStatClassifier.IsStaxCard(typeLine, oracleText)) { stax += quantity; }
```

### Pattern 2: Pre-built block text threaded through all 3 decoupled variants (multi-axis-score precedent, ADR-0001)

**What:** The multi-axis score is rendered to ASCII once in `DeckAnalysisPacketService.BuildScoreBlockText` (`:1287`), passed as a **trailing optional `string? scoreBlockText = null`** down the whole chain: `BuildAnalysisPrompt` (`:1271`) → `AnalysisPromptVariantRegistry.Build` → `IAnalysisPromptVariant.Build` → each of the 3 variants. Each variant **hand-edits** its own insertion (`ChatGptAnalysisPromptVariant.cs:94-98`: `if (!string.IsNullOrWhiteSpace(scoreBlockText)) { builder.AppendLine(); builder.AppendLine(scoreBlockText); }`). ADR-0001 forbids a shared helper — the same insertion is duplicated, intentionally, in Claude and Gemini variants.

**When to use:** Features 1 and 2 both produce a new block. Add `string? interactionBlockText = null` and `string? winConBlockText = null` as further trailing optional params on the SAME 5 signatures (interface, registry, `BuildAnalysisPrompt`, 3 variants). Each new block gets its own `if (!IsNullOrWhiteSpace) { AppendLine }` hand-written into all 3 variants.

**Trade-offs:** Up to 3 hand-edits per block; reviewer must confirm all 3 variants got it (ADR-0001 §Consequences). Byte-identical-OFF is automatic: the param defaults null and the builder runs only inside the flag-gated branch.

### Pattern 3: Additive sim field, single pass, deck-level aggregate (TAP-02 precedent)

**What:** TAP-02 added `Turn1UntappedTrials` (additive `int { get; init; }`) to `CardCastability` (`ManabaseModels.cs:176`). It is counted **inside the existing per-trial loop** of `CastabilitySimulator.Simulate` — a "1-bit observation, no second sim, no RNG draw, determinism preserved" (`CastabilitySimulator.cs:252-255, 610-613`). It is then aggregated across rows by `ManabaseAnalyzer.ComputeTapAnalysis` (`ManabaseAnalyzer.cs:829-878`) into `ManabaseTapAnalysis.Turn1UntappedPercent`, hung off `ManabaseReport.TapAnalysis` (additive, defaults null — `ManabaseModels.cs:877`), and gated by `analysis.manabase.tap-analyzer` in `ManabaseAnalysisService` via `IsFlagOn` (`:189, 331-336`).

**When to use:** Feature 3 (mulligan evaluator). The keepable-hand probability is already computed inside `LondonMulligan` (`CastabilitySimulator.cs:1172-1232`) — a non-forced keep IS a "kept hand." Add `KeepableHandTrials` / an opening-color-screw counter as additive `CardCastability` fields counted in the same loop, then aggregate exactly like `ComputeTapAnalysis`. The mulligan decision is **spell-independent** (depends only on land bands + deck colors via `OpeningLandColorMask`/`ColorKeepSatisfied`), so any non-commander row is representative — averaging across rows (the TAP approach) is fine.

**Trade-offs:** The additive-field half is trivial and proven. The **routing half is the hard part** — see Anti-Pattern 1 and Build Order.

## Per-feature integration map (NEW vs MODIFIED)

### Feature 1 — Interaction & answers audit

| Layer | NEW | MODIFIED |
|-------|-----|----------|
| Core | `InteractionAudit` record + (optional) `InteractionAuditor` static (counts → coverage-gap flags), mirroring `MultiAxisScorer` | `DeckStatClassifier` (+`IsRemovalCard`/`IsStaxCard`/`IsProtectionCard`/…); `DeckStatSummary`/`DeckStatAggregator` (+additive fields, +tallies) |
| Web service | `BuildInteractionBlockText(...)` in `DeckAnalysisPacketService` | `BuildAsync` — compute under a new `interactionEnabled` snapshot gate, reusing `cardReferenceBundle.CardReferences` (same input prep as `BuildDeckStatsText`/score at `:715-717`); `BuildAnalysisPrompt` (+param) |
| Prompt variants | — | All 3 `*AnalysisPromptVariant.cs` (+`interactionBlockText` param + hand-written insertion) + `IAnalysisPromptVariant` + `AnalysisPromptVariantRegistry` |
| View/flag | flag `analysis.interaction-audit` (seed OFF in `FeatureFlagStore` Pg+SQLite SQL `:226-266`; description in `FeatureFlagCatalog`) | `DeckAnalysis.cshtml` (+block gated on new `Model.<field>`); `DeckAnalysisViewModel` (+field); CSS in `site-common.css`; round-trip via a hidden field IFF the Step-3 early-return must restore it (mirror `ScoreJson`, `DeckAnalysisRequest.cs:140` + the `TryDeserializeScore` validation pattern at `:1310`) |

**Lowest risk.** Purely additive over the most-exercised groove; no new external call; raw inputs (`Interaction`, `Counters`, `Wipes`) already exist.

### Feature 2 — Win-condition & combo map

| Layer | NEW | MODIFIED |
|-------|-----|----------|
| Core | `WinConMap`/`ComboMap` record + builder (win lines, redundancy count, assembly-turn read); combines combo data + `DeckStatClassifier.IsClosingPowerCard` | possibly +`DeckScoreRationale`-style sub-records |
| Web service | `BuildWinConBlockText(...)` | `BuildAsync` — the combo fetch is **already wired and already widened** for the score (`comboTask`, `:650-652`); reuse `comboResult` (do NOT add a 2nd fetch). `SpellbookCombo.Popularity`/`ManaValueNeeded` (`CommanderSpellbookService.cs:16-21`) are captured but currently unused → feed redundancy / assembly-turn read. `BuildAnalysisPrompt` (+param) |
| Prompt variants | — | All 3 variants + interface + registry (+`winConBlockText`) |
| View/flag | flag `analysis.win-con-map` (seed OFF) + description | `DeckAnalysis.cshtml` block + viewmodel field + CSS |

**Medium risk.** Combo data is null-graceful (`FindCombosAsync` returns null on API failure — `CommanderSpellbookService.cs:124`); the block must handle null. The `comboDetectionAvailable` distinction the scorer already makes (`DeckAnalysisPacketService.cs:714`) is the precedent for "ran-and-empty vs unavailable." No new HTTP surface. There is already a `BuildComboReferenceText` (`:999`) for the combo-question path — the win-con block is a *different, additive* rendering, not a replacement.

### Feature 3 — Opening-hand / mulligan evaluator

| Layer | NEW | MODIFIED |
|-------|-----|----------|
| Core | additive `KeepableHandTrials` (+optional opening-color-screw counter) on `CardCastability`; a `MulliganEvaluation` record (or extend `ManabaseTapAnalysis`) | `CastabilitySimulator.Simulate` (+counter in the existing loop, like TAP-02); `ManabaseAnalyzer` (+`ComputeMulligan` aggregate, like `ComputeTapAnalysis`); `ManabaseReport` (+additive nullable field) |
| Web | **the routing decision** (see below) | `ManabaseAnalysisService` (+`IsFlagOn` gate) **OR** a new manabase-classify bridge in `DeckAnalysisPacketService` |
| View/flag | flag (seed OFF) | block on `Manabase.cshtml` (cheap) and/or `DeckAnalysis.cshtml` (expensive) |

**Highest lift — the cross-pipeline boundary.** Two routing options:
- **3a (cheap, recommended first cut):** surface the mulligan metric on the **manabase tool** (`Manabase.cshtml` + its paste artifact), exactly mirroring TAP-01/02. The sim, classify, flag plumbing, and view all already exist there. This is Pattern-3-only.
- **3b (expensive):** surface it as a discrete metric inside **`/deck-analysis`**. This requires bridging `ManabaseClassifier` (text→`ManabaseDeck`) + the 20k-trial sim into `DeckAnalysisPacketService`, which today does neither — a new dependency, a new Scryfall-fact dependency, and a heavier per-request cost on the 512MB Render tier. The milestone goal ("a discrete deck-eval metric") leans 3b, but 3a delivers the metric with a fraction of the risk and can be promoted later.

## Data Flow

### Existing flag-gated block flow (the recipe Features 1 & 2 follow)
```
request -> DeckAnalysisPacketService.BuildAsync
  -> flagCache.Snapshot().TryGetValue("analysis.X", out on) && on    (byte-identical-OFF gate)
  -> compute Core record from cardReferenceBundle.CardReferences      (reuse, no new fetch)
  -> BuildXBlockText(record)  -> ASCII block string
  -> BuildAnalysisPrompt(..., xBlockText)
  -> registry.Build(platform, ..., xBlockText)
  -> variant.Build(...) { if (!IsNullOrWhiteSpace(xBlockText)) AppendLine(xBlockText); }  x3
  -> result.X (typed record) also flows to DeckAnalysisViewModel for the on-page readout
```

### Mulligan flow (Feature 3, Pattern 3)
```
CastabilitySimulator.Simulate (per spell, 20k trials)
  -> LondonMulligan already decides keepable per trial  (CastabilitySimulator.cs:1172)
  -> count KeepableHandTrials in the SAME loop          (no 2nd sim, determinism preserved)
ManabaseAnalyzer
  -> ComputeMulligan(castability rows) average across non-commander rows  (mirror ComputeTapAnalysis)
  -> ManabaseReport.MulliganEvaluation (additive, default null)
ManabaseAnalysisService.IsFlagOn("analysis.X") gates surfacing
```

## Anti-Patterns (project-specific — violating these breaks the milestone contract)

### Anti-Pattern 1: Re-running / duplicating the Monte-Carlo sim for the mulligan metric
**What people do:** Add a second simulation pass (or a fresh classify) to get the keep rate.
**Why it's wrong:** TAP-02 deliberately computes its metric as a "1-bit observation inside the existing loop … no second sim, no RNG draw, so determinism is preserved" (`CastabilitySimulator.cs:606-613`). A second pass doubles cost (matters on 512MB Render) and risks non-determinism.
**Do this instead:** Additive counter in the existing per-trial loop + a `ComputeTapAnalysis`-style aggregate.

### Anti-Pattern 2: Extracting a shared block-text helper across the 3 prompt variants
**What people do:** "DRY up" the near-identical `if (block) AppendLine` across ChatGpt/Claude/Gemini.
**Why it's wrong:** ADR-0001 explicitly forbids it; a 2026-05-27 consolidation (`a1fa5ad`) was reverted same-day (`b2ffba7`). Prompt bytes are the product; each platform must stay independently tunable.
**Do this instead:** Hand-edit all 3 variants; reviewer confirms all 3.

### Anti-Pattern 3: Gating with `IFeatureFlagCache.IsEnabled`
**What people do:** Use `IsEnabled(key)` to read the new flag.
**Why it's wrong:** `IsEnabled` defaults a **missing** key to **true** (`IFeatureFlagCache.cs` doc — default-on semantics) — an unseeded row would silently turn the feature ON, breaking byte-identical-OFF.
**Do this instead:** `flagCache.Snapshot().TryGetValue(key, out var on) && on` — the explicit pattern used by the score (`DeckAnalysisPacketService.cs:640-642`), deck-stats (`:684-686`), command-zone (`:306-309`), and manabase `IsFlagOn` (`ManabaseAnalysisService.cs:331-336`). Seed the key FALSE in BOTH `PostgresSeedSql` and `SqliteSeedSql` (`FeatureFlagStore.cs:226-266`) and add a `FeatureFlagCatalog` description (a missing description fails `FeatureFlagCatalogTests`).

### Anti-Pattern 4: Widening the packet-session cache key for new flag-gated content
**What people do:** Add the new block/flag to `DeckAnalysisCacheInputs` so cache parity holds.
**Why it's wrong:** Command-zone awareness (HIGH-1) showed widening the key risks the OFF byte-identity contract; the fix was to **bypass the cache while the flag is ON** (`TryComputeCacheKeyAsync` returns null, `:338-341`; write-side skip, `:832-836`).
**Do this instead:** If a new flag changes `AnalysisPromptText`, mirror the bypass; do not widen the key.

### Anti-Pattern 5: Putting scoring/classification logic in Web services
**What people do:** Write the audit/win-con/mulligan math in `Services/` next to the HTTP.
**Why it's wrong:** Core is the pure, reliably-testable layer (VSTest is flaky in WSL; Core unit tests are the dependable gate). `DeckStatClassifier`/`MultiAxisScorer`/`CastabilitySimulator` are all pure Core.
**Do this instead:** Keep all new pure logic in `DeckFlow.Core/Analysis` (or `/Manabase`); keep only block-text rendering + flag gates + hydration in Web.

## Integration Points

### External services (already wired — reuse, don't re-add)
| Service | Integration pattern | Notes |
|---------|---------------------|-------|
| Commander Spellbook | `ICommanderSpellbookService.FindCombosAsync` via RestSharp+Polly `spellbook` pipeline; `comboTask` already started in `BuildAsync` (`:650-652`) and **widened** to fire for the score flag | Null-graceful; `Popularity`/`ManaValueNeeded` captured but unused → Feature 2 fuel. Reuse `comboResult` — never double-fetch |
| Scryfall | `cardReferenceBundle.CardReferences` already resolved for the prompt; carries `TypeLine`/`OracleText`/`ManaCost` — exactly `DeckStatCardInput` | Feature 1 reuses this with zero new calls (same prep as score at `:715-717`) |
| Postgres / FeatureFlagStore | Seeded-OFF rows + 30s-polled `FeatureFlagCache` snapshot | `ON CONFLICT (key) DO NOTHING` preserves operator toggles across deploys |

### Internal boundaries
| Boundary | Communication | Notes |
|----------|---------------|-------|
| `DeckFlow.Core` ↔ `DeckFlow.Web` | Web calls pure Core statics/records; Core has no HTTP/DI | Keep all new pure logic in Core; only block-text + flag gates in Web |
| `/deck-analysis` ↔ `/manabase` | **None today** — separate services/controllers | Feature 3 is the only feature that wants to cross this; prefer 3a (surface on `/manabase`) to avoid the bridge |
| packet service ↔ 3 prompt variants | trailing optional `string?` block params + `AiPlatform.Normalize` dispatch via registry | ADR-0001 decoupled; add params, never a shared helper |

## Suggested build order (dependency-aware)

1. **Feature 1 — Interaction audit (FIRST).** Strongest precedent (Phase 77, exact same files), purely additive, no external call, establishes the repeatable "new block param through 3 variants + new flag" recipe end-to-end. Lowest risk, fastest to green.
2. **Feature 2 — Win-con/combo map (SECOND).** Reuses the block recipe from Feature 1 and the **already-wired** combo fetch; medium risk is only combo-null handling (precedent exists). Independent of Feature 1's output.
3. **Feature 3 — Mulligan evaluator (LAST).** Additive-sim-field half is trivial (TAP-02 clone), but the **routing decision (3a vs 3b)** is the milestone's real architectural choice. Recommend shipping **3a (manabase-tool surface)** first — Pattern-3-only, reuses the entire manabase pipeline — and treating **3b (/deck-analysis bridge)** as an explicit, separately-scoped option if the metric must live in deck-analysis.

**Dependencies:** Features 1 & 2 are mutually independent and both slot into `DeckAnalysisPacketService`'s score-block groove; doing 1 first de-risks 2's plumbing (the shared "block param through 3 variants" recipe). Feature 3 depends on no other feature's output but on a routing decision the roadmap should resolve before the phase is planned. No feature consumes another's output, so all three can be parallelized at the Core layer if needed.

## Sources

- Live `deckflow-cycle14` worktree code (HIGH): `DeckFlow.Core/Analysis/{DeckStatClassifier,DeckStatAggregator,MultiAxisScorer,MultiAxisScore}.cs`; `DeckFlow.Web/Services/DeckAnalysisPacketService.cs`; `DeckFlow.Web/Services/PromptBuilders/Analysis/*`; `DeckFlow.Web/Services/CommanderSpellbookService.cs`; `DeckFlow.Core/Manabase/{CastabilitySimulator,ManabaseModels,ManabaseAnalyzer}.cs`; `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs`; `DeckFlow.Web/Services/FeatureFlags/{FeatureFlagCatalog,FeatureFlagStore,IFeatureFlagCache}.cs`; `DeckFlow.Web/Views/Deck/{DeckAnalysis,Manabase}.cshtml`; `DeckFlow.Web/Models/{DeckAnalysisRequest,DeckAnalysisViewModel}.cs`
- `docs/decisions/0001-prompt-variants-decoupled.md` (ADR-0001), `0002-calver-versioning-named-milestones.md` (ADR-0002)
- `.planning/PROJECT.md` (Cycle 14 scope)

---
*Architecture research for: DeckFlow Cycle 14 — Deeper Deck Evaluation*
*Researched: 2026-06-30*
