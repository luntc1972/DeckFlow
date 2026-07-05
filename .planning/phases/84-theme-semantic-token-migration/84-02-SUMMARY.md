---
phase: 84-theme-semantic-token-migration
plan: 02
subsystem: ui
tags: [css, custom-properties, playwright, e2e, theming, accessibility]

# Dependency graph
requires:
  - phase: 84-theme-semantic-token-migration (Plan 01)
    provides: "--link/--focus/--cta-border re-pointed to var(--accent-strong); 19 affordance swaps; theme-baseline-pre84.json"
provides:
  - "Permanent theming.spec.ts regression guard: computed --danger != computed --link in every theme"
  - "Permanent theming.spec.ts regression guard: --link/--focus/--cta-border resolve to a real color in every theme"
  - "UI-VS-* workaround audit (rakdos UI-VS-02 override dispositioned KEEP)"
  - "All-24-themes x {light,dark} post-migration computed-style snapshot (theme-snapshot-post84.json) diffed against the committed pre-migration baseline"
  - "Red-guild (8 themes) desktop+mobile screenshot evidence of danger-vs-link visual distinction"
  - "D1-shift before/after evidence (Classic + Nyx)"
affects: [86-ui-audit-rescore]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Computed-custom-property regression guard: getComputedStyle(document.documentElement).getPropertyValue('--token') resolves nested var() chains to a final color (verified empirically against site-commander-table.css's `--link: var(--accent-strong)`), so a plain string-inequality assertion is a valid, cheap, permanent structural guard — no perceptual/Delta-E library needed."
    - "Scratch-script no-drift diff: one-off Node + playwright-core script (not a tracked dependency) re-resolves the exact same 35 probe declarations as the committed baseline against the live, migrated CSS tree, then a second script does an ID-keyed diff, bucketed by category (d1_shift/swap/decorative), to produce a machine-checked THEME-03 proof instead of a manual visual-only spot check."

key-files:
  created:
    - .planning/phases/84-theme-semantic-token-migration/theme-snapshot-post84.json
    - .planning/phases/84-theme-semantic-token-migration/evidence/redguild-*-desktop.png (8 files)
    - .planning/phases/84-theme-semantic-token-migration/evidence/redguild-*-mobile.png (8 files)
    - .planning/phases/84-theme-semantic-token-migration/evidence/red-guild-token-report.json
    - .planning/phases/84-theme-semantic-token-migration/evidence/d1-shift-classic-light.png
    - .planning/phases/84-theme-semantic-token-migration/evidence/d1-shift-nyx-dark.png
  modified:
    - DeckFlow.Web/e2e/theming.spec.ts

key-decisions:
  - "rakdos's site-rakdos.css UI-VS-02 `--link:#ff9ea4` override dispositioned KEEP, not redundant — it is the ONLY reason rakdos's 7 LINK-role swap sites (about-page a, help-index__link, etc.) now correctly render pink instead of crimson; removing it would collapse rakdos's link color back onto --accent-strong."
  - "IMPORTANT FINDING for Task 3 sign-off: the no-drift diff is genuinely 8-D1-sites-only in 23 of 24 themes, but rakdos additionally shows 7 LINK-role swap-category sites changing color (crimson accent-strong -> pink #ff9ea4). This is judged a CORRECT, intended THEME-01 outcome (those sites are real link affordances that were incorrectly on raw --accent-strong pre-migration; the swap now correctly routes them through --link, and rakdos's pre-existing --link override is what makes the color visibly change there) — not a defect — but it means the literal plan wording 'all 19 swap sites resolve to the SAME color as baseline in every theme' does not hold for rakdos specifically. Flagged prominently for human review rather than silently absorbed."

requirements-completed: [THEME-02, THEME-03]  # Task 3 checkpoint:human-verify APPROVED by developer 2026-07-04 ("Approved"). Rakdos crimson→pink link-affordance delta accepted as correct/intended THEME-01 behavior.

# Metrics
duration: 55min
completed: 2026-07-04
---

# Phase 84 Plan 02: Danger/Link Regression Guard + No-Drift Evidence Summary (Tasks 1-2 of 3 — Task 3 checkpoint pending)

**Extended theming.spec.ts with a permanent danger!=link structural guard across all 24 themes, then produced a full 24-theme x {light,dark} computed-style no-drift diff plus red-guild screenshot evidence — surfacing one genuine, intended-but-unplanned additional color delta in rakdos for human sign-off.**

## Performance

- **Duration:** ~55 min
- **Started:** 2026-07-04T20:15:00Z (approx)
- **Completed:** 2026-07-04T20:50:00Z
- **Tasks:** 2 of 3 (Task 3 is a `checkpoint:human-verify` gate, intentionally not self-approved)
- **Files modified:** 1 (theming.spec.ts) + 12 evidence/data artifacts created

## Accomplishments

- Extended `ThemeSnapshot`/`readThemeSnapshot` in `theming.spec.ts` to read `--danger`/`--link`/`--focus`/`--cta-border` computed root values, additively (existing tiers untouched, confirmed via `git diff --stat`: 59 insertions, 0 deletions).
- Added a permanent test asserting `getComputedStyle(document.documentElement).getPropertyValue('--danger') != ...getPropertyValue('--link')` for every one of the 24 `themeFiles` (not a sample) — the THEME-02 structural guard. Confirmed empirically that `getPropertyValue` on a custom property resolves nested `var()` chains to a final color (tested against `site-commander-table.css`'s `--link: var(--accent-strong)` -> resolved to `#1f5c39`, not literal `var(...)` text), so the plain string-inequality check is valid.
- Added a second permanent test asserting `--link`/`--focus`/`--cta-border` each resolve to a real (non-empty, non-auto/none/transparent) color in every theme.
- Full `theming` e2e suite: **10/10 passed** (5 test functions x 2 Playwright projects: `chromium-desktop` + `chromium-mobile`), run headless against the already-running `scripts/run-web-test.sh`-equivalent server (no Windows browser opened) — confirmed via `ps aux` before the run.
- Audited every `UI-VS-*` tagged comment in `wwwroot/css/` (14 files). Only ONE is an actual override (not just a section-header label): `site-rakdos.css:13`'s `UI-VS-02 --link:#ff9ea4`. Dispositioned **KEEP**.
- Re-captured a post-migration computed-style snapshot (`theme-snapshot-post84.json`) using the exact same 35 probe definitions (id/selector/prop/src) as the committed `theme-baseline-pre84.json`, across all 24 `themeFiles` x {light, dark} = 1,680 values, via a one-off Node + `playwright-core` scratch script (not a tracked dependency) driving the headless server.
- Diffed the two snapshots programmatically. Result: **23 of 24 themes** show exactly the predicted pattern (8/8 d1_shift probes changed, 0/19 swap probes changed, 0/8 decorative probes changed, in both light and dark). **rakdos is the one exception** — see Key Finding below.
- Captured desktop (1280x900) + mobile (390x844) screenshot evidence for all 8 red-guild themes (rakdos/boros/gruul/mardu/jund/naya/grixis/jeskai) showing a real `.feedback-error` element (`color: var(--danger)`) next to a `--link`-colored sample, proving visible distinction in every case — plus a `red-guild-token-report.json` recording each theme's live `--link`/`--danger`/`--accent-strong` hex values.
- Captured D1-shift-selector swatch screenshots (`--focus`/`--link`/`--cta-border`, post-migration resolved colors) for Classic (light) and Nyx (dark).
- Confirmed `git diff DeckFlow.Web/wwwroot/css/site-rakdos.css` is empty — override retained, `RAKDOS_OVERRIDE_RETAINED` gate passes.
- `dotnet build DeckFlow.sln` clean (0 warnings, 0 errors) after Task 1's edit.

## Task Commits

1. **Task 1: Extend theming.spec.ts with the THEME-02 danger!=link regression guard** - `33dcb129` (test)
2. **Task 2: Audit UI-VS-* workarounds and capture THEME-03 visual spot-check evidence** - see commit below (evidence + snapshot artifacts, no CSS changes)

_No test-only TDD RED/GREEN split — Task 1 is a permanent regression-guard addition to an existing green suite (not a bug-fix TDD cycle); Task 2 is pure evidence capture with zero source changes._

## Files Created/Modified

- `DeckFlow.Web/e2e/theming.spec.ts` - Extended `ThemeSnapshot` type + `readThemeSnapshot` to capture `--danger`/`--link`/`--focus`/`--cta-border`; added 2 new permanent tests (danger!=link per theme; token-resolution per theme).
- `.planning/phases/84-theme-semantic-token-migration/theme-snapshot-post84.json` - Post-migration computed-style snapshot, same 35-probe/24-theme/2-scheme shape as the committed baseline, for reproducibility and future re-diffing.
- `.planning/phases/84-theme-semantic-token-migration/evidence/redguild-{rakdos,boros,gruul,mardu,jund,naya,grixis,jeskai}-{desktop,mobile}.png` - 16 screenshots, real `.feedback-error` (--danger) text next to `--link`-colored sample text, per red-guild theme.
- `.planning/phases/84-theme-semantic-token-migration/evidence/red-guild-token-report.json` - Live `--link`/`--danger`/`--accent-strong` hex per red-guild theme.
- `.planning/phases/84-theme-semantic-token-migration/evidence/d1-shift-{classic-light,nyx-dark}.png` - `--focus`/`--link`/`--cta-border` swatch colors, post-migration, for the two representative themes RESEARCH's Pitfall 2 called out.

## UI-VS-* Audit Table

| Tag | Files | What it actually is | Disposition |
|-----|-------|----------------------|-------------|
| UI-VS-01 | 11 guild forks (abzan, bant, esper, grixis, jeskai, jund, mardu, naya, nyx, planeswalker-dark, sultai) | Section-header comment ("type scale — inherited shape...") labeling a font-size block, no color logic | N/A — not a THEME-02/03 workaround; unchanged |
| UI-VS-02 (label) | Same 11 forks + `site.css` + `site-commander-table.css` | Section-header comment labeling where `--link`/`--danger`/`--focus`/`--cta-border` are defined at `:root` | N/A — organizational label only, not an override; unchanged |
| **UI-VS-02 (override)** | **`site-rakdos.css:13`** | **`--link: #ff9ea4;`** — an actual value override, distinct from both `--accent-strong` (#a92434) and `--danger` (#c53030) | **KEEP.** Confirmed by the no-drift diff (below): this override is now the reason rakdos's real link affordances (about-page links, help-index links, kb-back-link, etc.) render in a distinct pink rather than the guild's crimson accent-strong. Reverting it would (a) violate 84-RESEARCH's explicit anti-pattern ("Reverting site-rakdos.css's existing --link override... is a correct, already-shipped, deliberate divergence, not leftover debt") and (b) regress the visual link/accent-strong distinction this override exists for. `git diff` on this file is empty — untouched. |
| UI-VS-03 | Same 11 forks | Section-header comment ("hoisted hex tokens") | N/A — organizational label only; unchanged |

No other `UI-VS-*` tags exist in the CSS tree (confirmed via `grep -rl "UI-VS-" DeckFlow.Web/wwwroot/css/` — 14 files, all accounted for above).

## danger != link Test Coverage Summary

- **Test:** `computed --danger never equals computed --link, in every theme` (`theming.spec.ts`)
- **Coverage:** all 24 `themeFiles` (includes all 8 red guilds: rakdos, boros, gruul, mardu, jund, naya, grixis, jeskai), on both `chromium-desktop` and `chromium-mobile` Playwright projects (2 x 24 = 48 per-theme assertions per run).
- **Result:** PASSED. Every theme's `--danger` (`#c53030` everywhere — confirmed unchanged and untinted) is string-distinct from its `--link` (12 forks' `--link` == their own `--accent-strong` hex post-migration; rakdos == `#ff9ea4`; commander-table == its own `--accent-strong` `#1f5c39`; the 10 cascade `@import` forks inherit their own `--accent-strong` hex).
- **Companion test:** `every theme resolves --link, --focus, and --cta-border to a real color` — PASSED for all 24 themes (catches the exact D4 gap 84-01 fixed for `site-commander-table.css`, which previously resolved these to empty strings).
- **Full suite:** 10/10 passed (`npx --no-install playwright test theming`, run against the headless server).

## No-Drift Diff Result (THEME-03)

Diffed `theme-snapshot-post84.json` (post-migration, captured this plan) against the committed `theme-baseline-pre84.json` (pre-migration, captured by 84-01 Task 0), probe-by-probe, theme-by-theme, scheme-by-scheme (24 themes x 2 schemes x 35 probes = 1,680 comparisons).

| Category | Themes with 0 unexpected changes | Themes with unexpected changes |
|----------|-----------------------------------|----------------------------------|
| d1_shift (8 probes, expected to change in EVERY theme) | 24/24 changed the expected count, EXCEPT rakdos (4/8 changed — see finding below, this is correct given rakdos's pre-existing `--link` override) | — |
| swap (19 probes, expected to be color-neutral / unchanged) | 23/24 themes: 0/19 changed | **rakdos: 7/19 changed** (see Key Finding) |
| decorative (8 probes, expected to be unchanged) | 24/24 themes: 0/8 changed | none |

**Totals:** 390 of 1,680 probe values changed. 368 of those are the expected 23-theme x 2-scheme x 8-d1-probe pattern (23 x 2 x 8 = 368). The remaining 22 are all rakdos (2 schemes x (4 d1_shift + 7 swap) = 22) — accounted for below.

### Key Finding (for Task 3 human sign-off)

**rakdos is not a byte-identical "8 D1 sites only" case — it has 11 changed probes (4 d1_shift + 7 swap), not 8.**

Root cause: rakdos's `--link` was `#ff9ea4` (its UI-VS-02 override) both BEFORE and AFTER this migration — that value itself never moved. What changed is that 84-01's affordance swap made 7 site-common.css LINK-role selectors (`.kb-chip--followed`, `.kb-clip-origin--followed`, `.about-page a`, `.help-index__link`, `.help-breadcrumb a`, `.kb-back-link`, `.help-prose a`) reference `var(--link, ...)` for the first time — pre-migration they referenced raw `var(--accent-strong, ...)` only, bypassing `--link` entirely. In the 23 other themes this swap is genuinely color-neutral because `--link` now equals `--accent-strong` there (D1's re-point). In rakdos specifically, `--link` (#ff9ea4, pink) has never equaled `--accent-strong` (#a92434, crimson) — so these 7 selectors visibly change from crimson to pink in rakdos only.

Judgment: this is the CORRECT, intended outcome of THEME-01 (these ARE genuine link affordances; they should follow `--link`, and rakdos has always intentionally distinguished its link color from its brand/error reds) — not a bug. But it is an additional, real visual delta beyond the "~8 D1 sites" framing in the plan, and the literal Task 2 acceptance wording ("all 19 swapped affordance selectors resolve to the SAME color as baseline... in every theme") does not hold for rakdos. This is surfaced here, not silently absorbed, precisely for the human to confirm at Task 3.

**Rakdos `__tokens` before -> after (both light and dark, identical — no `prefers-color-scheme` in any theme file):**
- `--link`: `#ff9ea4` -> `#ff9ea4` (unchanged, its own override)
- `--danger`: `#c53030` -> `#c53030` (unchanged, fixed everywhere)
- `--focus`: `#d13b47` (was `--accent`) -> `#a92434` (`--accent-strong`) — this IS one of the 8 D1 sites, expected
- `--cta-border`: `#d13b47` -> `#a92434` — also expected D1

## Red-Guild Screenshot Evidence (Task 2c)

All 8 red guilds, desktop (1280x900) + mobile (390x844), real `.feedback-error` element (production class, `color: var(--danger)`) next to a `--link`-colored sample:

| Theme | `--link` (post) | `--danger` (post, fixed) | Desktop | Mobile |
|-------|------------------|--------------------------|---------|--------|
| rakdos | `#ff9ea4` | `#c53030` | `evidence/redguild-rakdos-desktop.png` | `evidence/redguild-rakdos-mobile.png` |
| boros | `#8d2a15` | `#c53030` | `evidence/redguild-boros-desktop.png` | `evidence/redguild-boros-mobile.png` |
| gruul | `#812817` | `#c53030` | `evidence/redguild-gruul-desktop.png` | `evidence/redguild-gruul-mobile.png` |
| mardu | `#8f3729` | `#c53030` | `evidence/redguild-mardu-desktop.png` | `evidence/redguild-mardu-mobile.png` |
| jund | `#a6613f` | `#c53030` | `evidence/redguild-jund-desktop.png` | `evidence/redguild-jund-mobile.png` |
| naya | `#91482e` | `#c53030` | `evidence/redguild-naya-desktop.png` | `evidence/redguild-naya-mobile.png` |
| grixis | `#d16670` | `#c53030` | `evidence/redguild-grixis-desktop.png` | `evidence/redguild-grixis-mobile.png` |
| jeskai | `#922b23` | `#c53030` | `evidence/redguild-jeskai-desktop.png` | `evidence/redguild-jeskai-mobile.png` |

All paths above are relative to `.planning/phases/84-theme-semantic-token-migration/`. Visual review confirms danger text is a clearly brighter/different red than link text in every case (most pronounced in rakdos, where link is pink; subtler-but-still-distinct hue/luminance difference in the other 7, where link == accent-strong, a dark brick-red family, vs. danger's brighter red).

## D1-Shift Before/After Evidence

- `evidence/d1-shift-classic-light.png` - Classic (site.css) light: `--focus`/`--link`/`--cta-border` swatches, all now `#1e4f82` (was `#2b6cb0`).
- `evidence/d1-shift-nyx-dark.png` - Nyx (dark fork) light-scheme render: all now `#a88cd8` (was `#8b6cc1`).
- Full before/after values for both themes, all 8 D1 probes, are recorded in the diff run output (see commit body) and in `theme-snapshot-post84.json` vs `theme-baseline-pre84.json` directly.

## D3 Typography Deferral Confirmation

Confirmed (per 84-01-SUMMARY's own handoff note, re-verified here): zero `font-size` literals touched by this plan. Task 1 only edited `theming.spec.ts` (TypeScript test file, no CSS). Task 2 made zero CSS edits (audit + evidence capture only — `git status` shows no `wwwroot/css/*` changes). Phase 86 still owns the Typography/`font-size` migration gap; not silently dropped.

## Decisions Made

- Chose to reuse the real production `.feedback-error` CSS class for the red-guild screenshot evidence (rather than an entirely synthetic swatch) so the evidence reflects an actually-shipped selector's resolved color, not just a raw token value.
- Chose to diff the FULL 24-theme x {light,dark} matrix programmatically (1,680 comparisons) rather than a 2-theme manual sample, per the plan's explicit "ALL themes x light/dark" acceptance criterion — this is what surfaced the rakdos finding that a smaller sample would likely have missed.
- Did NOT alter `site-rakdos.css` or any other CSS file in this plan — Task 2 is audit + evidence only, per its file scope.

## Deviations from Plan

None requiring a code fix (Task 2 makes no source changes). One **finding requiring human judgment, not a deviation**: the rakdos-specific 7-swap-site color delta documented above under "Key Finding" — flagged for Task 3 sign-off rather than resolved unilaterally, since resolving it either way (accepting the extra delta as correct, or treating it as scope creep requiring a plan amendment) is a product/visual-design call, not a code-correctness bug.

## Issues Encountered

- Confirmed empirically (via a throwaway Node script, not committed) that `getComputedStyle(document.documentElement).getPropertyValue('--custom-prop')` resolves nested `var()` references to a final computed color rather than returning literal `var(...)` text — this was necessary to validate before writing Task 1's assertions, since `site-commander-table.css`'s tokens are themselves defined as `var(--accent-strong)` rather than a literal hex.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- **Task 3 (checkpoint:human-verify) is next and PENDING** — not self-approved per this plan's explicit instruction. See the checkpoint return for what needs sign-off: (1) the theming e2e result, (2) the red-guild screenshots, (3) the rakdos-specific additional-delta finding above.
- Once approved: `requirements mark-complete THEME-02 THEME-03`, `state advance-plan`, `roadmap update-plan-progress 84`, and the final `docs(84-02): complete...` metadata commit are all still owed — intentionally deferred to whichever agent processes the Task 3 sign-off.
- No blockers. Build clean (0/0); theming e2e 10/10 green.

---
*Phase: 84-theme-semantic-token-migration*
*Completed: 2026-07-04 (Tasks 1-2 only; Task 3 checkpoint pending)*
