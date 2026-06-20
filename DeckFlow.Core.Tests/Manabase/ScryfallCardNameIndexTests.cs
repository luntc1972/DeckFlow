using DeckFlow.Core.Manabase;

using Xunit;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Validates <see cref="ScryfallCardNameIndex"/> name resolution: case/whitespace
/// normalization, multi-faced front-face fallback, and not-found handling.
/// </summary>
public sealed class ScryfallCardNameIndexTests
{
    private static ScryfallCardData Card(string name) => new() { Name = name };

    [Fact]
    public void TryResolve_ExactName_Matches()
    {
        var index = new ScryfallCardNameIndex();
        ScryfallCardData sol = Card("Sol Ring");
        index.Add(sol);

        Assert.True(index.TryResolve("Sol Ring", out ScryfallCardData? hit));
        Assert.Same(sol, hit);
    }

    [Theory]
    [InlineData("SOL RING")]
    [InlineData("sol ring")]
    [InlineData("  Sol Ring  ")]
    public void TryResolve_IsCaseAndWhitespaceInsensitive(string query)
    {
        var index = new ScryfallCardNameIndex();
        ScryfallCardData sol = Card("Sol Ring");
        index.Add(sol);

        Assert.True(index.TryResolve(query, out ScryfallCardData? hit));
        Assert.Same(sol, hit);
    }

    [Fact]
    public void TryResolve_MultiFacedCard_MatchesByFullNameAndFrontFace()
    {
        var index = new ScryfallCardNameIndex();
        ScryfallCardData fire = Card("Fire // Ice");
        index.Add(fire);

        Assert.True(index.TryResolve("Fire // Ice", out ScryfallCardData? full));
        Assert.Same(fire, full);

        // An entry written as just the front face still resolves.
        Assert.True(index.TryResolve("Fire", out ScryfallCardData? front));
        Assert.Same(fire, front);
    }

    [Fact]
    public void TryResolve_EntryWithFrontFaceOnly_MatchesCardIndexedByFullName()
    {
        var index = new ScryfallCardNameIndex();
        ScryfallCardData card = Card("Wear // Tear");
        index.Add(card);

        Assert.True(index.TryResolve("  wear  ", out ScryfallCardData? hit));
        Assert.Same(card, hit);
    }

    [Fact]
    public void TryResolve_EntryAsFullName_MatchesCardIndexedByFrontFace()
    {
        // The deck entry carries the full "A // B" name but only the front face was indexed.
        var index = new ScryfallCardNameIndex();
        ScryfallCardData card = Card("Fire");
        index.Add(card);

        Assert.True(index.TryResolve("Fire // Ice", out ScryfallCardData? hit));
        Assert.Same(card, hit);
    }

    [Fact]
    public void TryResolve_UnknownName_ReturnsFalseAndNull()
    {
        var index = new ScryfallCardNameIndex();
        index.Add(Card("Sol Ring"));

        Assert.False(index.TryResolve("Mana Crypt", out ScryfallCardData? hit));
        Assert.Null(hit);
    }

    [Fact]
    public void Add_DuplicateKey_LastWriteWins()
    {
        var index = new ScryfallCardNameIndex();
        index.Add(Card("Forest"));
        ScryfallCardData second = Card("Forest");
        index.Add(second);

        Assert.True(index.TryResolve("forest", out ScryfallCardData? hit));
        Assert.Same(second, hit);
    }
}
