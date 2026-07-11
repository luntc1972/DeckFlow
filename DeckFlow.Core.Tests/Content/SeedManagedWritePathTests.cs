using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for the <c>seed_managed</c> bound-parameter invariant across the three
/// <c>Upsert*Async</c> row variants (Task 1: <see cref="ContentSiteIndexRow.SeedManaged"/> is a
/// per-row bound parameter, never a hardcoded SQL literal, T-91-01), and for
/// <see cref="IContentSiteIndexStore.SetSeedManagedIfNullAsync"/> — the null-only idempotent
/// backfill write (Task 2).
/// </summary>
public sealed class SeedManagedWritePathTests : IDisposable
{
    private readonly string _dbPath;

    public SeedManagedWritePathTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"content-site-index-seedmanaged-write-{Guid.NewGuid():N}.db");
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

    // ── Bound-param invariant across upsert variants (Task 1) ────────────────

    [Fact]
    public async Task UpsertRowAsync_PlainLocalDistillPath_LeavesSeedManagedNull()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertRowAsync(CreateYoutubeRow("yt-local-distill", seedManaged: null));

        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-local-distill");
        Assert.NotNull(row);
        Assert.Null(row!.SeedManaged);
    }

    [Fact]
    public async Task UpsertContentColumnsOnlyAsync_SeedManagedTrue_RoundTripsThroughGetAllRowsAsync()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-columns-only", seedManaged: true));

        var all = await store.GetAllRowsAsync();
        var stored = Assert.Single(all);
        Assert.True(stored.SeedManaged);

        var byKey = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-columns-only");
        Assert.NotNull(byKey);
        Assert.True(byKey!.SeedManaged);
    }

    [Fact]
    public async Task UpsertRowPreservingVisibilityAsync_SeedManagedTrue_RoundTrips()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertRowPreservingVisibilityAsync(CreateYoutubeRow("yt-preserving-vis", seedManaged: true));

        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-preserving-vis");
        Assert.NotNull(row);
        Assert.True(row!.SeedManaged);
    }

    [Fact]
    public async Task GetAllRowsAndGetByIdAsync_RoundTripSeedManaged()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-roundtrip-id", seedManaged: false));

        var byKey = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-roundtrip-id");
        Assert.NotNull(byKey);

        var byId = await store.GetByIdAsync(byKey!.Id);
        Assert.NotNull(byId);
        Assert.False(byId!.SeedManaged);

        var all = await store.GetAllRowsAsync();
        Assert.Contains(all, r => r.Id == byKey.Id && r.SeedManaged == false);

        var approved = await store.GetApprovedRowsAsync();
        Assert.Contains(approved, r => r.Id == byKey.Id && r.SeedManaged == false);

        await store.SetVisibilityAsync(byKey.Id, visible: true);
        var published = await store.GetPublishedRowsAsync();
        Assert.Contains(published, r => r.Id == byKey.Id && r.SeedManaged == false);
    }

    // ── SetSeedManagedIfNullAsync (Task 2) ────────────────────────────────────

    [Fact]
    public async Task SetSeedManagedIfNullAsync_NullRow_SetsTrueAndAffectsOneRow()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-backfill", seedManaged: null));
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-backfill");
        Assert.NotNull(row);

        var affected = await store.SetSeedManagedIfNullAsync(row!.Id, true);

        Assert.Equal(1, affected);
        var updated = await store.GetByIdAsync(row.Id);
        Assert.True(updated!.SeedManaged);
    }

    [Fact]
    public async Task SetSeedManagedIfNullAsync_SecondCall_IsNoOpAndDoesNotOverwrite()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-backfill-noop", seedManaged: null));
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-backfill-noop");
        Assert.NotNull(row);

        var firstAffected = await store.SetSeedManagedIfNullAsync(row!.Id, true);
        var secondAffected = await store.SetSeedManagedIfNullAsync(row.Id, false);

        Assert.Equal(1, firstAffected);
        Assert.Equal(0, secondAffected);

        var updated = await store.GetByIdAsync(row.Id);
        Assert.True(updated!.SeedManaged, "second call must not overwrite the first classification");
    }

    [Fact]
    public async Task SetSeedManagedIfNullAsync_RowAlreadyClassifiedFalse_IsNotOverwritten()
    {
        var store = new ContentSiteIndexStore(_dbPath);
        await store.UpsertContentColumnsOnlyAsync(CreateYoutubeRow("yt-backfill-false", seedManaged: false));
        var row = await store.GetByNaturalKeyAsync(ContentSourceType.Youtube, "yt-backfill-false");
        Assert.NotNull(row);

        var affected = await store.SetSeedManagedIfNullAsync(row!.Id, true);

        Assert.Equal(0, affected);
        var updated = await store.GetByIdAsync(row.Id);
        Assert.False(updated!.SeedManaged, "a row already classified false must not be overwritten");
    }

    // ── Throwing default interface method (non-implementing double) ─────────

    [Fact]
    public async Task SetSeedManagedIfNullAsync_DefaultInterfaceMethod_ThrowsNotSupported()
    {
        IContentSiteIndexStore doubleWithoutOverride = new NonImplementingStoreDouble();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => doubleWithoutOverride.SetSeedManagedIfNullAsync(1, true));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ContentSiteIndexRow CreateYoutubeRow(string youtubeVideoId, bool? seedManaged)
        => new()
        {
            Id = 0,
            Source = "The Command Zone",
            Title = $"Video {youtubeVideoId}",
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
            SeedManaged = seedManaged,
        };

    /// <summary>
    /// Minimal <see cref="IContentSiteIndexStore"/> double that implements nothing beyond the
    /// interface's required members — used to prove <c>SetSeedManagedIfNullAsync</c>'s default
    /// interface method throws for stores that don't override it (mirrors <c>SetBodySha256IfNullAsync</c>).
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
