namespace DeckFlow.Core.Integration;

/// <summary>
/// Pure Whisper transcription result returned to the harvest verb for persistence decisions.
/// </summary>
public sealed record WhisperTranscriptionResult
{
    /// <summary>
    /// Final transcription outcome.
    /// </summary>
    public required TranscriptOutcome Outcome { get; init; }

    /// <summary>
    /// Total Whisper seconds billed for successful transcription.
    /// </summary>
    public int SecondsBilled { get; init; }

    /// <summary>
    /// Whisper cost in USD for successful transcription.
    /// </summary>
    public decimal CostUsd { get; init; }

    /// <summary>
    /// Verb-supplied month key echoed back for ledger consistency.
    /// </summary>
    public required string MonthKey { get; init; }

    /// <summary>
    /// Concatenated transcript body for successful transcription.
    /// </summary>
    public string? Body { get; init; }

    /// <summary>
    /// Failure reason when transcription could not complete.
    /// </summary>
    public string? FailureReason { get; init; }
}
