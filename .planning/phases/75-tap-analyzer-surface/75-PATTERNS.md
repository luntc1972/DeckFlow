# Phase 75: Tap Analyzer Surface — Pattern Map

**Mapped:** 2026-06-28
**Files analyzed:** 18 (12 production files + 6 test files)
**Analogs found:** 18 / 18 (all are modifications to existing files; one new test file created from existing test analog)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Core/Manabase/CastabilitySimulator.cs` | service | batch (Monte Carlo) | itself — existing `Simulate` counter loop + `AverageDelay` additive output | exact |
| `DeckFlow.Core/Manabase/ManabaseModels.cs` | model | transform | itself — `AverageDelay { get; init; }` + `LandTarget? { get; init; }` patterns | exact |
| `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` | service | batch/transform | itself — `BuildColorFindings`, `EffectiveSources(color, untappedOnly)` | exact |
| `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` | utility | transform | itself — optional `verdict` + `budget` parameters + conditional block append | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` | config | request-response | itself — `["analysis.manabase.commander-castability"]` last entry | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` | config | CRUD | itself — `PostgresSeedSql`/`SqliteSeedSql` last manabase entry | exact |
| `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` | service | request-response | itself — `PlainLanguageVerdictFlagKey` + `plainLanguage` flag read/propagation | exact |
| `DeckFlow.Web/Models/ManabaseViewModel.cs` | model | request-response | itself — `ShowPlainLanguage bool { get; init; }` at line 45 | exact |
| `DeckFlow.Web/Models/ManabaseDisplay.cs` | utility | transform | itself — `CastChip(int castPercent)` at lines 63-76 | exact |
| `DeckFlow.Web/Controllers/ManabaseController.cs` | controller | request-response | itself — ViewModel construction + `Download` Build call | exact |
| `DeckFlow.Web/Views/Deck/Manabase.cshtml` | view | request-response | itself — `@if (showPlainLanguage)` gloss blocks + `.manabase-twolens` chrome | exact |
| `DeckFlow.Web/wwwroot/css/site-common.css` | config | N/A | itself — `.manabase-twolens` + `@media (max-width: 640px)` at lines 2769-2773 | exact |
| `DeckFlow.Core.Tests/Manabase/ManabaseTapAnalysisTests.cs` (NEW) | test | batch | `ManabaseAnalyzerTests.cs` — Core manabase unit test structure | role-match |
| `DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs` | test | transform | itself — `HealthyCasualReport()` fixture + `Build(...)` assertions | exact |
| `DeckFlow.Web.Tests/Manabase/ManabaseDisplayTests.cs` | test | transform | itself — `CastChip_LabelsSeverityByBand` Theory pattern | exact |
| `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` | test | request-response | itself — `[InlineData("analysis.manabase.commander-castability")]` | exact |
| `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` | test | CRUD | itself — `[InlineData("analysis.manabase.commander-castability", false)]` | exact |
| `DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs` | test | request-response | itself — `StubService` with `showPlainLanguage` + file content assertions | exact |

---

## Pattern Assignments

### `DeckFlow.Core/Manabase/CastabilitySimulator.cs` (service, batch — Monte Carlo sim)

**Analog:** itself

**Existing additive `out` param pattern on `SimulateGame`** (lines 470-483):
```csharp
private static bool SimulateGame(
    IReadOnlyList<LibraryCard> library,
    int[] shuffled,
    bool[] active,
    int handCount,
    int turn,
    int effectiveCost,
    (int Bit, int Count)[] pipReq,
    List<(int Mask, int Amount)> availableColors,
    List<int> onlineLandMasks,
    bool gateRampOnCastable,
    out bool manaShort,
    out bool colorShort,
    out int firstCastableTurn)
```
New `out bool hadUntappedT1` appends to this signature (after `firstCastableTurn`).

**Where turn-1 state is evaluable — existing `OnlineMana` call at `currentTurn == 1`** (lines 561-591 show the per-turn loop structure; the T1 hook goes after `PlayOneLand` + `TryDeployRamp` calls, before the `if (currentTurn < turn) continue` guard at line 594):
```csharp
// From loop at line 520: for (int currentTurn = 1; currentTurn <= lastTurn; currentTurn++)
// PlayOneLand called at line 532
// rampSpentThisTurn accumulated at line 560-591
// Hook point for T1 check (after deploy, before early continue at line 594):
if (currentTurn == 1)
{
    hadUntappedT1 = OnlineMana(landsOnBoard, rampOnBoard, 1) > 0;
}

