# Phase 49: Dapper Data-Access Adoption - Context

**Gathered:** 2026-06-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Swap the execution + row-mapping mechanism in the dual-provider store classes from hand-written `DbCommand`/`ExecuteReaderAsync` loops to Dapper, behind the unchanged `IRelationalDialect`/`RelationalDatabaseConnection` abstraction. SQL text (UPSERT `ON CONFLICT`, `RETURNING`, dialect fragments) is kept verbatim; only the way it is executed and mapped changes. `FeedbackStore` is the spike that gates the full sweep of the 13 eligible stores. DDL/migration methods and `RequestMetricsStore` unnest-batch stay raw ADO.NET by design.

</domain>

<spec_lock>
## Requirements (locked via SPEC.md)

**6 requirements are locked.** See `49-SPEC.md` for full requirements, boundaries, and acceptance criteria.

Downstream agents MUST read `49-SPEC.md` before planning or implementing. Requirements are not duplicated here.

**In scope (from SPEC.md):**
- Add `Dapper` package to `DeckFlow.Core`
- ≤5 provider-aware Dapper type handlers (DateTime, decimal, bool, Guid, DateTimeOffset) + registration (see D-06)
- Convert `FeedbackStore` first as the spike, gated by the objective PASS criterion
- After PASS: convert all 13 eligible stores' query/execute/scalar paths to Dapper, in waves
- Keep all SQL text verbatim — only the execution/mapping mechanism changes
- Per-store, per-provider test verification at each wave
- A `49-GATE-VERDICT.md` recording the spike outcome (PASS or FAIL + rationale)

**Out of scope (from SPEC.md):**
- `RequestMetricsStore.UpsertBatchAsync` unnest-array batch (no Dapper equivalent for Npgsql array binding)
- All DDL / schema-init / migration / `ALTER TABLE`+backfill methods
- Any change to `IRelationalDialect` / `RelationalDatabaseConnection` / `PostgresConnectionStringNormalizer` public surface
- Replacing the dual-provider design or adopting EF Core
- Adding a second new package (SQL builder, `Dapper.Contrib`, etc.)
- Performance tuning / query rewrites
- Forced removal of `RelationalDatabaseConnection.AddParameter` (it stays for the raw carve-outs)

</spec_lock>

<decisions>
## Implementation Decisions

### Column mapping (snake_case DB → PascalCase C#)
- **D-01:** Use a single **global** `Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true`, set once at initialization. Every snake_case column (`commander_name`, `is_visible`, `approval_status`, …) auto-maps to its PascalCase property; `SELECT` lists stay clean (no per-column `AS` aliases). Rationale: we own 100% of the queries, so the global flag is safe, and it avoids the silent-null risk of forgetting an alias on a newly added column.
- **D-02:** Do NOT use explicit `AS` aliases or per-type `SetTypeMap` for the general case (rejected as verbose / boilerplate that defeats the point of Dapper). A per-query alias is acceptable only where a column genuinely cannot match a property name even with underscore-stripping (flag any such case in the plan).

### Type-handler provider branching
- **D-03:** Implement a **single global set of ≤5 `SqlMapper.TypeHandler<T>`** (DateTime, decimal, bool, Guid, DateTimeOffset — see D-06). Each handler is provider-agnostic and **self-detects at runtime**:
  - `Parse(object value)` (read path): branch on the runtime type of the value the reader returned — `string`/`long` ⇒ SQLite-encoded, decode (ISO-8601 → DateTime, text → decimal, int → bool, text → Guid); already-native `DateTime`/`bool`/`decimal`/`Guid` ⇒ Postgres passthrough.
  - `SetValue(IDbDataParameter p, T value)` (write path): branch on the concrete parameter type — `SqliteParameter` ⇒ encode to text/int matching today's bind-time formatting; `NpgsqlParameter` ⇒ assign native value.
- **D-04:** Handlers are registered **exactly once** via a thread-safe, idempotent guard (e.g. a static `EnsureRegistered()` invoked from a single chokepoint — candidate: `RelationalDatabaseConnection` static init or a Core registration helper). The guard must tolerate the test suite constructing stores directly AND DI wiring, and tolerate both providers being exercised in the same process (the suite does exactly that). Do NOT use an ambient "current provider" flag (rejected — breaks when both providers run in one process).
- **D-05:** The handlers must reproduce **today's exact coercion semantics** — including DateTime `Kind`/offset handling (the current SQLite path uses `ToString("O", InvariantCulture)` + round-trip parse). Parity with the pre-Dapper values is the bar, not "a reasonable encoding."

