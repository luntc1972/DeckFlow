namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Distills harvested Content KB transcripts into summaries, clips, tags, and site-index rows.
/// </summary>
public interface IDistillOrchestrator
{
    /// <summary>
    /// Distills transcript-ready videos for the requested scope.
    /// </summary>
    /// <param name="limit">Maximum number of videos to process.</param>
    /// <param name="dryRun">Whether to project work without writing distill output.</param>
    /// <param name="isSubscriptionProvider">Whether the active LLM provider is subscription-backed instead of metered.</param>
    /// <param name="redistill">
    /// When <see langword="true"/>, bypasses the already-distilled skip for videos that are in the
    /// targeted <paramref name="videoIds"/> set and clears their prior distill output before
    /// re-distilling. Has no effect on videos not in <paramref name="videoIds"/>; distilled videos
    /// outside the targeted set are still skipped. Defaults to <see langword="false"/>.
    /// </param>
    /// <param name="videoIds">Optional targeted video identifiers that override broad source enumeration.</param>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured distill counts and status information.</returns>
    Task<DistillResult> DistillAsync(
        int limit,
        bool dryRun,
        bool isSubscriptionProvider,
        bool redistill = false,
        IReadOnlyList<string>? videoIds = null,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists harvested-but-not-yet-distilled videos across all enabled sources so a host can
    /// offer a DB-backed distill selection that survives an app/circuit restart.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The flat, de-duplicated (by YouTube video id) list of pending-distill videos.</returns>
    // Why: default-throw keeps existing test doubles that only stub DistillAsync compiling.
    Task<IReadOnlyList<PendingDistillVideo>> ListPendingDistillAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{GetType().Name} does not implement {nameof(ListPendingDistillAsync)}.");
}
