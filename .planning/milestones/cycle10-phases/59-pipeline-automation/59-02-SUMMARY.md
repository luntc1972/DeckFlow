---
phase: 59-pipeline-automation
plan: 02
subsystem: studio-auto-approve
tags: [auto-approve, settings, persistence, harvest-ui, blazor]
requires:
  - "ClipCountAutoApproveSignal.DefaultCutoff (Plan 01) — single source of truth for the default cutoff"
  - "DeckFlow.Studio Harvest page + DI conventions (Cycle 9)"
provides:
  - "AutoApproveSettings record (Enabled + Cutoff; Default ON/5)"
  - "AutoApproveSettingsStore — file-backed persistence with semantic clamp (D-07, T-59-03)"
  - "Harvest-page Auto-approve panel (toggle + cutoff, disabled-when-off, saved on commit)"
affects:
  - "Plan 03 (Studio host reads these persisted settings to drive one-click auto-approval)"
tech-stack:
  added: []
  patterns:
    - "Persisted local JSON settings store (System.Text.Json, no new NuGet)"
    - "Immutable record + `with` mutation, save-on-commit (onchange) not per-keystroke"
    - "Semantic clamp on load AND save so a bad value never reaches disk"
key-files:
  created:
    - DeckFlow.Studio/AutoApproveSettings.cs
    - DeckFlow.Studio/AutoApproveSettingsStore.cs
    - DeckFlow.Studio.Tests/AutoApproveSettingsStoreTests.cs
  modified:
    - DeckFlow.Studio/Program.cs
    - DeckFlow.Studio/Pages/Harvest.razor
    - DeckFlow.Studio.Tests/HarvestPageTests.cs
decisions:
  - "Persistence mechanism (D-07 Claude's Discretion): a single JSON file (auto-approve-settings.json) in the studio data dir — lightest option, System.Text.Json already present, no new NuGet"
  - "Cutoff seeded from ClipCountAutoApproveSignal.DefaultCutoff — one source of truth shared with Core (D-03)"
  - "Sanitize applied on BOTH Load and Save: negative cutoff -> DefaultCutoff, >MaxCutoff(1000) -> MaxCutoff (T-59-03)"
  - "Cutoff input binds onchange (commit/blur) not oninput, and is disabled when auto-approve is OFF (Codex MEDIUM)"
metrics:
  duration: ~35m
  completed: 2026-06-20
  tasks: 2
  files: 6
---

# Phase 59 Plan 02: Persisted Auto-Approve Settings + Harvest-Page Panel Summary

Persisted on/off + clip-cutoff auto-approve settings (default ON/5, surviving Studio restarts via a
JSON file with a semantic clamp on the stored cutoff) and a point-of-use "Auto-approve" panel on the
Harvest page that reads and writes them — the operator controls Plan 03 will read to drive one-click
auto-approval.

## What Was Built

### Task 1 — Persisted settings + file-backed store (with semantic clamp) + DI (commit a881c4c1)
- `AutoApproveSettings(bool Enabled, int Cutoff)` sealed record with
  `Default => new(true, ClipCountAutoApproveSignal.DefaultCutoff)` (D-06 ON, D-03 cutoff 5, sourced
  from the Plan 01 const — one source of truth).
- `AutoApproveSettingsStore` taking the studio data dir; reads/writes `auto-approve-settings.json`
  via System.Text.Json (no new NuGet). `Load()` returns `Default` when the file is missing or
  unparseable (wrapped try/catch on `JsonException`/`IOException`/`UnauthorizedAccessException` —
  never throws to the UI). `Save()` creates the directory if needed.
- SEMANTIC VALIDATION (Codex MEDIUM): `public const int MaxCutoff = 1000;` and a private
  `Sanitize(int)` mapping a negative cutoff to `DefaultCutoff` and clamping `>MaxCutoff` down.
  Applied on the value read in `Load()` AND the value written in `Save()`, so a bad cutoff can never
  reach disk (T-59-03).
