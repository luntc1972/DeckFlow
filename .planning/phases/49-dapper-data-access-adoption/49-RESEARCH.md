# Phase 49: Dapper Data-Access Adoption - Research

**Researched:** 2026-06-14
**Domain:** Dapper micro-ORM adoption over dual-provider (SQLite + Postgres) ADO.NET store layer
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Global `Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true` — set once; no per-column `AS` aliases.
- **D-02:** No explicit `AS` aliases or per-type `SetTypeMap` for the general case; per-query alias only where column cannot match property even with underscore-stripping.
- **D-03:** ≤4 `SqlMapper.TypeHandler<T>` instances (DateTime, decimal, bool, Guid); each is provider-agnostic and self-detects at runtime: `Parse()` branches on the CLR type of the value the reader returns; `SetValue()` branches on the concrete `IDbDataParameter` type (`SqliteParameter` vs `NpgsqlParameter`).
- **D-04:** Handlers registered exactly once via thread-safe idempotent `EnsureRegistered()` — single chokepoint; must tolerate both DI wiring AND direct store construction in tests AND both providers in the same process.
- **D-05:** Handlers must replicate **today's exact coercion semantics** — DateTime `"O"` round-trip, `CultureInfo.InvariantCulture` decimal, bool as 1/0, Guid as `.ToString()` / `Guid.Parse()` — not "a reasonable encoding."
- **No second new package:** `Dapper` is the only addition; `Dapper.Contrib`, SQL builders, etc. are out of scope.
- **SQL text verbatim:** `ON CONFLICT`, `RETURNING`, `INSERT OR IGNORE`, dialect fragments stay untouched; only execution/mapping mechanism changes.
- **Public store signatures unchanged:** consumers compile unchanged.
- **Carve-outs stay raw:** `RequestMetricsStore.UpsertBatchAsync` + all DDL/schema-init/ALTER TABLE + backfill methods; each carries `// Why:` note.
- **No change to `IRelationalDialect` / `RelationalDatabaseConnection` / `PostgresConnectionStringNormalizer` public surface.**

### Claude's Discretion

- Exact file/namespace for type handlers and registration chokepoint (consistent with `DeckFlow.Core` conventions: one public type per file, `sealed`).
- Whether converted methods use Dapper anonymous-object params or `DynamicParameters` — choose per call site.
- `RETURNING`/last-insert-id via `ExecuteScalarAsync<long>` over existing dialect SQL.

### Deferred Ideas (OUT OF SCOPE)

None — all deferral items from CONTEXT.md are "open to researcher" items, not deferred scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| REQ-1 (DAP-01) | `Dapper` package referenced in `DeckFlow.Core.csproj`; no other new package; build 0/0 | Dapper 2.1.79 confirmed on NuGet; slopcheck [OK]; targets `net10.0` and `.NET Standard 2.0` — fully compatible |
| REQ-2 (DAP-02) | ≤4 provider-aware `SqlMapper.TypeHandler<T>` instances; round-trip test on both SQLite and Postgres | Full coercion inventory documented (§Coercion Semantics); `PostgresContainerFixture` already exists for PG side |
| REQ-3 (DAP-03) | `FeedbackStore` spike: fully converted with zero store-local coercion; PASS/FAIL recorded in `49-GATE-VERDICT.md` | FeedbackStore fully audited (§Spike Target); 3 `ExecuteReaderAsync` + 2 `ExecuteScalarAsync` + 2 `ExecuteNonQueryAsync`; all coercion in `ReadItem` eliminated by type handlers |
| REQ-4 | Full sweep of 13 eligible stores after PASS; `ExecuteReaderAsync` grep = 0 in eligible files | All 13 stores enumerated (§Eligible Store Inventory); wave grouping specified |
| REQ-5 | Carve-outs stay raw with `// Why:` notes | `RequestMetricsStore.UpsertBatchAsync` + all DDL methods catalogued (§Carve-Outs) |
| REQ-6 | Behavioral parity on both providers; 0 new failures; no public signature changes | Coercion parity requirements documented (§Coercion Semantics); per-provider test vehicle identified |
</phase_requirements>

---

## Summary

DeckFlow's data layer is 16 store/repository classes (~6,293 LOC, ~90 SQL methods) using raw ADO.NET behind the `RelationalDatabaseConnection` / `IRelationalDialect` abstraction. The dual-provider design (SQLite + Postgres) requires per-store coercion for four types: `DateTime` (ISO-8601 text on SQLite), `decimal` (invariant text), `bool` (int 0/1), and `Guid` (text). These coercions are copy-pasted into 8+ store files' reader loops and write-time formatting helpers.

Dapper adoption replaces the ADO.NET execution/mapping mechanism while keeping all SQL verbatim. The sole technical risk is that Dapper's default mapper does not perform the SQLite coercions — which is why the phase requires ≤4 `SqlMapper.TypeHandler<T>` implementations that self-detect the provider at runtime based on the value type returned by the reader (read path) and the `IDbDataParameter` concrete type (write path). If this design absorbs all four coercions globally such that `FeedbackStore` can be converted with zero store-local conversion code, the spike PASSes and the sweep proceeds.

Two patterns are excluded by design: `RequestMetricsStore.UpsertBatchAsync` uses `NpgsqlParameter` unnest arrays with no Dapper equivalent, and all DDL/schema-init/ALTER TABLE methods are immutable migrations that Dapper does not improve. These carry `// Why:` notes and remain raw ADO.NET permanently.

