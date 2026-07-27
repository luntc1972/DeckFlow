# cEDH Baseline Drift Guard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the monthly cEDH land baseline pipeline fail closed on corrupt data instead of silently writing it.

**Architecture:** Two independent gates. Gate 1 fails `fetch.py` if any card name went unresolved. Gate 2 is a pure `CedhBaselineDriftCheck` in `DeckFlow.Core` that compares a candidate snapshot against the committed one and is evaluated by the CLI *before any file is written*, so a bad refresh cannot clobber the last-known-good artifacts.

**Tech Stack:** Python 3 (stdlib only), C# 12 / .NET 10, xUnit, System.Text.Json.

**Spec:** `.planning/captures/cedh-baseline-drift-guard-design.md` (commit `cdfc33fd`)

## Global Constraints

- **Codex writes all code; Claude plans and reviews.** Dispatch per `CLAUDE.md` delegation rules (`gpt-5.4`, `model_reasoning_effort=medium`, `-s danger-full-access`).
- **Line endings:** preserve each touched file's existing endings exactly. Detect per file — some files are LF, some CRLF, some mixed. Do NOT convert, normalize, or assume a repo-wide style. Every file in this plan is currently **LF**.
- **No new dependencies.** Python is stdlib-only. No new NuGet packages.
- **Never convert `{ get; init; }` to `{ get; }`** — System.Text.Json silently skips get-only properties in .NET 9+ and has broken deserialization in this repo before.
- **Do not reformat surrounding code.** The repo runs a changed-lines-only format gate (`scripts/format-check-changed.sh`).
- **No Python test framework.** The repo has none; do not introduce one. Gate 1 is verified by targeted manual run.
- **Thresholds have no code-side defaults.** All six properties are `required` so a missing field throws rather than silently falling back — a typo must not disable the gate.
- **`dotnet` on this machine is `"/mnt/c/Program Files/dotnet/dotnet.exe"`.** Do not set `MTG_DATA_DIR`.

---

## File Structure

| File | Responsibility |
|---|---|
| `scripts/cedh-baseline/fetch.py` (modify) | Gate 1: fail on any unresolved card name |
| `DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs` (create) | Gate 2: pure drift evaluation + threshold/finding/verdict records |
| `scripts/cedh-baseline/drift-thresholds.json` (create) | Committed threshold config |
| `DeckFlow.CLI/CedhBaselineCommandRunner.cs` (modify) | Load previous snapshot + thresholds, evaluate before writing |
| `DeckFlow.CLI/Program.cs` (modify) | Add `--thresholds` option |
| `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftCheckTests.cs` (create) | Per-rule unit tests + boundaries |
| `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftRegressionTests.cs` (create) | 2026-07 incident regression |
| `DeckFlow.Core.Tests/Fixtures/cedh-drift/*.json` (create) | Incident fixtures |
| `DeckFlow.Web.Tests/Manabase/CedhLandBaselineProviderTests.cs` (modify) | Re-scope churn-prone `n` pins |
| `scripts/cedh-baseline/README.md` (modify) | Document both gates |

---

## Task 1: Gate 1 — fail `fetch.py` on unresolved card names

**Files:**
- Modify: `scripts/cedh-baseline/fetch.py:82-102` (`main`)

**Interfaces:**
- Consumes: existing `distinct_card_names(decks)`, `load_card_cache(cards_path)`, `resolve_missing_cards(...)`
- Produces: `main()` returns `1` when any name is unresolved; `0` otherwise

- [ ] **Step 1: Add the unresolved gate to `main()`**

In `scripts/cedh-baseline/fetch.py`, replace the tail of `main()` (currently the `resolve_missing_cards(...)` call, the `print`, and `return 0`) with:

```python
    resolve_missing_cards(missing_names, card_cache, cards_path)
    print(f"Wrote {cards_path} with {len(card_cache)} cached cards.")

    # Gate: the baseline builder counts lands off resolved cards, so an unresolved name is a
    # silently dropped card, not a cosmetic warning. Modal-DFC lands resolving as "missing" once
    # under-counted ~1.9 lands/deck across the whole 2026-07 snapshot. Fail closed instead.
    unresolved = sorted(name for name in all_names if name not in card_cache)
    if unresolved:
        print(
            f"ERROR: {len(unresolved)} of {len(all_names)} card names did not resolve. "
            f"The baseline would under-count these cards, so refusing to continue.",
            file=sys.stderr,
        )
        for name in unresolved[:20]:
            print(f"  unresolved: {name}", file=sys.stderr)
        if len(unresolved) > 20:
            print(f"  ... and {len(unresolved) - 20} more.", file=sys.stderr)
        return 1

    return 0
```

The card cache is written *before* this check so the expensive Scryfall work is preserved and the run stays resumable.

- [ ] **Step 2: Verify both gate paths with a stubbed harness**

Do NOT try to verify by editing `_calib` and re-running the script. `main()` calls
`fetch_all_decks` first and overwrites `decks_all.json` from the network, so a bogus card injected
into that file is erased before the gate sees it, and deleting entries from `cards_full.json` just
causes them to be re-resolved. Either route also costs ~5 minutes of live traffic per attempt.

Stub the network instead — this exercises the gate directly and makes two Scryfall calls total:

```bash
rm -rf /tmp/gate-pass /tmp/gate-fail
python3 - <<'PY'
import importlib.util, sys

def load():
    spec = importlib.util.spec_from_file_location("f", "scripts/cedh-baseline/fetch.py")
    m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m); return m

m = load()
m.fetch_all_decks = lambda a, b: [{"commanders": ["Kinnan, Bonder Prodigy"], "maindeck": ["Sol Ring", "Sink into Stupor // Soporific Springs"]}]
sys.argv = ["fetch.py", "--outdir", "/tmp/gate-pass"]
print(f"PASS-CASE exit={m.main()}  (expected 0)")

m2 = load()
m2.fetch_all_decks = lambda a, b: [{"commanders": ["Kinnan, Bonder Prodigy"], "maindeck": ["Sol Ring", "Not A Real Card Xyzzy"]}]
sys.argv = ["fetch.py", "--outdir", "/tmp/gate-fail"]
print(f"FAIL-CASE exit={m2.main()}  (expected 1)")
PY
rm -rf /tmp/gate-pass /tmp/gate-fail scripts/cedh-baseline/__pycache__
```

Expected: `PASS-CASE exit=0` and `FAIL-CASE exit=1`, the latter preceded by
`ERROR: 1 of 3 card names did not resolve...` naming `Not A Real Card Xyzzy`. The pass case
deliberately includes a modal-DFC land, so it also confirms front-face resolution still works.

- [ ] **Step 3: Confirm no artifacts left behind**

```bash
git status --porcelain scripts/cedh-baseline/
```
Expected: only `fetch.py` modified — no `__pycache__`.

