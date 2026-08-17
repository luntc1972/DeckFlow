# UI review — weighted category table on /suggest-categories

**Date:** 2026-08-17
**Scope:** the `Category | Decks | % | Sources` table added by `1b550a2b`, as landed on `main` (`ca9ad25f`).
**Method:** headless Chromium (Playwright, WSL, no browser on the Windows host), 2 viewports —
desktop 1440×900 and mobile 390×844, `deviceScaleFactor: 2`. Plus a static pass over the Razor markup,
the model, and `.conflicts-table` CSS.
**Verdict:** ⛔ **1 BLOCK, 1 HIGH, 2 MEDIUM, 4 LOW.** Do not push until BLOCK-1 is resolved — the
feature is invisible to real users in its current form.

## Test data — read this before reproducing

The local `artifacts/category-knowledge.db` was **unusable**: 322,677 observations and 349,596 totals,
but `cards` and `sources` were **empty (0 rows)**, and the tables were on the **pre-interning schema**
(`source TEXT` / `normalized_card_name TEXT` inline, no `card_id`/`source_id`). Current code joins
`cards`, so every query fails or returns nothing. That DB predates the interning refactor and was never
migrated.

For the review the legacy file was moved aside, the app was allowed to create a fresh schema, and six
categories were seeded for `Sol Ring` (1078 total decks) with deliberately varied magnitudes plus one
long label. **The original DB was restored afterwards** (322,677 rows verified back in place). Leftovers
to delete when convenient: `artifacts/category-knowledge.db.ORIG-BACKUP`,
`artifacts/category-knowledge.db.SEEDED-uireview`.

---

## ⛔ BLOCK-1 — the weighted table never renders in a real browser

**The feature does not reach users at all.** It renders only when JavaScript does not intercept the
form, which never happens in normal use.

Evidence, three independent confirmations:

1. **Live browser, both viewports:** filling `Sol Ring` and clicking **Suggest** produces the
   "Suggested Categories" copy box, the deck-count line, and `Source used: Scryfall Tagger + cached
   store` — and **no table**. `table.conflicts-table` count = 0. No console errors.
2. **JS-free `curl` POST to `/suggest-categories`:** the table **is** present in the response, fully
   populated and correctly ordered.
3. **Code path:** `wwwroot/ts/category-suggestions.ts:460-463` —
   `form.addEventListener('submit', event => { event.preventDefault(); … submitSuggestion(form); })`.
   The browser posts to `/api/suggestions/card` instead. `CategorySuggestionApiResponse`
   (`Models/Api/SuggestionResponses.cs:11`) exposes `MergedCategoriesText`, `ExactCategoriesText`,
   `InferredCategoriesText` and their context strings — **no weighted-category field**. No TS builds a
   table.

**Root cause is a scoping gap, not a coding error.** PLAN.md § Non-goals states: *"`SuggestionsApiController`
JSON response stays unchanged (out of scope; web view only)."* That was written as though the Razor view
were the live surface. It is not — the view is the no-JS fallback. The plan's own § Edit inventory never
listed a TS file, so nothing in the build could have flagged it.

**Fix direction (needs a decision, not just an edit):** either add the weighted rows to
`CategorySuggestionApiResponse` and render the table in `category-suggestions.ts`, or drop the
`preventDefault()` for this form so it posts natively. The former keeps the current UX; the latter is
smaller but loses the progress overlay.

⭐ **Process lesson:** every prior gate on this feature reviewed the diff, the plan, and the tests. None
opened a browser, so a feature that renders nothing passed a BLOCK-and-MEDIUM review, a rebase, a
full-suite run and an ff to `main`. **Server-rendered markup on a JS-driven page is not evidence the
markup is reachable.**

## 🔴 HIGH-1 — numbers wrap mid-value at 390px, corrupting the reading

At mobile width, two-digit values **split across lines inside the cell**:

- `16` renders as `1` / `6`
- `38` renders as `3` / `8`

