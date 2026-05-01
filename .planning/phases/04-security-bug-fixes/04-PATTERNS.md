# Phase 04: Security & Bug Fixes - Pattern Map

**Mapped:** 2026-05-01
**Files analyzed:** 7 (4 modified + 3 new)
**Analogs found:** 7 / 7

## File Classification

| New/Modified File | Plan | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|------|-----------|----------------|---------------|
| `DeckFlow.Web/Infrastructure/AdminBruteForceTracker.cs` (NEW) | 04-01 | utility (in-memory store) | event-driven (counter) | `DeckFlow.Web/Services/TaggerSessionCache.cs` (singleton, IMemoryCache-backed in-memory store) + `DeckFlow.Web/Services/ScryfallThrottle.cs` (static `ConcurrentDictionary` / `SemaphoreSlim` shape) | exact (role + data flow) |
| `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` (MODIFIED) | 04-01 | middleware | request-response | (self) — current `Challenge(...)` lines 70-76 is the surgical site | exact |
| `DeckFlow.Web/Program.cs` (MODIFIED) | 04-01 | config / composition root | startup | (self) — `DeriveFeedbackPartitionKey` lines 341-350; `AddRateLimiter` lines 136-149; `AddSingleton<ITaggerSessionCache>` line 108 | exact |
| `DeckFlow.Web.Tests/Security/AdminBruteForceTrackerTests.cs` (NEW) | 04-01 | test (unit) | request-response | `DeckFlow.Web.Tests/Security/ForwardedHeadersOptionsTests.cs` (sibling Phase 03 test) + `DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs` (DefaultHttpContext + InvokeAsync) | exact |
| `DeckFlow.Web/Services/ScryfallTaggerService.cs` (MODIFIED) | 04-02 | service (HTTP adapter) | request-response (multi-upstream) | (self) — `ResolveCardPrintingAsync` lines 123-148 replaced; `FetchTaggerSessionAsync` lines 155-177 mirrored for HEAD probe shape; `ScryfallTaggerService` ctor lines 53-71 extended with `IMemoryCache` | exact |
| `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs` (MODIFIED) | 04-02 | test (orchestration) | request-response (multi-upstream) | (self) — `CreateService` lines 27-48 + cold-flow test lines 50-86 | exact |
| `DeckFlow.Web/Program.cs` (MODIFIED — 04-02 leg) | 04-02 | config | startup | (self) — line 200 `AddSingleton<IScryfallTaggerService, ScryfallTaggerService>()`; expand to factory closure resolving `IMemoryCache` | exact |

## Pattern Assignments

### `DeckFlow.Web/Infrastructure/AdminBruteForceTracker.cs` (NEW — Plan 04-01)

**Role:** utility (in-memory partitioned counter store with TTL)
**Data Flow:** event-driven (RecordFailure → bucket increment; IsThrottled → bucket read)

**Primary analog:** `DeckFlow.Web/Services/TaggerSessionCache.cs` (singleton, in-memory, TTL semantics, sealed class implementing a small interface, ArgumentNullException ctor guards, `///` XML doc comments).

**Secondary shape reference:** `ConcurrentDictionary<string, T>` usage — this pattern does not yet exist in the repo for state buckets (the only `ConcurrentDictionary` usage is in build-time tooling), so the BCL `AddOrUpdate` shape from RESEARCH.md Pattern 1 is canonical.

**Imports pattern** (copy from `TaggerSessionCache.cs:1-6`):
```csharp
using System;
using System.Collections.Concurrent;       // NEW for this file
using Microsoft.AspNetCore.Http;            // NEW — for HttpContext (planner may keep tracker pure-string and pass key in)

namespace DeckFlow.Web.Infrastructure;
```

**Class shape pattern** (mirrors `TaggerSessionCache` at `DeckFlow.Web/Services/TaggerSessionCache.cs:12-95`):
```csharp
// Mirror the record + interface + sealed class triple from TaggerSessionCache:

/// <summary>
/// Per-IP fixed-window brute-force counter for /Admin basic-auth. (D-06: 10 attempts / 15 min.)
/// </summary>
public sealed record BucketEntry(int Count, DateTimeOffset WindowStart);

/// <summary>
/// In-memory IP→bucket store for admin basic-auth throttling (BUG-02 / D-02).
/// </summary>
public interface IAdminBruteForceTracker
{
    (bool Throttled, int RetryAfterSeconds) IsThrottled(string partitionKey, DateTimeOffset now);
    void RecordFailure(string partitionKey, DateTimeOffset now);
}

public sealed class AdminBruteForceTracker : IAdminBruteForceTracker
{
    private const int PermitLimit = 10;                                    // D-06
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);    // D-06
    private readonly ConcurrentDictionary<string, BucketEntry> _buckets = new();

    // ... see RESEARCH.md Pattern 1 lines 207-247 for full body
}
```

