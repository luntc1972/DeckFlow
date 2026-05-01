# Phase 03: Tech-Debt Cleanup - Pattern Map

**Mapped:** 2026-04-30
**Files analyzed:** 14 (1 new, 13 modified, 2 deleted)
**Analogs found:** 13 / 13 (TD-03 `.gitignore` and TD-01 deletes have no code analog by nature)

## File Classification

| File | Op | Role | Data Flow | Closest Analog | Match Quality |
|------|----|------|-----------|----------------|---------------|
| `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs` | NEW | test-helper factory | static-construction | `DeckFlow.Web.Tests/TestDoubles/FakeScryfallRestClientFactory.cs` + `FakeHttpClientFactory.cs` | role-match (test double family); style-match |
| `DeckFlow.Web/Services/CardLookupService.cs` | MODIFY | service (Scryfall HTTP) | request-response | self (canonical 2-ctor seam → 1-ctor target) | exact |
| `DeckFlow.Web/Services/CardSearchService.cs` | MODIFY | service (Scryfall HTTP) | request-response | `CardLookupService.cs` | exact (same family) |
| `DeckFlow.Web/Services/ScryfallSetService.cs` | MODIFY | service (Scryfall HTTP) | request-response | `CardLookupService.cs` | exact (same family) |
| `DeckFlow.Web/Services/ScryfallCommanderSearchService.cs` (in `CommanderSearchService.cs`) | MODIFY | service (Scryfall HTTP) | request-response | `CardLookupService.cs` | exact (same family) |
| `DeckFlow.Web/Services/CommanderBanListService.cs` | MODIFY | service (HTTP scrape) | request-response | self (alt-shape 2-ctor seam) | exact |
| `DeckFlow.Web/Services/CommanderSpellbookService.cs` | MODIFY | service (HTTP API) | request-response | `CommanderBanListService.cs` | role-match (non-Scryfall HTTP) |
| `DeckFlow.Web/Services/DeckConvertService.cs` | MODIFY | service (pure transform) | transform | `CommanderBanListService.cs` | role-match (the seam pattern) |
| `DeckFlow.Web/Services/ChatGptDeckPacketService.cs` | MODIFY | service (artifact build) | transform + file-IO | `CommanderBanListService.cs` | role-match |
| `DeckFlow.Web/Services/ChatGptDeckComparisonService.cs` | MODIFY | service (artifact build) | transform + file-IO | `ChatGptDeckPacketService.cs` | exact (same family) |
| `DeckFlow.Web/Services/ChatGptCedhMetaGapService.cs` | MODIFY | service (artifact build) | transform + file-IO | `ChatGptDeckPacketService.cs` | exact (same family) |
| `DeckFlow.Web/Services/Http/NullHttpClientFactory.cs` | DELETE | test-only factory (orphan) | n/a | n/a | n/a |
| `DeckFlow.Web/Services/Http/NullScryfallRestClientFactory.cs` | DELETE | test-only factory (orphan) | n/a | n/a | n/a |
| `DeckFlow.Web/Program.cs` | MODIFY | composition root (DI + middleware) | config | self (existing `Configure<ForwardedHeadersOptions>` block lines 117–128) | exact |
| `.gitignore` | MODIFY | repo config | n/a | self (current 17-line file) | exact |
| `README.md` | MODIFY | docs | n/a | self | exact |
| `DeckFlow.Web.Tests/CardLookupServiceTests.cs` (and 9 sibling test files) | MODIFY | test sites (call-site migration) | n/a | `CardLookupServiceTests.cs:14-29` | exact |

## Pattern Assignments

### `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs` (NEW — test-helper factory, static-construction)

