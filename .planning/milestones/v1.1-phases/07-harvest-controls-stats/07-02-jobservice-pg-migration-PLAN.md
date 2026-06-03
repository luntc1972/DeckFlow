---
phase: 07-harvest-controls-stats
plan: 02
type: execute
wave: 2
depends_on: [01]
files_modified:
  - DeckFlow.Web/Services/ArchidektCacheJobService.cs
  - DeckFlow.Web/Services/IArchidektCacheJobService.cs
  - DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs
  - DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs
autonomous: true
requirements: [HARV-01, HARV-03]
tags: [harvest, jobs, cancellation, postgres, commander-capture]

must_haves:
  truths:
    - "ArchidektCacheJobService writes every job state transition to harvest_runs as the single source of truth (D-01)"
    - "In-memory ConcurrentDictionary<Guid, ArchidektCacheJobStatus> _jobs is removed (D-01)"
    - "Existing public API ArchidektCacheJobsController contract (freeform 1-3600s, GetJob/GetActiveJob shapes) keeps working — the interface signature is wire-compatible (D-04)"
    - "Operator cancel via _activeJobCts.Cancel() linked to host stoppingToken transitions a Running job to Cancelled within 30s (D-05, ROADMAP SC #3)"
    - "On successful single-deck import (bulk path), commander_name is captured and persisted on the deck_queue row via UPDATE in the existing MarkDecksProcessedAsync site (D-17, planner discretion: UPDATE site chosen)"
    - "CategoryKnowledgeRepository exposes a public `MarkUrlDeckProcessedAsync(deckId, commanderName, ct)` UPSERT (B2) used by Plan 04 SubmitUrl so the URL-harvested deck lands in deck_queue with processed=1 and commander_name populated — without this method ROADMAP SC #2 is unprovable"
    - "EnqueueAsync 60-min hard cap is preserved (HARV-01 / D-04)"
  artifacts:
    - path: "DeckFlow.Web/Services/ArchidektCacheJobService.cs"
      provides: "Background job runner with PG-backed state, _activeJobCts cancel plumbing, channel-based queue retained"
      contains: "_activeJobCts"
    - path: "DeckFlow.Web/Services/IArchidektCacheJobService.cs"
      provides: "Public contract gains CancelActiveAsync(CancellationToken); existing EnqueueAsync/GetJob/GetActiveJob preserved"
      contains: "CancelActiveAsync"
    - path: "DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs"
      provides: "PersistDeckAsync extracts commander name from imported entries and forwards it to repository"
      contains: "commanderName"
    - path: "DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs"
      provides: "Two new public methods — MarkDeckProcessedAsync (single-deck UPDATE for bulk path) and MarkUrlDeckProcessedAsync (UPSERT for URL path, B2)"
      contains: "MarkUrlDeckProcessedAsync"
  key_links:
    - from: "ArchidektCacheJobService.EnqueueAsync"
      to: "IHarvestRunStore.InsertQueuedAsync"
      via: "PG row insert with kind='bulk'"
      pattern: "_runStore\\.InsertQueuedAsync"
    - from: "ArchidektCacheJobService.ExecuteAsync"
      to: "IHarvestRunStore.UpdateStateAsync"
      via: "Per-state-transition row update; OCE → Cancelled, Exception → Failed"
      pattern: "_runStore\\.UpdateStateAsync"
    - from: "ArchidektDeckCacheSession.PersistDeckAsync"
      to: "deck_queue.commander_name"
      via: "MarkDeckProcessedAsync (single-deck overload) UPDATE statement"
      pattern: "commander_name"
    - from: "CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync"
      to: "deck_queue (deck_id PK) row with processed=1, commander_name populated"
      via: "INSERT … ON CONFLICT(deck_id) DO UPDATE EXCLUDED upsert idiom (B2)"
      pattern: "ON CONFLICT.deck_id. DO UPDATE"
---

<objective>
Migrate `ArchidektCacheJobService` from in-memory dictionary state to Postgres-backed state via `IHarvestRunStore`, add the per-job linked CancellationTokenSource for graceful operator cancel, capture commander identity at the existing `MarkDecksProcessedAsync` UPDATE site so the harvest stats panel (Plan 06) can compute top-10 commanders, and add the `MarkUrlDeckProcessedAsync` UPSERT method (B2) that Plan 04's SubmitUrl flow needs to make ROADMAP SC #2 (commander appears in top-N after URL submit) provable.

