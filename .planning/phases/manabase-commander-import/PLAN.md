---
phase: manabase-commander-import
title: Robust Commander Detection on Import
author: Claude (plan) / Codex (implementation)
created: 2026-07-11
waves: 4
requirements: [D-01, D-02, D-03, D-04, D-05, D-06]
must_haves:
  truths:
    - Moxfield MTGO and Plain-Text pastes (commander as lone trailing line after SIDEBOARD) flag the commander correctly; it is NOT dropped to sideboard and NO mainboard card is mis-flagged.
    - IsCommanderEligible lives in one shared DeckFlow.Core helper, accepts Legendary Creature / Vehicle / Planeswalker-with-text / Background, and is called by both DeckAnalysisPacketService and ManabaseAnalysisService.
    - When no eligible commander resolves, the manabase page shows a user-facing commander picker (not a silent log, not a wrong guess).
    - Partner/background pairs (up to 2 commanders) survive every code path unchanged.
    - Core stays Scryfall-free; all existing tests pass; new Core + Web tests added; README updated.
---

# Plan: Robust Commander Detection on Import

Fixes the reproduced defect where Moxfield "Copy for MTGO" / "Copy Plain Text" bury the
commander on the sideboard and a wrong card is inferred. Adds a shared eligibility validator and
a user-facing picker fallback. See CONTEXT.md for locked decisions D-01..D-06.

> **Codex dispatch note:** danger-full-access, approval never, cwd repo root. Preserve LF line
> endings (`.gitattributes`: `* text=auto eol=lf`) — no CRLF. Scope-fence to the files listed per
> task; ignore unrelated repo work. Build with Windows `dotnet.exe`; also build DeckFlow.Web.Tests
> and DeckFlow.Core.Tests. This is a UI-touching change → after Wave 3, render + screenshot the
> manabase picker at desktop + mobile across themes (Wave 4).

---

## Wave 1 — Core primitives (parallel, no deps)

<task id="W1-A" type="tdd">
<objective>Shared commander-eligibility helper in Core, extended for Background.</objective>
<read_first>
- DeckFlow.Web/Services/DeckAnalysisPacketService.cs (lines ~1959-1979: IsCommanderEligible, IsLegendaryType, NormalizeOracleText)
- DeckFlow.Core/Manabase/ManabaseClassifier.cs (line ~1544: IsLegendary — dedup target)
- DeckFlow.Core/Parsing/MoxfieldParser.cs (namespace/style reference for a new Core static class)
</read_first>
<action>
Create DeckFlow.Core/Loading/CommanderEligibility.cs — a public static class with
`bool IsEligible(string typeLine, string? oracleText)`. Port the logic from
DeckAnalysisPacketService.IsCommanderEligible (ref DeckAnalysisPacketService.cs:1959,1977): eligible
when typeLine is Legendary Creature OR Legendary Vehicle OR (Planeswalker AND oracleText contains
"can be your commander"). ADD Background eligibility with a PRECISE predicate (review finding 4 —
NOT a loose `Contains("Background")`): require Legendary AND Enchantment AND a `Background`
type/subtype token (split the type line on spaces / the `—` subtype separator and match the token,
so a card merely NAMED "...Background..." or a non-legendary Background does not qualify). Keep an
internal `IsLegendaryType(typeLine, requiredType)` helper. Do not depend on Scryfall types — take
plain strings. **Oracle-text note (review finding 8):** the caller is responsible for passing
already-joined multi-face oracle text (mirror DeckAnalysisPacketService.NormalizeOracleText:1998);
CommanderEligibility itself just consumes the string.
</action>
<acceptance_criteria>
- DeckFlow.Core/Loading/CommanderEligibility.cs exists with `public static bool IsEligible(string, string?)`.
- Unit tests (DeckFlow.Core.Tests): Legendary Creature → true; Legendary Vehicle → true; Planeswalker w/ "can be your commander" → true; `Legendary Enchantment — Background` → true; plain Creature / Land / Instant → false; a card named "Background Story" (non-Background type) → false; a non-legendary Enchantment — Background → false.
- `dotnet build DeckFlow.Core` clean.
</acceptance_criteria>
</task>

