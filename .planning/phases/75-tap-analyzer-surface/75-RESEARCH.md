# Phase 75: Tap Analyzer Surface — Research

**Researched:** 2026-06-28
**Domain:** C# / ASP.NET Core 10 — manabase simulation, Core model extension, feature-flag plumbing, Razor view, paste-artifact text builder
**Confidence:** HIGH — all findings verified against branch-correct source at commit tip of `plan/cycle-13-deck-eval`

---

## Summary

Phase 75 surfaces two tap-quality metrics the `CastabilitySimulator` implicitly tracks but has never exposed: (a) untapped-source composition — the fraction of a deck's colored sources that enter untapped, overall and per color — and (b) turn-1 untapped availability — the share of simulated games in which the player has at least one mana-producing source usable on turn 1. Both numbers are additive counters inside the existing 20 k-trial Monte Carlo loop; no second simulation pass is needed.

The implementation chain is: `CastabilitySimulator` (new `Turn1UntappedTrials` counter) → `CardCastability` (new additive field) → `ManabaseAnalyzer.Analyze` (new `ManabaseTapAnalysis` Core record built from the castability rows + static composition) → `ManabaseReport.TapAnalysis` (new additive `{ get; init; }` field, null-default) → `ManabaseAnalysisResult.ShowTapAnalyzer` (new bool) → `ManabaseViewModel.ShowTapAnalyzer` (new bool) → `Manabase.cshtml` (new third card after `.manabase-twolens`) + `ManabaseReportTextBuilder.Build` (new optional `ManabaseTapAnalysis? tap` parameter, skipped when null so the artifact is byte-identical when the flag is OFF).

The feature flag `analysis.manabase.tap-analyzer` seeds OFF in both SQLite and Postgres. The three places that must be updated to add a flag are: `FeatureFlagCatalog.Descriptions`, the seed SQL in `FeatureFlagStore` (two dialects), and the two flag-guard test files.

**Primary recommendation:** Implement TAP-01 (composition) as a static pass over `deck.Sources` using the already-computed `EffectiveSources(color, untappedOnly: true)` data in `BuildColorFindings` — store it on `ColorSourceFinding.UntappedSources` (new additive field). Implement TAP-02 (T1 availability) by adding `out bool hadUntappedT1` to `SimulateGame` and accumulating a `Turn1UntappedTrials` counter in `Simulate`; average across all `CardCastability` rows in `ManabaseAnalyzer.Analyze` to get the deck-level figure. Both metrics fold into one new `ManabaseTapAnalysis?` record on `ManabaseReport`.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

No CONTEXT.md exists for this phase — no prior `/gsd:discuss-phase` session ran. Constraints come entirely from REQUIREMENTS.md, STATE.md, ROADMAP.md, UI-SPEC.md, and CLAUDE.md.

### Locked Decisions (from STATE.md)
- Tap Analyzer (Phase 75): additive counters ONLY inside the existing 20k-trial loop; `{ get; init; }` fields only (never `required`); flag `analysis.manabase.tap-analyzer` seeded OFF.
- Cycle 13 granularity = coarse; 4 phases.

### Claude's Discretion
- Exact shape of `ManabaseTapAnalysis` record fields and names.
- Whether `Turn1UntappedTrials` lives on `CardCastability` or is computed differently.
- cEDH handling (UI-SPEC defers to plan-phase — see Open Questions below).
- Exact ✓/⚠ threshold value (80% proposed by UI-SPEC; informational only).

### Deferred Ideas (OUT OF SCOPE)
- Rebuilding the CastabilitySimulator engine for any purpose.
- Any new manabase flags beyond `analysis.manabase.tap-analyzer`.
- Any work on Phases 76-78.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| TAP-01 | Manabase report surfaces untapped-source frequency — overall and per color — as a discrete metric. | Verified: `EffectiveSources(color, untappedOnly: true)` already computed in `BuildColorFindings` at ManabaseAnalyzer.cs:443. Store in new `ColorSourceFinding.UntappedSources` additive field + new `ManabaseTapAnalysis` record on `ManabaseReport`. |
| TAP-02 | Manabase report surfaces turn-1 untapped availability — chance of having untapped mana on turn 1. | Verified: add `out bool hadUntappedT1` to `SimulateGame` (check `OnlineMana(landsOnBoard, rampOnBoard, 1) > 0` after turn-1 processing); accumulate into `CardCastability.Turn1UntappedTrials` additive field; average across rows in `ManabaseAnalyzer.Analyze` to produce `ManabaseTapAnalysis.Turn1UntappedPercent`. |
| TAP-03 | Metrics derived within the existing single simulation pass; no second sim; additive `{ get; init; }` fields only; single source of truth per metric. | Verified: (a) composition data already exists as local variables; (b) T1 counter is one new `out bool` param on `SimulateGame` + one counter in the existing trials loop; (c) all new fields follow `{ get; init; }` pattern (see existing `AverageDelay`). No new sim call required. |
| TAP-04 | Metrics appear in both `/manabase` page and paste artifact; behind flag `analysis.manabase.tap-analyzer` seeded OFF; flag OFF = byte-identical output. | Verified: flag plumbing mirrors `analysis.manabase.plain-language-verdict`; text builder takes optional `ManabaseTapAnalysis? tap = null` — omits block when null; `@if (Model.ShowTapAnalyzer && ...)` in view; flag seeded with `FALSE`/`0`. |
</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Untapped-source composition metric | `DeckFlow.Core` (ManabaseAnalyzer) | — | Pure deck data; existing `EffectiveSources` already knows it |
| Turn-1 untapped availability metric | `DeckFlow.Core` (CastabilitySimulator) | ManabaseAnalyzer (aggregation) | Trial-level flag lives in the sim; deck-level average in the analyzer |
| Model storage (`ManabaseTapAnalysis`) | `DeckFlow.Core` (ManabaseModels) | — | Peer of existing Core record types |
| Flag read + result assembly | `DeckFlow.Web` (ManabaseAnalysisService) | — | All other manabase flags read here |
| ViewModel gate (`ShowTapAnalyzer`) | `DeckFlow.Web` (ManabaseViewModel / ManabaseController) | — | Matches `ShowPlainLanguage` / `ShowCommanderCastability` pattern |
| On-page card | `DeckFlow.Web` (Manabase.cshtml) | — | Third lens card after `.manabase-twolens` |
| Paste-artifact section | `DeckFlow.Core` (ManabaseReportTextBuilder) | — | Optional parameter; null → skip → byte-identical |
| CSS layout (2 new classes) | `DeckFlow.Web` (`site-common.css`) | — | Layout-only; chrome from reused `.manabase-lens` |

