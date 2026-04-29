# Coding Conventions

**Analysis Date:** 2026-04-29

## Naming Patterns

**Files:**
- One public type per `.cs` file; file name matches the type name exactly (`CardLookupService.cs` contains `ScryfallCardLookupService`).
- Interface and implementation often co-located in the same file (e.g., `ICardLookupService` + `ScryfallCardLookupService` + result records all live in `DeckFlow.Web/Services/CardLookupService.cs`).
- Test files mirror the source type with a `Tests` suffix: `CardLookupService.cs` → `CardLookupServiceTests.cs`.
- Razor views use PascalCase `.cshtml` (`CommanderCategories.cshtml`); shared partials prefixed `_` (`_ViewImports.cshtml`).
- TypeScript files in `DeckFlow.Web/wwwroot/ts/` use kebab/dot lowercase to match emitted JS bundles.

**Types:**
- Interfaces: `I` prefix, PascalCase (`ICardLookupService`, `ICommanderSpellbookService`, `IScryfallRestClientFactory`).
- Classes: PascalCase, prefer `sealed` on leaf types — see `public sealed class ScryfallCardLookupService` (`DeckFlow.Web/Services/CardLookupService.cs:42`) and `public sealed record DeckEntry` (`DeckFlow.Core/Models/DeckEntry.cs:3`).
- Records used for immutable DTOs / results: `CardLookupResult`, `SingleCardLookupResult`, `SpellbookCombo`, `DeckEntry`. Prefer `sealed record` with `init`/`required` properties.
- Test classes: `public sealed class XxxTests`.
- Test doubles: `Fake*` for stateful behavior fakes (`FakeCategoryKnowledgeStore`, `FakeHttpClientFactory`), `Stub*` for queue-driven stubs (`StubHttpMessageHandler`), `Throwing*` for exception injection (`ThrowingCardSearchService`).

**Methods:**
- PascalCase, async methods always end in `Async` (`LookupAsync`, `FindCombosAsync`, `GetCategoriesAsync`).
- Internal/private helpers PascalCase too (`FormatCard`, `NormalizeName`, `ExtractMechanicNames`).

**Variables / Fields:**
- Private instance fields: `_camelCase` with leading underscore (`_executeAsync`, `_logger`, `_httpClientFactory`).
- Static readonly fields: `PascalCase` (`MinInterval`, `RetryAfterCap`, `Gate`, `QuantityPrefixRegex`).
- Constants: `PascalCase` (`CollectionBatchSize`, `MaxCardsPerSubmission`, `ApiUrl`, `MaxIncluded`).
- Locals and parameters: `camelCase`.

