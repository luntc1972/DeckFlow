using DeckFlow.Core.Content;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Auto-approve settings persistence + the shared post-distill auto-approve step (D-04/D-05/D-07/
/// D-09), extracted from the <c>Harvest</c> page code-behind (Phase 82 SRP split). The page keeps
/// the <c>AutoApproveSettings</c> field (bound in markup) and passes it in; this coordinator owns
/// the store round-trip and the immutable-record update rules. Behavior is identical to the prior
/// inline implementation.
/// </summary>
public sealed class AutoApproveSettingsCoordinator
{
    private readonly AutoApproveSettingsStore _store;
    private readonly IAutoApproveSignal _signal;
    private readonly IContentSiteIndexStore _indexStore;

    /// <summary>Creates the coordinator with the settings store, auto-approve signal, and site-index store.</summary>
    public AutoApproveSettingsCoordinator(AutoApproveSettingsStore store, IAutoApproveSignal signal, IContentSiteIndexStore indexStore)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(indexStore);
        _store = store;
        _signal = signal;
        _indexStore = indexStore;
    }

    /// <summary>Loads the persisted auto-approve settings (D-07).</summary>
    public AutoApproveSettings Load() => _store.Load();

    /// <summary>Persists the current auto-approve on/off + cutoff (D-04/D-05/D-07).</summary>
    public void Save(AutoApproveSettings settings) => _store.Save(settings);

    /// <summary>Applies the auto-approve on/off toggle change to an immutable settings record.</summary>
    public AutoApproveSettings ApplyEnabledChange(AutoApproveSettings current, bool enabled)
        => current with { Enabled = enabled };

    /// <summary>
    /// Applies a cutoff commit if <paramref name="rawValue"/> parses as an integer; returns
    /// <see langword="null"/> on a bad value so the caller can skip the save, matching the prior
    /// inline <c>if (int.TryParse(...))</c> gate exactly.
    /// </summary>
    public AutoApproveSettings? TryApplyCutoffChange(AutoApproveSettings current, string? rawValue)
        => int.TryParse(rawValue, out var cutoff) ? current with { Cutoff = cutoff } : null;

    /// <summary>
    /// Shared post-distill auto-approve step (D-09): when <paramref name="settings"/> is enabled,
    /// selects the distilled videos whose clip count is at or above the persisted cutoff (via
    /// <see cref="IAutoApproveSignal"/>) and batch-flips their <c>approval_status</c> to 'approved'
    /// (only approval_status is mutated). Returns the number of rows actually flipped (0 when
    /// disabled or none qualify).
    /// </summary>
    public async Task<int> ApplyAutoApproveAsync(DistillResult result, AutoApproveSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(settings);

        // Why: pure key selection (enabled + clip-count cutoff via the swappable signal) lives in
        // HarvestPlanner; this coordinator keeps the store write + cancellation. Empty selection => no call.
        var keys = HarvestPlanner.SelectAutoApproveKeys(
            result.DistilledVideos,
            settings.Enabled,
            settings.Cutoff,
            _signal);

        if (keys.Count == 0)
        {
            return 0;
        }

        return await _indexStore.SetApprovalStatusAsync(keys, "approved", cancellationToken);
    }
}
