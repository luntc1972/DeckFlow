# Phase 2 — ManabaseBaselineWeighting (pure helper) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a pure, zero-dependency Core helper `ManabaseBaselineWeighting.Compute(...)` that turns a commander's per-bracket average lands/ramp/draw (with its sample size) plus a global-per-bracket fallback into a confidence-weighted baseline result (commander value when the sample is solid, blended toward the global baseline as it thins, global when absent).

**Architecture:** One pure static class + two records + one enum in `DeckFlow.Core/Manabase/`. No I/O, no storage, no analyzer wiring (those are Phases 1/3/4). This is the heart of the feature and is independently testable.

**Tech Stack:** C# 12 / .NET 10, xUnit (`DeckFlow.Core.Tests`). No new dependencies. LF endings; changed lines pass the format gate.

**Conventions (confirmed):** Core types live in `namespace DeckFlow.Core.Manabase;`, `public enum` + `public sealed record` style (see `ManabaseRampDrawBudget.cs`). Test files use `namespace DeckFlow.Core.Tests;`, xUnit is a global `<Using Include="Xunit" />` (no `using Xunit;` needed), ImplicitUsings on.

**Build/test:**
- Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj`
- Test: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "ManabaseBaselineWeightingTests"`

---

## File Structure

**Create:**
- `DeckFlow.Core/Manabase/ManabaseBaselineWeighting.cs` — enum `ManabaseBaselineSource`, records `ManabaseBaselineMetric` + `ManabaseBaselineResult`, static `ManabaseBaselineWeighting`.
- `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs` — unit tests.

---

## Task 1: Define the types + failing tests

**Files:**
- Create (stub): `DeckFlow.Core/Manabase/ManabaseBaselineWeighting.cs`
- Create: `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`:

