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
    public async Task UpsertContentColumnsOnlyAsync_NewRow_MirrorsSourceApproval()
    {
        // D-01: the insert now mirrors the source row's approval_status instead of a hardcoded 'pending'.
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-new-pending", approvalStatus: "pending"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-new-approved", approvalStatus: "approved"));

        var pending = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-new-pending");
        var approved = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-new-approved");

        Assert.NotNull(pending);
        Assert.NotNull(approved);
        Assert.Equal("pending", pending!.ApprovalStatus);
        Assert.Equal("approved", approved!.ApprovalStatus);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_ExistingPendingRow_HealsToApprovedFromApprovedSource()
    {
        // D-02: a drifted prod row at pending is healed to approved when re-pushed from an approved source
        // (DirectPush reads only approved local rows), and the reverse value is likewise mirrored (not hardcoded).
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-heal-drift", title: "Original", approvalStatus: "pending"));

        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow(
            "yt-heal-drift",
            title: "Updated",
            artifactPath: "content-kb/command-zone/yt-heal-drift-v2.md",
            approvalStatus: "approved"));

        var healed = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-heal-drift");
        Assert.NotNull(healed);
        Assert.Equal("approved", healed!.ApprovalStatus);
        Assert.Equal("Updated", healed.Title);
        Assert.Equal("content-kb/command-zone/yt-heal-drift-v2.md", healed.ArtifactPath);

        // Reverse: a re-push from a pending source mirrors back to pending (value is mirrored, not forced approved).
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-heal-drift", approvalStatus: "pending"));
        var reverted = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-heal-drift");
        Assert.Equal("pending", reverted!.ApprovalStatus);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_PreservesOperatorVisibleEvergreen_AndMirrorsApproval()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-preserve-admin-visible", approvalStatus: "approved"));
        var inserted = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve-admin-visible");
        Assert.NotNull(inserted);

        Assert.Equal(1, await _store.SetVisibilityAsync(inserted!.Id, visible: true));
        Assert.Equal(1, await _store.SetEvergreenAsync(inserted.Id, evergreen: true));

        // Content-columns-only re-upsert from an approved source: operator-owned visibility/evergreen survive,
        // approval mirrors the (still approved) source.
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow(
            "yt-preserve-admin-visible",
            title: "Updated admin title",
            artifactPath: "content-kb/command-zone/yt-preserve-admin-visible-v2.md",
            approvalStatus: "approved"));

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve-admin-visible");

        Assert.NotNull(row);
        Assert.True(row!.IsVisible);
        Assert.False(row.IsHidden);
        Assert.True(row.IsEvergreen);
        Assert.Equal("approved", row.ApprovalStatus);
        Assert.Equal("Updated admin title", row.Title);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_PreservesOperatorHidden_AndMirrorsApproval()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-preserve-hidden", approvalStatus: "approved"));
        var inserted = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve-hidden");
        Assert.NotNull(inserted);

        Assert.Equal(1, await _store.SetHiddenAsync(inserted!.Id, hidden: true));

        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow(
            "yt-preserve-hidden",
            title: "Updated hidden title",
            artifactPath: "content-kb/command-zone/yt-preserve-hidden-v2.md",
            approvalStatus: "approved"));

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
    public async Task GetPublishedRowsAsync_ReturnsOnlyApprovedAndVisibleRows()
    {
        // D-04: the browse serve query returns approved+visible only — excludes visible-but-pending and approved-but-hidden.
        var approvedVisibleId = await SeedRowAsync("yt-pub-approved-visible", approvalStatus: "approved", visible: true);
        await SeedRowAsync("yt-pub-visible-pending", approvalStatus: "pending", visible: true);
        await SeedRowAsync("yt-pub-approved-hidden", approvalStatus: "approved", visible: false);

        var rows = await _store.GetPublishedRowsAsync();

        var row = Assert.Single(rows);
        Assert.Equal("yt-pub-approved-visible", row.YoutubeVideoId);
        Assert.Equal(approvedVisibleId, row.Id);
    }

    [Fact]
    public async Task GetPublishedByIdAsync_ReturnsRow_OnlyWhenApprovedAndVisible()
    {
        // D-04 / Codex HIGH: the public detail read returns a row only when approved+visible; null otherwise.
        var approvedVisibleId = await SeedRowAsync("yt-byid-approved-visible", approvalStatus: "approved", visible: true);
        var visiblePendingId = await SeedRowAsync("yt-byid-visible-pending", approvalStatus: "pending", visible: true);
        var approvedHiddenId = await SeedRowAsync("yt-byid-approved-hidden", approvalStatus: "approved", visible: false);

        var approvedVisible = await _store.GetPublishedByIdAsync(approvedVisibleId);
        var visiblePending = await _store.GetPublishedByIdAsync(visiblePendingId);
        var approvedHidden = await _store.GetPublishedByIdAsync(approvedHiddenId);
        var missing = await _store.GetPublishedByIdAsync(999_999);

        Assert.NotNull(approvedVisible);
        Assert.Equal("yt-byid-approved-visible", approvedVisible!.YoutubeVideoId);
        Assert.Null(visiblePending);
        Assert.Null(approvedHidden);
        Assert.Null(missing);

        // GetByIdAsync stays unfiltered — admin/Studio still see the pending row.
        Assert.NotNull(await _store.GetByIdAsync(visiblePendingId));
    }

    // Seeds an approved/visible-controlled row and returns its id.
    private async Task<long> SeedRowAsync(string youtubeVideoId, string approvalStatus, bool visible)
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow(youtubeVideoId, approvalStatus: approvalStatus));
        var inserted = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, youtubeVideoId);
        Assert.NotNull(inserted);
        if (visible)
        {
            await _store.SetVisibilityAsync(inserted!.Id, visible: true);
        }

        return inserted!.Id;
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

    [Fact]
    public async Task SetApprovalStatusAsync_Single_UpdatesMatchingRow()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-single-update"));

        var rowsAffected = await _store.SetApprovalStatusAsync(
            ContentSourceType.Youtube,
            "yt-single-update",
            "approved");

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-single-update");
        Assert.Equal(1, rowsAffected);
        Assert.NotNull(row);
        Assert.Equal("approved", row!.ApprovalStatus);
    }

    [Fact]
    public async Task SetApprovalStatusAsync_Single_NoMatch_ReturnsZero()
    {
        await _store.EnsureSchemaAsync();

        var rowsAffected = await _store.SetApprovalStatusAsync(
            ContentSourceType.Youtube,
            "yt-does-not-exist",
            "approved");

        Assert.Equal(0, rowsAffected);
    }

    [Fact]
    public async Task SetApprovalStatusAsync_Batch_UpdatesAllKeys()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-batch-01"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-batch-02"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-batch-03"));

        var keys = new (string Type, string Value)[]
        {
            (ContentSourceType.Youtube, "yt-batch-01"),
            (ContentSourceType.Youtube, "yt-batch-02"),
            (ContentSourceType.Youtube, "yt-batch-03"),
        };

        var rowsAffected = await _store.SetApprovalStatusAsync(keys, "rejected");

        Assert.Equal(3, rowsAffected);
        var row1 = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-batch-01");
        var row2 = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-batch-02");
        var row3 = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-batch-03");
        Assert.Equal("rejected", row1!.ApprovalStatus);
        Assert.Equal("rejected", row2!.ApprovalStatus);
        Assert.Equal("rejected", row3!.ApprovalStatus);
    }

    [Fact]
    public async Task SetApprovalStatusAsync_Batch_IsAtomic()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-atomic-01"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-atomic-02"));

        var keys = new (string Type, string Value)[]
        {
            (ContentSourceType.Youtube, "yt-atomic-01"),
            (ContentSourceType.Youtube, "yt-atomic-02"),
        };

        // Use an already-cancelled token so the batch is aborted mid-flight (before or during commits).
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _store.SetApprovalStatusAsync(keys, "approved", cts.Token));

        // Both rows must still be at their original "pending" status — transaction rolled back, nothing committed.
        var row1 = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-atomic-01");
        var row2 = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-atomic-02");
        Assert.Equal("pending", row1!.ApprovalStatus);
        Assert.Equal("pending", row2!.ApprovalStatus);
    }

    [Fact]
    public async Task SetApprovalStatusAsync_Batch_EmptyList_ReturnsZero()
    {
        await _store.EnsureSchemaAsync();

        var rowsAffected = await _store.SetApprovalStatusAsync(
            Array.Empty<(string Type, string Value)>(),
            "approved");

        Assert.Equal(0, rowsAffected);
    }

    [Fact]
    public async Task SetApprovalStatusAsync_InvalidStatus_Throws()
    {
        await _store.EnsureSchemaAsync();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.SetApprovalStatusAsync(ContentSourceType.Youtube, "yt-invalid", "deleted"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.SetApprovalStatusAsync(
                new (string Type, string Value)[] { (ContentSourceType.Youtube, "yt-invalid") },
                "deleted"));
    }

    [Fact]
    public async Task SetApprovalStatusAsync_PreservesAdminFields()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-preserve-fields"));
        var inserted = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve-fields");
        Assert.NotNull(inserted);

        await _store.SetVisibilityAsync(inserted!.Id, visible: true);
        await _store.SetEvergreenAsync(inserted.Id, evergreen: true);

        await _store.SetApprovalStatusAsync(
            ContentSourceType.Youtube,
            "yt-preserve-fields",
            "approved");

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve-fields");
        Assert.NotNull(row);
        Assert.True(row!.IsVisible);
        Assert.True(row.IsEvergreen);
        Assert.Equal("approved", row.ApprovalStatus);
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
