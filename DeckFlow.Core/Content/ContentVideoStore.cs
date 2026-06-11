using System.Data.Common;
using System.Globalization;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

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
    public async Task<ContentVideo?> GetVideoByYoutubeIdAsync(
        long sourceId,
        string youtubeVideoId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = GetVideoByYoutubeIdSql;
        RelationalDatabaseConnection.AddParameter(command, "@sourceId", sourceId);
        RelationalDatabaseConnection.AddParameter(command, "@youtubeVideoId", youtubeVideoId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadVideo(reader);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(
        long sourceId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        // Why: source-scoped so a video is only ever distilled under its own
        // source slug and a disabled source's videos are skipped by the caller (HIGH-2).
        command.CommandText = ListVideosPendingDistillSql;
        RelationalDatabaseConnection.AddParameter(command, "@sourceId", sourceId);

        var videos = new List<ContentVideo>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            videos.Add(ReadVideo(reader));
        }

        return videos;
    }

    /// <inheritdoc />
    public async Task UpdateTranscriptStatusAsync(
        long videoId,
        string status,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!IsValidTranscriptStatus(status))
        {
            throw new ArgumentException($"Unknown transcript status: {status}.", nameof(status));
        }

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = UpdateTranscriptStatusSql;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);
        RelationalDatabaseConnection.AddParameter(command, "@status", status);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
    public async Task<ContentTranscriptBody?> GetLatestTranscriptAsync(
        long videoId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = GetLatestTranscriptSql;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ContentTranscriptBody
        {
            Body = reader.GetString(0),
            Source = reader.GetString(1),
        };
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
    public async Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM content_videos
             WHERE youtube_video_id = @youtubeVideoId;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@youtubeVideoId", youtubeVideoId);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllVideosAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM content_videos;
            """;
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        // Why: enables idempotent clean re-distill before re-inserting generated
        // child rows, avoiding duplicates and content_tags UNIQUE violations.
        command.CommandText = ClearDistillOutputSql;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> GetDistillStatusAsync(long videoId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = GetDistillStatusSql;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return reader.GetString(0);
    }

    /// <inheritdoc />
    public async Task SetDistillStatusAsync(
        long videoId,
        string status,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!IsValidDistillStatus(status))
        {
            throw new ArgumentException($"Unknown distill status: {status}.", nameof(status));
        }

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = _connectionInfo.IsPostgres ? PostgresSetDistillStatusSql : SqliteSetDistillStatusSql;
        RelationalDatabaseConnection.AddParameter(command, "@videoId", videoId);
        RelationalDatabaseConnection.AddParameter(command, "@status", status);
        RelationalDatabaseConnection.AddParameter(command, "@updatedUtc", FormatTimestamp(DateTimeOffset.UtcNow));
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

    private static bool IsValidTranscriptStatus(string status)
        => status is TranscriptStatus.Pending
            or TranscriptStatus.Captions
            or TranscriptStatus.Whisper
            or TranscriptStatus.Failed
            or TranscriptStatus.SkippedOverCap
            or TranscriptStatus.SkippedNoCaptions;

    private static bool IsValidDistillStatus(string status)
        => status is DistillStatusDistilled
            or DistillStatusSkippedOverCap
            or DistillStatusFailed;

    private static ContentVideo ReadVideo(DbDataReader reader)
        => new()
        {
            Id = reader.GetInt64(0),
            SourceId = reader.GetInt64(1),
            YoutubeVideoId = reader.IsDBNull(2) ? null : reader.GetString(2),
            RssGuid = reader.IsDBNull(3) ? null : reader.GetString(3),
            Title = reader.GetString(4),
            VideoUrl = reader.GetString(5),
            PublishedUtc = reader.IsDBNull(6) ? null : ReadDateTimeOffset(reader, 6),
            TranscriptStatus = reader.GetString(7),
            CreatedUtc = ReadDateTimeOffset(reader, 8)
        };

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

    private const string GetVideoByYoutubeIdSql = """
        SELECT id,
               source_id,
               youtube_video_id,
               rss_guid,
               title,
               video_url,
               published_utc,
               transcript_status,
               created_utc
          FROM content_videos
         WHERE source_id = @sourceId
           AND youtube_video_id = @youtubeVideoId;
        """;

    private const string ListVideosPendingDistillSql = """
        SELECT v.id,
               v.source_id,
               v.youtube_video_id,
               v.rss_guid,
               v.title,
               v.video_url,
               v.published_utc,
               v.transcript_status,
               v.created_utc
          FROM content_videos v
         WHERE v.source_id = @sourceId
           AND v.transcript_status IN ('captions','whisper')
           AND EXISTS (
               SELECT 1
                 FROM content_transcripts t
                WHERE t.video_id = v.id)
         ORDER BY v.id;
        """;

    private const string UpdateTranscriptStatusSql = """
        UPDATE content_videos
           SET transcript_status = @status
         WHERE id = @videoId;
        """;

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

    private const string GetLatestTranscriptSql = """
        SELECT body,
               source
          FROM content_transcripts
         WHERE video_id = @videoId
         ORDER BY id DESC
         LIMIT 1;
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

    private const string ClearDistillOutputSql = """
        DELETE FROM content_summaries
         WHERE video_id = @videoId;
        DELETE FROM content_clips
         WHERE video_id = @videoId;
        DELETE FROM content_tags
         WHERE video_id = @videoId;
        """;

    private const string GetDistillStatusSql = """
        SELECT status
          FROM content_distill_status
         WHERE video_id = @videoId;
        """;

    private const string PostgresSetDistillStatusSql = """
        INSERT INTO content_distill_status (video_id, status, updated_utc)
        VALUES (@videoId, @status, @updatedUtc)
        ON CONFLICT (video_id) DO UPDATE
           SET status = EXCLUDED.status,
               updated_utc = EXCLUDED.updated_utc;
        """;

    private const string SqliteSetDistillStatusSql = """
        INSERT INTO content_distill_status (video_id, status, updated_utc)
        VALUES (@videoId, @status, @updatedUtc)
        ON CONFLICT(video_id) DO UPDATE
           SET status = excluded.status,
               updated_utc = excluded.updated_utc;
        """;

    private const string DistillStatusDistilled = "distilled";
    private const string DistillStatusSkippedOverCap = "skipped_over_cap";
    private const string DistillStatusFailed = "failed";

    // Why: content_distill_status supersedes derived artifact/index idempotency
    // (review HIGH-3), and a new IF-NOT-EXISTS table is schema-safe for UAT dbs.
    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_videos (
          id                BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          source_id         BIGINT NOT NULL REFERENCES content_sources(id) ON DELETE CASCADE,
          youtube_video_id  TEXT NULL,
          rss_guid          TEXT NULL,
          title             TEXT NOT NULL,
          video_url         TEXT NOT NULL,
          published_utc     TIMESTAMPTZ NULL,
          transcript_status TEXT NOT NULL DEFAULT 'pending' CHECK (transcript_status IN ('pending','captions','whisper','failed','skipped_over_cap','skipped_no_captions')),
          created_utc       TIMESTAMPTZ NOT NULL DEFAULT now(),
          UNIQUE (youtube_video_id),
          UNIQUE (rss_guid),
          CHECK ((youtube_video_id IS NOT NULL) <> (rss_guid IS NOT NULL))
        );
        CREATE TABLE IF NOT EXISTS content_distill_status (
          video_id    BIGINT PRIMARY KEY REFERENCES content_videos(id) ON DELETE CASCADE,
          status      TEXT NOT NULL CHECK (status IN ('distilled','skipped_over_cap','failed')),
          updated_utc TIMESTAMPTZ NOT NULL DEFAULT now()
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
          transcript_status TEXT NOT NULL DEFAULT 'pending' CHECK (transcript_status IN ('pending','captions','whisper','failed','skipped_over_cap','skipped_no_captions')),
          created_utc       TEXT NOT NULL DEFAULT (datetime('now')),
          UNIQUE (youtube_video_id),
          UNIQUE (rss_guid),
          CHECK ((youtube_video_id IS NOT NULL) <> (rss_guid IS NOT NULL))
        );
        CREATE TABLE IF NOT EXISTS content_distill_status (
          video_id    INTEGER PRIMARY KEY REFERENCES content_videos(id) ON DELETE CASCADE,
          status      TEXT NOT NULL CHECK (status IN ('distilled','skipped_over_cap','failed')),
          updated_utc TEXT NOT NULL DEFAULT (datetime('now'))
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
