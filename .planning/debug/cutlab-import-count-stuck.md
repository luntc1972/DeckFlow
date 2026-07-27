---
status: diagnosed
trigger: "after pasting 100 cards in and trying import you get the error message for need enough cards, but if I add more on import it keeps the error message"
created: 2026-07-25
updated: 2026-07-25
---

# Debug Session: cutlab-import-count-stuck

## Symptoms

- **Expected behavior:** Pasting a decklist with enough non-commander cards (101-150) into Cut Lab's import box and clicking Import should load the pool and proceed past intake.
- **Actual behavior:** Import shows the error "This pool already has 100 cards or fewer — Cut Lab is for trimming an oversized pool down to 100. Try Deck Sync or Deck Analysis instead." (from `CutLabPoolValidator.ValidateCardCount`, `DeckFlow.Web/Services/CutLab/CutLabPoolValidator.cs:34`, thrown when non-commander count < `MinPoolCards` = 101). User then edits the SAME textarea to add more card lines and re-clicks Import — the identical error message persists, even though the pasted text now has more cards.
- **Error message (verbatim, user-confirmed):** "pool already has 100 cards or fewer"
- **Import method:** Pasted decklist text (not a Moxfield/Archidekt URL import).
- **Retry action taken:** Edited the same textarea (added more card lines), then re-clicked Import — did not reload the page or start a new session first.
- **Timeline:** Regression — user reports Cut Lab import used to work before; broken recently.

## Known related context (from memory, NOT yet confirmed as same root cause)

- A prior, DIFFERENT bug was root-caused 2026-07-21 (`Cut Lab 100-card error root cause: Moxfield v3 import falls back to mainboard-only`): server-side Moxfield v3 **URL** fetch hits a network block, falls back to Commander Spellbook proxy which strips sideboard/maybeboard, landing at 99 non-commander cards. That was for URL import, not pasted text — do not assume same cause; verify independently.
- `CutLabPoolValidator.MinPoolCards` = 101, `MaxPoolCards` = 150 (non-commander count, commander excluded) — `DeckFlow.Web/Services/CutLab/CutLabPoolValidator.cs`.

## Current Focus

hypothesis: CONFIRMED — see Resolution.root_cause
test: complete
expecting: n/a
next_action: none — diagnose-only run complete; hand off root cause to fix dispatch (Codex, per project policy). No source files were edited during this investigation.

## Evidence

- timestamp: 2026-07-25
  checked: `DeckFlow.Web/Controllers/CutLabController.cs` (`Process` action) and `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` (`ProcessAsync`)
  found: On every POST to `/cut-lab`, `deckSource` is built fresh from `request.DeckText` (via `DeckInputReconciler.Reconcile`) and `_deckEntryLoader.LoadFromSourceAsync(deckSource)` is called fresh — no server-side caching keyed independent of content. `nonCommanderCardCount` (`CutLabPageService.cs:232`) is recomputed from freshly parsed `analyzedEntries` every request, then validated via `CutLabPoolValidator.ValidateCardCount` (`CutLabPageService.cs:235-242`).
  implication: Server does NOT reuse a stale cached count across requests — the "state-only restore" shortcut in `CutLabController.Process` (lines 52-58) only fires when both `DeckText` and `DeckUrl` are blank, which is not the case on a real resubmit with edited text. Rules out a server-side result cache as the mechanism.

- timestamp: 2026-07-25
  checked: `DeckFlow.Web/wwwroot/ts/cut-lab.ts` (`attachSubmitHandler`, `writeStateToHiddenInput`) and `DeckFlow.Web/wwwroot/ts/deck-sync.ts` (`attachGenericPersistedForms`, `persistFormState`/`hydrateFormState`) and `DeckFlow.Web/wwwroot/ts/deck-input-store.ts` (`restoreSplitFields`)
  found: The main Cut Lab intake form (`form[data-cache-key="cut-lab"]`) submits as a real native HTML POST (full page navigation) — `attachSubmitHandler` only overwrites the hidden `CutLabStateJson` field before submit, it never calls `preventDefault()`. The generic sessionStorage form-state restore (`deck-sync.ts` `hydrateFormState`) and the deck-input carry-over (`deck-input-store.ts` `restoreSplitFields`) both only restore fields when the corresponding field is CURRENTLY BLANK at page load; since the error re-render always echoes back `@Model.Request.DeckText` (the just-submitted, edited text) into the textarea, these restore paths are no-ops and cannot overwrite the user's edited textarea content.
  implication: Rules out client-side stale-cache/restore mechanisms (sessionStorage persistence, deck-input-store carry-over, busy-indicator overlay) as the cause — the browser genuinely POSTs the current, edited textarea contents to the server on every Import click.

- timestamp: 2026-07-25
  checked: `DeckFlow.Core/Parsing/MoxfieldParser.cs` lines 61-64 + 399-407, and `DeckFlow.Core/Parsing/ArchidektParser.cs` lines 40-43 + 307-315 (`IsStoppingLine`)
  found: Both text parsers contain identical logic: `if (IsStoppingLine(line) && foundEntries) { break; }`. `IsStoppingLine` matches a line (after trimming and stripping a trailing `:`) equal to "Possible names", "Possible name", "Notes", "Description", or "Primer" (case-insensitive). Once ANY entry has already been parsed and one of these lines is encountered, the parser immediately stops consuming the rest of the input — permanently, for the remainder of the text — with no error, warning, or indication that lines were skipped.
  implication: falsifiable mechanism identified — verified empirically next.

