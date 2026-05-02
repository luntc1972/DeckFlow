---
phase: 01-visual-system-tokens
plan: 03
subsystem: ui
tags: [css, theming, design-tokens, typography, color-tokens, guild-themes]

# Dependency graph
requires:
  - phase: 01-visual-system-tokens
    provides: Type-scale tokens (Plan 01) and semantic-color/hex-hoist tokens (Plan 02) declared in site.css :root
provides:
  - 11 non-importer guild themes (abzan, bant, esper, grixis, jeskai, jund, mardu, naya, nyx, planeswalker-dark, sultai) carry the full canonical token block in :root
  - Rakdos --link override (#ff9ea4 peach) lands the UI-VS-02 error-vs-link disambiguation
  - Selesnya / Dimir audit decisions documented (both inherit; rationale recorded)
  - Stale-shadow audit clears 9 importer themes (zero new-token redeclarations)
  - Deferred per-theme literal counts logged for Phase 2 planner
affects: [phase-02-theme-cleanup, phase-02-typography-residuals, future-guild-additions]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Non-importer guild themes carry an explicit 16-line token block at the END of :root; importers inherit via @import url('site.css')"
    - "Per-theme --link override only when accent fights --danger contrast (Rakdos peach over red)"
    - "--danger hard-locked to #c53030 across all themes so error red survives theme accents"

key-files:
  created: []
  modified:
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
    - DeckFlow.Web/wwwroot/css/site-rakdos.css

key-decisions:
  - "Selesnya: INHERIT, no --link override — green --accent (#3a7a52) on cream panels reads cleanly, existing 'a' rule already binds --accent-strong"
  - "Dimir: INHERIT, no --link override — cobalt --accent (#3d81ea) on dark bg is conventional; flagged literal 'a { color: #9fc6ff }' as Phase 2 hoist candidate"
  - "Rakdos: REQUIRED override --link: #ff9ea4 mirrors existing 'a' literal so error red (--danger #c53030) is visually distinct from body link"
  - "9 'clean' importers (azorius, boros, dimir, golgari, gruul, izzet, orzhov, simic, temur) have zero stale shadows of new tokens — pure @import inheritance"

patterns-established:
  - "Non-importer fork token-injection: insert canonical 16-line block at END of existing :root, before closing brace, never modify existing theme tokens"
  - "Importer-side audit: grep for accidental --fs-*/--link/--danger redeclarations to catch shadow drift before merge"
  - "Per-theme override is the exception, not the rule — only Rakdos needed one in this phase"

requirements-completed: [UI-VS-04]

# Metrics
duration: ~30min
completed: 2026-04-30
---

# Phase 01 Plan 03: Theme Token Propagation Summary

**Token block propagated to 11 non-importer guild themes; Rakdos --link override (#ff9ea4) lands the error-vs-link disambiguation; importer themes audit clean.**

## Performance

- **Duration:** ~30 min (Task 1 + Task 2 implementation; Task 3 manual smoke)
- **Started:** 2026-04-30T18:23:08Z (Task 1 commit)
- **Completed:** 2026-04-30T18:30:00Z (~12:30pm MDT user approval)
- **Tasks:** 3 (2 auto + 1 human-verify checkpoint)
- **Files modified:** 12

## Accomplishments
- All 11 non-importer guild themes (abzan, bant, esper, grixis, jeskai, jund, mardu, naya, nyx, planeswalker-dark, sultai) now carry the canonical 16-line token block in :root — type-scale (--fs-xs through --fs-2xl) plus semantic color (--link, --danger, --cta-border, --focus) plus hoisted hex defaults (--on-accent, --bg-default, --info-default, --success, --error-strong, etc.)
- Rakdos `--link: #ff9ea4` override lands the UI-VS-02 success criterion: error red (--danger #c53030) is visually distinct from body link (peach) on red theme
- Stale-shadow audit verified 9 importer themes inherit cleanly via `@import url('site.css')` with zero token redeclarations
- All 25 :root-declaring CSS files now reach the new tokens (12 explicit + 11 importer-inherited + site-common.css + site-commander-table.css)
- User-confirmed visual smoke check on classic + Rakdos + Selesnya + Dimir across `/`, `/feedback`, `/help`, `/about`, `/sync` — no regressions reported

## Task Commits

1. **Task 1: Inject token block into 11 non-importer themes** — `0ec144b` (feat)
2. **Task 2: Rakdos --link override + importer stale-shadow audit** — `2c193c6` (feat)
3. **Task 3: Manual smoke check on classic + Rakdos + Selesnya + Dimir** — APPROVED 2026-04-30 ~12:30pm MDT (no commit; human-verify checkpoint)

## Files Created/Modified

### Task 1 — `0ec144b` (11 files, +286 / -0)

| File | Lines added |
|------|-------------|
| `DeckFlow.Web/wwwroot/css/site-abzan.css` | +26 |
| `DeckFlow.Web/wwwroot/css/site-bant.css` | +26 |
| `DeckFlow.Web/wwwroot/css/site-esper.css` | +26 |
| `DeckFlow.Web/wwwroot/css/site-grixis.css` | +26 |
| `DeckFlow.Web/wwwroot/css/site-jeskai.css` | +26 |
| `DeckFlow.Web/wwwroot/css/site-jund.css` | +26 |
| `DeckFlow.Web/wwwroot/css/site-mardu.css` | +26 |
| `DeckFlow.Web/wwwroot/css/site-naya.css` | +26 |
| `DeckFlow.Web/wwwroot/css/site-nyx.css` | +26 |
| `DeckFlow.Web/wwwroot/css/site-planeswalker-dark.css` | +26 |
| `DeckFlow.Web/wwwroot/css/site-sultai.css` | +26 |

Each file got the 16-line canonical token block injected at the end of `:root` (block expands to 26 lines with comments + spacing). No existing theme tokens were modified.

### Task 2 — `2c193c6` (1 file, +3 / -0)

- `DeckFlow.Web/wwwroot/css/site-rakdos.css:14` — `--link: #ff9ea4;` (placed in :root after `--accent-strong`, mirrors existing literal in `a { color: #ff9ea4 }` body rule)

## Decisions Made

### Selesnya (`site-selesnya.css`) — INHERIT, no override
- **Rationale:** `--link` resolves via the @import cascade to Selesnya's green `--accent` (#3a7a52). The existing `a` selector in the theme already binds to `--accent-strong` (deeper green) for body-link weight, so cream-on-green reads cleanly without an override. No visual regression observed during smoke check.

### Dimir (`site-dimir.css`) — INHERIT, no override
- **Rationale:** `--link` resolves to Dimir's cobalt `--accent` (#3d81ea) on dark bg — conventional and readable.
- **Phase 2 flag:** existing literal `a { color: #9fc6ff }` in Dimir is slightly brighter than the token value. Out of scope for Plan 03 (per-theme literal hoisting is Phase 2 territory), but logged here so the next milestone planner can pick it up.

### Rakdos (`site-rakdos.css`) — REQUIRED override
- **Rationale:** Without the override, `--link` would fall through to Rakdos's red accent — visually colliding with `--danger` (#c53030). Setting `--link: #ff9ea4;` (the same peach already used by the body `a` literal) ensures error messages render as red while body links render as peach, satisfying UI-VS-02.

### Importer stale-shadow audit — 9 importers CLEAN
- **Audited:** site-azorius.css, site-boros.css, site-dimir.css, site-golgari.css, site-gruul.css, site-izzet.css, site-orzhov.css, site-simic.css, site-temur.css
- **Result:** Zero redeclarations of `--fs-*`, `--link`, `--danger`, `--cta-border`, `--focus`, `--on-accent`. All inherit cleanly via `@import url('site.css')`.
- Only intentional override across all 11 importers: Rakdos `--link` (this plan).

## Deviations from Plan

None — plan executed exactly as written. Selesnya and Dimir audit decisions matched the planner's pre-flagged "default behavior: inherit, no override" guidance.

## Issues Encountered

None.

## Smoke Check Results (Task 3)

- **Pages tested:** `/`, `/feedback`, `/help`, `/about`, `/sync`
- **Themes tested:** classic (default), rakdos, selesnya, dimir
- **Approval:** user-confirmed APPROVED 2026-04-30 ~12:30pm MDT
- **Regressions:** none reported
- **Rakdos disambiguation:** confirmed — error red and body-link peach render as distinctly different hues
- **Token resolution:** Selesnya green `--link` and Dimir cobalt `--link` both resolved cleanly in DevTools (no fallback bleed, no "not defined")
- **Live-site parity (deferred):** post-deploy walk on https://www.deckflow.gg pending (Render auto-deploys from main). This is ROADMAP success criterion #5 — final post-merge sign-off.

## Phase 01 Status

| ROADMAP Criterion | Status |
|---|---|
| #1 — site.css + site-common.css font-size literals migrated to var(--fs-*) | DONE (Plan 01) |
| #2 — Rakdos --link override gives error-vs-link disambiguation | DONE (Plan 03 Task 2) |
| #3 — site.css + site-common.css hex literals hoisted into :root tokens | DONE (Plan 02) |
| #4 — All 25 :root-declaring CSS files reach new tokens (UI-VS-04) | DONE (Plan 03 Task 1 + audit) |
| #5 — Live-site parity sign-off on https://www.deckflow.gg | PENDING (post-deploy) |

Criteria #1-#4 covered. Criterion #5 awaits the next Render deploy from main.

## Next Phase Readiness
- Single semantic-token layer now drives typography + color across classic + all 25 themes
- Per-theme literal residuals (font-size and hex) catalogued below for Phase 2 scoping
- No blockers for downstream UI work

---

## Deferred — non-importer theme literals (Phase 2 candidate)

ROADMAP criterion #1 only required `site.css` + `site-common.css` migration; per-theme residuals are bounded scope reduction, deferred. Counts captured at Plan 03 close so the next milestone planner sees the surface:

```
DeckFlow.Web/wwwroot/css/site-abzan.css: 17 rem-literals, 62 hex-literals
DeckFlow.Web/wwwroot/css/site-bant.css: 17 rem-literals, 57 hex-literals
DeckFlow.Web/wwwroot/css/site-esper.css: 17 rem-literals, 60 hex-literals
DeckFlow.Web/wwwroot/css/site-grixis.css: 17 rem-literals, 71 hex-literals
DeckFlow.Web/wwwroot/css/site-jeskai.css: 17 rem-literals, 58 hex-literals
DeckFlow.Web/wwwroot/css/site-jund.css: 17 rem-literals, 69 hex-literals
DeckFlow.Web/wwwroot/css/site-mardu.css: 17 rem-literals, 58 hex-literals
DeckFlow.Web/wwwroot/css/site-naya.css: 17 rem-literals, 57 hex-literals
DeckFlow.Web/wwwroot/css/site-nyx.css: 17 rem-literals, 53 hex-literals
DeckFlow.Web/wwwroot/css/site-planeswalker-dark.css: 17 rem-literals, 68 hex-literals
DeckFlow.Web/wwwroot/css/site-sultai.css: 17 rem-literals, 75 hex-literals
```

Total residual surface: **187 rem-literals + 688 hex-literals** across 11 non-importer forks. Note: Dimir importer also has a literal `a { color: #9fc6ff }` flagged above as Phase 2 hoist candidate.

---
*Phase: 01-visual-system-tokens*
*Plan: 03*
*Completed: 2026-04-30*

## Self-Check: PASSED

- SUMMARY file: FOUND `.planning/phases/01-visual-system-tokens/01-03-SUMMARY.md`
- Task 1 commit: FOUND `0ec144b`
- Task 2 commit: FOUND `2c193c6`
