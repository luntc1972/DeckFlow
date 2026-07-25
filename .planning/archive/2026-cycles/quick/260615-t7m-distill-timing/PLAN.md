---
quick_id: 260615-t7m
slug: distill-timing
type: quick
date: 2026-06-15
follow_up_to: phase 45-04 (HARV-05)
description: Show per-video distill duration in the progress log and a live "currently taking" elapsed clock while distilling
files_modified:
  - DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs
  - DeckFlow.Studio/Pages/Harvest.razor
  - DeckFlow.Core.Tests (only if an existing test asserts the exact "distilled {id}" progress string)
---

# Quick Task: distill timing (per-video + live clock)

## Goal

While distilling a batch the operator wants to see (1) how long EACH video took, and (2) how long the
current run has been going (live), so a long batch reads as "progressing" not "hung".

## Tasks

### Task 1 — Core: per-video elapsed in the progress lines

- `ContentKbOrchestrator.cs`: add `using System.Diagnostics;` if not present.
- In `DistillVideoAsync` (~991): start `var sw = Stopwatch.StartNew();` right after `naturalKey` is computed.
- Append the elapsed to the per-video progress reports (keep the existing prefix words so nothing else
  that greps them breaks the meaning — only add a suffix):
  - success (~line 1183): `progress?.Report($"distilled {naturalKey} ({sw.Elapsed.TotalSeconds:F1}s)");`
  - failed (~1190): `progress?.Report($"distill failed {naturalKey} ({sw.Elapsed.TotalSeconds:F1}s)");`
  - if there is a "drop"/filtered progress report inside this method, add the same `({…s})` suffix; do
    NOT touch the abort/config-error report.
- The log `_logger.Log*` lines may stay as-is (or also include elapsed — optional, low value).
- If any existing test asserts the EXACT string `distilled {id}` / `distill failed {id}` via equality,
  relax it to `Contains`/`StartsWith` so the new `(…s)` suffix passes. Do not invent new tests for this;
  just keep the suite green.

### Task 2 — Studio: live elapsed clock + total time

- `DeckFlow.Studio/Pages/Harvest.razor` `@code`:
  - Add fields: `private Stopwatch? _distillStopwatch;` `private TimeSpan? _distillTotalElapsed;`
    `private CancellationTokenSource? _distillTickerCts;` (add `@using System.Diagnostics` at top).
  - In `RunDistillStageBAsync`, when starting the live run (after setting `_distillLiveInFlight = true`):
    - `_distillStopwatch = Stopwatch.StartNew(); _distillTotalElapsed = null;`
    - Start a 1-second ticker that re-renders so the elapsed updates live. Use a dedicated CTS
      (`_distillTickerCts = new();`) and a fire-and-forget loop with `PeriodicTimer`:
      ```csharp
      _ = Task.Run(async () =>
      {
          using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
          try
          {
              while (await timer.WaitForNextTickAsync(_distillTickerCts.Token))
              {
                  await InvokeAsync(StateHasChanged);
              }
          }
          catch (OperationCanceledException) { }
          catch (ObjectDisposedException) { }
      });
      ```
  - In the `finally` of `RunDistillStageBAsync`: `_distillStopwatch?.Stop(); _distillTotalElapsed = _distillStopwatch?.Elapsed;`
    then `_distillTickerCts?.Cancel(); _distillTickerCts?.Dispose(); _distillTickerCts = null;` (before the existing StateHasChanged).
  - Add a small formatter `private static string FormatElapsed(TimeSpan t) => t.TotalSeconds < 60 ? $"{t.TotalSeconds:F0}s" : $"{(int)t.TotalMinutes}m {t.Seconds}s";`
- Markup:
  - Next to the "Distilling..." spinner (the live Run button, ~line 595 region) OR just below the run
    button while `_distillLiveInFlight`, show: `<span class="text-muted small ms-2">Elapsed: @FormatElapsed(_distillStopwatch?.Elapsed ?? TimeSpan.Zero)</span>`.
  - On the Stage B result card (`Distill complete`, ~line 631), add a row: `Total time: @FormatElapsed(_distillTotalElapsed ?? TimeSpan.Zero)`.
- `Dispose()` (~1335): also cancel+dispose `_distillTickerCts` and stop `_distillStopwatch` (guard nulls).
- Keep all existing Task.Run / `_cts` / progress-sink / spend-gate behavior unchanged. The ticker is
  display-only; it must never throw into the circuit (the try/catch above + InvokeAsync handle disposal races).

## Acceptance

- `dotnet build DeckFlow.sln` — 0 errors, 0 new warnings; both test projects build.
- Progress log shows e.g. `distilled abc123 (12.4s)` per video.
- While a live distill runs, an "Elapsed: Ns" counter updates ~once a second and stops on completion;
  the result card shows "Total time: Ns".
- Circuit drop / cancel mid-run does not throw from the ticker (disposed cleanly).
- Existing suite stays green.

## Out of scope

- Persisting durations to the DB or run store.
- Per-video live countdown / ETA estimation.