- timestamp: 2026-07-25
  checked: Built a throwaway console app (in scratchpad, `ProjectReference` to `DeckFlow.Core.csproj`, no repo files touched) calling `MoxfieldParser.ParseText` directly with (a) a decklist containing a commander + 100 mainboard cards + a trailing "Notes" line, and (b) the same text with 20 additional card lines appended AFTER the "Notes" line (simulating a user retrying Import by appending more cards below an existing paste that ends in a Notes/Description block).
  found: First paste parsed to 101 entries (100 mainboard + 1 commander) as expected. Second paste — despite having 20 additional card lines appended — ALSO parsed to exactly 101 entries. The 20 appended lines were silently discarded because the parser `break`s at the "Notes" line before ever reaching them.
  implication: CONFIRMED. This directly reproduces the reported symptom: `CutLabPageService.ProcessAsync` recomputes `nonCommanderCardCount` from the parser's output every request (see evidence above — no server-side staleness), but if the user's pasted text contains one of the five stopping-line keywords and they append new card lines below it, the parser returns the IDENTICAL entry set on the retry, so `CutLabPoolValidator.ValidateCardCount` throws the exact same "100 cards or fewer" error every time, no matter how many cards are added — matching "if I add more on import it keeps the error message" exactly.

## Eliminated

- hypothesis: A server-side cache (memory cache, scoped-service reuse, or the CutLabStateJson "state-only restore" shortcut) revalidates against a stale prior card count instead of the freshly submitted DeckText.
  evidence: `CutLabPageService.ProcessAsync` re-parses `deckSource` from `request.DeckText` on every call (no caching); `ICutLabPageService` is registered `Scoped` (`Program.cs:181`, fresh instance per request); the state-only restore shortcut in `CutLabController.Process` requires `DeckText`/`DeckUrl` both blank, which is false on a real resubmit.
  timestamp: 2026-07-25

- hypothesis: A client-side JS mechanism (generic form-state sessionStorage persistence, `deck-input-store.ts` last-deck carry-over, or the busy-indicator overlay) reverts or blocks the textarea so the browser actually resubmits the OLD (pre-edit) DeckText.
  evidence: The main Cut Lab form has no `submit` interception with `preventDefault()` — it is a genuine full-page POST. `hydrateFormState` and `restoreSplitFields` both guard on the target field being blank at load time; since the error re-render always echoes the just-submitted `DeckText` back into the textarea (non-blank), neither restore path fires. Busy-indicator overlay defaults to `hidden` and is reset on `pageshow`.
  timestamp: 2026-07-25

## Resolution

root_cause: |
  `MoxfieldParser.ParseText` (`DeckFlow.Core/Parsing/MoxfieldParser.cs:61-64`) and
  `ArchidektParser.ParseText` (`DeckFlow.Core/Parsing/ArchidektParser.cs:40-43`) both contain:

      if (IsStoppingLine(line) && foundEntries)
      {
          break;
      }

  `IsStoppingLine` (`MoxfieldParser.cs:399-407`, identically `ArchidektParser.cs:307-315`) matches a
  line — after trimming and stripping a trailing `:` — equal (case-insensitive) to any of:
  "Possible names", "Possible name", "Notes", "Description", "Primer".

  Once at least one card entry has already been parsed (`foundEntries == true`) and the parser
  reaches one of these five keyword lines, it immediately `break`s out of the line loop — permanently
  discarding every remaining line in the pasted text, with no exception, warning, or any signal that
  content was dropped. `DeckEntryLoader.LoadFromSourceAsync` (`DeckFlow.Core/Loading/DeckEntryLoader.cs:136`)
  calls `_moxfieldParser.ParseText(deckSource)` for plain-text paste input and returns this truncated
  entry list as a clean success (no `DeckParseException`), so the Archidekt parser fallback is never
  attempted and nothing downstream knows lines were skipped.

  `CutLabPageService.ProcessAsync` (`DeckFlow.Web/Services/CutLab/CutLabPageService.cs:190-242`)
  recomputes `nonCommanderCardCount` from these (silently truncated) `analyzedEntries` on every
  request — confirmed via evidence above that there is no server- or client-side staleness/caching
  bug. Reproduced directly against the production parser (throwaway console app, scratchpad-only, no
  repo files touched): a decklist ending in a "Notes" line parsed to 101 entries; appending 20 more
  card lines AFTER that "Notes" line still parsed to exactly 101 entries — the appended lines never
  reached the parser loop.

  Mechanism -> symptom: a user whose pasted decklist text contains (or is followed by) one of the five
  stopping-line keywords, who then appends additional card lines below that point to try to push the
  non-commander count from <=100 up into Cut Lab's required 101-150 range, will see
  `CutLabPoolValidator.ValidateCardCount` (`DeckFlow.Web/Services/CutLab/CutLabPoolValidator.cs:30-35`)
  throw the exact same "This pool already has 100 cards or fewer..." error on every retry, no matter
  how many cards are added — because the parser never reaches the newly appended lines to count them.

fix: (not applied — diagnose-only run per project policy; fix to be implemented by Codex dispatch)
verification: (not applicable — no fix applied in this session)
files_changed: []