---

## Standard Stack

No new packages. Every capability builds on in-solution tech — all marked [VERIFIED: codebase].

| Library | Purpose |
|---------|---------|
| `DeckFlow.Core.Manabase` [VERIFIED: codebase] | All Core manabase logic |
| `DeckFlow.Web.Models` [VERIFIED: codebase] | ViewModel + display helpers |
| `DeckFlow.Web.Services.Manabase` [VERIFIED: codebase] | Flag reading + result assembly |
| `DeckFlow.Web.Services.FeatureFlags` [VERIFIED: codebase] | `FeatureFlagCatalog` + `FeatureFlagStore` |
| xUnit 2.9.3 (already present) [VERIFIED: codebase] | Test framework for both test projects |

**Installation:** none — zero new dependencies.

---

## Package Legitimacy Audit

> No external packages are added by this phase. Section skipped — not applicable.

---

## Architecture Patterns

### System Architecture Diagram

```
[ManabaseRequest (form POST)]
        │
        ▼
[ManabaseController.Manabase(POST)]
        │  reads IsFlagOn("analysis.manabase.tap-analyzer")
        ▼
[ManabaseAnalysisService.AnalyzeAsync]
        │  passes showTapAnalyzer bool
        ▼
[ManabaseAnalyzer.Analyze]
   ├── BuildCastability (calls CastabilitySimulator.Simulate per spell)
   │       └── CastabilitySimulator.Simulate
   │               │  existing trial loop (20k)
   │               │  NEW: per-trial "hadUntappedT1?" check via OnlineMana at T1
   │               └─ returns CardCastability { Turn1UntappedTrials: int }  ← NEW field
   │
   ├── BuildColorFindings
   │       └── EffectiveSources(color, untappedOnly=true) ALREADY computed at :443
   │           NEW: stored in ColorSourceFinding.UntappedSources ← NEW field
   │
   └── ComputeTapAnalysis (NEW private method)
           ├── Overall untapped %: sum deck.Sources where EntersUntapped / total Weight
           ├── Per-color: from new ColorSourceFinding.UntappedSources fields
           └── T1 %: average CardCastability.Turn1UntappedTrials / DefaultTrials × 100
           └─ returns ManabaseTapAnalysis  ← NEW Core record
                  stored as ManabaseReport.TapAnalysis  ← NEW additive field
        │
        ▼
[ManabaseAnalysisResult { ShowTapAnalyzer: bool }]  ← NEW bool
        │
        ▼
[ManabaseViewModel { ShowTapAnalyzer: bool }]  ← NEW bool (from result)
        │
   ┌────┴─────────────┐
   ▼                  ▼
[Manabase.cshtml]   [ManabaseReportTextBuilder.Build(..., ManabaseTapAnalysis? tap)]
 @if (ShowTapAnalyzer)  when null → skipped → byte-identical
  third .manabase-taplens card
  after .manabase-twolens
  before .manabase-context
```

### Recommended Project Structure (changes only)

```
DeckFlow.Core/Manabase/
├── CastabilitySimulator.cs     # add out hadUntappedT1 + Turn1UntappedTrials
├── ManabaseModels.cs           # add ManabaseTapAnalysis record + ColorTapFinding record
│                               # add ColorSourceFinding.UntappedSources additive field
│                               # add ManabaseReport.TapAnalysis additive field
│                               # add CardCastability.Turn1UntappedTrials additive field
├── ManabaseAnalyzer.cs         # wire new counters → ManabaseTapAnalysis
└── ManabaseReportTextBuilder.cs  # optional tap parameter + Untapped Sources block

DeckFlow.Web/
├── Services/FeatureFlags/
│   ├── FeatureFlagCatalog.cs   # add "analysis.manabase.tap-analyzer" description
│   └── FeatureFlagStore.cs     # add to PostgresSeedSql + SqliteSeedSql (both FALSE/0)
├── Services/Manabase/
│   └── ManabaseAnalysisService.cs  # add TapAnalyzerFlagKey const + flag read + ShowTapAnalyzer
├── Models/
│   ├── ManabaseViewModel.cs    # add ShowTapAnalyzer bool
│   └── ManabaseDisplay.cs      # add TapMarker(int pct) + TapAnalyzerGloss const
├── Controllers/
│   └── ManabaseController.cs   # pass ShowTapAnalyzer to ViewModel; pass tap to Download
└── Views/Deck/
    └── Manabase.cshtml         # @if (Model.ShowTapAnalyzer) card after .manabase-twolens
DeckFlow.Web/wwwroot/css/
└── site-common.css             # add .manabase-taplens + .manabase-taplens-split (2 rules)
```

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Untapped source fraction | Custom per-color loop | `ManabaseAnalyzer.EffectiveSources(color, untappedOnly: true)` already exists | Already computing this at ManabaseAnalyzer.cs:443; just not stored |
| Turn-1 availability | New simulation object | Additive counter inside existing `SimulateGame` | TAP-03 explicitly forbids a second sim; the existing game loop already models T1 land-play precisely |
| Flag registration | Ad-hoc DB insert | `FeatureFlagStore` seed SQL pattern | All flags go through the two-dialect seed; the `ON CONFLICT DO NOTHING` contract preserves operator toggles |
| CSS colors | New theme tokens | Reused `.manabase-lens` classes + existing tokens | Per the project theme constraint: new colors go in `:root` of every theme fork; the tap card needs none |

