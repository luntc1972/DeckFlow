namespace DeckFlow.Core.Knowledge;

/// <summary>
/// Immutable model for a content source, such as a YouTube channel or podcast RSS feed.
/// </summary>
public sealed record ContentSource
{
    /// <summary>Surrogate identifier for the content source row.</summary>
    public required long Id { get; init; }

    /// <summary>URL-safe slug used when constructing content artifact paths.</summary>
    public required string SourceSlug { get; init; }

    /// <summary>Human-readable source name shown in content KB surfaces.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Source discriminator matching one of the <see cref="ContentSourceType"/> constants.</summary>
    public required string SourceType { get; init; }

    /// <summary>Canonical source URL, such as a channel URL or RSS feed URL.</summary>
    public required string SourceUrl { get; init; }

    /// <summary><see langword="true"/> when the source is eligible for local harvest runs.</summary>
    public required bool IsEnabled { get; init; }

    /// <summary>UTC timestamp when the source row was created.</summary>
    public required DateTimeOffset CreatedUtc { get; init; }
}

/// <summary>
/// Immutable model for a harvested content item, such as a video or podcast episode.
/// </summary>
public sealed record ContentVideo
{
    /// <summary>Surrogate identifier for the content video row.</summary>
    public required long Id { get; init; }

    /// <summary>Identifier of the source that owns this content item.</summary>
    public required long SourceId { get; init; }

    /// <summary>YouTube video identifier, or <see langword="null"/> for podcast episodes.</summary>
    public string? YoutubeVideoId { get; init; }

    /// <summary>RSS item GUID, or <see langword="null"/> for YouTube videos.</summary>
    public string? RssGuid { get; init; }

    /// <summary>Content title supplied by the upstream source.</summary>
    public required string Title { get; init; }

    /// <summary>Canonical URL for the content item.</summary>
    public required string VideoUrl { get; init; }

    /// <summary>UTC publication timestamp, or <see langword="null"/> when unavailable.</summary>
    public DateTimeOffset? PublishedUtc { get; init; }

    /// <summary>Transcript status matching one of the <see cref="TranscriptStatus"/> constants.</summary>
    public required string TranscriptStatus { get; init; }

    /// <summary>UTC timestamp when the content item row was created.</summary>
    public required DateTimeOffset CreatedUtc { get; init; }
}

/// <summary>
/// Immutable model for transcript text associated with a content item.
/// </summary>
public sealed record ContentTranscript
{
    /// <summary>Surrogate identifier for the transcript row.</summary>
    public required long Id { get; init; }

    /// <summary>Identifier of the content item this transcript belongs to.</summary>
    public required long VideoId { get; init; }

    /// <summary>Transcript source matching one of the <see cref="TranscriptSource"/> constants.</summary>
    public required string Source { get; init; }

    /// <summary>Full transcript body.</summary>
    public required string Body { get; init; }

    /// <summary>UTC timestamp when the transcript row was created.</summary>
    public required DateTimeOffset CreatedUtc { get; init; }
}

/// <summary>
/// Immutable model for a generated summary associated with a content item.
/// </summary>
public sealed record ContentSummary
{
    /// <summary>Surrogate identifier for the summary row.</summary>
    public required long Id { get; init; }

    /// <summary>Identifier of the content item this summary belongs to.</summary>
    public required long VideoId { get; init; }

    /// <summary>Summary body generated from the transcript.</summary>
    public required string Body { get; init; }

    /// <summary>UTC timestamp when the summary row was created.</summary>
    public required DateTimeOffset CreatedUtc { get; init; }
}

/// <summary>
/// Immutable model for a timestamped clip excerpt associated with a content item.
/// </summary>
public sealed record ContentClip
{
    /// <summary>Surrogate identifier for the clip row.</summary>
    public required long Id { get; init; }

    /// <summary>Identifier of the content item this clip belongs to.</summary>
    public required long VideoId { get; init; }

    /// <summary>Clip timestamp in seconds from the start of the content item.</summary>
    public required int TimestampS { get; init; }

    /// <summary>Excerpt text for the timestamped clip.</summary>
    public required string Excerpt { get; init; }

    /// <summary>Stable sort order for clips associated with the same content item.</summary>
    public required int SortOrder { get; init; }
}

/// <summary>
/// Immutable model for an allowlisted tag attached to a content item.
/// </summary>
public sealed record ContentTag
{
    /// <summary>Surrogate identifier for the tag row.</summary>
    public required long Id { get; init; }

    /// <summary>Identifier of the content item this tag belongs to.</summary>
    public required long VideoId { get; init; }

    /// <summary>Tag dimension matching one of the <see cref="ContentTagDimension"/> constants.</summary>
    public required string Dimension { get; init; }

    /// <summary>Allowlisted tag value within the selected dimension.</summary>
    public required string TagValue { get; init; }
}

/// <summary>
/// Shared discriminator values for transcript source rows.
/// </summary>
public static class TranscriptSource
{
    /// <summary>Transcript text came from upstream captions.</summary>
    public const string Captions = "captions";

    /// <summary>Transcript text came from Whisper transcription.</summary>
    public const string Whisper = "whisper";
}

/// <summary>
/// Shared discriminator values for content transcript status rows.
/// </summary>
public static class TranscriptStatus
{
    /// <summary>Transcript work has not started.</summary>
    public const string Pending = "pending";

    /// <summary>Caption transcript was fetched successfully.</summary>
    public const string Captions = "captions";

    /// <summary>Whisper transcript was generated successfully.</summary>
    public const string Whisper = "whisper";

    /// <summary>Transcript fetch or generation failed.</summary>
    public const string Failed = "failed";

    /// <summary>Whisper transcription was skipped because the monthly cap would be exceeded.</summary>
    public const string SkippedOverCap = "skipped_over_cap";

    /// <summary>Transcript fetch was skipped because captions were unavailable and Whisper was disabled.</summary>
    public const string SkippedNoCaptions = "skipped_no_captions";
}

/// <summary>
/// Shared discriminator values for content source types.
/// </summary>
public static class ContentSourceType
{
    /// <summary>YouTube channel source type stored in content source rows.</summary>
    public const string Youtube = "youtube_channel";

    /// <summary>Podcast RSS source type stored in content source rows.</summary>
    public const string Podcast = "podcast_rss";
}

/// <summary>
/// Shared discriminator values for content tag dimensions.
/// </summary>
public static class ContentTagDimension
{
    /// <summary>Archetype or strategy tag dimension.</summary>
    public const string Archetype = "archetype";

    /// <summary>Commander bracket tag dimension.</summary>
    public const string Bracket = "bracket";

    /// <summary>Functional card category tag dimension.</summary>
    public const string CardCategory = "card_category";
}
