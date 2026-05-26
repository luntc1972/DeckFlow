using DeckFlow.Core.Knowledge;

namespace DeckFlow.Web.Services.Content;

/// <summary>
/// Persists local content videos and their transcript, summary, clip, and tag child rows.
/// </summary>
public interface IContentVideoStore
{
    /// <summary>
    /// Ensures the content video aggregate schema exists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a content video and returns its surrogate identifier.
    /// </summary>
    /// <param name="sourceId">Identifier of the owning content source.</param>
    /// <param name="youtubeVideoId">YouTube video identifier, or <see langword="null"/> for RSS-only content.</param>
    /// <param name="rssGuid">RSS GUID, or <see langword="null"/> for YouTube-only content.</param>
    /// <param name="title">Content title.</param>
    /// <param name="videoUrl">Canonical content URL.</param>
    /// <param name="publishedUtc">UTC publication timestamp, when known.</param>
    /// <param name="transcriptStatus">Transcript status matching one of the <see cref="TranscriptStatus"/> constants.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inserted video identifier.</returns>
    Task<long> InsertVideoAsync(
        long sourceId,
        string? youtubeVideoId,
        string? rssGuid,
        string title,
        string videoUrl,
        DateTimeOffset? publishedUtc,
        string transcriptStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts transcript text for a video.
    /// </summary>
    /// <param name="videoId">Identifier of the owning video.</param>
    /// <param name="source">Transcript source matching one of the <see cref="TranscriptSource"/> constants.</param>
    /// <param name="body">Transcript body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inserted transcript identifier.</returns>
    Task<long> InsertTranscriptAsync(
        long videoId,
        string source,
        string body,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a generated summary for a video.
    /// </summary>
    /// <param name="videoId">Identifier of the owning video.</param>
    /// <param name="body">Summary body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inserted summary identifier.</returns>
    Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a timestamped clip excerpt for a video.
    /// </summary>
    /// <param name="videoId">Identifier of the owning video.</param>
    /// <param name="timestampS">Timestamp in seconds from the start of the content item.</param>
    /// <param name="excerpt">Clip excerpt text.</param>
    /// <param name="sortOrder">Stable sort order for clips under the same video.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inserted clip identifier.</returns>
    Task<long> InsertClipAsync(
        long videoId,
        int timestampS,
        string excerpt,
        int sortOrder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a dimensioned tag for a video.
    /// </summary>
    /// <param name="videoId">Identifier of the owning video.</param>
    /// <param name="dimension">Tag dimension matching one of the <see cref="ContentTagDimension"/> constants.</param>
    /// <param name="tagValue">Tag value within the selected dimension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inserted tag identifier.</returns>
    Task<long> InsertTagAsync(
        long videoId,
        string dimension,
        string tagValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a video row by identifier.
    /// </summary>
    /// <param name="videoId">Video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts transcript rows for a video.
    /// </summary>
    /// <param name="videoId">Video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of transcript rows for the video.</returns>
    Task<int> CountTranscriptsByVideoAsync(long videoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts summary rows for a video.
    /// </summary>
    /// <param name="videoId">Video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of summary rows for the video.</returns>
    Task<int> CountSummariesByVideoAsync(long videoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts clip rows for a video.
    /// </summary>
    /// <param name="videoId">Video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of clip rows for the video.</returns>
    Task<int> CountClipsByVideoAsync(long videoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts tag rows for a video.
    /// </summary>
    /// <param name="videoId">Video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of tag rows for the video.</returns>
    Task<int> CountTagsByVideoAsync(long videoId, CancellationToken cancellationToken = default);
}