**Key insight:** The bulk of the heavy lifting was done in Phases 70-72. This phase is surface work on top of existing infrastructure — the only new computation is one 1-bit per-trial flag and an averaging pass.

---

## Detailed Findings by Research Question

### Q1: CastabilitySimulator — where tapped/untapped state is tracked, where to add counters

**Location:** `DeckFlow.Core/Manabase/CastabilitySimulator.cs` [VERIFIED: codebase]

The sim distinguishes lands by kind:
```csharp
// CastabilitySimulator.cs:46-50 [VERIFIED: codebase]
private enum CardKind { UntappedLand, TappedLand, Ramp, Filler }
```

Lands are classified at library-build time from `ManaSource.EntersUntapped`:
```csharp
// CastabilitySimulator.cs:384 [VERIFIED: codebase]
CardKind kind = source.EntersUntapped ? CardKind.UntappedLand : CardKind.TappedLand;
```

In `SimulateGame`, after playing a land, the `OnlineTurn` is set:
```csharp
// CastabilitySimulator.cs:727 [VERIFIED: codebase]
int onlineTurn = played.Kind == CardKind.TappedLand ? currentTurn + 1 : currentTurn;
landsOnBoard.Add((played.ColorMask, onlineTurn, played.ManaAmount));
```

**Turn-1 untapped availability hook point:** After the land-play and ramp-deploy steps on `currentTurn == 1`, call `OnlineMana(landsOnBoard, rampOnBoard, 1) > 0`. The `OnlineMana` method (CastabilitySimulator.cs:861) already exists and returns 0 when no source has `OnlineTurn <= 1`. This check captures:
- An untapped land played T1 (OnlineTurn = 1)
- A 0-cost fast mana (Lotus Petal, Mox Diamond) deployed T1 (DeployCost == 0 → OnlineTurn = currentTurn = 1)
- A tapped land played T1 does NOT contribute (OnlineTurn = 2)
- A 2-cost rock played T1 does NOT contribute (DeployCost = 2, not affordable, not deployed)

This is the precise definition the UI-SPEC uses: "share of games with an untapped source to spend on turn 1."

**Counter accumulation in `Simulate` (CastabilitySimulator.cs:228-262):** [VERIFIED: codebase]
```csharp
int turn1UntappedSuccesses = 0;  // NEW counter
for (int t = 0; t < trials; t++)
{
    // ... partial-source rolls, shuffle ...
    bool success = SimulateGame(
        ..., out bool manaShort, out bool colorShort, out int firstCastableTurn,
        out bool hadUntappedT1);  // NEW out param
    if (hadUntappedT1) turn1UntappedSuccesses++;  // NEW accumulation
    // ... rest of trial ...
}
// Return:
return new CardCastability
{
    // ... existing 7 fields ...
    Turn1UntappedTrials = turn1UntappedSuccesses,  // NEW additive field
};
```

**In `SimulateGame`** (CastabilitySimulator.cs:470): add `out bool hadUntappedT1`, initialize to `false`, set it once after `currentTurn == 1` processing (after `PlayOneLand` and `TryDeployRamp` calls, before the `if (currentTurn < turn) continue` guard):
```csharp
// Set ONCE at T1, before the early-continue:
if (currentTurn == 1)
{
    hadUntappedT1 = OnlineMana(landsOnBoard, rampOnBoard, 1) > 0;
}
```

The `PlayOneLand` and `TryDeployRamp` calls already happen before this point in the loop, so `landsOnBoard` and `rampOnBoard` are populated at this point. [VERIFIED: reading loop structure at CastabilitySimulator.cs:520-597]

### Q2: `CardCastability` and `ManabaseReport` record shapes

**`CardCastability`** (`ManabaseModels.cs:133-169`): [VERIFIED: codebase]
- All 7 existing fields use `public required ... { get; init; }` (Name, ManaValue, OnCurveTurn, CastPercent, LimitingFactor) or `public ... { get; init; }` with defaults (IsCommander, IsCostOverridden, AverageDelay)
- The `AverageDelay` field (`ManabaseModels.cs:168`) is the exact pattern to mirror: `public double AverageDelay { get; init; }` — not `required`, safe default 0
- New field: `public int Turn1UntappedTrials { get; init; }` — analogous to `AverageDelay`

**`ManabaseReport`** (`ManabaseModels.cs:553-951`): [VERIFIED: codebase]
- Sealed record with `required` fields (ActualLands, TargetLands, ColorFindings, Summary) + optional additive fields with safe defaults (Castability = `Array.Empty<>()`, LandTarget = null, DemandingCards = `Array.Empty<>()`, etc.)
- Pattern for new optional field: `public ManabaseTapAnalysis? TapAnalysis { get; init; }` — null default, identical to `ManabaseLandTargetBreakdown? LandTarget { get; init; }`

**`ColorSourceFinding`** (`ManabaseModels.cs:315-392`): [VERIFIED: codebase]
- All fields are `{ get; init; }`, no `required`
- New additive field: `public double UntappedSources { get; init; }` — safe default 0.0

**New Core record `ManabaseTapAnalysis`** (new, goes in `ManabaseModels.cs`):
```csharp
public sealed record ManabaseTapAnalysis
{
    /// <summary>Overall untapped fraction (0-100), across all weighted sources.</summary>
    public int OverallUntappedPercent { get; init; }

    /// <summary>Weighted untapped source count (numerator for OverallUntappedPercent).</summary>
    public double UntappedSources { get; init; }

    /// <summary>Total weighted source count (denominator).</summary>
    public double TotalSources { get; init; }

    /// <summary>
    /// Turn-1 untapped availability: fraction of simulated games where player had ≥1 mana source
    /// available to spend on turn 1. Averaged across the castability spell rows (0-100).
    /// </summary>
    public int Turn1UntappedPercent { get; init; }

    /// <summary>Per-color untapped composition (key = ManaColor, value = the finding).</summary>
    public IReadOnlyDictionary<ManaColor, ColorTapFinding> ColorTap { get; init; }
        = new Dictionary<ManaColor, ColorTapFinding>();
}

public sealed record ColorTapFinding
{
    /// <summary>Weighted untapped sources of this color.</summary>
    public double UntappedSources { get; init; }

    /// <summary>Total weighted sources of this color (equals ColorSourceFinding.ActualSources).</summary>
    public double TotalSources { get; init; }

    /// <summary>Rounded untapped fraction (0-100).</summary>
    public int UntappedPercent { get; init; }
}
```

