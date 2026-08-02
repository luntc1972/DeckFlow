---
status: resolved
trigger: "on the searching rules page on mobile the text area containing the full rules does not scroll, it used to can fix it and check to see if there are other errors, do not break anything"
created: 2026-08-01
updated: 2026-08-01
---

# Mobile rules textarea does not scroll

## Symptoms

- **Page:** `/mechanic-lookup` (Mechanic Rules) — confirmed with user.
- **Element:** `DeckFlow.Web/Views/Deck/MechanicLookup.cshtml:63`
  `<textarea id="mechanic-rules-output" readonly spellcheck="false">@Model.RulesText</textarea>`
- **Expected:** the full Comprehensive Rules text for the looked-up mechanic is readable on a phone.
- **Actual:** the box shows only a few lines and the rest of the text is unreachable.
- **Timeline:** worked before; regressed at some point.
- **Environment:** iOS Safari / iPhone (per user).
- **Repro:** load `/mechanic-lookup`, look up any mechanic with long rules text (e.g. Prowess), view on a phone.

## Current Focus

- hypothesis: A mobile-only `max-height: 10rem` cap on the bare `textarea` element
  selector shrinks readonly *output* textareas to a ~5-line window, leaving the
  remaining content reachable only by internal scrolling.
- test: measure computed height / scrollHeight of `#mechanic-rules-output` at 390px
  against the real stylesheets, then prototype a scoped fix and re-measure.
- expecting: clientHeight far smaller than scrollHeight.
- next_action: verify candidate fix in harness, then confirm the iOS-specific
  touch-scroll mechanism on a real device.

## Evidence

- timestamp: 2026-08-01 — **Measured, Chromium @ 390x844**, real stylesheets loaded in
  `_Layout.cshtml` order (site-common -> theme -> site-theme-overrides -> site-mobile),
  real DOM ancestry (`.page-shell > main.content-shell > #mechanic-lookup-results.stack >
  .result-panel > textarea`):

  | property | value |
  |---|---|
  | `clientHeight` | **88px** |
  | `scrollHeight` | **2903px** |
  | computed `height` / `min-height` / `max-height` | `90px` / `90px` / `150px` |
  | `overflow` / `overflow-y` | `auto` / `auto` |
  | `touch-action` | `auto` |
  | ancestor `.result-panel` / `.page-shell` / `body` `overflow` | all `visible` |
  | programmatic `scrollTop = 9999` | -> `2815` (scrollable in Blink) |

  At desktop 1280px the same element is `clientHeight 238px`, `max-height: none`.
  So the defect is mobile-only and is a *height* defect, not an overflow-clipping defect.

- timestamp: 2026-08-01 — **No JS is involved.** `grep` for `style.height` / `scrollHeight` /
  `rows =` across `wwwroot/ts/*.ts` finds no code that sizes any textarea. The only
  textarea-sizing JS is the Expand toggle at `wwwroot/ts/deck-sync.ts:358-374`, which
  toggles the `prompt-artifact-textarea--expanded` class.

- timestamp: 2026-08-01 — **Regressing commit identified: `98f525d23`**
  (2026-06-13, "fix(ui): mobile chrome polish — compact header, secondary download, tighter forms").
  Found via `git log --all -S"max-height: 10rem"`. It added to `site-mobile.css`
  `@media (max-width: 600px)`:

  ```css
  textarea {
    min-height: 6rem;
    max-height: 10rem;
    overflow: auto;
  }
  ```

  The commit body states both the intent and the blind spot verbatim:
  - intent: *"cap textarea height (6-10rem) so **empty paste boxes** don't balloon the page"*
  - verification: *"Verified at 390x844 on **/deck-analysis**"* — a single page, and an
    *input* page at that.

  The cap was aimed at empty **input** paste boxes, but the selector is the bare
  `textarea` element selector, so it also hit every readonly **output** textarea.

- timestamp: 2026-08-01 — **Blast radius: 38 readonly output textareas across 13 views**
  (`grep -rn "<textarea[^>]*readonly" DeckFlow.Web/Views`). Only the ~7 carrying
  `.prompt-artifact-textarea` have an Expand escape hatch
  (`.prompt-artifact-textarea--expanded { max-height: none }`, site-common.css:1433).
  `#mechanic-rules-output` has no escape hatch. Affected views: DeckComparison,
  DeckPrimer, CutLab, Bracket, DeckConvert, CedhMetaGap, DeckSync, DeckHistory,
  DeckAnalysis, MechanicLookup, JudgeQuestions, Manabase, SuggestCategories.

