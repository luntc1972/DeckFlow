---
phase: 100-creator-style-tool-surface
reviewed: 2026-07-19T00:00:00Z
depth: standard
files_reviewed: 30
files_reviewed_list:
  - DeckFlow.Web/Controllers/CreatorStyleController.cs
  - DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs
  - DeckFlow.Web/Services/CreatorStyle/CreatorStyleDeckAnalysis.cs
  - DeckFlow.Web/Services/CreatorStyle/SubmittedDeckStatsBuilder.cs
  - DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs
  - DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs
  - DeckFlow.Web/Services/CreatorStyle/ArchidektOwnerClient.cs
  - DeckFlow.Web/Services/CreatorStyle/CreatorProfileDeckCrawler.cs
  - DeckFlow.Web/Services/Scryfall/ScryfallCollectionResolver.cs
  - DeckFlow.Web/Services/Scryfall/ScryfallBatching.cs
  - DeckFlow.Web/Services/Scryfall/CachedNameResolution.cs
  - DeckFlow.Web/Services/Scryfall/CardGroundingGuard.cs
  - DeckFlow.Web/Services/Scryfall/ScryfallCardNameGrounder.cs
  - DeckFlow.Web/Services/Scryfall/CardLookupService.cs
  - DeckFlow.Web/Services/Scryfall/ScryfallLimits.cs
  - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs
  - DeckFlow.Web/Services/Content/CreatorStyleSeedLoader.cs
  - DeckFlow.Web/Services/Content/ICreatorStyleSeedLoader.cs
  - DeckFlow.Web/Services/Content/ContentKbSeedLoader.cs
  - DeckFlow.Web/Services/SeedJson.cs
  - DeckFlow.Web/Services/PacketSessionCache.cs
  - DeckFlow.Web/Services/DeckConvertService.cs
  - DeckFlow.Web/Services/Packets/ScryfallReferenceResolver.cs
  - DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs
  - DeckFlow.Web/Extensions/HttpClientServiceCollectionExtensions.cs
  - DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs
  - DeckFlow.Web/Program.cs
  - DeckFlow.Web/Models/CreatorStyleViewModel.cs
  - DeckFlow.Web/Models/CreatorStyleRequest.cs
  - DeckFlow.Web/Views/Deck/CreatorStyle.cshtml
  - DeckFlow.Core/Content/CreatorStyleProfileStore.cs
  - DeckFlow.Core/Content/CreatorStyleProfileSummary.cs
  - DeckFlow.Core/Content/CreatorStyleProfileReadModel.cs
  - DeckFlow.Core/Content/ContentKbPaths.cs
  - DeckFlow.Core/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorer.cs
  - DeckFlow.Core/Knowledge/ProfileFusion/ProfileFusionEngine.cs
  - DeckFlow.Core/Knowledge/MeasuredStyleExtraction/StapleStripper.cs
  - DeckFlow.CLI/CreatorStyleCommandRunners.cs
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
  - DeckFlow.Web/Services/Tools/ToolRegistry.cs
  - DeckFlow.Web/Models/DeckPageTab.cs
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 100: Code Review Report

**Reviewed:** 2026-07-19
**Depth:** standard
**Files Reviewed:** 30 (production-code focus; test files spot-checked, not exhaustively re-litigated)
**Status:** issues_found

## Summary

Reviewed the creator-style tool surface (controller, packet service, shared Scryfall/manabase
resolution refactors, seed loaders, profile store, DI wiring) plus the post-phase "simplify" batch
that collapsed several constructors into `Func<...>` field patterns and extracted shared helpers
(`CreatorStyleDeckAnalysis`, `ScryfallCollectionResolver`, `CachedNameResolution`, `ScryfallBatching`).

No BLOCKER-class defects were found. The mechanical refactors (`ProfileFusionEngine.CreateFusedTarget`
extraction, `ManabaseAnalysisService`/`MeasuredStyleProfileBuilder`/`SubmittedDeckStatsBuilder` now
routing through the shared `CreatorStyleDeckAnalysis`/`ScryfallCollectionResolver` helpers, and the
`CreatorStylePacketService`/`ArchidektOwnerClient` constructor collapses) were traced parameter-by-
parameter against their pre-refactor call sites and preserve behavior exactly, including error-message
text, HTTP status-code handling, and DI registration argument order (cross-checked against
`PacketServiceCollectionExtensions.cs` and `CreatorStyleDiRegistrationTests.cs`). XSS surface in
`CreatorStyle.cshtml` is clean (all interpolation goes through Razor's default HTML encoding, no
`Html.Raw`); CSRF (`[ValidateAntiForgeryToken]` + `@Html.AntiForgeryToken()`) and the feature-flag gate
are present on both the GET and POST actions. `GetAllAsync`/`GetBySlugAsync` bool/timestamp decoding
parity in `CreatorStyleProfileStore` is fine because both route through the same global Dapper type
handlers (`DapperTypeHandlers.cs`) — there's no per-query divergence to find.

