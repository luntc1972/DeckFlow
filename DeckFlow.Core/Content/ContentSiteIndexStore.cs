using System.Data.Common;
using System.Globalization;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Content;

/// <summary>
/// Default implementation of <see cref="IContentSiteIndexStore"/> for the only Render-bound
/// Content KB table; it carries no transcript, audio, or spend data.
/// </summary>
public sealed class ContentSiteIndexStore : IContentSiteIndexStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    /// <summary>
    /// Creates a SQLite-backed site-index store using the file at <paramref name="databasePath"/>.
    /// </summary>
    /// <param name="databasePath">Path to the SQLite file.</param>
    public ContentSiteIndexStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    /// <summary>
    /// Creates a site-index store using the supplied <see cref="RelationalDatabaseConnection"/>.
    /// </summary>
    /// <param name="connectionInfo">Provider + connection string descriptor.</param>
    public ContentSiteIndexStore(RelationalDatabaseConnection connectionInfo)
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
    public async Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentException.ThrowIfNullOrWhiteSpace(row.Source);
        ArgumentException.ThrowIfNullOrWhiteSpace(row.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(row.VideoUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(row.ArtifactPath);
        ArgumentNullException.ThrowIfNull(row.ArchetypeTags);
        ArgumentNullException.ThrowIfNull(row.BracketTags);
        ArgumentNullException.ThrowIfNull(row.CardCategoryTags);

        var naturalKey = GetNaturalKey(row);
        ValidateArtifactPath(row.ArtifactPath);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = UpsertSql;
        RelationalDatabaseConnection.AddParameter(command, "@source", row.Source);
        RelationalDatabaseConnection.AddParameter(command, "@title", row.Title);
        RelationalDatabaseConnection.AddParameter(command, "@videoUrl", row.VideoUrl);
        RelationalDatabaseConnection.AddParameter(command, "@artifactPath", row.ArtifactPath);
        RelationalDatabaseConnection.AddParameter(command, "@publishedUtc", FormatTimestamp(row.PublishedUtc));
        RelationalDatabaseConnection.AddParameter(command, "@indexedUtc", FormatTimestamp(row.IndexedUtc));
        RelationalDatabaseConnection.AddParameter(command, "@archetypeTags", ContentArtifactSpec.SerializeTags(row.ArchetypeTags));
        RelationalDatabaseConnection.AddParameter(command, "@bracketTags", ContentArtifactSpec.SerializeTags(row.BracketTags));
        RelationalDatabaseConnection.AddParameter(command, "@cardCategoryTags", ContentArtifactSpec.SerializeTags(row.CardCategoryTags));
        RelationalDatabaseConnection.AddParameter(command, "@naturalKeyType", naturalKey.Type);
        RelationalDatabaseConnection.AddParameter(command, "@naturalKeyValue", naturalKey.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(
        string naturalKeyType,
        string naturalKeyValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyValue);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,
                   source,
                   title,
                   video_url,
                   artifact_path,
                   published_utc,
                   indexed_utc,
                   archetype_tags,
                   bracket_tags,
                   card_category_tags,
                   natural_key_type,
                   natural_key_value
              FROM content_site_index
             WHERE natural_key_type = @naturalKeyType
               AND natural_key_value = @naturalKeyValue;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@naturalKeyType", naturalKeyType);
        RelationalDatabaseConnection.AddParameter(command, "@naturalKeyValue", naturalKeyValue);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadRow(reader);
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

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

    private static (string Type, string Value) GetNaturalKey(ContentSiteIndexRow row)
    {
        var hasYoutubeVideoId = !string.IsNullOrWhiteSpace(row.YoutubeVideoId);
        var hasRssGuid = !string.IsNullOrWhiteSpace(row.RssGuid);
        if (hasYoutubeVideoId == hasRssGuid)
        {
            throw new ArgumentException(
                "Exactly one of YoutubeVideoId or RssGuid must be supplied for a content site-index row.",
                nameof(row));
        }

        return hasYoutubeVideoId
            ? (ContentSourceType.Youtube, row.YoutubeVideoId!)
            : (ContentSourceType.Podcast, row.RssGuid!);
    }

    private static void ValidateArtifactPath(string artifactPath)
    {
        // Why: REVIEW #5 requires rejecting traversal or rooted paths before later phases
        // resolve this relative pointer against MTG_DATA_DIR.
        if (Path.IsPathRooted(artifactPath) || IsWindowsRootedPath(artifactPath))
        {
            throw new ArgumentException(
                "Artifact path must be relative.",
                nameof(ContentSiteIndexRow.ArtifactPath));
        }

        var segments = artifactPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Artifact path must not contain '..' path segments.",
                nameof(ContentSiteIndexRow.ArtifactPath));
        }
    }

    private static bool IsWindowsRootedPath(string artifactPath)
        => artifactPath.Length >= 3
            && char.IsLetter(artifactPath[0])
            && artifactPath[1] == ':'
            && (artifactPath[2] == '\\' || artifactPath[2] == '/');

    private static ContentSiteIndexRow ReadRow(DbDataReader reader)
    {
        var naturalKeyType = reader.GetString(10);
        var naturalKeyValue = reader.GetString(11);
        var youtubeVideoId = naturalKeyType == ContentSourceType.Youtube ? naturalKeyValue : null;
        var rssGuid = naturalKeyType == ContentSourceType.Podcast ? naturalKeyValue : null;

        if (youtubeVideoId is null && rssGuid is null)
        {
            throw new InvalidOperationException($"Unknown content_site_index.natural_key_type value '{naturalKeyType}'.");
        }

        return new ContentSiteIndexRow
        {
            Id = reader.GetInt64(0),
            Source = reader.GetString(1),
            Title = reader.GetString(2),
            VideoUrl = reader.GetString(3),
            ArtifactPath = reader.GetString(4),
            PublishedUtc = reader.IsDBNull(5) ? null : ReadDateTimeOffset(reader, 5),
            IndexedUtc = ReadDateTimeOffset(reader, 6),
            ArchetypeTags = ContentArtifactSpec.DeserializeTags(reader.GetString(7)),
            BracketTags = ContentArtifactSpec.DeserializeTags(reader.GetString(8)),
            CardCategoryTags = ContentArtifactSpec.DeserializeTags(reader.GetString(9)),
            YoutubeVideoId = youtubeVideoId,
            RssGuid = rssGuid
        };
    }

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

    private const string UpsertSql = """
        INSERT INTO content_site_index (
          source,
          title,
          video_url,
          artifact_path,
          published_utc,
          indexed_utc,
          archetype_tags,
          bracket_tags,
          card_category_tags,
          natural_key_type,
          natural_key_value)
        VALUES (
          @source,
          @title,
          @videoUrl,
          @artifactPath,
          @publishedUtc,
          @indexedUtc,
          @archetypeTags,
          @bracketTags,
          @cardCategoryTags,
          @naturalKeyType,
          @naturalKeyValue)
        ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE SET
          source             = EXCLUDED.source,
          title              = EXCLUDED.title,
          video_url          = EXCLUDED.video_url,
          artifact_path      = EXCLUDED.artifact_path,
          published_utc      = EXCLUDED.published_utc,
          indexed_utc        = EXCLUDED.indexed_utc,
          archetype_tags     = EXCLUDED.archetype_tags,
          bracket_tags       = EXCLUDED.bracket_tags,
          card_category_tags = EXCLUDED.card_category_tags;
        """;

    private const string PostgresCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_site_index (
          id                 BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
          source             TEXT NOT NULL,
          title              TEXT NOT NULL,
          video_url          TEXT NOT NULL,
          artifact_path      TEXT NOT NULL,
          published_utc      TIMESTAMPTZ NULL,
          indexed_utc        TIMESTAMPTZ NOT NULL DEFAULT now(),
          archetype_tags     TEXT NOT NULL DEFAULT '[]',
          bracket_tags       TEXT NOT NULL DEFAULT '[]',
          card_category_tags TEXT NOT NULL DEFAULT '[]',
          natural_key_type   TEXT NOT NULL CHECK (natural_key_type IN ('youtube_channel','podcast_rss')),
          natural_key_value  TEXT NOT NULL,
          UNIQUE (natural_key_type, natural_key_value)
        );
        """;

    private const string SqliteCreateTableSql = """
        CREATE TABLE IF NOT EXISTS content_site_index (
          id                 INTEGER PRIMARY KEY AUTOINCREMENT,
          source             TEXT NOT NULL,
          title              TEXT NOT NULL,
          video_url          TEXT NOT NULL,
          artifact_path      TEXT NOT NULL,
          published_utc      TEXT NULL,
          indexed_utc        TEXT NOT NULL DEFAULT (datetime('now')),
          archetype_tags     TEXT NOT NULL DEFAULT '[]',
          bracket_tags       TEXT NOT NULL DEFAULT '[]',
          card_category_tags TEXT NOT NULL DEFAULT '[]',
          natural_key_type   TEXT NOT NULL CHECK (natural_key_type IN ('youtube_channel','podcast_rss')),
          natural_key_value  TEXT NOT NULL,
          UNIQUE (natural_key_type, natural_key_value)
        );
        """;
}
