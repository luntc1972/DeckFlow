using DeckFlow.Core.Integration;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Tests for <see cref="EdhrecCardLookup"/> covering name slugification and EDHREC API URL construction.
/// </summary>
public sealed class EdhrecLookupTests
{
    [Fact]
    public void EdhrecCardLookup_SlugifiesNames()
    {
        Assert.Equal("wandering-archaic-explore-the-vastlands", EdhrecCardLookup.Slugify("Wandering Archaic // Explore the Vastlands"));
        Assert.Equal("bello-bard-of-the-brambles", EdhrecCardLookup.Slugify("Bello, Bard of the Brambles"));
    }
}