- DI: `builder.Services.AddSingleton(_ => new AutoApproveSettingsStore(studioDataDirectory));` next to
  the content-KB singletons in Program.cs.
- 8 xUnit cases (temp dir, IDisposable cleanup): default ON/5, missing-file -> default, save-then-
  fresh-store round-trip (D-07 restart), named-file write, corrupt-file -> default no-throw,
  negative-cutoff clamp, high-cutoff clamp to Max, and save-side negative clamp.

### Task 2 — Auto-approve panel on the Harvest page (commit 6360aaa8)
- Injected `AutoApproveSettingsStore` into Harvest.razor; loaded at `OnInitializedAsync`
  (`_autoApproveSettings = AutoApproveSettingsStore.Load();`) alongside `RefreshCapDisplayAsync`.
- "Auto-approve" panel in the Distill card (Section 4, beside the spend-cap block, D-05): a checkbox
  bound to `Enabled` and a number input bound to `Cutoff` (min 0), plus helper copy
  "Off -> every distill enters the review queue" (D-04 / SC3).
- SAVE ON COMMIT (Codex MEDIUM): the cutoff uses `@onchange="OnAutoApproveCutoffChanged"` (blur/commit),
  not per-keystroke; the toggle's `@onchange` calls `OnAutoApproveEnabledChanged`. Both rebuild the
  immutable record via `with` and call `Save(...)` (store clamps bad values).
- DISABLE WHEN OFF (Codex MEDIUM): `disabled="@(!_autoApproveSettings.Enabled)"` on the cutoff input.
- 3 bUnit cases added to HarvestPageTests (each over a per-test temp dir): default render shows
  toggle ON + cutoff 5 + enabled cutoff; toggle-off persists `Enabled=false` and disables the cutoff
  input; cutoff change to 7 persists `Cutoff=7`. Registered the store in the `RenderHarvest` fixture.

## Deviations from Plan

### Auto-fixed Issues

None affecting product code. One convention reconciliation:
- `[Rule 3 - Blocking]` The changed-lines format gate (IDE0161) required the NEW test file
  `AutoApproveSettingsStoreTests.cs` to use a file-scoped namespace, while the existing
  `HarvestPageTests.cs` uses block-scoped. Since the gate is changed-lines-only and the new file's
  lines are all new, I authored the new file with a file-scoped namespace to pass the gate; the
  existing block-scoped file was left untouched (no mass reflow). The gate then reported "format check
  passed for changed lines; off-hunk violations ignored" for the existing-file warning.

## Verification

- `DeckFlow.Studio.Tests` build: **Build succeeded, 0 errors** (both tasks).
- New store tests: **8 passed, 0 failed**. New bUnit auto-approve tests: **3 passed, 0 failed**.
  Full `HarvestPageTests`: **9 passed, 0 failed** (6 existing + 3 new — no regression).
- Acceptance greps: `auto-approve-settings.json` in store = 3 (>=1); `Auto-approve` in Harvest.razor
  = 5 (>=1); `AutoApproveSettingsStore(studioDataDirectory)` singleton in Program.cs = 1;
  `OnAutoApproveCutoffChanged` (onchange-bound) = 2.
- Changed-lines format gate: clean on all changed lines (one off-hunk IDE0161 on a pre-existing line,
  ignored by the gate as designed).
- No file deletions in either commit. Per the dirty-tree warning, only my plan's files were staged;
  the pre-existing card_text/Manabase working-tree changes and `DeckFlow.sln` break were not touched
  (DeckFlow.Studio does not reference DeckFlow.Web, so the break does not affect this plan).

## Success Criteria

- [x] Harvest-page Auto-approve panel with toggle + cutoff (D-05), default ON/5 (D-03, D-06), cutoff disabled when OFF, saved on commit
- [x] Settings persist across Studio restarts (D-07); a semantically-bad persisted cutoff is clamped on load
- [x] Off path documented as "every distill enters the review queue" (D-04 / SC3) — enforced in Plan 03

## Self-Check: PASSED
