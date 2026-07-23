# GSD Debug Knowledge Base

Resolved debug sessions. Used by `gsd-debugger` to surface known-pattern hypotheses at the start of new investigations.

---

## cut-lab-pills-not-clickable — Cut Lab card pills were inert and Lock All text lacked contrast
- **Date:** 2026-07-23
- **Error patterns:** Cut Lab pills, not clickable, inert, Lock All, unreadable text, Commander Table, dark color scheme
- **Root cause:** Individual card pills inside role groups were rendered as display-only spans and had no event path to the canonical pool checkbox. Separately, `.manabase-pill` did not set an unselected foreground, so dark OS color schemes supplied white native `ButtonText` over Commander Table's explicitly light panel background.
- **Fix:** Rendered non-commander individual card pills as `aria-pressed` buttons, delegated clicks to toggle the matching pool checkbox and refresh/serialize lock reflections, and set the shared unselected pill foreground to `var(--ink)`. Added unit and desktop/mobile dark-scheme browser regressions.
- **Files changed:** DeckFlow.Web/Views/Deck/CutLab.cshtml, DeckFlow.Web/wwwroot/ts/cut-lab.ts, DeckFlow.Web/wwwroot/css/site-common.css, DeckFlow.Web/ts-tests/cut-lab-lock-interactions.test.ts, DeckFlow.Web/e2e/cut-lab-pill-interactions.spec.ts
---
