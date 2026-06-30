# Phase 77: Multi-Axis Deck Score - Pattern Map

**Mapped:** 2026-06-29
**Files analyzed:** 21 (4 new, 17 modified)
**Analogs found:** 21 / 21

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Core/Analysis/MultiAxisScore.cs` | model | transform | `DeckFlow.Core/Bracket/BracketClassification.cs` | exact (sealed record with positional params, same Core pattern) |
| `DeckFlow.Core/Analysis/MultiAxisScorer.cs` | service | transform | `DeckFlow.Core/Bracket/BracketClassifier.cs` | exact (pure static classifier, same Core pattern) |
| `DeckFlow.Core/Analysis/DeckStatClassifier.cs` | utility | transform | self — existing predicates in same file | exact |
| `DeckFlow.Core/Analysis/DeckStatAggregator.cs` | utility | transform | self — existing tally loop in same file | exact |
| `DeckFlow.Web/Models/DeckAnalysisRequest.cs` | model | request-response | self — `DeckProfileJson` field, lines 125-132 | exact (hidden-field round-trip pattern) |
| `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` | model | request-response | self — existing `init`-only properties | exact |
| `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` | service | request-response | self — `ReferenceDeckStatsFlag` gate (lines 647-652), `companionName` injection (lines 680-698) | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` | config | — | self — `analysis.command-zone-awareness` entry | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` | config | — | self — seed rows at lines 227-228 / 261-262 | exact |
| `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` | interface | request-response | self — existing `companionName = null` param at line 37 | exact |
| `DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs` | service | request-response | self + `ClaudeAnalysisPromptVariant.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs` | service | request-response | self + `ChatGptAnalysisPromptVariant.cs` | exact |
| `DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs` | service | request-response | `ChatGptAnalysisPromptVariant.cs` (same markdown pattern) | role-match |
| `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` | component | request-response | self — `@if (Model.AnalysisResponse is not null)` block, lines 519-564 | exact |
| `DeckFlow.Web/wwwroot/css/site-common.css` | utility | — | `.manabase-health--*` (lines 2564-2586), `.bracket-callout` (lines 1852-1868), `.manabase-lens` (lines 2602-2676) | exact |
| `DeckFlow.Core.Tests/DeckStatClassifierTests.cs` | test | — | self — existing `[Theory]`/`[InlineData]` predicate tests | exact |
| `DeckFlow.Core.Tests/DeckStatAggregatorTests.cs` | test | — | self — `Card()` factory + `Compute_TalliesRoles*` pattern | exact |
| `DeckFlow.Core.Tests/MultiAxisScorerTests.cs` | test | — | `DeckFlow.Core.Tests/Bracket/BracketClassifierTests.cs` | exact |
| `DeckFlow.Web.Tests/AnalysisScorePromptParityTests.cs` | test | — | `DeckFlow.Web.Tests/Bracket/BracketPromptVariantParityTests.cs` | exact |

---

## Pattern Assignments

### `DeckFlow.Core/Analysis/MultiAxisScore.cs` (model, transform)

**Analog:** `DeckFlow.Core/Bracket/BracketClassification.cs`

**Sealed record with positional params** (BracketClassification.cs lines 32-39):
```csharp
public sealed record BracketClassification(
    int BracketNumber,
    IReadOnlyList<string> DetectedGameChangers,
    IReadOnlyList<string> DetectedMassLandDenial,
    IReadOnlyList<string> DetectedExtraTurnCards,
    IReadOnlyList<TwoCardCombo>? TwoCardCombos,
    bool ComboDetectionAvailable,
    string EffectiveDate)
```

**Apply this pattern for `DeckMultiAxisScore`:** positional params, `sealed record`, file-scoped namespace `DeckFlow.Core.Analysis;`, XML doc on every public type and property.

**New file structure to copy:**
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

---

### `DeckFlow.Core/Analysis/MultiAxisScorer.cs` (service, transform)

**Analog:** `DeckFlow.Core/Bracket/BracketClassifier.cs`

**Static class with `ArgumentNullException.ThrowIfNull`** (BracketClassifier.cs lines 19-40):
```csharp
public static class BracketClassifier
{
    public static BracketClassification Classify(
        IReadOnlyList<DeckEntry> entries,
        GameChangerCatalog catalog,
        IReadOnlyList<TwoCardCombo>? twoCardCombos)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(catalog);
        // ...
    }
}
```

**Band derivation pattern** — switch expression over int (BracketClassifier.cs lines 79-99):
```csharp
if (detectedGCs.Count >= BracketRubricThresholds.CedhGameChangerCount)
{
    bracketNumber = 5;
}
else if (detectedMld.Count > 0
    || combos.Count > 0
    || detectedGCs.Count >= BracketRubricThresholds.HardFloorGameChangerCount)
{
    bracketNumber = 4;
}
else if (detectedGCs.Count >= BracketRubricThresholds.MinGameChangersForB3)
{
    bracketNumber = 3;
}
else
{
    bracketNumber = BracketRubricThresholds.ZeroSignalBracket;
}
```

Apply same chained-if threshold pattern for each axis band (not switch expressions — avoids re-indent carve-out risk).

**`BandLabel` switch expression:**
```csharp
public static string BandLabel(int band) => band switch
{
    0 => "None", 1 => "Low", 2 => "Modest", 3 => "Moderate", 4 => "High", _ => "Extreme"
};
```

**Null-vs-empty semantics** (from BracketClassifier.cs lines 63-68 — exact pattern to replicate for combo availability):
```csharp
bool comboAvailable = twoCardCombos is not null;

