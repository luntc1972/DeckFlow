using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.CLI;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Tests;

public sealed class CliFeatureFlagServicesTests : IDisposable
{
    private const string CacheFlagKey = "service.scryfall-collection-cache.enabled";
    private readonly string _artifactsPath = Path.Combine(Path.GetTempPath(), $"deckflow-cli-flags-{Guid.NewGuid():N}");
    private readonly string? _previousArtifactsPath = Environment.GetEnvironmentVariable("MTG_DATA_DIR");

    public CliFeatureFlagServicesTests()
    {
        Environment.SetEnvironmentVariable("MTG_DATA_DIR", _artifactsPath);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("MTG_DATA_DIR", _previousArtifactsPath);
        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (Directory.Exists(_artifactsPath))
        {
            Directory.Delete(_artifactsPath, recursive: true);
        }
    }

    [Fact]
    public void AddCliFeatureFlags_ResolvesFlagAndScryfallCaches()
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddCliFeatureFlags();
        services.AddSingleton(serviceProvider => new ScryfallCollectionCardCache(
            serviceProvider.GetService<IFeatureFlagCache>()));

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<FeatureFlagCache>(provider.GetRequiredService<IFeatureFlagCache>());
        Assert.NotNull(provider.GetRequiredService<ScryfallCollectionCardCache>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InitializeFeatureFlagsAsync_AppliesDatabaseFlagToCollectionCache(bool enabled)
    {
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddCliFeatureFlags();
        services.AddSingleton(serviceProvider => new ScryfallCollectionCardCache(
            serviceProvider.GetService<IFeatureFlagCache>()));
        using ServiceProvider provider = services.BuildServiceProvider();
        IFeatureFlagStore store = provider.GetRequiredService<IFeatureFlagStore>();
        await store.SetEnabledAsync(CacheFlagKey, enabled);

        await CliFeatureFlagServices.InitializeFeatureFlagsAsync(provider, CancellationToken.None);
        var cache = provider.GetRequiredService<ScryfallCollectionCardCache>();
        cache.SetNamePositive("sol-ring", new ScryfallCard("Sol Ring", null, "Artifact", null, null, null, null, null, null, null, null));

        if (!enabled)
        {
            await store.SetEnabledAsync(CacheFlagKey, true);
            await provider.GetRequiredService<IFeatureFlagCache>().ReloadAsync();
        }

        Assert.Equal(enabled, cache.TryGetName("sol-ring", out _));
    }
}
