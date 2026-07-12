using System.Data.Common;
using System.Text.Json;
using Dapper;
using DeckFlow.Core.Models;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default implementation of <see cref="ICreatorDeckCacheStore"/> for creator-scoped Archidekt deck caches.
/// </summary>
public sealed class CreatorDeckCacheStore : ICreatorDeckCacheStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly bool _ensureSchemaEnabled;
    private readonly Func<CancellationToken, Task<DbConnection>>? _connectionFactoryOverride;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed creator deck-cache store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public CreatorDeckCacheStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a creator deck-cache store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    /// <param name="ensureSchemaEnabled">
    /// When <c>true</c> (default) the store auto-creates its schema on first use. When
    /// <c>false</c> <see cref="EnsureSchemaAsync"/> is a no-op so the store never issues CREATE/ALTER/DROP.
    /// </param>
    public CreatorDeckCacheStore(RelationalDatabaseConnection connectionInfo, bool ensureSchemaEnabled = true)
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
    internal CreatorDeckCacheStore(
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
    public async Task UpsertAsync(CreatorDeckCacheEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.CreatorSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.DeckId);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ContentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.ConfidenceMarker);
        ArgumentNullException.ThrowIfNull(entry.Entries);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var parameters = new DynamicParameters();
        parameters.Add("creatorSlug", entry.CreatorSlug);
        parameters.Add("deckId", entry.DeckId);
        parameters.Add("contentHash", entry.ContentHash);
        parameters.Add("folderId", entry.FolderId);
        parameters.Add("folderName", entry.FolderName);
        parameters.Add("size", entry.Size);
        parameters.Add("confidenceMarker", entry.ConfidenceMarker);
        parameters.Add("entriesJson", JsonSerializer.Serialize(entry.Entries));
        parameters.Add("cachedUtc", entry.CachedUtc);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CreatorDeckCacheEntry>> GetByCreatorAsync(string creatorSlug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creatorSlug);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<CreatorDeckCacheReadModel>(new CommandDefinition(
            """
            SELECT creator_slug AS CreatorSlug,
                   deck_id AS DeckId,
                   content_hash AS ContentHash,
                   folder_id AS FolderId,
                   folder_name AS FolderName,
                   size AS Size,
                   confidence_marker AS ConfidenceMarker,
                   entries_json AS EntriesJson,
                   cached_utc AS CachedUtc
              FROM creator_deck_cache
             WHERE creator_slug = @creatorSlug
             ORDER BY deck_id;
            """,
            new { creatorSlug },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(ToEntry).ToArray();
    }

    /// <inheritdoc />
    public async Task<string?> GetContentHashAsync(string creatorSlug, string deckId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(creatorSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            """
            SELECT content_hash
              FROM creator_deck_cache
             WHERE creator_slug = @creatorSlug
               AND deck_id = @deckId;
            """,
            new
            {
                creatorSlug,
                deckId
            },
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

    private static CreatorDeckCacheEntry ToEntry(CreatorDeckCacheReadModel row)
    {
        var entries = JsonSerializer.Deserialize<List<DeckEntry>>(row.EntriesJson) ?? [];
        return new CreatorDeckCacheEntry
        {
            CreatorSlug = row.CreatorSlug,
            DeckId = row.DeckId,
            ContentHash = row.ContentHash,
            FolderId = row.FolderId,
            FolderName = row.FolderName,
            Size = row.Size,
            ConfidenceMarker = row.ConfidenceMarker,
            Entries = entries,
            CachedUtc = row.CachedUtc
        };
    }

    private sealed record CreatorDeckCacheReadModel
    {
        public required string CreatorSlug { get; init; }

        public required string DeckId { get; init; }

        public required string ContentHash { get; init; }

        public int? FolderId { get; init; }

        public string? FolderName { get; init; }

        public required int Size { get; init; }

        public required string ConfidenceMarker { get; init; }

        public required string EntriesJson { get; init; }

        public required DateTimeOffset CachedUtc { get; init; }
    }

    private const string UpsertSql = """
        INSERT INTO creator_deck_cache (
            creator_slug,
            deck_id,
            content_hash,
            folder_id,
            folder_name,
            size,
            confidence_marker,
            entries_json,
            cached_utc)
        VALUES (
            @creatorSlug,
            @deckId,
            @contentHash,
            @folderId,
            @folderName,
            @size,
            @confidenceMarker,
            @entriesJson,
            @cachedUtc)
        ON CONFLICT (creator_slug, deck_id) DO UPDATE
        SET content_hash = EXCLUDED.content_hash,
            folder_id = EXCLUDED.folder_id,
            folder_name = EXCLUDED.folder_name,
            size = EXCLUDED.size,
            confidence_marker = EXCLUDED.confidence_marker,
            entries_json = EXCLUDED.entries_json,
            cached_utc = EXCLUDED.cached_utc;
        """;

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS creator_deck_cache (
            creator_slug TEXT NOT NULL,
            deck_id TEXT NOT NULL,
            content_hash TEXT NOT NULL,
            folder_id INTEGER NULL,
            folder_name TEXT NULL,
            size INTEGER NOT NULL,
            confidence_marker TEXT NOT NULL,
            entries_json TEXT NOT NULL,
            cached_utc TIMESTAMPTZ NOT NULL DEFAULT now(),
            PRIMARY KEY (creator_slug, deck_id)
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS creator_deck_cache (
            creator_slug TEXT NOT NULL,
            deck_id TEXT NOT NULL,
            content_hash TEXT NOT NULL,
            folder_id INTEGER NULL,
            folder_name TEXT NULL,
            size INTEGER NOT NULL,
            confidence_marker TEXT NOT NULL,
            entries_json TEXT NOT NULL,
            cached_utc TEXT NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (creator_slug, deck_id)
        );
        """;
}
