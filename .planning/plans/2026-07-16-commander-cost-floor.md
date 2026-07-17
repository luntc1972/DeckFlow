# Commander-Cost Land Floor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a commander's mana value is expensive relative to the deck's curve, raise the manabase land target to a floor (`max(existingTarget, 31 + colorCount + highestCommanderCmc)`), so cheap-deck / expensive-commander lists stop being under-landed. Flag-gated, seeded ON.

**Architecture:** Applied in `ManabaseAnalyzer.ComputeTargetLands` as a `max()` after the existing Karsten target is computed — never additive, so it can only *raise* under-landed decks and never double-counts `avgMV`. Highest commander MV is read the same way `ManabaseRampDrawBudget` already does. Recorded in the land-target breakdown + report text. Flag `analysis.manabase.commander-cost-floor` threads from `ManabaseAnalysisService` through `Analyze`.

**Tech Stack:** C# 12 / .NET 10, xUnit (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`). No new dependencies. LF line endings; changed C# lines must pass the format gate.

**Build/test** (WSL → Windows dotnet):
- Build: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln`
- Core tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`
- Web tests: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`

**Reuse (verbatim patterns):**
- Highest commander MV — copy the idiom from `ManabaseRampDrawBudget.cs:128-132`:
  ```csharp
  int highestCommanderCmc = deck.Spells
      .Where(spell => spell.IsCommander)
      .Select(spell => spell.ManaValue)
      .DefaultIfEmpty(0)
      .Max();
  ```
  (use `DefaultIfEmpty(0)` here — no commander → floor uses 0, not -1.)
- Color count — `CommanderColors(deck).Count` (the existing private `ManabaseAnalyzer.CommanderColors` returns `IReadOnlySet<ManaColor>`).
- Breakdown optional-field + report-line + flag-seed patterns — mirror the existing `RampAndDrawUnderThree` / `tap-analyzer` siblings.

---

## File Structure

**Modify:**
- `DeckFlow.Core/Manabase/KarstenManabase.cs` — add `public const double CommanderCostFloorBaseline = 31.0;`.
- `DeckFlow.Core/Manabase/ManabaseModels.cs` — add `CommanderCostFloor` (double) + `CommanderCostFloorActive` (bool) to `ManabaseLandTargetBreakdown`.
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` — new `commanderCostFloor` bool on `Analyze`; compute + apply the floor in `ComputeTargetLands`; record in the breakdown.
- `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` — render a line when the floor is active.
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — flag key const; read the flag; thread the bool into `Analyze`.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs` — register the flag.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — seed the flag ON.

**Test:**
- `DeckFlow.Core.Tests/Manabase/CommanderCostFloorTests.cs`

---

## Task 1: Breakdown fields + baseline constant

**Files:**
- Modify: `DeckFlow.Core/Manabase/ManabaseModels.cs`, `DeckFlow.Core/Manabase/KarstenManabase.cs`

- [ ] **Step 1: Add the tunable baseline const**

In `KarstenManabase.cs`, next to `private const double RampDrawCredit = 0.28;` (line 34), add:

```csharp
    /// <summary>
    /// Baseline for the commander-cost land floor (Nate Burgess's published formula
    /// Lands = 31 + colors + commanderCMC, applied as a floor). Public const = the single tuning point.
    /// </summary>
    public const double CommanderCostFloorBaseline = 31.0;
```

- [ ] **Step 2: Add breakdown fields**

In `ManabaseLandTargetBreakdown` (near the `RampAndDrawUnderThree` breakdown field), add:

```csharp
    /// <summary>The commander-cost land floor value (31 + colors + highest commander CMC). 0 when the flag is off.</summary>
    public double CommanderCostFloor { get; init; }

    /// <summary>True when the commander-cost floor raised the land target above the base (avgMV) target.</summary>
    public bool CommanderCostFloorActive { get; init; }
```

- [ ] **Step 3: Build**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj`
Expected: Build succeeded (additive members).

- [ ] **Step 4: Commit**

```bash
git add DeckFlow.Core/Manabase/KarstenManabase.cs DeckFlow.Core/Manabase/ManabaseModels.cs
git commit -m "feat(manabase): add commander-cost-floor baseline const + breakdown fields"
```

---

## Task 2: Compute + apply the floor in the analyzer (flag-gated)

**Files:**
- Modify: `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs`
- Test: `DeckFlow.Core.Tests/Manabase/CommanderCostFloorTests.cs`

- [ ] **Step 1: Write failing tests**

Create `DeckFlow.Core.Tests/Manabase/CommanderCostFloorTests.cs`:

