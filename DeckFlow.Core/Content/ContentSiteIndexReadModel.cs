using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// Dapper materialization target for content-site-index read queries.
/// </summary>
public sealed class ContentSiteIndexReadModel
{
    /// <summary>
    /// Gets the row identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// Gets the content source label.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the content title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the source video URL.
    /// </summary>
    public required string VideoUrl { get; init; }

    /// <summary>
    /// Gets the relative artifact path.
    /// </summary>
    public required string ArtifactPath { get; init; }

    /// <summary>
    /// Gets the published timestamp, if any.
    /// </summary>
    public DateTimeOffset? PublishedUtc { get; init; }

    /// <summary>
    /// Gets the prod-push timestamp, if any.
    /// </summary>
    public DateTimeOffset? PushedToProdUtc { get; init; }

    /// <summary>
    /// Gets the indexed timestamp.
    /// </summary>
    public DateTimeOffset IndexedUtc { get; init; }

    /// <summary>
    /// Gets the serialized archetype tags payload.
    /// </summary>
    public required string ArchetypeTags { get; init; }

    /// <summary>
    /// Gets the serialized bracket tags payload.
    /// </summary>
    public required string BracketTags { get; init; }

    /// <summary>
    /// Gets the serialized card-category tags payload.
    /// </summary>
    public required string CardCategoryTags { get; init; }

    /// <summary>
    /// Gets the natural-key type discriminator.
    /// </summary>
    public required string NaturalKeyType { get; init; }

    /// <summary>
    /// Gets the natural-key value.
    /// </summary>
    public required string NaturalKeyValue { get; init; }

    /// <summary>
    /// Gets a value indicating whether the row is visible.
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// Gets a value indicating whether the row is hidden.
    /// </summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// Gets a value indicating whether the row is evergreen.
    /// </summary>
    public bool IsEvergreen { get; init; }

    /// <summary>
    /// Gets the approval status.
    /// </summary>
    public required string ApprovalStatus { get; init; }

    /// <summary>
    /// Gets the body hash, if any.
    /// </summary>
    public string? BodySha256 { get; init; }

    /// <summary>
    /// Gets the awaiting-confirm timestamp, if any.
    /// </summary>
    public DateTimeOffset? AwaitingConfirmUtc { get; init; }

    /// <summary>
    /// Gets the seed-managed marker, if any.
    /// </summary>
    public bool? SeedManaged { get; init; }
}

/// <summary>
/// Shared SELECT column list for content-site-index read queries.
/// </summary>
public static class ContentSiteIndexReadColumns
{
    /// <summary>
    /// Gets the canonical read SELECT column list in stable order.
    /// </summary>
    public const string SelectList = "id, source, title, video_url, artifact_path, published_utc, pushed_to_prod_utc, indexed_utc, archetype_tags, bracket_tags, card_category_tags, natural_key_type, natural_key_value, is_visible, is_hidden, is_evergreen, approval_status, body_sha256, awaiting_confirm_utc, seed_managed";
}

/// <summary>
/// Maps read-model rows into domain rows.
/// </summary>
public static class ContentSiteIndexRowMapper
{
    /// <summary>
    /// Maps a materialized read-model row into a <see cref="ContentSiteIndexRow"/>.
    /// </summary>
    /// <param name="row">The materialized row.</param>
    /// <returns>The mapped domain row.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="ContentSiteIndexReadModel.NaturalKeyType"/> is unknown.
    /// </exception>
    public static ContentSiteIndexRow ToRow(ContentSiteIndexReadModel row)
    {
        var naturalKeyType = row.NaturalKeyType;
        var naturalKeyValue = row.NaturalKeyValue;
        var youtubeVideoId = naturalKeyType == ContentSourceType.Youtube ? naturalKeyValue : null;
        var rssGuid = naturalKeyType == ContentSourceType.Podcast ? naturalKeyValue : null;

        if (youtubeVideoId is null && rssGuid is null)
        {
            throw new InvalidOperationException($"Unknown content_site_index.natural_key_type value '{naturalKeyType}'.");
        }

        return new ContentSiteIndexRow
        {
            Id = row.Id,
            Source = row.Source,
            Title = row.Title,
            VideoUrl = row.VideoUrl,
            ArtifactPath = row.ArtifactPath,
            PublishedUtc = row.PublishedUtc,
            PushedToProdUtc = row.PushedToProdUtc,
            IndexedUtc = row.IndexedUtc,
            ArchetypeTags = ContentArtifactSpec.DeserializeTags(row.ArchetypeTags),
            BracketTags = ContentArtifactSpec.DeserializeTags(row.BracketTags),
            CardCategoryTags = ContentArtifactSpec.DeserializeTags(row.CardCategoryTags),
            YoutubeVideoId = youtubeVideoId,
            RssGuid = rssGuid,
            IsVisible = row.IsVisible,
            IsHidden = row.IsHidden,
            IsEvergreen = row.IsEvergreen,
            ApprovalStatus = row.ApprovalStatus,
            BodySha256 = row.BodySha256,
            AwaitingConfirmUtc = row.AwaitingConfirmUtc,
            SeedManaged = row.SeedManaged
        };
    }
}
