using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
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

    private static PublishCoordinator Build(IPrivateKbRootProvider provider, FakeContentKbOrchestrator orchestrator)
        => new(
            provider,
            orchestrator,
            new FakeContentSiteIndexStore(),
            new ContentKbOrchestratorOptions { ArtifactRoot = Path.Combine(Path.GetTempPath(), "content-kb") },
            new PublishStateDeriver(),
            new CreatorSuppressionRowFilter(new FakeCreatorSuppressionStore(), new FakeCreatorIdentityResolver()));

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
}
