# Phase 77: Multi-Axis Deck Score — Research

**Researched:** 2026-06-29
**Domain:** C# / ASP.NET Core / DeckFlow scoring heuristics / prompt variant pipeline
**Confidence:** HIGH (every claim verified against the working-tree source)

---

## Summary

Phase 77 folds a four-axis Power/Speed/Control/Consistency score block into the existing
`/deck-analysis` paste artifact and Step-3 results panel. No new tool tile, no new HTTP
call, no new NuGet package. All signals derive from data already resolved during the
Step-2 card-reference bundle build (Scryfall-resolved oracle text and mana costs) plus the
Phase-76 bracket classifier (Game Changers count) and the existing combo result.

The primary challenge is that several required signals — **tutor count, fast-mana count,
ramp/draw-under-MV-2 count, and counterspell count** — do not yet exist in
`DeckStatClassifier` / `DeckStatSummary`. They must be added there (Core layer) before
the scorer can consume them. The bracket classifier (`BracketClassifier.Classify`) must
also be wired into `DeckAnalysisPacketService`, which currently does not invoke it.

ADR-0001 (prompt variants decoupled) mandates hand-editing all three variant files; a
parity test modelled on `BracketPromptVariantParityTests` guards completeness. The cycle
flag pattern (`analysis.multi-axis-score` seeded OFF) ensures byte-identical output until
the operator flips the flag.

**Primary recommendation:** Extend `DeckStatSummary` with four new `{ get; init; }` fields
(`Tutors`, `FastMana`, `RampDrawUnderThreeMv`, `Counters`), add corresponding
`DeckStatClassifier` predicates, build a pure `MultiAxisScorer` in Core, and wire it into
the packet service. Pass the computed score text to all three analysis prompt variant
`Build()` calls via a new optional `string? scoreBlockText = null` parameter.

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SCORE-01 | User sees deck scored on four axes (Power/Speed/Control/Consistency), each a coarse 0-5 labeled band | Band-label vocab in UI-SPEC §3; `MultiAxisScorer.BandLabel()` maps int → word |
| SCORE-02 | Speed/Consistency from existing signals (avg MV, fast mana, ramp/draw-under-3, combo density, tutor count); Power from proxy signals (GC count + combo density + fast mana); Control from new interaction/removal classifier | Signals inventory (§Signals), new `DeckStatClassifier` predicates (§New Signals), `MultiAxisScorer` design |
| SCORE-03 | Each axis reports inline rationale (signals that produced the band); score cross-checked against bracket classification | `DeckMultiAxisScore.BracketCrossCheckText` field; bracket number from Phase-76 `BracketClassification.BracketNumber` |
| SCORE-04 | Score block folds into existing `/deck-analysis` artifact for all three prompt variants (ADR-0001 parity test); no new tool tile | Variant insertion points (§Prompt Builder), parity test pattern (§Parity) |
</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Signal derivation (avg MV, tutors, fast mana, counters, ramp/draw) | Core (`DeckStatClassifier` / `DeckStatAggregator`) | — | Pure CPU domain logic; no HTTP, no DI |
| Band mapping (signal counts → 0-5) | Core (`MultiAxisScorer`) | — | Pure deterministic mapping; must be testable |
| Bracket cross-check logic | Core (`MultiAxisScorer`) | — | Pure comparison of band vs bracket number |
| Score computation orchestration | Web (`DeckAnalysisPacketService`) | — | Has Scryfall card data + bracket catalog + combo result |
| Prompt text emission (score section) | Web (each `*AnalysisPromptVariant`) | — | ADR-0001: each variant hand-edited independently |
| UI score card HTML rendering | Web (Razor view `DeckAnalysis.cshtml`) | — | Additive markup inside existing Step-3 result panel |
| CSS tokens and layout | Web (`wwwroot/css/site-common.css`) | — | All cross-theme layout goes here per project constraint |
| Feature-flag gate | Web (`FeatureFlagStore`, `FeatureFlagCatalog`) | — | Cycle pattern: seeded OFF, byte-identical when off |

---

## Standard Stack

### No new packages needed
All implementation builds on in-solution tech (REQUIREMENTS.md "Out of Scope: New NuGet/npm dependencies").

---

## Key Architecture: How the Analysis Packet Flow Works Today

[VERIFIED: source DeckAnalysisPacketService.cs]

The analysis packet build (`DeckAnalysisPacketService.BuildAsync`) has three distinct paths:

**Step 2 (wantsAnalysisPacket = true):**
- Loads deck entries via `IDeckEntryLoader`
- Resolves card oracle data via Scryfall (`LookupCardReferencesAsync`) → produces `cardReferenceBundle.CardReferences` (type: `IReadOnlyList<CardReference>`, each carries `Name`, `ManaCost`, `TypeLine`, `OracleText`, `Quantity`, `IsCommander`, `Scope`)
- Fetches combos from Commander Spellbook (gate: `RequiresComboLookup(selectedQuestions)`) → `comboResult` (`CommanderSpellbookResult?`)
- Calls `BuildAnalysisPrompt(...)` → dispatches to `AnalysisPromptVariantRegistry.Build(...)` → routes to the platform-specific variant's `Build()` method
- **Where score must be computed:** here, after `cardReferenceBundle` and `comboResult` are resolved

**Step 3 (workflowStep == 3 without DeckSource — early return at line 371):**
- Parses `DeckProfileJson` → `DeckAnalysisResponse` and returns immediately
- No Scryfall call, no card references, no score recomputation
- Score must survive the round-trip via a `ScoreJson` hidden form field (same pattern as `DeckProfileJson`)

**Step 3 (workflowStep >= 3 WITH DeckSource — cache hit path):**
- Runs `TryComputeCacheKeyAsync` and retrieves cached `DeckAnalysisPacketResult` including score
- Score is available from the cached result

**Insertion point for score in `BuildAsync`:** immediately after `comboResult` is awaited and before calling `BuildAnalysisPrompt()`, compute `DeckMultiAxisScore` from the card references + combo result + bracket classification.

---

## Signal Inventory

### Signals that ALREADY exist (verified by source)