**Constructor guard pattern** — copy from `TaggerSessionCache.cs:65-69`:
```csharp
public TaggerSessionCache(IMemoryCache memoryCache)
{
    ArgumentNullException.ThrowIfNull(memoryCache);
    _memoryCache = memoryCache;
}
```
For `AdminBruteForceTracker`, the ctor is parameter-less (no DI deps) — but if the planner takes a clock/`TimeProvider` for testability, ctor guard pattern still applies.

**Static helpers naming pattern** — `ScryfallThrottle.cs` uses `PascalCase` for static readonly fields (`MinInterval`, `Gate`). `AdminBruteForceTracker` follows: `PermitLimit`, `Window`.

---

### `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs` (MODIFIED — Plan 04-01)

**Role:** middleware
**Data Flow:** request-response

**Analog:** self. The throttle hook is a localized add at `Challenge(...)` (lines 70-76).

**Current `Challenge` shape** (`BasicAuthMiddleware.cs:70-76` — the surgical site):
```csharp
private void Challenge(HttpContext context, string reason)
{
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    _logger.LogWarning("Admin basic-auth challenge issued: {Reason} from {RemoteIp}", reason, remoteIp);
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    context.Response.Headers["WWW-Authenticate"] = $"Basic realm=\"{_realm}\", charset=\"UTF-8\"";
}
```

**Constructor pattern** (`BasicAuthMiddleware.cs:8-19`):
```csharp
public sealed class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BasicAuthMiddleware> _logger;
    private readonly string _realm;

    public BasicAuthMiddleware(RequestDelegate next, ILogger<BasicAuthMiddleware> logger, string realm)
    {
        _next = next;
        _logger = logger;
        _realm = realm;
    }
```

