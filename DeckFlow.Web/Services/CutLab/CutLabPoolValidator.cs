namespace DeckFlow.Web.Services.CutLab;

/// <summary>Validation guards for Cut Lab deck-source length and oversized-pool card-count intake rules.</summary>
public static class CutLabPoolValidator
{
    /// <summary>Maximum allowed deck-input length before import is rejected.</summary>
    public const int MaxDeckSourceChars = 100_000;

    /// <summary>Minimum valid non-commander pool size accepted by Cut Lab.</summary>
    public const int MinPoolCards = 101;

    /// <summary>Maximum valid non-commander pool size accepted by Cut Lab.</summary>
    public const int MaxPoolCards = 150;

    /// <summary>Rejects oversized raw deck input before any deck loading or resolution work begins.</summary>
    /// <param name="deckSourceLength">Character count of the raw deck input.</param>
    public static void ValidateSourceLength(int deckSourceLength)
    {
        if (deckSourceLength > MaxDeckSourceChars)
        {
            throw new InvalidOperationException("That deck input is too large to import.");
        }
    }

    /// <summary>Rejects non-commander pool sizes outside Cut Lab's supported 101-150 inclusive range.</summary>
    /// <param name="nonCommanderCardCount">Loaded non-commander pool card count, excluding the commander — the commander is the plus one.</param>
    public static void ValidateCardCount(int nonCommanderCardCount)
    {
        if (nonCommanderCardCount < MinPoolCards)
        {
            throw new InvalidOperationException("This pool already has 100 cards or fewer — Cut Lab is for trimming an oversized pool down to 100. Try Deck Sync or Deck Analysis instead.");
        }

        if (nonCommanderCardCount > MaxPoolCards)
        {
            throw new InvalidOperationException("This pool has too many cards for Cut Lab (limit 150 plus commander). Trim it closer to 150 before importing.");
        }
    }
}
