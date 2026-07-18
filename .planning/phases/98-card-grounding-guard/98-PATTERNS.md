# Phase 98: Card-Grounding Guard - Pattern Map

**Mapped:** 2026-07-18
**Files analyzed:** 11 (net-new) + 3 (modified)
**Analogs found:** 14 / 14

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `DeckFlow.Core/Knowledge/CardGrounding/ICardGroundingGuard.cs` | service (Core seam) | request-response | `DeckFlow.Core/Knowledge/StatedRulesExtraction/ICardNameGrounder.cs` | exact |
| `DeckFlow.Core/Knowledge/CardGrounding/CardGroundingVerdict.cs` | model | transform | `DeckFlow.Core/Manabase/ManabaseVerdict.cs` (shape) + `DeckFlow.Core/Knowledge/StatedRulesExtraction/ICardNameGrounder.cs` (`CardGroundingResult`, minimal-shape counterexample) | role-match |
| `DeckFlow.Core/Knowledge/CardGrounding/CardGroundingRejectReason.cs` | model (enum) | transform | none directly (no existing reject-reason enum) — pattern: string-tag verdicts in `ConflictCalculator`/`ConflictCalculationResult` | partial |
| `DeckFlow.Core/Knowledge/CardGrounding/CardGroundingDeckContext.cs` | model | transform | `DeckFlow.Core/Knowledge/ProfileFusion/ConflictCalculator.cs` input-record style; `CreatorDeckCacheEntry.cs` (sealed record, `required`/`init`) | role-match |
| `DeckFlow.Core/Knowledge/CardGrounding/CardGroundingRules.cs` | utility (pure decision logic) | transform | `DeckFlow.Core/Knowledge/ProfileFusion/ConflictCalculator.cs` + `DeckFlow.Core/Knowledge/DistillationValidation.cs` | role-match |
| `DeckFlow.Web/Services/Scryfall/CardGroundingGuard.cs` | service (Web impl) | request-response + CRUD-cache | `DeckFlow.Web/Services/Scryfall/ScryfallCardNameGrounder.cs` | exact |
| `DeckFlow.Web/Services/Scryfall/ScryfallErrorResponse.cs` | model (DTO) | transform | `DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs` (`ScryfallCollectionIdentifier`, small response DTOs) | role-match |
| `DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs` (MODIFIED — add `Legalities`) | model (DTO) | transform | itself, existing `ScryfallCard` record | exact |
| `DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs` | service | batch + CRUD-read | `DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs` (batch Scryfall + store-read orchestration) | role-match |
| `DeckFlow.Web/Extensions/ScryfallServiceCollectionExtensions.cs` (MODIFIED — register guard) | config (DI) | request-response | itself, existing `AddDeckFlowScryfallServices` | exact |
| `DeckFlow.Core.Tests/Knowledge/CardGrounding/CardGroundingRulesTests.cs` | test | transform | `DeckFlow.Core.Tests` pure-logic test style (see Shared Patterns) | role-match |
| `DeckFlow.Web.Tests/Services/Scryfall/CardGroundingGuardTests.cs` | test | request-response | `DeckFlow.Web.Tests/Services/Scryfall/ScryfallCardNameGrounderTests.cs` | exact |
| `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorWhitelistPoolBuilderTests.cs` | test | batch | `ScryfallCardNameGrounderTests.cs` (`FakeResolver` pattern) + a `Fake*Store` per existing `Fake*` naming convention | role-match |
| `DeckFlow.Web.Tests/Services/Scryfall/CardGroundingHallucinationFixtureTests.cs` | test (fixture table) | request-response | `ScryfallCardNameGrounderTests.cs` (typo-heals-to-real-card fixture) | role-match |

## Pattern Assignments

### `DeckFlow.Core/Knowledge/CardGrounding/ICardGroundingGuard.cs` (service seam, request-response)

**Analog:** `DeckFlow.Core/Knowledge/StatedRulesExtraction/ICardNameGrounder.cs` (read in full — 28 lines)

