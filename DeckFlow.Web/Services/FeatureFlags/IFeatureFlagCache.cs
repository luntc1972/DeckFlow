namespace DeckFlow.Web.Services.FeatureFlags;

/// <summary>
/// In-memory snapshot of the feature_flags table (Phase 6, FLAG-02). Reads are lock-free,
/// allocation-free hot-path lookups. Refreshed by:
///   1. Synchronous initial load in StartAsync before Kestrel binds (D-14).
///   2. 30-second BackgroundService poller as backstop (FLAG-02).
///   3. Explicit ReloadAsync from the admin write path so toggles take effect within
///      one HTTP round-trip (D-10).
/// </summary>
public interface IFeatureFlagCache
{
    /// <summary>
    /// Returns the current value for the flag key, or true (default-on) if the key is
    /// not present in the snapshot (D-13). Emits a WARN log on first miss per key per
    /// process; subsequent misses for the same key are silent (dedupe).
    /// </summary>
    /// <param name="key">Flag key (dotted-namespace, e.g. "service.scryfall-tagger.enabled").</param>
    /// <returns>True if the flag is enabled, or true (default-on) if the key is missing.</returns>
    bool IsEnabled(string key);

    /// <summary>
    /// Returns the current snapshot as a read-only key→enabled dict. Used by
    /// /Admin/Flags index view (plan 05) to render the toggle list.
    /// </summary>
    /// <returns>The current snapshot of all known feature flag values.</returns>
    IReadOnlyDictionary<string, bool> Snapshot();

    /// <summary>
    /// Forces a synchronous re-read of the feature_flags table. Called by the admin
    /// write path (plan 05) after every SetEnabledAsync so the new value is visible
    /// immediately (D-10). Failures preserve the prior snapshot — no zero-row stomp.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token; if signaled, the reload is aborted.</param>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
