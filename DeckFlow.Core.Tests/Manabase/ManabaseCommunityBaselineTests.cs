using System.Text.Json;
using DeckFlow.Core.Manabase;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Verifies the bundled manabase community-baseline JSON deserializes into the snapshot DTOs
/// (camelCase Web defaults + explicit property names), covering the B2-B5 land rows.
/// </summary>
public sealed class ManabaseCommunityBaselineTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Snapshot_deserializes_bracket_rows()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "generatedUtc": "2026-07-17T00:00:00Z",
              "source": "edhrec-pilot-aggregate",
              "brackets": [
                { "bracket": 2, "avgLands": 35.9, "deckCount": 124221 },
                { "bracket": 3, "avgLands": 35.5, "deckCount": 140632 },
                { "bracket": 4, "avgLands": 34.5, "deckCount": 72399 },
                { "bracket": 5, "avgLands": 30.5, "deckCount": 4761, "note": "genuine-cEDH mean" }
              ]
            }
            """;

        var snapshot = JsonSerializer.Deserialize<ManabaseBaselineSnapshot>(json, WebOptions);

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot!.SchemaVersion);
        Assert.Equal("edhrec-pilot-aggregate", snapshot.Source);
        Assert.Equal(4, snapshot.Brackets.Count);

        var b3 = snapshot.Brackets.Single(b => b.Bracket == 3);
        Assert.Equal(35.5, b3.AvgLands, 3);
        Assert.Equal(140632, b3.DeckCount);
        Assert.Null(b3.Note);

        var b5 = snapshot.Brackets.Single(b => b.Bracket == 5);
        Assert.Equal("genuine-cEDH mean", b5.Note);
    }
}
