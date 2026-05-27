# Phase 21: Content KB Distillation + Artifact Emit (local) - Research

**Researched:** 2026-05-27
**Domain:** Local CLI orchestration + OpenAI structured-output distillation + SQLite persistence (codebase integration)
**Confidence:** HIGH (all findings verified against in-repo source; AI-SDK depth deferred to 21-AI-SPEC.md)

## Scope note

The OpenAI .NET SDK / `ChatClient` / Structured Outputs technical depth is fully captured in `21-AI-SPEC.md` sections 3, 4, 4b and is NOT repeated here. This document answers the 7 OPEN codebase + design questions and maps each KB requirement (KB-01, KB-02, KB-06, KB-07) to concrete, lowest-rework recommendations grounded in the actual schema and CLI patterns.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Model = `gpt-4o-mini` for all distillation chat calls.
- **D-02:** THREE separate strict-`json_schema` calls per video (summary, clips, tags) — NOT combined. `strict: true`.
- **D-03:** Summary ≤200 words; clips 3–8 per video.
- **D-04:** LLM tags filtered through `ContentTagVocabulary.IsValid(dimension, value)`. Out-of-vocab → WARN + drop, never persist. Vocab is fully populated (15 archetypes / 5 brackets / 11 card_categories) — do not extend.
- **D-05:** SEPARATE LLM spend cap/ledger, independent of Whisper. Mirror `WhisperSpendLedger` (`RecordCallAsync` / `GetMonthlyTotalAsync` / `WouldExceedCapAsync`). Cap-check before calls; over-cap → skip + `aborted_reason`. Do NOT fold into Whisper ledger.
- **D-06:** Artifacts under `MTG_DATA_DIR/content-kb`, filename `{source-slug}/{video_id}.md`. Gitignored.
- **D-07:** Emit EXACTLY the `ContentArtifactSpec` format (YAML frontmatter + `## Summary` + `## Key Clips` + `## Tags`).
- **D-08:** Best-effort timestamps. Real `[mm:ss]` when transcript provides timing; omit/approximate otherwise. No hard requirement to re-fetch.
- **D-09:** SEPARATE `distill` CLI verb. `harvest` stays transcript-only. `distill` processes videos that HAVE a transcript but are NOT yet distilled.
- **D-10:** Staged, idempotent resume. Re-run skips already-distilled videos — never re-calls LLM/Whisper.
- **D-11:** Each `distill` run writes a `content_harvest_runs` row (`StartRunAsync` → `CompleteRunAsync`) capturing videos distilled, LLM calls, LLM spend, `aborted_reason`. Run-store columns are Whisper-oriented — planner decides mapping.
- **D-12:** Per-video distill failure → mark failed, log, continue. Single attempt, NO retry-that-masks-failure. Re-runnable later. Batch never aborts on one video.
- **D-13:** Source add/edit/disable operable from CLI; respected via `is_enabled` filter. Soft-disable keeps prior data.

### Claude's Discretion
- Exact strict `json_schema` shapes for summary/clips/tags calls.
- `ChatClient` construction details — mirror `WhisperTranscriptionService`.
- The precise distill-status persistence mechanism (new column vs derived from artifact-file + slim-index-row presence). Must support D-10 idempotent skip.
- Prompt wording for each call.
- Per-process LLM call pacing / Polly pipeline (no retry that masks failures as success).

### Deferred Ideas (OUT OF SCOPE)
- Podcast distillation (YouTube-first; only the `ITranscriptSource` abstraction is built).
- Site rendering + Render-hosted index materialization + artifact serving/upload (Phase 22).
- Bounded retry on LLM failure (user chose mark-failed + continue).
- Re-fetching timed captions to GUARANTEE clip timestamps (best-effort only).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| KB-01 | Source list add/edit/disable runtime via harvester CLI; `is_enabled` filter; soft-disable keeps data | Q6: add `content-source-set-enabled` verb + `ContentSourceStore.SetEnabledAsync` + (optionally) update mutators. `ListEnabledSourcesAsync` already filters by `is_enabled`. |
| KB-02 | End-to-end local run + `content_harvest_runs` record | Q4 + Q5: `distill` verb orchestrator wires `ContentHarvestRunStore.StartRunAsync`/`CompleteRunAsync`; column mapping recommended (overload `whisper_calls`/`spend_usd` as LLM-call/LLM-spend for distill runs). |
| KB-06 | LLM summary + 3–8 clips → artifact file, Structured Outputs | Q1 (timestamps), Q3 (spend ledger), Q5 (orchestration), Q7 (artifact writer). AI-SPEC §3/§4 owns SDK depth. |
| KB-07 | Controlled-vocab tags persisted + slim index row | Tag inference via AI-SPEC §4; filter through `ContentTagVocabulary.IsValid`; persist via `ContentVideoStore.InsertTagAsync` + `ContentSiteIndexStore.UpsertRowAsync`. |
</phase_requirements>

## Summary

Phase 19 built every persistence sink and the locked artifact contract; Phase 20 built transcript ingestion and left a marker at `CommandRunners.cs:532` for Phase 21. The distillation layer is genuinely greenfield: a new `LlmDistillationService` (ChatClient), a new `LlmSpendLedger`, a new `distill` CLI verb + orchestrator, an artifact writer, and small additive query/mutator methods on existing stores.

The single largest discovery: **caption timing IS available at harvest time but is being discarded.** `YouTubeTranscriptFetcher` (line 76) does `string.Join(" ", track.Captions.Select(c => c.Text))`, dropping `ClosedCaption.Offset` (a `TimeSpan`, confirmed present in YoutubeExplode 6.6.0 `[CITED: github.com/Tyrrrz/YoutubeExplode]`). The persisted `content_transcripts.body` is therefore plain text with no timing. This shapes the D-08 recommendation.

The second structural constraint: **there is no migration mechanism.** Every store uses `CREATE TABLE IF NOT EXISTS` only — an existing UAT database with the current `transcript_status` CHECK constraint will REJECT any `INSERT`/`UPDATE` using a new enum value that isn't in the baked-in CHECK list. This rules out "add a `distilled` value to `transcript_status`" without a destructive reset, and steers every schema-change answer toward derived state or net-new tables.

