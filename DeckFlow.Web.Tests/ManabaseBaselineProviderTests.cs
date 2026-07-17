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
}
