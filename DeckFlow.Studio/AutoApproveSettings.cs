using DeckFlow.Core.Content;

namespace DeckFlow.Studio;

/// <summary>
/// Operator-set auto-approve configuration: whether high-clip distills skip the review
/// queue (D-04) and the clip cutoff at or above which a distill auto-approves (D-03).
/// Unlike <see cref="SessionCapOverride"/> these settings persist across Studio restarts
/// (D-07) — see <see cref="AutoApproveSettingsStore"/>.
/// </summary>
/// <param name="Enabled">When <see langword="true"/>, distills at or above <paramref name="Cutoff"/> auto-approve.</param>
/// <param name="Cutoff">The clip count at or above which a distill auto-approves.</param>
public sealed record AutoApproveSettings(bool Enabled, int Cutoff)
{
    /// <summary>
    /// The shipped defaults: auto-approve ON (D-06) at cutoff
    /// <see cref="ClipCountAutoApproveSignal.DefaultCutoff"/> (5, D-03) — the single source of
    /// truth shared with the Core signal.
    /// </summary>
    public static AutoApproveSettings Default => new(true, ClipCountAutoApproveSignal.DefaultCutoff);
}
