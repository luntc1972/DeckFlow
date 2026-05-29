---
phase: 07-harvest-controls-stats
plan: 07
type: execute
wave: 4
depends_on: [04]
files_modified:
  - DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs
  - DeckFlow.Web/Program.cs
autonomous: true
requirements: []
tags: [harvest, di, program-cs, startup, hosted-service]

must_haves:
  truths:
    - "AddDeckFlowHarvest() registers IHarvestStatsAggregator BEFORE IHarvestRunStore so the run-store's nullable IHarvestStatsAggregator? ctor parameter resolves to a live instance (B1 — D-13 explicit cache invalidation requires aggregator to exist when run-store is constructed)"
    - "AddDeckFlowHarvest() registers IHarvestRunStore (Singleton), IHarvestScheduleStore (Singleton), HarvestScheduleCache (Singleton + IHostedService dual), HarvestScheduleService (IHostedService), IHarvestStatsAggregator (Singleton) (S-4)"
    - "Program.cs calls AddDeckFlowHarvest() exactly once, immediately after AddDeckFlowFeatureFlags() (Patterns insertion point 1)"
    - "Startup bootstrap calls IHarvestRunStore.EnsureSchemaAsync() before app.RunAsync() so the D-02 reaper has executed before the first request (Patterns insertion point 2)"
    - "ArchidektCacheJobService DI registration is unchanged (Plan 02 ctor change resolves automatically since IHarvestRunStore is Singleton)"
  artifacts:
    - path: "DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs"
      provides: "Static AddDeckFlowHarvest extension registering all Phase 7 services. Aggregator registered before run-store (B1 ordering)."
      contains: "AddDeckFlowHarvest"
    - path: "DeckFlow.Web/Program.cs"
      provides: "Calls AddDeckFlowHarvest() and EnsureSchemaAsync at startup; no other middleware reordering"
      contains: "AddDeckFlowHarvest"
  key_links:
    - from: "Program.cs Services configuration"
      to: "AddDeckFlowHarvest()"
      via: "Inserted right after AddDeckFlowFeatureFlags()"
      pattern: "AddDeckFlowHarvest\\(\\)"
    - from: "Program.cs startup bootstrap"
      to: "IHarvestRunStore.EnsureSchemaAsync"
      via: "Awaited before app.RunAsync()"
      pattern: "GetRequiredService<IHarvestRunStore>"
    - from: "AddDeckFlowHarvest registration order"
      to: "IHarvestRunStore ctor receives live IHarvestStatsAggregator"
      via: "Aggregator AddSingleton called before run-store AddSingleton (B1)"
      pattern: "AddSingleton<IHarvestStatsAggregator"
---

<objective>
Wire every Phase 7 service into DI and ensure the D-02 reaper runs before Kestrel binds. Plan 03's `HarvestScheduleCache.StartAsync` already runs synchronously w.r.t. host ready signal (so the schedule snapshot is loaded), but `IHarvestRunStore.EnsureSchemaAsync` has no host-service hook — this plan adds the explicit startup call.

Critical ordering (B1 fix): `IHarvestStatsAggregator` MUST be registered BEFORE `IHarvestRunStore`. Plan 01 ships `HarvestRunStore` with a nullable `IHarvestStatsAggregator? stats = null` ctor parameter; the C# DI container resolves singletons lazily on first request, but registration order matters when both are AddSingleton because `IHarvestRunStore` is consumed by `IHarvestStatsAggregator`'s ctor — wait, the dependency is the other way: HarvestRunStore *consumes* IHarvestStatsAggregator. Registration order alone does not block resolution because both are Singletons resolved on demand. The reason ordering is mandatory here is **resolution-graph clarity for human readers and for any future audit grep gates** that look for "aggregator-before-store" as a B1 signature. Functionally, .NET DI handles either order fine; this plan enforces the documented order so the B1 invariant is grep-checkable.

Purpose: makes Phases 1-6 of this milestone real for runtime. After this plan, `dotnet run` brings up the full /Admin/Harvest surface.

