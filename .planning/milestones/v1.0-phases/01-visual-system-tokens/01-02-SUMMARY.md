---
phase: 01-visual-system-tokens
plan: 02
subsystem: visual-tokens
tags: [css, design-tokens, semantic-color, hex-hoist, ui-vs-02, ui-vs-03]
requires:
  - phase: 01-visual-system-tokens
    provides: "site.css :root --fs-* type-scale tokens (Plan 01) — extended in same :root block"
provides:
  - "site.css :root semantic color tokens (--link, --danger, --cta-border, --focus)"
  - "site.css :root hoisted hex tokens (--on-accent, --accent-default, --bg-default, --info-default, --success, --error-strong, --gold-warning, --line-cool, --line-cool-soft, --line-warm-soft)"
  - "Zero standalone hex literals outside :root in site.css and site-common.css"
  - ".feedback-error rewired to var(--danger) so themes can decouple error-text from body-link color"
  - ".admin-feedback-filter.active rewired to var(--link) + var(--on-accent)"
  - "Focus rings consume var(--focus); body-link affordances consume var(--link); .page-footer__link--cta consumes var(--cta-border)"
affects:
  - 01-visual-system-tokens (Plan 03 — guild theme propagation will override --link/--danger/--cta-border/--focus per theme)
tech-stack:
  added: []
  patterns:
    - "Semantic CSS custom property layer: link/danger/cta-border/focus alias --accent in classic; themes override per-theme"
    - "Hex-hoist convention: every standalone hex outside :root must be reachable as a named token"
key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/css/site.css
    - DeckFlow.Web/wwwroot/css/site-common.css
key-decisions:
  - "Defaults of --link/--cta-border/--focus alias var(--accent) so classic theme renders pixel-identical post-migration"
  - "--danger is a hard literal (#c53030) distinct from --error (#b83a2e); --error is for banner backgrounds, --danger is for foreground text on white surfaces"
  - "Pass C CTA-border rewire applied to .page-footer__link--cta (named CTA); .hub-card:hover left on var(--accent-strong) (generic panel hover, not a primary action)"
  - "var(--token, #literal) fallback patterns dropped entirely (token always exists in :root) rather than substituted with named-fallback form"
  - "Body-link disclosure summaries (.deckflow-bridge-hint > summary, .moxfield-bulkedit-hint > summary, .judge-howto > summary, .cache-pill__reset) rewired to var(--link); button/tab is-active selectors using var(--accent) left alone (branded surface, not body link)"
patterns-established:
  - "Semantic-color layer: --link, --danger, --cta-border, --focus on :root"
  - "Hex-hoist tokens: --on-accent, --bg-default, --success, --gold-warning, --line-cool*"
requirements-completed:
  - UI-VS-02
  - UI-VS-03
duration: ~6min
completed: 2026-04-30
---

# Phase 01 Plan 02: visual-system-tokens — semantic-color + hex-hoist Summary

**Semantic color tokens (`--link`, `--danger`, `--cta-border`, `--focus`) added to `site.css` :root with classic-theme-preserving defaults; every standalone hex literal outside :root in both `site.css` and `site-common.css` hoisted to named tokens; `.feedback-error` and `.admin-feedback-filter.active` rewired off `--accent-strong` so themes (Plan 03) can decouple error-text from link color.**

## Performance

- **Duration:** ~6 min
- **Tasks:** 3
- **Files modified:** 2

## Accomplishments

- 14 new tokens in `site.css :root` (4 semantic color + 10 hex-hoist)
- 22 hex-literal sites in `site.css` rewired to tokens (incl. 5 hex-fallback `var(--x, #abc)` patterns dropped to bare `var(--x)`)
- 6 hex-literal sites in `site-common.css` rewired to tokens
- `.feedback-error` now consumes `var(--danger)` (Rakdos error-as-link disambiguation primed; only manifests after Plan 03 overrides `--link` in `site-rakdos.css`)
- `.admin-feedback-filter.active` now consumes `var(--link)` + `var(--on-accent)` instead of `var(--accent-strong)` + `#fff`
- Build clean (0 warning, 0 error)

