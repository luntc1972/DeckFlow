using System.Data.Common;
using System.Globalization;
using DeckFlow.Core.Storage;

namespace DeckFlow.Web.Services.Content;

/// <summary>
/// Default implementation of <see cref="IContentVideoStore"/> backed by the local Content KB database.
/// </summary>
public sealed class ContentVideoStore : IContentVideoStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public ContentVideoStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    public ContentVideoStore(RelationalDatabaseConnection connectionInfo)
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
    /// DI constructor that resolves the always-local Content KB connection.
    /// </summary>
    /// <param name="environment">Web host environment used by the connection factory.</param>
    public ContentVideoStore(IWebHostEnvironment environment)
        : this(DeckFlowDatabaseConnectionFactory.CreateLocalContentKbConnection(environment)) { }

    /// <inheritdoc />
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaReady) return;
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady) return;

            // Why: REVIEW #1 / D-04 require content_sources to exist before content_videos
            // declares its FK parent, and Postgres rejects FKs to missing parent tables.
            var sourceStore = new ContentSourceStore(_connectionInfo);
            await sourceStore.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

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
    public async Task<long> InsertVideoAsync(
        long sourceId,
        string? youtubeVideoId,
        string? rssGuid,
        string title,
        string videoUrl,
        DateTimeOffset? publishedUtc,
        string transcriptStatus,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(videoUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(transcriptStatus);
        var hasYoutubeVideoId = !string.IsNullOrWhiteSpace(youtubeVideoId);
        var hasRssGuid = !string.IsNullOrWhiteSpace(rssGuid);
        if (hasYoutubeVideoId == hasRssGuid)
        {
            throw new ArgumentException(
                "Exactly one of youtubeVideoId or rssGuid must be supplied for a content video.",
                nameof(youtubeVideoId));
        }

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = InsertVideoSql;
        RelationalDatabaseConnection.AddParameter(command, "@sourceId", sourceId);
        RelationalDatabaseConnection.AddParameter(command, "@youtubeVideoId", (object?)youtubeVideoId ?? DBNull.Value);
        RelationalDatabaseConnection.AddParameter(command, "@rssGuid", (object?)rssGuid ?? DBNull.Value);
        RelationalDatabaseConnection.AddParameter(command, "@title", title);
        RelationalDatabaseConnection.AddParameter(command, "@videoUrl", videoUrl);
        RelationalDatabaseConnection.AddParameter(command, "@publishedUtc", FormatTimestamp(publishedUtc));
        RelationalDatabaseConnection.AddParameter(command, "@transcriptStatus", transcriptStatus);

        var id = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return ContentStoreGeneratedId.Read(id);
    }

    /// <inheritdoc />
    public async Task<long> InsertTranscriptAsync(
        long videoId,
        string source,
        string body,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = InsertTranscriptSql;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);
        RelationalDatabaseConnection.AddParameter(command, "@source", source);
        RelationalDatabaseConnection.AddParameter(command, "@body", body);

        var id = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return ContentStoreGeneratedId.Read(id);
    }

    /// <inheritdoc />
    public async Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = InsertSummarySql;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);
        RelationalDatabaseConnection.AddParameter(command, "@body", body);

        var id = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return ContentStoreGeneratedId.Read(id);
    }

    /// <inheritdoc />
    public async Task<long> InsertClipAsync(
        long videoId,
        int timestampS,
        string excerpt,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(excerpt);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = InsertClipSql;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);
        RelationalDatabaseConnection.AddParameter(command, "@timestampS", timestampS);
        RelationalDatabaseConnection.AddParameter(command, "@excerpt", excerpt);
        RelationalDatabaseConnection.AddParameter(command, "@sortOrder", sortOrder);

        var id = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return ContentStoreGeneratedId.Read(id);
    }

    /// <inheritdoc />
    public async Task<long> InsertTagAsync(
        long videoId,
        string dimension,
        string tagValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension);
        ArgumentException.ThrowIfNullOrWhiteSpace(tagValue);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = InsertTagSql;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);
        RelationalDatabaseConnection.AddParameter(command, "@dimension", dimension);
        RelationalDatabaseConnection.AddParameter(command, "@tagValue", tagValue);

        var id = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return ContentStoreGeneratedId.Read(id);
    }

    /// <inheritdoc />
    public async Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM content_videos
             WHERE id = @videoId;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountTranscriptsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
        => await CountByVideoAsync(CountTranscriptsByVideoSql, videoId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> CountSummariesByVideoAsync(long videoId, CancellationToken cancellationToken = default)
        => await CountByVideoAsync(CountSummariesByVideoSql, videoId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> CountClipsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
        => await CountByVideoAsync(CountClipsByVideoSql, videoId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> CountTagsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
        => await CountByVideoAsync(CountTagsByVideoSql, videoId, cancellationToken).ConfigureAwait(false);

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    private async Task<int> CountByVideoAsync(
        string commandText,
        long videoId,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);

        var count = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(count, CultureInfo.InvariantCulture);
    }

    private object FormatTimestamp(DateTimeOffset? value)
    {
        if (value is null)
        {
            return DBNull.Value;
        }

        return _connectionInfo.IsPostgres
            ? value.Value.UtcDateTime
            : value.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }

    private const string InsertVideoSql = """
        INSERT INTO content_videos (
          source_id,
          youtube_video_id,
          rss_guid,
          title,
          video_url,
          published_utc,
          transcript_status)
        VALUES (
          @sourceId,
          @youtubeVideoId,
          @rssGuid,
          @title,
          @videoUrl,
          @publishedUtc,
          @transcriptStatus)
        RETURNING id;
        """;

    private const string InsertTranscriptSql = """
        INSERT INTO content_transcripts (video_id, source, body)
        VALUES (@videoId, @source, @body)
        RETURNING id;
        """;

    private const string InsertSummarySql = """
        INSERT INTO content_summaries (video_id, body)
        VALUES (@videoId, @body)
        RETURNING id;
        """;

    private const string InsertClipSql = """
        INSERT INTO content_clips (video_id, timestamp_s, excerpt, sort_order)
        VALUES (@videoId, @timestampS, @excerpt, @sortOrder)
        RETURNING id;
        """;

    private const string InsertTagSql = """
        INSERT INTO content_tags (video_id, dimension, tag_value)
        VALUES (@videoId, @dimension, @tagValue)
        RETURNING id;
        """;

    private const string CountTranscriptsByVideoSql = """
        SELECT COUNT(*)
          FROM content_transcripts
         WHERE video_id = @videoId;
        """;

    private const string CountSummariesByVideoSql = """
        SELECT COUNT(*)
          FROM content_summaries
         WHERE video_id = @videoId;
        """;

    private const string CountClipsByVideoSql = """
        SELECT COUNT(*)
          FROM content_clips
         WHERE video_id = @videoId;
        """;

    private const string CountTagsByVideoSql = """
        SELECT COUNT(*)
          FROM content_tags
         WHERE video_id = @videoId;
        """;

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_videos (
          id                BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          source_id         BIGINT NOT NULL REFERENCES content_sources(id) ON DELETE CASCADE,
          youtube_video_id  TEXT NULL,
          rss_guid          TEXT NULL,
          title             TEXT NOT NULL,
          video_url         TEXT NOT NULL,
          published_utc     TIMESTAMPTZ NULL,
          transcript_status TEXT NOT NULL DEFAULT 'pending' CHECK (transcript_status IN ('pending','captions','whisper','failed','skipped_over_cap')),
          created_utc       TIMESTAMPTZ NOT NULL DEFAULT now(),
          UNIQUE (youtube_video_id),
          UNIQUE (rss_guid),
          CHECK ((youtube_video_id IS NOT NULL) <> (rss_guid IS NOT NULL))
        );
        CREATE TABLE IF NOT EXISTS content_transcripts (
          id          BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          video_id    BIGINT NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          source      TEXT NOT NULL CHECK (source IN ('captions','whisper')),
          body        TEXT NOT NULL,
          created_utc TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS content_summaries (
          id          BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          video_id    BIGINT NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          body        TEXT NOT NULL,
          created_utc TIMESTAMPTZ NOT NULL DEFAULT now()
        );
        CREATE TABLE IF NOT EXISTS content_clips (
          id          BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          video_id    BIGINT NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          timestamp_s INT NOT NULL,
          excerpt     TEXT NOT NULL,
          sort_order  INT NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS content_tags (
          id        BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          video_id  BIGINT NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          dimension TEXT NOT NULL CHECK (dimension IN ('archetype','bracket','card_category')),
          tag_value TEXT NOT NULL,
          UNIQUE (video_id, dimension, tag_value)
        );
        CREATE INDEX IF NOT EXISTS ix_content_videos_source_id       ON content_videos(source_id);
        CREATE INDEX IF NOT EXISTS ix_content_transcripts_video_id   ON content_transcripts(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_summaries_video_id     ON content_summaries(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_clips_video_id         ON content_clips(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_tags_video_id          ON content_tags(video_id);
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_videos (
          id                INTEGER PRIMARY KEY AUTOINCREMENT,
          source_id         INTEGER NOT NULL REFERENCES content_sources(id) ON DELETE CASCADE,
          youtube_video_id  TEXT NULL,
          rss_guid          TEXT NULL,
          title             TEXT NOT NULL,
          video_url         TEXT NOT NULL,
          published_utc     TEXT NULL,
          transcript_status TEXT NOT NULL DEFAULT 'pending' CHECK (transcript_status IN ('pending','captions','whisper','failed','skipped_over_cap')),
          created_utc       TEXT NOT NULL DEFAULT (datetime('now')),
          UNIQUE (youtube_video_id),
          UNIQUE (rss_guid),
          CHECK ((youtube_video_id IS NOT NULL) <> (rss_guid IS NOT NULL))
        );
        CREATE TABLE IF NOT EXISTS content_transcripts (
          id          INTEGER PRIMARY KEY AUTOINCREMENT,
          video_id    INTEGER NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          source      TEXT NOT NULL CHECK (source IN ('captions','whisper')),
          body        TEXT NOT NULL,
          created_utc TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE TABLE IF NOT EXISTS content_summaries (
          id          INTEGER PRIMARY KEY AUTOINCREMENT,
          video_id    INTEGER NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          body        TEXT NOT NULL,
          created_utc TEXT NOT NULL DEFAULT (datetime('now'))
        );
        CREATE TABLE IF NOT EXISTS content_clips (
          id          INTEGER PRIMARY KEY AUTOINCREMENT,
          video_id    INTEGER NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          timestamp_s INTEGER NOT NULL,
          excerpt     TEXT NOT NULL,
          sort_order  INTEGER NOT NULL DEFAULT 0
        );
        CREATE TABLE IF NOT EXISTS content_tags (
          id        INTEGER PRIMARY KEY AUTOINCREMENT,
          video_id  INTEGER NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          dimension TEXT NOT NULL CHECK (dimension IN ('archetype','bracket','card_category')),
          tag_value TEXT NOT NULL,
          UNIQUE (video_id, dimension, tag_value)
        );
        CREATE INDEX IF NOT EXISTS ix_content_videos_source_id       ON content_videos(source_id);
        CREATE INDEX IF NOT EXISTS ix_content_transcripts_video_id   ON content_transcripts(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_summaries_video_id     ON content_summaries(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_clips_video_id         ON content_clips(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_tags_video_id          ON content_tags(video_id);
        """;
}
