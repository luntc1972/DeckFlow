# Phase: Manabase Research-Gap Closure - Pattern Map

**Mapped:** 2026-07-12
**Files analyzed:** 12 production files + 6 test files (certain/likely touches from CONTEXT.md + RESEARCH.md)
**Analogs found:** 12 / 12 (all patterns exist in-repo; this phase extends existing machinery, no new architecture)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Core/Manabase/ManabaseClassifier.cs` (restricted-land regex + census, MBGAP-01) | service (classifier) | transform | Check-land/Snarl census (`Cls:487-507, 994-1072`) — same file | exact (same file, sibling pattern) |
| `DeckFlow.Core/Manabase/ManabaseClassifier.cs` (fast/slow/ELD/Verge/Vivid/Training-Compound detection, MBGAP-02) | service (classifier) | transform | Check-land census (static) + granted-source conditional weight (`Cls:1439-1542`) | exact for Verge/Vivid; role-match for fast/slow/ELD (new dynamic path) |
| `DeckFlow.Core/Manabase/CastabilitySimulator.cs` (new `CardKind.ConditionalCountLand` + `PlayOneLand` resolution, MBGAP-02) | service (Monte-Carlo sim) | event-driven (per-trial) | `PlayOneLand` (`CS:1197-1300`), `CardKind` enum (`CS:44-51`) — same file | role-match (genuinely new dynamic primitive, closest existing shape is the static UntappedLand/TappedLand split it extends) |
| `DeckFlow.Core/Manabase/KarstenManabase.cs` (ritual land-target credit term, MBGAP-03) | service (pure math) | transform | `CedhLandTarget`/`CedhBaselineBlendWeight` (`Kar:36-125`) — same file | exact |
| `DeckFlow.Core/Manabase/ManabaseVerdictSynthesizer.cs` (wording fixes, MBGAP-05a-d) | service (text synthesis) | transform | `CollectIssues`/`BuildColorIssue` (`VS:47-136`) — same file | exact |
| `DeckFlow.Core/Manabase/ManabaseModels.cs` (new bool/record fields for MBGAP-01 disclosure) | model | — | `IsCostOverridden` on `CardCastability`/spell requirement (`Models:159,196`), `UnsupportedInteraction` record (`Models:454-461`) | exact |
| `DeckFlow.Web/Views/Deck/Manabase.cshtml` (disclosure marker + plural fixes) | view (Razor) | request-response | Alt-cost `*` marker (`Manabase.cshtml:697-698,710-712`), unsupported-interactions `<details>` (`:655-666`) | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` (2 new flag descriptions) | config | — | `ritual-burst-mana`/`cedh-land-target` entries (`Catalog.cs:96-103`) | exact |
| `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` (seed OFF, PG+SQLite) | config | — | `('analysis.manabase.ritual-burst-mana', FALSE)` / `(..., 0)` pairs (`Store.cs:230-231,270-271`) | exact |
| `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` (flag read + thread to `Analyze`) | service (orchestrator) | request-response | `RitualBurstFlagKey`/`CedhLandTargetFlagKey` constants + `IsFlagOn` + `Analyze(ritualBurst: ...)` (`Service.cs:224,282,361-364`) | exact |
| `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` (trailing optional param(s) for new flags) | service (pure math orchestrator) | transform | `ritualBurst`/`useHealthBandCastability`/`cedhContext` trailing optional params (`Analyzer.cs:26-51,120-127`) | exact |
| `DeckFlow.CLI/CedhCalibrateCommandRunner.cs` (extend for ritual-credit 3rd target column, MBGAP-03) | CLI command | batch | Existing `RunAsync` deck-replay loop (`Runner.cs:27-150`) — same file | exact |
| `docs/manabase-analysis-rules.md` (doc updates, every MBGAP item) | config/doc | — | Existing flag table + rule sections (read in full during research) | exact |
| `DeckFlow.Web/Help/manabase.md` (MBGAP-11 re-audit) | config/doc | — | Same file, line-by-line audit | exact (in-place rewrite) |
| `DeckFlow.Core.Tests/Manabase/ManabaseClassifierTests.cs` (extend) | test | — | Existing check-land/Snarl/bond test cases in same file (1568 lines) | exact |
| `DeckFlow.Core.Tests/Manabase/ManabaseLiveOracleCanaryTests.cs` (extend, new regex canaries) | test | — | Existing canary assertions, same file | exact |
| `DeckFlow.Core.Tests/Manabase/CedhLandTargetHybridTests.cs` (extend for ritual credit) | test | — | Existing hybrid-target constant tests, same file (77 lines) | exact |
| `DeckFlow.Core.Tests/Manabase/ManabaseVerdictSynthesizerTests.cs` (extend for 05a-d) | test | — | Existing synthesizer tests, same file | exact |
| `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` (2 new `[InlineData]`) | test | — | `Describe_EverySeededFlag_HasNonEmptyDescription` theory (lines 15-53) | exact |
| New file: `DeckFlow.Core.Tests/Manabase/ConditionalCountLandTests.cs` (or similar, per-trial CardKind) | test | — | No exact analog — closest is `KarstenManabaseCastabilityTests.cs` sim-facing test shape | role-match (new test file needed, RESEARCH.md Wave-0 gap) |
| Playwright: `DeckFlow.Web/e2e/manabase-ramp-disclosure.spec.ts` (extend/sibling for MBGAP-01 marker) | test (e2e) | request-response | Same spec's existing disclosure-marker assertions | role-match |

