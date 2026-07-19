using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests;

/// <summary>
/// Unit tests for the pure deck tendencies report builder.
/// </summary>
public sealed class DeckTendenciesReportBuilderTests
{
    /// <summary>
    /// Verifies repeated non-commander cards are ranked by deck count then ordinal card name.
    /// </summary>
    [Fact]
    public void Build_OrdersRepeatCardsByDeckCountThenCardName()
    {
        var samples = new[]
        {
            Sample("deck-1", Entry("Arcane Signet"), Entry("Counterspell"), Entry("Negate")),
            Sample("deck-2", Entry("Arcane Signet"), Entry("Counterspell")),
            Sample("deck-3", Entry("Counterspell"), Entry("Negate")),
        };

        var report = DeckTendenciesReportBuilder.Build(samples, EmptyCategories);

        Assert.Collection(
            report.RepeatCards,
            row =>
            {
                Assert.Equal("Counterspell", row.CardName);
                Assert.Equal(3, row.DeckCount);
                Assert.Equal(1.0, row.Frequency);
            },
            row =>
            {
                Assert.Equal("Arcane Signet", row.CardName);
                Assert.Equal(2, row.DeckCount);
                Assert.Equal(2d / 3d, row.Frequency, 6);
            },
            row =>
            {
                Assert.Equal("Negate", row.CardName);
                Assert.Equal(2, row.DeckCount);
                Assert.Equal(2d / 3d, row.Frequency, 6);
            });
    }

    /// <summary>
    /// Verifies repeat-card presence is boolean per deck regardless of card quantity.
    /// </summary>
    [Fact]
    public void Build_IgnoresQuantityForRepeatCardPresence()
    {
        var samples = new[]
        {
            Sample("deck-1", Entry("Cultivate", quantity: 3)),
            Sample("deck-2", Entry("Cultivate")),
        };

        var report = DeckTendenciesReportBuilder.Build(samples, EmptyCategories);

        var row = Assert.Single(report.RepeatCards);
        Assert.Equal("Cultivate", row.CardName);
        Assert.Equal(2, row.DeckCount);
        Assert.Equal(1.0, row.Frequency);
    }

    /// <summary>
    /// Verifies basic lands, including snow-covered variants, are excluded from repeat cards.
    /// </summary>
    [Fact]
    public void Build_ExcludesBasicLandsIncludingSnowCoveredVariantsFromRepeatCards()
    {
        var samples = new[]
        {
            Sample("deck-1", Entry("Forest"), Entry("Snow-Covered Island"), Entry("Arcane Signet")),
            Sample("deck-2", Entry("forest"), Entry("snow-covered island"), Entry("Arcane Signet")),
        };

        var report = DeckTendenciesReportBuilder.Build(samples, EmptyCategories);

        var row = Assert.Single(report.RepeatCards);
        Assert.Equal("Arcane Signet", row.CardName);
    }

    /// <summary>
    /// Verifies commander-board cards are excluded from repeat cards and counted separately.
    /// </summary>
    [Fact]
    public void Build_SplitsCommanderCardsOutOfRepeatCardsAndIntoRepeatCommanders()
    {
        var samples = new[]
        {
            Sample("deck-1", CommanderEntry("Atraxa, Praetors' Voice"), Entry("Sol Ring")),
            Sample("deck-2", CommanderEntry("Atraxa, Praetors' Voice"), Entry("Sol Ring")),
        };

        var report = DeckTendenciesReportBuilder.Build(samples, EmptyCategories);

        var repeatCard = Assert.Single(report.RepeatCards);
        Assert.Equal("Sol Ring", repeatCard.CardName);

        var repeatCommander = Assert.Single(report.RepeatCommanders);
        Assert.Equal("Atraxa, Praetors' Voice", repeatCommander.CardName);
        Assert.Equal(2, repeatCommander.DeckCount);
        Assert.Equal(1.0, repeatCommander.Frequency);
    }

