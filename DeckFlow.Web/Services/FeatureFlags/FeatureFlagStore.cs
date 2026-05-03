using System.Data.Common;
using System.Globalization;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>
/// Default implementation of <see cref="IFeatureFlagStore"/> backed by
/// <see cref="RelationalDatabaseConnection"/> (Postgres in production, SQLite in tests
/// and local-dev). Schema is lazy-initialized on first call via a SemaphoreSlim gate,
/// mirroring AdminBruteForceTrackerStore. Seed list (Phase 6 D-09 + Phase 7 B3 + Phase 7.1
/// CATFLAG-01) inserts default-on rows for 'scryfall.tagger.enabled', 'page.help.enabled',
/// 'harvest.cron.enabled', and 'feature.categories.enabled' using
/// ON CONFLICT (key) DO NOTHING so re-bootstrapping on an existing DB never overwrites
/// operator changes (FLAG-01).
/// </summary>
public sealed class FeatureFlagStore : IFeatureFlagStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
    /// Mirrors AdminBruteForceTrackerStore's test-seam ctor for in-memory / temp-file
    /// SQLite tests.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file (created if missing).</param>
    public FeatureFlagStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a store using the supplied <see cref="RelationalDatabaseConnection"/>
    /// directly. Used by tests that want to inject a Postgres-or-SQLite connection
    /// without going through the DI factory.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    public FeatureFlagStore(RelationalDatabaseConnection connectionInfo)
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
    /// <see cref="DeckFlowDatabaseConnectionFactory.CreateFeatureFlagConnection"/>,
    /// which shares the feedback DB (D-07).
    /// </summary>
    /// <param name="environment">Web host environment used by the connection factory.</param>
    public FeatureFlagStore(IWebHostEnvironment environment)
        : this(DeckFlowDatabaseConnectionFactory.CreateFeatureFlagConnection(environment)) { }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, bool>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT key, enabled FROM feature_flags";

        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = reader.GetString(0);
            var enabled = ReadBool(reader, 1);
            result[key] = enabled;
        }
        return result;
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(string key, bool enabled, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = _connectionInfo.IsPostgres ? PostgresUpsertSql : SqliteUpsertSql;
        RelationalDatabaseConnection.AddParameter(command, "@key", key);
        RelationalDatabaseConnection.AddParameter(
            command, "@enabled",
            _connectionInfo.IsPostgres ? (object)enabled : (enabled ? 1 : 0));
        RelationalDatabaseConnection.AddParameter(
            command, "@now",
            _connectionInfo.IsPostgres
                ? (object)now.UtcDateTime
                : now.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

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

    private static bool ReadBool(DbDataReader reader, int ordinal)
    {
        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            bool b => b,
            long l => l != 0,
            int i => i != 0,
            short s => s != 0,
            string str => str == "1" || string.Equals(str, "true", StringComparison.OrdinalIgnoreCase),
            _ => Convert.ToBoolean(raw, CultureInfo.InvariantCulture)
        };
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = _connectionInfo.CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    // D-07 schema. Postgres uses BOOLEAN + TIMESTAMPTZ with now() default;
    // SQLite uses INTEGER (0/1) + TEXT with datetime('now') default.
    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS feature_flags (
          key        TEXT PRIMARY KEY,
          enabled    BOOLEAN NOT NULL DEFAULT TRUE,
          updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS feature_flags (
          key        TEXT PRIMARY KEY,
          enabled    INTEGER NOT NULL DEFAULT 1,
          updated_at TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;

    // D-09 seed. ON CONFLICT (key) DO NOTHING preserves operator-set values on
    // re-bootstrap so toggles survive app restarts (FLAG-01 default-on contract).
    private const string PostgresSeedSql = """
        INSERT INTO feature_flags (key, enabled) VALUES
          ('scryfall.tagger.enabled', TRUE),
          ('page.help.enabled', TRUE),
          ('harvest.cron.enabled', TRUE),
          ('feature.categories.enabled', TRUE)
        ON CONFLICT (key) DO NOTHING;
        """;

    private const string SqliteSeedSql = """
        INSERT INTO feature_flags (key, enabled) VALUES
          ('scryfall.tagger.enabled', 1),
          ('page.help.enabled', 1),
          ('harvest.cron.enabled', 1),
          ('feature.categories.enabled', 1)
        ON CONFLICT (key) DO NOTHING;
        """;

    // EXCLUDED works on both Postgres and SQLite; preferred over table-qualified
    // columns per memory feedback_sqlite_postgres_sql_divergence.md.
    private const string PostgresUpsertSql = """
        INSERT INTO feature_flags (key, enabled, updated_at)
        VALUES (@key, @enabled, @now)
        ON CONFLICT (key) DO UPDATE SET
          enabled    = EXCLUDED.enabled,
          updated_at = EXCLUDED.updated_at;
        """;

    private const string SqliteUpsertSql = """
        INSERT INTO feature_flags (key, enabled, updated_at)
        VALUES (@key, @enabled, @now)
        ON CONFLICT (key) DO UPDATE SET
          enabled    = excluded.enabled,
          updated_at = excluded.updated_at;
        """;
}