**Full pattern to mirror** (entire file):
```csharp
namespace DeckFlow.Core.Knowledge.StatedRulesExtraction;

/// <summary>
/// Core-facing seam for card name grounding behind the Web-hosted Scryfall-backed implementation.
/// </summary>
/// <remarks>
/// This contract exists because Core cannot reach the internal Web Scryfall throttle directly.
/// ...
/// </remarks>
public interface ICardNameGrounder
{
    Task<CardGroundingResult> TryGroundAsync(string candidateName, CancellationToken cancellationToken = default);
}

public sealed record CardGroundingResult(bool Resolved, string CanonicalName);
```

**How to apply (D-01/D-02/D-04):** Do NOT reuse or extend `ICardNameGrounder`/`CardGroundingResult` — D-01
requires a fully separate interface. Namespace: `DeckFlow.Core.Knowledge.CardGrounding` (new sub-namespace,
mirroring how `StatedRulesExtraction` and `ProfileFusion` each get their own folder+namespace under
`DeckFlow.Core/Knowledge/`). Shape:
```csharp
namespace DeckFlow.Core.Knowledge.CardGrounding;

public interface ICardGroundingGuard
{
    Task<CardGroundingVerdict> TryValidateAsync(
        string candidateName,
        CardGroundingDeckContext deckContext,
        CancellationToken cancellationToken = default);

    Task<CardGroundingBatchResult> ValidateAllAsync(
        IReadOnlyList<string> candidateNames,
        CardGroundingDeckContext deckContext,
        CancellationToken cancellationToken = default);
}
```
Use XML `<remarks>` explaining the Core-cannot-reach-Web-throttle rationale, same as the analog — this
doc comment is load-bearing for future maintainers deciding whether logic belongs in Core or Web.

---

### `DeckFlow.Core/Knowledge/CardGrounding/CardGroundingVerdict.cs` (model, D-13)

**Analog A (shape/naming precedent for "Verdict" records):** `DeckFlow.Core/Manabase/ManabaseVerdict.cs`
(full file, 19 lines) — `sealed record` with `required` + `init` properties, one property per decision
facet.

**Analog B (the "minimal-shape counterexample" the CONTEXT explicitly calls out):**
`CardGroundingResult(bool Resolved, string CanonicalName)` in `ICardNameGrounder.cs` — this is what D-13
says NOT to replicate; the guard's verdict must carry the reject-reason enum too.

**Pattern to copy — sealed record with `required`/`init`, matching project convention** (from
`DeckFlow.Core/Content/CreatorDeckCacheEntry.cs:8-36`):
```csharp
public sealed record CreatorDeckCacheEntry
{
    public required string CreatorSlug { get; init; }
    public required string DeckId { get; init; }
    // ...
}
```

**Apply as:**
```csharp
namespace DeckFlow.Core.Knowledge.CardGrounding;

public sealed record CardGroundingVerdict
{
    public required bool Accepted { get; init; }
    public required string CanonicalName { get; init; }
    public required CardGroundingRejectReason RejectReason { get; init; } // None when Accepted
}
```
CRITICAL (CLAUDE.md carve-out): use `{ get; init; }`, never downgrade to `{ get; }` — System.Text.Json
silently skips get-only properties in .NET 9+ and this record may be cache-(de)serialized indirectly via
`IMemoryCache` object storage (in-proc, no JSON involved for `MemoryCache.Set`, but keep the convention
anyway since it is the project-wide rule regardless of serialization path).

---

### `DeckFlow.Core/Knowledge/CardGrounding/CardGroundingRejectReason.cs` (model enum, D-13)

**No direct enum analog exists in the codebase** (verified: no existing `enum ... Reason` pattern for
verdicts). Nearest precedent is the *string*-tag verdict field in
`DeckFlow.Core/Knowledge/ProfileFusion/ConflictCalculator.cs:33,39,48,51` (`"insufficient-measured"`,
`"agree"`, `"conflict"` as bare strings) — CONTEXT D-13 explicitly upgrades this to a typed enum, so do not
copy the string-tag style; use a real C# enum per general project convention (PascalCase enum members,
`DeckFlow.Core` namespace, no HTTP dependency):
```csharp
namespace DeckFlow.Core.Knowledge.CardGrounding;

public enum CardGroundingRejectReason
{
    None,
    NotFound,
    Ambiguous,
    NotLegal,
    IdentityViolation,
    SingletonDuplicate,
    Uncastable,
    UpstreamUnavailable,
}
```

---

