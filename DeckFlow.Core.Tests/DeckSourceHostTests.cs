using DeckFlow.Core.Integration;
using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Pure accept/reject matrix for <see cref="DeckSourceHost"/> trust predicates.
/// These tests are the SEC-03 regression guard: a substring <c>Contains</c> host match
/// would pass the look-alike rows and fail this suite.
/// </summary>
public sealed class DeckSourceHostTests
{
    [Theory]
    [InlineData("https://moxfield.com/decks/x", true)]
    [InlineData("https://www.moxfield.com/decks/x", true)]
    [InlineData("https://api.moxfield.com/v2/decks/x", true)]
    [InlineData("HTTPS://MOXFIELD.COM/decks/x", true)]
    [InlineData("https://moxfield.com.evil.tld/decks/x", false)]
    [InlineData("https://evilmoxfield.com/decks/x", false)]
    [InlineData("https://moxfield.com@evil.tld/decks/x", false)]
    [InlineData("https://moxfield.com./decks/x", false)]
    [InlineData("https://archidekt.com/decks/x", false)]
    public void IsMoxfield_VariousHosts_ReturnsExpected(string url, bool expected)
    {
        var uri = new Uri(url);
        Assert.Equal(expected, DeckSourceHost.IsMoxfield(uri));
    }

    [Theory]
    [InlineData("https://archidekt.com/decks/123", true)]
    [InlineData("https://www.archidekt.com/decks/123", true)]
    [InlineData("https://archidekt.com.evil.tld/d/x", false)]
    [InlineData("https://evilarchidekt.com/d/x", false)]
    [InlineData("https://archidekt.com@evil.tld/d/x", false)]
    [InlineData("https://archidekt.com./d/x", false)]
    [InlineData("https://moxfield.com/decks/x", false)]
    public void IsArchidekt_VariousHosts_ReturnsExpected(string url, bool expected)
    {
        var uri = new Uri(url);
        Assert.Equal(expected, DeckSourceHost.IsArchidekt(uri));
    }
}
