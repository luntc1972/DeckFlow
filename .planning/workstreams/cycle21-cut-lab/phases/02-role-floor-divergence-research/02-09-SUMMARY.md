---
phase: 02-role-floor-divergence-research
plan: 09
wave: 6
status: complete
executed: 2026-07-28
commits:
  - 9be93f82  # feat(02-09): stream EDHREC card counts with a denominator gate
  - 4cc6abdf  # refactor(02-09): make denominator mismatches structured and ratio-ordered
  - df94707e  # feat(02-09): add EdhrecBulk source tag and expected-role-count figure type
  - 0d95f6de  # feat(02-09): add edhrec-role-grid expected-role-count command
  - bc7c8b39  # fix(02-09): validate grid input paths before the dry run returns
gates:
  build: "0 errors; 0 new warnings recorded"
  core_tests: "1749 passed / 0 failed"
  web_tests: "2095 passed / 0 failed / 16 skipped"
  eol: "new summary is LF; no code-file EOL churn in this docs commit"
verification: "review findings closed; reviewer independently re-verified the corpus figures with a quote-aware CSV parser and confirmed the plan's counts exactly"
---

# Phase 2 Plan 09 — Summary

The plan's three tasks landed in five commits because review found one Task-1 defect and one Task-3
defect after the first implementation pass. The final state stayed within the plan's code scope,
added the third EDHREC corpus arm without touching the main harness, and deliberately did **not** run
the grid against the live archive.

## 1. Public surface shipped

### `EdhrecCardCountsReader`

The shipped public surface is:

```csharp
public static class EdhrecCardCountsReader
{
    public static IReadOnlyCollection<string> ReadDistinctCardNames(
        string edhrecCsvPath,
        out int malformedRows);

    public static IReadOnlyDictionary<string, long> ReadSoloDenominators(
        string averagesCsvPath);

    public static EdhrecBulkGridResult Accumulate(
        string edhrecCsvPath,
        IReadOnlyDictionary<string, long> denominators,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardRoles,
        IReadOnlyCollection<string> roleKeys);
}

public sealed record EdhrecBulkCommanderTotals
{
    public required string Commander { get; init; }
    public required long Denominator { get; init; }
    public required int RowsConsumed { get; init; }
    public required double MaxRatio { get; init; }
    public required string MaxRatioCard { get; init; }
    public required double TotalInclusionRate { get; init; }
    public required IReadOnlyDictionary<string, double> ExpectedByRole { get; init; }
}

public sealed record EdhrecDenominatorMismatch
{
    public required string Commander { get; init; }
    public required string Card { get; init; }
    public required int Count { get; init; }
    public required long Denominator { get; init; }
    public required double Ratio { get; init; }
}

public sealed record EdhrecBulkGridResult
{
    public required IReadOnlyList<EdhrecBulkCommanderTotals> Commanders { get; init; }
    public required IReadOnlyList<EdhrecDenominatorMismatch> DenominatorMismatches { get; init; }
    public required IReadOnlyList<string> MissingDenominators { get; init; }
    public required int MalformedRows { get; init; }
    public required int DistinctCardCount { get; init; }
    public required long RowsRead { get; init; }
    public string? Failure { get; init; }
}
```

Two details matter for later phases:

- `DenominatorMismatches` did **not** stay `IReadOnlyList<string>`. Review found that shape satisfied
  the criterion's letter but broke D-03's actual reporting requirement, "report the worst five
  ratios," because prose strings would need to be parsed and re-sorted. `4cc6abdf` fixed that to the
  structured `EdhrecDenominatorMismatch` record, ordered descending by `Ratio`, so worst-five is
  `Take(5)`.
- This repeated the same flattening anti-pattern plan `02-07` D-04 had already forbidden for
  `knownMissedCards`: because this plan named the existence of a collection but not its shape, the
  first pass flattened structured evidence into strings again.

### `EdhrecBulkRoleExpectation`

The shipped figure surface is:

```csharp
public sealed record EdhrecBulkRoleExpectation : IRoleFloorFigure
{
    public required RoleFloorSource Source { get; init; }
    public required string Role { get; init; }
    public required string CommanderName { get; init; }
    public required double ExpectedCount { get; init; }
    public required long DeckCount { get; init; }
    public required int RowsConsumed { get; init; }
    public required double MaxCardInclusion { get; init; }
}
```

`RoleFloorSource` now has three explicit non-zero members: `Postgres = 1`, `Edhrec = 2`,
`EdhrecBulk = 3`.