if (currentTurn < turn)
{
    continue;
}
```

**Existing `Simulate` counter accumulation pattern** (lines 228-278) — mirrors existing `successes` and `delaySum` counters:
```csharp
int turn1UntappedSuccesses = 0;  // NEW — mirrors int successes = 0; int delaySum = 0;
for (int t = 0; t < trials; t++)
{
    // ... shuffle, mulligan ...
    bool success = SimulateGame(
        library, shuffled, active, handCount, turn, effectiveCost, pipReq, availableColors,
        onlineLandMasks, gateRampOnCastable,
        out bool manaShort, out bool colorShort, out int firstCastableTurn,
        out bool hadUntappedT1);  // NEW out param
    if (hadUntappedT1) turn1UntappedSuccesses++;  // NEW accumulation
    // ... existing success/delay tracking ...
}
```

**Existing `CardCastability` construction at return** (lines 268-278) — new field appends after `AverageDelay`:
```csharp
return new CardCastability
{
    Name = spell.Name,
    ManaValue = spell.ManaValue,
    OnCurveTurn = turn,
    CastPercent = castPercent,
    LimitingFactor = limiting,
    IsCommander = spell.IsCommander,
    IsCostOverridden = spell.IsCostOverridden,
    AverageDelay = averageDelay,
    Turn1UntappedTrials = turn1UntappedSuccesses,  // NEW
};
```

---

### `DeckFlow.Core/Manabase/ManabaseModels.cs` (model, transform)

**Analog:** itself

**Pattern A — additive non-required field on `CardCastability`** (line 168):
```csharp
// Existing (CardCastability, line 168) — safe default 0, no `required`:
public double AverageDelay { get; init; }

// New field mirrors exactly:
public int Turn1UntappedTrials { get; init; }
```

**Pattern B — additive non-required field on `ColorSourceFinding`** (lines 315-392 show all fields are `{ get; init; }`, none `required`):
```csharp
// New field — safe default 0.0, same style as existing double fields on the record:
public double UntappedSources { get; init; }
```

**Pattern C — optional nullable additive field on `ManabaseReport`** (line 853):
```csharp
// Existing (ManabaseReport, line 853):
public ManabaseLandTargetBreakdown? LandTarget { get; init; }

// New field mirrors:
public ManabaseTapAnalysis? TapAnalysis { get; init; }
```

**Pattern D — new `sealed record` with `{ get; init; }` fields and safe defaults** (mirrors `CardCastability` record at lines 133-169):
```csharp
/// <summary>Tap-quality metrics: untapped-source composition + turn-1 untapped availability.</summary>
public sealed record ManabaseTapAnalysis
{
    /// <summary>Overall untapped fraction (0–100) across all weighted sources.</summary>
    public int OverallUntappedPercent { get; init; }

    /// <summary>Weighted untapped source count (numerator for OverallUntappedPercent).</summary>
    public double UntappedSources { get; init; }

    /// <summary>Total weighted source count (denominator).</summary>
    public double TotalSources { get; init; }

    /// <summary>
    /// Share of simulated games where the player had ≥1 mana source available on turn 1 (0–100).
    /// Averaged across non-commander castability rows.
    /// </summary>
    public int Turn1UntappedPercent { get; init; }

    /// <summary>Per-color untapped composition (key = ManaColor).</summary>
    public IReadOnlyDictionary<ManaColor, ColorTapFinding> ColorTap { get; init; }
        = new Dictionary<ManaColor, ColorTapFinding>();
}

/// <summary>One color's untapped-source composition.</summary>
public sealed record ColorTapFinding
{
    /// <summary>Weighted untapped sources of this color.</summary>
    public double UntappedSources { get; init; }

    /// <summary>Raw weighted total sources of this color (un-rounded EffectiveSources; ColorSourceFinding.ActualSources is the rounded display value).</summary>
    public double TotalSources { get; init; }

    /// <summary>Rounded untapped fraction (0–100).</summary>
    public int UntappedPercent { get; init; }
}
```

**Critical carve-out (CLAUDE.md + CarveOutGuard):** All new fields MUST use `{ get; init; }`, never `{ get; }`. System.Text.Json silently skips get-only properties in .NET 9+. The `AverageDelay` pattern (no `required`, no default literal needed) is the authoritative model.

---

### `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` (service, batch/transform)

**Analog:** itself

**Existing `EffectiveSources(color, untappedOnly)` call** (lines 442-443 — the values are already computed; store the RAW untapped on the new field):
```csharp
// Current (ManabaseAnalyzer.cs:442-443) — raw locals; untappedSources not yet stored:
double allSources = EffectiveSources(deck, color, untappedOnly: false);
double untappedSources = EffectiveSources(deck, color, untappedOnly: true);

