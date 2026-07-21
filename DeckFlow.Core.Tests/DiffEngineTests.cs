using DeckFlow.Core.Diffing;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for <see cref="DiffEngine"/> covering loose-mode, strict-mode, printing-conflict detection,
/// commander-fallback matching, and quantity-delta consolidation.
/// </summary>
public sealed class DiffEngineTests
{
    [Fact]
    public void Compare_LooseMode_FindsPrintingConflictAndSkipsDelta()
    {
        var moxfield = new MoxfieldParser().ParseText("1 Giggling Skitterspike (DSC) 66");
        var archidekt = new ArchidektParser().ParseText("1 Giggling Skitterspike (dsc) 39 [Burn]");

        var diff = new DiffEngine(MatchMode.Loose).Compare(moxfield, archidekt);

        Assert.Empty(diff.ToAdd);
        Assert.Single(diff.PrintingConflicts);
        Assert.Equal("Burn", diff.PrintingConflicts[0].ArchidektVersion.Category);
    }

    [Fact]
    public void Compare_DoesNotAddCommanderWhenTargetParsedItAsMainboard()
    {
        var moxfield = new MoxfieldParser().ParseText("""
            Commander:
            1 Bello, Bard of the Brambles
            """);
        var archidekt = new ArchidektParser().ParseText("1 Bello Bard of the Brambles (blb) 1");

        var diff = new DiffEngine(MatchMode.Loose).Compare(moxfield, archidekt);

        Assert.Empty(diff.ToAdd);
        Assert.Empty(diff.OnlyInArchidekt);
    }

    [Fact]
    public void Compare_StrictMode_UsesLooseFallbackForPrintingConflict()
    {
        var moxfield = new MoxfieldParser().ParseText("1 Birds of Paradise (7ED) 231 *F*");
        var archidekt = new ArchidektParser().ParseText("1 Birds of Paradise (cn2) 176 [Ramp]");

        var diff = new DiffEngine(MatchMode.Strict).Compare(moxfield, archidekt);

        Assert.Empty(diff.ToAdd);
        Assert.Single(diff.PrintingConflicts);
    }

    [Fact]
    public void Compare_SumsCategorySplitsBeforeComparing()
    {
        var moxfield = new MoxfieldParser().ParseText("3 Snow-Covered Mountain");
        var archidekt = new ArchidektParser().ParseText("""
            1 Snow-Covered Mountain (khm) 283 [Ramp]
            1 Snow-Covered Mountain (khm) 283 [Lands]
            """);

        var diff = new DiffEngine(MatchMode.Loose).Compare(moxfield, archidekt);

        var toAdd = Assert.Single(diff.ToAdd);
        Assert.Equal(1, toAdd.Quantity);
    }

    [Fact]
    public void Compare_CutOnlyScenario_PutsRemovedCardsInOnlyInArchidekt()
    {
        var finalEntries = new List<DeckEntry>
        {
            new() { Name = "Kinnan, Bonder Prodigy", NormalizedName = "kinnan bonder prodigy", Quantity = 1, Board = "commander" },
            new() { Name = "Forest", NormalizedName = "forest", Quantity = 99, Board = "mainboard" },
        };
        var originalEntries = new List<DeckEntry>
        {
            new() { Name = "Kinnan, Bonder Prodigy", NormalizedName = "kinnan bonder prodigy", Quantity = 1, Board = "commander" },
            new() { Name = "Forest", NormalizedName = "forest", Quantity = 99, Board = "mainboard" },
            new() { Name = "Llanowar Elves", NormalizedName = "llanowar elves", Quantity = 1, Board = "sideboard" },
        };

        var diff = new DiffEngine(MatchMode.Loose).Compare(finalEntries, originalEntries);

        Assert.Empty(diff.ToAdd);
        var cut = Assert.Single(diff.OnlyInArchidekt);
        Assert.Equal("Llanowar Elves", cut.Name);
        Assert.Equal(1, cut.Quantity);
    }

    [Fact]
    public void Compare_QuantityDecrease_PutsRemovedCountInCountMismatch()
    {
        var finalEntries = new List<DeckEntry>
        {
            new() { Name = "Forest", NormalizedName = "forest", Quantity = 7, Board = "mainboard" },
        };
        var originalEntries = new List<DeckEntry>
        {
            new() { Name = "Forest", NormalizedName = "forest", Quantity = 10, Board = "mainboard" },
        };

        var diff = new DiffEngine(MatchMode.Loose).Compare(finalEntries, originalEntries);

        Assert.Empty(diff.ToAdd);
        Assert.Empty(diff.OnlyInArchidekt);
        var mismatch = Assert.Single(diff.CountMismatch);
        Assert.Equal("Forest", mismatch.Name);
        Assert.Equal(3, mismatch.Quantity);
    }
}
