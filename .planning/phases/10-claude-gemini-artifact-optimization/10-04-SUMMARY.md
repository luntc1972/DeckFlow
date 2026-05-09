---
phase: 10-claude-gemini-artifact-optimization
plan: 04
subsystem: ui
tags: [typescript, browser, hardening, debounce, form-state]

requires:
  - phase: 09-bracket-ux-ai-selector-foundation
    provides: registerChatGptDownloadDebounce + wireChatGptZipUpload + skipPersistence flag (added in v1.2 polish bundle, commits ce043df and 13bb656)
provides:
  - Module-scope CHATGPT_DOWNLOAD_DEBOUNCE_MS const documenting the b09fd46 / sticky-busy-overlay constraint
  - Auto-clear setTimeout that prevents skipPersistence flag from sticking after a transient upload failure
affects: []

tech-stack:
  added: []
  patterns:
    - "Magic-number lift: when a literal is part of a tradeoff worth documenting, lift to a named constant with a comment block — not just for symmetry."
    - "Self-clearing UI state flags: when a flag's lifetime is supposed to be 'until next navigation', a guarded auto-clear timeout is the smallest fix that prevents stuck-state on transient failure."

key-files:
  created: []
  modified:
    - DeckFlow.Web/wwwroot/ts/deck-sync.ts

key-decisions:
  - "D-14 picked option (a) — named constant + comment — over option (b) wiring re-enable to a stronger response signal. Option (b) risks re-introducing the b09fd46 sticky-busy-overlay regression that data-no-busy was originally added to fix; option (a) is the smallest correct fix."
  - "D-15 picked the auto-clear timeout approach over clear-on-error or single-cycle scoping. The auto-clear has the simplest mental model and tolerates any error path the upload POST might take (network, server 500, browser cancel, abort). 30s is comfortably longer than any happy-path upload+navigate."
  - "Auto-clear callback guards on `if (form.dataset.skipPersistence === 'true')` so a deliberate later setter to a different value (or a delete by another code path) is not clobbered by the timeout."
  - "Comment in the CHATGPT_DOWNLOAD_DEBOUNCE_MS block references the b09fd46 regression by description (the phrase 'sticky-busy-overlay'), not by commit hash — keeps source comment decoupled from git history."

patterns-established:
  - "Magic-number lift with explanatory comment: prefer when the literal carries a tradeoff worth explaining (response timing, regression mitigation), not for every numeric constant."
  - "Auto-clear guard pattern: `if (form.dataset.flag === 'true') { delete form.dataset.flag }` inside a setTimeout — narrow enough to survive other code-path overrides."

requirements-completed: []  # No AISEL REQ-ID mapping — sourced from CONTEXT.md scope addendum 2026-05-09 (D-14, D-15).

duration: 30min (Codex full gpt-5.4, single pass + QA twice; small surface)
completed: 2026-05-09
---

# Phase 10-04: D-14 Debounce Hardening + D-15 skipPersistence Auto-Clear

**Magic 3000ms literal lifted to a documented module-scope constant; skipPersistence flag now self-clears 30s after set so a transient upload failure cannot silently disable form-state persistence for the rest of the page lifetime.**

## Performance

- **Duration:** ~30 minutes wallclock (Codex full gpt-5.4, one dispatch + two QA passes)
- **Completed:** 2026-05-09
- **Tasks:** 2 (D-14 lift + D-15 auto-clear)
- **Files modified:** 1
- **Lines changed:** +24 / -7

## Accomplishments

- Closes both LOW-severity findings from the Codex code review of the v1.2 polish bundle (commits `ce043df` and `13bb656`).
- D-14: `CHATGPT_DOWNLOAD_DEBOUNCE_MS` const introduced at module scope with a comment block documenting the Render-cold-response timing tradeoff, the duplicate-POST risk on early re-enable, the user-facing annoyance on late re-enable, AND that `data-no-busy` MUST stay (regression mitigation by description, not commit hash).
- D-15: `wireChatGptZipUpload` now schedules a guarded 30-second auto-clear `setTimeout` immediately after `form.dataset.skipPersistence = 'true'`. Callback `delete`s the dataset entry only if it's still `'true'`, so a deliberate later override by another code path is not clobbered.
- The b09fd46 fix (`data-no-busy` attribute on download buttons + `registerBusyIndicator`'s short-circuit for that attribute) is preserved verbatim — sticky-busy-overlay regression cannot recur.
- The other two `form.dataset.skipPersistence = 'true'` setters in this file (lines 1230, 1469) are intentional one-shot suppressions in unrelated form-toggle flows and are unchanged.

## Task Commits

Single atomic commit captures both tasks since they touch the same file in adjacent areas:

1. **Both tasks** — `b292cfe` (feat)

**Plan metadata:** TBD on next docs commit

## Files Created/Modified

- `DeckFlow.Web/wwwroot/ts/deck-sync.ts` — `+24 / -7`

## Decisions Made

- **Option (a) for D-14** (named const + comment) over option (b) (response-signal-coupled re-enable). Option (b) risks re-introducing the b09fd46 sticky-busy-overlay regression. Option (a) is the smallest correct fix and the comment block makes the constraint explicit so future maintainers don't try to "improve" the timing-based approach.
- **Auto-clear timeout for D-15** over clear-on-error (would require wrapping the form submit's network behavior — bigger surface) or single-cycle scoping (would require entanglement with `persistFormState`'s `if (form.dataset.skipPersistence === 'true') return;` consumer). Timeout is the smallest correct fix.
- **30 seconds** as the auto-clear window. Longer than any realistic upload+navigate happy path. If the upload navigates the page, the page is gone before the timeout fires. If the upload errors, the flag clears 30s later. The narrow guard ensures unrelated code paths aren't clobbered.
- **Reference the regression by phrase, not commit hash** in the comment block: "sticky-busy-overlay regression". Decouples source comment from git history so a future history rewrite doesn't orphan the reference.

## Deviations from Plan

None — plan executed exactly as written. Two-task scope, single file. Cosmetic blank-line tightening inside `registerChatGptDownloadDebounce` and `wireChatGptZipUpload` (Codex's idiomatic preference) is incidental and does not affect behavior.

## Issues Encountered

- Sandbox `dotnet build` Roslyn named-pipe permissions surfaced again; resolved with `-m:1 -p:UseSharedCompilation=false` per the lesson from 10-01. Local WSL session compiles cleanly without the extra flags.

## Next Phase Readiness

Plans 10-02 and 10-03 unaffected by this work — different files entirely (services + parsers vs browser TS). 10-04 was scheduled in Wave 1 alongside 10-01 specifically because the two are independent.

The commit is on `v1.2` branch and pushed to origin. Code review of this and the parent v1.2 polish bundle was already performed by Codex on 2026-05-09 (1 verdict each: PASS / PASS-with-nits / NEEDS-FIX) and surfaced these two findings; Phase 10-04 closes both.

---
*Phase: 10-claude-gemini-artifact-optimization*
*Completed: 2026-05-09*
