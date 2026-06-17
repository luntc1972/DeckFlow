# Phase 49-03 Summary

## Outcome

- Completed Tasks 1-4 in order on `v1.7`.
- Converted the four wave-4 stores' non-DDL paths to Dapper while keeping SQL text verbatim.
- Preserved the locked D-06/D-07 handler regime from earlier waves without modifying handler registration.

## Store Sweep

- `ContentHarvestRunStore`: converted start/get/complete paths to Dapper; removed local decimal and `DateTimeOffset` coercion helpers; `EnsureSchemaAsync` stays raw.
- `HarvestRunStore`: converted all non-migration methods to Dapper with `CommandDefinition` cancellation propagation; Guid and nullable `DateTimeOffset?` now flow through the global handlers; the constraint-migration path stays raw.
- `ContentVideoStore`: converted all non-DDL reads/writes/counts to Dapper; removed local `DateTimeOffset` coercion helpers; schema creation and filtered-distill constraint migration stay raw.
- `ContentSiteIndexStore`: converted all non-DDL reads/writes to Dapper via a local Dapper row DTO for natural-key/tag reconstruction; removed local bool and `DateTimeOffset` coercion helpers; `EnsureSchemaAsync` ALTERs and `GetTableColumnsAsync` introspection stay raw.

## Aliases

- One per-query alias was required in this wave:
  - `HarvestRunStore.GetRecentRevisionAsync` uses `AS started_utc`, `AS completed_utc`, and `AS count` so Dapper can map the aggregate row cleanly.

## Raw Carve-Outs

- No methods were left raw beyond the documented carve-outs.
- Remaining raw reader usage is limited to:
  - `HarvestRunStore` SQLite constraint-migration index introspection helper.
  - `ContentSiteIndexStore.GetTableColumnsAsync` schema introspection for SQLite and Postgres.

## Verification

- `dotnet build DeckFlow.sln`: `0 Warning(s)`, `0 Error(s)` on every task gate.
- SQLite verification passed for:
  - `ContentHarvestRunStoreTests`
  - `HarvestRunStoreTests`
  - `ContentVideoStoreTests` + `ContentVideoStoreDistillTests`
  - `ContentSiteIndexStoreTests` + `ContentSiteIndexStoreVisibilityTests` + `ContentSiteIndexStoreApprovalTests`
- Postgres tests remain env-gated and were not run in this wave.
