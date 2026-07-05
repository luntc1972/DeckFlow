---
phase: 84-theme-semantic-token-migration
plan: 01
subsystem: ui
tags: [css, custom-properties, design-tokens, theming, accessibility]

# Dependency graph
requires:
  - phase: 82-refactor-review-sweep-ui-baseline-audit
    provides: "Color pillar gap (2/4) identified in the UI baseline audit, assigned to Phase 84"
provides:
  - "--link/--focus/--cta-border re-pointed to var(--accent-strong) in site.css + 11 forks"
  - "site-commander-table.css :root gained the missing 4-token semantic block"
  - "19 genuine link/focus/cta-border affordance sites swapped from raw --accent-strong onto the correct semantic token, with defensive accent-strong fallback preserved"
  - "Exhaustive, counted decorative classification (37/3/2 residual primary --accent-strong consumers) documented and left unchanged"
  - "Pre-migration computed-style baseline artifact for Plan 84-02's no-drift diff"
affects: [84-02-theme-semantic-token-migration, 86-ui-audit-rescore]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Semantic CSS custom-property alias re-pointing: change the alias definition (--link/--focus/--cta-border -> var(--accent-strong)) before touching call sites, so the majority of already-correct call sites stay byte-identical and only a small, enumerated set of already-migrated sites absorb the intentional shade shift (D1)."
    - "Defensive var() fallback chain at swap sites: var(--<semantic-token>, var(--accent-strong, <original-tail>)) keeps --accent-strong as the safety-net fallback for any theme fork missing the new token."

key-files:
  created:
    - .planning/phases/84-theme-semantic-token-migration/theme-baseline-pre84.json
  modified:
    - DeckFlow.Web/wwwroot/css/site.css
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web/wwwroot/css/site-mobile.css
    - DeckFlow.Web/wwwroot/css/site-theme-overrides.css
    - DeckFlow.Web/wwwroot/css/site-commander-table.css
    - DeckFlow.Web/wwwroot/css/site-abzan.css
    - DeckFlow.Web/wwwroot/css/site-bant.css
    - DeckFlow.Web/wwwroot/css/site-esper.css
    - DeckFlow.Web/wwwroot/css/site-grixis.css
    - DeckFlow.Web/wwwroot/css/site-jeskai.css
    - DeckFlow.Web/wwwroot/css/site-jund.css
    - DeckFlow.Web/wwwroot/css/site-mardu.css
    - DeckFlow.Web/wwwroot/css/site-naya.css
    - DeckFlow.Web/wwwroot/css/site-nyx.css
    - DeckFlow.Web/wwwroot/css/site-planeswalker-dark.css
    - DeckFlow.Web/wwwroot/css/site-sultai.css

key-decisions:
  - "D1 (from 84-CONTEXT.md) executed as specified: re-pointed --link/--focus/--cta-border to var(--accent-strong) rather than leaving them at var(--accent), so the ~30 real-role call sites are byte-identical and only the ~8 already-migrated sites shift shade."
  - "D2 executed: 37 site-common.css + 3 site-mobile.css + 2 site-theme-overrides.css decorative --accent-strong consumers left unchanged and explicitly commented at 4 representative sites (bracket-badge--b3, feedback-submit, page-footer__link--cta, active-tab)."
  - "site-theme-overrides.css:27 (.maintenance-page__action border-color) required NO change — it already read var(--cta-border) pre-migration; only :13 needed the --focus wrap."

requirements-completed: [THEME-01]

# Metrics
duration: 50min
completed: 2026-07-04
---

# Phase 84 Plan 01: Semantic-Token Alias Re-Point + Affordance Swap Summary

**Re-pointed --link/--focus/--cta-border to var(--accent-strong) across site.css + 11 forks, added the missing token block to site-commander-table.css, and swapped exactly 19 genuine link/focus/cta-border affordance sites onto the semantic tokens — leaving 37+3+2 decorative --accent-strong consumers correctly unchanged.**

## Performance

- **Duration:** ~50 min
- **Started:** 2026-07-05T02:15:00Z (approx, per STATE.md session start)
- **Completed:** 2026-07-05T02:27:36Z
- **Tasks:** 3 (baseline capture, alias re-point, affordance swap)
- **Files modified:** 16 (1 created + 15 CSS edited)

## Accomplishments