A percentage broken across two lines reads as two separate numbers; `38` becoming a stacked `3` and `8`
is worse than useless on a column whose entire job is quick comparison. Headers wrap too — `Decks` →
`Deck` / `s`, `Sources` → `Sourc` / `es`.

Cause: `.conflicts-table th, td` (`site-common.css:5708`) sets only padding, border, `text-align: left`,
`vertical-align: top`. No `white-space: nowrap`, no column sizing. The `%` header is one character wide,
so the column collapses to near-minimum and its own data cannot fit.

Desktop 1440px is unaffected — table 100% width, no overflow, renders cleanly.

**Fix direction:** `white-space: nowrap` on the three numeric columns, and give `%` a min-width. Per the
project's standing rule the new rule belongs in `site-common.css`, not `site.css`, because guild themes
are standalone forks. Verify at 390px after.

## 🟠 MEDIUM-1 — no `scope="col"` on any header cell

Verified in the DOM: all four `<th>` return `scope: null`. Screen readers lose the column-to-cell
association, so a value is announced without the header naming it. One attribute per header.

## 🟠 MEDIUM-2 — the `N/M` ratio is explained only in a `title` tooltip

`<th title="Sources that agreed / sources that contributed">Sources</th>` is the **only** place the
ratio's meaning appears. `title` does not surface on touch devices and is not keyboard-reachable, so
mobile and keyboard users get an unexplained `2/2`. The `aria-label` duplicate helps screen readers but
not sighted touch users. Consider a visible caption or footnote under the table.

## 🟡 LOW findings

- **LOW-1 — numeric columns left-aligned.** `.conflicts-table` forces `text-align: left` on every cell,
  verified for all four columns at both viewports. `412` / `96` / `7` / `3` down a ragged left edge is
  harder to compare than right-aligned. Cosmetic, but this table exists for comparison.
- **LOW-2 — `%` renders a bare integer.** `16`, not `16%`; the unit lives only in the header, while the
  Commander Categories page renders `% of decks`. Two surfaces read differently. (Confirms LOW-2 from the
  2026-08-16 gate.)
- **LOW-3 — sub-1% rounds to `0`.** `Draw` shows `Decks 3`, `% 0`. `Percent` is `int?`, so 3/1078 =
  0.28% floors to `0`, and a row reading "3 decks, 0%" looks like a bug to the user. `<1` would be honest.
- **LOW-4 — `—` for unavailable values has no accessible text.** Tagger-only categories render an em dash
  in Decks and %, announced as "em dash" or skipped. A visually-hidden "not available" would read better.

## What is correct — verified, not assumed

- **Ordering matches the spec exactly:** agreement desc, then % desc. Observed `Fast Mana 2/2`,
  `Mana Rock 2/2`, then `Ramp 1/2 38%`, `Colorless… 1/2 1%`, `Draw 1/2 0%`.
- **The `—` fallback works.** Tagger-only categories (`Activated Ability`, `Adds Multiple Mana`,
  `Full Refund`) correctly show `—` for Decks and % while still showing `Sources 1/2`, exactly as the
  plan's data-availability section requires.
- **No layout overflow at 390px:** table 340px wide, `documentElement.scrollWidth == clientWidth == 390`,
  no horizontal page scroll, no internal table scroll.
- **No console errors** at either viewport.
- **The Copy box is unchanged** — plain `- Category` lines, weights stay on screen, per the locked
  decision.
- **Junk filtering works:** seeded `Artifact` was correctly excluded by `CategoryFilter`.

---

## ✅ BLOCK-1 discharged — 2026-08-17, branch `fix/category-weights-block1`

**Route chosen:** add the weighted rows to the API and render the table in TypeScript (option A). The
`preventDefault()` stays, so the progress overlay is preserved.

**Changes (uncommitted at time of writing):**

- `DeckFlow.Web/Services/Categories/CategoryWeightRowFactory.cs` (new) — the ranking + canonical-key lookup
  moved out of `DeckCategoriesController`'s private helpers so the MVC and API paths share one
  implementation and cannot drift again. Behavior identical.
