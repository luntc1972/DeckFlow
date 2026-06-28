using System.Text.Json;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.ViewModels;

namespace DeckFlow.Studio.Tests;

/// <summary>
/// Fast unit tests for <see cref="PublishCoordinator"/> — the Publish-to-Git orchestration extracted
/// from the page code-behind (H1 split). These exercise the export-and-diff change classification
/// (added / updated / removed via canonical per-row JSON), the failure-status discrimination, and the
/// stage-and-commit + stamp path directly with fakes, without the bUnit render the logic previously
/// required.
/// </summary>
public sealed class PublishCoordinatorTests
{
    private static readonly DateTimeOffset IndexedAt = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    // Serializer used to author CannedHeadSeed JSON so it matches the coordinator's canonical shape.
    private static readonly JsonSerializerOptions HeadSeedJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private sealed class NoOpProgress : IOrchestratorProgress
    {
        public void Report(string message)
        {
        }
    }

    private static ContentSiteIndexRow ApprovedYoutube(long id, string videoId, bool pushed, bool visible)
        => new()
        {
            Id = id,
            Source = "test-channel",
            Title = $"Video {id}",
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            IndexedUtc = IndexedAt,
            ApprovalStatus = "approved",
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
            YoutubeVideoId = videoId,
            PushedToProdUtc = pushed ? IndexedAt : null,
            IsVisible = visible,
        };

    private static ContentIndexExportRow ExportRow(string videoId, string title)
        => new()
        {
            NaturalKeyType = ContentSourceType.Youtube,
            NaturalKeyValue = videoId,
            Source = "test-channel",
            Title = title,
            VideoUrl = $"https://youtu.be/{videoId}",
            ArtifactPath = $"content-kb/test-channel/{videoId}.md",
            IndexedUtc = IndexedAt,
            ArchetypeTags = Array.Empty<string>(),
            BracketTags = Array.Empty<string>(),
            CardCategoryTags = Array.Empty<string>(),
        };

    private static string HeadSeed(params ContentIndexExportRow[] rows)
        => JsonSerializer.Serialize(rows.ToList(), HeadSeedJson);

    private static PublishCoordinator Build(
        FakeGitRepository git,
        FakeContentKbOrchestrator orchestrator,
        FakeContentSiteIndexStore store)
        => new(
            git,
            orchestrator,
            store,
            new ContentKbOrchestratorOptions { ArtifactRoot = "/data/content-kb" },
            new PublishStateDeriver());

    // ── LoadInitDataAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task LoadInitDataAsync_ReturnsRepoInfoAndGroupsApprovedRows()
    {
        var git = new FakeGitRepository { CannedRepoRoot = "/fake/repo", CannedBranch = "v1.7" };
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(ApprovedYoutube(1, "vid1", pushed: true, visible: true));
        store.Rows.Add(ApprovedYoutube(2, "vid2", pushed: true, visible: true));
        var coordinator = Build(git, new FakeContentKbOrchestrator(), store);

        var init = await coordinator.LoadInitDataAsync(CancellationToken.None);

        Assert.Equal("/fake/repo", init.RepoRoot);
        Assert.Equal("v1.7", init.Branch);
        Assert.Equal(2, init.ApprovedCount);
        // ArtifactRoot already carries content-kb/; dataRoot is its parent (D-01/D-03/D-10).
        // Compare via the same API the coordinator uses so the assertion is platform-separator agnostic.
        Assert.Equal(Path.GetDirectoryName("/data/content-kb"), init.DataRoot);
        Assert.DoesNotContain("content-kb", init.DataRoot);
        Assert.Equal(2, init.StateSummary.Sum(s => s.Count));
    }

    // ── ExportAndDiffAsync — failure statuses ────────────────────────────────

    [Fact]
    public async Task ExportAndDiffAsync_SeedExportFails_ReturnsSeedExportFailedWithMessage()
    {
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedExportResult = new ContentIndexExportResult { Success = false, Message = "disk full" },
        };
        var coordinator = Build(new FakeGitRepository(), orchestrator, new FakeContentSiteIndexStore());

