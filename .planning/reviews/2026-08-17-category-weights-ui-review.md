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

### ✅ HIGH-1 discharged — 2026-08-17, `234a2a70`

`white-space: nowrap` on `th`/`td` `nth-child(n + 2)` plus a `min-width: 3ch` floor on column 3, both in
`site-common.css` next to the `.conflicts-table` block, **scoped to `[data-api-panel="weighted"]`**. The
scoping is the load-bearing part: `.conflicts-table` is shared with Cut Lab, Deck Sync, cEDH Meta-Gap and
Commander Categories, whose prose columns must keep wrapping, so a blanket rule would have been a four-page
regression. Follows the existing `[data-prompt-cedh-reference-table]` scoping precedent at
`site-common.css:1065`.

`nowrap` does double duty — besides stopping intra-value breaks it forces the auto layout to give those
columns max-content width, drawing the slack from the wrappable Category column, which is what reopens the
collapsed `%` column. The `min-width` floor covers the all-em-dash case where no wide numeric content holds
the column open.

Guarded by `DeckFlow.Web.Tests/WeightedCategoryTableCssTests.cs`, which asserts both scoped rules exist
**and** that no unscoped `.conflicts-table th`/`td` rule sets `white-space: nowrap` — the third assertion is
the one that stops a future edit from silently regressing the other four tables.

**Verified headless, 390×844 and 1440×900:** 24 numeric cells measured by counting line boxes per cell
(`Range.getClientRects().length`), **zero wrapped**, all four headers single-line, table 340px at mobile, no
page overflow. Mutation-checked: deleting the two rules via CSSOM reproduces exactly two wrapped cells —
the same `16` and `38` from the original evidence — so the rules are load-bearing and the measurement
genuinely detects the defect rather than passing vacuously.

## 🟠 MEDIUM-1 — no `scope="col"` on any header cell

Verified in the DOM: all four `<th>` return `scope: null`. Screen readers lose the column-to-cell
association, so a value is announced without the header naming it. One attribute per header.

### ✅ MEDIUM-1 discharged — 2026-08-17

`scope="col"` on all four headers in `SuggestCategories.cshtml`. An audit while fixing it showed the
convention is already near-universal here — Cut Lab 29 of 36 `<th>`, cEDH Meta-Gap 9 of 11, Commander
Categories 6 of 8, Deck Sync 4 of 5 — so this view was the sole 0-of-4 outlier rather than a novel gap.

Guarded in `DeckCategoriesControllerTests` by extracting the rendered table and asserting **no `<th>` inside
it lacks `scope="col"`**, so a future fifth column cannot ship unscoped. Three assertions in the existing
above-the-copy-box test had pinned the old bare `<th>` markup and were updated.

No browser pass needed for this one: the render test drives the real Razor engine, so its output is the
served markup, and the TypeScript only ever fills `<tbody>` — `<thead>` is never rewritten client-side.

~~**Follow-up worth its own sweep (not done here):** 12 unscoped `<th>` remain across four other views —
Cut Lab 7, cEDH Meta-Gap 2, Commander Categories 2, Deck Sync 1.~~

#### ⚠ Retracted 2026-08-17 — that follow-up was a counting artifact, not a gap

Re-counted before scheduling the sweep: **there are zero unscoped `<th>` in any of those views.** The
original tally used a `<th`-prefixed grep, which also matches `<thead>`. Per file the claimed "unscoped"
count is exactly that file's `<thead>` count — Cut Lab 7, cEDH Meta-Gap 2, Commander Categories 2,
Deck Sync 1 — and `<th[ >]` versus `<th [^>]*scope=` gives 29/29, 9/9, 6/6 and 4/4 scoped. No TypeScript
creates `<th>` either (`createElement('th')` has no hits under `wwwroot/ts/`), so there is no client-rendered
header escaping the count.

⭐ Lesson matching the vacuous-assertion one above: **a grep prefix that is also a prefix of another tag
silently inflates a defect count**, and the inflated number looked plausible enough to become a scheduled
work item. Anchor the tag with `<th[ >]` when counting header cells.

## 🟠 MEDIUM-2 — the `N/M` ratio is explained only in a `title` tooltip

`<th title="Sources that agreed / sources that contributed">Sources</th>` is the **only** place the
ratio's meaning appears. `title` does not surface on touch devices and is not keyboard-reachable, so
mobile and keyboard users get an unexplained `2/2`. The `aria-label` duplicate helps screen readers but
not sighted touch users. Consider a visible caption or footnote under the table.

### ✅ MEDIUM-2 discharged — 2026-08-17

A visible footnote under the table, `<p class="manabase-lens-note">Sources — how many of the consulted
sources agreed on the category, out of how many contributed.</p>`, reusing the class already present on
this same view (line 130) so no new CSS was needed.

Two placement details are load-bearing. It sits **inside** the `data-api-panel="weighted"` wrapper, so it
hides with the table instead of dangling above the copy box on an empty result — and because the wrapper
is what the TypeScript toggles, the footnote needed no TS change at all. And it sits **after** `</table>`
but **before** the copy-box textarea, keeping the reading order table → explanation → copy.