// null means detection unavailable; do NOT treat as zero.
var combos = twoCardCombos ?? (IReadOnlyList<TwoCardCombo>)[];
```

---

### `DeckFlow.Core/Analysis/DeckStatClassifier.cs` (utility, transform) — MODIFIED

**Analog:** self — existing predicates, lines 16-107

**Predicate method signature pattern** (lines 16-23):
```csharp
/// <summary>
/// Returns <see langword="true"/> when the card is a ramp source: ...
/// </summary>
/// <param name="typeLine">Card type line (e.g. "Artifact — Treasure").</param>
/// <param name="oracleText">Normalized oracle text.</param>
public static bool IsRampCard(string typeLine, string oracleText)
    => typeLine.Contains("Land", StringComparison.OrdinalIgnoreCase)
        || oracleText.Contains("add one mana", StringComparison.OrdinalIgnoreCase)
        || ...;
```

**Four new predicates to add** using this exact `|| oracle.Contains(..., OrdinalIgnoreCase)` chaining pattern:

`IsTutorCard(string oracleText)` — single-param (no typeLine needed):
- Match: `oracleText.Contains("search your library for", OrdinalIgnoreCase)`
- Exclude: `&& !oracleText.Contains("basic land", OrdinalIgnoreCase) && !oracleText.Contains("land card", OrdinalIgnoreCase) && !oracleText.Contains("land onto the battlefield", OrdinalIgnoreCase)`

`IsFastManaCard(string typeLine, string oracleText, string manaCost)` — three params:
- Mirror `ManabaseClassifier.cs` line 214: `card.ManaValue == 0 && IsType(card.TypeLine, "Artifact") && ProducesMana(card)`
- In DeckStatClassifier terms: `DeckStatAggregator.EstimateManaValue(manaCost) == 0 && typeLine.Contains("Artifact", OrdinalIgnoreCase) && (oracleText.Contains("{T}: Add", OrdinalIgnoreCase) || oracleText.Contains("Add {", OrdinalIgnoreCase))`

`IsRampOrDrawUnderThreeMv(string typeLine, string oracleText, string manaCost)` — three params:
- `DeckStatAggregator.EstimateManaValue(manaCost) <= 2 && (IsRampCard(typeLine, oracleText) || IsDrawCard(oracleText))`

`IsCounterspellCard(string oracleText)` — single-param:
- `oracleText.Contains("counter target spell", OrdinalIgnoreCase)`

---

### `DeckFlow.Core/Analysis/DeckStatAggregator.cs` — MODIFIED (includes `DeckStatSummary`)

**Analog:** self — tally loop, lines 79-157; `DeckStatSummary` record, lines 28-39

**`DeckStatSummary` record — additive `{ get; init; }` fields only** (cycle discipline):

The existing record uses positional params (lines 28-39). The new four fields must be added with `{ get; init; }` as trailing optional members — NOT as new positional params — to preserve backward compatibility:
```csharp
// ADD after the last positional param ClosingPower:
/// <summary>Count of tutor effects (search library for non-land card).</summary>
public int Tutors { get; init; }

/// <summary>Count of 0-cost mana artifacts (fast mana: Mana Crypt, Jeweled Lotus, etc.).</summary>
public int FastMana { get; init; }

/// <summary>Count of ramp or draw pieces with estimated mana value &lt;= 2.</summary>
public int RampDrawUnderThreeMv { get; init; }

/// <summary>Count of cards that counter target spells (subset of Interaction).</summary>
public int Counters { get; init; }
```

**Tally variable declaration pattern** (DeckStatAggregator.cs lines 67-77):
```csharp
var totalCards = 0;
var nonlandCardCount = 0;
var manaValueTotal = 0m;
var lands = 0;
// ... (one var per tally)
var ramp = 0;
var draw = 0;
var interaction = 0;
```

Add four more `var` declarations: `var tutors = 0; var fastMana = 0; var rampDrawUnderThreeMv = 0; var counters = 0;`

**Classifier call pattern inside the foreach loop** (lines 128-156):
```csharp
if (DeckStatClassifier.IsRampCard(typeLine, oracleText))
{
    ramp += quantity;
}

