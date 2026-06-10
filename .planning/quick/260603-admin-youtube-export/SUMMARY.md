---
date: 2026-06-03
slug: admin-youtube-export
type: quick
status: complete
---

# Summary: Admin YouTube channel export

## What changed
- New admin page /Admin/YoutubeExport (BasicAuth branch, sidebar link): enter a channel
  handle/URL/id + max-videos (1-500, default 100) -> downloads `{slug}-videos.txt` in the
  same layout as the original salubrious-snail file (header, `# Views Date Title` table,
  per-video URL line, totals footer).
- `YouTubeChannelVideo.ViewCount` added; `YouTubeChannelVideoLister` now captures
  `Engagement.ViewCount` in the SAME per-video metadata call it already made for
  upload dates (no extra HTTP) — both ListRecentAsync and GetByIdsAsync paths.
- `YouTubeVideoListExport.BuildText` pure formatter in Core (reusable from CLI later).
- Export POST double-guarded: [ValidateAntiForgeryToken] + SameOriginRequestValidator
  (admin mutation convention); 5-min linked-CTS timeout; errors render an
  `admin-banner--error` banner (new one-line CSS variant next to --success).
- DI: named HttpClient "youtube-metadata" + transient lister registration.

## Files
- DeckFlow.Core/Integration/YouTubeChannelVideo.cs (+ViewCount)
- DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs (views in metadata fetch)
- DeckFlow.Core/Integration/YouTubeVideoListExport.cs (new)
- DeckFlow.Web/Controllers/Admin/AdminYoutubeExportController.cs (new)
- DeckFlow.Web/Models/Admin/AdminYoutubeExportViewModel.cs (new)
- DeckFlow.Web/Views/AdminYoutubeExport/{Index,_ViewStart}.cshtml (new)
- DeckFlow.Web/Views/Shared/_AdminLayout.cshtml (sidebar link)
- DeckFlow.Web/wwwroot/css/admin-common.css (admin-banner--error)
- DeckFlow.Web/Program.cs (DI)
- Tests: DeckFlow.Core.Tests/YouTubeVideoListExportTests.cs (new, 2),
  DeckFlow.Web.Tests/AdminYoutubeExportControllerTests.cs (new, 5)

## Verification
- Solution build 0/0. Web 533/538 pass (5 pre-existing PG skips) incl. 5 new controller
  tests (cross-origin 403 before any YouTube call, blank input, file content/name,
  empty channel, lister-throw banner). Core 265/265 incl. 2 formatter tests.
- Known intermittent: 1 pre-existing Core flake hit 2/10 runs today (name not captured;
  6 consecutive clean runs after; new tests are deterministic fakes). Track if recurs.

## Notes
- YoutubeExplode from Render egress can hit YouTube bot checks — page degrades to an
  error banner; reliable from local runs.
- Answers the "only 30 videos" gap: the original file scraped the channel page's first
  render (30 items, no pagination). This export walks the FULL uploads playlist up to
  the requested count.
