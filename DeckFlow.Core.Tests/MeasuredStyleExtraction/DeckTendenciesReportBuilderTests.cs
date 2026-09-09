using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.MeasuredStyleExtraction;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Tests.MeasuredStyleExtraction;

/// <summary>
/// Unit tests for the pure deck tendencies report builder.
/// </summary>
public sealed class DeckTendenciesReportBuilderTests
{
    /// <summary>
    /// Verifies an empty sample list returns an empty report rather than throwing (PTOOL-04 empty
    /// edge assumption).
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
    /// Verifies passing a null sample list throws, rather than producing a report.
    /// </summary>
    [Fact]
    public void Build_ThrowsArgumentNullException_WhenSamplesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => DeckTendenciesReportBuilder.Build(null!, EmptyCategories));
    }

    /// <summary>
    /// Verifies passing a null card-category map throws, rather than producing a report.
    /// </summary>
    [Fact]
    public void Build_ThrowsArgumentNullException_WhenCardCategoriesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => DeckTendenciesReportBuilder.Build([], null!));
    }

    /// <summary>
    /// Verifies a single sample produces one deck row and no repeat rows (nothing repeats across
    /// one deck).
    /// </summary>
    [Fact]
    public void Build_SingleSample_ProducesOneDeckRowAndNoRepeatRows()
    {
        var samples = new[]
        {
            Sample(
                "deck-1",
                folderName: "Folder A",
                entries:
                [
                    CommanderEntry("Atraxa, Praetors' Voice"),
                    Entry("Sol Ring"),
                    Entry("Arcane Signet"),
                ]),
        };

        var report = DeckTendenciesReportBuilder.Build(samples, EmptyCategories);

        var row = Assert.Single(report.Decks);
        Assert.Equal("deck-1", row.DeckId);
        Assert.Equal(3, row.CardCount);
        Assert.Equal("Folder A", row.FolderName);
        Assert.Equal(["Atraxa, Praetors' Voice"], row.Commanders);
        Assert.Empty(report.RepeatCards);
        Assert.Empty(report.RepeatCommanders);
    }

    /// <summary>
    /// Verifies repeated non-commander cards are ranked by deck count then ordinal card name, and
    /// that a card in exactly two of three decks is included (the at-least-two threshold is
    /// inclusive at two) while ties on deck count break deterministically by card name.
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
    /// Verifies the at-least-two repeat threshold is inclusive at exactly two decks: a card present
    /// in exactly two of three decks is reported, a card present in exactly one is not.
    /// </summary>
    [Fact]
    public void Build_RepeatThreshold_IsInclusiveAtExactlyTwoDecksAndExcludesSingleDeckCards()
    {
        var samples = new[]
        {
            Sample("deck-1", Entry("Rhystic Study"), Entry("Lone Card")),
            Sample("deck-2", Entry("Rhystic Study")),
            Sample("deck-3", Entry("Something Else")),
        };

        var report = DeckTendenciesReportBuilder.Build(samples, EmptyCategories);

        var row = Assert.Single(report.RepeatCards);
        Assert.Equal("Rhystic Study", row.CardName);
        Assert.Equal(2, row.DeckCount);
        Assert.DoesNotContain(report.RepeatCards, r => r.CardName == "Lone Card");
        Assert.DoesNotContain(report.RepeatCards, r => r.CardName == "Something Else");
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
    /// Verifies commander-board cards are excluded from repeat cards and counted separately, and
    /// that basic lands are NOT excluded from the repeat-commanders board (only from the
    /// non-commander board).
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
    /// Verifies a repeated basic land on the commander board is not excluded there, distinguishing
    /// repeat-commander handling from repeat-card handling.
    /// </summary>
    [Fact]
    public void Build_DoesNotExcludeBasicLandsFromRepeatCommanders()
    {
        var samples = new[]
        {
            Sample("deck-1", CommanderEntry("Plains")),
            Sample("deck-2", CommanderEntry("Plains")),
        };

        var report = DeckTendenciesReportBuilder.Build(samples, EmptyCategories);

        var repeatCommander = Assert.Single(report.RepeatCommanders);
        Assert.Equal("Plains", repeatCommander.CardName);
        Assert.Empty(report.RepeatCards);
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
    /// Verifies cards on boards outside mainboard/commander (e.g. sideboard) are excluded from
    /// repeat-card computation but do not change the reported deck count, which counts the
    /// supplied samples rather than the filtered entries.
    /// </summary>
    [Fact]
    public void Build_ExcludesNonIncludedBoardsFromRepeatsButNotFromDeckCount()
    {
        var samples = new[]
        {
            Sample("deck-1", SideboardEntry("Sideboard Tech")),
            Sample("deck-2", SideboardEntry("Sideboard Tech")),
        };

        var report = DeckTendenciesReportBuilder.Build(samples, EmptyCategories);

        Assert.Equal(2, report.DeckCount);
        Assert.Empty(report.RepeatCards);
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
    /// Verifies a category's quantity-weighted tendency reflects <see cref="DeckEntry.Quantity"/>
    /// rather than per-deck presence: a card at quantity 4 in one deck contributes 4, not 1, so the
    /// weighted average must differ from what presence-only counting (1 + 1) / 2 = 1.0 would give
    /// (PTOOL-04 explicit quantity-weighting requirement).
    /// </summary>
    [Fact]
    public void Build_CategoryAverageIsQuantityWeighted_NotPresenceCounted()
    {
        var samples = new[]
        {
            Sample("deck-1", Entry("Cultivate", quantity: 1)),
            Sample("deck-2", Entry("Rampant Growth", quantity: 4)),
        };
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Cultivate"] = ["Ramp"],
            ["Rampant Growth"] = ["Ramp"],
        };

        var report = DeckTendenciesReportBuilder.Build(samples, cardCategories);

        var row = Assert.Single(report.CategoryTendencies);
        Assert.Equal("Ramp", row.Category);
        // Quantity-weighted: (1 + 4) / 2 decks = 2.5. Presence-only counting would give (1 + 1) / 2 = 1.0.
        Assert.Equal(2.5, row.AverageCountPerDeck);
        Assert.NotEqual(1.0, row.AverageCountPerDeck);
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
    /// Verifies deck names apply by deck id while missing names remain null and commanders preserve
    /// entry order, and that deck rows preserve source input order.
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

    /// <summary>
    /// Verifies two runs over the same input produce equal reports, including collection order —
    /// the builder is deterministic and does not depend on unordered-collection iteration.
    /// </summary>
    [Fact]
    public void Build_IsDeterministicAcrossRepeatedRuns_IncludingCollectionOrder()
    {
        var samples = new[]
        {
            Sample("deck-1", CommanderEntry("Atraxa, Praetors' Voice"), Entry("Arcane Signet"), Entry("Counterspell")),
            Sample("deck-2", CommanderEntry("Atraxa, Praetors' Voice"), Entry("Arcane Signet"), Entry("Negate")),
            Sample("deck-3", CommanderEntry("Muldrotha, the Gravetide"), Entry("Counterspell"), Entry("Negate")),
        };
        var cardCategories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Arcane Signet"] = ["Ramp"],
            ["Counterspell"] = ["Interaction"],
            ["Negate"] = ["Interaction"],
        };
        var baseline = new GlobalCategoryBaseline
        {
            TotalDecks = 10,
            DecksWithCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Ramp"] = 5,
                ["Interaction"] = 7,
            },
            DecksWithCategoryPair = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
        };

        var first = DeckTendenciesReportBuilder.Build(samples, cardCategories, baseline);
        var second = DeckTendenciesReportBuilder.Build(samples, cardCategories, baseline);

        Assert.Equal(first.DeckCount, second.DeckCount);
        Assert.Equal(first.Decks.Select(row => row.DeckId), second.Decks.Select(row => row.DeckId));
        Assert.Equal(first.RepeatCards.Select(row => row.CardName), second.RepeatCards.Select(row => row.CardName));
        Assert.Equal(first.RepeatCommanders.Select(row => row.CardName), second.RepeatCommanders.Select(row => row.CardName));
        Assert.Equal(first.CategoryTendencies.Select(row => row.Category), second.CategoryTendencies.Select(row => row.Category));
        Assert.Equal(
            first.CategoryTendencies.Select(row => row.AverageCountPerDeck),
            second.CategoryTendencies.Select(row => row.AverageCountPerDeck));
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
