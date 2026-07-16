# Plan: Weighted categories on the Suggest Categories tool

Branch `feat/category-weights` (worktree `../deckflow-cat-weights`, off main 8596b7d9).

## Goal

On the **Suggest Categories** deck tool (`/suggest-categories`), show a weighted
table above the existing copy box so the user can see, per suggested category:
- **Decks + %** — crawl popularity (`deck_count` for that category ÷ the card's total
  decks). Only available for categories present in the cached store; `—` otherwise.
- **Sources** — `N/M` agreement, where **M** = number of suggestion sources that
  returned anything this run, **N** = how many of them picked this category.

Sort: **SourceCount desc, then % desc (nulls last), then name**.

**The Copy box is unchanged** — it still copies plain `- Category` lines
(`MergedCategoriesText`). Weights are on-screen only.

## Decisions (from brainstorming, locked)

- Surface: Suggest Categories tool only for new work.
- Metrics: Decks + % (popularity) AND Sources N/M (agreement).
- Copy output: plain category names only (weights never enter the paste).
- Commander Categories page: **no change** — it already renders `% of decks` + `Decks`,
  has no copy button, and the 25-row + overflow cap is kept as-is.

## Non-goals

- No change to `CategorySuggestionReporter.Merge` / `ToText` (the copy-text path).
- No change to the Commander Categories page.
- No new feature flag (additive, low-risk display; copy path untouched). Reviewer may
  veto and request a `tool.categories.*` flag if warranted.
- `SuggestionsApiController` JSON response stays unchanged (out of scope; web view only).

## Data-availability reality (drives the `—` handling)

The tool merges four per-card sources: reference-deck (exact), Scryfall Tagger, cached
store (inferred), EDHREC (fallback). Only the **cached store** carries a deck count.
So % popularity exists ONLY for categories seen in the crawl; Tagger/EDHREC-only
categories render `—` for Decks and %. Sources `N/M` is available for every category.

## Correctness guard (MUST — repeat of the DeckShare `>100%` class of bug)

Per-category `deck_count` via `SUM(o.deck_count)` over
`card_category_observations JOIN cards ON normalized_card_name` **sums across printings**
(multiple `card_id` share a normalized name) and can exceed the card's distinct-deck
total → `% > 100`. The DeckShare fix (`a3b52ba3`) already hit this.
Requirements:
- Compute the per-category numerator with the **same distinct-deck counting** the
  denominator uses (`GetCardDeckTotalsAsync` / `CardDeckTotals.TotalDeckCount`), so the
  ratio is internally consistent.
- **Clamp the rendered percentage to `[0, 100]`** as a belt-and-suspenders guard.
- If `TotalDeckCount == 0`, show `—` (no divide-by-zero, no bogus %).

## Edit inventory (additive)

### Data layer
1. `DeckFlow.Core/Knowledge/CardCategoryRepository.cs` — add
   `internal Task<IReadOnlyDictionary<string,int>> GetCategoryDeckCountsAsync(cardName, ct)`.
   SELECT per-category distinct-deck counts for the normalized card name, consistent with
   `GetCardDeckTotalsAsync`'s counting. Apply `CategoryFilter` include/junk parity with
   `GetCategoriesAsync`. Dialect-neutral SQL (matches existing Dapper queries).
