# Code-review gate — `feat/category-weights`

- **Branch:** `feat/category-weights` @ `93f1ad8d2`
- **Merge-base with `main`:** `8596b7d9` (2026-07-16)
- **Scope:** 1 commit, 19 files, +844 / −33
- **Review worktree:** `../deckflow-cat-weights-review` (detached, read-only)
- **Reviewed:** 2026-08-16 by Claude (lead). Codex stage 1 (`gpt-5.6-sol`, medium) — see § Stage 1.
- **Status:** findings recorded; stage 1 pending at time of writing.

## Why this gate was owed

The branch had **no code review of any kind**. The 2026-08-16 review-gate inventory listed it as the
most orphaned branch in the repo with "zero record anywhere in `.planning/`".

**That claim was wrong and is corrected here:** the branch carries its own
`.planning/category-weights/PLAN.md` (187 lines, with locked decisions, non-goals and an explicit
correctness guard). The inventory missed it because it grepped `.planning/` on `main`, and the plan
only ever existed on the branch. The branch is *unreviewed*, not *unplanned* — a materially different
thing, and the plan turned out to be the most useful review instrument available.

## What it does

Adds a weighted table above the copy box on `/suggest-categories`, showing per suggested category:
crawl popularity (`Decks` + `%`) and source agreement (`N/M`). Copy output is unchanged — weights are
on-screen only.

## Claim-vs-code verification of the plan's own guards

The plan flags a specific bug class ("the DeckShare `>100%` class", fixed once already in `a3b52ba3`):
`card_category_observations JOIN cards ON normalized_card_name` sums across printings, because
multiple `card_id` share one normalized name, so a per-category numerator can exceed the card's deck
total. It sets three requirements. Verified each against the code:

| Plan requirement | Verdict | Evidence |
|---|---|---|
| Numerator uses the same distinct-deck counting as the denominator | **Met in effect, not as worded** | `CardCategoryRepository.GetCategoryDeckCountsAsync` still uses `SUM(o.deck_count)` across printings — but so does the denominator `GetCardDeckTotalsAsync` (`SUM(t.deck_count)`, same normalized-name fan-out). The new `JOIN card_deck_totals ON source_id, card_id, board` pins numerator and denominator to the **same grain**, which is what actually bounds the ratio. `ux_totals_grain` is UNIQUE on `(source_id, card_id, board)` (`CategoryCacheSchema.cs:97`), so the join cannot multiply rows. |
| Clamp rendered percentage to `[0,100]` | **Met** | `DeckCategoriesController.BuildCategoryWeightRow`: `Math.Clamp(percent, 0, 100)`. |
| `TotalDeckCount == 0` renders `—`, no divide-by-zero | **Met** | same method: `\|\| totalDeckCount <= 0` returns a row with null `DeckCount`/`Percent`. |

**Board-filter check (not in the plan, the residual hole):** `GetCardDeckTotalsAsync` takes an
optional `boardFilter`. If the denominator were board-filtered and the numerator were not, the ratio
could exceed 100% regardless of the grain join. `CategorySuggestionService` calls it with
`cancellationToken:` named and **no** `boardFilter`, so both sides span all boards. Bounded. Had a
future caller passed a board filter, the clamp would silently mask a wrong number — worth a comment
at the call site, but not a defect today.

**Verdict on the guard: implemented, defence-in-depth intact.**

## Copy-path non-regression (the plan's headline non-goal)

Non-goal: *"No change to `CategorySuggestionReporter.Merge` / `ToText` (the copy-text path)"* and
*"The Copy box is unchanged."*

The commit **does** rewrite `Merge` — it now delegates to the new `MergeWeighted` — and the
controller now builds `MergedCategoriesText` from `MergeWeighted`'s output. On its face that breaks
the non-goal. Verified it does not change behavior:

- `MergeWeighted` reuses the identical ordering triple: `OrderByDescending(SourceCount)` →
  `ThenByDescending(Authority)` → `ThenBy(DisplayLabel, OrdinalIgnoreCase)`.
