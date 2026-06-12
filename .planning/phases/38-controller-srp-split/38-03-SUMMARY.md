# 38-03 Summary

- Pre-dispatch git SHA: `f8b8a47c943570d45c04eb51018400a99a7dcddb`
- Warning baseline (`"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj` then `grep -c ': warning '`): `0`

## Members moved

- `DeckController.CardLookup()`
- `DeckController.DownloadCardLookup(CardLookupRequest)`
- `DeckController.DownloadCardLookupJson(CardLookupRequest)`
- `DeckController.SingleCardLookup(string? name)`
- `DeckController.MechanicLookup()`
- `DeckController.MechanicLookup(MechanicLookupRequest)`
- `DeckController.DownloadCardLookupAsync(CardLookupRequest, CardLookupDownloadFormat)`
- `DeckController.BuildVerificationFile(CardLookupResult)`
- `DeckController.CardLookupDownloadFormat`
- `DeckController.SuggestCategories()`
- `DeckController.SuggestCategories(CategorySuggestionRequest)`
- `DeckController.CardSearch(string query)`
- `DeckController.HasSuggestionInput(CategorySuggestionRequest)`

These members now live in `DeckLookupController` and `DeckCategoriesController`.

## DeckController constructor

- Removed dependencies: `ICardLookupService`, `IMechanicLookupService`, `ICategorySuggestionService`, `ICardSearchService`
- Remaining dependencies: `IDeckAnalysisPacketService`, `IDeckPrimerPacketService`, `IDeckComparisonService`, `IMetaGapService`, `PacketSessionCache`, `ILogger<DeckController>`

## Runtime notes

- `DeckCategoriesController` preserves the full `[FeatureFlagGate(...)]` attribute on both `SuggestCategories` overloads and keeps `[ValidateAntiForgeryToken]` on the POST action.
- The POST `SuggestCategories` action now uses `using var timeoutCts = CreateTimeoutScope(SuggestionTimeout);` with the existing `var cancellationToken = timeoutCts.Token;`.
- `DeckController` no longer contains `_cardSearchService`, `_cardLookupService`, `_mechanicLookupService`, `_categorySuggestionService`, or any local `SuggestionTimeout` reference.

## Build verification

- Task 1 build result: `Build succeeded`, warnings `0`
- Task 2 build result: `Build succeeded`, warnings `0`
- Final Web build result: `Build succeeded`, warnings `0`
