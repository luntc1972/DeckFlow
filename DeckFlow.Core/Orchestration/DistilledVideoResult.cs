namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Public per-video distilled outcome: the natural key plus the clip count that the auto-approve
/// signal (D-01) reads. The Studio host (Plan 03) feeds <see cref="NaturalKeyType"/> /
/// <see cref="NaturalKeyValue"/> straight into <c>SetApprovalStatusAsync</c>.
/// </summary>
/// <remarks>
/// Named <c>DistilledVideoResult</c> — deliberately NOT <c>DistilledVideoOutcome</c> — to avoid a
/// near-homonym collision with the private orchestrator accumulator <c>DistillVideoOutcome</c>
/// (Codex MEDIUM). The natural key carries whichever key the distill ran on: YouTube
/// (<c>youtube_video_id</c>) OR podcast (<c>rss_guid</c>), never a YouTube-only id.
/// </remarks>
public sealed record DistilledVideoResult
{
    /// <summary>Gets the natural-key type (e.g. <c>youtube</c> or <c>podcast</c>).</summary>
    public required string NaturalKeyType { get; init; }

    /// <summary>Gets the natural-key value (YouTube video id or podcast RSS GUID).</summary>
    public required string NaturalKeyValue { get; init; }

    /// <summary>Gets the number of clips the distill produced — the auto-approve signal source (D-01).</summary>
    public required int ClipCount { get; init; }
}
