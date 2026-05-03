using DeckFlow.Web.Services.Harvest;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Extensions;

/// <summary>
/// DI registration extension for the Phase 7 harvest system. Mirrors
/// <see cref="FeatureFlagsServiceCollectionExtensions.AddDeckFlowFeatureFlags"/>:
/// one extension call wires the stores, the singleton-and-hosted-service schedule
/// cache, and the recurring scheduler service.
/// </summary>
public static class HarvestServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Phase 7 harvest services and stores. B1 ordering is intentional:
    /// <see cref="IHarvestStatsAggregator"/> is registered before
    /// <see cref="IHarvestRunStore"/> so the run-store's nullable stats dependency
    /// resolves to a live instance when present.
    /// </summary>
    /// <param name="services">DI service collection.</param>
    /// <param name="env">Web host environment used by harvest store DI ctors.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDeckFlowHarvest(this IServiceCollection services, IWebHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(env);

        // Plan 06 in Wave 5 replaces this scaffold with the real HarvestStatsAggregator.
        services.AddSingleton<IHarvestStatsAggregator, NullHarvestStatsAggregator>();
        services.AddSingleton<IHarvestRunStore, HarvestRunStore>();
        services.AddSingleton<IHarvestScheduleStore, HarvestScheduleStore>();

        services.AddSingleton<HarvestScheduleCache>();
        services.AddSingleton<IHarvestScheduleCache>(sp => sp.GetRequiredService<HarvestScheduleCache>());
        services.AddHostedService(sp => sp.GetRequiredService<HarvestScheduleCache>());

        services.AddHostedService<HarvestScheduleService>();

        return services;
    }

    internal sealed class NullHarvestStatsAggregator : IHarvestStatsAggregator
    {
        public void Invalidate()
        {
        }
    }
}