if (DeckStatClassifier.IsDrawCard(oracleText))
{
    draw += quantity;
}
```

Add four analogous blocks after the existing ones (still inside `foreach`, after the land-skip `continue`). The `IsFastManaCard` and `IsRampOrDrawUnderThreeMv` calls need `card.ManaCost` as third arg.

**Return statement** (lines 161-172): add the four new fields to `DeckStatSummary` constructor with the tallied values:
```csharp
return new DeckStatSummary(
    totalCards,
    lands,
    creatures,
    averageManaValue,
    curveBuckets,
    ramp,
    draw,
    interaction,
    wipes,
    recursion,
    closingPower)
{
    Tutors = tutors,
    FastMana = fastMana,
    RampDrawUnderThreeMv = rampDrawUnderThreeMv,
    Counters = counters,
};
```

---

### `DeckFlow.Web/Models/DeckAnalysisRequest.cs` — MODIFIED

**Analog:** self — `DeckProfileJson` hidden-field pattern, lines 125-132

**Hidden-field round-trip property pattern:**
```csharp
private string _deckProfileJson = string.Empty;

/// <summary>
/// Serialized deck-profile JSON round-tripped between workflow steps and through the analysis artifact zip.
/// </summary>
public string DeckProfileJson
{
    get => _deckProfileJson;
    set => _deckProfileJson = value ?? string.Empty;
}
```

**Apply for `ScoreJson`:** add a `_scoreJson` backing field initialized to `string.Empty`, then a public property `ScoreJson` with the same null-guard setter. Place it immediately after `DeckProfileJson`.

---

### `DeckFlow.Web/Models/DeckAnalysisViewModel.cs` — MODIFIED

**Analog:** self — `init`-only properties, lines 8-103

**`init`-only property pattern** (lines 22-24):
```csharp
/// <summary>
/// Gets whether the <c>analysis.command-zone-awareness</c> feature flag is enabled. ...
/// </summary>
public bool CommandZoneAwarenessEnabled { get; init; }
```

Add after `CommandZoneAwarenessEnabled`:
```csharp
/// <summary>
/// Gets the four-axis deck score computed at Step 2; null when the
/// <c>analysis.multi-axis-score</c> flag is off or the score was not computed.
/// </summary>
public DeckMultiAxisScore? Score { get; init; }
```

---

### `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (DeckAnalysisPacketResult) — MODIFIED

**Analog:** self — existing optional record params, lines 57-62

**Optional positional param pattern** (lines 57-62):
```csharp
public sealed record DeckAnalysisPacketResult(
    string InputSummary,
    ...
    DeckAnalysisResponse? AnalysisResponse = null,
    SetUpgradeResponse? SetUpgradeResponse = null,
    string? ImportWarning = null,
    string? ResolvedCommanderName = null,
    string? DecklistText = null,
    IReadOnlyDictionary<string, string>? SetUpgradeCardText = null);
```

Add `DeckMultiAxisScore? Score = null` as a new trailing optional param. Must be last or before other nullables with defaults — check param ordering to avoid CS1737.

---

### `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (BuildAsync scoring wiring) — MODIFIED

**Analog:** self — `ReferenceDeckStatsFlag` gate + `BuildDeckStatsText()` call (lines 647-654); `commandZoneAwareness` flag gate (lines 673-698); `BuildAnalysisPrompt()` call (line 705)

**Flag gate pattern** (lines 647-652 — EXACT pattern to replicate for score):
```csharp
var deckStatsEnabled = _flagCache is not null
    && _flagCache.Snapshot().TryGetValue(ReferenceDeckStatsFlag, out var deckStatsOn)
    && deckStatsOn;
var deckStatsText = deckStatsEnabled
    ? BuildDeckStatsText(cardReferenceBundle.CardReferences)
    : string.Empty;
```

**Score flag gate to add** (after `comboResult` is awaited at line 656, before line 705):
```csharp
internal const string MultiAxisScoreFlag = "analysis.multi-axis-score";

var scoreEnabled = _flagCache is not null
    && _flagCache.Snapshot().TryGetValue(MultiAxisScoreFlag, out var scoreOn)
    && scoreOn;

string? scoreBlockText = null;
DeckMultiAxisScore? score = null;
if (scoreEnabled)
{
    // Mirror BracketClassificationService.cs lines 99-104 for the combo mapping.
    IReadOnlyList<TwoCardCombo>? twoCardCombosForScore = comboForScoreResult is null
        ? null
        : comboForScoreResult.IncludedCombos
            .Where(c => c.CardNames.Count == 2)
            .Select(c => new TwoCardCombo(c.CardNames, c.Results))
            .ToList();

    // Mirror BuildDeckStatsText input-preparation pattern (lines 1130-1132).
    var scoreInputs = cardReferenceBundle.CardReferences
        .Where(card => IsCurrentDeckScope(card.Scope) && !card.IsCommander)
        .Select(card => new DeckStatCardInput(card.Quantity, card.TypeLine, card.OracleText, card.ManaCost));
    var stats = DeckStatAggregator.Compute(scoreInputs);

    // Mirror BracketClassificationService.cs lines 106-109 for catalog + classify.
    GameChangerCatalog catalog = _catalogService.GetCatalog();
    BracketClassification bracketClassification = BracketClassifier.Classify(
        deckEntries, catalog, twoCardCombosForScore);

    int twoCardComboCount = twoCardCombosForScore?.Count ?? 0;
    bool comboAvailable = twoCardCombosForScore is not null;

    score = MultiAxisScorer.Score(
        stats,
        gameChangerCount: bracketClassification.DetectedGameChangers.Count,
        twoCardComboCount: twoCardComboCount,
        comboDetectionAvailable: comboAvailable,
        bracketNumber: bracketClassification.BracketNumber);

    scoreBlockText = BuildScoreBlockText(score);
}
```

