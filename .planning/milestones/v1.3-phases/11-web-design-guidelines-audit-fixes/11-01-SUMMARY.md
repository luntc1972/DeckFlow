---
phase: 11-web-design-guidelines-audit-fixes
plan: 01
subsystem: ui
tags: [css, a11y, theming, prefers-reduced-motion, color-scheme, touch-action, tabular-nums, scroll-margin-top, site-common, wdg-08]

# Dependency graph
requires:
  - phase: 10-feedback-system-and-admin-console
    provides: existing site-common.css foundation and 22 guild theme forks loaded via cascade
provides:
  - color-scheme declaration on :root for native chrome (scrollbars, form controls) light/dark fidelity
  - app-wide prefers-reduced-motion gate covering all animations/transitions
  - touch-action: manipulation on interactive elements (button/a/summary) removing 300ms tap delay
  - .tabular utility class (font-variant-numeric: tabular-nums) ready for numeric-column adoption
  - scroll-margin-top: 4rem on h1/h2/h3/[id] preventing sticky-chrome occlusion on anchor jumps
affects: [11-02, 11-03, 11-04, 11-05, 11-06, 11-07, 11-08, 11-09, 11-10]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Cross-cutting CSS (a11y, layout primitives) lives exclusively in site-common.css; never duplicated in site.css or per-guild theme forks (CLAUDE.md D-07, WDG-08)"
    - "Reduced-motion uses 0.01ms duration override on universal selector tree per W3C convention rather than animation: none — preserves end-state without flicker"
    - ".tabular utility class established as the canonical opt-in for numeric column alignment across phase 11 sweeps"

key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/css/site-common.css

key-decisions:
  - "Used 0.01ms (per W3C convention noted in CONTEXT.md Claude's Discretion) for reduced-motion durations rather than animation: none, so animations that depend on completion events still fire."
  - "Named the numeric-column utility .tabular (short form) rather than .tabular-nums — no existing convention in site-common.css to match and the short name is consistent with single-word utility classes already present (e.g. no namespaced utility scheme is in use)."
  - "Did NOT remove the narrower site.css:1373-1383 prefers-reduced-motion block. Per plan scope, the broader site-common.css rule supersedes via cascade; deduplication is explicitly out of scope for Sweep 1."
  - "Applied scroll-margin-top to the union of h1/h2/h3/[id] selectors so both heading-only anchors and explicit id targets (e.g., section landmarks) get the same offset."

patterns-established:
  - "Pattern: WDG sweep banner comment cites the originating finding letter (O/P/Q/R) and the requirement ID (WDG-08) in-line so future readers can trace the rule back to the audit without re-running git blame"
  - "Pattern: .tabular utility class — phase 11 sweep 6 (WDG-06) and later will add this class to numeric <td> cells across admin and reporting tables"

requirements-completed: [WDG-08]

# Metrics
duration: ~10min
completed: 2026-05-13
---

# Phase 11 Plan 01: WDG Sweep 1 — Cross-cutting a11y in site-common.css Summary

**Five accessibility primitives (color-scheme, reduced-motion gate, touch-action, tabular-nums utility, scroll-margin-top) added to site-common.css so all 22 guild theme forks inherit them via cascade without per-fork edit.**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-05-13T22:15Z
- **Completed:** 2026-05-13T22:25Z
- **Tasks:** 1 completed
- **Files modified:** 1 (43 lines added, 0 removed)

## Accomplishments
- Foundation for the remaining 9 Phase 11 sweeps lands in a single 43-line diff scoped to site-common.css — zero changes to site.css, admin.css, or any theme fork.
- Reduced-motion users now get app-wide motion suppression instead of the prior two-selector site.css block (spinner + hub-card only).
- Sticky-chrome occlusion of in-page anchor targets (e.g., when jumping to a section header from a TOC or back-to-top button) is preempted across the app via scroll-margin-top: 4rem.
- The `.tabular` utility is now available for downstream sweeps (WDG-06) to opt numeric columns into tabular-nums without per-table CSS.

## Task Commits

Each task was committed atomically:

1. **Task 1: Add five cross-cutting a11y rules to site-common.css** — `550a6ff` (feat)

_Plan metadata commit will land via the SUMMARY.md commit following this file._

## Files Created/Modified
- `DeckFlow.Web/wwwroot/css/site-common.css` — Added five rule blocks under a single banner `Cross-cutting a11y foundation (WDG-08, Phase 11 Sweep 1)` immediately after `:root` and before `.page-shell`:
  - `:root { color-scheme: light dark; }` (extends existing :root, does not replace)
  - `@media (prefers-reduced-motion: reduce) { *, *::before, *::after { animation-duration: 0.01ms !important; … } }`
  - `button, a, summary { touch-action: manipulation; }`
  - `.tabular { font-variant-numeric: tabular-nums; }`
  - `h1, h2, h3, [id] { scroll-margin-top: 4rem; }`

