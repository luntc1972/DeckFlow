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

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            _connectionInfo.IsPostgres ? PostgresInsertSql : SqliteInsertSql,
            new { displayName = trimmedName, channelRef = trimmedRef, normalizedChannelRef = normalizedRef },
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
                   added_utc
              FROM creator_sources
             ORDER BY display_name ASC,
                      id ASC;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return creators.ToList();
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    // Why: lives outside the content_* prefix so Phase 37.5-style corpus resets (which purge
    // content_* tables) leave the curated creator list intact, exactly like blocked_videos.
    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS creator_sources (
          id                     BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          display_name           TEXT NOT NULL,
          channel_ref            TEXT NOT NULL,
          normalized_channel_ref TEXT NOT NULL UNIQUE,
          added_utc              TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS creator_sources (
          id                     INTEGER PRIMARY KEY AUTOINCREMENT,
          display_name           TEXT NOT NULL,
          channel_ref            TEXT NOT NULL,
          normalized_channel_ref TEXT NOT NULL UNIQUE,
          added_utc              TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;

    private const string PostgresInsertSql = """
        INSERT INTO creator_sources (
          display_name,
          channel_ref,
          normalized_channel_ref
        )
        VALUES (
          @displayName,
          @channelRef,
          @normalizedChannelRef
        )
        ON CONFLICT (normalized_channel_ref) DO NOTHING;
        """;

    private const string SqliteInsertSql = """
        INSERT OR IGNORE INTO creator_sources (
          display_name,
          channel_ref,
          normalized_channel_ref
        )
        VALUES (
          @displayName,
          @channelRef,
          @normalizedChannelRef
        );
        """;
}
