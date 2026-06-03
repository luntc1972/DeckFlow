---
phase: 07-harvest-controls-stats
plan: 03
type: execute
wave: 2
depends_on: [01]
files_modified:
  - DeckFlow.Web/Services/Harvest/IHarvestScheduleCache.cs
  - DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs
  - DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs
autonomous: true
requirements: [HARV-04, HARV-05]
tags: [harvest, schedule, cache, hosted-service, feature-flag-gate]

must_haves:
  truths:
    - "IHarvestScheduleCache exposes a Snapshot() returning HarvestScheduleSnapshot from in-memory state (D-07)"
    - "HarvestScheduleCache.StartAsync runs ReloadAsync synchronously before host signals ready, ensuring the schedule snapshot is populated before Kestrel binds (D-07, S-5)"
    - "Reload preserves the existing snapshot on transient PG failure (S-7 mirror)"
    - "PeriodicTimer poller refreshes the snapshot every 30s as a safety net behind the explicit-invalidate write path (D-07)"
    - "HarvestScheduleService is a BackgroundService that ticks every 60s and enqueues a 60-min sweep when now >= last_success_utc + interval_hours, NOT paused, interval_hours IS NOT NULL (D-06)"
    - "Whole scheduler is gated by IFeatureFlagCache.IsEnabled(\"harvest.cron.enabled\") — flag off short-circuits the tick (D-06, S-8)"
  artifacts:
    - path: "DeckFlow.Web/Services/Harvest/IHarvestScheduleCache.cs"
      provides: "Schedule cache contract: Snapshot() + ReloadAsync(CancellationToken)"
      contains: "interface IHarvestScheduleCache"
    - path: "DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs"
      provides: "BackgroundService impl with sync StartAsync initial load + 30s PeriodicTimer poller + atomic snapshot replace"
      contains: "sealed class HarvestScheduleCache"
    - path: "DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs"
      provides: "BackgroundService that wakes every 60s, gated by harvest.cron.enabled, fires bulk harvest when due"
      contains: "sealed class HarvestScheduleService"
  key_links:
    - from: "HarvestScheduleCache.StartAsync"
      to: "IHarvestScheduleStore.GetAsync"
      via: "Initial sync load before host ready signal"
      pattern: "StartAsync.*ReloadAsync"
    - from: "HarvestScheduleCache.ExecuteAsync"
      to: "PeriodicTimer 30s tick → ReloadAsync"
      via: "Backstop refresh"
      pattern: "PeriodicTimer\\(.*30"
    - from: "HarvestScheduleService.TickAsync"
      to: "IFeatureFlagCache.IsEnabled(\"harvest.cron.enabled\")"
      via: "Kill-switch gate"
      pattern: "harvest.cron.enabled"
    - from: "HarvestScheduleService.TickAsync"
      to: "IArchidektCacheJobService.EnqueueAsync(60min)"
      via: "Schedule firing"
      pattern: "EnqueueAsync.*FromMinutes\\(60\\)"
---

<objective>
Mirror the Phase 6 `FeatureFlagCache` pattern for the harvest schedule, and implement the periodic-tick BackgroundService that fires bulk harvests on the configured interval. The cache + service together deliver HARV-04 (pause/resume) and HARV-05 (interval picker persisted in PG).

Purpose: makes the cron observable from the operator's perspective without per-tick PG roundtrips. Snapshot reads from cache; cache hot-reloads on admin write (Plan 04 hooks the reload).

