---
quick_id: 260611-p9x
slug: fix-primer-copy-buttons-port-data-copy-t
date: 2026-06-12
status: complete
---

# Quick Task 260611-p9x: Fix Primer copy buttons — Summary

## Problem

Deck Primer page Copy buttons (`#primer-output`, `#primer-chat-title`) were dead.
`DeckPrimer.cshtml` loads only `primer-selection.js`, which had zero copy logic.
The `[data-copy-target]` click handler lives in `deck-sync.ts` (`attachActionButtons`)
and only runs on pages that load `deck-sync.js`. The Primer page (added Phase 31)
shipped without copy wiring.

## Fix

Ported the copy helpers from `deck-sync.ts:557-613` into `primer-selection.ts`:
- `copyElementValue` — reads target `.value`/`.textContent`, writes to clipboard
- `setTemporaryButtonText` — "Copied"/"Copy failed" flash with `is-copied`/`is-copy-failed`
- `announceToScreenReader` — `[data-copy-announcer]` parity (no-op on Primer page)
- `attachPrimerCopyButtons` — binds click on every `[data-copy-target]`

`attachPrimerCopyButtons()` runs at the top of `initPrimerSelection()`, before the
bracket-form early-return guards, so copy works regardless of form state. Exposed on
`win.DeckFlow.attachPrimerCopyButtons` as a test seam.

Did NOT extract a shared copy module — per-page copy duplication stays as-is;
consolidation belongs in Phase 38 (Controller SRP Split), not this fix.

## Files

- `DeckFlow.Web/wwwroot/ts/primer-selection.ts` (edit, +67)
- `DeckFlow.Web/ts-tests/primer-copy.test.ts` (new — 2 tests)

## Verification

- `npx tsc -p tsconfig.json --noEmit` → exit 0
- `npm test` → 9/9 passed (7 existing + 2 new primer-copy)
- Manual click-test on /deck-primer: **deferred** — user opted to commit without manual test

## Delegation

Codex (gpt-5.4 medium) implemented; Claude planned + reviewed (APPROVE, no changes).
