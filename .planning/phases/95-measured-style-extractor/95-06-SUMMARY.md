# Plan 95-06 Summary

## What Was Built

- Added `ArchidektOwnerUrl` with an HTTPS-only Archidekt host guard that accepts bare usernames and trusted `archidekt.com` / `*.archidekt.com` profile URLs while rejecting lookalikes, userinfo tricks, private/link-local IPs, and non-HTTPS inputs.
- Added `IArchidektOwnerClient` + `ArchidektOwnerClient` for the two net-new Archidekt endpoints: owner resolve (`/api/users/?username=`) and paginated owner deck listing (`/api/decks/v3/?ownerUsername=&pageSize=&page=`).
- The owner client uses RestSharp query parameters, a bounded `MaxResponseBytes` check before `JsonDocument.Parse`, graceful `JsonException` handling, and hard `MaxPages` / `MaxDecks` pagination ceilings.
- Added `CreatorProfileDeckCrawler` with the required creator-level freshness short-circuit: within the freshness window it rebuilds `CreatorDeckSample[]` entirely from `creator_deck_cache` and makes zero Archidekt calls; outside the window it resolves, re-enumerates, drops `Size > 105` before import, reuses cached deck entries when a stored hash exists, caches imported decks, and stamps `last_crawled_utc`.
- Added focused xUnit coverage for SSRF allow/deny cases, malformed and oversized payload handling, pagination caps, warm-cache zero-HTTP behavior, expired-window re-enumeration with cache reuse, force-refresh bypass, and manual-URL fallback.
- Added additive DI registrations in `Program.cs` for `IArchidektOwnerClient` and `CreatorProfileDeckCrawler`.

## Key Files

- Added `DeckFlow.Web/Services/CreatorStyle/ArchidektOwnerUrl.cs`
- Added `DeckFlow.Web/Services/CreatorStyle/ArchidektOwnerClient.cs`
- Added `DeckFlow.Web/Services/CreatorStyle/CreatorProfileDeckCrawler.cs`
- Updated `DeckFlow.Web/Program.cs`
- Added `DeckFlow.Web.Tests/Services/CreatorStyle/ArchidektOwnerClientTests.cs`
- Added `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorProfileDeckCrawlerTests.cs`

## Decisions And Deviations

- Reused the existing `"banlist"` named resilience pipeline to stay inside the scope fence; `ResiliencePipelineFactory.cs` was not changed.
- The crawler writes only to `creator_deck_cache` plus `creator_profile_source.last_crawled_utc`; it does not touch the global corpus tables or cache-session helpers.
- Per the plan acceptance path, the expired-window cache-reuse branch treats an existing creator/deck cache hash row as an unchanged deck and rebuilds from persisted `entries_json` without calling `ImportAsync`.
- No scope deviations. No Core files were edited, and no references to `ArchidektRecentDecksImporter` or `websockets.archidekt.com` were added in the new creator-style files.

## Verification Status

- `dotnet.exe build DeckFlow.Web/DeckFlow.Web.csproj`: PASSED
- `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~ArchidektOwnerClientTests"`: PASSED
- `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~CreatorProfileDeckCrawlerTests"`: PASSED
- `rg -n "ArchidektRecentDecksImporter|websockets\\.archidekt\\.com" DeckFlow.Web/Services/CreatorStyle DeckFlow.Web.Tests/Services/CreatorStyle`: no matches
- `rg -n "card_category_observations|ReplaceSourceRowsAsync|AddDeckIdsAsync|PersistDeckCategoryBatchAsync" DeckFlow.Web/Services/CreatorStyle/CreatorProfileDeckCrawler.cs`: no matches
- LF-only check on the fenced source and test files: PASSED

## Self-Check

- PASSED