- Captured a pre-migration computed-style baseline (`theme-baseline-pre84.json`) across all 24 `themeFiles` x {light, dark}, committed BEFORE any CSS edit, for Plan 84-02's no-drift diff.
- Re-pointed the three brand-emphasis alias tokens (`--link`, `--cta-border`, `--focus`) from `var(--accent)` to `var(--accent-strong)` in `site.css` and its 11 duplicating forks, leaving `--danger` untouched — the actual structural fix (danger no longer implicitly tracks the same value family as the accent aliases).
- Added the missing 4-token semantic block to `site-commander-table.css` (the one fork with no `@import` and no prior tokens), preventing an unset-custom-property regression once its shared call sites resolve `var(--link)`/`var(--focus)`/`var(--cta-border)`.
- Swapped exactly 19 genuine link/focus/cta-border affordance sites across `site-common.css` (16), `site-mobile.css` (2), and `site-theme-overrides.css` (1) onto the correct semantic token, using the defensive `var(--<token>, var(--accent-strong, <tail>))` form so `--accent-strong` remains a fallback for any fork lacking the token.
- Left the exhaustive decorative bucket unchanged: 37 residual primary `var(--accent-strong` consumers in `site-common.css`, 3 in `site-mobile.css`, 2 in `site-theme-overrides.css` — verified via the plan's exact `grep -c`/`grep -oP` count gates, all passing.
- `dotnet build DeckFlow.sln` clean after every task; all 6 pre-existing `theming.spec.ts` Playwright tests still pass (no regression).

## Task Commits

Each task was committed atomically:

1. **Task 0: Capture pre-migration computed-style baseline** - `74d3ca14` (feat) — JSON artifact only, no CSS files in this commit (verified via `git show --stat`).
2. **Task 1: Re-point alias definitions + add commander-table token block** - `3e66493f` (feat)
3. **Task 2: Swap 19 affordance sites onto semantic tokens** - `d12c6a5e` (feat)

_No test-only (TDD) commits — this plan is a pure CSS-value migration with automated grep/build gates, not unit-testable C# behavior._

## Files Created/Modified

- `.planning/phases/84-theme-semantic-token-migration/theme-baseline-pre84.json` - Pre-migration computed-style snapshot (35 probes x 24 themes x 2 color schemes), captured via a one-off Node + `playwright-core` script driving the headless `scripts/run-web-test.sh` server (not a tracked dependency).
- `DeckFlow.Web/wwwroot/css/site.css` - `--link`/`--cta-border`/`--focus` re-pointed to `var(--accent-strong)`; `--danger` unchanged.
- `DeckFlow.Web/wwwroot/css/site-commander-table.css` - Added net-new 4-token semantic block to `:root`; its 2 pre-existing decorative `--accent-strong` consumers (now at lines 310/1043) untouched.
- `DeckFlow.Web/wwwroot/css/site-common.css` - 16 of the 19 swap sites (7 link + 6 focus + 3 cta-border); 37 residual decorative consumers unchanged; 4 sites tagged with a "Phase 84: decorative brand emphasis... (D2)" comment.
- `DeckFlow.Web/wwwroot/css/site-mobile.css` - 2 focus swap sites (`.hub-hero--primary` hover/focus border + paired box-shadow ring); 3 residual decorative consumers unchanged.
- `DeckFlow.Web/wwwroot/css/site-theme-overrides.css` - 1 focus swap site (`.hub-card--primary` hover/focus border); 2 residual decorative consumers unchanged; confirmed `:27` needed no change (already `var(--cta-border)` pre-migration).
- `DeckFlow.Web/wwwroot/css/site-{abzan,bant,esper,grixis,jeskai,jund,mardu,naya,nyx,planeswalker-dark,sultai}.css` - Identical 3-line alias re-point applied to each duplicating fork's `:root`; `--danger` line unchanged in every fork (confirmed via `git diff`).

## The 19 Swapped Affordance Sites

**LINK role -> `var(--link, var(--accent-strong, ...))`  [7 sites, site-common.css]:**
1. `:576` `.kb-chip--followed` (shared rule with `.kb-chip--pinned`)
2. `:620` `.kb-clip-origin--followed` (shared rule with `.kb-clip-origin--pinned`)
3. `:1266` `.about-page a`
4. `:1300` `.help-index__link`
5. `:1318` `.help-breadcrumb a`
6. `:1330` `.kb-back-link`
7. `:1356` `.help-prose a`