- It reuses the same `MergeSource` / `MergeEntry` internals and the same junk filter and
  canonical-key dedup, now extracted to `GetSourceEntries` with no logic change.
- `MergeSource`'s new `if (sourceEntries.Count == 0) return 0;` early-exit skips a `foreach` over an
  empty list — a no-op.

**Label sequence is therefore identical; the copy text is unchanged.** The non-goal is honoured in
substance. Restructuring `Merge` to share one pass is a net improvement over two divergent orderings.

## Findings

### LOW-1 — `SourceTotal` counts post-junk-filter sources, plan said "non-empty inputs"

Plan: *"`SourceTotal` = count of the four inputs that were non-empty."* Implementation returns `1`
from `MergeSource` only when `sourceEntries.Count > 0`, i.e. after `CategoryFilter.IsJunk` filtering
and canonical dedup. A source returning only junk contributes `0` to `M`.

The implementation is **better** than the plan (a source that contributed nothing to the merge should
not inflate the denominator of an agreement ratio) and matches its own xmldoc, *"the total number of
sources that contributed at least one merged category"*. Recorded as a deliberate, documented
deviation, not a defect. No action.

### LOW-2 — `%` column renders a bare integer with no unit in the cell

`SuggestCategories.cshtml`: `<th>%</th>` with `<td>@row.Percent</td>` renders `42`, not `42%`. The
unit lives only in the header. Legible in a table, but the plan notes the Commander Categories page
renders `% of decks`, so the two surfaces now read differently. Cosmetic.

### LOW-3 — table reuses `class="conflicts-table"`

A class named for the deck-conflicts feature now styles category weights. Semantic mismatch, and it
couples this table's appearance to unrelated future edits of that class.

Worth noting the **upside**: reusing an existing class means the commit adds **no CSS at all**, which
sidesteps the project's standing hazard that guild themes are standalone CSS forks and layout must
land in `site-common.css`. A new class here would have needed care; reuse needed none. If renamed
later, the new rule belongs in `site-common.css`.

### INFO-1 — no feature flag, as the plan anticipated

Plan non-goal: *"No new feature flag (additive, low-risk display; copy path untouched). Reviewer may
veto and request a `tool.categories.*` flag if warranted."*

**Not vetoing.** The copy path is verified unchanged, the table renders only when
`WeightedCategories.Count > 0`, and every unavailable metric degrades to `—`. Worst case on bad data
is a wrong percentage in a display column, which the clamp bounds. A flag is not warranted.

### INFO-2 — positional record gained a parameter mid-list

`CategorySuggestionResult` gained `IReadOnlyDictionary<string,int> CategoryDeckCounts` **before**
`CardDeckTotals`. Any positional construction with the old arity fails to compile, so the compiler is
the enforcement; no silent runtime breakage is possible. Test doubles were updated in the same commit
(`FakeCategoryKnowledgeStore`, and 6 test files touched). No action.

## Mergeability — the dominant practical risk

The branch is **1 ahead / 935 behind** `main`. `git merge-tree` reports exactly one conflict:

```
CONFLICT (content): DeckFlow.Core/Knowledge/CardCategoryRepository.cs
```

Auto-merging succeeds for `CategoryKnowledgeRepository.cs`, `CategoryKnowledgeStore.cs`,
`SuggestCategories.cshtml`, `Help/category-suggestions.md`.

**The conflict is textual, not semantic.** Verified every API the branch depends on still exists on
today's `main` — `GetCardDeckTotalsAsync`, `CategoryFilter.IncludedOrFallback`,
`CategoryCanonicalizer.CanonicalKey`, and both the `card_deck_totals` and
`card_category_observations` tables. `CategoryCacheSchema.cs` is **unchanged** since the merge-base.
`main`'s +105 lines in the conflicting file are **additive** — `GetGlobalCategoryBaselineAsync`,
`GetCategoryDeckMembershipForCommanderAsync`, `BuildCategoryPairKey` — landing near the same region.

So the rebase is a mechanical adjacency resolution, not a redesign.

## Not verified — required before merge

1. **Build and test run.** No build or test execution was performed against the branch in this
   review; all findings above are static. The suite has moved ~935 commits, so a rebase-then-run is
   mandatory before merge, not optional.
