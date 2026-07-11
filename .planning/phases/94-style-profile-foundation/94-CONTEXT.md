# Phase 94: Style-Profile Foundation - Context

**Gathered:** 2026-07-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Persist a creator style-profile schema — stated rules, measured metrics, fused targets — behind a dialect-guarded (SQLite + Postgres) store keyed by creator source slug. **Pure substrate: no UI, no crawler, no distiller, no fusion logic yet.** Delivers the record types (CS-01), the store + DDL (CS-02), the min-deck floor + insufficient-sample marking (CS-03), and round-trip tests on both dialects (CS-04). Unlocks every downstream Cycle 17 phase.

**Out of scope (later phases):** measured extraction (P95), stated distillation (P96), fusion/conflict ledger logic (P97), card-grounding guard (P98), artifact engine (P99), tool page/flag (P100).

</domain>

<decisions>
## Implementation Decisions

### Persistence shape
- **D-01:** **JSON-blob columns, single flat table** `creator_style_profile`. One row per creator slug. Scalar meta columns + one JSON column per nested array section. Chosen over normalized child tables because nothing in CS-01..04 (or downstream P97 fusion / P99 rubric, which load the *whole* profile at once) needs per-metric SQL query; JSON-blob minimizes dialect-guarded DDL and gives trivial round-trip.
- **D-02:** Column shape:
  - `slug TEXT` (PK) — reuse `SlugifySourceName` (`DeckFlow.Core/Content/SlugifySourceName.cs`)
  - `platform TEXT`
  - `min_decks INT` — the deck count the profile was computed from (auditable at compute-time)
  - `insufficient_sample` — bool (Postgres `boolean` / SQLite `INTEGER` 0/1 per dialect)
  - `stated_rules_json` — nullable (Postgres `jsonb` / SQLite `TEXT`)
  - `measured_metrics_json` — nullable
  - `fused_targets_json` — nullable
  - `updated_utc` — Postgres `timestamptz` / SQLite `TEXT` (dialect-guarded; see F-51-PG-01 lesson — TEXT-vs-timestamptz cast trap on Npgsql)
- **D-03:** Section arrays serialized whole via **System.Text.Json**. CLAUDE.md carve-out applies — records MUST use `{ get; init; }` (never `{ get; }`); System.Text.Json silently skips get-only properties in .NET 9+ (has broken `EdhTop16Client` before). Guarded by `CarveOutGuard` test.

### Profile versioning
- **D-04:** **Overwrite, single row per slug (UPSERT).** Each recompute (P95 measured, P97 fusion) UPSERTs the current profile; `updated_utc` tracks freshness. Matches `ContentSiteIndexStore` upsert semantics. Say-vs-do drift-over-time history is an explicit later-cycle concern, NOT MVP substrate. Downstream loads current only.

### Min-deck floor + insufficient behavior
- **D-05:** **Floor constant = 5**, matching EDHREC's threshold cited in the research report. Named const in Core (e.g. `CreatorStyleProfile.MinDeckFloor = 5`). Persisted on the row (`min_decks`) so the compute-time deck count is auditable.
- **D-06:** **Persist + flag** below the floor — store the profile normally with `insufficient_sample = true`. Nothing lost; downstream (P97/P99) decides whether to trust or warn. Matches CS-03 "marked, not silently trusted". Store never refuses/throws on low count. Round-trip test asserts the flag survives write→read.

### Partial-profile shape
- **D-07:** **Nullable section columns + empty-array reads.** Each `*_json` column is independently nullable so a measured-only (P95 lands before P96/P97) or stated-only profile round-trips cleanly. A missing section reads back as an **empty `IReadOnlyList<T>` (never null)** per the house "return `Array.Empty` not null" convention. No presence-flag columns — emptiness IS the signal. Round-trip tests cover measured-only, stated-only, and fully-fused cases.

### Record field shapes (from CS-01, locked)
- **D-08:** Exact record fields per CS-01 (no additions this phase):
  - `StatedRule{ category, targetMetric, targetValue, comparator, sourceClip, confidence }`
  - `MeasuredMetric{ metric, value, numDecks, distribution }`
  - `FusedTarget{ metric, value, weight, source, conflict? }`
  - `distribution` (on MeasuredMetric) and `conflict?` (on FusedTarget) are themselves nested objects — serialized inside the section JSON, not broken into columns (consequence of D-01). Their internal shape is defined here as minimal substrate; P95/P97 may extend the nested record but MUST keep the CS-01 top-level field names.

