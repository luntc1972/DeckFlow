using DeckFlow.Core.Content;
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Orchestration;

/// <summary>
/// Public content-index seed row contract shared by hosts. The property set and declaration order
/// intentionally mirror the existing CLI shape so JSON export remains byte-stable.
/// </summary>
public sealed record ContentIndexExportRow
{
    /// <summary>Gets the natural key type.</summary>
    public required string NaturalKeyType { get; init; }

    /// <summary>Gets the natural key value.</summary>
    public required string NaturalKeyValue { get; init; }

    /// <summary>Gets the source display name.</summary>
    public required string Source { get; init; }

    /// <summary>Gets the exported content title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the canonical video URL.</summary>
    public required string VideoUrl { get; init; }

    /// <summary>Gets the relative artifact path.</summary>
    public required string ArtifactPath { get; init; }

    /// <summary>Gets the publication timestamp when known.</summary>
    public DateTimeOffset? PublishedUtc { get; init; }

    /// <summary>Gets the UTC timestamp when the row was indexed.</summary>
    public required DateTimeOffset IndexedUtc { get; init; }

    /// <summary>Gets the archetype tags.</summary>
    public required IReadOnlyList<string> ArchetypeTags { get; init; }

    /// <summary>Gets the bracket tags.</summary>
    public required IReadOnlyList<string> BracketTags { get; init; }

    /// <summary>Gets the card-category tags.</summary>
    public required IReadOnlyList<string> CardCategoryTags { get; init; }

    /// <summary>
    /// Creates an export row from one published content site-index row.
    /// </summary>
    /// <param name="row">Published content site-index row.</param>
    /// <returns>The export-row projection.</returns>
    public static ContentIndexExportRow From(ContentSiteIndexRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var (naturalKeyType, naturalKeyValue) = GetNaturalKey(row);

        return new ContentIndexExportRow
        {
            NaturalKeyType = naturalKeyType,
            NaturalKeyValue = naturalKeyValue,
            Source = row.Source,
            Title = row.Title,
            VideoUrl = row.VideoUrl,
            ArtifactPath = row.ArtifactPath,
            PublishedUtc = row.PublishedUtc,
            IndexedUtc = row.IndexedUtc,
            ArchetypeTags = row.ArchetypeTags,
            BracketTags = row.BracketTags,
            CardCategoryTags = row.CardCategoryTags,
        };
    }

    private static (string NaturalKeyType, string NaturalKeyValue) GetNaturalKey(ContentSiteIndexRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.YoutubeVideoId))
        {
            return (ContentSourceType.Youtube, row.YoutubeVideoId);
        }

        if (!string.IsNullOrWhiteSpace(row.RssGuid))
        {
            return (ContentSourceType.Podcast, row.RssGuid);
        }

        throw new InvalidOperationException($"Content site-index row {row.Id} has no natural key.");
    }
}