## Pattern Assignments

### `DeckFlow.Core/Manabase/ManabaseClassifier.cs` — MBGAP-01 restricted-land composition gates

**Analog:** Check-land/Snarl census, same file (`Cls:479-507`, `994-1072`)

**Regex-family pattern to copy** (`ManabaseClassifier.cs:487-507`):
```csharp
private static readonly Regex CheckLandRegex = new(
    @"tapped unless you control (?:a|an) ([^.]+)",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

private static readonly Regex SnarlRevealRegex = new(
    @"reveal ([^.]+?) card from your hand",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);

// Oracle templates whose first capture group is the named-basic-type clause a conditional land
// keys off. Tried in order, first match wins. Future census families (Verge, Vivid, Training
// Compound — see docs/manabase-analysis-rules.md §4b backlog) add one entry here, not another
// branch in ConditionalUntappedTypes. Must be declared after the regexes it references.
private static readonly Regex[] ConditionalTypeTemplates = { CheckLandRegex, SnarlRevealRegex };

// A conditional-untapped land (check/Snarl) is modeled untapped when the deck runs at least this
// many lands bearing one of its named basic types...
private const int CheckLandMatchTypeThreshold = 6;
```
For the new `SpendOnlyCreatureRegex` (Cavern/Unclaimed/Ziggurat) and the Nykthos devotion-clause
detector, add sibling `private static readonly Regex` fields immediately below this block, with
the same `[ASSUMED]`-flagged verification comment style pointing at
`ManabaseLiveOracleCanaryTests.cs` (see Shared Patterns below).

**Static-census computation pattern to copy** (`ManabaseClassifier.cs:1031-1072`):
```csharp
private static int CountLandsBearingAnyType(IReadOnlyList<CardFact> cards, IReadOnlyList<string> types, CardFact candidate)
{
    int count = 0;
    foreach (CardFact card in cards)
    {
        if (ReferenceEquals(card, candidate) || !IsLandType(card.TypeLine)) { continue; }
        string front = CardTypeLine.FrontFace(card.TypeLine);
        foreach (string type in types)
        {
            if (front.Contains(type, StringComparison.OrdinalIgnoreCase)) { count += card.Quantity; break; }
        }
    }
    return count;
}

private static bool IsConditionallyUntapped(CardFact card, IReadOnlyList<CardFact> cards)
{
    if (IsBondLand(card)) { return true; }
    IReadOnlyList<string> types = ConditionalUntappedTypes(card);
    return types.Count > 0 && CountLandsBearingAnyType(cards, types, card) >= CheckLandMatchTypeThreshold;
}
```
**New logic needed (genuinely new, no existing helper):** a subtype/tribal-share census —
`TypeLine.Split('—')` to extract creature subtypes, then a per-deck histogram and
`max(histogram.Values) / totalCreatureCount` "dominant type share" (see RESEARCH.md D-03 section
for the exact formula gap — this is new code, not a reuse).