```csharp
using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for the confidence-weighted manabase baseline: commander value when the sample is solid,
/// linear blend toward the global bracket baseline in the mid band, global when the sample is thin
/// or the commander cell is missing.
/// </summary>
public sealed class ManabaseBaselineWeightingTests
{
    [Fact]
    public void Solid_sample_uses_commander_values()
    {
        var r = ManabaseBaselineWeighting.Compute(
            commanderLands: 34, commanderRamp: 10, commanderDraw: 9, commanderDeckCount: 500,
            globalLands: 35.5, globalRamp: 9, globalDraw: 8);

        Assert.Equal(34, r.Lands.Value);
        Assert.Equal(ManabaseBaselineSource.Commander, r.Lands.Source);
        Assert.Equal(10, r.Ramp.Value);
        Assert.Equal(ManabaseBaselineSource.Commander, r.Ramp.Source);
        Assert.Equal(9, r.Draw.Value);
        Assert.Equal(ManabaseBaselineSource.Commander, r.Draw.Source);
        Assert.Equal(44, r.TotalSources);          // lands + ramp
        Assert.Equal(500, r.CommanderDeckCount);
    }

    [Fact]
    public void Thin_sample_uses_global_values()
    {
        var r = ManabaseBaselineWeighting.Compute(
            commanderLands: 34, commanderRamp: 10, commanderDraw: 9, commanderDeckCount: 50,
            globalLands: 35.5, globalRamp: 9, globalDraw: 8);

        Assert.Equal(35.5, r.Lands.Value);
        Assert.Equal(ManabaseBaselineSource.Global, r.Lands.Source);
        Assert.Equal(9, r.Ramp.Value);
        Assert.Equal(ManabaseBaselineSource.Global, r.Ramp.Source);
        Assert.Equal(8, r.Draw.Value);
        Assert.Equal(ManabaseBaselineSource.Global, r.Draw.Source);
    }

    [Fact]
    public void Mid_sample_blends_linearly()
    {
        // deckCount 250 -> w = (250-100)/(400-100) = 0.5
        var r = ManabaseBaselineWeighting.Compute(
            commanderLands: 30, commanderRamp: 12, commanderDraw: 10, commanderDeckCount: 250,
            globalLands: 36, globalRamp: 8, globalDraw: 6);

        Assert.Equal(33, r.Lands.Value, 3);        // 0.5*30 + 0.5*36
        Assert.Equal(ManabaseBaselineSource.Blended, r.Lands.Source);
        Assert.Equal(10, r.Ramp.Value, 3);         // 0.5*12 + 0.5*8
        Assert.Equal(ManabaseBaselineSource.Blended, r.Ramp.Source);
        Assert.Equal(8, r.Draw.Value, 3);          // 0.5*10 + 0.5*6
        Assert.Equal(ManabaseBaselineSource.Blended, r.Draw.Source);
    }

    [Fact]
    public void Missing_commander_cell_falls_back_to_global()
    {
        var r = ManabaseBaselineWeighting.Compute(
            commanderLands: null, commanderRamp: null, commanderDraw: null, commanderDeckCount: 0,
            globalLands: 35.5, globalRamp: 9, globalDraw: 8);

        Assert.Equal(35.5, r.Lands.Value);
        Assert.Equal(ManabaseBaselineSource.Global, r.Lands.Source);
        Assert.Equal(44.5, r.TotalSources, 3);     // 35.5 + 9
    }

    [Fact]
    public void Missing_both_yields_none()
    {
        var r = ManabaseBaselineWeighting.Compute(
            commanderLands: null, commanderRamp: null, commanderDraw: null, commanderDeckCount: 0,
            globalLands: null, globalRamp: null, globalDraw: null);

        Assert.Null(r.Lands.Value);
        Assert.Equal(ManabaseBaselineSource.None, r.Lands.Source);
        Assert.Null(r.TotalSources);               // can't sum with a null
    }

    [Fact]
    public void Metrics_are_independent_missing_draw_uses_global_draw()
    {
        // Solid sample, but this commander cell has no draw figure -> draw falls to global.
        var r = ManabaseBaselineWeighting.Compute(
            commanderLands: 34, commanderRamp: 10, commanderDraw: null, commanderDeckCount: 500,
            globalLands: 35.5, globalRamp: 9, globalDraw: 8);

        Assert.Equal(34, r.Lands.Value);
        Assert.Equal(ManabaseBaselineSource.Commander, r.Lands.Source);
        Assert.Equal(8, r.Draw.Value);
        Assert.Equal(ManabaseBaselineSource.Global, r.Draw.Source);
    }

    [Fact]
    public void Mid_sample_without_global_yields_none()
    {
        // Mid band but no global to blend against: we cannot express confidence, so omit the metric
        // rather than upgrade a weak sample to full trust. (Degenerate — global is normally always present.)
        var r = ManabaseBaselineWeighting.Compute(
            commanderLands: 30, commanderRamp: 12, commanderDraw: 10, commanderDeckCount: 250,
            globalLands: null, globalRamp: null, globalDraw: null);

        Assert.Null(r.Lands.Value);
        Assert.Equal(ManabaseBaselineSource.None, r.Lands.Source);
        Assert.Null(r.Ramp.Value);
        Assert.Equal(ManabaseBaselineSource.None, r.Ramp.Source);
        Assert.Null(r.TotalSources);
    }

    [Fact]
    public void At_low_threshold_weight_is_zero_so_value_equals_global_but_source_is_blended()
    {
        // deckCount == LOW (100): NOT below LOW, so it enters the blend with w = 0 -> value == global,
        // but the source is Blended (it went through the blend path, not the pure-global path).
        var r = ManabaseBaselineWeighting.Compute(
            commanderLands: 30, commanderRamp: 12, commanderDraw: 10, commanderDeckCount: 100,
            globalLands: 36, globalRamp: 8, globalDraw: 6);

        Assert.Equal(36, r.Lands.Value, 3);
        Assert.Equal(ManabaseBaselineSource.Blended, r.Lands.Source);
        Assert.Equal(8, r.Ramp.Value, 3);
        Assert.Equal(ManabaseBaselineSource.Blended, r.Ramp.Source);
    }

    [Fact]
    public void At_high_threshold_uses_commander()
    {
        // deckCount == HIGH (400): trusted fully.
        var r = ManabaseBaselineWeighting.Compute(
            commanderLands: 30, commanderRamp: 12, commanderDraw: 10, commanderDeckCount: 400,
            globalLands: 36, globalRamp: 8, globalDraw: 6);

        Assert.Equal(30, r.Lands.Value, 3);
        Assert.Equal(ManabaseBaselineSource.Commander, r.Lands.Source);
        Assert.Equal(12, r.Ramp.Value, 3);
        Assert.Equal(ManabaseBaselineSource.Commander, r.Ramp.Source);
        Assert.Equal(10, r.Draw.Value, 3);
        Assert.Equal(ManabaseBaselineSource.Commander, r.Draw.Source);
    }

    [Fact]
    public void TotalSources_is_null_when_a_component_is_null()
    {
        // Solid sample, lands present but ramp missing -> ramp falls to global; if global ramp is also
        // null, ramp is None/null and TotalSources cannot be summed.
        var r = ManabaseBaselineWeighting.Compute(
            commanderLands: 34, commanderRamp: null, commanderDraw: 9, commanderDeckCount: 500,
            globalLands: 35.5, globalRamp: null, globalDraw: 8);

        Assert.Equal(34, r.Lands.Value);
        Assert.Null(r.Ramp.Value);
        Assert.Equal(ManabaseBaselineSource.None, r.Ramp.Source);
        Assert.Null(r.TotalSources);
    }
}
```