    /// <summary>
    /// Verifies the staple flag comes from the creator personal-staple calculation.
    /// </summary>
    [Fact]
    public void Build_FlagsPersonalStaplesForCardsPresentInMoreThanSixtyPercentOfDecks()
    {
        var samples = new[]
        {
            Sample("deck-1", Entry("Mystic Remora")),
            Sample("deck-2", Entry("Mystic Remora")),
            Sample("deck-3", Entry("Mystic Remora")),
            Sample("deck-4", Entry("Mystic Remora")),
            Sample("deck-5", Entry("Arcane Signet")),
        };

        var report = DeckTendenciesReportBuilder.Build(samples, EmptyCategories);

        var row = Assert.Single(report.RepeatCards);
        Assert.Equal("Mystic Remora", row.CardName);
        Assert.True(row.IsPersonalStaple);
    }

    /// <summary>
    /// Verifies category averages are quantity-weighted and zero-filled across all decks.
    /// </summary>
    [Fact]
    public void Build_ComputesCategoryAveragesAndPresenceAcrossAllDecks()
    {
        var samples = new[]
        {
            Sample("deck-1", Entry("Cultivate", quantity: 2), Entry("Growth Spiral")),
            Sample("deck-2", Entry("Ponder")),
            Sample("deck-3", Entry("Arcane Signet")),
        };
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cultivate"] = ["Ramp"],
            ["Growth Spiral"] = ["Ramp", "Draw"],
            ["Ponder"] = ["Draw"],
        };

        var report = DeckTendenciesReportBuilder.Build(samples, cardCategories);

