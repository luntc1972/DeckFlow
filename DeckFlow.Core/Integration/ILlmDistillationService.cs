using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Integration;

/// <summary>
/// Distills transcript text into pure summary, clip, and tag results without persisting them.
/// </summary>
public interface ILlmDistillationService
{
    /// <summary>
    /// Creates a concise strategy summary from a transcript.
    /// </summary>
    /// <param name="transcript">Transcript text to summarize.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A summary result with token usage from the completion.</returns>
    Task<SummaryResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts key clips from a transcript.
    /// </summary>
    /// <param name="transcript">Transcript text to scan for key clips.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A clips result with token usage from the completion.</returns>
    Task<ClipsResult> ExtractClipsAsync(string transcript, CancellationToken cancellationToken = default);

    /// <summary>
    /// Infers controlled-vocabulary candidate tags from a transcript.
    /// </summary>
    /// <param name="transcript">Transcript text to classify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tags result with token usage from the completion.</returns>
    Task<TagsResult> InferTagsAsync(string transcript, CancellationToken cancellationToken = default);
}
