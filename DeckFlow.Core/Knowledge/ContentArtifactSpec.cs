using System.Text.Json;

namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Canonical Content KB artifact file-format contract and tag serialization helpers.
/// </summary>
public static class ContentArtifactSpec
{
    /// <summary>
    /// Markdown artifact format expected by the Content KB emitter and renderer phases.
    /// </summary>
    public const string ArtifactFileFormat = """
        ---
        source: "The Command Zone"
        title: "cEDH Tier List 2025 Edition"
        url: "https://www.youtube.com/watch?v=XXXXXXXXXXX"
        video_id: "XXXXXXXXXXX"
        tags:
          archetype: ["combo", "control"]
          bracket: ["cEDH", "Optimized"]
          card_category: ["win-cons", "counter"]
        generated_utc: "2026-05-26T18:00:00Z"
        ---

        ## Summary

        [200 words or fewer. Plain prose, no sub-headers, paste-ready, and able to stand alone without the clips section.]

        ## Key Clips

        - **[02:14]** [timestamped excerpt - 1-3 sentences]
        - **[08:47]** [timestamped excerpt]
        - **[41:22]** [3-8 total clips]

        ## Tags

        **Archetypes/Strategy:** combo, control
        **Format/Bracket:** cEDH, Optimized
        **Card Categories:** win-cons, counter
        """;

    /// <summary>
    /// Serializes a tag list to the locked JSON-array representation.
    /// </summary>
    /// <param name="tags">Tag list to serialize.</param>
    /// <returns>JSON array text, with empty lists serialized as <c>[]</c>.</returns>
    public static string SerializeTags(IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return JsonSerializer.Serialize(tags);
    }

    /// <summary>
    /// Deserializes a locked JSON-array tag representation.
    /// </summary>
    /// <param name="serializedTags">JSON array text, or <see langword="null"/> when no tags were stored.</param>
    /// <returns>A non-null tag list, with null or empty input returning an empty list.</returns>
    public static IReadOnlyList<string> DeserializeTags(string? serializedTags)
    {
        if (string.IsNullOrWhiteSpace(serializedTags))
        {
            return Array.Empty<string>();
        }

        return JsonSerializer.Deserialize<string[]>(serializedTags) ?? Array.Empty<string>();
    }
}

/// <summary>
/// Machine-parseable metadata emitted into Content KB artifact front matter.
/// </summary>
public sealed record ContentArtifactMetadata
{
    /// <summary>Source discriminator or display value written to artifact front matter.</summary>
    public required string Source { get; init; }

    /// <summary>Artifact title shown to readers and prompt consumers.</summary>
    public required string Title { get; init; }

    /// <summary>Canonical URL for the source content item.</summary>
    public required string Url { get; init; }

    /// <summary>YouTube video identifier, or <see langword="null"/> for RSS podcast artifacts.</summary>
    public string? YoutubeVideoId { get; init; }

    /// <summary>RSS item GUID, or <see langword="null"/> for YouTube artifacts.</summary>
    public string? RssGuid { get; init; }

    /// <summary>Allowlisted archetype tags serialized into artifact front matter.</summary>
    public required IReadOnlyList<string> ArchetypeTags { get; init; }

    /// <summary>Allowlisted bracket tags serialized into artifact front matter.</summary>
    public required IReadOnlyList<string> BracketTags { get; init; }

    /// <summary>Allowlisted card category tags serialized into artifact front matter.</summary>
    public required IReadOnlyList<string> CardCategoryTags { get; init; }

    /// <summary>UTC timestamp when the artifact was generated.</summary>
    public required DateTimeOffset GeneratedUtc { get; init; }
}

/// <summary>
/// Slim Content KB site-index row intended for browse and filter surfaces.
/// </summary>
public sealed record ContentSiteIndexRow
{
    /// <summary>Surrogate identifier for the site-index row.</summary>
    public required long Id { get; init; }

    /// <summary>Source name or discriminator shown in the site index.</summary>
    public required string Source { get; init; }

    /// <summary>Artifact title shown in the site index.</summary>
    public required string Title { get; init; }

    /// <summary>Canonical URL for the indexed content item.</summary>
    public required string VideoUrl { get; init; }

    /// <summary>
    /// RELATIVE artifact path in <c>content-kb/{source-slug}/{youtube_video_id}.md</c> or
    /// <c>content-kb/{source-slug}/{rss_guid}.md</c> form. Later phases resolve it against
    /// <c>MTG_DATA_DIR</c>; slugs and natural keys must be sanitized before becoming paths,
    /// and stores reject rooted or <c>..</c> traversal paths.
    /// </summary>
    public required string ArtifactPath { get; init; }

    /// <summary>UTC publication timestamp, or <see langword="null"/> when unavailable.</summary>
    public DateTimeOffset? PublishedUtc { get; init; }

    /// <summary>UTC timestamp when the index row was generated.</summary>
    public required DateTimeOffset IndexedUtc { get; init; }

    /// <summary>Allowlisted archetype tags for filtering and display.</summary>
    public required IReadOnlyList<string> ArchetypeTags { get; init; }

    /// <summary>Allowlisted bracket tags for filtering and display.</summary>
    public required IReadOnlyList<string> BracketTags { get; init; }

    /// <summary>Allowlisted card category tags for filtering and display.</summary>
    public required IReadOnlyList<string> CardCategoryTags { get; init; }

    /// <summary>YouTube video identifier, or <see langword="null"/> for RSS podcast index rows.</summary>
    public string? YoutubeVideoId { get; init; }

    /// <summary>RSS item GUID, or <see langword="null"/> for YouTube index rows.</summary>
    public string? RssGuid { get; init; }
}
