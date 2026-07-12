using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;

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
        content_type: "youtube"
        stated_rules: [{"category":"ramp","metric":"lands","value":37,"value_min":null,"value_max":null,"comparator":"gte","condition":"control","clip_ts":134,"source_clip":"Play at least 37 lands in control shells.","confidence":0.91,"card_reference":null,"card_grounded":null,"video_date":"2026-05-26T12:00:00+00:00"}]
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
    /// Serializes stated rules to the locked JSON-array representation used in artifact front matter.
    /// </summary>
    /// <param name="rules">Stated rules to serialize.</param>
    /// <returns>JSON array text, with empty lists serialized as <c>[]</c>.</returns>
    public static string SerializeStatedRules(IReadOnlyList<StatedRuleCandidate> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        return JsonSerializer.Serialize(rules.Select(
            rule => new StatedRuleArtifactEntry(
                rule.Category,
                rule.Metric,
                rule.Value,
                rule.ValueMin,
                rule.ValueMax,
                rule.Comparator,
                rule.Condition,
                rule.ClipTimestampSeconds,
                rule.SourceClip,
                rule.Confidence,
                rule.CardReference,
                rule.CardGrounded,
                rule.VideoDateUtc)));
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
    /// <summary>
    /// Initializes metadata defaults while preserving required-member compatibility for existing callers.
    /// </summary>
    [SetsRequiredMembers]
    public ContentArtifactMetadata()
    {
        Source = string.Empty;
        Title = string.Empty;
        Url = string.Empty;
        ContentType = string.Empty;
        ArchetypeTags = Array.Empty<string>();
        BracketTags = Array.Empty<string>();
        CardCategoryTags = Array.Empty<string>();
        StatedRules = Array.Empty<StatedRuleCandidate>();
    }

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

    /// <summary>Opaque content type discriminator serialized into artifact front matter.</summary>
    public required string ContentType { get; init; }

    /// <summary>Allowlisted archetype tags serialized into artifact front matter.</summary>
    public required IReadOnlyList<string> ArchetypeTags { get; init; }

    /// <summary>Allowlisted bracket tags serialized into artifact front matter.</summary>
    public required IReadOnlyList<string> BracketTags { get; init; }

    /// <summary>Allowlisted card category tags serialized into artifact front matter.</summary>
    public required IReadOnlyList<string> CardCategoryTags { get; init; }

    /// <summary>UTC timestamp when the artifact was generated.</summary>
    public required DateTimeOffset GeneratedUtc { get; init; }

    /// <summary>Structured stated rules serialized into artifact front matter.</summary>
    public IReadOnlyList<StatedRuleCandidate> StatedRules { get; init; } = Array.Empty<StatedRuleCandidate>();
}

internal sealed record StatedRuleArtifactEntry(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("metric")] string Metric,
    [property: JsonPropertyName("value")] double? Value,
    [property: JsonPropertyName("value_min")] double? ValueMin,
    [property: JsonPropertyName("value_max")] double? ValueMax,
    [property: JsonPropertyName("comparator")] string Comparator,
    [property: JsonPropertyName("condition")] string? Condition,
    [property: JsonPropertyName("clip_ts")] int? ClipTimestampSeconds,
    [property: JsonPropertyName("source_clip")] string SourceClip,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("card_reference")] string? CardReference,
    [property: JsonPropertyName("card_grounded")] bool? CardGrounded,
    [property: JsonPropertyName("video_date")] DateTimeOffset VideoDateUtc);

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

    /// <summary>UTC instant the operator pushed this version to deckflow.gg production, or <see langword="null"/> if never pushed. Local fact, written ONLY by StampPushedToProdAsync; never by an upsert.</summary>
    public DateTimeOffset? PushedToProdUtc { get; init; }

    /// <summary>UTC timestamp when the index row was generated.</summary>
    public required DateTimeOffset IndexedUtc { get; init; }

    /// <summary>Whether this row is published to the public Content KB surface; <see langword="false"/> (hidden) by default until an admin curates it visible.</summary>
    public bool IsVisible { get; init; }

    /// <summary>Whether this entry is deliberately hidden from the public Content KB surface; <see langword="true"/> implies not visible.</summary>
    public bool IsHidden { get; init; }

    /// <summary>Whether this artifact can fill evergreen advice slots for any deck analysis prompt.</summary>
    public bool IsEvergreen { get; init; }

    /// <summary>Approval workflow status: pending, approved, or rejected.</summary>
    public string ApprovalStatus { get; init; } = "pending";

    /// <summary>
    /// Lowercase hex SHA-256 of the LF-normalized, UTF-8-decoded artifact body (post-front-matter),
    /// computed by <c>ContentSiteIndexContentSignature.ComputeBodySha256</c>.
    /// <see langword="null"/> for legacy rows that predate the content-hash backfill.
    /// </summary>
    public string? BodySha256 { get; init; }

    /// <summary>
    /// UTC instant this row's content was pushed to prod and is durably awaiting a deploy
    /// hash-confirm before <c>PushedToProdUtc</c> stamps and <c>IsVisible</c> flips (D-10).
    /// <see langword="null"/> means the row is not currently mid-flight. Set by
    /// <c>SetAwaitingConfirmAsync</c>, cleared by <c>ClearAwaitingConfirmAsync</c>; never written by
    /// an upsert. Local fact only — never mirrored to prod's content-only upsert column list.
    /// </summary>
    public DateTimeOffset? AwaitingConfirmUtc { get; init; }

    /// <summary>
    /// Row-level seed-ownership marker (SYNC-17): <see langword="true"/> when this row is
    /// seed-managed (its natural key currently appears in <c>index-seed.json</c>),
    /// <see langword="false"/> when it is classified prod-owned, and <see langword="null"/> when
    /// unclassified. <see langword="null"/> is distinct from <see langword="false"/> so the D-02
    /// backfill can be re-run safely without clobbering an existing classification. Seed-driven
    /// removal (SYNC-12) applies ONLY to rows where this is <see langword="true"/>.
    /// </summary>
    public bool? SeedManaged { get; init; }

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

    /// <summary>Stable identifier used for pinning/matching — the row's natural key (YouTube video id or RSS guid).</summary>
    public string? PinId => YoutubeVideoId ?? RssGuid;
}
