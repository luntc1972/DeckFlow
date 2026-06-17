# Phase 49-04 Summary

## Outcome

- Completed Task 1 and Task 2 on `v1.7` with atomic `feat(49)` commits.
- Completed Task 3 documentation inside the fence by adding the `RequestMetricsStore.UpsertBatchAsync` intentional-raw `// Why:` carve-out comment.
- Phase-wide parity gate: PASS.

## Files

- Converted to Dapper:
  - `DeckFlow.Web/Services/CategoryKnowledgeStore.cs`
  - `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs`
- Intentional raw carve-out documented:
  - `DeckFlow.Web/Services/Analytics/RequestMetricsStore.cs`

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln`
  - `0 Warning(s)`
  - `0 Error(s)`
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`
  - `Passed: 346`
  - `Failed: 0`
  - `Skipped: 0`
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`
  - `Passed: 622`
  - `Failed: 0`
  - `Skipped: 11`
  - Postgres-side tests remained env-gated skips as expected.

## Converted Store Checks

- `CategoryKnowledgeStore`
  - Comment-filtered `ExecuteReaderAsync` count: `0`
  - `CoerceCount` retained for tests
  - Targeted tests: `Passed: 23, Failed: 0, Skipped: 0`
- `CategoryKnowledgeRepository`
  - Comment-filtered `ExecuteReaderAsync` count: `0`
  - Coercion-residue grep (`GetInt64|GetString|GetBoolean|DateTime.Parse|ToString("O")|ExecuteReaderAsync`): `0`
  - Targeted tests: `Passed: 17, Failed: 0, Skipped: 0`

## Phase-Wide Parity Gate

PASS. The eligible-store sweep is reader-loop-free for all non-DDL methods. The only remaining `ExecuteReaderAsync` sites are sanctioned DDL/schema-introspection/migration carve-outs plus the separately sanctioned raw `RequestMetricsStore` unnest batch:

- `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs:477`
  - SQLite `sqlite_master` index-name introspection for the constraint-migration helper.
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs:530`
  - SQLite `PRAGMA table_info` schema introspection helper.
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs:551`
  - Postgres `information_schema.columns` schema introspection helper.
- `DeckFlow.Web/Services/Analytics/RequestMetricsStore.cs`
  - `UpsertBatchAsync` remains raw by design for `NpgsqlParameter` unnest-array batch binding; comment-only diff recorded in this task.

Eligible-file grep results:

- `FeedbackStore`: `0`
- `BlockedVideoStore`: `0`
- `ContentSourceStore`: `0`
- `SpendLedgerBase`: `0`
- `LlmSpendLedger`: `0`
- `WhisperSpendLedger`: `0`
- `AdminBruteForceTrackerStore`: `0`
- `FeatureFlagStore`: `0`
- `HarvestScheduleStore`: `0`
- `ContentHarvestRunStore`: `0`
- `HarvestRunStore`: `1` sanctioned carve-out
- `ContentVideoStore`: `0`
- `ContentSiteIndexStore`: `2` sanctioned carve-outs
- `CategoryKnowledgeStore`: `0`
- `CategoryKnowledgeRepository`: `0`

## Transaction Correctness Checklist

Threat `T-49-11` acceptance is satisfied. `CategoryKnowledgeRepository` now carries `transaction: transaction` on every transaction-scoped Dapper call, with `0` leftover `command.Transaction = transaction` assignments in converted non-DDL code. Direct transaction-bearing Dapper call sites in the file: `11`.

Checklist:

| Row | Method / Helper | Status | Evidence |
| --- | --- | --- | --- |
| 1 | `ReplaceSourceRowsAsync` | complete | `ResolveSourceIdForReadAsync` nullable-forwarding at line 1052 or `ResolveSourceIdAsync` at line 1072; delete at line 421; `ResolveCardIdAsync` at line 1037; `UpsertCategoryObservationAsync` at line 1135 |
| 2 | `DeleteSourceDataAsync` | complete | `ResolveSourceIdForReadAsync` at line 1052; deletes at lines 475 and 481 |
| 3 | `PersistObservedCategoriesAsync` | complete | `ResolveSourceIdAsync` at line 1072; `ResolveCardIdAsync` at line 1037; `UpsertCategoryObservationAsync` at line 1135 |
| 4 | `PersistCardDeckTotalsAsync` | complete | `ResolveSourceIdAsync` at line 1072; `ResolveCardIdAsync` at line 1037; `UpsertCardDeckTotalAsync` at line 1166 |
| 5 | `PersistDeckCategoryBatchAsync` observations loop | complete | `ResolveSourceIdAsync` at line 1072; `ResolveCardIdAsync` at line 1037; `UpsertCategoryObservationAsync` at line 1135 |
| 6 | `PersistDeckCategoryBatchAsync` totals loop | complete | `ResolveCardIdAsync` at line 1037; `UpsertCardDeckTotalAsync` at line 1166 |
| 7 | `AddDeckIdsAsync` | complete | batch UPSERT at line 734 |
| 8 | `MarkDecksProcessedAsync` | complete | per-deck update at line 1008 |
| H1 | `ResolveCardIdAsync` | complete | `RETURNING id` call at line 1037 |
| H2 | `ResolveSourceIdForReadAsync` | complete | nullable transaction forwarded at line 1052; read-outside-tx `null` call site remains at line 682 |
| H3 | `ResolveSourceIdAsync` | complete | helper read at line 1096; source UPSERT `RETURNING id` at line 1072 |
| H4 | `ResolveDeckQueueIdForSourceAsync` | complete | deck-queue id read at line 1096 |
| H5 | `UpsertCategoryObservationAsync` | complete | UPSERT write at line 1135 |
| H6 | `UpsertCardDeckTotalAsync` | complete | UPSERT write at line 1166 |

Counter checks:

- Dapper calls (`QueryAsync|QuerySingleOrDefaultAsync|ExecuteAsync|ExecuteScalarAsync`): `38`
- Calls carrying `transaction:`: `11`
- Leftover `.Transaction = transaction` in non-DDL code: `0`

## RequestMetricsStore Carve-Out

- `RequestMetricsStore.UpsertBatchAsync` was not converted.
- Diff remained comment-only:
  - Added a `// Why:` note stating the unnest-array `NpgsqlParameter` batch shape has no Dapper equivalent and remains raw by design per the Phase 49 boundaries.

## Manual Postgres Gate

Run before `/gsd:verify-work`:

```bash
DECKFLOW_POSTGRES_TESTS=1 "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj
```

## Phase 44 Note

Phase 44 also touches `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs`. Because Phase 49 converted that file first, Phase 44 must be re-checked against the converted Dapper version before Phase 44 executes.