### Handler count (amended 2026-06-14)
- **D-06:** The handler set is **≤5**, adding `DateTimeOffsetTypeHandler` to the original four (DateTime, decimal, bool, Guid). Rationale: `HarvestRunStore` + the content stores persist `DateTimeOffset`, which `TypeHandler<DateTime>` does not cover; the spike (`FeedbackStore`, DateTime-only) passes with 4, and the 5th is added in the sweep. This is a deliberate amendment of SPEC REQ-2's original `≤4` cap (user-approved 2026-06-14), not a spike FAIL. **Semantics (locked, identical logic to `DateTimeTypeHandler`):** write path — SQLite ⇒ `value.UtcDateTime.ToString("O", InvariantCulture)`, Postgres ⇒ native `DateTimeOffset`; read path — `string` ⇒ **two-step parse**: (1) `DateTimeOffset.TryParse(text, InvariantCulture, DateTimeStyles.RoundtripKind)` for `"O"`/offset/`Z` strings; (2) fallback `DateTimeOffset.Parse(text, InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)` — **without** `RoundtripKind` (the three flags are mutually incompatible per Microsoft `DateTimeStyles` docs). Native `DateTimeOffset`/`DateTime` ⇒ passthrough. Grounded in RESEARCH Assumption A1 / Pitfall 5.
- **D-07 (write-path handler firing):** Dapper resolves the built-in `typeMap` (DbType) BEFORE registered `typeHandlers` for parameter binding, so `TypeHandler<T>.SetValue` is NOT guaranteed to fire for built-in primitives (`DateTime`/`decimal`/`bool`/`Guid`/`DateTimeOffset`) bound via anonymous objects. To force the handlers to win on the write path, `EnsureRegistered()` calls `SqlMapper.RemoveTypeMap(typeof(T))` + `RemoveTypeMap(typeof(T?))` for each handled type before `AddTypeHandler`. The override is global (lives only in `EnsureRegistered()`) — stores keep zero local coercion (REQ-3 holds). The spike must empirically prove `SetValue` fires (raw-on-disk SQLite assertion, not Dapper round-trip); if it cannot be made to fire, the spike FAILs. Codex-sourced from Dapper `SqlMapper.cs` internals (2026-06-14).

### Claude's Discretion
- Exact file/namespace for the type handlers and the registration chokepoint — planner/researcher choose, consistent with `DeckFlow.Core` conventions (one public type per file, `sealed`).
- Whether converted methods use Dapper anonymous-object params or `DynamicParameters` — choose per call site; `RETURNING`/last-insert-id via `ExecuteScalarAsync<long>` over the existing dialect SQL.

### Open items deferred to research/planner (not decided here)
- **Per-provider test mechanism** (NOT discussed — left open): the SPEC requires proving parity on BOTH SQLite and Postgres. `Testcontainers.PostgreSql` 3.10.0 and `DeckFlow.Web.Tests/Integration/PostgresContainerFixture.cs` ALREADY exist and are the obvious vehicle — but CI does not currently spin a PG container (PG tests self-skip unless env flags set), and VSTest is unreliable in WSL. Research/planner must decide how the "both providers" acceptance is actually exercised (extend CI with a Postgres service/Docker, run the Testcontainers fixture, or a documented manual PG harness) and whether the type-handler round-trip test (REQ-2) runs in CI or as a gated manual step. Do NOT add a new package for this — reuse the existing fixture.
- **Wave grouping + gate mechanics** (NOT discussed — left open): order to convert the 13 eligible stores and how the FAIL path halts the sweep. Suggested risk-first ordering for the planner to confirm: `FeedbackStore` (spike) → simple content stores (`BlockedVideoStore`, `ContentSourceStore`, ledgers) → mid (`ContentVideoStore`, `ContentSiteIndexStore`, harvest/flags/bruteforce) → `CategoryKnowledgeRepository`/`CategoryKnowledgeStore` last (transactions + UPSERT + the card-id cache loop). Gate: write `49-GATE-VERDICT.md` after the spike; FAIL stops before any further store is touched.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Locked requirements
- `.planning/phases/49-dapper-data-access-adoption/49-SPEC.md` — Locked requirements, boundaries, acceptance criteria. MUST read before planning.

