using System.Data.Common;
using System.Globalization;
using Dapper;
using DeckFlow.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Default implementation of <see cref="IHarvestRunStore"/> backed by
/// <see cref="RelationalDatabaseConnection"/> (Postgres in production, SQLite in tests
/// and local-dev). Schema is lazy-initialized via a SemaphoreSlim gate that also runs
/// the D-02 startup reaper (UPDATE non-terminal rows to <c>Failed</c>) on first call.
/// Mirrors the <see cref="DeckFlow.Web.Services.FeatureFlags.FeatureFlagStore"/> shape.
/// Stats invalidation resolves <see cref="IHarvestStatsAggregator"/> lazily from an
/// optional <see cref="IServiceProvider"/> so run-store writes can invalidate the
/// aggregate cache without creating a circular constructor graph.
/// </summary>
public sealed class HarvestRunStore : IHarvestRunStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly IServiceProvider? _services;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
    /// Mirrors the <c>FeatureFlagStore</c> test-seam ctor for in-memory / temp-file
    /// SQLite tests.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file (created if missing).</param>
    /// <param name="services">Optional service provider used for best-effort stats invalidation after writes.</param>
    public HarvestRunStore(string databasePath, IServiceProvider? services = null)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath), services) { }

    /// <summary>
    /// Creates a store using the supplied <see cref="RelationalDatabaseConnection"/>
    /// directly. Used by tests that want to inject a Postgres-or-SQLite connection
    /// without going through the DI factory.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="services">Optional service provider used for best-effort stats invalidation after writes.</param>
    public HarvestRunStore(RelationalDatabaseConnection connectionInfo, IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
        _services = services;
        if (_connectionInfo.IsSqlite)
        {
            var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    /// <summary>
    /// DI ctor — resolves the connection via
    /// <see cref="DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection"/>,
    /// which shares the feedback DB (D-07), and keeps stats invalidation lazy to
    /// avoid the run-store/stats circular dependency at startup.
    /// </summary>
    /// <param name="environment">Web host environment used by the connection factory.</param>
    /// <param name="services">Optional service provider used for best-effort stats invalidation after writes.</param>
    public HarvestRunStore(IWebHostEnvironment environment, IServiceProvider? services = null)
        : this(DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection(environment), services) { }

    /// <inheritdoc />
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            // Why: schema creation and constraint migration are intentional raw ADO.NET carve-outs for this phase.
            await using (var create = connection.CreateCommand())
            {
                create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await EnsureHarvestRunsConstraintsAsync(connection, cancellationToken).ConfigureAwait(false);
            if (_connectionInfo.IsPostgres)
            {
                await EnsurePostgresHarvestRunsShapeAsync(connection, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Add after SQLite rebuild as a backstop for tables that need no rebuild.
                await EnsureAdditiveColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
            }

            await using (var reaper = connection.CreateCommand())
            {
                reaper.CommandText = _connectionInfo.IsPostgres ? PostgresReaperSql : SqliteReaperSql;
                await reaper.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Guid> InsertQueuedAsync(
        HarvestRunKind kind,
        int durationSeconds,
        string? url,
        DateTimeOffset now,
        HarvestTriggerSource? triggerSource,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var id = Guid.NewGuid();
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO harvest_runs (id, kind, state, requested_utc, duration_seconds, url, trigger_source)
            VALUES (@id, @kind, 'Queued', @now, @duration, @url, @triggerSource);
            """,
            new
            {
                id,
                kind = ToStoredKind(kind),
                now,
                duration = durationSeconds,
                url,
                triggerSource = ToStoredTrigger(triggerSource)
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        // D-13: explicit invalidation so the stats panel reflects the new queued row.
        InvalidateStats();
        return id;
    }

    /// <inheritdoc />
    public async Task UpdateStateAsync(
        Guid id,
        HarvestRunState state,
        DateTimeOffset? startedUtc,
        DateTimeOffset? completedUtc,
        int? decksProcessed,
        int? additionalDecksFound,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE harvest_runs
               SET state = @state,
                   started_utc = COALESCE(@startedUtc, started_utc),
                   completed_utc = COALESCE(@completedUtc, completed_utc),
                   decks_processed = COALESCE(@decksProcessed, decks_processed),
                   additional_decks_found = COALESCE(@additionalDecksFound, additional_decks_found),
                   error_message = @errorMessage
             WHERE id = @id;
            """,
            new
            {
                id,
                state = state.ToString(),
                startedUtc,
                completedUtc,
                decksProcessed,
                additionalDecksFound,
                errorMessage
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        // D-13: explicit invalidation on every state change.
        InvalidateStats();
    }

    /// <inheritdoc />
    public async Task UpdateProgressAsync(
        Guid id,
        int decksProcessed,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE harvest_runs
               SET decks_processed = @decksProcessed
             WHERE id = @id;
            """,
            new { id, decksProcessed },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        InvalidateStats();
    }

    /// <inheritdoc />
    public async Task SetSweepCountsAsync(Guid id, int decksEnqueued, int decksDrained, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE harvest_runs SET decks_enqueued = @decksEnqueued, decks_drained = @decksDrained WHERE id = @id;",
            new { id, decksEnqueued, decksDrained }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        InvalidateStats();
    }

    /// <inheritdoc />
    public async Task SetUpdateCountsAsync(Guid id, int pagesPolled, int refreshesRequeued, int refreshesDrained, int newIdsSeen, CancellationToken cancellationToken = default)
    {
        // D-05: update runs never touch sweep counts, keeping zero-discovery bulk-only.
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE harvest_runs SET pages_polled = @pagesPolled, refreshes_requeued = @refreshesRequeued, refreshes_drained = @refreshesDrained, new_ids_seen = @newIdsSeen WHERE id = @id;",
            new { id, pagesPolled, refreshesRequeued, refreshesDrained, newIdsSeen }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        InvalidateStats();
    }

    /// <inheritdoc />
    public async Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<HarvestRunRowData>(new CommandDefinition(
            """
            SELECT id, kind, state, requested_utc, started_utc, completed_utc,
                   duration_seconds, decks_processed, additional_decks_found, decks_enqueued, decks_drained, error_message, url, trigger_source, pages_polled, refreshes_requeued, refreshes_drained, new_ids_seen
              FROM harvest_runs
             WHERE state IN ('Queued','Running','Stopping')
             ORDER BY requested_utc DESC
             LIMIT 1;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : ToHarvestRunRow(row);
    }

    /// <inheritdoc />
    public async Task<HarvestRunRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<HarvestRunRowData>(new CommandDefinition(
            """
            SELECT id, kind, state, requested_utc, started_utc, completed_utc,
                    duration_seconds, decks_processed, additional_decks_found, decks_enqueued, decks_drained, error_message, url, trigger_source, pages_polled, refreshes_requeued, refreshes_drained, new_ids_seen
              FROM harvest_runs
             WHERE id = @id
             LIMIT 1;
            """,
            new { id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : ToHarvestRunRow(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        // NULLS LAST works on Postgres natively; SQLite >= 3.30 (shipped with Microsoft.Data.Sqlite 10) supports it too.
        var rows = await connection.QueryAsync<HarvestRunRowData>(new CommandDefinition(
            """
            SELECT id, kind, state, requested_utc, started_utc, completed_utc,
                   duration_seconds, decks_processed, additional_decks_found, decks_enqueued, decks_drained, error_message, url, trigger_source, pages_polled, refreshes_requeued, refreshes_drained, new_ids_seen
              FROM harvest_runs
             ORDER BY started_utc DESC NULLS LAST
             LIMIT @n;
            """,
            new { n },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToHarvestRunRow).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HarvestRunRow>> GetRecentHealthSignalRunsAsync(int n, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        // Succeeded is essential: sweep counts are written before the terminal state transition.
        var rows = await connection.QueryAsync<HarvestRunRowData>(new CommandDefinition(
            """
            SELECT id, kind, state, requested_utc, started_utc, completed_utc,
                   duration_seconds, decks_processed, additional_decks_found, decks_enqueued, decks_drained, error_message, url, trigger_source, pages_polled, refreshes_requeued, refreshes_drained, new_ids_seen
              FROM harvest_runs
             WHERE kind = 'bulk' AND state = 'Succeeded'
               AND decks_enqueued IS NOT NULL AND decks_drained IS NOT NULL
             ORDER BY completed_utc DESC NULLS LAST
             LIMIT @n;
            """,
            new { n },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToHarvestRunRow).ToList();
    }

    /// <inheritdoc />
    public async Task<string> GetRecentRevisionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<HarvestRunRevisionRow>(new CommandDefinition(
            "SELECT MAX(started_utc) AS started_utc, MAX(completed_utc) AS completed_utc, COUNT(1) AS count FROM harvest_runs;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (row is null)
        {
            return "||0";
        }

        var startedTicks = row.StartedUtc is null
            ? string.Empty
            : row.StartedUtc.Value.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
        var completedTicks = row.CompletedUtc is null
            ? string.Empty
            : row.CompletedUtc.Value.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);

        return $"{startedTicks}|{completedTicks}|{row.Count.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<DateTimeOffset?>(new CommandDefinition(
            "SELECT MAX(completed_utc) FROM harvest_runs WHERE state='Succeeded';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastScheduledSuccessUtcAsync(HarvestRunKind kind, CancellationToken cancellationToken = default)
    {
        // Manual runs must not move a schedule; legacy NULL counts as scheduled on deploy.
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var completedUtc = await connection.ExecuteScalarAsync<object?>(new CommandDefinition(
            "SELECT MAX(completed_utc) FROM harvest_runs WHERE kind = @kind AND state = 'Succeeded' AND (trigger_source = 'scheduled' OR trigger_source IS NULL);",
            new { kind = ToStoredKind(kind) }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return ConvertCompletedUtc(completedUtc);
    }

    /// <inheritdoc />
    public async Task<HarvestFailureStreak> GetFailureStreakSinceLastSuccessAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleAsync<FailureStreakRow>(new CommandDefinition(
            """
            SELECT COUNT(1) AS ConsecutiveFailures,
                   MAX(completed_utc) AS LastFailureUtc,
                   (SELECT MAX(completed_utc) FROM harvest_runs WHERE state = 'Succeeded') AS LastSuccessUtc
            FROM harvest_runs
            WHERE state = 'Failed'
              AND kind = 'bulk'
              AND completed_utc IS NOT NULL
              AND (NOT EXISTS (SELECT 1 FROM harvest_runs WHERE state = 'Succeeded')
                   OR completed_utc > (SELECT MAX(completed_utc) FROM harvest_runs WHERE state = 'Succeeded'));
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return new HarvestFailureStreak(
            checked((int)row.ConsecutiveFailures),
            ConvertCompletedUtc(row.LastFailureUtc),
            ConvertCompletedUtc(row.LastSuccessUtc));
    }

    /// <inheritdoc />
    public async Task<HarvestFailureStreak> GetFailureStreakSinceLastSuccessAsync(HarvestRunKind kind, CancellationToken cancellationToken = default)
    {
        // Manual runs must not move a schedule; legacy NULL counts as scheduled on deploy.
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleAsync<FailureStreakRow>(new CommandDefinition(
            """
            SELECT COUNT(1) AS ConsecutiveFailures, MAX(completed_utc) AS LastFailureUtc,
                   (SELECT MAX(completed_utc) FROM harvest_runs WHERE kind = @kind AND state = 'Succeeded' AND (trigger_source = 'scheduled' OR trigger_source IS NULL)) AS LastSuccessUtc
              FROM harvest_runs
             WHERE kind = @kind AND state = 'Failed' AND completed_utc IS NOT NULL
               AND (trigger_source = 'scheduled' OR trigger_source IS NULL)
               AND (NOT EXISTS (SELECT 1 FROM harvest_runs WHERE kind = @kind AND state = 'Succeeded' AND (trigger_source = 'scheduled' OR trigger_source IS NULL))
                    OR completed_utc > (SELECT MAX(completed_utc) FROM harvest_runs WHERE kind = @kind AND state = 'Succeeded' AND (trigger_source = 'scheduled' OR trigger_source IS NULL)));
            """, new { kind = ToStoredKind(kind) }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return new HarvestFailureStreak(checked((int)row.ConsecutiveFailures), ConvertCompletedUtc(row.LastFailureUtc), ConvertCompletedUtc(row.LastSuccessUtc));
    }

    /// <inheritdoc />
    public async Task<long> GetTotalSucceededCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT(1) FROM harvest_runs WHERE state='Succeeded';",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private void InvalidateStats()
    {
        try
        {
            _services?.GetService<IHarvestStatsAggregator>()?.Invalidate();
        }
        catch
        {
            // Best-effort invalidation must never break a successful write path.
        }
    }

    private static DateTimeOffset? ConvertCompletedUtc(object? value)
        => value switch
        {
            null or DBNull => null,
            DateTimeOffset completedUtc => completedUtc,
            DateTime completedUtc => new DateTimeOffset(completedUtc.Kind == DateTimeKind.Local
                ? completedUtc.ToUniversalTime()
                : DateTime.SpecifyKind(completedUtc, DateTimeKind.Utc)),
            string completedUtc => DateTimeOffset.Parse(completedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            _ => throw new InvalidOperationException($"Unsupported completed_utc value type: {value.GetType().FullName}.")
        };

    /// <summary>Maps consecutive failures and boundary timestamps used to measure harvest failure streaks.</summary>
    private sealed class FailureStreakRow
    {
        public long ConsecutiveFailures { get; init; }

        public object? LastFailureUtc { get; init; }

        public object? LastSuccessUtc { get; init; }
    }

    private static HarvestRunRow ToHarvestRunRow(HarvestRunRowData row)
        => new(
            row.Id,
            ParseHarvestKind(row.Kind),
            Enum.Parse<HarvestRunState>(row.State, ignoreCase: false),
            row.RequestedUtc,
            row.StartedUtc,
            row.CompletedUtc,
            row.DurationSeconds,
            row.DecksProcessed,
            row.AdditionalDecksFound,
            row.DecksEnqueued,
            row.DecksDrained,
            row.ErrorMessage,
            row.Url,
            ParseHarvestTriggerSource(row.TriggerSource),
            row.PagesPolled,
            row.RefreshesRequeued,
            row.RefreshesDrained,
            row.NewIdsSeen);

    private static HarvestRunKind ParseHarvestKind(string raw) => raw switch
    {
        "bulk" => HarvestRunKind.Bulk,
        "url" => HarvestRunKind.Url,
        "update" => HarvestRunKind.Update,
        _ => throw new InvalidOperationException($"Unknown harvest_runs.kind value '{raw}'.")
    };

    private static string ToStoredKind(HarvestRunKind kind) => kind switch
    {
        HarvestRunKind.Bulk => "bulk",
        HarvestRunKind.Url => "url",
        HarvestRunKind.Update => "update",
        // An unmapped member must fail loudly; the old ternary would write it as URL.
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown harvest run kind.")
    };

    private static string? ToStoredTrigger(HarvestTriggerSource? triggerSource) => triggerSource switch
    {
        null => null,
        HarvestTriggerSource.Manual => "manual",
        HarvestTriggerSource.Scheduled => "scheduled",
        _ => throw new ArgumentOutOfRangeException(nameof(triggerSource), triggerSource, "Unknown harvest trigger source.")
    };

    private static HarvestTriggerSource? ParseHarvestTriggerSource(string? raw) => raw switch
    {
        null => null,
        "manual" => HarvestTriggerSource.Manual,
        "scheduled" => HarvestTriggerSource.Scheduled,
        _ => throw new InvalidOperationException($"Unknown harvest_runs.trigger_source value '{raw}'.")
    };

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionInfo.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private async Task EnsureHarvestRunsConstraintsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (_connectionInfo.IsSqlite)
        {
            await EnsureSqliteHarvestRunsConstraintsCurrentAsync(connection, cancellationToken).ConfigureAwait(false);
            return;
        }

        await EnsurePostgresStateConstraintAllowsInterruptedAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureAdditiveColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var columns = await GetHarvestRunColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (var column in AdditiveColumns)
        {
            if (columns.Contains(column.Name))
            {
                continue;
            }

            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    $"ALTER TABLE harvest_runs ADD COLUMN {(_connectionInfo.IsPostgres ? "IF NOT EXISTS " : string.Empty)}{column.Name} {(_connectionInfo.IsPostgres ? column.PostgresType : column.SqliteType)} NULL;",
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
            catch (DbException exception) when (exception.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
            {
                // Another process won the additive migration race.
            }
        }
    }

    private async Task<HashSet<string>> GetHarvestRunColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var sql = _connectionInfo.IsPostgres
            ? "SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'harvest_runs';"
            : "SELECT name FROM pragma_table_info('harvest_runs');";
        var columns = await connection.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
    }

    private async Task EnsurePostgresHarvestRunsShapeAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var columns = await GetHarvestRunColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        var missingColumns = AdditiveColumns.Where(column => !columns.Contains(column.Name)).ToArray();
        var kindChecks = await GetPostgresHarvestRunKindChecksAsync(connection, cancellationToken).ConfigureAwait(false);
        var hasTriggerConstraint = await PostgresHarvestRunConstraintExistsAsync(connection, HarvestRunTriggerSourceConstraintName, cancellationToken).ConfigureAwait(false);
        if (missingColumns.Length == 0 && kindChecks.Count == 1 && kindChecks[0].Name == PostgresHarvestRunKindConstraintName && kindChecks[0].Definition.Contains("'update'", StringComparison.Ordinal) && hasTriggerConstraint)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var statements = new List<string> { "SET LOCAL lock_timeout = '5s'" };
        statements.AddRange(missingColumns.Select(column => $"ALTER TABLE harvest_runs ADD COLUMN IF NOT EXISTS {column.Name} {column.PostgresType} NULL"));
        // Postgres generates the inline constraint name, so only its definition identifies it.
        statements.AddRange(kindChecks.Select(check => $"ALTER TABLE harvest_runs DROP CONSTRAINT IF EXISTS {QuotePostgresIdentifier(check.Name)}"));
        // DROP IF EXISTS before ADD makes a retry idempotent.
        statements.Add($"ALTER TABLE harvest_runs DROP CONSTRAINT IF EXISTS {QuotePostgresIdentifier(PostgresHarvestRunKindConstraintName)}");
        statements.Add("ALTER TABLE harvest_runs ADD CONSTRAINT ck_harvest_runs_kind CHECK (kind IN ('bulk','url','update'))");
        statements.Add($"ALTER TABLE harvest_runs DROP CONSTRAINT IF EXISTS {QuotePostgresIdentifier(HarvestRunTriggerSourceConstraintName)}");
        statements.Add("ALTER TABLE harvest_runs ADD CONSTRAINT ck_harvest_runs_trigger_source CHECK (trigger_source IN ('manual','scheduled'))");
        // lock_timeout bounds the ACCESS EXCLUSIVE wait during a rolling deploy; failure keeps the old instance.
        command.CommandText = string.Join(";", statements) + ";";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<PostgresConstraint>> GetPostgresHarvestRunKindChecksAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT con.conname, pg_get_constraintdef(con.oid)
              FROM pg_constraint con INNER JOIN pg_class rel ON rel.oid = con.conrelid
              INNER JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
             WHERE rel.relname = 'harvest_runs' AND nsp.nspname = current_schema() AND con.contype = 'c'
               AND pg_get_constraintdef(con.oid) LIKE '%kind%' AND pg_get_constraintdef(con.oid) LIKE '%''bulk''%';
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var checks = new List<PostgresConstraint>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) checks.Add(new(reader.GetString(0), reader.GetString(1)));
        return checks;
    }

    private static async Task<bool> PostgresHarvestRunConstraintExistsAsync(DbConnection connection, string constraintName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (SELECT 1 FROM pg_constraint con INNER JOIN pg_class rel ON rel.oid = con.conrelid
            INNER JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace WHERE rel.relname = 'harvest_runs'
            AND nsp.nspname = current_schema() AND con.conname = @constraintName);
            """;
        RelationalDatabaseConnection.AddParameter(command, "@constraintName", constraintName);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static string QuotePostgresIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    private static async Task EnsureSqliteHarvestRunsConstraintsCurrentAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (await SqliteHarvestRunsConstraintsCurrentAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var oldColumnNames = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT name FROM pragma_table_info('harvest_runs');",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        var oldColumns = new HashSet<string>(oldColumnNames, StringComparer.OrdinalIgnoreCase);
        var existingIndexSql = await GetSqliteHarvestRunIndexSqlAsync(connection, cancellationToken).ConfigureAwait(false);
        var copyColumns = string.Join(", ", HarvestRunColumns.Where(oldColumns.Contains));
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var create = connection.CreateCommand())
        {
            create.Transaction = transaction;
            create.CommandText = SqliteCreateMigratedHarvestRunsTableSql;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var copy = connection.CreateCommand())
        {
            copy.Transaction = transaction;
            // Names originate only from this constant; the intersection preserves old columns.
            copy.CommandText = $"INSERT INTO harvest_runs_new ({copyColumns}) SELECT {copyColumns} FROM harvest_runs;";
            await copy.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var drop = connection.CreateCommand())
        {
            drop.Transaction = transaction;
            drop.CommandText = "DROP TABLE harvest_runs;";
            await drop.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var rename = connection.CreateCommand())
        {
            rename.Transaction = transaction;
            rename.CommandText = "ALTER TABLE harvest_runs_new RENAME TO harvest_runs;";
            await rename.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var indexSql in existingIndexSql)
        {
            await using var recreateIndex = connection.CreateCommand();
            recreateIndex.Transaction = transaction;
            recreateIndex.CommandText = indexSql;
            await recreateIndex.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsurePostgresStateConstraintAllowsInterruptedAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var constraintName = await GetPostgresHarvestRunStateConstraintNameAsync(connection, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(constraintName))
        {
            return;
        }

        var definition = await GetPostgresConstraintDefinitionAsync(connection, constraintName, cancellationToken).ConfigureAwait(false);
        if (constraintName == PostgresHarvestRunStateConstraintName &&
            definition.Contains("'Interrupted'", StringComparison.Ordinal))
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"""
            ALTER TABLE harvest_runs
            DROP CONSTRAINT IF EXISTS "{PostgresHarvestRunStateConstraintName}";
            {BuildOptionalPostgresConstraintDropSql(constraintName)}
            ALTER TABLE harvest_runs
            ADD CONSTRAINT "{PostgresHarvestRunStateConstraintName}"
            CHECK (state IN ('Queued','Running','Stopping','Succeeded','Interrupted','Failed','Cancelled'));
            """;
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string BuildOptionalPostgresConstraintDropSql(string constraintName)
        => constraintName == PostgresHarvestRunStateConstraintName
            ? string.Empty
            : $"ALTER TABLE harvest_runs DROP CONSTRAINT IF EXISTS \"{constraintName}\";";

    private static async Task<bool> SqliteHarvestRunsConstraintsCurrentAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sql
              FROM sqlite_master
             WHERE type = 'table'
               AND name = 'harvest_runs';
            """;
        var sql = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        return sql?.Contains("'Interrupted'", StringComparison.Ordinal) == true &&
            sql.Contains("'update'", StringComparison.Ordinal) &&
            sql.Contains("'scheduled'", StringComparison.Ordinal);
    }

    private static async Task<List<string>> GetSqliteHarvestRunIndexSqlAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var indexes = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sql
              FROM sqlite_master
             WHERE type = 'index'
               AND tbl_name = 'harvest_runs'
               AND sql IS NOT NULL
             ORDER BY name;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            indexes.Add(reader.GetString(0));
        }

        return indexes;
    }

    private static async Task<string?> GetPostgresHarvestRunStateConstraintNameAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT con.conname
              FROM pg_constraint con
              INNER JOIN pg_class rel ON rel.oid = con.conrelid
              INNER JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
             WHERE rel.relname = 'harvest_runs'
               AND con.contype = 'c'
               AND pg_get_constraintdef(con.oid) LIKE '%Queued%'
               AND pg_get_constraintdef(con.oid) LIKE '%Cancelled%';
            """;
        var name = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static async Task<string> GetPostgresConstraintDefinitionAsync(
        DbConnection connection,
        string constraintName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT pg_get_constraintdef(con.oid)
              FROM pg_constraint con
              INNER JOIN pg_class rel ON rel.oid = con.conrelid
              INNER JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
             WHERE rel.relname = 'harvest_runs'
               AND con.conname = @constraintName;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@constraintName", constraintName);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    // D-03 schema. Postgres uses UUID + TIMESTAMPTZ + BOOLEAN-style CHECKs.
    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS harvest_runs (
          id                       UUID PRIMARY KEY,
          kind                     TEXT NOT NULL,
          state                    TEXT NOT NULL,
          requested_utc            TIMESTAMPTZ NOT NULL DEFAULT now(),
          started_utc              TIMESTAMPTZ NULL,
          completed_utc            TIMESTAMPTZ NULL,
          duration_seconds         INT NOT NULL,
          decks_processed          INT NOT NULL DEFAULT 0,
          additional_decks_found   INT NOT NULL DEFAULT 0,
          decks_enqueued           INT NULL,
          decks_drained            INT NULL,
          error_message            TEXT NULL,
          url                      TEXT NULL,
          trigger_source           TEXT NULL,
          pages_polled             INT NULL,
          refreshes_requeued       INT NULL,
          refreshes_drained        INT NULL,
          new_ids_seen             INT NULL,
          CONSTRAINT ck_harvest_runs_kind CHECK (kind IN ('bulk','url','update')),
          CONSTRAINT ck_harvest_runs_trigger_source CHECK (trigger_source IN ('manual','scheduled')),
          CONSTRAINT ck_harvest_runs_state CHECK (state IN ('Queued','Running','Stopping','Succeeded','Interrupted','Failed','Cancelled'))
        );
        CREATE INDEX IF NOT EXISTS ix_harvest_runs_state         ON harvest_runs(state);
        CREATE INDEX IF NOT EXISTS ix_harvest_runs_started_utc   ON harvest_runs(started_utc DESC);
        """;

    // SQLite mirror — UUID -> TEXT, TIMESTAMPTZ -> TEXT, now() -> datetime('now').
    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS harvest_runs (
          id                       TEXT PRIMARY KEY,
          kind                     TEXT NOT NULL,
          state                    TEXT NOT NULL,
          requested_utc            TEXT NOT NULL DEFAULT (datetime('now')),
          started_utc              TEXT NULL,
          completed_utc            TEXT NULL,
          duration_seconds         INTEGER NOT NULL,
          decks_processed          INTEGER NOT NULL DEFAULT 0,
          additional_decks_found   INTEGER NOT NULL DEFAULT 0,
          decks_enqueued           INTEGER NULL,
          decks_drained            INTEGER NULL,
          error_message            TEXT NULL,
          url                      TEXT NULL,
          trigger_source           TEXT NULL,
          pages_polled             INTEGER NULL,
          refreshes_requeued       INTEGER NULL,
          refreshes_drained        INTEGER NULL,
          new_ids_seen             INTEGER NULL,
          CONSTRAINT ck_harvest_runs_kind CHECK (kind IN ('bulk','url','update')),
          CONSTRAINT ck_harvest_runs_trigger_source CHECK (trigger_source IN ('manual','scheduled')),
          CONSTRAINT ck_harvest_runs_state CHECK (state IN ('Queued','Running','Stopping','Succeeded','Interrupted','Failed','Cancelled'))
        );
        CREATE INDEX IF NOT EXISTS ix_harvest_runs_state         ON harvest_runs(state);
        CREATE INDEX IF NOT EXISTS ix_harvest_runs_started_utc   ON harvest_runs(started_utc DESC);
        """;

    private const string SqliteCreateMigratedHarvestRunsTableSql = """
        CREATE TABLE harvest_runs_new (
          id                       TEXT PRIMARY KEY,
          kind                     TEXT NOT NULL,
          state                    TEXT NOT NULL,
          requested_utc            TEXT NOT NULL DEFAULT (datetime('now')),
          started_utc              TEXT NULL,
          completed_utc            TEXT NULL,
          duration_seconds         INTEGER NOT NULL,
          decks_processed          INTEGER NOT NULL DEFAULT 0,
          additional_decks_found   INTEGER NOT NULL DEFAULT 0,
          decks_enqueued           INTEGER NULL,
          decks_drained            INTEGER NULL,
          error_message            TEXT NULL,
          url                      TEXT NULL,
          trigger_source           TEXT NULL,
          pages_polled             INTEGER NULL,
          refreshes_requeued       INTEGER NULL,
          refreshes_drained        INTEGER NULL,
          new_ids_seen             INTEGER NULL,
          CONSTRAINT ck_harvest_runs_kind CHECK (kind IN ('bulk','url','update')),
          CONSTRAINT ck_harvest_runs_trigger_source CHECK (trigger_source IN ('manual','scheduled')),
          CONSTRAINT ck_harvest_runs_state CHECK (state IN ('Queued','Running','Stopping','Succeeded','Interrupted','Failed','Cancelled'))
        );
        """;

    private static readonly string[] HarvestRunColumns = ["id", "kind", "state", "requested_utc", "started_utc", "completed_utc", "duration_seconds", "decks_processed", "additional_decks_found", "decks_enqueued", "decks_drained", "error_message", "url", "trigger_source", "pages_polled", "refreshes_requeued", "refreshes_drained", "new_ids_seen"];
    private static readonly AdditiveColumn[] AdditiveColumns = [new("decks_enqueued", "INTEGER", "INT"), new("decks_drained", "INTEGER", "INT"), new("trigger_source", "TEXT", "TEXT"), new("pages_polled", "INTEGER", "INT"), new("refreshes_requeued", "INTEGER", "INT"), new("refreshes_drained", "INTEGER", "INT"), new("new_ids_seen", "INTEGER", "INT")];
    private sealed record AdditiveColumn(string Name, string SqliteType, string PostgresType);
    private sealed record PostgresConstraint(string Name, string Definition);

    private const string PostgresHarvestRunKindConstraintName = "ck_harvest_runs_kind";
    private const string HarvestRunTriggerSourceConstraintName = "ck_harvest_runs_trigger_source";
    private const string PostgresHarvestRunStateConstraintName = "ck_harvest_runs_state";

    // D-02: any non-terminal row at startup is by definition orphaned (single-instance
    // Render). Reaper UPDATE is idempotent — zero rows on fresh DB or already-terminal state.
    private const string PostgresReaperSql = """
        UPDATE harvest_runs
           SET state='Failed',
               error_message='interrupted by redeploy',
               completed_utc = now()
         WHERE state IN ('Queued','Running','Stopping');
        """;

    private const string SqliteReaperSql = """
        UPDATE harvest_runs
           SET state='Failed',
               error_message='interrupted by redeploy',
               completed_utc = datetime('now')
         WHERE state IN ('Queued','Running','Stopping');
        """;

    /// <summary>Maps persisted harvest run columns before converting database values into the domain row.</summary>
    private sealed class HarvestRunRowData
    {
        public Guid Id { get; init; }
        public required string Kind { get; init; }
        public required string State { get; init; }
        public DateTimeOffset RequestedUtc { get; init; }
        public DateTimeOffset? StartedUtc { get; init; }
        public DateTimeOffset? CompletedUtc { get; init; }
        public int DurationSeconds { get; init; }
        public int DecksProcessed { get; init; }
        public int AdditionalDecksFound { get; init; }
        public int? DecksEnqueued { get; init; }
        public int? DecksDrained { get; init; }
        public string? ErrorMessage { get; init; }
        public string? Url { get; init; }
        public string? TriggerSource { get; init; }
        public int? PagesPolled { get; init; }
        public int? RefreshesRequeued { get; init; }
        public int? RefreshesDrained { get; init; }
        public int? NewIdsSeen { get; init; }
    }

    /// <summary>Supplies run timestamps and count for the admin harvest history revision token.</summary>
    private sealed record HarvestRunRevisionRow(
        DateTimeOffset? StartedUtc,
        DateTimeOffset? CompletedUtc,
        long Count);
}
