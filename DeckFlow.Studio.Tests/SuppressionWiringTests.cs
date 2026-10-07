using System.Reflection;
using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;
using DeckFlow.Studio.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace DeckFlow.Studio.Tests;

public sealed class SuppressionWiringTests
{
    [Fact]
    public async Task StudioRegistration_ContentSourceFilterUsesRegisteredResolver()
    {
        var databasePath = Path.GetTempFileName();
        ServiceProvider? provider = null;
        try
        {
            provider = new ServiceCollection().AddStudioContentKbStores(databasePath).BuildServiceProvider();
            var source = Assert.IsType<ContentSourceStore>(provider.GetRequiredService<IContentSourceStore>());
            Assert.IsAssignableFrom<IContentSiteIndexStore>(provider.GetRequiredService<IContentSiteIndexStore>());
            var resolver = provider.GetRequiredService<ICreatorIdentityResolver>();
            var filter = Assert.IsType<CreatorSuppressionFilter>(typeof(ContentSourceStore)
                .GetField("_suppressionFilter", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(source));
            var lazyResolver = Assert.IsType<Lazy<ICreatorIdentityResolver>>(typeof(CreatorSuppressionFilter)
                .GetField("_resolver", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(filter));

            Assert.Same(resolver, lazyResolver.Value);
            await source.ListEnabledSourcesAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            provider?.Dispose();
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.GetFullPath(databasePath)}"));
            File.Delete(databasePath);
        }
    }
}
