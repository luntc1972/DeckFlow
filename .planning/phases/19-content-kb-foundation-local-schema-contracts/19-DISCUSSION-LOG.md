# Phase 19: Content KB Foundation — Local Schema + Contracts - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-26
**Phase:** 19-content-kb-foundation-local-schema-contracts
**Areas discussed:** Store project placement, Whisper spend-log shape, AI-prompt artifact file-format, Slim-index columns, Data lifecycle / transcript retention

---

## Store Project Placement

| Option | Description | Selected |
|--------|-------------|----------|
| Records Core, stores Web | Pure distill records in `DeckFlow.Core/Knowledge`; 4 stores + `EnsureSchemaAsync` in `DeckFlow.Web/Services/Content`. CLI can reference Core without Web. | ✓ |
| Everything in Core | Records AND stores in Core. Breaks "stores live in Web" convention. | |
| Everything in Web | Both in Web. Contradicts ROADMAP "distill models in Core"; CLI couldn't reference without Web dep. | |

**User's choice:** Records Core, stores Web
**Notes:** Honors ROADMAP + lets a local CLI harvester reference Core records.

## FK Cascade Enforcement (SQLite landmine)

| Option | Description | Selected |
|--------|-------------|----------|
| Set in RelationalDatabaseConnection | `PRAGMA foreign_keys=ON` centrally per opened SQLite connection; all stores inherit; Postgres unaffected. Add cascade-fires test. | ✓ |
| Each store sets it | Localized pragma per store; easy to forget on a future store. | |
| Document only, defer | DDL declares CASCADE but pragma deferred — leaves landmine armed. | |

**User's choice:** Set in RelationalDatabaseConnection
**Notes:** Flagged blast-radius — central change touches every existing SQLite store; researcher must verify none rely on FK being OFF.

## Whisper Spend-Log Shape

| Option | Description | Selected |
|--------|-------------|----------|
| Per-call rows + SUM | One row per Whisper call; `GetMonthlyTotalAsync` = `SUM(cost_usd) WHERE month_key`. Full audit trail. | ✓ |
| Per-call + monthly rollup row | Adds denormalized monthly total; write-time consistency burden, overkill single-user. | |
| Monthly buckets only | One row per month; loses per-video audit trail. | |

**User's choice:** Per-call rows + SUM

| Option (month_key) | Description | Selected |
|--------|-------------|----------|
| TEXT 'YYYY-MM' | Readable, lexical sort, identical SQLite/Postgres. | ✓ |
| Derive from created_utc | No column; pushes date-math/timezone into every query. | |
| INT YYYYMM | Compact but less readable, needs conversion. | |

**User's choice:** TEXT 'YYYY-MM'

| Option (cap helper) | Description | Selected |
|--------|-------------|----------|
| WouldExceedCapAsync(estCost) | `bool` helper reading env cap + projected total; Phase 20 calls before each Whisper call. | ✓ |
| Return remaining budget | Caller does cap-vs-projected comparison; duplicates logic. | |
| Just expose total, defer helper | Cap comparison entirely Phase 20; SC6 not proven in Phase 19. | |

**User's choice:** WouldExceedCapAsync(estCost)

| Option (cap config) | Description | Selected |
|--------|-------------|----------|
| Config/env only + video status | Cap in env; `skipped_over_cap` as `content_videos` status; ledger records only real calls. | ✓ |
| Cap stored in DB | Settings table for runtime override; contradicts "plain local". | |

**User's choice:** Config/env only + video status

## AI-Prompt Artifact File-Format

| Option | Description | Selected |
|--------|-------------|----------|
| Markdown + YAML front-matter | Machine-parseable front-matter + paste-ready markdown body. Works with Markdig. | ✓ |
| Pure markdown | Metadata as header table; index builder must parse prose — fragile. | |
| JSON sidecar + markdown | Separate metadata JSON; doubles files, sync burden. | |

**User's choice:** Markdown + YAML front-matter

| Option (location) | Description | Selected |
|--------|-------------|----------|
| /data, slug-by-video-id | `/data/content-kb/{slug}/{video_id}.md`; persistent disk, dedup-friendly, out of git. | ✓ |
| Repo-committed | Diffable in git but bloats repo, couples content to deploys. | |
| Configurable root | Path from config; Phase 19 defines path-builder only. | |

**User's choice:** /data, slug-by-video-id

## Slim-Index Columns

| Option | Description | Selected |
|--------|-------------|----------|
| Pointer = relative artifact path | `artifact_path` relative; portable for upload OR commit-then-deploy on Render. | ✓ |
| Pointer = inline artifact body | Stores markdown in a column; uploads content to Render (brushes KB-08). | |
| Pointer = external URL | Points at repo raw/public host; couples display to external reachability. | |

**User's choice:** Pointer = relative artifact path

| Option (tags) | Description | Selected |
|--------|-------------|----------|
| Separate columns per dimension | `archetype_tags`/`bracket_tags`/`card_category_tags`; clean per-dimension filter. | ✓ |
| Single tags blob | One JSON column; every filter must unnest. | |
| Normalized join table | Most queryable but a second Render table — heavier than "slim". | |

**User's choice:** Separate columns per dimension

## Data Lifecycle / Transcript Retention

| Option | Description | Selected |
|--------|-------------|----------|
| Keep (re-distill cache) | Retain transcripts locally so re-distill never re-pays Whisper; raw video/audio still discarded. | ✓ |
| Delete after artifact | Drop transcript once artifact exists; re-distill re-pays Whisper, loses audit. | |
| Keep + prune helper | Retain by default + manual CASCADE-safe prune; extra Phase-19 surface. | |

**User's choice:** Keep (re-distill cache)
**Notes:** Raised by user as a clarifying question ("after a video is analyzed, is it not needed/stored anymore?"). Clarified: raw YouTube video never downloaded (captions only); podcast audio transient for Whisper then discarded; transcript deliberately kept as re-distill cache to protect the spend cap.

---

## Claude's Discretion

- Exact column types/nullability beyond those specified, index choices, precise dual Postgres/SQLite DDL constant text (follow `HarvestRunStore` pattern).
- Exact `transcript_status`/source-discriminator enum value names.
- Interface naming/namespacing within the decided Core-records / Web-stores placement.

## Deferred Ideas

- Transcript prune/disk-reclaim helper — retain-by-default chosen; add later only if disk becomes a constraint.
- DB-stored cap value — rejected for "plain local" model.
- `ContentTagVocabulary` exact value lists, transcript source-discriminator enum, `content_videos` status enum — not deep-dived; finalize during planning/research.
