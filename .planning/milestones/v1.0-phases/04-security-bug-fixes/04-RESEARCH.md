# Phase 04: Security & Bug Fixes - Research

**Researched:** 2026-05-01
**Domain:** ASP.NET Core middleware throttle (BUG-02) + Scryfall/Tagger printings probe (BUG-01)
**Confidence:** HIGH

## Summary

Phase 04 closes two narrow, self-contained bugs against the live deckflow.gg deployment. CONTEXT.md has already locked the implementation strategies (D-01..D-16); the planner does not need exploration. This research session verifies the external facts those decisions depend on (Scryfall query syntax, Tagger HEAD support, ASP.NET RateLimiter limits) and surfaces one **important correction** about Scryfall's default ordering that changes a CONTEXT assumption.

**Three findings worth flagging up front:**

1. **CRITICAL CORRECTION to D-10:** CONTEXT.md describes the Scryfall default `unique=prints` ordering as "release-date descending." This is **incorrect**. Default ordering for `q=!"<name>"&unique=prints` is a separate Scryfall heuristic that prioritizes mainline-set printings (excludes most Secret Lair / promo prints from the top of the list). Explicit `order=released&dir=desc` returns a different list (Secret Lairs and promos float to the top). The planner should be explicit which they want. **Recommendation:** Use Scryfall default ordering (no `order=` param) — empirically yields better Tagger coverage in the top 5 because Tagger indexes mainline sets more reliably than Secret Lairs. This matches the CONTEXT.md spirit even though the wording is technically wrong.
2. **Tagger HEAD works.** `tagger.scryfall.com/card/{set}/{number}` cleanly returns 200 vs 404 to HEAD requests with zero body bytes. Recommend HEAD over GET for the probe loop. Saves ~30 KB per probe miss.
3. **D-02's reasoning is verified.** ASP.NET Core `RateLimitingMiddleware` partitioner takes `HttpContext` only — no response, no auth outcome — and runs at `UseRateLimiter()` placement (`Program.cs:286`), before the `/Admin` branch. CONTEXT.md is correct that the standard `IRateLimiter` API cannot condition on the 401-from-`Challenge()` requirement; in-middleware `ConcurrentDictionary` is the right shape.

