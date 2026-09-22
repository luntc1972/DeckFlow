using DeckFlow.Core.Content;
using DeckFlow.Studio.Extensions;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DeckFlow.Studio.Tests;

public sealed class CreatorSuppressionRegistrationTests
{
    [Fact]
    public void StudioLocalComposition_ResolvesContentKbStores()
    {
        using var provider = new ServiceCollection()
            .AddStudioContentKbStores(Path.GetTempFileName())
            .BuildServiceProvider();

        Assert.IsAssignableFrom<ICreatorSuppressionStore>(provider.GetRequiredService<ICreatorSuppressionStore>());
        Assert.IsAssignableFrom<ICreatorIdentityResolver>(provider.GetRequiredService<ICreatorIdentityResolver>());
        Assert.IsAssignableFrom<IContentVideoStore>(provider.GetRequiredService<IContentVideoStore>());
    }

    [Fact]
    public void StudioProdFactory_CreatesStoreWithSuppressionDependency()
    {
        var store = new ProdStoreFactory().Create("Host=localhost;Database=deckflow;Username=test;Password=test");

        Assert.NotNull(typeof(ContentSiteIndexStore)
            .GetField("_suppressionStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(store));
    }
}
