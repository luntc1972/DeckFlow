# Phase: Robust Commander Detection on Import — Context

**Gathered:** 2026-07-11 (interactive session — this conversation served as discuss-phase)
**Status:** Ready for planning
**Source:** Support-session investigation (Winota deck, Moxfield MTGO/Plain-Text exports)

<domain>
## Phase Boundary

Make the Mana Base analyzer reliably identify the commander from pasted decklists across
every Moxfield/Archidekt export format, and when it still can't, ask the user to pick one
(a user-facing selection, never a silent log). URL import already works and is out of scope
except as the reference behavior to match.

Reproduced defect: Moxfield **Copy for MTGO** and **Copy Plain Text** emit the commander as a
lone trailing line AFTER a `SIDEBOARD:` section with no header. `MoxfieldParser` leaves
`board=sideboard` active across the blank line, so the commander (Winota) is parsed onto the
sideboard and dropped by the analyzer (`AnalyzedBoards = {mainboard, commander}`), while
leading-card inference mis-flags the first alphabetical mainboard card (Academy Rector) as the
commander.
</domain>

<decisions>
## Implementation Decisions (LOCKED)

### D-01 Detection strategy — positional pick + eligibility validate
Keep positional/section-based inference to PICK the commander candidate; reuse property-based
`IsCommanderEligible` to VALIDATE it (reject a candidate that is not a legal commander). User
chose this over pure-property (over-selects among many legendaries) and over adding a mandatory
commander form field. (AskUserQuestion, 2026-07-11.)

### D-02 Parser fix — Moxfield trailing-commander block
`MoxfieldParser` must recognize Moxfield's MTGO/Plain-Text convention: a lone, blank-line-
separated trailing block appearing AFTER a Sideboard/Maybeboard section, containing 1–2 one-of
cards, is the commander block → tag `board=commander` (not sideboard). Structural signal only
(position + isolation); no Scryfall in Core.

### D-03 Shared eligibility helper (+ Background)
Lift `IsCommanderEligible(typeLine, oracleText)` + `IsLegendaryType` out of
`DeckAnalysisPacketService` into a shared `DeckFlow.Core` helper. BOTH DeckAnalysisPacketService
and ManabaseAnalysisService call it. **Extend it to accept the `Background` subtype**
(`Legendary Enchantment — Background`) — a legal second commander it currently misses. Dedup the
scattered `IsLegendary` copies (`ManabaseClassifier.IsLegendary`, `ScryfallSetService`).

### D-04 Service validation
`ManabaseAnalysisService`: after Scryfall resolve, validate each positionally-inferred
`IsCommander` entry against the shared helper (both `TypeLine` and `OracleText` are present on
`ScryfallCardData`); clear the flag when not eligible. Kills the "Academy Rector" false pick.

### D-05 UI picker fallback — surface, do not log
When no valid commander is resolved after D-02/D-04, set a `CommanderSelectionRequired` state on
the manabase view model and render a picker: a dropdown of the deck's own commander-eligible
cards (deterministic, always correct), with the existing commander-search autocomplete as a
backstop. On re-submit the user's pick flags the commander. This is user-facing — NOT a silent
log or a wrong-card guess. Reuse the established `MissingCommander` + `/…/commander-search`
pattern from DeckConvert. **Corrected by plan-review:** the reusable autocomplete helper is the
`input[data-commander-search]` pattern in `deck-sync.ts` (~:2338), NOT `commander-search.ts` (a
different hardcoded helper); the backstop search uses `ICardSearchService.SearchCommandersAsync`
(`is:commander name:`, CardSearchService.cs:119), NOT ScryfallCommanderSearchService. Add a
`/manabase/commander-search` GET reusing that service.