**Pattern to apply (from RESEARCH.md Pitfall 6 + Pattern 3):**
- Add `IAdminBruteForceTracker tracker` as a 4th ctor parameter (after `realm`).
- At top of `InvokeAsync` (line 22 — BEFORE the env-var-missing check at lines 26-31): if `tracker.IsThrottled(ip, now)` → set 429 + `Retry-After` header + log warn (preserves SC #1 "warn fires every challenge") + return.
- Inside `Challenge(...)` body (after the existing `LogWarning` line 73, after setting 401): call `tracker.RecordFailure(ip, now)`.
- D-01 invariant: `Challenge` is called ONLY on auth-fail paths (lines 36, 47, 54, 63), so RecordFailure happens only on 401 — the 503 env-var path doesn't increment.

**Existing logging template to preserve verbatim** (line 73):
```csharp
_logger.LogWarning("Admin basic-auth challenge issued: {Reason} from {RemoteIp}", reason, remoteIp);
```

**New 429 emission pattern** (mirrors `Challenge` style — set status + header):
```csharp
context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
```

---

### `DeckFlow.Web/Program.cs` (MODIFIED — Plan 04-01 + Plan 04-02)

**Role:** composition root
**Data Flow:** startup

**Analog:** self.

**Existing `DeriveFeedbackPartitionKey`** (`Program.cs:341-350`) — refactor target per D-05:
```csharp
/// <summary>
/// Partition key for the feedback-submit rate limiter (TD-04 / Phase 03 SC #4,
/// retrieved 2026-04-30). Reads the immediate-peer IP directly. Render's edge collapses
/// all production traffic to a single partition - acceptable at DeckFlow's
/// expected feedback volume (well under 5/hr globally). Forwarded-header spoofing
/// cannot rotate this key. See DeckFlow.Web.Tests/Security/ForwardedHeadersOptionsTests.cs
/// for the invariant.
/// </summary>
internal static string DeriveFeedbackPartitionKey(HttpContext context)
    => "peer:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
```

**Refactor pattern (D-05 / Claude's discretion):**
```csharp
internal static string DerivePeerIpKey(HttpContext context, string prefix)
    => prefix + ":" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

internal static string DeriveFeedbackPartitionKey(HttpContext context) => DerivePeerIpKey(context, "peer");
internal static string DeriveAdminPartitionKey(HttpContext context) => DerivePeerIpKey(context, "admin");
```

**Singleton DI registration analog for tracker** (Plan 04-01) — copy shape from `Program.cs:108`:
```csharp
// CSRF + cookie session store for the Tagger flow (D-07, HIGH-2: 270s TTL).
builder.Services.AddSingleton<ITaggerSessionCache, TaggerSessionCache>();
```
New line for tracker:
```csharp
// Admin basic-auth brute-force throttle (BUG-02 / D-02: 10 attempts / 15min, in-memory).
builder.Services.AddSingleton<IAdminBruteForceTracker, AdminBruteForceTracker>();
```

**Factory-closure DI pattern for ScryfallTaggerService** (Plan 04-02) — replace `Program.cs:200`:

Current single-line registration:
```csharp
builder.Services.AddSingleton<IScryfallTaggerService, ScryfallTaggerService>();
```

Replacement pattern (mirrors `Program.cs:187-192` `AddSingleton<ICommanderSpellbookService>` factory closure that already resolves `IMemoryCache`):
```csharp
builder.Services.AddSingleton<IScryfallTaggerService>(sp =>
    new ScryfallTaggerService(
        sp.GetRequiredService<IScryfallRestClientFactory>(),
        sp.GetRequiredService<IScryfallTaggerHttpClient>(),
        sp.GetRequiredService<ITaggerSessionCache>(),
        sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
        sp.GetRequiredService<IMemoryCache>(),
        sp.GetService<ILogger<ScryfallTaggerService>>()));
```

**Closest analog for the factory shape** — `Program.cs:187-192`:
```csharp
builder.Services.AddSingleton<ICommanderSpellbookService>(sp =>
    new CommanderSpellbookService(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
        sp.GetRequiredService<IMemoryCache>(),
        sp.GetService<ILogger<CommanderSpellbookService>>()));
```

**MiddleWare/branch wiring stays unchanged** (`Program.cs:288-290`):
```csharp
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/Admin"),
    branch => branch.UseMiddleware<BasicAuthMiddleware>("DeckFlow Admin"));
```
Per D-02, throttle is intra-middleware — no second `UseRateLimiter` policy added.

---

### `DeckFlow.Web.Tests/Security/AdminBruteForceTrackerTests.cs` (NEW — Plan 04-01)

**Role:** unit/integration test
**Data Flow:** request-response

**Sibling-folder analog:** `DeckFlow.Web.Tests/Security/ForwardedHeadersOptionsTests.cs` (Phase 03 — same folder, same `namespace DeckFlow.Web.Tests.Security`).

**Test shape pattern** (copy from `ForwardedHeadersOptionsTests.cs:1-37` — full file, exact shape):
```csharp
using System.Net;
using DeckFlow.Web;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DeckFlow.Web.Tests.Security;

public sealed class ForwardedHeadersOptionsTests
{
    [Fact]
    public void DeriveFeedbackPartitionKey_IgnoresForwardedForHeader()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Forwarded-For"] = "1.2.3.4";
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.1");

        var key = Program.DeriveFeedbackPartitionKey(ctx);

        Assert.DoesNotContain("1.2.3.4", key);
        Assert.Contains("10.0.0.1", key);
        Assert.StartsWith("peer:", key);
    }
}
```

**Middleware-invocation analog:** `DeckFlow.Web.Tests/BasicAuthMiddlewareTests.cs:28-39` (the canonical `DefaultHttpContext` + `InvokeAsync` pattern):
```csharp
[Fact]
public async Task NoAuthHeader_Returns401_WithChallenge()
{
    using var _ = EnvScope.Set(EnvUser, "admin", EnvPass, "secret");
    var context = new DefaultHttpContext();
    var middleware = new BasicAuthMiddleware(
        _ => Task.CompletedTask,
        NullLogger<BasicAuthMiddleware>.Instance,
        "DeckFlow Admin");

    await middleware.InvokeAsync(context);

    Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    Assert.Contains("realm=\"DeckFlow Admin\"", context.Response.Headers["WWW-Authenticate"].ToString());
}
```

**EnvScope helper** — reuse the `EnvScope` IDisposable nested class at `BasicAuthMiddlewareTests.cs:86-120`. New tracker tests that invoke `BasicAuthMiddleware` end-to-end need the same env-var setup. Either:
- Copy `EnvScope` into `Security/AdminBruteForceTrackerTests.cs` (duplicates ~30 LOC), OR
- Promote `EnvScope` to a shared `TestDoubles/EnvScope.cs` (planner's call — Phase 03 didn't promote, so the local-copy precedent stands).

**Phase 04 04-01 test extension shape** (from RESEARCH.md Pattern 3 — assert 11-attempt sequence):
```csharp
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

**Pure-tracker unit test shape** (no middleware — closer to ForwardedHeadersOptionsTests style):
```csharp
[Fact]
public void RecordFailure_ElevenTimes_TenthIsAllowed_EleventhIsThrottled()
{
    var tracker = new AdminBruteForceTracker();
    var now = DateTimeOffset.UtcNow;
    for (var i = 0; i < 10; i++) tracker.RecordFailure("admin:10.0.0.1", now);

    var (throttled, retryAfter) = tracker.IsThrottled("admin:10.0.0.1", now);

    Assert.True(throttled);
    Assert.InRange(retryAfter, 1, 900);
}
```

**Naming convention:** `MethodUnderTest_Scenario_ExpectedOutcome` (per CONVENTIONS.md / TESTING.md):
- `ElevenFailures_FromSameIp_TenthReturns401_EleventhReturns429`
- `RecordFailure_AfterWindowExpiry_ResetsBucket`
- `IsThrottled_DifferentIPs_DoNotInterfere`
- `IsThrottled_RetryAfter_ReturnsRemainingSecondsInWindow`

---

### `DeckFlow.Web/Services/ScryfallTaggerService.cs` (MODIFIED — Plan 04-02)

**Role:** service (HTTP adapter)
**Data Flow:** request-response chain (Scryfall → Tagger HEAD probe loop → Tagger GET CSRF → Tagger POST GraphQL)

**Analog:** self. `ResolveCardPrintingAsync` (lines 123-148) is the surgical replacement site.

**Imports already present** (lines 1-10) — no new top-level using needed; `Microsoft.Extensions.Caching.Memory` is implicitly available via `IMemoryCache` injection (matches `TaggerSessionCache.cs:4`):
```csharp
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Registry;
using RestSharp;
```

Add: `using Microsoft.Extensions.Caching.Memory;` and `using DeckFlow.Core.Normalization;` (for `CardNormalizer.Normalize`).

**Constructor extension pattern** — current ctor (`ScryfallTaggerService.cs:53-71`):
```csharp
public ScryfallTaggerService(
    IScryfallRestClientFactory scryfallRestClientFactory,
    IScryfallTaggerHttpClient taggerHttpClient,
    ITaggerSessionCache taggerSessionCache,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<ScryfallTaggerService>? logger = null)
{
    ArgumentNullException.ThrowIfNull(scryfallRestClientFactory);
    ArgumentNullException.ThrowIfNull(taggerHttpClient);
    ArgumentNullException.ThrowIfNull(taggerSessionCache);
    ArgumentNullException.ThrowIfNull(pipelineProvider);
    _scryfallRestClientFactory = scryfallRestClientFactory;
    _taggerHttpClient = taggerHttpClient;
    _taggerSessionCache = taggerSessionCache;
    _scryfallPipeline = pipelineProvider.GetPipeline<RestResponse>("scryfall");
    _taggerPipeline = pipelineProvider.GetPipeline<RestResponse>("tagger");
    _taggerPostPipeline = pipelineProvider.GetPipeline<RestResponse>("tagger-post");
    _logger = logger ?? NullLogger<ScryfallTaggerService>.Instance;
}
```

**Add `IMemoryCache memoryCache` parameter** (positional placement: between `pipelineProvider` and the optional `logger` per Phase 03 single-ctor-no-test-seam convention, OR as the last required positional arg — match the `CommanderSpellbookService` ordering on Program.cs:187-192 which puts `IMemoryCache` BEFORE the optional `ILogger`):

```csharp
public ScryfallTaggerService(
    IScryfallRestClientFactory scryfallRestClientFactory,
    IScryfallTaggerHttpClient taggerHttpClient,
    ITaggerSessionCache taggerSessionCache,
    ResiliencePipelineProvider<string> pipelineProvider,
    IMemoryCache memoryCache,                                      // NEW — D-12
    ILogger<ScryfallTaggerService>? logger = null)
{
    ArgumentNullException.ThrowIfNull(scryfallRestClientFactory);
    ArgumentNullException.ThrowIfNull(taggerHttpClient);
    ArgumentNullException.ThrowIfNull(taggerSessionCache);
    ArgumentNullException.ThrowIfNull(pipelineProvider);
    ArgumentNullException.ThrowIfNull(memoryCache);                // NEW
    // ... assignments ...
    _memoryCache = memoryCache;                                    // NEW
}
```

**Existing Scryfall search pattern (canonical)** — copy from `CardLookupService.cs:297-300`:
```csharp
var request = new RestRequest("cards/search", Method.Get);
request.AddQueryParameter("q", query);                          // For Phase 04: $"!\"{cardName}\""
request.AddQueryParameter("unique", "prints");
request.AddQueryParameter("include_multilingual", "true");      // Optional — planner decides
```

**Existing Scryfall execute pattern (canonical)** — copy shape from `ScryfallTaggerService.cs:129-133` (the call this PR replaces — same RestClient/pipeline/throttle wrapper):
```csharp
var response = await ScryfallThrottle.ExecuteAsync(
    ct => _scryfallPipeline.ExecuteAsync(
        async pollyCt => await scryfallClient.ExecuteAsync(request, pollyCt).ConfigureAwait(false),
        ct).AsTask(),
    cancellationToken).ConfigureAwait(false);
```

**Existing Tagger GET shape (analog for HEAD probe)** — `ScryfallTaggerService.cs:155-163` (`FetchTaggerSessionAsync`):
```csharp
var taggerRestClient = new RestClient(_taggerHttpClient.Inner);
var pageRequest = new RestRequest($"card/{set}/{collectorNumber}", Method.Get);

var pageResponse = await _taggerPipeline.ExecuteAsync(
    async ct => await taggerRestClient.ExecuteAsync(pageRequest, ct).ConfigureAwait(false),
    cancellationToken).ConfigureAwait(false);
```

**HEAD probe pattern (Phase 04 BUG-01)** — change `Method.Get` to `Method.Head`, identical pipeline + RestSharp wiring:
```csharp
var probe = new RestRequest($"card/{set}/{number}", Method.Head);
var probeResponse = await _taggerPipeline.ExecuteAsync(
    async ct => await taggerRestClient.ExecuteAsync(probe, ct).ConfigureAwait(false),
    cancellationToken).ConfigureAwait(false);
if ((int)probeResponse.StatusCode == 200) { /* winner */ }
```

**IMemoryCache positive/negative pattern** — analog: `CommanderSpellbookService.cs:107-143` (canonical TryGetValue / Set in this codebase):
```csharp
// CommanderSpellbookService.cs:107-111 — POSITIVE READ
var cacheKey = $"spellbook:{string.Join("|", commanders)}::{string.Join("|", main)}";
if (_memoryCache.TryGetValue<CommanderSpellbookResult>(cacheKey, out var cached) && cached is not null)
{
    return cached;
}

// CommanderSpellbookService.cs:141-144 — POSITIVE WRITE
if (result is not null)
{
    _memoryCache.Set(cacheKey, result, CacheDuration);  // CacheDuration = static readonly TimeSpan
}
```

**Phase 04 application** (positive 24hr / negative 1hr per D-12):
```csharp
private static readonly TimeSpan PositiveCacheDuration = TimeSpan.FromHours(24);
private static readonly TimeSpan NegativeCacheDuration = TimeSpan.FromHours(1);

var cacheKey = $"tagger-printing:{CardNormalizer.Normalize(cardName)}";

if (_memoryCache.TryGetValue<(string Set, string Number)?>(cacheKey, out var cached))
{
    return cached ?? (string.Empty, string.Empty);  // null = negative-cache hit
}

// On winning probe:
_memoryCache.Set(cacheKey, ((string, string)?)(set, number), PositiveCacheDuration);

// On all-probes-404 or Scryfall search failure:
_memoryCache.Set(cacheKey, ((string, string)?)null, NegativeCacheDuration);
```

**Cache-key normalization** — D-discretion locked by RESEARCH.md to `DeckFlow.Core.Normalization.CardNormalizer.Normalize(cardName)` (`DeckFlow.Core/Normalization/CardNormalizer.cs:7-25`):
```csharp
public static string Normalize(string cardName)
{
    ArgumentNullException.ThrowIfNull(cardName);
    var normalized = cardName.Trim().ToLowerInvariant();
    // ... strips ★, *F*, splits double-faced, regex punctuation, multi-space collapse ...
    return normalized.Trim();
}
```

**Logging pattern** (preserve existing `ScryfallTaggerService` style — `cs:91, 137, 166, 264`):
```csharp
_logger.LogWarning("Tagger has no indexed printing for {CardName} after {Attempts} probes", cardName, probesAttempted);
_logger.LogWarning("Scryfall printings search failed for {CardName}: {Status}", cardName, response.StatusCode);
```

**Probe ceiling literal:** `private const int MaxProbeAttempts = 5;` (D-11). Naming follows `CardLookupService.CollectionBatchSize` / `MaxIncluded` PascalCase const convention.

---

### `DeckFlow.Web.Tests/Services/ScryfallTaggerServiceTests.cs` (MODIFIED — Plan 04-02)

**Role:** orchestration test (multi-upstream MockHttp)
**Data Flow:** request-response (multi-upstream)

**Analog:** self. `CreateService` helper (`ScryfallTaggerServiceTests.cs:27-48`) needs one-line update; existing test patterns (`LookupOracleTagsAsync_ColdFlow_ReturnsTagsFromGraphQL`, lines 50-86) are the canonical shape to copy for the new printings-iteration test.

**Existing `CreateService` helper** (lines 27-48) — surgical update site:
```csharp
private static ScryfallTaggerService CreateService(
    MockHttpMessageHandler scryfallMock,
    MockHttpMessageHandler taggerMock,
    ITaggerSessionCache? sessionCache = null)
{
    var scryfallHttpClient = scryfallMock.ToHttpClient();
    scryfallHttpClient.BaseAddress = new Uri("https://api.scryfall.com/");
    var restClientFactory = new FakeScryfallRestClientFactory(scryfallHttpClient);

    var taggerHttpClient = taggerMock.ToHttpClient();
    taggerHttpClient.BaseAddress = new Uri("https://tagger.scryfall.com/");
    var typedTaggerClient = new ScryfallTaggerHttpClient(taggerHttpClient);

    var cache = sessionCache
        ?? new TaggerSessionCache(new MemoryCache(new MemoryCacheOptions()));

    return new ScryfallTaggerService(
        restClientFactory,
        typedTaggerClient,
        cache,
        new FakeResiliencePipelineProvider());                 // ← currently 4 args; CTOR ADDS IMemoryCache
}
```

**Required update — match new ctor (5 required args):**
```csharp
private static ScryfallTaggerService CreateService(
    MockHttpMessageHandler scryfallMock,
    MockHttpMessageHandler taggerMock,
    ITaggerSessionCache? sessionCache = null,
    IMemoryCache? memoryCache = null)                          // NEW optional param with default
{
    // ... unchanged setup ...
    var cache = sessionCache
        ?? new TaggerSessionCache(new MemoryCache(new MemoryCacheOptions()));
    var printingCache = memoryCache ?? new MemoryCache(new MemoryCacheOptions());  // NEW

    return new ScryfallTaggerService(
        restClientFactory,
        typedTaggerClient,
        cache,
        new FakeResiliencePipelineProvider(),
        printingCache);                                        // NEW 5th positional arg
}
```

**MockHttp two-handler pattern** — exact shape from existing `LookupOracleTagsAsync_ColdFlow_ReturnsTagsFromGraphQL` (lines 50-86):
```csharp
[Fact]
public async Task LookupOracleTagsAsync_ColdFlow_ReturnsTagsFromGraphQL()
{
    using var scryfallMock = new MockHttpMessageHandler();
    using var taggerMock = new MockHttpMessageHandler();

    var scryfallRoute = scryfallMock
        .When(HttpMethod.Get, "https://api.scryfall.com/cards/named*")
        .Respond(HttpStatusCode.OK, "application/json", ScryfallCardJson);

    var csrfRoute = taggerMock
        .When(HttpMethod.Get, "https://tagger.scryfall.com/card/lea/161")
        .Respond(_ =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.OK);
            r.Content = new StringContent(TaggerCsrfHtml, System.Text.Encoding.UTF8, "text/html");
            r.Headers.Add("Set-Cookie", "_ga=test-cookie; Path=/; HttpOnly");
            return r;
        });

    var graphqlRoute = taggerMock
        .When(HttpMethod.Post, "https://tagger.scryfall.com/graphql")
        .Respond(HttpStatusCode.OK, "application/json", TaggerGraphQlJson);

    var sut = CreateService(scryfallMock, taggerMock);

    var tags = await sut.LookupOracleTagsAsync("Thrasios, Triton Hero", CancellationToken.None);

    Assert.NotNull(tags);
    Assert.NotEmpty(tags);
    Assert.Contains("Ramp", tags);

    Assert.Equal(1, scryfallMock.GetMatchCount(scryfallRoute));
    Assert.Equal(1, taggerMock.GetMatchCount(csrfRoute));
    Assert.Equal(1, taggerMock.GetMatchCount(graphqlRoute));
}
```

**Phase 04 BUG-01 new test shape** (mirrors above, plus HEAD probe sequence per RESEARCH.md Pattern 4):
- Replace `cards/named*` route with `cards/search*` returning JSON with 3 prints in `data` array.
- Add 3 HEAD routes on tagger mock: 2 × `HttpStatusCode.NotFound`, 1 × `HttpStatusCode.OK`.
- Keep existing CSRF GET + GraphQL POST routes for the winning printing.
- Assert `GetMatchCount` for each probe = 1 (proves loop visited each in order, stopped on first 200).

**Existing JSON fixture pattern** — `ScryfallTaggerServiceTests.cs:13-25` raw-string literal const:
```csharp
private const string ScryfallCardJson = """
{"object":"card","id":"abc123","name":"Thrasios, Triton Hero","set":"lea","collector_number":"161"}
""";
```

**New search-response fixture** (3-printings, mirror Scryfall API):
```csharp
private const string ScryfallSearchJson = """
{"object":"list","total_cards":3,"has_more":false,"data":[
  {"object":"card","name":"Sol Ring","set":"soc","collector_number":"128"},
  {"object":"card","name":"Sol Ring","set":"tmc","collector_number":"59"},
  {"object":"card","name":"Sol Ring","set":"lea","collector_number":"270"}
]}
""";
```

**Existing tests that compile against the 4-arg ctor** (lines 50, 88, 131, 172) all call `CreateService(...)` only — updating the helper signature once fixes all four callers in a single edit.

## Shared Patterns

### Pattern: ConcurrentDictionary lazy expiry (D-08)

**Source:** RESEARCH.md Pattern 1 (lines 207-247) — no existing in-repo analog. The closest in-spirit pattern is `ScryfallThrottle.cs` (process-wide static gate), but it uses `SemaphoreSlim` not a dict.

**Apply to:** `AdminBruteForceTracker.cs` only.

**Idiom:**
```csharp
public void RecordFailure(string partitionKey, DateTimeOffset now) =>
    _buckets.AddOrUpdate(
        partitionKey,
        _ => new BucketEntry(1, now),
        (_, existing) => (now - existing.WindowStart >= Window)
            ? new BucketEntry(1, now)
            : existing with { Count = existing.Count + 1 });
