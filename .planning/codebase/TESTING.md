# Testing Patterns

**Analysis Date:** 2026-04-29

## Test Framework

**Runner:**
- xUnit 2.9.3 (`xunit`, `xunit.runner.visualstudio` 3.1.4) on .NET 10.
- `Microsoft.NET.Test.Sdk` 17.14.1 in both test projects.
- No `xunit.runner.json` checked in — defaults apply (parallel test classes within an assembly, single-threaded inside a class).

**Assertion Library:**
- xUnit `Assert.*` (`Assert.Equal`, `Assert.Single`, `Assert.Contains`, `Assert.Throws`, `Assert.IsType`, `Assert.NotNull`, `Assert.Empty`). No FluentAssertions or Shouldly.

**Mocking / HTTP doubles:**
- `RichardSzalay.MockHttp` 7.0.0 is referenced by `DeckFlow.Web.Tests` but the in-repo pattern is **hand-rolled** test doubles (see `DeckFlow.Web.Tests/TestDoubles/`).
- No Moq / NSubstitute / FakeItEasy.

**Coverage tool:**
- `coverlet.collector` 6.0.4 in `DeckFlow.Core.Tests.csproj` only. Not wired into `DeckFlow.Web.Tests.csproj` yet.

**Run Commands:**
```bash
dotnet test DeckFlow.sln                         # all tests, both assemblies
dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj   # core only
dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj     # web only
dotnet test --filter FullyQualifiedName~CardLookup           # by name filter
dotnet test --collect:"XPlat Code Coverage"                  # coverage (Core only by default)
```

Integration tests gated by env var:
```bash
DECKSYNC_RUN_SCRYFALL_INTEGRATION=1 dotnet test \
    --filter FullyQualifiedName~CardLookupIntegrationTests
```

## Test File Organization

**Location:**
- **Separate assemblies, mirrored layout.** `DeckFlow.Core` ↔ `DeckFlow.Core.Tests` and `DeckFlow.Web` ↔ `DeckFlow.Web.Tests`. No tests inside production projects.
- Tests live flat at the root of the test project (e.g., `DeckFlow.Web.Tests/CardLookupServiceTests.cs`). Subfolders only for `Services/` (multi-file feature tests) and `TestDoubles/` (shared helpers).

**Naming:**
- File name = `<TypeUnderTest>Tests.cs`.
- Class: `public sealed class <TypeUnderTest>Tests`.
- Method: `MethodUnderTest_Scenario_ExpectedOutcome` (snake-style with underscores between segments).
  - `LookupAsync_PreservesQuantities_AndCollectsMissingLines`
  - `LookupAsync_ThrowsHttpRequestException_WhenScryfallFails`
  - `FindCombosAsync_HitsCache_OnSecondCall`
  - `Compare_LooseMode_FindsPrintingConflictAndSkipsDelta`

**Structure:**
```
DeckFlow.Core.Tests/
├── ParserTests.cs
├── DiffEngineTests.cs
├── ExporterTests.cs
└── ...                               # one file per Core type, flat

DeckFlow.Web.Tests/
├── CardLookupServiceTests.cs         # one file per Web service/controller
├── DeckControllerTests.cs
├── CategoryKnowledgeStoreTests.cs
├── Services/
│   ├── CommanderSpellbookServiceTests.cs
│   └── ScryfallTaggerServiceTests.cs
└── TestDoubles/
    ├── StubHttpMessageHandler.cs     # internal sealed
    ├── FakeHttpClientFactory.cs      # internal sealed
    ├── FakeResiliencePipelineProvider.cs
    ├── FakeScryfallRestClientFactory.cs
    └── FakeCategoryKnowledgeStore.cs # public sealed (interface impl)
```

## Test Structure

