# Phase 34 · Plan 01 — Summary

**Plan:** 34-01 KB Retrieval algorithm fix (KBR-01, KBR-02, KBR-04)
**Executed:** 2026-06-10 by Codex (gpt-5.4 medium), reviewed by Claude
**Files changed (scope-fenced):**
- `DeckFlow.Web/Services/ContentKbRelevanceService.cs`
- `DeckFlow.Web.Tests/ContentKbRelevanceServiceTests.cs`

## What changed

- **KBR-01 — per-video cap:** `SelectTopClips` now enforces `MaxClipsPerVideo = 1` so one row can no longer monopolize the auto-selection slots (the Spike 001 Run-2 defect). Single-relevant-video decks still get exactly 1 clip (not zero).
- **KBR-02 — topical scoring + demotion + floor:** scoring unified into one shared path used by both `ScoreArtifact` and the merged-clips scorer (`CalculateScoreAndDimensions` → `GetMergedClipsAsync`) to prevent drift. Added topical CONTENT-overlap as a primary signal, a curated zero-dep known-commander demotion (never penalizing the deck's own commander), and the relevance floor. No-match → null (no top-K fallback). Per-video row scoring + clip inheritance preserved (NOT per-clip). Internal test ctor signature unchanged.
- **KBR-04 — tests:** added the mandatory Spike 001 Run-2 Atraxa regression (≥2 distinct videos, Glass Cannon capped ≤1 clip, no Kaalia/Animar leakage) plus per-video cap, no-commander general-advice qualifies, null-on-no-match, own-commander-not-penalized; updated the old monopoly-asserting test.

## Calibrated constants

| Constant | Value | Atraxa-gold rationale |
|----------|-------|----------------------|
| `ContentOverlapWeight` | 0.45 | Lets "Too Much Ramp" / "Deckbuilding Mistakes" clear the floor on strategy language alone (no commander needed). |
| `OtherCommanderPenalty` | 0.9 / foreign commander token | Sinks the Kaalia/Animar/Isshin/Zur "Glass Cannon" row below the floor for Atraxa. |
| `MinSelectionScore` (floor) | 2.0 | Keeps existing two-signal matches alive while filtering demoted foreign-commander noise. |

## Verification

- `dotnet build DeckFlow.Web` → 0 warnings, 0 errors.
- `dotnet build DeckFlow.Web.Tests` + filtered `test --filter "FullyQualifiedName~ContentKbRelevanceServiceTests|FullyQualifiedName~ContentKbMergedClipsTests"` → **27 passed, 0 failed**; `ContentKbMergedClipsTests` green (merged-clips path not regressed).
- Internal test ctor signature unchanged (Spike001KbValueAbHarness still compiles).
- Zero new NuGet packages.

## Requirements

- [x] KBR-01 · [x] KBR-02 · [x] KBR-04 — all verified by the test run above.