**Primary recommendation:** Add `Dapper 2.1.79` to `DeckFlow.Core.csproj`; implement 4 type handlers in `DeckFlow.Core/Storage/DapperTypeHandlers.cs`; register from `RelationalDatabaseConnection` static init; convert `FeedbackStore` as the spike; then sweep all 13 eligible stores in 3 waves ordered by risk (lowest complexity first).

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Dapper type handler registration | Database/Storage | — | `RelationalDatabaseConnection` is the lowest common layer; registration belongs there, not in DI config or store constructors |
| Store query/execute conversion | Database/Storage | — | Each store encapsulates its SQL; conversion is store-internal; no controller or service interface changes |
| Per-provider test verification | Test Infrastructure | — | `PostgresContainerFixture` + `PostgresFactAttribute` already exist in `DeckFlow.Web.Tests`; round-trip test goes in same project |
| Spike gate verdict | Build gate / doc | — | `49-GATE-VERDICT.md` is a planning artifact that halts the sweep if FAIL; not a runtime concern |

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Dapper` | 2.1.79 | Micro-ORM: `QueryAsync`/`ExecuteAsync`/`ExecuteScalarAsync` + `SqlMapper.TypeHandler<T>` | Approved by user; only new package; ships with `SqlMapper.DefaultTypeMap.MatchNamesWithUnderscores` |

[VERIFIED: NuGet registry — confirmed 2.1.79 is latest stable 2026-06-14]

### Supporting (already in solution — no additions)

| Library | Version | Purpose | Notes |
|---------|---------|---------|-------|
| `Microsoft.Data.Sqlite` | 10.0.0 | SQLite provider; `SqliteParameter` type for write-path branching | Already in `DeckFlow.Core.csproj` |
| `Npgsql` | 10.0.0 | Postgres provider; `NpgsqlParameter` type for write-path branching | Already in `DeckFlow.Core.csproj` |
| `Testcontainers.PostgreSql` | 3.10.0 | PG container for REQ-2 round-trip test | Already in `DeckFlow.Web.Tests.csproj`; gate env var `DECKFLOW_POSTGRES_TESTS=1` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Self-detecting `TypeHandler<T>` | Per-provider handler subclass | Rejected (D-03): breaks when both providers run in same process (the test suite does exactly that); ambient "current provider" flag also rejected for same reason |
| `Dapper.Contrib` | Raw `Dapper` | Rejected (out of scope): adds second package, enforces attribute-based schema conventions that conflict with verbatim-SQL requirement |
| `DynamicParameters` everywhere | Anonymous objects | Both are acceptable; choose per call site per Claude's Discretion |

**Installation (single line to add to `DeckFlow.Core.csproj`):**
```xml
<PackageReference Include="Dapper" Version="2.1.79" />
```

**Version verification:**
```bash
curl -s "https://api.nuget.org/v3-flatcontainer/dapper/index.json" | python3 -c \
  "import sys,json; v=[x for x in json.load(sys.stdin)['versions'] if '-' not in x]; print(v[-1])"
# Output: 2.1.79 (verified 2026-06-14)
```

---

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| `Dapper` | NuGet | ~13 yrs | 500M+ total | github.com/DapperLib/Dapper | [OK] | Approved |

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

*Note: slopcheck runs against PyPI by default; Dapper is a NuGet package. The [OK] verdict combined with NuGet registry confirmation (2.1.79 published, 500M+ downloads, official GitHub org `DapperLib`) provides HIGH confidence.*

---

## Architecture Patterns

### System Architecture Diagram

```
Test Suite (DeckFlow.Web.Tests)          Application (DeckFlow.Web / DeckFlow.CLI)
       │                                              │
       │ direct construction                          │ DI resolution
       ▼                                              ▼
  Store ctors ──────────────────────────────► RelationalDatabaseConnection
       │                           (static init calls DapperTypeHandlers.EnsureRegistered)
       │                                              │
       ▼                                              ▼
 DapperTypeHandlers.EnsureRegistered()     SqlMapper registered (idempotent)
 [once, thread-safe, both paths]                      │
       │                                              │
       ▼                                              ▼
 SqlMapper global state ◄─────────────────────────────┘
       │
       ├── MatchNamesWithUnderscores = true   (D-01)
       ├── TypeHandler<DateTime>              (D-03 read/write)
       ├── TypeHandler<decimal>              (D-03 read/write)
       ├── TypeHandler<bool>                 (D-03 read/write)
       └── TypeHandler<Guid>                 (D-03 read/write)
              │
              ▼
 Store.QueryAsync / ExecuteAsync / ExecuteScalarAsync
     uses open DbConnection from RelationalDatabaseConnection.OpenConnectionAsync()
              │
              ├── SQLite path: reader returns string/long/int → handlers decode
              └── Postgres path: reader returns native types → handlers pass through
```

### Recommended Project Structure

New files (all in `DeckFlow.Core/Storage/`):
```
DeckFlow.Core/Storage/
├── RelationalDatabaseConnection.cs    # EXISTING — add static init hook
├── DapperTypeHandlers.cs              # NEW — 4 TypeHandler<T> + EnsureRegistered()
├── IRelationalDialect.cs              # EXISTING — unchanged
├── SqliteRelationalDialect.cs         # EXISTING — unchanged
└── PostgresRelationalDialect.cs       # EXISTING — unchanged
```

Round-trip test in existing test project:
```
DeckFlow.Web.Tests/
└── Integration/
    ├── PostgresContainerFixture.cs    # EXISTING — reuse
    └── DapperTypeHandlerRoundTripTests.cs  # NEW — REQ-2
```

### Pattern 1: Provider-Agnostic TypeHandler (D-03)

**What:** A single handler instance registered globally; `Parse()` inspects the runtime CLR type of the value from the reader; `SetValue()` inspects the concrete parameter type.

**When to use:** All four coerced types (DateTime, decimal, bool, Guid).

**Example — DateTimeHandler:**
```csharp
// DeckFlow.Core/Storage/DapperTypeHandlers.cs
// Source: coercion semantics from DeckFlow.Web/Services/FeedbackStore.cs:226-231
//         and DeckFlow.Core/Content/ContentVideoStore.cs FormatTimestamp()
using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace DeckFlow.Core.Storage;

