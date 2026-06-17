using System.Data.Common;
using Dapper;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default implementation of <see cref="IContentSourceStore"/> backed by the local Content KB database.
/// </summary>
public sealed class ContentSourceStore : IContentSourceStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public ContentSourceStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    public ContentSourceStore(RelationalDatabaseConnection connectionInfo)
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
    public async Task<long> InsertSourceAsync(
        string sourceSlug,
        string displayName,
        string sourceType,
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceUrl);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var id = await connection.ExecuteScalarAsync(new CommandDefinition(
            InsertSourceSql,
            new { sourceSlug, displayName, sourceType, sourceUrl },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return ContentStoreGeneratedId.Read(id);
    }

    /// <inheritdoc />
    public async Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<ContentSource>(new CommandDefinition(
            """
            SELECT id, source_slug, display_name, source_type, source_url, is_enabled, created_utc
              FROM content_sources
             WHERE id = @id;
            """,
            new { id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ContentSource?> GetSourceByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<ContentSource>(new CommandDefinition(
            """
            SELECT id, source_slug, display_name, source_type, source_url, is_enabled, created_utc
              FROM content_sources
             WHERE source_url = @url;
            """,
            new { url },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetEnabledAsync(long id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_sources
               SET is_enabled = @isEnabled
             WHERE id = @id;
            """,
            new { isEnabled, id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var sources = await connection.QueryAsync<ContentSource>(new CommandDefinition(
            """
            SELECT id, source_slug, display_name, source_type, source_url, is_enabled, created_utc
              FROM content_sources
             WHERE is_enabled = @isEnabled
             ORDER BY source_slug;
            """,
            new { isEnabled = true },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return sources.ToList();
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    private const string InsertSourceSql = """
        INSERT INTO content_sources (source_slug, display_name, source_type, source_url)
        VALUES (@sourceSlug, @displayName, @sourceType, @sourceUrl)
        RETURNING id;
        """;

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_sources (
          id           BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          source_slug  TEXT NOT NULL,
          display_name TEXT NOT NULL,
          source_type  TEXT NOT NULL CHECK (source_type IN ('youtube_channel','podcast_rss')),
          source_url   TEXT NOT NULL,
          is_enabled   BOOLEAN NOT NULL DEFAULT TRUE,
          created_utc  TIMESTAMPTZ NOT NULL DEFAULT now(),
          UNIQUE (source_url),
          UNIQUE (source_slug)
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_sources (
          id           INTEGER PRIMARY KEY AUTOINCREMENT,
          source_slug  TEXT NOT NULL,
          display_name TEXT NOT NULL,
          source_type  TEXT NOT NULL CHECK (source_type IN ('youtube_channel','podcast_rss')),
          source_url   TEXT NOT NULL,
          is_enabled   INTEGER NOT NULL DEFAULT 1,
          created_utc  TEXT NOT NULL DEFAULT (datetime('now')),
          UNIQUE (source_url),
          UNIQUE (source_slug)
        );
        """;
}