### `DeckFlow.Core/Knowledge/CardGrounding/CardGroundingRules.cs` (pure decision logic, CS-23)

**Analog 1:** `DeckFlow.Core/Knowledge/ProfileFusion/ConflictCalculator.cs` (static class, pure functions,
no HTTP, `ArgumentNullException.ThrowIfNull` guard, private helper methods, doc-commented public entry
point returning a result record).

**Imports/class-shape pattern** (lines 1-13):
```csharp
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Knowledge.StatedRulesExtraction;

namespace DeckFlow.Core.Knowledge.ProfileFusion;

public static class ConflictCalculator
{
    private const double ConflictThresholdPercent = 0.10;

    public static ConflictCalculationResult Evaluate(
        StatedRuleCandidate rule,
        double measuredValue,
        double? effectiveSampleSize)
    {
        ArgumentNullException.ThrowIfNull(rule);
        // ... pure decision tree, early returns for each verdict branch ...
    }
}
```

**Analog 2 (constants + pure validators, `internal static class`):**
`DeckFlow.Core/Knowledge/DistillationValidation.cs:10-61` — grouped constants at top (`Why:` comments on
non-obvious magic numbers), then one static `Validate*`/pure-check method per rule, each throwing or
returning a simple result.

**Apply as:** `CardGroundingRules` should be a `public static class` (public, since
`CardGroundingGuard` in `DeckFlow.Web` calls it) with one static method per D-11 check, each taking plain
data (no `ScryfallCard`, to keep it Core-testable per Pitfall 3 — accept a small internal projection or
`ScryfallCard`-shaped fields passed as scalar args/records):
```csharp
public static class CardGroundingRules
{
    public static bool IsLegalForCommander(IReadOnlyDictionary<string, string>? legalities)
        => legalities is not null
           && legalities.TryGetValue("commander", out var status)
           && string.Equals(status, "legal", StringComparison.OrdinalIgnoreCase);
    // fail-closed (D-12): null/missing => false, never true

    public static bool IsWithinColorIdentity(IReadOnlyList<string>? cardColorIdentity, IReadOnlySet<string> commanderIdentity) { /* subset check */ }

    public static bool IsSingletonViolation(string canonicalName, string typeLine, IReadOnlySet<string> deckCardNames)
    {
        if (typeLine.Contains("Basic Land", StringComparison.OrdinalIgnoreCase)) return false; // Pitfall 2
        return deckCardNames.Contains(canonicalName);
    }

    public static bool IsCastable(string? manaCost, IReadOnlySet<char> deckProducedColors) { /* pip-coverage check, Pitfall 3 */ }
}
```
Every "Why:" comment on a magic number (e.g. why `"Basic Land"` substring vs a hardcoded name list) should
follow the `DistillationValidation.cs:12-14` `// Why:` comment convention.

---

### `DeckFlow.Web/Services/Scryfall/CardGroundingGuard.cs` (Web impl, CS-21/CS-24)

**Analog:** `DeckFlow.Web/Services/Scryfall/ScryfallCardNameGrounder.cs` (full file, 52 lines — read
entirely, copy structure verbatim except for strictness).

**Full structure to mirror:**
```csharp
using DeckFlow.Core.Knowledge.StatedRulesExtraction;
using Microsoft.Extensions.Caching.Memory;

namespace DeckFlow.Web.Services.Scryfall;

public sealed class ScryfallCardNameGrounder(IScryfallCardResolver resolver, IMemoryCache cache) : ICardNameGrounder
{
    private static readonly TimeSpan PositiveCacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromHours(1);

    public async Task<CardGroundingResult> TryGroundAsync(string candidateName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(candidateName)) { return new CardGroundingResult(false, candidateName); }

        var cacheKey = BuildCacheKey(candidateName);
        if (cache.TryGetValue<CardGroundingResult>(cacheKey, out var cachedResult)) { return cachedResult!; }

        CardGroundingResult result;
        try
        {
            var card = await resolver.SearchPrintingFallbackCardAsync(candidateName, cancellationToken).ConfigureAwait(false);
            result = card is not null ? new CardGroundingResult(true, card.Name) : new CardGroundingResult(false, candidateName);
        }
        catch { result = new CardGroundingResult(false, candidateName); }

        cache.Set(cacheKey, result, result.Resolved ? PositiveCacheTtl : NegativeCacheTtl);
        return result;
    }

    private static string BuildCacheKey(string candidateName)
        => "card-grounder:" + candidateName.Trim().ToLowerInvariant();
}
```