**`BuildAnalysisPrompt` call (line 1191)** — add `scoreBlockText` as trailing arg mirroring the `companionName` addition:
```csharp
internal string BuildAnalysisPrompt(
    DeckAnalysisRequest request,
    string decklistText,
    string referenceText,
    string deckProfileSchemaJson,
    string? commanderName,
    IReadOnlyList<string> selectedQuestionIds,
    IReadOnlyList<string> bannedCards,
    CommanderSpellbookResult? comboResult = null,
    bool includeCardVersions = false,
    string? companionName = null,
    string? scoreBlockText = null)  // <-- NEW, last, optional
```

**Step-3 early-return path** (lines 371-392 — mirror `SetUpgradeResponseJson` deserialization):
```csharp
if (request.WorkflowStep == 3
    && string.IsNullOrWhiteSpace(request.DeckSource)
    && !string.IsNullOrWhiteSpace(request.DeckProfileJson))
{
    // ... existing savedAnalysisResponse parsing ...

    // NEW: deserialize ScoreJson if present (mirrors SetUpgradeResponseJson pattern at line 398).
    DeckMultiAxisScore? savedScore = string.IsNullOrWhiteSpace(request.ScoreJson)
        ? null
        : JsonSerializer.Deserialize<DeckMultiAxisScore>(request.ScoreJson);

    return new DeckAnalysisPacketResult(
        ...,
        Score: savedScore);
}
```

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — MODIFIED

**Analog:** self — last two entries in the `Descriptions` dictionary, lines 73-76

**Entry pattern:**
```csharp
["tool.bracket.enabled"] =
    "Enable the Bracket Check tool — auto-classify a Commander deck into its official 1-5 bracket " +
    "and generate a balancer prompt. Off = byte-identical to pre-Phase-76.",
```

**New entry to add** (after `tool.bracket.enabled`):
```csharp
["analysis.multi-axis-score"] =
    "Show a four-axis Power/Speed/Control/Consistency score block in the deck-analysis " +
    "Step-3 results and include the score in all three prompt artifacts. Off = byte-identical.",
```

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — MODIFIED

**Analog:** self — existing `analysis.*` seed rows + `tool.bracket.enabled` last entry, lines 227-263

**Postgres seed row pattern** (lines 227-229):
```sql
('analysis.command-zone-awareness', FALSE),
('tool.bracket.enabled', FALSE)
ON CONFLICT (key) DO NOTHING;
```

**SQLite seed row pattern** (lines 261-263):
```sql
('analysis.command-zone-awareness', 0),
('tool.bracket.enabled', 0)
ON CONFLICT (key) DO NOTHING;
```

Add before the `ON CONFLICT` line in both blocks:
```sql
-- Postgres: ('analysis.multi-axis-score', FALSE)
-- SQLite:   ('analysis.multi-axis-score', 0)
```

The last item before `ON CONFLICT` loses its trailing comma; add comma to the previous last item and make the new item the last without a comma.

---

### `DeckFlow.Web/Services/PromptBuilders/Analysis/IAnalysisPromptVariant.cs` — MODIFIED

**Analog:** self — `companionName = null` optional param at line 37

**Current interface signature** (lines 27-37):
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
    string? companionName = null);
```

**After modification** — add `scoreBlockText` as the new last optional param:
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
    string? scoreBlockText = null);   // <-- NEW
```

Also add `<param name="scoreBlockText">` XML doc tag mirroring the `companionName` doc style.

---

### `ChatGptAnalysisPromptVariant.cs` / `ClaudeAnalysisPromptVariant.cs` / `GeminiAnalysisPromptVariant.cs` — MODIFIED

**Analog:** each file self — the `companionName` optional-param addition and its guard block

**companionName guard pattern** (ChatGptAnalysisPromptVariant.cs lines 65-69):
```csharp
if (!string.IsNullOrWhiteSpace(companionName))
{
    builder.AppendLine($"companion: {companionName} (this deck's companion; applies its companion deckbuilding restriction)");
}
```

**Score block insertion** (after the `## DECK CONTEXT` / metadata block, before `## EVIDENCE RULES` in ChatGPT/Gemini; after the `<commander>` block in Claude):
```csharp
if (!string.IsNullOrWhiteSpace(scoreBlockText))
{
    builder.AppendLine();
    builder.AppendLine(scoreBlockText);
}
```

