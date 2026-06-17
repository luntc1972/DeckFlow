---
quick_id: 260615-t7m
slug: distill-timing
type: quick
date: 2026-06-15
status: complete
commits:
  - hash: 5a2cb65
    message: "feat(core): add per-video distill elapsed time to progress lines"
  - hash: d6f58e7
    message: "feat(studio): live distill elapsed clock and total time display"
files_modified:
  - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
  - DeckFlow.Studio/Pages/Harvest.razor
---

# Quick Task 260615-t7m: distill-timing — Summary

## One-liner

Per-video elapsed time `(N.Ns)` appended to Core progress lines; live `Elapsed: Ns` counter and `Total time: Ns` result row added to Studio Harvest page via a PeriodicTimer ticker disposed on cancel/circuit-drop.

## What was done

### Task 1 — Core: per-video elapsed in the progress lines

- Added `using System.Diagnostics;` at top of `ContentKbOrchestrator.cs`.
- Started `var sw = Stopwatch.StartNew();` immediately after `naturalKey` computed in `DistillVideoAsync`.
- Appended `({sw.Elapsed.TotalSeconds:F1}s)` suffix to three per-video progress reports:
  - Success: `distilled {naturalKey} ({sw.Elapsed.TotalSeconds:F1}s)`
  - Failed: `distill failed {naturalKey} ({sw.Elapsed.TotalSeconds:F1}s)`
  - Filtered/drop: `filtered {naturalKey} reason={Reason} ({sw.Elapsed.TotalSeconds:F1}s)`
- Logger `.Log*` lines unchanged.
- No test changes required — no test asserts exact progress message strings via equality.

### Task 2 — Studio: live elapsed clock + total time

- Added `@using System.Diagnostics` to `Harvest.razor`.
- Added fields: `_distillStopwatch`, `_distillTotalElapsed`, `_distillTickerCts`.
- In `RunDistillStageBAsync` start: starts stopwatch + fire-and-forget PeriodicTimer ticker (1s) via dedicated CTS; ticker calls `InvokeAsync(StateHasChanged)` and swallows `OperationCanceledException`/`ObjectDisposedException`.
- In `RunDistillStageBAsync` finally: stops stopwatch, records `_distillTotalElapsed`, cancels and disposes ticker CTS before `StateHasChanged`.
- In `Dispose()`: cancels/disposes ticker CTS; stops stopwatch (null-guarded).
- Added `FormatElapsed(TimeSpan)` static helper (s / m+s format).
- Markup: live `Elapsed: @FormatElapsed(...)` span next to Cancel button; `Total time:` dt/dd row in Distill complete result card.

## Build result

- `DeckFlow.Core`, `DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`: 0 errors, 0 warnings.
- `DeckFlow.Studio` Razor/C# compilation: 0 CS errors, 0 CS warnings. MSB3027 file-lock error is a copy-step artefact from the running Studio process — not a compilation failure.

## Deviations from plan

None — plan executed exactly as written.

## Self-Check: PASSED
