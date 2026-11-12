using System.Data.Common;
using System.Globalization;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>Stores durable maintenance run records in SQLite or Postgres.</summary>
public sealed class HarvestMaintenanceRunStore : IHarvestMaintenanceRunStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly ILogger<HarvestMaintenanceRunStore> _logger;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>Creates a SQLite run store.</summary>
    public HarvestMaintenanceRunStore(string databasePath, ILogger<HarvestMaintenanceRunStore> logger)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath), logger, () => DateTimeOffset.UtcNow) { }

    internal HarvestMaintenanceRunStore(string databasePath, ILogger<HarvestMaintenanceRunStore> logger, Func<DateTimeOffset> utcNow)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath), logger, utcNow) { }

    /// <summary>Creates a store for the configured harvest database.</summary>
    public HarvestMaintenanceRunStore(IWebHostEnvironment environment, ILogger<HarvestMaintenanceRunStore> logger)
        : this(DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection(environment), logger, () => DateTimeOffset.UtcNow) { }

    /// <summary>Creates a store for an explicit relational database.</summary>
    public HarvestMaintenanceRunStore(RelationalDatabaseConnection connectionInfo, ILogger<HarvestMaintenanceRunStore> logger)
        : this(connectionInfo, logger, () => DateTimeOffset.UtcNow) { }

    private HarvestMaintenanceRunStore(RelationalDatabaseConnection connectionInfo, ILogger<HarvestMaintenanceRunStore> logger, Func<DateTimeOffset> utcNow)
    {
        _connectionInfo = connectionInfo;
        _logger = logger;
        _utcNow = utcNow;
        if (connectionInfo.IsSqlite)
        {
            var directory = Path.GetDirectoryName(connectionInfo.ExtractSqlitePath());
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        }
    }

    /// <inheritdoc/>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            var time = _connectionInfo.IsPostgres ? "TIMESTAMPTZ" : "TEXT";
            var id = _connectionInfo.IsPostgres ? "UUID" : "TEXT";
            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS harvest_maintenance_runs (
                    run_id {id} PRIMARY KEY, operation TEXT NOT NULL, state TEXT NOT NULL,
                    started_utc {time} NOT NULL, last_progress_utc {time}, ended_utc {time},
                    decks_affected INTEGER NOT NULL DEFAULT 0, batches INTEGER NOT NULL DEFAULT 0,
                    stop_reason TEXT, stop_requested INTEGER NOT NULL DEFAULT 0, refusal_reason TEXT,
                    preview_retention_excess INTEGER, preview_purge_candidates INTEGER,
                    preview_refetch_candidates INTEGER, preview_as_of_utc {time});
                CREATE INDEX IF NOT EXISTS ix_harvest_maintenance_runs_started ON harvest_maintenance_runs (started_utc DESC);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            command.CommandText = "UPDATE harvest_maintenance_runs SET state = 'Interrupted', ended_utc = @now WHERE state IN ('Starting','Running')";
            RelationalDatabaseConnection.AddParameter(command, "@now", Time(_utcNow()));
            var count = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (count > 0) _logger.LogInformation("Harvest.Maintenance.RunsInterrupted count={Count}", count);
            _schemaReady = true;
        }
        finally { _schemaGate.Release(); }
    }

    /// <inheritdoc/>
    public async Task InsertAsync(HarvestMaintenanceStatus status, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO harvest_maintenance_runs (run_id,operation,state,started_utc,last_progress_utc,ended_utc,decks_affected,batches,stop_reason,stop_requested,refusal_reason,preview_retention_excess,preview_purge_candidates,preview_refetch_candidates,preview_as_of_utc) VALUES (@id,@operation,@state,@started,@progress,@ended,@decks,@batches,@stop,@requested,@refusal,@retention,@purge,@refetch,@preview)";
        Bind(command, status);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(HarvestMaintenanceStatus status, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        const string allowState = "state IN ('Starting','Running') OR @state IN ('Finished','Stopped','Failed','Refused')";
        command.CommandText = $"""
            UPDATE harvest_maintenance_runs SET
                last_progress_utc=@progress, decks_affected=@decks, batches=@batches, stop_requested=@requested,
                preview_retention_excess=@retention, preview_purge_candidates=@purge, preview_refetch_candidates=@refetch, preview_as_of_utc=@preview,
                state=CASE WHEN {allowState} THEN @state ELSE state END,
                ended_utc=CASE WHEN {allowState} THEN @ended ELSE ended_utc END,
                stop_reason=CASE WHEN {allowState} THEN @stop ELSE stop_reason END,
                refusal_reason=CASE WHEN {allowState} THEN @refusal ELSE refusal_reason END
            WHERE run_id=@id AND state IN ('Starting','Running','Interrupted')
            """;
        Bind(command, status);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<HarvestMaintenanceStatus?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM harvest_maintenance_runs ORDER BY started_utc DESC LIMIT 1";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        string? Text(string column) => reader[column] is DBNull ? null : Convert.ToString(reader[column], CultureInfo.InvariantCulture);
        DateTimeOffset? Date(string column) => reader[column] is DBNull ? null : reader[column] is DateTime date ? new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)) : DateTimeOffset.Parse(Text(column)!, CultureInfo.InvariantCulture);
        int Number(string column) => Convert.ToInt32(reader[column], CultureInfo.InvariantCulture);
        return new HarvestMaintenanceStatus
        {
            RunId = Guid.Parse(Text("run_id")!),
            Operation = Enum.Parse<HarvestMaintenanceOperation>(Text("operation")!),
            State = Enum.Parse<HarvestMaintenanceRunState>(Text("state")!),
            StartedUtc = Date("started_utc"),
            LastProgressUtc = Date("last_progress_utc"),
            EndedUtc = Date("ended_utc"),
            DecksAffected = Number("decks_affected"),
            Batches = Number("batches"),
            StopRequested = Number("stop_requested") != 0,
            StopReason = Text("stop_reason") is { } reason ? Enum.Parse<DeckPruningStopReason>(reason) : null,
            RefusalReason = Text("refusal_reason"),
            PreviewAsOfUtc = Date("preview_as_of_utc"),
            PreviewAtStart = reader["preview_retention_excess"] is DBNull ? null : new(Number("preview_retention_excess"), Number("preview_purge_candidates"), Number("preview_refetch_candidates"))
        };
    }

    private object? Time(DateTimeOffset? value) => _connectionInfo.IsPostgres ? value?.UtcDateTime : value?.ToString("O", CultureInfo.InvariantCulture);
    private void Bind(DbCommand command, HarvestMaintenanceStatus status)
    {
        void Add(string name, object? value) => RelationalDatabaseConnection.AddParameter(command, name, value);
        Add("@id", _connectionInfo.IsPostgres ? status.RunId : status.RunId?.ToString());
        Add("@operation", status.Operation?.ToString()); Add("@state", status.State?.ToString());
        Add("@started", Time(status.StartedUtc)); Add("@progress", Time(status.LastProgressUtc)); Add("@ended", Time(status.EndedUtc));
        Add("@decks", status.DecksAffected); Add("@batches", status.Batches); Add("@stop", status.StopReason?.ToString());
        Add("@requested", status.StopRequested ? 1 : 0); Add("@refusal", status.RefusalReason);
        Add("@retention", status.PreviewAtStart?.RetentionExcessDecks); Add("@purge", status.PreviewAtStart?.PurgeCandidates);
        Add("@refetch", status.PreviewAtStart?.RefetchCandidates); Add("@preview", Time(status.PreviewAsOfUtc));
    }
}
