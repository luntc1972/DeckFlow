using System.Reflection;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests.Services;

public sealed class HarvestScheduleServiceTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 14, false)]
    [InlineData(1, 15, true)]
    [InlineData(3, 59, false)]
    [InlineData(3, 60, true)]
    [InlineData(50, 59, false)]
    [InlineData(50, 60, true)]
    public async Task TickAsync_AppliesFailureBackoff(int failures, int minutesAgo, bool expectFire)
    {
        var path = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid() + ".db");
        var store = new HarvestRunStore(path);
        await store.EnsureSchemaAsync();
        var now = new DateTimeOffset(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < failures; i++)
        {
            var completed = now.AddMinutes(-minutesAgo);
            await SeedRunAsync(store, HarvestRunKind.Bulk, HarvestRunState.Failed, completed);
        }

        var job = new RecordingJob();
        var service = new HarvestScheduleService(
            new FakeFeatureFlagCache(), new FixedSchedule(1), new FixedUpdateSchedule(null), store, job,
            NullLogger<HarvestScheduleService>.Instance,
            new FakeTimeProvider(now));
        var tick = typeof(HarvestScheduleService).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)tick.Invoke(service, new object[] { CancellationToken.None })!;

        Assert.Equal(expectFire ? 1 : 0, job.Calls);
    }

    [Fact]
    public async Task TickAsync_DoesNotFireBeforeSuccessInterval()
    {
        var path = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid() + ".db");
        var store = new HarvestRunStore(path);
        await store.EnsureSchemaAsync();
        var now = new DateTimeOffset(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);
        await SeedRunAsync(store, HarvestRunKind.Bulk, HarvestRunState.Succeeded, now.AddMinutes(-10));

        var job = new RecordingJob();
        var service = new HarvestScheduleService(
            new FakeFeatureFlagCache(), new FixedSchedule(1), new FixedUpdateSchedule(null), store, job,
            NullLogger<HarvestScheduleService>.Instance,
            new FakeTimeProvider(now));
        var tick = typeof(HarvestScheduleService).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)tick.Invoke(service, new object[] { CancellationToken.None })!;

        Assert.Equal(0, job.Calls);
    }

    [Fact]
    public async Task TickAsync_Fires_EnqueuesScheduledBulkRunForFireDuration()
    {
        var path = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid() + ".db");
        var store = new HarvestRunStore(path);
        await store.EnsureSchemaAsync();
        var job = new RecordingJob();
        var service = new HarvestScheduleService(
            new FakeFeatureFlagCache(), new FixedSchedule(1), new FixedUpdateSchedule(null), store, job,
            NullLogger<HarvestScheduleService>.Instance,
            new FakeTimeProvider(new DateTimeOffset(2026, 6, 12, 12, 0, 0, TimeSpan.Zero)));
        var tick = typeof(HarvestScheduleService).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)tick.Invoke(service, new object[] { CancellationToken.None })!;

        Assert.Equal([(HarvestRunKind.Bulk, TimeSpan.FromMinutes(60), HarvestTriggerSource.Scheduled)], job.Enqueued);
    }

    [Fact]
    public async Task TickAsync_UpdateScheduleDue_EnqueuesScheduledUpdateRunEndToEnd()
    {
        var path = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid() + ".db");
        var runStore = new HarvestRunStore(path);
        await runStore.EnsureSchemaAsync();
        var updateStore = new HarvestUpdateScheduleStore(path);
        var now = new DateTimeOffset(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);
        await updateStore.SaveAsync(15, paused: false, now);
        var updateCache = new HarvestUpdateScheduleCache(updateStore);
        await updateCache.ReloadAsync();
        await SeedRunAsync(runStore, HarvestRunKind.Update, HarvestRunState.Succeeded, now.AddMinutes(-20));
        var job = new RecordingJob();
        var service = new HarvestScheduleService(new FakeFeatureFlagCache(), new FixedSchedule(null), updateCache, runStore, job, NullLogger<HarvestScheduleService>.Instance, new FakeTimeProvider(now));

        await InvokeTickAsync(service);

        Assert.Equal([(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Scheduled)], job.Enqueued);
    }

    [Theory]
    [InlineData(HarvestRunKind.Update, HarvestRunState.Queued)]
    [InlineData(HarvestRunKind.Update, HarvestRunState.Running)]
    [InlineData(HarvestRunKind.Update, HarvestRunState.Stopping)]
    [InlineData(HarvestRunKind.Bulk, HarvestRunState.Queued)]
    [InlineData(HarvestRunKind.Bulk, HarvestRunState.Running)]
    public async Task TickAsync_RunActive_SkipsDueKind(HarvestRunKind activeKind, HarvestRunState state)
    {
        var store = await CreateStoreAsync();
        await SeedRunAsync(store, activeKind, state, Now, HarvestTriggerSource.Manual);
        var dueKind = activeKind == HarvestRunKind.Update ? HarvestRunKind.Bulk : HarvestRunKind.Update;
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(dueKind == HarvestRunKind.Bulk ? 1 : null), new FixedUpdateSchedule(dueKind == HarvestRunKind.Update ? 15 : null), Now));
        Assert.Empty(job.Enqueued);
    }

    [Fact]
    public async Task TickAsync_UpdateDueWhileManualBulkRuns_SkipsThenFiresAfterRunEnds()
    {
        var store = await CreateStoreAsync();
        var clock = new FakeTimeProvider(Now);
        var id = await SeedRunAsync(store, HarvestRunKind.Bulk, HarvestRunState.Running, Now, HarvestTriggerSource.Manual);
        var job = new RecordingJob();
        var service = CreateService(store, job, new FixedSchedule(null), new FixedUpdateSchedule(15), clock);
        await InvokeTickAsync(service);
        await store.UpdateStateAsync(id, HarvestRunState.Succeeded, null, Now, null, null, null);
        clock.Advance(TimeSpan.FromSeconds(60));
        await InvokeTickAsync(service);
        Assert.Equal([(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Scheduled)], job.Enqueued);
    }

    [Fact]
    public async Task TickAsync_BulkEnqueueThrows_StillEvaluatesUpdateAndCompletes()
    {
        var store = await CreateStoreAsync();
        var job = new ThrowingBulkJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(1), new FixedUpdateSchedule(15), Now));
        Assert.Equal([(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Scheduled)], job.Enqueued);
    }

    [Fact]
    public async Task TickAsync_BothKindsDue_EnqueuesOnlyBulkThisTick()
    {
        var store = await CreateStoreAsync();
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(1), new FixedUpdateSchedule(15), Now));
        Assert.Equal([(HarvestRunKind.Bulk, TimeSpan.FromMinutes(60), HarvestTriggerSource.Scheduled)], job.Enqueued);
    }

    [Theory]
    [InlineData(HarvestRunKind.Bulk)]
    [InlineData(HarvestRunKind.Update)]
    public async Task TickAsync_ManualSuccess_DoesNotAnchorSchedule(HarvestRunKind kind)
    {
        var store = await CreateStoreAsync();
        await SeedRunAsync(store, kind, HarvestRunState.Succeeded, Now.AddMinutes(kind == HarvestRunKind.Bulk ? -180 : -20));
        await SeedRunAsync(store, kind, HarvestRunState.Succeeded, Now.AddMinutes(-1), HarvestTriggerSource.Manual);
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(kind == HarvestRunKind.Bulk ? 2 : null), new FixedUpdateSchedule(kind == HarvestRunKind.Update ? 15 : null), Now));
        Assert.Equal(kind, Assert.Single(job.Enqueued).Kind);
    }

    [Fact]
    public async Task TickAsync_LegacyNullTriggerBulkSuccess_AnchorsBulk()
    {
        var store = await CreateStoreAsync();
        await SeedRunAsync(store, HarvestRunKind.Bulk, HarvestRunState.Succeeded, Now.AddMinutes(-10), null);
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(2), new FixedUpdateSchedule(null), Now));
        Assert.Empty(job.Enqueued);
    }

    [Theory]
    [InlineData(HarvestRunKind.Bulk)]
    [InlineData(HarvestRunKind.Update)]
    public async Task TickAsync_OtherKindSuccess_DoesNotAnchor(HarvestRunKind kind)
    {
        var store = await CreateStoreAsync();
        await SeedRunAsync(store, kind == HarvestRunKind.Bulk ? HarvestRunKind.Update : HarvestRunKind.Bulk, HarvestRunState.Succeeded, Now.AddMinutes(-1));
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(kind == HarvestRunKind.Bulk ? 1 : null), new FixedUpdateSchedule(kind == HarvestRunKind.Update ? 15 : null), Now));
        Assert.Equal(kind, Assert.Single(job.Enqueued).Kind);
    }

    [Theory]
    [InlineData(1, 14, false)]
    [InlineData(1, 15, true)]
    [InlineData(3, 14, false)]
    [InlineData(3, 15, true)]
    public async Task TickAsync_UpdateFailureBackoff_CapsAtMinuteInterval(int failures, int minutesAgo, bool expectFire)
    {
        var store = await CreateStoreAsync();
        await SeedRunAsync(store, HarvestRunKind.Update, HarvestRunState.Succeeded, Now.AddMinutes(-60));
        for (var i = 0; i < failures; i++) await SeedRunAsync(store, HarvestRunKind.Update, HarvestRunState.Failed, Now.AddMinutes(-minutesAgo));
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(null), new FixedUpdateSchedule(15), Now));
        Assert.Equal(expectFire ? 1 : 0, job.Calls);
    }

    [Fact]
    public async Task TickAsync_BulkFailures_DoNotDelayUpdate()
    {
        var store = await CreateStoreAsync();
        for (var i = 0; i < 3; i++) await SeedRunAsync(store, HarvestRunKind.Bulk, HarvestRunState.Failed, Now.AddMinutes(-1));
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(null), new FixedUpdateSchedule(15), Now));
        Assert.Equal(HarvestRunKind.Update, Assert.Single(job.Enqueued).Kind);
    }

    [Fact]
    public async Task TickAsync_ManualFailures_DoNotBackOffSchedule()
    {
        var store = await CreateStoreAsync();
        await SeedRunAsync(store, HarvestRunKind.Update, HarvestRunState.Succeeded, Now.AddMinutes(-20));
        for (var i = 0; i < 3; i++) await SeedRunAsync(store, HarvestRunKind.Update, HarvestRunState.Failed, Now.AddMinutes(-1), HarvestTriggerSource.Manual);
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(null), new FixedUpdateSchedule(15), Now));
        Assert.Equal(HarvestRunKind.Update, Assert.Single(job.Enqueued).Kind);
    }

    [Fact]
    public async Task TickAsync_InterruptedUpdateRun_NeitherBlocksAnchorsNorBacksOff()
    {
        var store = await CreateStoreAsync();
        await SeedRunAsync(store, HarvestRunKind.Update, HarvestRunState.Succeeded, Now.AddMinutes(-20));
        await SeedRunAsync(store, HarvestRunKind.Update, HarvestRunState.Interrupted, Now.AddMinutes(-1));
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(null), new FixedUpdateSchedule(15), Now));
        Assert.Equal(HarvestRunKind.Update, Assert.Single(job.Enqueued).Kind);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(15, true)]
    public async Task TickAsync_UpdateScheduleOffOrPaused_NeverEnqueuesUpdate(int? minutes, bool paused)
    {
        var store = await CreateStoreAsync();
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(null), new FixedUpdateSchedule(minutes, paused), Now));
        Assert.Empty(job.Enqueued);
    }

    [Fact]
    public async Task TickAsync_CronFlagOff_EnqueuesNeitherKind()
    {
        var store = await CreateStoreAsync();
        var flags = new FakeFeatureFlagCache();
        flags.Flags["service.harvest-cron.enabled"] = false;
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(1), new FixedUpdateSchedule(15), Now, flags));
        Assert.Empty(job.Enqueued);
    }

    [Fact]
    public async Task TickAsync_UpdateNoHistory_FiresOnFirstEnabledTick()
    {
        var store = await CreateStoreAsync();
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(null), new FixedUpdateSchedule(30), Now));
        Assert.Equal([(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Scheduled)], job.Enqueued);
    }

    [Fact]
    public async Task HarvestScheduleCache_ReloadStartedBeforeForcePausedSnapshot_CannotUnpause()
    {
        var store = new GatedReadScheduleStore(new HarvestScheduleSnapshot(2, false, Now));
        var cache = new HarvestScheduleCache(store);
        await cache.ReloadAsync();
        store.HoldNextRead = true;
        var inFlight = cache.ReloadAsync();
        await store.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cache.ForcePausedSnapshot();
        store.Release.TrySetResult();
        await inFlight;
        Assert.True(cache.Snapshot().Paused); Assert.Equal(2, cache.Snapshot().IntervalHours);
        store.Current = store.Current with { Paused = false };
        await cache.ReloadAsync();
        Assert.False(cache.Snapshot().Paused);
    }

    [Fact]
    public async Task HarvestUpdateScheduleCache_ReloadStartedBeforeForcePausedSnapshot_CannotUnpause()
    {
        var store = new GatedReadUpdateScheduleStore(new HarvestUpdateScheduleSnapshot(15, false, Now));
        var cache = new HarvestUpdateScheduleCache(store);
        await cache.ReloadAsync();
        store.HoldNextRead = true;
        var inFlight = cache.ReloadAsync();
        await store.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cache.ForcePausedSnapshot();
        store.Release.TrySetResult();
        await inFlight;
        Assert.True(cache.Snapshot().Paused); Assert.Equal(15, cache.Snapshot().IntervalMinutes);
        store.Current = store.Current with { Paused = false };
        await cache.ReloadAsync();
        Assert.False(cache.Snapshot().Paused);
    }

    [Fact]
    public async Task TickAsync_AfterRateLimitTripWithFailedCacheReloads_EnqueuesNothing()
    {
        var store = await CreateStoreAsync();
        var job = new RecordingJob();
        await InvokeTickAsync(CreateService(store, job, new FixedSchedule(2, paused: true), new FixedUpdateSchedule(15, paused: true), Now));
        Assert.Empty(job.Enqueued);
    }

    private static DateTimeOffset Now => new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    private static async Task<HarvestRunStore> CreateStoreAsync()
    {
        var store = new HarvestRunStore(Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid() + ".db"));
        await store.EnsureSchemaAsync();
        return store;
    }

    private static HarvestScheduleService CreateService(HarvestRunStore store, IArchidektCacheJobService job, IHarvestScheduleCache schedule, IHarvestUpdateScheduleCache update, TimeProvider time, FakeFeatureFlagCache? flags = null) =>
        new(flags ?? new FakeFeatureFlagCache(), schedule, update, store, job, NullLogger<HarvestScheduleService>.Instance, time);

    private static HarvestScheduleService CreateService(HarvestRunStore store, IArchidektCacheJobService job, IHarvestScheduleCache schedule, IHarvestUpdateScheduleCache update, DateTimeOffset time, FakeFeatureFlagCache? flags = null) =>
        CreateService(store, job, schedule, update, new FakeTimeProvider(time), flags);

    private static async Task InvokeTickAsync(HarvestScheduleService service)
    {
        var tick = typeof(HarvestScheduleService).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)tick.Invoke(service, [CancellationToken.None])!;
    }

    private static async Task<Guid> SeedRunAsync(HarvestRunStore store, HarvestRunKind kind, HarvestRunState state, DateTimeOffset timestamp, HarvestTriggerSource? trigger = HarvestTriggerSource.Scheduled)
    {
        var id = await store.InsertQueuedAsync(kind, 60, null, timestamp, trigger);
        if (state != HarvestRunState.Queued)
        {
            await store.UpdateStateAsync(id, state,
                state == HarvestRunState.Running ? timestamp : null,
                state is HarvestRunState.Succeeded or HarvestRunState.Failed or HarvestRunState.Interrupted ? timestamp : null,
                null, null, null);
        }
        return id;
    }

    private sealed class FixedSchedule(int? hours, bool paused = false) : IHarvestScheduleCache
    {
        public HarvestScheduleSnapshot Snapshot() => new(hours, paused, DateTimeOffset.UtcNow);
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ForcePausedSnapshot() { /* Why: these tests never drive a rate-limit trip. */ }
    }

    private sealed class GatedReadScheduleStore(HarvestScheduleSnapshot snapshot) : IHarvestScheduleStore
    {
        public HarvestScheduleSnapshot Current { get; set; } = snapshot;
        public bool HoldNextRead { get; set; }
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public async Task<HarvestScheduleSnapshot> GetAsync(CancellationToken cancellationToken = default)
        {
            var read = Current;
            if (HoldNextRead) { HoldNextRead = false; ReadStarted.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); }
            return read;
        }
        public Task SaveAsync(int? intervalHours, bool paused, DateTimeOffset now, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class GatedReadUpdateScheduleStore(HarvestUpdateScheduleSnapshot snapshot) : IHarvestUpdateScheduleStore
    {
        public HarvestUpdateScheduleSnapshot Current { get; set; } = snapshot;
        public bool HoldNextRead { get; set; }
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public async Task<HarvestUpdateScheduleSnapshot> GetAsync(CancellationToken cancellationToken = default)
        {
            var read = Current;
            if (HoldNextRead) { HoldNextRead = false; ReadStarted.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); }
            return read;
        }
        public Task SaveAsync(int? intervalMinutes, bool paused, DateTimeOffset now, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedUpdateSchedule(int? minutes, bool paused = false) : IHarvestUpdateScheduleCache
    {
        public HarvestUpdateScheduleSnapshot Snapshot() => new(minutes, paused, DateTimeOffset.UtcNow);
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ForcePausedSnapshot() { /* Why: these tests never drive a rate-limit trip. */ }
    }

    private sealed class RecordingJob : IArchidektCacheJobService
    {
        public List<(HarvestRunKind Kind, TimeSpan Duration, HarvestTriggerSource Trigger)> Enqueued { get; } = [];
        public int Calls => Enqueued.Count;
        public Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(HarvestRunKind kind, TimeSpan duration, HarvestTriggerSource trigger, CancellationToken cancellationToken = default)
        {
            Enqueued.Add((kind, duration, trigger));
            return Task.FromResult<ArchidektCacheJobEnqueueResult>(null!);
        }
        public ArchidektCacheJobStatus? GetJob(Guid jobId) => null;
        public ArchidektCacheJobStatus? GetActiveJob() => null;
        public Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class ThrowingBulkJob : IArchidektCacheJobService
    {
        public List<(HarvestRunKind Kind, TimeSpan Duration, HarvestTriggerSource Trigger)> Enqueued { get; } = [];
        public Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(HarvestRunKind kind, TimeSpan duration, HarvestTriggerSource trigger, CancellationToken cancellationToken = default)
        {
            if (kind == HarvestRunKind.Bulk) throw new InvalidOperationException("bulk failure");
            Enqueued.Add((kind, duration, trigger));
            return Task.FromResult<ArchidektCacheJobEnqueueResult>(null!);
        }
        public ArchidektCacheJobStatus? GetJob(Guid jobId) => null;
        public ArchidektCacheJobStatus? GetActiveJob() => null;
        public Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