**Primary recommendation:** Track distill completion as DERIVED state (artifact file present AND slim-index row present) rather than a schema change — zero migration risk, satisfies D-10. Emit clips with `null` timestamps from the plain-text transcript (D-08 best-effort, lowest rework); optionally capture timing at harvest time later as a Phase 22+ quality slice. Add a net-new `llm_spend_ledger` table mirroring `WhisperSpendLedger`. For the run record, overload the existing `whisper_calls`/`spend_usd` columns to mean LLM-calls/LLM-spend on distill runs (no schema change). Add a small `ListVideosPendingDistillAsync` query to `ContentVideoStore`.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| LLM summary/clips/tags extraction | DeckFlow.Core (Integration) | — | Pure service, no DB writes (Phase 20 D-11 pattern); ChatClient seam mirrors `WhisperTranscriptionService` |
| Tag allowlist enforcement | DeckFlow.Core (Knowledge) | DeckFlow.CLI (orchestrator applies) | `ContentTagVocabulary.IsValid` is the locked gate; orchestrator drops + WARNs |
| LLM spend cap/ledger | DeckFlow.Core (Content) | — | Mirror `WhisperSpendLedger`; durable state owned outside the LLM service |
| Distill orchestration + persistence + artifact emit | DeckFlow.CLI (CommandRunners) | DeckFlow.Core (stores) | CLI is the single persistence owner (Phase 20 D-11); composes pure services + stores |
| Artifact file writing | DeckFlow.Core (Knowledge or new Content writer) | — | Pure file I/O over `ContentArtifactSpec`; `MTG_DATA_DIR` resolved by CLI and passed in |
| Source management (add/edit/disable) | DeckFlow.Core (Content store) | DeckFlow.CLI (verbs) | `ContentSourceStore` owns SQL; CLI verbs are thin wrappers |
| Idempotent resume / skip-completed | DeckFlow.CLI (orchestrator) | DeckFlow.Core (stores + file probe) | Derived from artifact-file + slim-index-row presence |

## Standard Stack

ZERO new packages (CLAUDE.md hard rule + AI-SPEC §2). All capabilities use already-referenced assemblies.

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `OpenAI` | 2.10.0 | `ChatClient` for distillation (NEW namespace `OpenAI.Chat` from same assembly) | Already powers Whisper via `AudioClient`; zero new dependency `[VERIFIED: DeckFlow.Core.csproj:17]` |
| `YoutubeExplode` | 6.6.0 | Caption fetch; `ClosedCaption.Offset`/`Duration` available if timed transcript ever needed | Already referenced `[VERIFIED: DeckFlow.Core.csproj:19]` |
| `Polly` | 8.x | Optional resilience pipeline for LLM calls (no masking retry per D-12) | Already the project resilience standard |
| `Microsoft.Data.Sqlite` / `Npgsql` | 10.0.0 | Backs the new `llm_spend_ledger` + existing stores | Existing storage dialect |
| `System.CommandLine` | 2.0.0-beta4 | New `distill` + source-mgmt verbs | Existing CLI host |
| `Serilog` | 4.2.0 | Per-video distill events, dropped-tag WARN, spend records | Existing CLI Serilog file sink |

**Installation:** None. Confirm existing refs only:
```bash
"/mnt/c/Program Files/dotnet/dotnet.exe" list DeckFlow.Core/DeckFlow.Core.csproj package | grep -iE "OpenAI|YoutubeExplode"
# expect: OpenAI 2.10.0 ; YoutubeExplode 6.6.0
```

## Package Legitimacy Audit

> Not applicable in the usual sense — **zero new packages are installed by this phase.** All recommended libraries are pre-existing, version-pinned `DeckFlow.Core` dependencies verified against `DeckFlow.Core.csproj`. CLAUDE.md forbids adding dependencies without explicit user approval, and AI-SPEC §2 confirms the design is built specifically to avoid it.

| Package | Registry | Status | Disposition |
|---------|----------|--------|-------------|
| OpenAI 2.10.0 | NuGet | Pre-existing in csproj (line 17) | Reuse, no install |
| YoutubeExplode 6.6.0 | NuGet | Pre-existing in csproj (line 19) | Reuse, no install |

No new package names are introduced; no slopcheck/registry verification required because nothing is installed.

## The 7 Open Questions

### Q1 — Clip timestamps (D-08 best-effort)

**Finding (HIGH confidence):**
- YoutubeExplode 6.6.0 `ClosedCaption` exposes `Offset` (`TimeSpan`, "Time at which the caption starts displaying"), `Duration` (`TimeSpan`), `Text`, and `Parts` `[CITED: github.com/Tyrrrz/YoutubeExplode ClosedCaption.cs]`. Timing IS available at fetch time.
- BUT the harvester discards it: `YouTubeTranscriptFetcher.FetchWithClientAsync` does `string.Join(" ", track.Captions.Select(caption => caption.Text))` (line 76) and persists that plain-text body via `ContentVideoStore.InsertTranscriptAsync`. `content_transcripts.body` therefore carries NO timing.
- Whisper transcripts are also stored as plain joined text (`WhisperTranscriptionService` joins chunk bodies with spaces). No timing there either.
- The persistence target `content_clips.timestamp_s` is a **non-nullable `INT NOT NULL`** (ContentVideoStore SQLite/Postgres DDL). The artifact spec shows `[02:14]` style timestamps but is prose-flexible.

**The three options, evaluated:**

| Option | Rework | D-08 fit | Verdict |
|--------|--------|----------|---------|
| (a) Emit clips with null/omitted timestamps from plain text | Lowest — no fetcher/schema change | Best — "omit or approximate when plain text" | **RECOMMENDED** |
| (b) Re-fetch timed caption track during distill | High — new fetch path, re-hits YouTube, couples distill to network, complicates idempotency | Explicitly deferred (CONTEXT Deferred Ideas) | Reject for MVP |
| (c) Capture timing at harvest time (store timed transcript variant) | Medium — changes Phase 20 fetcher + transcript schema | Out of scope this phase | Defer to Phase 22+ quality slice |

**RECOMMENDATION (Option a):** Emit clips with **best-effort / null timestamps** from the plain-text transcript.

Concrete handling required by the planner because `content_clips.timestamp_s` is `NOT NULL` while the LLM `ClipItem.TimestampSeconds` is `int?` (AI-SPEC §4):
- The model returns `null` (it cannot derive timing from untimed text — and per AI-SPEC P2 the schema field is `["integer","null"]`).
- **In the artifact file:** when `TimestampSeconds` is `null`, OMIT the `[mm:ss]` prefix and render the bullet as `- [excerpt]` (D-08 "omit … never confidently wrong"). The `ContentArtifactSpec.ArtifactFileFormat` is a prose template, not a parser — omitting the timestamp marker is spec-compatible.
- **In `content_clips` (DB):** because the column is `NOT NULL`, store a **sentinel `0`** for null timestamps AND rely on `sort_order` (the existing column) to preserve LLM clip ordering. The slim-index row carries no clip timestamps, so this DB sentinel never leaks to the public surface. Document the `0`-sentinel convention with a `// Why:` comment.
  - *Alternative the planner may prefer:* if the planner wants to distinguish "no timing" from "timestamp 0s", a follow-up could add a nullable column — but that is a schema change with the no-migration caveat below; for MVP the `0` + `sort_order` sentinel is the lowest-rework choice and matches "best-effort."

