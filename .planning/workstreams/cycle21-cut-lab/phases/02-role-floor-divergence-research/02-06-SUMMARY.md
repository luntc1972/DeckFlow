---
phase: 02-role-floor-divergence-research
plan: 06
wave: 4
status: complete
executed: 2026-07-27
commits:
  - 4eb471e4  # feat(02-06): add EdhrecCellReader over the fetcher's on-disk cell shape
  - 610ae71d  # feat(02-06): classify EDHREC cells through the production role assigner
  - 4dbdbab6  # feat(02-06): add --edhrec-data option to role-floor-research
  - 168f8ab7  # fix(02-06): name the connection-string environment variable in --help again
  - 8092c163  # docs(02-06): correct the connection-string grep criterion in Task 3
  - 840e524a  # test(02-06): move the EDHREC quantity tally into Core and enforce it
gates:
  build: "0 errors; 9 pre-existing CS8629 warnings (all in ManabaseBaselineWeightingTests.cs), 0 new"
  core_tests: "1736 passed / 0 failed (1715 at wave start; 1715 → 1728 → 1736)"
  web_tests: "2095 passed / 16 skipped / 0 failed — unchanged"
  eol: "every touched file LF before and after; zero churn"
verification: "blind foreman-verifier PASS on all items A–M, over the four code commits together"
---

# Phase 2 Plan 06 — Summary

Three plan tasks, plus a help-text repair, a plan correction, and a lead-added Core-tests task closing
a gap blind verification found. No user-visible change; no CalVer bump, no tag.

**No network call was made** — not to EDHREC, not to Scryfall, not to any database. The corpus was
already on disk from plan `02-02`'s completed fetch.

## 1. Did the on-disk shape match the plan?

**Yes, with no discrepancy.** `scripts/edhrec-brackets/README.md`'s "On-disk contract" agrees with the
plan and with independent measurement. Verified read-only against
`_edhrec-brackets/cells/adrix-and-nev-twincasters__*.json` and `manifest.json`:

- keys are **snake_case**: `slug`, `bracket`, `bracket_index`, `n_decks`, `deck`, `land`, `basic`,
  `nonbasic`, `savedate_summary`;
- `deck` is a raw `string[]` of `"<qty> <Card Name>"` — no pre-parsed `cards` array;
- there is **no `source` and no `estimateKind` field**, contrary to an earlier draft. No validation was
  written for either; `Source` is supplied by the CLI at projection time, which is where criterion 7's
  guarantee actually lives.

`EdhrecCellReader` binds with `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` plus
`PropertyNameCaseInsensitive = true`. The camelCase `JsonOptions` already in the runner is the WRONG
policy for these files and was deliberately not reused; a test pins that `bracket_index` and `n_decks`
actually bind.

## 2. The selection-floor / cell-floor trap

`manifest.json` carries **`min_decks: 8000`** — the commander-**selection** floor, applied against
`averages.csv`'s `number_decks` when choosing which 305 commanders to sweep. **It is not a cell floor.**
The per-cell qualifying floor is `n_decks >= 400`, read from each cell's own `n_decks`.

The two differ by two orders of magnitude on real data: the sample cell above carries `n_decks: 11`.
Conflating them would silently change which cells count, and nothing would fail.

`min_decks` appears nowhere in qualification logic — only in explanatory comments at
`EdhrecCellReader.cs:352` and `RoleFloorResearchCommandRunner.cs:37`. The reader's
`ManifestDocument` has no `MinDecks` property at all, so it cannot be read by accident.

## 3. The two new constants

```csharp
// Why: the 2026-07-16 prior set the per-cell EDHREC floor at 400 decks backing the cell, and
// the manifest's min_decks: 8000 is the commander-selection floor for which commanders were
// fetched, not the per-cell qualifying floor used by this harness.
private const int EdhrecMinCellDeckCount = 400;
private const int EdhrecThinBracketThreshold = 50;
```

Declared once, in the constant block beside the statistical thresholds. No second `400` literal exists
in the EDHREC path.

## 4. `RunAsync` signature after adding `edhrecDataPath`

```csharp
public static async Task<int> RunAsync(
    string? connectionString,
    int minDeckCount,
    string mode,
    string cardsCachePath,
    string outputPath,
    string outputJsonPath,
    string? edhrecDataPath = null,
    CancellationToken cancellationToken = default)
```

`edhrecDataPath` precedes `CancellationToken`, per the convention plan `02-01` enforced.

## 5. The quantity divergence — and why each path is correct

**This is the most consequential line in the EDHREC path.** The fact is built with
`ToCardFact(card, quantity: 1, isCommander: false)` at `:308`, but the tally adds `cardEntry.Quantity`
at `:321`. Two different numbers, two lines apart.

