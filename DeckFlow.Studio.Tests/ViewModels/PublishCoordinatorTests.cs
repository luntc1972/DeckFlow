using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio.Services;
using DeckFlow.Studio.ViewModels;

namespace DeckFlow.Studio.Tests;

/// <summary>Tests private-root export behavior without a product repository.</summary>
public sealed class PublishCoordinatorTests
{
    private sealed class NoOpProgress : IOrchestratorProgress
    {
        public void Report(string message)
        {
        }
    }

    private static readonly DateTimeOffset IndexedAt = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static PublishCoordinator Build(
        IPrivateKbRootProvider provider,
        FakeContentKbOrchestrator orchestrator,
        FakeContentSiteIndexStore? store = null,
        CreatorSuppressionRowFilter? filter = null)
        => new(
            provider,
            orchestrator,
            store ?? new FakeContentSiteIndexStore(),
            new ContentKbOrchestratorOptions { ArtifactRoot = Path.Combine(Path.GetTempPath(), "content-kb") },
            new PublishStateDeriver(),
            filter ?? new CreatorSuppressionRowFilter(new FakeCreatorSuppressionStore(), new FakeCreatorIdentityResolver()));

    private static ContentSiteIndexRow ApprovedYoutube(long id, string videoId, bool pushed = false, bool visible = false)
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

    [Fact]
    public async Task PrivateRootExport_UnsetRoot_ThrowsNamedConfigurationErrorWithoutWriting()
    {
        var orchestrator = new FakeContentKbOrchestrator();
        var coordinator = Build(new StudioPrivateKbRootProvider(null, null), orchestrator);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExportToPrivateRootAsync(
            Path.GetTempPath(), new NoOpProgress(), CancellationToken.None));

        Assert.Contains("DECKFLOW_KB_ROOT", exception.Message, StringComparison.Ordinal);
        Assert.Empty(orchestrator.ExportToFilePaths);
        Assert.Equal(0, orchestrator.CopyApprovedCallCount);
    }

    [Fact]
    public async Task PrivateRootExport_ValidRoot_WritesSeedAndApprovedBodiesWithoutGit()
    {
        var root = Path.Combine(Path.GetTempPath(), "deckflow-private-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var orchestrator = new FakeContentKbOrchestrator();
            var coordinator = Build(new StudioPrivateKbRootProvider(null, root), orchestrator);

            var result = await coordinator.ExportToPrivateRootAsync(Path.GetTempPath(), new NoOpProgress(), CancellationToken.None);

            Assert.Equal(PublishExportStatus.Success, result.Status);
            Assert.Equal(Path.Combine(root, "content-kb", "seed", "index-seed.json"), Assert.Single(orchestrator.ExportToFilePaths));
            Assert.Equal(1, orchestrator.CopyApprovedCallCount);
            Assert.Contains("content-kb/youtube_channel/abc123.md", result.WrittenPaths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadInitDataAsync_GroupsApprovedRowsAndResolvesDataRoot()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(ApprovedYoutube(1, "first"));
        store.Rows.Add(ApprovedYoutube(2, "second", pushed: true, visible: true));
        var coordinator = Build(new StudioPrivateKbRootProvider(null, null), new FakeContentKbOrchestrator(), store);

        var init = await coordinator.LoadInitDataAsync(CancellationToken.None);

        Assert.Equal(2, init.ApprovedCount);
        Assert.Equal(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), init.DataRoot.TrimEnd(Path.DirectorySeparatorChar));
        Assert.Equal(2, init.StateSummary.Sum(summary => summary.Count));
        Assert.Equal(2, init.StateSummary.Count);
    }

    [Fact]
    public async Task PrivateRootExport_SeedExportFails_ReturnsSeedExportFailure()
    {
        var root = CreatePrivateRoot();
        try
        {
            var orchestrator = new FakeContentKbOrchestrator
            {
                CannedExportResult = new ContentIndexExportResult { Success = false, Message = "disk full" },
            };
            var coordinator = Build(new StudioPrivateKbRootProvider(null, root), orchestrator);

            var result = await coordinator.ExportToPrivateRootAsync(Path.GetTempPath(), new NoOpProgress(), CancellationToken.None);

            Assert.Equal(PublishExportStatus.SeedExportFailed, result.Status);
            Assert.Equal("disk full", result.SeedExportMessage);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PrivateRootExport_ArtifactCopyThrows_ReturnsArtifactCopyFailure()
    {
        var root = CreatePrivateRoot();
        try
        {
            var orchestrator = new FakeContentKbOrchestrator { ThrowOnCopy = new IOException("artifact missing") };
            var coordinator = Build(new StudioPrivateKbRootProvider(null, root), orchestrator);

            var result = await coordinator.ExportToPrivateRootAsync(Path.GetTempPath(), new NoOpProgress(), CancellationToken.None);

            Assert.Equal(PublishExportStatus.ArtifactCopyFailed, result.Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PrivateRootExport_CopyCancelled_PropagatesCancellation()
    {
        var root = CreatePrivateRoot();
        try
        {
            var orchestrator = new FakeContentKbOrchestrator { ThrowOnCopy = new OperationCanceledException() };
            var coordinator = Build(new StudioPrivateKbRootProvider(null, root), orchestrator);

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                coordinator.ExportToPrivateRootAsync(Path.GetTempPath(), new NoOpProgress(), CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PrivateRootExport_SuppressedCreatorIsExcludedWhileControlIsExported()
    {
        var root = CreatePrivateRoot();
        try
        {
            var store = new FakeContentSiteIndexStore();
            store.Rows.Add(ApprovedYoutube(1, "blocked") with { Source = "blocked-creator" });
            store.Rows.Add(ApprovedYoutube(2, "control") with { Source = "allowed-creator" });
            var suppressed = new FakeCreatorSuppressionStore();
            suppressed.Suppressed.Add("blocked-creator");
            var orchestrator = new FakeContentKbOrchestrator();
            var coordinator = Build(
                new StudioPrivateKbRootProvider(null, root),
                orchestrator,
                store,
                new CreatorSuppressionRowFilter(suppressed, new FakeCreatorIdentityResolver()));

            var init = await coordinator.LoadInitDataAsync(CancellationToken.None);
            var result = await coordinator.ExportToPrivateRootAsync(init.DataRoot, new NoOpProgress(), CancellationToken.None);

            Assert.Equal(1, init.ApprovedCount);
            Assert.Equal(PublishExportStatus.Success, result.Status);
            Assert.Single(orchestrator.ExportToFilePaths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LoadInitDataAsync_ThrowingSuppressionStore_RefusesLoadAfterReadableStoreSucceeds()
    {
        var store = new FakeContentSiteIndexStore();
        store.Rows.Add(ApprovedYoutube(1, "control"));
        var provider = new StudioPrivateKbRootProvider(null, null);

        Assert.Equal(1, (await Build(provider, new FakeContentKbOrchestrator(), store).LoadInitDataAsync(CancellationToken.None)).ApprovedCount);
        var coordinator = Build(
            provider,
            new FakeContentKbOrchestrator(),
            store,
            new CreatorSuppressionRowFilter(new ThrowingCreatorSuppressionStore(), new FakeCreatorIdentityResolver()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.LoadInitDataAsync(CancellationToken.None));
    }

    private static string CreatePrivateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "deckflow-private-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