**Schema-compat caveat:** Do NOT add a nullable `timestamp_s` or new clip column unless the planner accepts the destructive-reset cost (see Q2 caveat). The `0`-sentinel needs no schema change.

---

### Q2 — Distill-status persistence (D-09/D-10 idempotent resume)

**Finding (HIGH confidence):**
- `content_videos.transcript_status` has a **baked-in CHECK constraint**: `CHECK (transcript_status IN ('pending','captions','whisper','failed','skipped_over_cap','skipped_no_captions'))` in BOTH the SQLite and Postgres DDL (ContentVideoStore lines 432 / 481).
- `UpdateTranscriptStatusAsync` ALSO guards in C# via `IsValidTranscriptStatus` (lines 272–278) — it throws `ArgumentException` for any status not in `TranscriptStatus`.
- **There is NO migration mechanism.** Every store's `EnsureSchemaAsync` runs only `CREATE TABLE IF NOT EXISTS`. An existing database keeps its original CHECK. Inserting/updating a row with a value like `'distilled'` would be rejected at the SQLite/Postgres level (constraint violation) AND by the C# guard.
- All persistence sinks for distill output already exist: `InsertSummaryAsync`, `InsertClipAsync`, `InsertTagAsync`, plus count helpers `CountSummariesByVideoAsync` / `CountClipsByVideoAsync` / `CountTagsByVideoAsync`, plus `ContentSiteIndexStore.GetByNaturalKeyAsync(naturalKeyType, naturalKeyValue)`.

**The options, evaluated:**

| Option | Migration risk | D-10 fit | Verdict |
|--------|---------------|----------|---------|
| New `transcript_status` value (`distilled`) | HIGH — needs CHECK change + C# guard change; existing DBs reject | conflates transcript stage w/ distill stage (a video is `captions` AND distilled) | Reject |
| New column on `content_videos` (`distill_status`) | MEDIUM — `ADD COLUMN` not emitted by `CREATE TABLE IF NOT EXISTS`; existing DB never gets it without manual ALTER | clean but migration-blocked | Reject for MVP |
| Separate `distill_runs`/`distill_status` table | LOW — net-new `CREATE TABLE IF NOT EXISTS` applies cleanly to existing DBs | explicit, queryable | Viable, but adds a table + store |
| **Derive from presence of (artifact file + slim-index row [+ summary row])** | **ZERO — no schema touched** | satisfies D-10 exactly | **RECOMMENDED** |

**RECOMMENDATION:** Track distill completion as **DERIVED state**. A video is "already distilled" when BOTH:
1. The artifact file exists at `MTG_DATA_DIR/content-kb/{source-slug}/{video_id}.md`, AND
2. A slim-index row exists for its natural key (`ContentSiteIndexStore.GetByNaturalKeyAsync(ContentSourceType.Youtube, video_id)` returns non-null).

