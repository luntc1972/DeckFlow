# 38-06 Summary

- Baseline SHA from `38-01-SUMMARY.md`: `2e2d5aa851a1b8d9d7655f689535cfc55225d933`
- Tasks 1+2 were combined into one commit because the Task 1 midpoint cannot build after Plan 04 deleted `DeckController`.
- Relocated shared fakes file: `DeckFlow.Web.Tests/DeckControllerTestFakes.cs`
- Deleted file: `DeckFlow.Web.Tests/DeckControllerTests.cs`

## Test Distribution

- `DeckFlow.Web.Tests/DeckLookupControllerTests.cs`
  - `CardLookup_ReturnsValidationError_WhenCardListMissing`
  - `CardLookup_ReturnsUserFacingError_WhenScryfallFails`
  - `CardLookup_ReturnsValidationMessage_WhenTooManyLinesSubmitted`
  - `DownloadCardLookup_ReturnsTextFile_WhenVerificationSucceeds`
  - `SingleCardLookup_ReturnsMechanicRules_WhenCardHasDetectedMechanics`
  - `SingleCardLookup_ReturnsNotFound_WhenCardMissing`
  - `SingleCardLookup_UsesResolvedCardName_WhenLookupFallsBackToAlternatePrintedName`
  - `SingleCardLookup_Continues_WhenOneMechanicLookupFails`
  - `SingleCardLookup_ReturnsServiceUnavailable_WhenScryfallFails`
  - `MechanicLookup_ReturnsValidationError_WhenMechanicMissing`
  - `MechanicLookup_ReturnsRules_WhenMechanicFound`
- `DeckFlow.Web.Tests/DeckCategoriesControllerTests.cs`
  - `BuildNoSuggestionsMessage_UsesCachedDataNotice_WhenNoDecks`
  - `BuildNoSuggestionsMessage_UsesGeneralMessage_WhenDecksExist`
  - `CardSearch_ReturnsServiceUnavailable_WhenScryfallFails`
- `DeckFlow.Web.Tests/DeckPacketControllerTests.cs`
  - `CedhMetaGap_Get_ReturnsExpectedViewModel`
  - `CedhMetaGap_Post_AdvancesToStep2WhenReferenceDecksAreFetched`
  - `CedhMetaGap_Post_ReturnsRateLimitMessage`
  - `DeckAnalysis_ReturnsValidationError_WhenBracketMissingForAnalysisStep`
  - `DeckAnalysis_ReturnsValidationError_WhenQuestionsMissingForAnalysisStep`
  - `DeckAnalysis_ReturnsValidationError_WhenSetSourceMissingForUpgradeStep`
  - `DeckAnalysis_PassesSelectedQuestionsAndSingleSetToService`
  - `DeckComparison_Get_RendersPage`
  - `DeckComparison_Post_ReturnsExpectedResultModel`
  - `DeckComparison_Post_ReturnsViewWithError_WhenModelStateInvalid`

## Test Count

- Original `DeckControllerTests.cs` `[Fact]` count: `24`
- New combined `[Fact]` count across the three per-controller files: `24`
- Only controller construction changed: `new DeckController(...)` became narrowed `new DeckLookupController(...)`, `new DeckCategoriesController(...)`, or `new DeckPacketController(...)` with `NullLogger<XxxController>.Instance`. Assertions, `HttpContext` / `ControllerContext` setup, and action invocations were otherwise preserved.

## Build Verification

- Full solution build command: `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln`
- Result: `Build succeeded`
- Errors: `0`
- Warnings: `0`
- Warning baseline from `38-01-SUMMARY.md`: `0`

## SC1 Route-Parity Proof

- `/Deck/Error` normalization: conventional pre-split route normalized to `Route("Deck/Error")`; post-split route captured from `[Route("Deck/Error")]` on `ShellController.Error`. Same URL preserved, controller moved `Deck -> Shell`, `UseExceptionHandler("/Deck/Error")` unchanged.
- PRE route list source: baseline `DeckController.cs` `HttpGet` / `HttpPost` attribute strings from `2e2d5aa851a1b8d9d7655f689535cfc55225d933`, plus appended normalized token `Route("Deck/Error")`
- POST route list source: `HttpGet` / `HttpPost` / `Route` attributes across `ShellController`, `DeckSyncController`, `DeckConvertController`, `DeckLookupController`, `DeckCategoriesController`, `DeckPacketController`, `DeckPrimerController`, and `JudgeQuestionsController`

### PRE

```text
HttpGet("/")
HttpGet("/api/set-options")
HttpGet("/card-lookup")
HttpGet("/card-lookup/single")
HttpGet("/cedh-meta-gap")
HttpGet("/convert")
HttpGet("/convert/commander-search")
HttpGet("/deck-analysis")
HttpGet("/deck-comparison")
HttpGet("/deck-primer")
HttpGet("/judge-questions")
HttpGet("/mechanic-lookup")
HttpGet("/suggest-categories")
HttpGet("/suggest-categories/card-search")
HttpGet("/sync")
HttpPost("/card-lookup/download")
HttpPost("/card-lookup/download-json")
HttpPost("/cedh-meta-gap")
HttpPost("/cedh-meta-gap/download")
HttpPost("/cedh-meta-gap/upload")
HttpPost("/convert")
HttpPost("/deck-analysis")
HttpPost("/deck-analysis/download")
HttpPost("/deck-analysis/upload")
HttpPost("/deck-comparison")
HttpPost("/deck-comparison/download")
HttpPost("/deck-comparison/upload")
HttpPost("/deck-primer")
HttpPost("/deck-primer/download")
HttpPost("/deck-primer/upload")
HttpPost("/mechanic-lookup")
HttpPost("/resolve")
HttpPost("/suggest-categories")
HttpPost("/sync")
Route("Deck/Error")
```

### POST

```text
HttpGet("/")
HttpGet("/api/set-options")
HttpGet("/card-lookup")
HttpGet("/card-lookup/single")
HttpGet("/cedh-meta-gap")
HttpGet("/convert")
HttpGet("/convert/commander-search")
HttpGet("/deck-analysis")
HttpGet("/deck-comparison")
HttpGet("/deck-primer")
HttpGet("/judge-questions")
HttpGet("/mechanic-lookup")
HttpGet("/suggest-categories")
HttpGet("/suggest-categories/card-search")
HttpGet("/sync")
HttpPost("/card-lookup/download")
HttpPost("/card-lookup/download-json")
HttpPost("/cedh-meta-gap")
HttpPost("/cedh-meta-gap/download")
HttpPost("/cedh-meta-gap/upload")
HttpPost("/convert")
HttpPost("/deck-analysis")
HttpPost("/deck-analysis/download")
HttpPost("/deck-analysis/upload")
HttpPost("/deck-comparison")
HttpPost("/deck-comparison/download")
HttpPost("/deck-comparison/upload")
HttpPost("/deck-primer")
HttpPost("/deck-primer/download")
HttpPost("/deck-primer/upload")
HttpPost("/mechanic-lookup")
HttpPost("/resolve")
HttpPost("/suggest-categories")
HttpPost("/sync")
Route("Deck/Error")
```

### Diff

```text
```
