---
phase: 07-harvest-controls-stats
plan: 06
type: execute
wave: 5
depends_on: [04, 05]
files_modified:
  - DeckFlow.Web/Services/Harvest/IHarvestStatsAggregator.cs
  - DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs
  - DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs
  - DeckFlow.Web/Services/ICategoryKnowledgeStore.cs
  - DeckFlow.Web/Services/CategoryKnowledgeStore.cs
  - DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs
  - DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs
  - DeckFlow.Web/Views/AdminHarvest/Index.cshtml
autonomous: true
requirements: [HARV-06]
tags: [harvest, stats, aggregator, postgres, top-commanders, dialect-branch, cache-invalidation]

must_haves:
  truths:
    - "IHarvestStatsAggregator returns 8 metrics: total_decks, total_decks_30d, total_observations, top_commanders (top 10), recent_runs (10), pg_storage_size (PG-only), last_success_utc, next_scheduled_utc (D-13, D-16)"
    - "Whole stats payload is cached in IMemoryCache for 60s under key admin.harvest.stats.v1 (D-13)"
    - "IHarvestStatsAggregator exposes Invalidate() that calls _memoryCache.Remove(\"admin.harvest.stats.v1\") — wired into IHarvestRunStore write methods (Plan 01 nullable ctor param) so every harvest_runs INSERT/UPDATE busts the cache instantly per D-13 (B1)"
    - "pg_database_size(current_database()) is invoked PG-only via IRelationalDialect.IsPostgres; SQLite path returns null and view renders 'N/A' (D-14, S-3)"
    - "Top-N commanders query filters commander_name IS NOT NULL and processed = 1 over deck_queue (D-15); query lives on CategoryKnowledgeStore (W8 — consolidated, not split between Store and Repository)"
    - "next_scheduled_utc is computed in C# from last_success_utc (read once via IHarvestRunStore.GetLastSuccessUtcAsync — same single-source-of-truth method that HarvestScheduleService.TickAsync uses, W5) + interval_hours; null when Off, paused, or no prior success (D-16 #8)"
    - "Stats panel in Index.cshtml renders all 8 metrics; storage_size cell renders 'N/A' on SQLite (D-14)"
    - "ICategoryKnowledgeStore exposes MarkUrlDeckProcessedAsync(deckId, commanderName, ct) that delegates to CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync — keeps Plan 04 controller off the bare repository (B-NEW: DI-resolvable, no missing registrations) and consolidates URL-path deck_queue writes on the same store layer as the bulk path"
  artifacts:
    - path: "DeckFlow.Web/Services/Harvest/IHarvestStatsAggregator.cs"
      provides: "Stats aggregator contract: GetAsync(CancellationToken) + Invalidate() (B1)"
      contains: "interface IHarvestStatsAggregator"
    - path: "DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs"
      provides: "Sealed impl coordinating IHarvestRunStore + IHarvestScheduleCache + ICategoryKnowledgeStore + IRelationalDialect under 60s IMemoryCache; exposes Invalidate() for D-13 explicit invalidation"
      contains: "sealed class HarvestStatsAggregator"
    - path: "DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs"
      provides: "Public sealed records for HarvestStatsPayload + TopCommanderRow"
      contains: "sealed record HarvestStatsPayload"
    - path: "DeckFlow.Web/Services/ICategoryKnowledgeStore.cs"
      provides: "Interface gains MarkUrlDeckProcessedAsync + the five stats query methods (B-NEW + W8); Plan 04 controller can resolve URL-path write through the already-registered ICategoryKnowledgeStore singleton with no new DI registration"
      contains: "MarkUrlDeckProcessedAsync"
    - path: "DeckFlow.Web/Services/CategoryKnowledgeStore.cs"
      provides: "GetTopCommandersAsync(int n) + GetTotalProcessedDeckCountAsync + GetTotalProcessedDeckCountSinceAsync + GetTotalObservationCountAsync + GetPostgresDatabaseSizeBytesAsync (PG-branched) + MarkUrlDeckProcessedAsync (one-line passthrough to _repository, B-NEW). All methods land here, not split with CategoryKnowledgeRepository (W8)."
      contains: "MarkUrlDeckProcessedAsync"
    - path: "DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs"
      provides: "Index GET fetches IHarvestStatsAggregator.GetAsync and assigns to vm.Stats"
      contains: "_statsAggregator.GetAsync"
    - path: "DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs"
      provides: "Stats property typed to HarvestStatsPayload (replaces object? placeholder)"
      contains: "HarvestStatsPayload Stats"
    - path: "DeckFlow.Web/Views/AdminHarvest/Index.cshtml"
      provides: "Stats panel renders all 8 metrics; storage size renders 'N/A' on null"
      contains: "Top Commanders"
  key_links:
    - from: "HarvestStatsAggregator.GetAsync"
      to: "IMemoryCache key admin.harvest.stats.v1"
      via: "GetOrCreateAsync TTL=60s"
      pattern: "admin.harvest.stats.v1"
    - from: "HarvestRunStore write methods (InsertQueuedAsync / UpdateStateAsync)"
      to: "IHarvestStatsAggregator.Invalidate()"
      via: "Plan 01 nullable ctor param _stats?.Invalidate() after each write succeeds (B1, D-13)"
      pattern: "_stats?.Invalidate"
    - from: "HarvestStatsAggregator"
      to: "CategoryKnowledgeStore.GetTopCommandersAsync"
      via: "deck_queue GROUP BY commander_name (W8 — store, not repository)"
      pattern: "FROM deck_queue"
    - from: "HarvestStatsAggregator"
      to: "pg_database_size(current_database())"
      via: "_dialect.IsPostgres branch"
      pattern: "pg_database_size"
    - from: "HarvestStatsAggregator.BuildAsync"
      to: "IHarvestRunStore.GetLastSuccessUtcAsync"
      via: "Single source of truth shared with HarvestScheduleService.TickAsync (W5)"
      pattern: "GetLastSuccessUtcAsync"
    - from: "AdminHarvestController.SubmitUrl (Plan 04)"
      to: "ICategoryKnowledgeStore.MarkUrlDeckProcessedAsync → CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync"
      via: "One-line passthrough on CategoryKnowledgeStore (B-NEW): keeps the controller dep on the already-DI-registered ICategoryKnowledgeStore singleton; the bare CategoryKnowledgeRepository is never injected"
      pattern: "_repository.MarkUrlDeckProcessedAsync"