**Deviations required by D-01/D-03/D-14 (do NOT copy these parts verbatim):**
- Use primary-constructor `(IScryfallCardResolver resolver, IMemoryCache cache) : ICardGroundingGuard` —
  same constructor style, different interface.
- Do NOT call `resolver.SearchPrintingFallbackCardAsync` (that is the *lenient* cascade — searches by
  printed-name, multiple queries, then fuzzy). CS-21 requires the *strict* single-fuzzy-accept path only.
  Compose `IScryfallCardResolver.ExecuteCollectionAsync` for the exact-name batch step (Pattern 3 below)
  and a net-new strict single fuzzy call (`cards/named?fuzzy=`) for the fallback — see
  `ScryfallCardResolver.cs:195-204` for the exact `RestRequest` shape to reuse, but branch the 404 body via
  the new `ScryfallErrorResponse` DTO (Pitfall 1) instead of returning `null` unconditionally.
- Cache key namespace must differ: use `"card-grounding-guard:"` prefix (not `"card-grounder:"`) so the
  lenient grounder's cache entries and the strict guard's cache entries never collide even though both use
  the same `IMemoryCache` singleton and same normalized-name key material.
- D-14: cache value must be a *small internal cache-record* (canonical name, color identity, legality
  string, mana cost) — NOT `CardGroundingVerdict` directly if `CardGroundingVerdict` doesn't already satisfy
  "verdict-relevant fields only" (it does, per the shape above — `Accepted`/`CanonicalName`/`RejectReason`
  contain no full-card JSON, so caching `CardGroundingVerdict` itself is fine and simpler than a second
  cache-record type).
- `try/catch` around the resolver call must translate a caught exception into
  `RejectReason.UpstreamUnavailable` (D-12 fail-closed), not silently reuse `NotFound`.

---

### `DeckFlow.Web/Services/Scryfall/ScryfallErrorResponse.cs` (NEW DTO, Pitfall 1)

**Analog:** `DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs:84-93` (`ScryfallCollectionIdentifier`,
`ScryfallPrintingIdentifier`) — small standalone `sealed record` DTOs with `JsonPropertyName` attributes,
declared in the same file/namespace as the other Scryfall DTOs.

**Imports pattern** (from `ScryfallDtos.cs:1-3`):
```csharp
using System.Text.Json.Serialization;

namespace DeckFlow.Web.Services;
```

**Shape to add (verified live 2026-07-18 against api.scryfall.com — see RESEARCH.md Pitfall 1):**
```csharp
public sealed record ScryfallErrorResponse(
    [property: JsonPropertyName("object")] string? Object,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("type")] string? Type = null,
    [property: JsonPropertyName("status")] int? Status = null,
    [property: JsonPropertyName("details")] string? Details = null);
```
Deserialize `response.Content` (raw body string, still available on `RestResponse<ScryfallCard>` even when
typed `Data` is null on a 404) with `System.Text.Json.JsonSerializer.Deserialize<ScryfallErrorResponse>`,
then branch: `Type == "ambiguous"` → `CardGroundingRejectReason.Ambiguous`, else →
`CardGroundingRejectReason.NotFound`. Do NOT branch on `Code` — both cases return `"not_found"`.

---

### `DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs` (MODIFIED — add `Legalities`, D-09)

**Existing shape to extend** (`ScryfallDtos.cs:39-57`, already read in full):
```csharp
public sealed record ScryfallCard(
    string Name,
    [property: JsonPropertyName("mana_cost")] string? ManaCost,
    [property: JsonPropertyName("type_line")] string TypeLine,
    // ...
    [property: JsonPropertyName("color_identity")] IReadOnlyList<string>? ColorIdentity,
    // ...
    [property: JsonPropertyName("produced_mana")] IReadOnlyList<string>? ProducedMana = null,
    [property: JsonPropertyName("rarity")] string? Rarity = null);
```
**Add** (append as the last optional positional parameter to avoid breaking existing positional call
sites — every existing call site in the codebase, including `ScryfallCardNameGrounderTests.Card()`, uses
named arguments, so appending is low-risk but keep it last regardless):
```csharp
[property: JsonPropertyName("legalities")] IReadOnlyDictionary<string, string>? Legalities = null
```
Verified live shape: lowercase format keys (`"commander"`, `"standard"`, ...), lowercase string values
(`"legal"`, `"not_legal"`, `"restricted"`, `"banned"`). `Legalities?["commander"]` missing/null MUST be
treated as reject (fail-closed, D-12/A2), never as implicit pass.

