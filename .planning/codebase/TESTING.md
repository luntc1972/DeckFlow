# Testing Patterns

**Analysis Date:** 2026-05-29

## Test Framework

**Runner:**
- xUnit 2.9.3 (both test projects)
- Config: `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` and `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`
- Test SDK: Microsoft.NET.Test.Sdk 17.14.1
- Test discovery: xunit.runner.visualstudio 3.1.4

**Assertion Library:**
- xUnit assertions (`Assert.Equal`, `Assert.Single`, `Assert.Throws`, `Assert.IsType`, etc.)

**Run Commands:**
```bash
dotnet build                          # Compile and run tests (build-clean is the gate; VSTest unreliable in WSL)
dotnet build --no-restore            # Rebuild without package restore
dotnet test                           # Run all tests (cross-platform)
dotnet test --filter "TestClassName" # Run specific test class
dotnet test --collect:"XPlat Code Coverage"  # Collect coverage via coverlet.collector 6.0.4
```

**Special Note (WSL):**
- VSTest is unreliable in WSL. Rely on `dotnet build` clean + targeted manual harness or push-and-watch CI.
- Build via Windows dotnet.exe over WSL (workspace-write sandbox cannot run Windows dotnet, use `--sandbox danger-full-access` for Codex dispatch).

## Test File Organization

**Location:**
- `DeckFlow.Core.Tests/` for core domain logic tests.
- `DeckFlow.Web.Tests/` for service, controller, and integration tests.
- `DeckFlow.Web.Tests/TestDoubles/` for shared test-double factories and stubs.
- `DeckFlow.Web.Tests/Services/` for service-specific tests (e.g., `ScryfallTaggerLookupServiceTests.cs`).
- `DeckFlow.Web.Tests/Infrastructure/` for test utilities (`EnvScope.cs`).
- `DeckFlow.Web.Tests/Integration/` for integration and container-based tests (`PostgresContainerFixture.cs`, `ScryfallTaggerCookieReplayTests.cs`).

**Naming:**
- Test class: `public sealed class XxxTests` (e.g., `CardLookupServiceTests`).
- Test methods: descriptive names, often `Method_Scenario_ExpectedResult` (e.g., `LookupAsync_PreservesQuantities_AndCollectsMissingLines`).

**Namespace:**
- All tests in a single namespace per project (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`) regardless of subfolder.
- File-scoped: `namespace DeckFlow.Web.Tests;` or `namespace DeckFlow.Core.Tests;`.

## Test Structure

**Suite Organization:**
```csharp
namespace DeckFlow.Web.Tests;

/// <summary>
/// Tests for <see cref="ScryfallCardLookupService"/> covering quantity preservation, missing-line collection, ...
/// </summary>
public sealed class CardLookupServiceTests
{
    [Fact]
    public async Task LookupAsync_PreservesQuantities_AndCollectsMissingLines()
    {
        // Arrange
        var service = TestServiceFactory.CreateScryfallCardLookupService(
            executeAsync: (request, _) => Task.FromResult(CreateCollectionResponse(...)));

        // Act
        var result = await service.LookupAsync("1 Sol Ring\nArcane Signet");

        // Assert
        Assert.Single(result.VerifiedOutputs);
        Assert.Empty(result.MissingLines);
    }
}
```

**Patterns:**
- Setup: Inline via local variables or test-double factories (see `TestServiceFactory.CreateScryfallCardLookupService`).
- Teardown: `IDisposable` on test class for resource cleanup (temp files, SQLite connections); implement `Dispose()` and clean up (`DeckFlow.Core.Tests/ContentVideoStoreDistillTests.cs:13-35`).
- Async lifetime: `IAsyncLifetime` on test classes that need async setup/teardown (e.g., `PostgresContainerFixture.cs`).
- Collection serialization: Use xUnit `[CollectionDefinition(..., DisableParallelization = true)]` + `[Collection(...)]` on test class for process-wide env variable mutation (e.g., `MTG_DATA_DIR` in `CategoryKnowledgeStoreTests.cs:14-22`).

## Mocking

**Framework:** RichardSzalay.MockHttp 7.0.0 for HTTP mocking.

**Patterns:**
```csharp
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
        r.Content = new StringContent(TaggerCsrfHtml, Encoding.UTF8, "text/html");
        r.Headers.Add("Set-Cookie", "_ga=test-cookie; Path=/; HttpOnly");
        return r;
    });

var sut = CreateService(scryfallMock, taggerMock);
var tags = await sut.LookupOracleTagsAsync("Thrasios, Triton Hero", CancellationToken.None);