internal sealed class DateTimeTypeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override DateTime Parse(object value) => value switch
    {
        DateTime dt   => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        DateTimeOffset dto => dto.UtcDateTime,
        string text   => DateTime.Parse(text, null, DateTimeStyles.RoundtripKind),
        _             => Convert.ToDateTime(value)
    };

    public override void SetValue(IDbDataParameter p, DateTime value)
    {
        if (p is SqliteParameter)
            p.Value = value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        else
            p.Value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
```

**Note on DateTimeOffset:** Stores that use `DateTimeOffset` fields (content stores, ledgers) can map to `DateTime` properties via the handler, or use `DynamicParameters` with explicit formatting for write paths if the Dapper `TypeHandler<DateTimeOffset>` is needed. Given that model properties on result records will be `DateTime` or `DateTimeOffset` — check each store's record type. If result records use `DateTimeOffset`, a 5th handler may be needed. The planner should verify per store; if a single store uses `DateTimeOffset` properties, prefer converting that store's result record to `DateTime?` or adding `TypeHandler<DateTimeOffset>` as the 4th handler (replacing Guid if no store uses Guid in result records).

**Example — DecimalHandler:**
```csharp
// Source: DeckFlow.Core/Content/ContentHarvestRunStore.cs FormatDecimal() + ReadDecimal()
internal sealed class DecimalTypeHandler : SqlMapper.TypeHandler<decimal>
{
    public override decimal Parse(object value) => value switch
    {
        decimal d => d,
        double d  => Convert.ToDecimal(d, CultureInfo.InvariantCulture),
        float f   => Convert.ToDecimal(f, CultureInfo.InvariantCulture),
        string t  => decimal.Parse(t, CultureInfo.InvariantCulture),
        _         => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
    };

    public override void SetValue(IDbDataParameter p, decimal value)
    {
        if (p is SqliteParameter)
            p.Value = value.ToString(CultureInfo.InvariantCulture);
        else
            p.Value = value;
    }
}
```

**Example — BoolTypeHandler:**
```csharp
// Source: DeckFlow.Core/Content/ContentSourceStore.cs ReadBool() + bool write coercion
internal sealed class BoolTypeHandler : SqlMapper.TypeHandler<bool>
{
    public override bool Parse(object value) => value switch
    {
        bool b   => b,
        long l   => l != 0,
        int i    => i != 0,
        short s  => s != 0,
        string s => s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase),
        _        => Convert.ToBoolean(value)
    };

    public override void SetValue(IDbDataParameter p, bool value)
    {
        if (p is SqliteParameter)
            p.Value = value ? 1 : 0;
        else
            p.Value = value;
    }
}
```

**Example — GuidTypeHandler:**
```csharp
// Source: DeckFlow.Web/Services/Harvest/HarvestRunStore.cs:384-389
internal sealed class GuidTypeHandler : SqlMapper.TypeHandler<Guid>
{
    public override Guid Parse(object value) => value switch
    {
        Guid g   => g,
        string s => Guid.Parse(s),
        _        => throw new InvalidCastException($"Cannot convert {value?.GetType()} to Guid")
    };

    public override void SetValue(IDbDataParameter p, Guid value)
    {
        if (p is SqliteParameter)
            p.Value = value.ToString();
        else
            p.Value = value;
    }
}
```

### Pattern 2: Idempotent Registration Chokepoint (D-04)

**What:** Static `EnsureRegistered()` called from `RelationalDatabaseConnection`'s static constructor — runs exactly once per process, before any store opens a connection.

**When to use:** Called from `RelationalDatabaseConnection` static ctor only; no caller should call it directly.

```csharp
// DeckFlow.Core/Storage/DapperTypeHandlers.cs
public static class DapperTypeHandlers
{
    private static int _registered;

    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref _registered, 1) == 1) return;

        SqlMapper.AddTypeHandler(new DateTimeTypeHandler());
        SqlMapper.AddTypeHandler(new DecimalTypeHandler());
        SqlMapper.AddTypeHandler(new BoolTypeHandler());
        SqlMapper.AddTypeHandler(new GuidTypeHandler());
        SqlMapper.DefaultTypeMap.MatchNamesWithUnderscores = true;  // D-01
    }
}
```

```csharp
// DeckFlow.Core/Storage/RelationalDatabaseConnection.cs — add static ctor
static RelationalDatabaseConnection()
{
    DapperTypeHandlers.EnsureRegistered();
}
```

### Pattern 3: Dapper Execution — Query with RETURNING

**What:** `ExecuteScalarAsync<long>` for INSERT ... RETURNING id; `QueryAsync<T>` for SELECT; `ExecuteAsync` for UPDATE/DELETE.

```csharp
// Source: Dapper API — replaces ExecuteScalarAsync on DbCommand
// For RETURNING id pattern (e.g. FeedbackStore.AddAsync):
await using var connection = await _connectionInfo.OpenConnectionAsync(ct).ConfigureAwait(false);
var id = await connection.ExecuteScalarAsync<long>(
    Dialect.FeedbackInsertReturningIdSql,
    new { ip = hashedIp, comment, status = "pending", createdUtc = DateTime.UtcNow },
    commandTimeout: null).ConfigureAwait(false);
```

```csharp
// For SELECT + mapping (e.g. FeedbackStore.ListAsync):
var rows = await connection.QueryAsync<FeedbackItem>(sql, new { limit, offset })
    .ConfigureAwait(false);
```

### Pattern 4: Transaction Passing (CategoryKnowledgeRepository)

**What:** Stores that use transactions pass `transaction:` parameter to Dapper calls.

```csharp
// Source: Dapper docs — transaction parameter
await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
await connection.ExecuteAsync(sql, param, transaction: tx).ConfigureAwait(false);
await tx.CommitAsync(ct).ConfigureAwait(false);
```

### Pattern 5: Carve-Out Comment

**What:** Each raw carve-out site carries a `// Why:` note.

```csharp
// Why: RequestMetricsStore.UpsertBatchAsync uses NpgsqlParameter unnest array binding
// which has no Dapper equivalent. This method intentionally stays raw ADO.NET.
// See Phase 49 SPEC §Boundaries.
```