**20k-trial loop populates `CardCastability.Turn1UntappedTrials`:** This value divided by `DefaultTrials` (20_000) gives the per-spell T1 availability fraction. All spell sims use the same library (modulo the self-exclusion for land-ramp spells), so T1 figures are nearly identical across spells. The deck-level figure is computed as the mean across all castability rows (excluding commanders for purity, but including them is also defensible — plan-phase decision).

### Q3: /manabase page render path

**Controller action:** `ManabaseController.Manabase(ManabaseRequest)` at `DeckFlow.Web/Controllers/ManabaseController.cs:77` [VERIFIED: codebase]
- Calls `RunAnalysisAsync` → `ManabaseAnalysisService.AnalyzeAsync` → returns `ManabaseAnalysisResult`
- Builds `ManabaseViewModel { ShowTapAnalyzer = result.ShowTapAnalyzer }` (new property)

**View:** `DeckFlow.Web/Views/Deck/Manabase.cshtml` [VERIFIED: codebase]
- The `.manabase-twolens` grid renders at line 180 and closes at line 226 (`</div>`)
- `.manabase-context` paragraph is at line 228
- The tap analyzer card inserts between lines 226 and 228:
  ```razor
  @if (Model.ShowTapAnalyzer && Model.HasResult && report?.TapAnalysis is { } tap)
  {
      <div class="manabase-lens manabase-taplens" role="group" aria-label="Untapped sources">
          <!-- ... per UI-SPEC Section 2 wireframe ... -->
      </div>
  }
  ```
- When `ShowTapAnalyzer == false`, the `@if` is false → no element, no whitespace, no comment → byte-identical

**View model property:** `ManabaseViewModel.ShowTapAnalyzer` — new bool, analogous to `ShowPlainLanguage` (ManabaseViewModel.cs:45) [VERIFIED: codebase]

### Q4: Paste artifact / zip round-trip path

**Method:** `ManabaseReportTextBuilder.Build(ManabaseReport report, string? deckName, string? decklistText, ManabaseMode mode, ManabaseVerdict? verdict, ManabaseRampDrawBudget? budget)` at `ManabaseReportTextBuilder.cs:28` [VERIFIED: codebase]

**Where to insert "Untapped Sources:" block:** After the "Color Sources:" table block (which ends at `sb.AppendLine();` around line 111), before the "Biggest fix callout" block. This matches the UI-SPEC Section 9 specification. [VERIFIED: reading ManabaseReportTextBuilder.cs:96-112]

**Signature change:** Add optional `ManabaseTapAnalysis? tap = null` parameter at the end of `Build(...)`. When `tap is null`, skip the block entirely — no bytes added. When `tap is not null`, append the "Untapped Sources:" section.

**The `Download` action** calls `ManabaseReportTextBuilder.Build` at `ManabaseController.cs:127-128` [VERIFIED: codebase]:
```csharp
string text = ManabaseReportTextBuilder.Build(
    result.Report, request.DeckName, decklistText: null, request.Mode, result.Verdict, result.Budget);
```
This must be extended to also pass `tap: result.ShowTapAnalyzer ? result.Report.TapAnalysis : null`.

**Byte-identity when OFF:** When `analysis.manabase.tap-analyzer` is OFF, `result.ShowTapAnalyzer = false`, so `tap = null` is passed, block is skipped, artifact bytes are unchanged. [ASSUMED as design intent — follows same pattern as `verdict`/`budget` optional blocks]

### Q5: Feature flag plumbing

**`FeatureFlagCatalog.Descriptions` dictionary** at `FeatureFlagCatalog.cs:14` [VERIFIED: codebase]:
- Pattern: one entry per flag key → one-line operator description
- New entry: `["analysis.manabase.tap-analyzer"] = "Surface untapped-source frequency and turn-1 untapped availability on the mana base page and its paste artifact. Off = byte-identical output."`

**`FeatureFlagStore` seed SQL** (two dialects): [VERIFIED: codebase]
- `PostgresSeedSql` at `FeatureFlagStore.cs:198` — add `('analysis.manabase.tap-analyzer', FALSE)`
- `SqliteSeedSql` at `FeatureFlagStore.cs:230` — add `('analysis.manabase.tap-analyzer', 0)`
- Both use `ON CONFLICT (key) DO NOTHING` — operator toggles survive re-bootstrap

**Existing namespacing pattern for manabase analysis flags:** [VERIFIED: codebase]
```
analysis.manabase.source-mana-quantity
analysis.manabase.ramp-credit-v2
analysis.manabase.color-aware-mulligan
analysis.manabase.land-ramp-sim
analysis.manabase.health-band-castability
analysis.manabase.health-band-headline-floor
analysis.manabase.plain-language-verdict
analysis.manabase.commander-castability
```
New flag: `analysis.manabase.tap-analyzer` — fits the pattern precisely.

**`ManabaseAnalysisService` flag key constant pattern** (ManabaseAnalysisService.cs:133-180): [VERIFIED: codebase]
```csharp
public const string TapAnalyzerFlagKey = "analysis.manabase.tap-analyzer";
```
Read with `IsFlagOn(TapAnalyzerFlagKey)`, stored in `ManabaseAnalysisResult` as a new bool property.

**`IsFlagOn` method** reads from `_featureFlags.Snapshot()` — the `FeatureFlagCache` snapshot, not a live DB call. Fail-safe OFF behavior: if the key is absent from the DB, `TryGetValue` returns false (flag defaults OFF). [VERIFIED: ManabaseAnalysisService.cs IsFlagOn usage pattern]

### Q6: Existing tests — coverage map and required additions

**Existing test files** (all verified by directory listing): [VERIFIED: codebase]