// The finding stores ActualSources = Math.Round(allSources, 1) (:565) — ROUNDED for display.
// New: store the RAW untappedSources on ColorSourceFinding.UntappedSources when building the finding.
// Codex HIGH-2: ComputeTapAnalysis must NOT use the rounded ActualSources for tap totals — it
// recomputes the raw per-color total via EffectiveSources(deck, color, untappedOnly: false).
```

**New private `ComputeTapAnalysis` method** — follows the style of other private static computation methods in the class (all take `ManabaseDeck`, `IReadOnlyList<CardCastability>`, etc.):
```csharp
// Pattern for new private method (mirrors BuildColorFindings, BuildCastability style):
private static ManabaseTapAnalysis ComputeTapAnalysis(
    ManabaseDeck deck,
    IReadOnlyList<ColorSourceFinding> colorFindings,
    IReadOnlyList<CardCastability> castability,
    int defaultTrials)
{
    // TAP-01 composition + per-color breakdown.
    // Codex HIGH-2 / D5: tap totals use the RAW (un-rounded) EffectiveSources count, NOT the rounded
    // display field ColorSourceFinding.ActualSources (= Math.Round(allSources, 1)). Numerator is the
    // RAW ColorSourceFinding.UntappedSources stored in BuildColorFindings. Reusing ActualSources would
    // skew whole-percent tap outputs.
    double totalUntapped = 0.0;
    double totalAll = 0.0;
    var colorTap = new Dictionary<ManaColor, ColorTapFinding>();
    foreach (ColorSourceFinding f in colorFindings)
    {
        double rawUntapped = f.UntappedSources;                                // RAW (stored un-rounded)
        double rawTotal = EffectiveSources(deck, f.Color, untappedOnly: false); // RAW total, not f.ActualSources
        totalUntapped += rawUntapped;
        totalAll += rawTotal;
        colorTap[f.Color] = new ColorTapFinding
        {
            UntappedSources = rawUntapped,
            TotalSources = rawTotal,
            UntappedPercent = rawTotal > 0
                ? (int)Math.Round(100.0 * rawUntapped / rawTotal)
                : 0,
        };
    }
    int overallPct = totalAll > 0
        ? (int)Math.Round(100.0 * totalUntapped / totalAll)
        : 0;

    // TAP-02: T1 average across non-commander rows (exclude commanders per Pitfall 6)
    var nonCmdRows = castability.Where(r => !r.IsCommander).ToList();
    IReadOnlyList<CardCastability> avgRows = nonCmdRows.Count > 0 ? nonCmdRows : castability;
    int t1Pct = avgRows.Count > 0 && defaultTrials > 0
        ? (int)Math.Round(100.0 * avgRows.Average(r => r.Turn1UntappedTrials) / defaultTrials)
        : 0;

    return new ManabaseTapAnalysis
    {
        OverallUntappedPercent = overallPct,
        UntappedSources = totalUntapped,
        TotalSources = totalAll,
        Turn1UntappedPercent = t1Pct,
        ColorTap = colorTap,
    };
}
```

**`DefaultTrials` constant** — already exists in `CastabilitySimulator`; expose or pass through `ManabaseAnalyzer.Analyze`:
```csharp
// CastabilitySimulator.cs has: private const int DefaultTrials = 20_000;
// ManabaseAnalyzer.Analyze can use the value directly or CastabilitySimulator.DefaultTrials
// (if made internal). Alternatively hard-code 20_000 with a comment referencing the sim constant.
```

---

### `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` (utility, transform)

**Analog:** itself

**Existing optional parameter pattern — signature** (lines 26-32):
```csharp
// Current signature:
public static string Build(
    ManabaseReport report,
    string? deckName,
    string? decklistText,
    ManabaseMode mode = ManabaseMode.Casual,
    ManabaseVerdict? verdict = null,
    ManabaseRampDrawBudget? budget = null)

// Extended signature (new parameter at end, null = skip block = byte-identical):
public static string Build(
    ManabaseReport report,
    string? deckName,
    string? decklistText,
    ManabaseMode mode = ManabaseMode.Casual,
    ManabaseVerdict? verdict = null,
    ManabaseRampDrawBudget? budget = null,
    ManabaseTapAnalysis? tap = null)
```

**Existing conditional block pattern** (lines 89-93 — `verdict is not null` guard):
```csharp
// Existing optional block (verdict):
if (verdict is not null)
{
    AppendVerdictBlock(sb, verdict, budget);
    sb.AppendLine();
}

// New optional block (tap) — insert after the "Color Sources:" table block (line 111):
if (tap is not null)
{
    AppendTapAnalysisBlock(sb, tap, report.ColorFindings.Count);
    sb.AppendLine();
}
```

**Existing column-formatted section pattern** (lines 96-111 — "Color Sources:" block):
```csharp
// Existing column block:
sb.AppendLine("Color Sources:");
sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
    $"{"Color",-10} {"Actual",8} {"Needed",7} {"Deficit",8}  Driving spell"));
sb.AppendLine(new string('-', 60));
foreach (ColorSourceFinding f in report.ColorFindings)
{
    string deficitOrOk = f.IsAdequate ? "OK" : string.Create(...);
    sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{f.Color,-10} ..."));
}
sb.AppendLine();