        var result = await coordinator.ExportAndDiffAsync("/fake/repo", "/data", new NoOpProgress(), CancellationToken.None);

        Assert.Equal(PublishExportStatus.SeedExportFailed, result.Status);
        Assert.Equal("disk full", result.SeedExportMessage);
    }

    [Fact]
    public async Task ExportAndDiffAsync_ArtifactCopyThrows_ReturnsArtifactCopyFailed()
    {
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedExportResult = new ContentIndexExportResult { Success = true, Rows = new[] { ExportRow("vid1", "T") } },
            ThrowOnCopy = new IOException("artifact missing"),
        };
        var coordinator = Build(new FakeGitRepository(), orchestrator, new FakeContentSiteIndexStore());

        var result = await coordinator.ExportAndDiffAsync("/fake/repo", "/data", new NoOpProgress(), CancellationToken.None);

        Assert.Equal(PublishExportStatus.ArtifactCopyFailed, result.Status);
    }

    [Fact]
    public async Task ExportAndDiffAsync_CopyCancelled_PropagatesCancellation()
    {
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedExportResult = new ContentIndexExportResult { Success = true, Rows = new[] { ExportRow("vid1", "T") } },
            ThrowOnCopy = new OperationCanceledException(),
        };
        var coordinator = Build(new FakeGitRepository(), orchestrator, new FakeContentSiteIndexStore());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.ExportAndDiffAsync("/fake/repo", "/data", new NoOpProgress(), CancellationToken.None));
    }

    // ── ExportAndDiffAsync — change classification ───────────────────────────

    [Fact]
    public async Task ExportAndDiffAsync_NewKeyNotAtHead_CountsAsAdded()
    {
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedExportResult = new ContentIndexExportResult { Success = true, Rows = new[] { ExportRow("vid1", "Title") } },
            CannedCopiedArtifactPaths = new[] { "content-kb/test-channel/vid1.md" },
        };
        var git = new FakeGitRepository { CannedHeadSeed = string.Empty };
        var coordinator = Build(git, orchestrator, new FakeContentSiteIndexStore());

        var result = await coordinator.ExportAndDiffAsync("/fake/repo", "/data", new NoOpProgress(), CancellationToken.None);

        Assert.Equal(PublishExportStatus.Success, result.Status);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(0, result.RemovedCount);
        // Staged = seed + copied artifact paths.
        Assert.Equal("content-kb/seed/index-seed.json", result.StagedPaths[0]);
        Assert.Contains("content-kb/test-channel/vid1.md", result.StagedPaths);
        Assert.Single(result.ExportedKeys);
    }

    [Fact]
    public async Task ExportAndDiffAsync_ChangedContentSameKey_CountsAsUpdated()
    {
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedExportResult = new ContentIndexExportResult { Success = true, Rows = new[] { ExportRow("vid1", "New Title") } },
        };
        var git = new FakeGitRepository { CannedHeadSeed = HeadSeed(ExportRow("vid1", "Old Title")) };
        var coordinator = Build(git, orchestrator, new FakeContentSiteIndexStore());

        var result = await coordinator.ExportAndDiffAsync("/fake/repo", "/data", new NoOpProgress(), CancellationToken.None);

        Assert.Equal(0, result.AddedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.RemovedCount);
    }

    [Fact]
    public async Task ExportAndDiffAsync_IdenticalContent_CountsNothing()
    {
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedExportResult = new ContentIndexExportResult { Success = true, Rows = new[] { ExportRow("vid1", "Same") } },
        };
        var git = new FakeGitRepository { CannedHeadSeed = HeadSeed(ExportRow("vid1", "Same")) };
        var coordinator = Build(git, orchestrator, new FakeContentSiteIndexStore());

        var result = await coordinator.ExportAndDiffAsync("/fake/repo", "/data", new NoOpProgress(), CancellationToken.None);

        Assert.Equal(0, result.AddedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(0, result.RemovedCount);
    }

    [Fact]
    public async Task ExportAndDiffAsync_KeyAtHeadMissingFromNew_CountsAsRemoved()
    {
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedExportResult = new ContentIndexExportResult { Success = true, Rows = Array.Empty<ContentIndexExportRow>() },
        };
        var git = new FakeGitRepository { CannedHeadSeed = HeadSeed(ExportRow("vid1", "Gone")) };
        var coordinator = Build(git, orchestrator, new FakeContentSiteIndexStore());

        var result = await coordinator.ExportAndDiffAsync("/fake/repo", "/data", new NoOpProgress(), CancellationToken.None);

        Assert.Equal(0, result.AddedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.RemovedCount);
    }

    [Fact]
    public async Task ExportAndDiffAsync_CommitMessageReportsDelta()
    {
        var orchestrator = new FakeContentKbOrchestrator
        {
            CannedExportResult = new ContentIndexExportResult
            {
                Success = true,
                Rows = new[] { ExportRow("vid1", "A"), ExportRow("vid2", "B") },
            },
        };
        var git = new FakeGitRepository { CannedHeadSeed = HeadSeed(ExportRow("vid2", "B")) };
        var coordinator = Build(git, orchestrator, new FakeContentSiteIndexStore());

        var result = await coordinator.ExportAndDiffAsync("/fake/repo", "/data", new NoOpProgress(), CancellationToken.None);

        Assert.Equal("content: publish KB seed (1 added, 0 updated, 0 removed)", result.CommitMessage);
    }

    // ── CommitAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CommitAsync_Success_ReturnsShaAndStampsKeys()
    {
        var git = new FakeGitRepository { CannedCommitSha = "deadbee" };
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(git, new FakeContentKbOrchestrator(), store);
        var keys = new[] { (ContentSourceType.Youtube, "vid1") };

        var result = await coordinator.CommitAsync(
            "/fake/repo",
            new[] { "content-kb/seed/index-seed.json" },
            "content: publish KB seed (1 added, 0 updated, 0 removed)",
            keys,
            CancellationToken.None);

        Assert.Equal("deadbee", result.Sha);
        Assert.False(result.LocalStampFailed);
        Assert.Single(store.StampCalls);
    }

    [Fact]
    public async Task CommitAsync_StampThrows_ReturnsLocalStampFailed_CommitStillSucceeds()
    {
        var git = new FakeGitRepository { CannedCommitSha = "deadbee" };
        var store = new FakeContentSiteIndexStore { ThrowOnStamp = new InvalidOperationException("local db locked") };
        var coordinator = Build(git, new FakeContentKbOrchestrator(), store);
        var keys = new[] { (ContentSourceType.Youtube, "vid1") };

        var result = await coordinator.CommitAsync(
            "/fake/repo",
            new[] { "content-kb/seed/index-seed.json" },
            "msg",
            keys,
            CancellationToken.None);

        Assert.Equal("deadbee", result.Sha);
        Assert.True(result.LocalStampFailed);
    }

    [Fact]
    public async Task CommitAsync_NoExportedKeys_SkipsStamp()
    {
        var git = new FakeGitRepository { CannedCommitSha = "deadbee" };
        var store = new FakeContentSiteIndexStore();
        var coordinator = Build(git, new FakeContentKbOrchestrator(), store);

        var result = await coordinator.CommitAsync(
            "/fake/repo",
            new[] { "content-kb/seed/index-seed.json" },
            "msg",
            Array.Empty<(string, string)>(),
            CancellationToken.None);

        Assert.Equal("deadbee", result.Sha);
        Assert.False(result.LocalStampFailed);
        Assert.Empty(store.StampCalls);
    }

    [Fact]
    public async Task CommitAsync_GitThrows_PropagatesToCaller()
    {
        var git = new FakeGitRepository { ThrowOnCommit = new GitCommandException("commit blew up") };
        var coordinator = Build(git, new FakeContentKbOrchestrator(), new FakeContentSiteIndexStore());

        await Assert.ThrowsAsync<GitCommandException>(() => coordinator.CommitAsync(
            "/fake/repo",
            new[] { "content-kb/seed/index-seed.json" },
            "msg",
            Array.Empty<(string, string)>(),
            CancellationToken.None));
    }
}
