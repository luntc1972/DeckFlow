using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Studio.Extensions;

/// <summary>Registers local content knowledge-base stores used by Studio.</summary>
public static class ContentKbStoreServiceCollectionExtensions
{
    /// <summary>Registers local content knowledge-base stores using a SQLite database path.</summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="databasePath">SQLite content knowledge-base database path.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddStudioContentKbStores(this IServiceCollection services, string databasePath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        services.AddSingleton<ICreatorSuppressionStore>(_ => new CreatorSuppressionStore(
            RelationalDatabaseConnection.FromSqlitePath(databasePath)));
        services.AddSingleton<IContentSourceStore>(provider => new ContentSourceStore(
            databasePath,
            provider.GetRequiredService<ICreatorSuppressionStore>()));
        services.AddSingleton<IContentVideoStore>(provider => new ContentVideoStore(
            databasePath,
            provider.GetRequiredService<ICreatorSuppressionStore>()));
        services.AddSingleton<IContentSiteIndexStore>(provider => new ContentSiteIndexStore(
            databasePath,
            provider.GetRequiredService<ICreatorSuppressionStore>()));
        services.AddSingleton<ICreatorIdentityResolver, CreatorIdentityResolver>();
        return services;
    }
}