- `Models/Api/SuggestionResponses.cs` — `CategorySuggestionApiResponse.WeightedCategories`, reusing the
  existing `CategoryWeightRow` record. Purely additive to the public JSON contract.
- `Controllers/Api/SuggestionsApiController.cs` — `Merge` → `MergeWeighted`, with `MergedCategoriesText`
  projected from the same pass, so the copy box and the table are built from one merge.
- `Views/Deck/SuggestCategories.cshtml` — the table skeleton is now always emitted inside a
  `data-api-panel="weighted"` wrapper (`hidden` when empty), with `<tbody data-api-field="weighted-body">`
  as the fill hook. The no-JS `@foreach` still populates it server-side.
- `wwwroot/ts/category-suggestions.ts` — `weightedCategories` on the response type (declared **optional**,
  so a stale cached script against a new server degrades to no table rather than throwing) plus a renderer
  that mirrors the Razor cells exactly, including the `—` fallbacks.

**Review finding on the fix, caught and corrected (round 2):** the wrapper was first given
`class="result-panel"`, nested inside the merged panel which is already a `.result-panel`. That class carries
border + radius + background (`site.css`) and `padding: 1rem` (`site-common.css`), so the table rendered
inside a second card, and the extra 2rem of horizontal padding would have shrunk the table at 390px —
aggravating HIGH-1. The new xUnit test had asserted the `result-panel hidden` literal, cementing it. Both
were corrected; the test now also asserts the wrapper does **not** carry `result-panel`.

**Verification:**

- `dotnet build` clean, no new warnings (pre-existing `NU1903` SSH.NET advisory remains).
- xUnit `DeckCategoriesControllerTests` + `SuggestionsApiControllerTests`: 22/22 pass, including 3 new tests.
  (The full Web suite still stalls under the WSL test host; the filtered run is the workaround.)
- vitest from `DeckFlow.Web`: 34 files / 128 tests pass (+1 file, +2 tests).
- **Headless 2-viewport browser pass, real JS path** (1440×900 and 390×844, seeded DB, no browser on the
  Windows host): the table renders with 8 rows in the specified order — `Fast Mana 171/16/2-of-2`,
  `Mana Rock 96/9/2-of-2`, `Ramp 412/38/1-of-2`, `Colorless Value Engine Enabler 7/1`, `Draw 3/0`, then
  `Activated Ability`, `Adds Multiple Mana`, `Full Refund` with `—`. Cell text matched the API payload
  row-for-row, wrapper carried no `result-panel`, no horizontal page overflow, no console errors. The
  `/api/suggestions/card` response was confirmed non-empty (5 rows in `CachedData` mode) so the assertions
  ran against real data rather than an empty-state short-circuit.

**Still open — deliberately untouched by this fix:** HIGH-1, MEDIUM-1, MEDIUM-2, LOW-1..4.

## 🟡 New observation — copy box and table disagree on order (pre-existing, not a regression)

Visible in the desktop capture: the table is ranked agreement-then-popularity, but the copy box lists
`Fast Mana, Mana Rock, Activated Ability, Adds Multiple Mana, Full Refund, Colorless Value Engine Enabler,
Draw, Ramp` — `MergeWeighted`'s raw order, because `MergedCategoriesText` projects the merge before the
display ranking is applied. Same eight categories, two different orders on one screen. This behavior is
identical on `main` (`DeckCategoriesController.cs:124-126` does the same), so it predates BLOCK-1 and its
fix; recording it rather than folding it in.

## Recommended order

1. **BLOCK-1** — decide API-plus-TS vs native POST, then implement. Nothing else matters until the table
   is reachable.
2. **HIGH-1** — nowrap + `%` min-width in `site-common.css`; re-verify at 390px.
3. **MEDIUM-1 / MEDIUM-2** — `scope="col"`, and surface the ratio's meaning visibly.
4. **LOW-1..4** — batch with any of the above.
5. **Re-run this 2-viewport pass after BLOCK-1**, against the real browser path rather than a forced
   native POST.
