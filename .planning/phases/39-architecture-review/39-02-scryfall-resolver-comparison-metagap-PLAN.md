---
phase: 39-architecture-review
plan: 02
type: execute
wave: 2
depends_on: ["39-01"]
files_modified:
  - DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs
  - DeckFlow.Web/Services/DeckComparisonService.cs
  - DeckFlow.Web/Services/MetaGapService.cs
  - DeckFlow.Web/Program.cs
  - DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs
autonomous: true
requirements: [ARCH-02]

must_haves:
  truths:
    - "Deck Comparison and Meta-Gap packets resolve Scryfall card references and per-card fuzzy fallbacks byte-identically to before."
    - "Comparison and Meta-Gap no longer each own a private SearchFallbackCardAsync; both route through the injected IScryfallCardResolver."
    - "Scryfall traffic still routes through ScryfallThrottle and the named 'scryfall' Polly pipeline unchanged."
    - "The internal Func<RestRequest,...> test seams for Comparison/Meta-Gap now live on the resolver's internal test ctor."
  artifacts:
    - path: "DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs"
      provides: "IScryfallCardResolver: collection batch fetch + simple (cards/search !\"name\") fuzzy fallback + name normalization; routes through ScryfallThrottle + scryfall pipeline"
      contains: "IScryfallCardResolver"
    - path: "DeckFlow.Web/Services/DeckComparisonService.cs"
      provides: "Comparison consuming IScryfallCardResolver; private SearchFallbackCardAsync + Scryfall Func seams deleted"
    - path: "DeckFlow.Web/Services/MetaGapService.cs"
      provides: "MetaGap consuming IScryfallCardResolver; private SearchFallbackCardAsync + Scryfall Func seams deleted"
  key_links:
    - from: "DeckFlow.Web/Services/DeckComparisonService.cs"
      to: "IScryfallCardResolver"
      via: "injected resolver"
      pattern: "_scryfallCardResolver\\."
    - from: "DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs"
      to: "ScryfallThrottle + scryfall pipeline"
      via: "preserved execute delegate"
      pattern: "ScryfallThrottle\\.ExecuteAsync"
---

<objective>
Extract the Scryfall card-reference hydration shared by Deck Comparison and Meta-Gap into a single injected `IScryfallCardResolver` (D-04). These two services have a BYTE-IDENTICAL `SearchFallbackCardAsync` and near-identical collection-batch + oracle-name-map plumbing; consolidate them first.

Purpose: Remove the duplicated Scryfall transport/fallback/normalization and the duplicated `Func<RestRequest,...>` ctor plumbing from two of the three Scryfall-touching packet services, while preserving exact request shapes, throttle/pipeline routing, and the canonical internal test-seam pattern.

Output: New `ScryfallCardResolver` (+ interface) in `Services/Scryfall/`; Comparison + Meta-Gap consume it; their private `SearchFallbackCardAsync` and Scryfall `Func` seams are deleted and MOVED onto the resolver's internal test ctor; DI + TestServiceFactory updated.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/phases/39-architecture-review/39-CONTEXT.md
@.planning/phases/39-architecture-review/39-01-SUMMARY.md
@./CLAUDE.md

<key_findings>
**Comparison `SearchFallbackCardAsync` and MetaGap `SearchFallbackCardAsync` are byte-identical** (D-04 confirmed): single `RestRequest("cards/search", Method.Get)` with `q = $"!\"{normalizedName}\""`, `unique=cards`, `order=name`; on 2xx return `response.Data?.Data.FirstOrDefault()`; on 404 return null; else throw `HttpRequestException($"Scryfall fallback lookup failed while resolving {cardName} with HTTP {(int)response.StatusCode}.", null, response.StatusCode)`.

**Their collection-batch loops differ in OUTPUT SHAPE but share the transport pattern:**
  - Comparison `LookupCardDetailsAsync` returns `(IReadOnlyList<ScryfallCard> Cards, IReadOnlyDictionary<string,string> OracleNameMap)` and ADDS fallback cards to `resolvedCards`.
  - MetaGap `ResolveOracleNameMapAsync` returns ONLY the oracle-name map (does NOT collect resolved cards) and takes a different input (my-deck names ∪ reference-deck names).
  Both: chunk by `ScryfallBatchSize=75`, POST `cards/collection` with `identifiers = chunk.Select(name => new { name })`, on non-2xx/null throw an HttpRequestException with a SERVICE-SPECIFIC message, map submitted→resolved names, then per-unresolved-name call `SearchFallbackCardAsync`.

