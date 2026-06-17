# 38-05 Summary

- CLI warning baseline before commit 1: `0` (`"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.CLI/DeckFlow.CLI.csproj | grep -c ': warning '`)
- Commit 1: `e59dfc770f238a08947750cf38a1abe516ff40de` `refactor(cli): extract ContentKbCliPaths shared path helper`
- Commit 2: `ba521a0153b70294e89c17d477238b07699b28c5` `refactor(cli): split CommandRunners into Deck + ContentKb runners`

## Mechanical Inventory

- STEP 0 live inventory was taken from the post-commit-1 `DeckFlow.CLI/CommandRunners.cs` baseline, so the two shared path helpers were already out of the file.
- Live STEP 0 counts: `63` methods + `6` nested types + `13` consts/fields = `82` top-level members.
- Reconciled allocation after the split:
  - `DeckCommandRunners`: `21` top-level members (`20` methods + `1` nested type)
  - `ContentKbCommandRunners`: `61` top-level members (`43` methods + `5` nested types + `13` consts/fields)
  - `ContentKbCliPaths`: `2` top-level members (`ResolveDatabasePath`, `ResolveArtifactRoot`)
- Combined split total: `84`, matching the planned `21/61/2` allocation after commit 1 had already extracted the shared helpers.
- Name-level reconciliation against the live STEP 0 inventory had no missing members and no duplicate homes. The only extra names in the post-split union were the expected renamed shared helpers: `ResolveDatabasePath`, `ResolveArtifactRoot`.

## Exhaustive Allocation Notes

- The D-04 list and earlier allocation drafts were non-exhaustive; the mechanical-enumeration + content-KB-store rule was applied to every live member before deletion.
- Previously omitted distill helpers confirmed in `ContentKbCommandRunners`:
  - `FilterTags`
  - `GetContentNaturalKeyInfo`
  - `ComputeProjectedVideoCostUsd`
  - `ComputeProjectedCallCostUsd`
- Required deck-side members confirmed in `DeckCommandRunners`:
  - `ResolveConflicts`
  - `BuildProbeRequest`
  - `CreateDeckEntryLoader`
  - `ScryfallCardDto`
- `IsTerminalSuccess` landed in `ContentKbCommandRunners`.
- All 13 distill/harvest consts landed in `ContentKbCommandRunners`:
  - `ShortVideoMaxDuration`
  - `SummaryMaxOutputTokens`
  - `ClipsMaxOutputTokens`
  - `TagsMaxOutputTokens`
  - `SummaryMaxWords`
  - `MinClipCount`
  - `MaxClipCount`
  - `MaxTranscriptInputTokens`
  - `DistillationCallCount`
  - `DistillStatusDistilled`
  - `DistillStatusSkippedOverCap`
  - `DistillStatusFailed`
  - `DistillStatusFiltered`

## Re-points And Deletion

- `Program.cs` re-points:
  - `DeckCommandRunners.` call targets: `13`
  - `ContentKbCommandRunners.` call targets: `11`
  - bare `CommandRunners.` call targets remaining: `0`
- 5 test files re-pointed to `ContentKbCommandRunners.`: `18` references
- bare `CommandRunners.` references remaining across the 5 test files: `0`
- `DeckFlow.CLI/CommandRunners.cs` was deleted with `git rm`.

## Verification

- Post-split CLI build: `0` errors, `0` warnings
- Post-split Core.Tests build: `0` errors, `0` warnings
- Post-split warning count versus baseline: `0` vs `0` (no increase)
- Internal seam intact: the `internal static` overloads for `RunDistillAsync`, `RunHarvestAsync`, `RunBlockVideoAsync`, `RunUnblockVideoAsync`, `RunListBlockedAsync`, and `RunCorpusResetAsync` remain in `ContentKbCommandRunners`, and the 5 CLI test files compile against them.
