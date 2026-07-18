using System.Text.Json;
using DeckFlow.Core.History;

namespace DeckFlow.Core.Tests;

public sealed class DeckHistorySerializerTests
{
    private static DeckHistoryFile SampleFile() => new()
    {
        DeckName = "Tivit Ad Nauseam",
        Source = new DeckHistorySource { Site = "moxfield", Url = "https://moxfield.com/decks/abc" },
        Versions =
        [
            new DeckSnapshot
            {
                Id = 1,
                Date = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                Notes = "Initial list.",
                Commander = ["Tivit, Seller of Secrets"],
                Cards = [new SnapshotCard { Name = "Sol Ring", Qty = 1 }, new SnapshotCard { Name = "Island", Qty = 8 }],
            },
        ],
    };

    [Fact]
    public void Serialize_ThenParse_RoundTripsAllFields()
    {
        var json = DeckHistorySerializer.Serialize(SampleFile());
        var result = DeckHistorySerializer.Parse(json);

        Assert.Null(result.Error);
        Assert.NotNull(result.File);
        Assert.Equal("Tivit Ad Nauseam", result.File!.DeckName);
        Assert.Equal("moxfield", result.File.Source?.Site);
        var snapshot = Assert.Single(result.File.Versions);
        Assert.Equal(1, snapshot.Id);
        Assert.Equal("Tivit, Seller of Secrets", Assert.Single(snapshot.Commander));
        Assert.Equal(2, snapshot.Cards.Count);
        Assert.Equal(8, snapshot.Cards[1].Qty);
    }

    [Fact]
    public void Serialize_UsesCamelCaseAndFormatHeader()
    {
        var json = DeckHistorySerializer.Serialize(SampleFile());

        Assert.Contains("\"format\": \"deckflow-history\"", json);
        Assert.Contains("\"formatVersion\": \"1.0\"", json);
        Assert.Contains("\"deckName\"", json);
        Assert.Contains("\"qty\"", json);
        Assert.DoesNotContain("\"Name\"", json);
    }

    [Fact]
    public void Parse_UnknownFields_ArePreservedOnReserialize()
    {
        var json = DeckHistorySerializer.Serialize(SampleFile())
            .Replace("\"deckName\"", "\"futureField\": \"keep-me\",\n  \"deckName\"");
        var parsed = DeckHistorySerializer.Parse(json);

        Assert.Null(parsed.Error);
        var rewritten = DeckHistorySerializer.Serialize(parsed.File!);
        Assert.Contains("futureField", rewritten);
        Assert.Contains("keep-me", rewritten);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsError()
    {
        var result = DeckHistorySerializer.Parse("{ not json");
        Assert.Null(result.File);
        Assert.Contains("not valid JSON", result.Error);
    }

    [Fact]
    public void Parse_WrongFormatMarker_ReturnsError()
    {
        var result = DeckHistorySerializer.Parse("{\"format\":\"something-else\",\"formatVersion\":\"1.0\"}");
        Assert.Null(result.File);
        Assert.Contains("not a DeckFlow history file", result.Error);
    }

    [Fact]
    public void Parse_NewerMajorVersion_ReturnsError()
    {
        var result = DeckHistorySerializer.Parse("{\"format\":\"deckflow-history\",\"formatVersion\":\"2.0\",\"deckName\":\"x\",\"versions\":[]}");
        Assert.Null(result.File);
        Assert.Contains("newer version of DeckFlow", result.Error);
    }

    [Fact]
    public void Parse_NewerMinorVersion_IsAccepted()
    {
        var result = DeckHistorySerializer.Parse("{\"format\":\"deckflow-history\",\"formatVersion\":\"1.7\",\"deckName\":\"x\",\"versions\":[]}");
        Assert.Null(result.Error);
        Assert.NotNull(result.File);
    }

    [Fact]
    public void Parse_BrokenIds_AreRepairedInDateOrderWithWarning()
    {
        var json = """
        {
          "format": "deckflow-history",
          "formatVersion": "1.0",
          "deckName": "x",
          "versions": [
            { "id": 9, "date": "2026-07-02T00:00:00Z", "commander": [], "cards": [] },
            { "id": 9, "date": "2026-07-01T00:00:00Z", "commander": [], "cards": [] }
          ]
        }
        """;
        var result = DeckHistorySerializer.Parse(json);

        Assert.Null(result.Error);
        Assert.Equal(1, result.File!.Versions[0].Id);
        Assert.Equal(2, result.File.Versions[1].Id);
        Assert.Equal(DateTimeOffset.Parse("2026-07-01T00:00:00Z"), result.File.Versions[0].Date);
        Assert.Contains(result.Warnings, w => w.Contains("repaired"));
    }

    [Fact]
    public void Parse_OversizedContent_ReturnsError()
    {
        var padding = new string('x', DeckHistorySerializer.MaxUploadBytes);
        var result = DeckHistorySerializer.Parse(
            $"{{\"format\":\"deckflow-history\",\"formatVersion\":\"1.0\",\"deckName\":\"{padding}\",\"versions\":[]}}");

        Assert.Null(result.File);
        Assert.Contains("too large", result.Error);
    }

    [Fact]
    public void Parse_NullCollections_NormalizeToEmpty()
    {
        var json = """
        {
          "format": "deckflow-history",
          "formatVersion": "1.0",
          "deckName": "x",
          "versions": [ { "id": 1, "date": "2026-07-01T00:00:00Z", "commander": null, "cards": null } ]
        }
        """;
        var result = DeckHistorySerializer.Parse(json);

        Assert.Null(result.Error);
        Assert.Empty(result.File!.Versions[0].Commander);
        Assert.Empty(result.File.Versions[0].Cards);
    }
}
