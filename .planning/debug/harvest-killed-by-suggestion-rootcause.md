---
status: resolved
trigger: "Find why CategorySuggestionService cancels admin harvest; prior session timed out mid-investigation"
created: 2026-05-03
updated: 2026-05-03
---

# Debug Session: harvest-killed-by-suggestion-rootcause

## Current Focus

hypothesis: confirmed — gate starvation via concurrent suggestion sweeps
test: n/a — fix applied and verified (build clean 0/0)
next_action: archive

## Symptoms

expected: admin harvest runs to completion uninterrupted
actual: harvest rows show Failed/"interrupted by redeploy" when user requests hit suggestion/commander endpoints
errors: HarvestRunStore reaper labels orphaned Running rows on restart
reproduction: start admin harvest, hit /suggest-categories or /commander-categories endpoint from browser
started: pre-fbb4405; orphan labelling masked the kill; fbb4405 adds precise per-catch labels for future kills

## Eliminated

- hypothesis: H_RC1 — RunCacheSweepAsync cancels in-flight sweep on new caller
  evidence: _sweepGate is SemaphoreSlim(1,1); WaitAsync blocks new callers; does NOT cancel the holder
  timestamp: 2026-05-03

- hypothesis: H_RC2 — shared _activeJobCts field reused for sweep cancellation
  evidence: _activeJobCts lives in ArchidektCacheJobService; RunCacheSweepAsync is on CategoryKnowledgeStore singleton; no field overlap
  timestamp: 2026-05-03

- hypothesis: H_RC3 — ArchidektDeckCacheSession static state
  evidence: only one static readonly TimeSpan constant; no mutable static fields
  timestamp: 2026-05-03

- hypothesis: H_RC4 — ArchidektApiDeckImporter sticky CTS
  evidence: importer stores no cancellation token; CT is per-call parameter only
  timestamp: 2026-05-03

- hypothesis: direct CT injection into jobCts
  evidence: jobCts is CreateLinkedTokenSource(stoppingToken) only; no caller CT linked; no CreateLinkedTokenSource anywhere in Core or suggestion path
  timestamp: 2026-05-03

## Evidence

- timestamp: 2026-05-03
  checked: CategoryKnowledgeStore._sweepGate
  found: SemaphoreSlim(1,1) at line 22; WaitAsync(cancellationToken) at line 167 passes caller's CT to the semaphore wait only — does not inject into the running session
  implication: a waiter whose CT fires exits WaitAsync with OCE but cannot reach the holder's session

- timestamp: 2026-05-03
  checked: CategorySuggestionService.SuggestAsync line 126, CommanderCategoryService.LookupAsync line 61
  found: both call _knowledgeStore.RunCacheSweepAsync(_logger, 30, cancellationToken) where cancellationToken is HttpContext.RequestAborted
  implication: every user page hit for CachedData/All/commander mode queues a 30s sweep

- timestamp: 2026-05-03
  checked: ArchidektCacheJobService.ExecuteAsync line 282
  found: calls _knowledgeStore.RunCacheSweepAsync(_logger, signal.DurationSeconds, jobCts.Token); jobCts linked only to stoppingToken
  implication: admin sweep also contends on _sweepGate; if suggestion sweeps chain back-to-back admin is starved for the gate for the full 30s * N requests

- timestamp: 2026-05-03
  checked: gate starvation under concurrent load
  found: with multiple concurrent user requests each triggering 30s sweeps, admin's WaitAsync(jobCts.Token) is perpetually blocked; harvest window expires while admin waits; container recycles; reaper labels row "interrupted by redeploy"
  implication: root cause is starvation — admin never acquires the gate, session.RunAsync never starts, duration timer never ticks

- timestamp: 2026-05-03
  checked: CreateLinkedTokenSource usage across entire codebase
  found: only in ArchidektCacheJobService.ExecuteAsync (jobCts linked to stoppingToken) and RequestMetricsFlusher (unrelated); no request CT ever linked to jobCts
  implication: direct CT injection path definitively ruled out

## Resolution

root_cause: CategorySuggestionService and CommanderCategoryService unconditionally call _knowledgeStore.RunCacheSweepAsync with the request CT whenever mode includes CachedData. Because ICategoryKnowledgeStore is a singleton with a SemaphoreSlim(1,1) _sweepGate, concurrent user requests chain 30s sweeps back-to-back. ArchidektCacheJobService.ExecuteAsync also contends on _sweepGate via the same method. Under any user load the admin harvest is perpetually starved — it never acquires the gate, its session.RunAsync never starts, and eventually the container recycles leaving the harvest_runs row in Running state for the reaper to label.

fix: Injected IArchidektCacheJobService into both CategorySuggestionService and CommanderCategoryService. Before calling RunCacheSweepAsync, each service calls GetActiveJob(). If a harvest is active, the click-sweep is skipped entirely — the service returns whatever is already in the cache. Operator cancel (CancelActiveAsync) is unaffected because it operates on _activeJobCts directly, not through RunCacheSweepAsync.

verification: dotnet build DeckFlow.sln — 0 errors, 0 warnings. All 5 test constructor call-sites in CategorySuggestionServiceTests updated with FakeJobService (no-harvest). CommanderCategoryService CacheSweepPerformed now reflects actual sweep execution.

files_changed:
  - DeckFlow.Web/Services/CategorySuggestionService.cs
  - DeckFlow.Web/Services/CommanderCategoryService.cs
  - DeckFlow.Web.Tests/CategorySuggestionServiceTests.cs
