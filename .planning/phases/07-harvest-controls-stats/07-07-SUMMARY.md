---
phase: 07-harvest-controls-stats
plan: 07
title: DI wiring + Program.cs startup gate
wave: 4
status: complete
shipped: 2026-05-03
requirements: [HARV-04, HARV-05, HARV-07]
---

# Plan 07-07 — DI wiring + Program.cs startup gate

## What shipped

Phase 7 harvest services are now wired into the runtime through a dedicated `AddDeckFlowHarvest()` extension, and `Program.cs` now bootstraps both harvest schemas before Kestrel binds. That makes the D-02 startup reaper run on every process start and guarantees the single-row `harvest_schedule` table exists before the hosted cache starts serving requests.

## Files modified

| File | Change |
|------|--------|
| `DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs` | NEW — adds the six Phase 7 registrations, including B1 aggregator-first ordering and the singleton + hosted-service dual registration for `HarvestScheduleCache` |
| `DeckFlow.Web/Program.cs` | Adds `AddDeckFlowHarvest(builder.Environment)` after feature flags and awaits both harvest `EnsureSchemaAsync()` calls before `app.RunAsync()` |
| `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs` | Adds `CreateHarvestStateConnection(IWebHostEnvironment)` as the dedicated harvest-state connection factory |
| `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` | DI ctor now uses `CreateHarvestStateConnection(...)` |
| `DeckFlow.Web/Services/Harvest/HarvestScheduleStore.cs` | DI ctor now uses `CreateHarvestStateConnection(...)` |

## Registration order

The new extension registers these services in this order:

1. `IHarvestStatsAggregator` → `NullHarvestStatsAggregator`
2. `IHarvestRunStore` → `HarvestRunStore`
3. `IHarvestScheduleStore` → `HarvestScheduleStore`
4. `HarvestScheduleCache` singleton
5. `IHarvestScheduleCache` → the singleton `HarvestScheduleCache`
6. `HarvestScheduleCache` again as `IHostedService`
7. `HarvestScheduleService` as `IHostedService`

B1 is preserved deliberately: the aggregator registration appears before the run-store registration so the invalidation dependency is present when the store is constructed. The current implementation is a no-op scaffold; Plan 06 in Wave 5 replaces it with the real `HarvestStatsAggregator`.

## Startup bootstrap

`Program.cs` now performs these startup actions before `app.RunAsync()`:

1. Existing `ValidateDatabaseConnectionsAsync(...)`
2. `IHarvestRunStore.EnsureSchemaAsync()` — creates `harvest_runs` if needed and runs the D-02 redeploy reaper
3. `IHarvestScheduleStore.EnsureSchemaAsync()` — creates `harvest_schedule` and seeds the default-Off row

`HarvestScheduleCache` still performs its own synchronous `StartAsync` reload, so the cache snapshot is also warm before the app reports ready.

## Build / verification

`dotnet build DeckFlow.Web/DeckFlow.Web.csproj --no-restore` passes cleanly in this workspace. The solution-level build path is still subject to the existing repo quirk where `DeckFlow.sln` restore can fail at the SDK workload-resolver layer, so the per-project no-restore build remains the reliable verification path in this environment.
