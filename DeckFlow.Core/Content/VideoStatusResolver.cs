using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Content;

/// <summary>
/// Resolves the per-video UI badge status by querying the real content stores.
/// Lives in DeckFlow.Core (pure store-query logic, no Blazor dependency) so it can be
/// unit-tested directly in DeckFlow.Core.Tests without inverting the project dependency (HIGH-2).
/// </summary>
public sealed class VideoStatusResolver
{
    private readonly IBlockedVideoStore _blockedStore;
    private readonly IContentSiteIndexStore _indexStore;
    private readonly IContentSourceStore _sourceStore;
    private readonly IContentVideoStore _videoStore;

    /// <summary>
    /// Initialises a new <see cref="VideoStatusResolver"/>.
    /// </summary>
    /// <param name="blockedStore">Store of blocked YouTube video identifiers.</param>
    /// <param name="indexStore">Store of distilled content site-index rows.</param>
    /// <param name="sourceStore">Store of enabled harvest sources.</param>
    /// <param name="videoStore">Store of harvested content videos.</param>
    public VideoStatusResolver(
        IBlockedVideoStore blockedStore,
        IContentSiteIndexStore indexStore,
        IContentSourceStore sourceStore,
        IContentVideoStore videoStore)
    {
        ArgumentNullException.ThrowIfNull(blockedStore);
        ArgumentNullException.ThrowIfNull(indexStore);
        ArgumentNullException.ThrowIfNull(sourceStore);
        ArgumentNullException.ThrowIfNull(videoStore);

        _blockedStore = blockedStore;
        _indexStore = indexStore;
        _sourceStore = sourceStore;
        _videoStore = videoStore;
    }

    /// <summary>
    /// Derives the <see cref="VideoStatus"/> for an already-loaded content-site-index row from its
    /// persisted fields. Extracted as a pure static helper so the same rule lives in one place —
    /// <see cref="ResolveStatusAsync"/> routes through this for the index-row branch, and
    /// <c>Review.razor</c> calls it directly for each displayed row without extra store round-trips.
    /// </summary>
    /// <param name="approvalStatus">The <c>approval_status</c> column value (e.g. "approved", "pending").</param>
    /// <param name="pushedToProdUtc">Timestamp when the artifact was pushed to prod, or <see langword="null"/>.</param>
    /// <param name="isVisible">Whether the artifact is visible on the production site.</param>
    /// <returns>
    /// <see cref="VideoStatus.Published"/> when pushed and visible;
    /// <see cref="VideoStatus.Approved"/> when approved but not yet live (including pushed-but-hidden);
    /// <see cref="VideoStatus.Distilled"/> otherwise.
    /// </returns>
    public static VideoStatus FromContentRow(
        string approvalStatus,
        DateTimeOffset? pushedToProdUtc,
        bool isVisible)
    {
        // Published: pushed AND visible (pushed-but-hidden stays Approved — operator limbo semantic).
        if (pushedToProdUtc.HasValue && isVisible)
        {
            return VideoStatus.Published;
        }

        // Approved: in KB and admin-approved but not yet live.
        if (approvalStatus == "approved")
        {
            return VideoStatus.Approved;
        }

        return VideoStatus.Distilled;
    }

    /// <summary>
    /// Resolves the <see cref="VideoStatus"/> badge for a YouTube video using real store queries.
    /// </summary>
    /// <remarks>
    /// Resolution rules (checked in order):
    /// <list type="number">
    ///   <item><description>Blocked wins: <see cref="VideoStatus.Blocked"/> if <see cref="IBlockedVideoStore.IsBlockedAsync"/> returns true.</description></item>
    ///   <item><description>Published: <see cref="VideoStatus.Published"/> if the index row is pushed to prod and visible.</description></item>
    ///   <item><description>Approved: <see cref="VideoStatus.Approved"/> if the index row has approval_status "approved" but is not yet published.</description></item>
    ///   <item><description>Distilled: <see cref="VideoStatus.Distilled"/> if a content_site_index row exists but is not yet approved.</description></item>
    ///   <item><description>Harvested: <see cref="VideoStatus.Harvested"/> if the video exists in any enabled source.</description></item>
    ///   <item><description>Not harvested: <see cref="VideoStatus.NotHarvested"/> otherwise.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="youtubeVideoId">YouTube video identifier to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resolved badge status.</returns>
    public async Task<VideoStatus> ResolveStatusAsync(string youtubeVideoId, CancellationToken ct = default)
    {
        // 1. Blocked wins — checked first regardless of any other signals.
        if (await _blockedStore.IsBlockedAsync(youtubeVideoId, ct).ConfigureAwait(false))
        {
            return VideoStatus.Blocked;
        }

        // 2. Index row exists — distinguish Approved/Published/Distilled without extra store calls.
        // Why: use ContentSourceType.Youtube constant — never the raw string literal (LOW-1).
        var indexRow = await _indexStore.GetByNaturalKeyAsync(
            ContentSourceType.Youtube,
            youtubeVideoId,
            ct).ConfigureAwait(false);

        if (indexRow is not null)
        {
            // Why: route through the shared pure mapper so the Published/Approved/Distilled rule
            // lives in exactly one place (FromContentRow) — Review.razor uses the same method.
            return FromContentRow(indexRow.ApprovalStatus, indexRow.PushedToProdUtc, indexRow.IsVisible);
        }

        // 3. Harvested: the video exists in at least one enabled source.
        // Iterate all enabled sources and stop on the first hit.
        var sources = await _sourceStore.ListEnabledSourcesAsync(ct).ConfigureAwait(false);
        foreach (var source in sources)
        {
            var video = await _videoStore.GetVideoByYoutubeIdAsync(source.Id, youtubeVideoId, ct).ConfigureAwait(false);
            if (video is not null)
            {
                return VideoStatus.Harvested;
            }
        }

        // 4. Not found in any enabled source.
        return VideoStatus.NotHarvested;
    }
}