[VERIFIED: DeckFlow.Core/Analysis/DeckStatAggregator.cs, DeckStatClassifier.cs]
[VERIFIED: DeckFlow.Core/Manabase/ManabaseClassifier.cs lines 84-294]
[VERIFIED: DeckFlow.Web/Services/DeckAnalysisPacketService.cs lines 1123-1147]

| Signal | Source in Analysis Flow | Type | Notes |
|--------|------------------------|------|-------|
| Average mana value (non-land) | `DeckStatSummary.AverageManaValue` | `decimal` | Already computed by `DeckStatAggregator.Compute()` from `cardReferenceBundle.CardReferences`; land cards excluded from denominator |
| Interaction pieces | `DeckStatSummary.Interaction` | `int` | `DeckStatClassifier.IsInteractionCard()`: Instant type OR "destroy target" OR "exile target" OR "counter target" OR "return target spell" OR "fight target" |
| Board wipes | `DeckStatSummary.Wipes` | `int` | `DeckStatClassifier.IsBoardWipeCard()`: "destroy all creatures/artifacts/enchantments", mass -X/-X, "exile all" |
| Ramp sources | `DeckStatSummary.Ramp` | `int` | `DeckStatClassifier.IsRampCard()` |
| Draw sources | `DeckStatSummary.Draw` | `int` | `DeckStatClassifier.IsDrawCard()` |
| Two-card combo count | `comboResult?.IncludedCombos.Count(c => c.CardNames.Count == 2)` | `int` | From Commander Spellbook; `null` when unavailable (never treat as zero) |
| Game Changers count | `BracketClassification.DetectedGameChangers.Count` | `int` | Phase-76 `BracketClassifier.Classify()`; NOT currently in the analysis flow — requires wiring |
| Bracket number | `BracketClassification.BracketNumber` | `int` | Phase-76; same wiring needed |

**Critical gap: `DeckStatAggregator.Compute()` is only called in `BuildDeckStatsText()` which is flag-gated (`analysis.reference.deck-stats`, seeded OFF).** For Phase 77, the scorer must call `DeckStatAggregator.Compute()` independently of that flag. Extract the Compute call so both the deck-stats text builder and the scorer can reuse the result.

### Signals that must be ADDED (new `DeckStatClassifier` predicates)

[VERIFIED: DeckFlow.Core/Analysis/DeckStatClassifier.cs — tutor/fast-mana/counterspell predicates do not exist]
[VERIFIED: DeckFlow.Core/Manabase/ManabaseClassifier.cs line 214 — fast-mana detection exists in manabase classifier but not in DeckStatClassifier]

| Signal | Location | Oracle/Type Detection Approach |
|--------|----------|-------------------------------|
| **Tutor count** | New `DeckStatClassifier.IsTutorCard(oracleText)` | `"search your library for" && !("land" in oracle)` — approximation; catches Demonic Tutor, Vampiric Tutor, Imperial Seal but also some land-tutors. Refinement: add explicit exclusion for "basic land" searches that go to hand (Cultivate/Farseek are ramp, not tutors). The scorer expects a reasonable estimate, not perfection. |
| **Fast mana count** | New `DeckStatClassifier.IsFastManaCard(typeLine, oracleText, manaCost)` | Mirror the manabase classifier: `estimatedManaValue == 0 && typeLine.Contains("Artifact") && (oracleText.Contains("{T}: Add") or oracleText.Contains("Add {"))` — catches Mana Crypt, Sol Ring, Lotus Petal, Chrome Mox, Mox Diamond, Jeweled Lotus, Vault; the `ManaValue` is estimated via `DeckStatAggregator.EstimateManaValue(manaCost)` |
| **Ramp/draw under MV 2** | New `DeckStatClassifier.IsRampOrDrawUnderThreeMv(typeLine, oracleText, manaCost)` | `estimatedMV <= 2 && (IsRampCard(typeLine, oracleText) || IsDrawCard(oracleText))` — mirrors the manabase classifier's `rampUnderThree` logic (line 179) but using the DeckStatClassifier predicates |
| **Counterspell count** | New `DeckStatClassifier.IsCounterspellCard(oracleText)` | `oracleText.Contains("counter target spell", OrdinalIgnoreCase)` — narrower than `IsInteractionCard` (which catches any "counter target"); this counts pure counterspells only |

**`DeckStatSummary` new fields (additive `{ get; init; }` per cycle discipline):**
```csharp
/// <summary>Count of tutor effects (search library for non-land card).</summary>
public int Tutors { get; init; }

/// <summary>Count of 0-cost mana artifacts (fast mana: Mana Crypt, Jeweled Lotus, etc.).</summary>
public int FastMana { get; init; }

/// <summary>Count of ramp or draw pieces with estimated mana value &lt;= 2.</summary>
public int RampDrawUnderThreeMv { get; init; }

/// <summary>Count of cards that counter target spells (subset of Interaction).</summary>
public int Counters { get; init; }
```

---

## Phase-76 APIs Available to Phase 77

[VERIFIED: DeckFlow.Web/Services/Bracket/BracketClassificationService.cs]
[VERIFIED: DeckFlow.Core/Bracket/BracketClassifier.cs]
[VERIFIED: DeckFlow.Core/Bracket/BracketClassification.cs]
[VERIFIED: DeckFlow.Core/Bracket/GameChangerCatalog.cs]
[VERIFIED: DeckFlow.Web/Services/Bracket/IGameChangerCatalogService.cs]

**`BracketClassifier.Classify(entries, catalog, twoCardCombos)`** — `DeckFlow.Core/Bracket/BracketClassifier.cs`
- Pure static; no DI, no HTTP
- Signature: `(IReadOnlyList<DeckEntry>, GameChangerCatalog, IReadOnlyList<TwoCardCombo>?) → BracketClassification`
- Returns `BracketClassification` with `.BracketNumber` (1-5), `.DetectedGameChangers` (list of card names), `.TwoCardCombos` (null = detection unavailable)
- Can be called directly inside `DeckAnalysisPacketService.BuildAsync()` using the already-loaded `deckEntries` and a `GameChangerCatalog` obtained from `IGameChangerCatalogService.GetCatalog()`

