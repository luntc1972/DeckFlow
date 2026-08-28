using DeckFlow.Core.Loading;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Manabase;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeckFlow.Web.Extensions;

/// <summary>DI registration extension for DeckFlow manabase services.</summary>
public static class ManabaseServiceCollectionExtensions
{
    /// <summary>Registers the scoped manabase analysis service.</summary>
    /// <remarks>
    /// Depends on the deck entry loader, Scryfall services, cache, and optional support services
    /// being registered before this call.
    /// </remarks>
    /// <param name="services">DI service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDeckFlowManabaseServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IManabaseAnalysisService>(sp =>
            new ManabaseAnalysisService(
                sp.GetRequiredService<IDeckEntryLoader>(),
                sp.GetRequiredService<IScryfallCardResolver>(),
                sp.GetService<IFeatureFlagCache>(),
                sp.GetService<ICategoryKnowledgeStore>(),
                sp.GetService<ICommanderSpellbookService>(),
                sp.GetService<ILogger<ManabaseAnalysisService>>(),
                sp.GetService<ICedhLandBaselineProvider>(),
                sp.GetService<IManabaseBaselineProvider>(),
                // Why: require the singleton so a dropped registration fails loudly instead of disabling the cache.
                sp.GetRequiredService<ScryfallCollectionCardCache>(),
                // Why: require the singleton so a dropped registration fails loudly instead of disabling the shared protocol.
                sp.GetRequiredService<IScryfallCollectionProtocol>()));

        return services;
    }
}