| File | Covers |
|------|--------|
| `DeckFlow.Core.Tests/Manabase/CastabilitySimulatorTests.cs` | Sim seeding, trial accuracy, per-spell behavior |
| `DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerTests.cs` | Analyzer integration, color findings, health |
| `DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs` | Text builder output format |
| `DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerCoverageTests.cs` | Coverage fixture tests |
| `DeckFlow.Core.Tests/Manabase/AvatarManabaseRegressionTests.cs` | Regression baseline against Avatar deck |
| `DeckFlow.Web.Tests/Manabase/ManabaseDisplayTests.cs` | Display helper unit tests |
| `DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs` | Download action + text file output |
| `DeckFlow.Web.Tests/Manabase/ManabaseFlagBaselineHarness.cs` | Flag baseline snapshots |
| `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` | Every seeded flag has a description |
| `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` | Each flag seeds at its expected default |

**No byte-identity test exists today** for the text builder (the tests assert contains-string, not equality). The plan must add one for flag-OFF byte-identity.

**New tests the plan must include:**

1. **`ManabaseTapAnalysisTests.cs`** (new file in `DeckFlow.Core.Tests/Manabase/`):
   - Given a deck with 75% untapped sources, `ManabaseTapAnalysis.OverallUntappedPercent` = 75
   - Per-color: a color with all untapped sources shows 100%; a color with all tapped shows 0%
   - Given a deck of all untapped lands, `Turn1UntappedPercent` ≥ 95% (near-certain T1 land available)
   - Given a deck of all tapped lands (no fast mana), `Turn1UntappedPercent` = 0%
   - Single-color deck: `ColorTap` has one entry; multi-color has one per color

2. **`ManabaseReportTextBuilderTests.cs`** (additions):
   - `Build(..., tap: null)` output equals `Build(...)` without tap parameter — byte-identity guard
   - `Build(..., tap: tapFixture)` output contains "Untapped Sources:" section
   - `Build(..., tap: tapFixture)` output contains "Turn-1 untapped availability:" line
   - Single-color deck with `tap` set: no per-color table emitted (TAP-01 scope: per-color only for multi-color)

3. **`FeatureFlagCatalogTests.cs`** (addition):
   - `[InlineData("analysis.manabase.tap-analyzer")]` in `Describe_EverySeededFlag_HasNonEmptyDescription`

4. **`FeatureFlagStoreSeedTests.cs`** (addition):
   - `[InlineData("analysis.manabase.tap-analyzer", false)]` in `EnsureSchema_SeedsManabaseFlags_AtExpectedDefault`

5. **`ManabaseDisplayTests.cs`** (additions):
   - `TapMarker(80)` → `("manabase-lens-met", "✓")`
   - `TapMarker(79)` → `("manabase-lens-short", "⚠")`
   - `TapMarker(100)` → met; `TapMarker(0)` → short

6. **`ManabaseControllerDownloadTests.cs`** (addition):
   - When flag is OFF (ShowTapAnalyzer = false), download artifact does not contain "Untapped Sources:"
   - When flag is ON (ShowTapAnalyzer = true), download artifact contains "Untapped Sources:" and "Turn-1"

**Determinism / seed note:** The sim uses `StableSeed(spell.Name)` (FNV-1a hash, CastabilitySimulator.cs:1358) for reproducible per-spell RNG. `Turn1UntappedTrials` inherits this determinism — same deck + same spell name → same count across runs. This makes byte-identity tests straightforward.

### Q7: Theme + mobile

**CSS files that need changes:** only `site-common.css`. [VERIFIED: UI-SPEC.md Section 3 + site-common.css inspection]

**Two new classes** (purely structural/layout, zero new color tokens):

```css
/* .manabase-taplens — full-width card tying to the grid above */
.manabase-taplens {
  margin: -0.25rem 0 1rem;
}

/* .manabase-taplens-split — two-column internal layout (headline | per-color list) */
.manabase-taplens-split {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr);
  gap: 1rem 1.5rem;
  align-items: start;
}

@media (max-width: 640px) {
  .manabase-taplens-split {
    grid-template-columns: 1fr;
  }
}
```

The `max-width: 640px` breakpoint is the same one used for `.manabase-twolens` at `site-common.css:2769-2773`. [VERIFIED: codebase]

**No per-theme fork changes:** All visible colors come from reused `.manabase-lens` classes and their existing token set (`--panel-soft-bg`, `--line`, `--success`, `--gold-warning`, `--accent-strong`, `--muted`, `--info`). These tokens are already defined in all 22 guild theme forks. [VERIFIED: UI-SPEC.md Section 6 + project CLAUDE.md theme constraint]

**Verify on:** `site.css` (Jeskai — default), `site-azorius.css`, `site-nyx.css` as the three canonical theme-verification targets per prior phases.

---

## Common Pitfalls

### Pitfall 1: Using `{ get; }` (get-only) instead of `{ get; init; }` on new record fields
**What goes wrong:** System.Text.Json silently skips get-only properties in .NET 9+ (the `{ get; }` carve-out from CLAUDE.md). Any zip/serialization round-trip loses the new fields. The CarveOutGuard test in CI catches this, but only if the format gate runs.
**Why it happens:** The `dotnet format` tool does NOT convert `{ get; init; }` to `{ get; }`, but a hand-written `required` record field with no default will prevent zero-arg construction in tests.
**How to avoid:** Mirror `AverageDelay` (CardCastability.cs:168) — `public double AverageDelay { get; init; }` — not `required`. All new fields are optional with safe defaults.
**Warning signs:** Test failure in `CarveOutGuardTests` or any test that round-trips `CardCastability` or `ManabaseReport` through JSON.

### Pitfall 2: Adding a second simulation invocation for T1 availability
**What goes wrong:** Violates TAP-03 ("no second pass, no new sim"). Also adds ~20k × N_spells × 2 trial overhead, doubling analysis latency.
**Why it happens:** Treating T1 availability as needing a separate probe deck rather than as a counter inside existing trials.
**How to avoid:** Add `out bool hadUntappedT1` to `SimulateGame` and set it on `currentTurn == 1` — it's a 1-bit observation inside the loop that already runs. The `OnlineMana(landsOnBoard, rampOnBoard, 1)` call is O(N_lands), fast.
**Warning signs:** Any new call to `CastabilitySimulator.Simulate` that wasn't there before.

