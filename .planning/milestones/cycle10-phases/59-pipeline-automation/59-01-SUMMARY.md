---
phase: 59-pipeline-automation
plan: 01
subsystem: content-kb-orchestration
tags: [auto-approve, distill, core, seam, clip-count]
requires:
  - "DeckFlow.Core orchestration distill/approve slice (Cycle 9)"
  - "GetContentNaturalKeyInfo (YouTube + podcast natural key) — pre-existing"
provides:
  - "IAutoApproveSignal — swappable auto-approve decision seam (D-02)"
  - "ClipCountAutoApproveSignal with DefaultCutoff = 5 (D-01, D-03)"
  - "DistilledVideoResult public per-video DTO (natural key + clip count)"
  - "DistillResult.DistilledVideos surfacing clip count per distilled video (D-01, D-11)"
affects:
  - "Plan 02 (Studio settings store seeds cutoff from ClipCountAutoApproveSignal.DefaultCutoff)"
  - "Plan 03 (Studio host runs auto-approve from DistillResult.DistilledVideos → SetApprovalStatusAsync)"
tech-stack:
  added: []
  patterns:
    - "Pure decision helper behind an interface (mirrors PublishStateDeriver / VideoStatusResolver)"
    - "init-only IReadOnlyList with Array.Empty default (mirrors FailedVideoIds)"
key-files:
  created:
    - DeckFlow.Core/Content/IAutoApproveSignal.cs
    - DeckFlow.Core/Content/ClipCountAutoApproveSignal.cs
    - DeckFlow.Core/Orchestration/DistilledVideoResult.cs
    - DeckFlow.Core.Tests/Content/ClipCountAutoApproveSignalTests.cs
    - DeckFlow.Core.Tests/Orchestration/DistillResultClipCountTests.cs
  modified:
    - DeckFlow.Core/Orchestration/DistillResult.cs
    - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
decisions:
  - "Clip count is the auto-approve signal; no confidence field added to schema (D-01, SC4)"
  - "Signal isolated behind IAutoApproveSignal so a future composite signal swaps only the impl (D-02)"
  - "Default cutoff >= 5 (5+ approve, 3-4 hold) (D-03)"
  - "DistilledVideos ordered by source/video processing order (documented on the property)"
  - "Public DTO named DistilledVideoResult, distinct from private DistillVideoOutcome (Codex MEDIUM)"
  - "Core only records clip count; it does NOT flip approval_status — that is the Studio host's job in Plan 03 (T-59-02 accept)"
metrics:
  duration: ~1h
  completed: 2026-06-20
  tasks: 2
  files: 7
---

# Phase 59 Plan 01: AUTO-02 Auto-Approve Signal Seam + Per-Video Clip Count Summary

Isolated the "is this distill good enough to auto-approve" decision behind a swappable
`IAutoApproveSignal` seam with a clip-count implementation (>= 5 default), and surfaced each
distilled video's natural key (YouTube or podcast) plus clip count on `DistillResult.DistilledVideos`
so the Studio host (Plans 02/03) can run auto-approval — all without touching the distill schema,
provider, or model.

## What Was Built

### Task 1 — Swappable auto-approve signal seam + clip-count implementation (commit 44d9d630)
- `IAutoApproveSignal.ShouldAutoApprove(int clipCount, int cutoff)` — the seam (D-02). A future
  composite signal implements the same interface without reworking the auto-approve plumbing.
- `ClipCountAutoApproveSignal : IAutoApproveSignal` — `clipCount >= cutoff`, with
  `public const int DefaultCutoff = 5` (D-03) as the single source of truth the Studio settings
  store (Plan 02) will seed from.
- 8 xUnit cases: boundary (5/5 → true), hold (4/5 → false), above (8/5 → true), operator-off
  (cutoff 0 → always true), high cutoff (99 → realistic 3-8 all false), and the default-cutoff guard.
- DistillationSchemas.cs untouched — no `confidence` field (D-01, SC4).

