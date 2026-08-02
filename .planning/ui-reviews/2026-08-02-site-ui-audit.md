# DeckFlow UI/UX Audit — all user-facing pages except Cut Lab

Date: 2026-08-02 · Scope: 22 Razor views, 30 stylesheets, 27 TS modules
Method: 5 parallel page sweeps + a cross-cutting stylesheet/TS scan. Every HIGH below was
independently re-verified against the source before being listed here.

Excluded by request: `Views/Deck/CutLab.cshtml`. Admin views also excluded (not user-facing).

---

## Tier 0 — Verified defects (something is actually broken)

These are not preferences. Each was confirmed by reading the code path.

### 0.1 Enter key downloads a zip instead of advancing the workflow
`DeckAnalysis.cshtml:99`, `DeckPrimer.cshtml:84`

The sticky "Download session (.zip)" button is the **first `type="submit"` in tree order** inside
the form opened at `DeckAnalysis.cshtml:96`. HTML implicit submission selects the first submit in
DOM order, not visual order. Pressing Enter in the deck URL, deck name, budget, or any card-name
input POSTs to the download endpoint — and the button carries `formnovalidate`, so validation is
skipped too.

**Fix:** add a hidden sentinel `<button type="submit" hidden data-prompt-submit-step="2">` as the
first submit in the form, or move the sticky bar out of the form and submit it via `form=` +
`formaction`.

### 0.2 Primer "Start Over" silently destroys the carried deck and does nothing visible
`DeckPrimer.cshtml:76`, `:282` · `deck-sync.ts:970`, `:984` · `deck-input-store.ts:208-218`

`DeckPrimer.cshtml:76` opens `<form method="post"` with **no `data-cache-key`**.
`attachGenericPersistedForms` (`deck-sync.ts:970`) only queries `form[data-cache-key]`, so the
clear-button handler at `:984` — the one that resets the form and navigates to `data-clear-href` —
never binds.

But `deck-input-store.ts:208-218` registers a **document-level** delegated listener for any
`[data-clear-cache]` click and calls `clearLastDeck()` + `removeRestoredNotice()` unconditionally.

Net: clicking Start Over wipes the carried deck from sessionStorage, does **not** navigate, and does
**not** reset the form. The page looks unchanged. The user has no idea their deck was discarded.

Same root cause also means the primer has **no form-state persistence at all** (bracket, style,
notes are lost on navigate-away) while DeckAnalysis has it via `data-cache-key="prompt-packets"`.

**Fix:** add `data-cache-key="deck-primer"` to `DeckPrimer.cshtml:76`. One attribute repairs both.

### 0.3 Deck Sync's printing-swap-checklist is unreachable with JS enabled
`DeckSync.cshtml:187-227` vs `:242-345` · `deck-sync.ts:1217-1221`

`#deck-sync-conflicts-js` (the panel real users see) is a **read-only table**. The per-card radios,
"Select All", and the "Generate Printing Swap Checklist" submit (`:294`) exist only inside
`<noscript>`. `deck-sync.ts:1217-1221` preventDefaults the form submit, so the JS path is the only
path anyone reaches. An advertised feature is dead code.

**Fix:** port the resolve form into `#deck-sync-conflicts-js` and POST `~/resolve` via fetch.

### 0.4 Three components render unstyled on 11 of 24 themes
`site.css` vs the 11 standalone theme sheets

11 theme files contain **zero `@import`** — abzan, bant, esper, grixis, jeskai, jund, mardu, naya,
nyx, planeswalker-dark, sultai. They are full forks that inlined `site.css` at fork time; anything
added to `site.css` afterward was never backported.

A scan of all 617 class tokens used in `Views/` found **17 classes that resolve only in `site.css`**:

| Component | Classes | Pages affected |
|---|---|---|
| Judge page chrome | `judge-divider` `judge-howto` `judge-howto__fallback` `judge-howto__steps` `judge-primary` `judge-secondary` `judge-suggested` `judge-tips` | JudgeQuestions |
| DeckFlow Bridge hint | `deckflow-bridge-hint` + 5 BEM children | **10 pages**: Bracket, CedhMetaGap, DeckComparison, DeckConvert, DeckHistory, DeckPrimer, DeckAnalysis, CutLab, Manabase, DeckSync |
| Moxfield bulk-edit hint | `moxfield-bulkedit-hint` + 2 children | same 10 |

`site-common.css:1444-1448` already carries a comment warning about exactly this failure mode. The
CLAUDE.md constraint "layout CSS goes in `site-common.css`" has no enforcement, which is how 17
classes drifted.

**Fix:** move the 17 classes to `site-common.css`, then add a CI guard. The detector is ~8 lines of
shell: extract every `class="…"` token from `Views/`, flag any that matches in `site.css` but in
none of `site-common.css` / `site-mobile.css` / a representative standalone theme.

### 0.5 `.table-wrapper` has no CSS rule anywhere
`CedhMetaGap.cshtml:255`

`grep -rn "table-wrapper" wwwroot/css/` returns nothing. The 9-column EDH Top 16 reference table has
no scroll container. Mobile card layout only engages at ≤600px, so between 601px and the desktop
breakpoint the table overflows its panel with no way to reach the right-hand columns.

**Fix:** `.table-wrapper { overflow-x: auto; }` plus `tabindex="0" role="region"` for keyboard scroll.

