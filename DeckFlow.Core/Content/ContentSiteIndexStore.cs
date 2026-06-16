using System.Data.Common;
using System.Globalization;
using Dapper;
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
            // Why: schema creation, ALTER backfills, and schema introspection are intentional raw ADO.NET carve-outs for this phase.
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

            if (!columns.Contains("approval_status"))
            {
                await using var addApprovalStatus = connection.CreateCommand();
                addApprovalStatus.CommandText =
                    "ALTER TABLE content_site_index ADD COLUMN approval_status TEXT NOT NULL DEFAULT 'pending';";
                await addApprovalStatus.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Why: grandfather the already-published seed to approved and re-run safely after
            // an ALTER-then-crash; only still-pending visible rows are updated on later passes.
            await using (var grandfatherApprovalStatus = connection.CreateCommand())
            {
                grandfatherApprovalStatus.CommandText = """
                    UPDATE content_site_index
                       SET approval_status = 'approved'
                     WHERE approval_status = 'pending'
                       AND is_visible = @visible;
                    """;
                RelationalDatabaseConnection.AddParameter(grandfatherApprovalStatus, "@visible", true);
                await grandfatherApprovalStatus.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
        await connection.ExecuteAsync(new CommandDefinition(
            UpsertSql,
            new
            {
                source = row.Source,
                title = row.Title,
                videoUrl = row.VideoUrl,
                artifactPath = row.ArtifactPath,
                publishedUtc = row.PublishedUtc,
                indexedUtc = row.IndexedUtc,
                archetypeTags = ContentArtifactSpec.SerializeTags(row.ArchetypeTags),
                bracketTags = ContentArtifactSpec.SerializeTags(row.BracketTags),
                cardCategoryTags = ContentArtifactSpec.SerializeTags(row.CardCategoryTags),
                naturalKeyType = naturalKey.Type,
                naturalKeyValue = naturalKey.Value,
                isHidden = row.IsHidden,
                isEvergreen = row.IsEvergreen
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
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
        await connection.ExecuteAsync(new CommandDefinition(
            UpsertPreservingVisibilitySql,
            new
            {
                source = row.Source,
                title = row.Title,
                videoUrl = row.VideoUrl,
                artifactPath = row.ArtifactPath,
                publishedUtc = row.PublishedUtc,
                indexedUtc = row.IndexedUtc,
                archetypeTags = ContentArtifactSpec.SerializeTags(row.ArchetypeTags),
                bracketTags = ContentArtifactSpec.SerializeTags(row.BracketTags),
                cardCategoryTags = ContentArtifactSpec.SerializeTags(row.CardCategoryTags),
                naturalKeyType = naturalKey.Type,
                naturalKeyValue = naturalKey.Value,
                isVisible = false,
                isHidden = false,
                isEvergreen = false
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
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
        await connection.ExecuteAsync(new CommandDefinition(
            UpsertContentColumnsOnlySql,
            new
            {
                source = row.Source,
                title = row.Title,
                videoUrl = row.VideoUrl,
                artifactPath = row.ArtifactPath,
                publishedUtc = row.PublishedUtc,
                indexedUtc = row.IndexedUtc,
                archetypeTags = ContentArtifactSpec.SerializeTags(row.ArchetypeTags),
                bracketTags = ContentArtifactSpec.SerializeTags(row.BracketTags),
                cardCategoryTags = ContentArtifactSpec.SerializeTags(row.CardCategoryTags),
                naturalKeyType = naturalKey.Type,
                naturalKeyValue = naturalKey.Value
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
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
        var row = await connection.QuerySingleOrDefaultAsync<ContentSiteIndexRowData>(new CommandDefinition(
            """
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
                   is_evergreen,
                   approval_status
              FROM content_site_index
             WHERE natural_key_type = @naturalKeyType
               AND natural_key_value = @naturalKeyValue;
            """,
            new { naturalKeyType, naturalKeyValue },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : ToContentSiteIndexRow(row);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ContentSiteIndexRowData>(new CommandDefinition(
            """
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
                   is_evergreen,
                   approval_status
              FROM content_site_index
             WHERE is_visible = @visible
             ORDER BY source, title, id;
            """,
            new { visible = true },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToContentSiteIndexRow).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ContentSiteIndexRowData>(new CommandDefinition(
            """
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
                   is_evergreen,
                   approval_status
              FROM content_site_index
             WHERE approval_status = 'approved'
             ORDER BY source, title, id;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToContentSiteIndexRow).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<ContentSiteIndexRowData>(new CommandDefinition(
            """
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
                   is_evergreen,
                   approval_status
              FROM content_site_index
             ORDER BY source, title, id;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.Select(ToContentSiteIndexRow).ToList();
    }

    /// <inheritdoc />
    public async Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var row = await connection.QuerySingleOrDefaultAsync<ContentSiteIndexRowData>(new CommandDefinition(
            """
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
                   is_evergreen,
                   approval_status
              FROM content_site_index
             WHERE id = @id;
            """,
            new { id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        return row is null ? null : ToContentSiteIndexRow(row);
    }

    /// <inheritdoc />
    public async Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET is_visible = @visible,
                   is_hidden = FALSE
             WHERE id = @id;
            """,
            new { visible, id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET is_hidden = @hidden,
                   is_visible = CASE WHEN @hidden THEN FALSE ELSE is_visible END
             WHERE id = @id;
            """,
            new { hidden, id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllRowsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM content_site_index;
            """,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM content_site_index
             WHERE id = @id;
            """,
            new { id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
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
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET is_evergreen = @evergreen
             WHERE id = @id;
            """,
            new { evergreen, id },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET is_visible = @visible,
                   is_hidden = FALSE
             WHERE source = @source;
            """,
            new { visible, source },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET is_hidden = @hidden,
                   is_visible = CASE WHEN @hidden THEN FALSE ELSE is_visible END
             WHERE source = @source;
            """,
            new { hidden, source },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetApprovalStatusAsync(
        string naturalKeyType,
        string naturalKeyValue,
        string status,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyType);
        ArgumentException.ThrowIfNullOrWhiteSpace(naturalKeyValue);
        ValidateApprovalStatus(status);
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE content_site_index
               SET approval_status = @status
             WHERE natural_key_type = @type
               AND natural_key_value = @value;
            """,
            new { status, type = naturalKeyType, value = naturalKeyValue },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> SetApprovalStatusAsync(
        IReadOnlyList<(string Type, string Value)> keys,
        string status,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ValidateApprovalStatus(status);
        if (keys.Count == 0)
        {
            return 0;
        }

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        // Why: one transaction = atomic + one logical round-trip (D-06); partial approvals are forbidden.
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        const string sql = """
            UPDATE content_site_index
               SET approval_status = @status
             WHERE natural_key_type = @type
               AND natural_key_value = @value;
            """;
        var total = 0;
        foreach (var (type, value) in keys)
        {
            total += await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new { status, type, value },
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return total;
    }

    private static readonly string[] AllowedApprovalStatuses = ["pending", "approved", "rejected"];

    private static void ValidateApprovalStatus(string status)
    {
        if (!AllowedApprovalStatuses.Contains(status, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                $"Invalid approval status '{status}'. Must be one of: pending, approved, rejected.",
                nameof(status));
        }
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        => await _connectionInfo.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

    private async Task<IReadOnlySet<string>> GetTableColumnsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        // Why: PRAGMA/information_schema schema introspection is an intentional raw ADO.NET carve-out for this phase.
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

    private static ContentSiteIndexRow ToContentSiteIndexRow(ContentSiteIndexRowData row)
    {
        var naturalKeyType = row.NaturalKeyType;
        var naturalKeyValue = row.NaturalKeyValue;
        var youtubeVideoId = naturalKeyType == ContentSourceType.Youtube ? naturalKeyValue : null;
        var rssGuid = naturalKeyType == ContentSourceType.Podcast ? naturalKeyValue : null;

        if (youtubeVideoId is null && rssGuid is null)
        {
            throw new InvalidOperationException($"Unknown content_site_index.natural_key_type value '{naturalKeyType}'.");
        }

        return new ContentSiteIndexRow
        {
            Id = row.Id,
            Source = row.Source,
            Title = row.Title,
            VideoUrl = row.VideoUrl,
            ArtifactPath = row.ArtifactPath,
            PublishedUtc = row.PublishedUtc,
            IndexedUtc = row.IndexedUtc,
            ArchetypeTags = ContentArtifactSpec.DeserializeTags(row.ArchetypeTags),
            BracketTags = ContentArtifactSpec.DeserializeTags(row.BracketTags),
            CardCategoryTags = ContentArtifactSpec.DeserializeTags(row.CardCategoryTags),
            YoutubeVideoId = youtubeVideoId,
            RssGuid = rssGuid,
            IsVisible = row.IsVisible,
            IsHidden = row.IsHidden,
            IsEvergreen = row.IsEvergreen,
            ApprovalStatus = row.ApprovalStatus
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

    private const string UpsertContentColumnsOnlySql = """
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
          approval_status)
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
          'pending')
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
        -- is_visible, is_hidden, is_evergreen, approval_status are intentionally absent here.
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
          approval_status    TEXT NOT NULL DEFAULT 'pending',
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
          approval_status    TEXT NOT NULL DEFAULT 'pending',
          UNIQUE (natural_key_type, natural_key_value)
        );
        """;

    private sealed class ContentSiteIndexRowData
    {
        public long Id { get; init; }
        public required string Source { get; init; }
        public required string Title { get; init; }
        public required string VideoUrl { get; init; }
        public required string ArtifactPath { get; init; }
        public DateTimeOffset? PublishedUtc { get; init; }
        public DateTimeOffset IndexedUtc { get; init; }
        public required string ArchetypeTags { get; init; }
        public required string BracketTags { get; init; }
        public required string CardCategoryTags { get; init; }
        public required string NaturalKeyType { get; init; }
        public required string NaturalKeyValue { get; init; }
        public bool IsVisible { get; init; }
        public bool IsHidden { get; init; }
        public bool IsEvergreen { get; init; }
        public required string ApprovalStatus { get; init; }
    }
}
