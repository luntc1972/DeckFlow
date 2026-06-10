# Phase 19: Content KB Foundation — Local Schema + Contracts - Context

**Gathered:** 2026-05-26
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase delivers the **persistence and contract foundation** for the local Content KB harvester — nothing executable, no outbound HTTP, no runtime behavior. Concretely:

- Local SQLite schema (via per-store `EnsureSchemaAsync`, idempotent `CREATE TABLE IF NOT EXISTS`) for: `content_sources`, `content_videos`, `content_transcripts`, `content_summaries`, `content_clips`, `content_tags`, `whisper_spend_ledger`, `content_harvest_runs`.
- `DeckFlow.Core` distillation record models.
- The **AI-prompt artifact file-format spec** (markdown layout for summary + timestamped clips + tags).
- The **slim site-index schema contract** (the only shape destined for Render Postgres).
- A stubbed cap-check helper for Phase 20 to wire.

Out of scope: any HTTP/ingestion (Phase 20), distillation/emit runtime (Phase 21), site index materialization + browse UI (Phase 22). Architecture (PK strategy, FK cascade, soft-disable, store granularity) was locked in ROADMAP discuss 2026-05-26 and is carried forward, not re-decided here.

</domain>

<decisions>
## Implementation Decisions

### Carried Forward (locked in ROADMAP discuss 2026-05-26 — do NOT re-decide)
- **PK strategy:** integer surrogate (`BIGINT GENERATED ALWAYS AS IDENTITY` / SQLite `INTEGER PRIMARY KEY`); natural keys (`youtube_video_id`, `rss_guid`) get a UNIQUE for harvest dedup.
- **FK behavior:** `ON DELETE CASCADE` on child FKs (transcripts/summaries/clips/tags → videos; videos → sources).
- **Source disable:** soft-disable via `content_sources.is_enabled BOOLEAN NOT NULL DEFAULT true`; harvest filters `WHERE is_enabled`; hard-delete is a separate rare op triggering CASCADE; transcript/spend history survives disable.
- **Store granularity:** ~4 grouped-by-aggregate stores (`ContentSourceStore`, `ContentVideoStore` over videos+transcripts+summaries+clips+tags, `WhisperSpendLedger`, `ContentHarvestRunStore`); each owns its `EnsureSchemaAsync` + own xUnit fixture.

### Store Project Placement
- **D-01:** Distillation **record models** (e.g., `ContentSource`, `ContentVideo`, `ContentTranscript`, `ContentSummary`, `ContentClip`, `ContentTag`, artifact DTO, slim-index row DTO) live in `DeckFlow.Core/Knowledge`. This lets a local CLI/app harvester (`DeckFlow.CLI` → `DeckFlow.Core`) reference them without depending on `DeckFlow.Web`, and honors ROADMAP's "DeckFlow.Core distillation record models."
- **D-02:** The 4 **stores** (with `EnsureSchemaAsync`) live in `DeckFlow.Web/Services/Content`, alongside every existing store (`HarvestRunStore`, `FeatureFlagStore`, `FeedbackStore`). Matches the established "stores live in Web/Services" convention.
- **Records preserve `{ get; init; }`** (System.Text.Json silently skips get-only props — already broke `EdhTop16Client` once). All DDL constants + raw-string literals byte-preserved per CLAUDE.md formatting rule.

### FK Cascade Enforcement (the SQLite landmine)
- **D-03:** Set `PRAGMA foreign_keys=ON` **centrally** when a SQLite connection is opened (inside `RelationalDatabaseConnection.CreateConnection`/open path) so every store inherits enforcement and Postgres is unaffected. `CreateConnection()` opens a fresh connection per call, so the pragma MUST be applied per-connection or cascades silently no-op on SQLite.
- **D-04:** Phase 19 adds a test that proves a cascade **actually fires** on SQLite (delete a video → its transcripts/summaries/clips/tags rows are gone), not just that the DDL declares CASCADE.
- ⚠ **Blast-radius (research before implementing):** enabling FK enforcement globally touches every existing SQLite store (`FeedbackStore`, `CategoryKnowledgeRepository`, `HarvestRunStore`, `HarvestScheduleStore`, etc.). Researcher MUST verify no existing SQLite code relies on FK being OFF (e.g., insert order, orphan rows, self-referential deletes) before flipping the pragma centrally.

