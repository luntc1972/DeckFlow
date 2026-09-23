using DeckFlow.Core.Content;
using DeckFlow.Web.Services.CreatorStyle;

namespace DeckFlow.Web.Tests;

internal static class CreatorSuppressionGateFixture
{
    public static ICreatorSuppressionGate LinkedRepresentation(string representation, string suppressedRepresentation)
    {
        var store = new FakeCreatorSuppressionStore();
        store.Rows.Add(new CreatorSuppression
        {
            Slug = suppressedRepresentation,
            Aliases = [],
            Reason = "test",
            RequestedUtc = new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero),
        });
        var resolver = new FakeCreatorIdentityResolver();
        resolver.Identities[representation] = new CreatorIdentity("canonical", [], [suppressedRepresentation], []);
        return new CreatorSuppressionGate(resolver, store);
    }
}
