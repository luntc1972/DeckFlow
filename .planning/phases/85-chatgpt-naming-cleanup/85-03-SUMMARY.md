---
phase: 85-chatgpt-naming-cleanup
plan: 03
subsystem: ui
tags: [typescript, razor, deck-analysis, deck-comparison, cedh-meta-gap, sessionStorage, playwright, xunit]

# Dependency graph
requires:
  - phase: 85-01
    provides: pre-rename baseline + full chatgpt-* inventory (TS/Views/tests)
  - phase: 85-02
    provides: chatgpt-* -> prompt-* CSS class renames in site-common.css + theme forks (same selectors this plan's views now emit)
provides:
  - deck-sync.ts, moxfield-extension-bridge.ts, busy-indicator.ts fully renamed (0 chatgpt any-case)
  - DeckAnalysis/DeckComparison/CedhMetaGap/_WorkflowStepTabs Razor views renamed to prompt-* classes/attrs
  - D5 client<->server cache-key contract (6 literals) renamed in lockstep across TS + Razor in one commit
  - 4 e2e specs + 4 xUnit view-render tests updated to assert prompt-* selectors
affects: [85-04, 85-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "D5 lockstep rename: client (TS) and server (Razor emitter) sides of a sessionStorage cache-key contract renamed in the same commit to avoid silent desync"
    - "Established site copy convention ('your AI') reused instead of inventing new wording when a TS validation-message string had to lose a literal 'ChatGPT' substring"

key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/ts/deck-sync.ts
    - DeckFlow.Web/wwwroot/ts/moxfield-extension-bridge.ts
    - DeckFlow.Web/wwwroot/ts/busy-indicator.ts
    - DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml
    - DeckFlow.Web/Views/Deck/DeckComparison.cshtml
    - DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml
    - DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml
    - DeckFlow.Web/e2e/deck-analysis-render.spec.ts
    - DeckFlow.Web/e2e/print-analysis-results.spec.ts
    - DeckFlow.Web/e2e/print-button-appearance.spec.ts
    - DeckFlow.Web/e2e/ui-responsive.spec.ts
    - DeckFlow.Web.Tests/DeckAnalysisScoreViewTests.cs
    - DeckFlow.Web.Tests/DeckAnalysisPrintButtonViewTests.cs
    - DeckFlow.Web.Tests/DeckComparisonPrintButtonViewTests.cs
    - DeckFlow.Web.Tests/MetaGapViewRenderTests.cs

key-decisions:
  - "TS validation-message strings and comments that used the literal 'ChatGPT' (product-name casing, capital GPT) were reworded rather than left in place, because the plan's Task 1 verify gate is 'zero chatgpt any-case' in the 3 TS files (not kebab-only). Reworded using the site's pre-existing 'your AI' generic-platform phrasing (already used verbatim in DeckAnalysis.cshtml/CedhMetaGap.cshtml prose) so no new copy convention was invented."
  - "Razor view prose (page titles, ledes, meta descriptions) that names ChatGPT/Claude/Gemini as supported platforms was left byte-identical (D3 keep-list) since the Views verify gate is kebab-only ('chatgpt-') and the plan explicitly instructs keeping user-visible prose verbatim in these views."
  - "The two self-referential Phase-82 comments (busy-indicator.ts, moxfield-extension-bridge.ts) were reworded to past tense without repeating the banned 'chatgpt' substring at all (even in historical/descriptive context), since the TS zero-chatgpt-any-case gate does not carve out an exception for comments describing the rename itself."

requirements-completed: [AICLEAN-02, AICLEAN-03]

# Metrics
duration: 35min
completed: 2026-07-05
---

# Phase 85 Plan 03: TS + D5-Contract Views chatgpt-* -> prompt-* Rename Summary

**Renamed the full behavior-critical chatgpt-* surface (38 TS symbols, 63+ data-attr selectors, 6 D5 cache-key/sync-panel/storage-key contract literals) to prompt-* across deck-sync.ts, moxfield-extension-bridge.ts, busy-indicator.ts, the 3 workflow views, and the shared step-tabs partial — client and server sides of the D5 contract renamed atomically in one commit.**

## Performance

- **Duration:** ~35 min
- **Completed:** 2026-07-05
- **Tasks:** 2/2 completed
- **Files modified:** 15 (7 in Task 1, 8 in Task 2)

## Accomplishments
- Renamed all 38 `ChatGpt*`/`CHATGPT_*`/`chatGpt*` TS identifiers in `deck-sync.ts` to `Prompt*`/`PROMPT_*`/`prompt*` (functions, types, consts, dataset-derived camelCase properties).
- Renamed all `data-chatgpt-*` selectors (63 kebab occurrences in `deck-sync.ts` alone) to `data-prompt-*`.
- Renamed the 6 D5 contract literals (`chatgpt-packets`, `chatgpt-deck-comparison`, `chatgpt-cedh-meta-gap`, `chatgpt-deck-url`, `chatgpt-deck-text`, `decksync-chatgpt-ui-mode`) on **both** the TS-consuming side (`deck-sync.ts`, `moxfield-extension-bridge.ts`) and the Razor-emitting side (`DeckAnalysis.cshtml`, `DeckComparison.cshtml`, `CedhMetaGap.cshtml`) in a single commit — closing the silent-desync hazard the plan's threat model (T-85-03-02) flagged.
- Renamed the 3 `moxfield-extension-bridge.ts` cache-key `if`-checks that were previously "moved VERBATIM — no rename" (per its own header comment, now reworded past-tense).
- Reworded the 2 self-referential Phase-82 comments (`busy-indicator.ts`, `moxfield-extension-bridge.ts`) so no `chatgpt` substring survives even in historical/descriptive text.
- Reworded 8 `ChatGPT`-branded strings in `deck-sync.ts` (3 comments/console messages, 5 user-facing validation messages) using the site's existing "your AI" generic-platform convention, to satisfy the strict zero-chatgpt-any-case TS gate without inventing new copy.
- Renamed the `.chatgpt-*` classes/data-attrs in `DeckAnalysis.cshtml`, `DeckComparison.cshtml`, `CedhMetaGap.cshtml`, and the shared `_WorkflowStepTabs.cshtml` partial to `prompt-*`, matching the CSS attr-selector rename already landed in 85-02 (verified `data-prompt-cedh-reference-table` etc. present in `site-common.css`).
- Updated the 4 Playwright e2e specs and 4 xUnit view-render tests to assert the renamed `prompt-*`/`data-prompt-*` selectors.
- Left the `bracket-smoke.spec.ts` and `interactions.spec.ts` D3-keep prose (`ChatGPT`/`Claude`/`Gemini` regex assertions) untouched, per plan instruction.
- Left genuine `TargetAiPlatform = "ChatGPT"` test-data values in the 2 PrintButton test files and `DeckAnalysisScoreViewTests.cs` untouched (D3 keep-list — real platform enum value, not an identifier).

## Task Commits

1. **Task 1: Rename TS symbols/selectors AND the 3 workflow views + shared partial — D5 both-sides in ONE commit** - `6fb4b050` (refactor)
2. **Task 2: Update the 4 e2e specs + 4 xUnit view-render tests to the renamed selectors** - `32c209d2` (test)

_Note: no plan-metadata-only commit was made separately; this SUMMARY + STATE/ROADMAP/REQUIREMENTS updates land in the final docs commit per the execute-plan workflow._

## Files Created/Modified
- `DeckFlow.Web/wwwroot/ts/deck-sync.ts` - 38 symbols + 63 kebab selectors + 6 D5 literals renamed; 8 ChatGPT-branded strings reworded
- `DeckFlow.Web/wwwroot/ts/moxfield-extension-bridge.ts` - 3 cache-key if-checks renamed in lockstep; header comment reworded
- `DeckFlow.Web/wwwroot/ts/busy-indicator.ts` - self-referential comment reworded (no identifier changes needed — file had none)
- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` - classes/data-attrs + D5 emitting-side values (`prompt-packets`, `prompt-deck-url`, `prompt-deck-text`) renamed; prose kept verbatim
- `DeckFlow.Web/Views/Deck/DeckComparison.cshtml` - classes/data-attrs + D5 value (`prompt-deck-comparison`) renamed; prose kept verbatim
- `DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml` - classes/data-attrs + D5 value (`prompt-cedh-meta-gap`) renamed; prose kept verbatim
- `DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml` - `.chatgpt-step-*` classes renamed to `.prompt-step-*` (shared by DeckAnalysis/DeckComparison/CedhMetaGap and DeckPrimer — DeckPrimer's own class usages are out of scope for this plan, handled by 85-04 in the same wave)
- `DeckFlow.Web/e2e/deck-analysis-render.spec.ts`, `print-analysis-results.spec.ts`, `print-button-appearance.spec.ts`, `ui-responsive.spec.ts` - selectors updated to `data-prompt-*`/`.prompt-*`
- `DeckFlow.Web.Tests/DeckAnalysisScoreViewTests.cs`, `DeckAnalysisPrintButtonViewTests.cs`, `DeckComparisonPrintButtonViewTests.cs`, `MetaGapViewRenderTests.cs` - assertions updated to `prompt-score*`/`data-prompt-print`

## Decisions Made
See `key-decisions` in frontmatter above (TS prose rewording using existing "your AI" convention; Views prose left verbatim; comment rewording without repeating the banned substring).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug/Gate-compliance] Reworded 8 "ChatGPT"-branded strings in deck-sync.ts not explicitly enumerated in the plan's symbol/literal tables**
- **Found during:** Task 1
- **Issue:** The plan's Task 1 verify command checks `grep -rIni 'chatgpt' $TS` — zero matches, any case, across all 3 TS files. Beyond the enumerated 38 symbols + 63 kebab selectors + 6 D5 literals, `deck-sync.ts` also contained 8 occurrences of the literal `ChatGPT` (product-name casing) in 3 code comments/console messages and 5 user-facing validation-message strings (e.g. `'Paste the deck_profile JSON returned from ChatGPT before rendering the analysis summary.'`). These would have failed the automated verify gate if left as-is, and are not covered by the plan's D3 keep-list description of these files ("NONE of these files contain a genuine ChatGPT-model reference").
- **Fix:** Reworded all 8 using the site's own pre-existing "your AI" generic-platform phrasing (already used verbatim in `DeckAnalysis.cshtml`/`CedhMetaGap.cshtml` prose, e.g. "Ask your AI to return the `deck_profile` JSON") rather than inventing new copy. Internal comments/console messages changed `ChatGPT` -> `Prompt`.
- **Files modified:** `DeckFlow.Web/wwwroot/ts/deck-sync.ts`
- **Verification:** `grep -rIni 'chatgpt' deck-sync.ts` returns empty; scoped e2e + xUnit suites still pass (these strings have no test coverage per 85-RESEARCH.md, consistent with the plan's own note that deck-sync.ts's chatgpt-prefixed functions are untested).
- **Committed in:** `6fb4b050` (Task 1 commit)

**2. [Rule 1 - Gate-compliance] Reworded busy-indicator.ts's self-referential comment without repeating the literal `chatgpt` substring at all**
- **Found during:** Task 1
- **Issue:** The plan's own guidance said to "reword to past-tense prompt-*/wording" the self-referential Phase-82 comment ("fully chatgpt-*-free..."). A literal past-tense reword that still names the old prefix (e.g. "no longer has chatgpt-* identifiers") would itself trip the zero-chatgpt-any-case TS gate.
- **Fix:** Reworded to describe the same fact without using the banned substring at all: "contains no legacy AI-platform-prefixed identifiers, so it required no changes during the Phase 85 naming cleanup."
- **Files modified:** `DeckFlow.Web/wwwroot/ts/busy-indicator.ts`
- **Verification:** `grep -rIni 'chatgpt' busy-indicator.ts` returns empty.
- **Committed in:** `6fb4b050` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 1 / gate-compliance rewording, no behavior change)
**Impact on plan:** Both deviations were necessary to satisfy the plan's own automated verify command (`grep -rIni 'chatgpt' $TS` must be empty). No scope creep — no identifiers, attributes, or D5 contract values were touched beyond what the plan specified; only prose/comment wording was adjusted, using an existing site convention.

## Issues Encountered
None beyond the deviations above.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `deck-sync.ts`, `moxfield-extension-bridge.ts`, `busy-indicator.ts`, and the 3 D5-contract views + shared partial are fully clean of `chatgpt` (any case); the D5 contract is consistent client<->server.
- 85-04 (remaining views, including `DeckPrimer.cshtml` which also consumes `_WorkflowStepTabs.cshtml`) runs in the same wave and must complete before the wave-boundary convergence check (85-05) — `DeckPrimer.cshtml`'s own `.chatgpt-step-*` class usages are still unrenamed as of this plan (out of scope here) and will transiently mismatch the now-renamed `.prompt-step-*` classes emitted by `_WorkflowStepTabs.cshtml` until 85-04 lands.
- Scoped xUnit (39/39) and scoped Playwright e2e (40 passed, 4 pre-existing conditional skips) both green against the renamed surface.

---
*Phase: 85-chatgpt-naming-cleanup*
*Completed: 2026-07-05*

## Self-Check: PASSED

All 15 files_modified paths verified present on disk; commits `6fb4b050` and `32c209d2` verified present in git log.
