# Phase 49: Dapper Data-Access Adoption — Specification

**Created:** 2026-06-14
**Ambiguity score:** 0.13 (gate: ≤ 0.20)
**Requirements:** 6 locked

## Goal

The dual-provider store classes use Dapper's `QueryAsync`/`ExecuteAsync` for row mapping and parameter binding instead of hand-written `DbCommand` + `ExecuteReaderAsync` loops, behind the unchanged `IRelationalDialect`/`RelationalDatabaseConnection` abstraction, with Sqlite⇄Postgres parity preserved by a small fixed set of provider-aware Dapper type handlers — gated by a `FeedbackStore` spike that must meet an objective "zero per-store mapping" bar before the full sweep proceeds.

## Background

DeckFlow has no ORM. Data access is raw ADO.NET: 16 store/repository classes (~6,293 LOC, ~90 SQL methods) each construct `DbCommand` objects via `RelationalDatabaseConnection.CreateConnection()`/`OpenConnectionAsync()`, set `CommandText`, bind params through the static `RelationalDatabaseConnection.AddParameter` helper (100% adoption, all named `@p`, no positional), and hand-map rows in `ExecuteReaderAsync` loops.

The dual-provider design is what makes mapping non-trivial: the SQLite path stores `DateTime`→ISO-8601 text, `decimal`→text, `bool`→`int` (1/0), and `Guid`→text, while Postgres uses native types. Eight files contain hand-written reader-coercion to convert these back on read, plus matching bind-time formatting on write. Dapper's default mapper does **not** perform this coercion and will throw on SQLite TEXT→`decimal` and INT→`bool`. Therefore Dapper adoption is only viable if a small set of registered `SqlMapper.TypeHandler<T>` instances (provider-aware) absorbs all four coercions globally — replacing the per-store reader loops rather than merely relocating them.

Two patterns resist Dapper and are excluded by design: `RequestMetricsStore.UpsertBatchAsync` binds `NpgsqlParameter` unnest arrays directly (no Dapper equivalent), and every embedded DDL/schema-init/migration method (12 files: `CREATE TABLE`/`INDEX`, `ALTER TABLE ADD COLUMN`, grandfather backfills, FK ordering) is schema management that Dapper does not improve and that the project's immutable-migration rule protects.

This phase is independent of the v1.7 publish-studio track (Phases 41–48); it touches only the `DeckFlow.Core` and `DeckFlow.Web` store layer.

## Requirements

1. **Dapper dependency**: The `Dapper` package is referenced where the stores live.
   - Current: No ORM/micro-ORM package in the solution; only `Microsoft.Data.Sqlite` 10 and `Npgsql` 10
   - Target: `Dapper` (latest stable) referenced by `DeckFlow.Core` (and transitively available to `DeckFlow.Web` stores); no other new package added
   - Acceptance: `DeckFlow.Core.csproj` contains a `Dapper` `<PackageReference>`; `dotnet build DeckFlow.sln` succeeds 0 errors / 0 new warnings; no second new package appears in any csproj

2. **Provider-aware type handlers**: A fixed, small set of Dapper type handlers round-trips the coerced types on both providers.
   - Current: Coercion is hand-written per-store in ~8 files (DateTime/decimal/bool/Guid/DateTimeOffset), both on read (reader loops) and write (bind formatting)
   - Target: ≤5 registered `SqlMapper.TypeHandler` implementations (DateTime, decimal, bool, Guid, DateTimeOffset) that detect/branch on provider and produce identical values to today's hand-coercion; registered once at store/DI initialization. (Amended 2026-06-14 ≤4→≤5: the spike exercises only DateTime and passes with 4; the sweep adds `DateTimeOffsetTypeHandler` because `HarvestRunStore` + content stores store `DateTimeOffset` natively/encoded — still a small fixed set, see CONTEXT D-06.)
   - Acceptance: A focused round-trip test writes and reads each of the four types through a Dapper query on **both** an in-memory/temp SQLite db and a Postgres db, asserting value equality (including DateTime kind/offset semantics matching the pre-Dapper behavior)