// New tap block (mirrors column style per UI-SPEC Section 9):
// private static void AppendTapAnalysisBlock(StringBuilder sb, ManabaseTapAnalysis tap, int colorCount)
// {
//     sb.AppendLine("Untapped Sources:");
//     sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
//         $"Turn-1 untapped availability: {tap.Turn1UntappedPercent}% " +
//         "(share of games with an untapped source to spend on turn 1)"));
//     sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
//         $"Overall: {tap.OverallUntappedPercent}% of colored sources enter untapped"));
//     if (colorCount > 1)
//     {
//         sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
//             $"{"Color",-12} {"Untapped",10}   Sources"));
//         sb.AppendLine(new string('-', 60));
//         foreach ((ManaColor color, ColorTapFinding f) in tap.ColorTap)
//         {
//             sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
//                 $"{color,-12} {f.UntappedPercent,9}%   {f.UntappedSources:F1} of {f.TotalSources:F1}"));
//         }
//     }
// }
```

**Insertion point:** After the `}` closing the `ColorSourceFinding` loop (line 110's `sb.AppendLine()`), before the "Biggest fix callout" block at line 113.

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (config, request-response)

**Analog:** itself — the manabase-family entries in `Descriptions`. The manabase family ENDS at `["analysis.manabase.commander-castability"]` (lines 67-68); `["analysis.command-zone-awareness"]` (lines 69-70) is a DIFFERENT family.

**Exact insertion pattern** (between the commander-castability entry :68 and command-zone-awareness :69, keeping the new key grouped with the manabase family):
```csharp
// Manabase family ENDS here (lines 67-68):
["analysis.manabase.commander-castability"] =
    "Shows command-zone castability ...",

// New manabase-family entry inserts directly AFTER commander-castability:
["analysis.manabase.tap-analyzer"] =
    "Surface untapped-source frequency and turn-1 untapped availability on the mana base page and its " +
    "paste artifact. Off = byte-identical output.",

// Different family (analysis.*, not manabase) — leave AFTER the new entry, do not treat as end-of-manabase:
["analysis.command-zone-awareness"] =
    "Names the full command zone ...",
```

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (config, CRUD)

**Analog:** itself — existing `PostgresSeedSql` (lines ~218-227) and `SqliteSeedSql` (lines ~250-259). The manabase-family rows END at `('analysis.manabase.commander-castability', ...)`; the final row before `ON CONFLICT` is the DIFFERENT-family `('analysis.command-zone-awareness', ...)`. Insert the new manabase-family row AFTER commander-castability (NOT after command-zone-awareness), so command-zone-awareness stays last.

**PostgresSeedSql insertion** (after `('analysis.manabase.commander-castability', FALSE)` at ~line 225):
```sql
-- Current (~lines 225-227): manabase family ends at commander-castability; command-zone-awareness is last:
  ('analysis.manabase.commander-castability', FALSE),
  ('analysis.command-zone-awareness', FALSE)
ON CONFLICT (key) DO NOTHING;

-- Modified (insert tap-analyzer into the manabase family group, before command-zone-awareness):
  ('analysis.manabase.commander-castability', FALSE),
  ('analysis.manabase.tap-analyzer', FALSE),
  ('analysis.command-zone-awareness', FALSE)
ON CONFLICT (key) DO NOTHING;
```

**SqliteSeedSql insertion** (same position, `0` not `FALSE`, after `('analysis.manabase.commander-castability', 0)` at ~line 257):
```sql
-- Current (~lines 257-259):
  ('analysis.manabase.commander-castability', 0),
  ('analysis.command-zone-awareness', 0)
ON CONFLICT (key) DO NOTHING;

-- Modified:
  ('analysis.manabase.commander-castability', 0),
  ('analysis.manabase.tap-analyzer', 0),
  ('analysis.command-zone-awareness', 0)
ON CONFLICT (key) DO NOTHING;
```

**RenamedFlagKeys:** the new `analysis.manabase.tap-analyzer` is brand-new (no old key) → NO entry in the `RenamedFlagKeys` old→new map (~:21-37); `command-zone-awareness` is likewise absent from it.

**Key safety:** `ON CONFLICT (key) DO NOTHING` preserves existing operator-set values on re-bootstrap. New entry seeds `FALSE`/`0` (OFF by default per locked decision in STATE.md).

---

### `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` (service, request-response)

**Analog:** itself — the `PlainLanguageVerdictFlagKey` constant + `plainLanguage` flag read + result propagation (lines 170-291)

**Flag key constant pattern** (lines 170-180):
```csharp
// Existing constants (lines 170-180):
public const string PlainLanguageVerdictFlagKey = "analysis.manabase.plain-language-verdict";
public const string CommanderCastabilityFlagKey = "analysis.manabase.commander-castability";

// New constant (same style, same namespace):
public const string TapAnalyzerFlagKey = "analysis.manabase.tap-analyzer";
```

**Flag read pattern** (lines 213-216 show the pattern — read before use, fail-safe OFF):
```csharp
// Existing pattern (line 216):
bool commanderCastability = IsFlagOn(CommanderCastabilityFlagKey);

// New (read alongside existing flags):
bool showTapAnalyzer = IsFlagOn(TapAnalyzerFlagKey);
```

**`IsFlagOn` implementation** (lines 318-323 — DO NOT CHANGE this method):
```csharp
private bool IsFlagOn(string key)
    => _featureFlags is { } flags
        && flags.Snapshot().TryGetValue(key, out bool enabled)
        && enabled;
