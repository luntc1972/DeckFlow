namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>
/// Persistence contract for the feature_flags table (Phase 6, FLAG-01..03).
/// Implementations bootstrap schema lazily on first call and seed default-on rows
/// for shipped features so a fresh DB never silently kills live behavior (FLAG-01).
/// </summary>
public interface IFeatureFlagStore
{
    /// <summary>
    /// Loads all flag rows as a key→enabled dict. Lazy-initializes schema + seed on first call.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the read.</param>
    /// <returns>Dictionary of flag-key to enabled state, keyed case-sensitively (D-08 lowercase-only).</returns>
    Task<IReadOnlyDictionary<string, bool>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// UPSERTs the flag row for <paramref name="key"/> with the new enabled value and
    /// updates updated_at = now. Idempotent. Caller is responsible for invalidating any
    /// in-memory cache (see IFeatureFlagCache.ReloadAsync, D-10).
    /// </summary>
    /// <param name="key">Dotted-namespace flag key (D-08), lowercase, dots only.</param>
    /// <param name="enabled">New enabled state.</param>
    /// <param name="cancellationToken">Token used to cancel the write.</param>
    Task SetEnabledAsync(string key, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces schema bootstrap (CREATE TABLE IF NOT EXISTS + seed) without reading any rows.
    /// Idempotent. Useful for tests that want to assert schema exists without consuming the
    /// first lazy-bootstrap inside GetAllAsync.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the bootstrap.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
}