<task id="W1-B" type="tdd">
<objective>MoxfieldParser recognizes the trailing-commander block (MTGO/Plain-Text).</objective>
<read_first>
- DeckFlow.Core/Parsing/MoxfieldParser.cs (blank-line handling 36-45; TryGetBoardHeader 230-264)
- CONTEXT.md (D-02, and the repro fixture under <specifics>)
</read_first>
<action>
In MoxfieldParser.ParseText, detect Moxfield's commander-last convention (blank-line reset claim
CONFIRMED by review — MoxfieldParser.cs:38 only resets board when commanderSectionActive). TIGHTEN
the rule to avoid false positives (review finding 5): promote a trailing block to
`Board = "commander"` ONLY when ALL hold — (a) it is the FINAL parseable card block in the input,
(b) it comes AFTER a Sideboard/Maybeboard section that already has ≥1 entry, (c) it is separated
from that section by at least one blank line, (d) it contains 1–2 lines, all quantity 1, no header,
(e) there are NO subsequent parseable card lines. Do not disturb decks with an explicit Commander
header/#commander tag or no qualifying trailing block. Keep the change structural — no card
identity/type checks in Core (eligibility validation happens post-resolve in W2-E, which will clear
a wrongly-promoted trailing card).
</action>
<acceptance_criteria>
- New DeckFlow.Core.Tests fixture using the real Winota MTGO/Plain-Text paste (mainboard, `SIDEBOARD:`, blank line, `1 Winota, Joiner of Forces`) → Winota parsed with Board == "commander"; no sideboard card promoted; mainboard cards unchanged.
- A 2-line trailing block (partners) → both entries Board == "commander".
- A normal deck with an explicit `Commander` header still yields exactly that commander (no regression).
- FALSE-POSITIVE GUARDS: a deck whose sideboard is exactly 1 card (no separate trailing block) → that card stays sideboard, NOT commander. A 2-card maybeboard as the final block → stays maybeboard. A trailing block followed by more card lines → not promoted.
- Existing MoxfieldParser tests still pass.
</acceptance_criteria>
</task>

<task id="W1-C" type="tdd">
<objective>Document CommanderInference's partner-guard limitation with a regression fixture (structural fix deferred to W2-E).</objective>
<read_first>
- DeckFlow.Core/Loading/CommanderInference.cs (Take(2) at 46; alphabetical guard 49-57)
</read_first>
<action>
**Review finding 1 (HIGH):** Core `CommanderInference` has only structure + names — with no
Scryfall it CANNOT reliably tell "partner 2 + mainboard card" from "commander + alphabetized
mainboard card". Do NOT attempt a structural partner fix here (it would regress the alphabetized
single-commander case). Instead: (a) leave the existing guard behavior intact, (b) add a regression
test that PINS current behavior and documents the limitation, (c) the real partner preservation is
handled post-resolve in W2-E using eligibility. Add a short code comment pointing to W2-E.
</action>
<acceptance_criteria>
- DeckFlow.Core.Tests: a test pins current CommanderInference output for (i) an alphabetized mainboard (first card only) and (ii) two leading non-alphabetical legendaries, with a comment noting eligibility-based partner recovery happens in ManabaseAnalysisService.
- No behavior change to CommanderInference; all existing CommanderInference tests pass.
</acceptance_criteria>
</task>

## Wave 2 — Service wiring (depends on Wave 1)

