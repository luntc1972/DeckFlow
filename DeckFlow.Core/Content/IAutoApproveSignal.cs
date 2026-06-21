namespace DeckFlow.Core.Content;

/// <summary>
/// Swappable seam for the "is this distill good enough to auto-approve" decision (D-02).
/// </summary>
/// <remarks>
/// Phase 59 ships the simplest defensible heuristic — clip count (D-01) — behind this
/// interface so a future composite signal (clip count + tag coverage + summary completeness,
/// gated on the KBVAL A/B harness) can replace only the implementation without reworking the
/// auto-approve plumbing in the Studio host (Plan 02/03).
/// </remarks>
public interface IAutoApproveSignal
{
    /// <summary>
    /// Decides whether a distilled video should be auto-approved.
    /// </summary>
    /// <param name="clipCount">The number of clips the distill produced for the video.</param>
    /// <param name="cutoff">The operator-set cutoff at or above which a distill auto-approves.</param>
    /// <returns><c>true</c> when the distill should auto-approve; otherwise <c>false</c> (stays in review).</returns>
    bool ShouldAutoApprove(int clipCount, int cutoff);
}
