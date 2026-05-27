using System.Text.RegularExpressions;

namespace DeckFlow.Core.Content;

/// <summary>
/// Builds deterministic URL-safe slugs from content source display names.
/// </summary>
public static class SlugifySourceName
{
    /// <summary>
    /// Converts a source display name to a lowercase ASCII slug.
    /// </summary>
    /// <param name="name">Source display name.</param>
    /// <returns>A non-empty lowercase ASCII slug.</returns>
    public static string Slugify(string name)
    {
        var slug = Regex
            .Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-")
            .Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? "source" : slug;
    }
}
