---
phase: 11
plan: 05
subsystem: frontend-typeahead
tags: [accessibility, aria, keyboard-nav, typescript, wdg-audit]
dependency_graph:
  requires: [11-01]
  provides: ["ARIA combobox + keyboard navigation on every typeahead input"]
  affects:
    - DeckFlow.Web/Views/Deck/SuggestCategories.cshtml
    - DeckFlow.Web/Views/Deck/DeckConvert.cshtml
    - DeckFlow.Web/Views/Deck/JudgeQuestions.cshtml
    - DeckFlow.Web/Views/Commander/CommanderCategories.cshtml
    - DeckFlow.Web/Views/Deck/CardLookup.cshtml
tech_stack:
  added: []
  patterns: ["WAI-ARIA 1.2 combobox-with-list-autocomplete", "aria-activedescendant navigation"]
key_files:
  created: [".planning/phases/11-web-design-guidelines-audit-fixes/11-05-SUMMARY.md"]
  modified: ["DeckFlow.Web/wwwroot/ts/df-typeahead.ts"]
decisions:
  - "No-wrap arrow navigation (ArrowDown stops at last option, ArrowUp stops at first) — matches common combobox UX, less surprising than wrap"
  - "tabindex=-1 on option buttons — focus stays on input; navigation via aria-activedescendant per ARIA 1.2 pattern"
  - "Auto-assign panel id in both createTypeaheadPanel and attachTypeahead (defensive) — ARIA wiring is robust whether consumers use the helper or build their own panel"
  - "mouseenter mirrors to keyboard highlight state — single source of truth so hover and ArrowDown can't disagree about which option is active"
  - "ArrowDown opens panel when closed — common combobox UX; triggers debounced fetch with current input value"
  - "Escape only preventDefault when something was actually dismissed — preserves native Escape semantics on already-closed panel"
metrics:
  duration_seconds: 164
  duration_human: "~2.7 min"
  completed_at: "2026-05-13T22:31:29Z"
  tasks_completed: 1
  files_modified: 1
requirements: [WDG-02]
---

# Phase 11 Plan 05: df-typeahead ARIA Combobox + Keyboard Navigation Summary

**One-liner:** Refactored `df-typeahead.ts` to implement the WAI-ARIA 1.2 combobox-with-list-autocomplete pattern (full ARIA attributes + ArrowDown/Up/Enter/Escape handlers) — one shared module fix lights up keyboard accessibility across all five typeahead consumer pages.

## Sweep 5 Lands

Per D-08 in `11-CONTEXT.md`, this is the single highest-leverage TypeScript fix in Phase 11 — the shared module is invoked from `SuggestCategories`, `DeckConvert`, `JudgeQuestions`, `CommanderCategories`, and `CardLookup`. Keyboard users previously could not pick suggestions from any of those five inputs; now they can on all five with one fix, zero consumer view changes.

## Tasks Completed

| Task | Name | Commit | Files |
| ---- | ---- | ------ | ----- |
| 1 | Add ARIA combobox attributes + keyboard handlers to df-typeahead.ts | `71e3b6e` | `DeckFlow.Web/wwwroot/ts/df-typeahead.ts` |

## What Changed

### ARIA combobox attributes (applied at attach-time)

On the input element:
- `role="combobox"` — declares the input as the combobox widget.
- `aria-autocomplete="list"` — announces "options are filtered as you type".
- `aria-expanded` — flips `"true"` / `"false"` as the panel opens and closes.
- `aria-controls` — set to the suggestion panel's id (auto-generated if absent).
- `aria-activedescendant` — present only while a keyboard-navigation option is highlighted; references the highlighted option's id.

On each suggestion button rendered into the panel:
- `role="option"` — declares it a combobox option (panel already had `role="listbox"`).
- Stable id `df-typeahead-panel-{N}-option-{I}` — used as the `aria-activedescendant` target.
- `aria-selected="true"` on the currently highlighted option, `"false"` otherwise.
- `tabindex="-1"` — focus stays on the input; movement happens via `aria-activedescendant` per the ARIA 1.2 pattern.

### Keyboard handlers (keydown on the input)

- **ArrowDown** — if the panel is closed, fire the existing debounced fetch (opens the list). If open with options, move highlight to next option (no-wrap; stays at last). `preventDefault()` to suppress caret movement.
- **ArrowUp** — symmetric to ArrowDown but in reverse. No-wrap; stays at first option. `preventDefault()`.
- **Enter** — if a suggestion is highlighted, commit the selection via the existing `onPick` callback and close the panel. `preventDefault()` to suppress accidental form submission. If nothing highlighted, falls through to native form behavior.
- **Escape** — if the panel is open, close it, clear `aria-activedescendant`, set `aria-expanded="false"`, and `preventDefault()`. If the panel was already closed, native Escape semantics are preserved (no `preventDefault`).

### Cross-modal consistency