**Suite Organization:**
```csharp
// DeckFlow.Web.Tests/CardLookupServiceTests.cs
namespace DeckFlow.Web.Tests;

public sealed class CardLookupServiceTests
{
    [Fact]
    public async Task LookupAsync_PreservesQuantities_AndCollectsMissingLines()
    {
        var service = new ScryfallCardLookupService(
            executeAsync: (request, _) => Task.FromResult(CreateCollectionResponse(...)),
            executeSearchAsync: (request, _) => Task.FromResult(...));

        var result = await service.LookupAsync("1 Sol Ring\nArcane Signet\nMade Up Card");

        Assert.Contains("Sol Ring", result.VerifiedOutputs[0]);
        Assert.Equal(new[] { "ERROR: Made Up Card" }, result.MissingLines);
    }

    private static RestResponse<ScryfallCollectionResponse> CreateCollectionResponse(...) { ... }
}
```

**Patterns:**
- **AAA inline, no comment markers.** Arrange / Act / Assert separated by blank lines.
- **One assertion *concept* per test** — multiple `Assert.*` calls are fine when they describe one outcome.
- `[Theory]` + `[InlineData(...)]` for parameterized cases; combined with `Assert.ThrowsAsync<TException>` for negative paths (see `CategoryKnowledgeStoreTests.GetCategoriesAsync_ThrowsForBlankCardName` at `DeckFlow.Web.Tests/CategoryKnowledgeStoreTests.cs:60-77`).
- **No setup/teardown base classes.** Per-test construction inline; tiny private static helpers when shared (`MainboardEntry`, `CommanderEntry`, `BuildService`, `CreateCollectionResponse`).
- **Process-wide environment isolation** via xUnit collections:
  ```csharp
  [CollectionDefinition("CategoryKnowledgeStoreTests", DisableParallelization = true)]
  public sealed class CategoryKnowledgeStoreTestsCollection { }

  [Collection("CategoryKnowledgeStoreTests")]
  public sealed class CategoryKnowledgeStoreTests { ... }
  ```
  Used when tests mutate `Environment.SetEnvironmentVariable` (e.g., `MTG_DATA_DIR`) — `CategoryKnowledgeStoreTests.cs:9-14`.
- Always restore env state in a `try/finally` block (`CategoryKnowledgeStoreTests.cs:23-37`).

## Mocking

**Framework:** None. The codebase uses three hand-rolled patterns.

### Pattern 1 — Delegate test seam (preferred for HTTP services)
The service's internal ctor accepts `Func<RestRequest, CancellationToken, Task<RestResponse<T>>>` delegates that bypass the live RestSharp client:
```csharp
// DeckFlow.Web/Services/CardLookupService.cs:106-121 (internal ctor)
internal ScryfallCardLookupService(
    RestClient? restClient = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeAsync = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsync = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallRulingsResponse>>>? executeRulingsAsync = null)
```
Tests construct directly with named args:
```csharp
var service = new ScryfallCardLookupService(
    executeAsync: (request, _) => Task.FromResult(CreateCollectionResponse(...)),
    executeSearchAsync: (request, _) => Task.FromResult(new RestResponse<ScryfallSearchResponse>(request)
    {
        StatusCode = HttpStatusCode.OK,
        Data = new ScryfallSearchResponse([])
    }));
```
Granted via `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` (`DeckFlow.Web/AssemblyInfo.cs:3`).

### Pattern 2 — `StubHttpMessageHandler` + `FakeHttpClientFactory`
Use when the service holds `IHttpClientFactory` directly (e.g., `CommanderSpellbookService`):
```csharp
// DeckFlow.Web.Tests/Services/CommanderSpellbookServiceTests.cs:29-39
private static CommanderSpellbookService BuildService(StubHttpMessageHandler stub, IMemoryCache? cache = null)
{
    var factory = new FakeHttpClientFactory(new Dictionary<string, HttpMessageHandler>
    {
        ["commander-spellbook"] = stub
    });
    return new CommanderSpellbookService(
        factory,
        new FakeResiliencePipelineProvider(),
        cache ?? new MemoryCache(new MemoryCacheOptions()));
}

var stub = new StubHttpMessageHandler();
stub.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
{
    Content = new StringContent(json, Encoding.UTF8, "application/json")
});
```
`StubHttpMessageHandler` exposes `Enqueue(HttpResponseMessage)`, `RecordedRequests`, `CallCount`, and `NextException` for fault injection. Records `(Uri, Method)` snapshots so assertions work after the request is disposed (`StubHttpMessageHandler.cs:8-9`).

