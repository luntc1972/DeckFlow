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
    /// Classifies a transcript as keep or drop for the Content KB.
    /// </summary>
    /// <param name="transcript">Transcript text to classify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A classification result with a keep/drop verdict and reason.</returns>
    Task<ClassificationResult> ClassifyAsync(string transcript, CancellationToken cancellationToken = default)
        => Task.FromException<ClassificationResult>(
            new NotSupportedException("Classifier requires the subscription LLM CLI provider."));

    /// <summary>
    /// Extracts key clips from a transcript.
    /// </summary>
    /// <param name="transcript">Transcript text to scan for key clips.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A clips result with token usage from the completion.</returns>
    Task<ClipsResult> ExtractClipsAsync(string transcript, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the summary, key clips, and candidate tags from a transcript in one call.
    /// </summary>
    /// <param name="transcript">Transcript text to distill.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A combined extraction result with token usage from the completion.</returns>
    async Task<CombinedExtractionResult> ExtractCombinedAsync(string transcript, CancellationToken cancellationToken = default)
    {
        var summary = await SummarizeAsync(transcript, cancellationToken).ConfigureAwait(false);
        var clips = await ExtractClipsAsync(transcript, cancellationToken).ConfigureAwait(false);
        var tags = await InferTagsAsync(transcript, cancellationToken).ConfigureAwait(false);

        return new CombinedExtractionResult(
            summary.Summary,
            clips.Clips,
            tags.Archetype,
            tags.Bracket,
            tags.CardCategory,
            new TokenUsage(
                summary.Usage.InputTokens + clips.Usage.InputTokens + tags.Usage.InputTokens,
                summary.Usage.OutputTokens + clips.Usage.OutputTokens + tags.Usage.OutputTokens));
    }

    /// <summary>
    /// Infers controlled-vocabulary candidate tags from a transcript.
    /// </summary>
    /// <param name="transcript">Transcript text to classify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tags result with token usage from the completion.</returns>
    Task<TagsResult> InferTagsAsync(string transcript, CancellationToken cancellationToken = default);
}
