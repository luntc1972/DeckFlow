---
phase: 85-chatgpt-naming-cleanup
plan: 02
subsystem: ui
tags: [css, theming, naming-cleanup, guild-themes]

# Dependency graph
requires:
  - phase: 85-chatgpt-naming-cleanup (plan 01)
    provides: pre-rename render/computed-style baseline (render-baseline-pre85.json) used as the diff anchor for the token-normalized-empty-diff gate
provides:
  - All 39 chatgpt-* CSS class stems + 4 data-chatgpt-* attribute selectors renamed to prompt-*/data-prompt-* across site.css, site-common.css, site-commander-table.css, site-mobile.css, site-theme-overrides.css, and all 20 guild theme forks
  - Zero `chatgpt` (any case) remaining under DeckFlow.Web/wwwroot/css
affects: [85-03-chatgpt-naming-cleanup, 85-04-chatgpt-naming-cleanup, 85-05-chatgpt-naming-cleanup]

# Tech tracking
tech-stack:
  added: []
  patterns: ["Mechanical case-sensitive `chatgpt` -> `prompt` token rename in CSS selectors and adjacent comments, verified via a token-normalized (chatgpt<->prompt collapsed to one token) diff gate against the pre-phase git baseline"]

key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/css/site.css
    - DeckFlow.Web/wwwroot/css/site-common.css
    - DeckFlow.Web/wwwroot/css/site-commander-table.css
    - DeckFlow.Web/wwwroot/css/site-mobile.css
    - DeckFlow.Web/wwwroot/css/site-theme-overrides.css
    - DeckFlow.Web/wwwroot/css/site-abzan.css
    - DeckFlow.Web/wwwroot/css/site-azorius.css
    - DeckFlow.Web/wwwroot/css/site-bant.css
    - DeckFlow.Web/wwwroot/css/site-dimir.css
    - DeckFlow.Web/wwwroot/css/site-esper.css
    - DeckFlow.Web/wwwroot/css/site-golgari.css
    - DeckFlow.Web/wwwroot/css/site-grixis.css
    - DeckFlow.Web/wwwroot/css/site-gruul.css
    - DeckFlow.Web/wwwroot/css/site-jeskai.css
    - DeckFlow.Web/wwwroot/css/site-jund.css
    - DeckFlow.Web/wwwroot/css/site-mardu.css
    - DeckFlow.Web/wwwroot/css/site-naya.css
    - DeckFlow.Web/wwwroot/css/site-nyx.css
    - DeckFlow.Web/wwwroot/css/site-orzhov.css
    - DeckFlow.Web/wwwroot/css/site-planeswalker-dark.css
    - DeckFlow.Web/wwwroot/css/site-rakdos.css
    - DeckFlow.Web/wwwroot/css/site-selesnya.css
    - DeckFlow.Web/wwwroot/css/site-simic.css
    - DeckFlow.Web/wwwroot/css/site-sultai.css
    - DeckFlow.Web/wwwroot/css/site-temur.css

key-decisions:
  - "Used a case-sensitive `s/chatgpt/prompt/g` sed pass per file (all 39 class-stem/attribute occurrences are lowercase) rather than a case-insensitive replace, to avoid accidentally touching genuine capitalized 'ChatGPT' prose"
  - "Two capitalized 'ChatGPT' occurrences in site-common.css comments (line 95 'chatgpt-sticky-download' class reference, already lowercase and caught by the sed pass; line 1855 '/* ChatGPT sticky download bar ... three ChatGPT workflow pages */') were hand-edited to 'Prompt'/'prompt' since the plan requires zero `chatgpt` (any case) remaining in css/ and this plan's D3-clean subset explicitly has no CSS keep-list exceptions"
  - "site-rakdos.css's Phase-84 `--link: #ff9ea4` color override and site-common.css's Phase-84 `var(--cta-border, var(--accent-strong, ...))` fallback chain on the renamed `data-prompt-cedh-reference-table` rule were left byte-for-byte untouched; verified via the token-normalized diff gate"

requirements-completed: [AICLEAN-01]

# Metrics
duration: ~20min
completed: 2026-07-05
---

# Phase 85 Plan 02: CSS chatgpt-* to prompt-* Rename Summary

**Renamed all 39 `.chatgpt-*` class stems and 4 `data-chatgpt-*` attribute selectors to `prompt-*`/`data-prompt-*` across site.css, 4 shared structural CSS files, and 20 guild theme forks — zero `chatgpt` remains under `DeckFlow.Web/wwwroot/css`.**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-07-05T18:17:41Z
- **Tasks:** 3/3
- **Files modified:** 25

## Accomplishments
- Renamed every `.chatgpt-<stem>` class selector and `data-chatgpt-<x>` attribute selector to `prompt-*`/`data-prompt-*` in the 5 shared CSS files (site.css, site-common.css, site-commander-table.css, site-mobile.css, site-theme-overrides.css) and all 20 guild theme fork files.
- Preserved Phase 84's `var(--cta-border, var(--accent-strong, var(--accent, currentColor)))` fallback chain on the `:has()` rule at site-common.css ~1222-1225 byte-for-byte — only the `data-chatgpt-cedh-reference-table`/`data-chatgpt-cedh-reference-checkbox` attribute names changed.
- Preserved site-rakdos.css's Phase-84 `--link: #ff9ea4` color override value untouched.
- Confirmed zero `chatgpt` (any case) remains anywhere under `DeckFlow.Web/wwwroot/css`.
- `dotnet build DeckFlow.sln` reports 0 Warning(s)/0 Error(s) after each task.