Output:
- `IHarvestScheduleCache` interface (Snapshot + ReloadAsync), exact analog of `IFeatureFlagCache`.
- `HarvestScheduleCache` sealed class — `BackgroundService` with sync StartAsync initial load and 30s poller.
- `HarvestScheduleService` sealed class — separate `BackgroundService` ticking every 60s, gated by `harvest.cron.enabled`, calling `IArchidektCacheJobService.EnqueueAsync(TimeSpan.FromMinutes(60), ...)` when due.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/phases/07-harvest-controls-stats/07-CONTEXT.md
@.planning/phases/07-harvest-controls-stats/07-RESEARCH.md
@.planning/phases/07-harvest-controls-stats/07-PATTERNS.md
@.planning/phases/07-harvest-controls-stats/07-01-SUMMARY.md
@DeckFlow.Web/Services/FeatureFlags/IFeatureFlagCache.cs
@DeckFlow.Web/Services/FeatureFlags/FeatureFlagCache.cs
@DeckFlow.Web/Services/ScryfallTaggerService.cs
@DeckFlow.Web/Services/ArchidektCacheJobService.cs
@DeckFlow.Web/Services/Harvest/IHarvestScheduleStore.cs
@DeckFlow.Web/Services/Harvest/HarvestRunModels.cs
@DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs

<interfaces>
<!-- Authoritative cache + service contracts. -->

From DeckFlow.Web/Services/Harvest/IHarvestScheduleCache.cs (NEW — Task 1):
```csharp
public interface IHarvestScheduleCache
{
    HarvestScheduleSnapshot Snapshot();
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
```

From DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs (NEW — Task 1):
```csharp
public sealed class HarvestScheduleCache : BackgroundService, IHarvestScheduleCache
{
    public HarvestScheduleCache(IHarvestScheduleStore store, ILogger<HarvestScheduleCache> logger);
    internal HarvestScheduleCache(IHarvestScheduleStore store);   // test seam
    public HarvestScheduleSnapshot Snapshot();
    public Task ReloadAsync(CancellationToken cancellationToken = default);
    public override Task StartAsync(CancellationToken cancellationToken);
    protected override Task ExecuteAsync(CancellationToken stoppingToken);
}
```

From DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs (NEW — Task 2):
```csharp
public sealed class HarvestScheduleService : BackgroundService
{
    public HarvestScheduleService(
        IFeatureFlagCache flagCache,
        IHarvestScheduleCache scheduleCache,
        IHarvestRunStore runStore,
        IArchidektCacheJobService jobService,
        ILogger<HarvestScheduleService> logger);
    protected override Task ExecuteAsync(CancellationToken stoppingToken);
}
```
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: HarvestScheduleCache (interface + sealed BackgroundService impl) mirroring FeatureFlagCache</name>
  <files>DeckFlow.Web/Services/Harvest/IHarvestScheduleCache.cs, DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs</files>
  <behavior>
    - `IHarvestScheduleCache` declares `Snapshot()` and `ReloadAsync(CancellationToken)`. Snapshot is sync (returns the in-memory record reference); ReloadAsync is async and used by both the controller (Plan 04 calls after admin write) and the internal poller.
    - `HarvestScheduleCache` extends `BackgroundService` and implements `IHarvestScheduleCache`.
    - Field `private volatile HarvestScheduleSnapshot _snapshot` initialized to `new HarvestScheduleSnapshot(IntervalHours: null, Paused: false, UpdatedUtc: DateTimeOffset.MinValue)` so callers always get a non-null snapshot even before first reload.
    - `StartAsync` (override) calls `await ReloadAsync(cancellationToken)` THEN `await base.StartAsync(...)`. This makes initial load synchronous w.r.t. host ready signal — Kestrel binds with the schedule snapshot already populated.
    - `ExecuteAsync` uses `PeriodicTimer(TimeSpan.FromSeconds(30))`; loops calling `ReloadAsync(stoppingToken)`. OCE on stoppingToken is the normal-shutdown swallow path.
    - `ReloadAsync` calls `_store.GetAsync(ct)` and assigns the result to `_snapshot` atomically. On exception (other than OCE on a passed-in cancelled token), log error with `_snapshot.UpdatedUtc` placeholder and PRESERVE the existing snapshot — never replace with a stub on PG failure.
    - Public ctor takes `IHarvestScheduleStore` + `ILogger<HarvestScheduleCache>`; internal ctor takes only the store and uses `NullLogger<HarvestScheduleCache>.Instance` for tests.
  </behavior>
  <action>
    Create two files mirroring `Services/FeatureFlags/{IFeatureFlagCache,FeatureFlagCache}.cs` line-for-line, swapping types as listed.

    **`IHarvestScheduleCache.cs`:**
    ```csharp
    using System.Threading;
    using System.Threading.Tasks;

    namespace DeckFlow.Web.Services.Harvest;

    /// <summary>
    /// In-memory cache of the harvest_schedule single-row state (D-06).
    /// Hot-reloaded on admin write (D-07) and on a 30s PeriodicTimer backstop.
    /// </summary>
    public interface IHarvestScheduleCache
    {
        /// <summary>Returns the current snapshot. Always non-null (defaults to Off/unpaused before first reload).</summary>
        HarvestScheduleSnapshot Snapshot();

        /// <summary>Forces a refresh from harvest_schedule. Called by AdminHarvestController after schedule writes.</summary>
        Task ReloadAsync(CancellationToken cancellationToken = default);
    }
    ```

    **`HarvestScheduleCache.cs`:**
    ```csharp
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    namespace DeckFlow.Web.Services.Harvest;

    /// <summary>
    /// Background service + singleton cache of the harvest_schedule row. Mirrors
    /// FeatureFlagCache: synchronous initial load via StartAsync, 30s PeriodicTimer
    /// poller as a backstop, atomic snapshot replace, preserve-on-failure semantics.
    /// </summary>
    public sealed class HarvestScheduleCache : BackgroundService, IHarvestScheduleCache
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
        private static readonly HarvestScheduleSnapshot DefaultSnapshot =
            new(IntervalHours: null, Paused: false, UpdatedUtc: DateTimeOffset.MinValue);

        private readonly IHarvestScheduleStore _store;
        private readonly ILogger<HarvestScheduleCache> _logger;
        private volatile HarvestScheduleSnapshot _snapshot = DefaultSnapshot;

        public HarvestScheduleCache(IHarvestScheduleStore store, ILogger<HarvestScheduleCache> logger)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(logger);
            _store = store;
            _logger = logger;
        }

        /// <summary>Test seam — bypasses the logger so unit tests don't have to wire one.</summary>
        internal HarvestScheduleCache(IHarvestScheduleStore store)
            : this(store, NullLogger<HarvestScheduleCache>.Instance) { }

        public HarvestScheduleSnapshot Snapshot() => _snapshot;

        public async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var fresh = await _store.GetAsync(cancellationToken).ConfigureAwait(false);
                _snapshot = fresh;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Never replace a good snapshot with a stub on transient PG failure.
                _logger.LogError(exception,
                    "Harvest.Schedule.ReloadFailure could not refresh harvest_schedule snapshot; existing snapshot preserved (intervalHours={IntervalHours} paused={Paused}).",
                    _snapshot.IntervalHours, _snapshot.Paused);
            }
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(false);
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(PollInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    await ReloadAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }
    }
    ```

    Build must compile after this task. The cache has no consumers yet — DI registration lands in Plan 07.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -q "interface IHarvestScheduleCache" DeckFlow.Web/Services/Harvest/IHarvestScheduleCache.cs && grep -q "sealed class HarvestScheduleCache" DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs && grep -q "PeriodicTimer(PollInterval)" DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs && grep -q "Harvest.Schedule.ReloadFailure" DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs && grep -q "ArgumentNullException.ThrowIfNull" DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs</automated>
  </verify>
  <done>Build exits 0; cache class extends BackgroundService and implements IHarvestScheduleCache; preserve-on-failure log message present; sync StartAsync initial load present.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: HarvestScheduleService BackgroundService — 60s tick, flag-gated, fires bulk harvest when due</name>
  <files>DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs</files>
  <behavior>
    - Sealed `BackgroundService` with `TickInterval = TimeSpan.FromSeconds(60)`.
    - Five-arg ctor (`IFeatureFlagCache`, `IHarvestScheduleCache`, `IHarvestRunStore`, `IArchidektCacheJobService`, `ILogger<HarvestScheduleService>`); `ArgumentNullException.ThrowIfNull` on each.
    - `ExecuteAsync` uses PeriodicTimer 60s loop; calls private `TickAsync(CancellationToken)` each tick.
    - `TickAsync` order of operations:
      1. `if (!_flagCache.IsEnabled("harvest.cron.enabled")) return;` — kill-switch gate (Phase 6 D-12 / FLAG-04 / S-8).
      2. `var snapshot = _scheduleCache.Snapshot();` — no per-tick PG roundtrip for schedule.
      3. `if (snapshot.Paused || snapshot.IntervalHours is null) return;` — Off or paused.
      4. `var lastSuccess = await _runStore.GetLastSuccessUtcAsync(ct);` — single PG query per tick (acceptable; 60s cadence).
      5. If `lastSuccess` is null, fire immediately (no prior successful run — interpret as "fire ASAP after enabling cron").
      6. Else compute `nextDue = lastSuccess.Value + TimeSpan.FromHours(snapshot.IntervalHours.Value)`. If `DateTimeOffset.UtcNow < nextDue`, return.
      7. Fire: `await _jobService.EnqueueAsync(TimeSpan.FromMinutes(60), ct);`. Log structured info: `Harvest.Schedule.Tick.Fired intervalHours={IntervalHours} lastSuccess={LastSuccess} nextDue={NextDue}`.
    - On any unexpected exception inside `TickAsync` (other than OCE on stoppingToken), log error and CONTINUE (do not let one bad tick kill the loop). The next tick retries.
    - The service does NOT directly write to harvest_runs — `EnqueueAsync` does that (Plan 02). The service only triggers the job service.
  </behavior>
  <action>
    Create `DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs`:
    ```csharp
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using DeckFlow.Web.Services.FeatureFlags;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    namespace DeckFlow.Web.Services.Harvest;

    /// <summary>
    /// Recurring harvest scheduler (D-06). Wakes every 60s, reads the cached
    /// harvest_schedule snapshot, and fires a 60-min bulk harvest when due.
    /// Whole loop is gated by IFeatureFlagCache.IsEnabled("harvest.cron.enabled")
    /// (kill switch from /Admin/Flags — Phase 6 FLAG-04 carry-forward).
    /// </summary>
    public sealed class HarvestScheduleService : BackgroundService
    {
        private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);
        private const string CronEnabledFlagKey = "harvest.cron.enabled";

        private readonly IFeatureFlagCache _flagCache;
        private readonly IHarvestScheduleCache _scheduleCache;
        private readonly IHarvestRunStore _runStore;
        private readonly IArchidektCacheJobService _jobService;
        private readonly ILogger<HarvestScheduleService> _logger;

        public HarvestScheduleService(
            IFeatureFlagCache flagCache,
            IHarvestScheduleCache scheduleCache,
            IHarvestRunStore runStore,
            IArchidektCacheJobService jobService,
            ILogger<HarvestScheduleService> logger)
        {
            ArgumentNullException.ThrowIfNull(flagCache);
            ArgumentNullException.ThrowIfNull(scheduleCache);
            ArgumentNullException.ThrowIfNull(runStore);
            ArgumentNullException.ThrowIfNull(jobService);
            ArgumentNullException.ThrowIfNull(logger);
            _flagCache = flagCache;
            _scheduleCache = scheduleCache;
            _runStore = runStore;
            _jobService = jobService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TickInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    try
                    {
                        await TickAsync(stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Harvest.Schedule.Tick.Failure error={Message}", exception.Message);
                        // Do not exit the loop — the next 60s tick will retry.
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        private async Task TickAsync(CancellationToken cancellationToken)
        {
            // FLAG-04 / D-06: kill-switch gate. Off → return without any PG/job work.
            if (!_flagCache.IsEnabled(CronEnabledFlagKey))
            {
                return;
            }

            var snapshot = _scheduleCache.Snapshot();
            if (snapshot.Paused || snapshot.IntervalHours is null)
            {
                return;
            }

            var lastSuccess = await _runStore.GetLastSuccessUtcAsync(cancellationToken).ConfigureAwait(false);

            DateTimeOffset? nextDue = lastSuccess.HasValue
                ? lastSuccess.Value + TimeSpan.FromHours(snapshot.IntervalHours.Value)
                : null;  // No prior success — fire immediately.

            var now = DateTimeOffset.UtcNow;
            if (nextDue.HasValue && now < nextDue.Value)
            {
                return;
            }

            _logger.LogInformation(
                "Harvest.Schedule.Tick.Fired intervalHours={IntervalHours} lastSuccess={LastSuccess} nextDue={NextDue}",
                snapshot.IntervalHours, lastSuccess, nextDue);

            await _jobService.EnqueueAsync(TimeSpan.FromMinutes(60), cancellationToken).ConfigureAwait(false);
        }
    }
    ```
    Build must compile. No DI registration in this plan — Plan 07 wires it.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -q "harvest.cron.enabled" DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs && grep -q "TimeSpan.FromMinutes(60)" DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs && grep -q "_flagCache.IsEnabled" DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs && grep -q "PeriodicTimer(TickInterval)" DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs && grep -q "Harvest.Schedule.Tick.Fired" DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs && grep -q "snapshot.Paused" DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs</automated>
  </verify>
  <done>Build exits 0; service is sealed and extends BackgroundService; flag-gate present on the literal key `harvest.cron.enabled`; 60-min EnqueueAsync call present; structured log template `Harvest.Schedule.Tick.Fired` present; pause/null short-circuit present.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Hosted scheduler → IArchidektCacheJobService.EnqueueAsync | In-process call; same trust zone. |