Two real, narrow findings below (both WARNING, not BLOCKER): a `Task.WhenAll` exception-swallowing gap
in the Program.cs startup seed-load, and a `CancellationToken.None` fallback that removes the timeout
guard specifically on the guarded controller's error-rendering path. Three INFO items round out the
report — a vestigial unused request field folded into the packet cache key, and an explicit note that
the new `PacketSessionCache` wiring for creator-style is a documented no-op today given the single-flag
topology (worth flagging so a future reviewer doesn't assume the cache path is actually exercised by
current tests/production traffic).

## Warnings

### WR-01: Startup seed-load `Task.WhenAll` can silently drop the second loader's exception

**File:** `DeckFlow.Web/Program.cs:289-291`
**Issue:** The content-KB and creator-style seed loaders are started concurrently and awaited via
`Task.WhenAll`:
```csharp
Task contentKbSeedTask = app.Services.GetRequiredService<IContentKbSeedLoader>().LoadIfPresentAsync();
Task creatorStyleSeedTask = app.Services.GetRequiredService<ICreatorStyleSeedLoader>().LoadIfPresentAsync();
await Task.WhenAll(contentKbSeedTask, creatorStyleSeedTask);
```
Both loaders are designed to fail fast on malformed seed JSON (`CreatorStyleSeedLoader.LoadProfilesIfPresentAsync`
/ `LoadDeckCacheIfPresentAsync` in `DeckFlow.Web/Services/Content/CreatorStyleSeedLoader.cs:57-101` let
`JsonException` propagate uncaught, with no internal try/catch or pre-throw logging). When you `await`
a `Task.WhenAll(...)` whose member tasks are *both* faulted, the awaiter only rethrows the **first**
task's exception (documented .NET behavior for `TaskAwaiter.GetResult()` against an `AggregateException`)
— the second task's exception is never observed or logged anywhere. Concretely: if a commit lands with
both `content-kb/seed/index-seed.json` and `content-kb/seed/creator-style-profiles.json` (or
`creator-deck-cache.json`) malformed at the same time, the top-level `Program.Main` catch/Serilog fatal
log (per the documented architecture pattern) will only ever report one of the two failures, and an
operator fixing the reported file will hit a second, unreported failure on the next deploy attempt.
**Fix:** Either observe both tasks explicitly and log each failure before rethrowing, or run them
sequentially now that they touch independent tables (no correctness reason for concurrency at
startup — this is a one-time serial path, not request-hot):
```csharp
var seedResults = await Task.WhenAll(
    contentKbSeedTask.ContinueWith(t => LogSeedFailure("content-kb", t), TaskScheduler.Default),
    creatorStyleSeedTask.ContinueWith(t => LogSeedFailure("creator-style", t), TaskScheduler.Default));
// or, simplest and equally fast at startup:
await app.Services.GetRequiredService<IContentKbSeedLoader>().LoadIfPresentAsync();
await app.Services.GetRequiredService<ICreatorStyleSeedLoader>().LoadIfPresentAsync();
```

### WR-02: Error-view picker rebuild drops the timeout guard on the general-exception path

**File:** `DeckFlow.Web/Controllers/CreatorStyleController.cs:141-181`
**Issue:** `RunGuardedAsync` wraps the whole POST body in `timeoutScope` (`LookupTimeout`), but its
local `ErrorViewAsync` helper always rebuilds the picker list with `CancellationToken.None`:
```csharp
async Task<IActionResult> ErrorViewAsync(string message)
{
    return View("CreatorStyle", new CreatorStyleViewModel
    {
        Request = request,
        AvailableCreators = await BuildPickerOptionsAsync(CancellationToken.None),
        ErrorMessage = message,
    });
}
```
This is deliberate for the `OperationCanceledException` (timeout) branch — you don't want the
just-expired token cancelling the error-view rebuild too. But it also applies unconditionally to the
`InvalidOperationException`, `HttpRequestException`, and generic `Exception` catch branches. If the
*original* failure was, e.g., a validation error unrelated to the profile/site-index stores, but those
same stores are independently degraded (slow SQLite lock contention, Postgres connection-pool
exhaustion), `BuildPickerOptionsAsync(CancellationToken.None)` has no timeout at all and can hang the
request indefinitely with no way for the client or `RequestAborted` to cut it off, unlike the primary
`body(timeoutScope.Token)` call which is bounded.
**Fix:** Give the error-view rebuild its own short, independent timeout instead of `CancellationToken.None`,
e.g. `using var errorScope = CreateTimeoutScope(LookupTimeout); ... BuildPickerOptionsAsync(errorScope.Token)`
(a fresh scope, not the already-cancelled `timeoutScope`).