**Analog:** `DeckFlow.Web.Tests/TestDoubles/FakeScryfallRestClientFactory.cs` + `FakeHttpClientFactory.cs` (file-shape, naming, namespace) and the **internal test ctor** in `DeckFlow.Web/Services/CardLookupService.cs:106-121` (signature shape that the new factory's static methods reproduce).

**Namespace + sealed-class shape** — match existing TestDoubles convention (`FakeHttpClientFactory.cs:1-5`):
```csharp
using System.Net.Http;

namespace DeckFlow.Web.Tests;

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
```
Notes for `TestServiceFactory.cs`:
- Namespace `DeckFlow.Web.Tests` (NOT `DeckFlow.Web.Tests.TestDoubles`) — every existing TestDoubles file uses the flat test namespace. Match it.
- Visibility `internal sealed` for the type — relies on assembly-internal scope already granted by `[InternalsVisibleTo("DeckFlow.Web.Tests")]` (`DeckFlow.Web/AssemblyInfo.cs:3`).
- Class itself is **`static`** (factory of static methods, not a fake of an interface). This is a small departure from the `Fake*` family but is the natural shape for a method-only helper.

**Static-method signature pattern** — derive each `Create<ServiceName>` directly from the existing internal ctor at `CardLookupService.cs:106-121`:
```csharp
internal ScryfallCardLookupService(
    RestClient? restClient = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeAsync = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsync = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallRulingsResponse>>>? executeRulingsAsync = null)
```
Translate to a `TestServiceFactory.CreateScryfallCardLookupService(...)` static method that:
1. Takes the same nullable delegate parameters (drop `restClient` if no test currently passes it — verify against `CardLookupServiceTests.cs` first).
2. Forwards directly to the new **single internal ctor** that the planner produces in TD-02 by passing the production deps via `FakeScryfallRestClientFactory`/`FakeHttpClientFactory`/`FakeResiliencePipelineProvider` for the dep slots tests don't care about.

**Pre-collapse delegate-only test invocation** — `DeckFlow.Web.Tests/CardLookupServiceTests.cs:16-29`:
```csharp
var service = new ScryfallCardLookupService(
    executeAsync: (request, _) => Task.FromResult(CreateCollectionResponse(...)),
    executeSearchAsync: (request, _) => Task.FromResult(new RestResponse<ScryfallSearchResponse>(request)
    {
        StatusCode = HttpStatusCode.OK,
        Data = new ScryfallSearchResponse([])
    }));
```
Migrates to:
```csharp
var service = TestServiceFactory.CreateScryfallCardLookupService(
    executeAsync: (request, _) => Task.FromResult(CreateCollectionResponse(...)),
    executeSearchAsync: (request, _) => Task.FromResult(new RestResponse<ScryfallSearchResponse>(request)
    {
        StatusCode = HttpStatusCode.OK,
        Data = new ScryfallSearchResponse([])
    }));
```
Named-parameter call style is preserved → mechanical sed-style migration across the 10 affected `*ServiceTests.cs` files.

**File-organization decision (claude's discretion per D-06):** Either one file `TestServiceFactory.cs` with 10 partial regions per service, OR 10 small files `TestServiceFactory.{ServiceName}.cs` using `internal static partial class TestServiceFactory`. Single file is fine if total <300 lines; split if any single service contributes >50 lines (e.g. ChatGpt* services with many delegate slots).

---

### Service ctor collapse (MODIFY 10 services — service, request-response)

**Canonical 2-ctor seam (BEFORE) — `DeckFlow.Web/Services/CardLookupService.cs:53-121`:**

Master private ctor (lines 53-89):
```csharp
private ScryfallCardLookupService(
    IScryfallRestClientFactory scryfallRestClientFactory,
    ResiliencePipeline<RestResponse> scryfallPipeline,
    RestClient? restClientOverride,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeAsyncOverride,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsyncOverride,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsyncOverride,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallRulingsResponse>>>? executeRulingsAsyncOverride)
{
    ArgumentNullException.ThrowIfNull(scryfallRestClientFactory);
    var pipeline = scryfallPipeline ?? ResiliencePipeline<RestResponse>.Empty;
    var client = restClientOverride ?? scryfallRestClientFactory.Create();
    _executeAsync = executeAsyncOverride ?? ((request, cancellationToken) =>
        ScryfallThrottle.ExecuteAsync(
            token => pipeline.ExecuteAsync(
                async pollyCt => await client.ExecuteAsync<ScryfallCollectionResponse>(request, pollyCt).ConfigureAwait(false),
                token).AsTask(),
            cancellationToken));
    // ... 3 more delegate fields with the same null-coalesce-then-build pattern
}
```

Public DI ctor (lines 91-104):
```csharp
public ScryfallCardLookupService(
    IScryfallRestClientFactory scryfallRestClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider)
    : this(
        scryfallRestClientFactory,
        pipelineProvider?.GetPipeline<RestResponse>("scryfall") ?? ResiliencePipeline<RestResponse>.Empty,
        null, null, null, null, null)
{
    ArgumentNullException.ThrowIfNull(pipelineProvider);
}
```

Internal test-compat ctor (lines 106-121) — **the seam being eliminated**:
```csharp
internal ScryfallCardLookupService(
    RestClient? restClient = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeAsync = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsync = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallRulingsResponse>>>? executeRulingsAsync = null)
    : this(
        NullScryfallRestClientFactory.Instance,         // <-- the Null* dep that anchors NullScryfallRestClientFactory.cs
        ResiliencePipeline<RestResponse>.Empty,
        restClient,
        executeAsync,
        executeSearchAsync,
        executeNamedAsync,
        executeRulingsAsync)
{
}
```

**Alternate 3-ctor shape (BEFORE) — `DeckFlow.Web/Services/CommanderBanListService.cs:44-93`:**

Same pattern but with an extra DI dep (`IMemoryCache`) and a single-delegate seam:
```csharp
private CommanderBanListService(
    IHttpClientFactory httpClientFactory,
    ResiliencePipeline<RestResponse> pipeline,
    IMemoryCache memoryCache,
    Func<CancellationToken, Task<string>>? fetchPageAsync)
{ /* null-coalesce + assign */ }

public CommanderBanListService(
    IHttpClientFactory httpClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider,
    IMemoryCache memoryCache,
    Func<CancellationToken, Task<string>>? fetchPageAsync = null)
    : this(httpClientFactory,
           pipelineProvider?.GetPipeline<RestResponse>("banlist") ?? ResiliencePipeline<RestResponse>.Empty,
           memoryCache, fetchPageAsync)
{ ArgumentNullException.ThrowIfNull(pipelineProvider); }

internal CommanderBanListService(
    IMemoryCache memoryCache,
    Func<CancellationToken, Task<string>>? fetchPageAsync)
    : this(
        NullHttpClientFactory.Instance,                  // <-- the Null* dep that anchors NullHttpClientFactory.cs
        ResiliencePipeline<RestResponse>.Empty,
        memoryCache, fetchPageAsync)
{ }
```

**Single-ctor target shape (AFTER) — D-04 mechanism:**

Per D-04 the master ctor IS the only ctor; visibility is `internal`; production deps + override delegates as nullable params with default `null`. Concrete pattern that the planner mints from the existing master ctor:

```csharp
// Was: private ScryfallCardLookupService(...). Now: internal, defaults appended, only ctor.
internal ScryfallCardLookupService(
    IScryfallRestClientFactory scryfallRestClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider,
    RestClient? restClientOverride = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeAsyncOverride = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsyncOverride = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsyncOverride = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallRulingsResponse>>>? executeRulingsAsyncOverride = null)
{
    ArgumentNullException.ThrowIfNull(scryfallRestClientFactory);
    ArgumentNullException.ThrowIfNull(pipelineProvider);
    var pipeline = pipelineProvider.GetPipeline<RestResponse>("scryfall") ?? ResiliencePipeline<RestResponse>.Empty;
    var client = restClientOverride ?? scryfallRestClientFactory.Create();
    _executeAsync = executeAsyncOverride ?? ((request, cancellationToken) =>
        ScryfallThrottle.ExecuteAsync(
            token => pipeline.ExecuteAsync(
                async pollyCt => await client.ExecuteAsync<ScryfallCollectionResponse>(request, pollyCt).ConfigureAwait(false),
                token).AsTask(),
            cancellationToken));
    // ... unchanged delegate field assigns
}
```

Key structural deltas vs BEFORE:
1. Single `internal` ctor — public DI ctor + private master ctor + internal test ctor → collapse to one.
2. Pipeline is resolved inline from `pipelineProvider.GetPipeline<RestResponse>("scryfall")` (was: a redundant null-coalesce-vs-`.Empty` outside the ctor, then again inside).
3. No `NullScryfallRestClientFactory.Instance` reference anywhere — the `restClientOverride ?? factory.Create()` line still handles the null-real path.
4. Test path no longer goes through a second ctor — tests call `TestServiceFactory.CreateScryfallCardLookupService(...)` which **calls this internal ctor directly** with a `Fake*` factory in the prod-dep slots.

**Single-ctor analog already in the codebase** — `DeckFlow.Web/Controllers/FeedbackController.cs:13-17`:
```csharp
public FeedbackController(IFeedbackStore store, IVersionService versionService)
{
    _store = store;
    _versionService = versionService;
}
```
This is the controller convention everywhere; TD-02 brings the 10 services into alignment with it.

---

### `DeckFlow.Web/Program.cs` DI registration adaptation (MODIFY)

**Current registration shape (BEFORE) — `Program.cs:164-176` (extracted block):**
```csharp
builder.Services.AddSingleton<ICommanderSearchService, ScryfallCommanderSearchService>();
builder.Services.AddSingleton<ICardSearchService, ScryfallCardSearchService>();
builder.Services.AddSingleton<ICardLookupService, ScryfallCardLookupService>();
builder.Services.AddSingleton<ICommanderBanListService, CommanderBanListService>();
builder.Services.AddSingleton<ICommanderSpellbookService, CommanderSpellbookService>();
builder.Services.AddSingleton<IScryfallSetService, ScryfallSetService>();
builder.Services.AddScoped<IChatGptDeckPacketService, ChatGptDeckPacketService>();
builder.Services.AddScoped<IChatGptDeckComparisonService, ChatGptDeckComparisonService>();
builder.Services.AddScoped<IChatGptCedhMetaGapService, ChatGptCedhMetaGapService>();
builder.Services.AddScoped<IDeckConvertService, DeckConvertService>();
```

**Existing factory-delegate analog (already used in this file) — `Program.cs:179-180`:**
```csharp
builder.Services.AddSingleton<IArchidektCacheJobService>(sp => sp.GetRequiredService<ArchidektCacheJobService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ArchidektCacheJobService>());
```

**Target shape (AFTER) — D-05 mechanism:** Stock generic `AddSingleton<TService, TImpl>()` cannot bind to an `internal` ctor; switch to factory-delegate form using the same `sp => new ...` style already in use:
```csharp
builder.Services.AddSingleton<ICardLookupService>(sp =>
    new ScryfallCardLookupService(
        sp.GetRequiredService<IScryfallRestClientFactory>(),
        sp.GetRequiredService<ResiliencePipelineProvider<string>>()));
builder.Services.AddSingleton<ICommanderBanListService>(sp =>
    new CommanderBanListService(
        sp.GetRequiredService<IHttpClientFactory>(),
        sp.GetRequiredService<ResiliencePipelineProvider<string>>(),
        sp.GetRequiredService<IMemoryCache>()));
// ... 8 more
```
Planner verifies in research whether MS DI activator actually rejects internal ctors (claude's discretion noted in D-05). If it accepts internal, the `AddSingleton<TService, TImpl>()` lines stay and only the override-default behaviour matters.

---

### `DeckFlow.Web/Program.cs` ForwardedHeaders block (MODIFY — TD-04)

**Current code (BEFORE) — `Program.cs:114-128`, the literal block to replace:**
```csharp
// Honor X-Forwarded-* headers from the reverse proxy (e.g. Render, Fly, Azure App Service)
// so request.Scheme reflects the browser's https scheme, not the http hop from proxy to app.
// Without this, SameOriginRequestValidator sees scheme=http while Origin=https and rejects the request.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    // Render assigns dynamic proxy IPs we can't enumerate; clear the defaults so forwarded
    // headers from any upstream are honored. Acceptable here because DeckFlow does not
    // authenticate requests, so spoofing a scheme only grants the same access unauth'd
    // callers already have.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
```

**Target shape (AFTER) — D-12 mechanism, env-conditional:**
```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;

    if (builder.Environment.IsProduction())
    {
        // Trust only Render's documented inbound proxy CIDR ranges + loopback (Kestrel
        // health checks). Source: <render docs URL retrieved 2026-04-30 by 03-04-PLAN research>.
        // Default IISIntegration loopback entries are preserved by NOT calling Clear().
        // CIDRs filled in by 03-04-PLAN research outcome.
        // options.KnownIPNetworks.Add(new IPNetwork(IPAddress.Parse("X.X.X.X"), Y));
    }
    else
    {
        // Dev: zero-config — no reverse proxy, only loopback in defaults.
        // Defaults already include 127.0.0.1 and ::1; do nothing.
    }
});
```

**Conditional-on-`IsProduction()` analog already in `Program.cs`** — `Program.cs:198-202`:
```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Deck");
    app.UseHsts();
}
```
Same `IsProduction()` / `!IsDevelopment()` branch style applies inside the `Configure<ForwardedHeadersOptions>` lambda.

**Pipeline-order constraint (carry-forward, DO NOT BREAK) — `Program.cs:193-196`:**
```csharp
// Must run before any middleware that reads request.Scheme/Host (HttpsRedirection,
// security headers, SameOriginRequestValidator in controllers) so those see the
// browser's original scheme/host, not the proxy hop.
app.UseForwardedHeaders();
```
The `Configure<>` body change does NOT alter middleware order; `UseForwardedHeaders()` placement is invariant per CLAUDE.md architectural constraint.

---

### `.gitignore` (MODIFY — TD-03)

**Current contents (BEFORE) — full file, 17 lines:**
```
.vs/
bin/
obj/
artifacts/
*.user
*.suo
*.log
node_modules/
package.json
package-lock.json
AGENTS.md
DeckFlow.Web/wwwroot/extensions/*.zip
/.codex
docs/superpowers/

# Probe scratch files
*_probe.json
```

**Insertion point (AFTER) — D-08 mechanism:** add a single glob next to the existing `DeckFlow.Web/wwwroot/extensions/*.zip` build-output exclusion. Suggested placement (preserves the topical grouping with the other generated-asset glob):
```
DeckFlow.Web/wwwroot/extensions/*.zip
DeckFlow.Web/wwwroot/js/*.js
```
Verified scope: `wwwroot/lib/` (vendored 3rd-party JS) is **not** under `wwwroot/js/` — confirmed via the `ls wwwroot/js/` directory listing returning exactly the 10 generated files (`card-lookup.js`, `card-search.js`, `category-suggestions.js`, `commander-search.js`, `deck-sync.js`, `df-select.js`, `df-typeahead.js`, `feedback.js`, `judge-questions.js`, `site.js`). No site.js conflict; the glob is safe.

---

### `DeckFlow.Web/Services/Http/NullHttpClientFactory.cs` + `NullScryfallRestClientFactory.cs` (DELETE — TD-01)

No code analog (deletion). Deletion semantics per D-01/D-02:
1. Wait until 03-01-PLAN (TD-02) ships and removes the only call sites (the `: this(NullScryfallRestClientFactory.Instance, ...)` and `: this(NullHttpClientFactory.Instance, ...)` delegate chains in the 10 services' internal test-compat ctors).
2. Verify with `grep -rn "NullHttpClientFactory\|NullScryfallRestClientFactory" DeckFlow.Web DeckFlow.Web.Tests` → must return zero.
3. `git rm DeckFlow.Web/Services/Http/NullHttpClientFactory.cs DeckFlow.Web/Services/Http/NullScryfallRestClientFactory.cs`.
4. `dotnet build` clean → ship.

The reference content of each file (read for posterity):
- `NullHttpClientFactory.cs` is 17 lines; only public surface is `static readonly Instance` + `CreateClient(string) => new HttpClient()`.
- `NullScryfallRestClientFactory.cs` is 38 lines; only public surface is `static readonly Instance` + `Create()` returning a real RestSharp `RestClient` against `https://api.scryfall.com`.
Both are zero-state singletons — deletion has no migration concerns.

---

### Test call-site migration (MODIFY ~10 `*ServiceTests.cs` files — D-06)

**Canonical analog — `DeckFlow.Web.Tests/CardLookupServiceTests.cs:14-29`:**
```csharp
[Fact]
public async Task LookupAsync_PreservesQuantities_AndCollectsMissingLines()
{
    var service = new ScryfallCardLookupService(
        executeAsync: (request, _) => Task.FromResult(CreateCollectionResponse(...)),
        executeSearchAsync: (request, _) => Task.FromResult(new RestResponse<ScryfallSearchResponse>(request)
        {
            StatusCode = HttpStatusCode.OK,
            Data = new ScryfallSearchResponse([])
        }));
```

**Migration pattern (mechanical):**
- `new ScryfallCardLookupService(...)` → `TestServiceFactory.CreateScryfallCardLookupService(...)`
- Named-parameter list is preserved verbatim.
- No test logic changes.

Affected test files (planner enumerates from `DeckFlow.Web.Tests/*ServiceTests.cs` matching the 10 services in D-03).

## Shared Patterns

### `[InternalsVisibleTo]` already-granted access
**Source:** `DeckFlow.Web/AssemblyInfo.cs:3`
```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]
```
**Apply to:** Every TD-02 single-ctor service. The collapsed internal ctor + `TestServiceFactory.Create*Service(...)` call into it both work with this single attribute already in place. **No new attribute work needed.** D-06 explicit.

### `Fake*` test-double family (already wired)
**Source:** `DeckFlow.Web.Tests/TestDoubles/FakeHttpClientFactory.cs`, `FakeScryfallRestClientFactory.cs`, `FakeResiliencePipelineProvider.cs`, `StubHttpMessageHandler.cs`
**Apply to:** `TestServiceFactory.cs` static methods that need to fill production-dep slots when the test only cares about delegate overrides. Pattern: `new Fake*(...)` for the prod dep, real delegate overrides on top.
Example invocation that the new factory's static methods will produce internally:
```csharp
return new ScryfallCardLookupService(
    new FakeScryfallRestClientFactory(httpClient),
    new FakeResiliencePipelineProvider(),
    restClientOverride: null,
    executeAsyncOverride: executeAsync,    // <-- the test's delegate
    ...);
```

### Single-ctor + null-guard convention (already universal in controllers)
**Source:** `DeckFlow.Web/Controllers/FeedbackController.cs:13-17`
```csharp
public FeedbackController(IFeedbackStore store, IVersionService versionService)
{
    _store = store;
    _versionService = versionService;
}
```
**Apply to:** All 10 collapsed services. Add `ArgumentNullException.ThrowIfNull(...)` per existing `CardLookupService.cs:62` / `CommanderBanListService.cs:50-51` convention.

### Factory-delegate DI registration (already used for `ArchidektCacheJobService`)
**Source:** `DeckFlow.Web/Program.cs:179-180`
```csharp
builder.Services.AddSingleton<IArchidektCacheJobService>(sp => sp.GetRequiredService<ArchidektCacheJobService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ArchidektCacheJobService>());
```
**Apply to:** Each of the 10 service registrations IF the MS DI activator rejects internal ctors. Switch the stock `AddSingleton<I, T>()` to `AddSingleton<I>(sp => new T(sp.GetRequiredService<...>(), ...))`.

### `IsProduction()` env-conditional convention
**Source:** `DeckFlow.Web/Program.cs:199` (`!app.Environment.IsDevelopment()` branch for `UseExceptionHandler` + `UseHsts`)
**Apply to:** TD-04 `Configure<ForwardedHeadersOptions>` body — same `builder.Environment.IsProduction()` guard around the CIDR-add block.

### Plain commit, README-current convention (project)
**Source:** CLAUDE.md "Constraints" + `feedback_commit_author` memory
**Apply to:** Every TD-0X commit. Plain default-author commit, no Co-Authored-By trailer. README updated in the same commit when behavior changes (TD-03 README onboarding text per D-10).

## No Analog Found

| File | Role | Reason |
|------|------|--------|
| `.gitignore` (TD-03 line addition) | repo config | Not C# code; structural pattern is "append a glob next to existing globs." Trivial, fully described above. |
| `NullHttpClientFactory.cs` / `NullScryfallRestClientFactory.cs` deletes | n/a | Deletes have no code analog by nature. Verification protocol (grep + build) is the analog. |
| `README.md` onboarding paragraph (TD-03 D-10) | docs | Project docs convention — planner picks placement. No code analog. |

## Metadata

**Analog search scope:** `DeckFlow.Web/Services/`, `DeckFlow.Web/Services/Http/`, `DeckFlow.Web/Controllers/`, `DeckFlow.Web/Program.cs`, `DeckFlow.Web/AssemblyInfo.cs`, `DeckFlow.Web.Tests/TestDoubles/`, `DeckFlow.Web.Tests/CardLookupServiceTests.cs`, `.gitignore`, `wwwroot/js/` (directory listing).
**Files scanned:** 13 reads + 1 directory listing.
**Pattern extraction date:** 2026-04-30.