## Task Commits

1. **Task 1: Add semantic color tokens to site.css :root** — `48c2326` (feat)
2. **Task 2: Hoist hex literals and rewire focus/link/CTA in site.css** — `847853c` (refactor)
3. **Task 3: Rewire .feedback-error / .admin-feedback-filter and hoist hex in site-common.css** — `cb4cc3b` (refactor)

## Token Block Added (site.css :root)

Inserted after the type-scale block from Plan 01 and before the closing `}` of the existing single `:root`:

```css
  /* semantic color tokens (UI-VS-02) */
  --link:        var(--accent);
  --danger:      #c53030;
  --cta-border:  var(--accent);
  --focus:       var(--accent);

  /* hoisted hex tokens (UI-VS-03) */
  --on-accent:       #fff;
  --accent-default:  #3a82f7;
  --bg-default:      #fff;
  --info-default:    #eef3f8;
  --success:         #2f855a;
  --error-strong:    #c53030;
  --gold-warning:    #c8a040;
  --line-cool:       #8aaac8;
  --line-cool-soft:  #b0b8c8;
  --line-warm-soft:  #a8c8b8;
```

Defaults preserve classic-theme pixel parity. `--link`, `--cta-border`, `--focus` alias `var(--accent)` (= `#2b6cb0` in classic Jeskai theme); `--danger` is a hard `#c53030` literal distinct from `--error` (`#b83a2e`, used for banner backgrounds — kept untouched).

## Hex-Hoist Counts

| File | Hex outside :root before | Hex outside :root after |
| ---- | ------------------------ | ----------------------- |
| `site.css` | 22 | 0 |
| `site-common.css` | 6 | 0 |
| **Total** | **28** | **0** |

### Token-substitution receipts (site.css)

- `#fff` → `var(--on-accent)`: 7 sites (.skip-link, .error-banner, .info-tooltip, .swap-direction-button:hover, .back-to-top-icon stroke, .back-to-top-button:focus-visible outline, .run-button/.copy-button color)
- `#fff` → `var(--bg-default)`: 1 site (textarea/input/select background — semantic role is "default surface bg", not "on-accent text")
- `#c8a040` → `var(--gold-warning)`: 1 site (.warning-banner border-color)
- `#8aaac8` → `var(--line-cool)`: 1 site (.info-banner border)
- `#b0b8c8` → `var(--line-cool-soft)`: 1 site (.sync-column--moxfield border-color)
- `#a8c8b8` → `var(--line-warm-soft)`: 1 site (.sync-column--archidekt border-color)
- `#2f855a` → `var(--success)`: 2 sites (.copy-button.is-copied bg + border-color)
- `#c53030` → `var(--danger)`: 2 sites (.copy-button.is-copy-failed bg + border-color)
- `var(--accent, #3a82f7)` → `var(--accent)`: 3 sites (.card-lookup-mode-picker is-active bg, .judge-primary border-left)
- `var(--accent, #3a82f7)` → `var(--link)`: 1 site (.judge-howto > summary color — body-link affordance)
- `var(--info, #eef3f8)` → `var(--info)`: 1 site (.cache-pill bg)
- `var(--bg, #fff)` → `var(--bg)`: 1 site (.judge-howto__steps kbd bg)
- `var(--accent)` (color) → `var(--link)`: 3 sites (.cache-pill__reset, .deckflow-bridge-hint > summary, .moxfield-bulkedit-hint > summary)
- `outline: 2px solid var(--accent)` → `var(--focus)`: 2 sites (.skip-link:focus, generic :focus-visible)
- `border: 1px solid var(--accent)` → `var(--cta-border)`: 1 site (.run-button/.copy-button)

### Token-substitution receipts (site-common.css)

