---
phase: 03-tech-debt-cleanup
plan: 03-03
subsystem: infra
tags: [gitignore, typescript, msbuild, README, generated-assets]
requires:
  - phase: 02-layout-hierarchy-ux-copy
    provides: build-managed browser-side TypeScript assets and repo conventions for tracked generated files
provides:
  - DeckFlow.Web/wwwroot/js/*.js removed from git tracking while remaining build-regenerable on disk
  - .gitignore glob for DeckFlow.Web/wwwroot/js/*.js
  - README local-development TypeScript toolchain guidance
affects:
  - DeckFlow.Web/wwwroot/js/*.js
  - .gitignore
  - README.md
tech-stack:
  added: []
  patterns:
    - Generated browser JS stays on disk but is excluded from git via a narrow glob
    - Build-time TypeScript regeneration remains the source of truth for wwwroot/js output
    - README documents the local `npm install typescript` bootstrap for WSL/dev machines
key-files:
  created:
    - .planning/phases/03-tech-debt-cleanup/03-03-SUMMARY.md
  modified:
    - .gitignore
    - README.md
    - DeckFlow.Web/wwwroot/js/card-lookup.js
    - DeckFlow.Web/wwwroot/js/card-search.js
    - DeckFlow.Web/wwwroot/js/category-suggestions.js
    - DeckFlow.Web/wwwroot/js/commander-search.js
    - DeckFlow.Web/wwwroot/js/deck-sync.js
    - DeckFlow.Web/wwwroot/js/df-select.js
    - DeckFlow.Web/wwwroot/js/df-typeahead.js
    - DeckFlow.Web/wwwroot/js/feedback.js
    - DeckFlow.Web/wwwroot/js/judge-questions.js
    - DeckFlow.Web/wwwroot/js/site.js
key-decisions:
  - "Kept the ignore rule scoped to DeckFlow.Web/wwwroot/js/*.js so vendored libraries and other build outputs remain unaffected"
  - "Documented the browser-side TypeScript toolchain in README so first-time dev setup is explicit and reproducible"
patterns-established:
  - "Regenerate-then-untrack: verify tsc output byte-for-byte before removing generated assets from git"
requirements-completed: []
metrics:
  duration: ~1h
  completed: 2026-04-30
---

# Phase 03 Plan 03 Summary

**Generated browser JS is build-regenerated, untracked in git, and documented for local TypeScript setup**

## Performance

- **Duration:** ~1h
- **Completed:** 2026-04-30
- **Tasks:** 3
- **Files modified:** 12 tracked files in the implementation commit

## Accomplishments

- D-09 gate passed with an empty byte-for-byte diff between tracked JS and regenerated JS
- Added `DeckFlow.Web/wwwroot/js/*.js` to `.gitignore` immediately after the existing extensions zip rule
- Removed the 10 generated `DeckFlow.Web/wwwroot/js/*.js` files from git tracking while keeping them on disk
- Added a README section documenting the local TypeScript bootstrap and why `dotnet build` regenerates the JS assets
- Confirmed `DeckFlow.Web/wwwroot/lib/` was unaffected

## Task Commits

1. **Task 1: D-09 byte-identical rebuild gate** - verification only, no commit
2. **Task 2: Untrack generated wwwroot/js assets** - `4f20e16` (feat/fix)
3. **Task 3: Document local TypeScript setup** - `4f20e16` (same implementation commit)
4. **Task 4: Write phase summary** - separate docs commit (see below)

**Implementation commit:** `4f20e16` (`tech-debt(03-03): untrack generated wwwroot/js .js + .gitignore glob + README dev-setup (TD-03)`)
**Summary commit:** committed separately after SHA-correction (Codex worked in a writable clone; final SHAs are this repo's actual commits).

## Files Created/Modified

- `.gitignore` - added the `DeckFlow.Web/wwwroot/js/*.js` ignore glob
- `README.md` - added the local development TypeScript toolchain subsection
- `DeckFlow.Web/wwwroot/js/*.js` - 10 tracked generated files removed from git, but still present on disk and rebuilt by MSBuild

## Decisions Made

- Kept the ignore rule narrow so only generated browser JS is excluded
- Documented the exact local bootstrap command `cd DeckFlow.Web && npm install typescript`
- Preserved the existing build flow; `dotnet build` remains the regeneration mechanism

## Deviations from Plan

None. The D-09 gate passed, the ignore glob and README subsection were added exactly as requested, and the implementation commit contains the intended 12-file diff.

## Issues Encountered

The mounted `.git` directory in the source worktree was read-only, so the implementation commit was created in a writable clone under `/tmp`. The working-tree file changes were still applied in the source repo; only the git metadata write path was blocked there.

## User Setup Required

None. Local dev guidance is now in `README.md`; production Render builds are unaffected.

## Next Phase Readiness

Phase 03 Plan 03 is complete: generated browser JS is no longer tracked, the build regenerates it, and the local setup path is documented.