- timestamp: 2026-08-01 — **CI cannot catch this class of bug.**
  `DeckFlow.Web/playwright.config.ts` defines only `chromium-desktop` and
  `chromium-mobile` projects. No WebKit project exists, so no iOS-engine behavior
  is ever exercised.

## Eliminated

- hypothesis: JS resizes or clips the textarea.
  why: no `style.height` / `scrollHeight` writes anywhere in `wwwroot/ts/*.ts`.

- hypothesis: an ancestor (`.result-panel`, `.page-shell`, `body`) clips with `overflow: hidden`.
  why: measured `overflow: visible` on all three at 390px.

- hypothesis: `touch-action` on an ancestor blocks panning.
  why: measured `touch-action: auto` on the textarea and every ancestor. The
  `touch-action: manipulation` rule at site-common.css:31 is scoped to
  `button, a, summary` only.

- hypothesis: `site-theme-overrides.css` overrides the sizing.
  why: the file contains no `textarea` or `.result-panel` rules.

- hypothesis: the recent CSS refactors caused it (`6106b12de` hoist,
  `5ccc80ba8` dead-selector removal).
  why: pickaxe attributes the cap to `98f525d23`, months earlier. `6106b12de`
  explicitly *excluded* `.result-panel textarea` from hoisting.

## Open / unverified

- **The iOS-specific mechanism is NOT yet verified.** Chromium *can* scroll this box
  programmatically, so Blink users merely get a cramped 88px window. Whether iOS
  Safari additionally refuses to *touch*-scroll a `readonly` textarea (a commonly
  reported WebKit behavior) could not be confirmed here: `npx playwright install webkit`
  fails on this WSL box for missing system libraries (`libwoff2dec`, `libhyphen`,
  `libmanette`, ...), which need a root `playwright install-deps webkit`.
  This matters for fix selection: if it is purely a height defect, enlarging the box
  fixes it; if WebKit also refuses touch-scroll on readonly textareas, the box must be
  large enough that internal scrolling is not required, or the control must change.

## Resolution

- root_cause: `98f525d23` (2026-06-13) capped mobile textarea height to `10rem` using the
  bare `textarea` element selector. The cap was intended for empty **input** paste boxes
  (and was verified on only one page, `/deck-analysis`), but the selector also matched all
  38 readonly **output** textareas, reducing them to an ~88px window onto content running
  thousands of pixels.

- fix: scope the readonly outputs out of the input cap, in the same
  `@media (max-width: 600px)` block of `site-mobile.css`:

  ```css
  textarea[readonly]:not(.prompt-artifact-textarea--expanded) {
    min-height: 50vh;
    max-height: 70vh;
  }
  ```

  `textarea[readonly]` is (0,1,1) so it beats the bare `textarea` (0,0,1); the `:not()`
  guard leaves the existing Expand button behavior untouched.

