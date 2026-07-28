---
phase: 02-role-floor-divergence-research
plan: 05
wave: 3
status: complete
executed: 2026-07-27
commits:
  - f2bd3916  # feat(02-05): add source-discriminated role-floor figure types
  - 0971f9d6  # feat(02-05): drive the role-floor verdict from the 25th percentile
  - e3017d75  # feat(02-05): emit per-source tables and per-source coverage
  - 58509d69  # test(02-05): characterize the floor-gap boundary and the all-zero corpus row
  - 3faae0ad  # docs(02-05): correct the ClearsBar test-deletion count in D-04
gates:
  build: "0 errors; 9 pre-existing CS8629 warnings (all in ManabaseBaselineWeightingTests.cs), 0 new"
  core_tests: "1715 passed / 0 failed (1708 at wave start; 1708 → 1716 → 1713 → 1715)"
  web_tests: "2095 passed / 16 skipped / 0 failed — unchanged"
  eol: "all five touched files LF before and after; zero churn"
verification: "blind foreman-verifier PASS on all items A–K, over all four code commits together"
---

# Phase 2 Plan 05 — Summary

Three plan tasks plus a lead-added Task 4 and one plan correction. No user-visible change;
`DeckFlow.Core/Research/` and `DeckFlow.CLI` are referenced by no controller, view or web service.
No CalVer bump, no tag.

## 1. `RoleFloorFigure.cs` — public surface

Plan `02-06` constructs these types, so the surface is pinned verbatim.

```csharp
namespace DeckFlow.Core.Research;

public enum RoleFloorSource
{
    Postgres = 1,
    Edhrec = 2,
}

public interface IRoleFloorFigure
{
    RoleFloorSource Source { get; }
    string Role { get; }
    string CommanderName { get; }
}

public sealed record PostgresRoleDistribution : IRoleFloorFigure
{
    public required RoleFloorSource Source { get; init; }
    public required string Role { get; init; }
    public required string CommanderName { get; init; }
    public required int DeckCount { get; init; }
    public required double Mean { get; init; }
    public required double P25 { get; init; }
    public required double StdDev { get; init; }
    public required double Ratio { get; init; }
    public required double ZScore { get; init; }
    public required double CohensD { get; init; }
    public required bool ClearsBar { get; init; }
}

public sealed record EdhrecRolePointEstimate : IRoleFloorFigure
{
    public required RoleFloorSource Source { get; init; }
    public required string Role { get; init; }
    public required string CommanderName { get; init; }
    public required string BracketSlug { get; init; }
    public required int BracketIndex { get; init; }
    public required double Count { get; init; }
    public required int DeckCount { get; init; }
    public required bool Qualifies { get; init; }
}

public static class RoleFloorFigureTable
{
    public static IReadOnlyList<string> PostgresColumns { get; }
    public static IReadOnlyList<string> EdhrecColumns { get; }
    public static bool HasSourceColumn(IReadOnlyList<string> columns);
}
```

Every record property is `{ get; init; }`, never `{ get; }` — the project's System.Text.Json
carve-out, because get-only properties are silently skipped in .NET 9+ and these records serialize
into `RESEARCH-FINDINGS.json`. A round-trip test covers both records.

**Notes for `02-06`, which populates these:**
- `Count` is `double`, not `int` — one EDHREC cell is a synthesized *average* deck, so a role count
  can be fractional even though `n_decks` is integral.
- `DeckCount` corresponds to the on-disk `n_decks`.
- The shipped cells carry no `source`, no `estimateKind` and no `qualifies` field. `Qualifies` is
  DERIVED by `02-06` from each cell's own `n_decks` against the 400 floor; `Source` is set at
  projection time.

### Column lists, verbatim and in order

```
PostgresColumns : Source | Commander | RAW N | DEDUPED N | Mean | P25 | Ratio | Z | Cohen's d | ClearsBar
EdhrecColumns   : Source | Commander | Bracket | Count | Decks backing cell | Qualifies
```

`Source` is first in both, by design.

## 2. Why criterion 7 is structural

`EdhrecRolePointEstimate` exposes **no** property whose name contains `P25`, `Percentile`, `StdDev`,
`ZScore`, `CohensD` or `Ratio`. Not "we chose not to compute one" — one EDHREC cell is one synthesized
average deck, so there is no sample, and a percentile / SD / z / effect size is *uncomputable*. The
type mirrors the data's real shape, and the compiler enforces it on every build.

Two reflection assertions back it, and **both were demonstrated to actually fail when violated**
rather than accepted green:

| Guard | Message when violated |
|---|---|
| `EdhrecRolePointEstimate` exposes no distribution property | `EdhrecRolePointEstimate must not expose distribution properties; found: P25` |
| every declared column list carries a `Source` column | `RoleFloorFigureTable declaration 'EdhrecColumns' must include a Source column.` |