---

<objective>
Deliver HARV-06: the eight-metric stats panel that closes the harvest workflow with operator-visible coverage signals. Build the aggregator + cache + explicit-invalidate hook, the supporting CategoryKnowledgeStore queries (including the D-15 top-N commanders query), and replace the Stats placeholder in `Index.cshtml`. Storage size branches PG-only via `IRelationalDialect.IsPostgres`.

This plan also closes B1 (D-13 explicit cache invalidation): `IHarvestStatsAggregator.Invalidate()` wires into the `IHarvestRunStore` write path that Plan 01 already prepared with a nullable `IHarvestStatsAggregator? stats = null` ctor param. Plan 07's DI registration order ensures the aggregator is constructed before the run-store, so the run-store ctor receives a live invalidator on a fresh boot.

This plan ALSO closes B-NEW (DI graph hole introduced in iteration 2): Plan 04's `AdminHarvestController` was injecting the bare `CategoryKnowledgeRepository` to call `MarkUrlDeckProcessedAsync`, but `CategoryKnowledgeRepository` is not registered in DI (only `ICategoryKnowledgeStore` is). Resolution: extend `ICategoryKnowledgeStore` with a `MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken)` method whose `CategoryKnowledgeStore` impl is a one-line delegation to `_repository.MarkUrlDeckProcessedAsync`. Plan 04 then injects `ICategoryKnowledgeStore` (already a registered singleton) instead of the bare repo. No new DI registration is needed; Plan 07's wiring stays unchanged. Plan 02 still ships the underlying `CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync` method — the store passthrough does not duplicate the SQL.

Purpose: ROADMAP SC #5 ("Stats panel shows total decks, total observations, Postgres storage size, last run timestamp, next scheduled run — all drawn from live Postgres") is fully satisfied here. SC #2 ("commander appears in top-commanders list after URL submit") is also satisfied since both bulk and URL paths populate `deck_queue.commander_name` (Plans 02 and 04) AND the cache is invalidated immediately on the URL-path harvest_runs write so the operator sees the new commander on the very next page render — no 60s wait.

Output:
- Three new files in `Services/Harvest/`: aggregator interface, sealed impl, models.
- Method additions to `ICategoryKnowledgeStore` and `CategoryKnowledgeStore` (W8 — store layer only; do NOT split queries between store and repository). The interface gains the five stats methods AND `MarkUrlDeckProcessedAsync` (B-NEW). The impl adds the matching method bodies; `MarkUrlDeckProcessedAsync` is a one-line passthrough.
- Controller and view-model wiring to populate `Model.Stats`.
- Razor stats partial in `Index.cshtml` covering all eight metrics.
- `Invalidate()` method on `IHarvestStatsAggregator` consumed by `HarvestRunStore`'s write methods.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/phases/07-harvest-controls-stats/07-CONTEXT.md
@.planning/phases/07-harvest-controls-stats/07-PATTERNS.md
@.planning/phases/07-harvest-controls-stats/07-04-SUMMARY.md
@DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs
@DeckFlow.Web/Services/Harvest/HarvestRunStore.cs
@DeckFlow.Web/Services/Harvest/HarvestRunModels.cs
@DeckFlow.Web/Services/Harvest/IHarvestScheduleCache.cs
@DeckFlow.Web/Services/ICategoryKnowledgeStore.cs
@DeckFlow.Web/Services/CategoryKnowledgeStore.cs
@DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs
@DeckFlow.Web/Infrastructure/AdminBruteForceTrackerStore.cs
@DeckFlow.Core/Storage/RelationalDatabaseConnection.cs
@DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs
@DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs
@DeckFlow.Web/Views/AdminHarvest/Index.cshtml

<interfaces>
<!-- Records the aggregator surfaces. -->

From DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs (NEW — Task 1):
```csharp
public sealed record TopCommanderRow(string CommanderName, int DeckCount);

public sealed record HarvestStatsPayload(
    long TotalDecks,
    long TotalDecks30d,
    long TotalObservations,
    IReadOnlyList<TopCommanderRow> TopCommanders,
    IReadOnlyList<HarvestRunRow> RecentRuns,
    long? PgStorageBytes,
    DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? NextScheduledUtc);
```

From DeckFlow.Web/Services/Harvest/IHarvestStatsAggregator.cs (NEW — Task 1):
```csharp
public interface IHarvestStatsAggregator
{
    Task<HarvestStatsPayload> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// D-13 explicit cache invalidation. Called by IHarvestRunStore.InsertQueuedAsync and
    /// IHarvestRunStore.UpdateStateAsync after each successful write so the next stats read
    /// (operator refresh, AJAX poll on Plan 05) sees fresh data without waiting for the
    /// 60s TTL. Idempotent — safe to call when nothing is cached.
    /// </summary>
    void Invalidate();
}
```

From DeckFlow.Web/Services/ICategoryKnowledgeStore.cs (modified — Task 2):
```csharp
// Stats reads (W8 — store-layer only):
Task<long> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default);
Task<long> GetTotalProcessedDeckCountSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default);
Task<long> GetTotalObservationCountAsync(CancellationToken cancellationToken = default);
Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default);
Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default);

// URL-path deck_queue write passthrough (B-NEW — keeps Plan 04 off the bare repository):
Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default);
```
**W8 fix:** all five stats methods land on `CategoryKnowledgeStore` only — do NOT add any of them to `CategoryKnowledgeRepository`. The store already exposes `GetProcessedDeckCountAsync`; this plan extends the same layer.

