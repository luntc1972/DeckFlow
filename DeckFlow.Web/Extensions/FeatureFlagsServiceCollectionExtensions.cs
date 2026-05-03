using DeckFlow.Web.Services.FeatureFlags;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Extensions;

/// <summary>
/// DI registration extension for the Phase 6 feature-flag system (FLAG-01..05).
/// Mirrors the AddDeckFlowResiliencePipelines() pattern: one extension call wires the
/// store + the singleton-and-hosted-service cache so Program.cs holds a single line.
/// </summary>
public static class FeatureFlagsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IFeatureFlagStore"/> + <see cref="FeatureFlagCache"/>
    /// (singleton + IHostedService). Call from Program.cs alongside the other
    /// AddSingleton store registrations.
    /// </summary>
    /// <param name="services">DI service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDeckFlowFeatureFlags(this IServiceCollection services)
    {
        services.AddSingleton<IFeatureFlagStore, FeatureFlagStore>();
        services.AddSingleton<FeatureFlagCache>();
        services.AddSingleton<IFeatureFlagCache>(sp => sp.GetRequiredService<FeatureFlagCache>());
        services.AddHostedService(sp => sp.GetRequiredService<FeatureFlagCache>());
        return services;
    }
}