**ADR-0001 critical rule:** each variant adds the `scoreBlockText` parameter to its own `Build()` signature and inserts it at its own chosen position. The `scoreBlockText` string is built ONCE in `DeckAnalysisPacketService.BuildScoreBlockText(score)` and passed as an argument. Do NOT build the block inside a variant. Do NOT extract a shared helper across variants.

**Build method signature update** (ChatGptAnalysisPromptVariant.cs lines 24-34 — add `string? scoreBlockText = null` as last param):
```csharp
public string Build(
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
    string? scoreBlockText = null)
```

Same change applied to `ClaudeAnalysisPromptVariant.cs` lines 26-36 and the analogous block in `GeminiAnalysisPromptVariant.cs`.

---

### `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` — MODIFIED

**Analog:** self — existing `@if` guards inside the `@if (Model.AnalysisResponse is not null)` block, lines 519-564; specifically the `@if (!string.IsNullOrWhiteSpace(Model.AnalysisResponse.Commander))` guards

**Insertion point** (between line 530 `<h3>Analysis Summary</h3>` and line 531 `<div class="stack">`):
```html
@if (Model.Score is not null)
{
    <div class="chatgpt-score" role="region" aria-label="Multi-Axis Deck Score">
        <p class="chatgpt-score__eyebrow">Deck Score &middot; four coarse bands (0&ndash;5) from your decklist signals</p>
        <div class="chatgpt-score-grid">
            @foreach (var (axisLabel, band, rationale) in new[] {
                ("POWER", Model.Score.PowerBand, Model.Score.PowerRationale.SignalText),
                ("SPEED", Model.Score.SpeedBand, Model.Score.SpeedRationale.SignalText),
                ("CONTROL", Model.Score.ControlBand, Model.Score.ControlRationale.SignalText),
                ("CONSISTENCY", Model.Score.ConsistencyBand, Model.Score.ConsistencyRationale.SignalText),
            })
            {
                <div class="chatgpt-score-card chatgpt-score-band--@band"
                     role="group"
                     aria-label="@axisLabel score: @band of 5, @MultiAxisScorer.BandLabel(band)">
                    <div class="chatgpt-score-label">@axisLabel</div>
                    <div class="chatgpt-score-value">@band</div>
                    <div class="chatgpt-score-meter" aria-hidden="true">
                        @for (int pip = 1; pip <= 5; pip++)
                        {
                            <span class="chatgpt-score-pip@(pip <= band ? " chatgpt-score-pip--filled" : "")"></span>
                        }
                    </div>
                    <div class="chatgpt-score-band">@MultiAxisScorer.BandLabel(band)</div>
                    <div class="chatgpt-score-rationale">@rationale</div>
                </div>
            }
        </div>
        <div class="chatgpt-score-crosscheck chatgpt-score-crosscheck--@(Model.Score.ScoreAlignsBracket ? "agree" : "diverge")"
             role="note">
            <span class="chatgpt-score-crosscheck__label">CROSS-CHECK</span>
            @Model.Score.BracketCrossCheckText
        </div>
    </div>
}
```

The flag-OFF path (`Model.Score is null`) produces byte-identical output — same invariant as `@if (Model.CommandZoneAwarenessEnabled)` elsewhere in the view.

---

### `DeckFlow.Web/wwwroot/css/site-common.css` — MODIFIED

**Analog 1:** `.manabase-health--*` baked-color pill pattern (lines 2564-2586):
```css
.manabase-health--excellent {
  background: #166534;
  border-color: #166534;
  color: #ffffff;
}
```

**Apply for band pills** (copy the baked hex + ink pattern — never use semantic `var(--success)` tokens which read differently per theme):
```css
.chatgpt-score-band--0 { background: #cbd5e1; color: #1c1917; }
.chatgpt-score-band--1 { background: #93c5fd; color: #1c1917; }
.chatgpt-score-band--2 { background: #60a5fa; color: #0b1220; }
.chatgpt-score-band--3 { background: #3b82f6; color: #ffffff; }
.chatgpt-score-band--4 { background: #2563eb; color: #ffffff; }
.chatgpt-score-band--5 { background: #1e3a8a; color: #ffffff; }
```

**Analog 2:** `.bracket-callout` left-border callout pattern (lines 1852-1868):
```css
.bracket-callout {
    border: 1px solid var(--line);
    border-left: 4px solid var(--accent-strong);
    border-radius: 8px;
    background: var(--panel-soft-bg);
    padding: 16px;
    margin-bottom: 16px;
}
```

**Apply for `.chatgpt-score-crosscheck`** (same shape; `--agree` uses `var(--success)` left-border, `--diverge` uses `var(--warning)`):
```css
.chatgpt-score-crosscheck {
    border: 1px solid var(--line);
    border-left: 4px solid var(--accent-strong);
    border-radius: 8px;
    background: var(--panel-soft-bg);
    padding: 16px;
    margin-top: 1rem;
}
.chatgpt-score-crosscheck--agree  { border-left-color: var(--success); }
.chatgpt-score-crosscheck--diverge { border-left-color: var(--warning); }
```

