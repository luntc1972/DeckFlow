using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

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
    /// Gets a YouTube content video by source and upstream video identifier.
    /// </summary>
    /// <param name="sourceId">Identifier of the owning content source.</param>
    /// <param name="youtubeVideoId">YouTube video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The content video when found; otherwise <see langword="null"/>.</returns>
    Task<ContentVideo?> GetVideoByYoutubeIdAsync(
        long sourceId,
        string youtubeVideoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists videos for one source that have a successful transcript ready for distillation.
    /// </summary>
    /// <param name="sourceId">Identifier of the owning content source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Videos scoped to the supplied source that have at least one transcript row.</returns>
    Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(
        long sourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists display-focused pending-distill rows for one source, including the raw durable distill status.
    /// </summary>
    /// <param name="sourceId">Identifier of the owning content source.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pending-distill display rows scoped to the supplied source.</returns>
    Task<IReadOnlyList<PendingDistillProjection>> ListPendingDistillDisplayAsync(
        long sourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the transcript status for a content video.
    /// </summary>
    /// <param name="videoId">Identifier of the video to update.</param>
    /// <param name="status">Transcript status matching one of the <see cref="TranscriptStatus"/> constants.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateTranscriptStatusAsync(
        long videoId,
        string status,
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
    /// Gets the most recently inserted transcript body for a video.
    /// </summary>
    /// <param name="videoId">Identifier of the owning video.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest transcript body and source when present; otherwise <see langword="null"/>.</returns>
    Task<ContentTranscriptBody?> GetLatestTranscriptAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This content video store does not support transcript reads.");

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
    /// Deletes a video row by its globally unique YouTube identifier.
    /// </summary>
    /// <param name="youtubeVideoId">YouTube video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of deleted video rows.</returns>
    Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all content video rows so FK-cascaded transcript, summary, clip, tag, and ledger children are purged too.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of deleted video rows.</returns>
    Task<int> DeleteAllVideosAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This content video store does not support deleting all video rows.");

    /// <summary>
    /// Deletes generated summary, clip, and tag rows for a video before a clean re-distill.
    /// </summary>
    /// <param name="videoId">Video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This content video store does not support clearing distill output.");

    /// <summary>
    /// Gets the durable distill status for a video.
    /// </summary>
    /// <param name="videoId">Video identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored status, or <see langword="null"/> when the video has not been attempted.</returns>
    Task<string?> GetDistillStatusAsync(long videoId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This content video store does not support distill status reads.");

    /// <summary>
    /// Sets the durable distill status for a video.
    /// </summary>
    /// <param name="videoId">Video identifier.</param>
    /// <param name="status">Distill status: <c>distilled</c>, <c>skipped_over_cap</c>, or <c>failed</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetDistillStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This content video store does not support distill status writes.");

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

/// <summary>
/// Latest transcript text returned to the distillation orchestrator.
/// </summary>
public sealed record ContentTranscriptBody
{
    /// <summary>Transcript body.</summary>
    public required string Body { get; init; }

    /// <summary>Transcript source matching one of the <see cref="TranscriptSource"/> constants.</summary>
    public required string Source { get; init; }
}
