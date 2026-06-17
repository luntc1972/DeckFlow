# 38-02 Summary

- Pre-dispatch git SHA: `270d49eb2c0d503f50e4b5d0e3588a51a7bc48fa`
- Warning baseline from `38-01-SUMMARY.md`: `0`
- Pre-edit `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj` warning count: `0`

## Members moved

- `DeckSyncController`
- `Index()` `[HttpGet("/sync")]`
- `Index(DeckDiffRequest)` `[HttpPost("/sync")]`
- `Resolve(DeckDiffRequest)` `[HttpPost("/resolve")]`
- `RenderDiffAsync(DeckDiffRequest)`
- `BuildViewModel(DeckDiffRequest, LoadedDecks, DeckDiff, string?)`
- `BuildUserFacingErrorMessage(DeckDiffRequest, Exception)`
- `IsMoxfieldForbidden(DeckDiffRequest, Exception)`
- `HasMoxfieldInput(DeckDiffRequest)`
- `HasArchidektInput(DeckDiffRequest)`

- `DeckConvertController`
- `Convert()` `[HttpGet("/convert")]`
- `Convert(DeckConvertRequest)` `[HttpPost("/convert")]`
- `ConvertCommanderSearch(string)` `[HttpGet("/convert/commander-search")]`

- `JudgeQuestionsController`
- `JudgeQuestions(string?)` `[HttpGet("/judge-questions")]`

## Dependency changes

- Removed from `DeckController`: `IDeckSyncService`
- Removed from `DeckController`: `IDeckConvertService`
- Retained in `DeckController`: `ICardSearchService` via `_cardSearchService` for Plan 03 (`/suggest-categories/card-search`)

## JudgeQuestions controller shape

- `JudgeQuestionsController` is parameterless
- No constructor
- No `ILogger`
- No private field
- Kept standalone rather than folding into Lookup so the `/judge-questions` route and `DeckPageTab.JudgeQuestions` ownership remain explicit

## Build verification

- Task 1 build result: `Build succeeded`, warnings `0`
- Task 2 build result: `Build succeeded`, warnings `0`
- Task 3 build result: `Build succeeded`, warnings `0`
- Final Web build result: `Build succeeded`, warnings `0`, errors `0`