2. **Mobile/theme UAT.** A 4-column table at 375px was not checked, and per the project rule any web
   change needs a 2-viewport pass. `conflicts-table`'s responsive behavior is inherited, not
   verified.
3. **Live `>100%` behavior.** The bound is argued from schema uniqueness and query shape, not
   observed against real crawl data. The clamp makes a breach invisible; if this matters, log when
   the clamp actually fires.

## Recommendation — DO NOT MERGE

⚠ **This section was revised after stage 1 returned.** The lead's static pass concluded "no BLOCK, no
HIGH"; that verdict was wrong and is superseded. See BLOCK-1.

**1 BLOCK, 1 MEDIUM, 3 LOW.** What still stands from the static pass: the plan's stated correctness
guard is genuinely implemented, the copy path is verifiably unchanged, the grain-pinning join works
and is properly tested, and the code follows repo conventions. None of that survives BLOCK-1, which
is a crash rather than a wrong number.

Path to landing, in order:

1. ~~**Fix BLOCK-1**~~ — ✅ DONE `cbed2adf`, verified. Core.Tests 1552/0/0.
2. ~~**Fix MEDIUM-1**~~ — ✅ DONE 2026-08-17 on `fix/category-weights-medium1`, verified. See
   § MEDIUM-1 discharge below. Core.Tests 2018/0/0, full suite 4768/0.
3. ~~Rebase onto `main`~~ — ✅ DONE 2026-08-17. The single adjacency conflict in
   `CardCategoryRepository.cs` was positional (both sides inserted a new method at the same point);
   resolved by keeping both. Two `ThrowingCategoryKnowledgeStore` doubles that appeared on `main`
   during the gap needed the new interface member stubbed.
4. ~~Run the full suite~~ — ✅ DONE. 4766/0 at ff time, 4768/0 after MEDIUM-1. ff'd to `main`
   (`8a87876d`), **unpushed**.
5. 2-viewport UI pass on the new table. ⚠ STILL OPEN.
6. Review the fix, with the stage-2 sweep folded in (see § Stage 2). ⚠ PARTIAL — the MEDIUM-1 fix was
   reviewed (one defect found and fixed, below); the stage-2 sweep for other case-sensitive
   `GROUP BY` / case-insensitive container pairings is **not** done.

## MEDIUM-1 discharge — 2026-08-17

**Fix.** `GetCategoryDeckCountsAsync` SQL now selects and groups by
`(source_id, card_id, board, category)` instead of collapsing by `category` alone. The canonical fold
moved into C#: filter raw labels through `CategoryFilter.IncludedOrFallback`, take `Max` per
`(grain, canonicalKey)`, then `Sum` those maxima across grains. `CategoryCanonicalizer` is C#-only, so
the per-grain collapse cannot live in SQL. `ORDER BY LOWER(o.category)` dropped — the return is a
dictionary, ordering was never load-bearing, and this removes the collation risk the review flagged.