**Analog 3:** `.manabase-lens` soft card + `.manabase-lens-big` headline + `.manabase-lens-label` eyebrow (lines 2602-2676):
```css
.manabase-lens {
  background: var(--panel-soft-bg, var(--surface, transparent));
  border: 1px solid var(--line, rgba(0, 0, 0, 0.18));
  border-radius: 12px;
  padding: 0.85rem 1rem;
}
.manabase-lens-label {
  /* uppercase, letter-spaced, --muted */
}
.manabase-lens-big {
  /* large headline in --accent-strong */
}
```

**Apply:** `.chatgpt-score-card` mirrors `.manabase-lens` (soft card shape); `.chatgpt-score-label` mirrors `.manabase-lens-label` (uppercase + letter-spacing); `.chatgpt-score-value` mirrors `.manabase-lens-big` (big numeral).

**Responsive grid** (UI-SPEC §8 — no existing analog; new pattern):
```css
.chatgpt-score-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 1rem; }
@media (max-width: 860px) { .chatgpt-score-grid { grid-template-columns: repeat(2, 1fr); } }
@media (max-width: 520px) { .chatgpt-score-grid { grid-template-columns: 1fr; } }
```

**CSS token placement rule:** New `:root` tokens (`--score-band-{0..5}-bg/ink`) must be added to `site-common.css` `:root` AND duplicated identically in every `site-*.css` theme file (the "full fork" convention). The existing band-color modifiers above are baked hex and do NOT need per-theme overrides, but the convention requires the `:root` block to be present in each theme file for completeness.

---

### `DeckFlow.Core.Tests/DeckStatClassifierTests.cs` — MODIFIED (extension)

**Analog:** self — existing `[Theory]`/`[InlineData]` blocks, lines 14-107

**Section header comment pattern** (lines 13-14):
```csharp
// -----------------------------------------------------------------------
// IsRampCard
// -----------------------------------------------------------------------
```

**True/False Theory pattern** (lines 15-36):
```csharp
[Theory]
[InlineData("Basic Land — Forest", "", true)]
[InlineData("", "add one mana of any color", true)]
public void IsRampCard_TrueCases(string typeLine, string oracleText, bool expected)
{
    Assert.Equal(expected, DeckStatClassifier.IsRampCard(typeLine, oracleText));
}

[Theory]
[InlineData("Creature — Elf", "Tap: deal 1 damage to target creature.", false)]
public void IsRampCard_FalseCases(string typeLine, string oracleText, bool expected)
{
    Assert.Equal(expected, DeckStatClassifier.IsRampCard(typeLine, oracleText));
}
```

**New sections to add** for `IsTutorCard`, `IsFastManaCard`, `IsRampOrDrawUnderThreeMv`, `IsCounterspellCard`. Test naming convention: `{MethodName}_TrueCases` / `{MethodName}_FalseCases`.

Known true cases from RESEARCH:
- `IsTutorCard`: "Search your library for a card" → true; "Search your library for a basic land card" → false (Rampant Growth)
- `IsFastManaCard`: Mana Crypt oracle "{T}: Add {C}{C}." with type "Artifact", manaCost "" → true; Sol Ring manaCost "{1}" → false (MV=1, not 0)
- `IsCounterspellCard`: "Counter target spell." → true; "counter target activated or triggered ability" → false (ability counter, not spell counter)

---

### `DeckFlow.Core.Tests/DeckStatAggregatorTests.cs` — MODIFIED (extension)

**Analog:** self — `Compute_TalliesRolesViaClassifierAndRespectsQuantity` (lines 48-68); `Card()` factory (line 10-11)

**Card factory pattern** (lines 10-11):
```csharp
private static DeckStatCardInput Card(int qty, string type, string oracle, string mana)
    => new(qty, type, oracle, mana);
```

**`[Fact]` test pattern** (lines 48-68):
```csharp
[Fact]
public void Compute_TalliesRolesViaClassifierAndRespectsQuantity()
{
    var summary = DeckStatAggregator.Compute(new[]
    {
        Card(2, "Artifact", "{T}: Add one mana of any color.", "{2}"),   // ramp x2
        Card(1, "Instant", "Counter target spell. Draw a card.", "{U}"), // interaction + draw
        // ...
    });

    Assert.Equal(2, summary.Ramp);
    Assert.Equal(1, summary.Draw);
}
```

**New test for four new fields:**
```csharp
[Fact]
public void Compute_TalliesNewSignalFields()
{
    var summary = DeckStatAggregator.Compute(new[]
    {
        Card(1, "Sorcery", "Search your library for a card, then shuffle.", "{B}"),          // tutor
        Card(2, "Artifact", "{T}: Add {C}{C}.", ""),                                          // fast mana (MV=0, artifact, produces mana)
        Card(1, "Instant", "Counter target spell.", "{U}"),                                    // counterspell
        Card(1, "Sorcery", "Draw two cards.", "{1}{U}"),                                      // ramp/draw under MV 2
    });

    Assert.Equal(1, summary.Tutors);
    Assert.Equal(2, summary.FastMana);   // quantity-weighted
    Assert.Equal(1, summary.Counters);
    Assert.Equal(1, summary.RampDrawUnderThreeMv);
}
```