---

### `DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs` (CS-22, D-05/D-06/D-07)

**Analog:** `DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs` — batch Scryfall
orchestration reading from a Core store, same directory/namespace convention
(`DeckFlow.Web.Services.CreatorStyle`).

**Constructor + DI pattern** (lines 20-60):
```csharp
namespace DeckFlow.Web.Services.CreatorStyle;

public sealed class MeasuredStyleProfileBuilder
{
    private const int ScryfallBatchSize = 75;
    private const int MaxLiftMetrics = 25; // <- house-style precedent for "bounded list capped at ~25"

    private readonly CreatorProfileDeckCrawler _deckCrawler;
    // ...
    private readonly IScryfallCardResolver _scryfallCardResolver;
    private readonly ICreatorStyleProfileStore _profileStore;
    private readonly ILogger<MeasuredStyleProfileBuilder> _logger;

    public MeasuredStyleProfileBuilder(/* DI ctor */) : this(/* ..., logger, null */) { }
}
```

**Batch chunk pattern to copy verbatim** (`MeasuredStyleProfileBuilder.cs:343-366`):
```csharp
for (int offset = 0; offset < identifiers.Count; offset += ScryfallBatchSize)
{
    object[] batch = identifiers.Skip(offset).Take(ScryfallBatchSize).ToArray();
    var request = new RestRequest("cards/collection", Method.Post);
    request.AddJsonBody(new { identifiers = batch });

    RestResponse<ScryfallCollectionResponse> response = await _scryfallCardResolver
        .ExecuteCollectionAsync(request, cancellationToken)
        .ConfigureAwait(false);

    if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices || response.Data is null)
    {
        throw new HttpRequestException(
            $"Scryfall card lookup (cards/collection) returned HTTP {(int)response.StatusCode} during ...",
            inner: null,
            statusCode: response.StatusCode);
    }
    // ...
}
```

**Store-read pattern to copy** (`DeckFlow.Core/Content/ICreatorDeckCacheStore.cs:27`):
```csharp
Task<IReadOnlyList<CreatorDeckCacheEntry>> GetByCreatorAsync(string creatorSlug, CancellationToken cancellationToken = default);
// CreatorDeckCacheEntry.Entries : IReadOnlyList<DeckEntry> — Name, Quantity, Board
```

**Apply as:** Constructor takes `ICreatorDeckCacheStore`, `ICardGroundingGuard` (Pitfall 4 — every ranked
candidate must ALSO pass the guard before embedding, so the whitelist builder is a *consumer* of
`CardGroundingGuard`, not a Scryfall-fetching peer of it — this differs from `MeasuredStyleProfileBuilder`,
which fetches Scryfall data directly; the whitelist builder should NOT duplicate Scryfall fetch logic,
it should call `ICardGroundingGuard.ValidateAllAsync` on its ranked/capped candidate names). Cap constant
should follow the `MaxLiftMetrics = 25` house-style precedent — pick 15-30 per RESEARCH Open Question 1.

---

### `DeckFlow.Web/Extensions/ScryfallServiceCollectionExtensions.cs` (MODIFIED — DI registration)

**Existing registration pattern to copy** (`ScryfallServiceCollectionExtensions.cs:55-59`):
```csharp
services.AddSingleton<IScryfallCardResolver>(sp =>
    new ScryfallCardResolver(
        sp.GetRequiredService<IScryfallRestClientFactory>(),
        sp.GetRequiredService<ResiliencePipelineProvider<string>>()));
services.AddSingleton<ICardNameGrounder, ScryfallCardNameGrounder>();
```

