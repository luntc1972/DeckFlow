using System.Reflection;
using DeckFlow.CLI;
using DeckFlow.Core.Content;
using DeckFlow.Core.Orchestration;
using DeckFlow.Core.Storage;

namespace DeckFlow.Core.Tests;

/// <summary>Verifies suppression dependencies are present in the core composition graph.</summary>
public sealed class CreatorSuppressionRegistrationTests
{
    [Fact]
    public void CoreFactory_RegistersSuppressionStore()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"deckflow-suppression-{Guid.NewGuid():N}.db");
        try
        {
            var orchestrator = ContentKbOrchestratorFactory.Create(
                RelationalDatabaseConnection.FromSqlitePath(databasePath),
                Path.GetTempPath(),
                new ThrowingLlmDistillationService(),
                new ThrowingYouTubeChannelVideoLister(),
                new ThrowingTranscriptSource(),
                new ThrowingFfmpegAudioChunker());

            var sourceStore = (ContentSourceStore)typeof(ContentKbOrchestrator)
                .GetField("_sourceStore", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(orchestrator)!;
            Assert.NotNull(typeof(ContentSourceStore).GetField("_suppressionStore", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sourceStore));
        }
        finally
        {
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public void CliDirectRunners_CreateStoresWithSuppressionDependency()
    {
        var stores = CreatorStyleCommandRunners.CreateStoresForDatabase(Path.GetTempFileName());

        Assert.NotNull(typeof(CreatorStyleStatedRuleStore)
            .GetField("_suppressionStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(stores.StatedRuleStore));
    }
}