The second is discovered by REFLECTION over every public `IReadOnlyList<string>` on
`RoleFloorFigureTable`, so a table declaration added later is covered without anyone remembering to
cover it.

**Deliberate non-invariant:** the `RoleFloorSource` member-count assertion of 2 is *expected* to
change — plan `02-09` adds `EdhrecBulk = 3` and carries narrow authorization to bump it. The
assertion exists to prevent a zero-valued default source, not to freeze the enum, and a comment
beside it says so. `IRoleFloorFigure`'s three-member assertion IS permanent, as is the
no-distribution-property assertion.

## 3. Exactly one statistical bar

`grep -rn "ClearsBar(" --include=*.cs .` (excluding `bin`/`obj` and `ClearsFloorBar`) returns
**nothing** — definition, CLI call site and every test gone. `RoleFloorDivergenceStats`'s class
xmldoc now states `ClearsFloorBar` is the only verdict bar.

The committed call site:

```csharp
ClearsBar = RoleFloorDivergenceStats.ClearsFloorBar(
    commander.DedupedN,
    commanderP25,
    baseline.P25,
    commanderMean,
    baseline.Mean,
    baseline.StdDev,
    minDeckCount,
    ratioLow: RatioLow,
    ratioHigh: RatioHigh,
    zThreshold: ZThreshold,
    absoluteFloorGap: AbsoluteFloorGap),
```

`commanderP25` is hoisted to a local before the object initializer, so it is computed once and shared
by the call and the record. `AbsoluteFloorGap = 2.0` sits at `:34`, immediately after `ZThreshold`, so
RFLR-02's "full written bar visible in one place" holds: `minDeckCount`, `RatioLow`, `RatioHigh`,
`ZThreshold`, `AbsoluteFloorGap`.

**Proof the switch changed behavior, not just the call.** Blind verification executed
`ClearsFloorBar` on a case where the two statistics disagree: commander P25 neutral (6.0 vs corpus
6.0) with a wildly divergent mean (18 vs 6, z≈56) → **False**. The deleted mean-driven `ClearsBar`
returned **True** on those same mean inputs. P25 decides divergence; the mean survives only as a
significance gate, because a sample percentile has no closed-form standard error.

## 4. The five deleted tests, and why

The plan's D-04 authorized deleting ONE test member. **Five** exercise `ClearsBar`, so deleting the
method broke compilation of all five and the authorization was unsatisfiable as written. Codex
returned `NEEDS_CONTEXT` rather than improvising — the third such correct refusal this cycle. The
plan was corrected (`3faae0ad`) before re-dispatch.