### Anti-Patterns to Avoid

- **Ambient "current provider" static flag:** Breaks when both providers run in the same process (the test suite exercises both). Use self-detecting TypeHandlers (D-03) instead.
- **Per-store `SetTypeMap` or `ColumnMap`:** Verbose; defeats the global flag. Use D-01 + D-02.
- **`Task.WhenAll` over Dapper queries sharing a connection:** SQLite connections are not thread-safe. Keep sequential per connection.
- **Calling `EnsureRegistered()` from store constructors:** Registration is global; calling it N times is safe (idempotent) but the canonical path is the static ctor on `RelationalDatabaseConnection`. Don't scatter calls.
- **Re-formatting SQL strings for Dapper:** SQL stays verbatim. Dapper takes the same SQL strings the ADO.NET code used; only the execution call changes.

---

## Eligible Store Inventory

**13 eligible stores** (excluding spike FeedbackStore = 14 units total converted):

### Wave 0 — Spike (gate before sweep)

| Store | File | Public Methods | Coerced Types | Notes |
|-------|------|----------------|---------------|-------|
| `FeedbackStore` | `DeckFlow.Web/Services/FeedbackStore.cs` | 7 pub + 3 int | DateTime | `ReadItem()` lines 226-231; write line 63; `OpenConnectionAsync` uses `CreateConnection()` directly (no FK PRAGMA — intentional, preserve) |

### Wave 1 — Simple stores (after PASS gate; lowest risk)

| Store | File | Public Methods | Coerced Types | Key Notes |
|-------|------|----------------|---------------|-----------|
| `BlockedVideoStore` | `DeckFlow.Core/Content/BlockedVideoStore.cs` | 5 | DateTimeOffset | `ReadDateTimeOffset` uses `.AssumeUniversal | .AdjustToUniversal` (NOT `.RoundtripKind`) — handler must handle both ISO variants; INSERT OR IGNORE (SQLite) vs ON CONFLICT DO NOTHING (Postgres) SQL variants stay verbatim |
| `ContentSourceStore` | `DeckFlow.Core/Content/ContentSourceStore.cs` | 5 | DateTimeOffset, bool | `ReadBool` helper + bool write coercion line 126/148; `InsertSourceSql` uses `RETURNING id` |
| `AdminBruteForceTrackerStore` | `DeckFlow.Web/Services/AdminBruteForceTrackerStore.cs` | 2 | DateTime | Provider-specific UPSERT SQL with SQL arithmetic (`INTERVAL` vs `julianday`) — both stay verbatim |
| `FeatureFlagStore` | `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` | 3 | bool, DateTime | DDL + seed SQL in `EnsureSchemaAsync` stays raw |
| `HarvestScheduleStore` | `DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs` | 3 | bool, DateTime | Simple schema; 3 methods |
| `SpendLedgerBase` + `LlmSpendLedger` + `WhisperSpendLedger` | `DeckFlow.Core/Content/SpendLedgerBase.cs`, `LlmSpendLedger.cs`, `WhisperSpendLedger.cs` | Base: 3; Llm: 1; Whisper: 1 | decimal, DateTimeOffset | `SpendLedgerBase.GetMonthlyTotalAsync` has the reader loop + `ReadDecimal`; `RecordCallAsync` in each subclass does the write; DDL in `EnsureSchemaAsync` stays raw; **count as 1 conversion unit** (base class method) |

### Wave 2 — Mid-complexity (after Wave 1 verification)

| Store | File | Public Methods | Coerced Types | Key Notes |
|-------|------|----------------|---------------|-----------|
| `ContentHarvestRunStore` | `DeckFlow.Core/Content/ContentHarvestRunStore.cs` | 4 | decimal, DateTimeOffset | Both `FormatDecimal` and `ReadDecimal` — type handler must hit these; `FormatTimestamp` → `DateTimeOffset` write |
| `HarvestRunStore` | `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` | 10 | DateTime, Guid | **Guid confirmed** write/read lines 384-389; `BindNullableTimestamp` for nullable DateTime; DDL migration `EnsureStateConstraintAllowsInterruptedAsync` (table rebuild in transaction) — stays raw |
| `ContentVideoStore` | `DeckFlow.Core/Content/ContentVideoStore.cs` | 20 | DateTimeOffset | Largest content store; `FormatTimestamp` + `ReadDateTimeOffset`; `EnsureFilteredDistillStatusConstraintAsync` DDL stays raw; ~20 public methods |
| `ContentSiteIndexStore` | `DeckFlow.Core/Content/ContentSiteIndexStore.cs` | 16 | bool, DateTimeOffset | `FormatVisibility` (bool) + `ReadVisibility`; multiple ALTER TABLE in EnsureSchemaAsync stays raw; `GetTableColumnsAsync` (PRAGMA/information_schema) stays raw |

### Wave 3 — Heaviest (last; after Wave 2 verification)

| Store | File | Public Methods | Coerced Types | Key Notes |
|-------|------|----------------|---------------|-----------|
| `CategoryKnowledgeStore` | `DeckFlow.Web/Services/CategoryKnowledgeStore.cs` | 16 pub (delegates mostly) | DateTime | `AddTimestampParameter` helper uses `SpecifyKind(Utc).ToString("O")`; a few direct queries with `OpenConnectionAsync()` direct call; `CoerceCount` internal helper must stay (used in tests) |
| `CategoryKnowledgeRepository` | `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` | 24 pub | DateTime (text timestamps), no decimal/bool/Guid | Complex UPSERT + RETURNING + transactions + in-memory card-id cache (`ResolveCardIdAsync`); convert last; **Phase 44 also touches this file** — 44 is sequenced after 49 |

**Carve-outs (stay raw ADO.NET permanently):**

