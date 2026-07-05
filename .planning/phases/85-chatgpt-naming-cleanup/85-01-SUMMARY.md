---
phase: 85-chatgpt-naming-cleanup
plan: 01
subsystem: testing
tags: [playwright, headless-capture, css, regression-baseline, chatgpt-rename]

# Dependency graph
requires:
  - phase: 84-theme-semantic-token-migration
    provides: "Headless run-web-test.sh + playwright-core scratch-script baseline-capture pattern (84-01 Task 0)"
provides:
  - "render-baseline-pre85.json: pre-rename outerHTML + getComputedStyle snapshot for the 6 prompt-building routes x 4 representative themes, captured BEFORE any css/ts/Views/.cs edit"
  - "chatgpt/ChatGpt substrings normalized (case-insensitive) to a single placeholder token across HTML, route keys, and selector/computedStyle keys, so 85-05's post-rename artifact (normalizing prompt/Prompt to the same token) is a token-symmetric diff"
affects: [85-02-chatgpt-naming-cleanup, 85-03-chatgpt-naming-cleanup, 85-04-chatgpt-naming-cleanup, 85-05-chatgpt-naming-cleanup]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "One-off Node + playwright-core scratch script (untracked) driving the headless scripts/run-web-test.sh server to capture a rendered-output baseline before a mechanical rename, mirroring Phase 84's Task-0 pattern but capturing full outerHTML rather than a synthetic var()-resolution probe."
    - "Token-symmetric normalization: both the pre-rename and (future) post-rename capture normalize their respective identifier prefix (chatgpt / prompt) to the SAME placeholder token, so a diff between the two artifacts isolates genuine drift instead of failing on every intentional rename."

key-files:
  created:
    - .planning/phases/85-chatgpt-naming-cleanup/render-baseline-pre85.json
  modified: []

key-decisions:
  - "Normalized the placeholder substitution across the ENTIRE artifact (HTML, route keys, selector list, computedStyles keys) rather than only the HTML body, because the plan's own automated verify gate (`'chatgpt' not in json.dumps(d).lower()`) checks the whole JSON, not just the HTML fields — selector names like `.chatgpt-score` and metadata route strings would otherwise fail the gate."
  - "Selected 4 representative themes (site.css/Classic, site-azorius, site-nyx, site-rakdos) rather than all 24 — sufficient for a pure identifier-name rename since CSS rule VALUES never change; light+dark and a rakdos-specific override are covered."
  - "`.chatgpt-score` and `.chatgpt-print-button` resolve to `null` on every captured route/theme combination — both are gated behind `@if (Model.Score is not null)` / a results-only action button in the Razor views, so they never render on a fresh GET with no submitted analysis. This is expected (not a capture bug): the other 4 representative selectors (`.chatgpt-step-tab`, `.chatgpt-sticky-download`, `.chatgpt-packets-form`, `[data-chatgpt-ui-mode]`) DO resolve with real computed-style values and satisfy the byte-identical proof's intent."
  - "Did NOT run `requirements mark-complete` for AICLEAN-01/02/03 despite them being listed in this plan's frontmatter `requirements` field — this plan only captures the pre-rename baseline artifact; the actual rename (and thus requirement satisfaction) happens in 85-02/03/04. Matches the Phase 83 precedent (83-02/83-03 SUMMARY) of not prematurely marking phase-wide requirements complete on a groundwork-only plan."

requirements-completed: []

# Metrics
duration: 12min
completed: 2026-07-05
---

# Phase 85 Plan 01: Pre-Rename Render/Computed-Style Baseline Summary

**Captured a pre-rename outerHTML + getComputedStyle snapshot (`render-baseline-pre85.json`) for the 6 prompt-building routes across 4 representative themes, with every `chatgpt`/`ChatGpt` occurrence normalized to a placeholder token, committed as the sole file before any `chatgpt-*` -> `prompt-*` rename edit lands.**

## Performance