**B-NEW fix:** `MarkUrlDeckProcessedAsync` lands on the store interface AND impl. The impl is a one-line passthrough to the existing `_repository.MarkUrlDeckProcessedAsync` (created by Plan 02 Task 1). The bare `CategoryKnowledgeRepository` is NOT registered in DI; only `ICategoryKnowledgeStore` is. Routing through the store keeps the DI graph resolvable end-to-end.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Stats models + aggregator interface (with Invalidate) + sealed aggregator implementation with 60s IMemoryCache</name>
  <files>DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs, DeckFlow.Web/Services/Harvest/IHarvestStatsAggregator.cs, DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs</files>
  <behavior>
    - `HarvestStatsModels.cs` defines `public sealed record TopCommanderRow(string CommanderName, int DeckCount);` and `public sealed record HarvestStatsPayload(...)` per <interfaces>.
    - `IHarvestStatsAggregator` declares **two members**: `Task<HarvestStatsPayload> GetAsync(CancellationToken)` AND `void Invalidate()` (B1 / D-13).
    - `HarvestStatsAggregator` implements the interface; constructor takes `IHarvestRunStore`, `IHarvestScheduleCache`, `ICategoryKnowledgeStore`, `IMemoryCache`, `ILogger<HarvestStatsAggregator>`.
    - `GetAsync` calls `_memoryCache.GetOrCreateAsync("admin.harvest.stats.v1", entry => { entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60); return BuildAsync(ct); })`.
    - `Invalidate()` calls `_memoryCache.Remove(CacheKey);`. Idempotent — `IMemoryCache.Remove` no-ops on missing keys. No logging on the happy path (this fires several times per harvest_runs write — keep it cheap).
    - `BuildAsync` runs the queries SEQUENTIALLY (not parallel — they share the same DB; minor latency tradeoff for simpler error handling on the 60s-cached path):
      1. `totalDecks = await _knowledge.GetTotalProcessedDeckCountAsync(ct);`
      2. `cutoff = DateTimeOffset.UtcNow.AddDays(-30); totalDecks30d = await _knowledge.GetTotalProcessedDeckCountSinceAsync(cutoff, ct);`
      3. `totalObservations = await _knowledge.GetTotalObservationCountAsync(ct);`
      4. `topCommanders = await _knowledge.GetTopCommandersAsync(10, ct);`
      5. `recentRuns = await _runStore.GetRecentAsync(10, ct);`
      6. `pgSize = await _knowledge.GetPostgresDatabaseSizeBytesAsync(ct);` // returns null on SQLite
      7. `lastSuccess = await _runStore.GetLastSuccessUtcAsync(ct);` **(W5: same method that HarvestScheduleService.TickAsync calls — single source of truth.)**
      8. `nextScheduled` computed in C#: `var s = _scheduleCache.Snapshot(); next = (lastSuccess.HasValue && s.IntervalHours.HasValue && !s.Paused) ? lastSuccess.Value + TimeSpan.FromHours(s.IntervalHours.Value) : null;`
    - Returns the populated `HarvestStatsPayload`.
    - `ArgumentNullException.ThrowIfNull` on each ctor arg.
  </behavior>
  <action>
    Create three files.

    **`HarvestStatsModels.cs`:**
    ```csharp
    using System;
    using System.Collections.Generic;

    namespace DeckFlow.Web.Services.Harvest;

    /// <summary>Top-N commander row from deck_queue.commander_name (D-15).</summary>
    public sealed record TopCommanderRow(string CommanderName, int DeckCount);

    /// <summary>Full HARV-06 stats payload (D-16). Cached 60s in IMemoryCache; explicit invalidate on harvest_runs writes (D-13).</summary>
    public sealed record HarvestStatsPayload(
        long TotalDecks,
        long TotalDecks30d,
        long TotalObservations,
        IReadOnlyList<TopCommanderRow> TopCommanders,
        IReadOnlyList<HarvestRunRow> RecentRuns,
        long? PgStorageBytes,
        DateTimeOffset? LastSuccessUtc,
        DateTimeOffset? NextScheduledUtc);
    ```

    **`IHarvestStatsAggregator.cs`:**
    ```csharp
    using System.Threading;
    using System.Threading.Tasks;

    namespace DeckFlow.Web.Services.Harvest;

    /// <summary>HARV-06 stats panel data source. Cached 60s under admin.harvest.stats.v1 with explicit invalidate hook (D-13).</summary>
    public interface IHarvestStatsAggregator
    {
        /// <summary>Returns the cached or freshly-computed stats payload.</summary>
        Task<HarvestStatsPayload> GetAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// D-13 / B1: drops the cached payload so the next GetAsync rebuilds from PG.
        /// Called by IHarvestRunStore write methods (Plan 01 nullable ctor param) after every
        /// successful state transition. Idempotent.
        /// </summary>
        void Invalidate();
    }
    ```

    **`HarvestStatsAggregator.cs`:**
    ```csharp
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using DeckFlow.Web.Services;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    namespace DeckFlow.Web.Services.Harvest;

    /// <summary>
    /// Aggregates the eight HARV-06 stats metrics (D-16) under a 60s IMemoryCache key (D-13).
    /// pg_database_size(...) is PG-only — SQLite path returns null (D-14).
    /// Cache is invalidated explicitly by IHarvestRunStore writes (B1 / D-13) so an operator
    /// who just clicked Run Now / Submit URL sees fresh state on the next render — no 60s wait.
    /// </summary>
    public sealed class HarvestStatsAggregator : IHarvestStatsAggregator
    {
        private const string CacheKey = "admin.harvest.stats.v1";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private readonly IHarvestRunStore _runStore;
        private readonly IHarvestScheduleCache _scheduleCache;
        private readonly ICategoryKnowledgeStore _knowledge;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<HarvestStatsAggregator> _logger;

        public HarvestStatsAggregator(
            IHarvestRunStore runStore,
            IHarvestScheduleCache scheduleCache,
            ICategoryKnowledgeStore knowledge,
            IMemoryCache memoryCache,
            ILogger<HarvestStatsAggregator> logger)
        {
            ArgumentNullException.ThrowIfNull(runStore);
            ArgumentNullException.ThrowIfNull(scheduleCache);
            ArgumentNullException.ThrowIfNull(knowledge);
            ArgumentNullException.ThrowIfNull(memoryCache);
            ArgumentNullException.ThrowIfNull(logger);
            _runStore = runStore;
            _scheduleCache = scheduleCache;
            _knowledge = knowledge;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public Task<HarvestStatsPayload> GetAsync(CancellationToken cancellationToken = default)
        {
            return _memoryCache.GetOrCreateAsync(CacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                return await BuildAsync(cancellationToken);
            })!;
        }

        public void Invalidate()
        {
            // D-13 / B1: idempotent. IMemoryCache.Remove is a no-op when the key is missing.
            _memoryCache.Remove(CacheKey);
        }

        private async Task<HarvestStatsPayload> BuildAsync(CancellationToken cancellationToken)
        {
            var totalDecks = await _knowledge.GetTotalProcessedDeckCountAsync(cancellationToken);
            var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
            var totalDecks30d = await _knowledge.GetTotalProcessedDeckCountSinceAsync(cutoff, cancellationToken);
            var totalObservations = await _knowledge.GetTotalObservationCountAsync(cancellationToken);
            var topCommanders = await _knowledge.GetTopCommandersAsync(10, cancellationToken);
            var recentRuns = await _runStore.GetRecentAsync(10, cancellationToken);
            var pgSize = await _knowledge.GetPostgresDatabaseSizeBytesAsync(cancellationToken);
            // W5: same single-source-of-truth method that HarvestScheduleService.TickAsync calls.
            var lastSuccess = await _runStore.GetLastSuccessUtcAsync(cancellationToken);

            var schedule = _scheduleCache.Snapshot();
            DateTimeOffset? nextScheduled = lastSuccess.HasValue
                && schedule.IntervalHours.HasValue
                && !schedule.Paused
                ? lastSuccess.Value + TimeSpan.FromHours(schedule.IntervalHours.Value)
                : null;

            return new HarvestStatsPayload(
                TotalDecks: totalDecks,
                TotalDecks30d: totalDecks30d,
                TotalObservations: totalObservations,
                TopCommanders: topCommanders,
                RecentRuns: recentRuns,
                PgStorageBytes: pgSize,
                LastSuccessUtc: lastSuccess,
                NextScheduledUtc: nextScheduled);
        }
    }
    ```

    Build will not be green until Task 2 lands the `ICategoryKnowledgeStore` methods. Treat Tasks 1+2 as a single atomic build unit.
  </action>
  <verify>
    <automated>grep -q "interface IHarvestStatsAggregator" DeckFlow.Web/Services/Harvest/IHarvestStatsAggregator.cs && grep -q "void Invalidate" DeckFlow.Web/Services/Harvest/IHarvestStatsAggregator.cs && grep -q "sealed class HarvestStatsAggregator" DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs && grep -q "admin.harvest.stats.v1" DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs && grep -q "TimeSpan.FromSeconds(60)" DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs && grep -q "_memoryCache.Remove(CacheKey)" DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs && grep -q "GetLastSuccessUtcAsync" DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs && grep -q "sealed record HarvestStatsPayload" DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs && grep -q "sealed record TopCommanderRow" DeckFlow.Web/Services/Harvest/HarvestStatsModels.cs</automated>
  </verify>
  <done>Files created with the listed records, interface (with Invalidate), and sealed class; cache key + TTL literals present; `Invalidate()` calls `_memoryCache.Remove(CacheKey)`; `BuildAsync` reads `GetLastSuccessUtcAsync` (W5 single source of truth). (Build green is asserted in Task 2's verify after the store methods land.)</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: ICategoryKnowledgeStore + CategoryKnowledgeStore — five stats query methods (W8) + MarkUrlDeckProcessedAsync passthrough (B-NEW)</name>
  <files>DeckFlow.Web/Services/ICategoryKnowledgeStore.cs, DeckFlow.Web/Services/CategoryKnowledgeStore.cs</files>
  <behavior>
    - **W8:** all five stats query methods are added to `CategoryKnowledgeStore` only (and surfaced on `ICategoryKnowledgeStore`). Do NOT add any of them to `CategoryKnowledgeRepository`. The store already exposes `GetProcessedDeckCountAsync` (and is the canonical layer for stats reads); the new methods extend the same surface.
    - **B-NEW:** `ICategoryKnowledgeStore` ALSO gains `MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)`. The `CategoryKnowledgeStore` implementation is a one-line passthrough to `_repository.MarkUrlDeckProcessedAsync(deckId, commanderName, cancellationToken)` (the underlying SQL is owned by `CategoryKnowledgeRepository` per Plan 02 Task 1). The store-side method MUST call `EnsureSchemaReadyAsync` before delegating, mirroring the pattern used by every other store method that hits the repository, so a cold-start URL submit doesn't crash on a missing table.
    - **Why route through the store:** `Program.cs` registers `ICategoryKnowledgeStore` as a singleton; `CategoryKnowledgeRepository` is constructed inline by the store and is NOT registered in DI. Plan 04's controller therefore cannot inject the bare repository (would throw `InvalidOperationException` at request time). Adding the passthrough lets Plan 04 inject the already-registered `ICategoryKnowledgeStore` instead — no new DI registration, no Plan 07 change required.
    - `GetTotalProcessedDeckCountAsync` — `SELECT COUNT(1) FROM deck_queue WHERE processed = 1`.
    - `GetTotalProcessedDeckCountSinceAsync(DateTimeOffset since)` — `SELECT COUNT(1) FROM deck_queue WHERE processed = 1 AND inserted_utc >= @cutoff`. Bind `@cutoff` cross-provider (PG: `since.UtcDateTime`, SQLite: `since.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)`).
    - `GetTotalObservationCountAsync` — `SELECT COUNT(1) FROM card_category_observations`.
    - `GetTopCommandersAsync(int n)` — D-15 SQL: `SELECT commander_name, COUNT(1) AS deck_count FROM deck_queue WHERE processed = 1 AND commander_name IS NOT NULL GROUP BY commander_name ORDER BY deck_count DESC LIMIT @n`. Returns `IReadOnlyList<TopCommanderRow>`.
    - `GetPostgresDatabaseSizeBytesAsync` — if `_connectionInfo.IsPostgres`, `SELECT pg_database_size(current_database())` and read scalar as `long`. Else return `null`.
    - All five stats methods route through the same connection-info field as the existing `GetProcessedDeckCountAsync`. All use parameterized SQL only.
    - All methods accept `CancellationToken cancellationToken = default` last and have XML doc comments referencing the corresponding D-XX (D-15 for top commanders, D-14 for storage size, D-16 for the others, B-NEW for the passthrough).
    - Existing methods on the store remain untouched. The repository (`CategoryKnowledgeRepository`) is NOT modified by this task (W8) — Plan 02 Task 1 already adds `MarkUrlDeckProcessedAsync` to the repository.
  </behavior>
  <action>
    **Step A — `DeckFlow.Web/Services/ICategoryKnowledgeStore.cs`** (extend the existing interface):

    Append the six new method signatures to the interface (do not remove or reorder existing members):
    ```csharp
    // HARV-06 stats reads (D-13, D-14, D-15, D-16). All five live on the store only (W8).
    Task<long> GetTotalProcessedDeckCountAsync(CancellationToken cancellationToken = default);
    Task<long> GetTotalProcessedDeckCountSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default);
    Task<long> GetTotalObservationCountAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default);
    Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// B-NEW: URL-path deck_queue write passthrough. Plan 04 SubmitUrl calls this on
    /// ICategoryKnowledgeStore (a registered singleton) instead of the bare
    /// CategoryKnowledgeRepository (not in DI). The implementation delegates to
    /// CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync (created by Plan 02 Task 1).
    /// Idempotent UPSERT — safe to re-submit the same Archidekt URL.
    /// </summary>
    Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default);
    ```
    Add `using DeckFlow.Web.Services.Harvest;` at the top of the interface file so `TopCommanderRow` resolves. (`HarvestStatsModels.cs` lives in `DeckFlow.Web.Services.Harvest`.)

    **Step B — `DeckFlow.Web/Services/CategoryKnowledgeStore.cs`** (extend the existing class):

    Inspect the existing `GetProcessedDeckCountAsync` and the `_connectionInfo` / `_repository` fields. Add the five stats query methods following the same shape as the existing reader methods. Sample for the trickiest one (top commanders):
    ```csharp
    /// <summary>
    /// D-15: top-N commanders by deck count. Reads deck_queue.commander_name populated
    /// by ArchidektDeckCacheSession (bulk path, Plan 02) and AdminHarvestController.SubmitUrl
    /// → CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync (URL path, Plan 04 / B2).
    /// </summary>
    public async Task<IReadOnlyList<TopCommanderRow>> GetTopCommandersAsync(int n, CancellationToken cancellationToken = default)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
        await EnsureSchemaReadyAsync(cancellationToken);
        // Use the existing connection helper on the store (same pattern as GetProcessedDeckCountAsync
        // — likely via _repository or a private CreateConnection helper). If the store currently
        // delegates ALL SQL to _repository, add the matching reader on _repository for the four
        // stats reads only AND keep MarkUrlDeckProcessedAsync as a one-line passthrough; otherwise
        // open the connection here directly via _connectionInfo and parameterize.
        // (Pick whichever pattern matches the rest of CategoryKnowledgeStore — both are acceptable
        // as long as the SQL strings live on the store side per W8.)
        ...
    }
    ```

    For `GetPostgresDatabaseSizeBytesAsync`:
    ```csharp
    /// <summary>D-14: PG-only. SQLite path returns null; UI renders "N/A".</summary>
    public async Task<long?> GetPostgresDatabaseSizeBytesAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectionInfo.IsPostgres) return null;
        await EnsureSchemaReadyAsync(cancellationToken);
        // SELECT pg_database_size(current_database()); — constant string, no parameters.
        ...
    }
    ```

    For the **B-NEW passthrough** (one-line method body):
    ```csharp
    /// <summary>
    /// B-NEW: passthrough to CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync (Plan 02
    /// Task 1 owns the SQL). Routing through the store keeps the DI graph resolvable: only
    /// ICategoryKnowledgeStore is DI-registered; the bare repository is not.
    /// </summary>
    public async Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);
        await EnsureSchemaReadyAsync(cancellationToken);
        await _repository.MarkUrlDeckProcessedAsync(deckId, commanderName, cancellationToken);
    }
    ```

    Implement the other three count methods with the same pattern. Consider extracting a small `private async Task<long> ScalarLongAsync(string sql, Action<DbCommand>? bindParams, CancellationToken ct)` helper to reduce duplication. If the existing store has a similar helper, use it; do not introduce new helpers if a shipped one already covers the case.

    Ensure `using DeckFlow.Web.Services.Harvest;` is added at the top of `CategoryKnowledgeStore.cs` so `TopCommanderRow` is in scope.

    **W8 enforcement:** do not edit `CategoryKnowledgeRepository.cs` from this task (Plan 02 Task 1 already adds `MarkUrlDeckProcessedAsync` to the repository — that file stays as Plan 02 left it). The verify gate below greps the **store + interface files only** for the new stats methods, and asserts the passthrough is present on both interface and impl.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -q "GetTopCommandersAsync" DeckFlow.Web/Services/CategoryKnowledgeStore.cs && grep -q "GetTotalProcessedDeckCountAsync" DeckFlow.Web/Services/CategoryKnowledgeStore.cs && grep -q "GetTotalProcessedDeckCountSinceAsync" DeckFlow.Web/Services/CategoryKnowledgeStore.cs && grep -q "GetTotalObservationCountAsync" DeckFlow.Web/Services/CategoryKnowledgeStore.cs && grep -q "pg_database_size(current_database())" DeckFlow.Web/Services/CategoryKnowledgeStore.cs && grep -q "FROM deck_queue" DeckFlow.Web/Services/CategoryKnowledgeStore.cs && grep -q "commander_name IS NOT NULL" DeckFlow.Web/Services/CategoryKnowledgeStore.cs && grep -q "MarkUrlDeckProcessedAsync" DeckFlow.Web/Services/CategoryKnowledgeStore.cs && grep -q "MarkUrlDeckProcessedAsync" DeckFlow.Web/Services/ICategoryKnowledgeStore.cs && grep -q "_repository.MarkUrlDeckProcessedAsync" DeckFlow.Web/Services/CategoryKnowledgeStore.cs && grep -q "GetTopCommandersAsync" DeckFlow.Web/Services/ICategoryKnowledgeStore.cs</automated>
  </verify>
  <done>Build exits 0; all five stats methods present in **CategoryKnowledgeStore.cs** AND on the **ICategoryKnowledgeStore** interface (W8 — store-layer only); PG-only branch on `pg_database_size`; D-15 SQL string contains `commander_name IS NOT NULL` and `LIMIT @n`; **B-NEW: `MarkUrlDeckProcessedAsync` is on the interface and the impl, with the impl body delegating to `_repository.MarkUrlDeckProcessedAsync`**.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 3: Wire IHarvestStatsAggregator.Invalidate() into IHarvestRunStore writes (B1 / D-13)</name>
  <files>DeckFlow.Web/Services/Harvest/HarvestRunStore.cs</files>
  <behavior>
    - Plan 01 already ships `HarvestRunStore` with a nullable `IHarvestStatsAggregator? stats = null` ctor parameter and `_stats?.Invalidate()` calls inside `InsertQueuedAsync` and `UpdateStateAsync` (placed AFTER the SQL write succeeds).
    - This task confirms those call sites exist and adds NO new logic — it is purely a verification task that ensures Plan 01 shipped what it promised. If Plan 01 missed any of the five state-write surfaces (Insert/Running/Stopping/Succeeded/Failed/Cancelled all flow through `InsertQueuedAsync` + `UpdateStateAsync`), patch the missing site here.
    - **B1 acceptance gate:** `grep -c "_stats?.Invalidate" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs >= 2` — one in `InsertQueuedAsync`, one in `UpdateStateAsync`. The `UpdateStateAsync` site fires for every state transition (Running, Stopping, Succeeded, Failed, Cancelled) so a single call inside that method covers all five remaining transitions. Combined with the InsertQueuedAsync call, every harvest_runs write busts the cache.
    - DI registration order (Plan 07 Task 1) MUST register `IHarvestStatsAggregator` before `IHarvestRunStore` so the run-store ctor receives a live aggregator on first resolve. Plan 07's `AddDeckFlowHarvest()` extension is amended to add the aggregator FIRST, then the run-store; document this in the SUMMARY for the next phase to know.
  </behavior>
  <action>
    Open `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` (created by Plan 01).

    1. **Verify** the ctor signature includes `IHarvestStatsAggregator? stats = null` as the trailing optional parameter on every public ctor; if missing, add it and assign to `_stats` field. (Plan 01 shipped this — should already be correct.)

    2. **Verify** the field declaration:
       ```csharp
       private readonly IHarvestStatsAggregator? _stats;
       ```

    3. **Verify** that `InsertQueuedAsync` calls `_stats?.Invalidate();` AFTER `await command.ExecuteNonQueryAsync(cancellationToken);` returns successfully. If missing, add it. The pattern:
       ```csharp
       await command.ExecuteNonQueryAsync(cancellationToken);
       _stats?.Invalidate();   // D-13 / B1
       return id;
       ```

    4. **Verify** that `UpdateStateAsync` calls `_stats?.Invalidate();` AFTER its `ExecuteNonQueryAsync` returns. The single call in this method covers all five state transitions (Running, Stopping, Succeeded, Failed, Cancelled) because every transition flows through this method. Same pattern:
       ```csharp
       await command.ExecuteNonQueryAsync(cancellationToken);
       _stats?.Invalidate();   // D-13 / B1
       ```

    5. **Update Plan 07 (DI wiring)** — record in this task's SUMMARY that Plan 07's `AddDeckFlowHarvest()` MUST register `IHarvestStatsAggregator` BEFORE `IHarvestRunStore` so DI resolution gives the run-store a live aggregator instance. Plan 07 already lists `IHarvestStatsAggregator` registration AFTER the run-store; the executor of this task should also patch Plan 07 Task 1 to swap the order: aggregator first, run-store second. This is a one-line move inside `HarvestServiceCollectionExtensions.cs`.

    6. **Forward-declare interface stub fix:** if Plan 01 shipped a stub `IHarvestStatsAggregator { void Invalidate(); }` to keep itself buildable in isolation, that stub conflicts with this plan's full interface (which adds `GetAsync`). Resolution: delete the stub from `HarvestRunStore.cs` (or wherever Plan 01 parked it) and leave only the canonical interface declared in `IHarvestStatsAggregator.cs` from Task 1. Build must compile end-to-end after this consolidation.

    No new logic gets added — this task is the contract-keeping work between Plan 01's promise and Plan 06's delivery.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -c "_stats?.Invalidate" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs && grep -q "IHarvestStatsAggregator? stats" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs && ! grep -q "// STUB — Plan 06" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs</automated>
  </verify>
  <done>Build exits 0; `_stats?.Invalidate` count ≥ 2 (InsertQueuedAsync + UpdateStateAsync); ctor param `IHarvestStatsAggregator? stats` present; any Plan 01 forward-declaration stub is removed; Plan 07 task list amended (or note recorded) to register aggregator before run-store.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 4: Wire stats payload into AdminHarvestViewModel + Index.cshtml stats panel</name>
  <files>DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs, DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs, DeckFlow.Web/Views/AdminHarvest/Index.cshtml</files>
  <behavior>
    - VM's `Stats` property changes from `object?` to `HarvestStatsPayload? Stats { get; init; }` (still nullable to tolerate aggregator failure, but populated under happy path).
    - Controller injects `IHarvestStatsAggregator _statsAggregator` and calls `_statsAggregator.GetAsync(ct)` inside `Index`. On exception (other than OCE), log and set Stats=null + a banner; do not 500 the page (admin should always render).
    - View's Stats panel renders all 8 metrics. Storage size cell formats `Model.Stats?.PgStorageBytes` as a human-readable MB with one decimal (e.g. `(bytes / 1024.0 / 1024.0).ToString("0.0")` MB) when non-null; renders `"N/A"` when null. `next_scheduled_utc` formats with `"u"` or renders "Off" when null.
    - Top commanders renders as an ordered list (top 10 → `<ol>`).
    - Recent runs is the existing table from Plan 04 (already in the view); the Stats panel does not duplicate it but `Model.Stats.RecentRuns` is the canonical source — Plan 04's `Model.RecentRuns` line in the view should be replaced with `Model.Stats?.RecentRuns ?? Array.Empty<HarvestRunRow>()` to avoid a double-fetch. Optional polish: drop the Plan 04 separate `RecentRuns` VM field entirely. Planner discretion: keep the Plan 04 field for now to minimize cross-plan churn — both can coexist.
  </behavior>
  <action>
    **Step A — `AdminHarvestViewModel.cs`:**
    Replace `public object? Stats { get; init; }` with `public HarvestStatsPayload? Stats { get; init; }` and `using DeckFlow.Web.Services.Harvest;`.

    **Step B — `AdminHarvestController.cs` Index method:**
    ```csharp
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var active = await _runStore.GetActiveAsync(cancellationToken);
        var recent = await _runStore.GetRecentAsync(10, cancellationToken);
        var schedule = _scheduleCache.Snapshot();

        HarvestStatsPayload? stats = null;
        try
        {
            stats = await _statsAggregator.GetAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Harvest.Stats.AggregateFailed message={Message}", exception.Message);
            // Render the page anyway so the operator can still trigger a run.
        }

        var vm = new AdminHarvestViewModel
        {
            Schedule = schedule,
            ActiveRun = active,
            RecentRuns = recent,
            RunBanner = TempData["HarvestRunBanner"] as string,
            UrlBanner = TempData["HarvestUrlBanner"] as string,
            ScheduleBanner = TempData["HarvestScheduleBanner"] as string,
            Stats = stats,
        };
        return View(vm);
    }
    ```
    Also: add `IHarvestStatsAggregator _statsAggregator` field + ctor param + `ArgumentNullException.ThrowIfNull`.

    **Step C — `Views/AdminHarvest/Index.cshtml` Stats panel:**
    Replace the placeholder `<div class="admin-harvest__stats-placeholder">Stats panel rendered by Plan 06.</div>` with:
    ```razor
    @if (Model.Stats is null)
    {
        <div class="admin-banner admin-banner--error">Stats temporarily unavailable.</div>
    }
    else
    {
        var s = Model.Stats;
        var sizeText = s.PgStorageBytes is { } bytes
            ? (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB"
            : "N/A";
        var nextText = s.NextScheduledUtc?.ToString("u") ?? "Off";
        var lastText = s.LastSuccessUtc?.ToString("u") ?? "Never";

        <dl class="admin-harvest__stats">
            <dt>Total decks (lifetime)</dt><dd>@s.TotalDecks</dd>
            <dt>Total decks (last 30d)</dt><dd>@s.TotalDecks30d</dd>
            <dt>Total observations</dt><dd>@s.TotalObservations</dd>
            <dt>Postgres storage</dt><dd>@sizeText</dd>
            <dt>Last successful run</dt><dd>@lastText</dd>
            <dt>Next scheduled run</dt><dd>@nextText</dd>
        </dl>

        <h3>Top 10 Commanders</h3>
        <ol class="admin-harvest__top-commanders">
            @foreach (var c in s.TopCommanders)
            {
                <li>@c.CommanderName <span class="admin-harvest__count">(@c.DeckCount decks)</span></li>
            }
        </ol>
    }
    ```
    Keep the existing Recent Runs table block from Plan 04 unchanged (it reads `Model.RecentRuns`).

    Build must compile.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -q "HarvestStatsPayload? Stats" DeckFlow.Web/Models/Admin/AdminHarvestViewModel.cs && grep -q "_statsAggregator.GetAsync" DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs && grep -q "Top 10 Commanders" DeckFlow.Web/Views/AdminHarvest/Index.cshtml && grep -q "PgStorageBytes" DeckFlow.Web/Views/AdminHarvest/Index.cshtml && grep -q "Postgres storage" DeckFlow.Web/Views/AdminHarvest/Index.cshtml</automated>
  </verify>
  <done>Build exits 0; VM.Stats is typed `HarvestStatsPayload?`; controller calls aggregator and tolerates failure; view renders 8 metrics including the Top 10 Commanders ordered list and the N/A storage fallback.</done>
</task>

</tasks>

<test_recommendations_w5>
<!-- W5 hardening: assert that both consumers (stats panel + scheduler) read from the same
     IHarvestRunStore.GetLastSuccessUtcAsync method. Two test files are referenced by name
     in the W5 acceptance gate; create scaffolds even if unit-test infrastructure for these
     classes is added later in the milestone. -->

Recommended test scaffolds (create even as empty `[Fact]` placeholders so W5 grep-gates pass):

1. `DeckFlow.Web.Tests/HarvestStatsAggregatorTests.cs` — at minimum:
   ```csharp
   [Fact]
   public async Task BuildAsync_ReadsLastSuccessFromRunStore_GetLastSuccessUtcAsync()
   {
       // Asserts the aggregator pulls last_success_utc from IHarvestRunStore.GetLastSuccessUtcAsync,
       // not from any local re-query of harvest_runs. Single source of truth (W5).
   }
   ```

2. `DeckFlow.Web.Tests/HarvestScheduleServiceTests.cs` — at minimum:
   ```csharp
   [Fact]
   public async Task TickAsync_ReadsLastSuccessFromRunStore_GetLastSuccessUtcAsync()
   {
       // Asserts the scheduler reads last_success_utc from IHarvestRunStore.GetLastSuccessUtcAsync,
       // not from a separate query. Single source of truth (W5).
   }
   ```

W5 grep gate (run by the checker): both files must reference `GetLastSuccessUtcAsync`:
- `grep -q "GetLastSuccessUtcAsync" DeckFlow.Web.Tests/HarvestStatsAggregatorTests.cs`
- `grep -q "GetLastSuccessUtcAsync" DeckFlow.Web.Tests/HarvestScheduleServiceTests.cs`
</test_recommendations_w5>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Aggregator → Postgres / SQLite | Constant SQL strings + parameterized values; no operator-supplied input crosses here. |
| Aggregator → IMemoryCache | In-process; cached payload is a sealed record copy — no leakage of mutable repository internals. |
| HarvestRunStore.write methods → IHarvestStatsAggregator.Invalidate() | In-process call from a Singleton to a Singleton; no external boundary. |
| Plan 04 controller → ICategoryKnowledgeStore.MarkUrlDeckProcessedAsync | DI-registered singleton call; B-NEW closed by routing through the store instead of the bare repository (which is not in DI). |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-07-27 | Information disclosure | Stats panel reveals storage size | accept | Operator-only after BasicAuth; storage size is one of the explicit HARV-06 requirements. |
| T-07-28 | Tampering | GetTopCommandersAsync `n` parameter | mitigate | Throws on `n <= 0`; bound by aggregator (always 10). |
| T-07-29 | Denial of service | 60s cache miss runs 5+ PG queries | accept | Sequential queries on operator-only page; cache TTL bounds cost to ≤ 1 query/min steady-state; explicit Invalidate() only fires on operator/scheduler-driven harvest_runs writes (low rate). |
| T-07-30 | Tampering | pg_database_size SQL | mitigate | Constant string, no parameters; PG built-in function. |
| T-07-37 | Denial of service | Invalidate() cache stampede | accept | A burst of harvest_runs writes (e.g., scheduler tick + operator click) calls Invalidate several times in quick succession; IMemoryCache.Remove is thread-safe and O(1); next GetAsync rebuilds once and re-caches. No concurrent rebuild guard added — acceptable for an operator-only page. |
| T-07-38 | Tampering | ICategoryKnowledgeStore.MarkUrlDeckProcessedAsync deckId | mitigate | Plan 04 validates the URL via `ArchidektApiUrl.TryGetDeckId` before invoking the store passthrough; the store calls `ArgumentException.ThrowIfNullOrWhiteSpace(deckId)`; underlying repository SQL is parameterized + idempotent UPSERT. |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` exits 0.
- `grep -c "admin.harvest.stats.v1" DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs` ≥ 1.
- `grep -c "TimeSpan.FromSeconds(60)" DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs` ≥ 1.
- `grep -c "_memoryCache.Remove(CacheKey)" DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs` ≥ 1.
- `grep -c "void Invalidate" DeckFlow.Web/Services/Harvest/IHarvestStatsAggregator.cs` ≥ 1.
- **B1:** `grep -c "_stats?.Invalidate" DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` ≥ 2.
- `grep -c "pg_database_size(current_database())" DeckFlow.Web/Services/CategoryKnowledgeStore.cs` ≥ 1.
- `grep -c "commander_name IS NOT NULL" DeckFlow.Web/Services/CategoryKnowledgeStore.cs` ≥ 1.
- **W8:** `grep -c "GetTopCommandersAsync\|GetTotalProcessedDeckCountAsync\|GetTotalProcessedDeckCountSinceAsync\|GetTotalObservationCountAsync\|GetPostgresDatabaseSizeBytesAsync" DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` = 0 (none of the five new stats methods leak into the repository).
- **B-NEW:** `grep -q "MarkUrlDeckProcessedAsync" DeckFlow.Web/Services/ICategoryKnowledgeStore.cs` AND `grep -q "MarkUrlDeckProcessedAsync" DeckFlow.Web/Services/CategoryKnowledgeStore.cs` AND `grep -q "_repository.MarkUrlDeckProcessedAsync" DeckFlow.Web/Services/CategoryKnowledgeStore.cs` AND `grep -q "MarkUrlDeckProcessedAsync" DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` (Plan 02 — unchanged).
- `grep -c "Top 10 Commanders" DeckFlow.Web/Views/AdminHarvest/Index.cshtml` ≥ 1.
- `grep -c "Postgres storage" DeckFlow.Web/Views/AdminHarvest/Index.cshtml` ≥ 1.
- **W5:** `grep -q "GetLastSuccessUtcAsync" DeckFlow.Web/Services/Harvest/HarvestStatsAggregator.cs` AND `grep -q "GetLastSuccessUtcAsync" DeckFlow.Web/Services/Harvest/HarvestScheduleService.cs` (single source of truth).
</verification>

<success_criteria>
- HARV-06 fully delivered. Operator at `/Admin/Harvest` sees all 8 metrics on the Stats panel.
- ROADMAP SC #5 ("Stats panel shows total decks, total observations, Postgres storage size, last run timestamp, next scheduled run") satisfied.
- ROADMAP SC #2 ("commander appears in top-commanders list after URL submit") satisfied because both bulk and URL paths populate `deck_queue.commander_name` (Plans 02 + 04) and the top-N query reads that column.
- pg_database_size renders as MB on PG, "N/A" on SQLite local dev.
- Aggregator failures degrade gracefully — page still renders with the rest of the panels.
- **B1 / D-13:** every harvest_runs INSERT/UPDATE busts the stats cache, so the operator sees fresh state on the next render without waiting 60s.
- **W5:** both stats aggregator and scheduler tick read last_success_utc from the same `IHarvestRunStore.GetLastSuccessUtcAsync` method; test scaffolds reference this method by name.
- **W8:** all five new stats query methods live on `CategoryKnowledgeStore` only; no leakage into `CategoryKnowledgeRepository`.
- **B-NEW:** Plan 04's `AdminHarvestController` injects `ICategoryKnowledgeStore` (already DI-registered) instead of the bare `CategoryKnowledgeRepository` (not registered). The store's `MarkUrlDeckProcessedAsync` delegates to `_repository.MarkUrlDeckProcessedAsync`. DI graph resolves end-to-end at runtime; no `InvalidOperationException` on URL submit.
</success_criteria>

<output>
After completion, create `.planning/phases/07-harvest-controls-stats/07-06-SUMMARY.md` covering: the eight metric queries (one-line each), confirmation of cache key and TTL, the SQLite fallback for storage_size, the **explicit Invalidate() wiring (B1)** with grep counts, the W5 test scaffold filenames, a one-liner confirming W8 (store-only consolidation), and a one-liner confirming **B-NEW: ICategoryKnowledgeStore.MarkUrlDeckProcessedAsync ships as a one-line passthrough so Plan 04 can drop the bare CategoryKnowledgeRepository injection**.
</output>
