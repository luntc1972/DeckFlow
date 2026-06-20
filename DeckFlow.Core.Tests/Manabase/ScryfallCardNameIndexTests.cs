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

    [Fact]
    public void TryResolve_ByPrinting_ResolvesAlternateName()
    {
        // The resolved card carries its canonical name; the deck entry uses a flavor name.
        // Resolution by set + collector number must still match.
        var index = new ScryfallCardNameIndex();
        var zilortha = new ScryfallCardData
        {
            Name = "Zilortha, Strength Incarnate",
            Set = "iko",
            CollectorNumber = "275",
        };
        index.Add(zilortha);

        Assert.True(index.TryResolve("Godzilla, King of the Monsters", "iko", "275", out ScryfallCardData? hit));
        Assert.Same(zilortha, hit);
    }

    [Fact]
    public void TryResolve_PrintingMatch_IsCaseInsensitive()
    {
        var index = new ScryfallCardNameIndex();
        var card = new ScryfallCardData { Name = "Sol Ring", Set = "C21", CollectorNumber = "263" };
        index.Add(card);

        Assert.True(index.TryResolve("unrelated", "c21", "263", out ScryfallCardData? hit));
        Assert.Same(card, hit);
    }

    [Fact]
    public void TryResolve_NoPrinting_FallsBackToName()
    {
        var index = new ScryfallCardNameIndex();
        ScryfallCardData sol = Card("Sol Ring");
        index.Add(sol);

        // Entry has no set/collector, so resolution falls back to the name.
        Assert.True(index.TryResolve("Sol Ring", null, null, out ScryfallCardData? hit));
        Assert.Same(sol, hit);
    }

    [Fact]
    public void TryResolve_PrintingMiss_FallsBackToName()
    {
        // Set/collector were supplied but that printing was never indexed; the name still resolves.
        var index = new ScryfallCardNameIndex();
        ScryfallCardData sol = Card("Sol Ring");
        index.Add(sol);

        Assert.True(index.TryResolve("Sol Ring", "xxx", "999", out ScryfallCardData? hit));
        Assert.Same(sol, hit);
    }

    [Theory]
    [InlineData(null, "275")]
    [InlineData("iko", null)]
    [InlineData("", "275")]
    [InlineData("iko", "  ")]
    public void PrintingKey_MissingPart_IsNull(string? setCode, string? collectorNumber)
    {
        Assert.Null(ScryfallCardNameIndex.PrintingKey(setCode, collectorNumber));
    }

    [Fact]
    public void PrintingKey_BothPresent_NormalizesToLowerPipe()
    {
        Assert.Equal("iko|275", ScryfallCardNameIndex.PrintingKey(" IKO ", "275"));
    }

    [Fact]
    public void TryResolve_ExactName_IsNotShadowedByAnotherCardsFrontFace()
    {
        // A split card whose front face is "Fire" must not overwrite a real card exactly named
        // "Fire": an exact-name match always wins over a front-face alias.
        var index = new ScryfallCardNameIndex();
        ScryfallCardData split = Card("Fire // Ice");
        var standalone = new ScryfallCardData { Name = "Fire" };
        index.Add(split);
        index.Add(standalone);

        Assert.True(index.TryResolve("Fire", out ScryfallCardData? exact));
        Assert.Same(standalone, exact); // exact name, not the split's "Fire" alias

        Assert.True(index.TryResolve("Fire // Ice", out ScryfallCardData? full));
        Assert.Same(split, full);
    }
}