**RED proof, independently re-run by the lead** (not taken on the executor's word): reverting only
`CardCategoryRepository.cs` to pre-fix leaves exactly 1 of 24 failing —
`GetCategoryDeckCountsAsync_WhenOneGrainCarriesTwoCanonicalAliases_CountsGrainOnce`,
`Expected: 3 / Actual: 5`. Inflated count, not a compile or wrong-key artifact.

**Defect found in the fix itself, and fixed:** the new `CategoryDeckCountRow` DTO first typed
`SourceId` as `string`, but `source_id` is `INTEGER NOT NULL` (`CategoryCacheSchema.cs:152`) and this
file's own `ResolveSourceIdAsync` / `ResolveSourceIdForReadAsync` return `long`. Tests passed anyway
because SQLite is dynamically typed and Dapper coerces; **Postgres — which prod runs — would have
thrown at runtime.** Green tests, prod crash. Corrected to `long`.
⭐ Standing lesson: a SQLite-backed test suite cannot prove a Dapper column-type mapping. Check new
DTO property types against the schema by hand.

**Tests.** 2 added, both `[Fact]` (a previous gate found a RED proof voided by `[Theory]`):
`..._WhenOneGrainCarriesTwoCanonicalAliases_CountsGrainOnce` (the RED) and
`..._WhenAliasesSpanGrains_SumsAcrossGrains` (cross-grain regression guard — passes pre-fix by design).
Both pre-existing tests pass **unchanged**; the test file diff is purely additive, zero deletions.

**Still open from this finding's neighborhood:** the commit body's "same-grain canonical-alias
overcount" note can now be dropped — that was this bug.

The three LOW findings remain optional polish and should not gate the merge.

## Stage 1 — Codex `codex review --base 8596b7d9`

Seat `gpt-5.6-sol`, effort `medium` (owed gate with no prior independent review). Log:
`<scratchpad>/catw-stage1.log` (2096 lines).

Returned 2 findings, both in `CardCategoryRepository.GetCategoryDeckCountsAsync`. **Both independently
verified against the repo and both are real.** They land in code the lead reviewed and passed — see
§ Review-process note.

### BLOCK-1 — ✅ FIXED `cbed2adf` (2026-08-17)

Folded case variants before keying, in memory, leaving the SQL untouched:

```csharp
// Why: SQL groups case-sensitively while this dictionary is case-insensitive, so fold rows before keying.
var countsByCategory = rows
    .GroupBy(row => row.Category, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(
        group => group.Key,
        group => checked((int)group.Sum(row => row.DeckCount)),
        StringComparer.OrdinalIgnoreCase);
```

Summed as `long`, narrowed once — casting per row to `int` before summing would have reintroduced an
overflow window. SQL left dialect-neutral deliberately: `GROUP BY LOWER(...)` would have put collation
behavior back in play across SQLite/Postgres.

**Verified independently of Codex's self-report** (Codex reported the red but could not produce suite
totals in either direction):

- Reverting **only** the production hunk reproduces the predicted crash exactly:
  `System.ArgumentException : An item with the same key has already been added. Key: draw`
- With the hunk reverted, the **pre-existing** `…_ReturnsCanonicalizedCountsAtCardDeckTotalsGrain`
  still **passes** — direct proof that the old test was blind to this defect, as claimed above.
- `DeckFlow.Core.Tests` with the fix: **1552 passed / 0 failed / 0 skipped**. No pre-existing failures
  on this 935-behind branch, which answers the baseline question Codex left open.
- Scope exactly 2 files. 0 CR bytes both files. (The 1-line gap between the normal and
  `--ignore-all-space` diffstats is the re-indentation of the `.ToDictionary` arguments, not EOL churn.)

Original finding retained below for the record.

### BLOCK-1 (stage 1 P1) — `ToDictionary` throws on case-variant category labels

`CardCategoryRepository.cs:88-91`:

```csharp
var countsByCategory = rows.ToDictionary(
    row => row.Category,
    row => checked((int)row.DeckCount),
    StringComparer.OrdinalIgnoreCase);
```

The result set is grouped `GROUP BY o.category`, which is **case-sensitive** under both SQLite
(BINARY collation) and Postgres. The dictionary keys it **case-insensitively**. Two rows differing
only by case therefore collide and `ToDictionary` throws
`ArgumentException: An item with the same key has already been added`.

Reachability confirmed — three independent facts:

1. **The write path stores category labels raw.** `card_category_observations` is inserted with a bare
   `@category` parameter (`:689`); there is no canonicalization or case-folding on write.
2. **The schema lets case variants coexist.** `ux_obs_grain` is UNIQUE on
   `(source_id, card_id, category, board)` (`CategoryCacheSchema.cs:93`) — case-sensitive, so `'Draw'`
   and `'draw'` are distinct rows. They also coexist trivially across different `source_id`s.
3. **Category labels are freeform user text** harvested from crawled Archidekt decks, so case variants
   of the same label are the expected condition, not an exotic one.

Effect: an **unhandled exception on the suggestion path** — not a wrong number — for any card whose
crawl history contains two case-spellings of one category. Both the cached-data path and "All
sources" fail.