**Design implication:** The resolver owns the LOW-LEVEL primitives, not the service-specific aggregation. Expose:
  - `Task<RestResponse<ScryfallCollectionResponse>> FetchCollectionAsync(IReadOnlyList<object> identifiers, CancellationToken)` OR a typed `Task<IReadOnlyList<ScryfallCard>> FetchCollectionBatchAsync(IEnumerable<string> names, CancellationToken)` that performs ONE batch POST (no chunking) and returns the raw `response` so the caller keeps its own error-message + mapping. Prefer returning the raw `RestResponse` so each service's distinct error message + null-check stays byte-identical.
  - `Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken)` — the shared byte-identical fallback (NOTE: the error message is identical between the two, so it moves verbatim).
  Keep the chunking + oracle-map aggregation IN each service (it differs), but route the actual HTTP through the resolver. This keeps every produced byte identical while removing the duplicated transport + fallback + Func plumbing.

Do NOT try to also fold Analysis's fallback in here — Analysis has a DIFFERENT 3-stage fallback (printed:/name: + cards/named fuzzy + NormalizeLookupName/NormalizeForScryfall). That is Plan 03.
</key_findings>

<interfaces>
From DeckFlow.Web/Services/DeckComparisonService.cs (current seam, to MOVE onto resolver):
```csharp
private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>> _executeCollectionAsync;
private readonly Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>> _executeSearchAsync;
// built from: IScryfallRestClientFactory.Create() + pipelineProvider.GetPipeline<RestResponse>("scryfall")
//   wrapped in ScryfallThrottle.ExecuteAsync(token => pipeline.ExecuteAsync(... client.ExecuteAsync<T>(request, ct) ...), ct)
private const int ScryfallBatchSize = 75;  // Comparison uses 75; MetaGap also 75
```
NOTE: Comparison declares `ScryfallBatchSize = 75`; MetaGap also 75. (Comparison's local const comment says 75; the `cards/collection` Scryfall hard cap is 75.)

From TestServiceFactory.cs (current Scryfall seam params on Comparison/MetaGap create methods):
```csharp
Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsync = null,
Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsync = null
```
These must keep flowing — but now to the resolver's internal ctor, not the service's.
</interfaces>

@DeckFlow.Web/Services/DeckComparisonService.cs
@DeckFlow.Web/Services/MetaGapService.cs
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create IScryfallCardResolver owning collection-batch HTTP + shared fuzzy fallback + name normalization</name>
  <files>DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs, DeckFlow.Web/Program.cs</files>
  <action>
Create `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` (one public type per file: the `IScryfallCardResolver` interface co-located with the `sealed` impl, per project convention). Namespace `DeckFlow.Web.Services.Scryfall` (a new sub-namespace — `Services/Scryfall/` does not yet exist; create the folder). The interface exposes:
  - `Task<RestResponse<ScryfallCollectionResponse>> ExecuteCollectionAsync(RestRequest request, CancellationToken cancellationToken);` — the single batch POST execute, wrapped in ScryfallThrottle + the named "scryfall" Polly pipeline EXACTLY as the current `_executeCollectionAsync` delegate does. Returns the raw response so callers keep their own status/null handling + service-specific error messages byte-identical.
  - `Task<ScryfallCard?> SearchFallbackCardAsync(string cardName, CancellationToken cancellationToken);` — the shared byte-identical fallback moved verbatim from Comparison/MetaGap (the `q=!"name"`, `unique=cards`, `order=name`, 2xx→FirstOrDefault, 404→null, else throw with the identical message). This internally uses the search-execute delegate.

Constructor design (mirror the canonical seam pattern, CLAUDE.md):
  - PUBLIC DI ctor: `ScryfallCardResolver(IScryfallRestClientFactory scryfallRestClientFactory, ResiliencePipelineProvider<string> pipelineProvider)` — builds the RestClient + resolves `GetPipeline<RestResponse>("scryfall") ?? ResiliencePipeline<RestResponse>.Empty` and constructs the two execute delegates wrapped in `ScryfallThrottle.ExecuteAsync(...)` (copy the exact delegate bodies currently in DeckComparisonService ctor lines ~115-126).
  - INTERNAL test ctor: accepts `Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallCollectionResponse>>>? executeCollectionAsyncOverride = null` and `Func<RestRequest, CancellationToken, Task<RestResponse<ScryfallSearchResponse>>>? executeSearchAsyncOverride = null` plus the same DI deps with `RestClient? restClientOverride = null`. This is where the moved seams live. Add `[assembly: InternalsVisibleTo("DeckFlow.Web.Tests")]` already covers it (AssemblyInfo.cs:3).
  - `ArgumentNullException.ThrowIfNull` on the DI deps (project error-handling convention).

Register in Program.cs: `builder.Services.AddSingleton<IScryfallCardResolver>(sp => new ScryfallCardResolver(sp.GetRequiredService<IScryfallRestClientFactory>(), sp.GetRequiredService<ResiliencePipelineProvider<string>>()));` (Singleton matches the other Scryfall services; the resolver holds no per-request state). Place it near the other Scryfall registrations (~Program.cs:175-280). Add the `using DeckFlow.Web.Services.Scryfall;` to Program.cs.

Do NOT migrate any service yet — this task only adds the new file + registration so the build stays green as a standalone commit.
  </action>
  <acceptance_criteria>
    - `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` exists with `IScryfallCardResolver` + sealed impl.
    - Public DI ctor + internal test ctor with the two `Func<RestRequest,...>` overrides.
    - `SearchFallbackCardAsync` body is byte-identical to the current Comparison/MetaGap version (same query params, same 404→null, same throw message).
    - Routes through `ScryfallThrottle.ExecuteAsync` + `GetPipeline<RestResponse>("scryfall")`.
    - Registered in Program.cs; solution builds clean.
  </acceptance_criteria>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/deckflow && grep -c "ScryfallThrottle.ExecuteAsync" DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs  # expect >=1</automated>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln 2>&1 | grep -E "error|warning" | grep -v "^$" | wc -l  # expect 0</automated>
  </verify>
  <done>Resolver added + registered, build clean. Commit: `refactor(39): add IScryfallCardResolver (collection batch + shared fuzzy fallback)`.</done>
</task>

<task type="auto">
  <name>Task 2: Migrate Comparison + Meta-Gap onto IScryfallCardResolver; delete their Scryfall Func seams + private SearchFallbackCardAsync</name>
  <files>DeckFlow.Web/Services/DeckComparisonService.cs, DeckFlow.Web/Services/MetaGapService.cs, DeckFlow.Web/Program.cs, DeckFlow.Web.Tests/TestDoubles/TestServiceFactory.cs</files>
  <action>
**Comparison:**
  - Inject `IScryfallCardResolver` (new ctor param); remove the `_executeCollectionAsync` + `_executeSearchAsync` fields, their override ctor params (`executeCollectionAsyncOverride`, `executeSearchAsyncOverride`), `restClientOverride`, the `IScryfallRestClientFactory` + `ResiliencePipelineProvider<string>` ctor params, and the delegate-construction block — UNLESS the factory/provider are used elsewhere (grep confirms Comparison uses them ONLY for the two delegates → remove both).
  - In `LookupCardDetailsAsync`: keep the chunk loop + oracle-map aggregation + the SERVICE-SPECIFIC error message + the `resolvedCards` collection UNCHANGED, but replace `await _executeCollectionAsync(request, ct)` with `await _scryfallCardResolver.ExecuteCollectionAsync(request, ct)` and replace `await SearchFallbackCardAsync(unresolvedName, ct)` with `await _scryfallCardResolver.SearchFallbackCardAsync(unresolvedName, ct)`. DELETE the private `SearchFallbackCardAsync`.

**Meta-Gap:**
  - Same injection + field/param removal.
  - In `ResolveOracleNameMapAsync`: keep the chunk loop + its service-specific error message UNCHANGED; replace the collection execute + the fallback call with resolver calls. DELETE the private `SearchFallbackCardAsync`.

**Program.cs:** add `sp.GetRequiredService<IScryfallCardResolver>()` to the Comparison + MetaGap factory new-expressions; remove the now-removed `IScryfallRestClientFactory` / `ResiliencePipelineProvider<string>` args from those two factories.

**TestServiceFactory:** the `CreateDeckComparisonService` / `CreateMetaGapService` methods currently accept `executeCollectionAsync` / `executeSearchAsync`. Keep those PARAMETERS (so existing tests compile unchanged), but route them into a `ScryfallCardResolver` built via its internal test ctor, and pass that resolver into the service. I.e. construct `new ScryfallCardResolver(<fake factory/provider the factory already builds>, executeCollectionAsyncOverride: executeCollectionAsync, executeSearchAsyncOverride: executeSearchAsync)` and inject it. This preserves the existing test call sites (`DeckComparisonServiceTests`, `MetaGapServiceTests` pass these Funcs) with ZERO test-file edits.

Preserve: `ScryfallBatchSize` const stays in each service (it governs the service's chunking, which stays in the service). Do NOT touch cache-key helpers (D-06) or `PromptBuilders/**` (D-07).
  </action>
  <acceptance_criteria>
    - `grep -c "private async Task<ScryfallCard?> SearchFallbackCardAsync" DeckFlow.Web/Services/DeckComparisonService.cs DeckFlow.Web/Services/MetaGapService.cs` → both `:0`.
    - Neither service declares `_executeCollectionAsync` / `_executeSearchAsync` fields anymore.
    - Both services reference `_scryfallCardResolver.ExecuteCollectionAsync` and `_scryfallCardResolver.SearchFallbackCardAsync`.
    - Program.cs injects `IScryfallCardResolver` into both factories.
    - `DeckComparisonServiceTests` / `MetaGapServiceTests` source unchanged (seam Funcs still accepted by TestServiceFactory).
  </acceptance_criteria>
  <verify>
    <automated>cd /mnt/c/users/chrislunt/source/personal/deckflow && grep -c "private async Task<ScryfallCard?> SearchFallbackCardAsync" DeckFlow.Web/Services/DeckComparisonService.cs DeckFlow.Web/Services/MetaGapService.cs  # expect both :0</automated>
    <automated>cd /mnt/c/users/chrislunt/source/personal/deckflow && grep -c "_scryfallCardResolver" DeckFlow.Web/Services/DeckComparisonService.cs DeckFlow.Web/Services/MetaGapService.cs  # expect both >=2</automated>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln 2>&1 | grep -E "error|warning" | grep -v "^$" | wc -l  # expect 0</automated>
  </verify>
  <done>Comparison + MetaGap consume the resolver; their Scryfall seams + private fallback deleted; build clean. Commit: `refactor(39): migrate Comparison + MetaGap onto IScryfallCardResolver`.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| app → Scryfall API | Outbound HTTP. Unchanged: same throttle + pipeline + request shapes. |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-39-02 | Tampering | Scryfall request-shape / fallback-message drift | mitigate | SearchFallbackCardAsync moved verbatim (byte-identical); chunking + service-specific error messages stay in each service; existing suites assert output text |
| T-39-03 | Denial of Service | Throttle/pipeline bypass | mitigate | Resolver wraps every call in `ScryfallThrottle.ExecuteAsync` + named "scryfall" pipeline (grep gate); no per-call pipeline construction |
| T-39-SC | Tampering | npm/pip/cargo installs | accept | No package installs; no new deps |
</threat_model>

<verification>
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` → 0 errors, 0 new warnings.
- CI runs `DeckComparisonServiceTests`, `MetaGapServiceTests`, `AiPlatformPhase10RoundTripTests`, `ResultContractTests` (VSTest unreliable in WSL → push-and-watch CI is the test proof).
- Grep gates: zero private `SearchFallbackCardAsync` in Comparison/MetaGap; resolver routes through `ScryfallThrottle.ExecuteAsync`.
</verification>

<success_criteria>
- Comparison + Meta-Gap resolve Scryfall references via the injected `IScryfallCardResolver`; their duplicated `SearchFallbackCardAsync` + `Func<RestRequest,...>` seams are deleted and the seams live on the resolver's internal test ctor.
- Scryfall traffic still routes through ScryfallThrottle + the named pipeline; request shapes + fallback message byte-identical.
- `dotnet build` clean; no cache-key (D-06) or PromptBuilders (D-07) edits; existing suites green in CI.
</success_criteria>

<output>
Create `.planning/phases/39-architecture-review/39-02-SUMMARY.md` when done. Record: the exact resolver contract chosen (raw-response vs typed), which ctor params were removed from each service, and confirmation that TestServiceFactory preserved the existing test seam params (no test-file edits required).
</output>