### Whisper Spend-Log Shape (KB-05)
- **D-05:** `whisper_spend_ledger` stores **one row per Whisper API call** (`video_id` FK, `seconds_billed`, `cost_usd`, `month_key`, `created_utc`). Full per-video audit trail per KB-04.
- **D-06:** `month_key` is **TEXT `'YYYY-MM'`** (e.g., `'2026-05'`). Human-readable, sorts lexically, identical on SQLite + Postgres, trivial from `DateTime`.
- **D-07:** `IWhisperSpendLedger.GetMonthlyTotalAsync(string yearMonth, ...)` returns the app-side `SELECT SUM(cost_usd) WHERE month_key = @m` aggregate.
- **D-08:** Cap-check helper stubbed for Phase 20: `IWhisperSpendLedger.WouldExceedCapAsync(decimal projectedCallCostUsd, string monthKey, CancellationToken)` → `bool`. Reads cap from config/env `DECKFLOW_WHISPER_MONTHLY_CAP_USD` (default `$15.00`), computes `GetMonthlyTotalAsync + projected`, returns whether to skip. **NO** TOCTOU advisory-lock / SERIALIZABLE / kill-switch machinery.
- **D-09:** The cap value lives in **config/env only** — NOT in the DB. The `skipped_over_cap` marker is a **status/discriminator on `content_videos`** (e.g., a `transcript_status` enum value), NOT a ledger row — the ledger only records calls that actually happened.

### AI-Prompt Artifact File-Format Spec (KB-06)
- **D-10:** Artifact is **markdown + YAML front-matter**. Front-matter block (machine-parseable for slim-index build): `source`, `title`, `url`, `video_id`, `tags` (3 dimensions), `generated_utc`. Body (paste-ready for ChatGPT/Claude/Gemini): `## Summary` (≤200 words target), `## Key Clips` (3–8 `[mm:ss]` timestamped excerpts), `## Tags`. Aligns with DeckFlow's existing markdown prompt-template + Markdig rendering.
- **D-11:** Artifacts written under `MTG_DATA_DIR` at `/data/content-kb/{source-slug}/{youtube_video_id}.md` (or `{rss_guid}` for podcasts). Natural-key filename = dedup-friendly + stable; lives on the persistent disk the app already mounts; keeps generated content out of git.

### Slim Site-Index Schema Contract (KB-08) — the ONLY shape destined for Render
- **D-12:** Columns: `id` (integer surrogate PK), `source`, `title`, `video_url`, `artifact_path` (**relative**, e.g. `content-kb/{slug}/{id}.md`), `published_utc`, `indexed_utc`; UNIQUE on the natural key (`youtube_video_id`/`rss_guid`). The pointer is the **relative artifact path** — portable so Phase 22 resolves it whether the artifact arrives on Render via upload OR commit-then-deploy. Index decoupled from where the file physically lives.
- **D-13:** Tags stored as **three separate columns** — `archetype_tags`, `bracket_tags`, `card_category_tags` (each a delimited/JSON list) — mirroring the 3-dimension `ContentTagVocabulary`. Lets the Phase 22 browse/filter UI filter per-dimension cleanly; YAML front-matter carries the same 3 keys.
- **D-14:** Postgres parity preserved in DDL for the slim-index subset (the only thing that ships to Render). Heavy tables (transcripts/audio/spend) are explicitly **local-only** and NEVER uploaded.

### Data Lifecycle / Retention
- **D-15:** **Raw video/audio is never stored.** YouTube: YoutubeExplode fetches caption text only — the video file is never downloaded. Podcast audio: fetched transiently for Whisper, then discarded — there is no audio table in the schema.
- **D-16:** **Transcripts are deliberately retained locally** (`content_transcripts`) as a **re-distill cache** — so re-distillation (better prompt, more clips, fixed tags) never re-pays Whisper or re-fetches captions, honoring the spend-cap intent. Heavy + local-only, never uploaded to Render. (No prune helper this phase; can be added later if disk reclaim is ever needed.)

### Claude's Discretion
- Exact column types/nullability beyond those specified, index choices, and the precise dual Postgres/SQLite DDL constant text (follow the `HarvestRunStore` dual-constant pattern).
- Exact `transcript_status`/source-discriminator enum value names (planner/researcher to align with KB-04 `source` discriminator + `skipped_over_cap`).
- Interface naming/namespacing within the `DeckFlow.Core/Knowledge` + `DeckFlow.Web/Services/Content` placement decided above.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope + requirements
- `.planning/ROADMAP.md` §"Phase 19: Content KB Foundation" (lines ~169–192) — goal, locked architecture decisions, 6 success criteria.
- `.planning/REQUIREMENTS.md` KB-01..KB-09 (lines ~29–37) + KB-section pivot note + traceability table (lines ~94–106) — repurposed local-harvester meanings.
- `.planning/STATE.md` §"Content KB Pivot (2026-05-26)" — architecture-shift rationale + SQLite CASCADE landmine note.

