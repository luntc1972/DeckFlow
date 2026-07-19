---
phase: 99-creator-style-artifact-engine
plan: 02
status: complete
completed: 2026-07-18
requirements: [CS-27, CS-29]
key-files:
  created:
    - DeckFlow.Web/Services/CreatorStyle/SubmittedDeckStatsBuilder.cs
    - DeckFlow.Web.Tests/Services/CreatorStyle/SubmittedDeckStatsBuilderTests.cs
  modified: []
---

# Plan 99-02 Summary — SubmittedDeckStatsBuilder (parity-path stats + deck context)

## What was built

- **`ISubmittedDeckStatsBuilder` / `SubmittedDeckStatsBuilder`** (Web, sealed): `BuildAsync(deckSource, ct)` → `SubmittedDeckAnalysis { Stats, DeckContext, Entries, ResolvedCommanderName, ImportNotice }`.
- **Numeric parity with the creator fused-profile pipeline (CS-27):**
  - `category_ratio:{category}` counts reuse **`CategoryCounter.CountPerDeck` directly** (submitted entries adapted into a synthetic `CreatorDeckSample`) — quantity-weighted by construction, identical semantics to the fused profile; categories via `CategoryKnowledgeRepository.GetCategoriesAsync` once per distinct card name; keys off exact `ContentTagVocabulary.CardCategories`.
  - `karsten:*` values replicate `MeasuredStyleProfileBuilder.AnalyzeDeckAsync`: commander re-flag via `CommanderInference.InferLeadingCommanderNames` (sideboard/maybeboard skipped), 75-card `cards/collection` batches + `SearchFallbackCardAsync` fallback, `ScryfallCardFactMapper.ToCardFacts` → `ManabaseClassifier.Classify(facts, isSingleton: true)` → `ManabaseAnalyzer.Analyze(deck, ManabaseMode.Casual)`; `ToHealthScore` Healthy=3/Functional=2/Workable=1/else 0. `// Why:` parity note in place.
  - `combo_density:included_per_deck` = `FindCombosAsync(...).IncludedCombos.Count`; null result or throw → 0, log-and-degrade.
- **`CardGroundingDeckContext`** (CS-29 input, single Scryfall pass): `CommanderColorIdentity` from the resolved commander's Scryfall `color_identity` (WUBRG symbols), `DeckProducedColors` from classified `ManabaseDeck.Sources[].Produces` (not any report property), `DeckCardNames` = `CardNormalizer.Normalize` over analyzed boards. Empty/unresolvable deck → zeroed karsten + empty-but-valid context, never throws.
- **Test seam:** internal ctor with Func overrides per I/O call (deck load, categories, combos, whole manabase-analysis, Scryfall batch + fallback), mirroring the DeckPrimerPacketService dual-ctor. No WebApplicationFactory, no live HTTP.

## Verification

- TDD red-first (CS0246 missing-type failure captured), then green.
- `SubmittedDeckStatsBuilderTests`: 8/8 pass (orchestrator re-run post-review). Covers the 2+1+1 ⇒ 4 quantity-weighted case, board filtering, combo-density null path, deck-size/commander counts, karsten parity vs direct `ManabaseAnalyzer.Analyze(Casual)`, health-score mapping, deck-context shapes, empty-deck safety.
- `DeckFlow.Web` build: 0 new warnings (NU1902 pre-existing only). LF endings clean.
- Acceptance greps: `ManabaseMode.Casual` ✓, `isSingleton: true` ✓, `GetCategoriesAsync` ✓, `IncludedCombos` ✓, `CategoryCounter` reuse ✓, `.Sources/.Produces` ✓, internal test-seam ctor ✓, zero `{ get; }`.

## Deviations

- Orchestrator applied a one-line formatting fix post-review (dangling comma join in the resolution initializer). No logic change.
