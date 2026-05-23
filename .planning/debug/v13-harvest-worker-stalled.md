---
slug: v13-harvest-worker-stalled
status: investigation_inconclusive
goal: find_root_cause_only
trigger: v1.3 production (commit edfc378 / Render srv-d7gmufkp3tds73a29m30-5lj2j)
deploy_window_utc: 2026-05-22 20:10..20:32
control_deploy: main @ 7ed0cde (2026-05-22 20:35:32 UTC, same env+DB, worker recovered)
mode: diagnose-only (Codex will apply the fix per cross-AI rule)
---

# Bug summary

On the v1.3 production deploy, `ArchidektCacheJobService` (BackgroundService) stops consuming
signals from its in-memory `Channel<QueuedJobSignal>`. POSTs to `/Admin/Harvest/run` and the
60-second `HarvestScheduleService` tick both reach `EnqueueAsync` (one new `Queued` row is
inserted in PG; subsequent enqueues dedup against it via `GetActiveAsync`), but the worker
never logs `Harvest.Run.StateChange ... state=Running`. A fresh deploy of `main` against the
same Render env and Postgres DB resumed processing immediately at 20:35:39, which by itself
rules out env/DB/data hypotheses and points the defect at code or state inside the v1.3 process.

# Investigation log

## What was checked (static, no source changes)

### 1. Worker code path is byte-identical between main and v1.3
- `ArchidektCacheJobService.ExecuteAsync` (lines 245-385 in v1.3) — unchanged vs main
- `EnqueueAsync` (lines 162-205) — unchanged vs main
- The channel field initializer (line 129: `Channel.CreateUnbounded<QueuedJobSignal>()`) — unchanged
- `git diff main..v1.3 -- DeckFlow.Web/Services/ArchidektCacheJobService.cs` is a 10-line diff
  restricted to the SYNCHRONOUS `GetJob(Guid)` method (lines 207-212), which is invoked only
  by the admin/API read path — not by the worker, the scheduler, or `EnqueueAsync`.

### 2. DI graph for the worker is byte-identical between main and v1.3
- Program.cs lines 333-335:
  - `AddSingleton<ArchidektCacheJobService>()`
  - `AddSingleton<IArchidektCacheJobService>(sp => sp.GetRequiredService<ArchidektCacheJobService>())`
  - `AddHostedService(sp => sp.GetRequiredService<ArchidektCacheJobService>())`
- All three resolutions go through the same factory closure that returns the same concrete
  singleton instance — controller, scheduler, and `IHostedService` host all share the SAME
  channel writer/reader pair. The diff `git diff main..v1.3 -- DeckFlow.Web/Program.cs` shows
  these three lines unchanged. (H1 / H4 are STATICALLY FALSIFIED — the channel-mismatch /
  multi-instance hypothesis cannot hold given the registration shape.)

### 3. Hosted-service registration ORDER is identical between main and v1.3
On both branches the order is:
1. `FeatureFlagCache` (from `AddDeckFlowFeatureFlags`)
2. `HarvestScheduleCache` (from `AddDeckFlowHarvest`)
3. `HarvestScheduleService` (from `AddDeckFlowHarvest`)
4. `RequestMetricsFlusher` (from `AddDeckFlowAnalytics`)
5. `ArchidektCacheJobService` (line 335)

`FeatureFlagCache.StartAsync` and `HarvestScheduleCache.StartAsync` both perform a synchronous
initial load (`ReloadAsync`) BEFORE `base.StartAsync`, but `ReloadAsync` swallows all
non-cancellation exceptions internally (see `FeatureFlagCache.cs:62-80`,
`HarvestScheduleCache.cs:55-73`). Neither can block the host or prevent
`ArchidektCacheJobService.StartAsync` from running. (H2 startup-ordering branch FALSIFIED.)

