---
slug: manabase-moxfield-bridge-hint
status: complete
completed: 2026-06-25
---

# Summary — Manabase Moxfield Bridge hint

Added the shared `_DeckFlowBridgeHint` partial under the Mana Base public-URL
input (matching Deck Analysis) so pasting a Moxfield URL surfaces the Bridge
install hint instead of failing silently.

Also closed a test-infra gap: `playwright.config.ts` `webServer.env` now sets
`DECKFLOW_DISABLE_AUTO_BROWSER=true`, so a Playwright-spawned server never opens
a Windows browser window.

## Commits (main)
- `f26c78c3` feat(manabase): show DeckFlow Bridge hint under the URL field
- `bbe00820` test(e2e): suppress Windows auto-browser in Playwright webServer

## Verification
- `dotnet build` clean (0 warnings, 0 errors).
- Playwright `manabase.spec.ts`: 20/20 pass on chromium-desktop + chromium-mobile,
  including the new bridge-hint render test.

## Notes
- README unchanged — line 305 already states the Moxfield URL fields carry the hint.
- Implementation by Codex (gpt-5.4 medium); review + e2e by Claude.
