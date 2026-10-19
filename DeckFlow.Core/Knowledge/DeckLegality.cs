using DeckFlow.Core.Integration;
using DeckFlow.Core.Loading;
using DeckFlow.Core.Models;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Evaluates Commander harvest eligibility according to spec 4.1 and OQ2.
/// </summary>
public static class DeckLegality
{
    /// <summary>Archidekt's Commander deck-format identifier.</summary>
    public const int CommanderDeckFormat = 3;

    /// <summary>Evaluates metadata, commander presence, and Commander card count in that order.</summary>
    public static DeckLegalityResult Evaluate(IReadOnlyList<DeckEntry> entries, ArchidektDeckMetadata? metadata)
    {
        if (metadata?.DeckFormat != CommanderDeckFormat)
        {
            return new DeckLegalityResult(false, DeckSkipReasons.NotCommanderFormat);
        }

        if (!entries.Any(DeckCommanderResolver.IsCommanderEntry))
        {
            return new DeckLegalityResult(false, DeckSkipReasons.NoCommander);
        }

        return DeckEntryLoader.CountCommanderDeckCards(entries) == 100
            ? new DeckLegalityResult(true, null)
            : new DeckLegalityResult(false, DeckSkipReasons.WrongSize);
    }
}
