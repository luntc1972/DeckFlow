---
status: complete
quick_id: 260624-nsm
slug: add-expandable-disclosure-on-mana-base-p
date: 2026-06-24
---

# Quick Task: Expandable ramp-cards disclosure on the Mana Base page

## What shipped

The "Ramp: N mana rock(s)/dork(s) · M ramp/draw piece(s)…" line on the Mana Base
page is now an expandable `<details class="manabase-ramp">` (mirrors the existing
"approximates or skips" disclosure). Expanding it lists WHICH cards were credited,
in two labeled groups: **Mana rocks/dorks** and **Ramp/draw ≤2 MV**. A card that
qualifies for both (Sol Ring, Arcane Signet, mana dorks) appears under each. Falls
back to the original plain `<p>` when both name lists are empty.

Additive display only — no feature flag. Ramp count math and all castability
numbers are unchanged; the names are projected from the EXACT predicates that
already produce the counts.

## Tasks (3/3, atomic commits — NOT pushed)

- `3f543d89` feat(manabase): project ramp rock/dork and ≤2 MV ramp/draw card names onto the report
- `dd371b3c` feat(manabase): expandable Ramp disclosure listing credited rock/dork and ramp/draw cards
- `d3100b78` test(manabase): cover ramp name projection (de-dup, order, cross-membership)

## Files

- `DeckFlow.Core/Manabase/ManabaseModels.cs` — `RampSourceNames` + `RampAndDrawNames` on `ManabaseReport`; `RampAndDrawNames` on `ManabaseDeck` (all `{ get; init; }`, `Array.Empty` default — get-only carve-out preserved for System.Text.Json).
- `DeckFlow.Core/Manabase/ManabaseClassifier.cs` — `rampNames` accumulator at the ≤2 MV credit site; `Distinct` onto the deck.
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` — projects the RampSourceCount predicate to names; copies `deck.RampAndDrawNames` to the report.
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` — disclosure + empty-list fallback; fixed the stale "mana rocks/dorks aren't listed" help sentence.
- `DeckFlow.Web/wwwroot/css/site-common.css` — grouped `manabase-ramp` with `manabase-unsupported`; added `.manabase-ramp-group-label`.
- `README.md` — manabase bullet updated to describe the card-name disclosure.
- New: `DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerRampNamesTests.cs`, `DeckFlow.Web/e2e/manabase-ramp-disclosure.spec.ts`.

## Verification (orchestrator-confirmed, not just executor-reported)

- `dotnet.exe build DeckFlow.sln` — 0 errors (only pre-existing warnings).
- xUnit `~RampNames` — **5/5 passed** (re-run by orchestrator): both lists, cross-membership (Sol Ring in both), de-dup, first-seen deck order.
- Playwright `manabase-ramp-disclosure` (real PasteText analysis, ramp-heavy deck) — **passed on chromium-desktop AND chromium-mobile** (re-run by orchestrator), details expands, lists a credited rock name, no horizontal overflow.
- Changed-lines format gate clean. LF + .editorconfig carve-outs preserved.

## Not done / follow-up

- NOT pushed — awaiting user review + push (will ride to prod on next main deploy; no flag, so it goes live immediately on deploy).
- XSS surface (card names) mitigated by default `@` Razor encoding (no `Html.Raw`).
