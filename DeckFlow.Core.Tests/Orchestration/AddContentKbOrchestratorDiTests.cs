using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Orchestration;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Core.Tests;

public sealed class AddContentKbOrchestratorDiTests
{
    [Fact]
    public void AddContentKbOrchestrator_ResolvesFacadeAndSlicesToOneScopedConcrete()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var concrete = scope.ServiceProvider.GetRequiredService<ContentKbOrchestrator>();
        var facade = scope.ServiceProvider.GetRequiredService<IContentKbOrchestrator>();
        var harvest = scope.ServiceProvider.GetRequiredService<IHarvestOrchestrator>();
        var distill = scope.ServiceProvider.GetRequiredService<IDistillOrchestrator>();
        var maintenance = scope.ServiceProvider.GetRequiredService<IContentMaintenanceOrchestrator>();
        var sourceManager = scope.ServiceProvider.GetRequiredService<IContentSourceManager>();
        var exporter = scope.ServiceProvider.GetRequiredService<IContentIndexExporter>();

        Assert.Same(concrete, facade);
        Assert.Same(concrete, harvest);
        Assert.Same(concrete, distill);
        Assert.Same(concrete, maintenance);
        Assert.Same(concrete, sourceManager);
        Assert.Same(concrete, exporter);
    }

    [Fact]
    public void AddContentKbOrchestrator_ResolvesDifferentScopedInstancesAcrossScopes()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<ContentKbOrchestrator>();
        var second = secondScope.ServiceProvider.GetRequiredService<ContentKbOrchestrator>();

        Assert.NotSame(first, second);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IContentSourceStore>(new FakeContentSourceStore([]));
        services.AddSingleton<IContentVideoStore>(new FakeContentVideoStore());
        services.AddSingleton<IContentSiteIndexStore>(new FakeContentSiteIndexStore());
        services.AddSingleton<IBlockedVideoStore>(new ThrowingBlockedVideoStore());
        services.AddSingleton<IContentHarvestRunStore>(new FakeContentHarvestRunStore());
        services.AddSingleton<ILlmSpendLedger>(new FakeLlmSpendLedger());
        services.AddSingleton<IWhisperSpendLedger>(new ThrowingWhisperSpendLedger());
        services.AddSingleton<ILlmDistillationService>(new FakeLlmDistillationService());
        services.AddSingleton<IYouTubeChannelVideoLister>(new ThrowingYouTubeChannelVideoLister());
        services.AddSingleton<ITranscriptSource>(new ThrowingTranscriptSource());
        services.AddSingleton<IFfmpegAudioChunker>(new ThrowingFfmpegAudioChunker());
        services.AddSingleton<Func<DateTimeOffset>>(() => DateTimeOffset.UtcNow);
        services.AddSingleton(new ContentKbOrchestratorOptions
        {
            ArtifactRoot = Path.Combine(Path.GetTempPath(), $"deckflow-di-{Guid.NewGuid():N}"),
        });

        services.AddContentKbOrchestrator();

        return services;
    }
}