**Namespaces:**
- File-scoped, mirror folder layout: `namespace DeckFlow.Web.Services;`, `namespace DeckFlow.Core.Models;`, `namespace DeckFlow.Web.Tests;`.
- Tests live in a single namespace per project (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`) regardless of subfolder.

## Code Style

**Formatting:**
- No `.editorconfig` or `.prettierrc` checked in — formatting is by convention/IDE defaults.
- 4-space indentation in C#; 2-space in `.json` config.
- Allman braces (open brace on new line) throughout C#.
- File-scoped namespaces (`namespace X;`) — never block-scoped.
- One `using` directive per line, sorted with `System.*` first then third-party then `DeckFlow.*`. No global `Using Include` in `DeckFlow.Web` (uses `ImplicitUsings=enable` instead); `DeckFlow.Core.Tests.csproj` adds `<Using Include="Xunit" />`.

**Project-wide MSBuild settings (every .csproj):**
- `<TargetFramework>net10.0</TargetFramework>`
- `<Nullable>enable</Nullable>` — nullable reference types are enforced everywhere.
- `<ImplicitUsings>enable</ImplicitUsings>` — `System`, `System.Linq`, `System.Threading.Tasks` etc. are implicit.
- `DeckFlow.Web.csproj` adds `<GenerateDocumentationFile>true</GenerateDocumentationFile>` with `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` to suppress missing-doc warnings on partials.

**Linting:**
- No external linter (no Roslyn analyzers, no StyleCop config). Compiler warnings + nullable diagnostics are the gate.

## Import Organization

**Order:**
1. `System.*` namespaces (`using System.Net;`, `using System.Text.Json;`).
2. Microsoft / framework (`using Microsoft.AspNetCore.Mvc;`, `using Microsoft.Extensions.Logging;`).
3. Third-party (`using Polly;`, `using RestSharp;`, `using Xunit;`).
4. First-party `DeckFlow.*` (`using DeckFlow.Core.Models;`, `using DeckFlow.Web.Services;`).

Implicit usings cover most `System.*` entries, so files often start directly with framework or third-party imports — see `CommanderController.cs:1-7`.

**Path Aliases:**
- Not applicable (C#). Project references via `<ProjectReference>` in `.csproj`:
  - `DeckFlow.Web` → `DeckFlow.Core`
  - `DeckFlow.Web.Tests` → `DeckFlow.Web` (which transitively pulls Core)
  - `DeckFlow.Core.Tests` → `DeckFlow.Core`

## Error Handling

**Patterns:**
- **Argument validation at the top of constructors:** `ArgumentNullException.ThrowIfNull(...)` — see `CommanderSpellbookService` ctor (`DeckFlow.Web/Services/CommanderSpellbookService.cs:77-78`) and `FakeHttpClientFactory:11`.
- **HTTP error translation:** non-2xx upstream responses throw `HttpRequestException` with the upstream status code preserved:
  ```csharp
  throw new HttpRequestException(
      $"Scryfall search returned HTTP {(int)response.StatusCode}.",
      null,
      response.StatusCode);
  ```
  (`DeckFlow.Web/Services/CardLookupService.cs:150-154`)
- **Centralized upstream-error messaging:** `UpstreamErrorMessageBuilder.BuildScryfallMessage(exception)` produces user-facing strings; controllers return 503 with this body (`CommanderController.cs:103-110`).
- **Controllers catch broad `Exception`** at the action boundary, log it, and re-render the view with an `ErrorMessage` populated on the view model (`CommanderController.cs:80-88`). They distinguish `OperationCanceledException` (timeout copy) from generic failures.
- **Graceful degradation in services:** `CommanderSpellbookService.FindCombosAsync` returns `null` on API failure rather than throwing, and the prompt builder continues without combo data (see service comments and `CommanderSpellbookServiceTests.FindCombosAsync_ApiFailure_ReturnsNull`).
- **Cancellation-token timeouts** wrap the request token: `CancellationTokenSource.CreateLinkedTokenSource(HttpContext?.RequestAborted ...).CancelAfter(LookupTimeout)` (`CommanderController.cs:55-57`).
- **Throw guards for upstream HTTP families** centralized in helper: `ScryfallThrottle.ThrowIfUpstreamUnavailable(HttpStatusCode)` raises `HttpRequestException` for 429 and 5xx (`ScryfallThrottle.cs:111-121`).

## Logging

**Framework:** Serilog (`Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`) configured in `Program.cs:34-47`. Services and controllers consume `ILogger<T>` from MS.Extensions.Logging abstractions.

**Patterns:**
- Inject `ILogger<TController>` / `ILogger<TService>` via constructor.
- Use **structured templates** with named placeholders, never string interpolation:
  ```csharp
  _logger.LogWarning("Commander category lookup for {Commander} timed out.", trimmed);
  _logger.LogError(exception, "Failed to load commander categories for {Commander}.", trimmed);
  ```
  (`CommanderController.cs:73, 82`).
- Default `ILogger<T>` parameter to optional/nullable in services and fall back to `NullLogger<T>.Instance` so tests don't have to wire one (`CommanderSpellbookService.cs:75, 82`; tests use `NullLogger<DeckController>.Instance`).
- File sink rolls daily, `retainedFileCountLimit: 14`, output under `<ContentRoot>/logs/web-.log`.
- Console sink stays enabled in production so platforms like Render/Fly capture stdout.
- Request logging via `app.UseSerilogRequestLogging();` in the middleware pipeline (`Program.cs:210`).

## Comments

**When to Comment:**
- XML doc comments (`/// <summary>`) on every public type, interface, public method, and public record. `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is on, so missing doc warnings are explicitly suppressed for known noise (`NoWarn` 1591/1573/1587).
- Use `<param>`/`<returns>` tags on non-trivial methods.
- Inline comments explain **why**, not what. Examples:
  - `ScryfallThrottle.cs:13-16`: explains the 200ms pacing choice and Cloudflare burst-detection rationale.
  - `Program.cs:114-127`: explains why `ForwardedHeaders` defaults must be cleared on Render.
- Decision/risk markers like `D-01`, `D-06`, `HIGH-2`, `B2` reference plan/CONTEXT documents (see `Program.cs:58-62, 82-104`).

**JSDoc/TSDoc:** Not applicable — TS code in `wwwroot/ts/` is browser glue, lightly commented.

## Function Design

**Size:**
- Service classes are larger (200-500+ LOC) but methods stay focused. Long methods are usually a single async pipeline (e.g., `LookupAsync` at ~90 LOC orchestrates parse → batched fetch → fallback → format).
- Helpers are extracted to private `static` methods when pure (no `this` access): `NormalizeName`, `FormatCard`, `Chunk`, `ExtractMechanicNames`, `ParseLines` (`CardLookupService.cs`).

**Parameters:**
- All async methods take an optional `CancellationToken cancellationToken = default` as the **last** parameter.
- Use `IReadOnlyList<T>` / `IReadOnlyDictionary<TK,TV>` on result records and method parameters; never expose mutable `List<T>` on public surface.
- Test seam pattern: optional `Func<...> executeAsyncOverride` delegates injected via internal constructor so tests bypass live HTTP without mocking `IHttpClientFactory` (`CardLookupService.cs:106-121`). Production constructor takes the DI-resolved dependencies; internal test ctor is exposed via `[InternalsVisibleTo("DeckFlow.Web.Tests")]` in `DeckFlow.Web/AssemblyInfo.cs:3`.

**Return Values:**
- Prefer `record`/`sealed record` for multi-value results (`CardLookupResult`, `CommanderSpellbookResult`).
- Use nullable return (`Task<T?>`) to indicate "operation succeeded but no match"; throw for upstream/system errors.
- For collection returns use `IReadOnlyList<T>`; for "nothing found" return `Array.Empty<T>()` not `null`.

## Module Design

**Exports:**
- `public` for surface that crosses project boundaries (controllers, services consumed by DI, view models, core models).
- `internal` for test doubles (`StubHttpMessageHandler`, `FakeHttpClientFactory`, `FakeResiliencePipelineProvider`, `FakeScryfallRestClientFactory`) so they stay scoped to the test assembly.
- `internal` constructor used for test seams + `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` to grant the test project access without leaking to consumers.
- `static` classes for stateless helpers (`ScryfallThrottle`, `MoxfieldApiUrl`, `ArchidektApiUrl`, `CardNormalizer`).

**Barrel Files:** None — every type is imported by full namespace.

## Dependency Injection Conventions

- All registrations live in `DeckFlow.Web/Program.cs:50-189`. No DI extension methods except `AddDeckFlowResiliencePipelines()` and `UseDeckFlowSecurityHeaders()`.
- Lifetime guidelines applied:
  - **Singleton** for stateless services and HTTP/Polly factories (`ICommanderSpellbookService`, `ICardLookupService`, `IScryfallRestClientFactory`, `ITaggerSessionCache`).
  - **Scoped** for orchestrators that touch per-request state or call multiple singletons (`IChatGptDeckPacketService`, `IDeckSyncService`, `ICategorySuggestionService`).
  - **Transient** for cheap parsers (`MoxfieldParser`, `ArchidektParser`).
- Hosted background work uses `AddHostedService` plus a singleton facade so controllers can call into it (`ArchidektCacheJobService` registered both as `Singleton` and `HostedService`, `Program.cs:178-180`).

## HTTP / Resilience Conventions

- `IHttpClientFactory` named clients configured in one place (`Program.cs:63-89`) — `commander-banlist`, `commander-spellbook`, `scryfall-rest`, plus a typed client `ScryfallTaggerHttpClient`.
- All external HTTP calls flow through **RestSharp** (`RestClient` wrapping the factory's `HttpClient`) plus **Polly v8** `ResiliencePipeline<RestResponse>` resolved via `ResiliencePipelineProvider<string>` keyed by name (`scryfall`, `spellbook`, ...).
- Static throttle gate `ScryfallThrottle.ExecuteAsync` is wrapped around every Scryfall call to enforce ~5 req/s pacing across the whole process.
- Each HTTP-touching service exposes a public DI ctor and an `internal` test ctor that injects a delegate (`Func<RestRequest, CancellationToken, Task<RestResponse<T>>>`) — this is the canonical test seam (see `CardLookupService.cs:91-121`).

---

*Convention analysis: 2026-04-29*
