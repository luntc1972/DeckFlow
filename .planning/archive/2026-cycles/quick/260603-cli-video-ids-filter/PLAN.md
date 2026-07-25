---
date: 2026-06-03
slug: cli-video-ids-filter
type: quick
status: complete
---

# Quick Task: --video-ids selection on harvest + distill

Allow processing specific videos instead of most-recent-N.

## Tasks
1. `IYouTubeChannelVideoLister.GetByIdsAsync(videoIds, ct)` + YoutubeExplode impl (per-id `Videos.GetAsync`, maps to `YouTubeChannelVideo`) + test-seam delegate.
2. `harvest --video-ids "a,b,c" [--source-id N]` — fetch exactly those IDs for the target source (single enabled source default; `--source-id` required when several). `--limit` ignored when set. Existing per-video resolve/skip/status logic reused.
3. `distill --video-ids "a,b,c"` — filter pending videos by natural key (YoutubeVideoId or RssGuid); `--limit` ignored when set.
4. Tests: harvest-by-ids happy path + multi-source error + already-harvested skip; distill filter selects only named keys.

## Acceptance
- Default behavior (no flag) byte-identical.
- Build 0/0; Core + Web test suites green.
