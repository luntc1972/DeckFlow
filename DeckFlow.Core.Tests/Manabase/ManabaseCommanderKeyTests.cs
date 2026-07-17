namespace DeckFlow.Core.Tests;

using DeckFlow.Core.Manabase;

public sealed class ManabaseCommanderKeyTests
{
    // CardNormalizer keeps \p{L} letters, so accents survive. We do not accent-fold because the
    // dump and Scryfall use the same accented spellings; only punctuation variants must collapse.
    [Fact]
    public void Create_LoneCommander_NormalizesCaseAndPunctuation()
        => Assert.Equal("y shtola night s blessed", ManabaseCommanderKey.Create("Y'shtola, Night's Blessed"));

    [Fact]
    public void Create_UnicodeApostropheAndAccents_MatchesAsciiForm()
        => Assert.Equal(ManabaseCommanderKey.Create("Y’shtola, Night’s Blessed"), ManabaseCommanderKey.Create("Y'shtola, Night's Blessed"));

    [Fact]
    public void Create_Pair_IsOrderInsensitive()
        => Assert.Equal(
            ManabaseCommanderKey.Create("Halana, Kessig Ranger", "Alena, Kessig Trapper"),
            ManabaseCommanderKey.Create("Alena, Kessig Trapper", "Halana, Kessig Ranger"));

    [Fact]
    public void Create_Pair_UsesDelimiterThatCannotCollideWithLoneNames()
        => Assert.Equal("alena kessig trapper||halana kessig ranger", ManabaseCommanderKey.Create("Halana, Kessig Ranger", "Alena, Kessig Trapper"));

    [Fact]
    public void Create_MdfcName_CollapsesToFrontFace()
        => Assert.Equal("birgi god of storytelling", ManabaseCommanderKey.Create("Birgi, God of Storytelling // Harnfel, Horn of Bounty"));

    [Fact]
    public void Create_PairOfMdfcNames_NormalizesEachBeforeJoining()
        => Assert.Equal(
            ManabaseCommanderKey.Create("Birgi, God of Storytelling // Harnfel, Horn of Bounty", "Esika, God of the Tree // The Prismatic Bridge"),
            ManabaseCommanderKey.Create("Esika, God of the Tree", "Birgi, God of Storytelling"));

    [Fact]
    public void Create_BlankPartner_TreatedAsLone()
        => Assert.Equal(ManabaseCommanderKey.Create("The Ur-Dragon"), ManabaseCommanderKey.Create("The Ur-Dragon", "  "));
}
