# 0006 — Keep collection resolution and cache mechanisms deliberately separate

Date: 2026-09-08

## Context

`ScryfallCollectionResolver.ResolveCardsAsync` and
`ManabaseAnalysisService.ResolveCardsAsync` both resolve Scryfall collection identifiers, but
they solve different problems. The shared resolver is stateless and delegate-driven: it deduplicates
identifiers, batches them, throws on a bad response, and returns a flat card list without an ordering
guarantee. Its production consumers are `CreatorStyleDeckAnalysis` and `DeckConvertService`.

The manabase path tracks every identifier's global deck position so ambiguous results resolve in deck
order rather than Scryfall response order. It maintains a `ScryfallCollectionCardCache` across calls
and uses the typed `IScryfallCollectionProtocol` abstraction also used by
`DeckAnalysisPacketService`. Flattening that position tracking would select the wrong printing for a
card with several recent printings and could shift the manabase land target. Related batch-match-key
asymmetry remains documented in [ADR 0004](0004-scryfall-batch-match-key-asymmetry.md).

Three similarly named cache types also differ materially. `CardLookupCache` owns a private
`MemoryCache` with `SizeLimit = 10_000` and `Size = 1` entries, preventing eviction from the shared
unbounded `IMemoryCache`; it uses a sentinel to distinguish a negative hit from a miss and exposes
imperative try/set operations. `ScryfallCollectionCardCache` owns a private char-budgeted cache,
a feature flag, and hit/miss/store/bypass statistics. `CachedNameResolution` instead supplies a
functional `GetOrAddAsync` surface over a caller-provided cache.

## Decision

Keep `ScryfallCollectionResolver` and `ManabaseAnalysisService` resolution paths separate. Do not
add cache, position-tracking, or typed-protocol parameters to the shared resolver, and do not promote
the manabase implementation into it. A future dedicated manabase-refactor cycle may supersede this
decision if a common abstraction proves worthwhile.

Keep `CardLookupCache`, `ScryfallCollectionCardCache`, and `CachedNameResolution` as separate cache
mechanisms. Merging them would either remove size isolation or impose size accounting on callers:
`CardGroundingGuard` and `ScryfallCardNameGrounder` for card lookup, and `DeckConvertService` for
collection resolution.

Share only values that are genuinely common. `ScryfallLimits.CollectionBatchSize` is the single
collection batch-size declaration, read by `DeckConvertService`, `ManabaseAnalysisService`,
`CardGroundingGuard`, `CardLookupService`, `ScryfallCollectionResolver`, and
`DeckFlow.CLI.ManabaseCommandRunner`. `CachedNameResolution.PositiveCacheTtl` and
`NegativeCacheTtl` are the single 24-hour and 1-hour declarations, read by `CardGroundingGuard`,
`ScryfallCardNameGrounder`, `CardLookupCache`, and `ScryfallCollectionCardCache`. Sharing values
while retaining distinct mechanisms is intentional.

`ScryfallLimits` is public rather than granting the CLI `InternalsVisibleTo`: that exposes exactly
the one compile-time constant the CLI needs rather than every Web internal type.

## Consequences

### Cycle 21 Phase 6 merge census

The following `ScryfallThrottle.ExecuteAsync` occurrence counts are identical on `main` and this
branch. They cover seven services and thirteen call sites; the `ResiliencePipelineFactory` match is a
doc comment, not a call.

| File | Count |
| --- | ---: |
| `DeckFlow.Web/Services/DeckConvertService.cs` | 1 |
| `DeckFlow.Web/Services/Scryfall/CardLookupService.cs` | 4 |
| `DeckFlow.Web/Services/Scryfall/CardSearchService.cs` | 1 |
| `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` | 3 |
| `DeckFlow.Web/Services/Scryfall/ScryfallCommanderSearchService.cs` | 1 |
| `DeckFlow.Web/Services/Scryfall/ScryfallSetService.cs` | 2 |
| `DeckFlow.Web/Services/Scryfall/ScryfallTaggerLookupService.cs` | 1 |

Reproduce the census with:

```bash
git grep -n 'ScryfallThrottle\.ExecuteAsync' main -- 'DeckFlow.Web/Services/*.cs'
git grep -n 'ScryfallThrottle\.ExecuteAsync' HEAD -- 'DeckFlow.Web/Services/*.cs'
```

This corrects the Phase 113 ROADMAP entry and `113-RESEARCH.md`: this cycle adds no
`ExecuteAsync` call site needing an endpoint key. Its three net-new throttle usages are
`ThrowIfUpstreamUnavailable` status-code checks at `CardGroundingGuard.cs:157`,
`CardGroundingGuard.cs:243`, and `ScryfallCardResolver.cs:157`; that method accepts a status code,
not an endpoint key. The per-endpoint pacing test against the merged Cycle 20 and Cycle 21 tree
remains open until Cycle 21 Phase 6 merges.

The Phase 112 Archidekt resilience pipeline remains resolved and used by the Archidekt import path;
the `ArchidektOwnerClient` tests cover that integration.

`ManabaseAnalysisService.cs` was deliberately untouched in Phase 113, including the explanatory code
comment proposed during research. The file is under active Cut Lab development on another branch, and
a comment would create another merge-conflict surface. This ADR is the durable explanation. Its diff
against `main` remains three insertions and three deletions, all from Phase 112.
