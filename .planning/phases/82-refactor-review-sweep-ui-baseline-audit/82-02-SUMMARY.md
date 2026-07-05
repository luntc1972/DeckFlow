---
phase: 82-refactor-review-sweep-ui-baseline-audit
plan: 02
subsystem: ui
tags: [css-audit, accessibility, ui-review, playwright, screenshot-audit]

# Dependency graph
requires:
  - phase: 82-refactor-review-sweep-ui-baseline-audit (plan 01)
    provides: REFACTOR-TRIAGE.md (unrelated triage output; no functional dependency)
provides:
  - "Re-scored 6-pillar UI baseline (tasks/UI-REVIEW.md), 18/24, +2 over prior 16/24"
  - "Enumerated, phase-tagged gap-to-20 handoff: Color+Typography fixes assigned to Phase 84"
  - "Explicit needs-assignment flags for Spacing / feedback double-submit / branded error page (no phase owner this cycle)"
affects: [phase-84-theme-semantic-token-migration, phase-86-ui-audit-rescore]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Headless UI audit via scripts/run-web-test.sh + playwright-core scratch script (no new tracked test file, no Windows browser)"]

key-files:
  created: []
  modified:
    - tasks/UI-REVIEW.md

key-decisions:
  - "Scored against live rendered screenshots (6 routes x 2 viewports) plus full-file CSS grep evidence, not code-review-only fallback -- server came up after npm ci restored missing node_modules"
  - "Color and Typography gaps assigned to Phase 84 (THEME) since both are CSS-token-migration work touching the same files THEME already owns; Spacing, feedback double-submit, and missing branded error page flagged under an explicit needs-assignment subsection rather than silently defaulting to Phase 86"

requirements-completed: [UIAUDIT-01]

# Metrics
duration: 15min
completed: 2026-07-04
---

# Phase 82 Plan 2: UI Baseline Audit Re-Score Summary

**Re-ran the 6-pillar UI audit against live screenshots (desktop + mobile, 6 routes) and source evidence, scoring 18/24 (+2 over the 2026-04-30 baseline of 16/24), then enumerated the gap-to-20 with Color and Typography fixes handed to Phase 84 and three residual gaps (Spacing, feedback double-submit, branded error page) flagged for explicit operator assignment rather than left for Phase 86.**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-07-04T09:41:00-06:00 (approx, server bring-up)
- **Completed:** 2026-07-04T09:51:53-06:00
- **Tasks:** 2 completed
- **Files modified:** 1 (`tasks/UI-REVIEW.md`)

## Accomplishments

- Brought up the headless test server (`scripts/run-web-test.sh`, `DECKFLOW_DISABLE_AUTO_BROWSER=true`) and captured 12 full-page screenshots (6 routes × desktop 1280×900 / mobile 390×844) via `playwright-core`, driven from a scratch Node script (not a repo-tracked spec) — no Windows browser opened.
- Re-scored all 6 pillars against the live render + full-file CSS grep evidence: Copywriting 3/4, Visuals 4/4 (up from 3/4 — home hero CTA resolved the prior "no focal point" gap), Color 2/4 (unchanged — `--accent-strong` overload persists at 58+4 call sites despite new alias tokens existing), Typography 3/4 (up from 2/4 — 6-step `--fs-*` scale landed with ~80% adoption), Spacing 3/4 (unchanged — no `--space-*` tokens), Experience Design 3/4 (unchanged score, but 2 of 3 prior gaps fixed: reduced-motion + inline-style removal; one new gap remains open — feedback-form double-submit).
- Enumerated the concrete gap-to-20 fixes and tagged each with an owning phase: Color's 58+4-call-site `--accent-strong`→alias migration and Typography's ~24-value literal-to-token cleanup both assigned to Phase 84 (THEME) since they touch the same CSS files THEME already owns. Spacing's missing `--space-*` scale, the feedback-form double-submit bug, and the missing branded `Error.cshtml` were explicitly flagged under a `### Out of Phase 84/85 scope — needs assignment` subsection — none defaulted to Phase 86.
- Target overall after Phase 84's fixes land: 21/24, clearing the >=20/24 bar with a 1-point margin.

## Task Commits

Each task was committed atomically:

1. **Task 1: Run the 6-pillar UI audit and produce the scored baseline** - `26f92192` (docs)
2. **Task 2: Enumerate the gap-to-20 and hand each fix to Phase 84/85** - `b47fb051` (docs)

**Plan metadata:** (this commit, docs: complete plan)

## Files Created/Modified

- `tasks/UI-REVIEW.md` - Overwritten with the 2026-07-04 re-scored baseline (18/24, prior 16/24) plus the appended Gap to >=20/24 — Handoff to Phases 84/85 section

## Decisions Made

