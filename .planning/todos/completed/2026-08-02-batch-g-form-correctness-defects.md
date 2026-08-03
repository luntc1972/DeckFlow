---
title: Batch G — five form-correctness defects (wrong behavior, not polish)
date: 2026-08-02
priority: high
source: 2026-08-02 second front-end audit pass (independent of the batch-A audit)
target_milestone: shipped on feat/ui-audit-batch-g (converged into feat/ui-audit-batch-a 2026-08-02)
status: COMPLETE
---

# Batch G — form-correctness defects

Five defects where the app does the **wrong thing**, not the ugly thing. None of these
overlap Batch A (`260802-m6s`), and none appear in its "Out of scope" list. Two cause
silent data loss; one makes a shipped feature unreachable.

⚠ **Re-verify every `file:line` below against the code before planning tasks.** The
batch-A planner had to amend 2 of its 11 prescribed fixes because the code path differed
from the audit's assumption. Assume the same rate here.

## G1 — Enter key triggers the wrong action on four tools (HIGH)

The sticky **"Download session (.zip)"** submit is the *first* submit button in the form,
making it the form's implicit default button. Pressing Enter in any text input — deck URL,
deck name, JSON paste box — downloads a ZIP instead of advancing the workflow.

- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml:99`
- `DeckFlow.Web/Views/Deck/DeckComparison.cshtml:181`
- `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml:50`

`wwwroot/ts/deck-sync.ts:1683-1686` early-returns for `data-prompt-download-submit`, which
makes the behavior worse rather than guarding it.

Same class, different action, on Mana Base: `Views/Deck/Manabase.cshtml:226-231` — "Load
deck & detect costs" (`formaction=~/manabase/load`) precedes "Analyze Mana Base", so Enter
runs the *load* path instead of the analysis.

**Fix:** move the sticky download bar after the first real submit in DOM order, or give it
`type="button"` plus an explicit JS submit. Whichever is chosen, apply it to all four.

**Test:** headless Playwright — focus the deck-URL input on each of the four pages, press
Enter, assert the expected workflow action fires and no download is triggered.

## G2 — Mobile users silently submit a different request (HIGH)

`DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml:396` — the "Include card versions (set code &
collector number)" checkbox sits inside `class="prompt-question-option desktop-only"`. On a
phone it is `display:none`, so the browser **does not submit it**, and it resets to false on
every mobile post. The user gets different prompt output and is never told why.

**Fix:** never hide a form control that participates in model binding. Hide the explanatory
copy instead, or render a hidden input carrying the persisted value.

**Sweep:** grep for `desktop-only` wrapping any `<input>`, `<select>` or `<textarea>` that
posts. `Views/Deck/CardLookup.cshtml:19,76` is the other known instance (see G5).

**Test:** post the Deck Analysis form at a 390px viewport with the option previously true;
assert the bound value survives.

## G3 — A shipped feature is unreachable with JavaScript enabled (HIGH)

`DeckFlow.Web/Views/Deck/DeckSync.cshtml:187-228` vs `:273-323` — the printing-conflict
**resolution** form (radio-per-card + "Generate Printing Swap Checklist", posting to
`/resolve`) exists **only inside `<noscript>`**. The JS path
(`wwwroot/ts/deck-sync.ts:1073-1096`) renders the conflicts table with three read-only cells
and no choice column, so a normal user can never reach `/resolve`.

**Fix:** render the resolution form in the JS path too. The controller action already exists.

**Test:** e2e with JS enabled — assert the radio column and the checklist submit are present
and that `/resolve` returns the expected checklist.

## G4 — Deck text lost on refresh/back on the two longest-paste tools (MED)

Batch A fixed this for Deck Primer (D8, `data-cache-key="deck-primer"`). Two tools still
have it, including the one with the longest paste in the app:

- `DeckFlow.Web/Views/Deck/Bracket.cshtml:47` — no `data-cache-key`, and its "Start over"
  at `:99` has no `data-clear-cache`. Bracket is the only deck tool entirely outside the
  `deck-input-store.ts` sessionStorage system.
- `DeckFlow.Web/Views/Deck/Manabase.cshtml:30` — no `data-cache-key`, while `:232` still
  renders a `data-clear-cache` "Start over" that therefore clears nothing.

**Fix:** add `data-cache-key="bracket"` / `data-cache-key="manabase"`, binding
`attachGenericPersistedForms` (`deck-sync.ts:965`) — the same mechanism D8 used.

**Sweep — do the whole bug class this time:** enumerate every deck-input form and assert it
carries a `data-cache-key`. Fixing only the tools named in an audit is how this defect
survived Batch A.

**Test:** paste a decklist, navigate away, navigate back, assert the textarea is restored;
assert "Start over" clears it.

## G5 — Card Lookup's 100-card cap is client-side only (MED)

`DeckFlow.Web/wwwroot/js/card-lookup.js:348-354` enforces the advertised cap.
`DeckFlow.Web/Controllers/DeckLookupController.cs:218-250`
(`DownloadCardLookupAsync`) has **no server-side line-count check**, so a direct POST is
unbounded. On a 512MB Render instance with a 200ms-paced Scryfall throttle
(`Services/Scryfall/ScryfallThrottle.cs`), an unbounded list is a self-inflicted stall.

**Fix:** enforce the same cap server-side and return a model error, matching the client copy.

**Related (separate item, not a defect):** the whole Card List mode is `.desktop-only`
(`Views/Deck/CardLookup.cshtml:19,76`), so half the tool does not exist on mobile with no
in-page explanation beyond `:9`. Decide: make it responsive, or show an explicit
"available on desktop" affordance instead of vanishing the tab.

**Test:** unit test on the controller posting 101 lines; assert model error, not a download.

## Out of scope for this batch

Accessibility labelling on Cut Lab's 150-row table, destructive-action confirmations, and
result-panel length — those are Cycle 21 Phase 7 (Cut Lab Workflow UX) and Batch E.