3. **FeedbackStore spike (gate)**: `FeedbackStore` is fully converted to Dapper as the first store, and an objective pass/fail gate decides whether the sweep proceeds.
   - Current: `FeedbackStore` (7 pub / 3 int methods) uses raw `DbCommand`/reader plus `Dialect.FeedbackInsertReturningIdSql`
   - Target: `FeedbackStore` uses `QueryAsync`/`ExecuteAsync`/`ExecuteScalarAsync` (or Dapper equivalents) for all non-DDL methods, with **zero** store-local type conversion — all four coercions handled solely by the global handlers from REQ-2
   - Acceptance: **PASS gate** = (a) the ≤4 global handlers cover all coercion AND (b) the converted `FeedbackStore` contains no per-store `GetInt64`/`GetBoolean`/`Parse`/`ToString("O")`-style conversion AND (c) `DeckFlow.Web.Tests` feedback tests pass on both providers. If any of (a)(b)(c) fails, the phase **stops at the spike** and a written `49-GATE-VERDICT.md` records FAIL + rationale; the sweep does not start.

4. **Full sweep of eligible stores**: After a PASS gate, all eligible (non-carveout) stores are converted to Dapper for their query/execute paths.
   - Current: 13 eligible stores still raw ADO.NET (all `Content/*Store`, `CategoryKnowledgeRepository`, `CategoryKnowledgeStore`, `AdminBruteForceTrackerStore`, `FeatureFlagStore`, `HarvestRunStore`, `HarvestScheduleStore`, ledgers)
   - Target: Each eligible store's non-DDL read/write methods use Dapper; reader loops removed; param binding via Dapper anonymous-object/`DynamicParameters`; UPSERT (`ON CONFLICT`) and `RETURNING` SQL kept verbatim, executed through Dapper
   - Acceptance: A grep for `ExecuteReaderAsync` in the eligible store files returns zero non-DDL occurrences; each converted store's existing tests pass on both providers; behavior is byte-identical (see REQ-6)

5. **Carve-outs stay raw**: The two Dapper-incompatible/inapplicable paths remain raw ADO.NET, documented as intentional.
   - Current: `RequestMetricsStore.UpsertBatchAsync` uses `NpgsqlParameter` unnest; 12 files embed DDL/migration SQL
   - Target: `RequestMetricsStore` unnest-batch path unchanged; all DDL/schema-init/`ALTER`+backfill methods unchanged; each carve-out carries a brief `// Why:` note that it is intentionally raw
   - Acceptance: `RequestMetricsStore.UpsertBatchAsync` diff is empty; no `CREATE TABLE`/`CREATE INDEX`/`ALTER TABLE` statement is rewritten or relocated; the SPEC's out-of-scope list is reflected by an in-code comment at each carve-out