| Hosted scheduler → IFeatureFlagCache.IsEnabled | In-process read of cached flag dict (Phase 6). |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-07-11 | Denial of service | Schedule tick loops | mitigate | 60s PeriodicTimer; per-tick DB read is one query (`GetLastSuccessUtcAsync`); flag-gate short-circuits when off. |
| T-07-12 | Tampering | harvest.cron.enabled flag | accept | Operator-only via /Admin/Flags (Phase 6 BasicAuth + antiforgery). |
| T-07-13 | Repudiation | Schedule firing without operator action | accept | All scheduled fires write a `harvest_runs` row with kind='bulk' and timestamps — implicit audit trail. |
| T-07-14 | Denial of service | Stuck tick task | mitigate | Per-tick try/catch (other than OCE on stoppingToken) keeps the loop alive across transient PG/job-service errors. |
| T-07-15 | Spoofing | Cache snapshot read | accept | In-process; no external trust boundary crossed. |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` exits 0.
- `grep -c "harvest.cron.enabled" DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs` ≥ 1.
- `grep -c "TimeSpan.FromMinutes(60)" DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs` ≥ 1.
- `grep -c "PeriodicTimer" DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs` ≥ 1 and same in HarvestScheduleService.cs.
- `grep -c "Harvest.Schedule.ReloadFailure" DeckFlow.Web/Services/Harvest/HarvestScheduleCache.cs` ≥ 1.
- No new HTTP client introduced; no Microsoft.Extensions.Http.Resilience reference added.
</verification>

<success_criteria>
- `IHarvestScheduleCache` and `HarvestScheduleCache` ship and follow `IFeatureFlagCache`/`FeatureFlagCache` shape exactly.
- `HarvestScheduleService` ticks every 60s, gated by `harvest.cron.enabled`, fires `EnqueueAsync(60min)` when due (or immediately on first run with non-null interval).
- Both BackgroundServices preserve the existing snapshot on transient PG failure.
- No DI registration yet (Plan 07 wires the singleton + IHostedService dual registration plus the scheduler's hosted service).
</success_criteria>

<output>
After completion, create `.planning/phases/07-harvest-controls-stats/07-03-SUMMARY.md` listing: cache poll interval, schedule tick interval, the literal flag key, and any deviation from the FeatureFlagCache pattern.
</output>