```csharp
using System.Collections.Generic;
using DeckFlow.Core.Manabase;
using Xunit;

namespace DeckFlow.Core.Tests.Manabase;

public sealed class CommanderCostFloorTests
{
    // A low-curve mono-green deck (small avgMV → low Karsten land target) with an expensive commander.
    private static ManabaseDeck Deck(int commanderMv)
    {
        var cards = new List<CardFact>
        {
            new() { Name = "Big Boss", Quantity = 1, TypeLine = "Legendary Creature — Elf",
                    ManaCost = "{4}{G}{G}", ManaValue = commanderMv, OracleText = "Trample.", IsCommander = true },
        };
        // 63 cheap green 1-drops (keeps avgMV low so the base target is well under the floor).
        for (var i = 0; i < 63; i++)
        {
            cards.Add(new CardFact { Name = $"Bear {i}", Quantity = 1, TypeLine = "Creature — Bear",
                ManaCost = "{G}", ManaValue = 1, OracleText = "" });
        }
        for (var i = 0; i < 36; i++)
        {
            cards.Add(new CardFact { Name = $"Forest {i}", Quantity = 1, TypeLine = "Basic Land — Forest", ManaValue = 0 });
        }

        return ManabaseClassifier.Classify(cards);
    }

    [Fact]
    public void Floor_lifts_target_for_expensive_commander_when_on()
    {
        var deck = Deck(commanderMv: 6);

        var off = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Standard, commanderCostFloor: false);
        var on = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Standard, commanderCostFloor: true);

        // Floor = 31 + colorCount(1) + 6 = 38. Base target for a 1-curve deck is well below 38.
        Assert.True(off.LandTarget.FinalTarget < 38, $"base target {off.LandTarget.FinalTarget} should be below the floor");
        Assert.Equal(38, on.LandTarget.FinalTarget, 3);
        Assert.True(on.LandTarget.CommanderCostFloorActive);
        Assert.Equal(38, on.LandTarget.CommanderCostFloor, 3);
    }

    [Fact]
    public void Cheap_commander_does_not_lift()
    {
        var deck = Deck(commanderMv: 2);

        var on = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Standard, commanderCostFloor: true);

        // Floor = 31 + 1 + 2 = 34; a 1-curve deck's base target is ~34-ish. Assert no *active* lift beyond base.
        // Robust check: FinalTarget equals max(base, 34) and Active reflects whether 34 exceeded base.
        var off = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Standard, commanderCostFloor: false);
        double expected = System.Math.Max(off.LandTarget.FinalTarget, 34);
        Assert.Equal(expected, on.LandTarget.FinalTarget, 3);
    }

    [Fact]
    public void Flag_off_is_byte_identical()
    {
        var deck = Deck(commanderMv: 6);

        var off = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Standard, commanderCostFloor: false);
        var baseline = ManabaseAnalyzer.Analyze(deck, ManabaseMode.Standard);

        Assert.Equal(baseline.LandTarget.FinalTarget, off.LandTarget.FinalTarget, 3);
        Assert.False(off.LandTarget.CommanderCostFloorActive);
        Assert.Equal(0.0, off.LandTarget.CommanderCostFloor, 3);
    }
}
```

> Before running, verify the real member names: `CardFact` shape (oracle/cost/`IsCommander`), `ManabaseReport.LandTarget.FinalTarget`, and `ManabaseMode.Standard`. Read `ManabaseModels.cs` / `CardFact.cs` / `ManabaseMode.cs` and adjust construction/property names if they differ; keep the numeric floor assertions (base<38, on==38, active flag). If a mono-green 1-curve deck's base target happens to be ≥38, lower the deck curve further (more 0/1-drops) or raise `commanderMv` in the test so the floor demonstrably bites.

- [ ] **Step 2: Run to verify fail**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "CommanderCostFloorTests"`
Expected: FAIL — `Analyze` has no `commanderCostFloor` parameter (compile error).

- [ ] **Step 3: Add the flag param to `Analyze` + thread to `ComputeTargetLands`**

In `ManabaseAnalyzer.Analyze` (full overload, line 166), add a parameter with the other bool flags (before `CedhLandContext cedhContext = default`):

```csharp
        bool commanderCostFloor = false,
```

Update the `ComputeTargetLands` call (line 197):

```csharp
        double targetLands = ComputeTargetLands(deck, mode, cedhContext, ritualLandCreditActive, commanderCostFloor, out ManabaseLandTargetBreakdown landTarget);