```

**`ManabaseAnalysisResult` construction pattern** (lines 285-291):
```csharp
// Current return (lines 285-291):
return new ManabaseAnalysisResult(
    report, resolved.InputSummary, resolved.Unresolved, resolved.FallbackNotice,
    swapPrompt, resolved.Deck.CostSuggestions, verdict, budget, plainLanguage)
{
    CommanderCastabilityEnabled = commanderCastability,
    CompanionRow = companionRow,
};

// Extended (add ShowTapAnalyzer as additive init property — mirrors CommanderCastabilityEnabled):
return new ManabaseAnalysisResult(
    report, resolved.InputSummary, resolved.Unresolved, resolved.FallbackNotice,
    swapPrompt, resolved.Deck.CostSuggestions, verdict, budget, plainLanguage)
{
    CommanderCastabilityEnabled = commanderCastability,
    CompanionRow = companionRow,
    ShowTapAnalyzer = showTapAnalyzer,  // NEW
};
```

**`ManabaseAnalysisResult` record** (lines 79-95) — add new additive property:
```csharp
// Existing additive properties on the result record (lines 90-94):
public bool CommanderCastabilityEnabled { get; init; }
public CardCastability? CompanionRow { get; init; }

// New additive property (same style):
public bool ShowTapAnalyzer { get; init; }
```

---

### `DeckFlow.Web/Models/ManabaseViewModel.cs` (model, request-response)

**Analog:** itself — `ShowPlainLanguage bool { get; init; }` at line 45 and `ShowCommanderCastability bool { get; init; }` at line 48

**Exact pattern** (lines 44-48):
```csharp
// Existing (lines 44-48):
/// <summary>Whether the UI should surface the plain-language glossary/verdict affordances.</summary>
public bool ShowPlainLanguage { get; init; }

/// <summary>Whether the UI should surface the command-zone castability affordances.</summary>
public bool ShowCommanderCastability { get; init; }

// New field (same style, append after ShowCommanderCastability):
/// <summary>Whether the UI should surface the tap-analyzer card and its paste-artifact section.</summary>
public bool ShowTapAnalyzer { get; init; }
```

**Controller wire-up pattern** (ManabaseController.cs lines 88-103 — ViewModel construction):
```csharp
// Existing (line 100):
ShowCommanderCastability = result.CommanderCastabilityEnabled,

// New (append after):
ShowTapAnalyzer = result.ShowTapAnalyzer,
```

---

### `DeckFlow.Web/Models/ManabaseDisplay.cs` (utility, transform)

**Analog:** itself — `CastChip(int castPercent)` at lines 63-76

**Exact helper pattern to mirror** (lines 63-76):
```csharp
// Existing CastChip (lines 63-76):
public static (string Css, string Label) CastChip(int castPercent)
{
    if (castPercent < 70)
    {
        return ("manabase-chip--low", "low");
    }

    if (castPercent < 90)
    {
        return ("manabase-chip--ok", "ok");
    }

    return ("manabase-chip--good", "good");
}

// New TapMarker (mirrors shape; single threshold per UI-SPEC Section 4):
/// <summary>
/// Maps an untapped-source percentage to a (cssClass, glyph) pair for the tap-analyzer card.
/// ≥80% = met (✓); below = short (⚠). Informational only — never contradicts the health verdict.
/// </summary>
public static (string Css, string Marker) TapMarker(int untappedPercent)
    => untappedPercent >= 80
        ? ("manabase-lens-met", "✓")
        : ("manabase-lens-short", "⚠");
```

**Existing gloss constant pattern** (there are `KarstenSourceGloss`, `CastRateGloss`, etc. on the class — read the top of ManabaseDisplay.cs to confirm naming):
```csharp
// New gloss constant (same style as KarstenSourceGloss, CastRateGloss):
/// <summary>Plain-language gloss for the tap-analyzer card (shown when ShowPlainLanguage is also on).</summary>
public const string TapAnalyzerGloss =
    "Tapped lands (Temples, tri-lands, taplands) can't tap for mana the turn they enter, " +
    "so they push back your first castable turn. Higher untapped % = faster, smoother starts.";
```

---

### `DeckFlow.Web/Controllers/ManabaseController.cs` (controller, request-response)

**Analog:** itself

**ViewModel construction pattern** (lines 88-103 — already covered in ViewModel section above).

**Download action `Build` call** (lines 127-128):
```csharp
// Current (lines 127-128):
string text = ManabaseReportTextBuilder.Build(
    result.Report, request.DeckName, decklistText: null, request.Mode, result.Verdict, result.Budget);

// Extended (pass tap only when flag is ON — null when OFF preserves byte-identity):
string text = ManabaseReportTextBuilder.Build(
    result.Report, request.DeckName, decklistText: null, request.Mode, result.Verdict, result.Budget,
    tap: result.ShowTapAnalyzer ? result.Report.TapAnalysis : null);