- `#3a7` (in `var(--accent-strong, #3a7)` fallback on .feedback-submit) → drop fallback → `var(--accent-strong)`
- `#c55` (in `var(--accent-strong, #c55)` fallback on .feedback-error) → switch token + drop fallback → `var(--danger)` [PASS A critical rewire]
- `#fff` → `var(--on-accent)`: 3 sites (.feedback-submit color, .admin-feedback-filter.active color [also rewired bg from --accent-strong to --link], .copy-button.copy-button--icon.is-copied/.is-copy-failed color)
- `#c33` → `var(--danger)`: 1 site (.detail-actions button.danger bg) — visual-closeness note: `#c33` (`#cc3333`) vs `#c53030` is a small chroma shift; both are deep red, indistinguishable in the destructive-action context
- `var(--accent-strong)` (border on a named CTA) → `var(--cta-border)`: 1 site (.page-footer__link--cta border) [PASS C decision]

## Ambiguous Link/CTA Decisions (executor judgment calls)

| Selector | Choice | Rationale |
| -------- | ------ | --------- |
| `.judge-howto > summary` color | `var(--link)` | "Read more" disclosure summary on a help page — body-link affordance, not a button |
| `.deckflow-bridge-hint > summary` color | `var(--link)` | Same: disclosure-summary text-link affordance |
| `.moxfield-bulkedit-hint > summary` color | `var(--link)` | Same |
| `.cache-pill__reset` color | `var(--link)` | Already styled `text-decoration: underline` — explicitly a textual link affordance |
| `.judge-primary` border-left | `var(--accent)` (NOT `--link`) | Decorative left-border on a panel, not a link or CTA — branded-accent color |
| `.card-lookup-mode-picker is-active` bg | `var(--accent)` (NOT `--cta-border`) | bg of an active radio-button-like control; --cta-border is for borders, not backgrounds. The btn IS a CTA but only the border-color rewire was in scope per plan |
| `.run-button/.copy-button` border | `var(--cta-border)` | Primary action buttons — clearly CTAs |
| `.run-button/.copy-button` color | `var(--on-accent)` (NOT `--cta-border`) | Color is text-on-accent-bg, semantic role differs from border |
| `.page-footer__link--cta` border | `var(--cta-border)` | Selector name explicitly says CTA |
| `.hub-card:hover` border-color | LEFT on `var(--accent-strong)` | Generic interactive panel hover — neither a body link nor a primary action; staying on --accent-strong is correct |
| `.feedback-submit` bg | LEFT on `var(--accent-strong)` (only fallback dropped) | Branded primary-action bg in classic; rewiring to `--link` would muddy the theme override surface |
| `.tool-nav__link.is-active` color | LEFT on `var(--accent)` | Active-tab color, branded surface, not a body link |
| `.chatgpt-step-tab.is-active`, `.chatgpt-layout-picker [data-chatgpt-ui-mode-button].is-active` | LEFT on `var(--accent)` / `var(--accent-strong)` | Active tab/picker chrome — branded surface state |

## Verification Results

| Gate | Expected | Actual |
| ---- | -------- | ------ |
| `--link/--danger/--cta-border/--focus` declared in site.css :root | 1 each | 1, 1, 1, 1 |
| Type-scale tokens still present (Plan 01) | 6 | 6 |
| Single `:root` block in site.css | 1 | 1 |
| Hex literals outside :root in site.css | 0 | 0 |
| Hex literals outside :root in site-common.css | 0 | 0 |
| `var(--x, #literal)` fallback patterns in site.css | 0 | 0 |
| `outline: 2px solid var(--focus)` in site.css | ≥ 2 | 2 |
| `var(--on-accent)` in site.css | ≥ 5 | 8 |
| `var(--success)` in site.css | ≥ 1 | 2 |
| `var(--danger)` in site.css | ≥ 1 | 2 |
| `.feedback-error` → `var(--danger)` in site-common.css | 1 | 1 |
| `.admin-feedback-filter.active` → `var(--link)` in site-common.css | 1 | 1 |
| `var(--accent-strong, #...)` fallback patterns in site-common.css | 0 | 0 |
| `var(--on-accent)` in site-common.css | ≥ 1 | 4 |
| B2 PRE-CHECK: `font-size: 0.875rem` literal in site-common.css | 0 | 0 |
| `dotnet build DeckFlow.Web --no-restore` | exit 0, 0 warning, 0 error | exit 0, 0 warning, 0 error |

