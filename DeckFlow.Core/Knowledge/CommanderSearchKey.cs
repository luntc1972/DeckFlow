using System.Globalization;
using System.Text;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Produces the only <c>commander_name_search_key</c> values and search prefixes.
/// </summary>
public static class CommanderSearchKey
{
    /// <summary>
    /// Normalizes a commander name for accent-insensitive prefix matching.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        try
        {
            var decomposed = trimmed.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                // Why: this accent fold runs in C# because SQLite's lower() folds ASCII only.
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            var normalized = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
            return normalized.Length == 0 ? null : normalized;
        }
        catch (ArgumentException)
        {
            var fallback = trimmed.ToLowerInvariant();
            return fallback.Length == 0 ? null : fallback;
        }
    }
}
