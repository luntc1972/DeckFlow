---
phase: 110
plan: 01
title: Card Text Lookup Infrastructure (CLUP-16 backend)
status: complete
completed: 2026-07-24
requirements_addressed: [CLUP-16]
executor: codex (gpt-5.4 medium)
verifier: claude
---

# Plan 110-01 Summary — View-Only Card-Text Lookup

## What was built
A view-only `CardTextByCardName` lookup on `CutLabViewModel`, populated from the
`ScryfallCardData` already resolved at intake — zero new fetches, zero new endpoints.
Plan 110-05 will consume it to render text-first disclosures.

- `sealed record CutLabCardTextView` in CutLabViewModel.cs with exactly five nullable
  string members: `TypeLine`, `ManaCost`, `SetCode`, `CollectorNumber`, `OracleText`
  (all nullable so a partially-resolved card never forces a blank required field — D-31).
- `IReadOnlyDictionary<string, CutLabCardTextView> CardTextByCardName` on `CutLabViewModel`
  and on `CutLabProcessResult`, both defaulting to an empty `StringComparer.OrdinalIgnoreCase`
  dictionary, mirroring the `RoleListByCardName` pattern (D-28).
- `CutLabPageService.BuildCardTextByCardName(pool, preResolvedCards)`: builds an internal
  normalized join map via `CutLabCardNames.ToLastWinsDictionary` (keys normalized inside the
  helper), then emits the final dictionary keyed by the pool card's DISPLAY `Name`
  (OrdinalIgnoreCase), mapping `ScryfallCardData.Set` → `SetCode`.
- `CutLabViewModel.From` reads `result.CardTextByCardName` into the new VM property.

## HIGH-1 keying fix (Codex-review finding)
The emitted dictionary is keyed by the card's DISPLAY name — the same string Razor renders
and looks up in 110-05 — NOT the normalized form. `CutLabCardNames.Normalize` (lowercases +
strips punctuation) is used ONLY for the internal Scryfall→pool-card join. A regression test
looks up a punctuated display name ("Atraxa, Praetors' Voice"-style) and confirms the hit.

## D-28 preserved (no POST-size regression)
`CutLabPoolCard` / `CutLabState.cs` gained NO text member — the lookup lives off the
serialized pool-card record, so `CutLabStateJson` does not grow and the Phase 108 POST-size
work stays intact. Grep gate `grep -c 'OracleText\|CardText' CutLabState.cs` returns 0.

## Tests
`CutLabPageServiceTests.cs`: (1) resolved card with a punctuated/mixed-case display name is
retrievable by display name with all five fields populated (HIGH-1 guard); (2) a card with no
resolved data has no entry (fail-open, no exception).

## Verification (claude)
- `dotnet build DeckFlow.Web` — clean, 0 warn / 0 err.
- `dotnet build DeckFlow.Web.Tests` — clean, 0 warn / 0 err.
- EOL: all three files LF, pure insertions (`git diff --stat` == `--ignore-all-space --stat`).
- Grep gates: VM=3, Svc=4, State=0, Tests=5. All pass.
- Code review: join-map keying verified against `ToLastWinsDictionary` (normalizes keys
  internally); final key is display Name OrdinalIgnoreCase — correct.

## Files changed
- DeckFlow.Web/Models/CutLabViewModel.cs
- DeckFlow.Web/Services/CutLab/CutLabPageService.cs
- DeckFlow.Web.Tests/CutLabPageServiceTests.cs