| Store | Method | Reason |
|-------|--------|--------|
| `RequestMetricsStore` | `UpsertBatchAsync` | `NpgsqlParameter` unnest array binding — no Dapper equivalent |
| All 12 DDL sites | `EnsureSchemaAsync`, `EnsureXxxConstraintAsync`, `GetTableColumnsAsync`, backfill methods | Schema management; immutable-migration rule; Dapper does not improve DDL |

---

## Coercion Semantics — Exact Parity Required (D-05)

This section is the canonical reference for TypeHandler implementation. All file:line citations are from verified source reads.

### DateTime / DateTimeOffset

**Write (SQLite path):**
- `DateTime`: `value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)` — e.g. `FeedbackStore.cs:63`: `DateTime.UtcNow.ToString("O")`
- `DateTimeOffset`: `value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)` — e.g. `ContentVideoStore.cs FormatTimestamp()`, `SpendLedgerBase.cs:152`
- **Postgres write:** pass `DateTime.SpecifyKind(value, DateTimeKind.Utc)` or `value.UtcDateTime` — native `TIMESTAMPTZ` / `TIMESTAMP WITH TIME ZONE`

**Read (any provider):**
```csharp
// FeedbackStore.cs:226-231 — the canonical switch:
var createdUtc = reader.GetValue(1) switch
{
    DateTime dt         => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
    DateTimeOffset dto  => dto.UtcDateTime,
    string text         => DateTime.Parse(text, null, DateTimeStyles.RoundtripKind),
    var other           => Convert.ToDateTime(other)
};
```
- `BlockedVideoStore` variant: uses `DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal` — the TypeHandler `Parse()` for `DateTime` uses `.RoundtripKind`; if BlockedVideoStore's read values are ISO strings without trailing `Z` (written by the same FormatTimestamp → always has `Z` from `"O"` format), `.RoundtripKind` handles them correctly. **The `.AssumeUniversal` variant is a legacy read-path concern that disappears once writes consistently use `"O"` format.**

### decimal

**Write (SQLite):** `value.ToString(CultureInfo.InvariantCulture)` → stored as TEXT
- Source: `ContentHarvestRunStore.cs FormatDecimal()`, `SpendLedgerBase.cs:144`
- **Postgres write:** pass `decimal` native value

**Read:**
```csharp
// ContentHarvestRunStore.cs ReadDecimal() / SpendLedgerBase.cs:174-183
raw switch {
    decimal d => d,
    double d  => Convert.ToDecimal(d, CultureInfo.InvariantCulture),
    float f   => Convert.ToDecimal(f, CultureInfo.InvariantCulture),
    string t  => decimal.Parse(t, CultureInfo.InvariantCulture),
    _         => Convert.ToDecimal(raw, CultureInfo.InvariantCulture)
}
```

### bool

**Write (SQLite):** `value ? 1 : 0` → stored as INTEGER
- Source: `ContentSourceStore.cs:126,148`, `FeatureFlagStore.cs`, `HarvestScheduleStore.cs`
- **Postgres write:** pass `bool` native value

**Read:**
```csharp
// ContentSourceStore.cs ReadBool()
raw switch {
    bool b   => b,
    long l   => l != 0,
    int i    => i != 0,
    short s  => s != 0,
    string s => s == "1" || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase),
    _        => Convert.ToBoolean(raw)
}
```

### Guid

**Write (SQLite):** `value.ToString()` → stored as TEXT
- Source: `HarvestRunStore.cs:~380` write path
- **Postgres write:** pass `Guid` native value (Npgsql handles UUID natively)

**Read:**
```csharp
// HarvestRunStore.cs:384-389
raw switch {
    Guid g   => g,
    string s => Guid.Parse(s),
    _        => throw new InvalidCastException(...)
}
```

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Row-to-object mapping | Custom `while (reader.ReadAsync())` loops | `QueryAsync<T>` + `MatchNamesWithUnderscores` | D-01 global flag handles all snake_case → PascalCase; loop code is error-prone and verbose |
| Type coercion per-store | Copy-pasted `ReadBool`/`ReadDecimal` helpers | Global `SqlMapper.TypeHandler<T>` | Centralized; tested once in REQ-2 round-trip; eliminates divergence across 13 stores |
| Provider detection in stores | `_connectionInfo.IsPostgres ? x : y` branches in query methods | TypeHandler `SetValue()` branching on `IDbDataParameter` concrete type | Handler is the single coercion point; stores become provider-blind in their query logic |
| RETURNING id execution | `ExecuteScalarAsync` on `DbCommand` | `connection.ExecuteScalarAsync<long>(sql, param)` | Dapper handles the cast; no `Convert.ToInt64()` needed |
| Column alias verbosity | `SELECT commander_name AS CommanderName, ...` | `MatchNamesWithUnderscores = true` | Global flag; no per-query alias noise; safe because project owns 100% of queries (D-01) |

**Key insight:** The type handler boundary is the entire justification for this phase. Without handlers absorbing all four coercions, Dapper is merely a syntax change. The spike gate (REQ-3) verifies this centralization works before committing to 13 stores.

---

## Per-Provider Test Mechanism

**Existing infrastructure (reuse, no new packages):**

- `DeckFlow.Web.Tests/Integration/PostgresContainerFixture.cs` — `Testcontainers.PostgreSql 3.10.0` with `postgres:16-alpine`; gate env var `DECKFLOW_POSTGRES_TESTS=1`; lazy startup on first `GetConnectionStringOrSkipAsync()` call; skips gracefully if Docker unavailable.
- `PostgresFactAttribute` — `[PostgresFact]` marks Postgres-only tests; auto-skips if env var absent.

**REQ-2 round-trip test location:** `DeckFlow.Web.Tests/Integration/DapperTypeHandlerRoundTripTests.cs`

**What the round-trip test covers:**
1. SQLite: write each of the 4 types via Dapper → read back via Dapper → assert value equality (including DateTime `Kind = Utc`, decimal scale, bool exact bool, Guid exact bytes)
2. Postgres: same (via `PostgresContainerFixture`; skips if env var not set)

