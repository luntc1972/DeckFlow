# Phase 21: Content KB Distillation + Artifact Emit (local) - Context

**Gathered:** 2026-05-27
**Status:** Ready for planning

<domain>
## Phase Boundary

Compose the Phase 20 ingestion services + a NEW local LLM distillation step into an end-to-end local run that turns each harvested video's transcript into:
1. an **AI-prompt artifact file** (per the locked `ContentArtifactSpec` format),
2. a **slim site-index row** (`ContentSiteIndexStore.UpsertRowAsync`), and
3. a **local run summary** (`content_harvest_runs`).

Distillation = LLM summary + timestamped clips + controlled-vocab tags via OpenAI Structured Outputs. Source add/edit/disable management runtime (KB-01) lives here too. Everything runs locally via CLI — nothing executes on Render.

**Already locked by Phase 19 (do NOT redesign):** artifact file format (`ContentArtifactSpec.cs`), tag allowlist (`ContentTagVocabulary.cs`), and the persistence schemas (video aggregate inserts, slim index, harvest-run store).

**Phase 21 builds the MISSING pieces:** LLM summarization service (new `ChatClient` usage — only Whisper `AudioClient` exists today), clip extraction, tag inference, artifact file emission, slim-index row insertion, run-record wiring, a new `distill` CLI verb, and a separate LLM spend ledger/cap.

</domain>

<decisions>
## Implementation Decisions

### LLM call (KB-06)
- **D-01:** Model = `gpt-4o-mini` for all distillation chat calls (cheap, adequate for ≤200-word summaries).
- **D-02:** Call shape = **THREE separate strict-`json_schema` calls per video** — (1) summary, (2) clips, (3) tags — NOT one combined call. Isolation/robustness chosen over fewer round-trips. Each call uses OpenAI Structured Outputs (`response_format: json_schema`, `strict: true`) for parse reliability (PITFALLS P4, target <0.1% parse failure).
- **D-03:** Summary target ≤200 words; clips target 3–8 per video.

### Tag inference (KB-07)
- **D-04:** LLM emits candidate tags; every tag is filtered through `ContentTagVocabulary.IsValid(dimension, value)`. Tags outside the allowlist are **rejected with a WARN log** and never persisted. Valid tags persist to `content_tags` AND the slim index row's tag arrays. Vocab is already fully populated (15 archetypes / 5 brackets / 11 card_categories) — do not extend it in this phase.

### LLM spend cap (KB-06)
- **D-05:** LLM chat spend uses a **SEPARATE cap/ledger**, independent of the Whisper spend ledger. Build a new LLM spend ledger mirroring the `WhisperSpendLedger` pattern (`RecordCallAsync` / `GetMonthlyTotalAsync` / `WouldExceedCapAsync`). Cap-check before LLM calls; over-cap → skip the video's distillation and record an `aborted_reason` (consistent with SC5). Do NOT fold LLM cost into the Whisper ledger.

### Artifact emission (KB-06)
- **D-06:** Artifacts land under **`MTG_DATA_DIR/content-kb`** (beside the SQLite dbs), filename **`{source-slug}/{video_id}.md`**. **Gitignored** — public repo, no artifact text committed. (Phase 22 owns serving/uploading to `/data`.)
- **D-07:** Emit EXACTLY the `ContentArtifactSpec` format (YAML frontmatter + `## Summary` + `## Key Clips` + `## Tags`). Do not invent a new layout.

### Clip timestamps (KB-06)
- **D-08:** **Best-effort timestamps.** Use real `[mm:ss]` timing when the transcript provides it; omit or approximate when the stored transcript is plain text. No hard requirement to re-fetch timed captions. (Open research item: confirm whether the Phase 20 stored transcript retains any timing — see RESEARCH targets.)

### Orchestration trigger + resume (KB-02)
- **D-09:** Distillation runs via a **SEPARATE `distill` CLI verb**. `harvest` stays transcript-only (unchanged). `distill` processes videos that HAVE a transcript but are NOT yet distilled.
- **D-10:** **Staged, idempotent resume.** Track per-video distill completion (artifact emitted + slim-index row present + summary/clip/tag rows written). A re-run **skips already-distilled videos** — never re-calls the LLM, never re-Whispers, never re-distills. (Satisfies SC2 resume guarantee.)
- **D-11:** Each `distill` run writes a `content_harvest_runs` row (`StartRunAsync` → `CompleteRunAsync`) capturing videos distilled, LLM calls, LLM spend USD, and `aborted_reason` if cap-hit. NOTE: existing run-store columns are Whisper-oriented (`whisper_calls`, `spend_usd`) — planner must decide how LLM-call/LLM-spend totals map (reuse columns vs thread additional fields).

### Failure handling (KB-02)
- **D-12:** On any per-video distill failure (API error, invalid structured output): **mark the video distill-failed, log it, continue the batch.** Single attempt — **no bounded retry** (user chose mark+continue over retry-then-mark). Re-runnable on a later `distill` invocation. Batch never aborts on one video — mirrors the Phase 20 per-source failure-isolation pattern.

### Source management runtime (KB-01)
- **D-13:** Source add/edit/disable is operable from the harvester CLI (`content-source-add` already exists; add edit/disable as needed) and respected by the next run via the `is_enabled` filter. Soft-disable keeps prior harvested data.