**`IGameChangerCatalogService.GetCatalog()`** — `DeckFlow.Web/Services/Bracket/IGameChangerCatalogService.cs`
- Returns the preloaded `GameChangerCatalog` (in-memory cache; no HTTP)
- Must be injected into `DeckAnalysisPacketService` as a new constructor parameter
- This is the only new DI dependency Phase 77 adds to the packet service

**Combo mapping for BracketClassifier:** The existing `comboTask` in `BuildAsync` returns `CommanderSpellbookResult?`. For bracket classification, need to map `IncludedCombos` to `IReadOnlyList<TwoCardCombo>` filtering to 2-card combos (same mapping as `BracketClassificationService.ClassifyAsync` lines 99-104). If `comboResult` is null, pass null to `BracketClassifier.Classify` (preserves null-vs-empty semantics).

**Combo task gate:** The combo task is currently guarded by `RequiresComboLookup(selectedQuestions)`. If none of the selected questions require combos, `comboResult` is null. For Phase 77, either:
- Always fetch combos when the score flag is ON (preferred: adds a ~100ms task but ensures accurate Power/Consistency bands), OR
- Accept null combos and disclose "combo data unavailable" in the rationale (same pattern as BRACKET-03)

**Recommended:** when `analysis.multi-axis-score` is ON, always fire the combo fetch regardless of selected questions, paralleling the bracket tool's always-on combo fetch.

---

## Prompt Builder: Exact Insertion Points

[VERIFIED: DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs]
[VERIFIED: DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs lines 1-50]
[VERIFIED: DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs lines 1-50]
[VERIFIED: DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs — not read in full but same interface]
[VERIFIED: DeckFlow.Web/Services/DeckAnalysisPacketService.cs line 1191 — BuildAnalysisPrompt dispatcher]

**`IAnalysisPromptVariant.Build()` interface** — add one new optional parameter:
```csharp
string Build(
    DeckAnalysisRequest request,
    string decklistText,
    string referenceText,
    string deckProfileSchemaJson,
    string? commanderName,
    IReadOnlyList<string> selectedQuestionIds,
    IReadOnlyList<string> bannedCards,
    CommanderSpellbookResult? comboResult,
    bool includeCardVersions,
    string? companionName = null,
    string? scoreBlockText = null);   // <-- NEW, last, optional; no breaking change for existing tests
```

**`BuildAnalysisPrompt()` in `DeckAnalysisPacketService`** (line 1191) — already passes through all variant params; add `scoreBlockText` as the last arg.

**Insertion point in each variant `Build()` method:**
Per UI-SPEC §10, the score section belongs "near the deck-summary header" in the artifact text. In the ChatGPT variant (lines 58-89 of `ChatGptAnalysisPromptVariant.cs`), this is after the `## DECK CONTEXT` block and before `## EVIDENCE RULES`. In the Claude variant (which uses `<task>` XML tags), it follows the deck context metadata lines. In the Gemini variant, same relative position as ChatGPT.

Emit the score block ONLY when `scoreBlockText` is not null/empty — the null path produces byte-identical output (flag-OFF behavior).

**Score artifact text format** (per UI-SPEC §10):
```
DECK SCORE (coarse 0-5 bands - magnitude, not quality)
  Power:       4/5  High      (4 Game Changers, 2 two-card combos, 9 fast-mana sources)
  Speed:       3/5  Moderate  (avg MV 2.6, 9 fast-mana, 7 ramp/draw under 3 MV)
  Control:     4/5  High      (11 interaction pieces, 4 board wipes, 3 counters)
  Consistency: 3/5  Moderate  (8 tutors, 2 redundant combo lines, smooth 2.6 curve)
Cross-check: score aligns with the Bracket 4 classification.
(These bands are DeckFlow heuristic estimates from decklist signals - re-check and refine.)
```
- ASCII only; no em/en dashes; plain hyphens
- This text is built by a new static helper in `DeckAnalysisPacketService` (or each variant builds it independently from the `DeckMultiAxisScore` object — the latter is cleaner for ADR-0001)

**ADR-0001 compliance:** Each variant receives `DeckMultiAxisScore?` (or the pre-built `string? scoreBlockText`) and emits its own score section. Prose may differ per platform; the figures (band numbers, signal counts) must match. A parity test asserts this.

---

## New Core Types: MultiAxisScore

**File:** `DeckFlow.Core/Analysis/MultiAxisScore.cs`

```csharp
namespace DeckFlow.Core.Analysis;

/// <summary>
/// The four-axis coarse band score for a Commander deck (SCORE-01/02/03).
/// Each band is 0-5; a <see cref="DeckScoreRationale"/> carries the signals that produced it.
/// </summary>
public sealed record DeckMultiAxisScore(
    int PowerBand,
    int SpeedBand,
    int ControlBand,
    int ConsistencyBand,
    DeckScoreRationale PowerRationale,
    DeckScoreRationale SpeedRationale,
    DeckScoreRationale ControlRationale,
    DeckScoreRationale ConsistencyRationale,
    int BracketNumber,
    string BracketCrossCheckText,
    bool ScoreAlignsBracket);

/// <summary>The signals that produced a single axis band.</summary>
public sealed record DeckScoreRationale(string SignalText);
```

**File:** `DeckFlow.Core/Analysis/MultiAxisScorer.cs`

```csharp
namespace DeckFlow.Core.Analysis;

public static class MultiAxisScorer
{
    public static DeckMultiAxisScore Score(
        DeckStatSummary stats,
        int gameChangerCount,
        int twoCardComboCount,
        bool comboDetectionAvailable,
        int bracketNumber) { ... }

    public static string BandLabel(int band) => band switch
    {
        0 => "None", 1 => "Low", 2 => "Modest", 3 => "Moderate", 4 => "High", _ => "Extreme"
    };
}
```

Band derivation logic (thresholds to be calibrated by the executor but shape fixed by spec):

**Power (GC-dominant, combo + fast mana as modifiers):**
| Condition | Band |
|-----------|------|
| 0 GC, 0 combos, <3 fast mana | 0-1 (None/Low) |
| 1-3 GC, ≤1 combo, some fast mana | 2 (Modest) |
| 1-3 GC, 1-2 combos OR 4+ fast mana | 3 (Moderate) |
| 4-9 GC, 1+ combos, fast mana | 4 (High) |
| 10+ GC (cEDH heuristic) | 5 (Extreme) |

