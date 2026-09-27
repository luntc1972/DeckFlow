namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>
/// Derives FLAGS-01 chips from flag keys. Chips are namespace-only per D-03, use dot-terminated
/// prefixes so service never captures services.x, and retain raw first-segment D-02 labels.
/// </summary>
public static class FlagFilterGroups
{
    /// <summary>Derives ordered namespace chips from listed keys.</summary>
    /// <param name="keys">Flag keys currently listed on the page.</param>
    /// <returns>Namespace chips sorted ordinally by label.</returns>
    public static IReadOnlyList<FlagFilterGroup> Derive(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        return keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.ToLowerInvariant())
            .Select(key => new { Key = key, Dot = key.IndexOf('.') })
            .Where(item => item.Dot > 0)
            .GroupBy(item => item.Key[..item.Dot], StringComparer.Ordinal)
            .Select(group => new FlagFilterGroup(group.Key + ".", group.Key, group.Count()))
            .OrderBy(group => group.Label, StringComparer.Ordinal)
            .ToArray();
    }
}
