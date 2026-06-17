---
quick_id: 260615-q3n
slug: queue-harvest-channel
type: quick
date: 2026-06-15
follow_up_to: phase 45-03 / quick 260615-h2v
description: Harvest pasted/queued videos and whole playlists by capturing each video's channel and ensuring a source per channel
files_modified:
  - DeckFlow.Core/Integration/YouTubeChannelVideo.cs
  - DeckFlow.Core/Integration/IYouTubeChannelVideoLister.cs
  - DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs
  - DeckFlow.Studio/Pages/Harvest.razor
  - (test doubles implementing IYouTubeChannelVideoLister — add ListPlaylistAsync to each)
---

# Quick Task: queue harvest resolves channel per video

## Problem

Harvesting a pasted video URL/ID (no prior channel browse) blocks with
"Browse a channel first — harvested videos need a target source." The auto-ensure source
(quick 260615-h2v) only derives the source from `_lastBrowsedChannel`, which the paste queue never sets.
`YouTubeChannelVideo` carries no channel, so the page cannot derive a source from a pasted video.

## Decision

Capture each video's channel from YouTube metadata, then in the harvest handler GROUP selected videos
by channel, ensure a source per channel, and harvest each group with its own `sourceId`. This unifies
browse + paste harvest and removes the "browse first" requirement. (YoutubeExplode exposes `.Author`
— `ChannelId` + `ChannelTitle` — on both `Video` and `PlaylistVideo`.)

## Tasks

### Task 1 — Core: carry the channel on the video model

- `YouTubeChannelVideo`: add `public string? ChannelId { get; init; }` and
  `public string? ChannelTitle { get; init; }` (xmldoc each; nullable so older callers/tests compile).
- `YouTubeChannelVideoLister`:
  - `GetVideoByIdAsync` (~229): set `ChannelId = metadata.Author.ChannelId.Value`,
    `ChannelTitle = metadata.Author.ChannelTitle`.
  - `MapVideo(PlaylistVideo video, …)` (~240): set `ChannelId = video.Author.ChannelId.Value`,
    `ChannelTitle = video.Author.ChannelTitle`.
  - If `PlaylistVideo.Author` is not available in YoutubeExplode 6.6.0 for the list path, leave the list
    path channel fields null (browse videos fall back to `_lastBrowsedChannel` in the page) and populate
    only the by-id path — the build will tell you. Do NOT add a new metadata round-trip per list video
    (Pitfall: unbounded per-video lookups / WR-02).

### Task 2 — Studio: group selected videos by channel and ensure a source each

- `DeckFlow.Studio/Pages/Harvest.razor`:
  - `VideoViewModel`: add `ChannelId` and `ChannelTitle` (string?, get-only) ctor params + properties.
    Update both construction sites — `_channelVideos.Add(new VideoViewModel(... v.ChannelId, v.ChannelTitle))`
    (~717) and `_queueVideos.Add(...)` (~794) — to pass the source video's channel fields. (Update the
    VideoViewModel ctor signature accordingly; keep existing args order, append the two new ones.)
  - In `HarvestSelectedAsync` (~1000):
    - REMOVE the `_lastBrowsedChannel` hard-guard block (~1018-1024).
    - Build a per-video channel resolution: for each selected video pick
      `channelUrl = !string.IsNullOrWhiteSpace(v.ChannelId) ? $"https://www.youtube.com/channel/{v.ChannelId}" : (!string.IsNullOrWhiteSpace(_lastBrowsedChannel) ? _lastBrowsedChannel : null)`
      and `channelName = v.ChannelTitle ?? v.ChannelId ?? _lastBrowsedChannel`.
    - Videos whose `channelUrl` is null → collect as "unresolved"; if ALL selected are unresolved, set a
      message ("Could not determine a channel for the selected video(s).") and abort cleanly (no throw).
      Otherwise log one warning line listing skipped unresolved ids and continue with the rest.
    - GROUP the resolvable selected videos by `channelUrl`. Inside the existing `Task.Run` (keep CTS +
      progress sink), FOREACH group:
      `var src = await SourceManager.EnsureYoutubeSourceAsync(channelUrl, channelName, progress, _cts.Token);`
      if `!src.Success || src.Id is null` → record the failure (append to an aggregate message), skip the
      group; else `var r = await HarvestOrchestrator.HarvestAsync(limit: groupIds.Count, videoIds: groupIds, sourceId: src.Id, progress: progress, cancellationToken: _cts.Token);`
      and accumulate `Captions/Whisper/SkippedNoCaptions`.
    - Return a single aggregate `HarvestResult`: `Success = (no group failed to ensure/harvest) && (atLeastOneGroupRan)`,
      summed counts, `Message =` first failure reason when not all succeeded.
  - Keep the badge refresh (`RefreshBadgesAsync(selectedIds)`), the streaming progress, CTS-on-Dispose,
    and the result summary markup unchanged (it already renders Captions/Whisper/Skipped + Message).
  - `_lastBrowsedChannel` stays (set on browse) ONLY as the fallback above; it is no longer a gate.

