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
    /// <param name="videoIds">Optional targeted video identifiers that override broad source enumeration.</param>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured distill counts and status information.</returns>
    Task<DistillResult> DistillAsync(
        int limit,
        bool dryRun,
        bool isSubscriptionProvider,
        IReadOnlyList<string>? videoIds = null,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);
}
