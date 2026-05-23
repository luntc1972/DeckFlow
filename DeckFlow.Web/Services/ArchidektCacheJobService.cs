using System.Threading.Channels;
using System.Diagnostics;
using DeckFlow.Web.Services.Harvest;

namespace DeckFlow.Web.Services;

/// <summary>
/// Background-job contract for Archidekt cache harvests. State persists in
/// Postgres <c>harvest_runs</c> via <see cref="IHarvestRunStore"/> (D-01); the
/// in-memory dictionary that previously tracked job state was removed in
/// Phase 7 Plan 02. Existing public-API consumers
/// (<c>ArchidektCacheJobsController</c>) keep the same wire contract — the
/// <see cref="ArchidektCacheJobStatus"/> shape is preserved and built from a
/// <see cref="HarvestRunRow"/> on read.
/// </summary>
public interface IArchidektCacheJobService
{
    /// <summary>
    /// Validates duration (1s..60min, HARV-01 / D-04), checks for an active row in
    /// <c>harvest_runs</c>, and either returns the existing job (StartedNewJob=false)
    /// or inserts a new <c>Queued</c> row and signals the worker channel.
    /// </summary>
    /// <param name="duration">Operator-selected sweep duration; must be in (0, 60min].</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(TimeSpan duration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the run row for the supplied id mapped to the public
    /// <see cref="ArchidektCacheJobStatus"/> shape, or null when not found.
    /// Reads Postgres synchronously via <c>.GetAwaiter().GetResult()</c> — admin
    /// API surface, sub-1RPS, no thread-pool starvation risk (T-07-10).
    /// </summary>
    /// <param name="jobId">Job UUID.</param>
    ArchidektCacheJobStatus? GetJob(Guid jobId);

    /// <summary>
    /// Returns the most recent non-terminal run row mapped to
    /// <see cref="ArchidektCacheJobStatus"/>, or null when no active run exists.
    /// </summary>
    ArchidektCacheJobStatus? GetActiveJob();

    /// <summary>
    /// Signals the currently active harvest job (if any) to stop after the
    /// in-flight deck completes. HARV-03 — graceful operator cancel. The
    /// linked <see cref="CancellationTokenSource"/> propagates the cancel through
    /// the existing inner deck-loop in
    /// <c>ArchidektDeckCacheSession.RunAsync</c>; <c>OperationCanceledException</c>
    /// flips the run row to <see cref="ArchidektCacheJobState.Cancelled"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a job was active and cancellation was signalled; false if no active job.</returns>
    Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Public-API state values surfaced by <c>ArchidektCacheJobsController</c>.
/// Mirrors <see cref="HarvestRunState"/> 1:1 since the controller serializes
/// <see cref="ArchidektCacheJobStatus.State"/> via <c>ToString()</c>.
/// </summary>
public enum ArchidektCacheJobState
{
    /// <summary>Row inserted, worker has not picked it up yet.</summary>
    Queued,

    /// <summary>Worker is processing decks.</summary>
    Running,

    /// <summary>Operator cancel requested; worker is winding down.</summary>
    Stopping,

    /// <summary>Run completed normally.</summary>
    Succeeded,

    /// <summary>Run aborted on exception, redeploy reaper, or upstream failure.</summary>
    Failed,

