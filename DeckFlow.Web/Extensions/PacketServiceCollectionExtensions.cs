using DeckFlow.Core.Loading;
using DeckFlow.Core.Parsing;
using DeckFlow.Web.Configuration;
using DeckFlow.Web.Services;
using DeckFlow.Web.Services.Bracket;
using DeckFlow.Web.Services.FeatureFlags;
using DeckFlow.Web.Services.PromptBuilders.Analysis;
using DeckFlow.Web.Services.PromptBuilders.Comparison;
using DeckFlow.Web.Services.PromptBuilders.FollowUp;
using DeckFlow.Web.Services.PromptBuilders.MetaGap;
using DeckFlow.Web.Services.PromptBuilders.Primer;
using DeckFlow.Web.Services.PromptBuilders.SetUpgrade;
using DeckFlow.Web.Services.Scryfall;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeckFlow.Web.Extensions;

/// <summary>
/// DI registration extension for the DeckFlow packet-building services.
/// Extracts the four scoped packet-service factories and <see cref="PacketSessionCache"/>
/// from Program.cs into a single extension call.
/// </summary>
public static class PacketServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PacketSessionCache"/> (singleton) and the four scoped
    /// packet-service factories:
    /// <see cref="IDeckAnalysisPacketService"/>, <see cref="IDeckComparisonService"/>,
    /// <see cref="IMetaGapService"/>, <see cref="IDeckPrimerPacketService"/>.
    /// </summary>
    /// <remarks>
    /// Depends on: <c>AddDeckFlowScryfallServices()</c> (IScryfallCardResolver, banlist,
    /// spellbook, set, EDH), <c>AddDeckFlowPromptVariants()</c> (registries), and
    /// <c>ICategoryKnowledgeStore</c> + <c>IDeckEntryLoader</c> registered separately in
    /// Program.cs before the <c>builder.Build()</c> call.
    /// </remarks>
    /// <param name="services">DI service collection.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddDeckFlowPacketServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<PacketSessionCache>();
        services.AddTransient<MoxfieldParser>();
        services.AddTransient<ArchidektParser>();

        services.AddScoped<IDeckAnalysisPacketService>(sp =>
            new DeckAnalysisPacketService(
                sp.GetRequiredService<IScryfallCardResolver>(),
                sp.GetRequiredService<ScryfallReferenceResolver>(),
                sp.GetRequiredService<IDeckEntryLoader>(),
                sp.GetRequiredService<IMechanicLookupService>(),
                sp.GetRequiredService<ICommanderBanListService>(),
                sp.GetRequiredService<IScryfallSetService>(),
                sp.GetRequiredService<ICommanderSpellbookService>(),
                sp.GetRequiredService<IGameChangerCatalogService>(),
                sp.GetRequiredService<AnalysisPromptVariantRegistry>(),
                sp.GetRequiredService<SetUpgradePromptVariantRegistry>(),
                sp.GetRequiredService<PacketSessionCache>(),
                sp.GetService<IFeatureFlagCache>(),
                sp.GetService<ILogger<DeckAnalysisPacketService>>(),
                sp.GetRequiredService<IScryfallCollectionProtocol>()));
        services.AddScoped<IDeckComparisonService>(sp =>
            new DeckComparisonService(
                sp.GetRequiredService<IScryfallCardResolver>(),
                sp.GetRequiredService<ScryfallReferenceResolver>(),
                sp.GetRequiredService<IDeckEntryLoader>(),
                sp.GetRequiredService<ICommanderSpellbookService>(),
                sp.GetRequiredService<ComparisonPromptVariantRegistry>(),
                sp.GetRequiredService<FollowUpPromptVariantRegistry>(),
                sp.GetRequiredService<PacketSessionCache>(),
                sp.GetService<ILogger<DeckComparisonService>>()));
        services.AddScoped<IMetaGapService>(sp =>
            new MetaGapService(
                sp.GetRequiredService<IScryfallCardResolver>(),
                sp.GetRequiredService<ScryfallReferenceResolver>(),
                sp.GetRequiredService<IDeckEntryLoader>(),
                sp.GetRequiredService<IEdhTop16Client>(),
                sp.GetRequiredService<ICommanderSpellbookService>(),
                sp.GetRequiredService<MetaGapPromptVariantRegistry>(),
                sp.GetRequiredService<PacketSessionCache>()));
        services.AddScoped<IDeckPrimerPacketService>(sp =>
            new DeckPrimerPacketService(
                sp.GetRequiredService<IDeckEntryLoader>(),
                sp.GetRequiredService<ICommanderSpellbookService>(),
                sp.GetRequiredService<IEdhTop16Client>(),
                sp.GetRequiredService<ICategoryKnowledgeStore>(),
                sp.GetRequiredService<PrimerPromptVariantRegistry>(),
                sp.GetRequiredService<PacketSessionCache>(),
                sp.GetRequiredService<IOptions<AiPlatformOptions>>(),
                sp.GetRequiredService<MoxfieldParser>(),
                sp.GetRequiredService<ArchidektParser>(),
                sp.GetService<ILogger<DeckPrimerPacketService>>()));

        return services;
    }
}
