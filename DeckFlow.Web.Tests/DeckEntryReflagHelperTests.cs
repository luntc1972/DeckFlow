using DeckFlow.Core.Models;
using DeckFlow.Web.Services.Packets;
using Xunit;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Unit tests for <see cref="DeckEntryReflagHelper.ReflagCommanderEntry"/>, the shared first-match
/// commander reflag copied verbatim from <c>DeckComparisonService</c>/<c>MetaGapService</c>.
/// </summary>
public sealed class DeckEntryReflagHelperTests
{
    [Fact]
    public void ReflagCommanderEntry_SingleMatch_ReflagsToCommander()
    {
        var entries = new List<DeckEntry>
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", 1, "mainboard"),
            CreateDeckEntry("Sol Ring", 1, "mainboard"),
        };

        var result = DeckEntryReflagHelper.ReflagCommanderEntry(entries, "Atraxa, Praetors' Voice");

        Assert.Equal("commander", result[0].Board);
        Assert.Equal("mainboard", result[1].Board);
    }

    [Fact]
    public void ReflagCommanderEntry_TwoNameMatches_OnlyFirstIsReflagged()
    {
        var entries = new List<DeckEntry>
        {
            CreateDeckEntry("Sol Ring", 1, "mainboard"),
            CreateDeckEntry("Atraxa, Praetors' Voice", 1, "mainboard"),
            CreateDeckEntry("Atraxa, Praetors' Voice", 1, "maybeboard"),
        };

        var result = DeckEntryReflagHelper.ReflagCommanderEntry(entries, "Atraxa, Praetors' Voice");

        Assert.Equal("mainboard", result[0].Board);
        Assert.Equal("commander", result[1].Board);
        Assert.Equal("maybeboard", result[2].Board);
    }

    [Fact]
    public void ReflagCommanderEntry_NoMatch_LeavesListUnchanged()
    {
        var entries = new List<DeckEntry>
        {
            CreateDeckEntry("Sol Ring", 1, "mainboard"),
            CreateDeckEntry("Arcane Signet", 1, "mainboard"),
        };

        var result = DeckEntryReflagHelper.ReflagCommanderEntry(entries, "Atraxa, Praetors' Voice");

        Assert.Equal("mainboard", result[0].Board);
        Assert.Equal("mainboard", result[1].Board);
        Assert.Equal(entries[0], result[0]);
        Assert.Equal(entries[1], result[1]);
    }

    [Fact]
    public void ReflagCommanderEntry_QuantityGreaterThanOne_NotReflagged()
    {
        var entries = new List<DeckEntry>
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", 2, "mainboard"),
        };

        var result = DeckEntryReflagHelper.ReflagCommanderEntry(entries, "Atraxa, Praetors' Voice");

        Assert.Equal("mainboard", result[0].Board);
    }

    [Fact]
    public void ReflagCommanderEntry_AlreadyOnCommanderBoard_NotDoubleReflaggedOrDuplicated()
    {
        var entries = new List<DeckEntry>
        {
            CreateDeckEntry("Atraxa, Praetors' Voice", 1, "commander"),
        };

        var result = DeckEntryReflagHelper.ReflagCommanderEntry(entries, "Atraxa, Praetors' Voice");

        Assert.Single(result);
        Assert.Equal("commander", result[0].Board);
    }

    private static DeckEntry CreateDeckEntry(string name, int quantity, string board)
        => new()
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = quantity,
            Board = board,
        };
}