    /// <summary>Run terminated cleanly via operator cancel (HARV-03).</summary>
    Cancelled
}

/// <summary>
/// Public wire-shape for a single <c>harvest_runs</c> row of <c>kind='bulk'</c>
/// surfaced through <c>ArchidektCacheJobsController</c>. Constructed from
/// <see cref="HarvestRunRow"/> on read.
/// </summary>
/// <param name="JobId">Server-generated UUID primary key.</param>
/// <param name="State">Current state-machine position.</param>
/// <param name="DurationSeconds">Operator-selected duration cap.</param>
/// <param name="RequestedUtc">Wall-clock time the row was inserted.</param>
/// <param name="StartedUtc">Wall-clock time the worker entered Running.</param>
/// <param name="CompletedUtc">Wall-clock time the row reached a terminal state.</param>
/// <param name="DecksProcessed">Decks fully imported during the run.</param>
/// <param name="AdditionalDecksFound">Newly-discovered deck IDs added to the queue.</param>
/// <param name="ErrorMessage">Failure / cancel / reaper reason; null on success.</param>
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

/// <summary>
/// Result of <see cref="IArchidektCacheJobService.EnqueueAsync"/>. <see cref="StartedNewJob"/>
/// is true only when a new <c>Queued</c> row was inserted; false when an active row already
/// existed and was returned to the caller.
/// </summary>
/// <param name="Job">Public-shape job status (live row or newly inserted).</param>
/// <param name="StartedNewJob">Whether a new row was inserted on this call.</param>
public sealed record ArchidektCacheJobEnqueueResult(
    ArchidektCacheJobStatus Job,
    bool StartedNewJob);

/// <summary>
/// Background harvest job runner. State source-of-truth is Postgres
/// <c>harvest_runs</c> via <see cref="IHarvestRunStore"/> (D-01). Channel-based
/// queue retained per RESEARCH Q3 RESOLVED — keep the BackgroundService+Channel
/// shape, do not refactor. Per-job linked <see cref="CancellationTokenSource"/>
/// (linked to host stoppingToken) provides graceful operator cancel (D-05);
/// <see cref="CancelActiveAsync"/> calls <c>Cancel()</c> which propagates OCE
/// through the existing inner deck-loop in
/// <c>ArchidektDeckCacheSession.RunAsync</c>.
/// </summary>
public sealed class ArchidektCacheJobService : BackgroundService, IArchidektCacheJobService
{
    private readonly Channel<QueuedJobSignal> _queue = Channel.CreateUnbounded<QueuedJobSignal>();
    private readonly ICategoryKnowledgeStore _knowledgeStore;
    private readonly IHarvestRunStore _runStore;
    private readonly ILogger<ArchidektCacheJobService> _logger;

    // T-07-08: lock-protected per-job CTS. Only one active job at a time by
    // design (D-01 single-active-bulk contract); cancel is idempotent on an
    // already-cancelled CTS.
    private readonly object _ctsLock = new();
    private CancellationTokenSource? _activeJobCts;

    /// <summary>
    /// DI ctor. Resolves the knowledge store, harvest run store, and logger from
    /// the service container. <see cref="IHarvestRunStore"/> is registered in
    /// <c>Program.cs</c> by Plan 07's DI wiring.
    /// </summary>
    /// <param name="knowledgeStore">Category-knowledge store (sweeps run against this).</param>
    /// <param name="runStore">Postgres harvest run store — single source of truth for state (D-01).</param>
    /// <param name="logger">Structured logger.</param>
    public ArchidektCacheJobService(
        ICategoryKnowledgeStore knowledgeStore,
        IHarvestRunStore runStore,
        ILogger<ArchidektCacheJobService> logger)
    {
        ArgumentNullException.ThrowIfNull(knowledgeStore);
        ArgumentNullException.ThrowIfNull(runStore);
        ArgumentNullException.ThrowIfNull(logger);
        _knowledgeStore = knowledgeStore;
        _runStore = runStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be greater than zero.");
        }

        if (duration > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration cannot exceed one hour.");
        }

        // D-01: PG is source of truth — check for any active row before inserting.
        var active = await _runStore.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (active is not null)
        {
            return new ArchidektCacheJobEnqueueResult(MapToStatus(active), StartedNewJob: false);
        }

        var durationSeconds = (int)Math.Ceiling(duration.TotalSeconds);
        var requestedUtc = DateTimeOffset.UtcNow;

