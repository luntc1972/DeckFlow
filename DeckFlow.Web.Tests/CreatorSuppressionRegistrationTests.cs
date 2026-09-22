using DeckFlow.Core.Content;
using DeckFlow.Core.Storage;
using DeckFlow.Web.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CreatorSuppressionRegistrationTests
{
    [Fact]
    public void WebComposition_ResolvesAllContentKbStores()
    {
        using var provider = new ServiceCollection()
            .AddDeckFlowContentKbStores(RelationalDatabaseConnection.FromSqlitePath(Path.GetTempFileName()))
            .BuildServiceProvider();

        Assert.IsAssignableFrom<ICreatorSuppressionStore>(provider.GetRequiredService<ICreatorSuppressionStore>());
        Assert.IsAssignableFrom<ICreatorIdentityResolver>(provider.GetRequiredService<ICreatorIdentityResolver>());
        Assert.IsAssignableFrom<IContentVideoStore>(provider.GetRequiredService<IContentVideoStore>());
        Assert.IsAssignableFrom<ICreatorStyleStatedRuleStore>(provider.GetRequiredService<ICreatorStyleStatedRuleStore>());
    }
}
