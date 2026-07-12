using DeckFlow.Core.Models;

namespace DeckFlow.Core.Knowledge.MeasuredStyleExtraction;

/// <summary>
/// Pure in-memory deck sample contract consumed by measured-style extraction helpers.
/// </summary>
public sealed record CreatorDeckSample
{
    /// <summary>Stable host-supplied deck identifier.</summary>
    public required string DeckId { get; init; }

    /// <summary>Deck entries already loaded by the host tier.</summary>
    public required IReadOnlyList<DeckEntry> Entries { get; init; }

    /// <summary>Cheap total-card field used for early oversize filtering.</summary>
    public required int CardCount { get; init; }

    /// <summary>Optional parent folder id captured by the host tier.</summary>
    public int? FolderId { get; init; }

    /// <summary>Optional parent folder name captured by the host tier.</summary>
    public string? FolderName { get; init; }

    /// <summary>Folder-derived contribution weight for this sample.</summary>
    public double FolderWeight { get; init; } = 1.0;

    /// <summary>Host- or extractor-supplied confidence marker for downstream interpretation.</summary>
    public required string ConfidenceMarker { get; init; }
}
