---
phase: 49-dapper-data-access-adoption
verified: 2026-06-17T02:24:17Z
status: human_needed
score: 7/7 must-haves verified
overrides_applied: 0
human_verification:
  - test: "Run Postgres parity gate before considering phase fully closed"
    expected: "DECKFLOW_POSTGRES_TESTS=1 dotnet test DeckFlow.Web.Tests passes with 0 failures on the Postgres-gated facts (round-trip handlers + store integration tests)"
    why_human: "Postgres container test requires Docker + env var; cannot be verified without a running Postgres instance. The SQLite path is fully proven; the PG path is a documented manual gate per 49-VALIDATION.md."
---

# Phase 49: Dapper Data-Access Adoption — Verification Report

**Phase Goal:** Replace raw ADO.NET reader/param boilerplate in the dual-provider store classes with Dapper behind the existing IRelationalDialect/RelationalDatabaseConnection abstraction; provider-aware type handlers preserve Sqlite+Postgres parity; DDL/migration + unnest-batch paths stay raw SQL; pure refactor (no behavior change).
**Verified:** 2026-06-17T02:24:17Z
**Status:** human_needed
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Dapper 2.1.79 referenced by DeckFlow.Core; solution builds 0/0 | VERIFIED | `DeckFlow.Core.csproj` contains `<PackageReference Include="Dapper" Version="2.1.79" />`; SUMMARY.md records 0W/0E on every wave gate |
| 2 | Five provider-aware type handlers (DateTime, decimal, bool, Guid, DateTimeOffset) with unconditional RemoveTypeMap(T)+RemoveTypeMap(T?) before AddTypeHandler per D-07 | VERIFIED | `DapperTypeHandlers.cs`: 10 RemoveTypeMap calls, 5 AddTypeHandler calls, 2 DateTimeOffsetTypeHandler registrations; file read directly confirms unconditional pattern with no conditional branching |
| 3 | EnsureRegistered() called exactly once via RelationalDatabaseConnection static constructor | VERIFIED | `RelationalDatabaseConnection.cs:23-26` — `static RelationalDatabaseConnection()` calls `DapperTypeHandlers.EnsureRegistered()`; Interlocked.Exchange guard in EnsureRegistered() ensures idempotent registration |
| 4 | All 13 swept stores execute non-DDL paths through Dapper with zero store-local coercion | VERIFIED | Phase-wide `ExecuteReaderAsync` grep returns 0 across all 13 converted stores (non-DDL); 3 sanctioned carve-outs confirmed in raw DDL/introspection methods (HarvestRunStore:477 sqlite_master, ContentSiteIndexStore:604+625 PRAGMA/information_schema); all stores have `using Dapper` and Dapper method calls confirmed |
| 5 | DDL/migration + RequestMetricsStore unnest-batch paths stay raw ADO.NET with `// Why:` comments | VERIFIED | FeedbackStore:252, ContentHarvestRunStore:51, HarvestRunStore:79, ContentSiteIndexStore and CategoryKnowledgeRepository each have `// Why:` DDL comments; RequestMetricsStore:164-165 has `// Why: Phase 49 leaves this method on raw ADO.NET because the unnest-array NpgsqlParameter batch shape has no Dapper equivalent`; body unchanged |
| 6 | IRelationalDialect/RelationalDatabaseConnection abstraction public surface unchanged | VERIFIED | `IRelationalDialect.cs` interface members unchanged; `RelationalDatabaseConnection` public surface (`Provider`, `ConnectionString`, `Dialect`, `CreateConnection`, `OpenConnectionAsync`, `IsSqlite`, `IsPostgres`, `FromSqlitePath`, `ExtractSqlitePath`, `AddParameter`) all preserved |
| 7 | CategoryKnowledgeRepository transaction correctness: every in-scope Dapper call carries `transaction: transaction`; no leftover `command.Transaction = transaction` in non-DDL | VERIFIED | Grep: `transaction:` count = 11; `command.Transaction = transaction` count = 0 in non-DDL; Dapper call count = 38; 49-04 SUMMARY contains full per-call-site checklist (rows 1-7, H1-H6) all marked complete |