// Assert both mocks fired exactly once
Assert.Equal(1, scryfallMock.GetMatchCount(scryfallRoute));
Assert.Equal(1, taggerMock.GetMatchCount(csrfRoute));
```

**What to Mock:**
- External HTTP calls via `MockHttpMessageHandler`.
- Database operations via stateful test doubles (`FakeContentVideoStore`, `FakeCategoryKnowledgeStore`).
- Service dependencies via `Func<...>` override delegates in internal constructors.

**What NOT to Mock:**
- In-memory caches (`IMemoryCache`) — use real `MemoryCache` in tests.
- Parsers and domain logic — use real implementations.
- Serialization (JSON, Markdown) — use real implementations.

## Test Doubles

**Naming Convention:**
- `Fake*` — stateful behavior fakes with internal state (queues, lists, dicts): `FakeCategoryKnowledgeStore`, `FakeHttpClientFactory`, `FakeContentVideoStore`, `FakeScryfallRestClientFactory`, `FakeResiliencePipelineProvider`, `FakeFeatureFlagCache`.
- `Stub*` — queue-driven stubs that return pre-enqueued responses: `StubHttpMessageHandler` (records requests, dequeues responses, optionally throws).
- `Throwing*` — exception injection doubles: `ThrowingCardSearchService`.

**Location:** `DeckFlow.Web.Tests/TestDoubles/` for shared doubles; inline in test file for test-local doubles.

**Examples:**

`StubHttpMessageHandler` (`DeckFlow.Web.Tests/TestDoubles/StubHttpMessageHandler.cs:10-40`):
```csharp
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    public IList<RecordedRequest> RecordedRequests { get; } = new List<RecordedRequest>();
    public int CallCount => RecordedRequests.Count;
    public Exception? NextException { get; set; }

    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RecordedRequests.Add(new RecordedRequest(request.RequestUri, request.Method.Method));
        
        if (NextException is not null)
        {
            var ex = NextException;
            NextException = null;
            throw ex;
        }

        var response = _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.NotFound);

        return Task.FromResult(response);
    }
}
```

`FakeCategoryKnowledgeStore` (inline test double in `AdminFeedbackControllerTests.cs:106-120`):
```csharp
private sealed class FakeStore : IFeedbackStore
{
    public List<FeedbackItem> Items { get; } = new();
    public List<(long Id, FeedbackStatus Status)> StatusUpdates { get; } = new();
    public List<long> Deletes { get; } = new();

    public Task<long> AddAsync(FeedbackSubmission s, FeedbackRequestContext c, CancellationToken ct = default) => Task.FromResult(0L);
    public Task<FeedbackItem?> GetAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
    public Task<IReadOnlyList<FeedbackItem>> ListAsync(FeedbackListQuery query, CancellationToken ct = default)
    {
        var filtered = Items.AsEnumerable();
        if (query.Status.HasValue) filtered = filtered.Where(i => i.Status == query.Status.Value);
        if (query.Type.HasValue) filtered = filtered.Where(i => i.Type == query.Type.Value);
        return Task.FromResult<IReadOnlyList<FeedbackItem>>(filtered.ToList());
    }
}
```

## Test Seam Pattern

**Core Pattern:**
Every service that touches HTTP, persistence, or external dependencies exposes an `internal` test-compatible constructor that accepts optional `Func<...>` override delegates.

**Example:** `ScryfallCardLookupService` (`DeckFlow.Web/Services/CardLookupService.cs:56-95`)

Production ctor (implicit via DI):
```csharp
public ScryfallCardLookupService(
    IScryfallRestClientFactory scryfallRestClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider)
{
    // production wiring
}
```

Internal test ctor (exposed via `[InternalsVisibleTo("DeckFlow.Web.Tests")]`):
```csharp
internal ScryfallCardLookupService(
    IScryfallRestClientFactory scryfallRestClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider,
    CardLookupCache? cache = null,
    RestClient? restClientOverride = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeAsyncOverride = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsyncOverride = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCard>>>? executeNamedAsyncOverride = null,
    Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallRulingsResponse>>>? executeRulingsAsyncOverride = null)
{
    ArgumentNullException.ThrowIfNull(scryfallRestClientFactory);
    ArgumentNullException.ThrowIfNull(pipelineProvider);
    _cache = cache ?? new CardLookupCache();
    var pipeline = pipelineProvider.GetPipeline<RestResponse>("scryfall") ?? ResiliencePipeline<RestResponse>.Empty;
    var client = restClientOverride ?? scryfallRestClientFactory.Create();
    _executeAsync = executeAsyncOverride ?? ((request, cancellationToken) =>
        ScryfallThrottle.ExecuteAsync(
            token => pipeline.ExecuteAsync(
                async pollyCt => await client.ExecuteAsync<ScryfallCollectionResponse>(request, pollyCt).ConfigureAwait(false),
                token).AsTask(),
            cancellationToken));
    // ... similar for search, named, rulings
}
```

**Test Usage:**
```csharp
var service = TestServiceFactory.CreateScryfallCardLookupService(
    executeAsync: (request, _) => Task.FromResult(CreateCollectionResponse(...)),
    executeSearchAsync: (request, _) => Task.FromResult(new RestResponse<ScryfallSearchResponse>(...)));

