using System.Data.Common;
using System.Globalization;
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

            await using (var create = connection.CreateCommand())
            {
                create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await EnsureStateConstraintAllowsInterruptedAsync(connection, cancellationToken).ConfigureAwait(false);

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
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var id = Guid.NewGuid();
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO harvest_runs (id, kind, state, requested_utc, duration_seconds, url)
            VALUES (@id, @kind, 'Queued', @now, @duration, @url);
            """;

        // SQLite stores Guid as TEXT; Npgsql accepts Guid directly.
        RelationalDatabaseConnection.AddParameter(
            command, "@id",
            _connectionInfo.IsPostgres ? (object)id : id.ToString());
        RelationalDatabaseConnection.AddParameter(
            command, "@kind",
            kind == HarvestRunKind.Bulk ? "bulk" : "url");
        RelationalDatabaseConnection.AddParameter(
            command, "@now",
            _connectionInfo.IsPostgres
                ? (object)now.UtcDateTime
                : now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        RelationalDatabaseConnection.AddParameter(command, "@duration", durationSeconds);
        RelationalDatabaseConnection.AddParameter(command, "@url", (object?)url ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

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
        int decksProcessed,
        int additionalDecksFound,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE harvest_runs
               SET state = @state,
                   started_utc = COALESCE(@startedUtc, started_utc),
                   completed_utc = COALESCE(@completedUtc, completed_utc),
                   decks_processed = @decksProcessed,
                   additional_decks_found = @additionalDecksFound,
                   error_message = @errorMessage
             WHERE id = @id;
            """;

        RelationalDatabaseConnection.AddParameter(
            command, "@id",
            _connectionInfo.IsPostgres ? (object)id : id.ToString());
        RelationalDatabaseConnection.AddParameter(command, "@state", state.ToString());
        RelationalDatabaseConnection.AddParameter(
            command, "@startedUtc", BindNullableTimestamp(startedUtc));
        RelationalDatabaseConnection.AddParameter(
            command, "@completedUtc", BindNullableTimestamp(completedUtc));
        RelationalDatabaseConnection.AddParameter(command, "@decksProcessed", decksProcessed);
        RelationalDatabaseConnection.AddParameter(command, "@additionalDecksFound", additionalDecksFound);
        RelationalDatabaseConnection.AddParameter(command, "@errorMessage", (object?)errorMessage ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        // D-13: explicit invalidation on every state change.
        InvalidateStats();
    }

    /// <inheritdoc />
    public async Task UpdateProgressAsync(
        Guid id,
        int decksProcessed,
        int additionalDecksFound,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE harvest_runs
               SET decks_processed = @decksProcessed,
                   additional_decks_found = @additionalDecksFound
             WHERE id = @id;
            """;

        RelationalDatabaseConnection.AddParameter(
            command, "@id",
            _connectionInfo.IsPostgres ? (object)id : id.ToString());
        RelationalDatabaseConnection.AddParameter(command, "@decksProcessed", decksProcessed);
        RelationalDatabaseConnection.AddParameter(command, "@additionalDecksFound", additionalDecksFound);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        InvalidateStats();
    }

    /// <inheritdoc />
    public async Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, kind, state, requested_utc, started_utc, completed_utc,
                   duration_seconds, decks_processed, additional_decks_found, error_message, url
              FROM harvest_runs
             WHERE state IN ('Queued','Running','Stopping')
             ORDER BY requested_utc DESC
             LIMIT 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }
        return ReadHarvestRunRow(reader);
    }

    /// <inheritdoc />
    public async Task<HarvestRunRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, kind, state, requested_utc, started_utc, completed_utc,
                   duration_seconds, decks_processed, additional_decks_found, error_message, url
              FROM harvest_runs
             WHERE id = @id
             LIMIT 1;
            """;

        // SQLite stores Guid as TEXT; Npgsql accepts Guid directly. Mirrors the
        // bind pattern at InsertQueuedAsync (line 117-119), UpdateStateAsync
        // (line 164-166), and UpdateProgressAsync (line 200-202).
        RelationalDatabaseConnection.AddParameter(
            command, "@id",
            _connectionInfo.IsPostgres ? (object)id : id.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }
        return ReadHarvestRunRow(reader);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HarvestRunRow>> GetRecentAsync(int n, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        // NULLS LAST works on Postgres natively; SQLite >= 3.30 (shipped with Microsoft.Data.Sqlite 10) supports it too.
        command.CommandText = """
            SELECT id, kind, state, requested_utc, started_utc, completed_utc,
                   duration_seconds, decks_processed, additional_decks_found, error_message, url
              FROM harvest_runs
             ORDER BY started_utc DESC NULLS LAST
             LIMIT @n;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@n", n);

        var rows = new List<HarvestRunRow>(capacity: n);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadHarvestRunRow(reader));
        }
        return rows;
    }

    /// <inheritdoc />
    public async Task<string> GetRecentRevisionAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(started_utc), MAX(completed_utc), COUNT(1) FROM harvest_runs;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return "||0";
        }

        var startedTicks = reader.IsDBNull(0)
            ? string.Empty
            : ReadTimestamp(reader, 0).ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
        var completedTicks = reader.IsDBNull(1)
            ? string.Empty
            : ReadTimestamp(reader, 1).ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
        var countRaw = reader.GetValue(2);
        var count = countRaw switch
        {
            long value => value,
            int value => value,
            _ => Convert.ToInt64(countRaw, CultureInfo.InvariantCulture)
        };

        return $"{startedTicks}|{completedTicks}|{count.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastSuccessUtcAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MAX(completed_utc) FROM harvest_runs WHERE state='Succeeded';";

        var raw = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (raw is null || raw is DBNull)
        {
            return null;
        }
        return ReadTimestampValue(raw);
    }

    /// <inheritdoc />
    public async Task<long> GetTotalSucceededCountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(1) FROM harvest_runs WHERE state='Succeeded';";

        var raw = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return raw switch
        {
            null => 0L,
            DBNull => 0L,
            long l => l,
            int i => i,
            _ => Convert.ToInt64(raw, CultureInfo.InvariantCulture)
        };
    }

    private object BindNullableTimestamp(DateTimeOffset? value)
    {
        if (value is null) return DBNull.Value;
        return _connectionInfo.IsPostgres
            ? (object)value.Value.UtcDateTime
            : value.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
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

    private HarvestRunRow ReadHarvestRunRow(DbDataReader reader)
    {
        var idRaw = reader.GetValue(0);
        var id = idRaw switch
        {
            Guid g => g,
            string s => Guid.Parse(s),
            _ => Guid.Parse(reader.GetString(0))
        };
        var kind = ParseHarvestKind(reader.GetString(1));
        var state = Enum.Parse<HarvestRunState>(reader.GetString(2), ignoreCase: false);
        var requestedUtc = ReadTimestamp(reader, 3);
        var startedUtc = reader.IsDBNull(4) ? (DateTimeOffset?)null : ReadTimestamp(reader, 4);
        var completedUtc = reader.IsDBNull(5) ? (DateTimeOffset?)null : ReadTimestamp(reader, 5);
        var durationSeconds = reader.GetInt32(6);
        var decksProcessed = reader.GetInt32(7);
        var additionalDecksFound = reader.GetInt32(8);
        var errorMessage = reader.IsDBNull(9) ? null : reader.GetString(9);
        var url = reader.IsDBNull(10) ? null : reader.GetString(10);

        return new HarvestRunRow(
            id, kind, state, requestedUtc, startedUtc, completedUtc,
            durationSeconds, decksProcessed, additionalDecksFound, errorMessage, url);
    }

    private static HarvestRunKind ParseHarvestKind(string raw) => raw switch
    {
        "bulk" => HarvestRunKind.Bulk,
        "url" => HarvestRunKind.Url,
        _ => throw new InvalidOperationException($"Unknown harvest_runs.kind value '{raw}'.")
    };

    private static DateTimeOffset ReadTimestamp(DbDataReader reader, int ordinal)
    {
        var raw = reader.GetValue(ordinal);
        return ReadTimestampValue(raw);
    }

    private static DateTimeOffset ReadTimestampValue(object raw)
    {
        return raw switch
        {
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
            DateTimeOffset dto => dto.ToUniversalTime(),
            string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime(),
            _ => new DateTimeOffset(Convert.ToDateTime(raw, CultureInfo.InvariantCulture), TimeSpan.Zero)
        };
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionInfo.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private async Task EnsureStateConstraintAllowsInterruptedAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (_connectionInfo.IsSqlite)
        {
            await EnsureSqliteStateConstraintAllowsInterruptedAsync(connection, cancellationToken).ConfigureAwait(false);
            return;
        }

        await EnsurePostgresStateConstraintAllowsInterruptedAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSqliteStateConstraintAllowsInterruptedAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (await SqliteStateConstraintAllowsInterruptedAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var existingIndexSql = await GetSqliteHarvestRunIndexSqlAsync(connection, cancellationToken).ConfigureAwait(false);
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
            copy.CommandText = """
                INSERT INTO harvest_runs_new (
                    id, kind, state, requested_utc, started_utc, completed_utc,
                    duration_seconds, decks_processed, additional_decks_found, error_message, url)
                SELECT
                    id, kind, state, requested_utc, started_utc, completed_utc,
                    duration_seconds, decks_processed, additional_decks_found, error_message, url
                  FROM harvest_runs;
                """;
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

    private static async Task<bool> SqliteStateConstraintAllowsInterruptedAsync(
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
        return sql?.Contains("'Interrupted'", StringComparison.Ordinal) == true;
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
          kind                     TEXT NOT NULL CHECK (kind IN ('bulk','url')),
          state                    TEXT NOT NULL,
          requested_utc            TIMESTAMPTZ NOT NULL DEFAULT now(),
          started_utc              TIMESTAMPTZ NULL,
          completed_utc            TIMESTAMPTZ NULL,
          duration_seconds         INT NOT NULL,
          decks_processed          INT NOT NULL DEFAULT 0,
          additional_decks_found   INT NOT NULL DEFAULT 0,
          error_message            TEXT NULL,
          url                      TEXT NULL,
          CONSTRAINT ck_harvest_runs_state CHECK (state IN ('Queued','Running','Stopping','Succeeded','Interrupted','Failed','Cancelled'))
        );
        CREATE INDEX IF NOT EXISTS ix_harvest_runs_state         ON harvest_runs(state);
        CREATE INDEX IF NOT EXISTS ix_harvest_runs_started_utc   ON harvest_runs(started_utc DESC);
        """;

    // SQLite mirror — UUID -> TEXT, TIMESTAMPTZ -> TEXT, now() -> datetime('now').
    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS harvest_runs (
          id                       TEXT PRIMARY KEY,
          kind                     TEXT NOT NULL CHECK (kind IN ('bulk','url')),
          state                    TEXT NOT NULL,
          requested_utc            TEXT NOT NULL DEFAULT (datetime('now')),
          started_utc              TEXT NULL,
          completed_utc            TEXT NULL,
          duration_seconds         INTEGER NOT NULL,
          decks_processed          INTEGER NOT NULL DEFAULT 0,
          additional_decks_found   INTEGER NOT NULL DEFAULT 0,
          error_message            TEXT NULL,
          url                      TEXT NULL,
          CONSTRAINT ck_harvest_runs_state CHECK (state IN ('Queued','Running','Stopping','Succeeded','Interrupted','Failed','Cancelled'))
        );
        CREATE INDEX IF NOT EXISTS ix_harvest_runs_state         ON harvest_runs(state);
        CREATE INDEX IF NOT EXISTS ix_harvest_runs_started_utc   ON harvest_runs(started_utc DESC);
        """;

    private const string SqliteCreateMigratedHarvestRunsTableSql = """
        CREATE TABLE harvest_runs_new (
          id                       TEXT PRIMARY KEY,
          kind                     TEXT NOT NULL CHECK (kind IN ('bulk','url')),
          state                    TEXT NOT NULL,
          requested_utc            TEXT NOT NULL DEFAULT (datetime('now')),
          started_utc              TEXT NULL,
          completed_utc            TEXT NULL,
          duration_seconds         INTEGER NOT NULL,
          decks_processed          INTEGER NOT NULL DEFAULT 0,
          additional_decks_found   INTEGER NOT NULL DEFAULT 0,
          error_message            TEXT NULL,
          url                      TEXT NULL,
          CONSTRAINT ck_harvest_runs_state CHECK (state IN ('Queued','Running','Stopping','Succeeded','Interrupted','Failed','Cancelled'))
        );
        """;

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
}
