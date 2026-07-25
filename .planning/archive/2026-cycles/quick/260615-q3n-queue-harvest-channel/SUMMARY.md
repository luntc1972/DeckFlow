---
quick_id: 260615-q3n
slug: queue-harvest-channel
date: 2026-06-15
status: complete
commits:
  - ea6f8f7
  - 7661107
tags: [core, studio, harvest, youtube, channel, playlist]
key_files:
  created: []
  modified:
    - DeckFlow.Core/Integration/YouTubeChannelVideo.cs
    - DeckFlow.Core/Integration/IYouTubeChannelVideoLister.cs
    - DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs
    - DeckFlow.Studio/Pages/Harvest.razor
decisions:
  - "ListPlaylistAsync added to interface with default-throw body so all existing fakes/throwing doubles keep compiling without changes"
  - "PlaylistVideo.Author confirmed available in YoutubeExplode 6.6.0 — ChannelId/ChannelTitle populated for list path without per-video metadata round-trip (avoids WR-02)"
  - "HarvestSelectedAsync groups by channelUrl so EnsureYoutubeSourceAsync gets a canonical URL string per group"
  - "Playlist browse does NOT set _lastBrowsedChannel to avoid polluting the channel-URL fallback with a playlist URL that spans multiple channels"
---

# Quick Task 260615-q3n: queue harvest resolves channel per video

## One-liner

Channel metadata (ChannelId/ChannelTitle) now flows from YouTube into each video model; harvest groups selected videos by channel and auto-ensures a source per channel, removing the Browse-first gate; Browse input also accepts playlist URLs.

## What was built

### Task 1 — Core: channel metadata on the video model (commit ea6f8f7)

**YouTubeChannelVideo** gains two nullable init properties:
- `string? ChannelId` — YouTube channel identifier for the video's author
- `string? ChannelTitle` — display name of the channel

Nullable so all existing record constructions without these properties compile unchanged.

**YouTubeChannelVideoLister** changes:
- `GetVideoByIdAsync`: populates ChannelId/ChannelTitle from `metadata.Author`
- `MapVideo(PlaylistVideo, ...)`: populates same fields from `video.Author` (confirmed on PlaylistVideo in YoutubeExplode 6.6.0; no extra per-video metadata round-trip — avoids WR-02)
- New `_listPlaylistAsync` delegate field (mirrors `_getByIdsAsync`); wired in public ctor via `CreateListPlaylistAsync(httpClient)` closure; optional third param on internal test ctor (existing 2-arg callers still compile)
- New `ListPlaylistAsync` public method with argument validation, delegates to `_listPlaylistAsync`
- New `ListPlaylistWithClientAsync` static: `PlaylistId.TryParse` + `youtube.Playlists.GetVideosAsync(...).CollectAsync(skip+limit)` + `Skip(skip)` + `MapVideo` per item; PublishedUtc/ViewCount left null (unavailable from playlist feed)

**IYouTubeChannelVideoLister**: `ListPlaylistAsync` added with `default` body (NotSupportedException) so ThrowingYouTubeChannelVideoLister, FakeYouTubeChannelVideoLister, and FakeLister need zero changes.

### Task 2+3 — Studio: per-channel harvest grouping + playlist browse (commit 7661107)

**VideoViewModel**: two optional ctor params appended (`string? channelId = null`, `string? channelTitle = null`) with get-only `ChannelId`/`ChannelTitle` properties. Pending-distill construction site unchanged (no channel needed there).

**Browse UI**: card header/label/placeholder/hint updated to mention playlist URLs; button label simplified to "Browse".

**BrowseChannelAsync**: detects playlist URL via `Contains("list=")` or `Contains("playlist?")` (case-insensitive); routes to `ListPlaylistAsync` or `ListRecentAsync`; passes v.ChannelId/v.ChannelTitle into VideoViewModel; only sets `_lastBrowsedChannel` for non-playlist inputs.

**AddToQueueAsync**: passes v.ChannelId/v.ChannelTitle from `GetByIdsAsync` result into VideoViewModel.

**HarvestSelectedAsync** rewrite:
- Removed the `_lastBrowsedChannel` hard guard
- Per-video resolution: v.ChannelId -> `https://www.youtube.com/channel/{id}`; fallback to `_lastBrowsedChannel`; videos with no URL collected as "unresolved"
- All unresolved -> clean abort with message; partial unresolved -> log warning, skip those, continue
- Groups resolvable videos by channelUrl; inside Task.Run, foreach group: EnsureYoutubeSourceAsync + HarvestAsync; aggregates Captions/Whisper/SkippedNoCaptions across groups
- Badge refresh, streaming progress, CTS-on-Dispose, result markup all unchanged

## Build verification

- DeckFlow.Core: 0 errors, 0 warnings
- DeckFlow.Core.Tests + CLI: 0 errors, 3 pre-existing xUnit2017 warnings
- DeckFlow.sln full: 0 errors, 3 warnings (same pre-existing)

## Tests skipped

New lister channel/playlist unit tests not added — live YouTube access required. Property additions are trivial nullable inits. EnsureYoutubeSourceAsync grouping logic covered by existing orchestrator tests. Out of scope per plan.

## Known stubs

None.

## Deviations from Plan

None — plan executed exactly as written.

## Known caveats (out of scope per plan)

- **Source de-dup not addressed:** If a channel was previously ensured via handle URL and now also via channel-id URL, two source rows may exist. EnsureYoutubeSourceAsync deduplicates by URL match; different URL forms produce separate rows. Harmless for local single-operator use.
- **Playlist-path videos have null PublishedUtc/ViewCount:** Not available from YoutubeExplode playlist feed.

## Self-Check: PASSED

- [x] DeckFlow.Core/Integration/YouTubeChannelVideo.cs — modified, exists
- [x] DeckFlow.Core/Integration/IYouTubeChannelVideoLister.cs — modified, exists
- [x] DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs — modified, exists
- [x] DeckFlow.Studio/Pages/Harvest.razor — modified, exists
- [x] Commit ea6f8f7 — feat(core): add ChannelId/ChannelTitle to YouTubeChannelVideo and ListPlaylistAsync
- [x] Commit 7661107 — feat(studio): per-channel harvest grouping + playlist browse
- [x] Build: 0 errors, 0 new warnings
