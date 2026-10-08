namespace DeckFlow.Core.Content;

/// <summary>Matches creator representations against a snapshot of suppression rows.</summary>
public sealed class CreatorSuppressionMatcher
{
    /// <summary>Compares creator values using the matcher normalization rules.</summary>
    public static IEqualityComparer<string> ValueComparer { get; } = new NormalizedValueComparer();

    private readonly HashSet<string> _suppressedValues;

    /// <summary>Creates a matcher from the supplied suppression rows.</summary>
    public CreatorSuppressionMatcher(IReadOnlyList<CreatorSuppression> suppressions)
    {
        ArgumentNullException.ThrowIfNull(suppressions);
        _suppressedValues = suppressions
            .SelectMany(row => row.Aliases.Append(row.Slug))
            .Select(NormalizeValue)
            .ToHashSet(ValueComparer);
    }

    /// <summary>Returns whether a creator name or alias is suppressed.</summary>
    public bool IsSuppressed(string nameOrAlias)
        => !string.IsNullOrWhiteSpace(nameOrAlias) && _suppressedValues.Contains(NormalizeValue(nameOrAlias));

    internal static string NormalizeValue(string value) => value.Trim().ToLowerInvariant();

    private sealed class NormalizedValueComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y)
            => ReferenceEquals(x, y)
                || x is not null && y is not null && string.Equals(NormalizeValue(x), NormalizeValue(y), StringComparison.Ordinal);

        public int GetHashCode(string obj) => StringComparer.Ordinal.GetHashCode(NormalizeValue(obj));
    }
}
