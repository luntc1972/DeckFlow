# Phase 21 Discussion Log

**Date:** 2026-05-27
**Mode:** discuss (default)

Pre-discussion recon (caveman:cavecrew-investigator) established that Phase 19 already locked the artifact format (`ContentArtifactSpec.cs`), the tag allowlist (`ContentTagVocabulary.cs`, fully populated), and all persistence schemas. Discussion therefore focused only on the HOW of the missing distillation pieces.

## Areas selected
All four offered gray areas: LLM call, artifact location, clip timestamps, distill trigger/resume.

## Questions, options, selections

| Area | Options presented | Selected |
|------|-------------------|----------|
| LLM call (model + shape) | gpt-4o-mini single call / gpt-4o single call / **gpt-4o-mini separate calls** | gpt-4o-mini, 3 separate strict-json calls |
| LLM spend cap | reuse Whisper ledger / **separate LLM cap** / no cap | Separate LLM cap/ledger |
| Artifact location + git | **MTG_DATA_DIR/content-kb gitignored** / in-repo committed / data-dir separate-commit | MTG_DATA_DIR/content-kb, gitignored, {source-slug}/{video_id}.md |
| Clip timestamps | require timed transcript / **best-effort** / LLM-estimated | Best-effort |
| Distill trigger | same harvest run / **separate `distill` verb** | Separate `distill` verb |
| Resume semantics | **staged status skip completed** / always re-distill | Staged, idempotent skip |
| Failure handling | **mark + continue** / retry then mark / abort run | Mark video distill-failed, log, continue (no retry) |

## Notes / open items routed to research
- Confirm whether the Phase 20 stored transcript retains any per-line timing (affects clip timestamp availability under D-08 best-effort).
- ChatClient strict-`json_schema` construction in OpenAI SDK 2.10 (.NET) — exact API.
- Run-store column mapping for LLM-call/LLM-spend totals (Whisper-oriented columns today, D-11).

## Deferred
Podcast distillation; Phase 22 site/upload; bounded LLM retry; timed-caption re-fetch.
