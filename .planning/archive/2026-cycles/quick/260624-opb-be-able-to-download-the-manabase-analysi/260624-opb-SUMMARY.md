---
phase: quick-260624-opb
plan: 01
subsystem: manabase
tags: [download, manabase, report, tdd]
tech-stack:
  added: []
  patterns: [pure-builder, formaction-mini-form, controller-download-action]
key-files:
  created:
    - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs
    - DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs
    - DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs
    - DeckFlow.Web/e2e/manabase-download.spec.ts
  modified:
    - DeckFlow.Web/Controllers/ManabaseController.cs
    - DeckFlow.Web/Views/Deck/Manabase.cshtml
    - DeckFlow.Web/wwwroot/css/site-common.css
decisions:
  - Download form placed as a separate <form> outside the result <section> because the
    result panel is rendered outside the main analyze form; hidden inputs carry the same
    deck fields as the analyze form so model-binding produces an identical ManabaseRequest.
  - Health labels hard-coded in Core (Healthy->Excellent, Functional->Solid,
    Workable->Workable, NeedsWork->Needs work) to match ManabaseDisplay.HealthLabel
    without introducing a Web dependency in Core.
  - Castability table included in text output only for Casual mode (mirrors the view's
    existing conditional, where cEDH hides the table).
  - ManabaseReportTextBuilder follows ManabaseSwapPromptBuilder's CultureInfo.InvariantCulture
    + StringBuilder discipline exactly (same namespace, same pattern).
metrics:
  duration: ~30 minutes
  completed: 2026-06-24
  tasks_completed: 3
  files_changed: 7
---

# Phase quick-260624-opb Plan 01: Manabase Analysis Download Summary

**One-liner:** POST /manabase/download re-runs the Karsten §6 analysis via ManabaseReportTextBuilder and returns a paste-ready manabase-analysis-{timestamp}.txt attachment.

## What Was Built

### Task 1 — Core ManabaseReportTextBuilder (TDD)

`DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs` — pure static builder:

- `Build(report, deckName, decklistText, mode)` returns a paste-ready plain-text string
- Sections: title + deck name, mode label, lands line with land-note (OK / ramp-covers-it / add N), health tier label, summary, per-color source table (Actual / Needed / Deficit-or-OK / Driving spell), biggest-fix callout (mirrors view's switch on PrimaryFix.Kind), castability table (Casual only, when non-empty), ramp names, unsupported interactions, optional decklist appendix
- Hard-codes health labels in Core (cannot reference Web): Healthy→"Excellent", Functional→"Solid", Workable→"Workable", NeedsWork→"Needs work"
- Never emits "add ~-N" (PrimaryFix guards this)
- All numeric formatting uses `CultureInfo.InvariantCulture`

13 unit tests in `DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs` covering all branches.

TDD gate compliance: RED commit `2dd96d89`, GREEN commit `5ceb4bae`.

### Task 2 — Controller + View + CSS + Web Tests

**Controller** (`DeckFlow.Web/Controllers/ManabaseController.cs`): `POST /manabase/download` action `Download(ManabaseRequest)` with identical attributes to the analyze action (`[ValidateAntiForgeryToken]` + `[FeatureFlagGate("feature.manabase.enabled", ...)]`). Body mirrors the analyze action exactly (enum coercion, timeout scope, `AnalyzeAsync`, error families). On success returns `File(UTF8.GetBytes(text), "text/plain; charset=utf-8", "manabase-analysis-{yyyyMMdd-HHmmss}.txt")`.

**View** (`DeckFlow.Web/Views/Deck/Manabase.cshtml`): a separate `<form method="post" action="~/manabase/download" class="toolbar manabase-download">` with `@Html.AntiForgeryToken()` and seven hidden inputs carrying all `ManabaseRequest` fields. Renders only inside the `@if (Model.HasResult)` block. Positioned between the result section and the ChatGPT swap prompt.

**CSS** (`site-common.css`): `.toolbar.manabase-download { justify-content: flex-start; flex-wrap: wrap; gap: 0.75rem; align-items: center; }` — matches `.toolbar.manabase-actions` layout; no new color tokens; layout CSS only, nothing in `site.css`.

4 Web tests in `DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs`: file result with correct content-type and timestamped filename, enum coercion guard, InvalidOperationException re-renders view, HttpRequestException re-renders view.

### Task 3 — Playwright Smoke

`DeckFlow.Web/e2e/manabase-download.spec.ts` — 3 tests × 2 projects (chromium-desktop + chromium-mobile) = 6 runs:

1. Download form absent on fresh GET /manabase (no result yet) — no console errors
2. Analyze form action unchanged at `/manabase`
3. No horizontal overflow (`scrollWidth <= innerWidth + 1`) on both viewports

All 6 passed live against a running server.

## Test Results

| Suite | Before | After | Delta |
|-------|--------|-------|-------|
| Core unit | 779 | 792 | +13 |
| Web unit | 772 | 776 | +4 |
| Web PG skip | 11 | 11 | 0 |
| Playwright e2e | ran | 6/6 pass | +6 |

## Commits

| Hash | Message |
|------|---------|
| `2dd96d89` | test(quick-260624-opb-01): add failing tests for ManabaseReportTextBuilder (RED) |
| `5ceb4bae` | feat(quick-260624-opb-01): implement ManabaseReportTextBuilder paste-ready report (GREEN) |
| `507cf822` | feat(quick-260624-opb-01): add Download action, download button, and Web tests |
| `3c5155c3` | test(quick-260624-opb-01): add Playwright smoke for manabase download button |

## Deviations from Plan

None — plan executed exactly as written. The only sub-decision needed was the download form placement (plan said "near the ChatGPT swap-prompt disclosure") — placed immediately before it, outside the result `<section>` but inside the `@if (Model.HasResult)` block.

## Known Stubs

None. The download re-uses the live `IManabaseAnalysisService.AnalyzeAsync` pipeline — no mocked or hard-coded data paths.

## Threat Flags

None. `/manabase/download` is protected by the same `[ValidateAntiForgeryToken]` and `[FeatureFlagGate]` attributes as the existing analyze action. No new network endpoints, auth paths, or schema changes.

## Self-Check: PASSED

- DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs: EXISTS
- DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs: EXISTS (13 tests)
- DeckFlow.Web.Tests/Manabase/ManabaseControllerDownloadTests.cs: EXISTS (4 tests)
- DeckFlow.Web/e2e/manabase-download.spec.ts: EXISTS (3 specs)
- ManabaseController.cs Download action: EXISTS (git log 507cf822)
- Manabase.cshtml download form: EXISTS (git log 507cf822)
- site-common.css .toolbar.manabase-download: EXISTS (git log 507cf822)
- dotnet build DeckFlow.sln: 0 errors, 0 new warnings on changed lines
- Core tests: 792/792 pass
- Web tests: 776/776 pass (11 PG skip)
- Playwright: 6/6 pass (chromium-desktop + chromium-mobile)
