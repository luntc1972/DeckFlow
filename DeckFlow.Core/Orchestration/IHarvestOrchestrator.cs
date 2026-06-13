namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Harvests transcripts for enabled or explicitly targeted Content KB videos.
/// </summary>
public interface IHarvestOrchestrator
{
    /// <summary>
    /// Harvests transcript content for the requested scope.
    /// </summary>
    /// <param name="limit">Maximum number of videos to process.</param>
    /// <param name="videoIds">Optional targeted video identifiers that override broad source enumeration.</param>
    /// <param name="sourceId">Optional targeted source identifier.</param>
    /// <param name="progress">Optional synchronous progress sink.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Structured harvest counts and status information.</returns>
    Task<HarvestResult> HarvestAsync(
        int limit,
        IReadOnlyList<string>? videoIds = null,
        long? sourceId = null,
        IOrchestratorProgress? progress = null,
        CancellationToken cancellationToken = default);
}