### 0.6 Comparison "Back" buttons are removed from the keyboard tab order on every render
`DeckComparison.cshtml:573`, `:769` · `deck-sync.ts:1739-1744`

Both footer Back buttons carry `data-prompt-comparison-show-step` — the same attribute the tablist
sync loop queries. On every render that loop sets `tabindex="-1"` and `aria-selected="false"` on any
element whose step ≠ current step. Both Back buttons always fail that test, so they are permanently
untabbable and announced as unselected tabs.

**Fix:** give the footer buttons a distinct attribute, and scope the loop at `:1739` to `[role="tab"]`.

### 0.7 Always-true guard prints "0 reference deck(s)"
`CedhMetaGap.cshtml:601`

`@if (item.RefCount >= 0)` is always true for a non-negative int. Its sibling at `:574` correctly
uses `> 0`. Cuts with zero references render "found in 0 reference deck(s)".

**Fix:** `> 0`.

### 0.8 Interactive content nested inside `<label>`
`DeckConvert.cshtml:54-61`, `:62-69` · `DeckSync.cshtml:99-106`, `:107-115`, `:134-141`, `:142-150`

`_DeckFlowBridgeHint` (a `<details>` containing a download anchor styled as a button) and
`_MoxfieldBulkEditHint` are rendered **inside** `<label class="field">` elements. Invalid HTML;
clicking the hint's summary or link retargets activation to the labeled control.

**Fix:** explicit `<label for>` + sibling `<div class="field">`, hints outside the label.

### 0.9 Dead partial
`Views/Shared/_BracketCallout.cshtml` — zero call sites across all views. Its own comment says the
real markup lives inline in `DeckAnalysis.cshtml`.

### 0.10 Dead attributes
`data-enable-on-ready` and `data-validate-lookup` have **no implementation** anywhere in `wwwroot/ts/`.
Present on `DeckSync.cshtml:74`, `SuggestCategories.cshtml:34,50`, `CommanderCategories.cshtml:40`.
They imply a "don't submit before hydration" guard that never landed.

---

## Tier 1 — Site-wide structural gaps

### 1.1 The copy button is the smallest tap target on mobile — deliberately
`site-mobile.css:122-128` · `site-common.css:974-977`

The 44px rule covers `.run-button`, `.clear-cache-button`, `select`, `.df-select__trigger`.
`.copy-button` is **not in the list**. Worse, `site-common.css:974-977` — inside
`@media (max-width: 600px)` — *shrinks* `.copy-button.copy-button--icon` to **2rem (32×32px)**.

DeckFlow's stated core value is output the user can paste into ChatGPT in one round-trip. On a
phone, the control that performs that paste is 27% under the touch minimum while every button
around it is 44px.

**Fix:** add `.copy-button` to the 44px selector list; delete or invert the `--icon` shrink.

### 1.2 No deep-linking, anywhere except Cut Lab
`pushState` / `replaceState` / `URLSearchParams` appear **once** in all 27 TS modules: `cut-lab.ts:491`.

Consequences across every tool: results aren't shareable or bookmarkable; refresh triggers form
resubmission; browser Back is unsafe; and `_ShareBar` — which shares the current URL — shares an
**empty tool** when rendered on a results page.

**Fix:** POST-redirect-GET with state in the query string, or `history.replaceState` on AJAX success.

### 1.3 Dark themes never declare `color-scheme`
`site-common.css:5` sets `color-scheme: light dark` at `:root`. **No guild theme file declares it.**

So native scrollbars, `<select>` dropdown popups, date pickers, and autofill chrome follow the
**OS**, not the picked theme — Dimir on a light-OS machine gets light scrollbars.
`site-theme-overrides.css:48-72` rebuilds checkbox and radio from scratch with `appearance:none`
specifically to dodge this; that workaround exists because the root cause was never fixed.

**Fix:** `color-scheme: dark` in `:root` of each dark theme, `light` in each light one.

### 1.4 Async result regions are silent to screen readers
`_Layout.cshtml:157` defines a `[data-copy-announcer]` live region. **Only `deck-sync.ts:308` reads it.**

Result panels revealed by JS with no `aria-live` and no focus move: `SuggestCategories.cshtml:124-140`,
`CardLookup.cshtml:43,64`, `JudgeQuestions.cshtml:106`, `DeckSync.cshtml:158`,
`DeckComparison.cshtml:598`, `DeckHistory.cshtml:114`, `Bracket.cshtml:105`.

Additionally `card-lookup.ts:134-168` is a **fork** of `deck-sync.ts:315-329` that omits
`announceToScreenReader` and sets `button.textContent = 'Copied'`, destroying the `aria-hidden`
glyph wrapper.

**Fix:** one `df-copy.ts` module; `aria-live="polite"` on every result container.

### 1.5 Generic accessible names
Twelve buttons named "Copy" on `DeckComparison.cshtml` alone (`:387,398,409,420,450,464,483,497,514,531,549,564`);
six on `DeckAnalysis.cshtml`; three on `DeckPrimer.cshtml`; two on `DeckSync.cshtml`. A screen-reader
button list is unusable.

### 1.6 No `env(safe-area-inset-*)`
Zero hits across all 30 stylesheets. Fixed and sticky elements (`site.css:312` back-to-top, 4 sticky
bars in `site-common.css`) sit under the iPhone home indicator.

