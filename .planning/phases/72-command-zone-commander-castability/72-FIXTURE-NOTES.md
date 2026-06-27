# Phase 72 Fixture Notes

## Sources

- Moxfield fixture: synthetic file at `DeckFlow.Core.Tests/Fixtures/moxfield-companion-direct.json`
- Archidekt fixture: real capture from `https://archidekt.com/api/decks/3674983/`
- Sample Archidekt deck URL: `https://archidekt.com/decks/3674983`

## Moxfield Ground Truth

- The direct importer reads top-level board objects on the root payload, not a `root.boards` wrapper.
- The companion board key is `companions`.
- The companion name path is `root.companions.<slot>.card.name`.
- Each board is an object shaped as `{ slotId: { quantity, card: { name, set, cn, isFoil? } } }` for importer purposes.
- This fixture is synthetic because live Moxfield direct API capture is Cloudflare-blocked from this environment.
- The synthetic schema was matched to `DeckFlow.Core/Integration/MoxfieldApiDeckImporter.cs` `AddBoardEntries`, which reads `root.commanders`, `root.mainboard`, `root.sideboard`, `root.maybeboard`, and now the companion board at the same level.
- Sample synthetic companion card: `Jegantha, the Wellspring` (`iko` / `189`).

## Archidekt Ground Truth

- Deck ID `3674983` is `Wilson, Refined Grizzly // Passionate Archaeologist`.
- The real Archidekt payload contains `Passionate Archaeologist` with `categories = ["Commander"]`.
- The same card reports `oracleCard.subTypes = ["Background"]`.
- Consequence: a real Archidekt Background arrives on the `commander` board through the existing importer because `DetermineBoard` routes the `Commander` category to the commander board.
- Consequence: `Commander` is treated as a board category and stripped from `DeckEntry.Category`.
- This contradicts the original plan assumption that a Background would arrive as a mainboard entry with category preserved.

## Archidekt Companion Correction

- Archidekt has no reliable `Companion` category for detection.
- Across real companion decks examined during phase research, companions were tagged as `Sideboard` or were entered as `Commander`.
- The per-card `companion` boolean was observed as `false`, so it is not a reliable signal.
- Consequence: the Archidekt companion-category detection path is dropped.
- Companion detection should rely on manual designator input plus Moxfield `DetectedCompanionName` only.

## Plan Deviations

- The original plan assumed Moxfield would be captured live. The final fixture is synthetic because the direct API was not capturable from this environment.
- The original plan assumed Archidekt Background/Companion handling depended on preserving category strings on normal deck entries. The real capture shows Background is surfaced as a commander-board card via `categories = ["Commander"]`, and that board category is stripped from `DeckEntry.Category`.