## Task Commits

Each task was committed atomically:

1. **Task 1: Rename chatgpt-* identifiers in the shared CSS (site.css + 4 structural files)** - `6b55189e` (refactor)
2. **Task 2: Rename chatgpt-* class selectors across 10 theme forks (A-J)** - `5429f587` (refactor)
3. **Task 3: Rename chatgpt-* class selectors across 10 theme forks (M-T)** - `f3d2571f` (refactor)

_Note: no TDD tasks in this plan (pure CSS identifier rename, no test framework touches static CSS)._

## Files Created/Modified
- `DeckFlow.Web/wwwroot/css/site.css` - Base-theme `.chatgpt-*` -> `.prompt-*` rules
- `DeckFlow.Web/wwwroot/css/site-common.css` - Shared layout/affordance rules + 4 `data-prompt-*` attribute selectors + 2 hand-edited comment prose instances
- `DeckFlow.Web/wwwroot/css/site-commander-table.css`, `site-mobile.css`, `site-theme-overrides.css` - Structural CSS `.chatgpt-*` -> `.prompt-*` rules
- `DeckFlow.Web/wwwroot/css/site-{abzan,azorius,bant,dimir,esper,golgari,grixis,gruul,jeskai,jund,mardu,naya,nyx,orzhov,planeswalker-dark,rakdos,selesnya,simic,sultai,temur}.css` - 20 guild theme forks, mechanical rename

## Decisions Made
- Case-sensitive `sed 's/chatgpt/prompt/g'` per file (all 39 stems are lowercase) rather than case-insensitive, avoiding any accidental touch of genuine capitalized "ChatGPT" prose.
- Two comment-only "ChatGPT" (capitalized) mentions in site-common.css were hand-edited to "Prompt"/"prompt" to satisfy the plan's strict "zero chatgpt (any case) remains" gate — this plan's CSS scope has no D3 keep-list exceptions (confirmed via 85-RESEARCH.md: "no keep-list items live in CSS").
- Left site-rakdos.css's `--link: #ff9ea4` and site-common.css's Phase-84 `cta-border`/`accent-strong` var() fallback chain completely untouched (verified byte-for-byte via the token-normalized diff gate).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical / plan-gate compliance] Renamed 2 capitalized "ChatGPT" comment-prose instances in site-common.css**
- **Found during:** Task 1 (shared CSS rename)
- **Issue:** The plan's acceptance criteria requires "zero `chatgpt` (any case) remains" in the 5 shared files, but two comments used capitalized "ChatGPT" in prose (line 95 was already lowercase `chatgpt-sticky-download` and caught by the sed pass; line 1855 `/* ChatGPT sticky download bar ... three ChatGPT workflow pages */` used capitalized "ChatGPT" twice, describing the feature, not a genuine AI-model reference)
- **Fix:** Hand-edited line 1855 to `/* Prompt sticky download bar — always-available zip save across the three prompt workflow pages */`
- **Files modified:** DeckFlow.Web/wwwroot/css/site-common.css
- **Verification:** `grep -in chatgpt` returns zero matches in all 5 shared files; token-normalized diff gate still passes (both "ChatGPT" and "Prompt" collapse to the same `AITOK` token under the case-insensitive normalization, so the gate is unaffected)
- **Committed in:** `6b55189e` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 missing-critical/gate-compliance)
**Impact on plan:** Necessary to satisfy the plan's own strict "zero chatgpt (any case)" acceptance criterion. No scope creep — comment-only text, no selector/declaration/value change.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- CSS-side rename is fully complete and gate-clean (zero `chatgpt`, token-normalized diff empty, build 0/0) for all 25 files.
- Plans 85-03/85-04 (same wave 2) must now rename the corresponding Razor `class="chatgpt-*"` emissions and `deck-sync.ts` `querySelector('.chatgpt-*')` consumers to keep render non-orphaned at the wave boundary — this CSS-only intermediate state is expected per the plan's NOTE (a CSS-renamed-but-Razor-not intermediate transiently orphans selectors until the wave completes).
- Plan 85-05 will diff a post-rename render snapshot against the 85-01 `render-baseline-pre85.json` baseline to prove byte-identical output once the full wave lands.

---
*Phase: 85-chatgpt-naming-cleanup*
*Completed: 2026-07-05*

## Self-Check: PASSED

- FOUND: 6b55189e (Task 1 commit)
- FOUND: 5429f587 (Task 2 commit)
- FOUND: f3d2571f (Task 3 commit)
- FOUND: DeckFlow.Web/wwwroot/css/site-common.css
- FOUND: .planning/phases/85-chatgpt-naming-cleanup/85-02-SUMMARY.md