| Path | Tally | Why it is correct there |
|---|---|---|
| Postgres | `roleCounts[role]++` | Commander is singleton for every nonland card that can plausibly earn a target role |
| EDHREC | `+= cardEntry.Quantity` | EDHREC's `deck` array carries real basic-land quantities |

Measured: an 82-entry `deck` list summing to 100 cards with EDHREC's own `basic` aggregate at 20 — so
roughly a dozen entries carry quantity > 1. Since decision D-C put `lands` in scope, quantity-1 would
undercount an average deck's lands by that margin, leave every other role correct, and pass the whole
suite. `lands` is the calibration control, so the failure would corrupt precisely the figure the phase
exists to measure. Both paths' reasoning is recorded in the emitted methodology, not buried.

**It is now test-enforced, not eyeball-verified.** Blind verification found that nothing in the repo
would have caught a `+= 1` regression before the live run — `EdhrecCellReaderTests` covers the reader,
not the runner's tally, and D-03's land self-check is a report, not a gate. Under the standing user
rule that console-app additions carry `DeckFlow.Core.Tests` coverage, the tally moved to
`DeckFlow.Core/Research/EdhrecRoleTally.cs`:

```csharp
public static IReadOnlyDictionary<string, int> TallyRoleCounts(
    IReadOnlyCollection<string> targetRoles,
    IEnumerable<(IReadOnlyList<string> Roles, int Quantity)> classifiedCards)
```

Pure — no classifier, no `DeckFlow.Web` type, no IO. Eight tests, including the one that matters:
a card classified `["lands"]` with `Quantity = 9` must contribute **9**. Bite-proved by flipping the
production line to `+= 1`:

```
Failed TallyRoleCounts_AddsQuantityRatherThanOneForSingleRole
Assert.Equal() Failure: Values differ
Expected: 9   Actual: 1
```

Zero quantity contributes 0; negative quantity subtracts as an arithmetic delta. The negative case is
**unreachable from real data** — `EdhrecCellReader.cs:411` rejects `quantity < 0` as a parse failure —
but is asserted so the helper's contract is explicit.

## 6. Quantity-parse failures observed

**Zero on the real corpus.** The reader's reality check over all 1,525 cells reported
`invalid 0, missing 0, unexpected 0`. Parse failures are exercised in unit tests instead: an entry of
`"Forest"` (no quantity prefix) lands in that cell's `ParseFailures` with its raw string and is
EXCLUDED from `Cards`, while the cell is still returned. A silent drop would undercount a role and look
like a real measurement.

**31 card-count anomalies** were observed — cells whose summed quantities do not total 100. They are
returned with their real sum and recorded in `CardCountAnomalies`; the reader reports, it does not
judge. Plan `02-07` decides whether an off-100 "average deck" belongs in a lands comparison.

## 7. One classifier for both corpora (RFLR-01)

`CutLabRoleAssigner.AssignRoles(fact, [], isComboPiece: false, resolvedMode)` — identical shape,
empty category list included — at `:273` (Postgres) and `:309` (EDHREC).

**Note for future greps:** a third call exists inside the taxonomy-drift probe at `:896-897`, split
across lines, so `grep -c 'CutLabRoleAssigner.AssignRoles'` returns **2**, not 3. The count is right
for the wrong reason, and an earlier report mislabeled which two sites it matched. Confirm the Postgres
loop by reading it, not by counting.

Both corpora also share one Scryfall resolution pass (D-04): EDHREC's distinct card names are folded
into `distinctCardNames` before `ResolveCardsAsync`, so one cache and one rate-limit budget serve both,
and EDHREC ingestion adds close to zero API calls for names already seen.

## 8. Per-bracket support labels — computed, never hardcoded

```csharp
private static string BuildEdhrecSupportLabel(int qualifyingCount)
{
    if (qualifyingCount <= 1)
    {
        return "NOT REPORTED — insufficient cells";
    }

    if (qualifyingCount < EdhrecThinBracketThreshold)
    {
        return FormattableString.Invariant($"THIN — {qualifyingCount} qualifying cells");
    }

    return "reported";
}
```

No bracket-specific branch exists. On the 2026-07-27 corpus:

| Bracket | Index | Fetched | Qualifying | Median backing decks | Support |
|---|--:|--:|--:|--:|---|
| exhibition | 1 | 305 | **1** | 36 | `NOT REPORTED — insufficient cells` |
| core | 2 | 305 | 284 | 1,138 | `reported` |
| upgraded | 3 | 305 | **305** | 1,048 | `reported` |
| optimized | 4 | 305 | 175 | 458 | `reported` |
| cedh | 5 | 305 | 40 | 51 | `THIN — 40 qualifying cells` |

A per-bracket figure computed over one cell is a single deck's number wearing the costume of an
average. Because the labels are pure functions of the counts, a re-fetch at a different `--min-decks`
relabels automatically instead of lying.

