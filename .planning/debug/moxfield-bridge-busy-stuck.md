---
status: root-cause-found
trigger: "on the deck analysis page if the alert error pops up about not being able to access moxfield when closed the loading does not go away, check other pages that use the moxfield chrome extension and if they have this bug as well"
created: 2026-06-26
updated: 2026-06-26
---

# Moxfield bridge: busy overlay stuck after access-error alert

## Symptoms
- Expected: after dismissing the Moxfield-access alert, the loading overlay clears and the page is usable.
- Actual: the `#busy-indicator` loading overlay stays visible forever after the alert is closed.
- Trigger: submit a Moxfield URL on a bridge-enabled page when the DeckFlow Bridge cannot access Moxfield (not installed / not allowed / mobile / import error).
- Scope question: does this affect pages other than Deck Analysis? YES — see below.

## Root cause
`DeckFlow.Web/wwwroot/ts/deck-sync.ts`, `attachMoxfieldExtensionImport()` (`:344-420`).

Two submit listeners run per form submit:
1. `attachMoxfieldExtensionImport` — **capture** phase (`:345` + `true` at `:419`). Runs first. Calls `event.preventDefault()` (`:361`), then on a bridge failure shows `window.alert(...)` and `return`s.
2. `registerBusyIndicator` — **bubble** phase (`:742`). Runs second. Calls `showBusyIndicator()` **unconditionally** (`:772`), even when `event.defaultPrevented` (only the `data-busy-min-ms` hold is skipped, `:786`).

Per submit: capture (bridge) → preventDefault → alert/return; THEN bubble → overlay shown. On the SUCCESS path the bridge re-fires the submit via `resubmitFormBypassingExtension()` (`:418`) and the navigation/`pageshow` (`:805-807`) clears the overlay. On the FOUR error/return paths nothing clears it:
- `:364-370` mobile browser (no desktop bridge) — alert, return
- `:375-383` extension not installed — alert + install popup, return
- `:385-394` extension installed but origin not allowed — alert + options popup, return
- `:396-416` import error catch — alert (or `promptToConfigureMoxfieldExtensionOrigin`), return

`hideBusyIndicator()` (`:656-674`) is never called on these paths, so `.hidden` is never re-added to `#busy-indicator`.

Timing subtlety: the mobile branch's `window.alert` is synchronous and runs BEFORE the bubble listener shows the overlay; the other three run after an `await`, by which time the overlay is already shown. A naive synchronous `hideBusyIndicator()` in the mobile branch would hide-then-get-reshown by the bubble listener. The fix must DEFER the hide to a macrotask (`setTimeout(..., 0)`) so it runs after the bubble-phase show on all paths. `queueMicrotask` is WRONG — DOM runs a microtask checkpoint after each listener, so it would fire before the bubble listener.

## Other pages affected
`deck-sync.js` is loaded on 10 tool pages, all sharing the one `attachMoxfieldExtensionImport` handler:
/sync, /card-lookup, /mechanic-lookup, /convert, /suggest-categories, /judge-questions, /deck-analysis, /deck-comparison, /cedh-meta-gap, /manabase, /deck-primer.
(Confirmed via `scripts.spec.ts` route→script map and `data-busy-title` forms.) Any of these that accepts a Moxfield URL hits the identical stuck-overlay on the alert-dismiss path. Not Deck-Analysis-only.

## Fix (for Codex — TDD)
1. Failing e2e test first (Playwright). On a bridge page (e.g. `/sync`) with no extension installed (CI default), submit a Moxfield URL, auto-dismiss the alert + popup via `page.on('dialog')` / popup handling, assert `#busy-indicator` gains `.hidden` within a short poll. Confirm it FAILS on current code (overlay stays visible).
2. Add a deferred-hide helper, e.g. `const abortBridgeBusy = (): void => { window.setTimeout(hideBusyIndicator, 0); };` and call it before each of the four `return`s (`:370`, `:382`, `:393`, `:415`). Single call before `:415` covers both catch sub-branches.
3. Confirm the test passes; success/resubmit path unchanged.

## Files
- DeckFlow.Web/wwwroot/ts/deck-sync.ts (fix)
- DeckFlow.Web/e2e/*.spec.ts (new regression test)