<task id="W2-D" type="execute">
<objective>DeckAnalysisPacketService uses the shared eligibility helper (dedup).</objective>
<read_first>
- DeckFlow.Web/Services/DeckAnalysisPacketService.cs (IsCommanderEligible ~1959)
- DeckFlow.Core/Loading/CommanderEligibility.cs (from W1-A)
</read_first>
<action>
Replace the private IsCommanderEligible/IsLegendaryType usage with a call to
CommanderEligibility.IsEligible(typeLine, oracleText). Remove the now-dead private copies. Keep
behavior identical for existing cases (creature/vehicle/planeswalker) and gain Background support.
</action>
<acceptance_criteria>
- DeckAnalysisPacketService references CommanderEligibility.IsEligible; private IsCommanderEligible removed.
- `dotnet build DeckFlow.Web` clean; existing DeckAnalysisPacketService tests pass.
</acceptance_criteria>
</task>

<task id="W2-E" type="execute">
<objective>ManabaseAnalysisService validates inferred commander(s) + computes eligible list.</objective>
<read_first>
- DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs (ReflagInferredCommanders 659; resolve loop 481-511; AnalyzedBoards 143)
- DeckFlow.Web/Services/Manabase/ScryfallCardDataMapper.cs (TypeLine + OracleText present)
- DeckFlow.Core/Loading/CommanderEligibility.cs
</read_first>
<action>
After Scryfall resolution, for each entry currently flagged IsCommander, validate with
CommanderEligibility.IsEligible(card.TypeLine, joinedOracleText) — join multi-face oracle text the
same way DeckAnalysisPacketService.NormalizeOracleText:1998 does (review finding 8). Clear
IsCommander when not eligible (kills the Academy Rector false pick). This is ALSO where partner
recovery lives (per W1-C): validate up to the two leading candidates by eligibility, keeping both
when both pass. Compute the set of commander-eligible resolved cards in the deck and surface it to
the controller (for the picker). When zero valid commanders remain, signal "selection required" up
the call chain. **Result-contract change (review finding 2, HIGH):** `ManabaseAnalysisResult`
currently requires a non-null `ManabaseReport` — the service cannot return "no report, picker
required" as-is. Change the contract: either make the report nullable OR introduce a
`ManabaseAnalysisOutcome` carrying `Report` (nullable), `CommanderSelectionRequired` (bool), and
`CommanderChoices` (eligible card names). Update ManabaseController.cs:96 accordingly. Preserve
up-to-2 commanders (D-06).
</action>
<acceptance_criteria>
- ManabaseAnalysisResult/Outcome can represent "picker required, no report" without a null-ref; ManabaseController handles it.
- DeckFlow.Web.Tests: MTGO/Plain-Text Winota paste → analysis reports Winota as commander (CommanderCount 1), Academy Rector NOT commander.
- A deck where inference picks a non-legendary → that flag cleared; outcome signals selection-required with a non-empty eligible-cards list and no null-ref.
- Partner deck → CommanderCount 2 preserved (both recovered by eligibility).
- `dotnet build DeckFlow.Web` clean.
</acceptance_criteria>
</task>

## Wave 3 — UI picker fallback (depends on Wave 2) — UI CHANGE

<task id="W3-F" type="execute">
<objective>Request/ViewModel/Controller wiring for commander selection.</objective>
<read_first>
- DeckFlow.Web/Models/ManabaseRequest.cs, DeckFlow.Web/Models/ManabaseViewModel.cs
- DeckFlow.Web/Controllers/ManabaseController.cs (RunAnalysisAsync mapping ~178; render ~96)
- DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs (ManabaseAnalysisOptions ~51-67)
- DeckFlow.Web/Controllers/DeckConvertController.cs:101 (SearchCommandersAsync backstop) + Services/Scryfall/CardSearchService.cs:119
</read_first>
<action>
Add `SelectedCommander` (string?) to ManabaseRequest. **Review finding 3 (MED):** ALSO add the
selected commander to `ManabaseAnalysisOptions` (~ManabaseAnalysisService.cs:51-67) and map it in
ManabaseController.RunAnalysisAsync (~:178) — a field on ManabaseRequest alone never reaches the
service. Add `CommanderSelectionRequired` (bool) and `CommanderChoices` (IReadOnlyList<string>) to
ManabaseViewModel. When the analysis outcome signals selection-required, populate CommanderChoices +
set the flag; when the user re-submits with SelectedCommander, thread it through Options so that card
is flagged commander (validated by CommanderEligibility). **Review finding 7 (LOW) — autocomplete
backstop:** do NOT reference ScryfallCommanderSearchService; the convert pattern uses
`ICardSearchService.SearchCommandersAsync` (`is:commander name:{query}`, CardSearchService.cs:119).
Add a `/manabase/commander-search` GET action that reuses `ICardSearchService.SearchCommandersAsync`
(no new service).
</action>
<acceptance_criteria>
- ManabaseRequest.SelectedCommander AND ManabaseAnalysisOptions selected-commander field both exist; controller maps request→options.
- ManabaseViewModel has CommanderSelectionRequired + CommanderChoices.
- `/manabase/commander-search` returns commander name suggestions via ICardSearchService.SearchCommandersAsync.
- DeckFlow.Web.Tests: posting a selection-required deck yields CommanderSelectionRequired=true and non-empty CommanderChoices; re-posting with SelectedCommander=Winota yields Winota as commander (proving request→options→service threading).
- `dotnet build DeckFlow.Web` + DeckFlow.Web.Tests clean.
</acceptance_criteria>
</task>

