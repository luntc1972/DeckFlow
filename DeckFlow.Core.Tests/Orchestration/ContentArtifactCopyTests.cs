using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Pins the copy behavior, missing-source error, and traversal rejection of
/// <see cref="IContentKbOrchestrator.CopyApprovedArtifactsToRepoAsync"/>.
/// </summary>
public sealed class ContentArtifactCopyTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _tempDir;
    private readonly ContentSiteIndexStore _indexStore;

    public ContentArtifactCopyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"deckflow-artifact-copy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "index.db");
        _indexStore = new ContentSiteIndexStore(_dbPath);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    [Fact]
    public async Task CopyApprovedArtifactsToRepoAsync_CopiesApprovedArtifactIntoRepo()
    {
        // Arrange
        const string slug = "test-source";
        const string videoId = "yt-copy-001";
        const string artifactPath = $"content-kb/{slug}/{videoId}.md";
        const string artifactContent = "# Test artifact\nSome content here.";

        await _indexStore.EnsureSchemaAsync();
        await _indexStore.UpsertRowAsync(BuildRow(videoId, slug, artifactPath, "approved"));
        // UpsertRowAsync defaults approval_status to 'pending' (Phase 43); approve via the
        // dedicated mutation so GetApprovedRowsAsync actually returns this row.
        await _indexStore.SetApprovalStatusAsync(ContentSourceType.Youtube, videoId, "approved");

        var dataRoot = Path.Combine(_tempDir, "data");
        var repoRoot = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(repoRoot);

        // Write source artifact under dataRoot (= parent of ArtifactRoot)
        // ArtifactRoot = {dataRoot}/content-kb; stored path already includes "content-kb/"
        var sourceFile = Path.Combine(dataRoot, artifactPath);
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        await File.WriteAllTextAsync(sourceFile, artifactContent);

        var orchestrator = CreateOrchestrator(_indexStore);

        // Act
        var result = await orchestrator.CopyApprovedArtifactsToRepoAsync(dataRoot, repoRoot);

        // Assert
        Assert.Single(result);
        Assert.Equal(artifactPath, result[0]);

        var destFile = Path.Combine(repoRoot, "content-kb", slug, $"{videoId}.md");
        Assert.True(File.Exists(destFile), $"Artifact should exist at {destFile}");
        Assert.Equal(artifactContent, await File.ReadAllTextAsync(destFile));
    }

    [Fact]
    public async Task CopyApprovedArtifactsToRepoAsync_MissingSource_Throws()
    {
        // Arrange
        const string slug = "test-source";
        const string videoId = "yt-missing-001";
        const string artifactPath = $"content-kb/{slug}/{videoId}.md";

        await _indexStore.EnsureSchemaAsync();
        await _indexStore.UpsertRowAsync(BuildRow(videoId, slug, artifactPath, "approved"));
        // UpsertRowAsync defaults approval_status to 'pending' (Phase 43); approve via the
        // dedicated mutation so GetApprovedRowsAsync actually returns this row.
        await _indexStore.SetApprovalStatusAsync(ContentSourceType.Youtube, videoId, "approved");

        var dataRoot = Path.Combine(_tempDir, "data-missing");
        var repoRoot = Path.Combine(_tempDir, "repo-missing");
        Directory.CreateDirectory(repoRoot);
        // Do NOT create the source file — it must throw, not silently skip

        var orchestrator = CreateOrchestrator(_indexStore);

        // Act + Assert
        await Assert.ThrowsAnyAsync<Exception>(
            () => orchestrator.CopyApprovedArtifactsToRepoAsync(dataRoot, repoRoot));
    }

    [Fact]
    public async Task CopyApprovedArtifactsToRepoAsync_RejectsTraversalPath()
    {
        // Arrange: build an export row with a ".." traversal in the artifact path and
        // test the containment guard directly.
        // The SQLite store may sanitize artifact_path on insert, so we unit-test the helper
        // via a minimal stub index store that returns a traversal row.
        var dataRoot = Path.Combine(_tempDir, "data-traversal");
        var repoRoot = Path.Combine(_tempDir, "repo-traversal");
        Directory.CreateDirectory(dataRoot);
        Directory.CreateDirectory(repoRoot);

        var orchestrator = CreateOrchestratorWithFakeIndex(
            new FakeApprovedIndexStore(
            [
                BuildRow("vid", "src", "content-kb/../../../etc/passwd", "approved"),
            ]));

        // Act + Assert: containment guard should throw
        await Assert.ThrowsAnyAsync<Exception>(
            () => orchestrator.CopyApprovedArtifactsToRepoAsync(dataRoot, repoRoot));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ContentKbOrchestrator CreateOrchestrator(IContentSiteIndexStore indexStore)
        => new(
            new ThrowingContentSourceStore(),
            new ThrowingContentVideoStore(),
            indexStore,
            new ThrowingBlockedVideoStore(),
            new ThrowingContentHarvestRunStore(),
            new ThrowingLlmSpendLedger(),
            new ThrowingWhisperSpendLedger(),
            new ThrowingLlmDistillationService(),
            new ThrowingYouTubeChannelVideoLister(),
            new ThrowingTranscriptSource(),
            new ThrowingFfmpegAudioChunker(),
            () => DateTimeOffset.Parse("2026-06-16T00:00:00Z"),
            new ContentKbOrchestratorOptions
            {
                ArtifactRoot = Path.Combine(Path.GetTempPath(), "deckflow-artifact-copy-art"),
            });

    private static ContentKbOrchestrator CreateOrchestratorWithFakeIndex(
        IContentSiteIndexStore indexStore)
        => CreateOrchestrator(indexStore);

    private static ContentSiteIndexRow BuildRow(
        string videoId,
        string slug,
        string artifactPath,
        string approvalStatus)
        => new()
        {
            Id = 0,
            YoutubeVideoId = videoId,
            RssGuid = null,
            Source = slug,
            Title = $"Title for {videoId}",
            VideoUrl = $"https://youtube.com/watch?v={videoId}",
            ArtifactPath = artifactPath,
            PublishedUtc = null,
            IndexedUtc = DateTimeOffset.Parse("2026-06-16T00:00:00Z"),
            ArchetypeTags = [],
            BracketTags = [],
            CardCategoryTags = [],
            ApprovalStatus = approvalStatus,
        };

    /// <summary>
    /// Fake store that returns a fixed list of rows from GetApprovedRowsAsync,
    /// used to inject traversal-path rows for containment-guard tests.
    /// </summary>
    private sealed class FakeApprovedIndexStore : IContentSiteIndexStore
    {
        private readonly IReadOnlyList<ContentSiteIndexRow> _rows;

        public FakeApprovedIndexStore(IReadOnlyList<ContentSiteIndexRow> rows)
        {
            _rows = rows;
        }

        public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(_rows);

        public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(string naturalKeyType, string naturalKeyValue, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> DeleteAllRowsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetApprovalStatusAsync(string naturalKeyType, string naturalKeyValue, string status, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SetApprovalStatusAsync(IReadOnlyList<(string Type, string Value)> keys, string status, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> StampPushedToProdAsync(IReadOnlyList<(string Type, string Value)> keys, DateTimeOffset pushedUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> SetVisibilityAsync(IReadOnlyList<(string Type, string Value)> keys, bool visible, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
