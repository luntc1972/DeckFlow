using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

public sealed class EdhrecAveragesConverterTests
{
    [Fact]
    public void Convert_ParsesRowsFiltersByDeckCountAndOrdersDeterministically()
    {
        const string csv = """
commander,commander2,avg_land,number_decks
The Ur-Dragon,,35,48802
"Atraxa, Praetors' Voice",,36,150
"Tinybones, Trinket Thief",,34,99
"Azami, Lady of Scrolls",,33,150
""";

        EdhrecAveragesResult result = EdhrecAveragesConverter.Convert(csv);

        Assert.Equal(0, result.SkippedMalformed);
        Assert.Equal(0, result.DuplicateCollisions);
        Assert.Collection(
            result.Commanders,
            commander =>
            {
                Assert.Equal("The Ur-Dragon", commander.Name);
                Assert.Null(commander.PartnerName);
                Assert.Equal(35d, commander.AvgLands);
                Assert.Equal(48802, commander.DeckCount);
            },
            commander =>
            {
                Assert.Equal("Atraxa, Praetors' Voice", commander.Name);
                Assert.Equal(36d, commander.AvgLands);
                Assert.Equal(150, commander.DeckCount);
            },
            commander =>
            {
                Assert.Equal("Azami, Lady of Scrolls", commander.Name);
                Assert.Equal(33d, commander.AvgLands);
                Assert.Equal(150, commander.DeckCount);
            });
    }

    [Fact]
    public void Convert_ParsesQuotedCommanderNameWithCommaAndApostrophe()
    {
        const string csv = """
commander,commander2,avg_land,number_decks
"Y'shtola, Night's Blessed",,35,250
""";

        EdhrecAveragesResult result = EdhrecAveragesConverter.Convert(csv);

        ManabaseCommanderBaseline commander = Assert.Single(result.Commanders);
        Assert.Equal("Y'shtola, Night's Blessed", commander.Name);
        Assert.Equal(35d, commander.AvgLands);
        Assert.Equal(250, commander.DeckCount);
    }

    [Fact]
    public void Convert_PartnerPairRow_PopulatesPartnerName()
    {
        const string csv = """
commander,commander2,avg_land,number_decks
"Halana, Kessig Ranger","Alena, Kessig Trapper",36,1234
""";

        EdhrecAveragesResult result = EdhrecAveragesConverter.Convert(csv);

        ManabaseCommanderBaseline commander = Assert.Single(result.Commanders);
        Assert.Equal("Halana, Kessig Ranger", commander.Name);
        Assert.Equal("Alena, Kessig Trapper", commander.PartnerName);
        Assert.Equal(36d, commander.AvgLands);
        Assert.Equal(1234, commander.DeckCount);
    }

    [Fact]
    public void Convert_MalformedRows_AreSkippedAndCounted()
    {
        const string csv = """
commander,commander2,avg_land,number_decks
Valid Commander,,35,250
Bad Average,,abc,250
 ,Partner,35,250
Too,Few,Columns
""";

        EdhrecAveragesResult result = EdhrecAveragesConverter.Convert(csv);

        ManabaseCommanderBaseline commander = Assert.Single(result.Commanders);
        Assert.Equal("Valid Commander", commander.Name);
        Assert.Equal(3, result.SkippedMalformed);
        Assert.Equal(0, result.DuplicateCollisions);
    }

    [Fact]
    public void Convert_DuplicateNormalizedKeys_KeepHigherDeckCountAndCountCollision()
    {
        const string csv = """
commander,commander2,avg_land,number_decks
"Y'shtola, Night's Blessed",,35,250
"Y’shtola, Night’s Blessed",,34,300
""";

        EdhrecAveragesResult result = EdhrecAveragesConverter.Convert(csv);

        ManabaseCommanderBaseline commander = Assert.Single(result.Commanders);
        Assert.Equal("Y’shtola, Night’s Blessed", commander.Name);
        Assert.Equal(34d, commander.AvgLands);
        Assert.Equal(300, commander.DeckCount);
        Assert.Equal(1, result.DuplicateCollisions);
    }

    [Fact]
    public void Convert_UsesHeaderColumnNamesRatherThanFixedIndexes()
    {
        const string csv = """
number_decks,avg_land,commander2,commander
250,35,,"The Ur-Dragon"
125,36,"Alena, Kessig Trapper","Halana, Kessig Ranger"
""";

        EdhrecAveragesResult result = EdhrecAveragesConverter.Convert(csv);

        Assert.Collection(
            result.Commanders,
            commander =>
            {
                Assert.Equal("The Ur-Dragon", commander.Name);
                Assert.Null(commander.PartnerName);
                Assert.Equal(250, commander.DeckCount);
            },
            commander =>
            {
                Assert.Equal("Halana, Kessig Ranger", commander.Name);
                Assert.Equal("Alena, Kessig Trapper", commander.PartnerName);
                Assert.Equal(125, commander.DeckCount);
            });
    }

    [Fact]
    public void Convert_MissingRequiredHeader_ThrowsFormatException()
    {
        const string csv = """
commander,commander2,avg_land
The Ur-Dragon,,35
""";

        FormatException exception = Assert.Throws<FormatException>(() => EdhrecAveragesConverter.Convert(csv));
        Assert.Contains("number_decks", exception.Message, StringComparison.Ordinal);
    }
}
