using System.Data.Common;
using Dapper;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services.Harvest;

/// <summary>
/// Default implementation of <see cref="IHarvestScheduleStore"/> backed by
/// <see cref="RelationalDatabaseConnection"/>. Stores a single row (id=1) holding the
/// scheduler interval (NULL = Off, 2/4/8/24 hours) plus the paused flag (D-06).
/// Schema is lazy-initialized via a SemaphoreSlim gate; the seed row is inserted via
/// <c>ON CONFLICT (id) DO NOTHING</c> so re-bootstrapping never overwrites operator
/// changes. Mirrors <see cref="DeckFlow.Web.Services.FeatureFlags.FeatureFlagStore"/>.
/// </summary>
public sealed class HarvestScheduleStore : IHarvestScheduleStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file (created if missing).</param>
    public HarvestScheduleStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a store using the supplied <see cref="RelationalDatabaseConnection"/>
    /// directly. Used by tests that want to inject a Postgres-or-SQLite connection
    /// without going through the DI factory.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    public HarvestScheduleStore(RelationalDatabaseConnection connectionInfo)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
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
    /// <see cref="DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection"/>
    /// (D-07 — same DB file as feature_flags / harvest_runs).
    /// </summary>
    /// <param name="environment">Web host environment used by the connection factory.</param>
    public HarvestScheduleStore(IWebHostEnvironment environment)
        : this(DeckFlowDatabaseConnectionFactory.CreateHarvestStateConnection(environment)) { }

    /// <inheritdoc />
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            // Why: schema creation is an intentional raw ADO.NET carve-out for this phase.
            await using (var create = connection.CreateCommand())
            {
                create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
                await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (var seed = connection.CreateCommand())
            {
                seed.CommandText = _connectionInfo.IsPostgres ? PostgresSeedSql : SqliteSeedSql;
                await seed.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<HarvestScheduleSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<HarvestScheduleRow>(new CommandDefinition(
            "SELECT interval_hours, paused, updated_utc FROM harvest_schedule WHERE id = 1;",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (row is null)
        {
            // Defensive — schema seed should always create the row.
            throw new InvalidOperationException("harvest_schedule seed row (id=1) is missing.");
        }

        return new HarvestScheduleSnapshot(row.IntervalHours, row.Paused, row.UpdatedUtc);
    }

    /// <inheritdoc />
    public async Task SaveAsync(int? intervalHours, bool paused, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            _connectionInfo.IsPostgres ? PostgresUpsertSql : SqliteUpsertSql,
            new { interval = intervalHours, paused, now },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionInfo.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    // Single-row schema — id=1 PK + CHECK so a malformed UPSERT can't create id=2.
    // interval_hours CHECK whitelists the four allowed cron intervals (2,4,8,24).
    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS harvest_schedule (
          id              INT PRIMARY KEY CHECK (id = 1),
          interval_hours  INT NULL CHECK (interval_hours IS NULL OR interval_hours IN (2,4,8,24)),
          paused          BOOLEAN NOT NULL DEFAULT FALSE,
          updated_utc     TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS harvest_schedule (
          id              INTEGER PRIMARY KEY CHECK (id = 1),
          interval_hours  INTEGER NULL CHECK (interval_hours IS NULL OR interval_hours IN (2,4,8,24)),
          paused          INTEGER NOT NULL DEFAULT 0,
          updated_utc     TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;

    // Seed default-Off row. ON CONFLICT (id) DO NOTHING preserves operator-saved
    // values across re-bootstraps (parallel to FeatureFlagStore D-09 idiom).
    private const string PostgresSeedSql = """
        INSERT INTO harvest_schedule (id, interval_hours, paused, updated_utc)
        VALUES (1, NULL, FALSE, now())
        ON CONFLICT (id) DO NOTHING;
        """;

    private const string SqliteSeedSql = """
        INSERT INTO harvest_schedule (id, interval_hours, paused, updated_utc)
        VALUES (1, NULL, 0, datetime('now'))
        ON CONFLICT (id) DO NOTHING;
        """;

    // EXCLUDED-form UPSERT — works on both Postgres and SQLite ≥ 3.24.
    private const string PostgresUpsertSql = """
        INSERT INTO harvest_schedule (id, interval_hours, paused, updated_utc)
        VALUES (1, @interval, @paused, @now)
        ON CONFLICT (id) DO UPDATE SET
          interval_hours = EXCLUDED.interval_hours,
          paused         = EXCLUDED.paused,
          updated_utc    = EXCLUDED.updated_utc;
        """;

    private const string SqliteUpsertSql = """
        INSERT INTO harvest_schedule (id, interval_hours, paused, updated_utc)
        VALUES (1, @interval, @paused, @now)
        ON CONFLICT (id) DO UPDATE SET
          interval_hours = excluded.interval_hours,
          paused         = excluded.paused,
          updated_utc    = excluded.updated_utc;
        """;

    private sealed class HarvestScheduleRow
    {
        public int? IntervalHours { get; set; }

        public required bool Paused { get; set; }

        public required DateTimeOffset UpdatedUtc { get; set; }
    }
}