**Speed (avg MV + fast mana + ramp/draw under MV2):**
| avg MV | fast mana | ramp/draw<3 | Band |
|--------|-----------|-------------|------|
| >3.5 | <3 | <4 | 0-1 |
| 3.0-3.5 | any | moderate | 2 |
| 2.5-3.0 | 3-6 | 5-8 | 3 |
| 2.0-2.5 | 6-9 | 8-12 | 4 |
| <2.0 | 10+ | 12+ | 5 |

**Control (interaction + wipes + counters):**
| interaction | wipes | counters | Band |
|-------------|-------|----------|------|
| 0-3 | 0 | 0 | 0 (None) |
| 4-7 | 0-1 | 0-1 | 1-2 |
| 8-12 | 1-2 | 1-3 | 3 |
| 13-18 | 3-5 | 3-6 | 4 |
| 19+ | 5+ | 6+ | 5 |

**Consistency (tutors + combo redundancy + avg MV smoothness):**
| tutors | combos | avg MV | Band |
|--------|--------|--------|------|
| 0-1 | 0 | >3.5 | 0-1 |
| 2-4 | 0-1 | 3.0-3.5 | 2 |
| 5-7 | 1-2 | 2.5-3.0 | 3 |
| 8-11 | 2-3 | 2.0-2.5 | 4 |
| 12+ | 3+ | <2.0 | 5 |

**Bracket cross-check (SCORE-03):**
- Power band 4-5 is consistent with bracket 4-5; misalign if Power ≥ 4 and bracket ≤ 2 (or Power ≤ 1 and bracket ≥ 4)
- Speed band 4-5 consistent with bracket 4-5
- If all axes within ±1 of what bracket implies → ScoreAlignsBracket = true

---

## Control Axis: Interaction Classifier

[VERIFIED: DeckFlow.Core/Analysis/DeckStatClassifier.cs]

Today's `IsInteractionCard()` detects:
- Instant type-line
- "destroy target", "exile target", "counter target", "return target spell", "fight target"

This already covers the broad control signal. What's missing for the Control axis rationale are:
- Board wipes (already in `DeckStatSummary.Wipes`)
- Counterspells as a separate sub-count (new `Counters` field)

**Recommended approach (option a from UI-SPEC §11):**
Use `DeckStatSummary.Interaction` as the total interaction count, `DeckStatSummary.Wipes` as board wipes, and a new `DeckStatSummary.Counters` from `IsCounterspellCard()`. No new external data source — all from oracle text already resolved.

---

## Score Persistence Across Workflow Steps

[VERIFIED: DeckFlow.Web/Models/DeckAnalysisRequest.cs — pattern of DeckProfileJson hidden field]
[VERIFIED: DeckFlow.Web/Services/DeckAnalysisPacketService.cs lines 371-392 — Step 3 early return]

**Problem:** At Step 3 the early-return path fires when DeckSource is blank and DeckProfileJson is set — no Scryfall data is available to recompute the score.

**Solution:** Add `ScoreJson` to `DeckAnalysisRequest` as a round-tripped hidden field, mirroring `DeckProfileJson`. The controller serializes `DeckMultiAxisScore` to JSON at Step 2 and stores it in the form. At Step 3, `BuildAsync` deserializes it from `ScoreJson` and includes it in the result. `DeckAnalysisViewModel` gets a `DeckMultiAxisScore? Score` property.

**Alternatively (simpler):** If the `PacketSessionCache` hit is reliable, the score is already in the cached result — no extra hidden field needed for the cache-hit path. But the cache bypass when `commandZoneAwareness` is ON (line 757) and the no-cache step-3 early-return mean the hidden-field approach is more robust.

---

## View Integration

[VERIFIED: DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml lines 519-554]
[VERIFIED: DeckFlow.Web/Models/DeckAnalysisViewModel.cs]
[VERIFIED: UI-SPEC.md §1, §2, §4]

**Insertion point in `DeckAnalysis.cshtml`:**
Inside the `@if (Model.AnalysisResponse is not null)` block (line 519), between:
- `<h3>Analysis Summary</h3>` (line 530)
- and `<section class="result-panel nested-panel"><h4>Overview</h4>` (line 532)

Gate with `@if (Model.Score is not null)` to preserve byte-identical output when flag is OFF.

**New CSS classes** (all in `site-common.css`, not per-theme fork):
`.chatgpt-score`, `.chatgpt-score__eyebrow`, `.chatgpt-score-grid` (4→2→1 responsive),
`.chatgpt-score-card`, `.chatgpt-score-label`, `.chatgpt-score-value`,
`.chatgpt-score-meter`, `.chatgpt-score-pip`, `.chatgpt-score-pip--filled`,
`.chatgpt-score-band`, `.chatgpt-score-band--0` through `--5`,
`.chatgpt-score-rationale`, `.chatgpt-score-crosscheck`,
`.chatgpt-score-crosscheck--agree`, `.chatgpt-score-crosscheck--diverge`

**New CSS tokens** (`:root` block in `site-common.css`; same values duplicated per theme file per "each theme is a full fork" convention):
`--score-band-{0..5}-bg` and `--score-band-{0..5}-ink` (12 tokens total, fixed color values per UI-SPEC §4)

---

## Feature Flag

[VERIFIED: DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs lines 196-264]
[VERIFIED: DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs]

**New flag key:** `analysis.multi-axis-score`
**Default:** FALSE (seeded OFF; matches the cycle pattern for TAP-04 and BRACKET)
**Read pattern:** explicit snapshot read (same as `ReferenceDeckStatsFlag` at line 647-649):
```csharp
var scoreEnabled = _flagCache is not null
    && _flagCache.Snapshot().TryGetValue(MultiAxisScoreFlag, out var scoreOn)
    && scoreOn;
```

**Seeding:** append to both `PostgresSeedSql` and `SqliteSeedSql` in `FeatureFlagStore`:
```sql
-- Postgres
('analysis.multi-axis-score', FALSE)
-- SQLite
('analysis.multi-axis-score', 0)
```

**Catalog description:**
```csharp
["analysis.multi-axis-score"] =
    "Show a four-axis Power/Speed/Control/Consistency score block in the deck-analysis " +
    "Step-3 results and include the score in all three prompt artifacts. Off = byte-identical.",
```

