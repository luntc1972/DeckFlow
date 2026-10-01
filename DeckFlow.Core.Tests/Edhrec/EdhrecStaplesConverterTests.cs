using DeckFlow.Core.Edhrec;

namespace DeckFlow.Core.Tests;

public sealed class EdhrecStaplesConverterTests
{
    [Fact]
    public void Convert_FiltersRowsAndOrdersDeterministically()
    {
        const string averages = """
commander,commander2,number_decks
Zulu,,100
Alpha,,100
Below,,99
Partner,Friend,500
""";
        const string data = """
commander,card,count
Zulu,Zeta,50
Zulu,Almost,49
Alpha,Bravo,50
Below,WouldQualify,99
Partner,WouldQualify,500
Missing,Ignored,100
""";

        CommanderStaplesSnapshot result = EdhrecStaplesConverter.Convert(new StringReader(data), new StringReader(averages), 100, 0.5);

        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal("edhrec-data", result.Source);
        Assert.Equal(100, result.MinDecks);
        Assert.Equal(0.5, result.MinInclusion);
        Assert.Collection(
            result.Commanders,
            commander =>
            {
                Assert.Equal("Alpha", commander.Name);
                Assert.Equal(100, commander.DeckCount);
                Assert.Equal(new[] { "Bravo" }, commander.Staples);
            },
            commander =>
            {
                Assert.Equal("Zulu", commander.Name);
                Assert.Equal(100, commander.DeckCount);
                Assert.Equal(new[] { "Zeta" }, commander.Staples);
            });
    }

    [Fact]
    public void Convert_ParsesQuotedCommanderNameWithComma()
    {
        const string averages = """
commander,commander2,number_decks
"Vaan, Street Thief",,100
""";
        const string data = """
commander,card,count
"Vaan, Street Thief","Card, With Comma",50
""";

        CommanderStaplesSnapshot result = EdhrecStaplesConverter.Convert(new StringReader(data), new StringReader(averages), 100, 0.5);

        CommanderStaplesCommander commander = Assert.Single(result.Commanders);
        Assert.Equal("Vaan, Street Thief", commander.Name);
        Assert.Equal(new[] { "Card, With Comma" }, commander.Staples);
    }

    [Fact]
    public void Convert_KeepsHighestDeckCountForRepeatedSoloCommander()
    {
        const string averages = """
commander,commander2,number_decks
Tolabow,,100
Tolabow,,200
""";
        const string data = """
commander,card,count
Tolabow,Included,100
Tolabow,Excluded,99
""";

        CommanderStaplesSnapshot result = EdhrecStaplesConverter.Convert(new StringReader(data), new StringReader(averages), 100, 0.5);

        CommanderStaplesCommander commander = Assert.Single(result.Commanders);
        Assert.Equal(200, commander.DeckCount);
        Assert.Equal(new[] { "Included" }, commander.Staples);
    }

    [Fact]
    public void Convert_MatchesCommanderRowsByNormalizedName()
    {
        const string averages = """
commander,commander2,number_decks
"Vaan, Street Thief",,100
"Vaan Street Thief",,200
""";
        const string data = """
commander,card,count
"Vaan, Street Thief",Included,100
""";

        CommanderStaplesSnapshot result = EdhrecStaplesConverter.Convert(new StringReader(data), new StringReader(averages), 100, 0.5);

        CommanderStaplesCommander commander = Assert.Single(result.Commanders);
        Assert.Equal("Vaan Street Thief", commander.Name);
        Assert.Equal(200, commander.DeckCount);
        Assert.Equal(new[] { "Included" }, commander.Staples);
    }

    [Fact]
    public void ParseCsvLine_HandlesQuotedCommasAndDoubledQuotes()
    {
        List<string> fields = EdhrecCsvParser.ParseCsvLine("\"Vaan, Street Thief\",\"A \"\"quoted\"\" card\",50");

        Assert.Equal(new[] { "Vaan, Street Thief", "A \"quoted\" card", "50" }, fields);
    }
}
