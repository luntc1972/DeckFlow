# Coding Conventions

**Analysis Date:** 2026-05-29

## Naming Patterns

**Files:**
- One public type per `.cs` file; filename matches the type name exactly (`CardLookupService.cs` contains `ScryfallCardLookupService`).
- Interface and implementation often co-located in the same file (e.g., `ICardLookupService` + `ScryfallCardLookupService` + result records all in `CardLookupService.cs`).
- Test files: mirror source type with `Tests` suffix (`CardLookupService.cs` → `CardLookupServiceTests.cs`).
- Razor views: PascalCase `.cshtml` (e.g., `CommanderCategories.cshtml`); shared partials prefixed `_` (e.g., `_ViewImports.cshtml`).
- TypeScript in `wwwroot/ts/`: kebab/dot lowercase to match emitted JS bundles.

**Functions:**
- Async methods always end in `Async` (`LookupAsync`, `FindCombosAsync`, `GetCategoriesAsync`).
- Internal/private helpers: PascalCase too (`FormatCard`, `NormalizeName`, `ExtractMechanicNames`).
- Parameters and locals: `camelCase`.
- All async methods take an optional `CancellationToken cancellationToken = default` as the **last** parameter.

**Variables:**
- Private instance fields: `_camelCase` with leading underscore (`_executeAsync`, `_logger`, `_httpClientFactory`).
- Static readonly fields: `PascalCase` (`MinInterval`, `RetryAfterCap`, `Gate`, `QuantityPrefixRegex`).
- Constants: `PascalCase` (`CollectionBatchSize`, `MaxCardsPerSubmission`, `ApiUrl`, `MaxIncluded`).
- Locals and parameters: `camelCase`.

**Types:**
- Classes: `PascalCase`, prefer `sealed` on leaf types (e.g., `public sealed class ScryfallCardLookupService`).
- Interfaces: `I` prefix, PascalCase (`ICardLookupService`, `ICommanderSpellbookService`).
- Records: `sealed record` for immutable DTOs with `init`/`required` properties (e.g., `public sealed record DeckEntry`, `public sealed record CardLookupResult(IReadOnlyList<string> VerifiedOutputs, IReadOnlyList<string> MissingLines)`).
- Test classes: `public sealed class XxxTests`.
- Test doubles: `Fake*` for stateful behavior fakes (`FakeCategoryKnowledgeStore`, `FakeHttpClientFactory`), `Stub*` for queue-driven stubs (`StubHttpMessageHandler`), `Throwing*` for exception injection (`ThrowingCardSearchService`).

## Code Style

**Formatting:**
- 4-space indentation in C#; 2-space in `.json` config.
- Allman braces (open brace on new line) throughout C#.
- File-scoped namespaces: `namespace DeckFlow.Web.Services;` (never block-scoped).
- One `using` directive per line, sorted with `System.*` first, then third-party, then `DeckFlow.*`.
- No global Using include in `DeckFlow.Web` (uses `ImplicitUsings=enable` instead); `DeckFlow.Core.Tests.csproj` adds `<Using Include="Xunit" />`.

**Project Settings:**
- `<TargetFramework>net10.0</TargetFramework>`
- `<Nullable>enable</Nullable>` — nullable reference types enforced everywhere.
- `<ImplicitUsings>enable</ImplicitUsings>` — `System`, `System.Linq`, `System.Threading.Tasks` etc. implicit.
- `<GenerateDocumentationFile>true</GenerateDocumentationFile>` with `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` to suppress missing-doc warnings on partials.

**Formatting Enforcement:**
- NO external linter (no Roslyn analyzers, no StyleCop config).
- Compiler warnings + nullable diagnostics are the gate.
- Do NOT run "Format Document" / "Code Cleanup" / ReSharper-style reformatting across existing files. Project style is pinned in `.editorconfig` + `.gitattributes`; any deviation is the formatter being wrong. Specifically:
  - Never auto-convert `{ get; init; }` to `{ get; }` (System.Text.Json silently skips get-only properties in .NET 9+).
  - Never inline `[Attribute]` onto the property line.
  - Never re-indent C# raw-string literals (changes the literal value).
  - Preserve switch expressions.
  - Preserve LF line endings.

## Import Organization

- Sorted by: `System.*` imports first, third-party imports second, `DeckFlow.*` imports last.
- One import per line, no `using static` or wildcard imports.
- No global Using configuration in Web project (ImplicitUsings handles it).

## Error Handling

**Argument Validation:**
- At the top of constructors: `ArgumentNullException.ThrowIfNull(...)`.
- Example: `CommanderSpellbookService` ctor (`DeckFlow.Web/Services/CommanderSpellbookService.cs:74-76`).

**HTTP Error Translation:**
- Non-2xx upstream responses throw `HttpRequestException` with upstream status code preserved (`DeckFlow.Web/Services/CardLookupService.cs:124-127`).
- Centralized upstream-error messaging: `UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)` produces user-facing strings; controllers return 503 with this body.

**Controllers:**
- Catch broad `Exception` at action boundary, log it, re-render view with `ErrorMessage` populated on view model.
- Distinguish `OperationCanceledException` (timeout copy) from generic failures.

**Services:**
- Graceful degradation: `CommanderSpellbookService.FindCombosAsync` returns `null` on API failure rather than throwing; prompt builder continues without combo data.
- Cancellation-token timeouts wrap the request token: `CancellationTokenSource.CreateLinkedTokenSource(HttpContext?.RequestAborted ...).CancelAfter(LookupTimeout)`.
- Throw guards for upstream HTTP families centralized in helper: `ScryfallThrottle.ThrowIfUpstreamUnavailable(HttpStatusCode)` raises `HttpRequestException` for 429 and 5xx.

