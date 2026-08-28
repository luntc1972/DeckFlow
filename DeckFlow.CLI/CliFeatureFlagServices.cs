using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.CLI;

/// <summary>Registers the CLI's database-backed feature flag services.</summary>
internal static class CliFeatureFlagServices
{
    /// <summary>Adds feature flag services used by CLI Scryfall composition roots.</summary>
    /// <param name="services">Service collection receiving the flag services.</param>
    /// <returns>The supplied service collection.</returns>
    internal static IServiceCollection AddCliFeatureFlags(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<IFeatureFlagStore>(_ => new FeatureFlagStore(
            DeckFlowDatabaseConnectionFactory.CreateFeatureFlagConnection(ResolveArtifactsPath())));
        services.AddSingleton<FeatureFlagCache>();
        services.AddSingleton<IFeatureFlagCache>(serviceProvider =>
            serviceProvider.GetRequiredService<FeatureFlagCache>());
        return services;
    }

    /// <summary>Loads the initial feature flag snapshot before CLI Scryfall services use it.</summary>
    /// <param name="serviceProvider">Provider containing the registered feature flag cache.</param>
    /// <param name="cancellationToken">Token used to cancel initial loading.</param>
    internal static async Task InitializeFeatureFlagsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // Why: without StartAsync, D-13's empty-snapshot default returns true for every key.
        await serviceProvider.GetRequiredService<FeatureFlagCache>()
            .StartAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string ResolveArtifactsPath()
    {
        var dataDir = Environment.GetEnvironmentVariable("MTG_DATA_DIR");
        return string.IsNullOrWhiteSpace(dataDir)
            ? Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "artifacts"))
            : Path.GetFullPath(dataDir);
    }
}