### Pattern 3 — Hand-written interface fakes
For non-HTTP collaborators, write a `Fake*` implementing the production interface with public `*Calls` counters and configurable returns:
```csharp
// DeckFlow.Web.Tests/TestDoubles/FakeCategoryKnowledgeStore.cs
public sealed class FakeCategoryKnowledgeStore : ICategoryKnowledgeStore
{
    public int RunCacheSweepCalls { get; private set; }
    public int RunCacheSweepResult { get; set; }
    public Exception? RunCacheSweepException { get; set; }

    public Task<int> RunCacheSweepAsync(ILogger logger, int duration, CancellationToken ct = default)
    {
        RunCacheSweepCalls++;
        if (RunCacheSweepException is not null) throw RunCacheSweepException;
        return Task.FromResult(RunCacheSweepResult);
    }
    // ...
}
```
Same shape for controller tests: `FakeDeckSyncService`, `FakeDeckConvertService`, `FakeChatGptDeckPacketService`, `ThrowingCardSearchService`, etc. (used in `DeckControllerTests.cs:23-35`).

### Polly resilience pipeline fake
`FakeResiliencePipelineProvider` returns `ResiliencePipeline<T>.Empty` for every key — disables retries/timeouts so tests aren't slow:
```csharp
internal sealed class FakeResiliencePipelineProvider : ResiliencePipelineProvider<string>
{
    public override ResiliencePipeline<T> GetPipeline<T>(string key) => ResiliencePipeline<T>.Empty;
}
```

**What to Mock:**
- External HTTP (Scryfall, Commander Spellbook, Archidekt, Moxfield, EDH Top 16, WOTC, Scryfall Tagger) — always.
- `ICategoryKnowledgeStore`, `IFeedbackStore`, and other DB-backed stores when testing services/controllers that consume them.
- `ResiliencePipelineProvider<string>` — replace with `FakeResiliencePipelineProvider` so retries don't fire.
- `ILogger<T>` — use `NullLogger<T>.Instance` (do not Fake; do not assert on logs).

**What NOT to Mock:**
- `MemoryCache` — instantiate the real `MemoryCache(new MemoryCacheOptions())` (e.g., `CommanderSpellbookServiceTests.cs:137`).
- Parsers (`MoxfieldParser`, `ArchidektParser`), normalizers (`CardNormalizer`), and other pure helpers — call them directly (`ParserTests.cs`, `DiffEngineTests.cs`).
- `DiffEngine` — instantiate with the production `MatchMode` enum.
- Domain records / DTOs — construct directly with `new`.

## Fixtures and Factories

**Test Data:**
- Constructed inline as object initializers / `new` — there is no shared fixture project.
- Small private static helpers per test class for repetitive shapes:
  ```csharp
  private static DeckEntry MainboardEntry(string name) => new DeckEntry
  {
      Name = name,
      NormalizedName = name.ToLowerInvariant(),
      Quantity = 1,
      Board = "mainboard"
  };
  ```
  (`CommanderSpellbookServiceTests.cs:13-19`).
- JSON payloads are inline raw-string literals (`"""..."""`) when the service consumes JSON (`CommanderSpellbookServiceTests.cs:44-57`).
- Decklist text uses raw strings so indentation is literal (`ParserTests.cs:46-54`).

**Location:**
- No `Fixtures/` folder. Helpers stay private to the test class unless reused — when reused they go in `DeckFlow.Web.Tests/TestDoubles/`.

## Coverage

**Requirements:** None enforced. No CI gate, no minimum coverage threshold.

**View Coverage (Core only):**
```bash
dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj \
    --collect:"XPlat Code Coverage"
# Output: DeckFlow.Core.Tests/TestResults/<guid>/coverage.cobertura.xml
```