### Task 2 — Per-video clip count + natural key on DistillResult (commit e1c15c79)
- `DistilledVideoResult` public sealed record: `required` `NaturalKeyType`, `NaturalKeyValue`,
  `ClipCount` (init-only). Named distinct from the private `DistillVideoOutcome` accumulator
  (Codex MEDIUM).
- `DistillResult.DistilledVideos` — init-only `IReadOnlyList<DistilledVideoResult>` defaulting to
  `Array.Empty<>`, mirroring `FailedVideoIds`. XML doc states the ordering contract: source/video
  processing order; filtered/failed/dry-run produce no entry.
- `ContentKbOrchestrator`: extended the private `DistillVideoOutcome` record with optional
  `NaturalKeyType`/`NaturalKeyValue`/`ClipCount` carried only on the `Distilled` factory; the
  distilled return now passes `GetContentNaturalKeyInfo(video)` (YouTube **and** podcast) plus
  `clips.Clips.Count`. `DistillCounts.Add` appends a `DistilledVideoResult` when `IsDistilled`,
  and the non-dry-run return sets `DistilledVideos = counts.DistilledVideos` (dry-run left empty).
- 6 xUnit cases via the in-memory fake-store harness: YouTube natural key + clip count, **podcast**
  natural key (RssGuid, NOT a YouTube-only id), filtered (drop) → no entry, failed → no entry +
  stays in FailedVideoIds, dry-run → empty, and multi-video ordering matches processing order.

## Deviations from Plan

### Auto-fixed Issues

None affecting product code. One test-authoring fix during TDD:
- The clip-count test's distillation double initially produced clips with all-zero timestamps,
  which `DistillationValidation.ValidateClips` correctly rejects ("cannot return every clip with
  timestamp 0"). Fixed the test double to emit ascending timestamps. This was a test-only fix,
  not a product change.

## Deferred Issues (out of scope)

### DEF-59-01: Pre-existing DeckFlow.Web.Tests build break
- `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs:128` fails to compile (`CS1503`,
  `ILogger<DeckAnalysisPacketService>` vs `IFeatureFlagCache?`) because the **working tree** has
  an uncommitted `DeckAnalysisPacketService` constructor signature change that the test double was
  not updated for. Entirely unrelated to plan 59-01 (which only touches Core + Core.Tests).
  Logged to `.planning/phases/59-pipeline-automation/deferred-items.md`. Not fixed per the
  executor SCOPE BOUNDARY rule.

## Verification

- `DeckFlow.Core` build: **Build succeeded, 0 errors** (pre-existing NU1903 SQLite advisory +
  one pre-existing CS1574 xmldoc warning — both out of scope, unchanged by this plan).
- `DeckFlow.Core.Tests` build: **Build succeeded, 0 errors**.
- Full `DeckFlow.Core.Tests` suite: **511 passed, 0 failed** (was 475; +14 from this plan + others
  in tree). Run via the Windows dotnet (`/mnt/c/Program Files/dotnet/dotnet.exe test`).
- Acceptance greps: `clips.Clips.Count` appears 2x in orchestrator; `confidence` count in
  DistillationSchemas.cs = 0; `DistilledVideoResult` named correctly (no `DistilledVideoOutcome`
  homonym); no existing `{ get; init; }` converted to `{ get; }`.
- Changed-lines format gate: clean on all staged files (no diff reported).
- `DeckFlow.sln` does NOT build clean, solely due to the pre-existing DEF-59-01 Web.Tests break in
  an unrelated, uncommitted-working-tree project. All 59-01 in-scope projects build and test green.

## Success Criteria

- [x] IAutoApproveSignal + ClipCountAutoApproveSignal exist with the >= 5 default cutoff (D-03), signal swappable (D-02)
- [x] DistillResult.DistilledVideos surfaces natural key (YouTube or podcast) + clip count per distilled video (D-01, D-11)
- [x] Distill provider/model/schema unchanged (SC4) — no confidence field added

## Self-Check: PASSED
