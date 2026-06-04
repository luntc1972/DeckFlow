---
status: passed
phase: 20-content-kb-ingestion-transcription-local
source: [20-VERIFICATION.md]
started: 2026-05-26T20:30:00-06:00
updated: 2026-05-26T20:30:00-06:00
---

## Current Test

[awaiting human testing]

## Tests

### 1. 5-channel local caption-coverage UAT (SC1 + SC2)
expected: Captions fetched for the majority of 5 MTG channels via YoutubeExplode 6.6.0 with no Google.Apis 403; per-channel AND aggregate whisper_fallback_ratio < 0.25 logged; each fetch line logs transcript_source (captions|whisper) AND caption_track_kind (manual|auto_generated); if ffmpeg is absent a single warning is logged and the run does not abort.
setup: internet access + `OPENAI_API_KEY` in environment; ffmpeg on PATH optional (absent → videos >24MB marked failed, run continues).
steps:
  1. Seed the 5 MTG channels: `content-source-add --url <channel-url> --type youtube --name <name>` (×5).
  2. Run: `"/mnt/c/Program Files/dotnet/dotnet.exe" run --project DeckFlow.CLI -- harvest --db artifacts/uat-content-kb.db --limit 2`
  3. Inspect CLI log for per-channel + aggregate whisper_fallback_ratio and per-fetch transcript_source/caption_track_kind.
result: [pending]

## Summary

total: 1
passed: 0
issues: 0
pending: 1
skipped: 0
blocked: 0

## Gaps

Correction note: corrected 2026-06-04 (HSK-03, D-10) — UAT PASSED 2026-05-27 per `.planning/milestones/v1.4-MILESTONE-AUDIT.md` (5-channel harvest, 10/10 captions).
