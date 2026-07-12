# Plan 95-07 Summary

## Outcome

Implemented the measured-style extractor capstone in `DeckFlow.Web` and `DeckFlow.Web.Tests` within the requested file fence.

- Added `CreatorDeckCategoryResolver` to resolve multi-bucket categories from `CategoryKnowledgeRepository.GetCategoriesAsync(...)` first, with `IScryfallTaggerLookupService.LookupOracleTagsAsync(...)` used only for the tail.
- Added `MeasuredStyleProfileBuilder` to crawl creator decks, staple-strip, compute folder effective sample size, build category/lift/combo/Karsten `MeasuredMetric[]`, mark `InsufficientSample` below `CreatorStyleProfile.MinDeckFloor`, and persist via `ICreatorStyleProfileStore.UpsertAsync(...)`.
- Fixed the Karsten substrate stance to `ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual)` with no feature-flag threading and an inline `// Why:` rationale for determinism/comparability.
- Added deterministic end-to-end builder coverage plus the checked-in Snail seed-corpus fixture and invariants test.
- Registered the new services in DI and added an additive README substrate note.

## Verification

- `dotnet.exe build DeckFlow.Web/DeckFlow.Web.csproj`
  - Passed
- `dotnet.exe test DeckFlow.Web.Tests --filter "FullyQualifiedName~MeasuredStyleProfileBuilderTests"`
  - Passed

## Notes

- Combo lookup is null-graceful: a `null` `FindCombosAsync(...)` result contributes zero combo density without throwing.
- Every emitted measured metric carries both raw `NumDecks` and `Distribution.EffectiveSampleSize`.
- The builder only reads `GetGlobalCategoryBaselineAsync(...)`; it does not write into corpus tables.
- Automated validation uses the checked-in reduced Snail fixture; the live 39-deck crawl remains manual-only.
