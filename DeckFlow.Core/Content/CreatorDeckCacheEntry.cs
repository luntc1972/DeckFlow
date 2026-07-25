using DeckFlow.Core.Models;

namespace DeckFlow.Core.Content;

/// <summary>
/// Cached deck payload for a creator-scoped Archidekt crawl.
/// </summary>
public sealed record CreatorDeckCacheEntry
{
    /// <summary>Creator slug that owns the cached deck.</summary>
    public required string CreatorSlug { get; init; }

    /// <summary>Archidekt deck identifier.</summary>
    public required string DeckId { get; init; }

    /// <summary>Canonical content hash for freshness checks.</summary>
    public required string ContentHash { get; init; }

    /// <summary>Optional Archidekt parent-folder identifier.</summary>
    public int? FolderId { get; init; }

    /// <summary>Optional Archidekt parent-folder display name.</summary>
    public string? FolderName { get; init; }

    /// <summary>Total imported deck size used by upstream filtering.</summary>
    public required int Size { get; init; }

    /// <summary>Confidence marker assigned by the crawler.</summary>
    public required string ConfidenceMarker { get; init; }

    /// <summary>Full imported entries persisted for warm-cache sample rebuilds.</summary>
    public required IReadOnlyList<DeckEntry> Entries { get; init; }

    /// <summary>Timestamp when the cache row was last written.</summary>
    public required DateTimeOffset CachedUtc { get; init; }
}