- **Server-bring-up blocker resolved via `npm ci` (Rule 3, blocking-issue auto-fix, not a new dependency):** the worktree's `DeckFlow.Web/node_modules/` was absent (fresh worktree checkout never had `npm install` run), causing the MSBuild TypeScript-compile step to fail with `Cannot find module '...\typescript\bin\tsc'`. Ran `npm ci` in `DeckFlow.Web/` — this restores exactly the versions already pinned in the committed `package-lock.json`, adding zero new packages, so it does not trigger the "no new dependencies without asking" rule. The server then built and served correctly.
- **Screenshot capture via a scratch Node script instead of a new Playwright spec file:** the plan's byte-neutral constraint means no new tracked test file could be added under `DeckFlow.Web/e2e/`. Wrote a temporary capture script to the session scratchpad directory (outside the repo) that drives `playwright-core`'s Chromium against the already-running headless server — satisfies the "capture screenshots via playwright" instruction without touching any tracked file.
- **Color and Typography gaps assigned to Phase 84, not split across 84/85:** both are pure CSS-token-migration work (alias substitution, literal-to-var migration) in the exact files (`site.css`, `site-common.css`, 27 theme forks) that Phase 84 (THEME) already owns per ROADMAP — no `chatgpt-*` identifier involvement, so nothing was assigned to Phase 85 in this pass. Phase 85 is expected to be scope-neutral for the UI score (byte-identical render, AICLEAN-03).
- **Spacing, feedback double-submit, and missing branded error page flagged as needs-assignment, not silently deferred to Phase 86:** per Phase 82 success-criterion 5 and the plan's explicit instruction, any gap that doesn't fit Phase 84's theme-token scope or Phase 85's naming-rename scope must be surfaced now for the operator to slot into a phase, rather than left for Phase 86 (re-score only) to discover mid-execution.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `npm ci` to restore missing `node_modules` for the TypeScript build step**
- **Found during:** Task 1 (server bring-up)
- **Issue:** `scripts/run-web-test.sh` failed at `dotnet run` with `EXEC : error : Cannot find module '...\typescript\bin\tsc'` — this worktree checkout had never run `npm install`/`npm ci` in `DeckFlow.Web/`, so the MSBuild `CompileTypeScriptAssets` target couldn't find the local `typescript` package.
- **Fix:** Ran `npm ci` in `DeckFlow.Web/` (restores exactly the lockfile-pinned versions — no new packages, no `package.json`/`package-lock.json` changes). Server then built and served successfully on `http://localhost:5173`.
- **Files modified:** None tracked (only `node_modules/`, which is gitignored build output — `git status --short` confirmed clean before and after).
- **Verification:** `curl http://localhost:5173/` returned 200; all 6 audited routes (`/`, `/sync`, `/lookup`, `/feedback`, `/help`, `/about`) returned 200.
- **Committed in:** N/A (no tracked file changed by this fix)

---

**Total deviations:** 1 auto-fixed (1 blocking — environment setup, no new dependency)
**Impact on plan:** Necessary to reach the PRIMARY (live-screenshot) audit path rather than falling back to code-review-only mode. No scope creep — no source file touched, no dependency added beyond what was already pinned in the committed lockfile.

## Issues Encountered

None beyond the `npm ci` blocker above, which was resolved without falling back to the code-review-only contingency path.

## User Setup Required

None — no external service configuration required. The headless test server and its background process were both stopped before this plan concluded (`pkill`/`kill -9` on the `dotnet.exe` PID, confirmed via a post-kill `curl` returning connection-refused).

## Next Phase Readiness

- Phase 84 (Theme Semantic-Token Migration) now has a concrete, evidence-backed worklist for its `--accent-strong` alias migration (58+4 call sites enumerated by file:line pattern) and can absorb the Typography literal-to-token cleanup as bundled scope in the same files.
- Phase 86 (UI Audit Re-Score) has a clean 18/24 baseline to re-score against, plus a target of 21/24 once Phase 84 lands — well clear of the >=20/24 bar.
- Three gaps (Spacing token scale, feedback-form double-submit, missing branded error page) remain unassigned and need an operator decision on which phase (or a new quick-task) absorbs them — flagged explicitly in `tasks/UI-REVIEW.md`'s "Out of Phase 84/85 scope" subsection so they are not silently discovered later.
- No blockers for proceeding to 82-03 (Wave 2 refactor execution, consuming `REFACTOR-TRIAGE.md`).

## Self-Check: PASSED

- `tasks/UI-REVIEW.md` exists: FOUND
- Commit `26f92192` exists: FOUND
- Commit `b47fb051` exists: FOUND
- `git diff --name-only HEAD -- DeckFlow.Web/wwwroot/css DeckFlow.Web/Views` printed nothing (verified before both commits): CONFIRMED

---
*Phase: 82-refactor-review-sweep-ui-baseline-audit*
*Completed: 2026-07-04*