        Assert.Collection(
            report.CategoryTendencies,
            row =>
            {
                Assert.Equal("Ramp", row.Category);
                Assert.Equal(1.0, row.AverageCountPerDeck);
                Assert.Equal(1d / 3d, row.PresenceRatio, 6);
                Assert.Null(row.BaselinePresenceRatio);
                Assert.Null(row.Lift);
            },
            row =>
            {
                Assert.Equal("Draw", row.Category);
                Assert.Equal(2d / 3d, row.AverageCountPerDeck, 6);
                Assert.Equal(2d / 3d, row.PresenceRatio, 6);
                Assert.Null(row.BaselinePresenceRatio);
                Assert.Null(row.Lift);
            });
    }

    /// <summary>
    /// Verifies baseline values remain null when no baseline is supplied.
    /// </summary>
    [Fact]
    public void Build_LeavesBaselineValuesNullWhenBaselineIsMissing()
    {
        var samples = new[]
        {
            Sample("deck-1", Entry("Cultivate")),
            Sample("deck-2", Entry("Cultivate")),
        };
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cultivate"] = ["Ramp"],
        };

        var report = DeckTendenciesReportBuilder.Build(samples, cardCategories, baseline: null);

        var row = Assert.Single(report.CategoryTendencies);
        Assert.Null(row.BaselinePresenceRatio);
        Assert.Null(row.Lift);
    }

    /// <summary>
    /// Verifies baseline presence and lift are computed from the supplied global deck counts.
    /// </summary>
    [Fact]
    public void Build_ComputesBaselinePresenceAndLiftWhenBaselineIsAvailable()
    {
        var samples = new[]
        {
            Sample("deck-1", Entry("Cultivate")),
            Sample("deck-2", Entry("Ponder")),
            Sample("deck-3", Entry("Cultivate")),
            Sample("deck-4"),
        };
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cultivate"] = ["Ramp"],
            ["Ponder"] = ["Draw"],
        };
        var baseline = new GlobalCategoryBaseline
        {
            TotalDecks = 10,
            DecksWithCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ramp"] = 5,
                ["Draw"] = 8,
            },
            DecksWithCategoryPair = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        };

        var report = DeckTendenciesReportBuilder.Build(samples, cardCategories, baseline);

        Assert.Collection(
            report.CategoryTendencies,
            row =>
            {
                Assert.Equal("Ramp", row.Category);
                Assert.Equal(0.5, row.AverageCountPerDeck);
                Assert.Equal(0.5, row.PresenceRatio);
                Assert.Equal(0.5, row.BaselinePresenceRatio);
                Assert.Equal(1.0, row.Lift);
            },
            row =>
            {
                Assert.Equal("Draw", row.Category);
                Assert.Equal(0.25, row.AverageCountPerDeck);
                Assert.Equal(0.25, row.PresenceRatio);
                Assert.Equal(0.8, row.BaselinePresenceRatio);
                Assert.Equal(0.3125, row.Lift);
            });
    }

    /// <summary>
    /// Verifies a zero baseline deck count is treated as missing instead of a zero denominator.
    /// </summary>
    [Fact]
    public void Build_LeavesBaselinePresenceAndLiftNullWhenBaselineCategoryDeckCountIsZero()
    {
        var samples = new[]
        {
            Sample("deck-1", Entry("Cultivate")),
            Sample("deck-2"),
        };
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cultivate"] = ["Ramp"],
        };
        var baseline = new GlobalCategoryBaseline
        {
            TotalDecks = 10,
            DecksWithCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ramp"] = 0,
            },
            DecksWithCategoryPair = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        };

        var row = Assert.Single(DeckTendenciesReportBuilder.Build(samples, cardCategories, baseline).CategoryTendencies);

        Assert.Equal(0.0, row.BaselinePresenceRatio);
        Assert.Null(row.Lift);
    }

    /// <summary>
    /// Verifies empty samples return an empty report instead of throwing.
    /// </summary>
    [Fact]
    public void Build_ReturnsEmptyReportForEmptySamples()
    {
        var report = DeckTendenciesReportBuilder.Build([], EmptyCategories);

        Assert.Equal(0, report.DeckCount);
        Assert.Empty(report.Decks);
        Assert.Empty(report.RepeatCards);
        Assert.Empty(report.RepeatCommanders);
        Assert.Empty(report.CategoryTendencies);
    }

    /// <summary>
    /// Verifies deck names apply by deck id while missing names remain null and commanders preserve entry order.
    /// </summary>
    [Fact]
    public void Build_AppliesDeckNamesAndPreservesDeckRowOrder()
    {
        var samples = new[]
        {
            Sample(
                "deck-1",
                folderName: "Folder A",
                entries:
                [
                    CommanderEntry("Brago, King Eternal"),
                    CommanderEntry("Spark Double"),
                    Entry("Ponder"),
                    SideboardEntry("Exclude Me"),
                ]),
            Sample("deck-2", folderName: "Folder B", entries: [Entry("Opt")]),
        };
        var deckNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["deck-1"] = "Blink Value",
        };

        var report = DeckTendenciesReportBuilder.Build(samples, EmptyCategories, deckNames: deckNames);

        Assert.Collection(
            report.Decks,
            row =>
            {
                Assert.Equal("deck-1", row.DeckId);
                Assert.Equal("Blink Value", row.DeckName);
                Assert.Equal(4, row.CardCount);
                Assert.Equal("Folder A", row.FolderName);
                Assert.Equal(["Brago, King Eternal", "Spark Double"], row.Commanders);
            },
            row =>
            {
                Assert.Equal("deck-2", row.DeckId);
                Assert.Null(row.DeckName);
                Assert.Equal(1, row.CardCount);
                Assert.Equal("Folder B", row.FolderName);
                Assert.Empty(row.Commanders);
            });
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyCategories { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    private static CreatorDeckSample Sample(string deckId, params DeckEntry[] entries)
    {
        return Sample(deckId, null, entries);
    }

    private static CreatorDeckSample Sample(string deckId, string? folderName, params DeckEntry[] entries)
    {
        return new CreatorDeckSample
        {
            DeckId = deckId,
            Entries = entries,
            CardCount = entries.Sum(entry => entry.Quantity),
            FolderName = folderName,
            ConfidenceMarker = "trusted",
        };
    }

    private static DeckEntry Entry(string name, int quantity = 1)
    {
        return new DeckEntry
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = quantity,
            Board = "mainboard",
        };
    }

    private static DeckEntry CommanderEntry(string name, int quantity = 1)
    {
        return new DeckEntry
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = quantity,
            Board = "commander",
        };
    }

    private static DeckEntry SideboardEntry(string name, int quantity = 1)
    {
        return new DeckEntry
        {
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            Quantity = quantity,
            Board = "sideboard",
        };
    }
}