### Claude's Discretion
- Exact strict `json_schema` shapes for the summary/clips/tags calls.
- `ChatClient` construction details — mirror the existing OpenAI client pattern in `WhisperTranscriptionService` (HttpClientPipelineTransport, `ApiKeyCredential`, `OPENAI_API_KEY` env var).
- The precise distill-status persistence mechanism (new `content_videos` column vs derived from artifact-file + slim-index-row presence). Must support D-10 idempotent skip.
- Prompt wording for each call.
- Per-process LLM call pacing / Polly pipeline (a resilience pipeline is fine, but per D-12 do NOT add retry that masks failures as success — transient errors still mark the video failed for re-run).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Locked contracts (Phase 19) — emit/validate against these exactly
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — the locked AI-prompt artifact file format (YAML frontmatter + `## Summary` + `## Key Clips` + `## Tags`). Artifact emitter MUST produce this layout.
- `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` — tag allowlist + `IsValid(dimension, value)`. All LLM tags filtered through this; out-of-vocab → WARN + drop.

### Persistence (Phase 19 — already exist, wire into orchestrator)
- `DeckFlow.Core/Content/ContentVideoStore.cs` — `InsertSummaryAsync` / `InsertClipAsync(videoId, timestampS, excerpt, sortOrder)` / `InsertTagAsync(videoId, dimension, tagValue)`.
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — `UpsertRowAsync(ContentSiteIndexRow)` (slim index row; columns include source/title/video_url/artifact_path/tag arrays/natural key).
- `DeckFlow.Core/Content/ContentHarvestRunStore.cs` — `StartRunAsync` / `CompleteRunAsync(...)` (run summary record).
- `DeckFlow.Core/Content/WhisperSpendLedger.cs` + `IWhisperSpendLedger.cs` — pattern to mirror for the NEW separate LLM spend ledger (`RecordCallAsync` / `GetMonthlyTotalAsync` / `WouldExceedCapAsync`).

### OpenAI client pattern
- `DeckFlow.Core/Integration/WhisperTranscriptionService.cs` (§165–208) — existing OpenAI SDK 2.10 client construction (HttpClientPipelineTransport, `ApiKeyCredential`, `OPENAI_API_KEY`). New `ChatClient` summarization service mirrors this.

### Orchestration entrypoint
- `DeckFlow.CLI/CommandRunners.cs` (§446–532, `RunHarvestAsync`) — line 532 marks `// Phase 21 owns distillation, artifact emit, slim-index rows, and run records.` The new `distill` verb wires in here.
- `DeckFlow.CLI/Program.cs` — CLI verb/option registration (model: existing `harvest` / `content-source-add` verbs).

### Requirements / roadmap
- `.planning/ROADMAP.md` — Phase 21 section + 5 Success Criteria (SC2 reconciled 2026-05-27 for opt-in Whisper + `skipped_no_captions`).
- `.planning/REQUIREMENTS.md` — KB-01, KB-02, KB-06, KB-07.

### Project conventions + prior context
- `CLAUDE.md` — Allman braces, file-scoped namespaces, `sealed record` DTOs, `IReadOnlyList<T>` on public surface, internal test-seam ctor pattern, NO new NuGet packages without approval, NO reformatting of untouched lines, LF endings.
- `.planning/phases/19-content-kb-foundation-local-schema-contracts/19-CONTEXT.md` — Phase 19 locked schema/contract decisions.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ContentArtifactSpec` + `ContentTagVocabulary`: artifact format and tag allowlist are done — consume, don't rebuild.
- `ContentVideoStore` insert methods (summary/clips/tags), `ContentSiteIndexStore.UpsertRowAsync`, `ContentHarvestRunStore` Start/Complete: all persistence sinks already exist.
- `WhisperSpendLedger` (cap-check via `WouldExceedCapAsync`): direct template for the new separate LLM spend ledger (D-05).
- OpenAI client construction in `WhisperTranscriptionService`: template for the new `ChatClient`-based summarization service.
- Phase 20 per-source/per-video failure-isolation (try/catch + continue) in `CommandRunners`: template for D-12 distill failure handling.

### Established Patterns
- Pure services + CLI single persistence owner (Phase 20 D-11): keep the summarization/clip/tag services pure (no DB writes); the `distill` orchestrator owns persistence + artifact emit.
- Strict `json_schema` Structured Outputs for parse reliability (PITFALLS P4).
- Env-var `OPENAI_API_KEY`; `MTG_DATA_DIR` for local data/artifact location.
- Spend ledger cap-check BEFORE the paid call; record cost after (mirror Whisper ledger ordering, Phase 20 CR-01).

### Integration Points
- New `distill` verb → `DeckFlow.CLI/Program.cs` + `CommandRunners.cs`.
- New `ILlmSummarizationService` (ChatClient) + clip/tag services → `DeckFlow.Core/Integration/`.
- New LLM spend ledger → `DeckFlow.Core/Content/`.
- New distill orchestrator composing the above + existing stores → emits artifact file + index row + run record.

</code_context>

<specifics>
## Specific Ideas

- gpt-4o-mini, three isolated strict-json calls (summary / clips / tags), ≤200-word summary, 3–8 clips.
- Artifacts at `MTG_DATA_DIR/content-kb/{source-slug}/{video_id}.md`, gitignored.
- Separate LLM spend cap; distill is a standalone re-runnable verb that skips completed videos.

</specifics>

<deferred>
## Deferred Ideas

- **Podcast distillation** — Phase 20/21 are YouTube-first; podcast RSS+audio distillation is a later slice (only the `ITranscriptSource` abstraction is built).
- **Site rendering + Render-hosted index materialization + artifact serving/upload** — Phase 22.
- **Bounded retry on LLM failure** — considered; user chose mark-failed + continue (D-12). Revisit if observed parse/API failure rate is high.
- **Re-fetching timed captions to guarantee clip timestamps** — out of scope; best-effort only (D-08). Could be a future quality slice if timestamps prove too sparse.

</deferred>

---

*Phase: 21-content-kb-distillation-artifact-emit-local*
*Context gathered: 2026-05-27*
