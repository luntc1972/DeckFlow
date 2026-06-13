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

            var columns = await GetTableColumnsAsync(connection, "content_site_index", cancellationToken).ConfigureAwait(false);
            if (!columns.Contains("is_visible"))
            {
                await using var addVisible = connection.CreateCommand();
                addVisible.CommandText = _connectionInfo.IsPostgres
                    ? "ALTER TABLE content_site_index ADD COLUMN is_visible BOOLEAN NOT NULL DEFAULT FALSE;"
                    : "ALTER TABLE content_site_index ADD COLUMN is_visible INTEGER NOT NULL DEFAULT 0;";
                await addVisible.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!columns.Contains("is_evergreen"))
            {
                await using var addEvergreen = connection.CreateCommand();
                addEvergreen.CommandText = _connectionInfo.IsPostgres
                    ? "ALTER TABLE content_site_index ADD COLUMN is_evergreen BOOLEAN NOT NULL DEFAULT FALSE;"
                    : "ALTER TABLE content_site_index ADD COLUMN is_evergreen INTEGER NOT NULL DEFAULT 0;";
                await addEvergreen.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!columns.Contains("is_hidden"))
            {
                await using var addHidden = connection.CreateCommand();
                addHidden.CommandText = _connectionInfo.IsPostgres
                    ? "ALTER TABLE content_site_index ADD COLUMN is_hidden BOOLEAN NOT NULL DEFAULT FALSE;"
                    : "ALTER TABLE content_site_index ADD COLUMN is_hidden INTEGER NOT NULL DEFAULT 0;";
                await addHidden.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

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
        RelationalDatabaseConnection.AddParameter(command, "@isHidden", FormatVisibility(row.IsHidden));
        RelationalDatabaseConnection.AddParameter(command, "@isEvergreen", FormatVisibility(row.IsEvergreen));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
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
        command.CommandText = UpsertPreservingVisibilitySql;
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
        RelationalDatabaseConnection.AddParameter(command, "@isVisible", FormatVisibility(false));
        RelationalDatabaseConnection.AddParameter(command, "@isHidden", FormatVisibility(false));
        RelationalDatabaseConnection.AddParameter(command, "@isEvergreen", FormatVisibility(false));
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
                   natural_key_value,
                   is_visible,
                   is_hidden,
                   is_evergreen
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
    {
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
                   natural_key_value,
                   is_visible,
                   is_hidden,
                   is_evergreen
              FROM content_site_index
             WHERE is_visible = @visible
             ORDER BY source, title, id;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@visible", FormatVisibility(true));

        return await ReadRowsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
    {
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
                   natural_key_value,
                   is_visible,
                   is_hidden,
                   is_evergreen
              FROM content_site_index
             ORDER BY source, title, id;
            """;

        return await ReadRowsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
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
                   natural_key_value,
                   is_visible,
                   is_hidden,
                   is_evergreen
              FROM content_site_index
             WHERE id = @id;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return ReadRow(reader);
    }

    /// <inheritdoc />
    public async Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE content_site_index
               SET is_visible = @visible,
                   is_hidden = FALSE
             WHERE id = @id;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@visible", FormatVisibility(visible));
        RelationalDatabaseConnection.AddParameter(command, "@id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE content_site_index
               SET is_hidden = @hidden,
                   is_visible = CASE WHEN @hidden THEN FALSE ELSE is_visible END
             WHERE id = @id;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@hidden", FormatVisibility(hidden));
        RelationalDatabaseConnection.AddParameter(command, "@id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllRowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM content_site_index;
            """;

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM content_site_index
             WHERE id = @id;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets evergreen flag for a single site-index row.
    /// </summary>
    /// <param name="id">Site-index row identifier.</param>
    /// <param name="evergreen">Whether the row should be evergreen.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows updated.</returns>
    public async Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE content_site_index
               SET is_evergreen = @evergreen
             WHERE id = @id;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@evergreen", FormatVisibility(evergreen));
        RelationalDatabaseConnection.AddParameter(command, "@id", id);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE content_site_index
               SET is_visible = @visible,
                   is_hidden = FALSE
             WHERE source = @source;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@visible", FormatVisibility(visible));
        RelationalDatabaseConnection.AddParameter(command, "@source", source);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE content_site_index
               SET is_hidden = @hidden,
                   is_visible = CASE WHEN @hidden THEN FALSE ELSE is_visible END
             WHERE source = @source;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@hidden", FormatVisibility(hidden));
        RelationalDatabaseConnection.AddParameter(command, "@source", source);

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    private static async Task<IReadOnlyList<ContentSiteIndexRow>> ReadRowsAsync(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        var rows = new List<ContentSiteIndexRow>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    private async Task<IReadOnlySet<string>> GetTableColumnsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (_connectionInfo.IsSqlite)
        {
            var command = connection.CreateCommand();
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

        var pgCommand = connection.CreateCommand();
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

    private object FormatVisibility(bool visible)
        => _connectionInfo.IsPostgres ? visible : visible ? 1 : 0;

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
            RssGuid = rssGuid,
            IsVisible = ReadVisibility(reader, 12),
            IsHidden = ReadVisibility(reader, 13),
            IsEvergreen = ReadVisibility(reader, 14)
        };
    }

    private static bool ReadVisibility(DbDataReader reader, int ordinal)
    {
        var raw = reader.GetValue(ordinal);
        return raw switch
        {
            bool b => b,
            long l => l != 0,
            int i => i != 0,
            short s => s != 0,
            string text => text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase),
            _ => Convert.ToInt64(raw, CultureInfo.InvariantCulture) != 0
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
          natural_key_value,
          is_hidden,
          is_evergreen)
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
          @naturalKeyValue,
          @isHidden,
          @isEvergreen)
        ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE SET
          source             = EXCLUDED.source,
          title              = EXCLUDED.title,
          video_url          = EXCLUDED.video_url,
          artifact_path      = EXCLUDED.artifact_path,
          published_utc      = EXCLUDED.published_utc,
          indexed_utc        = EXCLUDED.indexed_utc,
          archetype_tags     = EXCLUDED.archetype_tags,
          bracket_tags       = EXCLUDED.bracket_tags,
          card_category_tags = EXCLUDED.card_category_tags,
          is_hidden          = EXCLUDED.is_hidden,
          is_evergreen       = EXCLUDED.is_evergreen;
        """;

    private const string UpsertPreservingVisibilitySql = """
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
          natural_key_value,
          is_visible,
          is_hidden,
          is_evergreen)
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
          @naturalKeyValue,
          @isVisible,
          @isHidden,
          @isEvergreen)
        ON CONFLICT (natural_key_type, natural_key_value) DO UPDATE SET
          source             = EXCLUDED.source,
          title              = EXCLUDED.title,
          video_url          = EXCLUDED.video_url,
          artifact_path      = EXCLUDED.artifact_path,
          published_utc      = EXCLUDED.published_utc,
          indexed_utc        = EXCLUDED.indexed_utc,
          archetype_tags     = EXCLUDED.archetype_tags,
          bracket_tags       = EXCLUDED.bracket_tags,
          card_category_tags = EXCLUDED.card_category_tags,
          is_visible         = content_site_index.is_visible,
          is_hidden          = content_site_index.is_hidden,
          is_evergreen       = content_site_index.is_evergreen;
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
          is_visible         BOOLEAN NOT NULL DEFAULT FALSE,
          is_hidden          BOOLEAN NOT NULL DEFAULT FALSE,
          is_evergreen       BOOLEAN NOT NULL DEFAULT FALSE,
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
          is_visible         INTEGER NOT NULL DEFAULT 0,
          is_hidden          INTEGER NOT NULL DEFAULT 0,
          is_evergreen       INTEGER NOT NULL DEFAULT 0,
          UNIQUE (natural_key_type, natural_key_value)
        );
        """;
}