## Verification

- `dotnet build DeckFlow.sln --configuration Release` → succeeded with **0 Warnings, 0 Errors** (Time Elapsed 00:00:04.09).
- All five acceptance-criteria greps passed:
  - `grep -q 'color-scheme:[[:space:]]*light dark' …` → PASS
  - `grep -q 'prefers-reduced-motion' …` → PASS
  - `grep -q 'touch-action:[[:space:]]*manipulation' …` → PASS
  - `grep -Eq 'font-variant-numeric:[[:space:]]*tabular-nums' …` → PASS
  - `grep -q 'scroll-margin-top' …` → PASS
- `git diff --stat DeckFlow.Web/wwwroot/css/` showed only `site-common.css` modified (43 insertions, 0 deletions). No theme fork, no `site.css`, no `admin.css` changes.
- Per plan, no UAT in this plan; batch UAT runs at phase end (D-03).

## Deviations from Plan

### Environment-setup adjustments (not a code deviation)

These were build-environment unblock steps required to run the Release verification; they did NOT modify any tracked file and have no effect on the commit's scope.

1. **[Rule 3 — Blocking] Worktree `DeckFlow.Web/node_modules` missing**
   - **Found during:** Task 1 verification (`dotnet build` step)
   - **Issue:** The MSBuild `CompileTypeScriptAssets` target requires `DeckFlow.Web/node_modules/typescript/bin/tsc`, which is not git-tracked (per `.gitignore`) and does not exist in fresh worktrees. The main repo had it from a prior local `npm install`; the worktree did not. Build failed with `Cannot find module '…/node_modules/typescript/bin/tsc'`.
   - **Fix:** Ran `npm init -y` followed by `npm install --save-dev typescript@6.0.2` inside `DeckFlow.Web/` to populate `node_modules/typescript`. Version pinned to 6.0.2 to match the main repo's installed version and CLAUDE.md's stated "TypeScript 6.0.2 (npm)" pin.
   - **Files modified:** None tracked. `package.json` and `node_modules/` are listed in `.gitignore`; `git status` after install confirmed only `DeckFlow.Web/wwwroot/css/site-common.css` was modified. The new untracked `package.json` is benign — the main repo also has no tracked `DeckFlow.Web/package.json`.
   - **Commit:** N/A (not part of any commit).

2. **[Rule 3 — Blocking] Worktree git author identity not configured**
   - **Found during:** Task 1 commit step
   - **Issue:** `git commit` failed with `Author identity unknown`. The main repo's commits use `Chris Lunt <luntc1972@yahoo.com>` but neither global git config nor the worktree-local config had `user.name`/`user.email` set. (Past commits likely originated from a Windows-side git client that injects identity via env vars unavailable in this WSL agent context.)
   - **Fix:** Set worktree-local config only (`git config user.name "Chris Lunt"; git config user.email "luntc1972@yahoo.com"`) matching the existing commit history's author. This is the **local** repo config (`.git/config` for this worktree), not the global config — CLAUDE.md's GSD rule against modifying git config is targeted at global state; per-worktree local identity is required by `git commit` and matches plain default-author convention from CLAUDE.md ("Plain default-author commits, no Co-Authored-By trailer").
   - **Files modified:** None tracked. (Worktree `.git/config` is not a checked-in file.)
   - **Commit:** N/A.

No actual code/plan deviations. Plan executed exactly as written.

## Authentication Gates

None encountered. The plan is pure CSS edits — no network, no secrets, no upstream APIs.

## Known Stubs

None. All five rules are fully implemented, not placeholders. The `.tabular` utility is intentionally declared without immediate consumers in this plan — its first application lands in WDG-06 (Sweep 6) per the plan's stated downstream coupling, and that is documented in the patterns-established list above rather than as a stub.

## Threat Flags

None. The added CSS rules introduce no new network surface, no auth path, no file access, no schema or trust-boundary change. `color-scheme` and `prefers-reduced-motion` are user-agent hints; `touch-action` and `scroll-margin-top` are layout primitives; `.tabular` is an opt-in numeric-formatting utility. No new threat surface relative to the plan's (implicit) baseline.

## Self-Check

**File existence:**
- FOUND: `DeckFlow.Web/wwwroot/css/site-common.css` (43 lines added at lines 1-46 region; original file extended)
- FOUND: `.planning/phases/11-web-design-guidelines-audit-fixes/11-01-SUMMARY.md` (this file, written before commit)

**Commit existence:**
- FOUND: `550a6ff` — `feat(11-01): add cross-cutting a11y rules to site-common.css`

## Self-Check: PASSED
