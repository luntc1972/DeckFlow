---
date: 2026-06-03
slug: cli-video-ids-filter
type: quick
status: complete
---

# Summary: --video-ids selection on harvest + distill

## What changed
- `IYouTubeChannelVideoLister.GetByIdsAsync(videoIds, ct)` + YoutubeExplode impl
  (per-id `Videos.GetAsync`, input order preserved, unavailable ids omitted with
  per-id isolation mirroring the per-source policy) + delegate test seam.
- `harvest --video-ids "a,b,c" [--source-id N]` — fetches exactly those ids for the
  target source; single enabled YouTube source auto-selected, `--source-id` required
  when several; unresolved ids logged as warnings; `--limit` ignored.
- `distill --video-ids "a,b,c"` — pending videos filtered by natural key (YouTube id
  or RSS guid); `--limit` bypassed for the explicit set.
- `CommandRunners.ParseVideoIds` — comma-split, trim, ordinal-dedupe, null on blank.

## Files
- DeckFlow.Core/Integration/IYouTubeChannelVideoLister.cs (+GetByIdsAsync)
- DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs (impl + seam)
- DeckFlow.CLI/Program.cs (options + handlers)
- DeckFlow.CLI/CommandRunners.cs (ParseVideoIds, harvest explicit-id branch, distill filter)
- DeckFlow.Core.Tests/CommandRunnerHarvestTests.cs (+3 harvest tests, +2 ParseVideoIds tests, fake extended)
- DeckFlow.Core.Tests/RunDistillAsyncTests.cs (+1 filter test)

## Verification
- Solution build 0 warnings / 0 errors (Windows SDK)
- Core tests 263/263; Web tests 528 pass / 5 pre-existing PG skips
- Default behavior (no flag) unchanged — all pre-existing tests green untouched

## Usage (Salubrious Snail example)
1. dotnet run --project DeckFlow.CLI -- content-source-add --url https://www.youtube.com/@salubrioussnail --name "Salubrious Snail"
2. dotnet run --project DeckFlow.CLI -- harvest --video-ids "VLdny8IVXYE,IJYU_rzCcP8,Oh_a34vdtIA"
3. dotnet run --project DeckFlow.CLI -- distill --video-ids "VLdny8IVXYE,IJYU_rzCcP8,Oh_a34vdtIA"   (claude provider = $0)
4. dotnet run --project DeckFlow.CLI -- content-index-export