### Task 3 — Playlist browse (compose with the per-channel grouping)

- `YouTubeChannelVideoLister`: add
  `Task<IReadOnlyList<YouTubeChannelVideo>> ListPlaylistAsync(string playlistUrl, int limit, int skip = 0, CancellationToken ct = default)`
  to `IYouTubeChannelVideoLister` (interface + impl). Impl mirrors `ListRecentAsync`:
  `var pid = PlaylistId.TryParse(playlistUrl) ?? throw new ArgumentException(...);`
  `var items = await youtube.Playlists.GetVideosAsync(pid, ct).CollectAsync(skip + limit).ConfigureAwait(false);`
  then `items.Skip(skip)` mapped via `MapVideo` (which now carries ChannelId/ChannelTitle from
  `PlaylistVideo.Author`). Validate `limit >= 1`, `skip >= 0`. Add a delegate seam consistent with
  the existing `_executeAsync` pattern OR a direct `ListPlaylistWithClientAsync` static (match the
  file's existing structure; if you must change the internal test ctor delegate, update its callers/tests).
- Studio `Harvest.razor`: make the Browse input accept a **playlist URL** too:
  - Relabel the card/section + input + button to "Browse Channel or Playlist" / placeholder mentions a
    playlist URL; helper text notes both are accepted.
  - In `BrowseChannelAsync`: detect a playlist URL (input contains `list=` or `playlist?`) → call
    `Lister.ListPlaylistAsync(input, _browseLimit, _browseSkip, ct)`; otherwise `ListRecentAsync(...)`
    as today. Same Count/Skip apply. Reuse the existing table/badge rendering and `Task.Run` + CTS.
  - Do NOT set `_lastBrowsedChannel` to a playlist URL (a playlist spans channels); leave the per-video
    ChannelId grouping (Task 2) to attach each harvested video to its own channel source.

## Acceptance

- `dotnet build DeckFlow.sln` — 0 errors, 0 new warnings; both test projects build.
- Paste a playlist URL into Browse → its videos list with badges; select → Harvest Selected → each video
  attaches to its own channel's auto-ensured source and harvests (multi-channel playlists supported).
- Paste a video URL (no browse) → select → Harvest Selected → source auto-created from the video's
  channel, harvest completes, badge flips to Harvested (no "Browse a channel first" block).
- Browse → select → harvest still works (videos carry ChannelId → one group → one source).
- Selecting videos from two different channels harvests both, each attached to its own source.

## Out of scope

- Backfilling channel on already-harvested DB rows.
- De-duplicating any pre-existing handle-based source vs the new channel-id source (one-time artifact;
  harmless for a local single-operator tool — note it in SUMMARY).
- New Core unit tests for the lister channel populate (live YouTube only — note skipped; the model
  change is property-only and EnsureYoutubeSourceAsync is already covered).
