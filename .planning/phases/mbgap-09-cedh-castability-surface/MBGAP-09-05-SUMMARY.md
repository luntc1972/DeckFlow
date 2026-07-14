---
phase: mbgap-09-cedh-castability-surface
plan: 05
status: complete
executor: codex gpt-5.4 (cross-AI), reviewed + committed by Claude
commits:
  - 8f18df33 feat(MBGAP-09-05): mode-aware ShowCastability + lens gate on view model
  - 7d2cf786 feat(MBGAP-09-05): interaction display helpers
  - 45043419 test(MBGAP-09-05): lock presentation gate + helpers
key-files:
  created: []
  modified:
    - DeckFlow.Web/Models/ManabaseViewModel.cs
    - DeckFlow.Web/Models/ManabaseDisplay.cs
    - DeckFlow.Web/Controllers/ManabaseController.cs
    - DeckFlow.Web.Tests/Manabase/ManabaseViewModelTests.cs
    - DeckFlow.Web.Tests/Manabase/ManabaseDisplayTests.cs
---

# MBGAP-09-05 Summary — Presentation contract for the view

## What was built
- `ManabaseViewModel.ShowCedhInteractionLens { get; init; }`; `ShowCastability` now: rows exist AND (Casual OR (Cedh AND ShowCedhInteractionLens)) — Casual unchanged, cEDH table flag-gated (D-09).
- `ManabaseController` normal path sets `ShowCedhInteractionLens = result.ShowCedhInteractionLens`; report-text Build call passes `interactionLens: result.ShowCedhInteractionLens ? result.Report.InteractionLens : null` (D-14). Error/empty branches untouched.
- `ManabaseDisplay`: `InteractionHoldableMarker(percent, threshold)` reusing manabase-lens-met/short + ✓/⚠ (threshold param — 88 never re-hardcoded, D-12); `CedhInteractionLensGloss` with verbatim "assumes you hold mana open" (D-07); `DefaultVisibleInteractionCount = 5` (D-11); `InteractionSummaryText` states hidden remainder (L2 no silent truncation). All pure/static.
- Tests: three-state ShowCastability gate, marker boundary 87/88/90 @ 88, gloss caveat.

## Deviations
- Codex moved the pre-existing ShowCastability tests from ManabaseDisplayTests into ManabaseViewModelTests (correct home; old cEDH-always-false assertion was stale by design). LEAD review caught that the move dropped the `HasResult` true/false + no-report gate tests — restored by Claude during review (trivial-assertion exception) in 45043419.
- Pre-existing CS8602 warning in MetaGapServiceTests.cs (outside plan fence, present at HEAD before this plan) noted by Codex; left untouched.

## Verification
- Full Web suite: **1381 pass / 0 fail / 14 pre-existing skips** (Windows dotnet.exe), including restored HasResult tests.
- Builds 0 new warnings. EOL: no churn on all five files.

## Self-Check: PASSED