### Claude's Discretion
- Exact DDL text, `ensureSchema` gate wiring, and the test-seam `connectionFactoryOverride` ctor — mirror `ContentSiteIndexStore` verbatim (Dapper + `RelationalDatabaseConnection`, schema-gate `SemaphoreSlim`, `ensureSchemaEnabled` flag for prod-pointed no-op stores).
- Interface method surface on `ICreatorStyleProfileStore` (at minimum: upsert/save + get-by-slug; planner decides exact signatures) — async, `CancellationToken` last param.
- Namespace: `DeckFlow.Core.Knowledge` per CS-01 (records) — store may live in `Content` or `Knowledge`; planner picks to match the closest analog (`ContentSiteIndexStore` is in `Content`).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Cycle / requirements
- `.planning/REQUIREMENTS.md` §Style-Profile Foundation — CS-01..CS-04 (locked field names, store contract, floor, tests)
- `.planning/ROADMAP.md` §"Phase 94" + §"Design Stance (Cycle 17)" — numeric-targets-not-prose stance, `numDecks`/confidence-next-to-every-stat, min-deck floor, dialect-guarded
- `docs/research/creator-style-roadmap.md` — source of the min-deck-floor (EDHREC ≥5) and the locked phase arc

### Store / persistence pattern to mirror
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — **the migration pattern to copy** (Dapper, `RelationalDatabaseConnection`, schema-gate, `ensureSchemaEnabled` no-op for prod, internal test-seam ctor)
- `DeckFlow.Core/Content/SlugifySourceName.cs` — creator slug key (reuse, do not re-invent)
- `DeckFlow.Core/Content/CreatorSourceStore.cs` — existing creator-source store (reference for slug conventions; NOT the storage target for profiles)
- `DeckFlow.Core/Storage/IRelationalDialect.cs`, `SqliteRelationalDialect.cs`, `PostgresRelationalDialect.cs`, `RelationalDatabaseConnection.cs` — dialect seam

### House carve-outs / lessons (MUST honor)
- `CLAUDE.md` §Constraints — `{ get; init; }` carve-out (System.Text.Json get-only skip), LF line endings, `CarveOutGuard` test
- Postgres timestamp trap: `updated_utc` needs the dialect-guarded `::timestamptz` cast on PG (prior F-51-PG-01 bug: TEXT-vs-timestamptz Npgsql 42883)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ContentSiteIndexStore` — near-exact template for the new store's structure (schema-gate, dialect-guard, Dapper round-trip, test-seam ctor).
- `SlugifySourceName` — profile key; reuse directly.
- `RelationalDatabaseConnection` (sealed record) + `IRelationalDialect` (Sqlite/Postgres) — the persistence seam; `FromSqlitePath` helper exists.
- `DeckFlow.Core/Knowledge/` — target namespace for the CS-01 record set (sits beside `CategoryKnowledgeRepository`, `ContentModels`, `DistillationSchemas`).

### Established Patterns
- Dapper-based stores with a `SemaphoreSlim` schema gate + `volatile bool _schemaReady`; `ensureSchemaEnabled=false` makes prod-pointed stores never issue CREATE/ALTER/DROP.
- Test seam: internal ctor taking `Func<CancellationToken, Task<DbConnection>>? connectionFactoryOverride`; public ctors pass null. `[InternalsVisibleTo]` grants test access.
- Records = `sealed record` with `{ get; init; }` / `required`; `IReadOnlyList<T>` on collection surfaces; `Array.Empty<T>()` (never null) for empty.
- Testing framework for `DeckFlow.Core.Tests` = **xUnit** (`[Fact]`/`[Theory]`). Postgres round-trip tests gate on `DECKFLOW_POSTGRES_TESTS=1` + local Docker/Testcontainers (per Cycle 16 SYNC-16 pattern).

### Integration Points
- New store registered in DI (`DeckFlow.Web/Program.cs`) later — but P94 is substrate; registration may land here or defer to the first consuming phase (planner decides; no controller wiring this phase).
- No `iPD.txt`/`iCX.txt` DB-push files in this repo (that rule is for vtrans/nhdot/txdot) — schema ships via the store's `EnsureSchema`, mirroring ContentSiteIndex.

</code_context>

<specifics>
## Specific Ideas

- Prefer copying `ContentSiteIndexStore` shape wholesale over designing a fresh store — consistency + the prod-safe `ensureSchemaEnabled` no-op is already proven.
- Keep the nested `distribution` / `conflict?` shapes minimal this phase; P95/P97 extend them but must preserve CS-01 top-level field names so the JSON stays forward-compatible.

</specifics>

<deferred>
## Deferred Ideas

- **Versioned profile history / say-vs-do drift over time** — considered for D-04, deferred to a later cycle. MVP overwrites per slug.
- **Per-metric SQL queryability** (normalized tables) — rejected for D-01; revisit only if a future phase needs to query profiles by individual metric server-side.
- **DI registration + first real consumer** — belongs to P95+ (measured extractor) once there's something to store.

</deferred>

---

*Phase: 94-Style-Profile-Foundation*
*Context gathered: 2026-07-11*
