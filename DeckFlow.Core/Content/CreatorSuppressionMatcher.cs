namespace DeckFlow.Core.Content;

/// <summary>Matches creator representations against a snapshot of suppression rows.</summary>
public sealed class CreatorSuppressionMatcher
{
    private readonly HashSet<string> _suppressedValues;

    /// <summary>Creates a matcher from the supplied suppression rows.</summary>
    public CreatorSuppressionMatcher(IReadOnlyList<CreatorSuppression> suppressions)
    {
        ArgumentNullException.ThrowIfNull(suppressions);
        _suppressedValues = suppressions
            .SelectMany(row => row.Aliases.Append(row.Slug))
            .Select(NormalizeValue)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Returns whether a creator name or alias is suppressed.</summary>
    public bool IsSuppressed(string nameOrAlias)
        => !string.IsNullOrWhiteSpace(nameOrAlias) && _suppressedValues.Contains(NormalizeValue(nameOrAlias));

    internal static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();
}