### Pitfall 3: Emitting any HTML when the flag is OFF
**What goes wrong:** Byte-identity test fails; diff shows whitespace or comment nodes in the page output.
**Why it happens:** Using `<!--` comments or `@* *@` Razor comments inside the `@if` block, which still emit bytes.
**How to avoid:** The entire card markup must be inside `@if (Model.ShowTapAnalyzer && Model.HasResult && report?.TapAnalysis is { } tap)` with NO trailing whitespace, comment, or empty line outside the block. (Same pattern as the `ShowPlainLanguage` gloss blocks in the existing view.)

### Pitfall 4: Missing the `ManabaseReportTextBuilder.Build` call in the `Download` action
**What goes wrong:** The on-page card shows tap metrics; the downloaded `.txt` artifact does not. TAP-04 requires both surfaces to be consistent.
**Why it happens:** `ManabaseController.Download` calls `ManabaseReportTextBuilder.Build` directly with the result report (ManabaseController.cs:127), not through the analysis result.
**How to avoid:** When adding the `tap` parameter to `Build`, also update the call in `Download` to pass `tap: result.ShowTapAnalyzer ? result.Report.TapAnalysis : null`.

### Pitfall 5: Per-color breakdown for single-color decks
**What goes wrong:** A mono-color deck shows a per-color list with one entry — visually redundant with "Overall" and potentially confusing.
**Why it happens:** Iterating `report.ColorFindings` unconditionally.
**How to avoid:** Per the UI-SPEC (Section 4, single-color state), when `report.ColorFindings.Count == 1` emit only the "Overall" row and omit the per-color list. Same guard in the text builder.

### Pitfall 6: Averaging Turn1UntappedTrials across commander rows vs non-commander rows
**What goes wrong:** Commanders are often excluded from the castability table but still have sim rows. Including them biases the average (a commander is not a T1 play in most games).
**Why it happens:** Averaging all `Castability` rows without filtering.
**How to avoid:** Average only non-commander rows (`!r.IsCommander`) unless the deck is all-commander (edge case — fall back to all rows if none are non-commander). Plan-phase decision: either exclude commanders or include all; pick consistently for page + artifact.

---

## Code Examples

### Pattern: Additive `{ get; init; }` field on an existing record

```csharp
// Existing pattern from CardCastability (ManabaseModels.cs:168) [VERIFIED: codebase]
public double AverageDelay { get; init; }

// New field mirrors exactly:
public int Turn1UntappedTrials { get; init; }
```

### Pattern: Optional additive field on ManabaseReport

```csharp
// Existing pattern (ManabaseModels.cs:853) [VERIFIED: codebase]
public ManabaseLandTargetBreakdown? LandTarget { get; init; }

// New field mirrors:
public ManabaseTapAnalysis? TapAnalysis { get; init; }
```

### Pattern: Flag read + result propagation in ManabaseAnalysisService

```csharp
// Existing pattern (ManabaseAnalysisService.cs:248-249) [VERIFIED: codebase]
bool plainLanguage = IsFlagOn(PlainLanguageVerdictFlagKey);
// ... later in result:
return new ManabaseAnalysisResult(report, ..., showPlainLanguage: plainLanguage) { ... };

// New pattern:
bool showTapAnalyzer = IsFlagOn(TapAnalyzerFlagKey);
// ... pass tap = showTapAnalyzer ? report.TapAnalysis : null to text builder
// ... in result:
return new ManabaseAnalysisResult(..., showTapAnalyzer) { ... };
```

### Pattern: ViewModel bool gate

```csharp
// Existing pattern (ManabaseViewModel.cs:45) [VERIFIED: codebase]
public bool ShowPlainLanguage { get; init; }

// New:
public bool ShowTapAnalyzer { get; init; }
```

### Pattern: View conditional block (byte-identical when false)

```razor
@* EXISTING pattern for plain language gloss (Manabase.cshtml:205-209) [VERIFIED: codebase] *@
@if (showPlainLanguage)
{
    <p class="manabase-lens-gloss">@ManabaseDisplay.KarstenSourceGloss</p>
}

@* TAP CARD pattern — analogous: *@
@if (Model.ShowTapAnalyzer && Model.HasResult && report?.TapAnalysis is { } tap)
{
    <div class="manabase-lens manabase-taplens" role="group" aria-label="Untapped sources">
        @* ... UI-SPEC wireframe markup ... *@
    </div>
}
```

### Pattern: Optional text-builder parameter

```csharp
// Existing signature (ManabaseReportTextBuilder.cs:28) [VERIFIED: codebase]
public static string Build(
    ManabaseReport report,
    string? deckName,
    string? decklistText,
    ManabaseMode mode = ManabaseMode.Casual,
    ManabaseVerdict? verdict = null,
    ManabaseRampDrawBudget? budget = null)

// Extended signature:
public static string Build(
    ManabaseReport report,
    string? deckName,
    string? decklistText,
    ManabaseMode mode = ManabaseMode.Casual,
    ManabaseVerdict? verdict = null,
    ManabaseRampDrawBudget? budget = null,
    ManabaseTapAnalysis? tap = null)  // NEW — null = skip block = byte-identical
```

### Pattern: Feature flag seed entry

```sql
-- PostgresSeedSql addition (FeatureFlagStore.cs:198 block) [VERIFIED: codebase]
-- The existing last entry is ('analysis.command-zone-awareness', FALSE)
-- Insert before the ON CONFLICT line:
('analysis.manabase.tap-analyzer', FALSE)

-- SqliteSeedSql (FeatureFlagStore.cs:230 block) same pattern:
('analysis.manabase.tap-analyzer', 0)
```

### Pattern: ManabaseDisplay helper (mirrors CastChip)

