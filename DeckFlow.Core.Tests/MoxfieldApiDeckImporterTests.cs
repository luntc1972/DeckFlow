using DeckFlow.Core.Integration;
using RestSharp;
using Xunit;

namespace DeckFlow.Core.Tests;

public sealed class MoxfieldApiDeckImporterTests
{
    [Fact]
    public async Task ImportAsync_PreservesSideboardEntriesAsSideboard()
    {
        var importer = new MoxfieldApiDeckImporter(
            executeAsync: (_, _) => Task.FromResult(new RestResponse
        {
            StatusCode = System.Net.HttpStatusCode.OK,
            ResponseStatus = ResponseStatus.Completed,
            IsSuccessStatusCode = true,
            StatusDescription = "OK",
            Content = """
{
  "commanders": {
    "Atraxa, Praetors' Voice": {
      "quantity": 1,
      "card": { "name": "Atraxa, Praetors' Voice", "set": "c16", "cn": "28" }
    }
  },
  "mainboard": {
    "Sol Ring": {
      "quantity": 1,
      "card": { "name": "Sol Ring", "set": "c16", "cn": "272" }
    }
  },
  "sideboard": {
    "Swords to Plowshares": {
      "quantity": 1,
      "card": { "name": "Swords to Plowshares", "set": "c16", "cn": "74" }
    }
  },
  "maybeboard": {
    "Smothering Tithe": {
      "quantity": 1,
      "card": { "name": "Smothering Tithe", "set": "rna", "cn": "22" }
    }
  }
}
"""
        }));

        var entries = await importer.ImportAsync("https://www.moxfield.com/decks/test-deck-id");

        Assert.Contains(entries, entry => entry.Name == "Atraxa, Praetors' Voice" && entry.Board == "commander");
        Assert.Contains(entries, entry => entry.Name == "Sol Ring" && entry.Board == "mainboard");
        Assert.Contains(entries, entry => entry.Name == "Swords to Plowshares" && entry.Board == "sideboard");
        Assert.Contains(entries, entry => entry.Name == "Smothering Tithe" && entry.Board == "maybeboard");
    }
}