The orchestrator checks both at the START of each video (the D-10 / AI-SPEC Guardrail "skip-completed" gate) and skips — never calling the LLM. Optionally also require `CountSummariesByVideoAsync(videoId) > 0` for defense in depth, but file + index row is the authoritative pair (the index row is what Phase 22 serves, and it's only written on full success).

**Why derived beats a table for MVP:**
- Zero migration risk against the existing UAT database (the explicit caveat the user flagged).
- The slim-index row is already the "this video is published to the KB" signal — its presence IS distill-completion by construction (the orchestrator writes the index row LAST, after artifact + child rows).
- `ContentSiteIndexStore.UpsertRowAsync` is idempotent (`ON CONFLICT … DO UPDATE`), so a partially-failed prior run that wrote child rows but NOT the index row is correctly seen as "not distilled" and retried — exactly D-12 re-runnability.

**Ordering invariant the planner MUST encode (critical for correctness):**
Write order per video = (1) LLM calls + validate → (2) `InsertSummary`/`InsertClip`/`InsertTag` → (3) write artifact file → (4) `UpsertRowAsync` (index row LAST). If the run dies mid-video, the missing index row means the next `distill` re-processes it. To avoid duplicate child rows on re-run, either: (a) delete-then-reinsert child rows at start of distilling a video (simplest given no "distilled" flag), or (b) make the orchestrator tolerant of the `content_tags` UNIQUE(video_id,dimension,tag_value) constraint. Recommend (a): a small `ClearDistillOutputAsync(videoId)` helper that deletes summary/clip/tag rows before re-distilling, so re-runs are clean.

**Schema-compat caveat:** The derived approach touches NO schema. If the planner instead wants an explicit `distill_runs` table, it is safe (net-new `CREATE TABLE IF NOT EXISTS`) but adds a store + interface and is not required for D-10.

---

### Q3 — Separate LLM spend ledger (D-05)

**Finding (HIGH confidence):**
- `WhisperSpendLedger` is a concrete class over a concrete `whisper_spend_ledger` table with: `RecordCallAsync(videoId, secondsBilled, costUsd, monthKey)`, `GetMonthlyTotalAsync(yearMonth)`, `WouldExceedCapAsync(projectedCost, monthKey)`.
- Cap value source: env var **`DECKFLOW_WHISPER_MONTHLY_CAP_USD`** with default `15.00m`, read via an injected `Func<string,string?>?` resolver falling back to `Environment.GetEnvironmentVariable` (WhisperSpendLedger lines 12–13, 166–182). `[VERIFIED: WhisperSpendLedger.cs]`
- The table FK is `video_id … REFERENCES content_videos(id) ON DELETE CASCADE`, `seconds_billed INT`, `cost_usd` (decimal/TEXT), `month_key`, indexed on `month_key`.
- Whisper cost calc is `secondsBilled * 0.006 / 60` (`WhisperUsdPerMinute`). LLM cost is token-based, not seconds-based.

**RECOMMENDATION: Add a NEW `llm_spend_ledger` table + `LlmSpendLedger` class mirroring `WhisperSpendLedger`** (do NOT generalize). Rationale: D-05 explicitly says "build a new LLM spend ledger mirroring the WhisperSpendLedger pattern… Do NOT fold LLM cost into the Whisper ledger." Mirroring keeps the two caps independent and the code parallel/reviewable. Net-new table = zero migration risk.

Concrete shape:
- **New env var:** `DECKFLOW_LLM_MONTHLY_CAP_USD` (mirrors the Whisper key naming exactly). Pick a sensible default — AI-SPEC §4 estimates ~$1.10 for a 200-video harvest, so a default like `5.00m` gives generous headroom; the planner should confirm the default with the user, tag `[ASSUMED]`.
- **Table `llm_spend_ledger`** mirroring `whisper_spend_ledger`, but replace `seconds_billed INT` with token columns. Recommended columns: `video_id` (FK CASCADE), `input_tokens INT`, `output_tokens INT`, `cost_usd`, `month_key`, `created_utc`, index on `month_key`.
- **Methods:** `RecordCallAsync(long videoId, int inputTokens, int outputTokens, decimal costUsd, string monthKey, …)`, `GetMonthlyTotalAsync(string yearMonth)`, `WouldExceedCapAsync(decimal projectedCallCostUsd, string monthKey)`. Same `IXxxSpendLedger` interface style.
- **Cost calc (gpt-4o-mini):** `cost = inputTokens/1e6 * INPUT_PRICE + outputTokens/1e6 * OUTPUT_PRICE`. AI-SPEC §4 cites ≈ $0.15/1M input, ≈ $0.60/1M output `[CITED: platform.openai.com/docs/models/gpt-4o-mini]`. Compute the EXACT per-call cost from `completion.Usage.InputTokenCount` / `OutputTokenCount` AFTER the call (AI-SPEC §4 — "do not estimate at ledger time"). Store the two price constants as `decimal` consts on the ledger or distillation service (mirror `WhisperUsdPerMinute`); tag the prices `[ASSUMED]` so they get user confirmation (prices drift).
- **Cap-check ordering:** call `WouldExceedCapAsync(projectedCost, monthKey)` BEFORE the paid call (mirror Whisper ordering, Phase 20 CR-01); over-cap → skip the video's distillation, record `aborted_reason` on the run record, stop processing further videos (hard stop per AI-SPEC §7 / SC5). For the *projected* cost before the call, estimate from transcript token count × 3 calls (AI-SPEC §4 gives the formula); record actual after.
- **`EnsureSchemaAsync` FK ordering:** mirror `WhisperSpendLedger.EnsureSchemaAsync` — it first instantiates `new ContentVideoStore(_connectionInfo).EnsureSchemaAsync()` so the `content_videos` FK parent exists before declaring the FK (REVIEW #1 pattern, WhisperSpendLedger lines 60–63). The new ledger must do the same.

**Schema-compat caveat:** Net-new table via `CREATE TABLE IF NOT EXISTS` is safe against existing DBs. No migration needed.

---

### Q4 — `content_harvest_runs` column mapping (D-11)

**Finding (HIGH confidence):**
- `content_harvest_runs` columns: `started_utc`, `completed_utc`, `sources_processed`, `videos_processed`, `transcripts_fetched`, `whisper_calls`, `spend_usd`, `aborted_reason` (ContentHarvestRunStore DDL lines 207–233).
- `StartRunAsync()` inserts a row (started_utc + spend_usd=0) and returns the id. `CompleteRunAsync(runId, sourcesProcessed, videosProcessed, transcriptsFetched, whisperCalls, spendUsd, abortedReason)` updates it. There are validation guards (`ThrowIfNegative`) on all int/decimal params.
- These columns are Whisper/harvest-oriented; there is no `llm_calls` or `videos_distilled` column.

**The options, evaluated:**

| Option | Migration risk | Semantic clarity | Verdict |
|--------|---------------|------------------|---------|
| Add `llm_calls`, `videos_distilled` columns | MEDIUM — `ADD COLUMN` not emitted; existing DB never gets them | cleanest | Reject for MVP (migration-blocked) |
| Reuse/overload existing columns for distill runs | ZERO | acceptable with documented convention | **RECOMMENDED** |
| New `distill_runs` table | LOW (net-new) | cleanest + independent | Viable alternative if planner wants separation |

**RECOMMENDATION: Reuse `content_harvest_runs` with documented overloaded semantics for distill runs** (D-11's "reuse columns" path), because it requires no schema change and `CompleteRunAsync` already takes exactly the fields needed:
- `videos_processed` ← videos distilled (success count).
- `whisper_calls` ← **LLM calls this run** (3 per successfully distilled video). Document the overload with a `// Why:` comment: on a `distill` run this column counts LLM calls, not Whisper calls.
- `spend_usd` ← **LLM spend USD** this run (from `LlmSpendLedger` totals / summed per-call cost).
- `transcripts_fetched` ← 0 (distill fetches no transcripts) OR repurpose as "distill-failed count" with a documented comment. Recommend leaving it 0 and surfacing distill-failed count in the completion LOG (Serilog) rather than overloading a second column — keeps the overload minimal and auditable.
- `sources_processed` ← number of enabled sources whose videos were considered.
- `aborted_reason` ← set when the LLM cap is hit (SC5).

The orchestrator calls `StartRunAsync()` at the top and `CompleteRunAsync(...)` at the end (and in a `finally`/catch so a crashed run still records what it completed). Because a SINGLE column set is shared by harvest and distill runs, there is no way to tell run-type apart from the row alone — note this in a `// Why:` and accept it (the operator knows which verb they ran; run-type discrimination is a Phase 22 reporting concern if needed).

**Schema-compat caveat:** Overloading existing columns needs no schema change. If the planner prefers unambiguous reporting, a net-new `distill_runs` table (with `llm_calls`, `videos_distilled`, `spend_usd`, `aborted_reason`) is safe via `CREATE TABLE IF NOT EXISTS` and avoids the overload — but adds a store. Recommend overload for MVP; flag the trade-off.

---

### Q5 — `distill` verb wiring (D-09)

**Finding (HIGH confidence):** The `harvest` verb is the exact template. Pattern observed in `Program.cs` + `CommandRunners.cs`:
- **Program.cs:** declare `var distillCommand = new Command("distill", "...")`; declare options (`--db`, `--limit`, etc.); `distillCommand.AddOption(...)` for each; `rootCommand.AddCommand(distillCommand)`; `distillCommand.SetHandler((FileInfo? db, int limit) => { Environment.ExitCode = CommandRunners.RunDistillAsync(db, limit, Log.Logger, CancellationToken.None).GetAwaiter().GetResult(); }, distillDbOption, distillLimitOption);`
  - The existing handlers use `.GetAwaiter().GetResult()` at the verb boundary. AI-SPEC §4b warns against `.Result`/`.Wait()` deeper in the chain (deadlock + swallows exception type that D-12 needs) — but the top-level `SetHandler` boundary already uses `GetAwaiter().GetResult()` consistently, so MIRROR that exact boundary and keep everything below it `async`/`await`. (This is the established project pattern; do not introduce a different async bridge.)
- **CommandRunners.cs:** a public `RunDistillAsync(FileInfo? db, int limit, ILogger logger, CancellationToken ct)` that constructs concretes and delegates to an `internal RunDistillAsync(...)` taking interfaces (mirrors the two-layer `RunHarvestAsync` at lines 446–534, which exists specifically so tests can inject fakes). The Phase 21 marker is at line 532 inside the internal `RunHarvestAsync` — the new orchestrator is a SIBLING method, NOT an edit to harvest (D-09: harvest stays transcript-only).
  - DB path resolution: reuse `ResolveContentKbDatabasePath(db)` (line 963) — defaults to `artifacts/content-kb.db`.

**Dependencies the distill orchestrator needs (composition root in the public `RunDistillAsync`):**
- `new ContentSourceStore(dbPath)` → `ListEnabledSourcesAsync` (respects `is_enabled`, D-13).
- `new ContentVideoStore(dbPath)` → query un-distilled videos with transcripts (NEW method, see below) + `InsertSummary/Clip/Tag` + count helpers + (new) `ClearDistillOutputAsync`.
- `new ContentSiteIndexStore(dbPath)` → `GetByNaturalKeyAsync` (skip-completed probe) + `UpsertRowAsync` (final write).
- `new ContentHarvestRunStore(dbPath)` → `StartRunAsync`/`CompleteRunAsync`.
- `new LlmSpendLedger(dbPath)` (NEW, Q3).
- `new LlmDistillationService(httpClient)` (NEW, pure; AI-SPEC §3/§4 owns construction) — needs one `HttpClient` (mirror `whisperHttpClient` with a long timeout).
- An artifact writer (NEW, Q7) + the resolved `MTG_DATA_DIR/content-kb` root.
- A transcript-reader: **GAP** — there is currently NO method to READ `content_transcripts.body` back out. `ContentVideoStore` only has `InsertTranscriptAsync` and `CountTranscriptsByVideoAsync`. The orchestrator needs the transcript text to feed the LLM. **Add `GetLatestTranscriptAsync(long videoId)`** (or `GetTranscriptBodyAsync`) returning the most-recent `body` (and ideally `source`).

**Is there a "list videos pending distill" query? NO — it must be added.** `ContentVideoStore`/`IContentVideoStore` have `GetVideoByYoutubeIdAsync` (single, by source+ytid) and the count helpers, but no "list videos with a transcript that aren't distilled yet." **Add `ListVideosPendingDistillAsync(CancellationToken)`** (or scoped per-source `ListVideosWithTranscriptAsync(long sourceId, …)`) to both `IContentVideoStore` and `ContentVideoStore`:
- Query: videos whose `transcript_status IN ('captions','whisper')` (terminal-success — see `IsTerminalSuccess` at CommandRunners line 709) AND that have ≥1 `content_transcripts` row. The "not yet distilled" filter is then applied by the orchestrator via the DERIVED check (artifact file + index row), NOT in SQL — keeps the query simple and the idempotency logic in one place.
- Return `IReadOnlyList<ContentVideo>` (existing `ContentVideo` record; honors the project's `IReadOnlyList` public-surface convention).

**Failure isolation (D-12):** mirror `HarvestVideoAsync` (lines 558–593): per-video `try/catch (Exception) when (not OperationCanceledException)`, log `logger.Error(exception, "distill failed {VideoId}", …)`, continue the loop. No retry that masks failure. The batch aggregate counts mirror the `HarvestCounts` private class pattern.

---

### Q6 — Source edit/disable runtime (KB-01 / D-13)

**Finding (HIGH confidence):**
- `ContentSourceStore` / `IContentSourceStore` current methods: `EnsureSchemaAsync`, `InsertSourceAsync(slug, displayName, sourceType, sourceUrl)`, `GetSourceAsync(id)`, `ListEnabledSourcesAsync()`. `[VERIFIED: ContentSourceStore.cs]`
- The `content_sources` table HAS the `is_enabled` column (default 1/TRUE) and `ListEnabledSourcesAsync` already filters `WHERE is_enabled = @isEnabled` — so the READ path for soft-disable already works; the harvest/distill loops only see enabled sources.
- The `content-source-add` verb exists (`RunContentSourceAddAsync`, CommandRunners line 416) and is registered in Program.cs (lines 59–63, 98–101, 200–203), with slug computation via `SlugifySourceName.Slugify` and UNIQUE-violation handling.
- **MISSING for D-13:** no method or verb to flip `is_enabled`, and no method/verb to edit name/url/type.

**RECOMMENDATION:**
- **Add `ContentSourceStore.SetEnabledAsync(long id, bool isEnabled, CancellationToken)`** (+ interface method): `UPDATE content_sources SET is_enabled = @isEnabled WHERE id = @id;` using the same `IsPostgres ? (object)true/false : 1/0` boolean adaptation seen in `ListEnabledSourcesAsync` (lines 126–129). Soft-disable: existing videos/transcripts are untouched (D-13 "keeps prior harvested data") — no cascade.
- **Add a `content-source-set-enabled` verb** (or two verbs `content-source-disable` / `content-source-enable`). Recommend a single verb with a `--enabled <true|false>` option + `--id` (or `--slug`) to keep the surface small; mirror the `content-source-add` registration + handler exactly. Looking up by slug needs a `GetSourceBySlugAsync` or reuse of `ListEnabledSourcesAsync`+filter; recommend adding a small `GetSourceBySlugAsync` since disabled sources won't appear in `ListEnabledSourcesAsync`. (Minimal: accept `--id` to avoid the lookup entirely for MVP, and add slug lookup only if the user wants it.)
- **Edit (name/url/type):** D-13 says "edit/disable as needed." For MVP the highest-value, lowest-risk subset is **disable/enable** (the `is_enabled` flag is what the run loop respects). Full edit of name/url/type touches the `source_slug`/`source_url` UNIQUE constraints and the artifact-path slug (which the artifact files are keyed by) — renaming a slug would orphan existing artifact directories. **Recommend: implement enable/disable now; treat name/url edit as add-new + disable-old** (re-add under a new name, disable the stale row) to avoid slug-rename orphaning. Flag full in-place edit as a deferred nicety unless the user insists; tag the scope decision `[ASSUMED]` for confirmation.

**Schema-compat caveat:** `SetEnabledAsync` is a plain `UPDATE` on an existing column — zero migration risk.

---

### Q7 — Artifact writer (D-06/D-07)

**Finding (HIGH confidence):**
- **`MTG_DATA_DIR` resolution pattern** `[VERIFIED: DeckFlowDatabaseConnectionFactory.cs:161-170]`: read env var `MTG_DATA_DIR`; if set, `Path.GetFullPath(dataDir)`; else fall back to `Path.GetFullPath(Path.Combine(ContentRootPath, "..", "artifacts"))`. The Web project owns that helper (`ResolveArtifactsPath(IWebHostEnvironment)`), so it's NOT directly reusable from CLI/Core (it takes an `IWebHostEnvironment`). The CLI's own DB default is `ResolveContentKbDatabasePath` → `artifacts/content-kb.db` (no `MTG_DATA_DIR` consultation today).
  - **Recommendation:** the CLI should resolve the artifact root as: `MTG_DATA_DIR` env var if set → `Path.Combine(MTG_DATA_DIR, "content-kb")`; else `Path.Combine(<db-dir>, "content-kb")` so artifacts sit beside the SQLite db (D-06 "beside the SQLite dbs"). Add a small `ResolveContentKbArtifactRoot(FileInfo? db)` helper in `CommandRunners` mirroring `ResolveContentKbDatabasePath`. Pass the resolved absolute root INTO the writer (keep the writer pure — it receives a root, doesn't read env).
- **Existing WriteFile patterns** `[VERIFIED: DeltaExporter.cs:11-19]`: static exporters with `ToText(...)` (pure string build) + `WriteFile(..., outputPath, ...)` (calls `File.WriteAllText`). `FullImportExporter` follows the same shape. The CLI callers `Directory.CreateDirectory(output.DirectoryName ...)` before writing (e.g. `RunExportMoxfieldAsync` line 270). This is the project's canonical file-emit pattern.
- **Path safety:** `ContentSiteIndexStore.ValidateArtifactPath` (lines 169–187) already REJECTS rooted paths and `..` segments for the RELATIVE `artifact_path` it stores. The slim-index row's `ArtifactPath` must be the RELATIVE form `content-kb/{source-slug}/{video_id}.md` (its doc-comment, line 121–127, says exactly this). The slug + video_id must be sanitized before becoming path segments.

**RECOMMENDATION:**
- **Where it lives:** a new **`ContentArtifactWriter` (or `ContentArtifactEmitter`) in `DeckFlow.Core/Knowledge/`** (beside `ContentArtifactSpec`, which it consumes) OR `DeckFlow.Core/Content/`. Recommend `Knowledge/` since it renders the locked `ContentArtifactSpec` format and is pure (string-build + `File.WriteAllText`), exactly like `DeltaExporter`. Shape:
  - `static string ToText(ContentArtifactMetadata metadata, string summary, IReadOnlyList<(int? TimestampSeconds, string Excerpt)> clips)` — builds the YAML frontmatter + `## Summary` + `## Key Clips` + `## Tags` per `ContentArtifactSpec.ArtifactFileFormat`. Use `ContentArtifactSpec.SerializeTags`/the metadata tag lists for the frontmatter arrays.
  - `static void WriteFile(string artifactRoot, string sourceSlug, string videoId, string text)` → builds `Path.Combine(artifactRoot, sourceSlug, videoId + ".md")`, `Directory.CreateDirectory` on the parent, `File.WriteAllText`. Returns nothing or the relative path.
  - **Compute the RELATIVE path** `content-kb/{source-slug}/{video_id}.md` separately for the slim-index row's `ArtifactPath` (it must pass `ValidateArtifactPath`). The orchestrator passes that relative string to `UpsertRowAsync`.
- **Slug source:** the source's `source_slug` (already computed + stored at add time via `SlugifySourceName.Slugify`). `video_id` is `youtube_video_id` (already URL-safe). Both should still be re-sanitized defensively before path use (the index store will reject `..`/rooted, but the writer should not produce them in the first place).
- **Gitignore (D-06):** there is currently NO `content-kb` entry in `.gitignore` `[VERIFIED: grep returned nothing]`. The planner MUST add `content-kb/` (and/or the artifact path) to `.gitignore`. NOTE: `.gitignore` is on CLAUDE.md's "Do Not Modify Without Explicit Permission" list — the planner must surface this as a `checkpoint:human-verify` / ask the user before editing `.gitignore`. Tag `[ASSUMED]` until confirmed.

**Schema-compat caveat:** None — file I/O only. The slim-index `artifact_path` is stored RELATIVE and validated by the existing store guard.

## Architecture Patterns

### Recommended new files
```
DeckFlow.Core/
├── Integration/
│   ├── ILlmDistillationService.cs       # NEW: pure summary/clips/tags contract
│   └── LlmDistillationService.cs         # NEW: ChatClient + strict schema (AI-SPEC §3/§4); prod ctor + internal test-seam ctor
├── Knowledge/
│   ├── ContentArtifactSpec.cs            # EXISTING — consume
│   ├── ContentTagVocabulary.cs           # EXISTING — filter through IsValid
│   ├── DistillationSchemas.cs            # NEW: 3 BinaryData strict json_schemas (AI-SPEC §4)
│   └── ContentArtifactWriter.cs          # NEW: ToText + WriteFile (DeltaExporter pattern)
├── Content/
│   ├── ILlmSpendLedger.cs                # NEW
│   ├── LlmSpendLedger.cs                 # NEW: mirrors WhisperSpendLedger, token-based cost
│   ├── ContentVideoStore.cs              # EDIT: add ListVideosPendingDistillAsync, GetLatestTranscriptAsync, ClearDistillOutputAsync (+ interface)
│   └── ContentSourceStore.cs             # EDIT: add SetEnabledAsync (+ GetSourceBySlugAsync) (+ interface)
DeckFlow.CLI/
├── Program.cs                            # EDIT: register `distill` verb + `content-source-set-enabled` verb
└── CommandRunners.cs                     # EDIT: RunDistillAsync (public + internal); RunContentSourceSetEnabledAsync; ResolveContentKbArtifactRoot helper
DeckFlow.Core.Tests/
├── LlmDistillationServiceTests.cs        # NEW: inject delegate; parse/refusal/truncation (AI-SPEC E1-E4)
├── LlmSpendLedgerTests.cs                # NEW: record/total/cap
├── ContentArtifactWriterTests.cs         # NEW: ToText format + null-timestamp omission
└── RunDistillAsyncTests.cs               # NEW: skip-completed, failure-isolation, tag-drop (inject fakes via internal overload)
```

### Anti-Patterns to Avoid
- **Editing `RunHarvestAsync` to add distill steps** — D-09: harvest stays transcript-only; distill is a sibling verb. The marker at line 532 means "do it elsewhere," not "extend here."
- **Adding a `transcript_status='distilled'` value** — rejected by the baked-in CHECK on existing DBs (Q2).
- **Adding columns to existing tables** — `CREATE TABLE IF NOT EXISTS` never runs `ALTER`; existing DBs won't get them (Q2/Q4).
- **Letting the OpenAI SDK retry on top of Polly / masking transient failure as success** (AI-SPEC P3/P4, D-12).
- **Putting distill DB writes inside the pure LLM service** — Phase 20 D-11: services pure, orchestrator owns persistence.
- **Writing the slim-index row before the artifact file + child rows** — breaks the derived idempotency invariant (Q2 ordering).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON parse reliability | Custom JSON repair/regex | OpenAI strict `json_schema` (AI-SPEC §3) | <0.1% parse target (P4) |
| Tag validity | Inline string allowlist | `ContentTagVocabulary.IsValid` | Locked Phase 19 contract (D-04) |
| Artifact file format | Bespoke markdown layout | `ContentArtifactSpec.ArtifactFileFormat` + `SerializeTags` | Locked Phase 19 contract (D-07) |
| Spend cap | New cap logic | `LlmSpendLedger` mirroring `WhisperSpendLedger` | Proven ordering (CR-01) |
| Path traversal safety | Manual checks | `ContentSiteIndexStore.ValidateArtifactPath` (already runs on upsert) | Already enforced |
| Async-over-sync bridge | New pattern | `.GetAwaiter().GetResult()` ONLY at the `SetHandler` boundary | Project convention (Program.cs) |

## Runtime State Inventory

> This phase ADDS new local state (artifacts, ledger rows) and depends on existing UAT DB state. Refactor-style inventory:

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | UAT `content-kb.db` has ~10 harvested videos across 5 sources (per MEMORY: 2 sources have no videos). Existing `transcript_status` values are the 6-value baked-in set. `content_transcripts.body` is PLAIN TEXT (no timing). | Distill consumes these; the baked-in CHECK constraint blocks any new `transcript_status` value (Q2). NO data migration if derived-state approach used. |
| Live service config | None — fully local CLI. No Render/n8n/external registration. | None — verified by CONTEXT "Everything runs locally; nothing executes on Render." |
| OS-registered state | None — no scheduled tasks/daemons; operator runs the verb manually. | None. |
| Secrets/env vars | `OPENAI_API_KEY` (existing, used by Whisper) reused for ChatClient. NEW: `DECKFLOW_LLM_MONTHLY_CAP_USD` (Q3). `MTG_DATA_DIR` (existing) consulted for artifact root (Q7). | Document new env var; reuse existing key. No key rename. |
| Build artifacts | None new at build time. RUNTIME: artifact `.md` files under `MTG_DATA_DIR/content-kb/` (gitignored, D-06). | Add `content-kb/` to `.gitignore` — REQUIRES user permission (CLAUDE.md Do-Not-Modify list). |

## Common Pitfalls

### Pitfall 1: Existing-DB CHECK-constraint rejection
**What goes wrong:** Plan adds a `'distilled'` `transcript_status`; the UAT DB's baked-in CHECK rejects the UPDATE, and `IsValidTranscriptStatus` throws first anyway.
**How to avoid:** Use derived distill-completion state (Q2). Never introduce a new enum value or column against the existing DB without a destructive reset.
**Warning signs:** `ArgumentException: Unknown transcript status` or SQLite `CHECK constraint failed`.

### Pitfall 2: Non-idempotent re-run double-writes child rows
**What goes wrong:** Re-distilling a partially-failed video appends duplicate summary/clip rows (and hits the `content_tags` UNIQUE constraint).
**How to avoid:** `ClearDistillOutputAsync(videoId)` before re-distilling, and write the slim-index row LAST so completion is atomic-ish (Q2 ordering invariant).
**Warning signs:** Multiple summary rows per video; `UNIQUE` violation on `content_tags`.

### Pitfall 3: Confidently-wrong timestamps
**What goes wrong:** Model invents `[03:12]` for a plain-text transcript with no timing; flows into the KB as fact (AI-SPEC E4, FM).
**How to avoid:** Model returns `null` (schema `["integer","null"]`); OMIT the `[mm:ss]` prefix in the artifact when null (Q1, D-08).
**Warning signs:** Every clip has a precise timestamp despite a captions-only/plain-text source.

### Pitfall 4: Spend-cap race on parallel videos
**What goes wrong:** Parallel `Task.WhenAll` distill blows past the cap before any `RecordCallAsync` lands.
**How to avoid:** Process videos sequentially (or small bounded `SemaphoreSlim`); cap-check before each call (AI-SPEC §4b).

## Code Examples

The ChatClient construction, strict-schema calls, and `sealed record` result shapes are fully specified in **21-AI-SPEC.md §3 (Entry Point Pattern) and §4 (Core Pattern)** — do not duplicate. Reference those verbatim when planning the `LlmDistillationService`.

In-repo patterns to mirror:
- **Spend ledger:** `WhisperSpendLedger.cs` (cap env var, `WouldExceedCapAsync` before call, FK-parent `EnsureSchemaAsync` ordering).
- **File emit:** `DeltaExporter.cs` (`ToText` + `WriteFile` static pair; caller `Directory.CreateDirectory` then `File.WriteAllText`).
- **CLI two-layer runner + failure isolation:** `CommandRunners.RunHarvestAsync` (lines 446–593) and `HarvestVideoAsync` per-video try/catch-continue.
- **Verb registration:** `content-source-add` in `Program.cs` (lines 59–63, 98–101, 200–203).

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Server-harvest KB | Local CLI + file artifacts + slim index | 2026-05-26 (MEMORY: Content KB local pivot) | Distill runs locally only; nothing on Render |
| Combined LLM call | 3 isolated strict-schema calls | D-02 | Per-call robustness |

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | gpt-4o-mini price ≈ $0.15/1M in, $0.60/1M out | Q3 | Ledger cost off; cap math drifts. Verify at platform.openai.com before locking constants. |
| A2 | New env var name `DECKFLOW_LLM_MONTHLY_CAP_USD`, default ~$5.00 | Q3 | Operator-facing config; user should confirm name + default. |
| A3 | Enable/disable is the MVP subset of "edit/disable"; full name/url edit = add-new + disable-old | Q6 | If user wants in-place edit, scope grows (slug-rename orphaning risk). |
| A4 | `content-kb/` must be added to `.gitignore` (D-06) | Q7 | `.gitignore` is Do-Not-Modify (CLAUDE.md) — needs explicit user permission before edit. |
| A5 | `0`-sentinel + `sort_order` is acceptable for null clip timestamps in `content_clips` (NOT NULL column) | Q1 | If operator wants true null distinction, a schema change (migration-blocked) is needed. |

## Open Questions

1. **Run-type discrimination on `content_harvest_runs`** — overloaded columns mean harvest vs distill runs are indistinguishable from the row alone.
   - What we know: operator knows which verb they ran; AI-SPEC §7 treats the row as a per-run scoreboard.
   - Recommendation: accept for MVP; if Phase 22 reporting needs it, add a `distill_runs` table then (net-new, safe).

2. **Projected LLM cost before the call (for cap-check)** — exact tokens aren't known until after.
   - What we know: AI-SPEC §4 gives an estimate formula (transcript chars/4 × 3 calls + output caps).
   - Recommendation: estimate conservatively for `WouldExceedCapAsync`; record EXACT from `completion.Usage` after.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| OPENAI_API_KEY | ChatClient distillation | operator-set (same key as Whisper) | — | None — distill cannot run without it (mirror Whisper `ReadApiKey` throw) |
| .NET 10 SDK | build/run | ✓ (project baseline) | net10.0 | — |
| `content-kb.db` (UAT) | distill input | ✓ (~10 videos harvested) | — | run `harvest` first |
| ffmpeg | NOT needed by distill (Whisper-only) | n/a | — | — |

**Missing dependencies with no fallback:** `OPENAI_API_KEY` must be set or distill aborts (consistent with the Whisper `InvalidOperationException` at `WhisperTranscriptionService.cs:207`).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (`DeckFlow.Core.Tests`) |
| Config file | none (SDK-style csproj) |
| Quick run command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests --filter "FullyQualifiedName~Distill\|FullyQualifiedName~LlmSpend\|FullyQualifiedName~ContentArtifactWriter"` |
| Full suite command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests` |

> NOTE: VSTest unreliable in WSL (CLAUDE.md). Gate is clean `dotnet build` + targeted run / push-and-watch CI.

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| KB-06 | parse/refusal/truncation classification (E1) | unit | `…--filter LlmDistillationServiceTests` | ❌ Wave 0 |
| KB-07 | out-of-vocab tag dropped + WARN (E2) | unit | `…--filter RunDistillAsyncTests` | ❌ Wave 0 |
| KB-06 | summary ≤200 words (E3) | unit | `…--filter RunDistillAsyncTests` | ❌ Wave 0 |
| KB-06 | clip count 3–8 + null-timestamp omission (E4, Q1) | unit | `…--filter ContentArtifactWriterTests` | ❌ Wave 0 |
| KB-06 | LLM spend record/total/cap (D-05) | unit | `…--filter LlmSpendLedgerTests` | ❌ Wave 0 |
| KB-02/D-10 | skip already-distilled (artifact+index present) | unit | `…--filter RunDistillAsyncTests` | ❌ Wave 0 |
| KB-02/D-12 | per-video failure isolation, batch continues | unit | `…--filter RunDistillAsyncTests` | ❌ Wave 0 |
| KB-01/D-13 | `SetEnabledAsync` toggles, disabled source excluded from run | unit | `…--filter ContentSourceStore` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** quick filtered run above.
- **Per wave merge:** full `DeckFlow.Core.Tests` + clean `dotnet build DeckFlow.sln`.
- **Phase gate:** full suite green + manual UAT against the ~10 UAT videos (E5/E6 human review, AI-SPEC §5).

### Wave 0 Gaps
- [ ] `LlmDistillationServiceTests.cs` — covers KB-06 E1 (inject ChatCompletion delegate via internal test-seam ctor)
- [ ] `LlmSpendLedgerTests.cs` — covers D-05 ledger record/total/cap
- [ ] `ContentArtifactWriterTests.cs` — covers KB-06 E3/E4 + Q1 null-timestamp omission + D-07 format
- [ ] `RunDistillAsyncTests.cs` — covers KB-07 E2 tag-drop, D-10 skip, D-12 isolation (inject fakes via `internal RunDistillAsync` overload, mirror `RunHarvestAsync` test pattern)
- [ ] `ContentSourceStore` enable/disable test — KB-01/D-13
- [ ] `IContentVideoStore` test double update — new methods need fake implementations in `DeckFlow.Core.Tests` fakes

## Security Domain

> `security_enforcement` not configured for this phase; surface is a LOCAL admin-only CLI with no user-facing request path (AI-SPEC §6/§7). Minimal applicable controls:

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | yes | Strict `json_schema` + `ContentTagVocabulary.IsValid` post-filter; `ValidateArtifactPath` rejects traversal |
| V6 Cryptography | no | No new secrets; reuse `OPENAI_API_KEY` env var (never logged) |

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Path traversal via slug/video_id | Tampering | `ContentSiteIndexStore.ValidateArtifactPath` (rooted/`..` rejected) + sanitize before path build |
| Secret leakage (API key in logs) | Information disclosure | Mirror Whisper: read from env, never log; CLAUDE.md secrets rule |
| Uncapped spend (cost DoS) | DoS | `LlmSpendLedger.WouldExceedCapAsync` before each call (D-05/SC5) |

## Sources

### Primary (HIGH confidence)
- In-repo source (read this session): `ContentArtifactSpec.cs`, `ContentTagVocabulary.cs`, `ContentSiteIndexStore.cs`, `ContentHarvestRunStore.cs`, `ContentVideoStore.cs` + `IContentVideoStore.cs`, `WhisperSpendLedger.cs`, `WhisperTranscriptionService.cs`, `YouTubeTranscriptFetcher.cs`, `ContentSourceStore.cs`, `DeltaExporter.cs`, `DeckFlowDatabaseConnectionFactory.cs`, `CommandRunners.cs`, `Program.cs`, `ContentModels.cs`.
- `DeckFlow.Core.csproj` — OpenAI 2.10.0, YoutubeExplode 6.6.0 confirmed.
- `.planning/REQUIREMENTS.md` — KB-01/02/06/07 verbatim.
- `21-CONTEXT.md` (D-01..D-13) and `21-AI-SPEC.md` (SDK depth — deferred to, not re-researched).

### Secondary (MEDIUM confidence)
- `[CITED: github.com/Tyrrrz/YoutubeExplode]` — `ClosedCaption` exposes `Offset` (TimeSpan), `Duration`, `Text`, `Parts` (raw source fetch).
- `[CITED: platform.openai.com/docs/models/gpt-4o-mini]` (via AI-SPEC §4) — pricing.

## Metadata

**Confidence breakdown:**
- Codebase integration (Q2, Q4, Q5, Q6, Q7): HIGH — read directly from current source.
- Caption timing (Q1): HIGH — YoutubeExplode API confirmed + in-repo discard verified.
- Spend ledger / cost (Q3): HIGH on pattern, MEDIUM on prices (drift — A1).

**Research date:** 2026-05-27
**Valid until:** 2026-06-26 (stable codebase; re-verify gpt-4o-mini prices before locking ledger constants)