## Logging

**Framework:** `ILogger<T>` via constructor injection.

**Patterns:**
- Use **structured templates** with named placeholders, never string interpolation: `_logger.LogInformation("Processing {CardName} from {SourceId}", cardName, sourceId)`.
- Default `ILogger<T>` parameter to optional/nullable in services, fall back to `NullLogger<T>.Instance` so tests don't have to wire one (`CommanderSpellbookService.cs:81`; `DeckFlow.Web.Tests` tests use `NullLogger<DeckController>.Instance`).
- File sink rolls daily, `retainedFileCountLimit: 14`, output under `<ContentRoot>/logs/web-.log` (configured in `DeckFlow.Web/Program.cs:34-47`).
- Console sink stays enabled in production so platforms like Render/Fly capture stdout.
- Request logging via `app.UseSerilogRequestLogging();` in the middleware pipeline (`Program.cs:210`).

## Comments

**XML Doc Comments:**
- On every public type, interface, public method, and public record.
- `<param>` / `<returns>` tags on non-trivial methods.
- Example: `CardLookupService.cs:13-26` shows proper doc structure.

**Inline Comments:**
- Explain **why**, not what.
- Decision/risk markers like `D-01`, `D-06`, `HIGH-2`, `B2` reference plan/CONTEXT documents (e.g., `Program.cs:58-62`).

## Function Design

**Size and Scope:**
- Service classes are larger (200-500+ LOC) but methods stay focused.
- Long methods are usually a single async pipeline (e.g., `LookupAsync` at ~90 LOC orchestrates parse → batched fetch → fallback → format).
- Helpers extracted to private `static` methods when pure (no `this` access): `NormalizeName`, `FormatCard`, `Chunk`, `ExtractMechanicNames` (`CardLookupService.cs`).

**Parameters and Returns:**
- All async methods take `CancellationToken cancellationToken = default` as the **last** parameter.
- Use `IReadOnlyList<T>` / `IReadOnlyDictionary<TK,TV>` on result records and method parameters; never expose mutable `List<T>` on public surface.
- Prefer `record`/`sealed record` for multi-value results (`CardLookupResult`, `CommanderSpellbookResult`).
- Use nullable return (`Task<T?>`) to indicate "operation succeeded but no match"; throw for upstream/system errors.
- For collection returns use `IReadOnlyList<T>`; for "nothing found" return `Array.Empty<T>()` not `null`.

**Async/Await:**
- Always use `.ConfigureAwait(false)` in library code (even though ASP.NET typically doesn't require it, it's a defensive habit).
- Example: `CardLookupService.cs:121` uses `.ConfigureAwait(false)` throughout.

## Module Design

**Visibility:**
- `public` for surface that crosses project boundaries (controllers, services consumed by DI, view models, core models).
- `internal` for test doubles (`StubHttpMessageHandler`, `FakeHttpClientFactory`, `FakeScryfallRestClientFactory`) so they stay scoped to the test assembly.
- `internal` constructor used for test seams + `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` to grant test project access without leaking to consumers.

**Static Helpers:**
- `static` classes for stateless helpers (`ScryfallThrottle`, `MoxfieldApiUrl`, `ArchidektApiUrl`, `CardNormalizer`).

**Test Seam Pattern:**
- Prefer `internal` ctor with optional `Func<...> override` delegates injected so tests bypass live HTTP without mocking `IHttpClientFactory`.
- Production constructor takes DI-resolved dependencies; internal test ctor is exposed via `[InternalsVisibleTo("DeckFlow.Web.Tests")]` in `DeckFlow.Web/AssemblyInfo.cs:3`.
- Example: `CardLookupService.cs:56-95` shows both public production ctor (implicit, uses `Program.cs` DI) and internal test ctor with four override delegates.

## Dependency Injection Conventions

**Registration Location:** `DeckFlow.Web/Program.cs:50-189`

**Lifetime Guidelines:**
- `AddSingleton` for read-mostly caches (`IMemoryCache`, `TaggerSessionCache`).
- `AddScoped` for per-request services (`IDeckSyncService`, `ICategorySuggestionService`).
- `AddHostedService` for background work (`ArchidektCacheJobService`).
- Singleton facade for hosted services so controllers can call into them.

**Patterns:**
- All registrations in one place; no DI extension methods except `AddDeckFlowResiliencePipelines()` and `UseDeckFlowSecurityHeaders()`.

## HTTP / Resilience Conventions

**Named Clients:** `IHttpClientFactory` named clients configured once in `Program.cs:63-89`:
- `commander-banlist`
- `commander-spellbook`
- `scryfall-rest`
- Typed client: `ScryfallTaggerHttpClient`

**HTTP Egress Pattern:**
- All external HTTP calls flow through **RestSharp** (`RestClient` wrapping the factory's `HttpClient`) plus **Polly v8** `ResiliencePipeline<RestResponse>`.
- Services do not `new HttpClient()`; they receive `IHttpClientFactory` (or typed client) plus `ResiliencePipelineProvider<string>` and resolve a named pipeline.
- Resilience pipelines registered as named `ResiliencePipeline<RestResponse>` (banlist, spellbook, tagger, tagger-post, scryfall) via `ResiliencePipelineFactory.cs`.
- Consumers resolve by string name via `ResiliencePipelineProvider<string>` (NOT keyed services).

**Scryfall Rate Limiting:**
- Static throttle gate `ScryfallThrottle.ExecuteAsync` wraps every Scryfall call to enforce ~5 req/s pacing across the whole process.
- This is a **global constraint** — do not bypass it for Scryfall callers.

---

*Convention analysis: 2026-05-29*
