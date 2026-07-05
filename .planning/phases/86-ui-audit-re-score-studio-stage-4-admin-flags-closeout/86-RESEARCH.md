# Phase 86 RESEARCH — Theme UI Gaps (A/B/C/D)

**Researched:** 2026-07-05 via 3 read-only audit agents + Playwright ground-truth screenshots.
Scope: theme UI gap fixes A+B+C+D + visual-regression/interaction tests + 6-pillar re-score.
DEFERRED (not this pass): Studio DirectPush Stage 4; Admin/Flags on/off sorting (ADMIN-01).

## Architecture constraint (drives task sizing)
Theme load order (`Views/Shared/_Layout.cshtml:73-76`): `site-common.css` (global) → selected theme →
`site-theme-overrides.css` (global) → `site-mobile.css` (global).
- **11 `@import` themes** (inherit base): azorius, boros, dimir, golgari, gruul, izzet, orzhov, rakdos,
  selesnya, simic, temur. izzet = barest (token-only, 39 lines); rakdos = most complete (template).
- **13 STANDALONE full forks** each duplicate every base rule: site.css (Classic), abzan, bant, esper,
  grixis, jeskai, jund, mardu, naya, nyx, planeswalker-dark, sultai, commander-table.
- **EFFORT MULTIPLIER:** any base-rule change in site.css MUST be MIRRORED into the 13 standalone forks; the
  11 @import themes pick it up free. Byte-identical is NOT a goal here (intentional visual change).

## Confirmed bugs

### A — Active step-tab weak / dark-theme active TEXT fails WCAG (Tier 1)
- Base `site.css:311-325`: `.prompt-step-tab.is-active` reuses the SAME `background: var(--panel-soft-bg)` as
  inactive; only border/text/weight differ → low-salience active state.
- Playwright ground-truth (/deck-analysis): azorius + gruul readable & active distinguishable; **jund active
  text ~2.2:1 (rust on brown), dimir ~2.7:1 (blue on navy)** — fail.
- Contrast sweep FAILING (active-tab text): jund 2.19, dimir 2.67, sultai 3.00, golgari 3.49,
  planeswalker-dark 4.04; marginal grixis 4.64, nyx 5.13.
- 10 @import themes lack own `.prompt-step-tab.is-active` (fall to weak base): azorius, boros, dimir, golgari,
  gruul, izzet, orzhov, selesnya, simic, temur. rakdos:168 already a filled pill.
- FIX (chosen): filled-accent-pill — `background: var(--accent)` + `var(--accent-contrast,#fff)`. Template at
  `site-mobile.css:353`. Apply in base + mirror 13 forks; verify each `--accent` ≥4.5:1 with text.

### B — Hardcoded Jeskai-blue literals leak wrong accent into ALL 24 themes (Tier 1)
Three non-token `rgba(43,108,176,…)`:
1. `site-common.css:792` & `:797` — `.prompt-layout-segment:hover` / `.is-active` (global; zero theme
   overrides → leaks everywhere). TOP.
2. `site.css:308` — `.prompt-layout-picker [data-prompt-ui-mode-button].is-active { background }`.
3. `site.css:623` — `.clear-cache-button:hover { background }`.
FIX: replace with `--accent`-derived token/tint. Mirror #2/#3 into the 13 forks where duplicated.

### C — Analysis-questions checklist "stray grey pill" (Tier 2)
- `Views/Deck/DeckAnalysis.cshtml:306`: empty `<button class="prompt-question-bucket__toggle">` — no text,
  no aria-label.
- `site.css:533-551` styles it as a bordered empty box (1px `--line`, radius, tiny pad) whose only content is
  a `::after` ▶/▼ caret in faint `--muted` `--fs-xs` → reads as an orphan grey pill.
- FIX: plain chevron (drop border), raise caret contrast/size, add `aria-label`. Mirror into 13 forks.

### D — Layout picker Full/Compact/Advanced "does nothing" (Tier 2)
- Wiring intact (TS `deck-sync.ts:1256-1264,1687-1698` sets body/form `data-prompt-ui-mode` + button
  `.is-active`); CSS DOES respond (`site.css:388-434` hides secondary text in focused/expert).
- Root cause: effect imperceptible — little secondary text on the empty Step-1 landing; `guided`/Full is a
  do-nothing default; `.prompt-global-note` targeted but absent in view.
- FIX (UX, no JS): make modes visibly distinct (collapse whole panels / switch column layout); give Full a
  positive style.

## Process gap (why 256 e2e stayed green)
e2e assert DOM/selectors exist — NOT visual state (which tab is active, contrast) nor interaction OUTCOMES
(does clicking Compact change layout). This is the root reason A–D shipped green.

## Validation Architecture
Requirement → validation mapping for this phase (feeds VALIDATION.md / Nyquist Dimension 8):
- **UIAUDIT-02 (active-tab visibility, A):** visual-regression e2e — for representative themes {≥1 light
  @import, jund + dimir (failing darks), Classic}, assert `.prompt-step-tab.is-active` computed
  `background-color` ≠ inactive tab AND ≠ `var(--panel-soft-bg)` (proves filled pill), across desktop+mobile.
  Plus a WCAG check that active-tab text meets ≥4.5:1 for the 5 previously-failing themes.
- **UIAUDIT-02 (accent-leak, B):** assert no rendered `.prompt-layout-segment.is-active` /
  `[data-prompt-ui-mode-button].is-active` / `.clear-cache-button:hover` uses the literal
  `rgb(43,108,176)`-family on a non-Jeskai theme; grep-clean the 3 literals from CSS.
- **UIAUDIT-03 (checklist affordance, C):** the bucket toggle has an `aria-label`; the control is a chevron,
  not a bordered pill (assert no standalone border box / presence of accessible name).
- **UIAUDIT-03 (layout modes, D):** interaction e2e — toggling Compact/Advanced changes a measurable layout
  property (element hidden/shown or container layout differs) vs guided.
- **Phase acceptance:** full build 0/0; full xUnit green; full Playwright e2e green (incl new specs); 6-pillar
  UI re-score `tasks/UI-REVIEW.md` ≥20/24.

## Patterns to reuse
- Theme-loop + `deckflow-theme` cookie e2e: `DeckFlow.Web/e2e/bracket-smoke.spec.ts` (cookie values
  `site-<theme>.css`), `print-button-appearance.spec.ts` (per-theme addCookies). Copy for visual-regression.
- Filled-pill active state: `site-mobile.css:353`. Existing @import filled pill: `site-rakdos.css:168`.
- playwright.config.ts webServer `--launch-profile http-no-browser` (WSL-safe, no Windows browser).

## Open risks
- 13-fork mirror fan-out: high edit count; risk of drift between forks — plan should batch by fork group and
  verify each with a per-theme render check, not just base.
- `--accent-contrast` may not exist in all themes; some `--accent` values may still fail ≥4.5:1 with white →
  per-theme contrast verification is mandatory, not assumed.
- Changed-lines format gate: mirroring must touch only the changed rule lines in each fork.