## Info

### IN-01: `CreatorStyleRequest.Format` is threaded into the cache key but never used elsewhere

**File:** `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs:404-408`, `DeckFlow.Web/Models/CreatorStyleRequest.cs:69-73`
**Issue:** `BuildCacheInputs` includes `Format: request.Format.Trim()` in the `CreatorStyleCacheInputs`
record used to compute the packet cache key. Nothing else in the creator-style pipeline
(`CreatorStylePacketService.BuildAsync`, `SubmittedDeckStatsBuilder`, `CreatorStyleDeckAnalysis`,
`BuildArtifactText`) ever reads `request.Format` — the deck is always parsed/analyzed as Commander
regardless of the posted value, and the view doesn't expose a way to change it either (no `Format`
input control in `CreatorStyle.cshtml`). It's a vestigial carry-over from the `DeckAnalysisRequest`
cache-input shape. Harmless today (only widens the cache key), but confusing for the next person
reading the packet-build pipeline who will expect `Format` to gate something.
**Fix:** Either drop `Format` from `CreatorStyleRequest`/`CreatorStyleCacheInputs` until the tool
actually supports non-Commander formats, or wire it through to deck-context validation so the field
does something.

### IN-02: Creator-style `PacketSessionCache` wiring is a documented no-op in the current single-flag topology

**File:** `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs:120-125, 401-402`
**Issue:** `PromptMutatingCreatorStyleFlags` contains exactly one entry —
`CreatorStyleToolEnabledFlag` ("tool.creator-style.enabled") — which is also the flag gating both
controller actions (`[FeatureFlagGate(CreatorStylePacketService.CreatorStyleToolEnabledFlag)]`). Since
the flag must be ON for any request to reach `TryComputeCacheKeyAsync`/`BuildAsync` at all,
`ShouldBypassPacketCache()` is unconditionally `true` for every live request, so
`TryComputeCacheKeyAsync` always returns `null` and `FinalizeResult` never calls `_packetCache.Set(...)`.
The in-code comment acknowledges this ("the single-flag topology currently makes creator-style cache
bypass a no-op"), so this isn't a defect — but it means the cache-key field-bag completeness question
(focus area 3 of this review) is currently moot in production: the packet cache is never populated or
read for this tool today. Flagging for visibility only, so a future change that adds a second
prompt-mutating flag (and expects caching to already be exercised) doesn't assume existing test
coverage proves the cache path works end-to-end against live traffic.
**Fix:** None required now. When a second, non-gating prompt-mutating flag is added for creator-style,
add a test that exercises an actual cache hit (flag OFF... not possible today since the tool itself is
flag-gated) — more realistically, add an integration-style unit test that calls `BuildAsync` twice with
`ShouldBypassPacketCache()` stubbed false via the internal test constructor, to prove the key/read/write
path is wired correctly before it's needed.

### IN-03: `CardGroundingGuard.LoadBatchResolutionsAsync` can issue a redundant per-name Scryfall fallback call for case-variant duplicates

**File:** `DeckFlow.Web/Services/Scryfall/CardGroundingGuard.cs:73-128`
**Issue:** Candidates are deduplicated into `uniqueCandidates` by a case-insensitive cache key
(`seenKeys.Add(cacheKey)` where `cacheKey` is lower-invariant). If two candidate names differing only
by case are submitted in the same call (e.g. one from the whitelist-additional-candidates set built in
`CreatorStylePacketService.BuildAsync`), only the first-seen case variant is included in the batch sent
to `cards/collection`. If that batch call throws (network error, non-2xx), the `catch` block in
`ResolveBatchChunkAsync` (line 194-200) marks only the batch's own `candidateNames` as
`UpstreamUnavailable` and explicitly skips caching that outcome. The second case-variant, never having
been added to the batch or the cache, then falls through to `GetOrFetchResolutionAsync` in the final
loop of `LoadBatchResolutionsAsync` (line 111-125) and issues its own separate single-name Scryfall
request. Functionally correct (each variant still resolves independently), but it defeats the batching
optimization specifically on the upstream-failure path, and issues an extra live HTTP call the batch
already determined would fail. Performance is out of scope per review constraints, but noting since it
sits directly on the grounding/degradation error path this review was asked to trace.
**Fix (optional, not required for this ship):** When caching an `UpstreamUnavailable` batch failure,
still write a very-short-TTL negative marker keyed by the same lower-invariant cache key so
same-request case-variant duplicates short-circuit instead of re-hitting Scryfall.

---

_Reviewed: 2026-07-19_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