Purpose: makes the durability decision (D-01) real. After this plan, a Render redeploy mid-sweep results in a `Failed (interrupted by redeploy)` row on next boot — not a stuck `Running` row that confuses the AJAX poll. Adds the cancel knob the controller (Plan 04) will pull, and ships the URL-path repository UPSERT that closes the deck_queue write-side gap for SC #2.

Output:
- Mutations to `ArchidektCacheJobService`: drop `_jobs` + `_activeJobId`, hold `_activeJobCts`, route every state transition through `_runStore`, expose `CancelActiveAsync`.
- Interface gains `CancelActiveAsync` and the existing `GetJob`/`GetActiveJob` methods now read from PG (return `ArchidektCacheJobStatus?` mapped from `HarvestRunRow`).
- `ArchidektDeckCacheSession.PersistDeckAsync` extracts commander name from the imported `IReadOnlyList<DeckEntry>` (filter by `Category == "Commander"`, take first) and passes it to `MarkDeckProcessedAsync`.
- `CategoryKnowledgeRepository.MarkDecksProcessedAsync` adds an overload that accepts `string? commanderName` and writes it in the same UPDATE.
- `CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken)` (NEW, B2) — UPSERT mirroring `AddDeckIdsAsync` (lines 487-560) but writes processed=1 from the start and includes commander_name; consumed by Plan 04 SubmitUrl.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/phases/07-harvest-controls-stats/07-CONTEXT.md
@.planning/phases/07-harvest-controls-stats/07-RESEARCH.md
@.planning/phases/07-harvest-controls-stats/07-PATTERNS.md
@.planning/phases/07-harvest-controls-stats/07-01-SUMMARY.md
@DeckFlow.Web/Services/ArchidektCacheJobService.cs
@DeckFlow.Web/Services/IArchidektCacheJobService.cs
@DeckFlow.Web/Controllers/Api/ArchidektCacheJobsController.cs
@DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs
@DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs
@DeckFlow.Core/Models/DeckEntry.cs
@DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs
@DeckFlow.Web/Services/Harvest/HarvestRunModels.cs

<interfaces>
<!-- Existing public surface that must remain wire-compatible. -->

From DeckFlow.Web/Services/IArchidektCacheJobService.cs (existing):
```csharp
public interface IArchidektCacheJobService
{
    Task<ArchidektCacheJobEnqueueResult> EnqueueAsync(TimeSpan duration, CancellationToken cancellationToken = default);
    ArchidektCacheJobStatus? GetJob(Guid jobId);
    ArchidektCacheJobStatus? GetActiveJob();
}
// PLAN ADDS:
//   Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default);
//   GetJob and GetActiveJob become async or stay sync — this plan keeps them sync to preserve the
//   existing public-API controller contract; sync impl reads via .GetAwaiter().GetResult() on a
//   1-second-cached IMemoryCache snapshot. (Already-async overloads with `Async` suffix can be
//   added in a later plan if needed.)
```

From DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs (existing — modified):
```csharp
// Existing public method (line 644):
//   Task MarkDecksProcessedAsync(IEnumerable<string> deckIds, bool skip = false, CancellationToken cancellationToken = default);
//
// NEW overloads added by this plan:
//   Task MarkDeckProcessedAsync(string deckId, string? commanderName, bool skip = false, CancellationToken cancellationToken = default);
//   Task MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default);  // B2
//
// Existing batch overload retained verbatim (callers in tests/CLI rely on it).
```

From DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs (existing — modified):
```csharp
// Existing PersistDeckAsync (lines 148-155) returns DeckCacheWriteResult after a single-deck
// import. Plan extracts commander name from the imported entries list before returning, and
// changes its return type to (DeckCacheWriteResult, string? commanderName) so RunAsync can
// pass commanderName to the new single-deck MarkDeckProcessedAsync overload (replacing the
// MarkDecksProcessedAsync(new[] { deckId }, ...) call at line 107).
```

