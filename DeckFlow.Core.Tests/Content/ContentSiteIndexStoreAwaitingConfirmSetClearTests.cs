using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// SQLite integration tests for the composite-key <c>SetAwaitingConfirmAsync</c> /
/// <c>ClearAwaitingConfirmAsync</c> methods (D-10, 90-03 Task 2): set-then-read, clear-then-read,
/// keying-by-natural-key-only (no timestamp WHERE, avoiding the F-51-PG-01 class), and the
/// throwing default-interface-method idiom for non-implementing doubles.
/// </summary>
public sealed class ContentSiteIndexStoreAwaitingConfirmSetClearTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ContentSiteIndexStore _store;

    public ContentSiteIndexStoreAwaitingConfirmSetClearTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-awaitingconfirm-setclear-{Guid.NewGuid():N}.db");
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
    public async Task SetAwaitingConfirmAsync_UpdatesOnlyListedKeys_AndRoundTripsUtcInstant()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-awaiting-set-target"));
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-awaiting-set-untouched"));
        var whenUtc = DateTimeOffset.Parse("2026-07-07T22:14:15.1234567+00:00");

        var rowsAffected = await _store.SetAwaitingConfirmAsync(
            [(ContentSourceType.Youtube, "yt-awaiting-set-target")],
            whenUtc);

        var stamped = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-awaiting-set-target");
        var untouched = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-awaiting-set-untouched");

        Assert.Equal(1, rowsAffected);
        Assert.NotNull(stamped);
        Assert.NotNull(untouched);
        Assert.Equal(whenUtc, stamped!.AwaitingConfirmUtc);
        Assert.Null(untouched!.AwaitingConfirmUtc);
    }

    [Fact]
    public async Task SetAwaitingConfirmAsync_EmptyKeys_ReturnsZero()
    {
        var rowsAffected = await _store.SetAwaitingConfirmAsync([], DateTimeOffset.UtcNow);
        Assert.Equal(0, rowsAffected);
    }

    [Fact]
    public async Task ClearAwaitingConfirmAsync_NullsTheMarker()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-awaiting-clear"));
        await _store.SetAwaitingConfirmAsync(
            [(ContentSourceType.Youtube, "yt-awaiting-clear")],
            DateTimeOffset.Parse("2026-07-07T22:20:00Z"));

        var midFlight = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-awaiting-clear");
        Assert.NotNull(midFlight!.AwaitingConfirmUtc);

        var rowsAffected = await _store.ClearAwaitingConfirmAsync(
            [(ContentSourceType.Youtube, "yt-awaiting-clear")]);

        var cleared = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-awaiting-clear");
        Assert.Equal(1, rowsAffected);
        Assert.Null(cleared!.AwaitingConfirmUtc);
    }

    [Fact]
    public async Task ClearAwaitingConfirmAsync_EmptyKeys_ReturnsZero()
    {
        var rowsAffected = await _store.ClearAwaitingConfirmAsync([]);
        Assert.Equal(0, rowsAffected);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_PreservesExistingAwaitingConfirmStamp()
    {
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-awaiting-preserve"));
        var whenUtc = DateTimeOffset.Parse("2026-07-07T21:00:00Z");
        await _store.SetAwaitingConfirmAsync([(ContentSourceType.Youtube, "yt-awaiting-preserve")], whenUtc);

        // A later re-distill re-upsert (content-only) must not clear the marker — the marker is
        // owned exclusively by SetAwaitingConfirmAsync/ClearAwaitingConfirmAsync, never by upserts.
        await _store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow(
            "yt-awaiting-preserve",
            title: "Updated after distill"));

        var row = await _store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-awaiting-preserve");
        Assert.NotNull(row);
        Assert.Equal("Updated after distill", row!.Title);
        Assert.Equal(whenUtc, row.AwaitingConfirmUtc);
    }

    // ── Throwing default interface methods (non-implementing double) ────────

    [Fact]
    public async Task SetAwaitingConfirmAsync_DefaultInterfaceMethod_ThrowsNotSupported()
    {
        IContentSiteIndexStore doubleWithoutOverride = new NonImplementingStoreDouble();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => doubleWithoutOverride.SetAwaitingConfirmAsync(
                [(ContentSourceType.Youtube, "yt-x")],
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ClearAwaitingConfirmAsync_DefaultInterfaceMethod_ThrowsNotSupported()
    {
        IContentSiteIndexStore doubleWithoutOverride = new NonImplementingStoreDouble();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => doubleWithoutOverride.ClearAwaitingConfirmAsync([(ContentSourceType.Youtube, "yt-x")]));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ContentSiteIndexRow CreateYoutubeRow(string youtubeVideoId, string? title = null)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = title ?? $"Video {youtubeVideoId}",
            VideoUrl = $"https://www.youtube.com/watch?v={youtubeVideoId}",
            ArtifactPath = $"content-kb/command-zone/{youtubeVideoId}.md",
            PublishedUtc = DateTimeOffset.Parse("2026-05-26T12:00:00Z"),
            IndexedUtc = DateTimeOffset.Parse("2026-05-26T13:00:00Z"),
            ArchetypeTags = new[] { "combo" },
            BracketTags = new[] { "cEDH" },
            CardCategoryTags = new[] { "win-cons" },
            YoutubeVideoId = youtubeVideoId,
            RssGuid = null,
            ApprovalStatus = "approved",
        };

    /// <summary>
    /// Minimal <see cref="IContentSiteIndexStore"/> double that implements nothing beyond the
    /// interface's required members — used to prove <c>SetAwaitingConfirmAsync</c> /
    /// <c>ClearAwaitingConfirmAsync</c>'s default interface methods throw for stores that don't
    /// override them (mirrors <c>SetBodySha256IfNullAsync</c>/<c>DeleteAllRowsAsync</c>).
    /// </summary>
    private sealed class NonImplementingStoreDouble : IContentSiteIndexStore
    {
        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default) => Task.FromResult<ContentSiteIndexRow?>(null);
        public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Array.Empty<ContentSiteIndexRow>());
        public Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Array.Empty<ContentSiteIndexRow>());
        public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ContentSiteIndexRow>>(Array.Empty<ContentSiteIndexRow>());
        public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<ContentSiteIndexRow?>(null);
        public Task<ContentSiteIndexRow?> GetPublishedByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<ContentSiteIndexRow?>(null);
        public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetApprovalStatusAsync(string naturalKeyType, string naturalKeyValue, string status, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetApprovalStatusAsync(IReadOnlyList<(string Type, string Value)> keys, string status, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> StampPushedToProdAsync(IReadOnlyList<(string Type, string Value)> keys, DateTimeOffset pushedUtc, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> SetVisibilityAsync(IReadOnlyList<(string Type, string Value)> keys, bool visible, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