- [ ] **Step 5: Commit**

```bash
git add scripts/cedh-baseline/fetch.py
git commit -m "feat(cedh-baseline): fail the fetch when any card name is unresolved

An unresolved name is a silently dropped card, not a cosmetic warning:
the baseline builder counts lands off the resolved cache, so modal-DFC
lands landing in the unresolved bucket under-counted roughly 1.9 lands
per deck across the whole 2026-07 snapshot before anyone noticed.

Fail closed after the cache is persisted, so the expensive Scryfall work
is kept and the run stays resumable."
```

---

## Task 2: Gate 2 scaffolding + Rule 1 (`DroppedEstablishedCommander`)

**Files:**
- Create: `DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs`
- Test: `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftCheckTests.cs`

**Interfaces:**
- Consumes: `CedhLandBaselineSnapshot`, `CedhCommanderBaselineSnapshot` (existing, `DeckFlow.Core/Manabase/CedhLandBaseline.cs`)
- Produces:
  - `CedhDriftThresholds` — record with `required` `int MinEstablishedN`, `int MinPopulousN`, `double MaxSampleDropPct`, `double MoverThresholdLands`, `int MinMoversForDirectionTest`, `double MaxOneSidedPct`
  - `CedhDriftFinding` — record with `required string Rule`, `string? Commander`, `required string Detail`
  - `CedhDriftVerdict` — record with `required bool Passed`, `required IReadOnlyList<CedhDriftFinding> Findings`
  - `CedhBaselineDriftCheck.Evaluate(CedhLandBaselineSnapshot previous, CedhLandBaselineSnapshot candidate, CedhDriftThresholds thresholds) -> CedhDriftVerdict`

- [ ] **Step 1: Write the failing tests**

Create `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftCheckTests.cs`:

```csharp
using DeckFlow.Core.Manabase;
using Xunit;

namespace DeckFlow.Core.Tests.Manabase;

/// <summary>
/// Verifies the drift guard that compares a candidate cEDH baseline snapshot against the
/// committed one. Thresholds are calibrated against the 2026-07-27 corruption incident.
/// </summary>
public sealed class CedhBaselineDriftCheckTests
{
    private static readonly CedhDriftThresholds Thresholds = new()
    {
        MinEstablishedN = 10,
        MinPopulousN = 20,
        MaxSampleDropPct = 40,
        MoverThresholdLands = 0.5,
        MinMoversForDirectionTest = 10,
        MaxOneSidedPct = 90,
    };

    private static CedhLandBaselineSnapshot Snapshot(
        string generated,
        params (string Name, int N, double Mean)[] commanders) =>
        new()
        {
            Generated = generated,
            SampleSize = commanders.Sum(c => c.N),
            OverallMeanLands = commanders.Length == 0 ? 0 : Math.Round(commanders.Average(c => c.Mean), 1),
            Commanders = commanders.ToDictionary(
                c => c.Name,
                c => new CedhCommanderBaselineSnapshot { N = c.N, LandsMean = c.Mean, LandsSd = 1.0 }),
        };

    [Fact]
    public void Evaluate_IdenticalSnapshots_Passes()
    {
        CedhLandBaselineSnapshot snapshot = Snapshot("2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(snapshot, snapshot, Thresholds);

        Assert.True(verdict.Passed);
        Assert.Empty(verdict.Findings);
    }

    [Fact]
    public void Evaluate_EstablishedCommanderDisappears_Fails()
    {
        // "The Cabbage Merchant" sat at n=18 and vanished entirely in the corrupt 2026-07 run.
        CedhLandBaselineSnapshot previous = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8), ("The Cabbage Merchant", 18, 24.9));
        CedhLandBaselineSnapshot candidate = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        CedhDriftFinding finding = Assert.Single(verdict.Findings);
        Assert.Equal("DroppedEstablishedCommander", finding.Rule);
        Assert.Equal("The Cabbage Merchant", finding.Commander);
    }

    [Fact]
    public void Evaluate_ThinCommanderDisappears_Passes()
    {
        // Below MinEstablishedN the sample is too small for absence to mean anything.
        CedhLandBaselineSnapshot previous = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8), ("Yusri, Fortune's Flame", 3, 25.3));
        CedhLandBaselineSnapshot candidate = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_CommanderExactlyAtEstablishedFloorDisappears_Fails()
    {
        CedhLandBaselineSnapshot previous = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8), ("Elsha of the Infinite", 10, 25.2));
        CedhLandBaselineSnapshot candidate = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        Assert.Equal("Elsha of the Infinite", Assert.Single(verdict.Findings).Commander);
    }

    [Fact]
    public void Evaluate_NewCommanderAppears_Passes()
    {
        CedhLandBaselineSnapshot previous = Snapshot("2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8));
        CedhLandBaselineSnapshot candidate = Snapshot(
            "2026-07", ("Kinnan, Bonder Prodigy", 337, 25.8), ("Super-Skrull", 3, 27.0));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~CedhBaselineDriftCheckTests" --nologo`
Expected: build failure — `CedhDriftThresholds`, `CedhDriftVerdict`, `CedhBaselineDriftCheck` do not exist.

- [ ] **Step 3: Create the types and Rule 1**

Create `DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs`:

