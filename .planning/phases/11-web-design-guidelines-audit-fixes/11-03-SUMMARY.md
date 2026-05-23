---
phase: 11
plan: 03
subsystem: web-views
tags: [razor, html-validity, wdg-audit, sweep]
requires: [11-01]
provides: [valid-selected-attribute-rendering-across-deck-sync-convert-suggest-and-admin-harvest-views]
affects: [DeckFlow.Web/Views/Deck/DeckSync.cshtml, DeckFlow.Web/Views/Deck/DeckConvert.cshtml, DeckFlow.Web/Views/Deck/SuggestCategories.cshtml, DeckFlow.Web/Views/AdminHarvest/Index.cshtml]
tech_stack_added: []
patterns_introduced: []
key_files_created: []
key_files_modified:
  - DeckFlow.Web/Views/Deck/DeckSync.cshtml
  - DeckFlow.Web/Views/Deck/DeckConvert.cshtml
  - DeckFlow.Web/Views/Deck/SuggestCategories.cshtml
  - DeckFlow.Web/Views/AdminHarvest/Index.cshtml
decisions:
  - "Mirrored v1.2 commit 32bf620 ternary pattern byte-for-byte: `selected=\"@(predicate ? \"selected\" : null)\"` so Razor emits `selected=\"selected\"` when true and omits the attribute when false."
  - "Applied only at the 12 D-09 audit-driven sites; did not introduce the pattern at any other locations in scope of this sweep."
metrics:
  duration: "2m 24s"
  completed: 2026-05-13
  tasks: 1
  files_modified: 4
  commits: 1
requirements_completed: [WDG-07]
---

# Phase 11 Plan 03: Razor `selected=` ternary sweep — Summary

**One-liner:** Propagated the v1.2 commit `32bf620` Razor `selected=` ternary pattern across DeckSync, DeckConvert, SuggestCategories, and AdminHarvest/Index — replacing 13 buggy `selected="@(boolean)"` sites (which rendered as invalid `selected="True"`) with the HTML5-valid `selected="@(predicate ? "selected" : null)"` form.

## What Was Done

Task 1 — single-task plan executed atomically:

- Updated `DeckFlow.Web/Views/Deck/DeckSync.cshtml` lines 51–54, 61–62, 68–70, 93–94, 128–129 (13 `<option>` elements across SyncDirection, MatchMode, CategorySyncMode, MoxfieldInputSource, and ArchidektInputSource selectors).
- Updated `DeckFlow.Web/Views/Deck/DeckConvert.cshtml` lines 32–33, 39–40, 46–47 (6 `<option>` elements across SourceFormat, TargetFormat, InputSource).
- Updated `DeckFlow.Web/Views/Deck/SuggestCategories.cshtml` lines 40–43, 88–89 (6 `<option>` elements across CategorySuggestionMode and reference-deck ArchidektInputSource).
- Updated `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` lines 40, 90 (2 `<option>` elements for run-now duration and schedule interval).

Every site now follows: `selected="@(<existing predicate> ? "selected" : null)"`. The boolean predicate text was preserved verbatim — only the attribute-value wrapping changed. Pattern matches the canonical v1.2 `32bf620` ChatGPT-views fix byte-for-byte.

## Files Modified

| File | Sites Fixed | Notes |
|------|-------------|-------|
| `DeckFlow.Web/Views/Deck/DeckSync.cshtml` | 13 | All five D-09 fix regions (51–54, 61–62, 68–70, 93–94, 128–129) updated. |
| `DeckFlow.Web/Views/Deck/DeckConvert.cshtml` | 6 | All three D-09 fix regions (32–33, 39–40, 46–47) updated. |
| `DeckFlow.Web/Views/Deck/SuggestCategories.cshtml` | 6 | Both D-09 fix regions (40–43, 88–89) updated. |
| `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` | 2 | Both D-09 fix sites (40, 90) updated. |

Total: 27 changed lines across 4 files (4 files changed, 27 insertions, 27 deletions per git stat).

## Files Created

None.

## Verification Performed

- `grep -c 'selected="@('` per-file:
  - DeckSync: 13 (≥ 5 required) — PASS
  - DeckConvert: 6 (≥ 3 required) — PASS
  - SuggestCategories: 6 (≥ 2 required) — PASS
  - AdminHarvest/Index: 2 (≥ 2 required) — PASS
- `grep -Eq 'selected="@\(.*\?.*"selected".*:.*null\)"'` matches in every target file — PASS.
- `grep -nE 'selected="True"'` across all four files returned no matches — PASS.
- `grep -nE 'selected="@\([^?]*\)"'` (any remaining boolean-only predicate) returned no matches — PASS.
- `dotnet build DeckFlow.sln --configuration Release` (run via Windows `dotnet.exe` v10.0.300 from WSL) — exit 0, 0 Warnings, 0 Errors, 5.21s elapsed.

No UAT in this plan per D-03 — batch UAT at phase end will inspect rendered HTML in browser dev tools to confirm `selected="selected"` (or attribute absent) on each `<option>`.

## Commits

| Hash | Type | Description |
|------|------|-------------|
| `51cf8b3` | fix | `fix(11-03): use ternary selected= pattern across remaining Razor views` |

## Decisions Made

- **D-1 (mirrored v1.2 32bf620):** Used the exact `selected="@(<predicate> ? "selected" : null)"` form. Razor emits `selected="selected"` (HTML5-valid) when the predicate is true and omits the attribute entirely when false. This is byte-identical to the ChatGPT-views fix from v1.2 commit `32bf620`.
- **D-2 (D-09 scope):** Applied the pattern only at the audit-driven sites enumerated in D-09. Did not introduce the pattern at any other `<option>` locations encountered in the four files — only the boolean-bound `selected=` predicates were rewritten. No other Razor views were touched in this sweep.
- **D-3 (no JS, no value= changes):** Did not introduce or modify any JavaScript. Did not alter any `<option value=...>` attributes. Did not modify any predicate expressions — only the attribute wrapping changed.

## Deviations from Plan

None — plan executed exactly as written. All target sites in D-09 matched their documented file:line ranges and the only attribute wrapping changed; predicate text, surrounding markup, and unrelated `<option>` elements were left untouched.

## Known Stubs

None.

## Threat Flags

None — this change reduces HTML-validity surface area only and introduces no new network, auth, file-access, or schema surface.

## Self-Check: PASSED

Files verified to exist after writing:
- FOUND: `DeckFlow.Web/Views/Deck/DeckSync.cshtml` (modified)
- FOUND: `DeckFlow.Web/Views/Deck/DeckConvert.cshtml` (modified)
- FOUND: `DeckFlow.Web/Views/Deck/SuggestCategories.cshtml` (modified)
- FOUND: `DeckFlow.Web/Views/AdminHarvest/Index.cshtml` (modified)

Commit hash verified in git log:
- FOUND: `51cf8b3` (`fix(11-03): use ternary selected= pattern across remaining Razor views`)
