using System.Data.Common;
using System.Globalization;
using Dapper;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
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
            // Why: schema creation is an intentional raw ADO.NET carve-out for this phase.
            await using var create = connection.CreateCommand();
            create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await EnsureFilteredDistillStatusConstraintAsync(connection, cancellationToken).ConfigureAwait(false);

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
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertVideoSql,
            new { sourceId, youtubeVideoId, rssGuid, title, videoUrl, publishedUtc, transcriptStatus },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
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
        return await connection.QuerySingleOrDefaultAsync<ContentVideo>(new CommandDefinition(
            GetVideoByYoutubeIdSql,
            new { sourceId, youtubeVideoId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(
        long sourceId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        // Why: source-scoped so a video is only ever distilled under its own
        // source slug and a disabled source's videos are skipped by the caller (HIGH-2).
        var videos = await connection.QueryAsync<ContentVideo>(new CommandDefinition(
            ListVideosPendingDistillSql,
            new { sourceId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return videos.ToList();
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
        await connection.ExecuteAsync(new CommandDefinition(
            UpdateTranscriptStatusSql,
            new { videoId, status },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
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
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertTranscriptSql,
            new { videoId, source, body },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ContentTranscriptBody?> GetLatestTranscriptAsync(
        long videoId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<ContentTranscriptBody>(new CommandDefinition(
            GetLatestTranscriptSql,
            new { videoId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertSummarySql,
            new { videoId, body },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
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
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertClipSql,
            new { videoId, timestampS, excerpt, sortOrder },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
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
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertTagSql,
            new { videoId, dimension, tagValue },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> InsertStatedRuleAsync(
        long videoId,
        StatedRuleCandidate rule,
        int sortOrder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            InsertStatedRuleSql,
            new
            {
                videoId,
                rule.Category,
                rule.Metric,
                rule.Value,
                rule.ValueMin,
                rule.ValueMax,
                rule.Comparator,
                rule.Condition,
                clipTs = rule.ClipTimestampSeconds,
                rule.SourceClip,
                rule.Confidence,
                rule.CardReference,
                rule.CardGrounded,
                videoDateUtc = rule.VideoDateUtc,
                sortOrder
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StatedRuleCandidate>> GetStatedRulesBySourceSlugAsync(
        string sourceSlug,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSlug);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rules = await connection.QueryAsync<StatedRuleCandidate>(new CommandDefinition(
            GetStatedRulesBySourceSlugSql,
            new { sourceSlug },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rules.ToList();
    }

    /// <inheritdoc />
    public async Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM content_videos
             WHERE id = @videoId;
            """,
            new { videoId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(youtubeVideoId);

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM content_videos
             WHERE youtube_video_id = @youtubeVideoId;
            """,
            new { youtubeVideoId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllVideosAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM content_videos;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        // Why: enables idempotent clean re-distill before re-inserting generated
        // child rows, avoiding duplicates and content_tags UNIQUE violations.
        await connection.ExecuteAsync(new CommandDefinition(
            ClearDistillOutputSql,
            new { videoId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> GetDistillStatusAsync(long videoId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            GetDistillStatusSql,
            new { videoId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
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
        await connection.ExecuteAsync(new CommandDefinition(
            _connectionInfo.IsPostgres ? PostgresSetDistillStatusSql : SqliteSetDistillStatusSql,
            new { videoId, status, updatedUtc = DateTimeOffset.UtcNow },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
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
            or DistillStatusFailed
            or DistillStatusFiltered;

    private async Task<int> CountByVideoAsync(
        string commandText,
        long videoId,
        CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            commandText,
            new { videoId },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task EnsureFilteredDistillStatusConstraintAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        // Why: filtered-status schema migration is an intentional raw ADO.NET carve-out for this phase.
        if (_connectionInfo.IsSqlite)
        {
            if (await SqliteDistillStatusAllowsFilteredAsync(connection, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await using var rebuild = connection.CreateCommand();
            rebuild.CommandText = """
                DROP TABLE content_distill_status;
                CREATE TABLE content_distill_status (
                  video_id    INTEGER PRIMARY KEY REFERENCES content_videos(id) ON DELETE CASCADE,
                  status      TEXT NOT NULL CHECK (status IN ('distilled','skipped_over_cap','failed','filtered')),
                  updated_utc TEXT NOT NULL DEFAULT (datetime('now'))
                );
                """;
            await rebuild.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var constraintName = await GetPostgresDistillStatusConstraintNameAsync(connection, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(constraintName))
        {
            return;
        }

        var definition = await GetPostgresConstraintDefinitionAsync(connection, constraintName, cancellationToken).ConfigureAwait(false);
        if (definition.Contains("'filtered'", StringComparison.Ordinal))
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"""
            ALTER TABLE content_distill_status
            DROP CONSTRAINT "{constraintName}";
            ALTER TABLE content_distill_status
            ADD CONSTRAINT "{constraintName}"
            CHECK (status IN ('distilled','skipped_over_cap','failed','filtered'));
            """;
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> SqliteDistillStatusAllowsFilteredAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sql
              FROM sqlite_master
             WHERE type = 'table'
               AND name = 'content_distill_status';
            """;
        var sql = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        return sql?.Contains("'filtered'", StringComparison.Ordinal) == true;
    }

    private static async Task<string?> GetPostgresDistillStatusConstraintNameAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT con.conname
              FROM pg_constraint con
              INNER JOIN pg_class rel ON rel.oid = con.conrelid
              INNER JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
             WHERE rel.relname = 'content_distill_status'
               AND con.contype = 'c'
               AND pg_get_constraintdef(con.oid) LIKE '%distilled%';
            """;
        var name = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static async Task<string> GetPostgresConstraintDefinitionAsync(
        DbConnection connection,
        string constraintName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT pg_get_constraintdef(con.oid)
              FROM pg_constraint con
              INNER JOIN pg_class rel ON rel.oid = con.conrelid
              INNER JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
             WHERE rel.relname = 'content_distill_status'
               AND con.conname = @constraintName;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@constraintName", constraintName);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) ?? string.Empty;
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
           -- Why: exclude already-distilled videos so they don't linger in the pending list after
           -- distill/approve/publish. A successful distill writes content_distill_status='distilled'
           -- (ContentKbOrchestrator), the same marker the badge resolver's site_index row coincides
           -- with. 'failed' and 'skipped_over_cap' are intentionally NOT excluded — they are retriable.
           AND NOT EXISTS (
               SELECT 1
                 FROM content_distill_status ds
                WHERE ds.video_id = v.id
                  AND ds.status = 'distilled')
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

    private const string InsertStatedRuleSql = """
        INSERT INTO content_stated_rules (
          video_id,
          category,
          metric,
          value,
          value_min,
          value_max,
          comparator,
          condition,
          clip_ts,
          source_clip,
          confidence,
          card_reference,
          card_grounded,
          video_date_utc,
          sort_order)
        VALUES (
          @videoId,
          @Category,
          @Metric,
          @Value,
          @ValueMin,
          @ValueMax,
          @Comparator,
          @Condition,
          @clipTs,
          @SourceClip,
          @Confidence,
          @CardReference,
          @CardGrounded,
          @videoDateUtc,
          @sortOrder)
        RETURNING id;
        """;

    private const string GetStatedRulesBySourceSlugSql = """
        SELECT sr.category AS Category,
               sr.metric AS Metric,
               sr.value AS Value,
               sr.value_min AS ValueMin,
               sr.value_max AS ValueMax,
               sr.comparator AS Comparator,
               sr.condition AS Condition,
               sr.clip_ts AS ClipTimestampSeconds,
               sr.source_clip AS SourceClip,
               sr.confidence AS Confidence,
               sr.card_reference AS CardReference,
               sr.card_grounded AS CardGrounded,
               sr.video_date_utc AS VideoDateUtc
          FROM content_stated_rules sr
          INNER JOIN content_videos v
                  ON v.id = sr.video_id
          INNER JOIN content_sources s
                  ON s.id = v.source_id
         WHERE s.source_slug = @sourceSlug
         ORDER BY sr.video_id,
                  sr.sort_order;
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
        DELETE FROM content_stated_rules
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
    private const string DistillStatusFiltered = "filtered";

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
          status      TEXT NOT NULL CHECK (status IN ('distilled','skipped_over_cap','failed','filtered')),
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
        CREATE TABLE IF NOT EXISTS content_stated_rules (
          id              BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          video_id        BIGINT NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          category        TEXT NOT NULL,
          metric          TEXT NOT NULL,
          value           DOUBLE PRECISION NULL,
          value_min       DOUBLE PRECISION NULL,
          value_max       DOUBLE PRECISION NULL,
          comparator      TEXT NOT NULL,
          condition       TEXT NULL,
          clip_ts         INT NULL,
          source_clip     TEXT NOT NULL,
          confidence      DOUBLE PRECISION NOT NULL,
          card_reference  TEXT NULL,
          card_grounded   BOOLEAN NULL,
          video_date_utc  TIMESTAMPTZ NULL,
          sort_order      INT NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS ix_content_videos_source_id       ON content_videos(source_id);
        CREATE INDEX IF NOT EXISTS ix_content_transcripts_video_id   ON content_transcripts(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_summaries_video_id     ON content_summaries(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_clips_video_id         ON content_clips(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_tags_video_id          ON content_tags(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_stated_rules_video_id  ON content_stated_rules(video_id);
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
          status      TEXT NOT NULL CHECK (status IN ('distilled','skipped_over_cap','failed','filtered')),
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
        CREATE TABLE IF NOT EXISTS content_stated_rules (
          id              INTEGER PRIMARY KEY AUTOINCREMENT,
          video_id        INTEGER NOT NULL REFERENCES content_videos(id) ON DELETE CASCADE,
          category        TEXT NOT NULL,
          metric          TEXT NOT NULL,
          value           REAL NULL,
          value_min       REAL NULL,
          value_max       REAL NULL,
          comparator      TEXT NOT NULL,
          condition       TEXT NULL,
          clip_ts         INTEGER NULL,
          source_clip     TEXT NOT NULL,
          confidence      REAL NOT NULL,
          card_reference  TEXT NULL,
          card_grounded   INTEGER NULL,
          video_date_utc  TEXT NULL,
          sort_order      INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS ix_content_videos_source_id       ON content_videos(source_id);
        CREATE INDEX IF NOT EXISTS ix_content_transcripts_video_id   ON content_transcripts(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_summaries_video_id     ON content_summaries(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_clips_video_id         ON content_clips(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_tags_video_id          ON content_tags(video_id);
        CREATE INDEX IF NOT EXISTS ix_content_stated_rules_video_id  ON content_stated_rules(video_id);
        """;
}
