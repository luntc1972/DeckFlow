namespace DeckFlow.Core.Content;

/// <summary>
/// Clip-count implementation of <see cref="IAutoApproveSignal"/> — the Phase 59 auto-approve
/// heuristic (D-01). A distill auto-approves when its clip count meets or exceeds the
/// operator-set cutoff; lower-clip distills stay in the review queue.
/// </summary>
/// <remarks>
/// Intentionally the simplest defensible signal derived from existing distill output (D-01);
/// a richer composite signal is deferred until the KBVAL A/B harness exists (D-02). The schema,
/// provider, and model are unchanged — no model-returned confidence field is consulted (SC4).
/// </remarks>
public sealed class ClipCountAutoApproveSignal : IAutoApproveSignal
{
    /// <summary>
    /// The default auto-approve cutoff: 5+ clips approve, 3-4 hold (D-03). The Studio settings
    /// store (Plan 02) seeds from this single source of truth.
    /// </summary>
    public const int DefaultCutoff = 5;

    /// <summary>
    /// Returns <c>true</c> when <paramref name="clipCount"/> meets or exceeds <paramref name="cutoff"/>.
    /// </summary>
    /// <param name="clipCount">The number of clips the distill produced for the video.</param>
    /// <param name="cutoff">The operator-set cutoff at or above which a distill auto-approves.</param>
    /// <returns><c>true</c> when the distill should auto-approve; otherwise <c>false</c>.</returns>
    public bool ShouldAutoApprove(int clipCount, int cutoff) => clipCount >= cutoff;
}
