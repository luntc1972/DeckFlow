using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests.Knowledge;

/// <summary>
/// Tests Commander harvest legality gates.
/// </summary>
public sealed class DeckLegalityTests
{
    [Fact]
    public void Evaluate_FormatNotCommander_ReturnsNotCommanderFormat()
    {
        var result = DeckLegality.Evaluate(LegalEntries(), Metadata(deckFormat: 1));
        Assert.False(result.IsLegal);
        Assert.Equal(DeckSkipReasons.NotCommanderFormat, result.SkipReason);
    }

    [Fact]
    public void Evaluate_NoCommanderBoardEntry_ReturnsNoCommander()
    {
        var entries = new List<DeckEntry> { new() { Name = "Cards", NormalizedName = "cards", Quantity = 100, Board = "mainboard" } };
        var result = DeckLegality.Evaluate(entries, Metadata());
        Assert.Equal(DeckSkipReasons.NoCommander, result.SkipReason);
    }

    [Fact]
    public void Evaluate_LegalDeck_ReturnsLegalWithNullReason()
    {
        var result = DeckLegality.Evaluate(LegalEntries(), Metadata());
        Assert.True(result.IsLegal);
        Assert.Null(result.SkipReason);
    }

    private static List<DeckEntry> LegalEntries() =>
    [
        new() { Name = "Commander", NormalizedName = "commander", Quantity = 1, Board = "commander" },
        new() { Name = "Cards", NormalizedName = "cards", Quantity = 99, Board = "mainboard" },
    ];

    private static ArchidektDeckMetadata Metadata(int? deckFormat = DeckLegality.CommanderDeckFormat)
        => new(null, deckFormat, null, null, null, DateTimeOffset.UtcNow);
}
