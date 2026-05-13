---
phase: quick-260513-wdg
plan: 01
type: backlog-seed
mode: quick
status: captured
created: 2026-05-13
source: /web-design-guidelines audit (9 reviews, 2026-05-13 session)
guidelines_url: https://raw.githubusercontent.com/vercel-labs/web-interface-guidelines/main/command.md
intent: v1.3 frontend hardening backlog — pick high-leverage items, ship as sweep PRs
---

# Web Interface Guidelines Audit — Cumulative Findings

Rolled-up findings from 9 separate `/web-design-guidelines` runs against the DeckFlow frontend on 2026-05-13. Each section groups findings by leverage: P1 = real a11y/security bug, P2 = guideline violation with measurable user impact, P3 = polish/nit.

## Audit coverage

| Review | Targets | Outcome |
|--------|---------|---------|
| 1 | ChatGPT views (Packets, DeckComparison, CedhMetaGap) | findings logged |
| 2 | Shared partials (_Layout, _AdminLayout, _AiSelector, _BracketCallout, _BusyIndicator, _DeckFlowBridgeHint, _DeckToolTabs, _FormError, _MaintenancePage, _MoxfieldBulkEditHint, _WorkflowStepTabs) | findings logged |
| 3 | Admin views (AdminLanding, AdminFlags, AdminHarvest, AdminAnalytics, AdminFeedback Index/Detail) | findings logged |
| 4 | site.css + site-common.css (2621 lines) | findings logged |
| 5 | Other Deck views (Home, DeckSync, DeckConvert, CardLookup, MechanicLookup, JudgeQuestions, SuggestCategories, Error) | findings logged |
| 6 | Commander + About + Feedback + Help views | findings logged |
| 7 | admin.css (152 lines) | findings logged |
| 8 | TypeScript modules (12 files, 5961 lines — small read in full, large sampled) | findings logged |
| 9 | site-mobile.css + site-theme-overrides.css + dark-theme spot-check (site-nyx, site-planeswalker-dark) | findings logged |

**Not reviewed:** 20 remaining guild theme stylesheets (forks of site.css per CLAUDE.md), full reads of 5 large TS modules (sampled patterns only).

---

## P1 — Real bugs (a11y / security / spec violations)

### A. admin.css missing universal `:focus-visible` block — keyboard-broken admin shell

`_AdminLayout.cshtml:14` loads only `admin.css`. site.css's universal `:focus-visible` outline ring (`a, button, input, select, textarea, summary, [role="tab"]`) is NOT inherited because admin shell does NOT load site-common.css or site.css. **Result: admin pages have zero visible keyboard focus indicator across all interactive elements.**

- **File:** `DeckFlow.Web/wwwroot/css/admin.css`
- **Fix:** Add universal `:focus-visible` rule mirroring `site.css:109-118`
- **Impact:** Every admin user using keyboard navigation

### B. `df-typeahead.ts` autocomplete is mouse-only — no keyboard navigation

`wwwroot/ts/df-typeahead.ts` shared typeahead module used by SuggestCategories, DeckConvert (commander), JudgeQuestions, CommanderCategories, CardLookup. No ArrowDown/Up/Enter/Escape handlers on listbox panel. Input lacks ARIA combobox attributes (`role="combobox"`, `aria-autocomplete="list"`, `aria-expanded`, `aria-controls`). Options lack `role="option"`. `aria-activedescendant` missing.

- **File:** `DeckFlow.Web/wwwroot/ts/df-typeahead.ts`
- **Fix:** Add keyboard navigation + full ARIA combobox pattern
- **Impact:** Keyboard users cannot pick suggestions from any autocomplete in the app

### C. `_WorkflowStepTabs.cshtml:12-13` — all tabs render with `aria-selected="false"` `tabindex="-1"` server-side

ARIA tablist pattern requires exactly one tab with `aria-selected="true"` `tabindex="0"`. Server pre-render gives NO tab in focus order. If JS fails on load, keyboard users have no entry point to the tablist.

- **File:** `DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml`
- **Fix:** Server-render `aria-selected`/`tabindex` based on `currentStep`
- **Impact:** Keyboard users on Packets / DeckComparison / CedhMetaGap if JS fails