```csharp
namespace DeckFlow.Core.Manabase;

/// <summary>
/// Limits governing how far a candidate cEDH land baseline may move from the committed one
/// before the refresh is rejected. Every property is required: a missing field must throw
/// rather than silently fall back to a default, because a typo in the config file would
/// otherwise disable the guard entirely.
/// </summary>
public sealed record CedhDriftThresholds
{
    /// <summary>Prior sample size at or above which a commander's disappearance is a failure.</summary>
    public required int MinEstablishedN { get; init; }

    /// <summary>Prior sample size at or above which a commander is checked for sample collapse.</summary>
    public required int MinPopulousN { get; init; }

    /// <summary>Maximum tolerated percentage drop in a populous commander's sample size.</summary>
    public required double MaxSampleDropPct { get; init; }

    /// <summary>Minimum absolute change in mean lands for a commander to count as a mover.</summary>
    public required double MoverThresholdLands { get; init; }

    /// <summary>Minimum number of movers before the one-sidedness test is meaningful.</summary>
    public required int MinMoversForDirectionTest { get; init; }

    /// <summary>Maximum tolerated percentage of movers travelling in the same direction.</summary>
    public required double MaxOneSidedPct { get; init; }
}

/// <summary>One reason a candidate baseline was rejected.</summary>
public sealed record CedhDriftFinding
{
    /// <summary>Name of the rule that fired.</summary>
    public required string Rule { get; init; }

    /// <summary>Commander the finding concerns, when the rule is per-commander.</summary>
    public string? Commander { get; init; }

    /// <summary>Human-readable explanation including the observed value and the limit breached.</summary>
    public required string Detail { get; init; }
}

/// <summary>Outcome of comparing a candidate baseline against the committed one.</summary>
public sealed record CedhDriftVerdict
{
    /// <summary>True when no rule fired.</summary>
    public required bool Passed { get; init; }

    /// <summary>Every rule breach found, in rule order.</summary>
    public required IReadOnlyList<CedhDriftFinding> Findings { get; init; }
}

/// <summary>
/// Compares a freshly built cEDH land baseline against the committed one and rejects shapes that
/// indicate corrupt input rather than metagame movement.
/// </summary>
/// <remarks>
/// Calibrated against the 2026-07-27 incident, where a double-faced-card resolution bug dropped
/// 208 card names — heavily weighted toward modal-DFC lands — and produced a snapshot that
/// under-counted roughly 1.9 lands per deck while the pipeline reported success.
/// </remarks>
public static class CedhBaselineDriftCheck
{
    /// <summary>Evaluate a candidate snapshot against the previous one.</summary>
    /// <param name="previous">The committed snapshot being replaced.</param>
    /// <param name="candidate">The freshly built snapshot.</param>
    /// <param name="thresholds">Limits loaded from the committed thresholds file.</param>
    /// <returns>A verdict carrying every rule breach found.</returns>
    public static CedhDriftVerdict Evaluate(
        CedhLandBaselineSnapshot previous,
        CedhLandBaselineSnapshot candidate,
        CedhDriftThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(thresholds);

        List<CedhDriftFinding> findings = [];
        AddDroppedEstablishedCommanders(previous, candidate, thresholds, findings);

        return new CedhDriftVerdict { Passed = findings.Count == 0, Findings = findings };
    }

    private static void AddDroppedEstablishedCommanders(
        CedhLandBaselineSnapshot previous,
        CedhLandBaselineSnapshot candidate,
        CedhDriftThresholds thresholds,
        List<CedhDriftFinding> findings)
    {
        foreach ((string name, CedhCommanderBaselineSnapshot prior) in previous.Commanders)
        {
            if (prior.N < thresholds.MinEstablishedN || candidate.Commanders.ContainsKey(name))
            {
                continue;
            }

            findings.Add(new CedhDriftFinding
            {
                Rule = "DroppedEstablishedCommander",
                Commander = name,
                Detail =
                    $"present with n={prior.N} in the committed snapshot ({previous.Generated}) but absent "
                    + $"from the candidate; floor is n>={thresholds.MinEstablishedN}.",
            });
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~CedhBaselineDriftCheckTests" --nologo`
Expected: PASS, 5 tests.

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs DeckFlow.Core.Tests/Manabase/CedhBaselineDriftCheckTests.cs
git commit -m "feat(cedh-baseline): add drift guard with dropped-commander rule

Pure comparison of a candidate baseline against the committed one. First
rule catches an established commander (prior n>=10) vanishing outright,
which is what happened to The Cabbage Merchant in the corrupt 2026-07
run.

