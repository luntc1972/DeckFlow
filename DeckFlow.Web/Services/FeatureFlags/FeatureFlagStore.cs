using System.Data.Common;
using Dapper;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>
/// Default implementation of <see cref="IFeatureFlagStore"/> backed by
/// <see cref="RelationalDatabaseConnection"/> (Postgres in production, SQLite in tests
/// and local-dev). Schema is lazy-initialized on first call via a SemaphoreSlim gate,
/// mirroring AdminBruteForceTrackerStore. Seed list (Phase 6 D-09 + Phase 7 B3 + Phase 7.1
/// CATFLAG-01 + Phase 66 TOGGLE-01/06) inserts default-on rows for 'scryfall.tagger.enabled',
/// 'page.help.enabled', 'harvest.cron.enabled', and the public-tool visibility flags using
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
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        var rows = await connection.QueryAsync<FeatureFlagRow>(new CommandDefinition(
            "SELECT key, enabled FROM feature_flags",
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        foreach (var row in rows)
        {
            result[row.Key] = row.Enabled;
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
        await connection.ExecuteAsync(new CommandDefinition(
            _connectionInfo.IsPostgres ? PostgresUpsertSql : SqliteUpsertSql,
            new { key, enabled, now },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
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
          ('feature.categories.enabled', TRUE),
          ('content.kb.enabled', TRUE),
          ('feature.manabase.enabled', TRUE),
          ('tool.deck-analysis.enabled', TRUE),
          ('tool.deck-comparison.enabled', TRUE),
          ('tool.cedh-meta-gap.enabled', TRUE),
          ('tool.deck-sync.enabled', TRUE),
          ('tool.convert.enabled', TRUE),
          ('tool.deck-primer.enabled', TRUE),
          ('tool.card-lookup.enabled', TRUE),
          ('tool.mechanic-lookup.enabled', TRUE),
          ('tool.judge-questions.enabled', TRUE),
          ('tool.commander-categories.enabled', TRUE),
          ('analysis.reference.full-oracle-text', TRUE),
          ('analysis.reference.deck-stats', FALSE),
          ('manabase.source-mana-quantity', TRUE),
          ('manabase.ramp-credit-v2', TRUE),
          ('manabase.color-aware-mulligan', TRUE),
          ('manabase.land-ramp-sim', TRUE),
          ('manabase.health-band-castability', FALSE),
          ('manabase.health-band-headline-floor', TRUE),
          ('manabase.plain-language-verdict', FALSE)
        ON CONFLICT (key) DO NOTHING;
        """;

    private const string SqliteSeedSql = """
        INSERT INTO feature_flags (key, enabled) VALUES
          ('scryfall.tagger.enabled', 1),
          ('page.help.enabled', 1),
          ('harvest.cron.enabled', 1),
          ('feature.categories.enabled', 1),
          ('content.kb.enabled', 1),
          ('feature.manabase.enabled', 1),
          ('tool.deck-analysis.enabled', 1),
          ('tool.deck-comparison.enabled', 1),
          ('tool.cedh-meta-gap.enabled', 1),
          ('tool.deck-sync.enabled', 1),
          ('tool.convert.enabled', 1),
          ('tool.deck-primer.enabled', 1),
          ('tool.card-lookup.enabled', 1),
          ('tool.mechanic-lookup.enabled', 1),
          ('tool.judge-questions.enabled', 1),
          ('tool.commander-categories.enabled', 1),
          ('analysis.reference.full-oracle-text', 1),
          ('analysis.reference.deck-stats', 0),
          ('manabase.source-mana-quantity', 1),
          ('manabase.ramp-credit-v2', 1),
          ('manabase.color-aware-mulligan', 1),
          ('manabase.land-ramp-sim', 1),
          ('manabase.health-band-castability', 0),
          ('manabase.health-band-headline-floor', 1),
          ('manabase.plain-language-verdict', 0)
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

    private sealed class FeatureFlagRow
    {
        public required string Key { get; set; }

        public required bool Enabled { get; set; }
    }
}
