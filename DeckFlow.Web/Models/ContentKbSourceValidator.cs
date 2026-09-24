using System.Text.RegularExpressions;

namespace DeckFlow.Web.Models;

/// <summary>Validates Content KB paths accepted as feedback source pages.</summary>
public static partial class ContentKbSourceValidator
{
    /// <summary>Returns the canonical source path when it is an allowed Content KB page.</summary>
    public static string? GetValidSource(string? source)
    {
        return source is not null && ContentKbPathRegex().IsMatch(source) ? source : null;
    }

    [GeneratedRegex("^/content-kb(/[A-Za-z0-9_-]+)?\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ContentKbPathRegex();
}