### Pattern donors (existing code to mirror)
- `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` (esp. lines ~436–471) — the `EnsureSchemaAsync` + dual Postgres/SQLite DDL-constant + `SemaphoreSlim` schema-gate pattern to mirror for the new stores.
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` — store shape `HarvestRunStore` itself mirrors; test-seam ctor pattern.
- `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` — where `PRAGMA foreign_keys=ON` (D-03) must be applied; `FromSqlitePath`, `CreateConnection`, `AddParameter` helpers.
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — existing `DeckFlow.Core` store-style consumer of `RelationalDatabaseConnection` (per-connection `CreateConnection` usage; FK-pragma blast-radius candidate).
- `DeckFlow.Web/Services/FeatureFlags/IFeatureFlagStore.cs` + `FeatureFlagGateAttribute.cs` — for the Phase 22 `content_kb_enabled` flag (KB-09), not built here but the contract surface exists.

### Distillation reliability
- `PITFALLS.md` P4 (referenced by KB-06) — OpenAI Structured Outputs `strict: true`, <0.1% parse-failure target. *(Researcher: confirm file path — likely under `.planning/` research artifacts; locate before Phase 21.)*

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `RelationalDatabaseConnection` (`DeckFlow.Core/Storage`): provider-pluggable SQLite/Postgres handle with `FromSqlitePath`, `CreateConnection`, `AddParameter`. All new stores route through it. Central home for the FK pragma (D-03).
- `HarvestRunStore` dual-DDL-constant + schema-gate pattern: copy verbatim structure for the 4 content stores (one `PostgresCreateTableSql` + one `SqliteCreateTableSql` const per table group, `SemaphoreSlim _schemaGate`, `volatile bool _schemaReady`).
- Markdig (already a dependency, used by `HelpContentService`): available for Phase 22 artifact rendering — the markdown+front-matter artifact format (D-10) is chosen for compatibility with it.
- `IFeatureFlagStore` / `FeatureFlagGateAttribute`: the gating mechanism for KB-09 `content_kb_enabled` (Phase 22), already present.

### Established Patterns
- One public type per file; interface + impl + result records often co-located; `sealed record` for immutable DTOs with `{ get; init; }`.
- Stores expose a SQLite-path ctor (test seam) + a `RelationalDatabaseConnection` ctor; tests use own SQLite file or `:memory:` per fact scope (F-PROD-CONTRACT 999.6 lesson).
- Dual Postgres/SQLite DDL with type mapping: `UUID`→`TEXT`, `TIMESTAMPTZ`→`TEXT`, `now()`→`datetime('now')`, `BIGINT IDENTITY`→`INTEGER PRIMARY KEY`.

### Integration Points
- New stores register in `Program.cs` DI (lifetimes per existing store conventions) — wiring is Phase 20/21, but the registration surface is `Program.cs:50-189`.
- Slim-index DDL (D-12/D-14) is the only schema that later runs `EnsureSchemaAsync` against **Render Postgres** (Phase 22); everything else is SQLite-local.
- `DeckFlow.CLI` will reference the `DeckFlow.Core` record models (D-01) for the local harvester — Core must not pull Web.

</code_context>

<specifics>
## Specific Ideas

- User explicitly confirmed the data-lifecycle intent: raw video/audio gone after analysis; transcript retained as a re-distill cache to protect the Whisper spend cap (D-15/D-16).
- "Slim" is a hard constraint on the Render index — resist normalizing tags into a join table (rejected during discussion); per-dimension columns chosen to stay slim while keeping per-dimension filtering.
- Artifact body must stay paste-ready for ChatGPT/Claude/Gemini in one round-trip (Core Value) — front-matter is metadata-only, never bleeds into the pasteable prompt body.

</specifics>

<deferred>
## Deferred Ideas

- **Transcript prune/disk-reclaim helper** — considered (option "Keep + prune helper"); deferred. Retain-by-default chosen; add a CASCADE-safe manual prune later only if local disk becomes a real constraint.
- **DB-stored cap value (runtime override without redeploy)** — rejected for KB-05's "plain local" single-user model; cap stays config/env.
- **ContentTagVocabulary exact value lists, transcript source-discriminator enum, content_videos status enum** — surfaced as possible further gray areas; not deep-dived. Allowlist values are sketched in KB-07 (archetype ~15, Wizards Feb-2025 5-bracket, card_category); planner/researcher finalize exact lists when defining `static class ContentTagVocabulary`.

</deferred>

---

*Phase: 19-content-kb-foundation-local-schema-contracts*
*Context gathered: 2026-05-26*