---

## Parity Test Pattern

[VERIFIED: DeckFlow.Web.Tests/Bracket/BracketPromptVariantParityTests.cs]

The bracket parity test is the canonical template for Phase 77. Create
`DeckFlow.Web.Tests/AnalysisScorePromptParityTests.cs`:

```csharp
[Theory]
[InlineData("ChatGPT")]
[InlineData("Claude")]
[InlineData("Gemini")]
public void Score_Block_AppearsInAllThreeVariants(string platformName)
{
    // Build an analysis prompt with a non-null scoreBlockText
    // Assert.Contains("DECK SCORE", result, StringComparison.Ordinal)
}

[Theory]
[InlineData("ChatGPT")]
[InlineData("Claude")]
[InlineData("Gemini")]
public void Score_Block_AbsentInAllThreeVariants_WhenScoreBlockTextIsNull(string platformName)
{
    // Build with scoreBlockText = null
    // Assert.DoesNotContain("DECK SCORE", result, StringComparison.Ordinal)
}

[Theory]
[InlineData("ChatGPT")]
[InlineData("Claude")]
[InlineData("Gemini")]
public void Score_SignalFigures_MatchAcrossAllThreeVariants(string platformName)
{
    // Assert that the same band numbers (e.g. "4/5") appear in every variant
}
```

Key construction pattern (from `BracketPromptVariantParityTests.BuildRegistry()` line 19):
- **No shared variant helper** — instantiate each variant directly
- Use `[Theory]` + `[InlineData]` for the three platforms
- No live HTTP needed; pass a pre-built `scoreBlockText` string

---

## Test Fixtures for Golden Test (SCORE-03)

[VERIFIED: DeckFlow.Core.Tests/DeckStatAggregatorTests.cs — construction pattern for `DeckStatCardInput`]
[VERIFIED: DeckFlow.Core.Tests/Bracket/BracketClassifierTests.cs — fixture pattern]

For the golden test asserting "cEDH deck scores higher Power/Speed than a battlecruiser deck":

**No existing deck fixture files.** Both existing test files construct cards inline using `Card(qty, typeLine, oracleText, manaCost)` factory methods.

**Recommended test fixture construction in `MultiAxisScorerTests.cs`:**

```csharp
// cEDH proxy deck signals
var cedhStats = new DeckStatSummary(
    Cards: 99, Lands: 31, Creatures: 25,
    AverageManaValue: 1.8m,
    ManaCurve: ...,
    Ramp: 15, Draw: 12, Interaction: 18, Wipes: 2, Recursion: 3, ClosingPower: 8,
    Tutors: 12, FastMana: 10, RampDrawUnderThreeMv: 18, Counters: 8);

// battlecruiser proxy deck signals
var casualStats = new DeckStatSummary(
    Cards: 99, Lands: 38, Creatures: 28,
    AverageManaValue: 3.8m,
    ManaCurve: ...,
    Ramp: 8, Draw: 5, Interaction: 4, Wipes: 1, Recursion: 2, ClosingPower: 5,
    Tutors: 1, FastMana: 1, RampDrawUnderThreeMv: 4, Counters: 1);

// cEDH should score Power >= 4 and Speed >= 4
var cedhScore = MultiAxisScorer.Score(cedhStats, gameChangerCount: 8, twoCardComboCount: 3, comboDetectionAvailable: true, bracketNumber: 5);
Assert.True(cedhScore.PowerBand >= 4);
Assert.True(cedhScore.SpeedBand >= 4);

// casual should score Power <= 2 and Speed <= 2
var casualScore = MultiAxisScorer.Score(casualStats, gameChangerCount: 0, twoCardComboCount: 0, comboDetectionAvailable: true, bracketNumber: 2);
Assert.True(casualScore.PowerBand <= 2);
Assert.True(casualScore.SpeedBand <= 2);
```

This avoids any real deck data dependency and keeps tests deterministic.

---

## Architecture Patterns

### Recommended Project Structure (new files only)

```
DeckFlow.Core/
├── Analysis/
│   ├── DeckStatClassifier.cs         # MODIFIED: +IsTutorCard, +IsFastManaCard, +IsCounterspellCard, +IsRampOrDrawUnderThreeMv
│   ├── DeckStatAggregator.cs         # MODIFIED: tally Tutors, FastMana, RampDrawUnderThreeMv, Counters
│   ├── DeckStatSummary.cs (inline)   # MODIFIED: 4 new { get; init; } fields
│   ├── MultiAxisScore.cs             # NEW: DeckMultiAxisScore + DeckScoreRationale records
│   └── MultiAxisScorer.cs            # NEW: static band derivation + BandLabel

DeckFlow.Web/
├── Models/
│   ├── DeckAnalysisRequest.cs        # MODIFIED: +ScoreJson hidden field
│   ├── DeckAnalysisViewModel.cs      # MODIFIED: +DeckMultiAxisScore? Score
│   └── DeckAnalysisModels.cs (DeckAnalysisPacketResult) # MODIFIED: +DeckMultiAxisScore? Score
├── Services/
│   ├── DeckAnalysisPacketService.cs  # MODIFIED: inject IGameChangerCatalogService, compute score, pass scoreBlockText
│   ├── FeatureFlags/
│   │   ├── FeatureFlagCatalog.cs     # MODIFIED: +analysis.multi-axis-score description
│   │   └── FeatureFlagStore.cs       # MODIFIED: +seed rows both dialects
│   └── PromptBuilders/Analysis/
│       ├── IAnalysisPromptVariant.cs         # MODIFIED: +string? scoreBlockText = null param
│       ├── ChatGptAnalysisPromptVariant.cs   # MODIFIED: emit score section
│       ├── ClaudeAnalysisPromptVariant.cs    # MODIFIED: emit score section
│       └── GeminiAnalysisPromptVariant.cs    # MODIFIED: emit score section
├── Views/Deck/
│   └── DeckAnalysis.cshtml           # MODIFIED: score card HTML above Overview
└── wwwroot/css/
    └── site-common.css               # MODIFIED: 15 new classes, 12 new tokens; tokens also duplicated per theme

DeckFlow.Core.Tests/
├── DeckStatClassifierTests.cs        # MODIFIED: new predicate tests
├── DeckStatAggregatorTests.cs        # MODIFIED: new field tally tests
└── MultiAxisScorerTests.cs           # NEW: golden tests (cEDH vs casual), band mapping, BandLabel

DeckFlow.Web.Tests/
└── AnalysisScorePromptParityTests.cs # NEW: 3-platform parity (SCORE-04 / ADR-0001)
```

