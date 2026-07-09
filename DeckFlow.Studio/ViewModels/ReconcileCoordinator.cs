using DeckFlow.Studio.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Operator-action coordinator for the SYNC-11 reconcile dry-run (D-04), mirroring
/// <see cref="DirectPushCoordinator"/>'s constructor/optional-logger shape. Delegates entirely to
/// <see cref="IContentKbReconcileOrchestrator.RunDryRunAsync"/> — the orchestrator alone owns the
/// prod read, git tree walk, seed read, classification, and persistence (91-06) — so this
/// coordinator issues no destructive write of its own and does not read <c>sync.reconcile</c> (the
/// dry-run is flag-independent; only the removal Apply arriving in 91-08 is flag-gated). Also
/// exposes a thin pass-through to <see cref="IContentKbReconcileStore.GetOpenAsync"/> for the page
/// to render previously-persisted open discrepancies without re-running the dry-run.
/// </summary>
public sealed class ReconcileCoordinator
{
    /// <summary>The scope tag used when the operator has not specified one — a whole-catalog run.</summary>
    public const string FullScopeTag = "full";

    private readonly IContentKbReconcileOrchestrator _orchestrator;
    private readonly IContentKbReconcileStore _store;
    private readonly ILogger<ReconcileCoordinator> _logger;

    /// <summary>Creates the coordinator over the reconcile orchestrator, the local discrepancy store, and an optional logger.</summary>
    public ReconcileCoordinator(
        IContentKbReconcileOrchestrator orchestrator,
        IContentKbReconcileStore store,
        ILogger<ReconcileCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(orchestrator);
        ArgumentNullException.ThrowIfNull(store);
        _orchestrator = orchestrator;
        _store = store;
        // Optional logger (house convention, e.g. DirectPushCoordinator): the default keeps every
        // existing construction site + test compiling.
        _logger = logger ?? NullLogger<ReconcileCoordinator>.Instance;
    }

    /// <summary>
    /// Runs one read-only reconcile dry-run pass for <paramref name="scopeTag"/> and returns the
    /// orchestrator's <see cref="ReconcileDryRunResult"/> unchanged — including
    /// <see cref="ReconcileDryRunResult.SeedAvailable"/>, surfaced intact so the page can render the
    /// "seed unavailable" notice instead of reading an empty seed-drift group as "no drift"
    /// (T-91-28). Performs no visibility change and no prod DDL; the underlying orchestrator issues
    /// only prod reads, a git-tree walk, and a local-store persist (91-06).
    /// </summary>
    /// <param name="scopeTag">What this run examines; defaults to <see cref="FullScopeTag"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ReconcileDryRunResult> RunDryRunAsync(
        string scopeTag = FullScopeTag,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting reconcile dry-run for scope {ScopeTag}.", scopeTag);
        return _orchestrator.RunDryRunAsync(scopeTag, cancellationToken);
    }

    /// <summary>
    /// Pass-through to <see cref="IContentKbReconcileStore.GetOpenAsync"/> — every currently-open
    /// (unresolved) persisted discrepancy, optionally filtered to one scope tag.
    /// </summary>
    /// <param name="scopeTag">When non-null, restricts results to this scope tag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<StoredReconcileDiscrepancy>> GetOpenDiscrepanciesAsync(
        string? scopeTag = null,
        CancellationToken cancellationToken = default)
        => _store.GetOpenAsync(scopeTag, cancellationToken);
}