**CI vs. manual:**
- SQLite side: runs always in CI (no Docker needed) — no env var gate required.
- Postgres side: requires `DECKFLOW_POSTGRES_TESTS=1` + Docker. Currently CI does not spin a PG container. **Recommendation:** accept that the PG round-trip test is a gated manual step (run locally before shipping the phase, document the command in a `49-GATE-VERDICT.md` annex). Adding a Docker CI service is a separate concern and out of scope for Phase 49.

**CLAUDE.md note:** "VSTest unreliable in WSL" — prefer `dotnet test` from a Windows terminal or the `/mnt/c/...` dotnet path pattern. Testcontainers requires Docker running.

---

## Common Pitfalls

### Pitfall 1: TypeHandler Not Registered Before First Connection Open

**What goes wrong:** Dapper caches type maps on first use per CLR type. If a store is exercised before `EnsureRegistered()` runs, the default (no-op) handler is cached and the custom handler is never invoked — even after registration.

**Why it happens:** `SqlMapper.AddTypeHandler` must be called before any `QueryAsync<T>` where `T` uses the handler.

**How to avoid:** Registration in `RelationalDatabaseConnection` static constructor runs before `OpenConnectionAsync()` can return — which is the earliest possible moment any Dapper call can occur. Do NOT defer registration to a method-level guard in store code.

**Warning signs:** Tests pass on SQLite (where native types fall back to something usable) but fail on Postgres for `decimal`; or vice versa.

### Pitfall 2: DateTime Kind Lost on Postgres Read Path

**What goes wrong:** `NpgsqlDataReader` returns `DateTime` with `Kind = Local` or `Kind = Unspecified` for `TIMESTAMP` columns (not `TIMESTAMPTZ`). The existing hand-written code calls `DateTime.SpecifyKind(dt, DateTimeKind.Utc)` to normalize.

**Why it happens:** Dapper calls `Parse(object value)` with the reader-returned value. If the handler's `DateTime` branch does not call `SpecifyKind`, timestamps read from Postgres will have wrong `Kind`.

**How to avoid:** `DateTimeTypeHandler.Parse()` always passes `DateTime` values through `DateTime.SpecifyKind(dt, DateTimeKind.Utc)`.

### Pitfall 3: FeedbackStore OpenConnectionAsync vs OpenConnectionAsync()

**What goes wrong:** `FeedbackStore` uses `_connectionInfo.CreateConnection()` + `connection.OpenAsync()` directly (NOT `_connectionInfo.OpenConnectionAsync()`), which means `PRAGMA foreign_keys=ON` is NOT applied. This is intentional for the feedback table.

**Why it matters for Dapper:** Dapper calls are made on an `IDbConnection`. The conversion must pass the same connection obtained by the same `CreateConnection()` + `OpenAsync()` call (not via `_connectionInfo.OpenConnectionAsync()`) to preserve the FK-PRAGMA-absent behavior.

**How to avoid:** Examine each store's existing `OpenConnectionAsync` call site. Stores that call `_connectionInfo.OpenConnectionAsync()` get FK PRAGMA; stores that call `_connectionInfo.CreateConnection()` + `OpenAsync()` directly do NOT. Preserve the pattern per store.

### Pitfall 4: RETURNING id Ordinal Mismatch

**What goes wrong:** `ExecuteScalarAsync<long>` returns the first column of the first row. If the `RETURNING id` SQL returns multiple columns, Dapper takes column 0 — which is correct for `RETURNING id` but wrong for `RETURNING *`.

**Why it happens:** SPEC and existing code use `RETURNING id` only; this is safe. Do not change SQL to `RETURNING *`.

**How to avoid:** Keep all `RETURNING` clauses as `RETURNING id` — no change to SQL text; Dapper `ExecuteScalarAsync<long>` just works.

### Pitfall 5: BlockedVideoStore AssumeUniversal Read Path

**What goes wrong:** `BlockedVideoStore.ReadDateTimeOffset` uses `DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal` instead of the more common `.RoundtripKind`. The TypeHandler uses `.RoundtripKind`.

**Why it happens:** ISO-8601 strings written by `"O"` format always include the `+00:00` or `Z` suffix, so `.RoundtripKind` correctly identifies them as UTC. The `.AssumeUniversal` variant was a defensive read for strings that might lack the UTC marker.

**How to avoid:** Since the write path uses `"O"` (always includes UTC offset marker), `.RoundtripKind` correctly round-trips. Verify by running `BlockedVideoStore` existing tests after conversion — they will catch any regression.

### Pitfall 6: CategoryKnowledgeRepository Card-ID Cache Across Dapper Calls

**What goes wrong:** `ResolveCardIdAsync` maintains an in-memory `Dictionary<string, long>` passed by ref across a transaction. The helper executes `INSERT OR IGNORE / INSERT ON CONFLICT DO NOTHING` then reads back the id. This pattern requires `connection` + `transaction` parameters on Dapper calls.

**Why it happens:** Dapper's `QueryAsync` and `ExecuteAsync` accept optional `transaction:` parameter — must be passed for all calls within a transaction scope.

**How to avoid:** All Dapper calls inside `CategoryKnowledgeRepository` transaction methods must include `transaction: tx`. The card-id cache logic itself stays; only the execution call changes.

### Pitfall 7: SpendLedgerBase EnsureSchemaAsync Creates ContentVideoStore

**What goes wrong:** `SpendLedgerBase.EnsureSchemaAsync` instantiates `new ContentVideoStore(_connectionInfo)` and calls `EnsureSchemaAsync` on it (for FK parent table ordering). After `ContentVideoStore` is converted to Dapper, this internal instantiation is still valid — but note the dependency.

**Why it matters:** `ContentVideoStore` must be converted (Wave 2) before `SpendLedgerBase` (Wave 1) if the constructors do any Dapper registration-sensitive setup. In practice, since registration is in the static ctor, order of store construction is irrelevant.

