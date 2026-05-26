---
phase: 19
reviewers: [codex]
reviewed_at: 2026-05-26T14:00:00-06:00
plans_reviewed: [19-01-PLAN.md, 19-02-PLAN.md, 19-03-PLAN.md, 19-04-PLAN.md]
---

# Cross-AI Plan Review — Phase 19

> Reviewer selection: gemini missing, claude skipped (self — running inside Claude Code CLI).
> Codex is the authoritative plan reviewer per project CLAUDE.md cross-AI policy.

## Codex Review

**Overall Summary**
The plans are detailed and mostly aligned with Phase 19's "persistence + contracts only" boundary. The sequencing around `19-01` and `19-02` is sound, but I would not execute as-is: there are a few contract-level gaps that will be expensive later, especially podcast/RSS identity, parent-table ordering for FK DDL, heavy local-only stores accidentally having a Render/Postgres path, and verification commands that build tests but do not run them.

**Cross-Plan Concerns**
- **HIGH:** `19-04` should depend on `19-03`. `whisper_spend_ledger.video_id REFERENCES content_videos(id)` cannot be safely built/tested before `content_videos` exists, especially for Postgres DDL.
- **HIGH:** Heavy local-only stores get DI constructors using `CreateContentKbConnection`, which can route to Postgres in production. That conflicts with "transcripts/audio/spend are NEVER uploaded to Render." Split factory paths: local-heavy content DB vs Render-bound site-index DB.
- **HIGH:** Verification uses `dotnet build` only. The phase success criteria require tests passing with `Failed:0`; build-only proves compile, not behavior. Also `dotnet build ... | grep ...` can mask failure exit codes unless `pipefail` is set.
- **MEDIUM:** Several cross-plan discriminator strings are still re-spelled in DDL plans. Add shared constants for source type and tag dimensions, not just transcript source/status.

**19-01 Risk: MEDIUM**
- HIGH: `ContentArtifactMetadata` only models `VideoId`; podcast artifacts need `rss_guid` or a generic natural key (conflicts with D-11's `{rss_guid}` path).
- MEDIUM: No `ContentSourceType` / `ContentTagDimension` constants, so Plan 03/04 must re-spell DB CHECK values.
- LOW: Artifact spec tests only check broad headers; should assert required front-matter keys + key order.
- Suggestions: add nullable `YoutubeVideoId`+`RssGuid` (or `NaturalKeyKind/Value`) to artifact metadata; add `ContentSourceType`/`ContentTagDimension` constants; strengthen artifact tests.

**19-02 Risk: MEDIUM**
- HIGH: `CreateContentKbConnection` routes to Postgres when env says Postgres — fine for `content_site_index`, unsafe for heavy local-only tables.
- MEDIUM: `OpenConnectionAsync` can leak an opened connection if the pragma command fails unless it disposes on exception.
- MEDIUM: Helper is "central" only if stores use it; future code can still call `CreateConnection()+OpenAsync()` and skip FK enforcement.
- Suggestions: split factory (`CreateLocalContentKbConnection` vs `CreateContentSiteIndexConnection`); wrap open/pragma in try/catch + dispose before rethrow; add XML docs warning on `CreateConnection()`.

**19-03 Risk: MEDIUM-HIGH**
- HIGH: `ContentVideoStore` FK to `content_sources`; Postgres requires parent table to exist first — needs explicit source schema bootstrap ordering.
- HIGH: CASCADE proof is partly optional for summaries/clips/tags; success criteria require proving ALL child tables cascade, not just transcripts.
- MEDIUM: `content_sources.source_slug` used in artifact paths but not planned UNIQUE → slug/path collision.
- MEDIUM: `content_videos` lacks CHECK `youtube_video_id IS NOT NULL OR rss_guid IS NOT NULL`.
- Suggestions: require/call source schema creation first (or document+test bootstrap order); CASCADE test inserts transcript+summary+clip+tag then asserts all four gone; add `UNIQUE(source_slug)`; add natural-key presence CHECK.

**19-04 Risk: HIGH**
- HIGH: Missing dependency on `19-03`; `WhisperSpendLedger` depends on `content_videos`.
- HIGH: SQLite `SUM(cost_usd)` over TEXT coerces numerically, may not preserve exact decimal. Store integer micros, or read rows + sum app-side with `decimal.Parse`.
- HIGH: `ContentSiteIndexStore` upsert on `youtube_video_id` OR `rss_guid` underspecified — two nullable unique keys need separate conflict paths or normalized natural-key pair.
- MEDIUM: Artifact path documented relative but not validated on write — reject rooted paths and `..`.
- MEDIUM: Tag columns `TEXT` but serialization (JSON vs delimiter) not locked — lock now for Phase 22 filtering.
- Suggestions: add `19-03` to depends_on (or split ledger); integer micro-dollars or app-side decimal sum; define natural key as `natural_key_type+natural_key_value` or separate upsert SQL; validate `artifact_path`; choose JSON arrays + document canonical serialization.

---

## Consensus Summary

Single authoritative reviewer (Codex). No second CLI available this run.

### Agreed Strengths
- Phase-boundary discipline (persistence + contracts only, no runtime/DI behavior).
- `{ get; init; }` preservation; parameterized queries; dual DDL; CASCADE proof present.
- Correct rejection of advisory-lock/SERIALIZABLE/kill-switch complexity (D-08).

### Agreed Concerns (priority — HIGH first)
1. **FK dependency ordering:** `19-04` (whisper_spend_ledger) and the cross-store `content_videos → content_sources` FK both need correct parent-table/plan ordering. 19-04 must depend on 19-03; ContentVideoStore must bootstrap content_sources first.
2. **Data-locality leak:** heavy local-only stores must NOT be reachable via a Postgres/Render connection path. Split the factory: local-heavy vs Render-bound slim index. (Protects D-14 invariant.)
3. **Podcast identity gap:** artifact metadata + site-index natural key model only `youtube_video_id`; D-11/D-12 require `rss_guid` too.
4. **Money precision:** `cost_usd` SUM over TEXT loses decimal exactness — lock storage type now.
5. **Verification rigor:** build-only proves compile not behavior; `| grep` masks exit codes without `pipefail`.
6. **Lock-now contract details:** tag-column serialization (JSON vs delimiter), shared source-type/tag-dimension constants, artifact-path validation, UNIQUE(source_slug), natural-key presence CHECK.

### Divergent Views
None — single reviewer.

### Triage note (orchestrator)
Concerns 1–4 are genuine contract-level defects the Claude plan-checker (PASS) missed. Concern 5 partially collides with the project constraint "VSTest unreliable in WSL → build clean + push-and-watch CI" (CLAUDE.md) — the `pipefail`/exit-code-masking sub-point is valid regardless; the "must run tests locally" sub-point is satisfied via CI, not local run. All HIGH items are block-worthy → replan via `/gsd:plan-phase 19 --reviews` before execution.
