---
slug: harvest-killed-by-suggestion
status: investigating
trigger: "Admin harvest dies mid-run when a category suggestion request comes in; Run Log mislabels it 'interrupted by redeploy'"
created: 2026-05-03
updated: 2026-05-03
---

# Debug Session: harvest-killed-by-suggestion

## Symptoms (operator-supplied + screenshot evidence)

- **Symptom:** Admin starts a category harvest at /Admin/Harvest. While it's running, the operator (or a user) hits a category-suggestion endpoint elsewhere on the site. The admin harvest stops.
- **Run Log shows:** rows with State=Failed, Error="interrupted by redeploy", Decks=0. Two rows from the screenshot: 18:41:13Z (900s requested) and 18:37:46Z (3600s requested) — both Failed, 0 decks processed.
- **No actual redeploy occurred** at those timestamps per operator (Phase 8 deploy was at ~21:30, hours later).
- **Reproduction:** admin Run Now → admin or any user requests category suggestion → admin harvest dies. Reproducibility unconfirmed but operator describes it as a recognized pattern.

## Key code observations (already gathered)

**The "interrupted by redeploy" message is REAPER-only.** Source: `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs:385-399`. The reaper SQL runs at startup against any rows still in non-terminal state (`Queued/Running/Stopping`):

```sql
UPDATE harvest_runs
   SET state='Failed',
       error_message='interrupted by redeploy',
       completed_utc = now()
 WHERE state IN ('Queued','Running','Stopping');
```

Comment at line 383: "any non-terminal row at startup is by definition orphaned (single-instance Render)."

**Implication:** the message means "row was non-terminal at next process start." It does NOT mean "Render redeployed." If the harvest crashes mid-run without writing a terminal state to the row, the next innocent restart blames the older Phase 8 deploy.

**Cancellation wiring:**
- `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs:127` — `RunNow(int durationSeconds, CancellationToken cancellationToken)`. The `cancellationToken` parameter is auto-bound by ASP.NET Core to `HttpContext.RequestAborted`.
- Controller calls `_jobService.EnqueueAsync(duration, cancellationToken)` — passing the REQUEST token in.
- `DeckFlow.Web/Services/ArchidektCacheJobService.cs:137` — singleton hosted service. Holds `private CancellationTokenSource? _activeJobCts;`.
- `ArchidektCacheJobService.cs:254` — `ExecuteAsync(CancellationToken stoppingToken)` is the BackgroundService loop; line 259 creates `var jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)` for the JOB CTS.
- `CancelActiveAsync` (line 231) is what AdminHarvestController.Cancel calls. It uses `_activeJobCts.Cancel()`.

**The smoking-gun question:** does `EnqueueAsync` thread the **request CancellationToken** into the linked-CTS chain that drives the long-running harvest? If yes, then when the request completes (or RequestAborted fires for whatever reason), the harvest gets cancelled too. The catch (OperationCanceledException) clauses at lines 299/304 might NOT correctly distinguish "operator cancel" from "request token cancel" → the row gets left in non-terminal state → next startup reaper labels it "interrupted by redeploy".

## Hypothesis Bank

1. **H1 (most likely): EnqueueAsync leaks request CT into harvest CTS chain.** `AdminHarvestController.RunNow` passes `cancellationToken` (= `HttpContext.RequestAborted`) into `EnqueueAsync`. If EnqueueAsync linked-source-creates the harvest CTS using THAT token, then the harvest's lifetime is tied to the request that started it. The browser closing the page, the redirect after enqueue, or any RequestAborted fire kills the harvest. Repro pattern matches: starting harvest from admin works ON the response, but when a different request (category suggestion) arrives, ASP.NET Core's request-handling thread pool churn or HTTP/2 stream multiplexing might trigger early RequestAborted on the original admin request. Or worse, the linked CTS shares a token with something MORE persistent.
2. **H2: Singleton CategorySuggestionService steals the harvest CTS.** Maybe both `CategorySuggestionService` and `ArchidektCacheJobService` are talking to a shared resource (a static, an `IDeckEntryLoader`, an `IArchidektDeckImporter`) that keeps a `CancellationTokenSource` and cancels it on each use. Long shot, but worth checking.
3. **H3: Terminal-state-write failure leaves rows orphaned.** The catch (OperationCanceledException) at line 299 (when `stoppingToken.IsCancellationRequested` is true — which means HOST shutdown) doesn't write a terminal state. Then the lone catch (OperationCanceledException) at 304 (operator cancel) writes Cancelled. But if a request-token-cancel path exists, neither catch matches the right way, and the finally block doesn't write Failed either. Need to read 254-330 in full.
4. **H4: ArchidektApiDeckImporter cancellation issue.** The Phase 7 STATE.md "Pending Todo" says: "Pre-condition for Phase 7: audit ArchidektApiDeckImporter cancellation token threading before designing harvest cancel UI (pitfall B3 from SUMMARY.md)." Maybe that audit wasn't completed; the importer might be terminating runs unexpectedly.

## Files to read first

- `DeckFlow.Web/Services/ArchidektCacheJobService.cs` — full file. Pay attention to: `EnqueueAsync` (line 161-220ish), `_activeJobCts` lifecycle (writers and readers), `ExecuteAsync` loop (254-330ish), all catch clauses, finally block, and HOW the harvest run row is transitioned to terminal state.
- `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs:127-150` — RunNow path. Confirm what token is passed and what's done with it.
- `DeckFlow.Web/Services/CategorySuggestionService.cs` — does it share any state with the harvest service? `IDeckEntryLoader`, `IArchidektDeckImporter`, any static CTS, any singleton with a Cancel call inside it?
- `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` — does it accept a cancellation token, propagate it correctly, and return without sticky state?
- `git log --oneline -20 -- DeckFlow.Web/Services/ArchidektCacheJobService.cs DeckFlow.Web/Services/Harvest/ DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` — recent changes in this area (Phase 7 work).

## Current Focus

- hypothesis: H1 (request CT leaks into harvest CTS chain) — VERY likely based on parameter shape `EnqueueAsync(TimeSpan, CancellationToken)` accepting a cancellation token at all. A correct hosted-service-trigger API would NOT take an enqueue-time CT (or would use it ONLY to gate the database insert, not the long-running work).
- test: Read the ArchidektCacheJobService EnqueueAsync body and trace where the passed cancellationToken ends up. If it's stored in or linked-into `_activeJobCts`, H1 confirmed.
- expecting: A `CreateLinkedTokenSource(stoppingToken, cancellationToken)` or similar that ties harvest CTS lifetime to caller token. Fix shape: `EnqueueAsync` should use the caller token only for the DB insert (the synchronous "did we enqueue?" gate); the harvest CTS should link only `stoppingToken` (host lifetime).
- next_action: Confirm H1 by reading EnqueueAsync; if confirmed, propose fix; if NOT confirmed, escalate to reading CategorySuggestionService for shared-state path (H2).

## Evidence

(none yet — debugger to populate)

## Eliminated

(none yet)

## Resolution

- root_cause: TBD
- fix: TBD
- verification: TBD
- files_changed: TBD