2. `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — delegate method
   (mirrors the existing `GetCategoriesAsync` delegate at :65-66).
3. `DeckFlow.Web/Services/Persistence/ICategoryKnowledgeStore.cs` +
   `CategoryKnowledgeStore.cs` — add the interface method + passthrough (mirrors :44 / :73).

### Merge weighting
4. `DeckFlow.Core/Reporting/CategorySuggestionReporter.cs` — add
   `MergeWeighted(exact, inferred, edhrec, tagger)` →
   `IReadOnlyList<CategorySourceWeight>` (record: `Category`, `SourceCount`, `SourceTotal`).
   `SourceTotal` = count of the four inputs that were non-empty. Reuse the existing
   `MergeEntry`/`MergeSource`/ordering internals. **Leave `Merge` and `ToText` untouched.**

### Result + service
5. `DeckFlow.Web/Services/CategorySuggestionService.cs` — `CategorySuggestionResult`
   gains `IReadOnlyDictionary<string,int> CategoryDeckCounts` (empty default). On the
   cached path, populate it from the new store method. Update the `Empty(...)` factory
   (add empty dict).

### Controller + view model + view
6. `DeckFlow.Web/Controllers/DeckCategoriesController.cs` (~:111-123) — after the existing
   `Merge`/`ToText` (unchanged), call `MergeWeighted(...)`, and for each weight build a
   view row: look up `result.CategoryDeckCounts[cat]` (nullable), compute
   `pct = total>0 ? Clamp(round(deckCount/total*100),0,100) : null`, using
   `result.CardDeckTotals.TotalDeckCount`. Sort per the rule. Assign to the view model.
   `MergedCategoriesText` assignment stays exactly as-is.
7. View model backing `SuggestCategories.cshtml` (the type exposing `MergedCategoriesText`)
   — add `IReadOnlyList<CategoryWeightRow> WeightedCategories` (record:
   `Category`, `int? DeckCount`, `int? Percent`, `int SourceCount`, `int SourceTotal`).
8. `DeckFlow.Web/Views/Deck/SuggestCategories.cshtml` (~:131-138) — render a
   `Category | Decks | % | Sources` table INSIDE the merged `result-panel`, ABOVE the copy
   box. `—` when `DeckCount`/`Percent` is null; `Sources` = `{SourceCount}/{SourceTotal}`.
   The copy `<textarea>`/button are untouched. Table hidden when no rows.

## Tests

### Core (`DeckFlow.Core.Tests`)
- `MergeWeighted_ReportsSourceCountAndTotal`: category in 3 of 3 non-empty sources →
  `SourceCount=3, SourceTotal=3`; a tagger-only category → `1/3`.
- `MergeWeighted_SourceTotalCountsOnlyNonEmptyInputs`: empty sources don't inflate M.
- `Merge_And_ToText_Unchanged`: byte-identical copy output vs current (guard the paste).

### Web (`DeckFlow.Web.Tests`)
- Repo/store: `GetCategoryDeckCountsAsync` returns per-category distinct-deck counts and
  never exceeds `GetCardDeckTotalsAsync` total for the same card (the `>100%` guard).
- Controller: builds `WeightedCategories` with `—`(null) for a tagger-only category,
  a real % for a cached category, `%` clamped ≤100, sorted SourceCount desc then % desc.
- Controller: `MergedCategoriesText` (copy) is still plain `- Category` lines (unchanged).
- View render: table present above the copy box; copy textarea still holds plain names.

## Side Effects Report

**Direct:** the 8 files above + tests. **Transitive:** callers of `CategorySuggestionResult`
constructor/`Empty` (the service + tests) — additive field, update construction sites.
`ICategoryKnowledgeStore` gains a method → any test fake/stub of it must implement it
(grep `ICategoryKnowledgeStore` fakes and update).

**Shared state:** none. **External surfaces:** one new read-only SQL query (no schema
change, no migration). **Contract changes:** additive interface method + additive result
field + new reporter method; no existing signature changed. **Perf:** one extra indexed
read on the cached path only (same table already hit). **Backward compat:** copy text and
Commander page byte-identical; API JSON unchanged.

**Open questions:**
- Confirm the exact distinct-deck counting `GetCardDeckTotalsAsync` uses so the numerator
  matches (avoid the printings double-count). Reviewer/impl to align them precisely.
- Confirm the concrete view-model type name backing `SuggestCategories.cshtml`.

## Review revisions (Codex gpt-5.5 — folded in, authoritative)

Verdict: APPROVE-WITH-CHANGES. These supersede conflicting text above.

**R1 (HIGH — numerator grain, corrects the §"Correctness guard").** There is a UNIQUE
normalized-card index (`CategoryCacheSchema.cs:83`), so the "multiple card_id per
normalized name / printings double-count" premise is FALSE — drop it. The denominator
`CardDeckTotals.TotalDeckCount` = `SUM(t.deck_count)` from the **`card_deck_totals`** table
grouped by board (`CardCategoryRepository.cs:473-481,504`), NOT the observations table.
So `GetCategoryDeckCountsAsync` must produce a per-category numerator at the **same
`card_deck_totals` grain** — aggregate `o.deck_count` for the matching card, and prefer an
INNER JOIN to `card_deck_totals` on the same card/board grain so a category tied to a
source/board with no denominator total cannot inflate the percentage. The `%<=100` clamp
stays as a belt-and-suspenders guard but is NOT the fix; the grain match is. If
`TotalDeckCount == 0` → render `—`.

**R2 (MEDIUM — canonical keys).** `Merge`/`MergeWeighted` canonicalize labels via
`CategoryCanonicalizer` in `MergeSource` (`CategorySuggestionReporter.cs:97`;
e.g. `"Card Draw"→"Draw"`). The controller looks up counts by the MERGED (canonical) label,
so `GetCategoryDeckCountsAsync` must key its dictionary by
`CategoryCanonicalizer.CanonicalKey(category)` (canonicalize raw `o.category` before
keying), and the controller must look up by the same canonical key. Otherwise every
multi-word category silently shows `—`.

**R3 (MEDIUM/LOW — `N/M` semantics).** `M` = count of sources that contributed **≥1
non-junk category to the merge** (post-`CategoryFilter`/junk, not raw non-empty inputs).
`N` = `SourceCount` for that category. Label the column so `3/3` reads as "3 of 3
contributing sources agreed" (e.g. header `Sources` with title/aria "sources that
agreed / sources that contributed"). Do NOT present it as agreement over all four possible
sources when only some ran.

**R4 (construction/fakes — exact targets).**
- `CategorySuggestionResult` ctor/`Empty` new dict field, update: service `Empty`
  (`CategorySuggestionService.cs:44`) + ctor (`:153`); tests
  `DeckCategoriesControllerTests.cs:69`, `SuggestionsApiControllerTests.cs:60,90` and
  `Empty` call sites `SuggestionsApiControllerTests.cs:46,124,146,166,224,254`.
- `ICategoryKnowledgeStore` fakes to implement the new method:
  `TestDoubles/FakeCategoryKnowledgeStore.cs:12`, `CategorySuggestionServiceTests.cs:149`,
  `HarvestStatsAggregatorTests.cs:67,138`.
- View model confirmed = `DeckFlow.Web.Models.DeckDiffViewModel` (`:66`), shared with deck
  sync — add `WeightedCategories` there; default empty; deck-sync path leaves it empty.
- Do NOT refactor the shared copy-path ordering into `Merge`; `MergeWeighted` sorts its own.

## Flow

1. This PLAN → Codex gpt-5.5 plan-review (read-only). Fold findings.
2. Codex gpt-5.4 implements (LF preserved, scope-fenced, copy path untouched).
3. Claude verify: EOL, scope, code review, Core+Web suites, `/simplify`, UI screenshot of
   the table (desktop+mobile) via a real card with cached data.
4. Commit on `feat/category-weights`; user tests; then ff main + push per instruction.

## Acceptance criteria

- Suggest tool shows the weighted table; `—` for non-cached %, `N/M` sources for all,
  correct sort, `% ≤ 100`.
- Copy box output unchanged (plain names). Commander page unchanged.
- Core + Web suites green, no new warnings, EOL clean, README/help updated if user-facing
  copy changes.