---

### `DeckFlow.Core.Tests/MultiAxisScorerTests.cs` — NEW

**Analog:** `DeckFlow.Core.Tests/Bracket/BracketClassifierTests.cs`

**File header pattern** (BracketClassifierTests.cs lines 1-13):
```csharp
using DeckFlow.Core.Bracket;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for <see cref="BracketClassifier"/> ...
/// </summary>
public sealed class BracketClassifierTests
{
```

**For MultiAxisScorerTests:**
```csharp
using DeckFlow.Core.Analysis;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for <see cref="MultiAxisScorer"/> — covers band label mapping,
/// axis derivations (SCORE-01/02), and golden test (SCORE-03).
/// </summary>
public sealed class MultiAxisScorerTests
{
```

**Helper factory pattern** (BracketClassifierTests.cs — uses `BuildCatalog(gcCount, ...)` helpers):

For `MultiAxisScorerTests` use `DeckStatSummary` direct construction with `{ init }` field syntax:
```csharp
private static DeckStatSummary CedhStats() => new DeckStatSummary(
    Cards: 99, Lands: 31, Creatures: 25,
    AverageManaValue: 1.8m,
    ManaCurve: new Dictionary<string, int> { ["0-1"] = 30, ["2"] = 20, ["3"] = 15, ["4"] = 10, ["5+"] = 5 },
    Ramp: 15, Draw: 12, Interaction: 18, Wipes: 2, Recursion: 3, ClosingPower: 8)
{
    Tutors = 12, FastMana = 10, RampDrawUnderThreeMv = 18, Counters = 8,
};
```

**`[Theory]`/`[InlineData]` for BandLabel** (mirrors `ParseManaToken` test pattern):
```csharp
[Theory]
[InlineData(0, "None")]
[InlineData(1, "Low")]
[InlineData(2, "Modest")]
[InlineData(3, "Moderate")]
[InlineData(4, "High")]
[InlineData(5, "Extreme")]
[InlineData(6, "Extreme")]   // out-of-range clamps to Extreme
public void BandLabel_MapsCorrectly(int band, string expected)
{
    Assert.Equal(expected, MultiAxisScorer.BandLabel(band));
}
```

**Golden test pattern** (mirrors BracketClassifierTests `[Theory]`/`[InlineData]` gating at lines 18-38):
```csharp
[Fact]
public void Score_CedhDeck_ScoresPowerAndSpeedHigh()
{
    var score = MultiAxisScorer.Score(CedhStats(), gameChangerCount: 8,
        twoCardComboCount: 3, comboDetectionAvailable: true, bracketNumber: 5);
    Assert.True(score.PowerBand >= 4, $"Expected PowerBand >= 4, got {score.PowerBand}");
    Assert.True(score.SpeedBand >= 4, $"Expected SpeedBand >= 4, got {score.SpeedBand}");
}

[Fact]
public void Score_CasualDeck_ScoresPowerAndSpeedLow()
{
    var score = MultiAxisScorer.Score(CasualStats(), gameChangerCount: 0,
        twoCardComboCount: 0, comboDetectionAvailable: true, bracketNumber: 2);
    Assert.True(score.PowerBand <= 2, $"Expected PowerBand <= 2, got {score.PowerBand}");
    Assert.True(score.SpeedBand <= 2, $"Expected SpeedBand <= 2, got {score.SpeedBand}");
}
```

---

### `DeckFlow.Web.Tests/AnalysisScorePromptParityTests.cs` — NEW

**Analog:** `DeckFlow.Web.Tests/Bracket/BracketPromptVariantParityTests.cs` — EXACT template

**File header** (BracketPromptVariantParityTests.cs lines 1-9):
```csharp
// Why: ADR-0001 — bracket prompt variants are intentionally decoupled; test instantiates
// each concrete variant directly without a shared helper (mirrors the same principle in production).
using DeckFlow.Core.Bracket;
using DeckFlow.Web.Models;
using DeckFlow.Web.Services.PromptBuilders.Bracket;
using Xunit;

namespace DeckFlow.Web.Tests;
```

**Registry builder pattern** (lines 19-25 — no shared variant helper):
```csharp
// Why: ADR-0001 — no shared helper; concrete variants instantiated inline.
private static AnalysisPromptVariantRegistry BuildRegistry() =>
    new(new IAnalysisPromptVariant[]
    {
        new ChatGptAnalysisPromptVariant(),
        new ClaudeAnalysisPromptVariant(),
        new GeminiAnalysisPromptVariant(),
    });
```

**`AiPlatform.Normalize` invocation** (line 67):
```csharp
var platform = AiPlatform.Normalize(platformName);
```