```

**`RunAnalysisAsync` — no changes** (lines 151-162): the helper already passes through all flags via `ManabaseAnalysisOptions`; the new flag is read inside `AnalyzeAsync`, so no change to `RunAnalysisAsync` is needed.

---

### `DeckFlow.Web/Views/Deck/Manabase.cshtml` (view, request-response)

**Analog:** itself — `@if (showPlainLanguage)` gloss blocks (lines 205-209) + `.manabase-twolens` chrome (lines 178-226)

**Byte-identical guard pattern** (lines 205-209 — nothing emitted when false):
```razor
@* Existing — no bytes emitted when condition is false (line 205-209): *@
@if (showPlainLanguage)
{
    <p class="manabase-lens-gloss">@ManabaseDisplay.KarstenSourceGloss</p>
}
```

**New tap card insertion point:** After the `</div>` closing `.manabase-twolens` (line 226), before the `<p class="manabase-context">` (line 228):
```razor
@* TAP CARD — inserted between line 226 and line 228.
   Entire block inside @if so NO bytes emitted when flag OFF (byte-identity per TAP-04 / Pitfall 3). *@
@if (Model.ShowTapAnalyzer && Model.HasResult && report?.TapAnalysis is { } tap)
{
    <div class="manabase-lens manabase-taplens" role="group" aria-label="Untapped sources">
        <p class="manabase-lens-label">Untapped sources</p>
        <div class="manabase-taplens-split">
            <div>
                <div class="manabase-lens-big">@tap.Turn1UntappedPercent%<span>turn-1 untapped</span></div>
                <span class="manabase-lens-pill">share of games with an untapped source to spend on turn 1</span>
            </div>
            <div>
                <div class="manabase-lens-row">
                    <span class="manabase-lens-color">Overall</span>
                    <span><strong>@tap.OverallUntappedPercent% untapped</strong></span>
                </div>
                @if (report.ColorFindings.Count > 1)
                {
                    @foreach (var (color, tf) in tap.ColorTap)
                    {
                        var tm = ManabaseDisplay.TapMarker(tf.UntappedPercent);
                        <div class="manabase-lens-row">
                            <span class="manabase-lens-color">@color</span>
                            <span>
                                <strong>@tf.UntappedPercent% untapped</strong>
                                <span class="manabase-lens-muted">(@tf.UntappedSources.ToString("F1") / @tf.TotalSources.ToString("F1"))</span>
                                <span class="@tm.Css" aria-hidden="true">@tm.Marker</span>
                                <span class="sr-only">@(tf.UntappedPercent >= 80 ? "meets target" : "below target")</span>
                            </span>
                        </div>
                    }
                }
            </div>
        </div>
        <p class="manabase-lens-note">How often a colored source can be spent the turn it's available — tapped lands (Temples, tri-lands, taplands) can't make mana the turn they enter, so they push back your earliest castable turn. Drawn from the same simulation as the cast rate above, so it never contradicts it.</p>
        @if (showPlainLanguage)
        {
            <p class="manabase-lens-gloss">@ManabaseDisplay.TapAnalyzerGloss</p>
        }
    </div>
}
```

**Existing variable bindings** already in scope from the Razor block above line 178: `report`, `showPlainLanguage`, `castRows`. The `showPlainLanguage` local is already bound at the top of the result section.

---

### `DeckFlow.Web/wwwroot/css/site-common.css` (config, N/A)

**Analog:** itself — `.manabase-twolens` + its `@media (max-width: 640px)` collapse (lines 2769-2773)

**Exact CSS to add** (insert after the existing `.manabase-twolens` responsive rule, which is at line 2769):
```css
/* .manabase-taplens — full-width tap-analyzer card; ties to .manabase-twolens above it.
   No color tokens — chrome from composed .manabase-lens; layout-only per theme constraint. */
.manabase-taplens {
  margin: -0.25rem 0 1rem;
}

/* .manabase-taplens-split — headline column | per-color column internal grid. */
.manabase-taplens-split {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr);
  gap: 1rem 1.5rem;
  align-items: start;
}

