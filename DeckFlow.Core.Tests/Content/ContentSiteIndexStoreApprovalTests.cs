using System.IO;
using System.Reflection;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Approval-status integration tests for <see cref="ContentSiteIndexStore"/> using per-fact SQLite files.
/// Postgres migration column-presence coverage is deferred because CI for this suite is SQLite-only.
/// </summary>
public sealed class ContentSiteIndexStoreApprovalTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSiteIndexStore _store;

    public ContentSiteIndexStoreApprovalTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-approval-{Guid.NewGuid():N}.db");
        _store = new ContentSiteIndexStore(_dbPath);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task EnsureSchemaAsync_AddsApprovalStatusColumn_ToLegacySchema()
    {
        await CreateLegacySchemaAsync(CreateLegacySeed("yt-legacy-column", isVisible: false));
        Assert.False(await ColumnExistsAsync("approval_status"));

        await _store.EnsureSchemaAsync();

        Assert.True(await ColumnExistsAsync("approval_status"));
    }

    [Fact]
    public async Task EnsureSchemaAsync_Grandfather_SetsApprovedForVisibleRows_PendingForOthers()
    {
        await CreateLegacySchemaAsync(
            CreateLegacySeed("yt-grandfather-visible", isVisible: true),
            CreateLegacySeed("yt-grandfather-pending", isVisible: false));

        await _store.EnsureSchemaAsync();

        var visible = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-grandfather-visible");
        var pending = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-grandfather-pending");

        Assert.NotNull(visible);
        Assert.NotNull(pending);
        Assert.Equal("approved", visible!.ApprovalStatus);
        Assert.Equal("pending", pending!.ApprovalStatus);
    }

    [Fact]
    public async Task EnsureSchemaAsync_Grandfather_DoesNotRestampOperatorChangedStatus()
    {
        await CreateLegacySchemaAsync(CreateLegacySeed("yt-no-restamp", isVisible: true));

        await _store.EnsureSchemaAsync();
        await SetApprovalStatusAsync("yt-no-restamp", "rejected");

        var store2 = new ContentSiteIndexStore(_dbPath);
        await store2.EnsureSchemaAsync();

        var row = await store2.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-no-restamp");

        Assert.NotNull(row);
        Assert.Equal("rejected", row!.ApprovalStatus);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_NewRow_LandsAsPending()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-new-pending"));

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-new-pending");

        Assert.NotNull(row);
        Assert.Equal("pending", row!.ApprovalStatus);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_ExistingRow_PreservesApprovalStatus()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-preserve-approval", title: "Original"));
        await SetApprovalStatusAsync("yt-preserve-approval", "approved");

        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow(
            "yt-preserve-approval",
            title: "Updated",
            artifactPath: "content-kb/command-zone/yt-preserve-approval-v2.md"));

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve-approval");

        Assert.NotNull(row);
        Assert.Equal("approved", row!.ApprovalStatus);
        Assert.Equal("Updated", row.Title);
        Assert.Equal("content-kb/command-zone/yt-preserve-approval-v2.md", row.ArtifactPath);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_PreservesVisibleEvergreenApprovedFields()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-preserve-admin-visible"));
        var inserted = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve-admin-visible");
        Assert.NotNull(inserted);

        Assert.Equal(1, await _store.SetVisibilityAsync(inserted!.Id, visible: true));
        Assert.Equal(1, await _store.SetEvergreenAsync(inserted.Id, evergreen: true));
        await SetApprovalStatusAsync("yt-preserve-admin-visible", "approved");

        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow(
            "yt-preserve-admin-visible",
            title: "Updated admin title",
            artifactPath: "content-kb/command-zone/yt-preserve-admin-visible-v2.md"));

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve-admin-visible");

        Assert.NotNull(row);
        Assert.True(row!.IsVisible);
        Assert.False(row.IsHidden);
        Assert.True(row.IsEvergreen);
        Assert.Equal("approved", row.ApprovalStatus);
        Assert.Equal("Updated admin title", row.Title);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_PreservesHiddenRow()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-preserve-hidden"));
        var inserted = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve-hidden");
        Assert.NotNull(inserted);

        Assert.Equal(1, await _store.SetHiddenAsync(inserted!.Id, hidden: true));
        await SetApprovalStatusAsync("yt-preserve-hidden", "approved");

        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow(
            "yt-preserve-hidden",
            title: "Updated hidden title",
            artifactPath: "content-kb/command-zone/yt-preserve-hidden-v2.md"));

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve-hidden");

        Assert.NotNull(row);
        Assert.True(row!.IsHidden);
        Assert.False(row.IsVisible);
        Assert.Equal("approved", row.ApprovalStatus);
        Assert.Equal("Updated hidden title", row.Title);
    }

    [Fact]
    public async Task GetApprovedRowsAsync_ReturnsOnlyApprovedRows()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-approved-only"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-still-pending"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-rejected"));

        await SetApprovalStatusAsync("yt-approved-only", "approved");
        await SetApprovalStatusAsync("yt-still-pending", "pending");
        await SetApprovalStatusAsync("yt-rejected", "rejected");

        var rows = await _store.GetApprovedRowsAsync();

        var row = Assert.Single(rows);
        Assert.Equal("yt-approved-only", row.YoutubeVideoId);
        Assert.Equal("approved", row.ApprovalStatus);
    }

    [Fact]
    public async Task ApprovalStatusColumn_DefaultsToPending_WhenInsertedWithoutExplicitStatus()
    {
        await _store.EnsureSchemaAsync();
        await InsertRowWithoutApprovalStatusAsync("yt-ddl-default");

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-ddl-default");

        Assert.NotNull(row);
        Assert.Equal("pending", row!.ApprovalStatus);
    }

    [Fact]
    public void CreateTableDdl_IncludesApprovalStatusDefault()
    {
        var postgres = GetPrivateSql("PostgresCreateTableSql");
        var sqlite = GetPrivateSql("SqliteCreateTableSql");

        Assert.Contains("approval_status", postgres, StringComparison.Ordinal);
        Assert.Contains("TEXT NOT NULL DEFAULT 'pending'", postgres, StringComparison.Ordinal);
        Assert.Contains("approval_status", sqlite, StringComparison.Ordinal);
        Assert.Contains("TEXT NOT NULL DEFAULT 'pending'", sqlite, StringComparison.Ordinal);
    }

    private async Task CreateLegacySchemaAsync(params LegacySeed[] rows)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync()
            .ConfigureAwait(false);
        await using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE content_site_index (
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
        await create.ExecuteNonQueryAsync().ConfigureAwait(false);

        foreach (var row in rows)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = """
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
                  @isEvergreen);
                """;
            RelationalDatabaseConnection.AddParameter(insert, "@source", row.Source);
            RelationalDatabaseConnection.AddParameter(insert, "@title", row.Title);
            RelationalDatabaseConnection.AddParameter(insert, "@videoUrl", row.VideoUrl);
            RelationalDatabaseConnection.AddParameter(insert, "@artifactPath", row.ArtifactPath);
            RelationalDatabaseConnection.AddParameter(insert, "@publishedUtc", row.PublishedUtc);
            RelationalDatabaseConnection.AddParameter(insert, "@indexedUtc", row.IndexedUtc);
            RelationalDatabaseConnection.AddParameter(insert, "@archetypeTags", row.ArchetypeTagsJson);
            RelationalDatabaseConnection.AddParameter(insert, "@bracketTags", row.BracketTagsJson);
            RelationalDatabaseConnection.AddParameter(insert, "@cardCategoryTags", row.CardCategoryTagsJson);
            RelationalDatabaseConnection.AddParameter(insert, "@naturalKeyType", ContentSourceType.Youtube);
            RelationalDatabaseConnection.AddParameter(insert, "@naturalKeyValue", row.YoutubeVideoId);
            RelationalDatabaseConnection.AddParameter(insert, "@isVisible", row.IsVisible ? 1 : 0);
            RelationalDatabaseConnection.AddParameter(insert, "@isHidden", row.IsHidden ? 1 : 0);
            RelationalDatabaseConnection.AddParameter(insert, "@isEvergreen", row.IsEvergreen ? 1 : 0);
            await insert.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    private async Task SetApprovalStatusAsync(string youtubeVideoId, string approvalStatus)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync()
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE content_site_index
               SET approval_status = @approvalStatus
             WHERE natural_key_type = @naturalKeyType
               AND natural_key_value = @naturalKeyValue;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@approvalStatus", approvalStatus);
        RelationalDatabaseConnection.AddParameter(command, "@naturalKeyType", ContentSourceType.Youtube);
        RelationalDatabaseConnection.AddParameter(command, "@naturalKeyValue", youtubeVideoId);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task InsertRowWithoutApprovalStatusAsync(string youtubeVideoId)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync()
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
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
              @naturalKeyValue);
            """;
        RelationalDatabaseConnection.AddParameter(command, "@source", "Default Source");
        RelationalDatabaseConnection.AddParameter(command, "@title", $"Video {youtubeVideoId}");
        RelationalDatabaseConnection.AddParameter(command, "@videoUrl", $"https://www.youtube.com/watch?v={youtubeVideoId}");
        RelationalDatabaseConnection.AddParameter(command, "@artifactPath", $"content-kb/command-zone/{youtubeVideoId}.md");
        RelationalDatabaseConnection.AddParameter(command, "@publishedUtc", "2026-05-26T12:00:00.0000000Z");
        RelationalDatabaseConnection.AddParameter(command, "@indexedUtc", "2026-05-26T13:00:00.0000000Z");
        RelationalDatabaseConnection.AddParameter(command, "@archetypeTags", "[\"combo\"]");
        RelationalDatabaseConnection.AddParameter(command, "@bracketTags", "[\"cEDH\"]");
        RelationalDatabaseConnection.AddParameter(command, "@cardCategoryTags", "[\"win-cons\"]");
        RelationalDatabaseConnection.AddParameter(command, "@naturalKeyType", ContentSourceType.Youtube);
        RelationalDatabaseConnection.AddParameter(command, "@naturalKeyValue", youtubeVideoId);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task<bool> ColumnExistsAsync(string columnName)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync()
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(content_site_index);";
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            if (!reader.IsDBNull(1) && string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetPrivateSql(string fieldName)
    {
        var field = typeof(ContentSiteIndexStore).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return Assert.IsType<string>(field!.GetRawConstantValue());
    }

    private static LegacySeed CreateLegacySeed(
        string youtubeVideoId,
        bool isVisible,
        bool isHidden = false,
        bool isEvergreen = false,
        string? source = null,
        string? title = null)
        => new(
            YoutubeVideoId: youtubeVideoId,
            Source: source ?? "Legacy Source",
            Title: title ?? $"Legacy {youtubeVideoId}",
            VideoUrl: $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath: $"content-kb/legacy-source/{youtubeVideoId}.md",
            PublishedUtc: "2026-05-26T12:00:00.0000000Z",
            IndexedUtc: "2026-05-26T13:00:00.0000000Z",
            ArchetypeTagsJson: "[\"combo\"]",
            BracketTagsJson: "[\"cEDH\"]",
            CardCategoryTagsJson: "[\"win-cons\"]",
            IsVisible: isVisible,
            IsHidden: isHidden,
            IsEvergreen: isEvergreen);

    private static ContentSiteIndexRow CreateYoutubeRow(
        string youtubeVideoId,
        string? title = null,
        string? artifactPath = null,
        string? source = null,
        IReadOnlyList<string>? archetypeTags = null)
        => new()
        {
            Id = 0,
            Source = source ?? "The Command Zone",
            Title = title ?? $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = artifactPath ?? $"content-kb/command-zone/{youtubeVideoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = archetypeTags ?? new[] { "combo", "control" },
            BracketTags = new[] { "cEDH", "Optimized" },
            CardCategoryTags = new[] { "win-cons", "counter" },
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null
        };

    private sealed record LegacySeed(
        string YoutubeVideoId,
        string Source,
        string Title,
        string VideoUrl,
        string ArtifactPath,
        string PublishedUtc,
        string IndexedUtc,
        string ArchetypeTagsJson,
        string BracketTagsJson,
        string CardCategoryTagsJson,
        bool IsVisible,
        bool IsHidden,
        bool IsEvergreen);
}
