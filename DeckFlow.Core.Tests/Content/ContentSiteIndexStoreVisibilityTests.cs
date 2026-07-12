using System.IO;
using System.Reflection;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Visibility-focused integration tests for <see cref="ContentSiteIndexStore"/> using per-fact SQLite files.
/// </summary>
public sealed class ContentSiteIndexStoreVisibilityTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSiteIndexStore _store;

    public ContentSiteIndexStoreVisibilityTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-visibility-{Guid.NewGuid():N}.db");
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
    public async Task UpsertRowPreservingVisibilityAsync_NewRowsAreHiddenAndAllRowsIncludesThem()
    {
        var hiddenRow = CreateYoutubeRow("yt-hidden", approvalStatus: "approved") with { IsVisible = true, IsHidden = true };
        await _store.UpsertRowPreservingVisibilityAsync(hiddenRow);

        var allRows = await _store.GetAllRowsAsync();
        var publishedRows = await _store.GetPublishedRowsAsync();

        var row = Assert.Single(allRows);
        Assert.False(row.IsVisible);
        Assert.False(row.IsHidden);
        Assert.Equal("approved", row.ApprovalStatus);
        Assert.Empty(publishedRows);
    }

    [Fact]
    public async Task Upsert_PreservesIsVisible_OnExistingPublishedRow()
    {
        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow(
            "yt-preserve",
            title: "Original title",
            approvalStatus: "approved"));
        var inserted = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve");
        Assert.NotNull(inserted);
        Assert.Equal(1, await _store.SetVisibilityAsync(inserted!.Id, visible: true));

        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow(
            "yt-preserve",
            title: "Updated title",
            artifactPath: "content-kb/command-zone/yt-preserve-v2.md",
            archetypeTags: new[] { "stax" },
            approvalStatus: "approved"));

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve");

        Assert.NotNull(row);
        Assert.True(row!.IsVisible);
        Assert.False(row.IsHidden);
        Assert.Equal("approved", row.ApprovalStatus);
        Assert.Equal("Updated title", row.Title);
        Assert.Equal("content-kb/command-zone/yt-preserve-v2.md", row.ArtifactPath);
        Assert.Equal(new[] { "stax" }, row.ArchetypeTags);
    }

    [Fact]
    public async Task UpsertRowPreservingVisibilityAsync_OverwritesApprovalStatus_ButPreservesVisibility()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow(
            "yt-heal-pending",
            title: "Pending original",
            approvalStatus: "pending"));
        var inserted = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-heal-pending");
        Assert.NotNull(inserted);
        Assert.Equal(1, await _store.SetVisibilityAsync(inserted!.Id, visible: true));

        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow(
            "yt-heal-pending",
            title: "Approved update",
            approvalStatus: "approved"));

        var healed = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-heal-pending");

        Assert.NotNull(healed);
        Assert.True(healed!.IsVisible);
        Assert.False(healed.IsHidden);
        Assert.Equal("approved", healed.ApprovalStatus);
        Assert.Equal("Approved update", healed.Title);
    }

    [Fact]
    public async Task GetPublishedRowsAsync_FiltersHiddenRows_AndGetByIdReturnsVisibility()
    {
        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow("yt-hidden"));
        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow("yt-visible"));
        var visible = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-visible");
        Assert.NotNull(visible);
        Assert.Equal(1, await _store.SetVisibilityAsync(visible!.Id, visible: true));
        // The browse serve query now requires approval_status='approved'; approve the row we expect published.
        await _store.SetApprovalStatusAsync(ContentSourceType.Youtube, "yt-visible", "approved");

        var publishedRows = await _store.GetPublishedRowsAsync();
        var allRows = await _store.GetAllRowsAsync();
        var byId = await _store.GetByIdAsync(visible.Id);

        var published = Assert.Single(publishedRows);
        Assert.Equal("yt-visible", published.YoutubeVideoId);
        Assert.Equal(2, allRows.Count);
        Assert.NotNull(byId);
        Assert.True(byId!.IsVisible);
        Assert.False(byId.IsHidden);
    }

    [Fact]
    public async Task SetVisibilityAsync_AndSetVisibilityBySourceAsync_ReturnAffectedCounts()
    {
        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow("yt-source-a-1", source: "Source A"));
        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow("yt-source-a-2", source: "Source A"));
        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow("yt-source-b", source: "Source B"));
        // The browse serve query now requires approval_status='approved'; approve all rows we expect published.
        await _store.SetApprovalStatusAsync(ContentSourceType.Youtube, "yt-source-a-1", "approved");
        await _store.SetApprovalStatusAsync(ContentSourceType.Youtube, "yt-source-a-2", "approved");
        await _store.SetApprovalStatusAsync(ContentSourceType.Youtube, "yt-source-b", "approved");

        Assert.Equal(2, await _store.SetVisibilityBySourceAsync("Source A", visible: true));
        var sourceB = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-source-b");
        Assert.NotNull(sourceB);
        Assert.Equal(1, await _store.SetVisibilityAsync(sourceB!.Id, visible: true));
        Assert.Equal(0, await _store.SetVisibilityAsync(-1, visible: false));

        var allVisible = await _store.GetPublishedRowsAsync();
        Assert.Equal(3, allVisible.Count);

        Assert.Equal(2, await _store.SetVisibilityBySourceAsync("Source A", visible: false));
        var sourceBOnly = await _store.GetPublishedRowsAsync();

        var row = Assert.Single(sourceBOnly);
        Assert.Equal("Source B", row.Source);
        Assert.True(row.IsVisible);
        Assert.False(row.IsHidden);
    }

    [Fact]
    public async Task SetHiddenAsync_AndSetVisibilityAsync_EnforceTriStateInvariant()
    {
        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow("yt-hidden-toggle"));
        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-hidden-toggle");
        Assert.NotNull(row);

        Assert.Equal(1, await _store.SetVisibilityAsync(row!.Id, visible: true));
        Assert.Equal(1, await _store.SetHiddenAsync(row.Id, hidden: true));

        var hidden = await _store.GetByIdAsync(row.Id);
        Assert.NotNull(hidden);
        Assert.True(hidden!.IsHidden);
        Assert.False(hidden.IsVisible);

        Assert.Equal(1, await _store.SetVisibilityAsync(row.Id, visible: false));
        var unpublished = await _store.GetByIdAsync(row.Id);
        Assert.NotNull(unpublished);
        Assert.False(unpublished!.IsVisible);
        Assert.False(unpublished.IsHidden);
    }

    [Fact]
    public async Task UpsertRowPreservingVisibilityAsync_PreservesIsHidden_OnExistingHiddenRow()
    {
        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow("yt-hidden-preserve"));
        var inserted = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-hidden-preserve");
        Assert.NotNull(inserted);
        Assert.Equal(1, await _store.SetHiddenAsync(inserted!.Id, hidden: true));

        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow(
            "yt-hidden-preserve",
            title: "Updated hidden title",
            artifactPath: "content-kb/command-zone/yt-hidden-preserve-v2.md"));

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-hidden-preserve");

        Assert.NotNull(row);
        Assert.True(row!.IsHidden);
        Assert.False(row.IsVisible);
        Assert.Equal("Updated hidden title", row.Title);
    }

    [Fact]
    public async Task EnsureSchemaAsync_AddsVisibilityColumnsToLegacySchema_AndPreservesVisibilityOnReupsert()
    {
        await CreateLegacySchemaAsync();
        Assert.False(await ColumnExistsAsync("is_visible"));
        Assert.False(await ColumnExistsAsync("is_hidden"));

        await _store.EnsureSchemaAsync();
        Assert.True(await ColumnExistsAsync("is_visible"));
        Assert.True(await ColumnExistsAsync("is_hidden"));
        var legacy = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-legacy");
        Assert.NotNull(legacy);
        Assert.False(legacy!.IsVisible);
        Assert.False(legacy.IsHidden);

        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow(
            "yt-migrated",
            title: "Migrated original"));
        var migrated = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-migrated");
        Assert.NotNull(migrated);
        Assert.Equal(1, await _store.SetVisibilityAsync(migrated!.Id, visible: true));

        await _store.EnsureSchemaAsync();
        await _store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow(
            "yt-migrated",
            title: "Migrated updated"));
        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-migrated");

        Assert.NotNull(row);
        Assert.True(row!.IsVisible);
        Assert.False(row.IsHidden);
        Assert.Equal("Migrated updated", row.Title);
    }

    [Fact]
    public void CreateTableDdl_IncludesIsVisibleDefault_ForBothDialects()
    {
        var postgres = GetPrivateSql("PostgresCreateTableSql");
        var sqlite = GetPrivateSql("SqliteCreateTableSql");

        Assert.Contains("is_visible         BOOLEAN NOT NULL DEFAULT FALSE", postgres, StringComparison.Ordinal);
        Assert.Contains("is_hidden          BOOLEAN NOT NULL DEFAULT FALSE", postgres, StringComparison.Ordinal);
        Assert.Contains("is_visible         INTEGER NOT NULL DEFAULT 0", sqlite, StringComparison.Ordinal);
        Assert.Contains("is_hidden          INTEGER NOT NULL DEFAULT 0", sqlite, StringComparison.Ordinal);
    }

    private async Task CreateLegacySchemaAsync()
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
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
              UNIQUE (natural_key_type, natural_key_value)
            );
            """;
        await create.ExecuteNonQueryAsync();

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
        RelationalDatabaseConnection.AddParameter(insert, "@source", "Legacy Source");
        RelationalDatabaseConnection.AddParameter(insert, "@title", "Legacy title");
        RelationalDatabaseConnection.AddParameter(insert, "@videoUrl", "https://www.youtube.com/watch?v=yt-legacy");
        RelationalDatabaseConnection.AddParameter(insert, "@artifactPath", "content-kb/legacy-source/yt-legacy.md");
        RelationalDatabaseConnection.AddParameter(insert, "@publishedUtc", "2026-05-26T12:00:00.0000000Z");
        RelationalDatabaseConnection.AddParameter(insert, "@indexedUtc", "2026-05-26T13:00:00.0000000Z");
        RelationalDatabaseConnection.AddParameter(insert, "@archetypeTags", "[\"combo\"]");
        RelationalDatabaseConnection.AddParameter(insert, "@bracketTags", "[\"cEDH\"]");
        RelationalDatabaseConnection.AddParameter(insert, "@cardCategoryTags", "[\"win-cons\"]");
        RelationalDatabaseConnection.AddParameter(insert, "@naturalKeyType", ContentSourceType.Youtube);
        RelationalDatabaseConnection.AddParameter(insert, "@naturalKeyValue", "yt-legacy");
        await insert.ExecuteNonQueryAsync();
    }

    private async Task<bool> ColumnExistsAsync(string columnName)
    {
        await using var connection = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(content_site_index);";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
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

    private static ContentSiteIndexRow CreateYoutubeRow(
        string youtubeVideoId,
        string? title = null,
        string? artifactPath = null,
        string? source = null,
        IReadOnlyList<string>? archetypeTags = null,
        string approvalStatus = "pending")
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
            RssGuid = null,
            ApprovalStatus = approvalStatus
        };
}
