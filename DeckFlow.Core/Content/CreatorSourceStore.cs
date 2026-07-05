using System.Data.Common;
using Dapper;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default <see cref="ICreatorSourceStore"/> backed by the local Content KB database (SRC-01).
/// Mirrors <see cref="BlockedVideoStore"/>'s shape. Dedupe is enforced by a UNIQUE index on a
/// persisted normalized channel reference (trim + lowercase), so adding the same channel twice —
/// including whitespace/case variants — is idempotent and cannot drift between add and list.
/// </summary>
public sealed class CreatorSourceStore : ICreatorSourceStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public CreatorSourceStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    public CreatorSourceStore(RelationalDatabaseConnection connectionInfo)
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
    /// Normalizes a channel reference for dedupe: trim surrounding whitespace and lowercase
    /// (invariant). Small, intentional normalization — two refs that differ only by surrounding
    /// whitespace or letter case are treated as the same creator.
    /// </summary>
    /// <param name="channelRef">Raw channel reference.</param>
    /// <returns>The normalized reference.</returns>
    public static string NormalizeChannelRef(string channelRef)
    {
        ArgumentNullException.ThrowIfNull(channelRef);
        return channelRef.Trim().ToLowerInvariant();
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
            await using var create = connection.CreateCommand();
            create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            // Why: additive column backfills for an existing creator_sources table (P87). CREATE TABLE
            // IF NOT EXISTS never alters an existing table, so introspect and ADD COLUMN idempotently —
            // same carve-out pattern as ContentSiteIndexStore. Both columns nullable so legacy rows are
            // valid untouched.
            var columns = await GetTableColumnsAsync(connection, "creator_sources", cancellationToken).ConfigureAwait(false);
            if (!columns.Contains("source_slug"))
            {
                await TryAddColumnAsync(connection, "ALTER TABLE creator_sources ADD COLUMN source_slug TEXT NULL;", cancellationToken).ConfigureAwait(false);
            }

            if (!columns.Contains("content_source_id"))
            {
                await TryAddColumnAsync(
                    connection,
                    _connectionInfo.IsPostgres
                        ? "ALTER TABLE creator_sources ADD COLUMN content_source_id BIGINT NULL;"
                        : "ALTER TABLE creator_sources ADD COLUMN content_source_id INTEGER NULL;",
                    cancellationToken).ConfigureAwait(false);
            }

            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(string displayName, string channelRef, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelRef);

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var trimmedName = displayName.Trim();
        var trimmedRef = channelRef.Trim();
        var normalizedRef = NormalizeChannelRef(channelRef);
        // Why: provisional display-derived slug shown on /creators before the first harvest; the
        // canonical content_sources slug overwrites it at link time (LinkContentSourceAsync).
        var provisionalSlug = SlugifySourceName.Slugify(trimmedName);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            _connectionInfo.IsPostgres ? PostgresInsertSql : SqliteInsertSql,
            new { displayName = trimmedName, channelRef = trimmedRef, normalizedChannelRef = normalizedRef, sourceSlug = provisionalSlug },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM creator_sources
             WHERE id = @id;
            """,
            new { id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CreatorSource>> ListAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var creators = await connection.QueryAsync<CreatorSource>(new CommandDefinition(
            """
            SELECT id,
                   display_name,
                   channel_ref,
                   added_utc,
                   source_slug,
                   content_source_id
              FROM creator_sources
             ORDER BY display_name ASC,
                      id ASC;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return creators.ToList();
    }

    /// <inheritdoc />
    public async Task<CreatorSource?> GetByNormalizedRefAsync(string normalizedChannelRef, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedChannelRef);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<CreatorSource>(new CommandDefinition(
            """
            SELECT id,
                   display_name,
                   channel_ref,
                   added_utc,
                   source_slug,
                   content_source_id
              FROM creator_sources
             WHERE normalized_channel_ref = @normalizedChannelRef;
            """,
            new { normalizedChannelRef },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task LinkContentSourceAsync(long creatorId, long contentSourceId, string canonicalSlug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalSlug);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE creator_sources
               SET content_source_id = @contentSourceId,
                   source_slug = @canonicalSlug
             WHERE id = @creatorId;
            """,
            new { contentSourceId, canonicalSlug, creatorId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    // Why: the schema gate is process-local, so two Studio instances upgrading the same content-kb.db
    // can both pass the introspect check and race on the ALTER. SQLite has no ADD COLUMN IF NOT EXISTS,
    // so tolerate the loser's "duplicate column" error — the column exists afterward either way.
    private static async Task TryAddColumnAsync(DbConnection connection, string alterSql, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = alterSql;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException exception) when (exception.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
            // Another instance won the race and already added the column — the desired end state.
        }
    }

    // Why: PRAGMA / information_schema introspection is an intentional raw ADO.NET carve-out so the
    // additive column migration can run only when a column is actually missing (idempotent).
    private async Task<IReadOnlySet<string>> GetTableColumnsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_connectionInfo.IsSqlite)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(1))
                {
                    columns.Add(reader.GetString(1));
                }
            }

            return columns;
        }

        await using var pgCommand = connection.CreateCommand();
        pgCommand.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = @tableName
            ORDER BY ordinal_position;
            """;
        RelationalDatabaseConnection.AddParameter(pgCommand, "@tableName", tableName);
        await using var pgReader = await pgCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await pgReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!pgReader.IsDBNull(0))
            {
                columns.Add(pgReader.GetString(0));
            }
        }

        return columns;
    }

    // Why: lives outside the content_* prefix so Phase 37.5-style corpus resets (which purge
    // content_* tables) leave the curated creator list intact, exactly like blocked_videos.
    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS creator_sources (
          id                     BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          display_name           TEXT NOT NULL,
          channel_ref            TEXT NOT NULL,
          normalized_channel_ref TEXT NOT NULL UNIQUE,
          added_utc              TIMESTAMPTZ NOT NULL DEFAULT now(),
          source_slug            TEXT NULL,
          content_source_id      BIGINT NULL
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS creator_sources (
          id                     INTEGER PRIMARY KEY AUTOINCREMENT,
          display_name           TEXT NOT NULL,
          channel_ref            TEXT NOT NULL,
          normalized_channel_ref TEXT NOT NULL UNIQUE,
          added_utc              TEXT NOT NULL DEFAULT (datetime('now')),
          source_slug            TEXT NULL,
          content_source_id      INTEGER NULL
        );
        """;

    private const string PostgresInsertSql = """
        INSERT INTO creator_sources (
          display_name,
          channel_ref,
          normalized_channel_ref,
          source_slug
        )
        VALUES (
          @displayName,
          @channelRef,
          @normalizedChannelRef,
          @sourceSlug
        )
        ON CONFLICT (normalized_channel_ref) DO NOTHING;
        """;

    private const string SqliteInsertSql = """
        INSERT OR IGNORE INTO creator_sources (
          display_name,
          channel_ref,
          normalized_channel_ref,
          source_slug
        )
        VALUES (
          @displayName,
          @channelRef,
          @normalizedChannelRef,
          @sourceSlug
        );
        """;
}
