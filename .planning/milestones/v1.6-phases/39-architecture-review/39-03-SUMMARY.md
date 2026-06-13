# 39-03 Summary

## Resolver extension

- Extended `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` so both fallback behaviors now coexist on the resolver:
  - `SearchFallbackCardAsync` remains the simple exact-name search used by Comparison and MetaGap.
  - `SearchPrintingFallbackCardAsync` now carries Analysis's richer 3-stage fallback unchanged: two `cards/search` requests (`unique=prints`, `include_multilingual=true`) plus a final `cards/named?fuzzy=` lookup.
- Added the third internal execute seam for named lookups:
  - `Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>`
- `NormalizeLookupName` and `NormalizeForScryfall` now live on `ScryfallCardResolver` as public static helpers, so Analysis and the resolver share the exact same implementations.
- Scryfall traffic still routes through `ScryfallThrottle`, `ThrowIfUpstreamUnavailable`, and the named `"scryfall"` resilience pipeline.

## Analysis migration

- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` now injects `IScryfallCardResolver`.
- Removed Analysis-owned Scryfall transport seams:
  - `_executeCollectionAsync`
  - `_executeSearchAsync`
  - `_executeNamedAsync`
- Deleted Analysis's private `SearchFallbackCardAsync`, `NormalizeLookupName`, and `NormalizeForScryfall`.
- Migrated both Analysis collection execution sites to `_scryfallCardResolver.ExecuteCollectionAsync`:
  - `LookupCommanderColorIdentityAsync`
  - `LookupCardReferencesAsync`
- Migrated Analysis fallback consumers to `_scryfallCardResolver.SearchPrintingFallbackCardAsync`:
  - unresolved card reference resolution
  - `ValidateCommanderAsync`
- Kept the existing chunk loop, analysis-specific collection error text, resolved-card mapping, mechanic extraction, and color-identity projection unchanged.

## Wiring and scope

- Updated `DeckFlow.Web/Program.cs` so the Analysis factory resolves `IScryfallCardResolver` instead of constructing local Scryfall delegates.
- Updated `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs` so `CreateDeckAnalysisPacketService(...)` keeps the existing three seam parameters and routes them through the resolver's internal test constructor. No edits were required in `DeckAnalysisPacketServiceTests.cs`.
- No test source files were edited.
- No cache-key helper files were touched.
- No `Services/PromptBuilders/**` files were touched.

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` -> `0` warnings, `0` errors
- `grep -rc "private async Task<ScryfallCard?> SearchFallbackCardAsync" DeckFlow.Web/Services/DeckComparisonService.cs DeckFlow.Web/Services/MetaGapService.cs DeckFlow.Web/Services/DeckAnalysisPacketService.cs` -> all `0`
- `grep -c "_executeCollectionAsync\\|_executeSearchAsync\\|_executeNamedAsync" DeckFlow.Web/Services/DeckAnalysisPacketService.cs` -> `0`
- `grep -n "_scryfallCardResolver.ExecuteCollectionAsync" DeckFlow.Web/Services/DeckAnalysisPacketService.cs` -> both Analysis collection call sites migrated
- `grep -n "ThrowIfUpstreamUnavailable" DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` -> preserved in the resolver
