using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Loading;
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

    [Fact]
    public void Evaluate_NullMetadata_ReturnsNotCommanderFormat()
    {
        Assert.Equal(DeckSkipReasons.NotCommanderFormat, DeckLegality.Evaluate(LegalEntries(), null).SkipReason);
    }

    [Fact]
    public void Evaluate_NullDeckFormat_ReturnsNotCommanderFormat()
    {
        Assert.Equal(DeckSkipReasons.NotCommanderFormat, DeckLegality.Evaluate(LegalEntries(), Metadata(deckFormat: null)).SkipReason);
    }

    [Fact]
    public void Evaluate_NinetyNineCards_ReturnsWrongSize()
    {
        var entries = LegalEntries(mainboardQuantity: 98);
        Assert.Equal(DeckSkipReasons.WrongSize, DeckLegality.Evaluate(entries, Metadata()).SkipReason);
    }

    [Fact]
    public void Evaluate_OneHundredOneCards_ReturnsWrongSize()
    {
        var entries = LegalEntries(mainboardQuantity: 100);
        Assert.Equal(DeckSkipReasons.WrongSize, DeckLegality.Evaluate(entries, Metadata()).SkipReason);
    }

    [Fact]
    public void Evaluate_FormatAndSizeBothWrong_ReportsFormatFirst()
    {
        var entries = LegalEntries(mainboardQuantity: 98);
        Assert.Equal(DeckSkipReasons.NotCommanderFormat, DeckLegality.Evaluate(entries, Metadata(deckFormat: 1)).SkipReason);
    }

    [Fact]
    public void Evaluate_PartnerPair_IsLegal()
    {
        var entries = LegalEntries(commanderQuantity: 2, mainboardQuantity: 98);
        Assert.True(DeckLegality.Evaluate(entries, Metadata()).IsLegal);
    }

    [Fact]
    public void Evaluate_ZeroAndPositiveCommanders_IsLegal()
    {
        var entries = new List<DeckEntry>
        {
            new() { Name = "Zero", NormalizedName = "zero", Quantity = 0, Board = "commander" },
            new() { Name = "Positive", NormalizedName = "positive", Quantity = 1, Board = "commander" },
            new() { Name = "Cards", NormalizedName = "cards", Quantity = 99, Board = "mainboard" },
        };
        Assert.True(DeckLegality.Evaluate(entries, Metadata()).IsLegal);
    }

    [Fact]
    public void Evaluate_SideboardAndMaybeboardCards_AreNotCounted()
    {
        var entries = LegalEntries();
        entries.Add(new DeckEntry { Name = "Sideboard", NormalizedName = "sideboard", Quantity = 10, Board = "sideboard" });
        entries.Add(new DeckEntry { Name = "Maybeboard", NormalizedName = "maybeboard", Quantity = 5, Board = "maybeboard" });
        Assert.True(DeckLegality.Evaluate(entries, Metadata()).IsLegal);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(100)]
    public void CountCommanderDeckCards_MatchesValidateCommanderDeckSize(int totalCards)
    {
        var entries = LegalEntries(mainboardQuantity: totalCards - 1);
        var loader = new DeckEntryLoader(null!, null!, null!, null!);
        Assert.Equal(totalCards, DeckEntryLoader.CountCommanderDeckCards(entries));
        if (totalCards == 100)
        {
            loader.ValidateCommanderDeckSize("Test", entries);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => loader.ValidateCommanderDeckSize("Test", entries));
        }
    }

    private static List<DeckEntry> LegalEntries(int commanderQuantity = 1, int mainboardQuantity = 99) =>
    [
        new() { Name = "Commander", NormalizedName = "commander", Quantity = commanderQuantity, Board = "commander" },
        new() { Name = "Cards", NormalizedName = "cards", Quantity = mainboardQuantity, Board = "mainboard" },
    ];

    private static ArchidektDeckMetadata Metadata(int? deckFormat = DeckLegality.CommanderDeckFormat)
        => new(null, deckFormat, null, null, null, DateTimeOffset.UtcNow);
}
