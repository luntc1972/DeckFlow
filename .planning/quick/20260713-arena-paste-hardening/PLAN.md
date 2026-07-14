# Quick Task: Arena-format paste hardening

**Source:** `.planning/deck-import-sites-research.md` Tier-0 (usage-verdict: the only build-now item).
**Goal:** Pasted exports from MTG Arena, MTGGoldfish `arena_download`, ManaBox, TappedOut-Arena, legacy `.dec` parse successfully instead of throwing (or silently mis-parsing) in the paste cascade.

## Grounded failure modes (verified in source)

1. **`About` / `Name <deckname>` preamble** (MTGGoldfish arena_download; some Arena exports): first content lines before any card entry. `MoxfieldParser.ParseText` either throws (`Unable to parse Moxfield line …` — the `foundEntries=false` path at MoxfieldParser.cs:83-91) or, because `allowImplicitQuantity: true`, may silently parse `About` as a card named "About". Both wrong.
2. **`SB:` line prefix** (legacy `.dec`): `SB: 2 Duress` — quantity regex `^(?<quantity>\d+)\s+` can't match; throws or garbage.
3. Bare `Deck`/`Sideboard`/`Commander` headers (no colon) ALREADY parse (`IsSectionHeader` trims `:`) — pin with regression tests, no code change.
4. Loader failure message (`DeckEntryLoader.cs:155`) claims only Moxfield/Archidekt formats — stale once Arena works.

## Changes

### DeckFlow.Core/Parsing/MoxfieldParser.cs
- Treat `About` (normalized `TrimEnd(':')`, OrdinalIgnoreCase) as an ignorable label line (same list as `Deck`/`Commander` in `IsIgnorableLine`). No MTG card is named "About".
- Ignore a line matching `^Name\s+.+` **only while `foundEntries == false`** (preamble region) — Arena/Goldfish deck-name line. No MTG card name starts with the word `Name` followed by a space; the guard keeps any weird future card safe once entries exist.
- `SB:` prefix support: in the parse loop, when a trimmed line starts with `SB:` (OrdinalIgnoreCase), strip the prefix, parse the remainder as a normal entry, force `board = "sideboard"` for that entry only (do not change the running `board` state).
- Do NOT touch `PromoteTrailingCommanderBlock` or the Cockatrice commander-misclassification issue (separate known issue, out of scope).

### DeckFlow.Core/Loading/DeckEntryLoader.cs
- Update the `InvalidOperationException` message at :155 to include Arena: "…not recognized as a Moxfield URL, Archidekt URL, or a Moxfield, Archidekt, or MTG Arena deck export."
- No cascade changes — MoxfieldParser (first in cascade) absorbs the new grammar.

### Tests — DeckFlow.Core.Tests/ParserTests.cs (+ DeckEntryLoaderTests.cs if cascade-level cases fit better)
New cases (happy + edge per testing rules):
1. MTGGoldfish arena_download shape: `About\nName 8 Rack\n\nDeck\n4 Thoughtseize (2XM) 109\n…\nSideboard\n2 Duress` → parses; no card named "About"/"Name 8 Rack"; boards correct.
2. Pure Arena export with set/collector: `Deck\n1 Sol Ring (C21) 263\n…\nCommander\n1 Atraxa, Praetors' Voice (2X2) 190` → commander board correct.
3. Legacy `.dec`: mainboard lines + `SB: 2 Duress` → sideboard entry qty 2, running board untouched for following lines.
4. Regression pins: bare `Deck`/`Sideboard` headers (no colon) still parse; existing Moxfield bulk-edit fixtures unaffected.
5. Edge: `Name` line appearing AFTER entries is NOT swallowed by the new preamble rule (falls through to existing IsNonDeckTextLine behavior).
6. Loader-level: pasted Goldfish-arena text routes through cascade successfully.

### User-facing text (same change, per task instruction)
- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` (~:165), `DeckConvert.cshtml` (~:56), `DeckSync.cshtml` (~:101,136), `DeckComparison.cshtml` (:228 "same sources" sentence): where accepted formats are enumerated, add MTG Arena export mention.
- `DeckFlow.Web/Help/*.md`: only pages that enumerate accepted paste formats (grep "Moxfield or Archidekt URL" / "export text") — add Arena to the enumeration; do not reword pages that merely say "paste".
- `README.md`: bullet under `### Unreleased` describing Arena-format paste support.

## Side Effects Report (localized)
- **Blast radius:** MoxfieldParser.ParseText — consumed by every paste flow (analysis, comparison, primer, convert, sync, manabase, meta-gap) via DeckEntryLoader; changes are additive-only (new inputs accepted; existing inputs parse identically). Loader message string consumers: UI error display only.
- **Shared state:** none. **External surfaces:** none (no HTTP/DB).
- **Contract:** IParser signature unchanged; DeckParseException still thrown for genuinely unparseable text.
- **Tests updated/added:** ParserTests + possibly DeckEntryLoaderTests; no existing test should change behavior.
- **Back-compat risk:** silently-accepted-garbage risk bounded: `About` fixed label; `Name ` rule gated to pre-entry region.
- **Open questions:** none blocking.

## Definition of done
Build 0/0 via Windows dotnet.exe; Core + Web suites green; changed-lines format gate clean; LF preserved; README + hints updated; /simplify run; commit on `quick/arena-paste-hardening`.