**Score:** 7/7 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Core/Storage/DapperTypeHandlers.cs` | 5 type handlers + EnsureRegistered() with unconditional RemoveTypeMap-before-AddTypeHandler per D-07 | VERIFIED | 172 lines; 10 RemoveTypeMap + 5 AddTypeHandler + 1 MatchNamesWithUnderscores; DateTimeOffsetTypeHandler two-step parse confirmed (RoundtripKind first, AssumeUniversal fallback — not ORed); all 5 handlers: DateTime, decimal, bool, Guid, DateTimeOffset |
| `DeckFlow.Web.Tests/Integration/DapperTypeHandlerRoundTripTests.cs` | Raw on-disk write-path assertions for all 5 handler types; Postgres env-gated facts | VERIFIED | 237 lines; `GetFieldType` assertions present; raw value assertions at lines 34 and 155 (`ToString("O", InvariantCulture)`); 5 SQLite [Fact] methods with `_WithRawWritePathProof` suffix; Postgres [PostgresFact] methods present |
| `.planning/phases/49-dapper-data-access-adoption/49-GATE-VERDICT.md` | `VERDICT: PASS` with (a)(b)(c)(d) evidence; Handler-Count Note; Postgres Parity Command | VERIFIED | Top line `VERDICT: PASS`; contains Handler-Count Note section; contains Postgres Parity Command section; all 3 required grep targets found |
| `.planning/phases/49-dapper-data-access-adoption/49-GATE-ABORT.md` | `GATE: AUTHORIZED` consistent with VERDICT: PASS | VERIFIED | First line is `GATE: AUTHORIZED` |
| `DeckFlow.Web/Services/FeedbackStore.cs` | Zero store-local coercion; no ExecuteReaderAsync/GetInt64/GetString/DateTime.Parse/ToString("O"); ReadItem deleted; `// Why:` on DDL | VERIFIED | Comment-filtered coercion grep = 0; 8 Dapper calls; `using Dapper` present; ReadItem absent (grep returns 0); `// Why:` at line 252 |
| `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` | Heaviest store; 38 Dapper calls; transaction: on every in-scope call; card-id cache preserved | VERIFIED | 38 Dapper calls confirmed; 11 `transaction:` usages; 0 leftover `command.Transaction = transaction`; 4 `// Why:` DDL comments |
| `DeckFlow.Web/Services/Analytics/RequestMetricsStore.cs` | `// Why:` carve-out comment only; UpsertBatchAsync body unchanged | VERIFIED | `// Why:` at line 164-165; NpgsqlParameter + unnest array body intact; diff is comment-only as documented |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `RelationalDatabaseConnection.cs` | `DapperTypeHandlers.EnsureRegistered()` | static constructor | VERIFIED | Lines 23-26: `static RelationalDatabaseConnection()` calls `DapperTypeHandlers.EnsureRegistered()` |
| `DapperTypeHandlers.cs` | SqlMapper built-in type map override (D-07) | `RemoveTypeMap(T) + RemoveTypeMap(T?)` before `AddTypeHandler`, unconditionally | VERIFIED | 10 RemoveTypeMap calls in sequential pairs (T, T?) for each of 5 types; no conditional branching; direct file read confirms |
| `FeedbackStore.cs` | Dapper QueryAsync/ExecuteScalarAsync | connection extension methods (no ExecuteReaderAsync) | VERIFIED | `using Dapper`; 8 Dapper calls; 0 ExecuteReaderAsync in non-DDL; IsPostgres DateTime ternary removed |
| `CategoryKnowledgeRepository.cs` | Dapper ExecuteAsync/ExecuteScalarAsync with `transaction: transaction` | BeginTransactionAsync scope | VERIFIED | 11 `transaction:` bearers against 38 Dapper calls; 0 leftover ADO.NET transaction assignments |
| `ContentSiteIndexStore.cs` / `HarvestRunStore.cs` | Raw ADO.NET ExecuteReaderAsync | DDL/schema-introspection carve-outs only | VERIFIED | HarvestRunStore:477 is `sqlite_master` index introspection; ContentSiteIndexStore:604 is `PRAGMA table_info`; :625 is `information_schema.columns` — all 3 are sanctioned DDL/introspection paths |

