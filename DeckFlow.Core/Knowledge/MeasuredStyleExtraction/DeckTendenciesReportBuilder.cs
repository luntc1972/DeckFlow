using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Knowledge.MeasuredStyleExtraction;

/// <summary>
/// Pure builder for deterministic creator deck tendency reports.
/// </summary>
public static class DeckTendenciesReportBuilder
{
    private static readonly string[] IncludedBoards =
    [
        "mainboard",
        "commander",
    ];

    private static readonly HashSet<string> BasicLandNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Plains",
        "Island",
        "Swamp",
        "Mountain",
        "Forest",
        "Wastes",
        "Snow-Covered Plains",
        "Snow-Covered Island",
        "Snow-Covered Swamp",
        "Snow-Covered Mountain",
        "Snow-Covered Forest",
        "Snow-Covered Wastes",
    };

    /// <summary>
    /// Builds a deterministic deck tendencies report from creator deck samples.
    /// </summary>
    /// <param name="samples">Creator deck samples to summarize.</param>
    /// <param name="cardCategories">Resolved category map keyed by card name.</param>
    /// <param name="baseline">Optional global category baseline for lift math.</param>
    /// <param name="deckNames">Optional deck-name map keyed by deck id.</param>
    /// <returns>Deterministic report record derived from the supplied samples.</returns>
    public static DeckTendenciesReport Build(
        IReadOnlyList<CreatorDeckSample> samples,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories,
        GlobalCategoryBaseline? baseline = null,
        IReadOnlyDictionary<string, string>? deckNames = null)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(cardCategories);

        var includedSamples = samples.Select(FilterIncludedBoards).ToArray();
        var stapleSet = StapleStripper.ComputePersonalStaples(includedSamples);

        return new DeckTendenciesReport
        {
            DeckCount = samples.Count,
            Decks = BuildDeckRows(samples, deckNames),
            RepeatCards = BuildRepeatRows(
                includedSamples,
                stapleSet,
                board: "mainboard",
                excludeBasicLands: true),
            RepeatCommanders = BuildRepeatRows(
                includedSamples,
                stapleSet,
                board: "commander",
                excludeBasicLands: false),
            CategoryTendencies = BuildCategoryRows(includedSamples, cardCategories, baseline),
        };
    }

    private static IReadOnlyList<DeckTendencyDeckRow> BuildDeckRows(
        IReadOnlyList<CreatorDeckSample> originalSamples,
        IReadOnlyDictionary<string, string>? deckNames)
    {
        var rows = new DeckTendencyDeckRow[originalSamples.Count];

        for (var i = 0; i < originalSamples.Count; i++)
        {
            var sample = originalSamples[i];

            rows[i] = new DeckTendencyDeckRow
            {
                DeckId = sample.DeckId,
                DeckName = TryGetDeckName(sample.DeckId, deckNames),
                CardCount = sample.CardCount,
                FolderName = sample.FolderName,
                Commanders = sample.Entries
                    .Where(entry => IsBoard(entry, "commander"))
                    .Select(entry => entry.Name)
                    .ToArray(),
            };
        }

        return rows;
    }

    private static IReadOnlyList<RepeatCardRow> BuildRepeatRows(
        IReadOnlyList<CreatorDeckSample> samples,
        IReadOnlySet<string> stapleSet,
        string board,
        bool excludeBasicLands)
    {
        var deckCountByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var displayNameByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sample in samples)
        {
            var namesInDeck = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in sample.Entries)
            {
                if (!IsBoard(entry, board))
                {
                    continue;
                }

                if (excludeBasicLands && IsBasicLand(entry.Name))
                {
                    continue;
                }

                if (!namesInDeck.Add(entry.Name))
                {
                    continue;
                }

                deckCountByName[entry.Name] = deckCountByName.TryGetValue(entry.Name, out var count) ? count + 1 : 1;

                if (!displayNameByName.ContainsKey(entry.Name))
                {
                    displayNameByName[entry.Name] = entry.Name;
                }
            }
        }

        return deckCountByName
            .Where(pair => pair.Value >= 2)
            .Select(pair => new RepeatCardRow
            {
                CardName = displayNameByName[pair.Key],
                DeckCount = pair.Value,
                Frequency = pair.Value / (double)samples.Count,
                IsPersonalStaple = stapleSet.Contains(pair.Key),
            })
            .OrderByDescending(row => row.DeckCount)
            .ThenBy(row => row.CardName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<CategoryTendencyRow> BuildCategoryRows(
        IReadOnlyList<CreatorDeckSample> samples,
        IReadOnlyDictionary<string, IReadOnlyList<string>> cardCategories,
        GlobalCategoryBaseline? baseline)
    {
        var totalDecks = samples.Count;
        IReadOnlyDictionary<string, double> averageCounts = CategoryCounter.AggregateCounts(samples, cardCategories);
        var presenceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var sample in samples)
        {
            var counts = CategoryCounter.CountPerDeck(sample, cardCategories);

            foreach (var pair in counts)
            {
                if (pair.Value > 0)
                {
                    presenceCounts[pair.Key] = presenceCounts.TryGetValue(pair.Key, out var present) ? present + 1 : 1;
                }
            }
        }

        return averageCounts
            .Select(pair => BuildCategoryRow(pair.Key, pair.Value, totalDecks, presenceCounts, baseline))
            .OrderByDescending(row => row.AverageCountPerDeck)
            .ThenBy(row => row.Category, StringComparer.Ordinal)
            .ToArray();
    }

    private static CategoryTendencyRow BuildCategoryRow(
        string category,
        double averageCountPerDeck,
        int totalDecks,
        IReadOnlyDictionary<string, int> presenceCounts,
        GlobalCategoryBaseline? baseline)
    {
        var presenceRatio = presenceCounts.TryGetValue(category, out var presenceCount)
            ? presenceCount / (double)totalDecks
            : 0;

        var baselinePresenceRatio = TryGetBaselinePresenceRatio(category, baseline);

        return new CategoryTendencyRow
        {
            Category = category,
            AverageCountPerDeck = averageCountPerDeck,
            PresenceRatio = presenceRatio,
            BaselinePresenceRatio = baselinePresenceRatio,
            Lift = baselinePresenceRatio is > 0 ? presenceRatio / baselinePresenceRatio.Value : null,
        };
    }

    private static double? TryGetBaselinePresenceRatio(string category, GlobalCategoryBaseline? baseline)
    {
        if (baseline is null || baseline.TotalDecks <= 0)
        {
            return null;
        }

        return baseline.DecksWithCategory.TryGetValue(category, out var baselineDeckCount)
            ? baselineDeckCount / (double)baseline.TotalDecks
            : null;
    }

    private static CreatorDeckSample FilterIncludedBoards(CreatorDeckSample sample)
    {
        return sample with
        {
            Entries = sample.Entries.Where(IsIncludedBoard).ToArray(),
        };
    }

    private static bool IsIncludedBoard(DeckEntry entry)
    {
        return IncludedBoards.Contains(entry.Board, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsBoard(DeckEntry entry, string board)
    {
        return string.Equals(entry.Board, board, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBasicLand(string cardName)
    {
        return BasicLandNames.Contains(cardName);
    }

    private static string? TryGetDeckName(string deckId, IReadOnlyDictionary<string, string>? deckNames)
    {
        if (deckNames is null)
        {
            return null;
        }

        return deckNames.TryGetValue(deckId, out var deckName) ? deckName : null;
    }
}