        // D-03: insert Queued row, get the UUID.
        var jobId = await _runStore.InsertQueuedAsync(
            HarvestRunKind.Bulk,
            durationSeconds,
            url: null,
            requestedUtc,
            cancellationToken).ConfigureAwait(false);

        var writeAccepted = _queue.Writer.TryWrite(new QueuedJobSignal(jobId, durationSeconds));
        _logger.LogInformation(
            "Harvest.Worker.SignalEnqueued jobId={JobId} writeAccepted={WriteAccepted}",
            jobId, writeAccepted);

        var status = new ArchidektCacheJobStatus(
            jobId,
            ArchidektCacheJobState.Queued,
            durationSeconds,
            requestedUtc,
            StartedUtc: null,
            CompletedUtc: null,
            DecksProcessed: 0,
            AdditionalDecksFound: 0,
            ErrorMessage: null);
        return new ArchidektCacheJobEnqueueResult(status, StartedNewJob: true);
    }

    /// <inheritdoc />
    public ArchidektCacheJobStatus? GetJob(Guid jobId)
    {
        var row = _runStore.GetByIdAsync(jobId, CancellationToken.None).GetAwaiter().GetResult();
        return row is null ? null : MapToStatus(row);
    }

    /// <inheritdoc />
    public ArchidektCacheJobStatus? GetActiveJob()
    {
        var active = _runStore.GetActiveAsync(CancellationToken.None).GetAwaiter().GetResult();
        return active is null ? null : MapToStatus(active);
    }

    /// <inheritdoc />
    public Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cts;
        lock (_ctsLock)
        {
            cts = _activeJobCts;
        }
        if (cts is null)
        {
            return Task.FromResult(false);
        }

