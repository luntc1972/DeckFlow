using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

/// <summary>
/// SQLite integration tests for <see cref="IContentSiteIndexStore.UpsertContentColumnsOnlyBatchAsync"/>
/// (H4 — all-or-nothing transactional batch) and <see cref="ContentSiteIndexContentSignature"/>
/// (M2 — stable content-column signature for cross-dialect equality).
/// </summary>
/// <remarks>
/// Postgres parity note: rollback / transaction behaviour is verified on SQLite (Postgres
/// cannot run in WSL). The transaction wrapper is <c>DbConnection.BeginTransactionAsync</c>
/// which is dialect-agnostic, and <c>UpsertContentColumnsOnlySql</c> is shared verbatim between
/// dialects. Behaviour is therefore asserted equivalent — Postgres execution is NOT claimed here.
/// </remarks>
public sealed class ContentSiteIndexStoreBatchUpsertTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSiteIndexStore _store;

    public ContentSiteIndexStoreBatchUpsertTests()
    {
        _dbPath = Path.Combine(
            Path.GetTempPath(),
            $"content-site-index-batch-{Guid.NewGuid():N}.db");
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

    // ── Batch commit ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertContentColumnsOnlyBatchAsync_AllValid_CommitsAllRows()
    {
        var rowA = CreateYoutubeRow("yt-batch-a");
        var rowB = CreateYoutubeRow("yt-batch-b");

        await _store.UpsertContentColumnsOnlyBatchAsync([rowA, rowB]);

        var all = await _store.GetAllRowsAsync();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, r => r.YoutubeVideoId == "yt-batch-a");
        Assert.Contains(all, r => r.YoutubeVideoId == "yt-batch-b");
    }

    // ── Batch rollback ────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertContentColumnsOnlyBatchAsync_BadRowMidBatch_RollsBackAll()
    {
        // Why: force mid-batch failure deterministically via a '..' traversal path which
        // ValidateArtifactPath rejects, proving true all-or-nothing (not "skip the bad row").
        var validRow = CreateYoutubeRow("yt-rollback-good");
        var badRow = CreateYoutubeRow("yt-rollback-bad", artifactPath: "../escape.md");

        var ex = await Assert.ThrowsAsync<ContentSiteIndexBatchUpsertException>(
            () => _store.UpsertContentColumnsOnlyBatchAsync([validRow, badRow]));

        // Exception carries failing row identity (non-secret).
        Assert.Equal(badRow.Title, ex.FailedRowTitle);

        // validRowA was rolled back — ZERO rows in the store (all-or-nothing).
        var all = await _store.GetAllRowsAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyBatchAsync_BadRowMidBatch_ExceptionHasInnerException()
    {
        var validRow = CreateYoutubeRow("yt-inner-good");
        var badRow = CreateYoutubeRow("yt-inner-bad", artifactPath: "../escape.md");

        var ex = await Assert.ThrowsAsync<ContentSiteIndexBatchUpsertException>(
            () => _store.UpsertContentColumnsOnlyBatchAsync([validRow, badRow]));

        // InnerException is the underlying failure (available to the log sink).
        Assert.NotNull(ex.InnerException);
    }

    // ── Content-columns-only semantics ────────────────────────────────────────

    [Fact]
    public async Task UpsertContentColumnsOnlyBatchAsync_ExistingRow_PreservesIsVisibleAndIsEvergreen()
    {
        // Pre-seed a row with IsVisible=true and IsEvergreen=true via a full-row upsert
        // (the only path that sets those columns), then batch-upsert an updated title.
        await _store.EnsureSchemaAsync();
        await using var conn = await RelationalDatabaseConnection
            .FromSqlitePath(_dbPath)
            .OpenConnectionAsync();

        // Insert with is_visible=1, is_evergreen=1 directly so we don't depend on the
        // higher-level UpsertRowAsync (which goes through different SQL).
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO content_site_index
              (source, title, video_url, artifact_path, published_utc, indexed_utc,
               archetype_tags, bracket_tags, card_category_tags,
               natural_key_type, natural_key_value,
               is_visible, is_evergreen, approval_status)
            VALUES
              ('ch', 'Original Title', 'https://youtu.be/yt-preserve', 'content-kb/ch/yt-preserve.md',
               NULL, '2026-01-01T00:00:00Z',
               '[]', '[]', '[]',
               'youtube_channel', 'yt-preserve',
               1, 1, 'approved');
            """;
        await cmd.ExecuteNonQueryAsync();

        var updatedRow = new ContentSiteIndexRow
        {
            Id = 0,
            Source = "ch",
            Title = "Updated Title",
            VideoUrl = "https://youtu.be/yt-preserve",
            ArtifactPath = "content-kb/ch/yt-preserve.md",
            IndexedUtc = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = "yt-preserve",
            ApprovalStatus = "approved",
        };

        await _store.UpsertContentColumnsOnlyBatchAsync([updatedRow]);

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserve");
        Assert.NotNull(row);
        // Title was updated.
        Assert.Equal("Updated Title", row!.Title);
        // is_visible and is_evergreen were NOT touched by the content-only upsert.
        Assert.True(row.IsVisible, "is_visible must be preserved (not clobbered) by batch upsert");
        Assert.True(row.IsEvergreen, "is_evergreen must be preserved (not clobbered) by batch upsert");
    }

    // ── Empty input ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertContentColumnsOnlyBatchAsync_EmptyList_IsNoOp()
    {
        // Must complete normally without touching the DB.
        await _store.UpsertContentColumnsOnlyBatchAsync(Array.Empty<ContentSiteIndexRow>());

        var all = await _store.GetAllRowsAsync();
        Assert.Empty(all);
    }

    // ── ContentSiteIndexContentSignature unit tests ───────────────────────────

    [Fact]
    public void BuildSignature_SameContentColumns_ReturnsEqualStrings()
    {
        var a = CreateYoutubeRow("yt-sig-eq");
        var b = CreateYoutubeRow("yt-sig-eq");

        Assert.Equal(
            ContentSiteIndexContentSignature.BuildSignature(a),
            ContentSiteIndexContentSignature.BuildSignature(b));
    }

    [Fact]
    public void BuildSignature_DifferentTitle_ReturnsDifferentStrings()
    {
        var a = CreateYoutubeRow("yt-sig-diff", title: "Title A");
        var b = CreateYoutubeRow("yt-sig-diff", title: "Title B");

        Assert.NotEqual(
            ContentSiteIndexContentSignature.BuildSignature(a),
            ContentSiteIndexContentSignature.BuildSignature(b));
    }

    [Fact]
    public void BuildSignature_SubSecondTimeDifference_ReturnsEqualStrings()
    {
        // Why: SQLite stores timestamps at 1-second precision; Postgres at microsecond.
        // A sub-second difference must NOT trigger a false-positive "Updated" classification.
        var baseTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var subSecondLater = baseTime.AddMilliseconds(500);

        var a = CreateYoutubeRow("yt-sig-subsec") with { IndexedUtc = baseTime };
        var b = CreateYoutubeRow("yt-sig-subsec") with { IndexedUtc = subSecondLater };

        Assert.Equal(
            ContentSiteIndexContentSignature.BuildSignature(a),
            ContentSiteIndexContentSignature.BuildSignature(b));
    }

    [Fact]
    public void BuildSignature_SubSecondPublishedUtcDifference_ReturnsEqualStrings()
    {
        var baseTime = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var subSecondLater = baseTime.AddMilliseconds(500);

        var a = CreateYoutubeRow("yt-sig-pub-subsec") with { PublishedUtc = baseTime };
        var b = CreateYoutubeRow("yt-sig-pub-subsec") with { PublishedUtc = subSecondLater };

        Assert.Equal(
            ContentSiteIndexContentSignature.BuildSignature(a),
            ContentSiteIndexContentSignature.BuildSignature(b));
    }

    [Fact]
    public void BuildSignature_SameTagsDifferentListReferences_ReturnsEqualStrings()
    {
        // Tags serialized via ContentArtifactSpec.SerializeTags so list-reference equality
        // is never required.
        var tagsA = new[] { "combo", "control" };
        var tagsB = new[] { "combo", "control" }; // different array instance, same content

        var a = CreateYoutubeRow("yt-sig-tags") with { ArchetypeTags = tagsA };
        var b = CreateYoutubeRow("yt-sig-tags") with { ArchetypeTags = tagsB };

        Assert.Equal(
            ContentSiteIndexContentSignature.BuildSignature(a),
            ContentSiteIndexContentSignature.BuildSignature(b));
    }

    [Fact]
    public void AreContentEqual_IdenticalRows_ReturnsTrue()
    {
        var a = CreateYoutubeRow("yt-eq-a");
        var b = CreateYoutubeRow("yt-eq-a");

        Assert.True(ContentSiteIndexContentSignature.AreContentEqual(a, b));
    }

    [Fact]
    public void AreContentEqual_DifferentTitle_ReturnsFalse()
    {
        var a = CreateYoutubeRow("yt-eq-b", title: "Title A");
        var b = CreateYoutubeRow("yt-eq-b", title: "Title B");

        Assert.False(ContentSiteIndexContentSignature.AreContentEqual(a, b));
    }

    [Fact]
    public void BuildSignature_NullPublishedUtc_DoesNotMatchNonNullPublishedUtc()
    {
        var withNull = CreateYoutubeRow("yt-null-pub") with { PublishedUtc = null };
        var withDate = CreateYoutubeRow("yt-null-pub") with
        {
            PublishedUtc = DateTimeOffset.UtcNow
        };

        Assert.NotEqual(
            ContentSiteIndexContentSignature.BuildSignature(withNull),
            ContentSiteIndexContentSignature.BuildSignature(withDate));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ContentSiteIndexRow CreateYoutubeRow(
        string youtubeVideoId,
        string? title = null,
        string? artifactPath = null)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = title ?? $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = artifactPath ?? $"content-kb/command-zone/{youtubeVideoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = new[] { "combo", "control" },
            BracketTags = new[] { "cEDH", "Optimized" },
            CardCategoryTags = new[] { "win-cons", "counter" },
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null
        };
}
