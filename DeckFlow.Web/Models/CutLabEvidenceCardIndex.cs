using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Models;

/// <summary>Indexes non-commander cards for matching formatted finding evidence.</summary>
public sealed class CutLabEvidenceCardIndex
{
    private readonly (CutLabPoolCard Card, string NormalizedName, string ManaValuePrefix)[] _cards;

    /// <summary>Precomputes stable longest-name-first evidence candidates.</summary>
    public CutLabEvidenceCardIndex(IReadOnlyList<CutLabPoolCard> pool)
    {
        _cards = pool.Where(card => !card.IsCommander)
            .OrderByDescending(card => card.Name.Length)
            .Select(card =>
            {
                string normalizedName = NormalizeAsciiCase(card.Name);
                return (card, normalizedName, $"{normalizedName} · mv ");
            }).ToArray();
    }

    /// <summary>Finds a card using the existing exact name and mana-value suffix rules.</summary>
    public CutLabPoolCard? Find(string evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        string normalizedEvidence = NormalizeAsciiCase(evidence);

        return _cards
            .FirstOrDefault(entry =>
            {
                string normalizedCardName = entry.NormalizedName;
                if (normalizedEvidence.Equals(normalizedCardName, StringComparison.Ordinal))
                {
                    return true;
                }

                string manaValuePrefix = entry.ManaValuePrefix;
                return normalizedEvidence.StartsWith(manaValuePrefix, StringComparison.Ordinal)
                    && IsValidManaValueSuffix(normalizedEvidence.AsSpan(manaValuePrefix.Length));
            }).Card;
    }

    private static string NormalizeAsciiCase(string value) =>
        string.Create(value.Length, value, static (characters, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                characters[index] = character is >= 'A' and <= 'Z'
                    ? (char)(character + ('a' - 'A'))
                    : character;
            }
        });

    private static bool IsValidManaValueSuffix(ReadOnlySpan<char> value)
    {
        int index = 0;
        while (index < value.Length && IsAsciiDigit(value[index]))
        {
            index++;
        }

        if (index == 0)
        {
            return false;
        }

        if (index == value.Length)
        {
            return true;
        }

        if (value[index] != '.')
        {
            return false;
        }

        int fractionalStart = ++index;
        while (index < value.Length && IsAsciiDigit(value[index]))
        {
            index++;
        }

        int fractionalDigits = index - fractionalStart;
        return index == value.Length && fractionalDigits is >= 1 and <= 2;
    }

    private static bool IsAsciiDigit(char character) =>
        character is >= '0' and <= '9';

}