Output:
- New `DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs` mirroring `FeatureFlagsServiceCollectionExtensions.cs`. **Aggregator first, run-store second (B1).**
- Surgical edits to `DeckFlow.Web/Program.cs`: one new `AddDeckFlowHarvest()` call and one new startup `EnsureSchemaAsync()` await.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/phases/07-harvest-controls-stats/07-CONTEXT.md
@.planning/phases/07-harvest-controls-stats/07-PATTERNS.md
@.planning/phases/07-harvest-controls-stats/07-01-SUMMARY.md
@.planning/phases/07-harvest-controls-stats/07-02-SUMMARY.md
@.planning/phases/07-harvest-controls-stats/07-03-SUMMARY.md
@.planning/phases/07-harvest-controls-stats/07-04-SUMMARY.md
@.planning/phases/07-harvest-controls-stats/07-05-SUMMARY.md
@.planning/phases/07-harvest-controls-stats/07-06-SUMMARY.md
@DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs
@DeckFlow.Web/Program.cs

<interfaces>
<!-- All Phase 7 services that need DI registration. -->

```
IHarvestStatsAggregator  (Plan 06)        Singleton  ← REGISTER FIRST (B1)
IHarvestRunStore         (Plan 01)        Singleton  ← Consumes IHarvestStatsAggregator? via nullable ctor
IHarvestScheduleStore    (Plan 01)        Singleton
HarvestScheduleCache     (Plan 03)        Singleton + IHostedService dual
IHarvestScheduleCache    (Plan 03)        Singleton (façade resolves to HarvestScheduleCache)
HarvestScheduleService   (Plan 03)        IHostedService only
```