### D. Inline `style`/`onsubmit`/`onchange` violates strict CSP

- `Views/Deck/Error.cshtml:7-8` — `style="margin-top:2rem; padding:1.5rem;"` + `style="margin-top:0;"`
- `Views/AdminFeedback/Detail.cshtml:39-40` — `onsubmit="return confirm('Delete feedback #@Model.Id permanently?');"`
- `Views/AdminFeedback/Index.cshtml:33` — `onchange="this.form.submit()"`

Under strict CSP `script-src 'self'` / `style-src 'self'`, these are blocked. AdminFeedback Detail delete falls through to no-confirm immediate delete — security regression risk.

- **Fix:** Move inline styles to CSS files; move inline handlers to `~/js/admin-*.js` event listeners

### E. `deck-sync.ts:851 document.write(html)` after page load

Used as fallback when server returns non-zip response (error HTML). `document.write` post-DOM-load wipes the entire document, breaks back-button stack, kills CSP nonces, blocks paint.

- **Fix:** Replace with inline error rendering or `document.body.replaceChildren()` + parser

### F. `SuggestCategories.cshtml:161` + `CommanderCategories.cshtml:67` — `info-tooltip` keyboard-inaccessible

`<span class="info-tooltip" title="...">i</span>` — title attribute only shows on mouse hover; screen readers hear bare "i".

- **Fix:** Convert to `<button>` with `aria-describedby` or `<details><summary>i</summary><p>…</p></details>`

### G. `_AdminLayout.cshtml:29 + page <h1>s` — duplicate H1 across layout + pages

Layout topbar renders `<h1>@ViewData["Title"]</h1>`; AdminAnalytics/AdminFeedback/AdminFeedback Detail each ALSO render `<h1>`. Multiple H1s per page break semantic outline.

- **Fix:** Pick one source of truth (layout OR pages, not both)

### H. `Views/Deck/Home.cshtml` — missing `<h1>` element entirely

Page primary heading is `<span class="hub-hero__title">` styled large. Layout does not provide H1. Page violates "one h1 per page".

- **Fix:** Add `<h1>DeckFlow</h1>` or promote hub-hero title to h1

### I. `selected="@(condition)"` bool→string Razor bug across 5 files

Razor renders `selected="True"` when value is boolean — invalid HTML. v1.2 commit `32bf620` fixed this on ChatGPT views; pattern still wrong in:

- `Views/Deck/DeckSync.cshtml:51-54,61-62,68-70,93-94,128-129`
- `Views/Deck/DeckConvert.cshtml:32-33,38-41,45-48`
- `Views/Deck/SuggestCategories.cshtml:40-43,88-89`
- `Views/AdminHarvest/Index.cshtml:40,90`

- **Fix:** Apply `selected="@(x ? "selected" : null)"` sweep across these views

### J. `AdminFeedback/Index.cshtml:71`, `About/Index.cshtml:14` — link text issues

- AdminFeedback `<a>View</a>` — generic; SR users hear bare "View". Specific: "View feedback #@item.Id"
- About `<a>@Model.RepositoryUrl</a>` — link text IS the URL; SR users hear full URL string. Add `aria-label="Open DeckFlow source repository"`

### K. `Views/Deck/Home.cshtml:30` + `JudgeQuestions.cshtml:22,34-39,42` + `_DeckFlowBridgeHint:15` + `_MoxfieldBulkEditHint:9,12` — straight quotes in prose

Guideline: curly quotes `"` `"` and `'` not straight `"` `'`. Sweep applies.

### L. `judge-questions.ts:36-48 fetchCardDetails` — no AbortController timeout

Other modules (admin-analytics, admin-harvest) use `FETCH_TIMEOUT_MS` pattern. Judge questions card lookup hangs indefinitely if network stalls; submit button stays disabled.

- **Fix:** Add timeout

### M. `MechanicLookup.cshtml:69` — `rel="noreferrer"` missing `noopener`

External link `<a target="_blank" rel="noreferrer">` lacks `noopener`. Tab-napping risk: external page can `window.opener.location = phishing_url`.

