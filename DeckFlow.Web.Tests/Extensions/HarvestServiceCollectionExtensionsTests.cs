using DeckFlow.Web.Extensions;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Harvest;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DeckFlow.Web.Tests.Extensions;

/// <summary>
/// Tests for <see cref="HarvestServiceCollectionExtensions"/> verifying that DI registration resolves
/// harvest run store, stats, and hosted services without errors.
/// </summary>
public sealed class HarvestServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDeckFlowHarvest_ResolvesRunStoreStatsAndHostedServices()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"deckflow-harvest-di-{Guid.NewGuid():N}");
        var environment = new FakeWebHostEnvironment(contentRoot);

        var services = new ServiceCollection();
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddSingleton<ICategoryKnowledgeStore, FakeCategoryKnowledgeStore>();
        services.AddSingleton<IFeatureFlagCache, FakeFeatureFlagCache>();
        services.AddSingleton<IArchidektCacheJobService, FakeArchidektCacheJobService>();
        services.AddMemoryCache();
        services.AddLogging();
        services.AddDeckFlowHarvest(environment);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Assert.IsType<HarvestRunStore>(provider.GetRequiredService<IHarvestRunStore>());
        Assert.IsType<HarvestStatsAggregator>(provider.GetRequiredService<IHarvestStatsAggregator>());
        Assert.IsType<HarvestScheduleCache>(provider.GetRequiredService<IHarvestScheduleCache>());

        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        Assert.Contains(hostedServices, service => service is HarvestScheduleCache);
        Assert.Contains(hostedServices, service => service is HarvestScheduleService);
    }

    private sealed class FakeArchidektCacheJobService : IArchidektCacheJobService
    {
        public Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(
            TimeSpan duration,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The DI canary only resolves services.");

        public ArchidektCacheJobStatus? GetJob(Guid jobId) => null;

        public ArchidektCacheJobStatus? GetActiveJob() => null;

        public Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class FakeWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DeckFlow.Web.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = contentRootPath;

        public string EnvironmentName { get; set; } = Environments.Development;

        public string WebRootPath { get; set; } = contentRootPath;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
