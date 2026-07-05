using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Studio.Services;

namespace DeckFlow.Studio.ViewModels;

/// <summary>
/// Harvest queue management (HARV-02), extracted from the <c>Harvest</c> page code-behind
/// (Phase 82 SRP split). Owns the paste-queue fetch/build/dedupe sequence and the trivial
/// remove/toggle operations so the page keeps only the queue state fields (bound in markup)
/// and the busy/error wiring around this call. Behavior is identical to the prior inline
/// implementation.
/// </summary>
public sealed class HarvestQueueCoordinator
{
    private readonly IYouTubeChannelVideoLister _lister;
    private readonly VideoStatusResolver _statusResolver;

    /// <summary>Creates the coordinator with the channel-video lister and status resolver.</summary>
    public HarvestQueueCoordinator(IYouTubeChannelVideoLister lister, VideoStatusResolver statusResolver)
    {
        ArgumentNullException.ThrowIfNull(lister);
        ArgumentNullException.ThrowIfNull(statusResolver);
        _lister = lister;
        _statusResolver = statusResolver;
    }

    /// <summary>
    /// Resolves the pasted lines into queue additions: playlist lines expand via
    /// <see cref="IYouTubeChannelVideoLister.ListPlaylistAsync"/>, the remaining lines resolve as
    /// individual video ids/urls via <see cref="IYouTubeChannelVideoLister.GetByIdsAsync"/>. Videos
    /// already present in <paramref name="existingQueue"/> — or already added earlier in this same
    /// batch — are skipped, matching the prior inline duplicate-guard semantics exactly.
    /// </summary>
    public async Task<HarvestQueueAdditionResult> FetchQueueAdditionsAsync(
        IReadOnlyList<string> rawLines,
        IReadOnlyList<VideoViewModel> existingQueue,
        int browseLimit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rawLines);
        ArgumentNullException.ThrowIfNull(existingQueue);

        // Why: a pasted playlist URL is expanded via ListPlaylistAsync (bounded by Count); the
        // remaining lines resolve as individual video ids/urls through GetByIdsAsync. Lister calls
        // are serialized (no Task.WhenAll) to honor AngleSharp's single-thread constraint (Pitfall 6).
        // Why: a watch?v=…&list=… URL (copied from within a playlist) is a SINGLE video, not a
        // playlist — only bare playlist links expand. Classified via YouTubeUrlClassifier (main fix).
        var playlistLines = rawLines
            .Where(YouTubeUrlClassifier.IsPlaylistUrl)
            .ToList();
        var idLines = rawLines.Except(playlistLines).ToList();

        var videos = await Task.Run(
            async () =>
            {
                var collected = new List<YouTubeChannelVideo>();
                foreach (var pl in playlistLines)
                {
                    collected.AddRange(await _lister.ListPlaylistAsync(pl, browseLimit, 0, cancellationToken).ConfigureAwait(false));
                }
                if (idLines.Count > 0)
                {
                    collected.AddRange(await _lister.GetByIdsAsync(idLines.AsReadOnly(), cancellationToken).ConfigureAwait(false));
                }
                return (IReadOnlyList<YouTubeChannelVideo>)collected;
            },
            cancellationToken);

        var added = new List<VideoViewModel>();
        foreach (var v in videos)
        {
            // Skip if already in the queue (by VideoId) — or already added earlier in this batch,
            // matching the prior inline loop where each add grew the same list the guard checked.
            if (existingQueue.Any(q => q.VideoId == v.VideoId) || added.Any(q => q.VideoId == v.VideoId))
            {
                continue;
            }

            var status = await _statusResolver.ResolveStatusAsync(v.VideoId, cancellationToken);

            // Why: the paste queue shows Duplicate badge for already-in-DB videos (HARV-02).
            // Duplicate = Harvested or Distilled — a pre-harvest warning, not auto-exclusion.
            var displayStatus = (status == VideoStatus.Harvested || status == VideoStatus.Distilled)
                ? VideoStatus.Duplicate
                : status;

            added.Add(new VideoViewModel(v.VideoId, v.Url, v.Title, v.PublishedUtc, displayStatus, v.ChannelId, v.ChannelTitle));
        }

        return new HarvestQueueAdditionResult(added, added.Count);
    }

    /// <summary>Removes a single video from the paste queue.</summary>
    public void RemoveFromQueue(List<VideoViewModel> queueVideos, VideoViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(queueVideos);
        queueVideos.Remove(vm);
    }

    /// <summary>Toggles select-all across the paste queue and returns the new toggle state.</summary>
    public bool ToggleAllQueueSelections(IReadOnlyList<VideoViewModel> queueVideos, bool currentlyAllSelected)
    {
        ArgumentNullException.ThrowIfNull(queueVideos);

        var newState = !currentlyAllSelected;
        foreach (var vm in queueVideos)
        {
            vm.Selected = newState;
        }

        return newState;
    }
}

/// <summary>The videos added to the paste queue by a single fetch, and the count added.</summary>
public sealed record HarvestQueueAdditionResult(IReadOnlyList<VideoViewModel> AddedVideos, int AddedCount);
