namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>
/// Derives FLAGS-01 chips from flag keys. Chips are namespace-only per D-03, use dot-terminated
/// prefixes so service never captures services.x, and use friendly D-13 labels.
/// </summary>
public static class FlagFilterGroups
{
    // Why: labels remain server-side so Razor and admin-flags.ts need no label logic (D-13).
    private static readonly IReadOnlyDictionary<string, string> FriendlyLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["analysis"] = "Analysis",
        ["service"] = "Background services",
        ["sync"] = "Sync",
        ["tool"] = "Tools",
    };

    /// <summary>Returns the friendly chip label for a lower-cased namespace segment.</summary>
    /// <param name="namespaceSegment">The namespace segment produced by <see cref="Derive"/>.</param>
    /// <returns>A mapped friendly label or the segment with only its first character upper-cased.</returns>
    public static string LabelFor(string namespaceSegment)
    {
        ArgumentException.ThrowIfNullOrEmpty(namespaceSegment);

        return FriendlyLabels.TryGetValue(namespaceSegment, out var label)
            ? label
            : char.ToUpperInvariant(namespaceSegment[0]) + namespaceSegment[1..];
    }

    /// <summary>Derives ordered namespace chips from listed keys.</summary>
    /// <param name="keys">Flag keys currently listed on the page.</param>
    /// <returns>Namespace chips with friendly labels sorted ordinally by raw namespace.</returns>
    public static IReadOnlyList<FlagFilterGroup> Derive(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        return keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.ToLowerInvariant())
            .Select(key => new { Key = key, Dot = key.IndexOf('.') })
            .Where(item => item.Dot > 0)
            .GroupBy(item => item.Key[..item.Dot], StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new FlagFilterGroup(group.Key + ".", LabelFor(group.Key), group.Count()))
            .ToArray();
    }
}