`DeckFlow.Web.Tests` has no `coverlet.collector` reference; add it before running coverage there.

## Test Types

**Unit Tests (the bulk of the suite):**
- **Scope:** one class per test file; collaborators replaced via delegate seams or `Fake*` doubles.
- **Approach:** synchronous arrange + `await` act + `Assert.*`. No I/O, no real network, no real database.
- Examples: every file in `DeckFlow.Core.Tests/` and most of `DeckFlow.Web.Tests/`.

**Service-level integration-ish tests:**
- `CommanderSpellbookServiceTests` and `ScryfallTaggerServiceTests` exercise the full `IHttpClientFactory` + Polly + RestSharp pipeline against a `StubHttpMessageHandler`. They count HTTP calls (`stub.RecordedRequests`, `stub.CallCount`) to verify caching/retry behavior.
- `CategoryKnowledgeStoreTests` exercises real SQLite paths via `MTG_DATA_DIR` redirection in temp dirs.

**End-to-end / live integration:**
- `CardLookupIntegrationTests` shells out to the **real CLI** and hits **live Scryfall**. Gated by `DECKSYNC_RUN_SCRYFALL_INTEGRATION=1`; the body returns early when the env var is unset, so default `dotnet test` runs are offline-clean (`CardLookupIntegrationTests.cs:9, 17-20`).

**E2E / browser tests:** None. No Playwright, Selenium, or Cypress in the repo.

## Common Patterns

**Async Testing:**
```csharp
[Fact]
public async Task FindCombosAsync_HitsCache_OnSecondCall()
{
    var stub = new StubHttpMessageHandler();
    stub.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    });
    var sut = BuildService(stub, new MemoryCache(new MemoryCacheOptions()));

    var first = await sut.FindCombosAsync(deck, CancellationToken.None);
    var second = await sut.FindCombosAsync(deck, CancellationToken.None);

    Assert.Single(stub.RecordedRequests);   // HTTP fired once; second served from cache
    Assert.NotNull(first);
    Assert.NotNull(second);
}
```
- Always pass `CancellationToken.None` explicitly when the production API requires a token.
- Variable conventionally named `sut` (system under test) or `service`.

**Error Testing:**
```csharp
var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.LookupAsync("Sol Ring"));
Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
```
For `[Theory]` parametric exception types:
```csharp
[Theory]
[InlineData(null, typeof(ArgumentNullException))]
[InlineData("", typeof(ArgumentException))]
[InlineData("   ", typeof(ArgumentException))]
public async Task GetCategoriesAsync_ThrowsForBlankCardName(string? cardName, Type expectedExceptionType)
{
    var store = CreateStore();
    if (expectedExceptionType == typeof(ArgumentNullException))
    {
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => store.GetCategoriesAsync(cardName!));
        Assert.Equal("cardName", ex.ParamName);
        return;
    }
    var argEx = await Assert.ThrowsAsync<ArgumentException>(() => store.GetCategoriesAsync(cardName!));
    Assert.Equal("cardName", argEx.ParamName);
}
```
(`CategoryKnowledgeStoreTests.cs:60-77`)

**Cache verification:**
- Construct a real `MemoryCache(new MemoryCacheOptions())`, call the method twice, then assert `stub.RecordedRequests` count to prove the second call was served from cache.

**Controller tests:**
- Construct the controller manually with all `Fake*` collaborators (no `WebApplicationFactory<T>` / `TestServer`).
- Provide `ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }` when the action reads `HttpContext` (`DeckControllerTests.cs:75-79`).
- Assert on `IActionResult` shape: `Assert.IsType<ViewResult>(result)`, then drill into `.Model` / `.ViewName`.

**Fault injection:**
- HTTP failure → enqueue `new HttpResponseMessage(HttpStatusCode.InternalServerError)` on the stub.
- Network exception → set `stub.NextException = new HttpRequestException(...)`.
- Service-collaborator failure → set `fake.RunCacheSweepException = new InvalidOperationException(...)`.

---

*Testing analysis: 2026-04-29*