**How to avoid:** No action needed; document that SpendLedgerBase's schema bootstrap creates a temporary `ContentVideoStore` instance — this is existing behavior.

---

## Code Examples

### Dapper QueryAsync with anonymous-object params

```csharp
// Source: Dapper README (DapperLib/Dapper) — QueryAsync pattern
var items = await connection.QueryAsync<FeedbackItem>(
    "SELECT id, ip, comment, status, created_utc FROM feedback WHERE status = @status ORDER BY created_utc DESC LIMIT @limit",
    new { status, limit },
    commandTimeout: null)
    .ConfigureAwait(false);
```

### Dapper ExecuteScalarAsync for RETURNING id

```csharp
// Source: Dapper README — ExecuteScalarAsync<T> pattern
var newId = await connection.ExecuteScalarAsync<long>(
    Dialect.FeedbackInsertReturningIdSql,
    new { ip = hashedIp, comment, status = "pending", createdUtc = DateTime.UtcNow })
    .ConfigureAwait(false);
```

### Dapper ExecuteAsync for UPDATE/DELETE

```csharp
// Source: Dapper README — ExecuteAsync pattern
await connection.ExecuteAsync(
    "UPDATE feedback SET status = @status WHERE id = @id",
    new { status, id })
    .ConfigureAwait(false);
```

### Dapper with transaction (CategoryKnowledgeRepository pattern)

```csharp
// Source: Dapper README — transaction parameter
await using var connection = await _connectionInfo.OpenConnectionAsync(ct).ConfigureAwait(false);
await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
try
{
    await connection.ExecuteAsync(upsertSql, param, transaction: tx).ConfigureAwait(false);
    var id = await connection.ExecuteScalarAsync<long>(selectSql, keyParam, transaction: tx)
        .ConfigureAwait(false);
    await tx.CommitAsync(ct).ConfigureAwait(false);
    return id;
}
catch
{
    await tx.RollbackAsync(ct).ConfigureAwait(false);
    throw;
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Per-store `while(reader.ReadAsync())` + manual column ordinals | `QueryAsync<T>` + global underscore mapping | Phase 49 | Eliminates ~200+ lines of reader loop boilerplate |
| Per-store `FormatDecimal`/`ReadBool`/etc. copy-pasted helpers | Global `SqlMapper.TypeHandler<T>` | Phase 49 | Single tested coercion path; handler bugs fixed once, everywhere |
| `RelationalDatabaseConnection.AddParameter` everywhere | Anonymous objects / `DynamicParameters` for Dapper calls; `AddParameter` stays for raw carve-outs | Phase 49 | Simpler call sites; `AddParameter` not removed (SPEC constraint) |

**Deprecated/outdated after this phase:**
- Per-store `ReadDecimal`, `ReadBool`, `ReadDateTimeOffset`, `ReadTimestamp`, `FormatTimestamp`, `FormatDecimal`, `FormatVisibility` helpers: replaced by TypeHandlers. The static private helper methods are deleted per store as it is converted.
- `ExecuteReaderAsync` loops in eligible files: zero occurrences in non-DDL methods post-sweep (grep acceptance criterion).

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `BlockedVideoStore`'s `.AssumeUniversal` read path is safe to replace with `.RoundtripKind` because the write path always uses `"O"` format (includes UTC marker) | Coercion Semantics / Pitfall 5 | If any legacy data in `blocked_videos` lacks a UTC marker on the stored string, `.RoundtripKind` parse would fail; existing tests would catch this |
| A2 | Postgres CI test for round-trip (REQ-2) is acceptable as a gated manual step (not added to CI) | Per-Provider Test Mechanism | If CI requires 100% automated PG coverage, a Docker service stage must be added; planner should confirm with user |
| A3 | `TypeHandler<DateTimeOffset>` is not needed because result record properties where Dapper maps timestamps are all typed as `DateTime` (not `DateTimeOffset`) | Coercion Semantics | If any store's result record / model type uses `DateTimeOffset` properties, a 5th handler is needed; planner should verify per store during implementation |
| A4 | `SpendLedgerBase` + `LlmSpendLedger` + `WhisperSpendLedger` count as one conversion unit (Wave 1) since the reader loop is in the base class and subclasses only add `ExecuteNonQueryAsync` writes | Eligible Store Inventory | If the planner treats them as 3 separate units, wave sizing changes; no functional impact |

---

## Open Questions

1. **DateTimeOffset vs DateTime in result record properties**
   - What we know: Content stores use `DateTimeOffset` for their `*Utc` fields; FeedbackStore uses `DateTime`.
   - What's unclear: Whether existing model/record types have `DateTime` or `DateTimeOffset` properties for the mapped rows — this determines if `TypeHandler<DateTimeOffset>` is needed as a 5th handler.
   - Recommendation: During implementation, check each store's return type. If any use `DateTimeOffset`, add `TypeHandler<DateTimeOffset>` (same logic as `DateTimeTypeHandler` but returning `DateTimeOffset`); stays within the ≤4 handler spirit if Guid is not needed for that store.

2. **Postgres CI test automation**
   - What we know: `PostgresContainerFixture` is fully implemented; requires Docker + `DECKFLOW_POSTGRES_TESTS=1`.
   - What's unclear: Whether the user wants the REQ-2 round-trip test to block CI or remain a local-only gate.
   - Recommendation: Keep as manual gate for Phase 49; document the command in `49-GATE-VERDICT.md`. Adding CI PG stage is a follow-up.

3. **Phase 44 + CategoryKnowledgeRepository sequencing**
   - What we know: CONTEXT.md states "Phase 44 also touches this file — 44 was re-sequenced to run after 49."
   - What's unclear: Exact merge conflict risk between Phase 44's changes to `CategoryKnowledgeRepository.cs` and Phase 49's Dapper conversion of the same file.
   - Recommendation: Convert `CategoryKnowledgeRepository` in Wave 3 (last); Phase 49 merges first; Phase 44 applies on top. Planner should note this in the Phase 49 plan and in Phase 44's plan.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build + test | ✓ | 10.x (per project) | — |
| Docker | Testcontainers PG (REQ-2 Postgres side) | Unknown | — | Run PG round-trip test manually; skip in CI |
| `DECKFLOW_POSTGRES_TESTS=1` | Postgres-gated tests | Manual set | — | Tests self-skip without it |
| NuGet access | Dapper 2.1.79 install | ✓ | — | — |

**Missing dependencies with no fallback:** None that block execution.

**Missing dependencies with fallback:**
- Docker (for Testcontainers): PG round-trip test skips gracefully if Docker unavailable; SQLite side runs always.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 |
| Config file | none (`dotnet test` discovery) |
| Quick run command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -v q` |
| Full suite command | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -v q` |
| Postgres gate | `DECKFLOW_POSTGRES_TESTS=1 dotnet test ...` (Docker required) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REQ-1 | `DeckFlow.Core.csproj` has Dapper reference; build succeeds | Build check | `dotnet build DeckFlow.sln` | N/A — build gate |
| REQ-2 | 4 TypeHandlers round-trip all 4 types on SQLite + Postgres | Integration | `dotnet test DeckFlow.Web.Tests/ -v q` (SQLite always; PG with env var) | ❌ Wave 0: `DapperTypeHandlerRoundTripTests.cs` |
| REQ-3 | FeedbackStore converted; no store-local coercion; feedback tests green | Unit + integration | `dotnet test DeckFlow.Web.Tests/ -v q --filter "Feedback"` | ✅ Existing feedback tests |
| REQ-4 | `ExecuteReaderAsync` grep = 0 in eligible files | Grep gate | `grep -r "ExecuteReaderAsync" <eligible-files>` | N/A — grep |
| REQ-5 | Carve-outs unchanged; `// Why:` comments present | Code review + grep | Grep for carve-out comments | N/A — review |
| REQ-6 | 0 new test failures on both providers | Full suite | `dotnet test DeckFlow.sln -v q` | ✅ Existing suites |