### Pattern: Additive `{ get; init; }` fields only

Per cycle discipline (TAP-03 precedent: `TapAnalysis` on `ManabaseResult`), new fields on
existing records use `{ get; init; }` with a sensible default (0 for ints, null for
optionals). Never add `required`. This keeps deserialization backward-compatible and the
carveout guard happy.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Card text analysis | Custom oracle-text parser | `DeckStatClassifier` predicates (existing + new) | Already tested and calibrated |
| Bracket signals | Re-detect GC membership from raw card list | `BracketClassifier.Classify(entries, catalog, combos)` | Phase-76 deliverable; single source of truth |
| Band colors | Inline style attributes | CSS tokens `--score-band-{0..5}-bg/ink` in `site-common.css` | Follows `.manabase-health--*` precedent; theme-agnostic |
| Score text building | Complex string template in each variant | `BuildScoreBlockText(DeckMultiAxisScore score)` static helper inside `DeckAnalysisPacketService`, result passed as `scoreBlockText` | Single place to maintain the ASCII format; each variant still independently decides where in the prompt to insert it |

---

## Common Pitfalls

### Pitfall 1: Treating null combo result as zero combos
**What goes wrong:** `comboResult?.IncludedCombos.Count ?? 0` silently treats API-unavailable as "no combos", inflating the Power and Consistency band downward incorrectly and violating the BRACKET-03 null-vs-empty semantics already established.
**How to avoid:** Use `comboResult?.IncludedCombos.Count(c => c.CardNames.Count == 2) ?? 0` only for the *score band input*, but also track `comboDetectionAvailable = comboResult is not null` separately. When unavailable, disclose "combo data unavailable" in the rationale text instead of asserting zero combos.

### Pitfall 2: Extracting shared prompt text across variants
**What goes wrong:** A helper like `BuildScoreArtifactText()` shared between all three variant files would violate ADR-0001 and has been reverted before (commit `a1fa5ad` → `b2ffba7`).
**How to avoid:** The `scoreBlockText` string is built ONCE in `DeckAnalysisPacketService.BuildAsync` by a private static helper, then passed as an argument to each variant's `Build()` method. Each variant independently INSERTS the pre-built text block at its preferred position in the prompt — the variant does not build the block itself, but it still controls placement and surrounding prose. This is NOT shared text building at the variant level; each variant independently uses the shared input.

### Pitfall 3: Forgetting to also read the score from the Step-3 early-return path
**What goes wrong:** At Step 3 (line 371-392) `DeckAnalysisPacketResult` is returned early with no score; the view renders the summary but the score block is absent.
**How to avoid:** Read `ScoreJson` from `request.ScoreJson` in the early-return path and deserialize it into the `DeckAnalysisPacketResult.Score` field.

### Pitfall 4: Combo flag interaction — `RequiresComboLookup` gate
**What goes wrong:** If the user selects only non-combo questions (e.g., "budget upgrades"), the combo task returns null. The score's Power axis will always show the lowest band, which is wrong for a cEDH deck.
**How to avoid:** When `analysis.multi-axis-score` flag is ON, always fire the combo fetch unconditionally (same decision `BracketClassificationService` makes: it always fetches combos). Add a separate `comboForScoreTask` that runs in parallel with the existing `comboTask`.

### Pitfall 5: The `DeckStatAggregator.Compute()` is only called inside a flag-gated helper
**What goes wrong:** `BuildDeckStatsText()` is only called when `analysis.reference.deck-stats` is ON. If Phase 77 relies on `BuildDeckStatsText()` for `DeckStatSummary`, then the score would require that unrelated flag to be ON.
**How to avoid:** Call `DeckStatAggregator.Compute(inputs)` independently inside the score-computation code path, regardless of the `ReferenceDeckStatsFlag`. This produces a small compute overhead but avoids the incorrect flag dependency.

### Pitfall 6: Tutor vs ramp overlap in `IsTutorCard`
**What goes wrong:** "search your library for a basic land" matches both `IsRampCard` and a naive `IsTutorCard` predicate.
**How to avoid:** `IsTutorCard` must exclude land searches: `Contains("search your library for") && !Contains("land card") && !Contains("basic land") && !Contains("land onto the battlefield")`. Cross-check: Demonic Tutor, Vampiric Tutor, Mystical Tutor should match; Cultivate, Rampant Growth, Kodama's Reach should NOT.

### Pitfall 7: CSS in `site.css` instead of `site-common.css`
**What goes wrong:** Score block CSS added to `site.css` breaks on guild-themed pages (each guild theme @imports `site.css` but overrides layout).
**How to avoid:** All new classes go in `site-common.css`. The band-color tokens are added to `:root` in `site-common.css` AND duplicated identically to each `site-*.css` theme file (per the "full standalone CSS fork" convention). The band tokens carry fixed colors (not semantic theme tokens), so per-theme overriding is unnecessary — but the convention requires the token to be present in each file.

---

## Code Examples

### Invoking the bracket classifier inside `DeckAnalysisPacketService.BuildAsync()`
```csharp
// Source: DeckFlow.Core/Bracket/BracketClassifier.cs + BracketClassificationService.cs
// After comboResult is resolved:
var twoCardCombosForClassifier = comboResult is null
    ? null
    : comboResult.IncludedCombos
        .Where(c => c.CardNames.Count == 2)
        .Select(c => new TwoCardCombo(c.CardNames, c.Results))
        .ToList();
GameChangerCatalog catalog = _catalogService.GetCatalog();
BracketClassification bracketClassification = BracketClassifier.Classify(
    deckEntries, catalog, twoCardCombosForClassifier);
```

