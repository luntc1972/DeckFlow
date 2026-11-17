using System.Diagnostics.CodeAnalysis;

namespace DeckFlow.Core.Normalization;

/// <summary>
/// Matches a fixed list of canonical card names using normalized keys.
/// </summary>
public sealed class NormalizedCardNameSet
{
    private readonly IReadOnlyDictionary<string, string> _names;

    /// <summary>
    /// Creates a set from names with distinct, nonempty normalized keys.
    /// </summary>
    /// <param name="names">Canonical card names to retain exactly as supplied.</param>
    /// <exception cref="ArgumentNullException">The sequence is null.</exception>
    /// <exception cref="ArgumentException">An entry is invalid or shares a normalized key.</exception>
    public NormalizedCardNameSet(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Card names must not be null or whitespace.", nameof(names));
            }

            var key = CardNormalizer.Normalize(name);
            if (key.Length == 0)
            {
                throw new ArgumentException($"Card name '{name}' normalizes to an empty key.", nameof(names));
            }

            if (entries.TryGetValue(key, out var existingName))
            {
                // Why: Curated collisions are data errors and make canonical-name lookup ambiguous.
                throw new ArgumentException($"Card names '{existingName}' and '{name}' share a normalized key.", nameof(names));
            }

            entries.Add(key, name);
        }

        _names = entries;
    }

    /// <summary>
    /// Returns whether the supplied name matches a listed normalized key.
    /// </summary>
    /// <param name="name">Card name to check; null and whitespace do not match.</param>
    /// <returns>Whether a listed name matches.</returns>
    public bool Contains(string? name)
        => TryGetCanonicalName(name, out _);

    /// <summary>
    /// Resolves a name to the exact spelling supplied in the list.
    /// </summary>
    /// <param name="name">Card name to resolve; null and whitespace do not match.</param>
    /// <param name="canonicalName">The listed name on success, otherwise null.</param>
    /// <returns>Whether a listed name matches.</returns>
    public bool TryGetCanonicalName(string? name, [NotNullWhen(true)] out string? canonicalName)
    {
        canonicalName = null;
        return !string.IsNullOrWhiteSpace(name)
            && _names.TryGetValue(CardNormalizer.Normalize(name), out canonicalName);
    }
}
