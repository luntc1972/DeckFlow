using System.Net.Http.Headers;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.CardGrounding;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.CreatorStyle;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Extensions;

/// <summary>
/// DI registration extension for the creator-style engine.
/// </summary>
public static class CreatorStyleServiceCollectionExtensions
{
    /// <summary>
    /// Registers the single creator-style service graph for the admin-only creator-style engine (D-10).
    /// </summary>
    /// <param name="services">DI service collection.</param>
    /// <param name="environment">Web host environment used to resolve local artifact paths.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDeckFlowCreatorStyle(this IServiceCollection services, IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddHttpClient("archidekt-owner", client =>
        {
            client.BaseAddress = new Uri("https://archidekt.com/");
            client.MaxResponseContentBufferSize = ArchidektOwnerClient.MaxResponseBytes;
            // Why: TryAddWithoutValidation keeps an unexpected version string from throwing FormatException; D-13 replaces the legacy agent.
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", ArchidektUserAgent.Value);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddHttpClient("moxfield-owner", client =>
        {
            client.BaseAddress = new Uri("https://api2.moxfield.com/");
            client.MaxResponseContentBufferSize = MoxfieldOwnerClient.MaxResponseBytes;
            // Why: Moxfield's Cloudflare front rejects non-browser agents; match MoxfieldApiDeckImporter.
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", MoxfieldOwnerClient.BrowserUserAgent);
            client.DefaultRequestHeaders.Referrer = new Uri("https://moxfield.com/");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        });

        services.AddSingleton<ICardNameGrounder, ScryfallCardNameGrounder>();
        services.AddSingleton<ICardGroundingGuard, CardGroundingGuard>();

        services.AddSingleton<ICreatorDeckCacheStore>(provider =>
            new CreatorDeckCacheStore(
                DeckFlowDatabaseConnectionFactory.CreateCreatorDeckCacheConnection(environment),
                suppressionStore: provider.GetRequiredService<ICreatorSuppressionStore>(),
                identityResolver: new Lazy<ICreatorIdentityResolver>(() => provider.GetRequiredService<ICreatorIdentityResolver>())));
        services.AddSingleton<ICreatorProfileSourceStore>(_ =>
            new CreatorProfileSourceStore(
                DeckFlowDatabaseConnectionFactory.CreateCreatorDeckCacheConnection(environment)));
        services.AddSingleton(_ =>
            new CategoryKnowledgeRepository(
                DeckFlowDatabaseConnectionFactory.CreateCategoryKnowledgeConnection(environment)));
        services.AddSingleton<ICreatorStyleProfileStore>(provider =>
            // Why: creator-style profiles live in the local-only content-kb DB because production never crawls; it only reads git-shipped seeds.
            new CreatorStyleProfileStore(
                DeckFlowDatabaseConnectionFactory.CreateLocalContentKbConnection(environment),
                suppressionStore: provider.GetRequiredService<ICreatorSuppressionStore>(),
                identityResolver: new Lazy<ICreatorIdentityResolver>(() => provider.GetRequiredService<ICreatorIdentityResolver>())));

        services.AddSingleton<ICreatorSuppressionGate, CreatorSuppressionGate>();
        services.AddSingleton<CreatorWhitelistPoolBuilder>();
        services.AddSingleton<ICreatorStyleSeedLoader, CreatorStyleSeedLoader>();
        services.AddScoped<IArchidektOwnerClient, ArchidektOwnerClient>();
        services.AddSingleton<MoxfieldRequestPacer>();
        services.AddScoped<IMoxfieldOwnerClient, MoxfieldOwnerClient>();

        services.AddScoped<CreatorProfileDeckCrawler>();
        services.AddScoped<CreatorDeckCategoryResolver>();
        services.AddScoped<MeasuredStyleProfileBuilder>();
        services.AddScoped<ISubmittedDeckStatsBuilder, SubmittedDeckStatsBuilder>();
        services.AddScoped<ICreatorStylePacketService, CreatorStylePacketService>();

        return services;
    }
}