```csharp
// Existing (ManabaseDisplay.cs:63) [VERIFIED: codebase]
public static (string Css, string Label) CastChip(int castPercent)
{
    if (castPercent < 70) return ("manabase-chip--low", "low");
    if (castPercent < 90) return ("manabase-chip--ok", "ok");
    return ("manabase-chip--good", "good");
}

// New (mirrors with the 80% informational threshold from UI-SPEC):
public static (string Css, string Marker) TapMarker(int untappedPercent)
    => untappedPercent >= 80
        ? ("manabase-lens-met", "✓")
        : ("manabase-lens-short", "⚠");

public const string TapAnalyzerGloss =
    "Tapped lands (Temples, tri-lands, taplands) can't tap for mana the turn they enter, " +
    "so they push back your first castable turn. Higher untapped % = faster, smoother starts.";
```

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|------------------|--------|
| No tap-quality surface anywhere | Per UI-SPEC: third lens card on /manabase + "Untapped Sources:" block in .txt | Closes the gap between what the sim tracks internally and what users can see |
| `untappedSources` per color computed but discarded in `BuildColorFindings` local scope | Stored on `ColorSourceFinding.UntappedSources` (new additive field) | Zero new computation — just preserves existing work |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Averaging `Turn1UntappedTrials` across all non-commander castability rows gives a stable deck-level T1 figure | Q1 / TAP-02 | If spell-to-spell variance is high (e.g. land-ramp self-exclusion shifts T1 dramatically for some spells), the average may be noisy. Mitigation: the self-exclusion excludes at most 1 land-ramp card per name, which has trivial effect on T1 across 20k trials. |
| A2 | "Untapped sources" denominator includes non-land sources (rocks, dorks) since `ManaSource.EntersUntapped = true` by default for non-lands | Q1 / TAP-01 | If the planner decides tap metrics should be land-only, the composition formula changes slightly. The `EntersUntapped` flag is authoritative. |
| A3 | The tap card shows T1 availability in cEDH mode (since `BuildCastability` runs regardless of mode) | Q3 / UI-SPEC Section 4 | If the sim is later made mode-conditional, cEDH would need the "reduced card" fallback from UI-SPEC Section 4. Verify when coding the view. |
| A4 | Text builder byte-identity is ensured by `tap = null` producing zero appended bytes | Q4 / TAP-04 | If the caller passes a non-null `tap` accidentally when flag is off, the byte-identity contract breaks. Gate must be `tap: result.ShowTapAnalyzer ? result.Report.TapAnalysis : null` — null when flag is OFF. |

**If this table is empty the above four items need planner confirmation before locking.**

---

## Open Questions (RESOLVED)

> All four resolved during plan-phase (2026-06-28). Authoritative locked decisions live in `75-01-PLAN.md` "LOCKED DESIGN DECISIONS" (labels D1-D5). Recorded here for traceability.

1. **Averaging T1 across spells vs a dedicated probe spell**
   - What we know: Each spell's `Turn1UntappedTrials` measures the same "did T1 have an untapped source?" question, so all non-commander rows should give nearly identical numbers.
   - What's unclear: The planner may prefer a single "probe spell" (a colorless MV-1 dummy) to isolate T1 from spell-specific library effects.
   - **RESOLVED (D1):** Average across non-commander rows (already run; a probe would need a new sim call, which TAP-03 disallows). Fall back to all rows if no non-commander rows exist.

2. **cEDH tap-card display: full card or reduced card?**
   - What we know: The sim runs in cEDH (BuildCastability is not mode-gated); the cast-rate LENS is hidden at the view layer (`ShowCastability` = false in cEDH). T1 data is therefore available.
   - What's unclear: Should the view show the T1 headline in cEDH (where T1 performance matters MORE than in Casual), or render the "reduced" fallback from UI-SPEC Section 4?
   - **RESOLVED (D2):** Show the FULL card in cEDH — the sim runs, T1 data exists. The UI-SPEC "reduced cEDH card" path assumed the sim might NOT run; since it does, full card is correct.