**Add immediately after the grounder registration** (same simple `AddSingleton<TInterface, TImpl>`
shorthand works since `CardGroundingGuard`'s DI ctor takes only `IScryfallCardResolver` + `IMemoryCache`,
both already registered/resolvable):
```csharp
services.AddSingleton<ICardGroundingGuard, CardGroundingGuard>();
```
Add `using DeckFlow.Core.Knowledge.CardGrounding;` to the top import block alongside the existing
`using DeckFlow.Core.Knowledge.StatedRulesExtraction;` (line 1). Register `CreatorWhitelistPoolBuilder` as
a singleton too, following the same file's existing pattern for services with multiple constructor deps
(see `ICommanderSpellbookService` registration at lines 66-71 for the multi-dependency factory-lambda
shape) if `CreatorWhitelistPoolBuilder` needs an explicit factory lambda rather than the auto-DI shorthand.

---

## Shared Patterns

### Scryfall throttle + resilience (mandatory on every new Scryfall call)
**Source:** `DeckFlow.Web/Services/Scryfall/ScryfallThrottle.cs` (full file) +
`DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs:75-92` (how a resolver method wraps a call)
**Apply to:** Any code path in `CardGroundingGuard` or `CreatorWhitelistPoolBuilder` that issues a NEW
Scryfall HTTP call. In practice this phase should not need new throttle-wrapping code at all — every
Scryfall call goes through `IScryfallCardResolver`'s existing methods (`ExecuteCollectionAsync`,
`SearchFallbackCardAsync`, or a net-new strict fuzzy method added to the resolver interface), which already
wrap `ScryfallThrottle.ExecuteAsync` internally. Do not call `ScryfallThrottle` directly from the guard —
compose through the resolver, per D-02/Pattern 1.
```csharp
_executeNamedAsync = executeNamedAsyncOverride ?? ((request, cancellationToken) =>
    ScryfallThrottle.ExecuteAsync(
        token => pipeline.ExecuteAsync(
            async pollyCt => await client.ExecuteAsync<ScryfallCard>(request, pollyCt).ConfigureAwait(false),
            token).AsTask(),
        cancellationToken));
```

### Fail-closed 429/5xx translation
**Source:** `DeckFlow.Web/Services/Scryfall/ScryfallThrottle.cs:145-155`
```csharp
public static void ThrowIfUpstreamUnavailable(HttpStatusCode statusCode)
{
    var code = (int)statusCode;
    if (code == 429 || code >= 500)
    {
        throw new HttpRequestException($"Scryfall returned HTTP {code}.", inner: null, statusCode: statusCode);
    }
}
```
**Apply to:** `CardGroundingGuard`'s strict fuzzy fallback call, mirroring
`ScryfallCardResolver.cs:198` (`ScryfallThrottle.ThrowIfUpstreamUnavailable(namedResponse.StatusCode);`
immediately after the call). Catch the resulting `HttpRequestException` at the guard's outer `try/catch`
and translate to `RejectReason.UpstreamUnavailable` (D-12), not silently swallow like the lenient grounder
does (`ScryfallCardNameGrounder.cs:36-39` catches broad `Exception` and returns unresolved with no reason
code — the guard must be *more specific*, not merely mimic the catch-and-degrade).