Mouse `mouseenter` now mirrors the keyboard highlight state — hovering and arrow-keying both feed the same `highlightedIndex` and `setHighlight()` path, so hover and keyboard can never disagree about which option is active. Mouse `mousedown` selection (existing behavior) is preserved.

### API surface preservation

`attachTypeahead` and `createTypeaheadPanel` function signatures are unchanged. The five consumer modules (`card-search.ts`, `commander-search.ts`, `card-lookup.ts`, `judge-questions.ts`, `deck-sync.ts`) compile and behave identically without any edit. The only addition: `createTypeaheadPanel` now auto-assigns a stable `id` to the panel (so `aria-controls` has a real target), and `attachTypeahead` assigns one defensively if the caller supplied a panel without an id.

## Verification

- **`dotnet build DeckFlow.sln --configuration Release`** — exits 0, **0 warnings, 0 errors** (TypeScript strict-clean under `strict: true`, `module: "none"`, ES2017 target). MSBuild `CompileTypeScriptAssets` target produced `wwwroot/js/df-typeahead.js` (10,662 bytes).
- **All acceptance grep assertions pass:**
  - `role="combobox"` (set via `'combobox'` literal in `setAttribute`) — ✓ (`'combobox'`)
  - `aria-autocomplete`, `aria-expanded`, `aria-controls`, `aria-activedescendant` — ✓ all present
  - `role="option"` (set via `'option'` literal) — ✓
  - `"ArrowDown"`, `"ArrowUp"`, `"Enter"`, `"Escape"` (double-quoted key string literals) — ✓ all four
- **No consumer `.cshtml` modified** — `git diff --name-only` shows only `DeckFlow.Web/wwwroot/ts/df-typeahead.ts`.
- **No UAT in this plan** per D-03 — phase-end UAT will keyboard-test all five consumer typeahead inputs.

## Deviations from Plan

None — plan executed exactly as written. All acceptance criteria, decision constraints (no consumer view edits, no new dependencies, strict-clean TypeScript), and API preservation requirements were met on first pass.

The only minor self-correction during execution: initial implementation used single-quoted `'ArrowDown'` etc. for key comparisons. The acceptance criteria explicitly grep for double-quoted forms (`"ArrowDown"`), so the four key string literals were normalized to double quotes — TypeScript treats `'x' === "x"` identically, so this is a stylistic change with zero runtime impact.

## Decisions Made

1. **No-wrap arrow navigation** — pressing ArrowDown on the last option stays at the last; ArrowUp on the first stays at the first. Per CONTEXT.md Claude's Discretion (D-08), no-wrap is the most common combobox UX pattern and avoids "I pressed Down to go forward but I went to the top" surprises.

2. **tabindex=-1 on option buttons** — required by the ARIA 1.2 combobox pattern. Focus must stay on the input so screen readers correctly announce `aria-activedescendant` changes; the rendered options must not be in the tab order.

3. **Defensive panel id assignment in both `createTypeaheadPanel` and `attachTypeahead`** — the second assignment is technically redundant when consumers use the helper, but it makes `attachTypeahead` robust against future panels constructed in other ways. Cheap insurance; the panelIdCounter is module-scoped so ids stay unique.

4. **`mouseenter` mirrors to keyboard highlight** — without this, hovering over option B would visually highlight it but `aria-activedescendant` would still point at option A from the keyboard. Mirroring keeps the two in sync at the cost of two extra DOM ops per hover.

5. **ArrowDown opens the panel when closed** — standard combobox UX. The handler triggers the existing debounced fetch (same path as `input` / `focus`), which honors the `minChars` floor and reuses the current input value.

6. **Escape uses conditional `preventDefault`** — only call it when the handler actually dismissed something. If the panel was already closed, native Escape behavior (e.g., closing a modal, blurring) is preserved.

## Known Stubs

None. The module's runtime behavior is fully wired; no placeholder values or dead code paths were introduced.

## Self-Check: PASSED

- `[ -f DeckFlow.Web/wwwroot/ts/df-typeahead.ts ]` — FOUND
- `[ -f DeckFlow.Web/wwwroot/js/df-typeahead.js ]` — FOUND (build output, gitignored)
- `git log --oneline --all | grep -q 71e3b6e` — FOUND (Task 1 commit)
- No unintended files modified — git status shows only this SUMMARY pending
- All eight acceptance-criteria grep assertions pass

## Cross-References

- Phase context: `.planning/phases/11-web-design-guidelines-audit-fixes/11-CONTEXT.md` §D-08
- Audit source: `.planning/quick/260513-wdg-web-design-guidelines-audit-findings/260513-wdg-FINDINGS.md` §"P1 finding B" (lines 45-51) and §"Sweep 5" (lines 282-286)
- Requirement: `.planning/REQUIREMENTS.md` WDG-02 — Web Design Guidelines audit Sweep 5

## Phase 11 Success Criterion #2

Satisfied (pending end-of-phase UAT verification): All five consumer typeahead inputs now expose the full ARIA combobox pattern and respond to ArrowDown/Up/Enter/Escape from the keyboard, via this single shared-module fix.
