namespace DeckFlow.Core.Content;

/// <summary>
/// Curated creator-profile source configuration used to crawl measured style inputs.
/// </summary>
public sealed record CreatorProfileSource
{
    /// <summary>URL-safe creator slug produced from the source name.</summary>
    public required string Slug { get; init; }

    /// <summary>Platform identifier associated with the creator source.</summary>
    public required string Platform { get; init; }

    /// <summary>Archidekt profile username used for owner-scoped deck crawling.</summary>
    public required string ProfileUsername { get; init; }

    /// <summary>Optional canonical creator profile URL.</summary>
    public string? ProfileUrl { get; init; }

    /// <summary>Curated Archidekt parent-folder weights keyed by <c>parentFolderId</c>.</summary>
    public IReadOnlyDictionary<int, double> FolderWeights { get; init; } = new Dictionary<int, double>();

    /// <summary>Whether folder weights are uncurated and should default to full weight.</summary>
    public bool WeightsUncurated { get; init; }

    /// <summary>UTC timestamp of the last successful creator crawl, or <see langword="null"/> when uncrawled.</summary>
    public DateTimeOffset? LastCrawledUtc { get; init; }

    /// <summary>UTC timestamp indicating when this source mapping was last updated.</summary>
    public required DateTimeOffset UpdatedUtc { get; init; }
}