@media (max-width: 640px) {
  /* Same breakpoint as .manabase-twolens collapse at line 2769. */
  .manabase-taplens-split {
    grid-template-columns: 1fr;
  }
}
```

**No per-theme fork changes:** All visible colors come from existing `.manabase-lens` token set (`--panel-soft-bg`, `--line`, `--success`, `--gold-warning`, `--accent-strong`, `--muted`, `--info`) already defined in all 22 guild themes. Verify rendered result on `site.css` (Jeskai), `site-azorius.css`, `site-nyx.css`.

---

## Test File Patterns

### `DeckFlow.Core.Tests/Manabase/ManabaseTapAnalysisTests.cs` (NEW — unit, batch)

**Analog:** `DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerTests.cs` (Core manabase unit test structure)

**File header pattern** (ManabaseAnalyzerTests.cs lines 1-15):
```csharp
using System.Collections.Generic;
using System.Linq;
using DeckFlow.Core.Manabase;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates ManabaseTapAnalysis computation: untapped-source composition (TAP-01) and
/// turn-1 untapped availability (TAP-02) via ManabaseAnalyzer.Analyze on synthetic decks.
/// </summary>
public sealed class ManabaseTapAnalysisTests
{
```

**Test cases to include:**
- `Analyze_AllUntappedDeck_OverallUntappedPercent_Is100` — deck where every `ManaSource.EntersUntapped = true`
- `Analyze_AllTappedDeck_OverallUntappedPercent_Is0` — deck where every `ManaSource.EntersUntapped = false`
- `Analyze_MixedDeck_OverallUntappedPercent_MatchesWeightedFraction` — 75% untapped sources → 75%
- `Analyze_AllUntappedLands_Turn1UntappedPercent_IsNearCertain` — ≥ 95%
- `Analyze_AllTappedLands_NoFastMana_Turn1UntappedPercent_IsZero` — 0%
- `Analyze_SingleColorDeck_ColorTap_HasOneEntry`
- `Analyze_MultiColorDeck_ColorTap_HasOneEntryPerColor`

---

### `DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs` (MODIFY — unit, transform)

**Analog:** itself — existing `HealthyCasualReport()` fixture + `Build(...)` call structure

**Existing fixture construction style** (lines 47-70):
```csharp
private static ManabaseReport HealthyCasualReport() => new()
{
    ActualLands = 37,
    TargetLands = 37.0,
    ColorFindings = new List<ColorSourceFinding> { ... },
    Mode = ManabaseMode.Casual,
    Summary = "Mana base is well-built.",
};
```

**New test additions** (append to existing class):
```csharp
// Byte-identity guard (TAP-04 / TAP-03):
[Fact]
public void Build_NullTap_OutputByteIdenticalToOverloadWithoutTapParam()
{
    ManabaseReport report = HealthyCasualReport();
    string withoutTap = ManabaseReportTextBuilder.Build(report, "Test", null);
    string withNullTap = ManabaseReportTextBuilder.Build(report, "Test", null, tap: null);
    Assert.Equal(withoutTap, withNullTap);
}

// Content guard (TAP-04):
[Fact]
public void Build_WithTapAnalysis_ContainsUntappedSourcesSection()
{
    // tap fixture constructed from ManabaseTapAnalysis record
    string text = ManabaseReportTextBuilder.Build(
        HealthyCasualReport(), "Test", null,
        tap: new ManabaseTapAnalysis { OverallUntappedPercent = 82, Turn1UntappedPercent = 76, ... });
    Assert.Contains("Untapped Sources:", text);
    Assert.Contains("Turn-1 untapped availability:", text);
}

// Single-color omits per-color table (Pitfall 5):
[Fact]
public void Build_SingleColorDeckWithTap_OmitsPerColorTable()
{
    // report with ColorFindings.Count == 1, tap.ColorTap has 1 entry
    string text = ManabaseReportTextBuilder.Build(..., tap: singleColorTap);
    Assert.DoesNotContain("Color", text.Split("Untapped Sources:")[1].Split("Biggest fix:")[0]);
}
```

---

### `DeckFlow.Web.Tests/Manabase/ManabaseDisplayTests.cs` (MODIFY — unit, transform)

**Analog:** itself — `CastChip_LabelsSeverityByBand` Theory pattern (lines 29-41)

**New test additions** (append to existing class, mirror CastChip tests exactly):
```csharp
[Theory]
[InlineData(80, "manabase-lens-met", "✓")]
[InlineData(100, "manabase-lens-met", "✓")]
[InlineData(79, "manabase-lens-short", "⚠")]
[InlineData(0, "manabase-lens-short", "⚠")]
public void TapMarker_MapsPercentToCorrectCssAndGlyph(int percent, string expectedCss, string expectedMarker)
{
    var (css, marker) = ManabaseDisplay.TapMarker(percent);
    Assert.Equal(expectedCss, css);
    Assert.Equal(expectedMarker, marker);
}
```

---

### `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` (MODIFY — unit, request-response)

**Analog:** itself — the `[InlineData("analysis.manabase.commander-castability")]` at line 41

**Single addition** (after line 42, before the `public void` method):
```csharp
[InlineData("analysis.manabase.tap-analyzer")]
```

---

### `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` (MODIFY — unit, CRUD)

**Analog:** itself — `[InlineData("analysis.manabase.commander-castability", false)]` at line 38

**Single addition** (after line 40, before `public async Task`):
```csharp
[InlineData("analysis.manabase.tap-analyzer", false)]
```

---

### `DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs` (MODIFY — unit, request-response)

**Analog:** itself — `Download_IncludesVerdictAndBudgetText` (lines 52-92) + `StubService` with `showPlainLanguage`

**`StubService` extension pattern** (lines 172-199 — add `showTapAnalyzer` parameter):
```csharp
// Current StubService ctor (line 180-190):
public StubService(
    ManabaseReport report,
    ManabaseVerdict? verdict = null,
    ManabaseRampDrawBudget? budget = null,
    bool showPlainLanguage = false)

// Extended (add showTapAnalyzer):
public StubService(
    ManabaseReport report,
    ManabaseVerdict? verdict = null,
    ManabaseRampDrawBudget? budget = null,
    bool showPlainLanguage = false,
    bool showTapAnalyzer = false)
```

**New test additions:**
```csharp
[Fact]
public async Task Download_FlagOff_ArtifactDoesNotContainUntappedSourcesSection()
{
    var service = new StubService(ReportWithTapAnalysis(), showTapAnalyzer: false);
    var controller = BuildController(service);
    var result = await controller.Download(new ManabaseRequest { ... });
    var file = Assert.IsType<FileContentResult>(result);
    string text = Encoding.UTF8.GetString(file.FileContents);
    Assert.DoesNotContain("Untapped Sources:", text);
}

[Fact]
public async Task Download_FlagOn_ArtifactContainsUntappedSourcesAndTurn1Sections()
{
    var service = new StubService(ReportWithTapAnalysis(), showTapAnalyzer: true);
    var controller = BuildController(service);
    var result = await controller.Download(new ManabaseRequest { ... });
    var file = Assert.IsType<FileContentResult>(result);
    string text = Encoding.UTF8.GetString(file.FileContents);
    Assert.Contains("Untapped Sources:", text);
    Assert.Contains("Turn-1 untapped availability:", text);
}
```

---

## Shared Patterns

### `{ get; init; }` enforcement (carve-out)
**Source:** CLAUDE.md + `CarveOutGuardTests.cs` (existing test in `DeckFlow.Core.Tests`)
**Apply to:** ALL new record fields in `ManabaseModels.cs`, `ManabaseAnalysisResult`
```csharp
// CORRECT — matches AverageDelay pattern (ManabaseModels.cs:168):
public int Turn1UntappedTrials { get; init; }
public double UntappedSources { get; init; }
public ManabaseTapAnalysis? TapAnalysis { get; init; }

// WRONG — get-only is silently skipped by System.Text.Json in .NET 9+:
public int Turn1UntappedTrials { get; }  // NEVER DO THIS
```

### Flag read + fail-safe OFF
**Source:** `ManabaseAnalysisService.cs` lines 318-323
**Apply to:** `TapAnalyzerFlagKey` read in `AnalyzeAsync`
```csharp
// The IsFlagOn helper is the ONLY correct way to read flags (missing key → false):
private bool IsFlagOn(string key)
    => _featureFlags is { } flags
        && flags.Snapshot().TryGetValue(key, out bool enabled)
        && enabled;
// Do NOT use _featureFlags?.IsEnabled(key) — that method defaults missing keys ON.
```

### Byte-identity when flag OFF
**Source:** UI-SPEC.md Section 4 + RESEARCH.md Pitfall 3
**Apply to:** `Manabase.cshtml` view block, `ManabaseReportTextBuilder.Build` tap parameter
- View: the ENTIRE card markup is inside `@if (Model.ShowTapAnalyzer && ...)` — no trailing whitespace, no `@* *@` Razor comment, no empty line outside the block
- Text builder: `tap = null` produces exactly zero appended bytes — guard is `if (tap is not null)`

### Conditional tap parameter gate
**Source:** RESEARCH.md Q4 / Pitfall 4
**Apply to:** `ManabaseController.Download` Build call, `ManabaseAnalysisService.AnalyzeAsync`
```csharp
// Gate must use the bool, not null-check the report field directly:
tap: result.ShowTapAnalyzer ? result.Report.TapAnalysis : null
// NOT: tap: result.Report.TapAnalysis  (that would send tap data even when flag is OFF)
```

### Per-color table mono-color guard
**Source:** RESEARCH.md Pitfall 5 + UI-SPEC.md Section 4
**Apply to:** `Manabase.cshtml` + `ManabaseReportTextBuilder.AppendTapAnalysisBlock`
```razor
@* View: *@
@if (report.ColorFindings.Count > 1)
{
    @* per-color .manabase-lens-row list *@
}
```

### Commander-row exclusion when averaging T1
**Source:** RESEARCH.md Pitfall 6
**Apply to:** `ComputeTapAnalysis` in `ManabaseAnalyzer`
```csharp
var nonCmdRows = castability.Where(r => !r.IsCommander).ToList();
IReadOnlyList<CardCastability> avgRows = nonCmdRows.Count > 0 ? nonCmdRows : castability;
```

---

## No Analog Found

None — every file in scope is a modification to an existing file, and the only new file (`ManabaseTapAnalysisTests.cs`) has a strong role-match analog in `ManabaseAnalyzerTests.cs`.

---

## Metadata

**Analog search scope:** `/mnt/c/users/chrislunt/source/personal/deckflow-cycle13/DeckFlow.Core/Manabase/`, `DeckFlow.Web/Services/`, `DeckFlow.Web/Models/`, `DeckFlow.Web/Controllers/`, `DeckFlow.Web/Views/Deck/`, `DeckFlow.Web/wwwroot/css/`, `DeckFlow.Core.Tests/Manabase/`, `DeckFlow.Web.Tests/`
**Files scanned:** 18 source files read in detail
**Pattern extraction date:** 2026-06-28
