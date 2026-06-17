namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Null-safe harvest outcome contract. Callers must explicitly construct <see cref="Success"/>,
/// and optional status text is carried separately from the count fields.
/// </summary>
public sealed record HarvestResult
{
    /// <summary>Gets whether the harvest operation completed successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets the number of transcripts fetched from captions.</summary>
    public int Captions { get; init; }

    /// <summary>Gets the number of transcripts generated via Whisper fallback.</summary>
    public int Whisper { get; init; }

    /// <summary>Gets the number of videos skipped because no usable captions were available.</summary>
    public int SkippedNoCaptions { get; init; }

    /// <summary>Gets the Whisper fallback ratio across successful transcript fetches.</summary>
    public double WhisperFallbackRatio { get; init; }

    /// <summary>Gets an optional status or abort message for the host to display.</summary>
    public string? Message { get; init; }
}
