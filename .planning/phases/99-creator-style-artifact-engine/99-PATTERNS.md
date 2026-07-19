# Phase 99: Creator-Style Artifact Engine - Pattern Map

**Mapped:** 2026-07-19
**Files analyzed:** 8 new files + 2 modified (Program.cs / PacketServiceCollectionExtensions.cs or equivalent, DI tripwire test extension)
**Analogs found:** 8 / 8 (all files have a strong existing analog; this phase is pure composition, per RESEARCH.md)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|---------------|
| `DeckFlow.Core/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorer.cs` | service (pure static scorer) | transform | `DeckFlow.Core/Knowledge/ProfileFusion/ProfileFusionEngine.cs` | exact |
| `DeckFlow.Core/Knowledge/CreatorStyleRubric/RubricMetricScore.cs` | model | transform | `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` (`FusedTarget` record) | exact |
| `DeckFlow.Core/Knowledge/CreatorStyleRubric/RubricScoreResult.cs` | model | transform | `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` (`CreatorStyleProfile` aggregate record) | exact |
| `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs` | service (packet orchestrator) | request-response (CRUD-adjacent: read-only compose) | `DeckFlow.Web/Services/DeckPrimerPacketService.cs` (shape) + `DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs` (guard-gate pattern) | exact (shape) / role-match (size) |
| `DeckFlow.Web/Services/CreatorStyle/CreatorDeckExemplarSelector.cs` | utility (pure selection rule) | transform | `DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs` (`RankedCandidate`/`OrderByDescending...ThenBy` ranking block, lines 103-134) | role-match |
| DI registration block (extend `PacketServiceCollectionExtensions.cs` or `Program.cs`) | config | request-response | `DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs:49-91` | exact |
| `DeckFlow.Core.Tests/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorerTests.cs` | test | transform | `DeckFlow.Core.Tests/ProfileFusion/ProfileFusionEngineTests.cs` | exact |
| `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStylePacketServiceTests.cs` | test | request-response | `DeckFlow.Web.Tests` fakes pattern in `CreatorStyleDiRegistrationTests.cs` + `CreatorWhitelistPoolBuilderTests.cs` | exact |
| `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorDeckExemplarSelectorTests.cs` | test | transform | `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorWhitelistPoolBuilderTests.cs` | role-match |
| Extend `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStyleDiRegistrationTests.cs` | test (DI tripwire) | event-driven (build-time validation) | itself (existing file, extend in place) | exact |

## Pattern Assignments

### `DeckFlow.Core/Knowledge/CreatorStyleRubric/CreatorStyleRubricScorer.cs` (service, transform)

**Analog:** `DeckFlow.Core/Knowledge/ProfileFusion/ProfileFusionEngine.cs`

**Imports pattern** (mirror lines 1-3):
```csharp
using DeckFlow.Core.Knowledge.StatedRulesExtraction; // swap for whatever submitted-deck-stats
                                                       // input type CreatorStyleRubric defines

namespace DeckFlow.Core.Knowledge.CreatorStyleRubric;
```

**Core pure-static pattern** (`ProfileFusionEngine.cs:20-50`):
```csharp
public static class ProfileFusionEngine
{
    public static IReadOnlyList<FusedTarget> Fuse(
        IReadOnlyList<MeasuredMetric> measured,
        IReadOnlyList<StatedRuleCandidate> statedRules)
    {
        ArgumentNullException.ThrowIfNull(measured);
        ArgumentNullException.ThrowIfNull(statedRules);
        // ... pure computation, ordering deterministic via OrderBy/ThenBy on metric/condition ...
        return fused;
    }
}
```
Mirror shape for the new scorer: `CreatorStyleRubricScorer.Score(IReadOnlyList<FusedTarget> creatorTargets, SubmittedDeckStats submittedStats)` — a `static class`, `ArgumentNullException.ThrowIfNull` guards at entry, deterministic `OrderBy`/`ThenBy` on the output collection (never rely on dictionary enumeration order), no `await`, no I/O.