**FOCUS role -> `var(--focus, var(--accent-strong, ...))`  [9 sites]:**
8. site-common.css `:375` `.hub-card:hover,.hub-card:focus-visible` border
9. site-common.css `:694` `.hub-hero:hover,.hub-hero:focus-visible` border
10. site-common.css `:730` `.hub-card--primary:hover,.hub-card--primary:focus-visible` border
11. site-common.css `:2035` `details.info-tooltip > summary:focus-visible` outline
12. site-common.css `:2079` `details.chatgpt-helper-panel > summary:focus-visible` outline
13. site-common.css `:2481` `.manabase-pill > input:focus-visible + span` outline
14. site-mobile.css `:228` `.hub-hero--primary:hover,.hub-hero--primary:focus-visible` border
15. site-mobile.css `:229` same rule's paired `box-shadow` focus ring
16. site-theme-overrides.css `:13` `.hub-card--primary:hover,.hub-card--primary:focus-visible` border

**CTA-BORDER role -> `var(--cta-border, var(--accent-strong, ...))`  [3 sites, site-common.css]:**
17. `:1222` `table[data-chatgpt-cedh-reference-table] tr:has(...:checked)` border-color
18. `:1223` same rule's paired `box-shadow` ring
19. `:1946` `.ai-selector__option:checked + .ai-selector__option-label` border-color

## Exhaustive Decorative List (unchanged, stays on `--accent-strong`)

