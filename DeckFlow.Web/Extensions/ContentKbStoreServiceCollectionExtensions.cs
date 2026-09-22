using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Web.Extensions;

/// <summary>Registers content knowledge-base stores used by the web host.</summary>
public static class ContentKbStoreServiceCollectionExtensions
{
    /// <summary>Registers content knowledge-base stores using the supplied connection.</summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="connection">Content knowledge-base connection.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddDeckFlowContentKbStores(this IServiceCollection services, RelationalDatabaseConnection connection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connection);

        services.AddSingleton<ICreatorSuppressionStore>(_ => new CreatorSuppressionStore(connection));
        services.AddSingleton<IContentSourceStore>(provider => new ContentSourceStore(
            connection,
            provider.GetRequiredService<ICreatorSuppressionStore>()));
        services.AddSingleton<IContentVideoStore>(provider => new ContentVideoStore(
            connection,
            provider.GetRequiredService<ICreatorSuppressionStore>()));
        services.AddSingleton<IContentSiteIndexStore>(provider => new ContentSiteIndexStore(
            connection,
            suppressionStore: provider.GetRequiredService<ICreatorSuppressionStore>()));
        services.AddSingleton<ICreatorStyleStatedRuleStore>(provider => new CreatorStyleStatedRuleStore(
            connection,
            suppressionStore: provider.GetRequiredService<ICreatorSuppressionStore>()));
        services.AddSingleton<ICreatorIdentityResolver, CreatorIdentityResolver>();
        return services;
    }
}