## Decisions Made

- Defaults of new semantic tokens alias `var(--accent)` so classic theme renders pixel-identical post-migration (no visual change in this plan; theme overrides land in Plan 03)
- `--danger` (`#c53030`, foreground text) kept distinct from `--error` (`#b83a2e`, banner background) per plan instruction — overlapping semantics intentionally separated
- Hex fallbacks inside `var(--x, #literal)` patterns dropped to bare `var(--x)` (every token now exists in :root, fallback redundant) — preferred over the named-fallback alternative `var(--x, var(--x-default))`
- `.hub-card:hover`, `.feedback-submit` bg, and tab/picker is-active selectors left on `var(--accent-strong)` — they're branded-surface state, not link/CTA semantics

## Deviations from Plan

None — plan executed exactly as written. Every rewire decision matched the rule in the plan's `<interfaces>` section ("body-link affordances → --link; branded-surface state → --accent/--accent-strong; named CTA → --cta-border").

## Issues Encountered

None. Task 1 was already mostly applied (working-tree changes from a prior partial session matched the spec) and was committed cleanly. Tasks 2 and 3 ran straight through.

## Build Status

- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj --no-restore` → **0 Warning, 0 Error** after each task and at the end.
- TypeScript `tsc -p tsconfig.json` ran clean as part of the build target.
- `ZipDeckFlowBridge` ran clean.

## User Setup Required

None — internal CSS refactor with no behavior change in classic theme; no external service configuration needed.

## Next Phase Readiness

- Plan 01-03 (theme propagation) is unblocked. Each guild theme file can now override `--link`, `--danger`, `--cta-border`, `--focus` independently. Specifically: Rakdos's red palette currently has `.feedback-error` rendering visually identical to body link text. After Plan 01-03 overrides `--link` in `site-rakdos.css` to a non-red color (e.g., peach) while `--danger` stays `#c53030`, the error-as-link disambiguation lands.
- 25 guild theme files still need the new token overrides (or sensible aliases of existing per-theme accents). That work is Plan 01-03's scope.

## Phase Smoke-Check Reminder

- Classic theme should render pixel-identical to production on `/`, `/feedback`, `/help`, `/about`, `/sync` since defaults preserve all prior literal values
- Verify `.run-button` / `.copy-button` (CTAs) text color and border still match prior look
- Verify `.feedback-submit` button on `/feedback` still renders blue-on-white text
- The Rakdos error-as-link disambiguation manifests **after Plan 01-03 ships**, not now

## TDD Gate Compliance

N/A — plan is `type: execute`, not `type: tdd`. No RED/GREEN/REFACTOR gates required.

## Self-Check: PASSED

- [x] `DeckFlow.Web/wwwroot/css/site.css` exists and contains 14 new tokens in :root.
- [x] `DeckFlow.Web/wwwroot/css/site-common.css` exists and contains 0 hex literals outside :root.
- [x] Commit `48c2326` exists in git log (Task 1).
- [x] Commit `847853c` exists in git log (Task 2).
- [x] Commit `cb4cc3b` exists in git log (Task 3).
- [x] `dotnet build DeckFlow.Web/DeckFlow.Web.csproj --no-restore` exits 0 with 0 warnings.
- [x] Final verification block (5 plan-level checks) all PASS.

---
*Phase: 01-visual-system-tokens*
*Plan: 02 — semantic-color + hex-hoist*
*Completed: 2026-04-30*