**site-common.css (37 residual):** 78 (scrollbar-thumb hover), 166 (`.page-brand`), 262 (back-to-top locked BG !important), 495 (`.kb-expert-accordion__summary` text), 797 (`.chatgpt-layout-segment.is-active` text — *now D2-commented*), 940 (`.maintenance-page__action:focus-visible` BG), 1013 (`.page-footer__link--cta` text — *now D2-commented*), 1019 (its hover/focus BG), 1236, 1283, 1348 (decorative `h1` heading accents), 1414 (`.feedback-submit` BG — *now D2-commented*), 1514 (`.mechanic-row:hover`), 1579 (`.copy-button--icon:hover`), 1886, 1896 (`.bracket-callout`), 1949 (`.ai-selector` selected TEXT — note: this rule's `border-color` at line 1946 WAS swapped; only its `color` stays decorative), 2155 (`.primer-section__help summary`), 2684, 2716, 2777, 2788, 2800 (manabase emphasis coloring), 2918, 2919, 2966 (— *now D2-commented*), 2967 (`.bracket-badge`/`--b3`), 3114, 3131, 3132 (chatgpt score visualization), 3159, 3181 (interaction-audit headings), 3217, 3262 (interaction-audit labels), 3376, 3402 (wincon-map headings).
**site-mobile.css (3 residual):** 88, 220, 368 (mobile mirrors of desktop decorative accents).
**site-theme-overrides.css (2 residual):** 28, 33 (`.maintenance-page__action` decorative text/hover-BG; its border-color at `:27` was already `var(--cta-border)` pre-migration, untouched).
**site.css (3, out of Task 2 scope, unswapped by design — line numbers shifted +1 by Task 1's comment edit):** `:323` `.chatgpt-step-tab.is-active` text, `:688` `.back-to-top-button` base fill, `:1084` `.run-button:hover,.copy-button:hover` BG darken.
**site-commander-table.css (2, unswapped by design — line numbers shifted +9 by Task 1's new token block):** `:310`, `:1043` decorative accents.

## The ~8 Already-Migrated D1-Shift Sites (Plan 84-02 visual proof targets)

These sites already used the semantic alias tokens BEFORE this plan (i.e., they resolved through `var(--accent)` pre-migration) and are now the ONLY sites expected to shift shade, since the alias now resolves through `var(--accent-strong)` instead:

1. `site.css` global `:focus-visible` outline (`a:focus-visible, button:focus-visible, ...` + `.skip-link:focus`) — `outline: 2px solid var(--focus);`
2. `site.css` `.cache-pill__reset` — `color: var(--link);`
3. `site.css` `.judge-howto > summary` — `color: var(--link);`
4. `site.css` `.deckflow-bridge-hint > summary` — `color: var(--link);`
5. `site.css` `.moxfield-bulkedit-hint > summary` — `color: var(--link);`
6. `site.css` `.run-button, .copy-button` — `border: 1px solid var(--cta-border);`
7. `site-common.css:116` generic focus-visible outline — `outline: 2px solid var(--focus, var(--accent));`
8. `site-common.css:1957` generic focus-visible outline — `outline: 2px solid var(--focus, var(--accent));`

All 8 are captured in `theme-baseline-pre84.json` under the `d1_*` probe IDs, across all 24 themes x {light, dark}, so Plan 84-02 can diff post-migration renders against this pre-migration baseline and confirm these are the ONLY deltas.

## Baseline Artifact

`.planning/phases/84-theme-semantic-token-migration/theme-baseline-pre84.json` — committed in `74d3ca14`, before any CSS edit. Contains 35 probe definitions (8 D1-shift + 19 swap + 8 decorative) x 24 `themeFiles` x {light, dark} = 1,680 recorded computed-style values, plus per-theme raw token cross-reference (`--accent`, `--accent-strong`, `--link`, `--danger`, `--focus`, `--cta-border`). Values were resolved by assigning each selector's exact pre-migration `var()` declaration expression to a synthetic probe element on a live themed page and reading `getComputedStyle` — equivalent to real selector resolution for these simple `var()`-fallback declarations (no intermediate rule between `:root` and these components redefines the tokens involved), and robust to the fact that the 19 swap sites live on many different, unrelated routes.

## D3 Handoff Note (Typography deferred to Phase 86)

Per 84-CONTEXT.md D3, the Typography/font-size migration (`tasks/UI-REVIEW.md`'s "migrate ~24 remaining literal `font-size:` values onto `var(--fs-*)`") is explicitly OUT of Phase 84 scope. This plan touched zero `font-size` literals (verified via the Task 2 gate: `git diff | grep -E '^[+-].*font-size'` returns empty). Phase 86 (UI Audit Re-Score & Studio Stage 4 Closeout) owns closing this Typography gap; it is not silently dropped, just not this phase's responsibility.

## Decisions Made

- Followed D1/D2/D4 from `84-CONTEXT.md` exactly as locked — no new interpretation needed.
- Where a single CSS rule combines a swap-target selector with a non-target selector sharing one declaration (`.kb-chip--pinned, .kb-chip--followed { color: ... }` and `.kb-clip-origin--pinned, .kb-clip-origin--followed { color: ... }`), the shared declaration was swapped as a unit rather than splitting the rule — splitting would have added new selector lines, violating the "no selector-line changes" acceptance gate, and the plan's own line-numbered target list did not flag these as requiring a split.
- Added the Phase 84 D2 decorative-annotation comment to `site-common.css` at 4 of the plan's named "at least" targets (`.bracket-badge--b3`, `.feedback-submit`, `.page-footer__link--cta`, `.chatgpt-layout-segment.is-active`); the 5th named example, `site.css`'s `.chatgpt-step-tab.is-active`, was left uncommented because `site.css` is outside Task 2's file scope (already closed out by Task 1) and CLAUDE.md's changed-lines format discipline discourages touching an already-closed file for a comment-only addition.

## Deviations from Plan

None - plan executed exactly as written. All acceptance-criteria automated gates (`BASELINE_CAPTURED`, `RAKDOS_UNTOUCHED`, `PRIMARY_VS_FALLBACK_COUNTS_OK`) passed on first attempt per task; no auto-fixes were needed.

## Issues Encountered

- The baseline-capture script's `page.goto('/deck-analysis')` initially failed with "Cannot navigate to invalid URL" because a relative path was passed without a Playwright-configured `baseURL`; fixed by using the full `${baseUrl}/deck-analysis` URL. This was a scratch-script authoring issue, not a plan/gate deviation, and was fixed before the Task 0 commit (no re-commit needed).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 84-02 can now run its no-drift diff against `theme-baseline-pre84.json`, verifying that ONLY the 8 documented D1-shift sites changed shade across all 24 themes x light/dark, and that THEME-02 (danger != link per theme, especially red guilds) holds live.
- `--danger` remains untouched (`#c53030`) everywhere; the structural decoupling from `--accent-strong`/`--link` that THEME-02 requires is now in place at the token-definition level (site-rakdos.css's `--link:#ff9ea4` override is likewise untouched).
- No blockers. Build clean; existing `theming.spec.ts` suite green (6/6).

---
*Phase: 84-theme-semantic-token-migration*
*Completed: 2026-07-04*

## Self-Check: PASSED

All claimed files exist (`theme-baseline-pre84.json`, `site.css`, `site-commander-table.css`,
`site-common.css`, `site-mobile.css`, `site-theme-overrides.css`) and all three task commit
hashes (`74d3ca14`, `3e66493f`, `d12c6a5e`) are present in `git log --oneline --all`.