### 1.7 Back-to-top is hidden exactly where it's needed
`site-mobile.css:28-30` — `display: none` below 600px, on pages measuring 6,000–10,000px. Also
`site.css:317-318` is 37.6px and `site-mobile.css:6-7` shrinks it to 32px before hiding it.

### 1.8 `tabular-nums` used once
One hit in 5,743 lines of `site-common.css`. Manabase's castability table (MV, Cast %, Avg delay,
Sources, Needed) and CedhMetaGap's Finish column render proportional. CedhMetaGap gets it right in
places (`:265,299,302`), Manabase nowhere.

### 1.9 `window.alert()` for failures
`deck-sync.ts:454`, `:493` — "Download failed. Please try again." No cause, no recovery, blocks the
page. Used by Manabase and Deck History download paths.

### 1.10 Triple intro copy
`page-lede` + `lede` + `info-banner` all restating the same thing before any control:
`CedhMetaGap.cshtml:29,30,41-43`, `DeckComparison.cshtml:152,153,164-166`,
`DeckPrimer.cshtml:48,59-61,188`.

---

## Tier 2 — Per-page, ranked by user impact

### Manabase (1,197 lines)
- **[HIGH]** `:401-1071` — one continuous `result-panel`: verdict + 3 lenses + tap + mulligan + ramp + castability (~90 rows) + 2 methodology blocks. 600+ lines of markup, no segmentation.
- **[HIGH]** `:1078-1093` — "Download analysis (.txt)" re-POSTs and **re-runs the entire Monte-Carlo** server-side, but carries `data-no-busy`. Only feedback is `button.disabled`.
- **[HIGH]** `:295-353` — castability table is the longest data surface and is neither sortable nor filterable.
- **[MED]** `:282-293` vs `:521-633` — anchor nav order ≠ DOM order; the 4th nav item scrolls **up**. Nav also omits `#manabase-untapped-sources` (`:641`) and `#manabase-opening-hand` (`:746`).
- **[MED]** `:292,1112` — "Details" anchor targets a `<details>` closed by default; scrolls to a collapsed summary.
- **[MED]** `:1044-1051` — "Show all N rows" renders a **second complete table** with its own `<thead>` and independent column widths.
- **[MED]** `:1063-1070` — `ImportWarning` renders as a muted footnote *below* the tables it invalidates.
- **[MED]** `:134-138` — a 55-word instructional paragraph is the `<label>`, so SRs read all of it as the field's accessible name on every focus.
- **[MED]** `site-common.css:2434-2447` — `.manabase-pill` has no `min-height`; ~30px at ≤480px, and there are up to 12.
- **[MED]** `site-common.css:2305-2311` — every cell `white-space: nowrap` in an `overflow-x` wrapper with no sticky first column; the card name scrolls out of view.

### DeckAnalysis (1,166 lines)
- **[HIGH]** `:255-285,916-923` — "Advanced" layout mode hides `details.result-panel.nested-panel` wholesale via `site-common.css:5311-5320`, which removes **functional inputs** (Format, Deck name, Strategy notes, set-packet override), not just guidance.
- **[HIGH]** `:862-866` — generated decklists render in a bare `<pre>` with **no Copy button and no height clamp**. The most paste-worthy artifact on the page can only be hand-selected.
- **[HIGH]** `:901-905` — the set `<select>` is server-rendered empty and filled by fetch: no placeholder, no `aria-busy`, no loading text, and on failure an un-hidden `<small>` with no live region and no retry.
- **[HIGH]** `:65-73` — server error banners render above the tabs; after a failed Step-4 POST they sit thousands of pixels above the viewport with no scroll-to.
- **[HIGH]** `:200-513` / `:554-873` — Step 2 and Step 3 are each one unbroken multi-thousand-pixel scroll.
- **[HIGH]** `:343,365` — card-name inputs have no label; the `<span class="field-label">` at `:337` is referenced by nothing. JS-cloned rows inherit the gap.
- **[HIGH]** `:194` — "Start Over" destroys all state with no confirmation, full-width and stacked directly above "Next: Analysis" on mobile.
- **[MED]** `:183-191` — Companion `<details>` is closed even when auto-detected from Moxfield, so the detected value is invisible.
- **[MED]** `site-common.css:3598-3603` — `.prompt-score-band--0…5` use hard-coded hex; the only element that doesn't re-theme across 24 themes.

### DeckComparison (778 lines)
- **[HIGH]** `:8` vs `:600-766` — the page promises "exact card diffs"; the result is AI prose only. No card-level diff is rendered, despite `DeckAListText`/`DeckBListText` being available at `:452,466`.
- **[HIGH]** `:254-348` — no swap-A/B control; an A/B mix-up means re-entering both decks.
- **[HIGH]** `:288-294,335-341` — `type="url"` inputs inside `display:none` panels. A malformed URL typed then switched away from makes the browser block submit with a console-only "not focusable" error: the user clicks Generate and nothing happens.
- **[MED]** `deck-sync.ts:19` — `resolveSplitDeckValue` silently falls back to the *other* field, so "Paste text" with an empty textarea submits a stale URL with no indication.
- **[MED]** `:684-696` — the verdict, the single answer the user came for, is an unstyled `<p>` between two equal-weight columns.
- **[MED]** `:774-778` — omits `deck-input-store.js`, so the one tool needing **two** decks is the only one with no last-deck recall.
- **[MED]** `:447-560` — panel headings are raw filenames (`30-comparison-prompt.txt`).

