using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeckFlow.Core.Reporting;
using DeckFlow.Core.Models;
using DeckFlow.Web.Models;

namespace DeckFlow.Web.Services;

/// <summary>
/// Provides cached insights about commander category usage.
/// </summary>
public interface ICommanderCategoryService
{
    /// <summary>
    /// Retrieves category usage for the specified commander.
    /// </summary>
    Task<CommanderCategoryResult> LookupAsync(string commanderName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the commander category lookup outcome.
/// </summary>
public sealed record CommanderCategoryResult(
    string CommanderName,
    IReadOnlyList<CategoryKnowledgeRow> Rows,
    IReadOnlyList<CommanderCategorySummary> Summaries,
    int HarvestedDeckCount,
    CardDeckTotals CardDeckTotals);

/// <summary>
/// Default implementation of the commander category service.
/// </summary>
public sealed class CommanderCategoryService : ICommanderCategoryService
{
    private readonly ICategoryKnowledgeStore _knowledgeStore;

    /// <summary>
    /// Initializes a new instance of <see cref="CommanderCategoryService"/>.
    /// </summary>
    public CommanderCategoryService(ICategoryKnowledgeStore knowledgeStore)
    {
        _knowledgeStore = knowledgeStore;
    }

    /// <inheritdoc />
    public async Task<CommanderCategoryResult> LookupAsync(string commanderName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commanderName);

        var trimmed = commanderName.Trim();
        var rows = await _knowledgeStore.GetCategoryRowsForCommanderAsync(trimmed, cancellationToken);

        var deckCount = await _knowledgeStore.GetProcessedDeckCountAsync(cancellationToken);
        var commanderDeckCount = await _knowledgeStore.GetCommanderDeckCountAsync(trimmed, cancellationToken);
        var cardTotals = new CardDeckTotals(commanderDeckCount, new Dictionary<string, int>());
        var summaries = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Category))
            .GroupBy(row => CategoryCanonicalizer.CanonicalKey(row.Category), StringComparer.Ordinal)
            .Select(group =>
            {
                var category = group
                    .GroupBy(row => row.Category, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(labelGroup => labelGroup.Count())
                    .ThenByDescending(labelGroup => labelGroup.Sum(row => row.DeckCount))
                    .ThenByDescending(labelGroup => labelGroup.Sum(row => row.Count))
                    .ThenBy(labelGroup => labelGroup.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(labelGroup => CategoryCanonicalizer.Canonicalize(labelGroup.Key))
                    .First();
                var summaryDeckCount = group.Sum(row => row.DeckCount);
                var deckShare = commanderDeckCount > 0
                    ? (double)summaryDeckCount / commanderDeckCount
                    : 0d;

                return new CommanderCategorySummary(
                    category,
                    group.Sum(row => row.Count),
                    summaryDeckCount,
                    deckShare);
            })
            .Where(summary => summary.DeckCount >= 3 || summary.DeckShare >= 0.05d)
            .Where(summary => !CategoryFilter.IsJunk(summary.Category))
            .ToList();
        var includedCategories = CategoryFilter.IncludedOrFallback(summaries.Select(summary => summary.Category));
        var includedCategorySet = includedCategories.ToHashSet(StringComparer.OrdinalIgnoreCase);
        summaries = summaries
            .Where(summary => includedCategorySet.Contains(summary.Category))
            .OrderByDescending(summary => summary.DeckShare)
            .ThenByDescending(summary => summary.DeckCount)
            .ThenBy(summary => summary.Category, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CommanderCategoryResult(trimmed, rows, summaries, deckCount, cardTotals);
    }
}
