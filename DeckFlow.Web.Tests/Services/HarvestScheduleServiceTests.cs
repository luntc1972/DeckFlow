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
            var id = await store.InsertQueuedAsync(HarvestRunKind.Bulk, 60, null, completed);
            await store.UpdateStateAsync(id, HarvestRunState.Failed, null, completed, null, null, null);
        }

        var job = new RecordingJob();
        var service = new HarvestScheduleService(
            new EnabledFlags(), new FixedSchedule(1), store, job,
            NullLogger<HarvestScheduleService>.Instance,
            new FakeTimeProvider(now));
        var tick = typeof(HarvestScheduleService).GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await (Task)tick.Invoke(service, new object[] { CancellationToken.None })!;

        Assert.Equal(expectFire ? 1 : 0, job.Calls);
    }

    private sealed class EnabledFlags : IFeatureFlagCache
    {
        public bool IsEnabled(string key) => true;
        public IReadOnlyDictionary<string, bool> Snapshot() => new Dictionary<string, bool>();
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedSchedule(int hours) : IHarvestScheduleCache
    {
        public HarvestScheduleSnapshot Snapshot() => new(hours, false, DateTimeOffset.UtcNow);
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingJob : IArchidektCacheJobService
    {
        public int Calls { get; private set; }
        public Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(TimeSpan duration, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult<ArchidektCacheJobEnqueueResult>(null!); }
        public ArchidektCacheJobStatus? GetJob(Guid jobId) => null;
        public ArchidektCacheJobStatus? GetActiveJob() => null;
        public Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
