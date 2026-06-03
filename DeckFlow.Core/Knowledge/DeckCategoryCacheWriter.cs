using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;
using DeckFlow.Core.Reporting;

namespace DeckFlow.Core.Knowledge;

internal static class DeckCategoryCacheWriter
{
    /// <summary>
    /// Replaces the cached category rows for a single deck source.
    /// </summary>
    /// <param name="repository">Repository the categories should be persisted to.</param>
    /// <param name="source">Source label for the deck.</param>
    /// <param name="entries">Deck entries to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ReplaceDeckEntriesAsync(CategoryKnowledgeRepository repository, string source, IEnumerable<DeckEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrEmpty(source);

        await repository.DeleteSourceDataAsync(source, cancellationToken);
        await PersistDeckEntriesAsync(repository, source, entries, cancellationToken);
    }

    /// <summary>
    /// Persists the categories found in a single deck to the repository.
    /// </summary>
    /// <param name="repository">Repository the categories should be persisted to.</param>
    /// <param name="source">Source label for the deck.</param>
    /// <param name="entries">Stack of deck entries to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task PersistDeckEntriesAsync(CategoryKnowledgeRepository repository, string source, IEnumerable<DeckEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrEmpty(source);
        if (entries is null)
        {
            return;
        }

        var batch = BuildCanonicalBatch(entries);

        await repository.PersistDeckCategoryBatchAsync(source, batch.Observations, batch.Totals, cancellationToken);
    }

    internal static (IReadOnlyList<(string CardName, string Category, string Board, int Quantity, int DeckCountIncrement)> Observations, IReadOnlyList<(string CardName, string Board)> Totals) BuildCanonicalBatch(IEnumerable<DeckEntry>? entries)
    {
        var counts = new Dictionary<(string CardName, string Category, string Board), (int Quantity, int DeckIncrement)>(BoardCategoryComparer.Instance);
        var cardBoardHits = new HashSet<(string CardName, string Board)>(CardBoardComparer.Instance);

        if (entries is not null)
        {
            foreach (var entry in entries)
            {
                var board = NormalizeBoard(entry.Board);
                cardBoardHits.Add((entry.Name, board));
                foreach (var category in CategoryKnowledgeReporter.SplitCategories(entry.Category))
                {
                    var key = (entry.Name, category, board);
                    counts[key] = counts.TryGetValue(key, out var existing)
                        ? (existing.Quantity + entry.Quantity, existing.DeckIncrement)
                        : (entry.Quantity, 0);
                }
            }
        }

        var observations = new List<(string CardName, string Category, string Board, int Quantity, int DeckCountIncrement)>(counts.Count);
        foreach (var group in counts)
        {
            observations.Add((
                group.Key.CardName,
                group.Key.Category,
                group.Key.Board,
                group.Value.Quantity,
                DeckCountIncrement: 1));
        }

        var totals = new List<(string CardName, string Board)>(cardBoardHits.Count);
        foreach (var cardBoard in cardBoardHits)
        {
            totals.Add(cardBoard);
        }

        return (observations, totals);
    }

    internal static string ComputeCanonicalHash(IEnumerable<DeckEntry>? entries)
    {
        var batch = BuildCanonicalBatch(entries);
        var records = new List<string>(batch.Observations.Count + batch.Totals.Count);

        foreach (var observation in batch.Observations)
        {
            records.Add(EncodeRecord(
                "obs",
                CardNormalizer.Normalize(observation.CardName),
                observation.Category,
                observation.Board,
                observation.Quantity.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var total in batch.Totals)
        {
            records.Add(EncodeRecord(
                "total",
                CardNormalizer.Normalize(total.CardName),
                total.Board));
        }

        records.Sort(StringComparer.Ordinal);
        var canonical = string.Join('\n', records);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string EncodeRecord(params string[] fields)
    {
        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            builder.Append(Encoding.UTF8.GetByteCount(field));
            builder.Append(':');
            builder.Append(field);
        }

        return builder.ToString();
    }

    private static string NormalizeBoard(string? board)
    {
        if (string.IsNullOrWhiteSpace(board))
        {
            return "mainboard";
        }

        return board.Trim().ToLowerInvariant();
    }
}
