using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Extensions;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.Bracket;
using DeckFlow.Web.Services.PromptBuilders.Comparison;
using DeckFlow.Web.Services.PromptBuilders.Evolution;
using DeckFlow.Web.Services.PromptBuilders.FollowUp;
using DeckFlow.Web.Services.PromptBuilders.MetaGap;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DeckFlow.Web.Tests.Extensions;

/// <summary>
/// Smoke test that builds a <see cref="ServiceProvider"/> with
/// <see cref="ServiceProviderOptions.ValidateOnBuild"/> = true and resolves the four
/// packet-service interfaces and eight prompt-variant registries extracted from Program.cs.
/// </summary>
/// <remarks>
/// Why this test exists: the existing Web test suite does not boot the host/DI graph,
/// so a dropped or misordered extracted registration would pass a plain build silently.
/// This test closes that gap (Codex 53-02 MED). No new NuGet package — ServiceCollection
/// and BuildServiceProvider are already used across DeckFlow.Web.Tests.
/// </remarks>
public sealed class DiCompositionExtensionsTests
{
    [Fact]
    public void AddDeckFlowExtensions_ValidateOnBuild_ResolvesPacketServicesAndPromptVariantRegistries()
    {
        var services = new ServiceCollection();

        // Framework prerequisites
        var contentRoot = Path.Combine(Path.GetTempPath(), $"deckflow-di-{Guid.NewGuid():N}");
        services.AddSingleton<IWebHostEnvironment>(new StubWebHostEnvironment(contentRoot));
        services.AddLogging();
        services.AddMemoryCache();
        services.AddHttpClient();
        services.AddOptions();

        // AiPlatformOptions — required by DeckPrimerPacketService
        services.Configure<AiPlatformOptions>(_ => { });

        // Feature flags — required by ScryfallTaggerLookupService
        services.AddDeckFlowFeatureFlags();

        // Resilience pipelines — required by Scryfall services
        services.AddDeckFlowResiliencePipelines();

        // Http clients + Scryfall services (the two Task-1 extensions)
        services.AddDeckFlowHttpClients();
        services.AddDeckFlowScryfallServices();

        // Prompt variants (the Task-2 extension)
        services.AddDeckFlowPromptVariants();

        // IDeckEntryLoader, ICategoryKnowledgeStore, and IGameChangerCatalogService are registered
        // inline in Program.cs, not part of the packet-services group. Provide them so ValidateOnBuild
        // resolves (DeckAnalysisPacketService takes IGameChangerCatalogService as of Phase 77-04).
        services.AddScoped<IDeckEntryLoader, StubDeckEntryLoader>();
        services.AddSingleton<ICategoryKnowledgeStore, FakeCategoryKnowledgeStore>();
        services.AddSingleton<DeckFlow.Web.Services.Bracket.IGameChangerCatalogService,
            DeckFlow.Web.Services.Bracket.GameChangerCatalogService>();

        // Packet services (the Task-2 extension)
        services.AddDeckFlowPacketServices();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        // Resolve scoped packet services inside a scope
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IDeckAnalysisPacketService>());
        Assert.NotNull(sp.GetRequiredService<IDeckComparisonService>());
        Assert.NotNull(sp.GetRequiredService<IMetaGapService>());
        Assert.NotNull(sp.GetRequiredService<IDeckPrimerPacketService>());

        // Resolve the eight prompt-variant registries (singletons)
        Assert.NotNull(sp.GetRequiredService<AnalysisPromptVariantRegistry>());
        Assert.NotNull(sp.GetRequiredService<SetUpgradePromptVariantRegistry>());
        Assert.NotNull(sp.GetRequiredService<ComparisonPromptVariantRegistry>());
        Assert.NotNull(sp.GetRequiredService<FollowUpPromptVariantRegistry>());
        Assert.NotNull(sp.GetRequiredService<MetaGapPromptVariantRegistry>());
        Assert.NotNull(sp.GetRequiredService<PrimerPromptVariantRegistry>());
        Assert.NotNull(sp.GetRequiredService<BracketPromptVariantRegistry>());
        Assert.NotNull(sp.GetRequiredService<EvolutionPromptVariantRegistry>());
    }

    private sealed class StubWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    /// <summary>
    /// Minimal stub that satisfies <see cref="IDeckEntryLoader"/> for the DI canary.
    /// None of these methods are called; they exist only to satisfy the interface contract.
    /// </summary>
    private sealed class StubDeckEntryLoader : IDeckEntryLoader
    {
        public Task<List<DeckEntry>> LoadAsync(
            DeckLoadRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("DI canary only — do not call.");

        public Task<DeckSourceLoadResult> LoadFromSourceAsync(
            string deckSource,
            UnrecognizedPasteBehavior unrecognizedBehavior = UnrecognizedPasteBehavior.ThrowNotRecognized,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("DI canary only — do not call.");

        public void ValidateCommanderDeckSize(
            string systemName,
            IReadOnlyList<DeckEntry> entries,
            int requiredDeckSize = 100)
            => throw new NotSupportedException("DI canary only — do not call.");
    }
}