- **Fix:** Add `noopener` (other views in same dir do it correctly)

### N. `AdminHarvest.cshtml:54` `#harvest-status-live` missing `role="status" aria-live="polite"`

AJAX poll (`admin-harvest.ts:151 render`) updates this element every 3s during runs. SR users hear nothing on state transitions — directly contradicts Phase 7 SC #1/#3 intent.

- **Fix:** Add `role="status" aria-live="polite"` to the element

---

## P2 — Guideline violations with measurable impact

### O. `:root` missing `color-scheme` across all CSS

site.css, site-common.css, admin.css, site-nyx.css, site-planeswalker-dark.css — none declare `color-scheme`. Dark themes have native scrollbar/select/input chrome rendering light, mismatching dark page bg on Windows/iOS.

- **Fix:** Add `color-scheme: light dark` to site-common.css `:root` (covers all themes) OR per-theme `color-scheme: dark` in dark forks

### P. `prefers-reduced-motion` block coverage incomplete

site.css:1373-1383 only gates `.busy-indicator__spinner` + `.hub-card`. Missing reduced-motion overrides for: `.back-to-top-button` (site.css:697), `.hub-hero` (site-common.css:204), `.skip-link` (site.css:99), `.copy-button.is-copied/is-copy-failed` (site.css:1047/1053), card hover lift `transform: translateY(-1px)`. Dark theme forks (site-nyx.css, site-planeswalker-dark.css) have NO `prefers-reduced-motion` block at all.

- **Fix:** Move global motion-reduce gate into site-common.css

### Q. No `touch-action: manipulation` anywhere

300ms tap delay on touch devices for all buttons/links across the app.

- **Fix:** Add `button, a, summary { touch-action: manipulation }` to site-common.css

### R. No `font-variant-numeric: tabular-nums` on numeric tables

Only `.admin-analytics-table .num` has it (admin.css:139). Missing on `.admin-table` (AdminFlags, AdminHarvest), `.conflicts-table` (DeckSync, CedhMetaGap, CommanderCategories), `.admin-feedback-table`. Numeric column widths jitter on AJAX update.

- **Fix:** Add utility class `.tabular { font-variant-numeric: tabular-nums }` to site-common.css; apply to numeric `<td>`s

### S. Tables missing `<caption>` + `<th scope="col">`

Affected: `AdminFlags:21-24`, `AdminFeedback Index:50-58`, `AdminHarvest:140-160,169-179`, `DeckSync:195-300`, `CommanderCategories:74-79`, `CedhMetaGap:210-258`.

- **Fix:** Add `<caption class="sr-only">` and `scope="col"` to all admin/result tables

### T. `<input type="url">` placeholders + autocomplete + inputmode

Affected: `DeckSync:100,135`, `DeckConvert:56`, `SuggestCategories:96`, `AdminHarvest:76`, `ChatGptPackets:142`. All: placeholder lacks `…`; missing `autocomplete="url"` `inputmode="url"`.

- **Fix:** Sweep apply across views

### U. User-paste `<textarea>` missing `autocomplete="off"`

Long deck-text / JSON pastes trigger password-manager modal on some browsers. Affected: all ChatGPT views, DeckSync, DeckConvert, SuggestCategories, JudgeQuestions, CedhMetaGap.

- **Fix:** Sweep apply `autocomplete="off"`

### V. SVG icon `role="img"` + `aria-hidden="true"` conflict

`ChatGptPackets.cshtml:310-319,332-340` — card-picker `<svg>` has both `role="img"` and `aria-hidden="true"`. Contradictory.

- **Fix:** Drop `role="img"` since icon is decorative inside aria-labeled button

### W. `data-busy-progress` strings + `_BusyIndicator` copy missing `…`

`_BusyIndicator.cshtml:5-6` "Working" / "Request in progress." → "Working…" / "Request in progress…"
`SuggestCategories:34` + `CommanderCategories:30` data-busy-progress pipe-separated strings lack `…`

### X. `_DeckToolTabs.cshtml:14,24,33,43` missing `aria-controls`

Dropdown triggers have `aria-expanded` toggled by JS (site.ts:684,696) ✓ but no `aria-controls` linking trigger to dropdown panel `id`. Dropdowns lack `id` attributes.