- [ ] **Step 2: Create a compiling stub so the tests build and fail on assertions**

Create `DeckFlow.Core/Manabase/ManabaseBaselineWeighting.cs` with the full type shapes but a not-yet-correct body (returns all-null) so the file compiles and the tests fail on values, not on missing symbols:

```csharp
namespace DeckFlow.Core.Manabase;

/// <summary>Where a weighted baseline metric came from.</summary>
public enum ManabaseBaselineSource
{
    /// <summary>The commander's own cell (sample was solid).</summary>
    Commander,

    /// <summary>A linear blend of the commander cell and the global bracket baseline.</summary>
    Blended,

    /// <summary>The global-per-bracket baseline (commander cell thin or missing).</summary>
    Global,

    /// <summary>No data available for this metric.</summary>
    None,
}

/// <summary>One weighted baseline metric (lands, ramp, or draw) and where its value came from.</summary>
public sealed record ManabaseBaselineMetric(double? Value, ManabaseBaselineSource Source);

/// <summary>Confidence-weighted per-bracket baseline for a commander's mana base.</summary>
public sealed record ManabaseBaselineResult(
    ManabaseBaselineMetric Lands,
    ManabaseBaselineMetric Ramp,
    ManabaseBaselineMetric Draw,
    double? TotalSources,
    int CommanderDeckCount);

/// <summary>
/// Turns a commander's per-bracket average lands/ramp/draw (with its sample size) plus a
/// global-per-bracket fallback into a confidence-weighted baseline. Pure: no I/O.
/// </summary>
public static class ManabaseBaselineWeighting
{
    /// <summary>Below this deck count the commander cell is ignored in favor of the global baseline.</summary>
    public const int LowDeckThreshold = 100;

    /// <summary>At or above this deck count the commander cell is trusted fully.</summary>
    public const int HighDeckThreshold = 400;

    /// <summary>
    /// Compute the weighted baseline for all three metrics. A negative <paramref name="commanderDeckCount"/>
    /// is treated as a thin sample (falls back to the global baseline). Metric averages are assumed
    /// non-negative (guaranteed by the upstream corpus aggregation) and are not validated here.
    /// </summary>
    public static ManabaseBaselineResult Compute(
        double? commanderLands, double? commanderRamp, double? commanderDraw, int commanderDeckCount,
        double? globalLands, double? globalRamp, double? globalDraw)
    {
        var lands = new ManabaseBaselineMetric(null, ManabaseBaselineSource.None);
        var ramp = new ManabaseBaselineMetric(null, ManabaseBaselineSource.None);
        var draw = new ManabaseBaselineMetric(null, ManabaseBaselineSource.None);
        return new ManabaseBaselineResult(lands, ramp, draw, null, commanderDeckCount);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail (on values, not compile)**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "ManabaseBaselineWeightingTests"`
Expected: FAIL — build succeeds, assertions fail (stub returns nulls).

- [ ] **Step 4: Commit the failing state**

```bash
git add DeckFlow.Core/Manabase/ManabaseBaselineWeighting.cs DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs
git commit -m "test(manabase): failing tests + type stubs for baseline weighting"
```

---

## Task 2: Implement the weighting

**Files:**
- Modify: `DeckFlow.Core/Manabase/ManabaseBaselineWeighting.cs`

- [ ] **Step 1: Replace `Compute` + add the per-metric weigher**

In `ManabaseBaselineWeighting`, replace the `Compute` body and add a private `WeighMetric`:

