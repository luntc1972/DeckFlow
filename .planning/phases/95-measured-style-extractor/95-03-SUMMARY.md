# Plan 95-03 Summary

## What Was Built

- Added `ContentTagVocabulary.Staples` as the D-05 curated always-strip staple set, using the existing case-insensitive `HashSet<string>` idiom without changing any existing dimensions or `IsValid(...)`.
- Added `GlobalCategoryBaseline` plus a new processed-only `GetGlobalCategoryBaselineAsync` aggregate that computes total decks, per-category counts, and per-pair counts server-side from one shared `deck_queue.processed = 1` deck-to-category CTE.
- Extended repository coverage with a deterministic fixture proving a `deck_queue.processed = 0` deck is excluded from baseline totals, category counts, and pair counts.

## Key Files

- Modified `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs`
- Added `DeckFlow.Core/Knowledge/GlobalCategoryBaseline.cs`
- Modified `DeckFlow.Core/Knowledge/CardCategoryRepository.cs`
- Modified `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs`
- Modified `DeckFlow.Core.Tests/ContentTagVocabularyTests.cs`
- Modified `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs`

## Decisions And Deviations

- Kept `Staples` separate from `ContentTagDimension` validation so card-name staples remain queryable without changing the existing dimension contract.
- Returned the global baseline from one SQL aggregate result set built from a single processed-deck CTE, then applied `CategoryFilter.IsIncluded(...)` only after aggregation to mirror the existing category-filtering behavior.
- No scope deviations. No DI/program changes, no non-fenced file edits, and no changes to prior plan files.

## Task Commits

- Task 1: `cbf985c1` — `feat(95): add curated ContentTagVocabulary.Staples set (D-05)`
- Task 2: `7aec71d0` — `feat(95): add server-side global category lift-baseline aggregate (processed-only)`
- Task 3: `8cad8381` — `test(95): fixture proving lift-baseline aggregate + processed-flag exclusion`

## Verification Status

- `dotnet.exe build DeckFlow.Core/DeckFlow.Core.csproj`: PASSED
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~ContentTagVocabularyTests"`: PASSED
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~CategoryKnowledgeRepositoryTests"`: PASSED
- The processed=`0` exclusion fixture passed and asserted exclusion from `TotalDecks`, `DecksWithCategory`, and `DecksWithCategoryPair`.

## Self-Check

- PASSED