`RoleFloorFigureTable.EdhrecBulkColumns` shipped as:

```csharp
["Source", "Commander", "Role", "Expected count", "Decks (denominator)", "Rows", "Max card inclusion"]
```

No percentile, deviation, z-score, effect-size, or ratio field exists on the bulk figure type or its
table. `MaxCardInclusion` is named that way on the **plan's own instruction**: Task 2 A.2 says to
prefer renaming the property over loosening the naive `"Ratio"` reflection gate, and the xmldoc says
so explicitly. This was not dodging a guard; it was the guard the plan required.

## 2. The one authorized test change

Exactly one pre-existing assertion changed in `RoleFloorFigureTests`: the member-count assertion for
`RoleFloorSource` changed from `2` to `3`, with an in-file comment citing plan `02-09` and stating
that the assertion exists to prevent a zero-valued default source, not to freeze the enum forever.

That change was authorized by plan `02-09` D-02 because the enum genuinely gained a third member.
Nothing else in the pre-existing assertions was weakened:

- `IRoleFloorFigure` stayed at exactly three members.
- The existing `EdhrecRolePointEstimate` reflection assertion stayed intact.
- A parallel no-distribution reflection assertion and JSON round-trip test were added for
  `EdhrecBulkRoleExpectation`.

## 3. Estimator, denominator gate, and parser reuse

The command emits the estimator exactly as the plan required:

```text
expected[C, R] = SUM over cards ( count[C, card] / denominator[C] ) x isRole(card, R)
```

The markdown and JSON both describe it as a **mean-style expected count**, not a percentile, and
state that it feeds neither a floor nor a go/no-go.

The `maxRatio` gate shipped with the plan's behavior:

- `maxRatio <= 1.0` means the solo-row `number_decks` denominator is internally consistent for that
  commander.
- `maxRatio > 1.0` is structurally impossible under a correct denominator, so that commander is
  recorded in `DenominatorMismatches` with commander, offending card, count, denominator, and the
  unclamped ratio, then excluded from `Commanders` and from the emitted grid.
- The grid report uses the now-structured mismatch rows directly and prints the worst five by
  descending ratio.

`EdhrecAveragesConverter` was **not** reused directly for `averages.csv`. `ReadSoloDenominators`
duplicates its quote-aware line parser and required-column discovery, but not the converter entry
point itself. That was a deliberate limitation of the existing API, not an oversight: the converter's
public surface is `Convert(string csvText, int minDeckCount = ...)`, which requires the entire file as
one in-memory string and applies baseline-specific filtering and deduplication logic. Plan `02-09`
D-05 required a streaming path, and Task 1 required solo-row denominator extraction rather than
baseline conversion, so the reader reimplemented the same quote-aware parsing approach line-by-line.

That distinction is worth recording because the first naive reviewer pass using `awk -F','` produced
**badly wrong** numbers: `938` solo rows and `2,875` commanders. Re-running with a quote-aware parser
confirmed the plan's figures exactly:

- `averages.csv`: `6,585` data rows = `3,372` solo + `3,213` partner.
- `edhrec.csv`: `14,150,219` data rows, `3,378` distinct commanders, `31,788` distinct cards.
- `_role-floor-research/cards_full.json`: `14,167` entries, implying `17,621` cache misses on a
  cold-ish cache.

That is a live demonstration of exactly the corruption D-05 warned about: commander and card names
contain commas, so naive comma splitting silently corrupts the corpus.

## 4. Reused path versus deliberate duplication

The plan required reuse of the production card-resolution path, but it also explicitly fenced off
`RoleFloorResearchCommandRunner.cs`. The shipped command followed that split exactly.

Reused unchanged:

- `IScryfallCardResolver` and its existing HTTP / throttling / 429 handling shape.
- The shared `_role-floor-research/cards_full.json` cache file.
- The same `name -> ScryfallCardData` cache JSON shape.
- `ScryfallCardFactMapper.ToCardFact`.
- `CutLabRoleAssigner.AssignRoles`.
- `CutLabRoleAssigner.RoleKeys` plus `RoleFloorGuards.FindTaxonomyDrift`.
- `SnapshotFileWriter.WriteLfFile` for artifact writing.

Not reused:

- `RoleFloorResearchCommandRunner.ResolveCardsAsync`, because it is a `private static` method inside
  a file four prior plans owned across four earlier waves.

So the **only duplicated part** was the batching loop around the same resolver and cache. The code
itself says this in the `ResolveCardsAsync` comment: the command reuses the resolver, the shared
`cards_full.json` file, and the same cache format; only the batching loop was duplicated because
extracting the private harness method in wave 6 would have been a merge hazard.