The `title` and `aria-label` on the Sources header were kept. The footnote adds a channel for touch and
keyboard users rather than replacing the one screen readers already had.

Guarded in `DeckCategoriesControllerTests` by asserting the footnote's index falls between the `</table>`
index and the `id="merged-categories-output"` index — a missing footnote yields `IndexOf` = -1 and fails,
so the assertion cannot pass vacuously.

**Browser-verified as real visible text** (not a tooltip): rendered height 14px at 1280×900 and 28px at
390×844, where it wraps to two lines.

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

### ✅ LOW-1..4 discharged — 2026-08-17

**LOW-1 — right alignment.** `text-align: right` was added to the *existing*
`[data-api-panel="weighted"] .conflicts-table th/td:nth-child(n + 2)` block rather than a new rule, so the
alignment and the HIGH-1 `nowrap` share one scope and cannot drift apart. Census of the shared class before
changing it — `.conflicts-table` has **five** consumers (`CutLab`, `CedhMetaGap`, `CommanderCategories`,
`DeckSync`, `SuggestCategories`) and only the weighted panel carries the attribute, so the other four are
untouched. The guard test gained a matching pair: the scoped rule must exist, **and** no unscoped
`.conflicts-table th`/`td` rule may set `text-align: right`.

**LOW-2 / LOW-3 — the percent column.** Cells now render `{n}%`; `percent == 0 && deckCount > 0` renders
`<1%`; a genuine zero still renders `0%`. **The model was deliberately not touched** — `Percent` stays
`int?` and the API JSON contract is unchanged, because the distinction is display-layer and derivable from
data already on the row. Widening it to `decimal` would have meant a schema/DTO change, and per the
standing note that SQLite tests cannot prove Dapper type maps, that is a prod-Postgres risk bought for no
user-visible gain. In Razor the `<` is escaped by ordinary `@` encoding (`&lt;1%`); in TypeScript the value
goes through `textContent`, never `innerHTML`.

**LOW-4 — accessible unavailable values.** Both paths now emit
`<span aria-hidden="true">—</span><span class="sr-only">Not available</span>`, using the `.sr-only` utility
that already exists in `site-common.css`. The TypeScript builds it with `createElement`/`textContent` via a
dedicated `createUnavailableCell` helper, keeping `createTextCell` free of markup concerns.

**Both render paths were changed together.** The Razor `@foreach` and `renderWeightedCategories` in
`category-suggestions.ts` are independent implementations of the same table, and the earlier BLOCK-1 defect
was precisely a change landing on one path only. All four cases are now asserted on **both** — `<td>34%</td>`,
`&lt;1%`, `<td>0%</td>` and the paired dash/sr-only spans in `DeckCategoriesControllerTests`; the exact
`textContent` concatenations `'Protection120100%3/4'`, `'Tutor—Not available—Not available1/4'`,
`'Trinket3<1%1/3'`, `'Zero00%1/3'` in the vitest DOM test.

**Verification — 2 viewports, headless Chromium in WSL, no browser on the Windows host:**

- Live path (`CachedData`, seeded DB, 5 rows) at 1280×900 and 390×844: computed `text-align` is
  `left, right, right, right` for both header and body cells; **zero** cells wrapped across line boxes, so
  HIGH-1 did not regress; percents read `38% 16% 9% 1% <1%`; table 340px at mobile with
  `scrollWidth == clientWidth == 390`; no console errors.
- ⭐ **The live corpus gave every row a deck count, so it produced no em dash and the LOW-4 check passed
  vacuously.** Rather than let that stand, the real TypeScript renderer was driven directly in the browser
  with all four canonical cases, which returned exactly
  `171/16%`, `3/<1%`, `0/0%`, and `—/—` with `['Not available', 'Not available']` announced — closing LOW-2,
  LOW-3 and LOW-4 on the JS path with real data rather than an accidental one.
- **Mutation-checked:** deleting the scoped rule through CSSOM flips the percent column from `right` back to
  `left`, so the CSS is load-bearing and the measurement genuinely detects its absence.

`dotnet build` clean (pre-existing `NU1903` SSH.NET advisory only). Filtered xUnit 24/24; vitest 34 files /
128 tests. The full Web suite still stalls under the WSL test host, so the filtered run remains the
workaround.