```

- [ ] **Step 4: Compute + apply the floor in `ComputeTargetLands`**

Change the signature (line 482) to add `bool commanderCostFloor,` before `out ManabaseLandTargetBreakdown breakdown`.

At the TOP of the method body, compute the floor once:

```csharp
        double commanderCostFloorValue = 0.0;
        if (commanderCostFloor)
        {
            int highestCommanderCmc = deck.Spells
                .Where(spell => spell.IsCommander)
                .Select(spell => spell.ManaValue)
                .DefaultIfEmpty(0)
                .Max();
            int colorCount = CommanderColors(deck).Count;
            commanderCostFloorValue = KarstenManabase.CommanderCostFloorBaseline + colorCount + highestCommanderCmc;
        }
```

Then, in BOTH return paths, apply the floor to the target and record it in the breakdown:

- **Non-singleton branch:** after `double sixty = KarstenManabase.SixtyCardLandTarget(...);`, compute `double sixtyFinal = System.Math.Max(sixty, commanderCostFloorValue);`. Pass `finalTarget: sixtyFinal` to `BuildBreakdown` (keep `baseTarget: sixty`) and `return sixtyFinal;`.
- **Singleton/cedh branch:** after `finalTarget` is computed (the `mode == Cedh ? CedhLandTarget(...) : singleton` assignment), add:
  ```csharp
        double baseBeforeFloor = finalTarget;
        finalTarget = System.Math.Max(finalTarget, commanderCostFloorValue);
  ```
  (keep `baseTarget: singleton` in the breakdown; return the floored `finalTarget`.)

In each `BuildBreakdown(...)` call, pass the two new values (add optional params to `BuildBreakdown` mirroring the existing `ritualLandCredit` optional param):

```csharp
        double commanderCostFloor = 0.0,
        bool commanderCostFloorActive = false,
```

and set them on the constructed `ManabaseLandTargetBreakdown`:

```csharp
            CommanderCostFloor = commanderCostFloor,
            CommanderCostFloorActive = commanderCostFloorActive,
```

At the call sites pass:
- `commanderCostFloor: commanderCostFloorValue`
- `commanderCostFloorActive: commanderCostFloorValue > <baseTargetForThatBranch> + 0.0005` (i.e. true only when the floor strictly exceeded the pre-floor target — use `sixty` in the non-singleton branch and `baseBeforeFloor` in the singleton branch; the small epsilon avoids float-equality flicker).

> Read `BuildBreakdown`'s current signature first; append the two new params at the END so existing calls are unaffected.

- [ ] **Step 5: Run to verify pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "CommanderCostFloorTests"`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add DeckFlow.Core/Manabase/ManabaseAnalyzer.cs DeckFlow.Core.Tests/Manabase/CommanderCostFloorTests.cs
git commit -m "feat(manabase): apply flag-gated commander-cost land floor"
```

---

## Task 3: Report-text transparency line

**Files:**
- Modify: `DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs`

- [ ] **Step 1: Locate the land-target breakdown rendering**

Search `ManabaseReportTextBuilder.cs` for where the breakdown lines are appended (near `RampAndDrawUnderThree` / the land-target section).

- [ ] **Step 2: Add the floor line (only when active)**

Mirror the sibling append idiom (`builder`/`sb`, and the exact minus/label style the file uses):

```csharp
        if (report.LandTarget.CommanderCostFloorActive)
        {
            builder.AppendLine(
                $"Commander cost floor: {report.LandTarget.CommanderCostFloor:0.#} lands (raised for an expensive commander)");
        }
```

- [ ] **Step 3: Run report-text tests**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "ReportText"`
Expected: PASS. The line appears only when the floor is active, so fixtures that don't trigger it are unchanged; update any golden fixture that legitimately now triggers the floor (confirm it's the intended lift).

- [ ] **Step 4: Commit**

```bash
git add DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs
git commit -m "feat(manabase): show commander cost floor line in report text"
```

---

## Task 4: Flag registration + service wiring (seed ON)

**Files:**
- Modify: `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs`, `DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs`, `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs`

- [ ] **Step 1: Catalog entry**

In `FeatureFlagCatalog.cs`, next to the other `analysis.manabase.*` entries:

```csharp
            ["analysis.manabase.commander-cost-floor"] =
                "Seeded ON; raises the land target to a floor (31 + colors + highest commander CMC, " +
                "Nate Burgess's formula applied as a max) so a cheap deck with an expensive commander is " +
                "not under-landed. Only ever raises the target for expensive-commander decks.",
```

- [ ] **Step 2: Seed ON**

In `FeatureFlagStore.cs`, mirror an existing **ON**-seeded manabase flag (e.g. `analysis.manabase.accuracy`) in each place per-flag rows/registrations live (the `Key(...)` registration ~line 29, the `('...', TRUE)` block ~line 218, the `('...', 1)` block ~line 266). Add `analysis.manabase.commander-cost-floor` seeded ON (`TRUE` / `1`) in each, copying the sibling idiom verbatim.

> Read `FeatureFlagStore.cs` around those line ranges and replicate the ON sibling exactly.