Thresholds carry no code-side defaults so a missing config field throws
rather than quietly disabling the guard."
```

---

## Task 3: Rule 2 (`SampleCollapse`)

**Files:**
- Modify: `DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs`
- Test: `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftCheckTests.cs`

**Interfaces:**
- Consumes: `CedhBaselineDriftCheck.Evaluate`, `CedhDriftThresholds` (Task 2)
- Produces: findings with `Rule == "SampleCollapse"`

- [ ] **Step 1: Write the failing tests**

Append to `CedhBaselineDriftCheckTests`:

```csharp
    [Fact]
    public void Evaluate_PopulousCommanderSampleCollapses_Fails()
    {
        // Ral, Monsoon Mage fell 105 -> 7 (-93.3%) in the corrupt 2026-07 run because its own
        // card is a DFC and failed to resolve, so its decks could not be keyed to it.
        CedhLandBaselineSnapshot previous = Snapshot(
            "2026-07", ("Ral, Monsoon Mage // Ral, Leyline Prodigy", 105, 21.6));
        CedhLandBaselineSnapshot candidate = Snapshot(
            "2026-07", ("Ral, Monsoon Mage // Ral, Leyline Prodigy", 7, 17.9));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Findings, f => f.Rule == "SampleCollapse");
    }

    [Fact]
    public void Evaluate_OrdinaryWindowSlide_Passes()
    {
        // The corrected 2026-07 refresh's worst drop among populous commanders was -9.5%.
        CedhLandBaselineSnapshot previous = Snapshot("2026-07", ("Glarb, Calamity's Augur", 22, 28.5));
        CedhLandBaselineSnapshot candidate = Snapshot("2026-07", ("Glarb, Calamity's Augur", 20, 27.8));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_SampleDropExactlyAtLimit_Passes()
    {
        // Rule fires above the limit, not at it: 100 -> 60 is exactly 40%.
        CedhLandBaselineSnapshot previous = Snapshot("2026-07", ("Tivit, Seller of Secrets", 100, 28.3));
        CedhLandBaselineSnapshot candidate = Snapshot("2026-07", ("Tivit, Seller of Secrets", 60, 28.3));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_SampleDropJustPastLimit_Fails()
    {
        CedhLandBaselineSnapshot previous = Snapshot("2026-07", ("Tivit, Seller of Secrets", 100, 28.3));
        CedhLandBaselineSnapshot candidate = Snapshot("2026-07", ("Tivit, Seller of Secrets", 59, 28.3));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        Assert.Equal("SampleCollapse", Assert.Single(verdict.Findings).Rule);
    }

    [Fact]
    public void Evaluate_ThinCommanderSampleCollapses_Passes()
    {
        // Below MinPopulousN the swing is noise, not signal.
        CedhLandBaselineSnapshot previous = Snapshot("2026-07", ("Kaalia of the Vast", 19, 25.8));
        CedhLandBaselineSnapshot candidate = Snapshot("2026-07", ("Kaalia of the Vast", 3, 25.3));

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~CedhBaselineDriftCheckTests" --nologo`
Expected: FAIL — `Evaluate_PopulousCommanderSampleCollapses_Fails` and `Evaluate_SampleDropJustPastLimit_Fails` report `verdict.Passed` was true.

- [ ] **Step 3: Add Rule 2**

In `CedhBaselineDriftCheck.Evaluate`, add the call after the Rule 1 call:

```csharp
        AddDroppedEstablishedCommanders(previous, candidate, thresholds, findings);
        AddSampleCollapses(previous, candidate, thresholds, findings);
```

Add the method below `AddDroppedEstablishedCommanders`:

```csharp
    private static void AddSampleCollapses(
        CedhLandBaselineSnapshot previous,
        CedhLandBaselineSnapshot candidate,
        CedhDriftThresholds thresholds,
        List<CedhDriftFinding> findings)
    {
        foreach ((string name, CedhCommanderBaselineSnapshot prior) in previous.Commanders)
        {
            if (prior.N < thresholds.MinPopulousN
                || !candidate.Commanders.TryGetValue(name, out CedhCommanderBaselineSnapshot? current))
            {
                continue;
            }

            double dropPct = (prior.N - current.N) / (double)prior.N * 100.0;
            if (dropPct <= thresholds.MaxSampleDropPct)
            {
                continue;
            }

            findings.Add(new CedhDriftFinding
            {
                Rule = "SampleCollapse",
                Commander = name,
                Detail =
                    $"sample fell {dropPct:0.0}% (n {prior.N} -> {current.N}); "
                    + $"limit is {thresholds.MaxSampleDropPct:0.#}%.",
            });
        }
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~CedhBaselineDriftCheckTests" --nologo`
Expected: PASS, 10 tests.

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs DeckFlow.Core.Tests/Manabase/CedhBaselineDriftCheckTests.cs
git commit -m "feat(cedh-baseline): reject sample collapse in populous commanders

A commander with a prior sample of 20+ losing more than 40% of it is
input corruption, not window slide: the corrected 2026-07 refresh's
worst such drop was 9.5%, while the corrupt run put Ral, Monsoon Mage
at -93.3%."
```

---

## Task 4: Rule 3 (`OneSidedDrift`)

**Files:**
- Modify: `DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs`
- Test: `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftCheckTests.cs`

**Interfaces:**
- Consumes: `CedhBaselineDriftCheck.Evaluate`, `CedhDriftThresholds` (Task 2)
- Produces: findings with `Rule == "OneSidedDrift"` (not per-commander; `Commander` is null)

- [ ] **Step 1: Write the failing tests**

Append to `CedhBaselineDriftCheckTests`. Note the helper that builds many movers at once:

```csharp
    private static CedhLandBaselineSnapshot MoverSnapshot(int count, double meanDelta, int startAt = 0)
    {
        (string, int, double)[] rows = Enumerable.Range(startAt, count)
            .Select(i => ($"Commander {i}", 50, 26.0 + meanDelta))
            .ToArray();
        return Snapshot("2026-07", rows);
    }

    [Fact]
    public void Evaluate_ManyMoversAllSameDirection_Fails()
    {
        // The corrupt 2026-07 run moved 42 commanders by >=0.5 lands and every single one moved
        // down. Metagame drift scatters; systematic corruption pushes one way.
        CedhLandBaselineSnapshot previous = MoverSnapshot(12, 0.0);
        CedhLandBaselineSnapshot candidate = MoverSnapshot(12, -1.0);

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.False(verdict.Passed);
        CedhDriftFinding finding = Assert.Single(verdict.Findings, f => f.Rule == "OneSidedDrift");
        Assert.Null(finding.Commander);
    }

    [Fact]
    public void Evaluate_ManyMoversMixedDirections_Passes()
    {
        var previousRows = Enumerable.Range(0, 12).Select(i => ($"Commander {i}", 50, 26.0)).ToArray();
        var candidateRows = Enumerable.Range(0, 12)
            .Select(i => ($"Commander {i}", 50, i % 2 == 0 ? 27.0 : 25.0))
            .ToArray();

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(
            Snapshot("2026-07", previousRows), Snapshot("2026-07", candidateRows), Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_FewMoversAllSameDirection_Passes()
    {
        // Below MinMoversForDirectionTest the rule is inert: the corrected 2026-07 refresh had
        // only 4 movers (1 up, 3 down), which is 75% one-sided by chance.
        CedhLandBaselineSnapshot previous = MoverSnapshot(4, 0.0);
        CedhLandBaselineSnapshot candidate = MoverSnapshot(4, -1.0);

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public void Evaluate_SubThresholdMovementIsNotAMover()
    {
        // 0.4 lands is below MoverThresholdLands, so these do not count even though all 20 shift
        // the same way.
        CedhLandBaselineSnapshot previous = MoverSnapshot(20, 0.0);
        CedhLandBaselineSnapshot candidate = MoverSnapshot(20, -0.4);

        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, candidate, Thresholds);

        Assert.True(verdict.Passed);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~CedhBaselineDriftCheckTests" --nologo`
Expected: FAIL — `Evaluate_ManyMoversAllSameDirection_Fails` reports `verdict.Passed` was true.

- [ ] **Step 3: Add Rule 3**

In `Evaluate`, add the third call:

```csharp
        AddDroppedEstablishedCommanders(previous, candidate, thresholds, findings);
        AddSampleCollapses(previous, candidate, thresholds, findings);
        AddOneSidedDrift(previous, candidate, thresholds, findings);
```

Add the method:

```csharp
    private static void AddOneSidedDrift(
        CedhLandBaselineSnapshot previous,
        CedhLandBaselineSnapshot candidate,
        CedhDriftThresholds thresholds,
        List<CedhDriftFinding> findings)
    {
        int up = 0;
        int down = 0;

        foreach ((string name, CedhCommanderBaselineSnapshot prior) in previous.Commanders)
        {
            if (!candidate.Commanders.TryGetValue(name, out CedhCommanderBaselineSnapshot? current))
            {
                continue;
            }

            double delta = current.LandsMean - prior.LandsMean;
            if (Math.Abs(delta) < thresholds.MoverThresholdLands)
            {
                continue;
            }

            if (delta > 0)
            {
                up++;
            }
            else
            {
                down++;
            }
        }

        int movers = up + down;
        if (movers < thresholds.MinMoversForDirectionTest)
        {
            return;
        }

        double oneSidedPct = Math.Max(up, down) / (double)movers * 100.0;
        if (oneSidedPct < thresholds.MaxOneSidedPct)
        {
            return;
        }

        findings.Add(new CedhDriftFinding
        {
            Rule = "OneSidedDrift",
            Detail =
                $"{movers} commanders moved at least {thresholds.MoverThresholdLands:0.#} lands and "
                + $"{oneSidedPct:0.0}% went the same way (up {up}, down {down}); limit is "
                + $"{thresholds.MaxOneSidedPct:0.#}%. Metagame drift scatters; a one-sided shift "
                + "indicates systematic input corruption.",
        });
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~CedhBaselineDriftCheckTests" --nologo`
Expected: PASS, 14 tests.

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs DeckFlow.Core.Tests/Manabase/CedhBaselineDriftCheckTests.cs
git commit -m "feat(cedh-baseline): reject one-sided drift across many commanders

Metagame movement scatters in both directions; systematic input
corruption pushes every commander the same way. The corrupt 2026-07 run
moved 42 commanders by at least 0.5 lands and all 42 moved down, while
the corrected run had 4 movers split 1 up / 3 down.

The minimum-mover count keeps the rule inert on quiet months where a
handful of movers could align by chance."
```

---

## Task 5: Threshold parsing (`CedhDriftThresholds.FromJson`)

**Files:**
- Modify: `DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs`
- Create: `scripts/cedh-baseline/drift-thresholds.json`
- Test: `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftCheckTests.cs`

**Interfaces:**
- Consumes: `CedhDriftThresholds` (Task 2)
- Produces: `static CedhDriftThresholds CedhDriftThresholds.FromJson(string json)` — throws `JsonException` on malformed input or any missing property

- [ ] **Step 1: Write the failing tests**

Append to `CedhBaselineDriftCheckTests`:

```csharp
    [Fact]
    public void FromJson_CompleteDocument_BindsEveryThreshold()
    {
        const string Json = """
            {
              "minEstablishedN": 10,
              "minPopulousN": 20,
              "maxSampleDropPct": 40,
              "moverThresholdLands": 0.5,
              "minMoversForDirectionTest": 10,
              "maxOneSidedPct": 90
            }
            """;

        CedhDriftThresholds thresholds = CedhDriftThresholds.FromJson(Json);

        Assert.Equal(10, thresholds.MinEstablishedN);
        Assert.Equal(20, thresholds.MinPopulousN);
        Assert.Equal(40, thresholds.MaxSampleDropPct);
        Assert.Equal(0.5, thresholds.MoverThresholdLands);
        Assert.Equal(10, thresholds.MinMoversForDirectionTest);
        Assert.Equal(90, thresholds.MaxOneSidedPct);
    }

    [Fact]
    public void FromJson_MissingProperty_Throws()
    {
        // A typo must not silently disable the guard, so there are no code-side defaults.
        const string Json = """
            {
              "minEstablishedN": 10,
              "minPopulousN": 20,
              "maxSampleDropPct": 40,
              "moverThresholdLands": 0.5,
              "minMoversForDirectionTest": 10
            }
            """;

        Assert.Throws<JsonException>(() => CedhDriftThresholds.FromJson(Json));
    }

    [Fact]
    public void FromJson_Garbage_Throws()
    {
        Assert.Throws<JsonException>(() => CedhDriftThresholds.FromJson("{ nope"));
    }
```

Add `using System.Text.Json;` to the test file's usings.

- [ ] **Step 2: Run tests to verify they fail**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~CedhBaselineDriftCheckTests" --nologo`
Expected: build failure — `FromJson` does not exist.

- [ ] **Step 3: Add `FromJson`**

Add `using System.Text.Json;` to the top of `CedhBaselineDriftCheck.cs`, then add these members inside `CedhDriftThresholds`:

```csharp
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Parse thresholds from the committed configuration file's contents.</summary>
    /// <param name="json">Raw JSON document.</param>
    /// <returns>The parsed thresholds.</returns>
    /// <exception cref="JsonException">
    /// The document is malformed or omits any threshold. Missing values are fatal by design:
    /// falling back to defaults would let a typo disable the guard silently.
    /// </exception>
    public static CedhDriftThresholds FromJson(string json) =>
        JsonSerializer.Deserialize<CedhDriftThresholds>(json, JsonOptions)
        ?? throw new JsonException("Drift thresholds document was null.");
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~CedhBaselineDriftCheckTests" --nologo`
Expected: PASS, 17 tests.

If `FromJson_MissingProperty_Throws` fails, the `required` modifiers were dropped from Task 2 — restore them; `required` is what makes System.Text.Json reject an incomplete document.

- [ ] **Step 5: Create the committed thresholds file**

Create `scripts/cedh-baseline/drift-thresholds.json` (LF endings):

```json
{
  "minEstablishedN": 10,
  "minPopulousN": 20,
  "maxSampleDropPct": 40,
  "moverThresholdLands": 0.5,
  "minMoversForDirectionTest": 10,
  "maxOneSidedPct": 90
}
```

- [ ] **Step 6: Commit**

```bash
git add DeckFlow.Core/Manabase/CedhBaselineDriftCheck.cs DeckFlow.Core.Tests/Manabase/CedhBaselineDriftCheckTests.cs scripts/cedh-baseline/drift-thresholds.json
git commit -m "feat(cedh-baseline): load drift thresholds from committed config

Thresholds live beside the runbook rather than in DeckFlow.Web/Data,
which ships to production. Overriding a legitimate trip means editing
and committing this file, so the new normal is reviewed in a diff.

Every property is required, so an incomplete document throws instead of
falling back to defaults that would mask a config typo."
```

---

## Task 6: Wire the guard into the CLI

**Files:**
- Modify: `DeckFlow.CLI/CedhBaselineCommandRunner.cs:99-122`
- Modify: `DeckFlow.CLI/Program.cs:74-77,161-163,319-322`

**Interfaces:**
- Consumes: `CedhBaselineDriftCheck.Evaluate`, `CedhDriftThresholds.FromJson` (Tasks 2-5), existing `CedhLandBaseline.ToSnapshot`
- Produces: `CedhBaselineCommandRunner.RunAsync(string dataDirectory, string outputDirectory, string month, string thresholdsPath)`

- [ ] **Step 1: Add the `--thresholds` option in `Program.cs`**

After line 77 (`cedhLandBaselineMonthOption`), add:

```csharp
var cedhLandBaselineThresholdsOption = new Option<string>("--thresholds", () => Path.Combine("scripts", "cedh-baseline", "drift-thresholds.json")) { Description = "Path to the committed drift-threshold configuration." };
```

After line 163, add:

```csharp
cedhLandBaselineCommand.AddOption(cedhLandBaselineThresholdsOption);
```

Replace the handler at lines 319-322 with:

```csharp
cedhLandBaselineCommand.SetHandler((string dataDirectory, string outputDirectory, string month, string thresholdsPath) =>
{
    return CedhBaselineCommandRunner.RunAsync(dataDirectory, outputDirectory, month, thresholdsPath);
}, cedhLandBaselineDataOption, cedhLandBaselineOutOption, cedhLandBaselineMonthOption, cedhLandBaselineThresholdsOption);
```

- [ ] **Step 2: Add the guard to `CedhBaselineCommandRunner`**

Change the signature on line 29 to:

```csharp
    public static Task<int> RunAsync(string dataDirectory, string outputDirectory, string month, string thresholdsPath)
```

Then, immediately **after** the existing zero-deck guard (`"The cEDH gate kept zero decks; nothing to write."`) and **before** the `markdownPath`/`monthlyJsonPath`/`latestJsonPath` assignments, insert:

```csharp
            // Evaluate drift BEFORE writing anything. The 2026-07-27 corrupt run overwrote the
            // committed artifacts and they had to be recovered from git; failing first leaves the
            // last-known-good snapshot in place.
            string latestPath = Path.Combine(outputDirectory, "latest.json");
            if (File.Exists(latestPath))
            {
                if (!File.Exists(thresholdsPath))
                {
                    Console.Error.WriteLine($"Drift thresholds file not found at {thresholdsPath}.");
                    return Task.FromResult(1);
                }

                CedhDriftThresholds thresholds;
                CedhLandBaselineSnapshot? previous;
                try
                {
                    thresholds = CedhDriftThresholds.FromJson(File.ReadAllText(thresholdsPath));
                    previous = JsonSerializer.Deserialize<CedhLandBaselineSnapshot>(
                        File.ReadAllText(latestPath),
                        JsonOptions);
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine($"Could not read drift inputs: {ex.Message}");
                    return Task.FromResult(1);
                }

                if (previous is null)
                {
                    Console.Error.WriteLine($"Could not deserialize the committed snapshot at {latestPath}.");
                    return Task.FromResult(1);
                }

                CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(previous, snapshot, thresholds);
                if (!verdict.Passed)
                {
                    Console.Error.WriteLine(
                        $"Drift check FAILED with {verdict.Findings.Count} finding(s); no files written.");
                    foreach (CedhDriftFinding finding in verdict.Findings)
                    {
                        string subject = finding.Commander is null ? finding.Rule : $"{finding.Rule} [{finding.Commander}]";
                        Console.Error.WriteLine($"  {subject}: {finding.Detail}");
                    }

                    Console.Error.WriteLine(
                        "If this reflects a genuine metagame shift, retune and commit "
                        + $"{thresholdsPath}, then re-run.");
                    return Task.FromResult(1);
                }

                Console.WriteLine($"Drift check passed against {latestPath}.");
            }
            else
            {
                Console.WriteLine($"No committed snapshot at {latestPath}; skipping drift check (bootstrap run).");
            }
```

Then change the existing `string latestJsonPath = Path.Combine(outputDirectory, "latest.json");` line to reuse the variable:

```csharp
            string latestJsonPath = latestPath;
```

Note: `latestPath` is declared inside the `try` block scope that already exists in this method; declare it at the same nesting level as `markdownPath` so both the guard and the writer see it.

- [ ] **Step 3: Build**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln --nologo`
Expected: 0 errors, 0 new warnings.

- [ ] **Step 4: Verify the guard passes on the current good data**

Run:
```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.CLI -- cedh-land-baseline --data _calib --month 2026-07
```
Expected: `Drift check passed against ...latest.json`, then the three `Wrote ...` lines. `git diff --stat DeckFlow.Web/Data/cedh-land-baseline/` should show no change, since the inputs are identical.

- [ ] **Step 5: Verify the guard blocks a corrupt candidate**

Two traps make the obvious approaches fail:

- **Lowering the thresholds does nothing.** Re-running against unchanged `_calib` produces a
  candidate identical to the committed snapshot, so there are zero movers and zero collapses. No
  rule can fire regardless of how tight the limits are.
- **Do not use a `/tmp` path for `--out`.** `dotnet` here is the Windows binary invoked from WSL,
  so it resolves `/tmp` as `C:\tmp` — a different directory from WSL's `/tmp`. The run silently
  takes the bootstrap path and writes where you are not looking.

Instead, doctor a *previous* snapshot in a repo-internal scratch directory so the unchanged
candidate looks corrupt against it:

```bash
D=.superpowers/sdd/cedh-baseline-drift-guard-PLAN/drift-fail
rm -rf $D && mkdir -p $D
python3 - <<'PY'
import json
s = json.load(open("DeckFlow.Web/Data/cedh-land-baseline/latest.json"))
s["commanders"]["Kinnan, Bonder Prodigy"]["n"] = 1000   # candidate has 337 -> -66.3% collapse
s["commanders"]["Zzz Vanished Commander"] = {"n": 25, "landsMean": 26.0, "landsSd": 1.0}
json.dump(s, open(".superpowers/sdd/cedh-baseline-drift-guard-PLAN/drift-fail/latest.json","w"), indent=2)
PY
md5sum $D/latest.json
"/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.CLI -- cedh-land-baseline --data _calib --month 2026-07 --out $D
ls $D
md5sum $D/latest.json
git status --porcelain DeckFlow.Web/Data/cedh-land-baseline/
rm -rf $D
```

Expected: a `Drift check FAILED with 2 finding(s); no files written.` block naming both
`DroppedEstablishedCommander [Zzz Vanished Commander]` and
`SampleCollapse [Kinnan, Bonder Prodigy]` (66.3% vs the 40% limit); `ls` showing **only**
`latest.json`; an **unchanged** md5 proving it was not overwritten; and empty `git status` for the
real baseline directory.

- [ ] **Step 6: Commit**

```bash
git add DeckFlow.CLI/CedhBaselineCommandRunner.cs DeckFlow.CLI/Program.cs
git commit -m "feat(cedh-baseline): gate the baseline write on the drift check

The drift check runs after the snapshot is built but before any file is
written, so a rejected refresh leaves the committed artifacts intact.
The 2026-07-27 corrupt run overwrote them and they had to be recovered
from git.

A missing committed snapshot is treated as a bootstrap run and skips the
check; a malformed one fails, because a snapshot that cannot be verified
must not be replaced."
```

---

## Task 7: Regression test from the 2026-07 incident

**Files:**
- Create: `DeckFlow.Core.Tests/Fixtures/cedh-drift/previous-2026-07-11.json`
- Create: `DeckFlow.Core.Tests/Fixtures/cedh-drift/candidate-corrupt.json`
- Create: `DeckFlow.Core.Tests/Fixtures/cedh-drift/candidate-corrected.json`
- Create: `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftRegressionTests.cs`
- Modify: `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`

**Interfaces:**
- Consumes: `CedhBaselineDriftCheck.Evaluate`, `CedhDriftThresholds.FromJson` (Tasks 2-5)
- Produces: nothing consumed downstream

**Fixture provenance — read before building these.** The literal corrupt `latest.json` no longer exists; it was overwritten by the corrected rebuild before anyone thought to keep it. These fixtures are therefore **faithful reconstructions from the recorded incident measurements**, not the original bytes. Every value below was captured from the live comparison at the time. Label this in the fixture files so nobody later mistakes them for raw artifacts.

Recorded incident measurements:
- `Ral, Monsoon Mage // Ral, Leyline Prodigy`: n 105 -> 7, mean 21.6 -> 17.9
- `The Cabbage Merchant`: n 18, mean 24.9 -> dropped entirely
- `Vivi Ornitier`: n 66 -> 28, mean 26.0 -> 23.0
- `Magda, Brazen Outlaw`: n 37 -> 10, mean 24.4 -> 23.6
- 42 commanders moved >= 0.5 lands, **all downward**
- Corrected run: 4 movers (1 up, 3 down), worst populous sample drop -9.5%

- [ ] **Step 1: Build the three fixtures**

Create `DeckFlow.Core.Tests/Fixtures/cedh-drift/previous-2026-07-11.json`. Include the four named commanders plus 12 filler commanders that will become one-sided movers:

```json
{
  "_comment": "Reconstructed from the 2026-07-27 incident measurements, not raw pipeline output. See .planning/captures/cedh-baseline-drift-guard-design.md.",
  "generated": "2026-07",
  "sampleSize": 3281,
  "overallMeanLands": 26.5,
  "commanders": {
    "Ral, Monsoon Mage // Ral, Leyline Prodigy": { "n": 105, "landsMean": 21.6, "landsSd": 1.2 },
    "The Cabbage Merchant": { "n": 18, "landsMean": 24.9, "landsSd": 1.1 },
    "Vivi Ornitier": { "n": 66, "landsMean": 26.0, "landsSd": 1.0 },
    "Magda, Brazen Outlaw": { "n": 37, "landsMean": 24.4, "landsSd": 1.0 },
    "Filler 01": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 },
    "Filler 02": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 },
    "Filler 03": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 },
    "Filler 04": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 },
    "Filler 05": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 },
    "Filler 06": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 },
    "Filler 07": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 },
    "Filler 08": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 },
    "Filler 09": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 },
    "Filler 10": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 },
    "Filler 11": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 },
    "Filler 12": { "n": 50, "landsMean": 26.0, "landsSd": 1.0 }
  }
}
```

Create `candidate-corrupt.json` — same `_comment`, `generated` `2026-07`, `sampleSize` 3200, `overallMeanLands` 26.1, and:
- `Ral, Monsoon Mage // Ral, Leyline Prodigy`: `n` 7, `landsMean` 17.9
- `The Cabbage Merchant`: **omit the key entirely**
- `Vivi Ornitier`: `n` 28, `landsMean` 23.0
- `Magda, Brazen Outlaw`: `n` 10, `landsMean` 23.6
- all 12 `Filler NN`: `n` 50, `landsMean` **25.0** (a uniform -1.0 downward move)

Create `candidate-corrected.json` — same `_comment`, `generated` `2026-07`, `sampleSize` 3492, `overallMeanLands` 26.3, and:
- `Ral, Monsoon Mage // Ral, Leyline Prodigy`: `n` 114, `landsMean` 21.6
- `The Cabbage Merchant`: `n` 16, `landsMean` 24.9
- `Vivi Ornitier`: `n` 69, `landsMean` 26.0
- `Magda, Brazen Outlaw`: `n` 34, `landsMean` 24.4
- `Filler 01`: `n` 50, `landsMean` **26.6** (one upward mover)
- `Filler 02`, `Filler 03`: `n` 50, `landsMean` **25.4** (two downward movers)
- `Filler 04` through `Filler 12`: `n` 50, `landsMean` 26.0 (unchanged)

This yields 3 movers in the corrected fixture — below `minMoversForDirectionTest`, matching the real run's 4.

- [ ] **Step 2: Copy fixtures to the test output directory**

Add to `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` inside an `<ItemGroup>`:

```xml
    <Content Include="Fixtures\cedh-drift\*.json" CopyToOutputDirectory="PreserveNewest" />
```

- [ ] **Step 3: Write the failing test**

Create `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftRegressionTests.cs`:

```csharp
using System.Text.Json;
using DeckFlow.Core.Manabase;
using Xunit;

namespace DeckFlow.Core.Tests.Manabase;

/// <summary>
/// Pins the drift guard against the 2026-07-27 corruption incident, where a double-faced-card
/// resolution bug dropped 208 card names and produced a snapshot that under-counted roughly
/// 1.9 lands per deck while the pipeline reported success.
/// </summary>
/// <remarks>
/// These fixtures are reconstructions from the recorded incident measurements, not raw pipeline
/// output; the corrupt artifact was overwritten before it could be preserved. The point of this
/// test is that any future widening of the thresholds must still reject the corrupt candidate.
/// </remarks>
public sealed class CedhBaselineDriftRegressionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "cedh-drift", name);

    private static CedhLandBaselineSnapshot LoadSnapshot(string name) =>
        JsonSerializer.Deserialize<CedhLandBaselineSnapshot>(File.ReadAllText(FixturePath(name)), JsonOptions)
        ?? throw new InvalidOperationException($"Fixture {name} did not deserialize.");

    private static CedhDriftThresholds LoadCommittedThresholds()
    {
        string path = Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "scripts", "cedh-baseline", "drift-thresholds.json"));
        return CedhDriftThresholds.FromJson(File.ReadAllText(path));
    }

    [Fact]
    public void CommittedThresholds_RejectTheJuly2026CorruptSnapshot()
    {
        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(
            LoadSnapshot("previous-2026-07-11.json"),
            LoadSnapshot("candidate-corrupt.json"),
            LoadCommittedThresholds());

        Assert.False(
            verdict.Passed,
            "The committed thresholds must still reject the July 2026 corruption. If this fails, a "
            + "threshold was widened past the incident it was calibrated against.");
        Assert.Contains(verdict.Findings, f => f.Rule == "DroppedEstablishedCommander");
        Assert.Contains(verdict.Findings, f => f.Rule == "SampleCollapse");
        Assert.Contains(verdict.Findings, f => f.Rule == "OneSidedDrift");
    }

    [Fact]
    public void CommittedThresholds_AcceptTheCorrectedSnapshot()
    {
        CedhDriftVerdict verdict = CedhBaselineDriftCheck.Evaluate(
            LoadSnapshot("previous-2026-07-11.json"),
            LoadSnapshot("candidate-corrected.json"),
            LoadCommittedThresholds());

        Assert.True(
            verdict.Passed,
            "The corrected July 2026 refresh must pass. If this fails, a threshold is too tight and "
            + "will reject legitimate monthly refreshes.");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~CedhBaselineDriftRegressionTests" --nologo`
Expected: PASS, 2 tests.

If `CommittedThresholds_AcceptTheCorrectedSnapshot` fails, check `The Cabbage Merchant` — the corrected fixture must retain it (n=16), or Rule 1 fires.

- [ ] **Step 5: Commit**

```bash
git add DeckFlow.Core.Tests/Fixtures/cedh-drift/ DeckFlow.Core.Tests/Manabase/CedhBaselineDriftRegressionTests.cs DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj
git commit -m "test(cedh-baseline): pin the drift guard to the July 2026 incident

Threshold guards rot because nobody remembers what the numbers were
calibrated against; six months on someone widens a limit to unblock a
run and quietly destroys the gate. These fixtures make the thresholds a
claim about a specific known failure rather than arbitrary constants.

The fixtures are reconstructions from the recorded measurements, not raw
pipeline output: the corrupt artifact was overwritten by the corrected
rebuild before it could be preserved."
```

---

## Task 8: Re-scope the churn-prone provider test pins

**Files:**
- Modify: `DeckFlow.Web.Tests/Manabase/CedhLandBaselineProviderTests.cs:28,62`

**Interfaces:**
- Consumes: nothing from earlier tasks
- Produces: nothing consumed downstream

Exact-`n` equality fails on **every** refresh by construction — the 2026-07-27 run had to change 327→337 and 241→255 — while carrying no correctness signal, because sample counts move whenever the 6-month window slides. Task 7's guard now covers sample-population sanity far more thoroughly.

- [ ] **Step 1: Replace exact `n` equality with floors**

In `TryGetBaseline_SingleCommanderMatch_BindsLatestJson`, replace line 28:

```csharp
        Assert.Equal(337, n);
```

with:

```csharp
        // Floor rather than equality: sample counts move every refresh as the 6-month window
        // slides, which is churn, not signal. CedhBaselineDriftCheck now guards population sanity.
        // 200 is ~60% of the 2026-07 value (337), mirroring the 40% maxSampleDropPct limit.
        Assert.True(n >= 200, $"Kinnan sample fell to {n}; expected at least 200.");
```

In `TryGetBaseline_PartnerMatch_WorksInBothOrders`, replace line 62:

```csharp
        Assert.Equal(255, reverseN);
```

with:

```csharp
        // Floor rather than equality; 150 is ~60% of the 2026-07 value (255).
        Assert.True(reverseN >= 150, $"Rograkh/Thrasios sample fell to {reverseN}; expected at least 150.");
```

Leave every other assertion untouched. The `mean` and `sd` pins carry real signal — a large swing indicates corrupt data, which is what would have caught this incident. Plagon's `n >= 10` assertion and its comment already have the right shape.

- [ ] **Step 2: Run the tests**

Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --filter "FullyQualifiedName~CedhLandBaselineProviderTests" --nologo`
Expected: PASS, 7 tests.

- [ ] **Step 3: Commit**

```bash
git add DeckFlow.Web.Tests/Manabase/CedhLandBaselineProviderTests.cs
git commit -m "test(cedh-baseline): floor the sample-count pins instead of equality

Exact n equality failed on every refresh by construction while carrying
no correctness signal, since sample counts move whenever the 6-month
window slides. The mean and sd pins stay: a large swing there indicates
corrupt data.

CedhBaselineDriftCheck now guards sample-population sanity far more
thoroughly than a hard-coded integer could."
```

---

## Task 9: Document both gates in the runbook

**Files:**
- Modify: `scripts/cedh-baseline/README.md`

**Interfaces:**
- Consumes: nothing
- Produces: nothing

- [ ] **Step 1: Add a Guards section**

Append to `scripts/cedh-baseline/README.md`:

```markdown
### Guards

The pipeline fails closed at two points. Neither is advisory.

1. **Unresolved cards (`fetch.py`).** Any card name that does not resolve against Scryfall fails
   the fetch with a non-zero exit. An unresolved name is a silently dropped card: the baseline
   counts lands off the resolved cache, so unresolved modal-DFC lands under-count the deck. The
   card cache is written before the failure, so the run stays resumable.

2. **Drift check (`cedh-land-baseline`).** Before writing any artifact, the new snapshot is
   compared against the committed `latest.json` using the limits in
   `scripts/cedh-baseline/drift-thresholds.json`. Three rules fire on shapes that indicate corrupt
   input rather than metagame movement: an established commander disappearing, a populous
   commander's sample collapsing, and many commanders drifting the same direction at once. On
   failure nothing is written, so the last-known-good snapshot survives.

If a refresh trips the drift check because the metagame genuinely moved, retune and commit
`drift-thresholds.json`, then re-run. Committing the change means the new normal is reviewed in a
diff. `DeckFlow.Core.Tests/Manabase/CedhBaselineDriftRegressionTests.cs` will reject any retune
that would let the July 2026 corruption through.
```

- [ ] **Step 2: Run the full suites**

Run:
```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --nologo -v q
"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --nologo -v q
```
Expected: 0 failures in both. Baselines before this work: Core 1820 passed, Web 2043 passed / 16 skipped.

- [ ] **Step 3: Verify line endings across every touched file**

```bash
git diff --stat main..HEAD
git diff --ignore-all-space --stat main..HEAD
```
Expected: identical output. Any gap means EOL churn — re-normalize the affected file to its committed style before finishing.

- [ ] **Step 4: Commit**

```bash
git add scripts/cedh-baseline/README.md
git commit -m "docs(cedh-baseline): document the two pipeline guards

Both gates fail closed; neither is advisory. Records the override path
for a legitimate metagame shift and notes that the regression test will
reject any retune that reopens the July 2026 hole."
```

---

## Self-Review

**Spec coverage:**

| Spec requirement | Task |
|---|---|
| Gate 1: zero unresolved names, fail non-zero, cache persisted first | 1 |
| Gate 2: pure `CedhBaselineDriftCheck` in Core | 2-4 |
| Rule `DroppedEstablishedCommander` | 2 |
| Rule `SampleCollapse` | 3 |
| Rule `OneSidedDrift` | 4 |
| Thresholds file at `scripts/cedh-baseline/`, `required` properties | 5 |
| Evaluate before writing; nothing written on failure | 6 |
| No previous snapshot → skip; malformed → fail; bad thresholds → fail | 6 |
| Regression fixture from the real incident | 7 |
| Re-scope churn-prone `n` pins | 8 |
| Runbook documents both gates | 9 |

No gaps.

**Placeholder scan:** No TBD/TODO. Every code step carries actual code. Task 7's fixture derivation is spelled out value-by-value rather than described.

**Type consistency:** `CedhDriftThresholds`, `CedhDriftFinding`, `CedhDriftVerdict`, `CedhBaselineDriftCheck.Evaluate`, `CedhDriftThresholds.FromJson` are used identically in Tasks 2-7. Rule name strings (`DroppedEstablishedCommander`, `SampleCollapse`, `OneSidedDrift`) match between implementation and tests. `CedhLandBaselineSnapshot` / `CedhCommanderBaselineSnapshot` property names (`Generated`, `SampleSize`, `OverallMeanLands`, `Commanders`, `N`, `LandsMean`, `LandsSd`) verified against `DeckFlow.Core/Manabase/CedhLandBaseline.cs`.

**Known risk:** Task 6 Step 2 inserts into an existing `try` block; the reviewer should confirm `latestPath` is declared at a scope both the guard and the writer can see, and that `JsonException` is caught where the existing code catches its own deserialization failures.
