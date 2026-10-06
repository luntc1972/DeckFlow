using System.Collections.Concurrent;
using System.Globalization;
using DeckFlow.Core.Integration;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ArchidektCacheJobService"/> covering harvest scheduling, job control, and progress tracking.
/// </summary>
public sealed class ArchidektCacheJobServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"archidekt-cache-job-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(_dbPath)}"));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task UpdateRun_ManualEnqueue_RunsUpdateSweepForUpdateRunDurationAndRecordsCounters()
    {
        var store = new FakeCategoryKnowledgeStore
        {
            RunUpdateSweepResult = new(10, 3, 2, 4, 0, TimeSpan.Zero)
        };
        var runStore = new HarvestRunStore(_dbPath);
        await runStore.EnsureSchemaAsync();
        var service = CreateService(store, runStore);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var result = await service.EnqueueAsync(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Manual);
            var job = await WaitForTerminalJobAsync(service, runStore, result.Job.JobId);
            var row = await runStore.GetByIdAsync(result.Job.JobId);

            Assert.True(result.StartedNewJob);
            Assert.Equal(600, result.Job.DurationSeconds);
            Assert.Equal(ArchidektCacheJobState.Succeeded, job.State);
            Assert.NotNull(row);
            Assert.Equal(HarvestRunKind.Update, row!.Kind);
            Assert.Equal(HarvestTriggerSource.Manual, row.TriggerSource);
            Assert.Equal(600, row.DurationSeconds);
            Assert.Equal(10, row.PagesPolled);
            Assert.Equal(3, row.RefreshesRequeued);
            Assert.Equal(2, row.RefreshesDrained);
            Assert.Equal(4, row.NewIdsSeen);
            Assert.Null(row.DecksEnqueued);
            Assert.Null(row.DecksDrained);
            Assert.Equal(2, row.DecksProcessed);
            Assert.Equal(1, store.RunUpdateSweepCalls);
            Assert.Equal(0, store.RunCacheSweepCalls);
            Assert.Equal(600, store.LastRunUpdateSweepDurationSeconds);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]
    public async Task EnqueueAsync_ThrowsForInvalidDurations(int seconds)
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(seconds), HarvestTriggerSource.Manual));

        Assert.Equal("duration", exception.ParamName);
    }

    [Theory]
    [InlineData(HarvestRunKind.Url)]
    [InlineData((HarvestRunKind)99)]
    public async Task EnqueueAsync_RejectsUnsupportedKind(HarvestRunKind kind)
    {
        var runStore = await CreateSqliteRunStoreAsync();
        var service = CreateService(runStore: runStore);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.EnqueueAsync(kind, TimeSpan.FromSeconds(60), HarvestTriggerSource.Manual));

        Assert.Equal("kind", exception.ParamName);
        Assert.Empty(await runStore.GetRecentAsync(10));
    }

    [Fact]
    public async Task EnqueueAsync_RejectsUndefinedTrigger()
    {
        var runStore = await CreateSqliteRunStoreAsync();
        var service = CreateService(runStore: runStore);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(60), (HarvestTriggerSource)99));

        Assert.Equal("trigger", exception.ParamName);
        Assert.Empty(await runStore.GetRecentAsync(10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1)]
    [InlineData(7200)]
    public async Task EnqueueAsync_UpdateKind_IgnoresCallerDurationAndUsesUpdateRunDuration(int seconds)
    {
        var runStore = await CreateSqliteRunStoreAsync();
        var service = CreateService(runStore: runStore);

        var result = await service.EnqueueAsync(HarvestRunKind.Update, TimeSpan.FromSeconds(seconds), HarvestTriggerSource.Manual);
        var row = await runStore.GetByIdAsync(result.Job.JobId);

        Assert.True(result.StartedNewJob);
        Assert.Equal(600, result.Job.DurationSeconds);
        Assert.NotNull(row);
        Assert.Equal(600, row!.DurationSeconds);
    }

    [Theory]
    [InlineData(HarvestRunKind.Bulk, HarvestTriggerSource.Manual)]
    [InlineData(HarvestRunKind.Bulk, HarvestTriggerSource.Scheduled)]
    [InlineData(HarvestRunKind.Update, HarvestTriggerSource.Manual)]
    [InlineData(HarvestRunKind.Update, HarvestTriggerSource.Scheduled)]
    public async Task EnqueueAsync_RecordsKindAndTriggerOnQueuedRow(HarvestRunKind kind, HarvestTriggerSource trigger)
    {
        var runStore = await CreateSqliteRunStoreAsync();
        var service = CreateService(runStore: runStore);

        var result = await service.EnqueueAsync(kind, TimeSpan.FromSeconds(60), trigger);
        var row = await runStore.GetByIdAsync(result.Job.JobId);

        Assert.NotNull(row);
        Assert.Equal(kind, row!.Kind);
        Assert.Equal(trigger, row.TriggerSource);
    }

    [Theory]
    [InlineData(HarvestRunKind.Bulk, HarvestRunKind.Update)]
    [InlineData(HarvestRunKind.Update, HarvestRunKind.Bulk)]
    public async Task EnqueueAsync_ActiveRunOfOtherKind_ReturnsExistingJobWithoutInsert(HarvestRunKind firstKind, HarvestRunKind secondKind)
    {
        var runStore = await CreateSqliteRunStoreAsync();
        var service = CreateService(runStore: runStore);

        var first = await service.EnqueueAsync(firstKind, TimeSpan.FromSeconds(60), HarvestTriggerSource.Manual);
        var second = await service.EnqueueAsync(secondKind, TimeSpan.FromSeconds(60), HarvestTriggerSource.Scheduled);

        Assert.False(second.StartedNewJob);
        Assert.Equal(first.Job.JobId, second.Job.JobId);
        Assert.Single(await runStore.GetRecentAsync(10));
    }

    [Fact]
    public async Task UpdateRun_CallerDurationIgnored_WorkerSweepsForUpdateRunDuration()
    {
        var store = new FakeCategoryKnowledgeStore();
        var runStore = await CreateSqliteRunStoreAsync();
        var service = CreateService(store, runStore);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var result = await service.EnqueueAsync(HarvestRunKind.Update, TimeSpan.FromMinutes(45), HarvestTriggerSource.Scheduled);
            var job = await WaitForTerminalJobAsync(service, runStore, result.Job.JobId);

            Assert.Equal(ArchidektCacheJobState.Succeeded, job.State);
            Assert.Equal(600, store.LastRunUpdateSweepDurationSeconds);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task BulkRun_RecordsTriggerAndLeavesUpdateCountersNull()
    {
        var store = new FakeCategoryKnowledgeStore { RunCacheSweepResult = new(7, 0, 0, 0, 0, TimeSpan.Zero) };
        var runStore = await CreateSqliteRunStoreAsync();
        var service = CreateService(store, runStore);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var result = await service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(1), HarvestTriggerSource.Scheduled);
            var job = await WaitForTerminalJobAsync(service, runStore, result.Job.JobId);
            var row = await runStore.GetByIdAsync(result.Job.JobId);

            Assert.Equal(ArchidektCacheJobState.Succeeded, job.State);
            Assert.NotNull(row);
            Assert.Equal(HarvestTriggerSource.Scheduled, row!.TriggerSource);
            Assert.Equal(0, row.DecksEnqueued);
            Assert.Equal(7, row.DecksDrained);
            Assert.Null(row.PagesPolled);
            Assert.Null(row.RefreshesRequeued);
            Assert.Null(row.RefreshesDrained);
            Assert.Null(row.NewIdsSeen);
            Assert.Equal(0, store.RunUpdateSweepCalls);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task EnqueueAsync_CreatesQueuedJobWithCeilingDuration()
    {
        var service = CreateService();
        var result = await service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromMilliseconds(1250), HarvestTriggerSource.Manual);

        Assert.True(result.StartedNewJob);
        Assert.Equal(2, result.Job.DurationSeconds);
        Assert.Equal(ArchidektCacheJobState.Queued, result.Job.State);
        Assert.NotEqual(Guid.Empty, result.Job.JobId);
        // Records map structurally — content equal, not reference equal.
        Assert.Equal(result.Job, service.GetJob(result.Job.JobId));
    }

    [Fact]
    public async Task EnqueueAsync_ReturnsSameActiveJobWhenQueuedAlreadyExists()
    {
        var service = CreateService();

        var first = await service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(5), HarvestTriggerSource.Manual);
        var second = await service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(10), HarvestTriggerSource.Manual);

        Assert.True(first.StartedNewJob);
        Assert.False(second.StartedNewJob);
        Assert.Equal(first.Job.JobId, second.Job.JobId);
    }

    [Fact]
    public void GetJob_ReturnsNullForUnknownJob()
    {
        var service = CreateService();

        Assert.Null(service.GetJob(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetJob_ReturnsEnqueuedJob()
    {
        var service = CreateService();
        var result = await service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(1), HarvestTriggerSource.Manual);

        var job = service.GetJob(result.Job.JobId);

        Assert.NotNull(job);
        Assert.Equal(result.Job.JobId, job!.JobId);
        Assert.Equal(ArchidektCacheJobState.Queued, job.State);
    }

    [Fact]
    public void GetActiveJob_ReturnsNullBeforeAnyEnqueue()
    {
        var service = CreateService();

        Assert.Null(service.GetActiveJob());
    }

    [Fact]
    public async Task GetActiveJob_ReturnsQueuedJobAfterEnqueue()
    {
        var service = CreateService();
        var result = await service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(1), HarvestTriggerSource.Manual);

        var activeJob = service.GetActiveJob();

        Assert.NotNull(activeJob);
        Assert.Equal(result.Job.JobId, activeJob!.JobId);
        Assert.Equal(ArchidektCacheJobState.Queued, activeJob.State);
    }

    [Fact]
    public async Task BackgroundService_SucceedsAndUpdatesProcessedCounts()
    {
        var store = new FakeCategoryKnowledgeStore(initialProcessedDeckCount: 10, finalProcessedDeckCount: 14)
        {
            RunCacheSweepResult = new(7, 0, 0, 0, 0, TimeSpan.Zero)
        };
        var runStore = new FakeHarvestRunStore();
        var service = CreateService(store, runStore);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var enqueueResult = await service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(1), HarvestTriggerSource.Manual);
            var job = await WaitForTerminalJobAsync(service, runStore, enqueueResult.Job.JobId);

            Assert.Equal(ArchidektCacheJobState.Succeeded, job.State);
            Assert.Equal(7, job.DecksProcessed);
            Assert.Equal(4, job.AdditionalDecksFound);
            Assert.Equal(new[] { (0, 7) }, runStore.SweepCounts);
            Assert.NotNull(job.CompletedUtc);
            Assert.Null(service.GetActiveJob());
            Assert.NotNull(service.GetJob(job.JobId));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task BackgroundService_FailsAndCapturesErrorMessage()
    {
        var store = new FakeCategoryKnowledgeStore(initialProcessedDeckCount: 3, finalProcessedDeckCount: 3)
        {
            RunCacheSweepException = new InvalidOperationException("cache sweep failed")
        };
        var runStore = new FakeHarvestRunStore();
        var service = CreateService(store, runStore);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var enqueueResult = await service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(1), HarvestTriggerSource.Manual);
            var job = await WaitForTerminalJobAsync(service, runStore, enqueueResult.Job.JobId);

            Assert.Equal(ArchidektCacheJobState.Failed, job.State);
            Assert.Equal("cache sweep failed", job.ErrorMessage);
            Assert.NotNull(job.CompletedUtc);
            Assert.Empty(runStore.SweepCounts);
            var run = runStore.GetById(job.JobId);
            Assert.Null(run!.DecksEnqueued);
            Assert.Null(run.DecksDrained);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task GetActiveJob_ReturnsNullAfterCompletedJob()
    {
        var store = new FakeCategoryKnowledgeStore(initialProcessedDeckCount: 8, finalProcessedDeckCount: 11)
        {
            RunCacheSweepResult = new(2, 0, 0, 0, 0, TimeSpan.Zero)
        };
        var runStore = new FakeHarvestRunStore();
        var service = CreateService(store, runStore);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var enqueueResult = await service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(1), HarvestTriggerSource.Manual);
            var job = await WaitForTerminalJobAsync(service, runStore, enqueueResult.Job.JobId);

            Assert.Equal(ArchidektCacheJobState.Succeeded, job.State);
            Assert.Null(service.GetActiveJob());
            Assert.NotNull(service.GetJob(job.JobId));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task EnqueueAsync_AfterCompletion_CreatesFreshJob()
    {
        var store = new FakeCategoryKnowledgeStore(initialProcessedDeckCount: 6, finalProcessedDeckCount: 9)
        {
            RunCacheSweepResult = new(5, 0, 0, 0, 0, TimeSpan.Zero)
        };
        var runStore = new FakeHarvestRunStore();
        var service = CreateService(store, runStore);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var first = await service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(1), HarvestTriggerSource.Manual);
            var completed = await WaitForTerminalJobAsync(service, runStore, first.Job.JobId);
            Assert.Equal(ArchidektCacheJobState.Succeeded, completed.State);

            var second = await service.EnqueueAsync(HarvestRunKind.Bulk, TimeSpan.FromSeconds(2), HarvestTriggerSource.Manual);

            Assert.True(second.StartedNewJob);
            Assert.NotEqual(first.Job.JobId, second.Job.JobId);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task CancelActiveAsync_ReturnsFalseWhenNoActiveJob()
    {
        var service = CreateService();
        var result = await service.CancelActiveAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateRun_HostShutdownDuringSweep_EndsInterruptedWithNoCounters()
    {
        var knowledgeStore = new FakeCategoryKnowledgeStore { RunUpdateSweepBlocksUntilCancelled = true };
        var runStore = await CreateSqliteRunStoreAsync();
        var service = CreateService(knowledgeStore, runStore);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var result = await service.EnqueueAsync(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Scheduled);
            await WaitForAsync(() => knowledgeStore.RunUpdateSweepCalls == 1);
            await service.StopAsync(CancellationToken.None);

            var row = await runStore.GetByIdAsync(result.Job.JobId);
            Assert.NotNull(row);
            Assert.Equal(HarvestRunState.Interrupted, row!.State);
            Assert.Equal("interrupted by host shutdown", row.ErrorMessage);
            Assert.NotNull(row.CompletedUtc);
            Assert.Equal(HarvestRunKind.Update, row.Kind);
            Assert.Equal(HarvestTriggerSource.Scheduled, row.TriggerSource);
            Assert.Null(row.PagesPolled);
            Assert.Null(row.RefreshesRequeued);
            Assert.Null(row.RefreshesDrained);
            Assert.Null(row.NewIdsSeen);
            Assert.Null(row.DecksEnqueued);
            Assert.Null(row.DecksDrained);
            Assert.Null(await runStore.GetActiveAsync());
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task UpdateRun_OrphanedByHardKill_ReapedAtBootAndReleasesActiveSlot()
    {
        var storeA = await CreateSqliteRunStoreAsync();
        var id = await storeA.InsertQueuedAsync(HarvestRunKind.Update, 600, null, DateTimeOffset.UtcNow, HarvestTriggerSource.Scheduled);
        await storeA.UpdateStateAsync(id, HarvestRunState.Running, DateTimeOffset.UtcNow, null, null, null, null);
        var storeB = new HarvestRunStore(_dbPath);
        await storeB.EnsureSchemaAsync();

        var row = await storeB.GetByIdAsync(id);
        Assert.NotNull(row);
        Assert.Equal(HarvestRunState.Failed, row!.State);
        Assert.Equal("interrupted by redeploy", row.ErrorMessage);
        Assert.Null(row.PagesPolled);
        Assert.Null(row.RefreshesRequeued);
        Assert.Null(row.RefreshesDrained);
        Assert.Null(row.NewIdsSeen);
        Assert.Null(row.DecksEnqueued);
        Assert.Null(row.DecksDrained);
        Assert.Null(await storeB.GetActiveAsync());

        var service = CreateService(runStore: storeB);
        var next = await service.EnqueueAsync(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Manual);
        Assert.True(next.StartedNewJob);
        Assert.NotEqual(id, next.Job.JobId);
    }

    [Fact]
    public async Task UpdateRun_OperatorCancel_EndsCancelledWithNoCounters()
    {
        var knowledgeStore = new FakeCategoryKnowledgeStore { RunUpdateSweepBlocksUntilCancelled = true };
        var runStore = await CreateSqliteRunStoreAsync();
        var service = CreateService(knowledgeStore, runStore);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var result = await service.EnqueueAsync(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Manual);
            await WaitForAsync(() => knowledgeStore.RunUpdateSweepCalls == 1);
            Assert.True(await service.CancelActiveAsync());
            await WaitForTerminalJobAsync(service, runStore, result.Job.JobId);

            var row = await runStore.GetByIdAsync(result.Job.JobId);
            Assert.NotNull(row);
            Assert.Equal(HarvestRunState.Cancelled, row!.State);
            Assert.Null(row.PagesPolled);
            Assert.Null(row.RefreshesRequeued);
            Assert.Null(row.RefreshesDrained);
            Assert.Null(row.NewIdsSeen);
            Assert.Null(row.DecksEnqueued);
            Assert.Null(row.DecksDrained);
            Assert.Null(await runStore.GetActiveAsync());
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task UpdateRun_SweepThrows_EndsFailedWithMessageAndNoCounters()
    {
        var knowledgeStore = new FakeCategoryKnowledgeStore { RunUpdateSweepException = new InvalidOperationException("update sweep failed") };
        var runStore = await CreateSqliteRunStoreAsync();
        var service = CreateService(knowledgeStore, runStore);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var result = await service.EnqueueAsync(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Manual);
            await WaitForTerminalJobAsync(service, runStore, result.Job.JobId);
            var row = await runStore.GetByIdAsync(result.Job.JobId);
            Assert.NotNull(row);
            Assert.Equal(HarvestRunState.Failed, row!.State);
            Assert.Equal("update sweep failed", row.ErrorMessage);
            Assert.Null(row.PagesPolled);
            Assert.Null(row.RefreshesRequeued);
            Assert.Null(row.RefreshesDrained);
            Assert.Null(row.NewIdsSeen);
            Assert.Null(row.DecksEnqueued);
            Assert.Null(row.DecksDrained);
            Assert.Null(await runStore.GetActiveAsync());
        }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task UpdateRun_RateLimitedTrip_EndsFailedWithNoCounters()
    {
        var knowledgeStore = new FakeCategoryKnowledgeStore { RunUpdateSweepException = new DeckFlow.Core.Integration.ArchidektRateLimitedException("limited", TimeSpan.FromSeconds(120)) };
        var runStore = await CreateSqliteRunStoreAsync();
        var service = CreateService(knowledgeStore, runStore);
        await service.StartAsync(CancellationToken.None);
        try
        {
            var result = await service.EnqueueAsync(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Manual);
            await WaitForTerminalJobAsync(service, runStore, result.Job.JobId);
            var row = await runStore.GetByIdAsync(result.Job.JobId);
            Assert.NotNull(row);
            Assert.Equal(HarvestRunState.Failed, row!.State);
            Assert.NotNull(row.CompletedUtc);
            Assert.False(string.IsNullOrEmpty(row.ErrorMessage));
            Assert.Null(row.PagesPolled);
            Assert.Null(row.RefreshesRequeued);
            Assert.Null(row.RefreshesDrained);
            Assert.Null(row.NewIdsSeen);
            Assert.Null(row.DecksEnqueued);
            Assert.Null(row.DecksDrained);
            Assert.Null(await runStore.GetActiveAsync());
        }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task RateLimitedUpdateRun_EndsFailedMarksThrottleAndPausesBothSchedulesEndToEnd()
    {
        var knowledgeStore = new FakeCategoryKnowledgeStore
        {
            RunUpdateSweepException = new ArchidektRateLimitedException("limited", TimeSpan.FromSeconds(120)),
        };
        var runStore = await CreateSqliteRunStoreAsync();
        var throttleStore = new HarvestThrottleStore(_dbPath);
        var scheduleStore = new HarvestScheduleStore(_dbPath);
        var updateStore = new HarvestUpdateScheduleStore(_dbPath);
        await scheduleStore.EnsureSchemaAsync();
        await updateStore.EnsureSchemaAsync();
        await throttleStore.EnsureSchemaAsync();
        await scheduleStore.SaveAsync(4, false, DateTimeOffset.UtcNow);
        await updateStore.SaveAsync(30, false, DateTimeOffset.UtcNow);
        var scheduleCache = new HarvestScheduleCache(scheduleStore);
        var updateCache = new HarvestUpdateScheduleCache(updateStore);
        await scheduleCache.ReloadAsync();
        await updateCache.ReloadAsync();
        var service = CreateService(knowledgeStore, runStore, throttleStore, scheduleCache, updateCache);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var result = await service.EnqueueAsync(HarvestRunKind.Update, ArchidektCacheJobService.UpdateRunDuration, HarvestTriggerSource.Manual);
            await WaitForTerminalJobAsync(service, runStore, result.Job.JobId);
            var row = await runStore.GetByIdAsync(result.Job.JobId);
            Assert.Equal(HarvestRunState.Failed, row!.State);
            Assert.Equal(ArchidektCacheJobService.RateLimitedErrorMessage, row.ErrorMessage);
            Assert.NotNull((await throttleStore.GetAsync()).RateLimitedUtc);
            Assert.True((await scheduleStore.GetAsync()).Paused);
            Assert.True((await updateStore.GetAsync()).Paused);
            Assert.Equal(4, (await scheduleStore.GetAsync()).IntervalHours);
            Assert.Equal(30, (await updateStore.GetAsync()).IntervalMinutes);
            Assert.True(scheduleCache.Snapshot().Paused);
            Assert.True(updateCache.Snapshot().Paused);
        }
        finally { await service.StopAsync(CancellationToken.None); }
    }

    private static ArchidektCacheJobService CreateService(
        ICategoryKnowledgeStore? store = null,
        IHarvestRunStore? runStore = null,
        IHarvestThrottleStore? throttleStore = null,
        IHarvestScheduleCache? scheduleCache = null,
        IHarvestUpdateScheduleCache? updateScheduleCache = null)
        => new(
            store ?? new FakeCategoryKnowledgeStore(),
            runStore ?? new FakeHarvestRunStore(),
            throttleStore ?? new FakeHarvestThrottleStore(),
            scheduleCache ?? new FakeHarvestScheduleCache(),
            updateScheduleCache ?? new FakeHarvestUpdateScheduleCache(),
            NullLogger<ArchidektCacheJobService>.Instance);

    private sealed class FakeHarvestThrottleStore : IHarvestThrottleStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<HarvestThrottleSnapshot> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(new HarvestThrottleSnapshot(20, null, DateTimeOffset.UtcNow));
        public Task SaveRateAsync(int ratePerMinute, DateTimeOffset now, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkRateLimitedAsync(DateTimeOffset now, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> ResumeAfterRateLimitAsync(DateTimeOffset now, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class FakeHarvestScheduleCache : IHarvestScheduleCache
    {
        public HarvestScheduleSnapshot Snapshot() => new(null, false, DateTimeOffset.MinValue);
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeHarvestUpdateScheduleCache : IHarvestUpdateScheduleCache
    {
        public HarvestUpdateScheduleSnapshot Snapshot() => new(null, false, DateTimeOffset.MinValue);
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private async Task<HarvestRunStore> CreateSqliteRunStoreAsync()
    {
        var runStore = new HarvestRunStore(_dbPath);
        await runStore.EnsureSchemaAsync();
        return runStore;
    }

    private static async Task<ArchidektCacheJobStatus> WaitForTerminalJobAsync(
        ArchidektCacheJobService service,
        IHarvestRunStore runStore,
        Guid jobId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (true)
        {
            cts.Token.ThrowIfCancellationRequested();

            // The service rebuilds status from the run store on each GetJob call. Once the
            // background loop transitions the row to a terminal state the service no longer
            // sees it as the "active" row, so GetJob returns null. Read directly from the
            // fake store in that case.
            var job = service.GetJob(jobId);
            if (job is not null && IsTerminal(job.State))
            {
                return job;
            }

            var rowFromStore = await runStore.GetByIdAsync(jobId, cts.Token);
            if (rowFromStore is not null && IsTerminal(MapState(rowFromStore.State)))
            {
                return new ArchidektCacheJobStatus(
                    rowFromStore.Id,
                    MapState(rowFromStore.State),
                    rowFromStore.DurationSeconds,
                    rowFromStore.RequestedUtc,
                    rowFromStore.StartedUtc,
                    rowFromStore.CompletedUtc,
                    rowFromStore.DecksProcessed,
                    rowFromStore.AdditionalDecksFound,
                    rowFromStore.ErrorMessage);
            }

            await Task.Delay(25, cts.Token);
        }
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(25, cts.Token);
        }
    }

    private static bool IsTerminal(ArchidektCacheJobState state)
        => state is ArchidektCacheJobState.Succeeded
            or ArchidektCacheJobState.Failed
            or ArchidektCacheJobState.Cancelled;

    private static ArchidektCacheJobState MapState(HarvestRunState state)
        => Enum.Parse<ArchidektCacheJobState>(state.ToString(), ignoreCase: false);

    /// <summary>
    /// In-memory <see cref="IHarvestRunStore"/> for unit tests. Threadsafe via
    /// <see cref="ConcurrentDictionary{TKey,TValue}"/>; preserves the contract
    /// the production Postgres impl exposes (active = first non-terminal row,
    /// recent = ordered by started_utc DESC).
    /// </summary>
    private sealed class FakeHarvestRunStore : IHarvestRunStore
    {
        private readonly ConcurrentDictionary<Guid, HarvestRunRow> _rows = new();

        public List<(int DecksEnqueued, int DecksDrained)> SweepCounts { get; } = [];

        public HarvestRunRow? GetById(Guid id) => _rows.TryGetValue(id, out var row) ? row : null;

        public Task<HarvestRunRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(GetById(id));

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Guid> InsertQueuedAsync(
            HarvestRunKind kind,
            int durationSeconds,
            string? url,
            DateTimeOffset now,
            HarvestTriggerSource? triggerSource,
            CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            _rows[id] = new HarvestRunRow(
                id,
                kind,
                HarvestRunState.Queued,
                now,
                StartedUtc: null,
                CompletedUtc: null,
                durationSeconds,
                DecksProcessed: 0,
                AdditionalDecksFound: 0,
                DecksEnqueued: null,
                DecksDrained: null,
                ErrorMessage: null,
                Url: url,
                TriggerSource: triggerSource,
                PagesPolled: null,
                RefreshesRequeued: null,
                RefreshesDrained: null,
                NewIdsSeen: null);
            return Task.FromResult(id);
        }

        public Task UpdateStateAsync(
            Guid id,
            HarvestRunState state,
            DateTimeOffset? startedUtc,
            DateTimeOffset? completedUtc,
            int? decksProcessed,
            int? additionalDecksFound,
            string? errorMessage,
            CancellationToken cancellationToken = default)
        {
            _rows.AddOrUpdate(
                id,
                _ => throw new InvalidOperationException($"No queued row for {id}."),
                (_, existing) => existing with
                {
                    State = state,
                    StartedUtc = startedUtc ?? existing.StartedUtc,
                    CompletedUtc = completedUtc ?? existing.CompletedUtc,
                    DecksProcessed = decksProcessed ?? existing.DecksProcessed,
                    AdditionalDecksFound = additionalDecksFound ?? existing.AdditionalDecksFound,
                    ErrorMessage = errorMessage
                });
            return Task.CompletedTask;
        }

        public Task UpdateProgressAsync(
            Guid id,
            int decksProcessed,
            CancellationToken cancellationToken = default)
        {
            _rows.AddOrUpdate(
                id,
                _ => throw new InvalidOperationException($"No queued row for {id}."),
                (_, existing) => existing with
                {
                    DecksProcessed = decksProcessed,
                });
            return Task.CompletedTask;
        }

        public Task SetSweepCountsAsync(Guid id, int decksEnqueued, int decksDrained, CancellationToken cancellationToken = default)
        {
            SweepCounts.Add((decksEnqueued, decksDrained));
            _rows.AddOrUpdate(
                id,
                _ => throw new InvalidOperationException($"No queued row for {id}."),
                (_, existing) => existing with { DecksEnqueued = decksEnqueued, DecksDrained = decksDrained });
            return Task.CompletedTask;
        }

        public Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            HarvestRunRow? active = _rows.Values
                .Where(r => r.State is HarvestRunState.Queued or HarvestRunState.Running or HarvestRunState.Stopping)
                .OrderByDescending(r => r.RequestedUtc)
                .FirstOrDefault();
            return Task.FromResult(active);
        }

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<HarvestRunRow> rows = _rows.Values
                .OrderByDescending(r => r.StartedUtc ?? DateTimeOffset.MinValue)
                .Take(n)
                .ToList();
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<HarvestRunRow>> GetRecentHealthSignalRunsAsync(int n, CancellationToken cancellationToken = default)
            => GetRecentAsync(n, cancellationToken);

        public Task<string> GetRecentRevisionAsync(CancellationToken cancellationToken = default)
        {
            var startedTicks = _rows.Values
                .Select(r => r.StartedUtc?.ToUniversalTime().Ticks)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .DefaultIfEmpty()
                .Max();
            var completedTicks = _rows.Values
                .Select(r => r.CompletedUtc?.ToUniversalTime().Ticks)
                .Where(t => t.HasValue)
                .Select(t => t!.Value)
                .DefaultIfEmpty()
                .Max();
            var startedToken = startedTicks == 0 ? string.Empty : startedTicks.ToString(CultureInfo.InvariantCulture);
            var completedToken = completedTicks == 0 ? string.Empty : completedTicks.ToString(CultureInfo.InvariantCulture);
            var count = _rows.Count.ToString(CultureInfo.InvariantCulture);
            return Task.FromResult($"{startedToken}|{completedToken}|{count}");
        }

        public Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default)
        {
            var max = _rows.Values
                .Where(r => r.State == HarvestRunState.Succeeded)
                .Select(r => r.CompletedUtc)
                .Where(t => t is not null)
                .DefaultIfEmpty(null)
                .Max();
            return Task.FromResult(max);
        }

        public Task<HarvestFailureStreak> GetFailureStreakSinceLastSuccessAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HarvestFailureStreak(0, null, null));

        public Task<long> GetTotalSucceededCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((long)_rows.Values.Count(r => r.State == HarvestRunState.Succeeded));

        public Task SetUpdateCountsAsync(Guid id, int pagesPolled, int refreshesRequeued, int refreshesDrained, int newIdsSeen, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DateTimeOffset?> GetLastScheduledSuccessUtcAsync(HarvestRunKind kind, CancellationToken cancellationToken = default) => Task.FromResult<DateTimeOffset?>(null);
        public Task<HarvestFailureStreak> GetFailureStreakSinceLastSuccessAsync(HarvestRunKind kind, CancellationToken cancellationToken = default) => Task.FromResult(new HarvestFailureStreak(0, null, null));
    }
}
