using System;
using System.Collections.Generic;
using System.Linq;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services.Categories;

/// <summary>
/// Builds weighted category rows shared by the MVC and API suggestion paths.
/// </summary>
public static class CategoryWeightRowFactory
{
    // Ranks the weighted merge rows for display: most agreed-on first, then by popularity
    // (rows without a crawl percentage sink below those that have one), then alphabetical.
    /// <summary>Builds ranked weighted category rows for display.</summary>
    public static IReadOnlyList<CategoryWeightRow> Build(
        IReadOnlyList<CategorySuggestionReporter.CategorySourceWeight> weighted,
        IReadOnlyDictionary<string, int> categoryDeckCounts,
        int totalDeckCount)
        => weighted
            .Select(weight => BuildCategoryWeightRow(weight, categoryDeckCounts, totalDeckCount))
            .OrderByDescending(row => row.SourceCount)
            .ThenBy(row => row.Percent is null ? 1 : 0)
            .ThenByDescending(row => row.Percent)
            .ThenBy(row => row.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static CategoryWeightRow BuildCategoryWeightRow(
        CategorySuggestionReporter.CategorySourceWeight weight,
        IReadOnlyDictionary<string, int> categoryDeckCounts,
        int totalDeckCount)
    {
        var canonicalKey = CategoryCanonicalizer.CanonicalKey(weight.Category);
        if (!categoryDeckCounts.TryGetValue(canonicalKey, out var deckCount) || totalDeckCount <= 0)
        {
            return new CategoryWeightRow(weight.Category, null, null, weight.SourceCount, weight.SourceTotal);
        }

        var percent = (int)Math.Round((double)deckCount * 100d / totalDeckCount, MidpointRounding.AwayFromZero);
        percent = Math.Clamp(percent, 0, 100);
        return new CategoryWeightRow(weight.Category, deckCount, percent, weight.SourceCount, weight.SourceTotal);
    }
}
