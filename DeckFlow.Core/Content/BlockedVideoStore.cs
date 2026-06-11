using System.Data.Common;
using System.Globalization;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default implementation of <see cref="IBlockedVideoStore"/> backed by the local Content KB database.
/// </summary>
public sealed class BlockedVideoStore : IBlockedVideoStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public BlockedVideoStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    public BlockedVideoStore(RelationalDatabaseConnection connectionInfo)
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
    public async Task AddBlockAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = _connectionInfo.IsPostgres ? PostgresInsertSql : SqliteInsertSql;
        RelationalDatabaseConnection.AddParameter(command, "@youtubeVideoId", youtubeVideoId);
        RelationalDatabaseConnection.AddParameter(command, "@reason", reason);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveBlockAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM blocked_videos
             WHERE youtube_video_id = @youtubeVideoId;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@youtubeVideoId", youtubeVideoId);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <inheritdoc />
    public async Task<bool> IsBlockedAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
              FROM blocked_videos
             WHERE youtube_video_id = @youtubeVideoId;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@youtubeVideoId", youtubeVideoId);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result, CultureInfo.InvariantCulture) > 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlockedVideo>> ListBlockedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var blocked = new List<BlockedVideo>();

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT youtube_video_id,
                   reason,
                   blocked_utc
              FROM blocked_videos
             ORDER BY blocked_utc ASC,
                      youtube_video_id ASC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            blocked.Add(new BlockedVideo
            {
                YoutubeVideoId = reader.GetString(0),
                Reason = reader.IsDBNull(1) ? null : reader.GetString(1),
                BlockedUtc = ReadDateTimeOffset(reader, 2)
            });
        }

        return blocked;
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    private static DateTimeOffset ReadDateTimeOffset(DbDataReader reader, int ordinal)
    {
        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            DateTimeOffset dto => dto.ToUniversalTime(),
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc), TimeSpan.Zero),
            string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime(),
            _ => new DateTimeOffset(Convert.ToDateTime(raw, CultureInfo.InvariantCulture), TimeSpan.Zero)
        };
    }

    // Why: Phase 37.5 resets will purge content_* tables, so the block list must
    // live outside that prefix to remain intact across corpus resets.
    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS blocked_videos (
          youtube_video_id TEXT PRIMARY KEY,
          reason           TEXT NULL,
          blocked_utc      TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS blocked_videos (
          youtube_video_id TEXT PRIMARY KEY,
          reason           TEXT NULL,
          blocked_utc      TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;

    private const string PostgresInsertSql = """
        INSERT INTO blocked_videos (
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
        INSERT OR IGNORE INTO blocked_videos (
          youtube_video_id,
          reason
        )
        VALUES (
          @youtubeVideoId,
          @reason
        );
        """;
}