### 503 escalation copy for the "usable floor" case (D-12)
**Source:** `DeckFlow.Web/Services/UpstreamErrorMessageBuilder.cs:44-48`
```csharp
public static string BuildScryfallMessage(Exception exception)
    => BuildMoxfieldBlockedMessage(exception)
        ?? BuildDetailedScryfallMessage(exception)
        ?? BuildSiteSpecificMessage(exception)
        ?? "Scryfall could not be reached right now. Try again shortly.";
```
**Apply to:** The P99 consumer (out of this phase's scope per CONTEXT, but the guard's aggregate
`CardGroundingBatchResult` should expose enough (e.g. an `UnvalidatableRequiredCard` flag or the raw list
of `UpstreamUnavailable` verdicts) for P99 to call `UpstreamErrorMessageBuilder.BuildScryfallMessage` and
return a 503, without Phase 98 itself needing to reference `UpstreamErrorMessageBuilder` (Web-host-only
static helper; do not pull it into `DeckFlow.Core`).

### Test-double naming + fake-resolver seam
**Source:** `DeckFlow.Web.Tests/Services/Scryfall/ScryfallCardNameGrounderTests.cs:87-109` (`FakeResolver`,
full private nested class implementing `IScryfallCardResolver` with a delegate injected via primary
constructor, plus a call-count property for assertion).
**Apply to:** `CardGroundingGuardTests.cs` — same `Fake*` naming convention (not `Mock*`/`Stub*` unless the
existing `Stub*`-for-queue-driven-behavior convention specifically fits), same delegate-injection shape.
For `CreatorWhitelistPoolBuilderTests.cs`, add a `FakeCreatorDeckCacheStore` implementing
`ICreatorDeckCacheStore` in the same nested-private-class style.
```csharp
private sealed class FakeResolver(Func<string, Task<ScryfallCard?>> searchPrintingFallbackAsync) : IScryfallCardResolver
{
    public int SearchPrintingFallbackCallCount { get; private set; }
    // ... delegate to the injected func, incrementing the call count ...
}
```

### Sealed record with `required`/`init` (project-wide DTO/model convention)
**Source:** `DeckFlow.Core/Content/CreatorDeckCacheEntry.cs` (full file) and
`DeckFlow.Core/Manabase/ManabaseVerdict.cs` (full file)
**Apply to:** All new Core-side records in this phase (`CardGroundingVerdict`, `CardGroundingDeckContext`,
any batch-result record). CARVE-OUT REMINDER (CLAUDE.md): never downgrade `{ get; init; }` to `{ get; }`.

### Pure static decision-logic class (Core, no HTTP, xUnit-testable)
**Source:** `DeckFlow.Core/Knowledge/ProfileFusion/ConflictCalculator.cs` (full pattern) and
`DeckFlow.Core/Knowledge/DistillationValidation.cs:10-61` (constants-at-top + `// Why:` comment style)
**Apply to:** `CardGroundingRules.cs` — this is the CS-23 pure-logic core the planner should route to
`DeckFlow.Core.Tests` for direct, HTTP-free unit testing (matches RESEARCH's Wave-0 gap:
`DeckFlow.Core.Tests/Knowledge/CardGrounding/CardGroundingRulesTests.cs`).

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `DeckFlow.Web/Services/Scryfall/ScryfallErrorResponse.cs` parse step (branching on `type == "ambiguous"`) | utility (error-body parse) | transform | RESEARCH confirms: no existing caller in the codebase parses the Scryfall 404 Error JSON body today — every current call site (`ScryfallCardResolver`, `CardLookupService`, `ScryfallTaggerLookupService`) only checks HTTP status code range. This is genuinely new logic; the DTO *shape convention* has an analog (`ScryfallDtos.cs` small-record style) but the *parse-and-branch* logic does not. |
| `CardGroundingRejectReason` enum | model | transform | No existing verdict enum in the codebase (nearest precedent, `ConflictCalculator`'s bare string tags, is explicitly superseded by D-13's typed-enum requirement) — use plain C# enum conventions, no analog needed beyond general project enum style. |

## Metadata

**Analog search scope:** `DeckFlow.Core/Knowledge/**`, `DeckFlow.Core/Content/**`, `DeckFlow.Core/Manabase/**`,
`DeckFlow.Web/Services/Scryfall/**`, `DeckFlow.Web/Services/CreatorStyle/**`, `DeckFlow.Web/Extensions/**`,
`DeckFlow.Web.Tests/Services/Scryfall/**`
**Files scanned (read in full or targeted):** `ICardNameGrounder.cs`, `ScryfallCardNameGrounder.cs`,
`ScryfallCardResolver.cs`, `ScryfallDtos.cs`, `ScryfallThrottle.cs`, `ScryfallServiceCollectionExtensions.cs`,
`UpstreamErrorMessageBuilder.cs`, `ICreatorDeckCacheStore.cs`, `CreatorDeckCacheEntry.cs`,
`MeasuredStyleProfileBuilder.cs` (targeted), `ScryfallCardNameGrounderTests.cs`,
`ConflictCalculator.cs` (targeted), `DistillationValidation.cs` (targeted), `ManabaseVerdict.cs`,
`AssemblyInfo.cs` (both projects, confirming `[InternalsVisibleTo]` already wired — no new attribute needed)
**Pattern extraction date:** 2026-07-18
