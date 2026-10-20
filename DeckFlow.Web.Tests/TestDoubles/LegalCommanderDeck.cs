using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;
using DeckFlow.Core.Normalization;

namespace DeckFlow.Web.Tests;

/// <summary>
/// Creates legal Commander deck fixtures; duplicated from Core tests because test projects do not reference each other.
/// </summary>
internal static class LegalCommanderDeck
{
    internal const string CommanderName = "Test Commander";

    internal static ArchidektDeckMetadata Metadata(DateTimeOffset? updatedUtc = null)
        => new(3, DeckLegality.CommanderDeckFormat, false, null, updatedUtc, DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    internal static ArchidektDeckMetadata ForceCommanderFormat(ArchidektDeckMetadata? supplied, DateTimeOffset? updatedUtc = null)
        => supplied is null
            ? Metadata(updatedUtc)
            : supplied with { DeckFormat = DeckLegality.CommanderDeckFormat };

    internal static void AssertLegal(IReadOnlyList<DeckEntry> entries, ArchidektDeckMetadata? metadata)
    {
        var result = DeckLegality.Evaluate(entries, metadata);
        if (!result.IsLegal)
        {
            throw new InvalidOperationException($"Expected a legal Commander deck, but got {result.SkipReason}.");
        }
    }

    internal static List<DeckEntry> Complete(IEnumerable<DeckEntry> entries)
    {
        var complete = entries.ToList();
        if (!complete.Any(entry => string.Equals(entry.Board, "commander", StringComparison.OrdinalIgnoreCase)))
        {
            complete.Add(new DeckEntry
            {
                Name = CommanderName,
                NormalizedName = CardNormalizer.Normalize(CommanderName),
                Quantity = 1,
                Board = "commander",
            });
        }

        var total = DeckEntryLoader.CountCommanderDeckCards(complete);
        if (total > 100)
        {
            throw new InvalidOperationException("Fixture deck already exceeds 100 cards.");
        }

        if (total < 100)
        {
            complete.Add(new DeckEntry
            {
                Name = "Wastes",
                NormalizedName = "wastes",
                Quantity = 100 - total,
                Board = "mainboard",
            });
        }

        return complete;
    }

    internal static ArchidektDeckImportResult Result(IEnumerable<DeckEntry> entries, DateTimeOffset? updatedUtc = null)
        => new(Complete(entries), Metadata(updatedUtc));
}