var result = await service.LookupAsync("Sol Ring");
```

**InternalsVisibleTo:**
- Configured in `DeckFlow.Web/AssemblyInfo.cs:3`: `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]`
- Allows test project to call `internal` ctors and access `internal` test doubles without leaking to external consumers.

## Fixtures and Factories

**Test Data:**
- Use `TestServiceFactory` for common service construction patterns (`DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs`).
- Inline helpers for test-specific setup (e.g., `CreateCollectionResponse` in `CardLookupServiceTests.cs:268-278`).
- Seed helpers for database tests (e.g., `InsertSourceAsync`, `InsertVideoWithTranscriptAsync` in `ContentVideoStoreDistillTests.cs`).

**Location:**
- Shared: `DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs` (routes all service construction through internal ctors).
- Per-test: inline private helpers or `IDisposable` setup in test class.

**Example Seed Helper:**
```csharp
private async Task<long> InsertSourceAsync(string slug)
    => await _sourceStore.InsertSourceAsync(
        slug,
        $"Source {slug}",
        ContentSourceType.Youtube,
        $"https://example.test/{slug}");
```

## Coverage

**Requirements:** None enforced by build.

**View Coverage:**
```bash
dotnet test --collect:"XPlat Code Coverage"
```

**Tool:** coverlet.collector 6.0.4 (integrated into `DeckFlow.Core.Tests` csproj).

## Test Types

**Unit Tests:**
- Scope: Single class or function, mocked dependencies.
- Examples: `CardLookupServiceTests`, `DiffEngineTests`, `CategoryKnowledgeRepositoryTests`.
- All tests in `DeckFlow.Web.Tests` and `DeckFlow.Core.Tests` are unit tests with mocked/faked externals.

**Integration Tests:**
- Scope: Real or containerized externals (databases, HTTP services).
- Subfolders: `DeckFlow.Web.Tests/Integration/` (e.g., `PostgresContainerFixture.cs` with Testcontainers.PostgreSql 3.10.0, `ScryfallTaggerCookieReplayTests.cs` with real SocketsHttpHandler).
- Marked with `[Collection(..., DisableParallelization = true)]` if they mutate process-wide state (e.g., env vars).

**E2E Tests:**
- Not used. Test coverage focuses on unit + integration layers.

## Common Patterns

**Async Testing:**
```csharp
[Fact]
public async Task LookupAsync_PreservesQuantities_AndCollectsMissingLines()
{
    var service = TestServiceFactory.CreateScryfallCardLookupService(
        executeAsync: (request, _) => Task.FromResult(...));

    var result = await service.LookupAsync("1 Sol Ring");

    Assert.Single(result.VerifiedOutputs);
}
```

**Error Testing:**
```csharp
[Fact]
public async Task LookupAsync_ThrowsHttpRequestException_WhenScryfallFails()
{
    var service = TestServiceFactory.CreateScryfallCardLookupService(
        executeAsync: (request, _) => Task.FromResult(new RestResponse<ScryfallCollectionResponse>(request)
        {
            StatusCode = HttpStatusCode.ServiceUnavailable
        }));

    var exception = await Assert.ThrowsAsync<HttpRequestException>(() => service.LookupAsync("Sol Ring"));

    Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
}
```

**Theory Tests (Parameterized):**
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
        var nullException = await Assert.ThrowsAsync<ArgumentNullException>(() => store.GetCategoriesAsync(cardName!));
        Assert.Equal("cardName", nullException.ParamName);
        return;
    }

    var valueException = await Assert.ThrowsAsync<ArgumentException>(() => store.GetCategoriesAsync(cardName!));
    Assert.Equal("cardName", valueException.ParamName);
}
```

**Disposable Setup/Teardown:**
```csharp
public sealed class CategoryKnowledgeRepositoryTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _tempDirectory;

    public CategoryKnowledgeRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "DeckFlow.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _databasePath = Path.Combine(_tempDirectory, "category-knowledge.db");
    }

    public void Dispose()
    {
        if (File.Exists(_databasePath))
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Delete(_databasePath);
        }
    }

    [Fact]
    public async Task AddDeckIdsAsync_DoesNotRequeueRecentlyProcessedDeck()
    {
        var repository = CreateRepository();
        // test body
    }
}
```

## Content KB Testing Additions (v1.4)

**Dual-Dialect Support:**
- Tests use temporary SQLite databases; Postgres support tested via `Testcontainers.PostgreSql 3.10.0`.
- Example: `ContentVideoStoreDistillTests.cs` creates temp SQLite for distill operations.

**LLM Distillation Service Tests:**
- Mock `ILlmDistillationService` via `Func<...>` override delegates.
- Example: `CommandRunnerHarvestTests.cs` tests harvest command with `FakeWhisperSpendLedger`, `FakeContentVideoStore`, `FakeTranscriptSource`.

**Spend Ledger Tests:**
- Verify cost tracking and per-video cost calculations.
- Use `FakeWhisperSpendLedger` with `Records` list for assertion.

**CLI Command Runner Tests:**
- Test orchestration seams (harvest, probe, export commands).
- Location: `DeckFlow.Core.Tests/CommandRunnerHarvestTests.cs`.
- Pattern: stateful fakes capture side effects (ledger records, video inserts, status updates).

---

*Testing analysis: 2026-05-29*