### DeckStatAggregator call (separate from deck-stats text flag)
```csharp
// Source: DeckFlow.Core/Analysis/DeckStatAggregator.cs
var statInputs = cardReferenceBundle.CardReferences
    .Where(card => IsCurrentDeckScope(card.Scope) && !card.IsCommander)
    .Select(card => new DeckStatCardInput(card.Quantity, card.TypeLine, card.OracleText, card.ManaCost));
DeckStatSummary stats = DeckStatAggregator.Compute(statInputs);
```

### MultiAxisScorer invocation
```csharp
// Source: to be created in DeckFlow.Core/Analysis/MultiAxisScorer.cs
int twoCardComboCount = comboResult is null
    ? 0
    : comboResult.IncludedCombos.Count(c => c.CardNames.Count == 2);
bool comboAvailable = comboResult is not null;

DeckMultiAxisScore score = MultiAxisScorer.Score(
    stats,
    gameChangerCount: bracketClassification.DetectedGameChangers.Count,
    twoCardComboCount: twoCardComboCount,
    comboDetectionAvailable: comboAvailable,
    bracketNumber: bracketClassification.BracketNumber);
```

### Flag gate pattern (from `DeckAnalysisPacketService.cs` line 647)
```csharp
internal const string MultiAxisScoreFlag = "analysis.multi-axis-score";

var scoreEnabled = _flagCache is not null
    && _flagCache.Snapshot().TryGetValue(MultiAxisScoreFlag, out var scoreOn)
    && scoreOn;

string? scoreBlockText = scoreEnabled
    ? BuildScoreBlockText(score)
    : null;
```

---

## Project Constraints (from CLAUDE.md)

[VERIFIED: /mnt/c/users/chrislunt/source/personal/deckflow-cycle13/CLAUDE.md]

- **Tech stack:** ASP.NET 10 + Razor — no framework migration
- **No new NuGet/npm packages** — must ask if needed; Phase 77 needs none
- **Theme system:** layout CSS in `site-common.css`, NOT `site.css`; band tokens duplicated per-theme (full fork)
- **Testing:** `dotnet build` clean + targeted manual harness; VSTest unreliable in WSL; new feature tests go in `DeckFlow.Web.Tests` (xUnit) and `DeckFlow.Core.Tests` (xUnit with coverlet)
- **Commits:** plain default-author, conventional commits format, no Co-Authored-By trailer
- **ADR-0001:** three prompt variants MUST be hand-edited independently; no shared helper extracted; reviewers must not flag cross-variant prose duplication
- **Carve-out guard:** never convert `{ get; init; }` to `{ get; }` on new fields (System.Text.Json silently skips get-only in .NET 9+); never inline `[Attribute]` onto property line; never re-indent C# raw-string literals
- **Format gate:** changed lines must pass `scripts/format-check-changed.sh staged`; `.editorconfig` is authoritative
- **Flag pattern:** new analysis features seeded OFF (`analysis.multi-axis-score`, FALSE) with idempotent `ON CONFLICT DO NOTHING`
- **No external scoring DB:** Power axis uses proxy signals only (GC count + combo + fast mana), per REQUIREMENTS.md scope decision

---

## Validation Architecture

Nyquist validation is enabled (`workflow.nyquist_validation: true` in config.json).

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (Core.Tests + Web.Tests) |
| Config file | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`, `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |
| Quick run command | `dotnet test DeckFlow.Core.Tests --no-build -x` |
| Full suite command | `dotnet test DeckFlow.sln` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File |
|--------|----------|-----------|-------------------|------|
| SCORE-01 | Band labels None…Extreme map correctly to integers 0-5 | unit | `dotnet test DeckFlow.Core.Tests --no-build -x` | `MultiAxisScorerTests.cs` (new) |
| SCORE-02 / Speed | Low avg MV + fast mana = high Speed band | unit | `dotnet test DeckFlow.Core.Tests --no-build -x` | `MultiAxisScorerTests.cs` |
| SCORE-02 / Power | High GC count + combos = high Power band | unit | `dotnet test DeckFlow.Core.Tests --no-build -x` | `MultiAxisScorerTests.cs` |
| SCORE-02 / Control | High interaction + wipes + counters = high Control band | unit | `dotnet test DeckFlow.Core.Tests --no-build -x` | `MultiAxisScorerTests.cs` |
| SCORE-02 / Consistency | High tutors + combos = high Consistency band | unit | `dotnet test DeckFlow.Core.Tests --no-build -x` | `MultiAxisScorerTests.cs` |
| SCORE-02 / tutors | `IsTutorCard` matches Demonic Tutor, not Cultivate | unit | `dotnet test DeckFlow.Core.Tests --no-build -x` | `DeckStatClassifierTests.cs` |
| SCORE-02 / fast mana | `IsFastManaCard` matches Mana Crypt, not Sol Ring (MV 1) | unit | `dotnet test DeckFlow.Core.Tests --no-build -x` | `DeckStatClassifierTests.cs` |
| SCORE-03 / golden | cEDH proxied deck scores Power/Speed ≥ 4; casual ≤ 2 | unit | `dotnet test DeckFlow.Core.Tests --no-build -x` | `MultiAxisScorerTests.cs` |
| SCORE-04 / parity | All three prompt variants emit "DECK SCORE" when score non-null | unit | `dotnet test DeckFlow.Web.Tests --no-build -x` | `AnalysisScorePromptParityTests.cs` (new) |
| SCORE-04 / absent | All three variants omit score section when scoreBlockText is null | unit | `dotnet test DeckFlow.Web.Tests --no-build -x` | `AnalysisScorePromptParityTests.cs` |

### Sampling Rate
- **Per task commit:** `dotnet test DeckFlow.Core.Tests --no-build -x` (fast, Core only)
- **Per wave merge:** `dotnet test DeckFlow.sln` (full suite, all 4 projects)
- **Phase gate:** full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `DeckFlow.Core.Tests/MultiAxisScorerTests.cs` — covers SCORE-01 band labels + SCORE-02 axis derivations + SCORE-03 golden test
- [ ] `DeckFlow.Web.Tests/AnalysisScorePromptParityTests.cs` — covers SCORE-04 parity