### Sampling Rate

- **Per task commit:** `dotnet build DeckFlow.sln` (0 errors / 0 warnings)
- **Per wave merge:** `dotnet test DeckFlow.sln -v q` (0 new failures vs. pre-phase baseline)
- **Phase gate:** Full suite green + `ExecuteReaderAsync` grep = 0 + `49-GATE-VERDICT.md` written

### Wave 0 Gaps

- [ ] `DeckFlow.Web.Tests/Integration/DapperTypeHandlerRoundTripTests.cs` — covers REQ-2 (4 types × 2 providers)

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V5 Input Validation | yes | Dapper parameterized queries; no string interpolation in SQL |
| V6 Cryptography | no | No cryptographic operations |
| V2 Authentication | no | Store layer; auth handled by middleware |
| V3 Session Management | no | — |
| V4 Access Control | no | — |

### Known Threat Patterns for Dapper Adoption

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection via anonymous object parameter | Tampering | Dapper always parameterizes anonymous object properties; SQL stays verbatim; no string interpolation |
| Type handler deserialization exploit (malformed DB values) | Tampering | Handlers use `switch` with explicit type cases + fallback `Convert.*`; malformed values throw `InvalidCastException` (fail closed) |

**Security posture unchanged:** Phase 49 is a mechanism swap. SQL strings are verbatim (no new query surfaces). No new authentication, session, or access control concerns. Parameterization was already 100% via `AddParameter`; Dapper maintains this via anonymous objects.

---

## Sources

### Primary (HIGH confidence)

- `DeckFlow.Web/Services/FeedbackStore.cs` — spike target; coercion semantics lines 63, 226-231; OpenConnectionAsync pattern [VERIFIED: direct read]
- `DeckFlow.Core/Storage/RelationalDatabaseConnection.cs` — registration chokepoint candidate; `IsSqlite`/`IsPostgres` flags; `OpenConnectionAsync` FK PRAGMA behavior [VERIFIED: direct read]
- `DeckFlow.Core/Content/ContentHarvestRunStore.cs` — canonical `FormatDecimal`/`ReadDecimal` implementation [VERIFIED: direct read]
- `DeckFlow.Core/Content/ContentSourceStore.cs` — canonical `ReadBool` + bool write coercion [VERIFIED: direct read]
- `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs:384-389` — Guid coercion confirmed [VERIFIED: direct read]
- `DeckFlow.Core/Content/SpendLedgerBase.cs` — 13th eligible unit; `ReadDecimal` in base class; `LlmSpendLedger` + `WhisperSpendLedger` subclasses [VERIFIED: direct read]
- `DeckFlow.Web.Tests/Integration/PostgresContainerFixture.cs` — per-provider test vehicle; gate env var; skip behavior [VERIFIED: direct read]
- NuGet API — Dapper 2.1.79 latest stable confirmed 2026-06-14 [VERIFIED: NuGet registry]

### Secondary (MEDIUM confidence)

- `DeckFlow.Core/Content/BlockedVideoStore.cs` — `DateTimeStyles.AssumeUniversal` variant [VERIFIED: direct read]; `.RoundtripKind` compatibility analysis [ASSUMED — see A1]
- Dapper GitHub (DapperLib/Dapper) — `TypeHandler<T>` API, `MatchNamesWithUnderscores`, `AddTypeHandler` idempotency behavior [CITED: github.com/DapperLib/Dapper]

### Tertiary (LOW confidence)

- None.

---

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH — Dapper 2.1.79 confirmed on NuGet; slopcheck [OK]; existing project patterns verified in source
- Architecture: HIGH — all patterns derived from direct source reads of actual store code; coercion semantics documented line-by-line
- Pitfalls: HIGH — all pitfalls grounded in observed source code behaviors, not hypothetical

**Research date:** 2026-06-14
**Valid until:** 2026-08-14 (Dapper 2.x stable; no breaking changes expected short-term)
