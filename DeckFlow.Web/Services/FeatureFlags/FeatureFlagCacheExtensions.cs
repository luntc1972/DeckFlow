namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>Provides fail-safe feature flag checks.</summary>
public static class FeatureFlagCacheExtensions
{
    /// <summary>Returns true only when the flag is present and enabled.</summary>
    public static bool IsExplicitlyEnabled(this IFeatureFlagCache? cache, string key)
        => cache is not null && cache.Snapshot().TryGetValue(key, out bool on) && on;
}