**`[Theory]`/`[InlineData]` three-platform pattern** (lines 60-74):
```csharp
[Theory]
[InlineData("ChatGPT")]
[InlineData("Claude")]
[InlineData("Gemini")]
public void Build_ClassificationBlock_AppearsInAllThreeVariants(string platformName)
{
    var registry = BuildRegistry();
    var platform = AiPlatform.Normalize(platformName);
    var result = registry.Build(platform, BuildClassification(), null, null,
        BuildTiers(), BuildCatalog());
    Assert.Contains("WHY THIS BRACKET", result, StringComparison.Ordinal);
}
```

**Score parity tests** (mirror exactly — replace `BuildClassification()` with `BuildRequest(scoreBlockText: "DECK SCORE ...")`):
```csharp
[Theory]
[InlineData("ChatGPT")]
[InlineData("Claude")]
[InlineData("Gemini")]
public void Score_Block_AppearsInAllThreeVariants(string platformName)
{
    var registry = BuildRegistry();
    var platform = AiPlatform.Normalize(platformName);
    var scoreBlockText = "DECK SCORE (coarse 0-5 bands - magnitude, not quality)\n  Power: 4/5 High";

    var result = registry.Build(platform, BuildMinimalRequest(), BuildDecklistText(),
        BuildReferenceText(), BuildSchemaJson(), commanderName: null,
        selectedQuestionIds: [], bannedCards: [], comboResult: null,
        includeCardVersions: false, companionName: null, scoreBlockText: scoreBlockText);

    Assert.Contains("DECK SCORE", result, StringComparison.Ordinal);
}

[Theory]
[InlineData("ChatGPT")]
[InlineData("Claude")]
[InlineData("Gemini")]
public void Score_Block_AbsentWhenScoreBlockTextIsNull(string platformName)
{
    // ... same setup, scoreBlockText: null ...
    Assert.DoesNotContain("DECK SCORE", result, StringComparison.Ordinal);
}
```

---

## Shared Patterns

### Feature flag gate (explicit snapshot, default-OFF)
**Source:** `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` lines 647-652
**Apply to:** Score computation gate in `BuildAsync`
```csharp
var scoreEnabled = _flagCache is not null
    && _flagCache.Snapshot().TryGetValue(MultiAxisScoreFlag, out var scoreOn)
    && scoreOn;
```
Rationale: `IsEnabled()` returns `true` for absent keys (default-on store semantics); the explicit snapshot pattern is REQUIRED for features that must be off-by-default.

### Null-vs-empty combo semantics
**Source:** `DeckFlow.Core/Bracket/BracketClassifier.cs` lines 63-68; `BracketClassificationService.cs` lines 99-104
**Apply to:** `MultiAxisScorer.Score()` parameter `comboDetectionAvailable` + score rationale text
```csharp
// null = detection unavailable; [] = ran and found none. Never conflate.
bool comboAvailable = twoCardCombos is not null;
var combos = twoCardCombos ?? (IReadOnlyList<TwoCardCombo>)[];
```
When unavailable, disclose in rationale: "combo data unavailable" not "0 combos".

### Additive `{ get; init; }` fields on existing records
**Source:** `DeckFlow.Core/Analysis/DeckStatSummary.cs` (inline in DeckStatAggregator.cs, lines 28-39); carve-out guard reference
**Apply to:** All new fields on `DeckStatSummary` and `DeckAnalysisPacketResult`
Never add `required`; never convert to `{ get; }` (System.Text.Json silently skips get-only in .NET 9+, carve-out guard `CarveOutGuard` enforces this).

### Static pure-transform classes with ArgumentNullException guards
**Source:** `DeckFlow.Core/Bracket/BracketClassifier.cs` lines 19-40; `DeckFlow.Core/Analysis/DeckStatAggregator.cs` lines 47-57
**Apply to:** `MultiAxisScorer`
```csharp
public static class MultiAxisScorer
{
    public static DeckMultiAxisScore Score(DeckStatSummary stats, ...)
    {
        ArgumentNullException.ThrowIfNull(stats);
        // ...
    }
}
```

### ADR-0001: scoreBlockText pre-built once, inserted independently by each variant
**Source:** `docs/decisions/0001-prompt-variants-decoupled.md`; `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` line 705 for the dispatch pattern
**Apply to:** All three `*AnalysisPromptVariant.cs` files
`scoreBlockText` is built by `BuildScoreBlockText(score)` in `DeckAnalysisPacketService`. Each variant receives it as an argument and decides its own insertion point. The text building is NOT shared at the variant level — only the pre-built string is shared as an argument.

---

## No Analog Found

All files have close analogs. No entry required.

---

## Metadata

**Analog search scope:** `DeckFlow.Core/`, `DeckFlow.Core.Tests/`, `DeckFlow.Web/Services/`, `DeckFlow.Web/Models/`, `DeckFlow.Web/Views/`, `DeckFlow.Web.Tests/`, `DeckFlow.Web/wwwroot/css/`
**Files scanned:** 19 source files read or grepped
**Pattern extraction date:** 2026-06-29