### Data-Flow Trace (Level 4)

Not applicable — this is a pure refactor phase. No new user-visible data flows were added; existing data flows were preserved via behavior-neutral Dapper substitution. The round-trip tests with raw on-disk assertions constitute the behavioral correctness proof.

### Behavioral Spot-Checks

| Behavior | Evidence | Status |
|----------|----------|--------|
| Build 0 errors, 0 warnings | SUMMARY 49-04: `0 Warning(s)`, `0 Error(s)` | VERIFIED (build not re-run per instructions; known clean) |
| Core.Tests 346 passed / 0 failed | SUMMARY 49-04: `Passed: 346, Failed: 0, Skipped: 0` | VERIFIED (SQLite) |
| Web.Tests 622 passed / 0 failed / 11 skipped | SUMMARY 49-04: `Passed: 622, Failed: 0, Skipped: 11` | VERIFIED (SQLite; 11 skips are Postgres-gated) |

Step 7b behavioral spot-checks skipped for build commands per "build known clean — do not rebuild" instruction.

### Probe Execution

No probes declared for this phase. Step 7c: SKIPPED (no probe scripts in this refactor phase).

### Requirements Coverage

This is a pure refactor phase — no new REQ-IDs were introduced (confirmed per task instruction). The phase addresses internal quality improvements (DAP-01, DAP-02, DAP-03) which are refactor-class requirements not tracked in the public REQUIREMENTS.md.

| Requirement | Source Plan | Description | Status |
|-------------|------------|-------------|--------|
| DAP-01 | 49-01-PLAN.md | Dapper package added to DeckFlow.Core | SATISFIED — `Dapper 2.1.79` in Core.csproj |
| DAP-02 | 49-01-PLAN.md | Provider-aware type handlers with D-07 RemoveTypeMap default | SATISFIED — 5 handlers, 10 RemoveTypeMap calls, file verified |
| DAP-03 | 49-01 through 49-04 PLAN.md | All 13 eligible stores converted; DDL carve-outs documented; zero store-local coercion | SATISFIED — phase-wide grep confirms 0 ExecuteReaderAsync in non-DDL across all 13 stores |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | — | Debt marker scan (TODO/FIXME/TBD/XXX/HACK/PLACEHOLDER) across all 14 converted files | — | Clean: no markers found |

No anti-patterns detected. No unresolved debt markers in any file modified by this phase.

### Human Verification Required

#### 1. Postgres Parity Gate

**Test:** With Docker available and `DECKFLOW_POSTGRES_TESTS=1` set, run:
```
DECKFLOW_POSTGRES_TESTS=1 "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj
```
**Expected:** All Postgres-gated tests pass — the `[PostgresFact]` round-trip tests in `DapperTypeHandlerRoundTripTests.cs` and any store integration tests using `PostgresContainerFixture` complete with 0 failures. The 11 currently-skipped tests should run and pass.
**Why human:** Requires a running Docker daemon to start the Postgres container via `PostgresContainerFixture` and the `DECKFLOW_POSTGRES_TESTS` environment variable set in the shell. Cannot be verified programmatically without that runtime infrastructure.

### Gaps Summary

No gaps. All 7 observable truths are VERIFIED with file:line evidence. The only open item is the Postgres parity gate, which is a documented manual gate per 49-VALIDATION.md — not a gap in the Dapper adoption itself. The SQLite path (the primary correctness proof including raw on-disk write-path assertions) is fully verified.

The two sanctioned raw ExecuteReaderAsync sites in HarvestRunStore (sqlite_master introspection) and ContentSiteIndexStore (PRAGMA table_info + information_schema.columns) are intentional DDL/schema-introspection carve-outs, not missed conversions.

---

_Verified: 2026-06-17T02:24:17Z_
_Verifier: Claude (gsd-verifier)_
