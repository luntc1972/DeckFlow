# Phase 66-06 Summary

Implemented `DeckFlow.Web/e2e/tool-toggles.spec.ts` for the Phase 66 cross-surface Playwright coverage.

Covered behaviors:
- `/Admin/Tools` admin smoke with section headings, Card Lookup toggle presence, and no horizontal overflow.
- Hide/show cascade for Card Lookup across home tile, nav link, help index, landing route, and help topic route.
- Sibling negative coverage:
  - `GET /suggest-categories/card-search?query=sol` returns `404` when Category Suggestions is OFF.
  - Token-valid `POST /deck-analysis/download` returns `404` when Deck Analysis is OFF.
- Categories section collapse when both Categories tools are OFF, with restoration when toggled back ON.
- Core Analyze warning banner when Deck Analysis is disabled.
- Desktop + mobile execution, with representative theme-cookie coverage and per-test state restoration.

Verification run:
- `dotnet.exe build DeckFlow.Web`
- `npx --no-install playwright test e2e/tool-toggles.spec.ts --project=chromium-desktop --project=chromium-mobile`

Operator checkpoint still required:
1. Launch the app headless for manual review.
2. Verify Card Lookup hide/show manually across home, nav, help, and route.
3. Verify full Categories section collapse/restore manually.
4. Verify the inline Deck Analysis core warning manually.
5. Spot-check desktop + mobile widths across at least two themes for any overflow on `/Admin/Tools` and `/`.