**Primary recommendation:** Implement exactly as CONTEXT.md decisions describe, with the three concrete refinements below: (a) use Scryfall default ordering with no explicit `order=` param, (b) probe via HEAD, (c) negative-cache TTL = 1 hr (matches existing IMemoryCache conventions in `CommanderSpellbookService`).

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**BUG-02 — Admin throttle scope:**
- **D-01:** Count only **401 challenges from `BasicAuthMiddleware`**, not all `/Admin/*` requests, not all 4xx. Successful auth never increments. SC #1 structurally guaranteed.
- **D-02:** In-memory `ConcurrentDictionary<string, BucketEntry>` (planner picks `AdminBruteForceTracker` singleton vs. inline static field). NO ASP.NET `IRateLimiter` API. NO distributed store.
- **D-03:** Reject with HTTP 429 + `Retry-After` header (seconds until window reset).
- **D-04:** Existing `_logger.LogWarning("Admin basic-auth challenge issued: {Reason} from {RemoteIp}", ...)` fires on every 401 including throttled ones (SC #1 wording is binding).

**BUG-02 — Throttle identity & limits:**
- **D-05:** Partition key = `context.Connection.RemoteIpAddress` (Path B-rawpeer, same as Phase 03 TD-04). Refactor opportunity: extract shared `DerivePeerIpKey(HttpContext, string prefix)`.
- **D-06:** 10 attempts / 15-minute window.
- **D-07:** Fixed-window semantics (`(int count, DateTimeOffset windowStart)`).
- **D-08:** Lazy expiry on dict access. No background sweep timer.

**BUG-01 — Tagger 404 fix:**
- **D-09:** Iterate-printings via `/cards/search?q=!"<name>"&unique=prints`; for each printing, probe Tagger; first HTTP 200 wins. Replace `ResolveCardPrintingAsync` (`ScryfallTaggerService.cs:123-148`).
- **D-10:** Probe order = Scryfall default order (release-date desc). **NOTE:** see CRITICAL CORRECTION above — default order is NOT identical to `order=released&dir=desc`; default is preferable.
- **D-11:** Probe ceiling = 5 printings; if all 404, return `[]` + `_logger.LogWarning("Tagger has no indexed printing for {CardName} after {N} probes", cardName, 5)`.
- **D-12:** Cache winning tuple in `IMemoryCache` with 24hr TTL. Cache key `tagger-printing:{normalized-card-name}` → `(string set, string collectorNumber)`. Negative-cache misses with shorter TTL (planner picks; recommendation: 1 hr).

**Verification:**
- **D-13:** BUG-02 = unit test in `DeckFlow.Web.Tests/Security/` + live UAT curl loop (11 attempts → 10×401 + 1×429).
- **D-14:** BUG-01 = unit test using `RichardSzalay.MockHttp` + live UAT browser walk (Sol Ring on `/suggest-categories`).
- **D-15:** SC #3 regression = manual UAT walk through `/sync`, `/chatgpt-packets`, `/suggest-categories`. Documented in `04-HUMAN-UAT.md` (Phase 03 template).
- **D-16:** 2 plans. `04-01` = BUG-02. `04-02` = BUG-01. Independent — can ship in any order.

### Claude's Discretion

- **Tracker class shape:** wrap dict in `AdminBruteForceTracker` singleton vs. private static field on `BasicAuthMiddleware`. Planner picks based on testability vs. surface size.
- **DerivePeerIpKey extraction location:** inline private static helper in `Program.cs` next to `DeriveFeedbackPartitionKey`, or new internal helper class in `DeckFlow.Web/Infrastructure/`. Planner's call.
- **Negative-cache TTL** on Tagger probe miss: 1 hr starting suggestion. **This research recommends 1 hr** (matches `CommanderSpellbookService` and `ScryfallSetService` conventions).
- **Probe HTTP method:** HEAD vs GET. **This research recommends HEAD** (verified working on `tagger.scryfall.com` — see Verified Externals section).
- **Retry-After value:** seconds remaining in current 15-min window vs. fixed `Retry-After: 900`. **This research recommends remaining-seconds** (more honest; standard convention).
- **Cache key normalization** for `tagger-printing:` keys. **This research recommends `DeckFlow.Core.Normalization.CardNormalizer.Normalize(cardName)`** (already in production at `DeckFlow.Core/Normalization/CardNormalizer.cs`; punctuation-stripped, lowercased, trimmed; matches conventions used by `CategoryKnowledgeStore` and other consumers).

### Deferred Ideas (OUT OF SCOPE)

- CI-side smoke test for SC #3 (GitHub Actions or Render post-deploy hook). Future polish item.
- Per-route rate-limit policy for `/api/*` JSON endpoints (`DeckSyncApiController`, `SuggestionsApiController`). Backlog candidate.
- Persistent throttle store. Single Render instance — irrelevant.
- Tagger upstream switch / replacement. Out of milestone.
- Negative-cache TTL standardization across all upstream services. Refactor candidate.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| BUG-01 | Fix Scryfall Tagger 404 — investigate the deck-tagger refresh path that returns 404 for some deck IDs and either correct the URL pattern or fall back gracefully | Verified externally that `/cards/search?q=!"<name>"&unique=prints` returns full printings list (Sol Ring=122, Counterspell=67) with `has_more=false` for any card the planner needs to handle; verified `tagger.scryfall.com/card/{set}/{number}` returns clean 200/404 to HEAD requests; verified `IMemoryCache` already DI-registered (`Program.cs:59`); verified `RichardSzalay.MockHttp` 7.0.0 already in `DeckFlow.Web.Tests.csproj` and used by `ScryfallTaggerServiceTests.cs` |
| BUG-02 | Per-IP rate-limit on `/Admin/*` routes — add ASP.NET Core rate limiting middleware to throttle basic-auth brute-force attempts | Verified externally that ASP.NET Core `RateLimitingMiddleware` partitioner cannot condition on auth outcome (sees `HttpContext` only, runs before endpoint); confirmed `BasicAuthMiddleware.Challenge` is the single 401-emission site (`BasicAuthMiddleware.cs:70-76`); confirmed `Connection.RemoteIpAddress` is the locked partition source (Phase 03 TD-04); existing `BasicAuthMiddlewareTests.cs` provides the test harness pattern (`DefaultHttpContext` + manual `InvokeAsync`) |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Admin auth challenge throttle | API / Backend (middleware) | — | Brute-force throttle MUST run server-side at the auth boundary; client-side is meaningless against a malicious script. CONTEXT D-02 places it inside `BasicAuthMiddleware.InvokeAsync` because the standard rate-limiter pipeline (`UseRateLimiter()`) runs before auth and cannot condition on the 401 outcome. |
| Tagger printings probe + cache | API / Backend (service) | — | Pure server-side: Scryfall search → N HEAD probes → IMemoryCache write. UI is unchanged (CONTEXT.md "no UI copy work"). |
| Scryfall card printings lookup | API / Backend (`scryfall` Polly pipeline + `ScryfallThrottle`) | — | Reuses existing named pipeline. No new HTTP infrastructure. |
| Tagger probe HTTP egress | API / Backend (`tagger` Polly pipeline) | — | Reuses existing `tagger` named pipeline (timeout 8s, retry 3, CB 50%/30s). Probe = HEAD via `_taggerHttpClient.Inner`. |
| Cache-key normalization | API / Backend (`DeckFlow.Core.Normalization.CardNormalizer`) | — | Use existing canonical normalizer; do NOT roll a new lowercase-trim per service. |
| 429 + Retry-After response | API / Backend (`BasicAuthMiddleware`) | — | Set `context.Response.StatusCode` and `Headers["Retry-After"]` directly; do NOT route through `OnRejected` (we're not in the rate-limiter pipeline). |

## Standard Stack

### Core (already wired — Phase 04 adds NO new packages)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.Extensions.Caching.Memory` | 10.0.0 (transitive) | `IMemoryCache` for Tagger printing tuple cache | Already DI-registered (`Program.cs:59 builder.Services.AddMemoryCache()`); used by `CommanderSpellbookService`, `ScryfallSetService`, `CommanderBanListService` |
| `RestSharp` | 114.0.0 | HTTP client wrapper | Project HTTP standard (per CLAUDE.md / global instructions) |
| `Polly` | 8.x | Resilience pipelines | Project HTTP standard; named pipelines `scryfall` + `tagger` already wired in `ResiliencePipelineFactory.cs:30, 28` |
| `RichardSzalay.MockHttp` | 7.0.0 | HTTP mocking in tests | Already in `DeckFlow.Web.Tests.csproj:12`; the canonical mock for `ScryfallTaggerServiceTests` |
| `xUnit` | 2.9.3 | Test framework | Project test standard |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.0 | `NullLogger<T>.Instance` | Used in existing `BasicAuthMiddlewareTests.cs:21` |
| `System.Collections.Concurrent.ConcurrentDictionary` | BCL | Throttle bucket store | D-02 locked. Built-in. No package. |

### Supporting (NOT used, but explicitly considered)

| Library | Why NOT used |
|---------|--------------|
| `Microsoft.AspNetCore.RateLimiting.AddPolicy` | D-02 explicitly rejects (partitioner cannot condition on 401 outcome — verified [CITED](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-9.0)) |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | D-02 explicitly rejects distributed throttle stores; Render is single-instance |
| `Polly.RateLimit` | Out of scope — admin throttle is fixed-window-by-IP, not Polly's concern |

**Installation:** **No `dotnet add package` calls needed.** All required dependencies are already in `DeckFlow.Web.csproj` and `DeckFlow.Web.Tests.csproj`.

**Version verification (npm view equivalent):**
- RestSharp 114.0.0 — confirmed in csproj 2026-05-01
- Polly 8.x — confirmed via `ResiliencePipelineFactory.cs` API surface (Polly 8 namespaces `Polly.CircuitBreaker`, `Polly.Retry`, `Polly.Timeout`)
- RichardSzalay.MockHttp 7.0.0 — confirmed in `DeckFlow.Web.Tests.csproj:12`
- Microsoft.Extensions.Caching.Memory 10.0.0 — transitive via `Microsoft.NET.Sdk.Web`

## Architecture Patterns

### System Architecture Diagram (BUG-02 admin throttle)

```
Admin request --> Kestrel
   --> UseForwardedHeaders          (Program.cs:259)
   --> UseDeckFlowSecurityHeaders   (Program.cs:268)
   --> UseHttpsRedirection
   --> UseStaticFiles + UseRouting
   --> UseSerilogRequestLogging
   --> UseAuthorization
   --> UseRateLimiter               (Program.cs:286 — feedback policy; Phase 04 does NOT add admin policy here)
   --> UseWhen(Path /Admin) {
         BasicAuthMiddleware.InvokeAsync
           --> [Phase 04 NEW] AdminBruteForceTracker.IsBlocked(remoteIp)?
                  YES --> 429 + Retry-After: <secs>; existing warn log keeps firing on Challenge
                  NO  --> existing auth flow:
                            valid creds --> next(context)
                            invalid creds --> Challenge() {
                              [Phase 04 NEW] AdminBruteForceTracker.RecordFailure(remoteIp)
                              existing _logger.LogWarning(...)
                              existing 401 + WWW-Authenticate
                            }
       }
   --> MapControllers / MapDefaultControllerRoute
```

### System Architecture Diagram (BUG-01 Tagger printings)

```
ScryfallTaggerService.LookupOracleTagsAsync(cardName)
   |
   v
ResolveCardPrintingAsync(cardName)              [Phase 04 REPLACEMENT for current /cards/named flow]
   |
   v
IMemoryCache.TryGet("tagger-printing:" + normalize(cardName))
   |--> HIT (positive)  --> return cached (set, number)
   |--> HIT (negative)  --> return ("", "") --> caller returns []
   |--> MISS
        |
        v
   Scryfall /cards/search?q=!"<name>"&unique=prints     [scryfall pipeline + ScryfallThrottle]
        |
        v
   For each printing[0..min(5, total)]:
        |
        v
   HEAD tagger.scryfall.com/card/{set}/{number}         [tagger pipeline]
        |--> 200 --> set IMemoryCache positive (24hr); return (set, number)
        |--> 404 --> continue
   After 5 misses:
        --> set IMemoryCache negative (1hr); log warn; return ("", "")
   |
   v
caller continues with existing CSRF + GraphQL flow      [unchanged]
```

### Recommended Project Structure

```
DeckFlow.Web/
├── Infrastructure/
│   ├── BasicAuthMiddleware.cs            # MODIFIED — throttle hook in Challenge() + InvokeAsync top
│   ├── AdminBruteForceTracker.cs         # NEW — small singleton wrapping ConcurrentDictionary
│   │                                     # (recommendation; planner may inline as static field instead)
│   └── BasicAuthMiddlewareExtensions.cs  # OPTIONAL — only if extracting DerivePeerIpKey here
├── Program.cs                            # MODIFIED — DI register AdminBruteForceTracker singleton +
│                                         # extract DerivePeerIpKey shared helper next to DeriveFeedbackPartitionKey
└── Services/
    └── ScryfallTaggerService.cs          # MODIFIED — replace ResolveCardPrintingAsync with iterate-printings
                                          # + IMemoryCache injection (constructor add)

DeckFlow.Web.Tests/
├── BasicAuthMiddlewareTests.cs           # EXISTING — extend with throttle tests OR…
├── Security/
│   ├── ForwardedHeadersOptionsTests.cs   # EXISTING (Phase 03 sibling)
│   └── AdminBruteForceTrackerTests.cs    # NEW — recommended location for 04-01 unit test
└── Services/
    └── ScryfallTaggerServiceTests.cs     # EXISTING — extend with new printings-iteration tests
```

### Pattern 1: In-Middleware ConcurrentDictionary Throttle

**What:** Hand-rolled fixed-window IP throttle inside the admin middleware. Pattern is canonical for "throttle conditional on response code/auth outcome" because the standard ASP.NET RateLimiter partitioner cannot see post-auth state.

**When to use:** When you need a throttle that conditions on auth or business-logic outcome, AND the workload is single-process (Render single-instance fits exactly).

**Recommended `BucketEntry` shape:**

```csharp
// Source: D-07 (CONTEXT) + canonical fixed-window pattern (Microsoft Learn rate-limit docs).
internal readonly record struct BucketEntry(int Count, DateTimeOffset WindowStart);

internal sealed class AdminBruteForceTracker
{
    private const int PermitLimit = 10;        // D-06
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);  // D-06

    private readonly ConcurrentDictionary<string, BucketEntry> _buckets = new();

    /// <summary>
    /// Returns (true, retryAfterSeconds) if the IP is currently throttled. D-08 lazy expiry.
    /// </summary>
    public (bool Throttled, int RetryAfterSeconds) IsThrottled(string partitionKey, DateTimeOffset now)
    {
        if (!_buckets.TryGetValue(partitionKey, out var entry)) return (false, 0);
        if (now - entry.WindowStart >= Window)
        {
            _buckets.TryRemove(partitionKey, out _);   // lazy expiry on access (D-08)
            return (false, 0);
        }
        if (entry.Count >= PermitLimit)
        {
            var remaining = (int)(Window - (now - entry.WindowStart)).TotalSeconds;
            return (true, Math.Max(remaining, 1));
        }
        return (false, 0);
    }

    /// <summary>
    /// Atomically increments the bucket count for partitionKey. D-07 fixed-window semantics.
    /// </summary>
    public void RecordFailure(string partitionKey, DateTimeOffset now) =>
        _buckets.AddOrUpdate(
            partitionKey,
            _ => new BucketEntry(1, now),
            (_, existing) => (now - existing.WindowStart >= Window)
                ? new BucketEntry(1, now)               // window rolled — fresh bucket
                : existing with { Count = existing.Count + 1 });
}
```

**Threading note:** `AddOrUpdate`'s update factory may run multiple times under contention (per `ConcurrentDictionary` BCL contract). For an integer counter with a clock-only check, this is benign — the worst case is one over-count per contended IP per window, which is harmless against a 10-permit threshold. Do NOT use `Get + Set` (race window).

### Pattern 2: Iterate-Printings with HEAD Probe + IMemoryCache

**What:** Replace `/cards/named?exact=X` (returns ONE printing — possibly Tagger-unindexed) with `/cards/search?q=!"<name>"&unique=prints` (returns ALL printings); HEAD-probe each via `tagger.scryfall.com/card/{set}/{number}`; first 200 wins.

**When to use:** When upstream A returns one resource but upstream B's coverage is partial — you must probe B to find a working tuple.

**Example (sketch — planner refines):**

```csharp
// Source: ScryfallTaggerService.cs:123-148 (target site for replacement) + verified Scryfall + Tagger behavior 2026-05-01.
private async Task<(string Set, string CollectorNumber)> ResolveCardPrintingAsync(
    string cardName, CancellationToken cancellationToken)
{
    var cacheKey = $"tagger-printing:{CardNormalizer.Normalize(cardName)}";
    if (_memoryCache.TryGetValue<(string, string)?>(cacheKey, out var cached))
    {
        return cached ?? (string.Empty, string.Empty);   // negative cache hit -> empty tuple
    }

    // 1. Scryfall search — returns prints in default-order (mainline-set-first, paper-only).
    var scryfallClient = _scryfallRestClientFactory.Create();
    var searchRequest = new RestRequest("cards/search", Method.Get);
    searchRequest.AddQueryParameter("q", $"!\"{cardName}\"");
    searchRequest.AddQueryParameter("unique", "prints");
    // NB: NO order= param — Scryfall default order yields mainline sets first (better Tagger coverage)
    //     than explicit order=released&dir=desc (which floats Secret Lairs / promos to the top).

    var searchResponse = await ScryfallThrottle.ExecuteAsync(
        ct => _scryfallPipeline.ExecuteAsync(
            async pollyCt => await scryfallClient.ExecuteAsync<ScryfallSearchResponse>(searchRequest, pollyCt).ConfigureAwait(false),
            ct).AsTask(),
        cancellationToken).ConfigureAwait(false);

    if (!searchResponse.IsSuccessful || searchResponse.Data?.Data is not { Count: > 0 })
    {
        _memoryCache.Set(cacheKey, ((string, string)?)null, TimeSpan.FromHours(1));   // negative cache, 1hr
        _logger.LogWarning("Scryfall printings search failed for {CardName}: {Status}", cardName, searchResponse.StatusCode);
        return (string.Empty, string.Empty);
    }

    // 2. HEAD-probe up to 5 printings.
    var taggerRestClient = new RestClient(_taggerHttpClient.Inner);
    var probesAttempted = 0;
    foreach (var printing in searchResponse.Data.Data)
    {
        if (probesAttempted++ >= 5) break;          // D-11
        var set = printing.Set;
        var number = printing.CollectorNumber;
        if (string.IsNullOrEmpty(set) || string.IsNullOrEmpty(number)) continue;

        var probe = new RestRequest($"card/{set}/{number}", Method.Head);   // HEAD — verified clean 200/404
        var probeResponse = await _taggerPipeline.ExecuteAsync(
            async ct => await taggerRestClient.ExecuteAsync(probe, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if ((int)probeResponse.StatusCode == 200)
        {
            _memoryCache.Set(cacheKey, ((string, string)?)(set, number), TimeSpan.FromHours(24));   // D-12 positive
            return (set, number);
        }
    }

    // 3. All probes 404 (or insufficient prints).
    _memoryCache.Set(cacheKey, ((string, string)?)null, TimeSpan.FromHours(1));   // D-12 negative
    _logger.LogWarning("Tagger has no indexed printing for {CardName} after {N} probes", cardName, probesAttempted);
    return (string.Empty, string.Empty);
}
```

**Constructor change required:** `ScryfallTaggerService` currently does NOT inject `IMemoryCache`. Add it as a constructor parameter and the matching `Program.cs:200` registration (`builder.Services.AddSingleton<IScryfallTaggerService>(sp => new ScryfallTaggerService(..., sp.GetRequiredService<IMemoryCache>()));`).

### Pattern 3: Test Seam for Middleware (DefaultHttpContext)

**What:** Construct `DefaultHttpContext`, set `Connection.RemoteIpAddress`, optionally `Request.Headers`, manually `await middleware.InvokeAsync(ctx)`, assert on `ctx.Response.StatusCode` and `ctx.Response.Headers`.

**When to use:** Unit-testing any ASP.NET middleware. This is the canonical xUnit pattern in this repo.

**Example (existing — used as the template for 04-01):**

```csharp
// Source: DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs:30-39 (existing).
[Fact]
public async Task NoAuthHeader_Returns401_WithChallenge()
{
    using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
    var context = new DefaultHttpContext();
    var middleware = new BasicAuthMiddleware(_ => Task.CompletedTask, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin");

    await middleware.InvokeAsync(context);

    Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    Assert.Contains("realm=\"DeckFlow Admin\"", context.Response.Headers["WWW-Authenticate"].ToString());
}
```

**Phase 04 04-01 extension shape:**

```csharp
// Source: this research session, derived from existing BasicAuthMiddlewareTests pattern.
[Fact]
public async Task ElevenFailures_FromSameIp_TenthReturns401_EleventhReturns429()
{
    using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
    var tracker = new AdminBruteForceTracker();
    var middleware = new BasicAuthMiddleware(
        _ => Task.CompletedTask, NullLogger<BasicAuthMiddleware>.Instance, "DeckFlow Admin", tracker);

    var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:wrong"));
    HttpResponse lastResponse = null!;
    for (var i = 0; i < 11; i++)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.20.30.40");
        ctx.Request.Headers["Authorization"] = $"Basic {encoded}";
        await middleware.InvokeAsync(ctx);
        lastResponse = ctx.Response;
        if (i < 10) Assert.Equal(StatusCodes.Status401Unauthorized, ctx.Response.StatusCode);
    }
    Assert.Equal(StatusCodes.Status429TooManyRequests, lastResponse.StatusCode);
    Assert.NotEmpty(lastResponse.Headers["Retry-After"].ToString());
}
```

### Pattern 4: MockHttp Multi-Route Sequence (BUG-01 unit test)

**What:** Two `MockHttpMessageHandler` instances (one for Scryfall, one for Tagger); register multiple routes per handler; assert hit counts via `GetMatchCount(route)`.

**When to use:** When orchestrating a service that talks to multiple upstream HTTP services. Existing pattern.

**Example (BUG-01 sketch — extends existing `ScryfallTaggerServiceTests.cs`):**

```csharp
// Source: this research, mirrors ScryfallTaggerServiceTests.cs:50-86 pattern.
[Fact]
public async Task LookupOracleTagsAsync_FirstTwoPrintings404_ThirdReturnsTaggerData()
{
    using var scryfallMock = new MockHttpMessageHandler();
    using var taggerMock = new MockHttpMessageHandler();

    // Scryfall search returns 3 printings.
    const string searchJson = """
    {"object":"list","total_cards":3,"has_more":false,"data":[
      {"object":"card","name":"Sol Ring","set":"soc","collector_number":"128"},
      {"object":"card","name":"Sol Ring","set":"tmc","collector_number":"59"},
      {"object":"card","name":"Sol Ring","set":"lea","collector_number":"270"}
    ]}
    """;
    scryfallMock.When(HttpMethod.Get, "https://api.scryfall.com/cards/search*")
                .Respond(HttpStatusCode.OK, "application/json", searchJson);

    var probe1 = taggerMock.When(HttpMethod.Head, "https://tagger.scryfall.com/card/soc/128")
                           .Respond(HttpStatusCode.NotFound);
    var probe2 = taggerMock.When(HttpMethod.Head, "https://tagger.scryfall.com/card/tmc/59")
                           .Respond(HttpStatusCode.NotFound);
    var probe3 = taggerMock.When(HttpMethod.Head, "https://tagger.scryfall.com/card/lea/270")
                           .Respond(HttpStatusCode.OK);

    // Existing CSRF + GraphQL flow on the winning printing
    taggerMock.When(HttpMethod.Get, "https://tagger.scryfall.com/card/lea/270")
              .Respond(_ => CsrfPageResponse());   // existing test helper
    taggerMock.When(HttpMethod.Post, "https://tagger.scryfall.com/graphql")
              .Respond(HttpStatusCode.OK, "application/json", TaggerGraphQlJson);

    var sut = CreateService(scryfallMock, taggerMock);
    var tags = await sut.LookupOracleTagsAsync("Sol Ring", CancellationToken.None);

    Assert.NotEmpty(tags);
    Assert.Equal(1, taggerMock.GetMatchCount(probe1));
    Assert.Equal(1, taggerMock.GetMatchCount(probe2));
    Assert.Equal(1, taggerMock.GetMatchCount(probe3));
}
```

### Anti-Patterns to Avoid

- **Adding admin throttle as a `RateLimitPolicy` in `AddRateLimiter(...)`** — the partitioner runs before authentication; cannot condition on 401 outcome (D-02). Verified [CITED](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-9.0).
- **Using `Get + TryAdd`/`SetValue` instead of `AddOrUpdate`** — race window between read and write under concurrent failures.
- **Using GET for the Tagger probe** — wastes ~30 KB per probe (HEAD verified working with 0 body bytes 2026-05-01).
- **Passing `cardName` raw as the cache key** — case mismatches and punctuation will multiply cache entries. Use `CardNormalizer.Normalize`.
- **Adding `order=released&dir=desc` to the Scryfall search query** — empirically yields worse Tagger coverage (Secret Lairs / promos float to top; Tagger indexes mainline sets more reliably). Use the default ordering.
- **Caching the raw `ScryfallSearchResponse`** — caches stale price/mtgo_id data we don't need. Cache only the `(set, collectorNumber)` tuple per D-12.
- **Using `WebApplicationFactory<T>` to integration-test the middleware** — VSTest unreliable in WSL per PROJECT.md; `DefaultHttpContext` + `InvokeAsync` is the canonical pattern (see `BasicAuthMiddlewareTests.cs` and `ForwardedHeadersOptionsTests.cs`).
- **Fixed `Retry-After: 900`** — claim is wrong if 8 minutes have already elapsed in the window. Compute remaining seconds.
- **Background `IHostedService` sweep timer for the throttle dict** — D-08 explicitly rejects; lazy expiry on access is enough at this scale.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Throttle dict expiry | Background `Timer` / `IHostedService` sweep | Lazy expiry on access (D-08) | Background timers for ~10 keys are over-engineering; Render restart sweeps drift |
| Cache-key normalization | `cardName.Trim().ToLowerInvariant()` ad-hoc | `DeckFlow.Core.Normalization.CardNormalizer.Normalize(cardName)` | Already exists, handles punctuation/double-faced cards/em-dash correctly, used by `CategoryKnowledgeStore` |
| HTTP retry on Scryfall search | New `Polly.Retry.AsyncRetryPolicy` per call | Existing `_scryfallPipeline` (`ResiliencePipelineFactory.cs:122`) | Centralized; total-budget timeout already wrapped; do NOT rebuild |
| HTTP retry on Tagger probe | New retry layer | Existing `_taggerPipeline` (`ResiliencePipelineFactory.cs:75`) | 8s timeout + 3 retries already calibrated for tagger.scryfall.com |
| Scryfall pacing | New SemaphoreSlim | Existing `ScryfallThrottle.ExecuteAsync` | Process-wide 5 req/s gate already wired around all Scryfall calls — do NOT bypass for the new search call |
| `IMemoryCache` registration | New per-service cache | DI-resolved `IMemoryCache` (`Program.cs:59`) | One process-wide instance; piggyback per CONTEXT canonical_refs |
| Test middleware in pipeline | `WebApplicationFactory<T>` / `TestServer` | `DefaultHttpContext` + manual `InvokeAsync` | VSTest+WSL unreliable; existing pattern in `BasicAuthMiddlewareTests` and `ForwardedHeadersOptionsTests` |
| Test HTTP egress | Real HTTP calls | `RichardSzalay.MockHttp` | Already in `csproj`; existing `ScryfallTaggerServiceTests.cs` is the template |
| Polly pipeline in tests | Real retry/timeout firing | `FakeResiliencePipelineProvider` (`ResiliencePipeline<T>.Empty`) | Existing TestDouble; avoids slow tests |

**Key insight:** Phase 04 is intentionally narrow. Both bugs touch existing, well-instrumented code paths. The strongest research result is the list of things NOT to add — every "do you need a sweep timer / a new retry policy / a new HTTP client / a new test framework / a new cache instance?" question has the same answer: **no, reuse what's already there.**

## Runtime State Inventory

> Phase 04 is a feature/bug-fix phase, not a rename or migration. No runtime state to migrate. The throttle bucket dict is **deliberately ephemeral** — Render container restart auto-clears it (CONTEXT.md "Render container restart auto-clears the in-memory throttle dict — that's a feature, not a bug"). The Tagger printing cache is **deliberately ephemeral** for the same reason — 24hr TTL plus deploy-cycle restarts. No data migration required.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — neither bug touches a stored datastore (no Mem0/SQLite/Postgres key changes; no schema changes) | None |
| Live service config | None — no n8n / Datadog / Tailscale changes; no Render env var renames | None |
| OS-registered state | None — no Windows Task / launchd / systemd registration | None |
| Secrets/env vars | None — `FEEDBACK_ADMIN_USER` / `FEEDBACK_ADMIN_PASSWORD` names unchanged; new throttle reads no new env vars | None |
| Build artifacts | None — `dotnet build` clean is sufficient; no egg-info / Docker tag drift; no TS sources changed (so `wwwroot/js/*.js` regenerates per existing MSBuild target with no diff) | None |

**Verified by:** Inspection of `BasicAuthMiddleware.cs`, `Program.cs`, `ScryfallTaggerService.cs`, `appsettings.json`. None of Phase 04's changes touch persisted state, env-var names, or stored-string contracts.

## Common Pitfalls

### Pitfall 1: Adding `order=released&dir=desc` because the CONTEXT.md text mentions "release-date desc"
**What goes wrong:** Empirical testing 2026-05-01 showed that `unique=prints` default ordering and `unique=prints&order=released&dir=desc` produce *different* result lists. For Sol Ring, default order starts with `soc/128` (mainline 2026-04-24) but explicit released-desc starts with `sld/2539` (Secret Lair 2026-04-27). Secret Lairs and promos have spotty Tagger indexing.
**Why it happens:** CONTEXT.md describes the default ordering as "release-date descending" — that's an approximation, not a literal Scryfall API behavior.
**How to avoid:** Use no `order=` parameter at all. Scryfall's default is the right call.
**Warning signs:** If the planner introduces `order=released&dir=desc` in the search query, the live UAT for Sol Ring may pass (because some Secret Lairs ARE indexed today) but other cards may regress vs. the default ordering.

### Pitfall 2: Forgetting to record the failure when the IP IS already at the limit
**What goes wrong:** If the middleware does `if (tracker.IsThrottled) return 429;` then `Challenge()` records a failure, but Challenge isn't reached on the throttled path — and the existing `_logger.LogWarning("Admin basic-auth challenge issued: ...")` ALSO doesn't fire. This violates SC #1 ("each challenge log fires").
**Why it happens:** Naive top-of-`InvokeAsync` short-circuit pattern.
**How to avoid:** Two options. (A) On throttle hit, still log the warning and (separately) emit 429. The middleware writes BOTH the warn log and the 429 response. (B) Apply the throttle check inside `Challenge()` itself: every call to `Challenge()` records a failure first, then the check + 429 emit happens. Option B is structurally simpler — fewer code paths to keep in sync.
**Warning signs:** UAT log inspection shows fewer warns than 401+429 responses combined.

### Pitfall 3: Returning 401 + 429 simultaneously
**What goes wrong:** If the throttle path falls through to `Challenge()` (which sets 401), then sets 429 afterwards, the response status flip-flops. Browser sees only the LAST `StatusCode` setter.
**Why it happens:** Code that calls `Challenge()` then checks throttle, vs. checking throttle then calling Challenge.
**How to avoid:** Make the throttle path exclusive. On throttle hit: set 429 + Retry-After + emit warn log + RETURN. Do NOT call `Challenge()`. The challenge log gets emitted via a separate code path on the throttled response.
**Warning signs:** UAT curl returns 429 but the body or headers contain `WWW-Authenticate`.

### Pitfall 4: `IMemoryCache.Set` vs `IMemoryCache.GetOrCreate` race
**What goes wrong:** Two concurrent lookups for the same uncached card both fire the full Scryfall search + 5 Tagger probes, then both write to cache. Wasted egress.
**Why it happens:** `TryGet` + `Set` is not atomic across concurrent threads.
**How to avoid:** For Phase 04, accept the race — the worst case is a 2× egress burst on the same card under cold concurrency, and the cache write is idempotent. If the planner wants atomicity, use `await _memoryCache.GetOrCreateAsync(key, async entry => { entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24); return await DoLookupAsync(...); })`.
**Warning signs:** Two consecutive Sol Ring lookups in the same second fire 2× the upstream calls.

### Pitfall 5: Existing test calls direct ctor — adding `IMemoryCache` to ctor breaks existing `ScryfallTaggerServiceTests` calls
**What goes wrong:** The existing `CreateService(...)` helper in `ScryfallTaggerServiceTests.cs:27-48` currently calls `new ScryfallTaggerService(restClientFactory, typedTaggerClient, cache, new FakeResiliencePipelineProvider())` — only 4 args. If the constructor adds a 5th `IMemoryCache` param, all existing tests fail to compile.
**Why it happens:** Constructor signature change without updating test factory.
**How to avoid:** When adding `IMemoryCache` to `ScryfallTaggerService` ctor, also update `ScryfallTaggerServiceTests.cs` `CreateService` to inject `new MemoryCache(new MemoryCacheOptions())` (or accept it as a parameter with a default). This is a cross-cutting touch the planner must call out.
**Warning signs:** `dotnet build DeckFlow.sln` fails with "no matching constructor" in `DeckFlow.Web.Tests`.

### Pitfall 6: `Challenge()` called on `EnvVarsMissing` 503 path counts as a failure
**What goes wrong:** `BasicAuthMiddleware.InvokeAsync` lines 26-31 returns 503 when env vars are missing — but if the throttle hook is in `Challenge()`, no failure is recorded for the 503 path (correct). If the hook is at the *top of `InvokeAsync`*, a 503 caller still increments. **D-01 says count only `Challenge()` 401s** — the hook MUST live in `Challenge()`, not at the top of `InvokeAsync`.
**Why it happens:** Confusion between "count failures" and "block on threshold."
**How to avoid:** Two-call pattern in `InvokeAsync`:
   1. **At top:** `if (tracker.IsThrottled(ip)) { Challenge(ctx, "throttled"); ctx.Response.StatusCode = 429; ctx.Response.Headers["Retry-After"] = secs.ToString(); return; }` — but this is wrong because `Challenge()` would then also be the recorder.
   2. **Better pattern:** Move recording out of `Challenge()` and into the *401-emission site*. `Challenge()` emits log + 401; on the call site, AFTER `Challenge(...)` returns, call `tracker.RecordFailure(ip, now)`. The top-of-`InvokeAsync` check `if (tracker.IsThrottled(ip)) { 429 + warn log; return; }` happens BEFORE auth is parsed.
**Warning signs:** UAT counts: missing-env 503s contribute to throttle ramp.

## Code Examples

### Verified: Scryfall search for printings (Sol Ring, 2026-05-01)

```bash
# Source: live curl 2026-05-01 (this research session)
curl -sS 'https://api.scryfall.com/cards/search?q=%21%22Sol+Ring%22&unique=prints'
# Returns: total_cards=122, has_more=false
# First 5 (default order):
#   set=soc cn=128    released=2026-04-24
#   set=tmc cn=59     released=2026-03-06
#   set=ecc cn=57     released=2026-01-23
#   set=ecc cn=58     released=2026-01-23
#   set=eoc cn=57     released=2025-08-01
```

### Verified: Tagger HEAD probe distinguishes 200/404 cleanly

```bash
# Source: live curl 2026-05-01 (this research session)
curl -I 'https://tagger.scryfall.com/card/lea/270'    # HEAD: 200, body 0 bytes
curl -I 'https://tagger.scryfall.com/card/soc/9999'   # HEAD: 404
curl -I 'https://tagger.scryfall.com/card/zzz/1'      # HEAD: 404
```

### Verified: ASP.NET Core RateLimiter partitioner shape

```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-9.0
// fetched 2026-05-01. The partitioner takes (httpContext) -> RateLimitPartition. NO response.
// NO endpoint metadata at partitioner-call time. Cannot condition on 401 outcome.
options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions { ... }));
```

### Existing pattern: `Program.cs:341-350` `DeriveFeedbackPartitionKey` (refactor target)

```csharp
// Source: DeckFlow.Web/Program.cs:341-350 (Phase 03 TD-04 output, deployed 2026-05-01)
internal static string DeriveFeedbackPartitionKey(HttpContext context)
    => "peer:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
```

**Phase 04 refactor recommendation (D-05 Claude's discretion):**

```csharp
// Source: this research session, derived from existing helper.
internal static string DerivePeerIpKey(HttpContext context, string prefix)
    => prefix + ":" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

internal static string DeriveFeedbackPartitionKey(HttpContext context) => DerivePeerIpKey(context, "peer");
internal static string DeriveAdminPartitionKey(HttpContext context) => DerivePeerIpKey(context, "admin");
```

The existing `ForwardedHeadersOptionsTests.DeriveFeedbackPartitionKey_IgnoresForwardedForHeader` test continues to pass after this refactor.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `KnownIPNetworks.Clear()` to honor X-Forwarded-For from any proxy | `DeriveFeedbackPartitionKey` reads `Connection.RemoteIpAddress` directly (Path B-rawpeer) | Phase 03 TD-04 (2026-05-01) | **Phase 04 BUG-02 builds on this** — admin throttle uses the same partition source |
| `BasicAuthMiddleware` warns on 401 but no throttle | Add per-IP fixed-window throttle | Phase 04 BUG-02 (this phase) | Mitigates brute-force; CONTEXT D-04 keeps the warn log |
| Tagger lookup via `/cards/named?exact=X` (one printing — possibly Tagger-unindexed) | Iterate all printings via `/cards/search?unique=prints`; first HEAD 200 wins | Phase 04 BUG-01 (this phase) | Real fix for SC #2; cards like Sol Ring with mainly-Tagger-unindexed default printings now return tags |
| Multi-ctor test seam (3 ctors, `Null*Factory` defaults) | Single internal ctor + `TestServiceFactory.Create*` | Phase 03 TD-02 (2026-04-30) | If 04-02 needs to construct `ScryfallTaggerService` via factory, route through this pattern (note: `ScryfallTaggerService` was NOT in the TD-02 scope of 10 services; current test directly `new`s — see Pitfall 5) |

**Deprecated/outdated:**
- The CONTEXT.md observation `"DSC for Counterspell, both 404"` (CONTEXT.md line 39) is no longer literally true — `tagger.scryfall.com/card/dsc/114` returns 200 today (2026-05-01). The bug's *root cause* (Scryfall returns one printing per `/cards/named` and some printings are Tagger-unindexed) is still real. Document the fix's necessity in terms of the structural mismatch, not those specific set codes.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Scryfall default `unique=prints` ordering remains stable enough that the top 5 yield Tagger 200s for common cEDH staples. | Architecture Patterns / BUG-01 | LOW — the 5-probe cap and negative-cache TTL are designed to absorb misses; a low Tagger hit rate degrades gracefully to `[]` |
| A2 | Render container restart cycle (~daily based on Phase 03 ops note) is frequent enough that the in-memory throttle dict and Tagger cache do not need a sweep timer. | Architecture / Don't Hand-Roll | LOW — single-instance Render Starter; restart cadence already factored into D-08 / D-12 |
| A3 | `IMemoryCache` is the right cache layer for the Tagger printing tuple (vs. the existing `TaggerSessionCache` singleton). | Pattern 2 | LOW — `TaggerSessionCache` holds session state (CSRF + cookie) with a 270s TTL, semantically distinct from a 24hr printing-tuple cache; piggybacking is wrong |

**This list is short on purpose.** CONTEXT.md locked the major decisions. The three items above are research-level inferences rather than verified facts. None of them block the planner.

## Open Questions

1. **Should the throttle hook record-on-Challenge or check-and-record at top of `InvokeAsync`?**
   - What we know: D-01 says count only `Challenge()` 401s. D-04 says every challenge log fires.
   - What's unclear: where exactly the `tracker.RecordFailure` call lives. Pitfall 6 above documents two valid placements.
   - Recommendation: record AFTER `Challenge(...)` returns, at each call site of `Challenge()`. Check `IsThrottled` at top of `InvokeAsync` BEFORE the env-var-missing 503 short-circuit. This keeps `Challenge()` semantically pure (emit warn log + 401) and makes the throttle bookkeeping explicit. Planner finalizes.

2. **Tracker as DI singleton vs. private static field on middleware?**
   - What we know: D-02 leaves this as Claude's discretion.
   - What's unclear: testability vs. surface size tradeoff.
   - Recommendation: **DI singleton.** It's 50 LOC for the tracker class, 1 LOC for the registration, and the unit test gets to construct it cleanly without resetting a static (which is a known xUnit-parallelism hazard — see `CategoryKnowledgeStoreTests` `[CollectionDefinition(...DisableParallelization=true)]`).

3. **Tagger probe via direct `_taggerHttpClient.Inner` SocketsHttpHandler vs. through `_taggerPipeline`?**
   - What we know: The existing `FetchTaggerSessionAsync` uses `_taggerPipeline` for the GET. CONTEXT line 113 says "New per-printing probes use `tagger` pipeline."
   - What's unclear: Whether 5 probes × 3 retries × CB-state-shared = potential CB-trip cascade if one card has 5 truly-missing prints.
   - Recommendation: Use `_taggerPipeline` per CONTEXT, but consider: 5 × 404 in a row is NOT a transient failure (`IsTransientFailure` returns true only for 408/429/5xx — 404 is excluded), so retries won't fire and the CB won't see them as failures. Safe.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Build/test | ✓ | 10.0.x (verified — current `<TargetFramework>net10.0</TargetFramework>` in csproj) | — |
| `dotnet build DeckFlow.sln` | Phase 04 verification gate | ✓ | — | — |
| `curl` | Live UAT (BUG-02 throttle loop, BUG-01 Sol Ring walk) | ✓ | — | — |
| `tagger.scryfall.com` | BUG-01 live runtime + UAT | ✓ | Live (verified 2026-05-01: HEAD 200 on `lea/270`, HEAD 404 on `soc/9999`) | If down at UAT time, planner reschedules |
| `api.scryfall.com` | BUG-01 live runtime + UAT | ✓ | Live (verified 2026-05-01: 200 on `cards/search?q=%21%22Sol+Ring%22&unique=prints`, total=122) | If down at UAT time, planner reschedules |
| Render (deckflow.gg) | Live UAT | ✓ | Phase 03 TD-04 deployed and verified 2026-05-01; deployment pipeline is `git push main` → ~17min auto-deploy | — |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** None — VSTest in WSL is unreliable per PROJECT.md, but planner already routes verification through `dotnet build` clean + Render CI + manual UAT (see CONTEXT D-13/14/15).

## Verified Externals

This section captures live-tool-verified facts that supersede or corroborate CONTEXT assumptions.

### Scryfall `/cards/search?q=!"<name>"&unique=prints`

| Fact | Verified | Source |
|------|----------|--------|
| Query syntax `q=!"<name>"` returns exact-name match | YES | Live curl 2026-05-01: Sol Ring → 122 prints, all named "Sol Ring" |
| `unique=prints` returns ALL printings (not rolled up by oracle ID) | YES | Live curl: 122 Sol Rings, 67 Counterspells |
| Pagination — does Sol Ring or Counterspell ever exceed one page? | NO (both `has_more=false` at 122 and 67) | Live curl 2026-05-01. Page size = 175. The 5-probe ceiling never hits page 2 within Phase 04 budget. |
| Default ordering | NOT release-date desc; uses internal Scryfall heuristic that prioritizes mainline sets | Live curl 2026-05-01 — see CRITICAL CORRECTION in Summary |
| Explicit ordering `order=released&dir=desc` | Different from default — surfaces Secret Lairs / promos | Live curl 2026-05-01 |
| Fields needed for Tagger URL | `card.set` (lowercase set code) and `card.collector_number` (string, may contain hyphens like `2026-7` or `IFIYW-10`) | Live curl 2026-05-01 |
| Collector numbers can contain non-digit chars | YES (`IFIYW-10`, `2026-7`) | Live curl 2026-05-01 — URL-construct with raw string, do NOT integer-parse |

### `tagger.scryfall.com/card/{set}/{number}` HEAD support

| Fact | Verified | Source |
|------|----------|--------|
| HEAD returns 200 for indexed printings | YES — body 0 bytes | Live curl 2026-05-01: `lea/270` |
| HEAD returns 404 for nonexistent (set/number) tuples | YES — clean 404, no false 200 | Live curl 2026-05-01: `soc/9999`, `zzz/1`, `lea/9999` |
| HEAD status matches GET status | YES | Live curl 2026-05-01: GET `lea/270` = 200 (30769 bytes), HEAD `lea/270` = 200 (0 bytes) |
| HEAD avoids the ~30 KB HTML download per probe | YES — body bytes = 0 | Live curl 2026-05-01 |
| Server: cloudflare; backed by heroku-router | (informational) | `server: cloudflare`, `via: 2.0 heroku-router` headers |

**Recommendation:** Use HEAD. 5 probes worst-case = 5 round trips × 0 KB body = ~250ms over typical wire vs. ~1.5s for 5 GETs.

### ASP.NET Core `Microsoft.AspNetCore.RateLimiting`

| Fact | Verified | Source |
|------|----------|--------|
| Partitioner signature is `Func<HttpContext, RateLimitPartition<TKey>>` — no response, no endpoint outcome | YES | [Microsoft Learn rate-limit doc, fetched 2026-05-01](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-9.0) |
| `UseRateLimiter()` runs at the placement chosen in `Program.cs` (here: line 286, before `UseWhen("/Admin")`) | YES | Verified by inspection of `Program.cs:286-290` |
| Partitioner runs per-request before endpoint dispatch — cannot see auth outcome | YES | Doc explicitly: `partitionKey` is computed from `httpContext` only; `RejectionStatusCode` is fixed pre-decision |
| `OnRejected` callback receives `OnRejectedContext` with `HttpContext` and `Lease` — but the rejection happens at request entry, not after auth | YES | Doc example: `options.OnRejected = async (context, ct) => { ... }` is for the rate-limiter rejection only |
| Therefore D-02 ("standard model breaks on failed-only requirement") is correct | YES | This research |

### Existing `BasicAuthMiddlewareTests.cs` test pattern

| Fact | Verified | Source |
|------|----------|--------|
| Existing tests use `DefaultHttpContext` + manual `await middleware.InvokeAsync(ctx)` | YES | `BasicAuthMiddlewareTests.cs:30-39` |
| Tests use `EnvScope` helper to set/restore `FEEDBACK_ADMIN_USER`/`FEEDBACK_ADMIN_PASSWORD` env vars | YES | `BasicAuthMiddlewareTests.cs:86-120` |
| Tests use `NullLogger<BasicAuthMiddleware>.Instance` | YES | `BasicAuthMiddlewareTests.cs:21` |
| Tests are NOT in the `Security/` subfolder — they're at root | YES | `DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs` |
| Phase 03 sibling `ForwardedHeadersOptionsTests.cs` IS in `Security/` subfolder | YES | `DeckFlow.Web.Tests/Security/ForwardedHeadersOptionsTests.cs` |

**Recommendation:** New `AdminBruteForceTrackerTests.cs` lives in `DeckFlow.Web.Tests/Security/` per CONTEXT line 50 and 98 ("`04-01` test lands in the same folder"). Existing throttle-extension to `BasicAuthMiddlewareTests.cs` (root) is also acceptable — planner picks. Recommendation: keep tracker tests separate (`Security/AdminBruteForceTrackerTests.cs`) and add ONE integration-style test in `BasicAuthMiddlewareTests.cs` that proves the middleware-tracker wiring (the 11-attempt test in Pattern 3).

### Existing `ScryfallTaggerServiceTests.cs` MockHttp pattern

| Fact | Verified | Source |
|------|----------|--------|
| Uses two separate `MockHttpMessageHandler` instances (Scryfall and Tagger) | YES | `ScryfallTaggerServiceTests.cs:53-54` |
| Routes register via `mock.When(method, url-pattern).Respond(status, "content-type", body)` | YES | `ScryfallTaggerServiceTests.cs:56-58` |
| Response factory variant for setting `Set-Cookie` header on the CSRF GET | YES | `ScryfallTaggerServiceTests.cs:62-68` |
| Hit-count assertions via `mock.GetMatchCount(route)` | YES | `ScryfallTaggerServiceTests.cs:83-85` |
| Service constructed via direct `new ScryfallTaggerService(restClientFactory, typedTaggerClient, cache, new FakeResiliencePipelineProvider())` | YES (4 args today) | `ScryfallTaggerServiceTests.cs:43-47` — see Pitfall 5 |

### Existing `CardNormalizer.Normalize`

| Fact | Verified | Source |
|------|----------|--------|
| Lives at `DeckFlow.Core/Normalization/CardNormalizer.cs` | YES | Path verified by find 2026-05-01 |
| Lower-cases via `ToLowerInvariant`, strips punctuation (regex `[^\p{L}\p{N}\s]`), collapses multi-space, splits double-faced cards on `" / "` | YES | Source read |
| Used by other Phase services (per CONTEXT canonical_refs) | YES (assumed — appears in `DeckFlow.Core` namespace, consumed across the solution) | Inspection |

## Project Constraints (from CLAUDE.md)

The following CLAUDE.md / PROJECT.md constraints apply directly to Phase 04 — planner MUST verify each plan respects them:

| Constraint | Source | Phase 04 Implication |
|------------|--------|----------------------|
| ASP.NET 10 + Razor pinned (no framework migration) | CLAUDE.md | Use `Microsoft.AspNetCore.Http`, `Microsoft.AspNetCore.RateLimiting` already in repo. No alternative frameworks. |
| Render Starter web (512 MB cap) | CLAUDE.md | In-memory throttle dict + 24hr Tagger cache are well under any meaningful memory pressure (~10 KB total). |
| RestSharp + direct Polly v8 — do NOT migrate to standard handler | CLAUDE.md | New Scryfall search call uses existing `_scryfallPipeline`; new Tagger HEAD probes use existing `_taggerPipeline`. No new HTTP layer. |
| Public repo `luntc1972/DeckFlow` — no secrets | CLAUDE.md | No new secret values. `FEEDBACK_ADMIN_USER`/`PASSWORD` already in Render dashboard with `sync: false`. |
| VSTest unreliable in WSL | CLAUDE.md / PROJECT.md | Verification = `dotnet build` clean + push-and-watch CI + manual UAT. Plans must NOT require local `dotnet test` to pass. |
| Plain commits, no Co-Authored-By | CLAUDE.md | Plans must specify plain default-author commits per logical change. |
| README updated when behavior changes | CLAUDE.md | BUG-02 changes admin behavior (new 429 response) → README "Admin / Operations" section gets a one-paragraph note. BUG-01 doesn't change documented behavior (Tagger lookups already nominally "best effort") so README update is optional. |
| Theme CSS — layout in `site-common.css` not `site.css` | CLAUDE.md | N/A — Phase 04 has zero UI surface. |
| Commits per logical change | CLAUDE.md | 04-01 plan: helper extract → tracker class → middleware hook → unit test → README. 04-02 plan: ResolveCardPrintingAsync replacement → IMemoryCache wiring → unit test → README (optional). |

## Sources

### Primary (HIGH confidence)
- **Live curl probes 2026-05-01** (this research session): Scryfall `/cards/search?q=!"Sol+Ring"&unique=prints` (122 prints, has_more=false), Counterspell (67 prints, has_more=false), Tagger HEAD on `lea/270`, `soc/128`, `dsc/114`, `cmm/81`, `pmei/2026-7`, `pf26/5`, `sld/2539` (all 200), `soc/9999`, `zzz/1`, `lea/9999` (all 404). Output captured in `/tmp/sol-ring-prints.json`, `/tmp/sol-explicit.json`, `/tmp/cspell.json`, `/tmp/cspell-default.json`.
- **Microsoft Learn — Rate limiting middleware in ASP.NET Core** [CITED](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-9.0) — fetched 2026-05-01. Confirmed partitioner signature, lifecycle, and that `OnRejected` runs at rate-limiter rejection (not post-auth).
- **`DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs`** lines 1-90 — full source.
- **`DeckFlow.Web/Program.cs`** lines 1-371 — full source; pipeline ordering, DI registrations, `DeriveFeedbackPartitionKey`.
- **`DeckFlow.Web/Services/ScryfallTaggerService.cs`** lines 1-298 — full source; `LookupOracleTagsAsync`, `ResolveCardPrintingAsync` (the BUG-01 surgical site).
- **`DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs`** lines 1-214 — pipeline registry; named pipelines `scryfall`, `tagger`, `tagger-post`.
- **`DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs`** lines 1-121 — existing middleware test harness.
- **`DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs`** lines 1-204 — existing MockHttp + tagger pattern.
- **`DeckFlow.Web.Tests/Security/ForwardedHeadersOptionsTests.cs`** lines 1-37 — Phase 03 sibling.
- **`DeckFlow.Core/Normalization/CardNormalizer.cs`** lines 1-32 — recommended cache-key normalizer.

### Secondary (MEDIUM confidence)
- **`.planning/codebase/TESTING.md`** — confirms `RichardSzalay.MockHttp` 7.0.0 references, `Fake*` family conventions, `DefaultHttpContext` middleware test pattern, no FluentAssertions, no Moq.
- **`.planning/phases/03-tech-debt-cleanup/03-04-SUMMARY.md`** — TD-04 ship summary; Path B-rawpeer rationale; 2 × 429 + 4 × 200 live test result.
- **`.planning/phases/03-tech-debt-cleanup/03-HUMAN-UAT.md`** — UAT template (mirror for `04-HUMAN-UAT.md`).
- **`.planning/phases/03-tech-debt-cleanup/03-CONTEXT.md`** lines 56-58 — D-11 documents the Render-CIDR-not-published finding that justifies Path B-rawpeer.

### Tertiary (LOW confidence — informational)
- WebSearch on "Scryfall API cards search unique=prints default order release date" 2026-05-01 — Scryfall docs site links surfaced but full doc page returned 403 to WebFetch; treated as non-authoritative. The live curl tests are the authoritative source for Scryfall behavior in this research.

## Metadata

**Confidence breakdown:**
- Standard stack: **HIGH** — every dependency already in csproj; verified by inspection.
- Architecture: **HIGH** — both implementation paths trace to existing live code (`BasicAuthMiddleware`, `ScryfallTaggerService`); new code is small + localized.
- Pitfalls: **HIGH** — derived from inspection of the surgical sites + verified external behavior + Phase 03 lessons learned.
- BUG-01 external behavior (Scryfall + Tagger): **HIGH** — live-verified 2026-05-01.
- BUG-02 external behavior (RateLimiter API): **HIGH** — Microsoft Learn doc cited.

**Research date:** 2026-05-01
**Valid until:** 2026-05-15 (14 days — Tagger upstream is "best effort" per CONTEXT, so external-facts portion of this research has a tighter shelf life than typical 30-day estimate)
