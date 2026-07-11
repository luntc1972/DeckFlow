using System.Data.Common;
using Dapper;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default implementation of <see cref="ICreatorStyleProfileStore"/> for creator style profiles.
/// </summary>
public sealed class CreatorStyleProfileStore : ICreatorStyleProfileStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly bool _ensureSchemaEnabled;
    private readonly Func<CancellationToken, Task<DbConnection>>? _connectionFactoryOverride;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed creator style-profile store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public CreatorStyleProfileStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a creator style-profile store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="ensureSchemaEnabled">
    /// When <c>true</c> (default) the store auto-creates its schema on first use. When
    /// <c>false</c> <see cref="EnsureSchemaAsync"/> is a no-op so the store never issues CREATE/ALTER/DROP.
    /// </param>
    public CreatorStyleProfileStore(RelationalDatabaseConnection connectionInfo, bool ensureSchemaEnabled = true)
        : this(connectionInfo, ensureSchemaEnabled, connectionFactoryOverride: null) { }

    /// <summary>
    /// Test-seam constructor: injects a connection-factory override so tests can wrap the real
    /// connection with a recording double and assert the exact SQL issued.
    /// The public constructors pass <c>null</c> and behave exactly as production.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="ensureSchemaEnabled">Whether schema auto-ensure runs.</param>
    /// <param name="connectionFactoryOverride">
    /// Optional connection factory used by <see cref="OpenConnectionAsync"/> in place of the live one.
    /// </param>
    internal CreatorStyleProfileStore(
        RelationalDatabaseConnection connectionInfo,
        bool ensureSchemaEnabled,
        Func<CancellationToken, Task<DbConnection>>? connectionFactoryOverride)
    {
        ArgumentNullException.ThrowIfNull(connectionInfo);
        _connectionInfo = connectionInfo;
        _ensureSchemaEnabled = ensureSchemaEnabled;
        _connectionFactoryOverride = connectionFactoryOverride;
        if (_connectionInfo.IsSqlite)
        {
            var directory = Path.GetDirectoryName(_connectionInfo.ExtractSqlitePath());
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }

    /// <inheritdoc />
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (!_ensureSchemaEnabled) return;
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var create = connection.CreateCommand();
            create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpsertAsync(CreatorStyleProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Slug);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var parameters = new DynamicParameters();
        parameters.Add("slug", profile.Slug);
        parameters.Add("platform", profile.Platform);
        parameters.Add("minDecks", profile.MinDecks);
        parameters.Add("insufficientSample", profile.InsufficientSample);
        parameters.Add("statedRulesJson", CreatorStyleProfileSections.SerializeSection(profile.StatedRules));
        parameters.Add("measuredMetricsJson", CreatorStyleProfileSections.SerializeSection(profile.MeasuredMetrics));
        parameters.Add("fusedTargetsJson", CreatorStyleProfileSections.SerializeSection(profile.FusedTargets));
        parameters.Add("updatedUtc", profile.UpdatedUtc);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CreatorStyleProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<CreatorStyleProfileReadModel>(new CommandDefinition(
            $"""
            SELECT {CreatorStyleProfileReadColumns.SelectList}
              FROM creator_style_profile
             WHERE slug = @slug;
            """,
            new { slug },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : CreatorStyleProfileMapper.ToProfile(row);
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connectionFactoryOverride is not null)
        {
            return await _connectionFactoryOverride(cancellationToken).ConfigureAwait(false);
        }

        return await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    }

    private const string UpsertSql = """
        INSERT INTO creator_style_profile (
            slug,
            platform,
            min_decks,
            insufficient_sample,
            stated_rules_json,
            measured_metrics_json,
            fused_targets_json,
            updated_utc)
        VALUES (
            @slug,
            @platform,
            @minDecks,
            @insufficientSample,
            @statedRulesJson,
            @measuredMetricsJson,
            @fusedTargetsJson,
            @updatedUtc)
        ON CONFLICT (slug) DO UPDATE
        SET platform = EXCLUDED.platform,
            min_decks = EXCLUDED.min_decks,
            insufficient_sample = EXCLUDED.insufficient_sample,
            stated_rules_json = EXCLUDED.stated_rules_json,
            measured_metrics_json = EXCLUDED.measured_metrics_json,
            fused_targets_json = EXCLUDED.fused_targets_json,
            updated_utc = EXCLUDED.updated_utc;
        """;

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS creator_style_profile (
            slug TEXT PRIMARY KEY,
            platform TEXT NOT NULL,
            min_decks INTEGER NOT NULL,
            insufficient_sample BOOLEAN NOT NULL DEFAULT FALSE,
            stated_rules_json TEXT NULL,
            measured_metrics_json TEXT NULL,
            fused_targets_json TEXT NULL,
            updated_utc TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS creator_style_profile (
            slug TEXT PRIMARY KEY,
            platform TEXT NOT NULL,
            min_decks INTEGER NOT NULL,
            insufficient_sample INTEGER NOT NULL DEFAULT 0,
            stated_rules_json TEXT NULL,
            measured_metrics_json TEXT NULL,
            fused_targets_json TEXT NULL,
            updated_utc TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;
}