6. **Behavioral parity**: Conversion changes no observable behavior on either provider.
   - Current: Existing suites are green pre-phase (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`) per project records
   - Target: Same suites green post-phase with 0 new failures on Sqlite and on Postgres (PG-marked tests included where they run)
   - Acceptance: `dotnet build DeckFlow.sln` 0/0; full `DeckFlow.Core.Tests` and `DeckFlow.Web.Tests` runs show no new failures vs. the pre-phase baseline on both providers; no public store method signature changes

## Boundaries

**In scope:**
- Add `Dapper` package to `DeckFlow.Core`
- ≤5 provider-aware Dapper type handlers (DateTime, decimal, bool, Guid, DateTimeOffset) + their registration
- Convert `FeedbackStore` first as the spike, gated by the objective PASS criterion
- After PASS: convert all 13 eligible stores' query/execute/scalar paths to Dapper, in waves
- Keep all SQL text (UPSERT `ON CONFLICT`, `RETURNING`, `COALESCE`, dialect fragments) verbatim — only the execution/mapping mechanism changes
- Per-store, per-provider test verification at each wave (Codex implements, Claude reviews)
- A `49-GATE-VERDICT.md` recording the spike outcome (PASS or FAIL + rationale)

**Out of scope:**
- `RequestMetricsStore.UpsertBatchAsync` unnest-array batch — no Dapper equivalent for Npgsql array binding; stays raw
- All DDL / schema-init / migration / `ALTER TABLE`+backfill methods — Dapper does not help schema work and the project's immutable-migration rule protects them
- Any change to `IRelationalDialect`, `RelationalDatabaseConnection`, or `PostgresConnectionStringNormalizer` public surface — the abstraction is preserved, not replaced
- Replacing the dual-provider design or adopting EF Core — explicitly rejected in favor of the lighter micro-ORM
- Adding a second new package (e.g. `Dapper.Contrib`, a SQL builder) — out of scope; raw SQL strings stay
- Performance tuning / query rewrites — this is a mechanism swap, not an optimization phase
- The `RelationalDatabaseConnection.AddParameter` helper's removal — it may remain for carve-outs even after the sweep

## Constraints

- **Dual-provider parity is the hard constraint.** Every converted method must behave identically on SQLite (text/int-encoded types) and Postgres (native types). A regression that only manifests on one provider is a phase failure — verification must exercise both.
- **No new test framework / no second package.** xUnit stays; `Dapper` is the only addition (user-approved).
- **No public store signatures change** — `DeckFlow.Web` controllers/services that consume the stores must compile unchanged.
- **RAM budget** (Render Basic 256MB web tier) — Dapper is allocation-light; no caching layer or buffered bulk materialization that would grow working set.
- **Immutable migrations** — no existing migration/DDL is edited; carve-out comment-only additions at DDL sites are permitted.
- **LF line endings + pinned formatting** (`.gitattributes`/`.editorconfig`) — touch only the lines that change; no whole-file reformat.

## Acceptance Criteria

- [ ] `Dapper` package referenced in `DeckFlow.Core.csproj`; no other new package added; `dotnet build DeckFlow.sln` 0 errors / 0 new warnings
- [ ] ≤5 provider-aware Dapper type handlers exist and a round-trip test proves DateTime/decimal/bool/Guid/DateTimeOffset equality on both SQLite and Postgres
- [ ] `FeedbackStore` fully converted to Dapper with zero store-local type conversion; feedback tests pass on both providers
- [ ] Spike gate evaluated and recorded in `49-GATE-VERDICT.md` (PASS proceeds; FAIL stops the phase at the spike)
- [ ] On PASS: all 13 eligible stores converted; grep for `ExecuteReaderAsync` in eligible store files returns zero non-DDL occurrences
- [ ] `RequestMetricsStore.UpsertBatchAsync` and all DDL/migration methods are unchanged and carry an intentional-raw `// Why:` note
- [ ] `DeckFlow.Core.Tests` and `DeckFlow.Web.Tests` show 0 new failures vs. pre-phase baseline on both providers
- [ ] No public store method signature changed (consumers compile unchanged)

## Ambiguity Report

| Dimension          | Score | Min  | Status | Notes                                              |
|--------------------|-------|------|--------|----------------------------------------------------|
| Goal Clarity       | 0.92  | 0.75 | ✓      | Mechanism swap, spike-gated, scope = full sweep    |
| Boundary Clarity   | 0.85  | 0.70 | ✓      | Explicit carve-outs (RequestMetrics, all DDL)      |
| Constraint Clarity | 0.85  | 0.65 | ✓      | Dual-provider parity is the hard gate              |
| Acceptance Criteria| 0.85  | 0.70 | ✓      | 8 pass/fail checks incl. objective spike gate      |
| **Ambiguity**      | 0.13  | ≤0.20| ✓      |                                                    |

Status: ✓ = met minimum, ⚠ = below minimum (planner treats as assumption)

## Interview Log

| Round | Perspective     | Question summary                          | Decision locked                                                                 |
|-------|-----------------|-------------------------------------------|---------------------------------------------------------------------------------|
| 0     | Researcher      | What data access exists today? (scouted)  | 16 stores / ~6,293 LOC raw ADO.NET; 100% `AddParameter`; SQLite stores types as text/int |
| 1     | Boundary Keeper | Conversion scope — how many stores?       | Spike + full sweep (all 13 eligible after the spike)                            |
| 1     | Failure Analyst | Spike-gate PASS criterion (falsifiable?)  | Zero per-store mapping: ≤4 global handlers + no store-local conversion + feedback tests green both providers |
| 1     | Researcher      | New package approved?                      | `Dapper` approved (CLAUDE.md package gate satisfied)                             |

---

*Phase: 49-dapper-data-access-adoption*
*Spec created: 2026-06-14*
*Next step: /gsd:discuss-phase 49 — implementation decisions (type-handler design, wave grouping, per-provider test harness)*