### D-06 Partners / backgrounds — preserve (up to 2)
The stack already models up to 2 commanders end-to-end (`CommanderInference.Take(2)`,
`KarstenManabase` commanderCount "1 or 2 for partners/backgrounds", command-zone in
DeckAnalysis). All four fix parts MUST preserve this: the trailing block captures up to 2 lines;
validation runs per-commander; the picker allows selecting 1 or 2. **Corrected by plan-review:**
Core `CommanderInference` cannot structurally distinguish a partner-2 from an alphabetized
mainboard card without Scryfall — so partner recovery is done POST-RESOLVE in
`ManabaseAnalysisService` via eligibility (validate up to 2 leading candidates, keep both if both
pass). `CommanderInference` behavior is left unchanged (pinned by a regression test).

### Claude's Discretion
- Exact placement of the picker UI on the manabase result/form.
- Shape of the eligible-cards list passed to the view (record vs tuple).
- Whether the trailing-block detection lives fully in the parser or splits parser (block tag) +
  service (eligibility confirm) — implementer's call, but Core stays Scryfall-free.
</decisions>

<canonical_refs>
## Canonical References

### Reuse targets (read before implementing)
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs:1959` — `IsCommanderEligible` / `IsLegendaryType` to lift + extend for Background.
- `DeckFlow.Core/Parsing/MoxfieldParser.cs` — trailing-block fix (blank-line reset at line 36-45; headers 230-264).
- `DeckFlow.Core/Parsing/ArchidektParser.cs` — `[Commander]` category already handled (line 197); alt-format risk covered by picker.
- `DeckFlow.Core/Loading/CommanderInference.cs` — leading-pair inference + alphabetical guard (Take(2), line 46; guard 51-57).
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — `ReflagInferredCommanders` (659), `AnalyzedBoards` (143), resolve loop (481-511).
- `DeckFlow.Web/Controllers/DeckConvertController.cs:95` + `DeckFlow.Web/Views/Deck/DeckConvert.cshtml:79` — `MissingCommander` + `/convert/commander-search` picker pattern.
- `DeckFlow.Web/wwwroot/ts/commander-search.ts` — reusable autocomplete.
- `DeckFlow.Web/Services/Scryfall/ScryfallCommanderSearchService.cs:70` — `is:commander` search backstop.

### Files to modify
- `DeckFlow.Core/Parsing/MoxfieldParser.cs`, `DeckFlow.Core/Loading/CommanderInference.cs`
- NEW `DeckFlow.Core/Loading/CommanderEligibility.cs` (shared helper)
- `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (call shared helper), `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs`
- `DeckFlow.Web/Controllers/ManabaseController.cs`, `DeckFlow.Web/Models/ManabaseRequest.cs`, `DeckFlow.Web/Models/ManabaseViewModel.cs`, `DeckFlow.Web/Views/Deck/Manabase.cshtml`
- `DeckFlow.Web/wwwroot/ts/` (reuse commander-search.ts)
- Tests: `DeckFlow.Core.Tests/` (parser, eligibility, inference), `DeckFlow.Web.Tests/` (picker flow)
- `README.md`
</canonical_refs>

<specifics>
## Specific Ideas
- Repro data (real): Moxfield "Copy for MTGO" and "Copy Plain Text" of deck
  `moxfield.com/decks/ZoogHymnNkOXtPd2I2fwkA` (Winota) — commander is the final lone line after
  `SIDEBOARD:`. Use as a Core parser test fixture.
- Background eligibility fixture: a `Legendary Enchantment — Background` card must pass
  `IsCommanderEligible`.
- Partner fixture: two leading/ trailing legendaries → both flagged, CommanderCount=2.
</specifics>

<deferred>
## Deferred Ideas
- **Partner-legality enforcement** (Partner / Partner with / Friends Forever / Choose a Background /
  Doctor's companion keyword matrix) — MVP trusts the picker + URL-validated pairs. Follow-up.
- **Archidekt alt-format exact parsing** (MTGO/Arena/plain from Archidekt, `1x` quantity prefix) —
  not yet sampled from real exports; the D-05 picker de-risks it. Fold in once real Archidekt
  exports are provided.
</deferred>

---

*Phase: manabase-commander-import*
*Context gathered 2026-07-11 via interactive session (served as discuss-phase)*