### Deck History (264 lines)
- **[HIGH]** `:60` — refreshed page looks fully populated while `HistoryJson` is gone (correctly excluded from persistence per `deck-sync.ts:520-527`), and "Update history" then starts a fresh V1 with no warning.
- **[HIGH]** `:121,186` — the entire dataset lives in a hidden field with no `beforeunload` guard; a stray navigation destroys unsaved versions.
- **[HIGH]** `:194-209` — "Compare" is a full-page POST that rebuilds everything to swap two dropdowns.
- **[HIGH]** `:201-203` — the only older→newer cue is a `→` that is both `aria-hidden` and `display:none` below 640px; on mobile the two dropdowns are indistinguishable.
- **[MED]** `:148` — `Δ` as a bare column header. `:169-170` — adds/cuts distinguished by color alone.
- **[MED]** `:64` — bare file input: no dropzone, no filename echo, no version-count feedback until submit.

### CedhMetaGap (682 lines)
- **[HIGH]** `:225-332` — no empty state. Zero EDH Top 16 results makes the whole block vanish with no "0 decks found" and no hint that the filters were too tight.
- **[HIGH]** `:292-297` — once 3 references are checked, every other checkbox is silently `disabled` with no message and no counter. Rows go dead with no explanation.
- **[HIGH]** `:106-206` — four filters are POST-only; nothing is in the URL.
- **[HIGH]** `:479-537` — the analytical core (interaction / speed / mana efficiency vs meta) renders as ~14 separate `<p><strong>label:</strong> value</p>` lines. No alignment, no delta, no direction.
- **[MED]** `:231` — "The client enforces the limit before submit." leaks implementation detail to end users.
- **[MED]** `:409-668` — Download and Print, but **no Copy** for the Top 10 Adds/Cuts.

### Bracket (343 lines)
- **[HIGH]** `:80-93` — target bracket is described as optional, but once a radio is chosen there's no way to unset it short of "Start over".
- **[HIGH]** `:103-118` — the results section has **no heading**; h1 jumps straight to `<details>` summaries, and the verdict lives in a `<div>` with an `aria-label` and no role.
- **[HIGH]** `:164` vs `:258-264` — Game Changer *count* is shown, but the detected list only renders when over target. A user with no target sees "7 Game Changers" and cannot find out which.
- **[MED]** `:184` — hardcoded "· 0 mass extra-card draw" always renders zero, reading as a broken detector.
- **[MED]** `:303-306` — `ImportWarning` uses the least prominent styling on the page for the message saying the result may be wrong.
- **[MED]** `:309-317` — the AI balancer prompt, the tool's only next step, is inside a collapsed `<details>` whose summary is a question.