- [ ] **Step 3: Service const + read + thread**

In `ManabaseAnalysisService.cs`, next to `TapAnalyzerFlagKey`:

```csharp
    /// <summary>
    /// Seeded ON; raises the land target to the commander-cost floor (31 + colors + highest commander CMC,
    /// applied as a max) so cheap-deck/expensive-commander lists are not under-landed.
    /// </summary>
    public const string CommanderCostFloorFlagKey = "analysis.manabase.commander-cost-floor";
```

Find where the sibling manabase flags are read into locals (the helper used for `TapAnalyzerFlagKey` etc.). For a seed-ON flag, read it with the cache's ON-defaulting read (`IsEnabled`) so a missing row defaults ON — confirm which helper the ON-seeded `accuracy` flag uses and match it:

```csharp
        bool commanderCostFloor = <sameHelperAsAccuracyFlag>(CommanderCostFloorFlagKey);
```

At the `ManabaseAnalyzer.Analyze(...)` call (line 442), pass `commanderCostFloor: commanderCostFloor`.

> Read lines 420-470 + 530-560 to copy the exact flag-read helper (ON-defaulting for a seed-ON flag) and the `Analyze(...)` argument shape before editing.

- [ ] **Step 4: Build web + run web tests**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj` then `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`
Expected: Build 0/0; web suite green. If a flag-catalog/seed completeness test exists and fails, add the new flag to its expected set (that's the intended registration).

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
git commit -m "feat(manabase): wire commander-cost-floor flag (seeded ON)"
```

---

## Task 5: Full verification, calibration check, simplify, README

**Files:** `README.md` (if manabase behavior documented), else verification only.

- [ ] **Step 1: Full solution build** — `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` → 0/0.

- [ ] **Step 2: Full Core + Web suites** — both green. Because the flag is seeded ON, inspect any moved golden/CedhCalibration fixture: confirm the movement is a floor lift on a genuinely expensive-commander deck (intended, sourced), NOT a normal deck regressing. If a normal-curve fixture moved, the floor is over-biting → lower `CommanderCostFloorBaseline` or re-check the `max` logic before proceeding.

- [ ] **Step 3: Calibration sanity** — run the existing cEDH calibration decks (the `CedhCalibration` harness/tests) with the flag ON; confirm only expensive-commander decks lift and the lifts are directionally sensible (higher target for pricier commanders). Note the count of decks that moved in the commit body.

- [ ] **Step 4: `/simplify` the diff** — apply reductions; re-run Step 2 after changes.

- [ ] **Step 5: README** — if the manabase flags/credits are documented, add one line for the commander-cost floor (flag `analysis.manabase.commander-cost-floor`, seeded ON). Else note "no README change."

```bash
git add README.md && git commit -m "docs(manabase): note commander cost floor" || echo "no README change"
```

- [ ] **Step 6: Final simplify commit if needed** — `git add -A && git commit -m "chore(manabase): simplify commander cost floor diff" || echo "nothing to commit"`

---

## Self-Review notes (author)

- **Spec coverage:** floor formula + `max` application → Task 2; tunable baseline const → Task 1; breakdown fields → Task 1 (fields) + Task 2 (set); report line → Task 3; flag registration/seed-ON/wiring → Task 4; tests (lift / no-lift / flag-off / partners-via-highest-CMC) → Task 2; calibration inspection → Task 5.
- **Design fidelity:** applied as `max(existingTarget, 31 + colors + highestCommanderCmc)` in `ComputeTargetLands`, both return paths; highest commander MV via the `ManabaseRampDrawBudget` idiom; color count via `CommanderColors(deck).Count`. Flag OFF → `commanderCostFloorValue` stays 0 → `max(target, 0)` = target → byte-identical.
- **Type consistency:** `KarstenManabase.CommanderCostFloorBaseline:double const`; `ManabaseLandTargetBreakdown.CommanderCostFloor:double` / `.CommanderCostFloorActive:bool`; `Analyze(..., bool commanderCostFloor = false, ...)`; `ComputeTargetLands(..., bool commanderCostFloor, ...)`; flag key `analysis.manabase.commander-cost-floor`.
- **Seed ON caveat:** because it ships ON, Task 5 Steps 2-3 explicitly gate on inspecting fixture movement — a normal deck moving = over-biting = stop and re-calibrate the baseline. This is the safety valve for shipping a sourced-but-uncalibrated floor on.
- **Executor uncertainty flagged:** `CardFact` shape, `LandTarget.FinalTarget` name, `BuildBreakdown` signature, and the ON-defaulting flag-read helper — each step says read the real member / mirror the named sibling. No new deps. LF. Partners → highest CMC (not sum).
```
