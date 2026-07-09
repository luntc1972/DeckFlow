using DeckFlow.Core.Content;

namespace DeckFlow.Studio.Services;

/// <summary>
/// I/O orchestrator contract for the SYNC-11 reconcile dry-run (D-04). Composes the three inputs
/// (prod read, git content-kb tree walk, availability-aware seed read), drives the pure
/// <see cref="ContentKbReconcileClassifier"/>, persists scope-tagged results to the local
/// <see cref="IContentKbReconcileStore"/>, and writes the D-06 human-readable report. Read-only
/// against production and the git tree — issues no DDL, no visibility write, no destructive action.
/// </summary>
public interface IContentKbReconcileOrchestrator
{
    /// <summary>
    /// Runs one complete reconcile dry-run pass: reads prod exactly once, enumerates
    /// <c>content-kb/**/*.md</c> in the operator's git checkout, reads <c>index-seed.json</c> via
    /// the availability-aware <see cref="SeedIndexFileReader.Read"/>, classifies via
    /// <see cref="ContentKbReconcileClassifier.Classify"/>, persists the result under
    /// <paramref name="scopeTag"/>, and writes the D-06 report.
    /// </summary>
    /// <param name="scopeTag">
    /// Identifies what this run examined (e.g. <c>"full"</c> for a whole-catalog run). Passed
    /// straight through to <see cref="IContentKbReconcileStore.PersistRunAsync"/> — resolution-by-
    /// absence is scoped to this tag only.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The dry-run result. <see cref="ReconcileDryRunResult.SeedAvailable"/> comes straight from
    /// <see cref="SeedIndexFileReader.Read"/> (never inferred from the discrepancy list), so a
    /// downstream Apply can independently refuse an unavailable seed.
    /// </returns>
    Task<ReconcileDryRunResult> RunDryRunAsync(string scopeTag, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of one <see cref="IContentKbReconcileOrchestrator.RunDryRunAsync"/> pass.
/// </summary>
/// <param name="SeedAvailable">
/// Whether <c>index-seed.json</c> was present and successfully parsed for this run, taken directly
/// from <see cref="SeedIndexReadResult.SeedAvailable"/>. <see langword="false"/> means seed-drift
/// detection was skipped entirely for this run (zero seed-drift discrepancies) — this is NOT the
/// same as "no drift found" and MUST be surfaced to the operator distinctly (T-91-26).
/// </param>
/// <param name="Discrepancies">Every discrepancy this run detected, across all four classes.</param>
public sealed record ReconcileDryRunResult(
    bool SeedAvailable,
    IReadOnlyList<ContentKbReconcileDiscrepancy> Discrepancies);