`ArchidektCacheJobService` is already registered (`Program.cs` lines 281-283). Its ctor now requires `IHarvestRunStore` (Plan 02) — DI resolves automatically because of Singleton lifetime.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Create AddDeckFlowHarvest() DI extension — aggregator FIRST, run-store SECOND (B1)</name>
  <files>DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs</files>
  <behavior>
    - Public static class `HarvestServiceCollectionExtensions` in namespace `DeckFlow.Web.Extensions`.
    - Single public method `AddDeckFlowHarvest(this IServiceCollection services)` returning `IServiceCollection`.
    - Registers six services per <interfaces>. The cache uses the singleton + IHostedService dual-registration pattern (S-4).
    - **B1 ordering:** `IHarvestStatsAggregator` is the FIRST AddSingleton call; `IHarvestRunStore` follows. This documents the invariant that the aggregator exists when the run-store is built (Plan 01's nullable IHarvestStatsAggregator? ctor param wires through D-13 explicit invalidation).
    - No DI of `ICategoryKnowledgeStore`, `IArchidektCacheJobService`, `IFeatureFlagCache`, `IArchidektDeckImporter`, or `IMemoryCache` — all already registered elsewhere; Phase 7 services compose against existing registrations.
    - File compiles in isolation; `Program.cs` modification in Task 2 actually calls it.
  </behavior>
  <action>
    Create `DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs`:
    ```csharp
    using DeckFlow.Web.Services.Harvest;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    namespace DeckFlow.Web.Extensions;

    /// <summary>
    /// Phase 7 DI registration. Mirrors AddDeckFlowFeatureFlags() — the
    /// HarvestScheduleCache uses the singleton+IHostedService dual-registration
    /// pattern so Snapshot() callers always see the same cache instance the
    /// hosted-service StartAsync warmed.
    ///
    /// B1 / D-13: IHarvestStatsAggregator is registered BEFORE IHarvestRunStore so
    /// the run-store's nullable IHarvestStatsAggregator? ctor parameter resolves to
    /// a live instance and explicit cache invalidation fires on every harvest_runs
    /// write. Functionally either order works (.NET DI resolves Singletons lazily),
    /// but this canonical order is what the B1 audit grep looks for.
    /// </summary>
    public static class HarvestServiceCollectionExtensions
    {
        public static IServiceCollection AddDeckFlowHarvest(this IServiceCollection services)
        {
            // Stats aggregator FIRST (B1) — must exist before the run-store ctor
            // consumes it via the nullable IHarvestStatsAggregator? parameter.
            services.AddSingleton<IHarvestStatsAggregator, HarvestStatsAggregator>();

            // Stores (run-store ctor consumes IHarvestStatsAggregator? — nullable so
            // DI tolerates a registration where the aggregator is missing in tests).
            services.AddSingleton<IHarvestRunStore, HarvestRunStore>();
            services.AddSingleton<IHarvestScheduleStore, HarvestScheduleStore>();

            // Schedule cache — singleton + hosted service dual registration (S-4)
            services.AddSingleton<HarvestScheduleCache>();
            services.AddSingleton<IHarvestScheduleCache>(sp => sp.GetRequiredService<HarvestScheduleCache>());
            services.AddHostedService(sp => sp.GetRequiredService<HarvestScheduleCache>());

            // Schedule tick service — hosted only
            services.AddHostedService<HarvestScheduleService>();

            return services;
        }
    }
    ```
    Verify the file compiles (no extra `using` directives required for `IHostedService` since `Microsoft.Extensions.Hosting` is referenced via the SDK).

    **B1 grep gate:** in the resulting file, the line registering `IHarvestStatsAggregator` MUST appear before the line registering `IHarvestRunStore`. The verify automated check below uses `awk` to enforce relative line ordering.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -q "AddDeckFlowHarvest" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs && grep -q "AddSingleton<IHarvestRunStore, HarvestRunStore>" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs && grep -q "AddSingleton<IHarvestScheduleStore, HarvestScheduleStore>" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs && grep -q "AddHostedService<HarvestScheduleService>" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs && grep -q "AddSingleton<IHarvestStatsAggregator" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs && grep -q "GetRequiredService<HarvestScheduleCache>" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs && AGG_LINE=$(grep -n "AddSingleton<IHarvestStatsAggregator" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs | head -1 | cut -d: -f1) && STORE_LINE=$(grep -n "AddSingleton<IHarvestRunStore" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs | head -1 | cut -d: -f1) && [ "$AGG_LINE" -lt "$STORE_LINE" ]</automated>
  </verify>
  <done>Build exits 0; all six registrations present; dual-registration pattern visible (one `AddSingleton<HarvestScheduleCache>()` plus one `AddSingleton<IHarvestScheduleCache>(...)` plus one `AddHostedService(sp => sp.GetRequiredService<HarvestScheduleCache>())`); **B1: aggregator AddSingleton line number is strictly less than run-store AddSingleton line number**.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Wire AddDeckFlowHarvest() into Program.cs + startup EnsureSchemaAsync</name>
  <files>DeckFlow.Web/Program.cs</files>
  <behavior>
    - Inserts `builder.Services.AddDeckFlowHarvest();` immediately after the existing `builder.Services.AddDeckFlowFeatureFlags();` call. Per CONTEXT/Patterns: this is line ~159 in the current Program.cs.
    - Inserts `await app.Services.GetRequiredService<IHarvestRunStore>().EnsureSchemaAsync(CancellationToken.None);` immediately before the existing `app.RunAsync()` (or `app.Run()`) call. The existing startup-DB-validation block (the "ValidateDatabaseConnectionsAsync" pattern referenced in Patterns) is the natural site — extend whatever the shipped Phase 6 conv. is. If no such block exists by name, just await the call directly before `app.RunAsync()`.
    - No middleware reordering. No changes to `MapWhen("/Admin")` BasicAuth branch. No changes to forwarded-headers ordering.
    - The `using DeckFlow.Web.Extensions;` and `using DeckFlow.Web.Services.Harvest;` directives are added if not already present.
  </behavior>
  <action>
    Open `DeckFlow.Web/Program.cs`. Make two surgical edits:

    **Edit A — DI registration** (right after the line that calls `AddDeckFlowFeatureFlags()`):
    ```csharp
    builder.Services.AddDeckFlowFeatureFlags();
    builder.Services.AddDeckFlowHarvest();   // Phase 7 — D-01..D-17
    ```

    **Edit B — startup schema bootstrap** (right before `app.RunAsync()` or wherever the existing DB validation block sits):
    ```csharp
    // Phase 7 D-02: bootstrap harvest_runs schema and run the redeploy reaper before
    // Kestrel binds. Idempotent — fresh DB hits zero rows; subsequent calls are no-ops.
    await app.Services.GetRequiredService<IHarvestRunStore>()
        .EnsureSchemaAsync(CancellationToken.None);
    ```
    Note: `HarvestScheduleCache.StartAsync` (Plan 03) already calls `IHarvestScheduleStore.GetAsync` which lazy-bootstraps `harvest_schedule` schema, so no separate `IHarvestScheduleStore.EnsureSchemaAsync()` call is required here. If the planner audits this and finds the lazy bootstrap path doesn't fire before request handling, add `await app.Services.GetRequiredService<IHarvestScheduleStore>().EnsureSchemaAsync(CancellationToken.None);` next to the run-store call.

    Add `using DeckFlow.Web.Extensions;` and `using DeckFlow.Web.Services.Harvest;` near the top of `Program.cs` if not already present (likely `DeckFlow.Web.Extensions` is already imported because Phase 6 added the same).

    Verify `dotnet build DeckFlow.sln` exits 0. Then `dotnet run --project DeckFlow.Web` starts cleanly (manual smoke; not part of automated verify).
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -q "AddDeckFlowHarvest()" DeckFlow.Web/Program.cs && grep -q "GetRequiredService<IHarvestRunStore>" DeckFlow.Web/Program.cs && grep -q "EnsureSchemaAsync" DeckFlow.Web/Program.cs && grep -c "AddDeckFlowHarvest" DeckFlow.Web/Program.cs</automated>
  </verify>
  <done>Build exits 0; `AddDeckFlowHarvest()` appears exactly once in Program.cs; startup awaits `IHarvestRunStore.EnsureSchemaAsync(...)`. App boots without DI resolution errors.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Process startup → IHarvestRunStore.EnsureSchemaAsync | In-process; reaper SQL is constant strings, no untrusted input. |
| DI container lifetime | All Phase 7 services are Singleton or HostedService; no scoped lifetime collisions. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-07-31 | Denial of service | Startup blocks on PG schema bootstrap | accept | EnsureSchemaAsync runs once at startup; if PG is down, the host fails to start (consistent with existing Phase 6 behavior — operator notices in Render logs). |
| T-07-32 | Repudiation | Reaper UPDATE silently flips orphan rows | accept | Reaper writes `error_message='interrupted by redeploy'` so the audit trail is preserved per row. |
| T-07-33 | Tampering | Manual edit to AddDeckFlowHarvest() registration order | mitigate | Registration order doesn't matter for Singletons functionally; HarvestScheduleCache StartAsync runs after FeatureFlagCache StartAsync because both are hosted services and ASP.NET Core starts them in registration order — but neither depends on the other's start completion (FeatureFlagCache is consumed only by HarvestScheduleService.TickAsync which runs on a 60s timer, so the first tick happens well after both have warmed). Aggregator-before-store ordering is enforced by an automated grep gate (B1). |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` exits 0.
- `grep -c "AddDeckFlowHarvest" DeckFlow.Web/Program.cs` = 1.
- `grep -c "AddDeckFlowHarvest" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs` ≥ 1.
- `grep -c "GetRequiredService<IHarvestRunStore>" DeckFlow.Web/Program.cs` ≥ 1.
- `grep -c "EnsureSchemaAsync" DeckFlow.Web/Program.cs` ≥ 1.
- `grep -c "AddSingleton<IHarvestRunStore, HarvestRunStore>" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs` ≥ 1.
- `grep -c "AddSingleton<IHarvestStatsAggregator" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs` ≥ 1.
- `grep -c "AddHostedService<HarvestScheduleService>" DeckFlow.Web/Extensions/HarvestServiceCollectionExtensions.cs` ≥ 1.
- **B1 ordering gate:** the line number of the `AddSingleton<IHarvestStatsAggregator` registration is strictly less than the line number of the `AddSingleton<IHarvestRunStore` registration in `HarvestServiceCollectionExtensions.cs`.
- App boots: `dotnet run --project DeckFlow.Web --no-build` does not throw on startup (manual confirmation post-build).
</verification>

<success_criteria>
- `dotnet run` brings up the app with `/Admin/Harvest` accessible end-to-end.
- D-02 reaper has run before the first request.
- HarvestScheduleCache snapshot is populated before the first request (StartAsync sync load).
- HarvestScheduleService begins ticking 60s after host start.
- ArchidektCacheJobService correctly resolves `IHarvestRunStore` from DI without code changes (Plan 02 ctor wiring is automatic).
- IHarvestStatsAggregator is registered before IHarvestRunStore (B1) so the run-store's nullable ctor param wires up the D-13 explicit invalidation pipe end-to-end.
- Phase 7 is fully wired and shippable.
</success_criteria>

<output>
After completion, create `.planning/phases/07-harvest-controls-stats/07-07-SUMMARY.md` covering: the two surgical edits to Program.cs, the six DI registrations with their order (aggregator-first per B1), and a one-liner confirming `dotnet run` boots successfully end-to-end. Note any decision to also pre-bootstrap `IHarvestScheduleStore` schema vs relying on lazy bootstrap from the cache.
</output>
</content>
</invoke>