| Deleted member | Replaced by |
|---|---|
| `ClearsBar_WhenAllThresholdsMet_ReturnsTrue` | `ClearsFloorBar_P25DivergentAndSignificant_ReturnsTrue` |
| `ClearsBar_WhenDeckCountBelowMinimum_ReturnsFalse` | `ClearsFloorBar_BelowMinimumDeckCount_ReturnsFalse` |
| `ClearsBar_WhenRatioFallsInsideBand_ReturnsFalse` | `ClearsFloorBar_P25InsideNeutralBand_ReturnsFalseEvenWhenMeanIsWildlyDivergent` |
| `ClearsBar_WhenCorpusMeanIsZero_ReturnsFalse` | **nothing — by decision.** See §5. |
| `ClearsBar_ExistingMeanDrivenBehavior_IsUnchangedByThisPlan` | subject deleted (plan `02-03`'s guard) |

Verified by diffing `RoleFloorDivergenceStatsTests.cs` across `ba54ba31..58509d69`: exactly those five
removed, nothing else. **No coverage was lost** — four have direct counterparts that already existed.

## 5. The all-zero corpus row — a ratified behavior change

`ClearsBar` had a `corpusMean <= 0.0` guard. **`ClearsFloorBar` deliberately does not.** On an
all-zero corpus row (`corpusP25 = 0`, `corpusMean = 0`, `corpusStdDev = 0`) a commander with
`P25 >= absoluteFloorGap` now **clears**: the absolute-gap branch fires, and `ComputeZScore` returns
`PositiveInfinity` for unequal means against a zero-spread baseline, which passes any threshold. The
old bar returned false.

**The user ratified this on 2026-07-27.** Rationale: a commander running 2+ of a role the entire
corpus runs zero of is a genuine divergence, and handling `corpusP25 == 0` is precisely what
`absoluteFloorGap` was introduced for — porting the old guard forward would suppress real findings
and partly defeat that path.

Because the deleted `ClearsBar_WhenCorpusMeanIsZero_ReturnsFalse` was the only executable statement of
the old semantics, characterization tests now pin the new ones, with a comment naming the decision and
its date so a later "fix" has to argue with a test:

| Case | Test | Result |
|---|---|---|
| zero corpus P25, commander at exactly the gap | `ClearsFloorBar_ZeroCorpusP25_UsesAbsoluteGapFallback(2.0)` | clears |
| zero corpus P25, just below the gap | `ClearsFloorBar_ZeroCorpusP25_UsesAbsoluteGapFallback(1.999)` | does not clear |
| all-zero corpus row, at the gap | `ClearsFloorBar_AllZeroCorpusRow_AtAbsoluteGapStillClears` | clears |
| all-zero corpus row, below the gap | `ClearsFloorBar_AllZeroCorpusRow_BelowAbsoluteGapDoesNotClear` | does not clear |

The exact-`2.0` case closes a gap wave 1's verification flagged: the existing Theory covered `0.0`,
`3.0` and `1.0` against a gap of `2.0`, so a `>=`→`>` flip would have stayed green while silently
dropping every commander sitting exactly at the floor. Bite-proved:

```
Failed ClearsFloorBar_ZeroCorpusP25_UsesAbsoluteGapFallback(commanderP25: 2, expected: True)
Assert.Equal() Failure: Values differ
Expected: True   Actual: False
```

## 6. The emitted markdown — what `02-06` fills and `02-08` gates

`## Corpus Coverage` sits immediately after `## Run Provenance`, as two sub-blocks whose units
differ — decks versus cells — deliberately NOT merged:

```md
## Corpus Coverage
### Postgres (within-commander distributions)
| Metric | Value |
| Commanders enumerated / with membership / Raw deck count / Deduped deck count /
  Commanders qualifying at DEDUPED N >= <minDeckCount> / Unresolved (not_found) /
  Unresolved (rate_limited_after_retry) / Unresolved (total) |

### EDHREC (commander x bracket grid)
| Metric | Value |
| Cells fetched / Cells qualifying at >= 400 decks backing cell / Cells missing /
  Commanders reached / Per-cell minimum |
```

Figure tables — headers built from `RoleFloorFigureTable`, never a string literal
(`grep -nE '\| *Commander *\|'` returns no match):

```md
### Postgres — within-commander distribution (n decks per commander)
| Source | Commander | RAW N | DEDUPED N | Mean | P25 | Ratio | Z | Cohen's d | ClearsBar |

### EDHREC — commander x bracket point estimates
| Source | Commander | Bracket | Count | Decks backing cell | Qualifies |
```

**Every data row's first cell is that row's own source**, from `FormatRoleFloorSource(figure.Source)`
in `BuildPostgresFigureRow` / `BuildEdhrecFigureRow` — read from the figure, never a per-table
constant. A heading is not a source tag; the column is.

When `EdhrecPointEstimates` is empty the report prints
`_No EDHREC cells were supplied for this run (--edhrec-data not provided)._` rather than an empty
table or a silently omitted section — **a missing corpus must be visible.**

Beneath the EDHREC table, a standing note: *"Each figure above is a point estimate from a single
synthesized average deck. It is not a percentile and has no within-cell variance. EDHREC figures do
not enter the go/no-go."* (D-05 — `ClearsFloorBar` needs an SD and an `n` that one synthesized deck
cannot supply.)

JSON: each Postgres role object carries `"source": "postgres"`; a sibling top-level `edhrec` object
holds `cells` and `coverage`. `RoleFloorSource` serializes as its string name via
`JsonStringEnumConverter(JsonNamingPolicy.CamelCase)` configured at the call — verified by execution
that the same value serializes as `1` without it.

**Nothing populates EDHREC yet, by design.** `EdhrecPointEstimates` is only ever assigned `[]`, no
file is read, no `--edhrec-data` option exists, `Program.cs` is untouched. Plan `02-06` does all of
that. Writing the emitter before there is data to make it look right was deliberate.

## 7. Carry-forward — two LOW findings from blind verification

Both concern the *generality* of the guards, not the correctness of what shipped. The plan
anticipated this and deferred generalization to `02-09`; recording them so that deferral is
deliberate rather than forgotten.

1. **The "no distribution column" assertion is hardcoded to the literal `EdhrecColumns` property**,
   unlike the `Source`-column assertion, which reflects over every declared list. A contributor could
   add `EdhrecColumnsV2` carrying both `Source` and `P25` and pass every test in the file.
2. **The no-distribution-property guard is specific to `EdhrecRolePointEstimate`**, not to "any
   `IRoleFloorFigure` tagged `Edhrec`." A new sibling record tagged `Source = Edhrec` with its own
   `P25` would not be seen.

The honest summary: the design closes the hole it was built for and is proof against accident, not
against a determined future edit. `02-09` should generalize both assertions when it adds
`EdhrecBulk`, rather than adding a third one-off.

## 8. Next

Wave 4 — plan `02-06`: add `--edhrec-data`, read the 1,525-cell corpus already on disk in
`_edhrec-brackets/`, and project it into `EdhrecRolePointEstimate` / `EdhrecCoverage`, which this plan
left deliberately empty.
