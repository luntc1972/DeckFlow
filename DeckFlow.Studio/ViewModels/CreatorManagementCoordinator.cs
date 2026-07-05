using DeckFlow.Core.Content;
using DeckFlow.Core.Orchestration;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Creator management + video blocking (SRC-02, HSEL-02), extracted from the <c>Harvest</c> page
/// code-behind (Phase 82 SRP split). The page keeps all UI state (dropdown selection, confirm-block
/// focus, in-flight guards) and calls into this coordinator for the store/orchestrator I/O.
/// Behavior is identical to the prior inline implementation.
/// </summary>
public sealed class CreatorManagementCoordinator
{
    private readonly ICreatorSourceStore _creatorStore;
    private readonly IContentMaintenanceOrchestrator _maintenanceOrchestrator;

    /// <summary>Creates the coordinator with the creator-source store and the content maintenance orchestrator.</summary>
    public CreatorManagementCoordinator(ICreatorSourceStore creatorStore, IContentMaintenanceOrchestrator maintenanceOrchestrator)
    {
        ArgumentNullException.ThrowIfNull(creatorStore);
        ArgumentNullException.ThrowIfNull(maintenanceOrchestrator);
        _creatorStore = creatorStore;
        _maintenanceOrchestrator = maintenanceOrchestrator;
    }

    /// <summary>
    /// Loads the saved creators for the browse dropdown (SRC-02). Failure is non-fatal — the
    /// paste-URL fallback always remains usable, so a load error just returns an empty list,
    /// matching the prior inline try/catch exactly.
    /// </summary>
    public async Task<List<CreatorSource>> LoadCreatorsAsync()
    {
        try
        {
            var creators = await _creatorStore.ListAsync();
            return creators.ToList();
        }
        catch (Exception)
        {
            // Why: dropdown is a convenience over the URL fallback; never block browse on a load error.
            return new List<CreatorSource>();
        }
    }

    /// <summary>
    /// Blocks a YouTube video from future harvest runs (HSEL-02). The caller owns the busy guard,
    /// cancellation token source, and badge refresh — this coordinator only relays the orchestrator
    /// call so it stays independently testable without a bUnit render.
    /// </summary>
    public async Task<ContentMaintenanceResult> BlockVideoAsync(
        string videoId,
        IOrchestratorProgress progress,
        CancellationToken cancellationToken)
        => await _maintenanceOrchestrator.BlockVideoAsync(videoId, reason: null, progress, cancellationToken);

    /// <summary>
    /// Un-blocks a previously blocked YouTube video (reveal/restore hidden videos). Thin relay to the
    /// maintenance orchestrator — the caller owns the busy guard, cancellation token, and badge refresh,
    /// mirroring <see cref="BlockVideoAsync"/> so it stays independently testable without a bUnit render.
    /// </summary>
    public async Task<ContentMaintenanceResult> UnblockVideoAsync(
        string videoId,
        IOrchestratorProgress? progress,
        CancellationToken cancellationToken)
        => await _maintenanceOrchestrator.UnblockVideoAsync(videoId, progress, cancellationToken);

    /// <summary>
    /// Links the curated creator identified by <paramref name="creatorRef"/> to the content source it
    /// produced during harvest (P87), stamping the canonical slug. Keyed by the exact normalized
    /// channel ref carried as harvest provenance, so it links the right creator or none — never the
    /// wrong one. A no-op when no creator matches the ref (e.g. a paste-URL harvest).
    /// </summary>
    /// <param name="creatorRef">The creator's channel ref (harvest provenance).</param>
    /// <param name="contentSourceId">The ensured <c>content_sources</c> row id.</param>
    /// <param name="canonicalSlug">The content source's persisted slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when a creator row was linked; otherwise <see langword="false"/>.</returns>
    public async Task<bool> LinkCreatorToSourceAsync(
        string creatorRef,
        long contentSourceId,
        string canonicalSlug,
        CancellationToken cancellationToken = default)
    {
        var creator = await _creatorStore.GetByNormalizedRefAsync(
            CreatorSourceStore.NormalizeChannelRef(creatorRef), cancellationToken).ConfigureAwait(false);
        if (creator is null)
        {
            return false;
        }

        await _creatorStore.LinkContentSourceAsync(creator.Id, contentSourceId, canonicalSlug, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
