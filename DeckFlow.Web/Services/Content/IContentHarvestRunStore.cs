using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Content;

/// <summary>
/// Persists local Content KB harvest run summaries.
/// </summary>
public interface IContentHarvestRunStore
{
    /// <summary>
    /// Ensures the content harvest run schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a new content harvest run and returns its surrogate identifier.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inserted run identifier.</returns>
    Task<long> StartRunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes an existing content harvest run with summary counts and spend.
    /// </summary>
    /// <param name="runId">Identifier of the run to complete.</param>
    /// <param name="sourcesProcessed">Number of sources processed.</param>
    /// <param name="videosProcessed">Number of content items processed.</param>
    /// <param name="transcriptsFetched">Number of transcripts fetched or generated.</param>
    /// <param name="whisperCalls">Number of Whisper calls made.</param>
    /// <param name="spendUsd">Total USD spend for the run.</param>
    /// <param name="abortedReason">Reason the run aborted, or <see langword="null"/> for normal completion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CompleteRunAsync(
        long runId,
        int sourcesProcessed,
        int videosProcessed,
        int transcriptsFetched,
        int whisperCalls,
        decimal spendUsd,
        string? abortedReason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a content harvest run by surrogate identifier.
    /// </summary>
    /// <param name="runId">Run identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The run when found; otherwise <see langword="null"/>.</returns>
    Task<ContentHarvestRun?> GetRunAsync(long runId, CancellationToken cancellationToken = default);
}