*(Existing `DeckStatClassifierTests.cs` and `DeckStatAggregatorTests.cs` will be extended, not created fresh.)*

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|------------------|--------|
| Single `assessed_bracket` from AI response | DeckFlow-computed bracket (Phase 76) + 4-axis DeckFlow score (Phase 77) | User gets a local signal before the AI round-trip; AI still refines |
| AI infers "speed" as a string | DeckFlow computes Speed band 0-5 from avg MV + fast mana + ramp | Consistent, comparable signal across decks |

**No deprecated approaches** for this feature; it is entirely new.

---

## Open Questions (RESOLVED)

1. **Combo always-fetch when score flag is ON**
   - What we know: the existing `comboTask` is gated on `RequiresComboLookup(selectedQuestions)`; if no combo question selected, comboResult is null, degrading Power/Consistency accuracy.
   - RESOLVED: WIDEN the single existing combo gate to fire once when `scoreEnabled || RequiresComboLookup(selectedQuestions)`, reusing the one `comboResult` for both the prompt combo-reference text and the score's combo-density signal. Implemented in 77-04 Task 3 (Pitfall 4). Do NOT add a second parallel `comboForScoreTask` — that double-calls Commander Spellbook when a combo question is also selected (Codex HIGH). The prompt-side `RequiresComboLookup` gate still decides whether combo reference TEXT is emitted, so no-combo-question prompt output is unchanged.

2. **`IsTutorCard` false-positive rate for land-tutors**
   - What we know: `Cultivate`, `Farseek`, `Nature's Lore` contain "search your library" and "land" — should NOT be tutors for the Consistency axis.
   - RESOLVED: require both "search your library" AND absence of all land-related phrases ("basic land"/"land card"/"land onto the battlefield"). Implemented in 77-01 Task 1 (Pitfall 6 exclusion list); executor calibrates against a sample of known tutors and ramp spells.

3. **Band cutpoint calibration**
   - What we know: UI-SPEC §3 says thresholds are illustrative; the executor tunes exact cutpoints. The golden test catches gross miscalibration (cEDH < Power 4 would fail).
   - RESOLVED: executor calibrates cutpoints via the golden-test loop in 77-02 Task 2, validating against at least three known decks: a precon (Power ≤ 1), a focused-casual (Power 2-3), and a cEDH list (Power 4-5).

4. **`ScoreJson` vs cache-only score persistence**
   - What we know: `commandZoneAwareness` ON bypasses the session cache (line 757). The hidden-field approach is more robust but adds a new form field and JSON round-trip.
   - RESOLVED: implement `ScoreJson` hidden field (mirrors `DeckProfileJson`) — the established multi-step persistence pattern. Implemented in 77-04 Task 2.

---

## Environment Availability

Step 2.6: SKIPPED — no external dependencies identified for the scoring feature itself. The only external calls are:
- Scryfall (already in the analysis flow, no change)
- Commander Spellbook (already in the analysis flow; adds always-on fetch when score flag is ON)
- `IGameChangerCatalogService.GetCatalog()` is in-memory (no HTTP)

---

## Package Legitimacy Audit

No new packages are introduced by Phase 77. All implementation uses in-solution code.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `IGameChangerCatalogService.GetCatalog()` is synchronous and returns the in-memory catalog without HTTP | Phase-76 APIs | If it were async/HTTP, the `BuildAsync` wiring would need await; Phase 76 research confirms it is in-memory cache |
| A2 | `DeckStatAggregator.Compute()` calling `DeckStatClassifier.IsInteractionCard()` counts counters within the Interaction total but does NOT separate them | Signals inventory | If counters are already separately counted, Phase 77 wouldn't need `Counters` field; verified no separate counter tally exists in current code |
| A3 | The Gemini variant follows the same structural pattern as ChatGPT (markdown, same param order) | Prompt Builder | Only ChatGPT and Claude variants were read in detail; Gemini file confirmed to exist but not fully read — same interface, same approach |
| A4 | `DeckAnalysisPacketResult` is a `sealed record`; adding `DeckMultiAxisScore? Score` as a new optional parameter is additive and backward-compatible | Score persistence | If any code does exhaustive pattern matching on the record fields it would break; checked DeckController and no such matching exists |

---

## Sources

### Primary (HIGH confidence)
- `DeckFlow.Core/Analysis/DeckStatClassifier.cs` — predicates, their oracle-text patterns
- `DeckFlow.Core/Analysis/DeckStatAggregator.cs` — `Compute()` method, `DeckStatSummary` fields
- `DeckFlow.Core/Manabase/ManabaseClassifier.cs` — `fastMana` count (lines 214-216), `rampUnderThree` (line 179-181)
- `DeckFlow.Core/Bracket/BracketClassifier.cs` — classifier API, GC detection
- `DeckFlow.Core/Bracket/BracketClassification.cs` — record fields consumed by scorer
- `DeckFlow.Core/Bracket/BracketRubricThresholds.cs` — threshold constants
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` — `BuildAsync` flow, card reference bundle, Step-3 early return, `CardReference` record (line 1815), `BuildDeckStatsText` (line 1123)
- `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` — interface signature
- `DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs` — insertion region
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — seed SQL patterns
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — catalog entry pattern
- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` lines 519-554 — Step-3 result panel structure
- `DeckFlow.Web/Models/DeckAnalysisRequest.cs` — hidden-field round-trip pattern
- `DeckFlow.Web.Tests/Bracket/BracketPromptVariantParityTests.cs` — parity test template

### Secondary (MEDIUM confidence)
- `DeckFlow.Web/Services/Bracket/BracketClassificationService.cs` — combo-mapping code (lines 99-104) referenced as a template for the same mapping in `BuildAsync`
- `UI-SPEC.md` (same phase directory) — microcopy, CSS classes, band labels, artifact format, responsive grid
- `docs/decisions/0001-prompt-variants-decoupled.md` — ADR-0001 rationale confirmed
- `.planning/STATE.md` — Phase 77 decisions (no shared helper, no new tile)
- `.planning/REQUIREMENTS.md` — SCORE-01..04 verbatim

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all existing tools verified
- Architecture (signal locations, API shapes): HIGH — verified line-by-line in source
- Band cutpoints: LOW — placeholder thresholds; executor must calibrate
- Parity test pattern: HIGH — verified from working bracket parity test

**Research date:** 2026-06-29
**Valid until:** 60 days (stable codebase, few external API changes expected)