3. **Include/exclude commander rows when averaging Turn1UntappedTrials?**
   - What we know: Commanders have their own castability rows; their Turn1UntappedTrials counts reflect the same T1 check (the spell itself doesn't affect T1 untapped mana).
   - **RESOLVED (D3):** Exclude commander rows, consistent with how `AvgOnCurve` is shown in the cast-rate lens (commanders are visually separated).

4. **✓/⚠ threshold at 80%: absolute or mode-specific?**
   - UI-SPEC proposes 80% as informational, never affecting health. Casual and cEDH both use 80%.
   - **RESOLVED (D4):** 80% flat, mode-independent, labeled informational, never affects health. Distinct from the health-band threshold (80%/88% mode-dependent) — must not be conflated.

> D5 (denominator): untapped composition denominator = all colored sources via `EffectiveSources` weighting, with `EntersUntapped` authoritative. TAP-02 metric definition locked as deck-level "share of simulated games with ≥1 untapped mana source to spend on turn 1" (NOT per-color).
>
> **OVERRIDE 2026-06-28 (post-execution, after Codex review):** The TAP-02 turn-1 metric is now **color-matched** — "share of simulated games with ≥1 untapped source of a NEEDED COLOR on turn 1." A Codex review HIGH finding showed the prior `OnlineMana(...) > 0` check counted colorless and off-color untapped sources as a turn-1 success even for a colored spell, overstating the figure. Colorless spells (no colored pips) still accept any untapped source. This supersedes the prior "any untapped source" lock, per user decision. Implemented via `CastabilitySimulator.HasColorMatchedUntappedT1`; determinism preserved (the change adds no RNG draw, only a color-mask intersection over existing board state).

---

## Environment Availability

> Step 2.6: SKIPPED — phase is code/config changes only; no external tools, services, runtimes, or CLIs beyond the project's existing build pipeline (dotnet build, dotnet test).

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework (Core) | xUnit 2.9.3 (`DeckFlow.Core.Tests.csproj`) |
| Framework (Web) | xUnit 2.9.3 (`DeckFlow.Web.Tests.csproj`) |
| Config file | `DeckFlow.sln` — both test projects part of solution |
| Quick run command | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseTap" -x` |
| Full suite command | `dotnet test DeckFlow.sln` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| TAP-01 | Untapped composition % computed correctly from deck.Sources | unit | `dotnet test DeckFlow.Core.Tests --filter ManabaseTapAnalysis -x` | ❌ Wave 0: `DeckFlow.Core.Tests/Manabase/ManabaseTapAnalysisTests.cs` |
| TAP-02 | Turn-1 untapped availability from sim counter | unit + determinism | `dotnet test DeckFlow.Core.Tests --filter ManabaseTapAnalysis -x` | ❌ Wave 0: same file |
| TAP-03 | No second sim pass; `CardCastability.Turn1UntappedTrials` additive; `{ get; init; }` enforced | unit | `dotnet test DeckFlow.Core.Tests --filter CarveOutGuard -x` | ✅ `CarveOutGuardTests.cs` (existing, guards init accessor) |
| TAP-03 | Single source of truth: page value equals artifact value for same input | unit | `dotnet test DeckFlow.Core.Tests --filter TextBuilder -x` | ❌ Wave 0 addition to `ManabaseReportTextBuilderTests.cs` |
| TAP-04 (flag OFF) | Artifact byte-identical when flag OFF | unit | `dotnet test DeckFlow.Core.Tests --filter TextBuilder -x` | ❌ Wave 0 addition |
| TAP-04 (flag seed) | Flag seeds FALSE in both SQLite and Postgres | unit | `dotnet test DeckFlow.Web.Tests --filter FeatureFlagStoreSeed -x` | ✅ `FeatureFlagStoreSeedTests.cs` (needs one InlineData) |
| TAP-04 (catalog) | Flag has non-empty description in catalog | unit | `dotnet test DeckFlow.Web.Tests --filter FeatureFlagCatalog -x` | ✅ `FeatureFlagCatalogTests.cs` (needs one InlineData) |
| TAP-04 (display helper) | `TapMarker` ≥80 → met; <80 → short | unit | `dotnet test DeckFlow.Web.Tests --filter ManabaseDisplay -x` | ❌ Wave 0 addition to `ManabaseDisplayTests.cs` |

### Sampling Rate
- **Per task commit:** `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~Manabase" -x` (fast; ~30 s)
- **Per wave merge:** `dotnet test DeckFlow.sln`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `DeckFlow.Core.Tests/Manabase/ManabaseTapAnalysisTests.cs` — covers TAP-01 + TAP-02 determinism
- [ ] Addition to `DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs` — covers TAP-03 single-source-of-truth + TAP-04 byte-identity
- [ ] Addition to `DeckFlow.Web.Tests/Manabase/ManabaseDisplayTests.cs` — covers `TapMarker` helper
- [ ] Addition to `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` — one `[InlineData]`
- [ ] Addition to `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` — one `[InlineData]`
- [ ] `DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs` — addition for download artifact content when flag ON vs OFF

---

## Security Domain

> `security_enforcement` is not explicitly set to `false` in `.planning/config.json`. Applying required check.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | — (no new auth surface) |
| V3 Session Management | No | — (no session state added) |
| V4 Access Control | No | — (the flag is admin-gated by the existing `/Admin/Flags` Basic Auth) |
| V5 Input Validation | No | — (no new user input; metrics are computed from existing deck data) |
| V6 Cryptography | No | — (deterministic FNV-1a seed already in use; no new crypto) |

**Threat patterns relevant to this phase:**

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Tap metrics contradicting health verdict | Spoofing / Confusion | Enforced by design: tap ≥80% ⚠ can co-exist with Excellent health; note in text says "never contradicts it"; separate informational axis |
| Emitting bytes when flag is OFF | Information Disclosure | `@if (Model.ShowTapAnalyzer)` fully wraps all markup; text builder null guard; byte-identity test |

No new attack surfaces introduced. Phase is entirely additive to an existing page behind existing auth.

---

## Sources

### Primary (HIGH confidence — verified against branch-correct codebase)
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` — full sim loop, `SimulateGame`, `CardKind`, `OnlineMana`, `StableSeed`
- `DeckFlow.Core/Manabase/ManabaseModels.cs` — `CardCastability`, `ManabaseReport`, `ColorSourceFinding`, `ManaSource`
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` — `EffectiveSources`, `BuildColorFindings`, `BuildCastability`, `Analyze`
- `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` — `Build` signature, "Color Sources:" block placement
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — flag description pattern
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — seed SQL, both dialects
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — flag constants, `IsFlagOn`, `ManabaseAnalysisResult`
- `DeckFlow.Web/Models/ManabaseViewModel.cs` — `ShowPlainLanguage`, `ShowCommanderCastability` patterns
- `DeckFlow.Web/Models/ManabaseDisplay.cs` — `CastChip`, `KarstenMet` patterns
- `DeckFlow.Web/Controllers/ManabaseController.cs` — render path, Download action
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` — `.manabase-twolens` at line 180; `.manabase-context` at line 228
- `DeckFlow.Web/wwwroot/css/site-common.css` — existing lens classes at lines 2594-2773
- `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` — InlineData guard pattern
- `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` — seed value guard pattern
- `.planning/phases/75-tap-analyzer-surface/UI-SPEC.md` — full design contract

### Secondary (MEDIUM confidence)
- `.planning/REQUIREMENTS.md` — TAP-01..TAP-04 requirement text
- `.planning/STATE.md` — locked decision: "additive counters ONLY inside the existing 20k-trial loop; { get; init; } fields only; flag seeded OFF"
- `.planning/ROADMAP.md` — Phase 75 success criteria

### Tertiary (LOW confidence)
None — all claims verified against current branch codebase.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no external packages; all in-solution
- Architecture: HIGH — every method, line, and class cited verified in codebase
- Pitfalls: HIGH — all based on observed code patterns and prior phase notes
- Open questions: MEDIUM — design decisions deferred to plan-phase as noted

**Research date:** 2026-06-28
**Valid until:** 2026-07-28 (stable — Core manabase code changes rarely between phases)