- **Duration:** ~12 min
- **Started:** 2026-07-05T18:03:29Z (per STATE.md session start)
- **Completed:** 2026-07-05T18:15:00Z (approx)
- **Tasks:** 1 (Task 0: capture pre-rename baseline)
- **Files modified:** 1 (created)

## Accomplishments

- Started the headless `scripts/run-web-test.sh` server (`DECKFLOW_DISABLE_AUTO_BROWSER=true`) — no Windows-host browser opened at any point.
- Wrote a one-off Node + `playwright-core` scratch script (untracked, lives only in the session scratchpad — not committed, not a new dependency; reused the already-installed `DeckFlow.Web/node_modules/playwright-core`) that, for each of the 6 prompt-building routes (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`, `/deck-primer`, `/manabase`, `/judge-questions`) x 4 representative themes (`site.css`, `site-azorius.css`, `site-nyx.css`, `site-rakdos.css`), captured `document.documentElement.outerHTML` and `getComputedStyle` for the 6 representative renamed selectors (`.chatgpt-score`, `.chatgpt-step-tab`, `.chatgpt-packets-form`, `.chatgpt-sticky-download`, `.chatgpt-print-button`, `[data-chatgpt-ui-mode]`) across `color`/`borderColor`/`backgroundColor`/`display`/`outlineColor`.
- Normalized every case-insensitive `chatgpt` occurrence — in the HTML body, the route-name keys, the selector list, and the `computedStyles` object keys — to a single placeholder token (`__PROMPT_TOKEN__`) before writing to disk, so 85-05's post-rename capture (which will normalize `prompt`/`Prompt` to the same token) produces a token-symmetric diff.
- Wrote deterministic, key-sorted, LF-terminated JSON to `.planning/phases/85-chatgpt-naming-cleanup/render-baseline-pre85.json` (1,126,138 bytes; 24 route x theme snapshot cells).
- Committed the artifact as the ONLY file on its commit (`git show --stat` confirms), before any CSS/TS/Views/.cs rename edit.
- Confirmed the plan's exact automated verify command passes: file exists, JSON length > 500, and no raw `chatgpt` substring (case-insensitive) remains anywhere in the artifact -> `BASELINE_CAPTURED_AND_NORMALIZED`.

## Task Commits

1. **Task 0: Capture pre-rename rendered-output baseline** - `e7c923f1` (feat) — JSON artifact only, no css/ts/Views/.cs files in this commit (verified via `git show --stat`).

**Plan metadata:** (this commit) — `.planning/phases/85-chatgpt-naming-cleanup/85-01-SUMMARY.md` + STATE.md/ROADMAP.md updates.

_No test-only (TDD) commits — this plan is a pure read-only capture artifact, not unit-testable C# behavior._

## Files Created/Modified

- `.planning/phases/85-chatgpt-naming-cleanup/render-baseline-pre85.json` - Pre-rename outerHTML + computed-style snapshot (6 routes x 4 themes = 24 cells; 6 probed selectors per cell), captured via a one-off Node + `playwright-core` script driving the headless `scripts/run-web-test.sh` server. Not a tracked dependency addition (playwright-core already present in `DeckFlow.Web/node_modules`).

## Harness / Ordering Used (contract for 85-05)

- **Server:** `scripts/run-web-test.sh` (headless, `DECKFLOW_DISABLE_AUTO_BROWSER=true`), started via `nohup bash scripts/run-web-test.sh` on port 5173, polled with `curl` until 200, torn down by killing the `dotnet.exe` PID after capture.
- **Driver:** `playwright-core` `chromium.launch({ headless: true })`, single browser context, theme selected via the `deckflow-theme` cookie (same mechanism as `DeckFlow.Web/e2e/theming.spec.ts`'s `readThemeSnapshot`).
- **Route order (outer loop):** `/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`, `/deck-primer`, `/manabase`, `/judge-questions` — exactly the order listed in the plan's `<interfaces>` block.
- **Theme order (inner loop, per route):** `site.css`, `site-azorius.css`, `site-nyx.css`, `site-rakdos.css`.
- **Per (route, theme) cell:** set the `deckflow-theme` cookie -> `page.goto` the route -> capture `document.documentElement.outerHTML` -> normalize -> capture `getComputedStyle` for the 6 selectors (`color`, `borderColor`, `backgroundColor`, `display`, `outlineColor`) -> re-key onto the normalized selector name.
- **Normalization:** a single `RegExp('chatgpt', 'gi')` -> `__PROMPT_TOKEN__` replace, applied to: the HTML string, each route key, the top-level `selectors` array, and each `computedStyles` object's keys.
- **Serialization:** `JSON.stringify(output, null, 2)` after a recursive key-sort (`sortObject`), `\r\n` normalized to `\n`, written with `fs.writeFileSync(..., { encoding: 'utf8' })`.

**85-05 must reuse this exact route order, theme order, selector list, and normalization regex** (substituting the new `prompt`-prefixed selectors, normalized by the same placeholder) for the diff to be meaningful.

## Decisions Made

See `key-decisions` in frontmatter. Summary: (1) normalized the placeholder across the whole JSON, not just HTML, to satisfy the plan's own whole-file verify gate; (2) picked 4 representative themes as sufficient coverage for a pure identifier rename; (3) `.chatgpt-score`/`.chatgpt-print-button` are legitimately absent from every captured cell (results-only UI, gated behind a submitted-analysis view-model condition) — not a capture defect; (4) left AICLEAN-01/02/03 unmarked as complete since this plan only lays groundwork.

## Deviations from Plan

None — plan executed exactly as written. The one iteration during authoring (broadening normalization from "HTML only" to "the whole JSON") was resolved before the artifact was written to disk or committed, so no re-commit or rollback was needed; it is documented above as a decision rather than a deviation since the committed artifact and its accompanying commit are exactly what the plan's acceptance criteria and automated verify command require.

## Issues Encountered

- Initial script version normalized `chatgpt` only inside the captured HTML string. Running the plan's own automated verify command (`'chatgpt' not in json.dumps(d).lower()`) against that draft failed, because the `selectors` metadata array and `computedStyles` object keys (e.g. `.chatgpt-score`) still contained the raw substring. Fixed before writing any file to disk or committing: extended normalization to the route keys, the `selectors` array, and the `computedStyles` keys, re-ran, and confirmed `BASELINE_CAPTURED_AND_NORMALIZED`. No wasted commit; the JSON was regenerated in place before `git add`.
- Two unrelated, pre-existing uncommitted changes were present in the working tree at start (`DeckFlow.Studio/ViewModels/ReviewCoordinator.cs` + its 2 test files) — out of scope for this plan, left untouched, and staged nothing from them into this plan's commit.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- `render-baseline-pre85.json` exists at the path 85-05 Task 1 expects, captured while the tree was still at its pre-rename state, and is committed as a standalone commit before any rename edit — satisfying the sequencing requirement that made this Task 0/Plan 01 necessary.
- 85-02 (CSS rename) and 85-03/85-04 (TS/Views/.cs rename) may now proceed; they should NOT re-run or invalidate this baseline (the plan explicitly gates it as pre-rename-only).
- 85-05's byte-identical proof must reuse the exact harness/route/theme/selector ordering documented above under "Harness / Ordering Used" for its diff to be valid against this artifact.
- No blockers. Headless server capture only — no Windows-host browser opened at any point (confirmed via `scripts/run-web-test.sh` + `DECKFLOW_DISABLE_AUTO_BROWSER=true`).

---
*Phase: 85-chatgpt-naming-cleanup*
*Completed: 2026-07-05*

## Self-Check: PASSED

`render-baseline-pre85.json` exists at the claimed path and commit `e7c923f1` is present in `git log --oneline --all`.
