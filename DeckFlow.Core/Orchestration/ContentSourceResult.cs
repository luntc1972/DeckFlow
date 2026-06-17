namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Null-safe content-source mutation outcome contract. Callers must explicitly construct
/// <see cref="Success"/> and <see cref="Outcome"/> so CLI and Studio hosts can map the result deterministically.
/// </summary>
public sealed record ContentSourceResult
{
    /// <summary>Gets whether the source operation completed successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets the classified source-operation outcome for host exit-code and messaging decisions.</summary>
    public required ContentSourceOutcome Outcome { get; init; }

    /// <summary>Gets the computed source slug when available.</summary>
    public string? Slug { get; init; }

    /// <summary>Gets the inserted or targeted source identifier when available.</summary>
    public long? Id { get; init; }

    /// <summary>Gets an optional user-facing status or error message.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// Classifies the content-source result so the host can preserve its existing exit-code and operator-message mapping.
    /// </summary>
    public enum ContentSourceOutcome
    {
        /// <summary>A new source row was inserted.</summary>
        Added,

        /// <summary>The requested source already exists with the same canonical URL.</summary>
        AlreadyExistsSameUrl,

        /// <summary>The computed slug collides with a different source URL.</summary>
        SlugConflict,

        /// <summary>The requested source type is invalid.</summary>
        InvalidType,

        /// <summary>The operation failed for some other reason.</summary>
        Error,
    }
}