Note the shape: **the sibling `GetCategoriesAsync` (`:44-58`) does not have this bug.** It runs the
same case-sensitive `GROUP BY o.category` but returns `IEnumerable<string>` and never builds a
case-insensitive dictionary over it. The new method introduced the crash by adding an
`OrdinalIgnoreCase` container over a case-sensitively-grouped result — one side of a comparison
changed without the other.

**Fix direction:** aggregate case-insensitively before materializing — either fold to
`GROUP BY LOWER(o.category)` in SQL, or group the rows in memory and sum, instead of `ToDictionary`.

### MEDIUM-1 (stage 1 P2) — canonical aliases summed at the same grain can overcount `Decks`

`CardCategoryRepository.cs:101-104`. `CategoryCanonicalizer` maps `"card draw"` → `"Draw"`, so stored
labels `'Card Draw'` and `'Draw'` are distinct rows that the canonical fold then **sums**. Across
different decks that is correct and desirable. Within one `(source_id, card_id, board)` grain — a deck
carrying both alias labels for the same card — the same decks are counted twice.

The `Math.Clamp(percent, 0, 100)` guard hides the ratio error but **not the `Decks` column**, which
renders the raw inflated integer. Narrower than BLOCK-1: it needs one deck to carry both aliases for
one card.

**Fix direction:** collapse to canonical key at the `(source_id, card_id, board)` grain, taking a max
rather than a sum, before aggregating across grains.

### Test coverage — green, and blind to both

`CategoryKnowledgeRepositoryTests.GetCategoryDeckCountsAsync_ReturnsCanonicalizedCountsAtCardDeckTotalsGrain`
is a **good** test of the grain-pinning guard: it persists `'Draw'` with `deckCountIncrement: 99` for
`deck-3` and deliberately omits `PersistCardDeckTotalsAsync`, then asserts `counts["draw"] == 5`. The
99 being excluded is direct proof the `JOIN card_deck_totals` works. Credit where due.

But it exercises **neither** new finding:

- Its alias pair is `'Card Draw'` vs `'Draw'`, which differ by more than case. They are distinct
  under `OrdinalIgnoreCase` too, so no duplicate key arises and BLOCK-1 never fires.
- Its alias rows sit in **different** `source_id`/`board` grains (`deck-1` mainboard, `deck-2`
  sideboard), so summing them is legitimate and MEDIUM-1 never fires.

Its `Assert.True(counts.Values.All(count => count <= totals.TotalDeckCount))` reads like an
invariant guard but is satisfied by construction (5 ≤ 8). Another instance of a fixture that bounds
the wrong population.

**Required with the fix:** a case-variant case (`'Draw'` + `'draw'`, same card) that currently throws,
and a same-grain alias case (`'Card Draw'` + `'Draw'` under one `source_id`/`board`) that currently
over-counts.

## Stage 2 — not run, deliberately

CLAUDE.md condition 4 (normalization/matching/comparison logic changed) did hold, so stage 2 was
warranted. It is **not** being run, because stage 1 already surfaced that exact defect class and a
confirmed BLOCK now exists: more review of code that must change is waste. Fold the stage-2 sweep
into the review of the fix instead, and point it at the remaining question — whether any *other*
consumer of `card_category_observations` pairs a case-sensitive `GROUP BY` with a case-insensitive
container.

## Review-process note

The lead's static pass verified the plan's three stated guards, the copy-path non-regression, the
join cardinality via `ux_totals_grain`, and the board-filter hole — and **passed the file that
contains both defects.** It never checked the `ToDictionary` comparer against the `GROUP BY`
collation.

That is precisely the failure mode CLAUDE.md's stage-2 condition 4 describes — "updating one side of
a comparison and not the other is invisible to a diff-scoped reviewer" — and here the diff-scoped
reviewer was the one that caught it. Reviewing against the author's stated guards is efficient but
inherits the author's blind spots: neither the plan nor the lead's pass asked what happens when two
rows differ only by case. Worth carrying: **when a change introduces a keyed container over a SQL
result set, check the comparer against the query's collation explicitly** — it is not visible from
either side alone.
