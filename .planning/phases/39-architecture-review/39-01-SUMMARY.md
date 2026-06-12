# 39-01 Summary

## Pre-flight gate

- Ran:
  `grep -rn "parse failed\|not recognized\|DeckParseException\|InvalidOperationException" DeckFlow.Web.Tests/MetaGapServiceTests.cs DeckFlow.Web.Tests/DeckComparisonServiceTests.cs DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs DeckFlow.Web.Tests/DeckPrimerPacketServiceTests.cs`
- Result: pass. No asserted final-failure message would change under the shared-loader migration.
- Final-failure behavior preserved per service:
  - `MetaGapService`: propagated second `DeckParseException` still reaches the outer wrapper and remains `Deck parse failed: {DeckParseException.Message}`.
  - `DeckComparisonService`: still uses the generic unrecognized message inside the loader cascade, then wraps it as `{deckLabel} parse failed: The submitted deck was not recognized as a Moxfield URL, Archidekt URL, Moxfield export, or Archidekt export.`.
  - `DeckAnalysisPacketService`: still surfaces `InvalidOperationException("The submitted deck was not recognized as a Moxfield URL, Archidekt URL, Moxfield export, or Archidekt export.")`.
  - `DeckPrimerPacketService`: still surfaces `InvalidOperationException("The submitted deck was not recognized as a Moxfield URL, Archidekt URL, Moxfield export, or Archidekt export.")`.

## Loader design

- Extended `IDeckEntryLoader` additively only.
- Added:
  - `DeckSourceLoadResult(List<DeckEntry> Entries, string? FallbackNotice)`
  - `UnrecognizedPasteBehavior`
  - `LoadFromSourceAsync(string deckSource, UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized, CancellationToken cancellationToken = default)`
- Enum behavior:
  - `ThrowNotRecognized` is the default and preserves Comparison/Analysis/Primer behavior.
  - `PropagateParseException` is passed only by MetaGap and preserves its propagating-second-parser behavior.
- URL importer arg behavior:
  - All callers now pass the original `deckSource` string to importers.
  - MetaGap’s previous trimmed URL argument was removed as planned; this is behavior-neutral for parsed entries and cache identity.

## Service migration

- `DeckComparisonService`
  - Removed fields/ctor params: `_moxfieldDeckImporter`, `_archidektDeckImporter`, `_moxfieldParser`, `_archidektParser`
  - Retained: none of those deck-loading dependencies were used elsewhere
  - Now calls `_deckEntryLoader.LoadFromSourceAsync(deckSource, cancellationToken: cancellationToken)`

- `MetaGapService`
  - Removed fields/ctor params: `_moxfieldDeckImporter`, `_archidektDeckImporter`, `_moxfieldParser`, `_archidektParser`
  - Retained: none of those deck-loading dependencies were used elsewhere
  - Now calls `_deckEntryLoader.LoadFromSourceAsync(deckSource, UnrecognizedPasteBehavior.PropagateParseException, cancellationToken)`

- `DeckAnalysisPacketService`
  - Removed fields/ctor params: `_moxfieldDeckImporter`, `_archidektDeckImporter`, `_moxfieldParser`, `_archidektParser`
  - Retained: none of those deck-loading dependencies were used elsewhere
  - `_lastImportNotice` flow preserved by assigning `loaded.FallbackNotice` immediately after each shared-loader call, matching the former reset+set location semantics

- `DeckPrimerPacketService`
  - Removed fields/ctor params from the production constructor: `_moxfieldDeckImporter`, `_archidektDeckImporter`, `_moxfieldParser`, `_archidektParser`
  - Retained:
    - `_loadDeckEntriesAsyncOverride` test seam
    - nullable `_deckEntryLoader` field for the production path while the override-only test constructor remains supported
  - `_lastImportNotice` flow preserved:
    - override path explicitly sets `_lastImportNotice = null`
    - loader path assigns `loaded.FallbackNotice` immediately after the shared-loader call

## Wiring and verification

- `Program.cs` now resolves `IDeckEntryLoader` for Comparison, MetaGap, Analysis, and Primer.
- `TestServiceFactory` now builds a real `DeckEntryLoader` from the existing fake importers/parsers for Comparison, MetaGap, and Analysis.
- Zero private `LoadDeckEntriesAsync` methods remain across the four packet services.
- Cache-key helper methods were not edited.
- `Services/PromptBuilders/**` was not edited.