### Storage abstraction (unchanged public surface)
- `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` — connection factory, `OpenConnectionAsync` (applies SQLite `PRAGMA foreign_keys=ON`), static `AddParameter`. Dapper executes on connections opened here; the candidate registration chokepoint.
- `DeckFlow.Core/Storage/IRelationalDialect.cs` + `SqliteRelationalDialect.cs` + `PostgresRelationalDialect.cs` — dialect SQL fragments (`FeedbackInsertReturningIdSql`, surrogate-id types) kept verbatim and executed via Dapper.

### Spike target + reference stores
- `DeckFlow.Web/Services/FeedbackStore.cs` — the spike (REQ-3); uses `Dialect.FeedbackInsertReturningIdSql` + reader loops today.
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — heaviest store (transactions, UPSERT, `RETURNING`, card-id cache loop); convert last. NOTE: Phase 44 also touches this file — 44 was re-sequenced to run after 49.
- `DeckFlow.Web/Services/Analytics/RequestMetricsStore.cs` — CARVE-OUT (unnest-array batch stays raw).

### Per-provider testing (reuse, do not re-package)
- `DeckFlow.Web.Tests/Integration/PostgresContainerFixture.cs` — existing `Testcontainers.PostgreSql` 3.10.0 fixture; the vehicle for Postgres-side parity.
- `.planning/codebase/TESTING.md` — dual-dialect test patterns, WSL/VSTest caveat, temp-SQLite + `IDisposable` teardown, `[Collection(DisableParallelization=true)]` for env-mutating stores.

### Conventions
- `CLAUDE.md` (project) — formatting carve-outs that bind this work: never convert `{ get; init; }`→`{ get; }`, preserve LF, touch only changed lines. No new package beyond `Dapper`.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `RelationalDatabaseConnection.OpenConnectionAsync` — keep using it to obtain the open `DbConnection`; pass that connection (and `transaction:` where stores already `BeginTransactionAsync`) into Dapper calls. No new connection-management code.
- `PostgresContainerFixture` (Testcontainers, already a dependency) — reuse for the REQ-2 round-trip and converted-store PG parity; no new test package.
- Existing per-store temp-SQLite + `IDisposable` test harness (e.g. `CategoryKnowledgeRepositoryTests`, `ContentVideoStoreDistillTests`) — SQLite side already exercises real files; conversion keeps these.

### Established Patterns
- 100% of param binding flows through `RelationalDatabaseConnection.AddParameter` today (named `@p`, no positional) — consistent surface eases conversion; the helper stays for the raw carve-outs.
- One public type per file, `sealed` leaf types, file-scoped namespaces — type-handler files follow this.
- SQLite encodes `DateTime`→ISO text, `decimal`→text, `bool`→int, `Guid`→text; Postgres native — this asymmetry is the entire reason type handlers exist (D-03).

### Integration Points
- Type-handler registration chokepoint must cover BOTH DI wiring (`Program.cs`/Studio) AND direct store construction in tests — hence the idempotent guard (D-04).
- `RETURNING`/last-insert-id paths (5 files) execute via `ExecuteScalarAsync<long>` over the existing dialect SQL — no SQL change.

</code_context>

<specifics>
## Specific Ideas

- Parity-with-today is the explicit bar (D-05): the type handlers must emit byte-identical encoded values to the current hand-coercion, especially DateTime `O`-format round-trip — not merely "a valid encoding."
- The spike is a real gate, not a formality: if the global handlers can't absorb all coercion such that `FeedbackStore` needs zero store-local conversion, the phase STOPS at the spike with a written FAIL verdict (SPEC REQ-3).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope. (Per-provider test mechanism and wave grouping are not deferred *ideas* but in-scope decisions intentionally left to research/planner; see "Open items" under Implementation Decisions.)

</deferred>

---

*Phase: 49-dapper-data-access-adoption*
*Context gathered: 2026-06-14*