Follow-up recorded, not done here: unify
`RoleFloorResearchCommandRunner.ResolveCardsAsync` and `EdhrecRoleGridCommandRunner`'s batching loop
into one shared `DeckFlow.Core` component in a later phase.

## 5. The two review defects and their fixes

### Task 1 review defect

The first Task-1 implementation flattened denominator mismatches into `IReadOnlyList<string>`. That
met the acceptance criterion's narrow wording, "with all four values," but violated D-03's other
requirement to report the **worst five ratios** without reparsing prose. `4cc6abdf` fixed this by
introducing `EdhrecDenominatorMismatch { Commander, Card, Count, Denominator, Ratio }` and sorting
the collection descending by `Ratio`.

### Task 3 review defect

The first Task-3 implementation returned `0` from `--dry-run` **before** validating the input paths.
That meant the sanctioned pre-flight for plan `02-08` could print a nonexistent
`C:\\mnt\\c\\users\\...` path and still report success, contradicting the command's own
exit-`1`-on-missing-input contract. `bc7c8b39` fixed this by moving the `File.Exists` checks ahead of
the dry-run return. This matters specifically because `--dry-run` is the operator pre-flight plan
`02-08` is supposed to trust before any live Scryfall work.

## 6. Harness fence and no-run proof

`RoleFloorResearchCommandRunner.cs` was **not modified** by this plan. The landed five-commit range
touches only:

- `DeckFlow.Core/Research/EdhrecCardCountsReader.cs`
- `DeckFlow.Core.Tests/EdhrecCardCountsReaderTests.cs`
- `DeckFlow.Core/Research/RoleFloorFigure.cs`
- `DeckFlow.Core.Tests/RoleFloorFigureTests.cs`
- `DeckFlow.CLI/EdhrecRoleGridCommandRunner.cs`
- `DeckFlow.CLI/Program.cs`

The grid was also **not run** against the real archive in this worktree:

- no `EDHREC-ROLE-GRID.md` or `EDHREC-ROLE-GRID.json` artifact exists;
- `--dry-run` prints `archive read: skipped` and `scryfall calls: skipped`;
- reviewer fence check confirmed `_role-floor-research/cards_full.json` stayed unchanged at
  `8,220,503` bytes with its original mtime and `14,167` entries;
- reviewer fence check confirmed the two source archives stayed unchanged at their original sizes and
  mtimes;
- no live run means no Scryfall warming beyond the already-existing cache state.

That last point is important because D-04 deliberately deferred the expensive `~17,600` cache misses
to plan `02-08`, behind the operator checkpoint.

## 7. Plan deviations, stated as deviations

- The plan's `<success_criteria>` expects **three commits**, one per task. **Five** landed. The extra
  two are the two review-driven fixes above.
- Task 1's landed subject is
  `feat(02-09): stream EDHREC card counts with a denominator gate`, not the plan's longer
  `feat(02-09): add streaming EDHREC bulk card-counts reader with denominator gate`.
  Cause: the dispatching reviewer paraphrased the subject into the ticket instead of quoting the
  plan. Content was unaffected, so history was not rewritten over wording.

## 8. Requirement traceability gap

This arm still has **no ratified requirement ID**. That is deliberate.

- `RFLR-01` covers the Postgres corpus.
- `RFLR-11`, itself only **proposed** in plan `02-02`, covers the EDHREC average-decks bracket grid.
- This `edhrec.csv` expected-role-count arm is a third corpus with a third estimator and is covered
  by neither.

The plan therefore recorded **RFLR-13 (PROPOSED — NOT RATIFIED)** as a governance follow-up only:
per-commander expected role counts may be estimated across the full EDHREC bulk card-counts archive
as a breadth instrument, provided every such figure is labelled a mean-style expected count from its
own named source, is structurally incapable of appearing as a percentile, and never feeds a floor
recommendation.

That is a proposal only. This summary does **not** cite it as ratified.

## 9. Final state

The final shipped state matches the plan's intended boundaries:

- the third corpus arm exists and is source-discriminated as `EdhrecBulk`;
- its figure type is structurally incapable of carrying a percentile;
- the estimator is emitted as a mean-style expected count;
- denominator failures are surfaced, not hidden;
- the production classifier and resolver path are reused, with only the batching loop duplicated;
- the main role-floor harness stayed untouched;
- and the live operator run remains deferred to plan `02-08`.