**Verdict vocabulary to reuse verbatim** (`ProfileFusionEngine.cs:79-155`, `ToConfidenceBand` at 303-316):
```csharp
Verdict = "insufficient-measured", // or conflict.Verdict from ConflictCalculator
VerdictReason = "no-condition-breakdown",
Confidence = ToConfidenceBand(rule.Confidence), // "high" >= 0.8, "med" >= 0.5, else "low"
```
The rubric's per-metric verdict strings should reuse this exact `"high"/"med"/"low"` confidence-band vocabulary and the `"insufficient-measured"` verdict string so the fusion ledger and rubric ledger read consistently (RESEARCH.md Summary, ¶ "reusing the same Verdict/Confidence vocabulary").

**Metric-key vocabulary — MUST import, never invent** (`DeckFlow.Core/Knowledge/ProfileFusion/StatedMetricKeyMapper.cs:73-91`):
```csharp
foreach (string category in ContentTagVocabulary.CardCategories)
{
    mappings[category] = $"category_ratio:{category}";
}

mappings["karsten:target_lands"] = "karsten:target_lands";
mappings["karsten:land_delta"] = "karsten:land_delta";
mappings["karsten:health_score"] = "karsten:health_score";
mappings["combo_density:included_per_deck"] = "combo_density:included_per_deck";
```
Key every rubric row off `FusedTarget.Metric` using these exact strings (`category_ratio:{category}` for the 11 `ContentTagVocabulary.CardCategories`, plus the four `karsten:*`/`combo_density:*` keys). This is Pitfall 1 in RESEARCH.md — a rubric that invents new metric-name strings will silently produce zero matches.