**Weight-scaling-by-deck-fraction pattern to copy** (fetch-land weight, `Cls:348-364`):
```csharp
bool basicFetch = IsBasicFetch(card);
// A choice-fetch in a 3+ color deck can only grab one color at a time.
double weight = basicFetch && deckColorCount >= 3 ? 0.67 : 1.0;
```
This is the template for Cavern/Unclaimed/Ziggurat's "weight computed from deck composition, not
a flag" — NOT the fixed-0.25 conditional-source pattern below (that is for Nykthos only, per D-03).

**Conditional Bernoulli-gated weight pattern to copy** (`ManabaseClassifier.cs:1477-1542`, for
Nykthos low-weight modeling):
```csharp
sources.Add(new ManaSource
{
    Name = card.Name + " (granted)",
    Produces = deckColors,
    Weight = 0.25,
    IsLand = false,
    ManaAmount = 1,
    IsConditional = true, // gates a per-trial Bernoulli roll in the sim, CastabilitySimulator.cs §4.9
});
```

---

### `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — MBGAP-02 per-trial fast/slow/ELD resolution

**Analog:** `CardKind` enum + `PlayOneLand`, same file (`CS:44-51`, `1197-1300`)

**Current static CardKind assignment (the boundary this phase must cross for fast/slow/ELD)**
(`CastabilitySimulator.cs:884-890`):
```csharp
int amount = useManaQuantity && !source.IsConditional ? Math.Max(1, source.ManaAmount) : 1;
...
CardKind kind = source.EntersUntapped ? CardKind.UntappedLand : CardKind.TappedLand;
...
AddWeighted(cards, kind, mask, deployCost: 0, source.Weight, source.IsConditional, amount, rampPips: null);
```
This bakes tapped/untapped into a single fixed `CardKind` at classification time — the existing
"two options" (`UntappedLand`/`TappedLand`) that D-06/D-07 explicitly say cannot represent
fast/slow/ELD (whose tapped state depends on the trial's land-play sequence).

**Insertion/extension point — `PlayOneLand`** (`CastabilitySimulator.cs:1197-1300`, read in full):
```csharp
scratchOnlineMasks.Clear();
foreach ((int Mask, int OnlineTurn, int Amount) land in landsOnBoard)
{
    if (land.OnlineTurn <= currentTurn) { scratchOnlineMasks.Add(land.Mask); }
}
...
// [new] for a ConditionalCountLand candidate, compare landsOnBoard.Count (or a type-filtered
// subset for ELD) against its threshold HERE, at the pick/onlineTurn-decision point below:
LibraryCard played = library[hand[pick]];
int onlineTurn = played.Kind == CardKind.TappedLand ? currentTurn + 1 : currentTurn;
landsOnBoard.Add((played.ColorMask, onlineTurn, played.ManaAmount));
hand.RemoveAt(pick);
```
`landsOnBoard` is `List<(int Mask, int OnlineTurn, int Amount)>` — a **color mask**, not a
basic-type tag. Per RESEARCH.md Pitfall 2, ELD ("three or more other Islands") cannot be resolved
from this tuple alone without an added type-tag field — planner must explicitly choose extend-the-
tuple vs. fall back to static census for ELD.

**New `CardKind` case needed** (extend the enum at `CS:44-51`):
```csharp
private enum CardKind
{
    UntappedLand,
    TappedLand,
    Ramp,
    OneShotMana,
    Filler,
    // NEW: ConditionalCountLand — resolved per-trial in PlayOneLand using landsOnBoard.Count
}
```
No existing per-trial dynamic-resolution template exists for this exact shape (RESEARCH.md's
"Don't Hand-Roll" table calls this the one genuinely-new primitive in the phase) — extend
`landsOnBoard` tracking already computed in `PlayOneLand`, do not build a parallel counter.

---

### `DeckFlow.Core/Manabase/KarstenManabase.cs` — MBGAP-03 ritual land-target credit

**Analog:** `CedhLandTarget` + named calibration constants, same file (`Kar:36-125`)

```csharp
// Source: DeckFlow.Core/Manabase/KarstenManabase.cs:36-42
public const double CedhSafetyFloor = 22.0;
public const double CedhDisabledFloor = 28.0;
private const double CedhTargetCeiling = 45.0;
private const double CedhBaselineBlendWeight = 0.5;

// Source: KarstenManabase.cs:93-125 (CedhLandTarget) — insertion point for a new ritual-credit term
public static double CedhLandTarget(
    int totalCards, int commanderCount, double averageManaValue,
    double rampAndDrawUnderThree, double fastMana, CedhLandContext context)
{
    double singleton = SingletonLandTarget(totalCards, commanderCount, averageManaValue, rampAndDrawUnderThree, fastMana);
    if (!context.Enabled) { return Math.Max(CedhDisabledFloor, singleton - 3.5); }
    double curveTarget = singleton - 3.5;
    double mean = context.BaselineMean.GetValueOrDefault();
    bool useBaseline = context.BaselineN >= 10 && context.BaselineMean.HasValue && double.IsFinite(mean) && mean is >= 10.0 and <= 60.0;
    double target = useBaseline ? curveTarget - (CedhBaselineBlendWeight * (curveTarget - mean)) : curveTarget;
    return Math.Clamp(target, CedhSafetyFloor, CedhTargetCeiling);
}
```
**Template constant naming to copy:** `RitualLandCreditWeight = 0.5` (named, calibration-tunable,
capped) mirrors `CedhBaselineBlendWeight`/`CedhSafetyFloor` — a private/public const, not a magic
number inline. The credit term should read `deck.OneShots` (already populated by
`DetectOneShotBurstMana`, `Cls:550-599` — do not re-classify, just consume the existing
`ManabaseDeck.OneShots` list, `Models.cs:427`).

---

### `DeckFlow.Core/Manabase/ManabaseVerdictSynthesizer.cs` — MBGAP-05a-d wording fixes

**Analog:** same file, `CollectIssues`/`BuildColorIssue` (`VS:47-136`, read in full)

**05a — both `Math.Ceiling` overstatement sites** (fix BOTH per Q2 Addendum resolution):
```csharp
// VS:59-64 (land delta)
if (report.LandDelta < -1 && !report.LandShortfallCoveredByRamp)
{
    issues.Add(string.Create(CultureInfo.InvariantCulture,
        $"Add ~{Math.Ceiling(-report.LandDelta):F0} more land(s) - the base is short for this curve."));
}
// VS:102-111 (BuildColorIssue, color-source-short) — SAME defect class, same fix
if (finding.Deficit > 1.0)
{
    int shortfall = (int)Math.Ceiling(finding.Deficit);
    return string.Create(CultureInfo.InvariantCulture,
        $"You're ~{shortfall} {finding.Color} source(s) short - ...");
}
```

**05b — truncation** (`VS:94-97`):
```csharp
if (issues.Count > 3)
{
    issues.RemoveRange(3, issues.Count - 3);
}
```
Fix: append an `"...plus N more"` line instead of silently dropping — propagate to
`ManabaseVerdict.Lines` consumers (page render, `.txt` builder — grep
`ManabaseReportTextBuilder`/`ManabaseSwapPromptBuilder` for the same truncation/consumption
pattern before finalizing scope, per RESEARCH.md's explicit instruction).

**05c — `(s)` plural literal sites** (all in `ManabaseVerdictSynthesizer.cs`): `"source(s)"` (line
110), `"land(s)"` (line 63), `"spell(s)"` (line 119), `"piece(s)"` (line 135, `BuildBudgetIssue`).
Grep `DeckFlow.Web/Views/Deck/Manabase.cshtml` for independent `(s)` literals — not verified in
research, flagged as a required pre-implementation grep.

**05d — heuristic-guidance labeling:** copy-only change to the three surfaces already producing
per-color deficit text: `BuildColorIssue` (`VS:102-120`, Core), the `.txt` builder
(`ManabaseReportTextBuilder`), and the swap prompt (`ManabaseSwapPromptBuilder.cs:70` per the
verified-fixed H3 note — same file already handles a related branch). No math/threshold change.

---

### `DeckFlow.Web/Views/Deck/Manabase.cshtml` — MBGAP-01 disclosure marker (D-05)

**Analog:** the alt-cost `*` marker, same file (`Manabase.cshtml:697-698, 710-712`), verbatim
reusable template:
```razor
@* Source: DeckFlow.Web/Views/Deck/Manabase.cshtml:697-698, 710-712 *@
@c.ManaValue@if (c.IsCostOverridden)
{<span class="manabase-override-mark" title="reduced / alternative cost applied" aria-label="reduced or alternative cost applied">*</span>}
...
@if (report.Castability.Any(c => c.IsCostOverridden))
{
    <p class="manabase-help"><span class="manabase-override-mark">*</span> reduced / alternative cost applied from your overrides.</p>
}
```
Copy this shape for a new bool (e.g. `IsRestrictedSourceUsed`) on `CardCastability`
(`ManabaseModels.cs:196` sibling field) — new marker span + gated footnote `<p>`.

**Unsupported-interactions panel** (`Manabase.cshtml:655-666`), also verbatim-reusable:
```razor
@* Source: DeckFlow.Web/Views/Deck/Manabase.cshtml:655-666 *@
@if (report.UnsupportedInteractions.Count > 0)
{
    <details class="manabase-unsupported">
        <summary>⚠ @report.UnsupportedInteractions.Count card(s) use interactions this analysis approximates or skips</summary>
        <ul>
            @foreach (var u in report.UnsupportedInteractions)
            {
                <li><strong>@u.Name</strong> — @u.Reason</li>
            }
        </ul>
    </details>
}
```
Model source (`ManabaseModels.cs:454-461`):
```csharp
public sealed record UnsupportedInteraction
{
    public required string Name { get; init; }
    public required string Reason { get; init; }
}
```
Add one new `UnsupportedInteraction` entry per restricted-land approximation via the same
construction pattern used elsewhere for `UnsupportedInteractions` population (X-cost/hybrid-pip
entries — grep the deck-classification path that populates this list today for the exact
call-site shape before adding the restricted-land entry).

---

### `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` + `FeatureFlagStore.cs` — D-04/D-10 new flags

**Analog:** `ritual-burst-mana`/`cedh-land-target` flag pair, both files

Catalog entry template (`FeatureFlagCatalog.cs:96-103`):
```csharp
["analysis.manabase.ritual-burst-mana"] =
    "Credit instant/sorcery rituals (Dark Ritual, Rite of Flame, Cabal Ritual) as one-shot " +
    "burst mana in the manabase castability sim, cEDH mode only. Raises early-turn cast % " +
    "for ritual-fueled lists; land count and color counts stay unchanged. Off = byte-identical output.",
["analysis.manabase.cedh-land-target"] =
    "Enable the hybrid cEDH land target: keep the Karsten curve anchor, but drop the flat 28 " +
    "floor and optionally nudge toward the commander's committed cEDH land baseline when sample " +
    "size is deep enough. cEDH only; off = byte-identical output.",
```
Seed entry template, BOTH PG and SQLite branches must be updated together
(`FeatureFlagStore.cs:230-231` PG, `:270-271` SQLite):
```sql
-- Postgres (FeatureFlagStore.cs:230-231)
('analysis.manabase.ritual-burst-mana', FALSE),
('analysis.manabase.cedh-land-target', FALSE),
-- SQLite (FeatureFlagStore.cs:270-271)
('analysis.manabase.ritual-burst-mana', 0),
('analysis.manabase.cedh-land-target', 0),
```
New keys for this phase: `analysis.manabase.restricted-lands` (D-04) and
`analysis.manabase.ritual-land-credit` (D-10) — both seeded FALSE/0 in both dialect branches, both
added to `FeatureFlagCatalog.Descriptions`, and both added to the `[InlineData(...)]` theory in
`FeatureFlagCatalogTests.cs` (see Shared Patterns).

---

### `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` + `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` — flag threading

**Analog:** `RitualBurstFlagKey`/`CedhLandTargetFlagKey` end-to-end wiring

Web-layer flag key + read (`ManabaseAnalysisService.cs:224,282,361-364`):
```csharp
public const string CedhLandTargetFlagKey = "analysis.manabase.cedh-land-target";
...
bool ritualBurst = IsFlagOn(RitualBurstFlagKey);
...
ManabaseReport report = ManabaseAnalyzer.Analyze(
    ...,
    ritualBurst: ritualBurst,
    ...);
```
Core-layer trailing optional param (`ManabaseAnalyzer.cs:26,44-51`):
```csharp
public static ManabaseReport Analyze(
    ManabaseDeck deck,
    ManabaseMode mode,
    CommanderImportance importance = CommanderImportance.Standard,
    IReadOnlyDictionary<string, string>? costOverrides = null,
    bool useManaQuantity = false,
    bool colorAwareMulligan = false,
    bool gateRampOnCastable = false,
    bool ritualBurst = false,
    bool useHealthBandCastability = false,
    bool useHealthBandHeadlineFloor = false,
    CedhLandContext cedhContext = default)
```
This trailing-optional-param-with-safe-default shape is the mandatory pattern for both new flags
(`restrictedLands = false`, `ritualLandCredit = false`) — guarantees every existing caller (tests,
CLI, other Web callers) stays byte-identical without touching call sites.

---

### `DeckFlow.CLI/CedhCalibrateCommandRunner.cs` — MBGAP-03 D-09 calibration extension

**Analog:** same file, `RunAsync` deck-replay loop (`Runner.cs:27-150`, read in full)

```csharp
// Source: DeckFlow.CLI/CedhCalibrateCommandRunner.cs:92-128 (summarized structure)
ManabaseDeck classifiedDeck = ManabaseClassifier.Classify(facts, isSingleton: true, rampCreditV2: true, landRampSim: true, payLifeUntapped: true, checkLandUntapped: true);
...
double oldTarget = Math.Max(28.0, KarstenManabase.SingletonLandTarget(...) - 3.5);
...
double newTarget = KarstenManabase.CedhLandTarget(classifiedDeck.TotalCards, classifiedDeck.CommanderCount, classifiedDeck.AverageManaValue, classifiedDeck.RampAndDrawUnderThree, classifiedDeck.FastMana, context);
rows.Add(new CedhCalibrationRow(deck.CmdKey, actualLands, oldTarget, newTarget, hasBaseline));
...
CedhCalibrationReport report = CedhCalibration.Build(rows);
WriteLfFile(resolvedOutputPath, CedhCalibration.RenderMarkdown(report));
```
Extend by computing a THIRD target (`newTargetWithRitualCredit`, reading `classifiedDeck.OneShots`
— already populated) and extending `CedhCalibrationRow`/`CedhCalibration.Build`/`RenderMarkdown`
to show the delta column — do not build a parallel calibration harness (RESEARCH.md "Don't
Hand-Roll" table).

## Shared Patterns

### Flag rollout sequence (mandatory for D-04 and D-10)
**Source:** `ritual-burst-mana`/`cedh-land-target` precedent across 4 files:
`FeatureFlagCatalog.cs:96-103`, `FeatureFlagStore.cs:230-231,270-271`,
`ManabaseAnalysisService.cs:224,282,361-364`, `ManabaseAnalyzer.cs:26-51`.
**Apply to:** both new flags (`analysis.manabase.restricted-lands`,
`analysis.manabase.ritual-land-credit`).
**Sequence:**
1. Catalog description entry (both flags).
2. Seed `FALSE`/`0` in BOTH Postgres and SQLite branches of `FeatureFlagStore.cs`.
3. `[InlineData(...)]` added to `FeatureFlagCatalogTests.cs` (line ~44-45 sibling) and to whatever
   seed-parity test currently covers `FeatureFlagStore` (grep for `FeatureFlagStoreSeedTests` at
   plan time — not directly read in this pass).
4. Trailing optional bool param on `ManabaseAnalyzer.Analyze`, default `false`.
5. `ManabaseAnalysisService` reads the flag via `IsFlagOn(<key>)` and threads it through.
6. Extend `CedhCalibrateCommandRunner.cs` (D-09 only) / golden-deck diff proving flag-off
   byte-identical.
7. Update `docs/manabase-analysis-rules.md` flag table + rule sections in the SAME change.

### Disclosure marker (D-05, verbatim reuse)
**Source:** `Manabase.cshtml:697-698,710-712` (`manabase-override-mark` span) +
`Manabase.cshtml:655-666` (`UnsupportedInteractions` `<details>` panel) +
`ManabaseModels.cs:159,196,454-461` (`IsCostOverridden` bool + `UnsupportedInteraction` record).
**Apply to:** MBGAP-01 restricted-source-used marker in the castability table, plus one new
`UnsupportedInteraction` entry.

### Static census classification (composition-gated weighting)
**Source:** `ManabaseClassifier.cs:994-1072` (`ConditionalUntappedTypes` / `CountLandsBearingAnyType`
/ `IsConditionallyUntapped`), `CheckLandMatchTypeThreshold = 6` (`Cls:507`).
**Apply to:** MBGAP-01 weight math (Cavern/Unclaimed/Ziggurat/Nykthos), MBGAP-02 Verge and the MSH
Training Compound cycle (both are "static census, gate a color not a tapped-state" per RESEARCH.md
Addendum Q1/Q3).

### Conditional Bernoulli-gated weight (fixed-weight speculative source)
**Source:** `ManabaseClassifier.cs:1477-1542` (`AddGrantedSources`), `ManaSource.IsConditional`
(`ManabaseModels.cs:56`), sim-side gate at `CastabilitySimulator.cs` §4.9 (grep `IsConditional`).
**Apply to:** Nykthos low-weight modeling (MBGAP-01); optionally Vivid's charge-counter budget
(MBGAP-02, planner discretion per D-07).

### Named calibration constants, not magic numbers
**Source:** `KarstenManabase.cs:36-42` (`CedhSafetyFloor`, `CedhDisabledFloor`, `CedhTargetCeiling`,
`CedhBaselineBlendWeight`).
**Apply to:** MBGAP-03's `RitualLandCreditWeight` (or similarly named) constant.

### Trailing-optional-parameter flag threading (byte-identical-when-off guarantee)
**Source:** `ManabaseAnalyzer.Analyze` overload chain (`ManabaseAnalyzer.cs:26,83,120-138`) —
every new flag is a trailing `bool xyz = false` parameter, never a required/reordered one.
**Apply to:** both new D-04/D-10 flags; any MBGAP-02 per-trial toggle threading (rides existing
`analysis.manabase.accuracy` bundle per D-08, no new flag needed there).

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `DeckFlow.Core/Manabase/CastabilitySimulator.cs` new `CardKind.ConditionalCountLand` case + its per-trial resolution logic | service (sim primitive) | event-driven | No existing per-trial dynamic `CardKind` resolution exists — closest is the static `UntappedLand`/`TappedLand` split it must extend beyond. RESEARCH.md explicitly flags this as the one genuinely-new primitive in the phase. |
| New unit test file (e.g. `ConditionalCountLandTests.cs`) for fast/slow/ELD per-trial resolution | test | — | No existing test file targets this new sim primitive (RESEARCH.md Wave-0 Gap #1). |
| New/extended Playwright spec for the MBGAP-01 restricted-land disclosure marker | test (e2e) | — | `manabase-ramp-disclosure.spec.ts` is the closest analog by subject (a disclosure marker) but does not yet cover restricted-source rows specifically (RESEARCH.md Wave-0 Gap #5). |

## Metadata

**Analog search scope:** `DeckFlow.Core/Manabase/*.cs`, `DeckFlow.Web/Services/FeatureFlags/*.cs`,
`DeckFlow.Web/Services/Manabase/*.cs`, `DeckFlow.Web/Views/Deck/Manabase.cshtml`,
`DeckFlow.CLI/CedhCalibrateCommandRunner.cs`, `DeckFlow.Core.Tests/Manabase/*`,
`DeckFlow.Web.Tests/*`, `docs/manabase-analysis-rules.md`, `DeckFlow.Web/Help/manabase.md`.
**Files scanned:** 9 source files read directly (full or targeted sections) + 2 test files (full)
+ grep sweeps across `ManabaseClassifier.cs`, `CastabilitySimulator.cs`, `KarstenManabase.cs`,
`ManabaseVerdictSynthesizer.cs`, `ManabaseModels.cs`, `FeatureFlagCatalog.cs`, `FeatureFlagStore.cs`,
`ManabaseAnalyzer.cs`, `ManabaseAnalysisService.cs`, `Manabase.cshtml`.
**Pattern extraction date:** 2026-07-12
