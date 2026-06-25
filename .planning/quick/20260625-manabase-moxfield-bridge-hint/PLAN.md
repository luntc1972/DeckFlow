---
slug: manabase-moxfield-bridge-hint
created: 2026-06-25
type: quick
---

# Add Moxfield URL section (DeckFlow Bridge hint) to the Manabase page

## Goal
Copy the "Moxfield URL section" from Deck Analysis to the Mana Base page. That
section is the `_DeckFlowBridgeHint` partial — its summary reads "Moxfield URL
support requires the DeckFlow Bridge extension". Deck Analysis renders it under
its public-URL input (`DeckAnalysis.cshtml:162`); the Manabase URL field
(`Manabase.cshtml:44-48`) does not, so users pasting a Moxfield URL there get a
silent failure (the server cannot reach Moxfield without the Bridge extension).

## Scope decision
- Bridge hint only. Do NOT change the Manabase placeholder, label, or add the
  small "Required…" hint. Just render the shared partial under the URL input.

## Changes
1. `DeckFlow.Web/Views/Deck/Manabase.cshtml` — inside the URL field block
   (`data-sync-panel="manabase-deck-url"`, currently lines 44-48), after the
   `<input id="manabase-deck-url" .../>`, add:
   `@await Html.PartialAsync("_DeckFlowBridgeHint")`
   Match surrounding indentation (4-space, Allman/Razor style). Preserve LF.
2. `DeckFlow.Web/e2e/manabase.spec.ts` — add one Playwright test asserting the
   bridge hint renders on `/manabase` (URL mode is the default). Assert the
   `details.deckflow-bridge-hint` element is attached and its `summary` contains
   "DeckFlow Bridge extension". Mirror existing test style in that file.

## Out of scope / verification notes
- No CSS work: `.deckflow-bridge-hint` styling is shared (already used on Deck
  Analysis), so themes + mobile are covered by existing styles. No new tokens.
- No controller/model/service change — the partial is static markup.

## Verify
- `dotnet build` clean (no new warnings).
- Playwright: `npx --no-install playwright test manabase.spec.ts --project=chromium-desktop`
  and `--project=chromium-mobile`.
