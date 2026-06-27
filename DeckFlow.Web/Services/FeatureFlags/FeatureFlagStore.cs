using System.Data.Common;
using Dapper;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>
/// Default implementation of <see cref="IFeatureFlagStore"/> backed by
/// <see cref="RelationalDatabaseConnection"/> (Postgres in production, SQLite in tests
/// and local-dev). Schema is lazy-initialized on first call via a SemaphoreSlim gate,
/// mirroring AdminBruteForceTrackerStore. Seed list (Phase 6 D-09 + Phase 7 B3 + Phase 7.1
/// CATFLAG-01 + Phase 66 TOGGLE-01/06) inserts default-on rows for
/// 'service.scryfall-tagger.enabled', 'tool.help.enabled',
/// 'service.harvest-cron.enabled', the public-tool visibility flags, and the
/// analysis tuning flags. Before seeding, an idempotent rename migration carries
/// legacy rows forward to the new key names so re-bootstrapping on an existing DB
/// never overwrites operator changes (FLAG-01).
/// </summary>
public sealed class FeatureFlagStore : IFeatureFlagStore
{
    private static readonly (string OldKey, string NewKey)[] RenamedFlagKeys =
    [
        (Key("feature", "categories", "enabled"), "tool.categories.enabled"),
        (Key("feature", "manabase", "enabled"), "tool.manabase.enabled"),
        (Key("content", "kb", "enabled"), "tool.knowledge-base.enabled"),
        (Key("page", "help", "enabled"), "tool.help.enabled"),
        (Key("scryfall", "tagger", "enabled"), "service.scryfall-tagger.enabled"),
        (Key("harvest", "cron", "enabled"), "service.harvest-cron.enabled"),
        (Key("manabase", "source-mana-quantity"), "analysis.manabase.source-mana-quantity"),
        (Key("manabase", "ramp-credit-v2"), "analysis.manabase.ramp-credit-v2"),
        (Key("manabase", "color-aware-mulligan"), "analysis.manabase.color-aware-mulligan"),
        (Key("manabase", "land-ramp-sim"), "analysis.manabase.land-ramp-sim"),
        (Key("manabase", "health-band-castability"), "analysis.manabase.health-band-castability"),
        (Key("manabase", "health-band-headline-floor"), "analysis.manabase.health-band-headline-floor"),
        (Key("manabase", "plain-language-verdict"), "analysis.manabase.plain-language-verdict"),
        (Key("manabase", "commander-castability"), "analysis.manabase.commander-castability"),
    ];

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

            var now = DateTimeOffset.UtcNow;
            foreach (var (oldKey, newKey) in RenamedFlagKeys)
            {
                var parameters = new DynamicParameters();
                parameters.Add("old", oldKey);
                parameters.Add("new", newKey);
                parameters.Add("now", now);

                await connection.ExecuteAsync(new CommandDefinition(
                    RenameFlagSql,
                    parameters,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
                await connection.ExecuteAsync(new CommandDefinition(
                    DeleteLegacyFlagSql,
                    parameters,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
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

    private const string RenameFlagSql = """
        UPDATE feature_flags SET key = @new, updated_at = @now
         WHERE key = @old AND NOT EXISTS (SELECT 1 FROM feature_flags WHERE key = @new);
        """;

    private const string DeleteLegacyFlagSql = """
        DELETE FROM feature_flags
         WHERE key = @old AND EXISTS (SELECT 1 FROM feature_flags WHERE key = @new);
        """;

    // D-09 seed. ON CONFLICT (key) DO NOTHING preserves operator-set values on
    // re-bootstrap so toggles survive app restarts (FLAG-01 default-on contract).
    private const string PostgresSeedSql = """
        INSERT INTO feature_flags (key, enabled) VALUES
          ('service.scryfall-tagger.enabled', TRUE),
          ('tool.help.enabled', TRUE),
          ('service.harvest-cron.enabled', TRUE),
          ('tool.categories.enabled', TRUE),
          ('tool.knowledge-base.enabled', TRUE),
          ('tool.manabase.enabled', TRUE),
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
          ('analysis.manabase.source-mana-quantity', TRUE),
          ('analysis.manabase.ramp-credit-v2', TRUE),
          ('analysis.manabase.color-aware-mulligan', TRUE),
          ('analysis.manabase.land-ramp-sim', TRUE),
          ('analysis.manabase.health-band-castability', FALSE),
          ('analysis.manabase.health-band-headline-floor', TRUE),
          ('analysis.manabase.plain-language-verdict', FALSE),
          ('analysis.manabase.commander-castability', FALSE),
          ('analysis.command-zone-awareness', FALSE)
        ON CONFLICT (key) DO NOTHING;
        """;

    private const string SqliteSeedSql = """
        INSERT INTO feature_flags (key, enabled) VALUES
          ('service.scryfall-tagger.enabled', 1),
          ('tool.help.enabled', 1),
          ('service.harvest-cron.enabled', 1),
          ('tool.categories.enabled', 1),
          ('tool.knowledge-base.enabled', 1),
          ('tool.manabase.enabled', 1),
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
          ('analysis.manabase.source-mana-quantity', 1),
          ('analysis.manabase.ramp-credit-v2', 1),
          ('analysis.manabase.color-aware-mulligan', 1),
          ('analysis.manabase.land-ramp-sim', 1),
          ('analysis.manabase.health-band-castability', 0),
          ('analysis.manabase.health-band-headline-floor', 1),
          ('analysis.manabase.plain-language-verdict', 0),
          ('analysis.manabase.commander-castability', 0),
          ('analysis.command-zone-awareness', 0)
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

    private static string Key(params string[] parts) => string.Join('.', parts);
}
