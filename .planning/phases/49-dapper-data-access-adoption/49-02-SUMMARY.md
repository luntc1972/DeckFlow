# Phase 49-02 Summary

## Outcome

- Completed Tasks 1-3 in order on `v1.7`.
- Added the sanctioned fifth `DateTimeOffsetTypeHandler` and registered it with the same unconditional D-07 default recorded by `49-GATE-VERDICT.md`: `RemoveTypeMap(typeof(T))`, `RemoveTypeMap(typeof(T?))`, then `AddTypeHandler(...)`.
- Converted the six wave-3 stores' non-DDL paths to Dapper while keeping SQL text verbatim.

## Store Sweep

- `BlockedVideoStore`: converted add/remove/check/list paths to Dapper; removed local `DateTimeOffset` coercion.
- `ContentSourceStore`: converted insert/get/update/list paths to Dapper; removed local bool and `DateTimeOffset` coercion; preserved generated-id missing-row behavior through `ContentStoreGeneratedId.Read`.
- `SpendLedgerBase`, `LlmSpendLedger`, `WhisperSpendLedger`: converted monthly-total reads and call-record writes to Dapper; removed local decimal/`DateTimeOffset` coercion helpers.
- `AdminBruteForceTrackerStore`: converted read/write paths to Dapper; preserved provider-specific UPSERT arithmetic verbatim.
- `FeatureFlagStore`: converted `GetAllAsync` and `SetEnabledAsync` to Dapper; DDL + seed remain raw.
- `HarvestScheduleStore`: converted `GetAsync` and `SaveAsync` to Dapper; DDL + seed remain raw.

## Handler Notes

- `DateTimeOffsetTypeHandler` matches the locked D-06 semantics:
  - SQLite write path stores `value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)`.
  - Postgres write path stores native UTC `DateTime`.
  - String reads use the locked two-step parse: `RoundtripKind` first, then `AssumeUniversal | AdjustToUniversal` fallback without `RoundtripKind`.
- `DeckFlow.Web.Tests/Integration/DapperTypeHandlerRoundTripTests.cs` now includes the 49-02 `DateTimeOffset` SQLite raw-on-disk assertion and Postgres env-gated parity fact.

## Aliases

- No per-query `AS` aliases were required in this wave.

## Verification

- `dotnet build DeckFlow.sln`: `0 Warning(s)`, `0 Error(s)`.
- Handler registration counts after the fifth handler:
  - `SqlMapper.RemoveTypeMap`: `10`
  - `SqlMapper.AddTypeHandler`: `5`
- SQLite verification passed for:
  - `DapperTypeHandlerRoundTripTests`
  - `BlockedVideoStore` + `ContentSourceStore` test slice
  - `LlmSpendLedgerTests` + `WhisperSpendLedgerTests`
  - `AdminBruteForceTrackerStoreTests`
  - full `DeckFlow.Web.Tests` suite
- Postgres facts in the round-trip suite remain env-gated and skipped when `DECKFLOW_POSTGRES_TESTS` is unset.
