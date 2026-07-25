---
date: 2026-06-03
slug: admin-youtube-export
type: quick
status: complete
---

# Quick Task: Admin YouTube channel export

Admin page: enter a YouTube user/handle/URL + count -> download a .txt file
(same layout as artifacts/salubrious-snail-videos.txt) with video title, view
count, and upload date per video.

## Tasks
1. Core: `YouTubeChannelVideo.ViewCount` + lister populates it (both ListRecent + GetByIds paths; reuses the existing per-video metadata call, no extra HTTP).
2. Core: `YouTubeVideoListExport.BuildText(...)` pure formatter matching the snail file layout.
3. Web: transient `IYouTubeChannelVideoLister` DI via named HttpClient "youtube-metadata".
4. Web: `AdminYoutubeExportController` — GET form; POST Export ([ValidateAntiForgeryToken] + SameOriginRequestValidator) -> text/plain FileResult.
5. View + sidebar link (reuse admin-common styles, no new CSS).
6. Tests: formatter (Core.Tests) + controller guards/output (Web.Tests, fake lister).

## Notes / risks
- YoutubeExplode from Render egress may hit YouTube bot checks; page is admin-only,
  on-demand; failure renders an error banner. Works reliably from local runs.
- Doc gate live: every new public member gets XML docs.

## Acceptance
- Download produces the snail-format txt with exact views + yyyy-MM-dd dates.
- Build 0/0; Core + Web suites green.
