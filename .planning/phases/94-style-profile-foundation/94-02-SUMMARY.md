---
phase: 94-style-profile-foundation
plan: 02
status: complete
requirements: [CS-02, CS-03]
executor: codex (gpt-5.4)
reviewer: claude
---

# Plan 94-02 Summary — dialect-guarded CreatorStyleProfileStore

## What was built

The CS-02 store persisting a `CreatorStyleProfile` as a single flat row per slug in a new `creator_style_profile` table, mirroring `ContentSiteIndexStore`. Contracts land before implementation. No DI/controller wiring (deferred to P95, first consumer).

- **`DeckFlow.Core/Content/ICreatorStyleProfileStore.cs`** — `EnsureSchemaAsync` / `UpsertAsync` / `GetBySlugAsync`, all async, `CancellationToken` last, xmldoc.
- **`DeckFlow.Core/Content/CreatorStyleProfileReadModel.cs`** — three-part: Dapper read-model (8 `{ get; init; }` props, underscore-matched to snake_case), `CreatorStyleProfileReadColumns.SelectList` const (8 cols in order), `CreatorStyleProfileMapper.ToProfile` mapping scalars + `DeserializeSection<StatedRule|MeasuredMetric|FusedTarget>` for the 3 section columns.
- **`DeckFlow.Core/Content/CreatorStyleProfileStore.cs`** — `public sealed class : ICreatorStyleProfileStore`. Three public ctors + `internal` test-seam ctor `(RelationalDatabaseConnection, bool, Func<CancellationToken, Task<DbConnection>>?)`. Double-checked `_schemaGate` schema init; `if (!_ensureSchemaEnabled) return;` prod no-op precedes `_schemaReady` fast-path; NO GetTableColumns/ALTER backfill (brand-new table). Dapper-parameterized ON CONFLICT(slug) upsert; sections serialized via `CreatorStyleProfileSections.SerializeSection`.

## Decisions / locked overrides honored

- D-01 single flat JSON-blob table; D-02 column shape; D-04 ON CONFLICT(slug) single-row overwrite refreshing updated_utc; D-06 insufficient_sample persisted; D-07 empty section → NULL → empty list on read.
- **Locked override 1**: section columns `TEXT NULL` on BOTH dialects — NO `jsonb` (grep-confirmed 0). Avoids the Npgsql cast-trap class (F-51-PG-01 family).
- **Locked override 2**: `updated_utc` = `TIMESTAMPTZ` (Postgres) / `TEXT` (SQLite); only written/SELECTed, never filtered. `GetBySlugAsync` filters on `slug` (TEXT both dialects).

## DDL

- Postgres: `slug TEXT PK, platform TEXT NOT NULL, min_decks INTEGER NOT NULL, insufficient_sample BOOLEAN NOT NULL DEFAULT FALSE, *_json TEXT NULL, updated_utc TIMESTAMPTZ NOT NULL DEFAULT now()`.
- SQLite: same shape with `insufficient_sample INTEGER NOT NULL DEFAULT 0`, `updated_utc TEXT NOT NULL DEFAULT (datetime('now'))`.
- UPSERT updates all 7 non-PK columns from EXCLUDED.

## Verification

- `dotnet build DeckFlow.Core -c Debug`: 0 errors, 0 warnings.
- Scope: exactly the 3 intended files (`git diff --name-only 4c6b9a1f..HEAD`); unrelated `.planning` deletions untouched; AssemblyInfo InternalsVisibleTo not re-added.
- LF (0 CRLF). No `jsonb`. ON CONFLICT(slug) present. 3 SerializeSection call sites. Prod no-op ordered before `_schemaReady`. No GetTableColumns/ALTER loop.
- Threat model: all SQL Dapper-parameterized (T-94-02 mitigated); DDL gated by `ensureSchemaEnabled` (T-94-03).

## Commits

- `7f0c6435` feat(94): add ICreatorStyleProfileStore contract + read-model/mapper
- `97780da3` feat(94): add dialect-guarded CreatorStyleProfileStore (ON CONFLICT slug upsert)

## Enables

94-03 (tests): SQLite + Postgres round-trip tests exercise EnsureSchema/Upsert/GetBySlug incl. re-upsert overwrite, below-floor insufficient_sample, and NULL-section → empty-list fidelity.

## Self-Check: PASSED
