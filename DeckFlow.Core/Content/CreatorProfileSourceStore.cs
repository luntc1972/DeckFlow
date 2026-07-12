using System.Data.Common;
using System.Text.Json;
using Dapper;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default implementation of <see cref="ICreatorProfileSourceStore"/> for creator crawl-source mappings.
/// </summary>
public sealed class CreatorProfileSourceStore : ICreatorProfileSourceStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly bool _ensureSchemaEnabled;
    private readonly Func<CancellationToken, Task<DbConnection>>? _connectionFactoryOverride;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed creator profile-source store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public CreatorProfileSourceStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a creator profile-source store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="ensureSchemaEnabled">
    /// When <c>true</c> (default) the store auto-creates its schema on first use. When
    /// <c>false</c> <see cref="EnsureSchemaAsync"/> is a no-op so the store never issues CREATE/ALTER/DROP.
    /// </param>
    public CreatorProfileSourceStore(RelationalDatabaseConnection connectionInfo, bool ensureSchemaEnabled = true)
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
    internal CreatorProfileSourceStore(
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
    public async Task UpsertAsync(CreatorProfileSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Platform);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.ProfileUsername);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var parameters = new DynamicParameters();
        parameters.Add("slug", source.Slug);
        parameters.Add("platform", source.Platform);
        parameters.Add("profileUsername", source.ProfileUsername);
        parameters.Add("profileUrl", source.ProfileUrl);
        parameters.Add("folderWeightsJson", SerializeFolderWeights(source.FolderWeights));
        parameters.Add("weightsUncurated", source.WeightsUncurated);
        parameters.Add("lastCrawledUtc", source.LastCrawledUtc);
        parameters.Add("updatedUtc", source.UpdatedUtc);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CreatorProfileSource?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<CreatorProfileSourceReadModel>(new CommandDefinition(
            $"""
            SELECT {CreatorProfileSourceReadColumns.SelectList}
              FROM creator_profile_source
             WHERE slug = @slug;
            """,
            new { slug },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : CreatorProfileSourceMapper.ToSource(row);
    }

    /// <inheritdoc />
    public async Task SetLastCrawledAsync(string slug, DateTimeOffset whenUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE creator_profile_source
               SET last_crawled_utc = @when
             WHERE slug = @slug;
            """,
            new { slug, when = whenUtc },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connectionFactoryOverride is not null)
        {
            return await _connectionFactoryOverride(cancellationToken).ConfigureAwait(false);
        }

        return await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? SerializeFolderWeights(IReadOnlyDictionary<int, double> folderWeights)
    {
        ArgumentNullException.ThrowIfNull(folderWeights);

        if (folderWeights.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(folderWeights);
    }

    private const string UpsertSql = """
        INSERT INTO creator_profile_source (
            slug,
            platform,
            profile_username,
            profile_url,
            folder_weights_json,
            weights_uncurated,
            last_crawled_utc,
            updated_utc)
        VALUES (
            @slug,
            @platform,
            @profileUsername,
            @profileUrl,
            @folderWeightsJson,
            @weightsUncurated,
            @lastCrawledUtc,
            @updatedUtc)
        ON CONFLICT (slug) DO UPDATE
        SET platform = EXCLUDED.platform,
            profile_username = EXCLUDED.profile_username,
            profile_url = EXCLUDED.profile_url,
            folder_weights_json = EXCLUDED.folder_weights_json,
            weights_uncurated = EXCLUDED.weights_uncurated,
            last_crawled_utc = EXCLUDED.last_crawled_utc,
            updated_utc = EXCLUDED.updated_utc;
        """;

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS creator_profile_source (
            slug TEXT PRIMARY KEY,
            platform TEXT NOT NULL,
            profile_username TEXT NOT NULL,
            profile_url TEXT NULL,
            folder_weights_json TEXT NULL,
            weights_uncurated BOOLEAN NOT NULL DEFAULT FALSE,
            last_crawled_utc TIMESTAMPTZ NULL,
            updated_utc TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS creator_profile_source (
            slug TEXT PRIMARY KEY,
            platform TEXT NOT NULL,
            profile_username TEXT NOT NULL,
            profile_url TEXT NULL,
            folder_weights_json TEXT NULL,
            weights_uncurated INTEGER NOT NULL DEFAULT 0,
            last_crawled_utc TEXT NULL,
            updated_utc TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;

    private sealed class CreatorProfileSourceReadModel
    {
        public required string Slug { get; init; }

        public required string Platform { get; init; }

        public required string ProfileUsername { get; init; }

        public string? ProfileUrl { get; init; }

        public string? FolderWeightsJson { get; init; }

        public bool WeightsUncurated { get; init; }

        public DateTimeOffset? LastCrawledUtc { get; init; }

        public DateTimeOffset UpdatedUtc { get; init; }
    }

    private static class CreatorProfileSourceReadColumns
    {
        public const string SelectList = "slug, platform, profile_username, profile_url, folder_weights_json, weights_uncurated, last_crawled_utc, updated_utc";
    }

    private static class CreatorProfileSourceMapper
    {
        public static CreatorProfileSource ToSource(CreatorProfileSourceReadModel row)
            => new()
            {
                Slug = row.Slug,
                Platform = row.Platform,
                ProfileUsername = row.ProfileUsername,
                ProfileUrl = row.ProfileUrl,
                FolderWeights = DeserializeFolderWeights(row.FolderWeightsJson),
                WeightsUncurated = row.WeightsUncurated,
                LastCrawledUtc = row.LastCrawledUtc,
                UpdatedUtc = row.UpdatedUtc
            };

        private static IReadOnlyDictionary<int, double> DeserializeFolderWeights(string? folderWeightsJson)
        {
            if (string.IsNullOrWhiteSpace(folderWeightsJson))
            {
                return new Dictionary<int, double>();
            }

            return JsonSerializer.Deserialize<Dictionary<int, double>>(folderWeightsJson)
                ?? new Dictionary<int, double>();
        }
    }
}