- **Fix:** Add `aria-controls="<dropdown-id>"` + `id="<dropdown-id>"`

### Y. `_DeckToolTabs.cshtml:53` muted "AI Suggestions offline" `<span>` missing `aria-disabled="true"`

Visually muted but no programmatic disabled state for SR users.

### Z. `_Layout.cshtml:63` guild theme `<option>` labels not `translate="no"`

Brand names (Azorius, Dimir, Rakdos, …) get auto-translated by Chrome on non-English locales, garbling them. Apply `translate="no"` to all 24 options OR to the `<select>`.

### AA. `Views/Help/Topic.cshtml` missing `<h1>` fallback

Depends on Markdown content starting with `# Title`. If any `.md` file lacks `#`, page has no h1.

- **Fix:** Add `<h1>@Model.Title</h1>` before `<article>` OR audit all `Help/**/*.md` files

### BB. `AdminFeedback/Detail.cshtml:39-43` Delete uses native `confirm()`

Functional but: (a) blocked under strict CSP, (b) not styled, (c) not focus-trapped. Guideline prefers "confirmation modal or undo window".

- **Fix:** Implement proper modal (or accept native confirm as admin-only convenience)

### CC. `AdminHarvest:22-24` + `AdminAnalytics:6-8` — `<noscript><meta http-equiv="refresh">` inside `<body>`

Invalid HTML (meta must be in `<head>`). Browsers tolerate but spec-wrong.

### DD. `_FormError.cshtml:7` `data-@Model` injects Model as attribute name

All callers pass literal strings today ✓ but no sanitization. Defense-in-depth: regex-strip `[^a-z0-9-]` before injection.

### EE. `_BusyIndicator.cshtml:7` progress div missing `role="progressbar"` / `aria-valuenow`

If it reflects progress, add ARIA; if purely decorative spinner, add `aria-hidden="true"`.

### FF. `_BracketCallout.cshtml` is dead-code partial

NOT called via PartialAsync per its own L1-12 comment. Either delete file OR wire functionally (accept child slot).

### GG. `Feedback/Index.cshtml:23,29,35` — `<span asp-validation-for>` no `aria-live="polite"`

If JS injects errors (form has `novalidate`), SR users don't hear them.

---

## P3 — Polish / nits

- Straight quotes in prose across `_DeckFlowBridgeHint`, `_MoxfieldBulkEditHint`, `Home`, `JudgeQuestions`, `ChatGptPackets:186`
- "20 seconds", "10 MB" etc. lack non-breaking space (`&nbsp;`) per typography rule
- Hardcoded date format `.ToString("u")` / `"yyyy-MM-dd HH:mm UTC"` in `AdminHarvest:118-119,153,184`; wrap in `<time datetime="@iso">` element
- `admin-harvest.ts:41` `toISOString().replace('T', ' ').replace('.000Z', ' UTC')` — use `Intl.DateTimeFormat` OR `<time>` element
- `<pipe>` separators in `AdminHarvest:58-66` are SR-read literally; wrap `<span aria-hidden="true">|</span>`
- "N/A" → "—" em-dash for consistency (`AdminHarvest:117`, `AdminFeedback Detail:14-18`)
- `AboutIndex.cshtml:12` Model.Version → wrap with `translate="no"` (semver token)
- `site-mobile.css` no `(pointer: coarse)` media queries; touch styling axis-confused with width axis
- `site-mobile.css` no `env(safe-area-inset-*)` for iOS notch
- `site-mobile.css:29` `.back-to-top-button { display: none }` at mobile removes feature entirely; consider smaller variant
- 22 guild theme stylesheets are full forks per CLAUDE.md — every CSS gap (color-scheme, prefers-reduced-motion, touch-action, tabular-nums) duplicated ×22. Strategy: put cross-cutting a11y rules in `site-common.css` (loaded alongside theme) to avoid 22× sweep.

---

## Suggested sweep PRs

These group findings into shippable single-PR sweeps (smallest leverage first):