```csharp
    /// <summary>
    /// Compute the weighted baseline for all three metrics. A negative <paramref name="commanderDeckCount"/>
    /// is treated as a thin sample (falls back to the global baseline). Metric averages are assumed
    /// non-negative (guaranteed by the upstream corpus aggregation) and are not validated here.
    /// </summary>
    public static ManabaseBaselineResult Compute(
        double? commanderLands, double? commanderRamp, double? commanderDraw, int commanderDeckCount,
        double? globalLands, double? globalRamp, double? globalDraw)
    {
        ManabaseBaselineMetric lands = WeighMetric(commanderLands, commanderDeckCount, globalLands);
        ManabaseBaselineMetric ramp = WeighMetric(commanderRamp, commanderDeckCount, globalRamp);
        ManabaseBaselineMetric draw = WeighMetric(commanderDraw, commanderDeckCount, globalDraw);

        double? totalSources = lands.Value is double l && ramp.Value is double r ? l + r : null;

        return new ManabaseBaselineResult(lands, ramp, draw, totalSources, commanderDeckCount);
    }

    private static ManabaseBaselineMetric WeighMetric(double? commanderAvg, int deckCount, double? globalAvg)
    {
        // Commander cell missing or too thin -> lean on the global baseline (or nothing).
        if (commanderAvg is not double commander || deckCount < LowDeckThreshold)
        {
            return globalAvg is double g
                ? new ManabaseBaselineMetric(g, ManabaseBaselineSource.Global)
                : new ManabaseBaselineMetric(null, ManabaseBaselineSource.None);
        }

        // Solid sample -> trust the commander cell.
        if (deckCount >= HighDeckThreshold)
        {
            return new ManabaseBaselineMetric(commander, ManabaseBaselineSource.Commander);
        }

        // Mid band -> blend toward the global baseline. Without a global we cannot express confidence,
        // so omit rather than upgrade a weak sample to full trust. (Degenerate: global is normally present.)
        if (globalAvg is not double global)
        {
            return new ManabaseBaselineMetric(null, ManabaseBaselineSource.None);
        }

        double w = (double)(deckCount - LowDeckThreshold) / (HighDeckThreshold - LowDeckThreshold);
        double blended = (w * commander) + ((1.0 - w) * global);
        return new ManabaseBaselineMetric(blended, ManabaseBaselineSource.Blended);
    }
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "ManabaseBaselineWeightingTests"`
Expected: PASS (10 tests).

- [ ] **Step 3: Full Core build + suite (no regressions)**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj` then `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`
Expected: Build 0/0; full Core suite green (this is additive — nothing else references the new type yet).

- [ ] **Step 4: Commit**

```bash
git add DeckFlow.Core/Manabase/ManabaseBaselineWeighting.cs
git commit -m "feat(manabase): implement confidence-weighted baseline (lands/ramp/draw)"
```

---

## Task 3: Review for simplification

**Files:** possibly `DeckFlow.Core/Manabase/ManabaseBaselineWeighting.cs`

- [ ] **Step 1: Review the diff for simplification.** Look for reduction (e.g. collapsing repeated branch shapes in `WeighMetric`) without losing per-metric independence or any of the four sources. Apply only if it improves clarity. (If your harness has a `/simplify` command, run it; otherwise do the review by hand.)
- [ ] **Step 2: Re-run** `--filter "ManabaseBaselineWeightingTests"` → PASS.
- [ ] **Step 3: Commit if anything changed**

```bash
git add -A && git commit -m "chore(manabase): simplify baseline weighting" || echo "nothing to simplify"
```

---

## Self-Review notes (author)

- **Spec coverage:** implements spec Component 3 (`ManabaseBaselineWeighting`, per-metric blend by sample confidence, TotalSources = lands+ramp). Thresholds `LowDeckThreshold=100`/`HighDeckThreshold=400` are the spec's tunable consts. Storage (Phase 1), aggregation job (Phase 3), analyzer/UI (Phases 4/5) are out of scope here — the helper takes plain nullable doubles so it has zero dependency on the not-yet-built storage schema.
- **Edge cases covered by tests:** solid / thin / mid-blend / missing-commander / missing-both / per-metric independence (null draw) / blend-without-global.
- **Type consistency:** `Compute(double?, double?, double?, int, double?, double?, double?)` → `ManabaseBaselineResult(Lands, Ramp, Draw, TotalSources, CommanderDeckCount)` with `ManabaseBaselineMetric(double? Value, ManabaseBaselineSource Source)` and enum `{Commander, Blended, Global, None}`.
- **Result-level source (downstream):** intentionally per-metric only. The analyzer/UI (Phases 4/5) derive any aggregate "source" label from the three metric sources (e.g. worst-of, or a per-metric badge) — no result-level source field is added here.
- **Input assumptions (documented):** a negative `commanderDeckCount` is treated as thin (`< LowDeckThreshold` → global/none) — acceptable, no throw. Metric averages are assumed non-negative (the corpus aggregation guarantees it); the helper does not validate them. State both in the XML doc on `Compute`.
- **Codex plan-review (gpt-5.5) folded:** HIGH — mid-band + missing global now returns `None` (was Commander); MEDIUM — added boundary tests (count==LOW → global-value/Blended-source, count==HIGH → Commander) and a TotalSources-null test; LOW — ramp/draw source asserts added to solid/thin/blended tests, negative-input behavior documented, `/simplify` step softened to "review for simplification".
- **Constraints:** pure, no new deps, LF, additive (no existing type touched). Test namespace `DeckFlow.Core.Tests`, xUnit via global using.