From DeckFlow.Web/Services/Harvest/IHarvestRunStore.cs (Plan 01 output):
```csharp
Task<Guid> InsertQueuedAsync(HarvestRunKind kind, int durationSeconds, string? url, DateTimeOffset now, CancellationToken cancellationToken = default);
Task UpdateStateAsync(Guid id, HarvestRunState state, DateTimeOffset? startedUtc, DateTimeOffset? completedUtc, int decksProcessed, int additionalDecksFound, string? errorMessage, CancellationToken cancellationToken = default);
Task<HarvestRunRow?> GetActiveAsync(CancellationToken cancellationToken = default);
```
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Repository commander capture (D-17) + new MarkUrlDeckProcessedAsync UPSERT (B2) + session wiring</name>
  <files>DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs, DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs</files>
  <behavior>
    - New public method `MarkDeckProcessedAsync(string deckId, string? commanderName, bool skip = false, CancellationToken cancellationToken = default)` on `CategoryKnowledgeRepository`. Single-deck variant (vs existing batch). UPDATE statement writes processed=1, skipped=@skip, last_checked_utc=@now, commander_name=@commanderName all in one round-trip. NULL `commanderName` writes SQL NULL (skipped decks pass null).
    - **NEW (B2):** Public method `MarkUrlDeckProcessedAsync(string deckId, string? commanderName, CancellationToken cancellationToken = default)` on `CategoryKnowledgeRepository`. UPSERT shape mirrored on `AddDeckIdsAsync` (lines 487-560) but always writes `processed=1, skipped=0` from the start and populates `commander_name`. Idempotent — re-submitting the same Archidekt URL refreshes `last_checked_utc` and updates `commander_name` to the latest non-null value (`COALESCE(excluded.commander_name, deck_queue.commander_name)` so a re-import that fails to extract a commander does not blow away the previously-captured name).
    - Existing batch `MarkDecksProcessedAsync(IEnumerable<string>, bool, CancellationToken)` is retained but now delegates to the new single-deck method per id (preserves API for tests/CLI). Alternatively, add a second batch overload that takes `IEnumerable<(string DeckId, string? CommanderName)>` — planner picks; the simpler delegation pattern is preferred.
    - `ArchidektDeckCacheSession.PersistDeckAsync` extracts commander from the imported entries list immediately after `_deckImporter.ImportAsync(...)` returns. Logic: `entries.Where(e => string.Equals(e.Category, "Commander", StringComparison.OrdinalIgnoreCase)).Select(e => e.Name).FirstOrDefault()`. Returns a tuple `(DeckCacheWriteResult result, string? commanderName)`.
    - `RunAsync` (lines 92-118) replaces the two `MarkDecksProcessedAsync(new[] { deckId }, ...)` calls with single-deck variants: success path passes the captured commander name; failure/skip path passes null commander.
    - Build still passes for `DeckFlow.CLI` and tests (no caller signature broken — only new methods added).
  </behavior>
  <action>
    **Step A — `CategoryKnowledgeRepository.cs`:**
    Add two new public methods immediately above the existing `MarkDecksProcessedAsync` (around line 638).

    **A.1 — `MarkDeckProcessedAsync` (bulk-path UPDATE):**
    ```csharp
    public async Task MarkDeckProcessedAsync(
        string deckId,
        string? commanderName,
        bool skip = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        // D-17: capture commander identity in the same UPDATE that flips processed=1 so the
        // harvest stats panel (top-10 commanders) can read deck_queue.commander_name without
        // a join into card_category_observations.
        command.CommandText = """
            UPDATE deck_queue
               SET processed = 1,
                   skipped = @skipped,
                   last_checked_utc = @now,
                   commander_name = @commanderName
             WHERE deck_id = @deckId;
            """;
        RelationalDatabaseConnection.AddParameter(command, "@deckId", deckId);
        RelationalDatabaseConnection.AddParameter(command, "@now", DateTimeOffset.UtcNow.ToString("O"));
        RelationalDatabaseConnection.AddParameter(command, "@skipped", skip ? 1 : 0);
        RelationalDatabaseConnection.AddParameter(command, "@commanderName", (object?)commanderName ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    ```

    **A.2 — `MarkUrlDeckProcessedAsync` (URL-path UPSERT — B2):**
    ```csharp
    /// <summary>
    /// B2 / D-17: idempotently records a URL-imported deck as processed with its commander name.
    /// Mirrors the AddDeckIdsAsync UPSERT idiom but always lands processed=1 (URL flow has no
    /// queueing step) so Plan 04 SubmitUrl can ship a deck_queue row in one round-trip and
    /// SC #2 ("commander appears in top-commanders list after URL submit") is provable.
    /// COALESCE on commander_name preserves a previously-captured name if a re-import fails
    /// to extract one.
    /// </summary>
    public async Task MarkUrlDeckProcessedAsync(
        string deckId,
        string? commanderName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deckId);

        await EnsureSchemaAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow.ToString("O");
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO deck_queue (deck_id, inserted_utc, processed, skipped, last_checked_utc, commander_name)
            VALUES (@deckId, @now, 1, 0, @now, @commanderName)
            ON CONFLICT(deck_id) DO UPDATE
            SET processed = 1,
                skipped = 0,
                last_checked_utc = excluded.last_checked_utc,
                commander_name = COALESCE(excluded.commander_name, deck_queue.commander_name);
            """;
        RelationalDatabaseConnection.AddParameter(command, "@deckId", deckId);
        RelationalDatabaseConnection.AddParameter(command, "@now", now);
        RelationalDatabaseConnection.AddParameter(command, "@commanderName", (object?)commanderName ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    ```

    Add `<summary>` XML doc on `MarkDeckProcessedAsync` identifying D-17 + the harvest_runs stats use case. Do NOT modify the existing batch `MarkDecksProcessedAsync` signature — leave it intact (tests/CLI rely on it). Optionally have the batch method internally call the new single-deck method, but keep its surface unchanged.

    **Step B — `ArchidektDeckCacheSession.cs`:**
    1. Modify `PersistDeckAsync` (line 148) to return `Task<(DeckCacheWriteResult Result, string? CommanderName)>`. Body:
       ```csharp
       private async Task<(DeckCacheWriteResult Result, string? CommanderName)> PersistDeckAsync(string deckId, CancellationToken cancellationToken)
       {
           var source = $"archidekt_live:{deckId}";
           var alreadyCached = await _repository.HasSourceDataAsync(source, cancellationToken);
           var entries = await _deckImporter.ImportAsync(deckId, cancellationToken);

           // D-17: extract the commander entry from the imported deck. Most decks have exactly
           // one Commander; if there are multiple (partner pairs etc.) take the first deterministically.
           string? commanderName = entries
               .Where(e => string.Equals(e.Category, "Commander", StringComparison.OrdinalIgnoreCase))
               .Select(e => e.Name)
               .FirstOrDefault();

           await DeckCategoryCacheWriter.ReplaceDeckEntriesAsync(_repository, source, entries, cancellationToken);
           return (alreadyCached ? DeckCacheWriteResult.Updated : DeckCacheWriteResult.Added, commanderName);
       }
       ```
    2. In `RunAsync` (line ~96), update the call site:
       ```csharp
       var (cacheResult, commanderName) = await PersistDeckAsync(deckId, cancellationToken);
       if (cacheResult == DeckCacheWriteResult.Added)
       {
           added++;
       }
       else
       {
           updated++;
       }

       _logger?.LogInformation("Cached categories from deck {DeckId} ({Result}) commander={Commander}.", deckId, cacheResult, commanderName ?? "(none)");
       await _repository.MarkDeckProcessedAsync(deckId, commanderName, skip: false, cancellationToken: cancellationToken);
       ```
    3. In the catch block (line ~117), replace the batch call:
       ```csharp
       await _repository.MarkDeckProcessedAsync(deckId, commanderName: null, skip: true, cancellationToken: cancellationToken);
       ```
    Keep the rest of `RunAsync` (idle-poll, stopwatch, count returns) untouched. Add no new fields to the class. Use `using System.Linq;` if not already imported (likely already).
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -10 && grep -q "MarkDeckProcessedAsync" DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs && grep -q "MarkUrlDeckProcessedAsync" DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs && grep -q "commander_name = @commanderName" DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs && grep -q "ON CONFLICT(deck_id) DO UPDATE" DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs && grep -q "string\\? commanderName" DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs && grep -q "Category == \"Commander\"\\|\"Commander\"" DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs</automated>
  </verify>
  <done>Build exits 0; both new methods (`MarkDeckProcessedAsync`, `MarkUrlDeckProcessedAsync`) present; commander extraction logic present in `PersistDeckAsync`; success path calls `MarkDeckProcessedAsync(deckId, commanderName, skip: false, ...)`; skip path calls it with `commanderName: null`; URL-path UPSERT idiom present (`ON CONFLICT(deck_id) DO UPDATE`).</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: ArchidektCacheJobService PG migration + cancel CTS + interface CancelActiveAsync</name>
  <files>DeckFlow.Web/Services/ArchidektCacheJobService.cs, DeckFlow.Web/Services/IArchidektCacheJobService.cs</files>
  <behavior>
    - `IArchidektCacheJobService` gains `Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default)`. Returns `true` if a job was active and cancellation was signalled; `false` otherwise.
    - `ArchidektCacheJobService` ctor gains `IHarvestRunStore _runStore` parameter.
    - `_jobs` ConcurrentDictionary and `_activeJobId` field are removed entirely.
    - New fields: `private CancellationTokenSource? _activeJobCts;` and `private readonly object _ctsLock = new();`.
    - `EnqueueAsync` flow:
      1. Validate duration > 0 and ≤ 60 min (existing throws preserved verbatim).
      2. Call `await _runStore.GetActiveAsync(ct)`. If non-null, return existing-job result (`StartedNewJob=false`) by mapping the row to `ArchidektCacheJobStatus`.
      3. Else `await _runStore.InsertQueuedAsync(HarvestRunKind.Bulk, (int)Math.Ceiling(duration.TotalSeconds), url: null, DateTimeOffset.UtcNow, ct)` to get the new Guid.
      4. Write a `QueuedJobSignal(Guid jobId, int durationSeconds)` (new private record) to the existing channel.
      5. Return `new ArchidektCacheJobEnqueueResult(/* status from queued row */, StartedNewJob: true)`.
    - `ExecuteAsync` flow:
      ```csharp
      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          await foreach (var signal in _queue.Reader.ReadAllAsync(stoppingToken))
          {
              using var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
              lock (_ctsLock) { _activeJobCts = jobCts; }

              try
              {
                  await _runStore.UpdateStateAsync(signal.JobId, HarvestRunState.Running,
                      startedUtc: DateTimeOffset.UtcNow, completedUtc: null,
                      decksProcessed: 0, additionalDecksFound: 0, errorMessage: null,
                      jobCts.Token);

                  var initial = await _knowledgeStore.GetProcessedDeckCountAsync(jobCts.Token);
                  var decksProcessed = await _knowledgeStore.RunCacheSweepAsync(_logger, signal.DurationSeconds, jobCts.Token);
                  var final = await _knowledgeStore.GetProcessedDeckCountAsync(jobCts.Token);

                  await _runStore.UpdateStateAsync(signal.JobId, HarvestRunState.Succeeded,
                      startedUtc: null, completedUtc: DateTimeOffset.UtcNow,
                      decksProcessed: decksProcessed,
                      additionalDecksFound: Math.Max(final - initial, 0),
                      errorMessage: null, CancellationToken.None);
              }
              catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
              {
                  throw; // host shutdown — startup reaper handles next boot
              }
              catch (OperationCanceledException)
              {
                  await _runStore.UpdateStateAsync(signal.JobId, HarvestRunState.Cancelled,
                      startedUtc: null, completedUtc: DateTimeOffset.UtcNow,
                      decksProcessed: 0, additionalDecksFound: 0,
                      errorMessage: "cancelled by operator",
                      CancellationToken.None);
              }
              catch (Exception exception)
              {
                  _logger.LogError(exception, "Harvest.Run.Failed jobId={JobId} message={Message}", signal.JobId, exception.Message);
                  await _runStore.UpdateStateAsync(signal.JobId, HarvestRunState.Failed,
                      startedUtc: null, completedUtc: DateTimeOffset.UtcNow,
                      decksProcessed: 0, additionalDecksFound: 0,
                      errorMessage: exception.Message, CancellationToken.None);
              }
              finally
              {
                  lock (_ctsLock) { _activeJobCts = null; }
              }
          }
      }
      ```
    - `CancelActiveAsync` reads the field under `_ctsLock`, calls `Cancel()` on the CTS if non-null, returns true. Returns false (without throwing) when no job is active. The interim `Stopping` row is written by the controller (Plan 04) BEFORE calling this method — keeping the cancel itself fire-and-forget in the service.
    - `GetActiveJob()` and `GetJob(Guid)` now read from PG via `_runStore` (sync wrappers using `.GetAwaiter().GetResult()` — admin surface; tolerable). Both map `HarvestRunRow` → `ArchidektCacheJobStatus` via a private `MapToStatus` helper.
    - `EnqueueAsync` 60-min cap throw at lines 60-63 stays exactly as-is.
    - The existing public-API controller `ArchidektCacheJobsController` does not need any changes — its consumers see the same `ArchidektCacheJobStatus` shape.
  </behavior>
  <action>
    **Step A — `IArchidektCacheJobService.cs`:**
    Add a single new method to the interface:
    ```csharp
    /// <summary>
    /// Signals the currently active harvest job (if any) to stop after the in-flight deck completes.
    /// HARV-03 — graceful operator cancel.
    /// </summary>
    /// <returns>True if a job was active and cancellation was signalled; false if no active job.</returns>
    Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default);
    ```

    **Step B — `ArchidektCacheJobService.cs`:**
    1. Add `using DeckFlow.Web.Services.Harvest;` at top of file.
    2. Add ctor parameter `IHarvestRunStore runStore`. Update field declarations:
       - DELETE `_jobs` (line 39) and `_activeJobId` (line 43).
       - ADD:
         ```csharp
         private readonly IHarvestRunStore _runStore;
         private readonly object _ctsLock = new();
         private CancellationTokenSource? _activeJobCts;
         ```
       - Update ctor body to assign `_runStore` and `ArgumentNullException.ThrowIfNull(runStore)`.
    3. Replace the channel value type. Currently the channel carries `ArchidektCacheJobStatus`; change to a private `record QueuedJobSignal(Guid JobId, int DurationSeconds)` defined at the bottom of the file. Update `_queue` to `Channel<QueuedJobSignal>`. Update writer/reader sites accordingly.
    4. Rewrite `EnqueueAsync` per the <behavior> flow. Helper `MapToStatus(HarvestRunRow row)` builds an `ArchidektCacheJobStatus` from a row (preserve all fields the existing public API serializes; reuse the existing `ArchidektCacheJobStatus` record).
    5. Rewrite `ExecuteAsync` per the <behavior> body. Use structured logging template `"Harvest.Run.StateChange jobId={JobId} state={State} decksProcessed={DecksProcessed}"`.
    6. Add `CancelActiveAsync`:
       ```csharp
       public Task<bool> CancelActiveAsync(CancellationToken cancellationToken = default)
       {
           CancellationTokenSource? cts;
           lock (_ctsLock) { cts = _activeJobCts; }
           if (cts is null) return Task.FromResult(false);
           cts.Cancel();
           _logger.LogInformation("Harvest.Run.CancelRequested");
           return Task.FromResult(true);
       }
       ```
    7. Replace `GetJob(Guid)` and `GetActiveJob()` bodies to read PG synchronously via `.GetAwaiter().GetResult()` on `_runStore.GetActiveAsync(default)` / a new `_runStore.GetByIdAsync` if the existing `GetJob` is hit by callers — check `ArchidektCacheJobsController` for usage. If `GetByIdAsync` is needed, add it to `IHarvestRunStore` AND `HarvestRunStore` (Plan 01 contract extension acceptable since Plan 01 is the foundation; document the additive change in this plan's SUMMARY). If `GetJob` is unused after PG migration, throw `NotSupportedException` with a comment pointing readers to the new admin surface.
    8. Delete `ClearActiveJob` helper method entirely (lines 152-161).
    9. Confirm DI registration in `Program.cs` is unchanged — singleton + IHostedService wiring still works (DI resolves `IHarvestRunStore` automatically once Plan 07 registers it).

    Compile-time check: `dotnet build DeckFlow.sln` must pass. Public-API controller tests should still compile (they assert `ArchidektCacheJobStatus` shape, not internal storage).
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln --nologo --verbosity quiet 2>&amp;1 | tail -15 && grep -q "_activeJobCts" DeckFlow.Web/Services/ArchidektCacheJobService.cs && grep -q "_runStore.InsertQueuedAsync" DeckFlow.Web/Services/ArchidektCacheJobService.cs && grep -q "_runStore.UpdateStateAsync" DeckFlow.Web/Services/ArchidektCacheJobService.cs && grep -q "CancelActiveAsync" DeckFlow.Web/Services/IArchidektCacheJobService.cs && ! grep -q "ConcurrentDictionary<Guid, ArchidektCacheJobStatus>" DeckFlow.Web/Services/ArchidektCacheJobService.cs</automated>
  </verify>
  <done>Build exits 0; `_activeJobCts` field declared; both `_runStore.InsertQueuedAsync` and `_runStore.UpdateStateAsync` invoked at least once each; `CancelActiveAsync` exists on the interface; the old `ConcurrentDictionary<Guid, ArchidektCacheJobStatus>` declaration is gone.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Operator (BasicAuth) → AdminHarvestController → JobService.CancelActiveAsync | Cancel signal originates from authenticated /Admin surface (Plan 04). |
| JobService → IHarvestRunStore | All run-state writes flow through Plan 01's parameterized SQL. |
| Background loop → ArchidektDeckCacheSession → IArchidektDeckImporter | Existing trust boundary; commander_name is derived from already-parsed entries (no new untrusted input). |
| Plan 04 SubmitUrl → CategoryKnowledgeRepository.MarkUrlDeckProcessedAsync | Operator-supplied deck_id (validated upstream by ArchidektApiUrl.TryGetDeckId) flows into a parameterized UPSERT. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-07-07 | Tampering | commander_name capture | accept | Source is `IArchidektDeckImporter.ImportAsync` which already validates upstream JSON schema; commander_name is just one card's `Name` field — no escalation surface. |
| T-07-08 | Denial of service | _activeJobCts.Cancel() | mitigate | Lock-protected field; only one active job at a time by design (D-01 single-active-bulk contract); cancel is idempotent on already-cancelled CTS. |
| T-07-09 | Repudiation | error_message="cancelled by operator" | accept | Single-operator BasicAuth — repudiation not a threat at this user model. |
| T-07-10 | Tampering | sync GetJob/GetActiveJob via .GetAwaiter().GetResult() | accept | Admin/API surface only; no thread-pool starvation risk under expected sub-1RPS admin traffic. |
| T-07-35 | Tampering | MarkUrlDeckProcessedAsync deck_id parameter | mitigate | Plan 04 validates the URL with ArchidektApiUrl.TryGetDeckId before calling this method; SQL is parameterized; ON CONFLICT idempotent. |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` exits 0.
- `grep -c "_runStore.InsertQueuedAsync" DeckFlow.Web/Services/ArchidektCacheJobService.cs` ≥ 1.
- `grep -c "_runStore.UpdateStateAsync" DeckFlow.Web/Services/ArchidektCacheJobService.cs` ≥ 3 (Running + Succeeded + Cancelled + Failed transitions).
- `grep -c "CancelActiveAsync" DeckFlow.Web/Services/ArchidektCacheJobService.cs` ≥ 1.
- `grep -c "_activeJobCts" DeckFlow.Web/Services/ArchidektCacheJobService.cs` ≥ 4 (declaration + assign + read in cancel + null on finally).
- `grep -L "ConcurrentDictionary<Guid, ArchidektCacheJobStatus>" DeckFlow.Web/Services/ArchidektCacheJobService.cs` returns the file path (i.e. that dictionary is gone).
- `grep -c "MarkDeckProcessedAsync" DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` ≥ 1.
- `grep -c "MarkUrlDeckProcessedAsync" DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` ≥ 1.
- `grep -c "MarkDeckProcessedAsync" DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` ≥ 2 (success + skip paths).
- Existing public-API controller `ArchidektCacheJobsController` still compiles unchanged.
</verification>

<success_criteria>
- All bulk-job state transitions land in Postgres `harvest_runs`. In-memory dict is gone.
- Operator can cancel a running job via the (Plan-04-built) controller calling `CancelActiveAsync` — token landing flips state to `Cancelled` within 30s of the next deck-loop check (HARV-03, ROADMAP SC #3).
- Commander name is captured on every successful single-deck import in the bulk path; null on skips.
- URL-path UPSERT method `MarkUrlDeckProcessedAsync` (B2) exists for Plan 04 to consume; ROADMAP SC #2 becomes provable end-to-end.
- Existing public-API contract is preserved (no changes to `ArchidektCacheJobsController`).
- `EnqueueAsync` 60-min cap is intact (D-04 / HARV-01).
</success_criteria>

<output>
After completion, create `.planning/phases/07-harvest-controls-stats/07-02-SUMMARY.md` covering: removed fields, new ctor signature, the four state-transition write sites, the commander-capture site, the new URL-path UPSERT method (B2), and any contract addition to `IHarvestRunStore` (e.g., `GetByIdAsync` if it was needed).
</output>
</content>
</invoke>