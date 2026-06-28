using DeckFlow.Core.Content;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Mutable per-video view model for the Harvest + Distill page: identity + display fields plus the
/// UI-mutable <see cref="Selected"/>, <see cref="Status"/>, and <see cref="PendingBlock"/> flags.
/// Extracted from the Harvest page code-behind (H1 split) so the pure selection/planning logic in
/// <see cref="HarvestPlanner"/> can operate on it and be unit-tested without a bUnit render.
/// </summary>
public sealed class VideoViewModel
{
    /// <summary>YouTube video identifier.</summary>
    public string VideoId { get; }

    /// <summary>Canonical watch URL.</summary>
    public string Url { get; }

    /// <summary>Video title.</summary>
    public string Title { get; }

    /// <summary>Publish timestamp, when known.</summary>
    public DateTimeOffset? PublishedUtc { get; }

    /// <summary>Resolved harvest/distill status badge; mutated as the row is harvested.</summary>
    public VideoStatus Status { get; set; }

    /// <summary>Whether the operator has selected this row for the next action.</summary>
    public bool Selected { get; set; }

    /// <summary>Whether a block confirmation is pending for this row.</summary>
    public bool PendingBlock { get; set; }

    /// <summary>YouTube channel id for this video's author, when available from the listing source.</summary>
    public string? ChannelId { get; }

    /// <summary>Display name of the YouTube channel that published this video, when available.</summary>
    public string? ChannelTitle { get; }

    /// <summary>Creates a video view model from listing/status data.</summary>
    public VideoViewModel(
        string videoId,
        string url,
        string title,
        DateTimeOffset? publishedUtc,
        VideoStatus status,
        string? channelId = null,
        string? channelTitle = null)
    {
        VideoId = videoId;
        Url = url;
        Title = title;
        PublishedUtc = publishedUtc;
        Status = status;
        ChannelId = channelId;
        ChannelTitle = channelTitle;
    }
}
