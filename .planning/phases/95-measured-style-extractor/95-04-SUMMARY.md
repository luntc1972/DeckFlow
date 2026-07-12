# Plan 95-04 Summary

## What Was Built

- Added the pure `DeckFlow.Core.Knowledge.MeasuredStyleExtraction` input contract with `CreatorDeckSample` and `MeasuredStyleInputs`, keeping the seam host-agnostic and limited to plain in-memory deck, category, and baseline data.
- Added `StapleStripper` as a pure static helper covering `CardCount > 105` filtering, near-precon duplicate flagging via documented `>0.70` Jaccard overlap, strict `>60%` personal-staple detection, and staple stripping via `ContentTagVocabulary.Staples` union the computed personal set.
- Added `FolderWeighting` as a pure static helper that applies curated folder-id weights with a `1.0` fallback and reports both the raw deck count and the fractional effective sample size.
- Added focused xUnit coverage proving the locked behaviors for curated staples, strict `>60%` personal-staple cutoff, `CardCount > 105` filtering, near-precon re-marking, curated/missing/uncurated folder weights, and fractional effective sample computation.

## Key Files

- Added `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/CreatorDeckSample.cs`
- Added `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/MeasuredStyleInputs.cs`
- Added `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/StapleStripper.cs`
- Added `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/FolderWeighting.cs`
- Added `DeckFlow.Core.Tests/MeasuredStyleExtraction/StapleStripperTests.cs`
- Added `DeckFlow.Core.Tests/MeasuredStyleExtraction/FolderWeightingTests.cs`

## Decisions And Deviations

- Used `DeckEntry.NormalizedName` when available for case-insensitive overlap and staple comparisons, falling back to `Name` only when the normalized field is blank.
- Re-marked later near-precon duplicates to the literal confidence marker `near-precon`; earlier decks retain their incoming marker unchanged.
- No scope deviations. No edits outside the fenced files, no DI/program changes, and no changes to plans 95-01/02/03.

## Task Commits

- Task 0/1: `5a5202e1` — `feat(95): add pure MeasuredStyleExtraction input contract (CreatorDeckSample + MeasuredStyleInputs)`
- Task 2: `d8f474e6` — `feat(95): add StapleStripper (hybrid staple-strip + >105 filter + near-precon dedup)`
- Task 3: `222e4134` — `feat(95): add FolderWeighting (graded weights + fractional effective sample)`

## Verification Status

- `dotnet.exe build DeckFlow.Core/DeckFlow.Core.csproj`: PASSED
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~StapleStripperTests"`: PASSED
- `dotnet.exe test DeckFlow.Core.Tests --filter "FullyQualifiedName~FolderWeightingTests"`: PASSED
- `grep -rc "System.Net.Http\|DeckFlow.Web\|HttpClient" DeckFlow.Core/Knowledge/MeasuredStyleExtraction/`: `0` in every file
- `StapleStripper.cs` includes the required `// Why:` comment documenting the discretionary `>0.70` Jaccard threshold
- LF-only check on all six source/test files: PASSED

## Self-Check

- PASSED