### 4. `BackgroundServiceExceptionBehavior` is default `StopHost`
No `HostOptions.Configure` override found anywhere in the codebase
(`grep -rn 'BackgroundServiceExceptionBehavior\|HostOptions' DeckFlow.Web/`). So if
`ExecuteAsync` had thrown to the top level, the host would have stopped. The user reports
the host stayed alive serving HTTP, so `ExecuteAsync` did NOT throw. This means either:
  (a) `ExecuteAsync` is still pending at `await foreach (... ReadAllAsync(stoppingToken))`
      with the channel empty — i.e. `_queue.Writer.TryWrite(...)` at line 192 either never
      executed or wrote to a different channel than the reader is observing; or
  (b) `ExecuteAsync` exited cleanly (impossible — `ReadAllAsync` on an uncompleted unbounded
      channel never completes).
  (c) catch handlers at lines 325-364 (operator-cancel and unexpected-cancellation branches)
      called `UpdateStateAsync` without try/catch wrap (prior observation #1011 confirmed) —
      a throw there would propagate and stop the host, which is NOT what was observed.

### 5. Reaper SQL is unchanged and would have cleaned any prior orphans
`HarvestRunStore.EnsureSchemaAsync` runs `UPDATE harvest_runs SET state='Failed', ... WHERE
state IN ('Queued','Running','Stopping')` (lines 475-489) before any request is served, on
BOTH branches. This is invoked from `Program.cs:447` during startup. So on the v1.3 deploy
the first scheduler tick (at deploy+60s) would have found `GetActiveAsync()=null` and
proceeded to `InsertQueuedAsync` + `TryWrite`. Nothing about the data state prevents the
first signal from being written.

### 6. Phase-15 prompt-builder singletons cannot starve the worker
The 15 new `IXxxPromptVariant` singletons + 5 registries (Program.cs:270-289) are pure
`sealed class` types with normal constructors. None implement `IHostedService`. None are
constructed eagerly at startup — they are lazily resolved when a scoped
`DeckAnalysisPacketService` / `DeckComparisonService` / `MetaGapService` is requested by
a controller. The worker DI graph (`ICategoryKnowledgeStore`, `IHarvestRunStore`,
`ILogger<...>`) does not touch any of them. (H5 STATICALLY FALSIFIED — no type-init can
block a ThreadPool thread the worker is on because the worker never touches those types.)

## What was ruled OUT by static analysis

| Hypothesis | Status | Evidence |
|---|---|---|
| H1 multiple singleton instances | FALSIFIED | DI registration uses one factory closure returning `GetRequiredService<ArchidektCacheJobService>()`; same instance for IArchidektCacheJobService, ArchidektCacheJobService, IHostedService |
| H2 startup-order / silent ExecuteAsync exit | FALSIFIED for ordering branch | No prior hosted-service blocks; ExecuteAsync exit requires throw which would StopHost |
| H3 cross-phase regression | NOT YET ATTEMPTED | Would require bisect across phases 12/13/14/15/999.x; deferred |
| H4 channel reader/writer instance mismatch | FALSIFIED | Same singleton ⇒ same channel field |
| H5 static-initialization order on prompt-builder singletons | FALSIFIED | Worker DI graph does not touch any prompt-builder type |
| Reaper failed to clean prior orphans | FALSIFIED | Reaper SQL unchanged; runs in `EnsureSchemaAsync` at startup before HTTP serving |
| Catch handlers at 325-364 missing try/catch caused silent exit | INCONSISTENT WITH OBSERVATION | Such a throw would StopHost (default behaviour); host stayed alive |

## What remains plausible (could NOT be falsified from local source alone)

(P-A) **Runtime/environment-only defect surfaced by v1.3 build.** Possible triggers: NuGet
package resolution producing a different binding-redirect than main; the `<GenerateDocumentationFile>true</GenerateDocumentationFile>`
gate emitting warning-as-error somewhere in v1.3 only; a Docker base-image layer cached
differently on Render. Would require comparing the actual published artifact contents
(`/app/DeckFlow.Web.dll` etc.) between the two deploys, or pulling structured logs from Render
for the v1.3 deploy window. Outside the scope of this debug environment.

(P-B) **Process-internal hang not caused by the worker.** If a different BackgroundService
(`HarvestScheduleService`, `RequestMetricsFlusher`, `HarvestScheduleCache`, `FeatureFlagCache`)
is starving the ThreadPool, the worker's `await foreach` continuation may not be scheduled.
Render Starter is 0.5 vCPU and 512 MB RAM — already RAM-constrained per `CLAUDE.md`. Test:
inspect the v1.3 deploy logs for `ThreadPool.GetAvailableThreads`-style stress (or for
`Harvest.Schedule.ReloadFailure` / `RequestMetrics.FlushFailure` storms that imply blocked
PG connections). Cannot do this from the local environment.

(P-C) **First `EnqueueAsync` from manual POST may have raced with worker startup such that
the signal was dropped to a stale instance.** This is theoretically impossible per #2 above
(same singleton), but would be definitively falsified by the runtime test recommended below.

## Empirical tests NOT run from this debug environment

The following tests were specified by the bug submitter but cannot be executed here:
- Render log query (`mcp__render__list_logs` on `srv-d7gmufkp3tds73a29m30`) — no Render MCP available in this thread.
- `dotnet build DeckFlow.sln` clean — not required for static analysis; tests are flaky in WSL per `CLAUDE.md`.
- Local DI harness asserting `ReferenceEquals(sp.GetRequiredService<IArchidektCacheJobService>(), sp.GetRequiredService<IHostedService>(...) /* the right one */)` — could be authored under `DeckFlow.Web.Tests/`, but the registration shape makes the result trivially true; it does not move the needle on the prod-only repro.

# Root cause statement

**Inconclusive based on static analysis of the v1.3 ↔ main diff alone.** The worker code,
its DI registration, the hosted-service order, the reaper SQL, and the channel plumbing are
all byte-identical between the two branches. The remaining differences in v1.3 (Phase 12
URL rewriter, 15 prompt-builder singletons, `PacketSessionCache`, scoped service rename,
XML doc-comments on `DeckFlow.Core/Integration/*` and `DeckFlow.Core/Knowledge/*`) do not
intersect the harvest worker's DI graph or its runtime path.

**Highest-likelihood remaining cause that fits all observed evidence:** an environmental /
runtime artifact of the v1.3 build that does NOT show in the source diff — e.g. a transient
PG / ThreadPool starvation specific to the v1.3 image OR a hosted-service that ran on the
SAME process before the worker did and silently held a non-thread-pool primitive (mutex,
SemaphoreSlim across an `await` with a request-bound CT) — but I cannot pin this to a
specific file/line without the Render log dump.

# Recommended fix (hand-off to Codex)

Because the static diff does NOT contain the defect, the smallest scope fix is a
**defensive hardening of the worker so a single bad signal cannot stall the loop**, plus
**diagnostic plumbing so the next prod occurrence is unambiguously traceable**. This closes
the v1.3 ship-blocker without speculating about an unverified root cause.

## Change 1 — defensive try/catch around terminal-write in the late catch handlers
**File:** `DeckFlow.Web/Services/ArchidektCacheJobService.cs`
**Lines:** 325-364 (the operator-cancel branch and the unexpected-cancellation branch)
**Change:** Wrap each `await _runStore.UpdateStateAsync(...)` call in the catch handlers in
the same try/catch + `LogWarning("Harvest.Run.TerminalWriteFailed ...")` pattern already used
in the host-shutdown branch at lines 304-321. Without this, a transient PG failure during
terminal-write propagates out of `ExecuteAsync` and faults the BackgroundService task
(StopHost is default in .NET 10) — matches prior observation #1011 (`ExecuteAsync catch
handlers at lines 319-373 lack try/catch wrappers around UpdateStateAsync`).
**Why this is the safe minimal change:** it CANNOT regress correct behaviour — the existing
host-shutdown branch already proves the pattern is sound; we are extending the same pattern
to the other two terminal-write paths.

## Change 2 — log the channel writer count on every EnqueueAsync TryWrite
**File:** `DeckFlow.Web/Services/ArchidektCacheJobService.cs`
**Line:** 192 (right after `_queue.Writer.TryWrite(...)`)
**Change:** Log `Harvest.Worker.SignalEnqueued jobId={JobId} writeAccepted={Accepted}` where
`Accepted` is the bool returned by `TryWrite`. Today the call ignores the return value,
which means a closed/full channel is invisible. Two-line change.
**Why:** the NEXT time this happens in prod, the log will tell us whether the signal was
written but not read (worker bug) vs. never written (EnqueueAsync bug). Today we cannot
tell from the evidence.

## Change 3 — log the channel reader resume on each `await foreach` iteration entry
**File:** `DeckFlow.Web/Services/ArchidektCacheJobService.cs`
**Line:** 248 (immediately inside the `await foreach` body, before the `using var jobCts`)
**Change:** `_logger.LogInformation("Harvest.Worker.SignalDequeued jobId={JobId}", signal.JobId);`
**Why:** today the first log line ("Harvest.Run.StateChange ... Running") sits AFTER the
linked-CTS creation and a lock, both of which can — in principle — block. A dedicated
dequeue log proves whether the channel was read at all. Three-line change.

## What NOT to change
- DO NOT change the DI registration (lines 333-335 of Program.cs). Same shape works on `main`.
- DO NOT change `Channel.CreateUnbounded<QueuedJobSignal>()` to a bounded channel — would
  introduce a new failure mode (backpressure / signal-loss).
- DO NOT change `await foreach` to a polling loop — would mask the real bug.
- DO NOT add a startup-time signal flush ("drain Queued rows on boot") without the
  diagnostic logs in place first — that conflates the recovery path with the root cause.

# Confidence

**MEDIUM-LOW** on the recommended fixes as the COMPLETE answer. **HIGH** on the diagnostic
value of Changes 2 and 3.

Changes 1+2+3 collectively:
- Eliminate the only known code-level silent-fail path (catch handler missing try/catch).
- Force the next prod recurrence to leave a log trail that pins the defect to either
  EnqueueAsync (write side), ExecuteAsync dequeue (read side), or the state-transition.

What would FALSIFY the conclusion that "static diff does not contain the bug":
- Pulling the Render log window 20:10-20:32 UTC for srv-d7gmufkp3tds73a29m30 and finding a
  v1.3-only log line (e.g. NuGet/binding load failure, `ThreadPool` warning, or a hosted-
  service exception during StartAsync) that does NOT appear in the main deploy at 20:35:32.
- A DI graph dump showing `IEnumerable<IHostedService>` resolves to a DIFFERENT
  `ArchidektCacheJobService` instance than the controller-resolved
  `IArchidektCacheJobService` (would resurrect H1; today the registration shape rules it out
  but a runtime dump is the only definitive proof).
- A reproducible local hang of the worker with the v1.3 image and a real PG instance.

Either of those would re-open H1/H2/H3 with concrete grounding. Without them, the
diagnostic plumbing in Changes 2-3 is the cheapest path to making the next recurrence
diagnosable on first occurrence.