- verification:
  - Measured at 390x844 against the real stylesheets: `#mechanic-rules-output`
    **88px -> 420px**; editable `#input-paste-box` **88px -> 88px** (unchanged, so
    98f525d23's intent is preserved); `.prompt-artifact-textarea--expanded`
    **504px -> 504px** (unchanged); desktop unchanged in every row.
  - Regression test added to `e2e/ui-responsive.spec.ts` and **mutation-proofed**:
    with the CSS reverted it fails (`148px` received vs `337.6px` required); with the
    fix it passes on both chromium projects.
  - Full e2e suite run on `chromium-desktop` + `chromium-mobile` **with and without**
    the fix at `--workers=1`: **identical failure sets** (5 failures either way), so the
    change causes zero regressions. Those 5 are pre-existing
    (`content-kb-pending-hidden` x2, `interactions:89` x2, `deck-analysis-mobile:41`).
    Four further failures seen only in the 12-worker run
    (`bracket-smoke`, `cut-lab-structure`, `cut-lab-theme-readability`,
    `deck-analysis-render`) are parallelism flake — they pass at `--workers=1`, and
    `deck-analysis-render` failed inside `acquireAdminLock`.
  - Line endings verified per file against HEAD: all four files LF, matching HEAD;
    `git diff --stat` and `git diff --ignore-all-space --stat` identical (no churn).

- files_changed:
  - `DeckFlow.Web/wwwroot/css/site-mobile.css` (the fix)
  - `DeckFlow.Web/e2e/ui-responsive.spec.ts` (regression guard)
  - `DeckFlow.Web/playwright.config.ts` (new `webkit-mobile` project, scoped to the
    three mobile/responsive specs so CI cost stays bounded)
  - `.github/workflows/ci.yml` (install webkit alongside chromium — required, or the
    new project breaks CI)

- branch: `fix/mobile-readonly-textarea-scroll` (uncommitted, pending user device test)

## Follow-up: device UAT closed the open question, and reframed the fix

**iOS device confirmation (2026-08-01, user on iPhone):** the box **does** touch-scroll.
The suspected WebKit "readonly textareas do not touch-scroll" behavior is therefore NOT
in play here — that hypothesis is eliminated. But the user reported the box was still
too short to read comfortably: "with the small area it's hard to actually read because
you are always scrolling."

**Resolution: replace the control instead of resizing it.** The page already rendered
its *summary* as `<pre id="mechanic-summary-output" class="oracle-text">` (line 48), and
Card Lookup renders all rules text into `<pre class="cr-text">` / `<pre class="oracle-text">`
(`wwwroot/ts/card-lookup.ts:274-277`) — never a textarea. `site-common.css:1390` already
carries the shared, theme-safe rule block for these, headed "Card Lookup readability
redesign (shared across all themes) ... auto-grow pre". The rules body was the only part
of the page still on a fixed-height control.

`MechanicLookup.cshtml:63` changed from
`<textarea id="mechanic-rules-output" readonly spellcheck="false">` to
`<pre id="mechanic-rules-output" class="oracle-text" spellcheck="false">`.

Two facts made this a one-line change with no TypeScript work:
- `copyElementValue` (`wwwroot/ts/deck-sync.ts:296-298`) already falls back to
  `target.textContent ?? ''` for non-form elements, so the Copy button kept working.
- Repo-wide, `mechanic-rules-output` had exactly two references, both in this view.

`oracle-text` was chosen over `cr-text` because `.result-panel pre.cr-text`
(site-common.css:1416) adds `padding: 2.5rem 1rem 0.75rem` to clear Card Lookup's
*overlaid* icon copy button; Mechanic Rules puts Copy in the `.panel-heading`, so
`cr-text` would have left a dead 2.5rem gap.

Measured after the swap (live lookups against the running app):

| engine / viewport | mechanic | height | inner scroll | h-overflow |
|---|---|---|---|---|
| chromium 390x844 | Prowess (248 chars) | 250px | none | none |
| webkit iPhone 13 | Prowess | 250px | none | none |
| chromium 1280 | Prowess | 146px | none | none |
| webkit iPhone 13 | Landwalk (1060 chars) | 828px | none | none |
| webkit, Abzan fork theme | Prowess / Landwalk | 250px / 828px | none | none |

`clientHeight == scrollHeight` in every case, so there is no inner scrollbox left at all.
Copy verified end-to-end in chromium: clipboard received all 248 chars and the button
reported "Copied". The 12 fork themes' `.result-panel pre { min-height: 16rem }` does
apply (computed `min-height: 240px` under Abzan) but is inert in practice, since even the
shortest real section renders taller than that.

The `textarea[readonly]` mobile rule from the original fix REMAINS correct and in force —
it still governs the other 37 readonly output textareas across 12 other views.

## Closed

- **Local WebKit run — done.** WebKit was installed and the project ran. It immediately
  earned its keep by catching a real, pre-existing iOS-only defect: `.prompt-step-footer`
  overflowed the document by 12px on `/deck-analysis` because its buttons carry both
  `white-space: nowrap` and `flex-shrink: 0` while the row never set `flex-wrap`. Fixed in
  `c550391b`; `deck-analysis-mobile.spec.ts:41` went from failing 2 of 3 WebKit runs to
  passing 3 of 3.
- **Full CI-mode gate (`CI=1`, workers=1, retries=1, all three projects): 397 passed,
  4 failed.** The 4 are the content-kb pair x 2 projects, a purely local artifact — those
  tests carry `test.skip(count === 0, 'no KB entries seeded')` and self-skip against CI's
  fresh database, which is why `main`'s CI history is green. Identical failure set before
  and after every change in this session, so nothing here regressed anything.

## Still owed

- Nothing blocking. The only unexercised path is a device re-check that the new
  auto-growing `pre` reads well on a real iPhone once this reaches prod; the headless
  WebKit measurements above show no inner scrollbox at any tested length or theme.
