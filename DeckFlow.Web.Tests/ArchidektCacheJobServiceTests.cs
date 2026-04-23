using DeckFlow.Web.Services;
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
        Assert.Same(result.Job, service.GetJob(result.Job.JobId));
    }

    [Fact]
    public async Task EnqueueAsync_ReturnsSameActiveJobWhenQueuedAlreadyExists()
    {
        var service = CreateService();

        var first = await service.EnqueueAsync(TimeSpan.FromSeconds(5));
        var second = await service.EnqueueAsync(TimeSpan.FromSeconds(10));

        Assert.True(first.StartedNewJob);
        Assert.False(second.StartedNewJob);
        Assert.Same(first.Job, second.Job);
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
        var service = CreateService(store);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var enqueueResult = await service.EnqueueAsync(TimeSpan.FromSeconds(1));
            var job = await WaitForCompletedJobAsync(service, enqueueResult.Job.JobId);

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
        var service = CreateService(store);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var enqueueResult = await service.EnqueueAsync(TimeSpan.FromSeconds(1));
            var job = await WaitForCompletedJobAsync(service, enqueueResult.Job.JobId);

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
        var service = CreateService(store);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var enqueueResult = await service.EnqueueAsync(TimeSpan.FromSeconds(1));
            var job = await WaitForCompletedJobAsync(service, enqueueResult.Job.JobId);

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
        var service = CreateService(store);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var first = await service.EnqueueAsync(TimeSpan.FromSeconds(1));
            var completed = await WaitForCompletedJobAsync(service, first.Job.JobId);
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

    private static ArchidektCacheJobService CreateService(ICategoryKnowledgeStore? store = null)
        => new(store ?? new FakeCategoryKnowledgeStore(), NullLogger<ArchidektCacheJobService>.Instance);

    private static async Task<ArchidektCacheJobStatus> WaitForCompletedJobAsync(ArchidektCacheJobService service, Guid jobId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (true)
        {
            cts.Token.ThrowIfCancellationRequested();

            var job = service.GetJob(jobId);
            if (job is not null && job.State is ArchidektCacheJobState.Succeeded or ArchidektCacheJobState.Failed)
            {
                return job;
            }

            await Task.Delay(25, cts.Token);
        }
    }
}