**Karsten math to call directly, not re-derive** (`DeckFlow.Core/Manabase/KarstenManabase.cs:27-70`):
```csharp
public static double SingletonLandTarget(
    int totalCards, int commanderCount, double averageManaValue,
    double rampAndDrawUnderThree, double fastMana = 0,
    double mdfcCommon = 0, double mdfcMythic = 0) { ... }

public static double CedhLandTarget(
    int totalCards, int commanderCount, double averageManaValue,
    double rampAndDrawUnderThree, double fastMana = 0,
    double mdfcCommon = 0, double mdfcMythic = 0)
    => Math.Max(28.0, SingletonLandTarget(...) - 3.5);
```
Call `SingletonLandTarget`/`CedhLandTarget` with scalar deck stats already computed by the Web orchestrator; do not build a `ManabaseDeck`/`ManabaseAnalyzer` simulation (Anti-Pattern in RESEARCH.md; also Open Question #1, default scope = scalar comparison only).

**Error handling:** None — pure functions return degraded/insufficient verdicts rather than throwing (matches `ProfileFusionEngine`'s `"insufficient-measured"` fallback branches at lines 88-109, 113-133). Only `ArgumentNullException.ThrowIfNull` guards at the public entry point.

---

### `DeckFlow.Core/Knowledge/CreatorStyleRubric/RubricMetricScore.cs` / `RubricScoreResult.cs` (model, transform)

**Analog:** `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` (`FusedTarget`, `MeasuredMetric` records, lines 63-130)

**Record shape pattern:**
```csharp
public sealed record FusedTarget
{
    public required string Metric { get; init; }
    public required double Value { get; init; }
    public required double Weight { get; init; }
    public required string Source { get; init; }
    public string? Condition { get; init; }
    // ... optional diagnostic fields with XML doc comments on every property ...
}
```
Mirror as `sealed record` types with `required` on non-nullable/always-present members and plain nullable `{ get; init; }` on optional diagnostic fields. XML doc comment on every public property (project convention, enforced by `<GenerateDocumentationFile>`).

**CRITICAL carve-out reminder (project CLAUDE.md):** never write `{ get; }` where `{ get; init; }` (or `{ get; }` positional-record equivalent) is intended — `System.Text.Json` silently skips get-only properties on .NET 9+/10. All new record properties here must use `{ get; init; }`, matching every existing record in this codebase (`FusedTarget`, `CardGroundingVerdict`, `CreatorDeckCacheEntry`, etc.).

---

### `DeckFlow.Web/Services/CreatorStyle/CreatorStylePacketService.cs` (service, request-response)

**Analog (shape/size):** `DeckFlow.Web/Services/DeckPrimerPacketService.cs` — chosen over `DeckAnalysisPacketService` per RESEARCH.md Pitfall 4 (smaller, no feature flags, same `BuildAsync`/`TryComputeCacheKeyAsync` triad).

**Interface + result-record pattern** (`DeckPrimerPacketService.cs:22-36, 84-93`):
```csharp
public interface IDeckPrimerPacketService
{
    Task<DeckPrimerPacketResult> BuildAsync(DeckPrimerRequest request, CancellationToken cancellationToken = default);
    Task<string?> TryComputeCacheKeyAsync(DeckPrimerRequest request, CancellationToken cancellationToken);
}

// Why: this positional/init record must stay JSON-round-trippable; do not convert properties to
// get-only accessors because System.Text.Json drops get-only positional members in modern runtimes.
public sealed record DeckPrimerPacketResult(
    string InputSummary,
    string SuggestedChatTitle,
    string RequestContextText,
    IReadOnlyDictionary<string, string> PromptTextsByPlatform,
    string? TimingSummary,
    string? ImportWarning = null,
    ...);
```
Mirror: `ICreatorStylePacketService.BuildAsync(CreatorStyleRequest, CancellationToken)` returning `CreatorStylePacketResult` (a `sealed record` with the assembled artifact text + rubric result + exemplar list + a "no controller consumes this yet" doc comment per RESEARCH.md Architecture Diagram final box).

**Dual-constructor test-seam pattern** (`DeckPrimerPacketService.cs:155-218`):
```csharp
internal DeckPrimerPacketService(
    IDeckEntryLoader deckEntryLoader,
    ICommanderSpellbookService commanderSpellbookService,
    /* ...production deps... */
    ILogger<DeckPrimerPacketService>? logger = null)
{
    ArgumentNullException.ThrowIfNull(deckEntryLoader);
    // ... one ThrowIfNull per required dep ...
    _logger = logger ?? NullLogger<DeckPrimerPacketService>.Instance;
}

internal DeckPrimerPacketService(
    PrimerPromptVariantRegistry primerPromptRegistry,
    PacketSessionCache packetCache,
    Func<string, CancellationToken, Task<List<DeckEntry>>>? loadDeckEntriesAsyncOverride = null,
    /* ...one Func override per I/O dependency... */
    ILogger<DeckPrimerPacketService>? logger = null)
{
    // test-only ctor: overrides bypass real I/O without mocking IHttpClientFactory
}
```
Give `CreatorStylePacketService` this same pair: a production `internal` ctor taking real interfaces (`ICreatorStyleProfileStore`, `ICreatorDeckCacheStore`, `CreatorWhitelistPoolBuilder`, `ICardGroundingGuard`, `ICommanderSpellbookService`, `CategoryKnowledgeRepository`, `IDeckEntryLoader`, `PacketSessionCache`, optional `ILogger<...>`), plus a test-only `internal` ctor with `Func<...>Async` override delegates for each I/O call — exactly the seam `[InternalsVisibleTo("DeckFlow.Web.Tests")]` already grants.

**Guard-gate before returning (CS-29), batch not loop** (`CreatorWhitelistPoolBuilder.cs:62-83`):
```csharp
CardGroundingBatchResult validation = await _cardGroundingGuard
    .ValidateAllAsync(rawPool, deckContext, cancellationToken)
    .ConfigureAwait(false);

if (validation.HasUpstreamFailure)
{
    _logger.LogWarning(
        "Creator whitelist validation saw upstream failures for creator {CreatorSlug}; returning accepted subset only.",
        creatorSlug);
}

return validation.Verdicts
    .Where(verdict => verdict.Accepted)
    .Select(verdict => verdict.CanonicalName)
    .ToArray();
```
Apply this exact one-batch-call pattern to the union of exemplar-deck cards + whitelist cards + named combo cards (RESEARCH.md Code Examples, "Batch card-grounding gate"). Decide fail-open (mirror this exact log-and-continue) vs. fail-closed (throw/return a distinct failure result) per Pitfall 2 / Open Question #3 — this is a product decision the plan must record, not something to default silently.

**Combo lookup reuse** (`DeckPrimerPacketService.cs:446-456`, identical in `DeckAnalysisPacketService`):
```csharp
private async Task<CommanderSpellbookResult?> FindCombosAsync(IReadOnlyList<DeckEntry> entries, CancellationToken cancellationToken)
{
    if (_findCombosAsyncOverride is not null)
    {
        return await _findCombosAsyncOverride(entries, cancellationToken).ConfigureAwait(false);
    }

    return await _commanderSpellbookService!
        .FindCombosAsync(entries, cancellationToken)
        .ConfigureAwait(false);
}
```

**Category-ratio lookup for submitted deck** (`CategoryKnowledgeRepository.cs:65-66`):
```csharp
public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
    => _cardCategory.GetCategoriesAsync(cardName, cancellationToken);
```
Call once per submitted-deck card name to build the `category_ratio:*` counts fed into the rubric — reuse this repository (already Web-singleton-registered), never re-derive via raw oracle-tag matching (Anti-Pattern / Pitfall 1).

**Session cache key pattern** (`DeckPrimerPacketService.cs:220-286`, `PacketSessionCache.cs:52-59`):
```csharp
public static string ComputeKey(object fieldBag)
{
    var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(fieldBag, DeterministicJsonOptions);
    var hashBytes = SHA256.HashData(jsonBytes);
    return Convert.ToHexString(hashBytes).ToLowerInvariant();
}
```
If `BuildAsync` is exposed publicly this phase (RESEARCH.md recommends yes, per Open Question #2), follow the same `TryComputeCacheKeyAsync` → build a private `*CacheInputs` record → `PacketSessionCache.ComputeKey(inputs)` → `_packetCache.TryGet<T>`/`.Set(...)` round-trip already used by all four existing packet services.

**Error handling:** `ArgumentNullException.ThrowIfNull(request)` at method entry; `InvalidOperationException` with a user-facing message for "missing required input" cases (`DeckPrimerPacketService.cs:293-295, 309-310, 350-354`); catch narrow, log-and-degrade for optional enrichment lookups that shouldn't hard-fail the whole build (`DeckPrimerPacketService.cs:367-379` — `catch (Exception ex) { _logger.LogWarning(ex, ...); categoryDistribution = null; }`).

---

### `DeckFlow.Web/Services/CreatorStyle/CreatorDeckExemplarSelector.cs` (utility, transform)

**Analog:** `CreatorWhitelistPoolBuilder.cs:97-134` (`BuildRawPoolAsync`'s ranking block) — same "pick best N from a scored candidate list" shape.

**Ranking pattern to mirror** (`CreatorWhitelistPoolBuilder.cs:128-134`):
```csharp
return frequencyByName.Values
    .OrderByDescending(candidate => candidate.DistinctDeckCount)
    .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
    .Take(WhitelistCap)
    .Select(candidate => candidate.DisplayName)
    .ToArray();
```

**Proposed shape for the new selector** (RESEARCH.md Code Examples, no existing precedent — new logic):
```csharp
internal static IReadOnlyList<CreatorDeckCacheEntry> SelectExemplars(
    IReadOnlyList<CreatorDeckCacheEntry> creatorDecks,
    int submittedDeckSize,
    int maxExemplars = 3)
{
    return creatorDecks
        .OrderByDescending(deck => deck.ConfidenceMarker, StringComparer.Ordinal)
        .ThenBy(deck => Math.Abs(deck.Size - submittedDeckSize))
        .ThenBy(deck => deck.DeckId, StringComparer.Ordinal)
        .Take(maxExemplars)
        .ToArray();
}
```
Make this a pure `static` method (no I/O, no DI ctor) so it is directly unit-testable with hand-built `CreatorDeckCacheEntry` fixtures — same testability bar as `ProfileFusionEngine.Fuse`. `CreatorDeckCacheEntry` shape: `CreatorSlug`, `DeckId`, `ContentHash`, `FolderId`, `FolderName`, `Size`, `ConfidenceMarker`, `Entries: IReadOnlyList<DeckEntry>`, `CachedUtc` (`DeckFlow.Core/Content/CreatorDeckCacheEntry.cs:8-36`).

**Do not confuse with the whitelist (Pitfall 5):** `CreatorWhitelistPoolBuilder` returns flat *card names*; this selector returns whole `CreatorDeckCacheEntry` decklists. Both draw from `ICreatorDeckCacheStore` but serve distinct artifact sections.

---

### DI registration (extend `PacketServiceCollectionExtensions.cs` or `Program.cs`)

**Analog:** `DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs:49-91` (four existing `services.AddScoped<IXyzService>(sp => new XyzService(sp.GetRequiredService<...>(), ...))` blocks).

**Pattern to copy:**
```csharp
services.AddScoped<IDeckPrimerPacketService>(sp =>
    new DeckPrimerPacketService(
        sp.GetRequiredService<IDeckEntryLoader>(),
        sp.GetRequiredService<ICommanderSpellbookService>(),
        sp.GetRequiredService<IEdhTop16Client>(),
        sp.GetRequiredService<ICategoryKnowledgeStore>(),
        sp.GetRequiredService<PrimerPromptVariantRegistry>(),
        sp.GetRequiredService<PacketSessionCache>(),
        sp.GetRequiredService<IOptions<AiPlatformOptions>>(),
        sp.GetRequiredService<MoxfieldParser>(),
        sp.GetRequiredService<ArchidektParser>(),
        sp.GetService<ILogger<DeckPrimerPacketService>>()));
```
Add an equivalent `services.AddScoped<ICreatorStylePacketService>(sp => new CreatorStylePacketService(...))` block, resolving `ICreatorStyleProfileStore`, `ICreatorDeckCacheStore`, `CreatorWhitelistPoolBuilder` (already singleton-registered, `Program.cs:112`), `ICardGroundingGuard` (Phase 98 registration), `ICommanderSpellbookService`, `CategoryKnowledgeRepository` (`Program.cs:105-107`), `IDeckEntryLoader`, `PacketSessionCache`, and optional `ILogger<CreatorStylePacketService>`. Either extend `PacketServiceCollectionExtensions.AddDeckFlowPacketServices()` or add a sibling extension — both are established conventions (`CreatorWhitelistPoolBuilder` itself is registered directly in `Program.cs`, not the extensions file).

**D-14 pitfall to flag in the registration's `// Why:` comment** (`Program.cs:108-111`):
```csharp
builder.Services.AddSingleton<DeckFlow.Core.Content.ICreatorStyleProfileStore>(_ =>
    // Why: creator-style profiles live in the local-only content-kb.db per the CLI
    // (ContentKbCommandRunners) and Studio (Program.cs:92) convention (D-14: content-kb never ships to Render).
    new DeckFlow.Core.Content.CreatorStyleProfileStore(
        DeckFlowDatabaseConnectionFactory.CreateLocalContentKbConnection(builder.Environment)));
```
`ICreatorStyleProfileStore` is bound to a local-only SQLite connection (D-14). This doesn't block Phase 99 (no controller calls it in prod this phase), but any DI-graph work should carry forward this exact comment/awareness rather than silently assuming the store works identically in Render (RESEARCH.md Pitfall 3).

---

### Test files

**Analog for `CreatorStyleRubricScorerTests.cs`:** `DeckFlow.Core.Tests/ProfileFusion/ProfileFusionEngineTests.cs` — hand-built `MeasuredMetric`/`StatedRuleCandidate` fixtures, no mocks, `[Fact]`/`[Theory]` xUnit per project convention (`Method_Scenario_ExpectedResult` naming).

**Analog for `CreatorStylePacketServiceTests.cs`:** `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorWhitelistPoolBuilderTests.cs` (guard fake patterns) + the test-seam constructor pattern already used across `DeckPrimerPacketServiceTests`/`DeckAnalysisPacketServiceTests` (construct via the internal test ctor with `Func<...>Async` overrides — no `WebApplicationFactory`, satisfying SC #4's "no controller or page dependency").

**Analog for the DI-tripwire extension:** `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorStyleDiRegistrationTests.cs:19-66` — extend the existing `[Fact]` (or add a sibling `[Fact]`) that builds a `ServiceCollection`, registers the new `CreatorStylePacketService` dependency graph with fakes, and asserts:
```csharp
using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
{
    ValidateOnBuild = true,
    ValidateScopes = true,
});
using IServiceScope scope = provider.CreateScope();
Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreatorStylePacketService>());
```
This is mandatory precedent (RESEARCH.md Pattern 4) — Phase 98's gap-closure plan 98-05 shipped this exact test file because a missing store registration broke `dotnet run` cold start; Phase 99 must extend it before its own service ships.

## Shared Patterns

### Guard-gate before shipping any card-referencing content (CS-29)
**Source:** `DeckFlow.Web/Services/CreatorStyle/CreatorWhitelistPoolBuilder.cs:68-83`
**Apply to:** `CreatorStylePacketService.BuildAsync` — batch-validate the union of exemplar-deck cards, whitelist cards, and named combo cards in one `ICardGroundingGuard.ValidateAllAsync` call before assembling any artifact text. Never loop `TryValidateAsync` per card.

### Core-pure / Web-orchestration split
**Source:** `DeckFlow.Core/Knowledge/ProfileFusion/ProfileFusionEngine.cs` (pure) + `DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs` (orchestration, existing, do not modify)
**Apply to:** `CreatorStyleRubricScorer` (Core, static, testable with plain records) vs. `CreatorStylePacketService` (Web, I/O orchestration only — deck loading, guard calls, exemplar/whitelist fetch).

### Metric-key vocabulary — single source of truth
**Source:** `DeckFlow.Core/Knowledge/ProfileFusion/StatedMetricKeyMapper.cs:73-91`, `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs`
**Apply to:** Every rubric row's `Metric` string. Reuse `category_ratio:{category}` (11 categories), `karsten:target_lands`, `karsten:land_delta`, `karsten:health_score`, `combo_density:included_per_deck` verbatim — do not invent new metric-name strings.

### DI registration — factory-lambda scoped service
**Source:** `DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs:49-91`
**Apply to:** New `CreatorStylePacketService` registration, plus the mandatory `CreatorStyleDiRegistrationTests.cs` extension (Pattern 4 tripwire).

### Confidence/verdict vocabulary
**Source:** `DeckFlow.Core/Knowledge/ProfileFusion/ProfileFusionEngine.cs:303-316` (`ToConfidenceBand`), verdict strings at lines 79, 102, 127, 150-151, 175, 200
**Apply to:** `RubricMetricScore.Verdict`/`.Confidence` fields — reuse `"high"/"med"/"low"` and `"insufficient-measured"` rather than a new vocabulary.

## No Analog Found

None. Every file in scope has a strong existing analog per RESEARCH.md's own framing ("this phase is almost entirely composition of existing... substrate"). The only genuinely new logic across the whole phase is the exemplar-selection ranking rule (`CreatorDeckExemplarSelector`), and even that has a directly analogous ranking-shape precedent (`CreatorWhitelistPoolBuilder`'s `RankedCandidate` block) to structurally mirror.

## Metadata

**Analog search scope:** `DeckFlow.Core/Knowledge/{ProfileFusion,CardGrounding,CreatorStyleProfile.cs,ContentTagVocabulary.cs}`, `DeckFlow.Core/Manabase/KarstenManabase.cs`, `DeckFlow.Core/Content/{ICreatorStyleProfileStore,ICreatorDeckCacheStore,CreatorDeckCacheEntry}.cs`, `DeckFlow.Web/Services/{DeckPrimerPacketService.cs,DeckAnalysisPacketService.cs,PacketSessionCache.cs,CreatorStyle/*}`, `DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs`, `DeckFlow.Web/Program.cs` (store registrations ~lines 85-114), `DeckFlow.Core.Tests/ProfileFusion/*`, `DeckFlow.Web.Tests/Services/CreatorStyle/*`
**Files scanned:** ~20 read directly (imports/core/error-handling excerpts extracted); several more located via Glob/Grep for confirmation only
**Pattern extraction date:** 2026-07-19
