using DeckFlow.Web.Services;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.Registry;

namespace DeckFlow.Web.Extensions;

/// <summary>
/// DI registration extension for DeckFlow Scryfall-backed services.
/// Extracts Scryfall lookup, search, set, tagger, and commander-search wiring from Program.cs.
/// </summary>
public static class ScryfallServiceCollectionExtensions
{
    /// <summary>
    /// Registers Scryfall card lookup, card search, set, tagger-lookup, commander-search,
    /// ban-list, spellbook, EDH Top-16, and related support singletons.
    /// </summary>
    /// <remarks>
    /// Depends on: <c>AddDeckFlowHttpClients()</c> (named clients + CookieContainer),
    /// <c>AddDeckFlowResiliencePipelines()</c> (<see cref="ResiliencePipelineProvider{TKey}"/>),
    /// and <c>AddMemoryCache()</c> being registered before this call.
    /// </remarks>
    /// <param name="services">DI service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDeckFlowScryfallServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // IScryfallRestClientFactory - defined in Task 4 with static back-compat shim;
        // full IHttpClientFactory wiring lands in Task 10.
        services.AddSingleton<IScryfallRestClientFactory, ScryfallRestClientFactory>();

        // CSRF + cookie session store for the Tagger flow (D-07, HIGH-2: 270s TTL).
        services.AddSingleton<ITaggerSessionCache, TaggerSessionCache>();

        services.AddSingleton<ICommanderSearchService>(sp =>
            new ScryfallCommanderSearchService(
                sp.GetRequiredService<IScryfallRestClientFactory>(),
                sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                sp.GetRequiredService<IMemoryCache>()));
        services.AddSingleton<ICardSearchService>(sp =>
            new ScryfallCardSearchService(
                sp.GetRequiredService<IScryfallRestClientFactory>(),
                sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                sp.GetRequiredService<IMemoryCache>()));
        services.AddSingleton<CardLookupCache>();
        services.AddSingleton<ICardLookupService>(sp =>
            new ScryfallCardLookupService(
                sp.GetRequiredService<IScryfallRestClientFactory>(),
                sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                sp.GetRequiredService<CardLookupCache>()));
        // Why: a partial container without the flag cache must still compose.
        services.AddSingleton(sp => new ScryfallCollectionCardCache(sp.GetService<IFeatureFlagCache>()));
        services.AddSingleton<IScryfallCardResolver>(sp =>
            new ScryfallCardResolver(
                sp.GetRequiredService<IScryfallRestClientFactory>(),
                sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                sp.GetRequiredService<ScryfallCollectionCardCache>()));
        services.AddSingleton(sp => new ScryfallReferenceResolver(
            sp.GetRequiredService<IScryfallCardResolver>(),
            sp.GetRequiredService<ScryfallCollectionCardCache>()));
        services.AddSingleton<IMechanicLookupService, WotcMechanicLookupService>();
        services.AddSingleton<ICommanderBanListService>(sp =>
            new CommanderBanListService(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                sp.GetRequiredService<IMemoryCache>()));
        services.AddSingleton<ICommanderSpellbookService>(sp =>
            new CommanderSpellbookService(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetService<ILogger<CommanderSpellbookService>>()));
        services.AddSingleton<IScryfallSetService>(sp =>
            new ScryfallSetService(
                sp.GetRequiredService<IScryfallRestClientFactory>(),
                sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<IMechanicLookupService>()));
        services.AddSingleton<IEdhTop16Client, EdhTop16Client>();
        services.AddSingleton<IScryfallTaggerLookupService, ScryfallTaggerLookupService>();

        return services;
    }
}