        // D-05: signalling cancel propagates OCE through the inner deck-loop on
        // the next per-deck check. The catch (OperationCanceledException) clause
        // in ExecuteAsync flips state to Cancelled. The interim Stopping row is
        // written by the controller (Plan 04) BEFORE calling this method so the
        // AJAX poll sees it within 1s.
        cts.Cancel();
        _logger.LogInformation("Harvest.Run.CancelRequested");
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Harvest.Worker.LoopEntered");
        await foreach (var signal in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            _logger.LogInformation("Harvest.Worker.SignalDequeued jobId={JobId}", signal.JobId);
            // D-05: link host stoppingToken with a per-job CTS for graceful operator cancel.
            using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            lock (_ctsLock)
            {
                _activeJobCts = jobCts;
            }

            try
            {
                _logger.LogInformation(
                    "Harvest.Run.StateChange jobId={JobId} state={State} decksProcessed={DecksProcessed}",
                    signal.JobId, HarvestRunState.Running, 0);

                await _runStore.UpdateStateAsync(
                    signal.JobId,
                    HarvestRunState.Running,
                    startedUtc: DateTimeOffset.UtcNow,
                    completedUtc: null,
                    decksProcessed: 0,
                    additionalDecksFound: 0,
                    errorMessage: null,
                    jobCts.Token).ConfigureAwait(false);

                var initialDeckCount = await _knowledgeStore.GetProcessedDeckCountAsync(jobCts.Token).ConfigureAwait(false);
                var progress = new HarvestProgressWriter(signal.JobId, _runStore, _logger, jobCts.Token);
                var decksProcessed = await _knowledgeStore.RunCacheSweepAsync(_logger, signal.DurationSeconds, jobCts.Token, progress).ConfigureAwait(false);
                var finalDeckCount = await _knowledgeStore.GetProcessedDeckCountAsync(jobCts.Token).ConfigureAwait(false);

                _logger.LogInformation(
                    "Harvest.Run.StateChange jobId={JobId} state={State} decksProcessed={DecksProcessed}",
                    signal.JobId, HarvestRunState.Succeeded, decksProcessed);

                await _runStore.UpdateStateAsync(
                    signal.JobId,
                    HarvestRunState.Succeeded,
                    startedUtc: null,
                    completedUtc: DateTimeOffset.UtcNow,
                    decksProcessed: decksProcessed,
                    additionalDecksFound: Math.Max(finalDeckCount - initialDeckCount, 0),
                    errorMessage: null,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host shutdown. Previously we rethrew without writing a terminal
                // state — the row stayed Running until the next process start,
                // when the reaper labelled it "interrupted by redeploy". That
                // conflated host-shutdown / OOM-kill / SIGTERM with actual
                // redeploys and made the Run Log misleading. Now we write Failed
                // with a precise reason BEFORE rethrowing so the reaper only
                // ever sees rows orphaned by SIGKILL (no graceful shutdown).
                _logger.LogInformation(
                    "Harvest.Run.StateChange jobId={JobId} state={State} reason={Reason}",
                    signal.JobId, HarvestRunState.Failed, "interrupted by host shutdown");

                try
                {
                    await _runStore.UpdateStateAsync(
                        signal.JobId,
                        HarvestRunState.Failed,
                        startedUtc: null,
                        completedUtc: DateTimeOffset.UtcNow,
                        decksProcessed: 0,
                        additionalDecksFound: 0,
                        errorMessage: "interrupted by host shutdown",
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception writeException)
                {
                    // Best-effort terminal write during shutdown. If PG is also
                    // shutting down and the write fails, fall back to the reaper.
                    _logger.LogWarning(writeException, "Harvest.Run.TerminalWriteFailed jobId={JobId}", signal.JobId);
                }

                throw;
            }
            catch (OperationCanceledException) when (_activeJobCts?.IsCancellationRequested == true && !stoppingToken.IsCancellationRequested)
            {
                // D-05: operator cancel landed on the per-job CTS. Use CancellationToken.None
                // for the terminal write so the cancelled token doesn't abort it.
                _logger.LogInformation(
                    "Harvest.Run.StateChange jobId={JobId} state={State} decksProcessed={DecksProcessed}",
                    signal.JobId, HarvestRunState.Cancelled, 0);

                await _runStore.UpdateStateAsync(
                    signal.JobId,
                    HarvestRunState.Cancelled,
                    startedUtc: null,
                    completedUtc: DateTimeOffset.UtcNow,
                    decksProcessed: 0,
                    additionalDecksFound: 0,
                    errorMessage: "cancelled by operator",
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation landed on jobCts.Token but neither stoppingToken
                // nor _activeJobCts triggered it. This should not happen with the
                // current wiring — jobCts is linked only to stoppingToken — but
                // defending against future regressions where a request-scoped CT
                // leaks into the harvest path. Write a terminal Failed row so the
                // reaper never has to label this orphaned.
                _logger.LogWarning(
                    "Harvest.Run.UnexpectedCancellation jobId={JobId} stoppingTokenCancelled={Stopping}",
                    signal.JobId, stoppingToken.IsCancellationRequested);

                await _runStore.UpdateStateAsync(
                    signal.JobId,
                    HarvestRunState.Failed,
                    startedUtc: null,
                    completedUtc: DateTimeOffset.UtcNow,
                    decksProcessed: 0,
                    additionalDecksFound: 0,
                    errorMessage: "interrupted by unexpected cancellation",
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Harvest.Run.Failed jobId={JobId} message={Message}", signal.JobId, exception.Message);
                await _runStore.UpdateStateAsync(
                    signal.JobId,
                    HarvestRunState.Failed,
                    startedUtc: null,
                    completedUtc: DateTimeOffset.UtcNow,
                    decksProcessed: 0,
                    additionalDecksFound: 0,
                    errorMessage: exception.Message,
                    CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                lock (_ctsLock)
                {
                    _activeJobCts = null;
                }
            }
        }
    }

    /// <summary>
    /// Maps a <see cref="HarvestRunRow"/> from <see cref="IHarvestRunStore"/> to the
    /// public-API <see cref="ArchidektCacheJobStatus"/> shape consumed by
    /// <c>ArchidektCacheJobsController</c>. Both enums share names — direct
    /// <see cref="Enum.Parse{TEnum}(string)"/> roundtrips through the string form
    /// so future renames stay catchable.
    /// </summary>
    /// <param name="row">Raw run row from the store.</param>
    /// <returns>Public-shape status the controller serializes.</returns>
    private static ArchidektCacheJobStatus MapToStatus(HarvestRunRow row)
    {
        var state = Enum.Parse<ArchidektCacheJobState>(row.State.ToString(), ignoreCase: false);
        return new ArchidektCacheJobStatus(
            row.Id,
            state,
            row.DurationSeconds,
            row.RequestedUtc,
            row.StartedUtc,
            row.CompletedUtc,
            row.DecksProcessed,
            row.AdditionalDecksFound,
            row.ErrorMessage);
    }

    /// <summary>
    /// Internal channel signal — carries just the IDs needed to drive the worker
    /// loop. Replaces the old <c>ArchidektCacheJobStatus</c> channel value type:
    /// the worker re-reads state from PG via <see cref="IHarvestRunStore"/>
    /// rather than carrying status across the channel.
    /// </summary>
    /// <param name="JobId">Server-generated UUID for the queued run.</param>
    /// <param name="DurationSeconds">Operator-selected sweep cap.</param>
    private sealed record QueuedJobSignal(Guid JobId, int DurationSeconds);

    private sealed class HarvestProgressWriter : IProgress<int>
    {
        private static readonly TimeSpan WriteInterval = TimeSpan.FromSeconds(2);
        private const int DeckThreshold = 10;

        private readonly Guid _jobId;
        private readonly IHarvestRunStore _runStore;
        private readonly ILogger _logger;
        private readonly CancellationToken _cancellationToken;
        private readonly object _gate = new();
        private readonly Stopwatch _sinceLastWrite = Stopwatch.StartNew();
        private int _latestReportedDecks;
        private int _lastWrittenDecks;
        private bool _writeInFlight;

        public HarvestProgressWriter(Guid jobId, IHarvestRunStore runStore, ILogger logger, CancellationToken cancellationToken)
        {
            _jobId = jobId;
            _runStore = runStore;
            _logger = logger;
            _cancellationToken = cancellationToken;
        }

        public void Report(int value)
        {
            lock (_gate)
            {
                if (value > _latestReportedDecks)
                {
                    _latestReportedDecks = value;
                }

                TryStartWriteLocked();
            }
        }

        private async Task WriteProgressAsync(int decksProcessed)
        {
            try
            {
                await _runStore.UpdateProgressAsync(
                    _jobId,
                    decksProcessed,
                    additionalDecksFound: 0,
                    _cancellationToken).ConfigureAwait(false);

                lock (_gate)
                {
                    if (decksProcessed > _lastWrittenDecks)
                    {
                        _lastWrittenDecks = decksProcessed;
                    }

                    _sinceLastWrite.Restart();
                    _writeInFlight = false;
                    TryStartWriteLocked();
                }
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                lock (_gate)
                {
                    _writeInFlight = false;
                }
            }
            catch (Exception exception)
            {
                lock (_gate)
                {
                    _writeInFlight = false;
                }

                _logger.LogWarning(exception, "Harvest.Run.ProgressWriteFailed jobId={JobId} decksProcessed={DecksProcessed}", _jobId, decksProcessed);
            }
        }

        private void TryStartWriteLocked()
        {
            if (_writeInFlight)
            {
                return;
            }

            var nextDecks = _latestReportedDecks;
            if (nextDecks <= _lastWrittenDecks)
            {
                return;
            }

            if ((nextDecks - _lastWrittenDecks) < DeckThreshold && _sinceLastWrite.Elapsed < WriteInterval)
            {
                return;
            }

            _writeInFlight = true;
            _ = WriteProgressAsync(nextDecks);
        }
    }
}