```

### Pattern: Singleton DI registration

**Source:** `Program.cs:108` (`AddSingleton<ITaggerSessionCache, TaggerSessionCache>()`), `Program.cs:181` (`AddSingleton<IMechanicLookupService, WotcMechanicLookupService>()`).

**Apply to:** `AdminBruteForceTracker` registration in 04-01.

**Idiom:**
```csharp
builder.Services.AddSingleton<IInterface, Implementation>();
```

### Pattern: Factory-closure DI registration with multi-dependency resolution

**Source:** `Program.cs:187-192` (`AddSingleton<ICommanderSpellbookService>(sp => new CommanderSpellbookService(...))`).

**Apply to:** `ScryfallTaggerService` re-registration in 04-02 (constructor signature change).

**Idiom:**
```csharp
builder.Services.AddSingleton<IService>(sp =>
    new Service(
        sp.GetRequiredService<IDep1>(),
        sp.GetRequiredService<IDep2>(),
        sp.GetRequiredService<IMemoryCache>(),
        sp.GetService<ILogger<Service>>()));
```

### Pattern: ArgumentNullException ctor guards

**Source:** `TaggerSessionCache.cs:65-69`, `ScryfallTaggerService.cs:60-63`, `CommanderSpellbookService.cs:77-78`.

**Apply to:** `AdminBruteForceTracker` (if ctor takes args), `ScryfallTaggerService` updated ctor (new `IMemoryCache memoryCache` arg).

**Idiom:**
```csharp
public Service(IDep dep)
{
    ArgumentNullException.ThrowIfNull(dep);
    _dep = dep;
}
```

### Pattern: Structured logging (never interpolation)

**Source:** `BasicAuthMiddleware.cs:73`, `ScryfallTaggerService.cs:91, 137, 166, 264, 276, 285`, `CommanderSpellbookService.cs:121, 137`.

**Apply to:** All new log statements in 04-01 (throttle 429 emission) and 04-02 (probe-exhaustion warning, search failure).

**Idiom:**
```csharp
_logger.LogWarning("Description with {Placeholder1} and {Placeholder2}", value1, value2);
```

### Pattern: Optional `ILogger<T>` ctor parameter with `NullLogger` fallback

**Source:** `ScryfallTaggerService.cs:58, 70`, `CommanderSpellbookService.cs:75, 82`.

**Apply to:** `ScryfallTaggerService` updated ctor (preserve existing nullable logger param at end).

**Idiom:**
```csharp
public Service(/* required deps */, ILogger<Service>? logger = null)
{
    _logger = logger ?? NullLogger<Service>.Instance;
}
```

### Pattern: Test naming `Method_Scenario_Expected`

**Source:** `BasicAuthMiddlewareTests.cs:15, 29, 43, 56, 70`, `ScryfallTaggerServiceTests.cs:51, 89, 132, 173`.

**Apply to:** Both new test classes (`AdminBruteForceTrackerTests`, extensions to `ScryfallTaggerServiceTests`).

### Pattern: `DefaultHttpContext` + manual `InvokeAsync` middleware test

**Source:** `BasicAuthMiddlewareTests.cs:14-67`, `ForwardedHeadersOptionsTests.cs:11-25`.

**Apply to:** Any 04-01 test that exercises `BasicAuthMiddleware` end-to-end with the throttle hook.

**Idiom (compose):**
```csharp
var ctx = new DefaultHttpContext();
ctx.Connection.RemoteIpAddress = IPAddress.Parse("10.20.30.40");
ctx.Request.Headers["Authorization"] = $"Basic {encoded}";
await middleware.InvokeAsync(ctx);
Assert.Equal(StatusCodes.Status429TooManyRequests, ctx.Response.StatusCode);
```

### Pattern: MockHttp two-handler orchestration

**Source:** `ScryfallTaggerServiceTests.cs:50-86` (Scryfall handler + Tagger handler, both with multiple `.When().Respond()` routes, hit-counts via `GetMatchCount`).

**Apply to:** 04-02 new printings-iteration test (Scryfall search route + 3 Tagger HEAD routes + 1 Tagger CSRF GET + 1 Tagger POST).

**Idiom:** see "Existing test pattern" excerpt under `ScryfallTaggerServiceTests.cs` above.

### Pattern: Cache-key normalization via `CardNormalizer.Normalize`

**Source:** `DeckFlow.Core/Normalization/CardNormalizer.cs:7-25`.

**Apply to:** 04-02 cache-key prefix `tagger-printing:` suffix.

**Idiom:**
```csharp
var cacheKey = $"tagger-printing:{CardNormalizer.Normalize(cardName)}";
```

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| (none) | — | — | All 7 files have strong in-repo analogs. The `ConcurrentDictionary<string, BucketEntry>` body of `AdminBruteForceTracker` has no exact in-repo precedent (closest is `ScryfallThrottle`'s static `SemaphoreSlim`), but the surrounding shape (sealed class + interface + record + ArgumentNullException ctor + IMemoryCache-style singleton) maps cleanly to `TaggerSessionCache`. RESEARCH.md Pattern 1 fills the gap with the BCL-canonical `AddOrUpdate` body. |

## Metadata

**Analog search scope:**
- `DeckFlow.Web/Infrastructure/` — full directory (3 files)
- `DeckFlow.Web/Services/` — `TaggerSessionCache.cs`, `ScryfallTaggerService.cs`, `ScryfallThrottle.cs`, `CommanderSpellbookService.cs`, `CardLookupService.cs`
- `DeckFlow.Web/Program.cs` — DI registration block (lines 50-252), middleware pipeline (lines 254-293), helper methods (lines 341-350)
- `DeckFlow.Web.Tests/` — `BasicAuthMiddlewareTests.cs`, `Security/ForwardedHeadersOptionsTests.cs`, `Services/ScryfallTaggerServiceTests.cs`
- `DeckFlow.Core/Normalization/CardNormalizer.cs`

**Files scanned:** 11 source files + 3 test files + 1 csproj-derived metadata read (Program.cs sections via Grep).

**Pattern extraction date:** 2026-05-01

**Cross-cutting constraints (from CLAUDE.md / PROJECT.md / Phase 03):**
- Plain default-author commits, commit per logical change.
- README updated when behavior changes (BUG-02 adds 429 → admin/operations note).
- VSTest unreliable in WSL → verification via `dotnet build` clean + Render CI + manual UAT.
- RestSharp + direct Polly v8 — reuse `scryfall` and `tagger` named pipelines (`Program.cs` references at lines 67-69 in `ScryfallTaggerService` ctor body).
- Phase 03 single-ctor convention — `ScryfallTaggerService` is NOT in TD-02 scope; current test directly `new`s the service. Adding `IMemoryCache` to the ctor breaks the single test helper `CreateService` (1 update site, 4 test methods compile through it).

---

*Phase: 04-security-bug-fixes*
*Pattern map: 2026-05-01*