### Sweep 1: "site-common.css cross-cutting a11y" — 1 file, app-wide impact
- Add `color-scheme: light dark` to `:root`
- Add global `@media (prefers-reduced-motion: reduce)` block
- Add `button, a, summary { touch-action: manipulation }`
- Add `.tabular { font-variant-numeric: tabular-nums }` utility
- Add `h1, h2, h3, [id] { scroll-margin-top: 4rem }`
- Covers: O, P, Q, R (utility), and sticky-anchor jumps

### Sweep 2: "admin.css focus-visible block" — 1 file, admin shell a11y
- Mirror `site.css:109-118` universal `:focus-visible` rule into admin.css
- Add `color-scheme: dark` to admin.css `:root`
- Add `tabular-nums` to `.admin-table td:not(.route)`
- Covers: A, O (admin), R (admin tables)

### Sweep 3: "Razor `selected=True` bug sweep" — 4 views
- Apply `selected="@(x ? "selected" : null)"` pattern to DeckSync, DeckConvert, SuggestCategories, AdminHarvest
- Covers: I

### Sweep 4: "Inline handler removal (CSP hardening)" — 3 files
- Move `Error.cshtml` inline styles to error.css
- Move `AdminFeedback/Detail.cshtml` `onsubmit` to admin-feedback-detail.js
- Move `AdminFeedback/Index.cshtml` `onchange` to admin-feedback.js
- Covers: D

### Sweep 5: "df-typeahead.ts keyboard navigation + ARIA combobox" — 1 file, 5 views benefit
- Add ArrowDown/Up/Enter/Escape handlers
- Add ARIA combobox attributes
- Add `role="option"` to suggestion buttons
- Covers: B

### Sweep 6: "Table semantics sweep" — 6 tables across admin + deck views
- Add `<caption class="sr-only">` and `<th scope="col">` to AdminFlags, AdminFeedback Index, AdminHarvest, DeckSync, CommanderCategories, CedhMetaGap tables
- Covers: S

### Sweep 7: "URL input + textarea autocomplete sweep" — 5+ views
- Add `autocomplete="url" inputmode="url"` to all `<input type="url">`
- Add `autocomplete="off"` to all user-paste `<textarea>`
- Append `…` to all url placeholders
- Covers: T, U

### Sweep 8: "Info-tooltip a11y" — 2 views
- Convert `<span class="info-tooltip" title="…">i</span>` to button-or-details pattern in SuggestCategories + CommanderCategories
- Covers: F

### Sweep 9: "ARIA tablist server-render" — 1 partial
- Pre-select correct tab in `_WorkflowStepTabs.cshtml`
- Covers: C

### Sweep 10: "Misc P1 fixes"
- `deck-sync.ts:851 document.write` removal — E
- `_DeckToolTabs.cshtml aria-controls` — X
- `_AdminLayout` h1 source-of-truth — G
- `Home.cshtml` h1 — H
- `MechanicLookup` `rel="noopener"` — M
- `AdminHarvest` `role="status"` on live region — N
- `judge-questions.ts` fetch timeout — L

---

## Notes for triage

- All findings are documentation-grade — backed by explicit `file:line` references from the audit transcripts.
- No findings invalidate v1.2 functional requirements; this is hardening/polish work.
- Highest impact-to-effort: **Sweep 1** (one CSS file, app-wide), **Sweep 2** (admin a11y fix, one file), **Sweep 3** (Razor sweep, mechanical fix matching v1.2 commit `32bf620` pattern).
- **CLAUDE.md constraint:** layout CSS must go in `site-common.css`, not `site.css`. Cross-cutting a11y rules in Sweep 1 align with this rule.
- **Theme proliferation:** putting Sweep 1 changes in `site-common.css` avoids 22× fork edit; putting them in `site.css` requires duplicating into every guild theme.

## Coverage gaps for follow-up

- 20 guild theme stylesheets not reviewed (only site-nyx + site-planeswalker-dark spot-checked).
- 5 large TS modules (deck-sync, df-select, site, card-lookup, category-suggestions) sampled by grep — full code review pending.
- No browser-based testing performed (focus order, screen reader playback, mobile touch behavior). Per CLAUDE.md feedback memory: user starts dev server, so live UX testing deferred to user/explicit /webapp-testing run.
