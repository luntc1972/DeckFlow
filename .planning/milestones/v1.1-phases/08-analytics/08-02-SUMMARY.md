---
phase: 08-analytics
plan: 02
subsystem: analytics-buffer

tags: [channel, backgroundservice, write-behind, drop-oldest, analytics]

requires:
  - phase: 08-analytics
    plan: 01
    provides: RequestMetricEvent record, IRequestMetricsStore interface
provides:
  - RequestMetricsBuffer singleton — BoundedChannel<RequestMetricEvent> wrapper with DropOldest + atomic drop counter
  - RequestMetricsFlusher BackgroundService — whichever-fires-first drain (100 records OR 5s) + CreateScope per flush
affects: [08-03-middleware, 08-05-di-registration]

tech-stack:
  added: []
  patterns:
    - "System.Threading.Channels.Channel.CreateBounded with itemDropped callback — atomic drop counter without per-drop allocation"
    - "IServiceProvider.CreateScope() per flush — avoids circular singleton DI cycle (D-14, Phase 7.1 errata)"
    - "Linked CancellationTokenSource + CancelAfter(5s) inside WaitToReadAsync — whichever-fires-first between data arrival and timeout"
    - "Per-tick try/catch with OperationCanceledException when stoppingToken.IsCancellationRequested branch — loop never exits on transient errors"

key-files:
  created:
    - DeckFlow.Web/Services/Analytics/RequestMetricsBuffer.cs
    - DeckFlow.Web/Services/Analytics/RequestMetricsFlusher.cs
  modified: []

key-decisions:
  - "ShutdownDrainCeiling = 2s chosen per CONTEXT.md 'Claude's discretion' bullet — long enough to flush a partial batch on orderly restart, short enough to not stall Render/Fly graceful shutdown window"
  - "MaybeLogDrops resets _lastDropLog even when dropped==0 so the 60s window advances continuously, preventing spurious WARN bursts after a quiet period followed by a burst"
  - "XML doc crefs for Channel<T> and IServiceProvider.CreateScope() use <c>code</c> text rather than <see cref=...> to avoid CS1574 unresolved-cref warnings while preserving readability (GenerateDocumentationFile=true requires 0 warnings)"

requirements-completed: [ANLY-02]

duration: 10min
completed: 2026-05-03
---

# Phase 8 Plan 02: RequestMetricsBuffer + RequestMetricsFlusher Summary

**BoundedChannel write-behind buffer (capacity 10 000, DropOldest) + BackgroundService flusher with whichever-fires-first cadence (100 records OR 5 s) and IServiceProvider lazy-scope store resolution**

## Performance

- **Duration:** ~10 min
- **Completed:** 2026-05-03
- **Tasks:** 2
- **Files created:** 2

## Accomplishments

- `RequestMetricsBuffer`: `Channel.CreateBounded` with `BoundedChannelFullMode.DropOldest` and an `itemDropped` callback that atomically increments `_droppedCount` via `Interlocked.Increment` — no per-drop allocation, no hot-path blocking (D-08, T-08-07/T-08-08)
- `RequestMetricsFlusher`: `BackgroundService` drains buffer on whichever fires first — 100 events or 5-second timer — using a linked `CancellationTokenSource` with `CancelAfter(FlushInterval)` inside `WaitToReadAsync`. Per-tick `try/catch` distinguishes `stoppingToken` cancellation (break) from inner flush timeout (continue) (D-09, T-08-06)
- Store resolved via `IServiceProvider.CreateScope()` per flush, not in the constructor — eliminates the circular singleton DI cycle documented in Phase 7.1 errata (D-14)
- Drop WARN throttled: `MaybeLogDrops()` checks `DateTimeOffset.UtcNow - _lastDropLog < 60s` before calling `ConsumeDropCount()` — one WARN per minute maximum, never per-drop (D-10, T-08-10)
- `StopAsync` best-effort drain: flushes full 100-event batches then explicitly flushes the residual partial batch within a 2-second ceiling; errors logged as WARN, never propagated

## Task Commits

1. **Task 1 + Task 2 combined** — `bc2f7e5` (feat) — both files in one atomic commit

## Files Created

- `DeckFlow.Web/Services/Analytics/RequestMetricsBuffer.cs` — `public sealed class RequestMetricsBuffer`: `BoundedChannelOptions` static readonly, `Channel.CreateBounded` with itemDropped, `Reader`, `Enqueue(evt)`, `ConsumeDropCount()`
- `DeckFlow.Web/Services/Analytics/RequestMetricsFlusher.cs` — `public sealed class RequestMetricsFlusher : BackgroundService`: `ExecuteAsync` drain loop, `FlushBatchAsync` (CreateScope), `MaybeLogDrops`, `StopAsync` with 2s ceiling + residual partial batch flush

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] CS1574 XML doc cref warnings**
- **Found during:** First build (3 warnings)
- **Issue:** `<see cref="BoundedChannel{T}">` and `<see cref="IServiceProvider.CreateScope">` are not directly resolvable in the doc comment context — `BoundedChannel<T>` is internal to System.Threading.Channels, `CreateScope` is an extension method not a member
- **Fix:** Changed to `<see cref="System.Threading.Channels.Channel{T}"/>` for the buffer class doc and `<c>IServiceProvider.CreateScope()</c>` for the two flusher references
- **Files modified:** Both new files
- **Result:** 0 warnings on second build

## Known Stubs

None — no UI or data-serving code in this plan.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema changes introduced.

## Self-Check: PASSED

- `DeckFlow.Web/Services/Analytics/RequestMetricsBuffer.cs` — exists; contains `BoundedChannelFullMode.DropOldest`, `capacity: 10_000`, `Channel.CreateBounded<RequestMetricEvent>`, `Interlocked.Increment(ref _droppedCount)`, `public ChannelReader<RequestMetricEvent> Reader`, `public void Enqueue`, `public long ConsumeDropCount`, `SingleReader = true`, `SingleWriter = false`
- `DeckFlow.Web/Services/Analytics/RequestMetricsFlusher.cs` — exists; contains `public sealed class RequestMetricsFlusher : BackgroundService`, `BatchSize = 100`, `TimeSpan.FromSeconds(5)`, `WaitToReadAsync`, `using var scope = _services.CreateScope()`, `GetRequiredService<IRequestMetricsStore>`, `Analytics.Buffer.Drops`, `Analytics.Flusher.TickFailure`, `ShutdownDrainCeiling`, `_buffer.ConsumeDropCount()`, residual partial batch flush in `StopAsync`
- Flusher ctor: takes `RequestMetricsBuffer`, `IServiceProvider`, `ILogger<RequestMetricsFlusher>` — does NOT take `IRequestMetricsStore` directly (D-14 enforced)
- Build: `bc2f7e5` — 0 Warning(s), 0 Error(s)

## Next Phase Readiness

Wave 3 (08-03): analytics middleware — calls `RequestMetricsBuffer.Enqueue(evt)` from the request hot path; both contracts are now locked.
Wave 5 (08-05): DI registration — registers `RequestMetricsBuffer` as Singleton and `RequestMetricsFlusher` as Singleton + AddHostedService.

---
*Phase: 08-analytics*
*Completed: 2026-05-03*