**Reviewers:** `codex review` (gpt-5.6-sol) returned no actionable findings. The Gemini pass was attempted
but is **not** part of this discharge — the CLI needs configuring first (paused at the user's request), so
this batch carries one automated reviewer plus the lead's own repo census and browser pass. The `.conflicts-table` consumer
census above was run directly against the repo rather than inferred from the diff, since a diff-scoped
reviewer cannot see the four other tables the shared class reaches.

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

**Still open after BLOCK-1:** HIGH-1 (discharged separately below, `234a2a70`), MEDIUM-1, MEDIUM-2, LOW-1..4.

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

---

## ✅ Order disagreement discharged — 2026-08-17, GSD quick task `260817-kvn`, branch `fix/category-copy-box-order`

**Decision:** the copy box adopts the **table's** ranking. The copy text FORMAT is unchanged — plain
`- Category` lines, no weights, no percentages; the earlier locked decision still stands. Order only.

**The divergence was narrower than recorded above.** The copy box was not on "raw merge order" — both
paths sort `SourceCount DESC` first and then split:

| Path | Secondary | Tertiary |
|---|---|---|
| Copy text (`MergeWeighted`, `CategorySuggestionReporter.cs:105-110`) | Authority DESC (Exact/Tagger=3, Inferred=2, Edhrec=1) | label ASC |
| Table (`CategoryWeightRowFactory.Build`, `CategoryWeightRowFactory.cs:22-28`) | Percent-is-null ASC, then Percent DESC | Category ASC |

**Why the fix lives in Web, not Core.** Both controllers now hoist `Build`'s ranked rows into a local and
project the copy text *and* the table from that one list. `ToText` is unchanged and Core is behaviourally
untouched.

⚠ **Correction (2026-08-17, during the follow-up `/simplify` pass).** This section originally justified the
Web placement by claiming `Build` needs "Web-layer lookups Core never sees". **That claim is false.**
`GetCategoryDeckCountsAsync` originates in Core at `DeckFlow.Core/Knowledge/CardCategoryRepository.cs:136`
and is exposed by `CategoryKnowledgeRepository.cs:73`; the dictionary merely round-trips out through the
Web `ICategoryKnowledgeStore` facade and back into the Web factory. Core *can* see these counts, so
unifying the ranking in Core was always technically available. Web placement remains a defensible choice —
display ranking is presentation, and `Build` also owns the percent derivation and the em-dash fallback —
but it is a choice, not a constraint, and the original rationale should not be cited as one. The false
statement was also carried in a code comment on `CategorySuggestionReporter.Merge`; that method had zero
production callers and was deleted outright on `refactor/category-weights-simplify`, taking the comment
with it.

**Changes:** `DeckCategoriesController.cs:112-130` (`2b5eda2e`), `SuggestionsApiController.cs:88-95`
(`4ec7aaad`), and a comment-only correction at `CategorySuggestionReporter.cs:62-64` (`7003bbfb`) — the old
comment claimed the label overload "keeps the copy text and the weighted table in lockstep", which this
change makes false. That overload (`Merge`) has **zero production callers**; only
`DeckFlow.Core.Tests/CategorySuggestionReporterMergeTests.cs` calls it.

**The existing suite gave this defect zero coverage.** All four copy-text assertions
(`DeckCategoriesControllerTests.cs:82`, `:115`, `:224`, `:237`) use fixtures where the two comparators
happen to coincide — verified arithmetically, and none needed editing. Two new facts were added on a
fixture where they genuinely diverge: `edhrec=["Ramp"]`, `tagger=["Protection","Draw"]`, counts
`draw=6 / ramp=30` of 60 → merge order `Draw, Protection, Ramp` vs table order `Ramp, Draw, Protection`.
Alphabetical order equals the *old* order, so an accidental alpha sort also fails. Each test asserts the
literal order **and** the structural invariant that `WeightedCategories` matches the copy-text lines.

**Mutation proof (run independently by the reviewer, not taken on report):** flipping
`CategoryWeightRowFactory.cs:26` to `.ThenBy(row => row.Percent)` reddens **exactly** the two new facts,
23/25 stay green — the pre-existing table-order tests resolve their ties on the null-Percent flag. Line
restored and re-verified.

**Verification:** WSL `dotnet build` clean; filtered xUnit 25/25 via the Windows host (independent re-run);
`codex review` stage 1 (`gpt-5.6-sol`) found no actionable defects. Stage 2 judged unnecessary — the
consumer census is closed: `MergedCategoriesText` is rendered verbatim by
`SuggestCategories.cshtml:181` and `category-suggestions.ts:272-273` (`setFieldText`, no parsing or
re-sorting), and no vitest spec asserts merged ordering. EOL verified per file (all five LF, unchanged);
whitespace-ignoring diffstat is identical to the plain diffstat, so no reflow churn.

⚠ **Still owed: real-browser UAT.** Verification was headless and unit-level only.

⭐ **Process lesson.** The first Codex dispatch halted claiming the fixture did not diverge, reporting the
table order as `Draw, Protection, Ramp`. `BuildCategoryWeightRow` looks deck counts up by
`CategoryCanonicalizer.CanonicalKey`, which **lower-cases** (`CategoryCanonicalizer.cs:35`). Seed the fake
store with display labels instead of lower-case keys and every lookup misses silently, every `Percent`
goes null, the null-Percent tier stops separating anything, and the table collapses to the alphabetical
tiebreak — reproducing precisely that order. Nothing throws. Same write-normalized / read-raw dictionary
class as BLOCK-1's case-variant crash. A fixture whose keys miss is indistinguishable from a fixture that
does not diverge.
