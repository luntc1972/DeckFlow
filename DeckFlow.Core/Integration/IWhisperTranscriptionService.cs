namespace DeckFlow.Core.Integration;

/// <summary>
/// Transcribes downloaded audio with Whisper without persisting results.
/// </summary>
public interface IWhisperTranscriptionService
{
    /// <summary>
    /// Transcribes the supplied audio handle or returns a status result when gated or failed.
    /// </summary>
    /// <param name="audio">Temporary downloaded audio handle.</param>
    /// <param name="knownDuration">Authoritative video duration from the channel lister.</param>
    /// <param name="monthKey">Verb-supplied month key echoed back on <see cref="WhisperTranscriptionResult.MonthKey"/> for cap check consistency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A pure transcription result with billing metadata on success.</returns>
    Task<WhisperTranscriptionResult> TranscribeAsync(
        AudioDownloadResult audio,
        TimeSpan? knownDuration,
        string monthKey,
        CancellationToken ct = default);
}