### Deck Sync (345 lines)
- **[HIGH]** `DeckSyncApiController.cs:143` / `DeckSyncController.cs:195` — the Moxfield 403 message ships developer language to end users ("run the compare from the CLI/WSL environment") and never mentions the DeckFlow Bridge extension the same page promotes.
- **[MED]** `:158-234` vs `:242-345` — duplicate IDs `results-anchor`, `delta-output`, `full-import-output`. Only bites JS-disabled users (browsers don't parse `<noscript>` content as DOM when scripting is on), so lower severity than it first appears — but the no-JS path is genuinely broken.
- **[MED]** `:74` — Run is never disabled during the fetch; repeat clicks fire concurrent API calls.
- **[MED]** `:257,268,333` — copy buttons inside `<noscript>` are inert; their handler is in `deck-sync.ts`.
- **[MED]** `:160-163` — the Reconciliation Report is the only result panel with no Copy affordance.
- **[LOW]** `:101,136` vs `deck-sync.ts:243-251` — Razor placeholders end in `…`, the JS overwrite uses `...`.

### Lookup + utility tools (CardLookup, MechanicLookup, SuggestCategories, DeckConvert, JudgeQuestions)
- **[HIGH]** `JudgeQuestions.cshtml:100` — the page's primary action uses `.clear-cache-button`; there is no visual primary.
- **[HIGH]** `DeckConvert.cshtml:73-80` — commander field uses a native `<datalist>` while every other card field uses `df-typeahead.ts`. No `role=combobox`, no `aria-activedescendant`, no error state.
- **[HIGH]** `MechanicLookup.cshtml:35` — the only tool input with **no typeahead**, against a closed vocabulary (the CR keyword list).
- **[HIGH]** `card-lookup.ts:407` — `activate('single')` runs unconditionally, so a Card-List-mode error lands the user in Single mode with the banner pointing at an invisible form.
- **[HIGH]** `JudgeQuestions.cshtml:48` — the "Suggested opening message" renders **only** when arriving from Card Lookup, though the how-to tells every user they need one.
- **[HIGH]** `judge-questions.ts:43,102-106` — a failed card fetch aborts prompt generation entirely; the user's typed question produces nothing.
- **[HIGH]** `SuggestCategories.cshtml:120` — "Check Again" re-submits the identical query against the identical cache and deterministically returns the same empty result.
- **[HIGH]** `DeckConvert.cshtml:22-25` — the MissingCommander warning says "Enter a commander name below", but that field is `hidden` unless the source is Moxfield.
- **[HIGH]** `JudgeQuestions.cshtml:13` vs `:121` — loads `busy-indicator.js` but never renders the partial; Generate does a network fetch with no feedback.
- **[MED]** Five tools, five button conventions: "Look Up" / "Lookup Rules" / "Suggest" / "Convert" / "Generate Prompt". Four placeholder conventions.
- **[MED]** `judge-questions.ts:67` — `minChars` is 4 here vs 2 in every other typeahead.
- **[MED]** `site-mobile.css:117-119` — `textarea[readonly] { min-height: 50vh }` is a **deliberate fix** (commit `98f525d23`, measured on `/mechanic-lookup`: an 88px box onto 2,903px of rules text). But it applies unconditionally, so `#merged-categories-output` — typically 3-8 short lines — renders as a half-viewport of whitespace. Needs a `--longform` opt-in, not a revert.

---

## Recommended sequencing

**Batch A — verified defects, small diffs, no design decisions.** 0.2 (one attribute), 0.5 (one CSS
rule), 0.7 (one character), 0.9, 0.10, 1.1 (one selector list), 1.3 (one line per theme file).
High value per line changed.

**Batch B — the enforcement gap.** Fix 0.4 by relocating the 17 classes, then land the CI detector so
it cannot recur. The CLAUDE.md rule already exists; it just has nothing enforcing it.

**Batch C — correctness.** 0.1, 0.3, 0.6, 0.8. Each is a real broken interaction.

**Batch D — the structural bet.** 1.2 (deep-linkable results via POST-redirect-GET) unlocks
shareable results, safe refresh, working Back, and makes `_ShareBar` meaningful. It also removes the
"download re-runs the whole analysis" problem in Manabase. Largest payoff, largest scope.

**Batch E — page-level restructuring.** Manabase and DeckAnalysis result-panel segmentation; the
shared `_DeckSource` / `_PromptArtifact` / `_NextSteps` partials that several sweeps converged on
independently.

---

## Recurring themes worth naming

1. **Copy-paste divergence.** `card-lookup.ts:134-168` forks `deck-sync.ts:315-329` and drops the SR
   announcement. `DeckAnalysis` and `DeckPrimer` duplicate the same import block and the same
   artifact card four times. The same deck-source cluster is reimplemented in five views. Nearly
   every consistency finding traces to a component that was copied rather than shared.

2. **Attribute-as-contract, unenforced.** `data-cache-key`, `data-enable-on-ready`,
   `data-validate-lookup`, `data-prompt-comparison-show-step`. When two subsystems bind the same
   attribute with different scoping (0.2), a missing attribute becomes a *partial* action rather
   than no action — the worst failure mode.

3. **Warnings styled below the thing they invalidate.** `Manabase.cshtml:1063-1070`,
   `Bracket.cshtml:303-306`, `CedhMetaGap` import warnings. The message saying "this result may be
   wrong" consistently gets the least prominent treatment on the page.

4. **The paste step is the least-supported step.** Copy buttons: smallest tap target on mobile,
   generic accessible names, silent to screen readers, absent entirely from the generated decklists
   in DeckAnalysis and the Reconciliation Report in DeckSync. For a product whose stated core value
   is one-round-trip paste into ChatGPT, this is the highest-leverage cluster in the report.

---
---

# Part 2 — Entry pages, info pages, and shared partials

Covers Home, About, Help, Feedback, Error, ContentKb, CommanderCategories, and the shared
tab/selector/banner partials. Same verification standard: every item below marked **verified** was
re-checked against source.

## Tier 0 additions — verified defects

### 0.11 Three landing-page tool tiles render a "?" placeholder icon — **verified**
`_ToolTileIcon.cshtml:3-53` · `ToolRegistry.cs:17,25,26,61`

`ToolRegistry.cs:61` sets `IconKey = key`, and `Create(key, …)` takes the key as its first
positional argument; `helpSlug` is the 11th. Two icon cases were written against the **helpSlug**
instead of the key, and one tool has no case at all:

| Tool | Registry `Key` | Case present in the switch | Result |
|---|---|---|---|
| Ask a Judge | `judge-questions` | `"ask-a-judge"` (helpSlug) | `default:` → ? |
| Category Suggestions | `suggest-categories` | `"category-suggestions"` (helpSlug) | `default:` → ? |
| Deck History | `deck-history` | none | `default:` → ? |

Nothing catches it because `default:` (`:52`) emits a valid question-mark SVG. For 12 of 15 tools
key and helpSlug are identical strings, so the two that differ are exactly where it broke.

**Fix:** add the three cases. Then make it non-recurring: assert in a unit test that the switch is
total over `ToolRegistry.All.Select(t => t.IconKey)`.

### 0.12 The landing page has no `<h1>` — **verified**
`Home.cshtml` — first heading in the document is the section `<h2>` at `:37`.

### 0.13 404s bypass the branded error page entirely — **verified**
`Program.cs:215` wires only `UseExceptionHandler("/Deck/Error")`. There is no
`UseStatusCodePagesWithReExecute`, so a mistyped URL, a dead link, or any 403 renders the bare
framework/browser page. The most common error a visitor hits is the one that isn't handled.

**Fix:** `app.UseStatusCodePagesWithReExecute("/Deck/Error", "?code={0}")` and branch the copy on 404.

### 0.14 Feedback form validation is entirely inert — **verified**
`Feedback/Index.cshtml:20` sets `novalidate`; `:50` loads only `feedback.js` — no validation scripts
partial. So `required`, `minlength="10"`, and `type="email"` never fire. Every mistake costs a full
server round trip, and the 10-character minimum enforced at `FeedbackSubmission.cs:14` is never
communicated before submit.

### 0.15 Workflow step tabs have no accessible name on mobile — **verified**
`_WorkflowStepTabs.cshtml:34` · `site-mobile.css:352-354`

The button's two children are `<span class="prompt-step-tab__num" aria-hidden="true">` and
`<span class="prompt-step-tab__label">`. `site-mobile.css:352-354` sets
`.prompt-step-tab__label { display: none }` at ≤600px. The visible child is hidden from AT and the
AT-visible child is hidden from CSS, so **every workflow tab on 5 pages announces as an unnamed
button on mobile** — DeckAnalysis, DeckComparison, CedhMetaGap, DeckPrimer, CutLab.

**Fix:** `aria-label="@step.Label"` on the button, or make the label `.sr-only` rather than
`display:none`.

### 0.16 `role="tab"` buttons submit forms — **verified**
`_WorkflowStepTabs.cshtml:24,33` — `type="@(step.SubmitFormId is null ? "button" : "submit")"` plus
`form="@step.SubmitFormId"`. Activating a "tab" submits a form and navigates. Tab activation is
specified to reveal a panel, nothing more; AT users are told these are tabs and get a page load.

### 0.17 Disabled tabs break roving-tabindex navigation — **verified**
`_WorkflowStepTabs.cshtml:31-32` emits both `aria-disabled` and the real `disabled` attribute. A
`disabled` button is unfocusable, so arrow-key traversal of the tablist dead-ends at the first
incomplete step. Compounding it, the partial ships roving `tabindex` (`:30`) but **no arrow-key
handler** — only `primer-selection.ts:279-303` implements one. The four pages driven by
`deck-sync.ts` set `aria-selected`/`tabindex` on click only, so keyboard users can reach exactly one
tab and cannot move between steps.

### 0.18 `_DeckToolTabs` menu toggle controls itself — **verified**
`_DeckToolTabs.cshtml:11-12` — the `<button aria-controls="deck-tool-nav">` is nested **inside**
`<nav id="deck-tool-nav">`. A disclosure trigger pointing at its own ancestor.

Same line: the button carries `hidden`, and `site-mobile.css:147-149` overrides it with
`display: inline-flex !important`. Nothing ever removes the attribute — `site.ts:178-182` only
toggles `aria-expanded`. The control is semantically hidden and visually present on every page that
renders the tool nav.

### 0.19 DeckPrimer's step panels break the tablist contract — **verified as caller-side**
`DeckPrimer.cshtml:121,154,287` declare neither `role="tabpanel"` nor `aria-labelledby`, though
their ids match the partial's `aria-controls`. This is the **caller's** fault, not the partial's:
DeckAnalysis (`:150,200,515,881,961`), DeckComparison (`:228,356,578`), CedhMetaGap
(`:99,208,387`) and CutLab (`:322,853,914,1203`) all do it correctly.

Deeper issue: on Primer all three panels are visible at once and the tabs are jump-links
(`primer-selection.ts:252-256` says so in a comment). That isn't the tablist pattern at all, so
adding `role="tabpanel"` would paper over a wrong component choice.

---

## Tier 1 additions — site-wide

### 1.11 More sub-44px tap targets
- `site-mobile.css:336` — `.prompt-step-tab` is 2.5rem (40px), in the same file that enforces 44px
  at `:126`.
- `site-common.css:4059-4061` — `.share-bar__button` `min-height: 40px`.
- `site-mobile.css:394-398` — `.ai-selector__option-label` `min-height: 40px`.
- `site-common.css:1354-1363` — `.feedback-submit` ~38px, the only action on the Feedback page, with
  no mobile override at all.
- `CommanderCategories.cshtml:70-72` + `site-common.css:2014-2020` — the info tooltip is a 1.2rem
  (~19px) circle whose accessible name is the single letter "i".

### 1.12 iOS input auto-zoom
`site-common.css:1345-1352` — feedback inputs use `font: inherit`, and themes set `--fs-base: 0.95rem`
(~15.2px, e.g. `site-abzan.css:41`). iOS Safari auto-zooms any input under 16px on focus, shifting
the layout mid-typing.
**Fix:** `font-size: max(16px, 1em)`.

### 1.13 `role="alert"` on server-rendered content
Live regions only announce changes *after* load, so a server-rendered alert announces nothing —
while a JS-toggled one announces on every page load. Both mistakes are present:
`Feedback/Index.cshtml:15-17`, `CommanderCategories.cshtml:25-27` (which also duplicates
`:51-53`), `Bracket.cshtml:43-45`, `DeckHistory.cshtml:50`.

### 1.14 A third and fourth copy-to-clipboard implementation
Beyond `deck-sync.ts:315-329` and the `card-lookup.ts:134-168` fork already noted in Part 1:
`content-kb.ts:136-147` and `share-bar.ts:20-24` each reimplement the same label-swap, both without
a live region and both without a manual-select fallback on failure. Four implementations, one
correct.

---

## Tier 2 additions — per page

### Home / tool directory
- **[HIGH]** `:22-29` vs `:58-66` — Deck Analysis renders **twice**: as the hero and again as the
  first Analyze tile. Duplicate link, duplicate accessible name, and the hero loses its "start here"
  signal. Fix: filter `headlineWorkflow` out of the grid.
- **[HIGH]** `:20` — the lede ("Personal Magic: The Gathering deck tooling — analysis, sync,
  reference, and categories") reads as a private side project and names four abstract nouns. A
  first-timer can't tell it's free, Commander/cEDH-focused, or that most tools need no account.
- **[MED]** `:12` — `platformList` is computed and never rendered. The "works with
  ChatGPT/Claude/Gemini" reassurance that answers "do I need an AI account?" never reaches the page.
- **[MED]** `:42,48,51` — Analyze and Reference use the **identical** magnifier SVG; Card Lookup's
  tile icon is the same magnifier again; Categories' section icon duplicates the
  `commander-categories` tile icon. Icons stop functioning as wayfinding.
- **[MED]** `:60` — nothing on a tile says whether the tool is local math or requires pasting into
  an AI, though `ToolRegistry.cs:13,16` say "no AI needed" for some and `:14,15,19` imply AI without
  saying so.
- **[MED]** `site-common.css:364-369` — `.hub-card:focus-visible` changes only `border-color` and adds
  a transform; no outline. Keyboard focus across a 16-tile grid is near-invisible.
- **[MED]** `site-mobile.css:212-219` — 2 columns forced at ≤600px with `min-height: 8.75rem`, while
  tile descriptions range from ~70 to ~210 chars. At 360px the Cut Lab tile is a wall of 0.85rem
  text in a ~160px column.
- **[MED]** `:25` — "Headline workflow" is internal jargon. `:55` — section headings are bare enum
  names (`Analyze`, `Build`, `Reference`, `Categories`) with no subtitle.

### Help
- **[MED]** `Index.cshtml:12-18` — 18 topics in one flat ungrouped list ordered by an ad-hoc `order:`
  int, with `ai-methodology.md` and `browser-extension.md` both at `order: 90` so their relative
  order is decided by a title tiebreak (`HelpContentService.cs:76-78`). No "getting started" topic,
  so a confused first-timer faces 18 equal-weight choices.
- **[MED]** `Index.cshtml:11-19` — no empty state; a missing `Help/` directory renders a bare `<ul>`
  under the promise "User guides for each DeckFlow feature."
- **[MED]** `Topic.cshtml:16-18` — a topic is a dead end: no link to the tool it documents, though
  the registry already stores `HelpSlug` so the inverse mapping exists. No prev/next.
- **[LOW]** `Topic.cshtml:17` — the `<h1>` comes only from the markdown body starting with `#`;
  nothing enforces it, so a topic missing that line yields an h1-less page.

### About
- **[MED]** `:8-16` — leads with Version and a GitHub link (maintainer facts) and never says who the
  site is for or links back to the tools, Help, or Feedback.
- **[MED]** `:15,23` — `target="_blank"` with no indication to sighted or screen-reader users.

### Error page
- **[MED]** `:9` — "the error has been logged" but no request/correlation ID is shown, so a user who
  clicks through to feedback has nothing to quote and the report is unactionable. Fix: surface
  `Activity.Current?.Id ?? HttpContext.TraceIdentifier` in a copyable `<code>` and prefill it into
  the feedback link.
- **[MED]** `:9-10` — "Try again" is advice with no control, and the only exit is home; a user who
  was mid-task on `/manabase` is dumped at the root.
- **[LOW]** `:6` — wraps content in `.content-shell`, which `_Layout.cshtml:137` already applies to
  `<main>`. Nested duplicate padding and max-width.
- **[LOW]** `:2` — hardcodes `"Error — DeckFlow"` while every other view passes a bare title and lets
  the layout compose the suffix. Risks "Error — DeckFlow - DeckFlow".

### Feedback
- **[MED]** `feedback.ts:4-10` — submit is disabled and relabelled "Sending…", but the class
  `feedback-submit--busy` is never added, so the spinner at `site-common.css:1366-1385` is dead code.
  On bfcache back-navigation the button stays disabled reading "Sending…" forever.
- **[MED]** `:24,30,36` — no error summary and no focus move on a failed post.

### ContentKb
- **[HIGH]** `Index.cshtml:72-103` + `ContentKbController.cs:63-80` — every published row is rendered
  unpaginated and filtered client-side by hiding DOM nodes (`content-kb.ts:80`). Heavy first paint
  and a very long tab order at a few hundred entries.
- **[HIGH]** `content-kb.ts:10,34-66` — filter state lives only in `sessionStorage`; a filtered view
  can't be linked, bookmarked, or reached via Back/Forward.
- **[MED]** `Index.cshtml:86` — the entire `.kb-tag-list` is `aria-hidden="true"`, hiding
  source/bracket/archetype/category — the page's primary metadata — from screen readers.
- **[MED]** `site.ts:267-290` — whole-card click navigation via a document-level handler; the card is
  not focusable, has no `role`/`href`, and a mouse text-selection ending inside the card fires
  `click` → accidental navigation.
- **[MED]** `Index.cshtml:21` + `site.ts:241-252` — `<details open>` renders expanded, then JS
  collapses it after `DOMContentLoaded` at ≤600px: a visible flash and layout shift on every mobile
  load.
- **[MED]** `Index.cshtml:119` — the "N entries shown" count is `sr-only`; sighted users get no
  confirmation that filtering worked.
- **[MED]** `Detail.cshtml:40` vs `:43-48` — the lede says "paste into @platformList"
  (multi-platform) while the button says "Copy prompt for ChatGPT" and the `aria-label` says "Copy
  this ChatGPT-ready prompt". The aria-label also doesn't contain the visible label, breaking
  label-in-name for voice control.
- **[MED]** `Detail.cshtml:33-36` — the `ArtifactUnavailable` state is one unstyled sentence with no
  retry and no link to the source video.

### Commander Categories
- **[MED]** `:99-121` — the 25-row cut duplicates the **entire table** inside a `<details>`,
  repeated `<thead>` and a second `<caption>`. AT users encounter two tables of the same data.
- **[MED]** `:93,115` — `Math.Round(share*100)` prints "0%" for any category under 0.5% even when
  `DeckCount > 0`. Fix: render "<1%".
- **[MED]** `:21` — neutral explanatory text styled as `.warning-banner`, reading as a problem.
  `:22`, `:45`, `:57` say the same thing about the cached Archidekt database three times.
- **[MED]** `:41` — "Clear" is ambiguous; `data-clear-cache` clears cached results, not the input.
- **[MED]** `:79-98` — `.conflicts-table` has no ≤480px treatment; a 3-column table with long
  category names overflows horizontally.

### Shared partials
- **[HIGH]** `_FormError.cshtml:7` — the banner is not associated with any field
  (`aria-errormessage`/`aria-describedby`) and nothing moves focus to it. A keyboard user submitting
  an invalid form gets an announcement and no way to find the field.
- **[MED]** `_BusyIndicator.cshtml:1-7` — a blocking-looking overlay that neither traps focus, sets
  `aria-busy`, nor disables the form; users can click through and re-submit. No cancel affordance
  and no timeout state, so a stalled request shows "Working" indefinitely. Its `role="status"` is on
  an element that is `.hidden` at load and mutated before being shown, which makes announcements
  unreliable.
- **[MED]** `_ShareBar.cshtml:14-19` — link names are just "Reddit", "X", "Bluesky"; out of context
  they don't state the action, and the new-tab behaviour is unannounced. `:5-8` — copy result is a
  silent label swap with no live region. `:2-3` — `aria-label` duplicates the visible label, so AT
  announces it twice.
- **[MED]** `_AiSelector.cshtml:21-22` — a `<p>` heading plus a separate `aria-label` on the
  radiogroup; the visible heading is not programmatically the group's label. Fix:
  `<fieldset><legend>`.
- **[LOW]** `_AiSelector.cshtml:15-18` — a persisted "Gemini" preference is silently rewritten to
  ChatGPT with no notice.
- **[MED]** `_DeckToolTabs.cshtml:29` — the current tool is marked with `is-active` only; no
  `aria-current="page"`. `:19,22` — `aria-haspopup="true"` on a trigger whose "popup" is a plain
  `<div>` of links with no id. `site.ts:214-218` — Escape closes all groups but never returns focus
  to the trigger, leaving focus on a now-hidden link.

---

## Revised sequencing

Part 1's batches stand. These slot in:

**Into Batch A (small diffs, no design decisions):** 0.11 (three switch cases), 0.12 (one `<h1>`),
0.13 (one middleware line), 0.14 (remove `novalidate`), 0.15 (one `aria-label`), 0.17 (drop the
`disabled` attribute), 0.18 (one id + one class), 1.12 (one `font-size`), and the 1.11 tap targets.
Collectively these are perhaps 30 lines of change and they close three HIGH accessibility defects
and two visible bugs on the landing page.

**Into Batch B (enforcement):** the icon-switch totality test belongs with the CSS-location CI guard
— same category of failure, same fix shape.

**New Batch F — the shared-tab component.** 0.15/0.16/0.17/0.19 are one problem: `_WorkflowStepTabs`
is used for two incompatible patterns (one-panel-at-a-time tabs on 4 pages, jump-nav on Primer) and
its keyboard contract is implemented four separate times in TS, correctly once. Splitting it into
`_WorkflowStepTabs` (true tablist, owning its own keyboard handler) and `_WorkflowStepJumpNav`
(anchors) resolves the whole cluster.

## Note on the "Copy" cluster

With Part 2 folded in, there are now **four** copy-to-clipboard implementations —
`deck-sync.ts:315-329`, the `card-lookup.ts:134-168` fork, `content-kb.ts:136-147`, and
`share-bar.ts:20-24`. Exactly one announces to screen readers. None offer a manual-select fallback
when the clipboard API fails. The `--icon` variant visually breaks on success (writes the 6-character
word "Copied" into a 32px square styled `color: var(--on-accent)` over a transparent background).

For a product whose stated core value is "output the user can paste into ChatGPT in one round-trip",
consolidating these into one `df-copy.ts` is the single highest-leverage change in this report.
