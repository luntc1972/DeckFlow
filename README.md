# DeckSyncWorkbench

DeckSyncWorkbench helps deck builders translate decks between Moxfield and Archidekt without manual editing. The CLI and web projects both rely on a shared core that parses exports, compares quantities, flags printing conflicts, and generates delta imports, full exports, and cache-backed category suggestions.

**Repository description (≤350 characters):** DeckSyncWorkbench unifies Moxfield and Archidekt decks, harvests Archidekt category data, and exposes CLI/web tools for diffs, printing conflict reports, and cache-backed category suggestions.

## Highlights
- `DeckSyncWorkbench.Core` contains parsers, diffing logic, exporters, and the Archidekt/Moxfield integrations.
- `DeckSyncWorkbench.Web` provides an ASP.NET Core MVC UI for running syncs, viewing comparisons, and suggesting categories/tags with caching support.
- `DeckSyncWorkbench.CLI` exposes the same functionality in a console tool so you can script imports or harvest Archidekt decks for the category cache.
- The Commander Categories page surfaces the Archidekt categories that show up most frequently in decks where the queried card is listed as the commander; it does not try to re-label cards, it just reports the tags observers assigned when that card led the deck.

## Getting Started
1. Restore/build: `dotnet build DeckSyncWorkbench.sln` (Web may still fail locally if the SDK resolver cannot resolve `Microsoft.NET.SDK.WorkloadAutoImportPropsLocator`; the CLI binary builds independently.)
2. Run the web app: `dotnet run --project DeckSyncWorkbench.Web`
3. Use the CLI to compare or harvest decks: `dotnet run --project DeckSyncWorkbench.CLI -- --help`

## Archidekt category cache
- Run `dotnet run --project DeckSyncWorkbench.CLI -- archidekt-cache --minutes 5` to keep the local cache fed with the latest public decks. The CLI now runs a dedicated cache session that respects rate limits (via Polly), records skips for noisy decks, and persists card/category observations to `artifacts/category-knowledge.db`.
- The web cache service now reuses the same session logic, so the MVC tools can refresh a few decks on demand or run longer sweeps with `CategoryKnowledgeStore.RunCacheSweepAsync`.

## CLI usage examples
- `dotnet run --project DeckSyncWorkbench.CLI -- compare --moxfield my.deck --archidekt other.deck --out diff.txt`
- `dotnet run --project DeckSyncWorkbench.CLI -- archidekt-cache --minutes 10` (harvest for ten minutes)
- `dotnet run --project DeckSyncWorkbench.CLI -- category-find --card "Guardian Project" --cache-seconds 30`


## Architecture
- Core logic is isolated in `DeckSyncWorkbench.Core` (diff engine, export helpers, parsers, integration clients, knowledge store).
- Web and CLI layers orchestrate requests and rely on DI to resolve the shared services (deck sync, knowledge store, importers).
- Importers for Archidekt/Moxfield now implement typed interfaces so the service can swap implementations easily during testing.