<task id="W3-G" type="execute">
<objective>Manabase view picker UI, reusing the deck-sync.ts data-attribute autocomplete.</objective>
<read_first>
- DeckFlow.Web/Views/Deck/Manabase.cshtml (form 40-166; result panel 168+)
- DeckFlow.Web/Views/Deck/DeckConvert.cshtml:79 (input[data-commander-search] usage)
- DeckFlow.Web/wwwroot/ts/deck-sync.ts (~:2338 — the reusable input[data-commander-search] helper)
- CLAUDE.md (theme CSS: layout goes in site-common.css, not site.css)
</read_first>
<action>
When CommanderSelectionRequired, render a picker: a dropdown/select of CommanderChoices (the deck's
eligible cards) bound to SelectedCommander, plus an `input[data-commander-search]` autocomplete as a
backstop pointing at `/manabase/commander-search`, with a clear prompt ("We couldn't identify your
commander — pick it"). Wire re-submit to re-run the analysis. **Review finding 6 (MED):** reuse the
`input[data-commander-search]` helper in deck-sync.ts (~:2338) — NOT commander-search.ts, which is a
different hardcoded helper (`#commander-search-input`, `/commander-categories/search`). Any layout
CSS goes in site-common.css.
</action>
<acceptance_criteria>
- Manabase.cshtml renders the picker only when CommanderSelectionRequired; autocomplete uses input[data-commander-search] → /manabase/commander-search.
- TS compiles (tsc via MSBuild) with no errors; no committed .js.
- Picker submits SelectedCommander back to the analyze action.
</acceptance_criteria>
</task>

## Wave 4 — Docs, tests, visual verify (depends on Wave 3)

<task id="W4-H" type="execute">
<objective>README, full test suite, UI review.</objective>
<read_first>
- README.md (import/manabase sections)
- feedback: web-page change → tests+themes+mobile; UI review after every UI change
</read_first>
<action>
Update README to document commander detection across Moxfield/Archidekt exports + the picker
fallback. Run the full suite (DeckFlow.Core.Tests + DeckFlow.Web.Tests via dotnet.exe;
Playwright e2e with DECKFLOW_DISABLE_AUTO_BROWSER + admin creds). Render + screenshot the manabase
picker at desktop (1280) and mobile (390) across at least the default + one guild theme.
</action>
<acceptance_criteria>
- README mentions commander detection + picker fallback.
- All Core + Web tests pass; TS + format gate clean.
- Screenshots captured at 2 viewports; picker readable + no horizontal overflow on mobile.
</acceptance_criteria>
</task>

---

## Out of scope (deferred — see CONTEXT.md)
- Partner-legality keyword enforcement (trust picker + URL-validated pairs).
- Archidekt alt-format exact parsing / `1x` quantity — picker covers; fold in once real Archidekt
  exports are sampled.
