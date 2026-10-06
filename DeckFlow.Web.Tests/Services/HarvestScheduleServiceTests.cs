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

    private static async Task InvokeTickAsync(HarvestScheduleService service)
    {
        var tick = typeof(HarvestScheduleService).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)tick.Invoke(service, [CancellationToken.None])!;
    }

    private static async Task SeedRunAsync(HarvestRunStore store, HarvestRunKind kind, HarvestRunState state, DateTimeOffset completed)
    {
        var id = await store.InsertQueuedAsync(kind, 60, null, completed, HarvestTriggerSource.Scheduled);
        await store.UpdateStateAsync(id, state, null, completed, null, null, null);
    }

    private sealed class FixedSchedule(int? hours, bool paused = false) : IHarvestScheduleCache
    {
        public HarvestScheduleSnapshot Snapshot() => new(hours, paused, DateTimeOffset.UtcNow);
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedUpdateSchedule(int? minutes, bool paused = false) : IHarvestUpdateScheduleCache
    {
        public HarvestUpdateScheduleSnapshot Snapshot() => new(minutes, paused, DateTimeOffset.UtcNow);
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
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
}
