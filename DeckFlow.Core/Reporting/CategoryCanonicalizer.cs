namespace DeckFlow.Core.Reporting;

/// <summary>
/// Canonicalizes freeform deck category labels for grouping and deduplication.
/// </summary>
public static class CategoryCanonicalizer
{
    private static readonly Dictionary<string, string> CanonicalLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["card draw"] = "Draw",
        ["cuts"] = "Cut",
        ["creatures"] = "Creature",
        ["lands"] = "Land",
        ["tokens & extras"] = "Tokens",
    };

    /// <summary>Returns a canonical display label for <paramref name="category"/>.</summary>
    /// <param name="category">Freeform category label.</param>
    /// <returns>The canonical display label.</returns>
    public static string Canonicalize(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        var collapsed = CollapseWhitespace(category);
        return CanonicalLabels.TryGetValue(collapsed, out var canonical)
            ? canonical
            : collapsed;
    }

    /// <summary>Returns the case-folded grouping key for <paramref name="category"/>.</summary>
    /// <param name="category">Freeform category label.</param>
    /// <returns>Case-insensitive grouping key.</returns>
    public static string CanonicalKey(string category)
    {
        return Canonicalize(category).ToLowerInvariant();
    }

    private static string CollapseWhitespace(string category)
    {
        var trimmed = category.Trim();
        var builder = new System.Text.StringBuilder(trimmed.Length);
        var previousWasWhitespace = false;

        foreach (var character in trimmed)
        {
            if (char.IsWhiteSpace(character))
            {
                if (previousWasWhitespace)
                {
                    continue;
                }

                builder.Append(' ');
                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }
}