Independent corroboration reached without reference to this run:
`ManabaseAnalysisService.cs:603-605` already restricts EDHREC-derived land use to brackets 2-3 on the
grounds that the EDHREC population is casual-dominated, and the committed
`DeckFlow/Data/manabase-baseline/latest.json` carries bracket rows for 2, 3, 4 and 5 only — **no B1
row at all.**

## 9. What plan 02-07 can read directly

Exposed on `ResearchComputation` so `02-07` reads rather than recomputes:

- `EdhrecPointEstimates` — every cell projected per role, including `Qualifies == false` ones
  (720 of 1,525 fall below the floor; dropping them would make coverage unreportable);
- `EdhrecCoverage` — `CellsFetched`, `CellsQualifying`, `CellsMissing`, `InvalidCells`,
  `UnexpectedCells`, `CommandersReached`, `MinCellDeckCount`, `MinSaveDate`, `MaxSaveDate`;
- `EdhrecCoverage.Brackets` — per-bracket `CellsFetched`, `CellsQualifying`,
  `MedianBackingDeckCount`, `SupportLabel`, **so a `NOT REPORTED` bracket's figures can be excluded
  from the lands comparison by construction rather than by remembering to**;
- `EdhrecCoverage.LandSelfChecks` — per cell `CellId`, `EdhrecLandCount`, `HarnessLandCount`, `Delta`;
- `EdhrecParseFailureCount` and `EdhrecCardCountAnomalyCount`.

### The land self-check (D-03) cannot be read yet

The emitted `### EDHREC land self-check` block carries exact-match / within-one / diverged-by-more
counts and the worst five divergences — **and a caveat sentence stating the comparison is only
meaningful after a full Scryfall resolution pass.**

Offline sampling showed deltas of −14 to −20, **entirely explained by card-cache coverage, not by the
quantity rule**:

| Cell | EDHREC land | Harness lands | Delta | Unresolved names | Unresolved qty | Explained |
|---|--:|--:|--:|--:|--:|---|
| `adrix…__exhibition` | 37 | 19 | −18 | 42 | 51 | 100% |
| `adrix…__core` | 35 | 17 | −18 | 43 | 51 | 100% |
| `adrix…__optimized` | 35 | 16 | −19 | 46 | 54 | 100% |
| `adrix…__cedh` | 31 | 17 | −14 | 50 | 54 | 100% |

`_role-floor-research/cards_full.json` (14,167 entries) lacks `island`, `mountain`, `plains`, `swamp`
and `sol ring`, while holding `forest` and `arcane signet` — the cache was built from the Postgres
corpus's distinct names, which evidently do not carry most basics. Those names resolve in a real run
because D-04 folds them into the same resolution pass. **Plan `02-08`'s live run is the first moment
D-03 yields a real reading.**

## 10. Deviations and corrections

**A. A defective acceptance criterion caused a real regression.** Task 3 demanded
`grep -c 'DECKFLOW_ROLE_FLOOR_CONNECTION_STRING' DeckFlow.CLI/Program.cs` return 0. Its intent was
"no second place READS the variable" — but plan `02-04` deliberately NAMES it in two `--help` strings,
which is a mention, not a read. The only way to satisfy the grep was to delete the name (`4dbdbab6`
took the count 2 → 0), leaving `--help` saying "the runner's dedicated environment-variable fallback"
without saying which one. That undercuts D-07's purpose: keeping a credential off the command line
only helps if an operator can discover the variable. Restored at `168f8ab7`; criterion corrected at
`8092c163` to assert on the read site instead.

**Third defective grep criterion in this phase**, after Task 1's `"interaction",` and plan `02-05`'s
`cycle21-cut-lab == 2`. **Assert on the construct, not on a file-wide substring count** — a name can
appear for more than one reason, and a count cannot tell them apart.

**B. A lead stop-condition was mis-specified and blocked the wave once.** The dispatch required a live
land self-check while forbidding network calls — but the check is meaningless without a resolution
pass, so deltas of −18 to −20 were guaranteed. Codex stopped as instructed and correctly diagnosed the
cause as cache coverage rather than the quantity rule. Requirement replaced with an
unresolved-coverage breakdown; D-03's real reading deferred to `02-08`. **A stop condition no correct
implementation can satisfy is as much a defect as a missing one.**

**C. A leaked `testhost` process** held file locks on the test output DLLs
(`MSB3027 … The file is locked by: "testhost (10924)"`), which looked like a hang. Recovery, now
recorded in the ledger: `dotnet build-server shutdown`, check for a stray `testhost`, retry.

## 11. Next

Wave 5 — plan `02-07`: the lands calibration verdict against the 2026-07-16 prior, the protection
under-detection disclosure, and the no-go findings template. It reads §9's exposed surfaces, and must
weigh both the 31 card-count anomalies and the fact that B1 carries no reportable support.
