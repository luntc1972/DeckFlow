using System.Collections.Concurrent;
using System.Globalization;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Harvest;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class ArchidektCacheJobServiceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]
    public async Task EnqueueAsync_ThrowsForInvalidDurations(int seconds)
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.EnqueueAsync(TimeSpan.FromSeconds(seconds)));

        Assert.Equal("duration", exception.ParamName);
    }

    [Fact]
    public async Task EnqueueAsync_CreatesQueuedJobWithCeilingDuration()
    {
        var service = CreateService();
        var result = await service.EnqueueAsync(TimeSpan.FromMilliseconds(1250));

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

        var first = await service.EnqueueAsync(TimeSpan.FromSeconds(5));
        var second = await service.EnqueueAsync(TimeSpan.FromSeconds(10));

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
        var result = await service.EnqueueAsync(TimeSpan.FromSeconds(1));

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
        var result = await service.EnqueueAsync(TimeSpan.FromSeconds(1));

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
            RunCacheSweepResult = 7
        };
        var runStore = new FakeHarvestRunStore();
        var service = CreateService(store, runStore);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var enqueueResult = await service.EnqueueAsync(TimeSpan.FromSeconds(1));
            var job = await WaitForTerminalJobAsync(service, runStore, enqueueResult.Job.JobId);

            Assert.Equal(ArchidektCacheJobState.Succeeded, job.State);
            Assert.Equal(7, job.DecksProcessed);
            Assert.Equal(4, job.AdditionalDecksFound);
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
            var enqueueResult = await service.EnqueueAsync(TimeSpan.FromSeconds(1));
            var job = await WaitForTerminalJobAsync(service, runStore, enqueueResult.Job.JobId);

            Assert.Equal(ArchidektCacheJobState.Failed, job.State);
            Assert.Equal("cache sweep failed", job.ErrorMessage);
            Assert.NotNull(job.CompletedUtc);
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
            RunCacheSweepResult = 2
        };
        var runStore = new FakeHarvestRunStore();
        var service = CreateService(store, runStore);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var enqueueResult = await service.EnqueueAsync(TimeSpan.FromSeconds(1));
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
            RunCacheSweepResult = 5
        };
        var runStore = new FakeHarvestRunStore();
        var service = CreateService(store, runStore);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var first = await service.EnqueueAsync(TimeSpan.FromSeconds(1));
            var completed = await WaitForTerminalJobAsync(service, runStore, first.Job.JobId);
            Assert.Equal(ArchidektCacheJobState.Succeeded, completed.State);

            var second = await service.EnqueueAsync(TimeSpan.FromSeconds(2));

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

    private static ArchidektCacheJobService CreateService(
        ICategoryKnowledgeStore? store = null,
        IHarvestRunStore? runStore = null)
        => new(
            store ?? new FakeCategoryKnowledgeStore(),
            runStore ?? new FakeHarvestRunStore(),
            NullLogger<ArchidektCacheJobService>.Instance);

    private static async Task<ArchidektCacheJobStatus> WaitForTerminalJobAsync(
        ArchidektCacheJobService service,
        FakeHarvestRunStore runStore,
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

            var rowFromStore = runStore.GetById(jobId);
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

        public HarvestRunRow? GetById(Guid id) => _rows.TryGetValue(id, out var row) ? row : null;

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Guid> InsertQueuedAsync(
            HarvestRunKind kind,
            int durationSeconds,
            string? url,
            DateTimeOffset now,
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
                ErrorMessage: null,
                url);
            return Task.FromResult(id);
        }

        public Task UpdateStateAsync(
            Guid id,
            HarvestRunState state,
            DateTimeOffset? startedUtc,
            DateTimeOffset? completedUtc,
            int decksProcessed,
            int additionalDecksFound,
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
                    DecksProcessed = decksProcessed,
                    AdditionalDecksFound = additionalDecksFound,
                    ErrorMessage = errorMessage
                });
            return Task.CompletedTask;
        }

        public Task UpdateProgressAsync(
            Guid id,
            int decksProcessed,
            int additionalDecksFound,
            CancellationToken cancellationToken = default)
        {
            _rows.AddOrUpdate(
                id,
                _ => throw new InvalidOperationException($"No queued row for {id}."),
                (_, existing) => existing with
                {
                    DecksProcessed = decksProcessed,
                    AdditionalDecksFound = additionalDecksFound
                });
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

        public Task<long> GetTotalSucceededCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((long)_rows.Values.Count(r => r.State == HarvestRunState.Succeeded));
    }
}
