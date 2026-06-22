---
status: resolved
trigger: "in mobile the 3 steps on the deck primer page, they may not work in desktop either"
created: 2026-06-22
updated: 2026-06-22
resolution: "Option B (scroll-to-section). Added id=primer-step-panel-{1,2,3} anchors to the three
  step sections; added attachPrimerStepNav() in primer-selection.ts that scroll-navigates to a tab's
  aria-controls target on click + roving tabindex/arrow keys. Live-verified: click Step 2 → scrollY
  0→1006, panel2 to top, aria-selected flips, no console errors. Playwright test (desktop+mobile) added."
---

# Debug: Deck Primer 3-step tabs do nothing (mobile AND desktop)

## Symptoms
- The 3 step tabs (Step 1 Deck / Step 2 Build Primer / Step 3 Results) on the deck-primer
  page do not switch content when clicked. Noticed on mobile; suspected broken on desktop too.

## Root Cause (FOUND)
The deck-primer page is the odd one out among the four deck-tool workflow pages. It renders the
shared `_WorkflowStepTabs` step strip but **nothing wires those tabs up**:

1. **No JS handler for `primer-show-step`.** The step-tab click/panel-switch logic lives entirely
   in `DeckFlow.Web/wwwroot/ts/deck-sync.ts`, keyed on `data-chatgpt-show-step` (+ `comparison`/`cedh`
   variants, deck-sync.ts:1673, 2200, 2349). There is no `primer-show-step` branch anywhere.
2. **The primer page loads `primer-selection.js`, not `deck-sync.js`** (DeckPrimer.cshtml:298).
   `primer-selection.ts` has zero step/tab/panel handling (grep: 0 hits). So even the generic
   handler never loads on this page.
3. **No step panels exist to switch.** The other pages wrap each step in a `data-chatgpt-step="N"`
   panel that the handler shows/hides. The primer view has **0** such panels — its three step
   sections render stacked on one scrolling page (DeckPrimer.cshtml has no `data-*-step` panel attrs).
4. No global/delegated show-step listener exists (site.ts etc. — grep clean).

Net: the 3 tab buttons render (from `_WorkflowStepTabs`, emitting `data-primer-show-step="N"`) and
*look* interactive (same `chatgpt-step-tab` styling as the working pages), but have no listener and
no target panels → clicking is a no-op on **both** desktop and mobile. The report's "may not work in
desktop either" is correct: it never worked anywhere.

### Comparison table (evidence)
| View | DataShowStepAttribute | Script loaded | Handler? | Panels? |
|------|----------------------|---------------|----------|---------|
| DeckAnalysis | chatgpt-show-step | deck-sync.js | ✅ | ✅ |
| DeckComparison | chatgpt-comparison-show-step | deck-sync.js | ✅ | ✅ |
| CedhMetaGap | chatgpt-cedh-show-step | deck-sync.js | ✅ | ✅ |
| **DeckPrimer** | **primer-show-step** | **primer-selection.js** | ❌ | ❌ |

## Key files
- `DeckFlow.Web/Views/Deck/DeckPrimer.cshtml` — packetTabs uses `primer-show-step`; loads only
  primer-selection.js (line 298); no step panels.
- `DeckFlow.Web/wwwroot/ts/deck-sync.ts:1640-1700, 2110-2300, 2330-2570` — the show-step/next-step
  panel-switch handlers, per-tool but none for primer.
- `DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml` — shared tab-strip partial (emits the buttons).
- `DeckFlow.Web/wwwroot/ts/primer-selection.ts` — primer's only script; no tab logic.

## Fix options (design decision needed)
- **A. Make the tabs real (wire panels + handler).** Wrap the three primer sections in
  `data-primer-step="N"` panels, add a `primer-show-step` handler (generalize the deck-sync.ts
  pattern, or a small block in primer-selection.ts), show one panel at a time like the other tools.
  Most consistent with sibling pages; most work.
- **B. Make the tabs scroll-anchors.** On click, smooth-scroll to the matching section heading
  (sections already render stacked). Small change; keeps single-page flow; tabs become a jump-nav.
- **C. Remove the interactive affordance.** Render the strip as a non-clickable progress indicator
  so it doesn't look like a broken tab. Smallest change; loses navigation.

## Recommendation
Option B (scroll-to-section) is the lowest-risk match for how the primer page is actually built
(single stacked form), and immediately fixes the "dead click" perception on mobile + desktop.
Option A if parity with the other three tools (one-panel-at-a-time) is desired.
