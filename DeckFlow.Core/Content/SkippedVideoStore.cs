using System.Data.Common;
using Dapper;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default <see cref="ISkippedVideoStore"/> backed by the local Content KB database (HSEL-02/03).
/// Mirrors <see cref="BlockedVideoStore"/> but writes ONLY its own <c>skipped_videos</c> table —
/// it never touches <c>blocked_videos</c>, <c>content_*</c>, or any artifact file. Skipping is a
/// soft "don't surface this candidate again", strictly distinct from Block.
/// </summary>
public sealed class SkippedVideoStore : ISkippedVideoStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public SkippedVideoStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    public SkippedVideoStore(RelationalDatabaseConnection connectionInfo)
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
    public async Task AddSkipAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            _connectionInfo.IsPostgres ? PostgresInsertSql : SqliteInsertSql,
            new { youtubeVideoId, reason },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveSkipAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM skipped_videos
             WHERE youtube_video_id = @youtubeVideoId;
            """,
            new { youtubeVideoId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<bool> IsSkippedAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var result = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            SELECT COUNT(*)
              FROM skipped_videos
             WHERE youtube_video_id = @youtubeVideoId;
            """,
            new { youtubeVideoId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return result > 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SkippedVideo>> ListSkippedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var skipped = await connection.QueryAsync<SkippedVideo>(new CommandDefinition(
            """
            SELECT youtube_video_id,
                   reason,
                   skipped_utc
              FROM skipped_videos
             ORDER BY skipped_utc ASC,
                      youtube_video_id ASC;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return skipped.ToList();
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    // Why: a SEPARATE table from blocked_videos (skip != block) and outside the content_* prefix so
    // Phase 37.5-style corpus resets leave the skip list intact.
    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS skipped_videos (
          youtube_video_id TEXT PRIMARY KEY,
          reason           TEXT NULL,
          skipped_utc      TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS skipped_videos (
          youtube_video_id TEXT PRIMARY KEY,
          reason           TEXT NULL,
          skipped_utc      TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;

    private const string PostgresInsertSql = """
        INSERT INTO skipped_videos (
          youtube_video_id,
          reason
        )
        VALUES (
          @youtubeVideoId,
          @reason
        )
        ON CONFLICT (youtube_video_id) DO NOTHING;
        """;

    private const string SqliteInsertSql = """
        INSERT OR IGNORE INTO skipped_videos (
          youtube_video_id,
          reason
        )
        VALUES (
          @youtubeVideoId,
          @reason
        );
        """;
}
