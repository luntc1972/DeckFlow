using System.IO;
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Services.Manabase;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Verifies the manabase community-baseline provider loads the bundled JSON, resolves per-bracket
/// rows, and fails open (missing/malformed file → null, never throws).
/// </summary>
public sealed class ManabaseBaselineProviderTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public ManabaseBaselineProviderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"manabase-baseline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "latest.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private ManabaseBaselineProvider CreateProvider()
        => new(_path, new MemoryCache(new MemoryCacheOptions()));

    private void WriteFile(string json) => File.WriteAllText(_path, json);

    private const string SampleJson = """
        {
          "schemaVersion": 1,
          "source": "edhrec-pilot-aggregate",
          "brackets": [
            { "bracket": 2, "avgLands": 35.9, "deckCount": 124221 },
            { "bracket": 3, "avgLands": 35.5, "deckCount": 140632 },
            { "bracket": 5, "avgLands": 30.5, "deckCount": 4761 }
          ],
          "commandersSource": "edhrec-averages",
          "commanders": [
            { "name": "The Ur-Dragon", "avgLands": 35, "deckCount": 48802 },
            { "name": "Halana, Kessig Ranger", "partnerName": "Alena, Kessig Trapper", "avgLands": 36, "deckCount": 1234 },
            { "name": "Y'shtola, Night's Blessed", "avgLands": 34, "deckCount": 456 },
            { "name": "Niv-Mizzet, Parun", "avgLands": 35, "deckCount": 150 },
            { "name": "Niv Mizzet Parun", "avgLands": 36, "deckCount": 200 }
          ]
        }
        """;

    private const string Increment1Json = """
        {
          "schemaVersion": 1,
          "source": "edhrec-pilot-aggregate",
          "brackets": [
            { "bracket": 2, "avgLands": 35.9, "deckCount": 124221 },
            { "bracket": 3, "avgLands": 35.5, "deckCount": 140632 },
            { "bracket": 5, "avgLands": 30.5, "deckCount": 4761 }
          ]
        }
        """;

    [Fact]
    public void Known_bracket_returns_row()
    {
        WriteFile(SampleJson);
        var row = CreateProvider().TryGetBracketBaseline(3);
        Assert.NotNull(row);
        Assert.Equal(35.5, row!.AvgLands, 3);
        Assert.Equal(140632, row.DeckCount);
    }

    [Fact]
    public void Row_backfills_snapshot_source()
    {
        WriteFile(SampleJson); // rows omit their own "source"; snapshot source is edhrec-pilot-aggregate
        var row = CreateProvider().TryGetBracketBaseline(2);
        Assert.NotNull(row);
        Assert.Equal("edhrec-pilot-aggregate", row!.Source);
    }

    [Fact]
    public void Unknown_bracket_returns_null()
    {
        WriteFile(SampleJson);
        Assert.Null(CreateProvider().TryGetBracketBaseline(4)); // not in this file
    }

    [Fact]
    public void Missing_file_returns_null_no_throw()
    {
        Assert.Null(CreateProvider().TryGetBracketBaseline(3)); // file never written
    }

    [Fact]
    public void Malformed_file_returns_null_no_throw()
    {
        WriteFile("{ this is not valid json ");
        Assert.Null(CreateProvider().TryGetBracketBaseline(3));
    }

    [Fact]
    public void Known_commander_returns_row()
    {
        WriteFile(SampleJson);

        ManabaseCommanderBaseline? row = CreateProvider().TryGetCommanderBaseline(["The Ur-Dragon"]);

        Assert.NotNull(row);
        Assert.Equal(35, row!.AvgLands);
        Assert.Equal(48802, row.DeckCount);
    }

    [Fact]
    public void Known_partner_pair_returns_row_in_both_orders()
    {
        WriteFile(SampleJson);
        ManabaseBaselineProvider provider = CreateProvider();

        ManabaseCommanderBaseline? forward = provider.TryGetCommanderBaseline(["Halana, Kessig Ranger", "Alena, Kessig Trapper"]);
        ManabaseCommanderBaseline? reverse = provider.TryGetCommanderBaseline(["Alena, Kessig Trapper", "Halana, Kessig Ranger"]);

        Assert.NotNull(forward);
        Assert.Equal(forward, reverse);
        Assert.Equal("Alena, Kessig Trapper", forward!.PartnerName);
    }

    [Fact]
    public void Lone_commander_does_not_match_pair_row()
    {
        WriteFile(SampleJson);

        Assert.Null(CreateProvider().TryGetCommanderBaseline(["Halana, Kessig Ranger"]));
    }

    [Fact]
    public void Unknown_commander_returns_null()
    {
        WriteFile(SampleJson);

        Assert.Null(CreateProvider().TryGetCommanderBaseline(["Atraxa, Praetors' Voice"]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Unsupported_commander_count_returns_null(int count)
    {
        WriteFile(SampleJson);
        string[] names = Enumerable.Range(1, count).Select(i => $"Commander {i}").ToArray();

        Assert.Null(CreateProvider().TryGetCommanderBaseline(names));
    }

    [Fact]
    public void Commander_lookup_is_case_and_punctuation_insensitive()
    {
        WriteFile(SampleJson);

        ManabaseCommanderBaseline? row = CreateProvider().TryGetCommanderBaseline(["y’shtola night’s blessed"]);

        Assert.NotNull(row);
        Assert.Equal("Y'shtola, Night's Blessed", row!.Name);
    }

    [Fact]
    public void Duplicate_normalized_keys_keep_higher_deck_count_row()
    {
        WriteFile(SampleJson);

        ManabaseCommanderBaseline? row = CreateProvider().TryGetCommanderBaseline(["Niv-Mizzet, Parun"]);

        Assert.NotNull(row);
        Assert.Equal(36, row!.AvgLands);
        Assert.Equal(200, row.DeckCount);
    }

    [Fact]
    public void Increment1_snapshot_shape_returns_null_for_commander_lookup_but_brackets_still_work()
    {
        WriteFile(Increment1Json);
        ManabaseBaselineProvider provider = CreateProvider();

        Assert.Null(provider.TryGetCommanderBaseline(["The Ur-Dragon"]));

        ManabaseBracketBaseline? bracket = provider.TryGetBracketBaseline(3);
        Assert.NotNull(bracket);
        Assert.Equal(35.5, bracket!.AvgLands, 3);
    }

    [Fact]
    public void Malformed_file_returns_null_for_commander_lookup_no_throw()
    {
        WriteFile("{ this is not valid json ");

        Assert.Null(CreateProvider().TryGetCommanderBaseline(["The Ur-Dragon"]));
    }
}
