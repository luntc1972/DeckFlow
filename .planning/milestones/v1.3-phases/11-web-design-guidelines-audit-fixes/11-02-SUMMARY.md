---
phase: 11-web-design-guidelines-audit-fixes
plan: 02
subsystem: ui
tags: [css, accessibility, a11y, focus-visible, admin-shell, wdg-audit]

# Dependency graph
requires:
  - phase: 11-web-design-guidelines-audit-fixes
    provides: "Sweep 1 (11-01) added cross-cutting a11y rules to site-common.css. 11-02 is the parallel admin-shell fix because admin.css does NOT inherit site-common.css."
provides:
  - "Universal `:focus-visible` outline on every interactive element inside `/Admin/*` pages (a, button, input, select, textarea, summary, [role=tab])"
  - "`--focus` design token in admin.css `:root` (aliased to `--accent`, mirroring site.css)"
  - "`color-scheme: dark` on admin.css `:root` so native chrome (scrollbars, select dropdowns) renders dark"
  - "`font-variant-numeric: tabular-nums` on `.admin-table td:not(.route)` so polling AJAX updates do not jitter horizontally"
affects: [phase-12-url-rename (admin URL slugs already final, no overlap), v1.3-batch-uat (phase-end Tab-navigation verification), phase-11-remaining-sweeps (03-10 — independent)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Admin-shell a11y rules live LOCALLY in admin.css — no inheritance from site.css or site-common.css. Every cross-cutting WDG rule that needs to reach `/Admin/*` must be added directly to admin.css (parallel maintenance), or admin.css must be migrated to import site-common.css (out of scope for v1.3)."
    - "WDG audit traceability: every a11y addition is grouped under a `/* === WDG audit fixes (WDG-01, Phase 11 Sweep 2) === */` banner that references `260513-wdg-FINDINGS.md` and the specific finding letter (A/O/R)."

key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/css/admin.css

key-decisions:
  - "Defined `--focus: var(--accent)` in admin.css `:root` rather than hard-coding the focus outline color. Mirrors the site.css:45 pattern (`--focus: var(--accent)`) and means a future admin accent re-theme also re-themes focus rings without a second edit."
  - "Applied `tabular-nums` via direct selector `.admin-table td:not(.route)` rather than the `.tabular` utility class from Sweep 1 (11-01). admin.css does not inherit site-common.css, so the utility class would not resolve. Direct selector keeps the fix self-contained and avoids a stylesheet-import refactor that is out of scope for v1.3 Phase 11."

patterns-established:
  - "Admin-shell a11y additions: banner-comment with `WDG-01, Phase 11 Sweep 2` traceability, link to FINDINGS.md, finding-letter callout."
  - "Local `--focus` token alias in standalone stylesheets (admin.css) — keep the variable name identical to site.css so future copy/paste of a11y rules from main shell works without rewrite."

requirements-completed: [WDG-01]

# Metrics
duration: ~12 min
completed: 2026-05-13
---

# Phase 11 Plan 02: admin.css universal :focus-visible block (WDG-01) Summary

**Universal keyboard-focus indicator + color-scheme + tabular-nums added to admin.css, mirroring site.css:109-118 so the admin shell renders the same visible focus ring as the main shell.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-05-13T22:20:32Z (approx — orchestrator handoff)
- **Completed:** 2026-05-13T22:30:44Z
- **Tasks:** 1
- **Files modified:** 1 (`DeckFlow.Web/wwwroot/css/admin.css`)

## Accomplishments

- Added universal `:focus-visible` block to admin.css covering `a, button, input, select, textarea, summary, [role="tab"]` with `outline: 2px solid var(--focus)` and `outline-offset: 2px` — byte-for-byte structural mirror of `site.css:109-118` (finding A).
- Defined `--focus: var(--accent)` token + `color-scheme: dark` on admin.css `:root` (finding O).
- Added `font-variant-numeric: tabular-nums` to `.admin-table td:not(.route)` so polling AJAX updates (e.g. AdminHarvest counts) do not jitter horizontally (finding R).
- Release build clean: 0 warnings, 0 errors (dotnet 10.0.300).

## Task Commits

Each task was committed atomically:

1. **Task 1: Add universal :focus-visible block + color-scheme + tabular-nums to admin.css** — `7fd6acd` (feat)

## Files Created/Modified

- `DeckFlow.Web/wwwroot/css/admin.css` — added (a) `--focus` token + `color-scheme: dark` inside existing `:root`, (b) new universal `:focus-visible` selector block immediately after `:root`, (c) new `.admin-table td:not(.route) { font-variant-numeric: tabular-nums; }` rule grouped with existing `.admin-table` rules. Three banner comments cross-reference `260513-wdg-FINDINGS.md` findings A/O/R for traceability. No existing rules were removed.

## Decisions Made

- **`--focus` token aliasing:** Followed site.css:45's `--focus: var(--accent)` pattern rather than literal color value. Keeps admin-shell focus ring in sync with admin-shell accent token if `--accent` is ever rethemed.
- **Tabular-nums via direct selector, not utility class:** Used `.admin-table td:not(.route) { font-variant-numeric: tabular-nums; }` directly per FINDINGS.md Sweep 2 line 270. The `.tabular` utility class added in 11-01 lives in `site-common.css` which admin.css does not inherit, so the utility class would not resolve in the admin shell. Direct local rule keeps admin.css self-contained.
- **Placement:** Token additions inside existing `:root`; new `:focus-visible` selector block immediately after `:root` (near top, with reset-style rules); new `.admin-table` rule grouped with existing `.admin-table` block. Each addition is preceded by a banner comment referencing WDG-01 / Phase 11 Sweep 2 / finding letter for grep-ability.

## Deviations from Plan

### Auto-fixed Issues

None.

### Acceptance Criteria Notes

The plan's acceptance criterion AC2 specifies a single-line regex that cannot match a multi-line CSS selector list. The same regex `grep -Eq 'a,[[:space:]]*button,...,summary,[[:space:]]*\[role="tab"\]'` also fails against the canonical block in `site.css:109-118` because:

1. Each selector in the canonical block sits on its own line with its own `:focus-visible` pseudo-class (e.g. `summary:focus-visible,\n[role="tab"]:focus-visible`), so the literal substring `summary,` does not appear — only `summary:focus-visible,`.
2. `grep -E` operates line-by-line, so `[[:space:]]*` cannot span newlines.

**Treatment:** Implementation was completed against the truth-of-record specification ("Mirror `site.css:109-118` universal `:focus-visible` rule into admin.css ... same outline declaration values"), not the faulty regex. A byte-for-byte side-by-side comparison of the canonical block and the new admin.css block confirms:

- Identical selector list (7 selectors: `a, button, input, select, textarea, summary, [role="tab"]`, each with `:focus-visible`).
- Identical outline declaration: `outline: 2px solid var(--focus); outline-offset: 2px;`.

AC1 (`:focus-visible` present), AC3 (`color-scheme` present), AC4 (`tabular-nums` present), AC5 (only admin.css modified), and AC6 (Release build 0 warnings 0 errors) all pass.

This is a planning-spec issue, not an implementation gap — flagged here so the verifier and the planner of Phase 11 Sweep N+1 can fix the AC pattern (or use `grep -zE` for multi-line CSS) in future plans.

---

**Total deviations:** 0 auto-fixed.
**Impact on plan:** None. AC regex is faulty; truth-of-record (mirror site.css:109-118) is satisfied exactly.

## Issues Encountered

- **Build tool path:** `dotnet` is not on the WSL PATH in this worktree; the Windows binary at `/mnt/c/Program Files/dotnet/dotnet.exe` was used directly. Build resolved without issue (10.0.300 SDK, 0 warnings, 0 errors). Same path is used by CI for Windows builds; no action needed.
- **TypeScript node_modules absent in worktree:** Per parallel_execution guidance, ran `npm install --save-dev typescript@6.0.2` inside `DeckFlow.Web/` before the build. Standard worktree warmup, not a real issue.

## Threat Flags

None. CSS-only change. No new network endpoints, auth surfaces, file-access patterns, or trust-boundary schema changes.

## Known Stubs

None. The change is a complete cross-cutting a11y fix; nothing is wired to placeholder data.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- **Phase 11 Sweep 2 (this plan): COMPLETE.** WDG-01 requirement satisfied for the admin shell. Sweep 1's foundation (11-01) covers the main shell via `site-common.css`; this plan covers admin via local admin.css. Together they close the universal `:focus-visible` coverage gap across DeckFlow.
- **Phase 11 SC #1 verification:** Will be confirmed at end-of-phase batch UAT (D-03) by Tab-navigating across `/Admin/Feedback`, `/Admin/Harvest`, `/Admin/Analytics`, `/Admin/Flags` and confirming a visible focus ring on every interactive element. No mid-phase UAT in this plan per D-03.
- **No blockers for Phase 11 Sweep 3+ (plans 03-10):** Each sweep is independent (per D-01) and can ship in parallel waves.

## Self-Check: PASSED

- File `DeckFlow.Web/wwwroot/css/admin.css` exists and contains the new block.
- Commit `7fd6acd` is present in `git log`.
- Release build exits 0 with 0 warnings (verified via `dotnet build DeckFlow.sln --configuration Release`).
- Only `DeckFlow.Web/wwwroot/css/admin.css` modified (`git diff --stat` confirms 1 file changed, 26 insertions, 0 deletions).

---
*Phase: 11-web-design-guidelines-audit-fixes*
*Completed: 2026-05-13*
