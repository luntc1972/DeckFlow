---
status: complete
quick_task: 260817-kvn
branch: fix/category-copy-box-order
commits:
  - 2b5eda2e
  - 4ec7aaad
  - 7003bbfb
---

# Suggest Categories copy-box ordering

The MVC and API copy text now projects the same ranked display rows used by the weighted evidence table.

## TDD evidence

- `SuggestCategories_Success_MergedCopyTextFollowsWeightedTableOrder` was RED with expected `Ramp, Draw, Protection` and actual `Draw, Protection, Ramp`; it is GREEN after the MVC wiring change.
- `PostCardSuggestionAsync_MergedCopyTextFollowsWeightedTableOrder` was RED with the same expected/actual ordering; it is GREEN after the API wiring change.

## Mutation proof

Temporarily changed `CategoryWeightRowFactory.cs` from `.ThenByDescending(row => row.Percent)` to `.ThenBy(row => row.Percent)`. Exactly the two new facts failed; the existing MVC and API table-order facts remained green. The line was restored exactly, and the combined controller test set passed 25/25.

## Verification

- WSL `dotnet build DeckFlow.sln -v:q -nologo`: exit 0, no build warnings or errors.
- Windows filtered tests for `DeckCategoriesControllerTests`, `SuggestionsApiControllerTests`, and `CategorySuggestionReporterMergeTests`: Web 25/25 and Core 8/8 passed.
- `scripts/format-check-changed.sh staged` passed for each atomic commit.

The Windows test invocation retained existing NU1903 SSH.NET advisory warnings and unrelated existing Core test nullable warnings; no new warnings were introduced by this task.
