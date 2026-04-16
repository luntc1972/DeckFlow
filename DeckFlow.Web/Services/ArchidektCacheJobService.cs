using System.Collections.Concurrent;
using System.Threading.Channels;

namespace DeckFlow.Web.Services;

public interface IArchidektCacheJobService
{
    Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(TimeSpan duration, CancellationToken cancellationToken = default);
    ArchidektCacheJobStatus? GetJob(Guid jobId);
    ArchidektCacheJobStatus? GetActiveJob();
}

public enum ArchidektCacheJobState
{
    Queued,
    Running,
    Succeeded,
    Failed
}

public sealed record ArchidektCacheJobStatus(
    Guid JobId,
    ArchidektCacheJobState State,
    int DurationSeconds,
    DateTimeOffset RequestedUtc,
    DateTimeOffset? StartedUtc,
    DateTimeOffset? CompletedUtc,
    int DecksProcessed,
    int AdditionalDecksFound,
    string? ErrorMessage);

public sealed record ArchidektCacheJobEnqueueResult(
    ArchidektCacheJobStatus Job,
    bool StartedNewJob);

public sealed class ArchidektCacheJobService : BackgroundService, IArchidektCacheJobService
{
    private readonly Channel<ArchidektCacheJobStatus> _queue = Channel.CreateUnbounded<ArchidektCacheJobStatus>();
    private readonly ConcurrentDictionary<Guid, ArchidektCacheJobStatus> _jobs = new();
    private readonly ICategoryKnowledgeStore _knowledgeStore;
    private readonly ILogger<ArchidektCacheJobService> _logger;
    private readonly object _sync = new();
    private Guid? _activeJobId;

    public ArchidektCacheJobService(
        ICategoryKnowledgeStore knowledgeStore,
        ILogger<ArchidektCacheJobService> logger)
    {
        _knowledgeStore = knowledgeStore;
        _logger = logger;
    }

    public Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than zero.");
        }

        if (duration > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot exceed one hour.");
        }

        lock (_sync)
        {
            if (_activeJobId is Guid activeJobId
                && _jobs.TryGetValue(activeJobId, out var activeJob)
                && (activeJob.State == ArchidektCacheJobState.Queued || activeJob.State == ArchidektCacheJobState.Running))
            {
                return Task.FromResult(new ArchidektCacheJobEnqueueResult(activeJob, StartedNewJob: false));
            }

            var job = new ArchidektCacheJobStatus(
                Guid.NewGuid(),
                ArchidektCacheJobState.Queued,
                (int)Math.Ceiling(duration.TotalSeconds),
                DateTimeOffset.UtcNow,
                null,
                null,
                0,
                0,
                null);

            _jobs[job.JobId] = job;
            _activeJobId = job.JobId;
            _queue.Writer.TryWrite(job);
            return Task.FromResult(new ArchidektCacheJobEnqueueResult(job, StartedNewJob: true));
        }
    }

    public ArchidektCacheJobStatus? GetJob(Guid jobId)
        => _jobs.TryGetValue(jobId, out var status) ? status : null;

    public ArchidektCacheJobStatus? GetActiveJob()
    {
        if (_activeJobId is not Guid activeJobId)
        {
            return null;
        }

        return GetJob(activeJobId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var queuedJob in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var runningJob = queuedJob with
            {
                State = ArchidektCacheJobState.Running,
                StartedUtc = DateTimeOffset.UtcNow
            };
            _jobs[runningJob.JobId] = runningJob;

            try
            {
                var initialDeckCount = await _knowledgeStore.GetProcessedDeckCountAsync(stoppingToken);
                var decksProcessed = await _knowledgeStore.RunCacheSweepAsync(_logger, runningJob.DurationSeconds, stoppingToken);
                var finalDeckCount = await _knowledgeStore.GetProcessedDeckCountAsync(stoppingToken);
                var completedJob = runningJob with
                {
                    State = ArchidektCacheJobState.Succeeded,
                    CompletedUtc = DateTimeOffset.UtcNow,
                    DecksProcessed = decksProcessed,
                    AdditionalDecksFound = Math.Max(finalDeckCount - initialDeckCount, 0)
                };

                _jobs[completedJob.JobId] = completedJob;
                ClearActiveJob(completedJob.JobId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Archidekt cache background job {JobId} failed.", runningJob.JobId);
                var failedJob = runningJob with
                {
                    State = ArchidektCacheJobState.Failed,
                    CompletedUtc = DateTimeOffset.UtcNow,
                    ErrorMessage = exception.Message
                };

                _jobs[failedJob.JobId] = failedJob;
                ClearActiveJob(failedJob.JobId);
            }
        }
    }

    private void ClearActiveJob(Guid jobId)
    {
        lock (_sync)
        {
            if (_activeJobId == jobId)
            {
                _activeJobId = null;
            }
        }
    }
}
