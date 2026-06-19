using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for <see cref="VideoStatusResolver"/> badge-resolution logic.
/// Uses in-file fakes (no mocking library) per project convention.
/// </summary>
public sealed class VideoStatusResolverTests
{
    // ---------------------------------------------------------------------------
    // In-file fakes
    // ---------------------------------------------------------------------------

    private sealed class FakeBlockedVideoStore : IBlockedVideoStore
    {
        private readonly bool _isBlocked;

        public FakeBlockedVideoStore(bool isBlocked) => _isBlocked = isBlocked;

        public Task<bool> IsBlockedAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            => Task.FromResult(_isBlocked);

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddBlockAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RemoveBlockAsync(string youtubeVideoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<BlockedVideo>> ListBlockedAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeSiteIndexStore : IContentSiteIndexStore
    {
        private readonly ContentSiteIndexRow? _row;

        public FakeSiteIndexStore(ContentSiteIndexRow? row) => _row = row;

        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(
            string naturalKeyType,
            string naturalKeyValue,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_row);

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> SetApprovalStatusAsync(string naturalKeyType, string naturalKeyValue, string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> SetApprovalStatusAsync(IReadOnlyList<(string Type, string Value)> keys, string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> StampPushedToProdAsync(IReadOnlyList<(string Type, string Value)> keys, DateTimeOffset pushedUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> SetVisibilityAsync(IReadOnlyList<(string Type, string Value)> keys, bool visible, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeSourceStore : IContentSourceStore
    {
        private readonly IReadOnlyList<ContentSource> _sources;

        public FakeSourceStore(IReadOnlyList<ContentSource> sources) => _sources = sources;

        public Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_sources);

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<long> InsertSourceAsync(string sourceSlug, string displayName, string sourceType, string sourceUrl, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// Configurable fake that returns a video for a specific (sourceId, youtubeVideoId) pair
    /// and null for all other lookups, so tests can assert iteration behaviour.
    /// </summary>
    private sealed class FakeVideoStore : IContentVideoStore
    {
        private readonly long _hitSourceId;
        private readonly string _hitYoutubeVideoId;
        private readonly ContentVideo? _hitResult;

        /// <summary>Tracks every (sourceId, youtubeVideoId) pair the resolver called.</summary>
        public List<(long SourceId, string VideoId)> Lookups { get; } = [];

        public FakeVideoStore(long hitSourceId, string hitYoutubeVideoId, ContentVideo? hitResult)
        {
            _hitSourceId = hitSourceId;
            _hitYoutubeVideoId = hitYoutubeVideoId;
            _hitResult = hitResult;
        }

        public Task<ContentVideo?> GetVideoByYoutubeIdAsync(long sourceId, string youtubeVideoId, CancellationToken cancellationToken = default)
        {
            Lookups.Add((sourceId, youtubeVideoId));
            ContentVideo? result = sourceId == _hitSourceId && youtubeVideoId == _hitYoutubeVideoId
                ? _hitResult
                : null;
            return Task.FromResult(result);
        }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<long> InsertVideoAsync(long sourceId, string? youtubeVideoId, string? rssGuid, string title, string videoUrl, DateTimeOffset? publishedUtc, string transcriptStatus, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateTranscriptStatusAsync(long videoId, string status, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> InsertTranscriptAsync(long videoId, string source, string body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> InsertClipAsync(long videoId, int timestampS, string excerpt, int sortOrder, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> InsertTagAsync(long videoId, string dimension, string tagValue, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountTranscriptsByVideoAsync(long videoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountSummariesByVideoAsync(long videoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountClipsByVideoAsync(long videoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountTagsByVideoAsync(long videoId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static ContentSource MakeSource(long id, string slug = "test-source")
        => new()
        {
            Id = id,
            SourceSlug = slug,
            DisplayName = slug,
            SourceType = ContentSourceType.Youtube,
            SourceUrl = $"https://youtube.com/channel/{slug}",
            IsEnabled = true,
            CreatedUtc = DateTimeOffset.UtcNow,
        };

    private static ContentVideo MakeVideo(long id, string ytId)
        => new()
        {
            Id = id,
            SourceId = 1L,
            YoutubeVideoId = ytId,
            Title = "Test Video",
            VideoUrl = $"https://youtu.be/{ytId}",
            TranscriptStatus = "ready",
            CreatedUtc = DateTimeOffset.UtcNow,
        };

    private static ContentSiteIndexRow MakeIndexRow(
        string approvalStatus = "pending",
        DateTimeOffset? pushedToProdUtc = null,
        bool isVisible = false)
        => new()
        {
            Id = 1L,
            Source = "test-source",
            Title = "Test Video",
            VideoUrl = "https://youtu.be/vid001",
            ArtifactPath = "content-kb/test-source/vid001.md",
            IndexedUtc = DateTimeOffset.UtcNow,
            ArchetypeTags = [],
            BracketTags = [],
            CardCategoryTags = [],
            ApprovalStatus = approvalStatus,
            PushedToProdUtc = pushedToProdUtc,
            IsVisible = isVisible,
        };

    // ---------------------------------------------------------------------------
    // Tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ResolveStatusAsync_BlockedVideo_ReturnsBlocked()
    {
        // Arrange: blocked=true; other stores would return Distilled if reached — proves blocked wins.
        var resolver = new VideoStatusResolver(
            new FakeBlockedVideoStore(isBlocked: true),
            new FakeSiteIndexStore(row: MakeIndexRow()),
            new FakeSourceStore([MakeSource(1)]),
            new FakeVideoStore(1, "vid001", MakeVideo(1, "vid001")));

        // Act
        var status = await resolver.ResolveStatusAsync("vid001");

        // Assert
        Assert.Equal(VideoStatus.Blocked, status);
    }

    [Fact]
    public async Task ResolveStatusAsync_SiteIndexRowPresent_ReturnsDistilled()
    {
        // Arrange: not blocked + content_site_index row present → Distilled.
        var resolver = new VideoStatusResolver(
            new FakeBlockedVideoStore(isBlocked: false),
            new FakeSiteIndexStore(row: MakeIndexRow()),
            new FakeSourceStore([MakeSource(1)]),
            new FakeVideoStore(hitSourceId: 0, hitYoutubeVideoId: "", hitResult: null));

        // Act
        var status = await resolver.ResolveStatusAsync("vid002");

        // Assert
        Assert.Equal(VideoStatus.Distilled, status);
    }

    [Fact]
    public async Task ResolveStatusAsync_FoundInSecondEnabledSource_ReturnsHarvested()
    {
        // Arrange: not blocked, no site-index row; video only exists in source #2 (not #1).
        // This proves the resolver iterates all enabled sources, not just the first.
        var videoId = "vid003";
        var source1 = MakeSource(1, "source-one");
        var source2 = MakeSource(2, "source-two");
        var videoStore = new FakeVideoStore(
            hitSourceId: 2,
            hitYoutubeVideoId: videoId,
            hitResult: MakeVideo(10, videoId));

        var resolver = new VideoStatusResolver(
            new FakeBlockedVideoStore(isBlocked: false),
            new FakeSiteIndexStore(row: null),
            new FakeSourceStore([source1, source2]),
            videoStore);

        // Act
        var status = await resolver.ResolveStatusAsync(videoId);

        // Assert: Harvested because found in source #2
        Assert.Equal(VideoStatus.Harvested, status);

        // Prove iteration: resolver called source #1 first (miss) then source #2 (hit)
        Assert.Contains((1L, videoId), videoStore.Lookups);
        Assert.Contains((2L, videoId), videoStore.Lookups);
        Assert.True(videoStore.Lookups.Count >= 2, "Resolver must have iterated at least two sources");
    }

    [Fact]
    public async Task ResolveStatusAsync_NotFoundInAnySources_ReturnsNotHarvested()
    {
        // Arrange: not blocked, no site-index row, no source has the video.
        var resolver = new VideoStatusResolver(
            new FakeBlockedVideoStore(isBlocked: false),
            new FakeSiteIndexStore(row: null),
            new FakeSourceStore([MakeSource(1), MakeSource(2, "source-two")]),
            new FakeVideoStore(hitSourceId: 0, hitYoutubeVideoId: "", hitResult: null));

        // Act
        var status = await resolver.ResolveStatusAsync("vid004");

        // Assert
        Assert.Equal(VideoStatus.NotHarvested, status);
    }

    [Fact]
    public async Task ResolveStatusAsync_ApprovedNotPushed_ReturnsApproved()
    {
        // Arrange: not blocked + index row with approval_status="approved" + no push.
        var resolver = new VideoStatusResolver(
            new FakeBlockedVideoStore(isBlocked: false),
            new FakeSiteIndexStore(row: MakeIndexRow(approvalStatus: "approved", pushedToProdUtc: null)),
            new FakeSourceStore([MakeSource(1)]),
            new FakeVideoStore(hitSourceId: 0, hitYoutubeVideoId: "", hitResult: null));

        var status = await resolver.ResolveStatusAsync("vid001");

        Assert.Equal(VideoStatus.Approved, status);
    }

    [Fact]
    public async Task ResolveStatusAsync_PushedAndVisible_ReturnsPublished()
    {
        // Arrange: not blocked + index row with push timestamp + is_visible=true.
        var resolver = new VideoStatusResolver(
            new FakeBlockedVideoStore(isBlocked: false),
            new FakeSiteIndexStore(row: MakeIndexRow(
                approvalStatus: "approved",
                pushedToProdUtc: DateTimeOffset.UtcNow,
                isVisible: true)),
            new FakeSourceStore([MakeSource(1)]),
            new FakeVideoStore(hitSourceId: 0, hitYoutubeVideoId: "", hitResult: null));

        var status = await resolver.ResolveStatusAsync("vid001");

        Assert.Equal(VideoStatus.Published, status);
    }

    [Fact]
    public async Task ResolveStatusAsync_PushedButHidden_ReturnsApproved()
    {
        // Arrange: pushed but is_visible=false → shows Approved (limbo semantic).
        var resolver = new VideoStatusResolver(
            new FakeBlockedVideoStore(isBlocked: false),
            new FakeSiteIndexStore(row: MakeIndexRow(
                approvalStatus: "approved",
                pushedToProdUtc: DateTimeOffset.UtcNow,
                isVisible: false)),
            new FakeSourceStore([MakeSource(1)]),
            new FakeVideoStore(hitSourceId: 0, hitYoutubeVideoId: "", hitResult: null));

        var status = await resolver.ResolveStatusAsync("vid001");

        Assert.Equal(VideoStatus.Approved, status);
    }

    [Fact]
    public async Task ResolveStatusAsync_UnblockedWithNoIndexOrHarvest_ReturnsNotHarvested()
    {
        // Arrange: not blocked, no index row, no harvested row across enabled sources.
        // Pins the SC5 unblock->re-browse loop at the resolver level.
        var resolver = new VideoStatusResolver(
            new FakeBlockedVideoStore(isBlocked: false),
            new FakeSiteIndexStore(row: null),
            new FakeSourceStore([MakeSource(1), MakeSource(2, "source-two")]),
            new FakeVideoStore(hitSourceId: 0, hitYoutubeVideoId: "", hitResult: null));

        var status = await resolver.ResolveStatusAsync("vid005");

        Assert.Equal(VideoStatus.NotHarvested, status);
    }
}
