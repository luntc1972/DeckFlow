namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Null-safe content-index export outcome contract. Callers must explicitly construct <see cref="Success"/>,
/// and exported rows are always initialized so hosts can serialize empty exports deterministically.
/// </summary>
public sealed record ContentIndexExportResult
{
    /// <summary>Gets the exported content-index rows, always initialized to a non-null list.</summary>
    public IReadOnlyList<ContentIndexExportRow> Rows { get; init; } = Array.Empty<ContentIndexExportRow>();

    /// <summary>Gets the exported row count.</summary>
    public int RowCount { get; init; }

    /// <summary>Gets whether the export operation completed successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>Gets an optional host-facing status or error message.</summary>
    public string? Message { get; init; }
}
