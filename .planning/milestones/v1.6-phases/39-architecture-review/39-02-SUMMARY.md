# 39-02 Summary

## Resolver contract

- Added `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` in namespace `DeckFlow.Web.Services.Scryfall`.
- Chosen contract:
  - `Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken)`
  - `Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken)`
- The resolver returns the raw `RestResponse<ScryfallCollectionResponse>` so `DeckComparisonService` and `MetaGapService` keep their own status/null handling and service-specific HTTP error messages byte-identically.
- `SearchFallbackCardAsync` was moved verbatim from the two services:
  - `cards/search`
  - `q = !"name"`
  - `unique = cards`
  - `order = name`
  - 2xx returns `response.Data?.Data.FirstOrDefault()`
  - 404 returns `null`
  - all other statuses throw `HttpRequestException($"Scryfall fallback lookup failed while resolving {cardName} with HTTP {(int)response.StatusCode}.", null, response.StatusCode)`
- Scryfall traffic still routes through `ScryfallThrottle.ExecuteAsync` and the named `"scryfall"` pipeline.

## Service migration

- `DeckComparisonService`
  - Removed ctor params: `IScryfallRestClientFactory`, `ResiliencePipelineProvider<string>`, `RestClient? restClientOverride`, `Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsyncOverride`, `Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsyncOverride`
  - Added ctor param: `IScryfallCardResolver scryfallCardResolver`
  - Removed fields: `_executeCollectionAsync`, `_executeSearchAsync`
  - Deleted private `SearchFallbackCardAsync`
  - Kept chunking, `ScryfallBatchSize`, oracle-name aggregation, fallback-add-to-resolved-cards behavior, and the comparison-specific collection error message unchanged

- `MetaGapService`
  - Removed ctor params: `IScryfallRestClientFactory`, `ResiliencePipelineProvider<string>`, `RestClient? restClientOverride`, `Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsyncOverride`, `Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsyncOverride`
  - Added ctor param: `IScryfallCardResolver scryfallCardResolver`
  - Removed fields: `_executeCollectionAsync`, `_executeSearchAsync`
  - Deleted private `SearchFallbackCardAsync`
  - Kept chunking, `ScryfallBatchSize`, oracle-name aggregation, and the meta-gap-specific collection error message unchanged

## Wiring and test seams

- `Program.cs`
  - Registered `IScryfallCardResolver` as a singleton beside the other Scryfall registrations
  - Updated `DeckComparisonService` and `MetaGapService` factories to inject `IScryfallCardResolver`

- `TestServiceFactory`
  - Preserved the existing `executeCollectionAsync` and `executeSearchAsync` parameters on `CreateDeckComparisonService` and `CreateMetaGapService`
  - Routed those params into a `ScryfallCardResolver` via its internal test ctor
  - No test-file edits were required in `DeckComparisonServiceTests` or `MetaGapServiceTests`

## Verification

- `grep -c "ScryfallThrottle.ExecuteAsync" DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` -> `2`
- `grep -c "private async Task<ScryfallCard?> SearchFallbackCardAsync" DeckFlow.Web/Services/DeckComparisonService.cs DeckFlow.Web/Services/MetaGapService.cs` -> both `0`
- `grep -c "_scryfallCardResolver" DeckFlow.Web/Services/DeckComparisonService.cs DeckFlow.Web/Services/MetaGapService.cs` -> both `4`
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` -> `0` warnings, `0` errors

## Scope guard

- No edits to `DeckAnalysisPacketService`
- No edits to `DeckPrimerPacketService`
- No edits to cache-key helpers
- No edits to `Services/PromptBuilders/**`
