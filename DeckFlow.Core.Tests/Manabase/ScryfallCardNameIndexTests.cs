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

    private static ScryfallCardData Printing(string name, string set, string collectorNumber)
        => new() { Name = name, Set = set, CollectorNumber = collectorNumber };

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
    public void Add_DuplicateKeyFullTie_IncumbentWins()
    {
        var index = new ScryfallCardNameIndex();
        ScryfallCardData first = Card("Forest");
        index.Add(first);
        index.Add(Card("Forest"));

        Assert.True(index.TryResolve("forest", out ScryfallCardData? hit));
        Assert.Same(first, hit);
    }

    [Fact]
    public void Add_CollidingNames_PickTheSameWinnerInEitherOrder()
    {
        // Why: the whole point of the precedence rule. Insertion order is Scryfall's response
        // order, which is not a contract, so the winner must not depend on it.
        ScryfallCardData Winner() => Printing("Forest", "aaa", "1");
        ScryfallCardData Loser() => Printing("Forest", "zzz", "9");

        var forward = new ScryfallCardNameIndex();
        forward.Add(Winner());
        forward.Add(Loser());

        var reverse = new ScryfallCardNameIndex();
        reverse.Add(Loser());
        reverse.Add(Winner());

        Assert.True(forward.TryResolve("forest", out ScryfallCardData? forwardHit));
        Assert.True(reverse.TryResolve("forest", out ScryfallCardData? reverseHit));
        Assert.Equal("aaa", forwardHit!.Set);
        Assert.Equal("aaa", reverseHit!.Set);
    }

    [Fact]
    public void Add_CardWithPrinting_BeatsCardWithout()
    {
        // Why: a card carrying set + collector is strictly more identifiable, so it outranks one
        // that carries neither regardless of which arrived first.
        var index = new ScryfallCardNameIndex();
        index.Add(Printing("Forest", "zzz", "9"));
        index.Add(Card("Forest"));

        Assert.True(index.TryResolve("forest", out ScryfallCardData? hit));
        Assert.Equal("zzz", hit!.Set);
    }

    [Fact]
    public void Add_HigherPriority_BeatsTheBetterPrintingInEitherOrder()
    {
        // Why: the caller knows things the card does not -- which submission it was matched to, and
        // how confidently. Priority lets it say so instead of encoding it in call order. "aaa|1"
        // would win the printing tiebreak, so only priority can put "zzz" in the slot.
        var forward = new ScryfallCardNameIndex();
        forward.Add(Printing("Forest", "aaa", "1"), priority: 0);
        forward.Add(Printing("Forest", "zzz", "9"), priority: 5);

        var reverse = new ScryfallCardNameIndex();
        reverse.Add(Printing("Forest", "zzz", "9"), priority: 5);
        reverse.Add(Printing("Forest", "aaa", "1"), priority: 0);

        Assert.True(forward.TryResolve("forest", out ScryfallCardData? forwardHit));
        Assert.True(reverse.TryResolve("forest", out ScryfallCardData? reverseHit));
        Assert.Equal("zzz", forwardHit!.Set);
        Assert.Equal("zzz", reverseHit!.Set);
    }

    [Fact]
    public void Add_FrontFaceAlias_UsesTheSamePrecedenceRule()
    {
        // Why: _byFrontFace was last-write-wins alongside _byName. One type, one collision rule.
        var index = new ScryfallCardNameIndex();
        index.Add(Printing("Fire // Ice", "aaa", "1"));
        index.Add(Printing("Fire // Ice", "zzz", "9"));

        Assert.True(index.TryResolve("Fire", out ScryfallCardData? hit));
        Assert.Equal("aaa", hit!.Set);
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

    [Fact]
    public void Add_NormalizedExactNameCollision_PreservesExactNameWinnerOverDfcAlias()
    {
        var index = new ScryfallCardNameIndex();
        ScryfallCardData split = Card("Fire // Ice");
        ScryfallCardData standalone = Card("  FIRE  ");
        index.Add(split);
        index.Add(standalone);

        Assert.True(index.TryResolve("fire", out ScryfallCardData? hit));
        Assert.Same(standalone, hit);
        Assert.True(index.TryResolve("Fire // Ice", out ScryfallCardData? full));
        Assert.Same(split, full);
    }
}
