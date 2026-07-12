# Phase 95: Measured-Style Extractor - Pattern Map

**Mapped:** 2026-07-11
**Files analyzed:** 12 (new/modified)
**Analogs found:** 12 / 12 (all have at least a role-match; 2 gap items flagged as "extend, not reuse")

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Core/Content/ICreatorProfileSourceStore.cs` + `CreatorProfileSourceStore.cs` (NEW) | model/store | CRUD | `DeckFlow.Core/Content/ICreatorStyleProfileStore.cs` + `CreatorStyleProfileStore.cs` | exact |
| `DeckFlow.Web/Services/CreatorStyle/CreatorProfileDeckCrawler.cs` (NEW) | service (Web-host orchestrator) | request-response + batch (paginated HTTP crawl) | `DeckFlow.Core/Integration/ArchidektRecentDecksImporter.cs` (pagination shape only — NOT the HTML-scrape idiom) + `ArchidektApiDeckImporter.cs` (per-deck fetch, reused verbatim) | role-match (pagination shell); exact (per-deck fetch step) |
| Creator-scoped deck cache (NEW — table/store, shape TBD by planner per RESEARCH Open Question 1) | service/store | CRUD (cache) | `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` | shape-only match — target table MUST differ (see Pitfall 3 below) |
| `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/StapleStripper.cs`, `CategoryCounter.cs`, `LiftCalculator.cs`, `FolderWeighting.cs` (NEW, pure) | utility/transform | transform (pure, HttpClient-free) | No direct existing analog for shape (net-new pure-Core statistics module) — closest sibling pattern is `DeckFlow.Core/Reporting/CategoryFilter.cs` (pure static filtering helper) and `DeckFlow.Core/Manabase/KarstenManabase.cs` (pure static math helper) | role-match (pure static Core helper convention) |
| `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` (MODIFIED — add staple set) | config/vocabulary | — | itself (existing `Archetypes`/`Brackets`/`CardCategories` `HashSet<string>` dimensions) | exact (extend existing class, same idiom) |
| `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` + `CardCategoryRepository.cs` (MODIFIED — new global lift-baseline read method) | model/repository | CRUD (aggregate read) | itself — `GetCategoryRowsForCommanderAsync` / `GetCategoriesAsync` (existing scoped read methods) | role-match (same repository, new aggregate query shape) |
| `DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs` (NEW) | service (Web-host orchestrator) | request-response (composes pure Core + 2 Web services) | `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` (Scryfall-resolve → CardFact → pure-Core-classify chain, lines 408-463) | exact (pattern to replicate) |
| Karsten/combo calls inside `MeasuredStyleProfileBuilder.cs` | service call-site | request-response | `DeckFlow.Web/Services/CommanderSpellbookService.cs` (`FindCombosAsync`) + `DeckFlow.Core/Manabase/KarstenManabase.cs`/`ManabaseAnalyzer.cs`/`ManabaseClassifier.cs` | exact |
| `DeckFlow.Core.Tests/MeasuredStyleExtraction/*Tests.cs` (NEW) | test | — | `DeckFlow.Core.Tests/CreatorStyleProfileStoreTests.cs` + `CreatorStyleProfileTestData.cs` (round-trip harness convention); pure-logic tests style from `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` | role-match |
| `DeckFlow.Web.Tests/Services/CreatorStyle/CreatorProfileDeckCrawlerTests.cs` (NEW) | test | — | `DeckFlow.Web.Tests/Services/CommanderSpellbookServiceTests.cs` (`StubHttpMessageHandler` + `FakeHttpClientFactory` house pattern) | exact |
| `DeckFlow.Core.Tests/CreatorStyleProfileStoreTests.cs` + `CreatorStyleProfileTestData.cs` (EXTEND) | test | — | itself | exact |
| `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` (EXTEND, if new global-baseline method added) | test | — | itself | exact |

---

## Pattern Assignments

### `DeckFlow.Core/Content/CreatorProfileSourceStore.cs` (store, CRUD)

**Analog:** `DeckFlow.Core/Content/CreatorStyleProfileStore.cs` (verified read in full, 193 lines) + its paired interface `DeckFlow.Core/Content/ICreatorStyleProfileStore.cs`

**Do NOT use `CreatorSourceStore.cs`** — CONTEXT.md D-01 explicitly flags it as the wrong shape (it's a channel-ref-keyed YouTube content-source store with `source_slug`/`content_source_id` migration columns; verified at `DeckFlow.Core/Content/CreatorSourceStore.cs:1-118`). The new table needs slug + platform + profile URL/username + curated folder-weight map + "weights uncurated" flag — `CreatorStyleProfileStore`'s shape (slug-keyed, JSON-serialized nested sections, dialect-guarded) is the correct template.

**Constructor / test-seam pattern** (`CreatorStyleProfileStore.cs:11-64`):
```csharp
public sealed class CreatorStyleProfileStore : ICreatorStyleProfileStore
{
    private readonly RelationalDatabaseConnection _connectionInfo;
    private readonly bool _ensureSchemaEnabled;
    private readonly Func<CancellationToken, Task<DbConnection>>? _connectionFactoryOverride;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private volatile bool _schemaReady;

    public CreatorStyleProfileStore(string databasePath)
        : this(RelationalDatabaseConnection.FromSqlitePath(databasePath)) { }

    public CreatorStyleProfileStore(RelationalDatabaseConnection connectionInfo, bool ensureSchemaEnabled = true)
        : this(connectionInfo, ensureSchemaEnabled, connectionFactoryOverride: null) { }

    // internal test-seam ctor — public ctors always pass null and behave exactly as production.
    internal CreatorStyleProfileStore(
        RelationalDatabaseConnection connectionInfo,
        bool ensureSchemaEnabled,
        Func<CancellationToken, Task<DbConnection>>? connectionFactoryOverride)
    { /* ArgumentNullException.ThrowIfNull + SQLite directory bootstrap */ }
}
```

**Schema-gate pattern** (`CreatorStyleProfileStore.cs:67-85`):
```csharp
public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
{
    if (!_ensureSchemaEnabled) return;
    if (_schemaReady) return;
    await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        if (_schemaReady) return;
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var create = connection.CreateCommand();
        create.CommandText = _connectionInfo.IsPostgres ? PostgresCreateTableSql : SqliteCreateTableSql;
        await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        _schemaReady = true;
    }
    finally { _schemaGate.Release(); }
}
```

**Upsert (Dapper, `ON CONFLICT` dialect-shared) pattern** (`CreatorStyleProfileStore.cs:88-166`) — reuse the exact `DynamicParameters` + `CommandDefinition` idiom and the paired `PostgresCreateTableSql`/`SqliteCreateTableSql` `const string` fields (note: Postgres uses `BOOLEAN`/`TIMESTAMPTZ`, SQLite uses `INTEGER`/`TEXT` for the same logical columns — mirror this exact dialect split for the new `weights_uncurated` boolean and any timestamp columns).

**Read pattern** (`CreatorStyleProfileStore.cs:111-127`) — `QuerySingleOrDefaultAsync<TReadModel>` against a `SELECT {ColumnList} ... WHERE slug = @slug` then map via a `*Mapper.ToProfile(row)` static — mirror this for `GetBySlugAsync`/`GetByCreatorSlugAsync` on the new store; if the folder-weight map is stored as JSON, mirror `CreatorStyleProfileSections.SerializeSection(...)` (used at line 99-101) for JSON (de)serialization of the nested weight dictionary.

---

### `DeckFlow.Web/Services/CreatorStyle/CreatorProfileDeckCrawler.cs` (Web-host orchestrator, request-response + batch)

**Analog for the per-deck fetch step (reuse verbatim, no changes):** `DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs` (verified read in full, 156 lines)
```csharp
// DeckFlow.Core/Integration/ArchidektApiDeckImporter.cs:14-49
public sealed class ArchidektApiDeckImporter : IArchidektDeckImporter
{
    private readonly RestClient _restClient;
    private static readonly AsyncRetryPolicy<RestResponse> RetryPolicy = Policy<RestResponse>
        .HandleResult(response => response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
        .WaitAndRetryAsync(retryCount: 6, sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250)));

    public ArchidektApiDeckImporter(RestClient? restClient = null)
    {
        _restClient = restClient ?? new RestClient(new RestClientOptions
        {
            BaseUrl = new Uri("https://archidekt.com"),
            ThrowOnAnyError = false,
        });
    }

    public async Task<List<DeckEntry>> ImportAsync(string urlOrDeckId, CancellationToken cancellationToken = default)
    {
        // ArchidektApiUrl.TryGetDeckId(...) -> RetryPolicy.ExecuteAsync(... _restClient.ExecuteAsync ...)
        // -> JsonDocument.Parse(body) -> walk cards[]/categories[]/card.oracleCard.name
    }
}
```
The crawler calls `ArchidektApiDeckImporter.ImportAsync(deckId)` unchanged for step 3 of the crawl — do not reimplement per-deck parsing.

**Anti-pattern warning (Pitfall 1, RESEARCH):** Do **NOT** extend or reuse `DeckFlow.Core/Integration/ArchidektRecentDecksImporter.cs` (verified read in full, 165 lines) for the owner-resolve/deck-list calls. It hits a **different** endpoint (`websockets.archidekt.com/search/decks`, regex HTML scrape of `href="/decks/{id}"`) for a **different** purpose (site-wide recent-decks harvest queue), not an owner-scoped listing. Its only reusable *shape* is the pagination-loop idiom:
```csharp
// DeckFlow.Core/Integration/ArchidektRecentDecksImporter.cs:68-114 (pagination loop shape to mirror,
// NOT the HTML-regex body — the new owner-list endpoint is JSON, follow `next` instead of incrementing page blindly)
public async Task<IReadOnlyList<string>> ImportRecentDeckIdsAsync(int count, int startPage, CancellationToken cancellationToken = default)
{
    var deckIds = new List<string>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var page = Math.Max(1, startPage);
    while (deckIds.Count < count)
    {
        var pageIds = await ImportRecentDeckIdsPageAsync(page, cancellationToken);
        if (pageIds.Count == 0) break;
        foreach (var deckId in pageIds) { if (seen.Add(deckId)) { deckIds.Add(deckId); if (deckIds.Count == count) break; } }
        page += 1;
    }
    return deckIds;
}
```
Also mirror its `RestClient` construction + `AsyncRetryPolicy<RestResponse>` idiom (constructor injectable `RestClient? restClient = null`, `BaseUrl` set once) for the two net-new `GET /api/users/?username=` and `GET /api/decks/v3/?ownerUsername=&pageSize=&page=` calls — parse with `JsonDocument.Parse` (System.Text.Json, same as `ArchidektApiDeckImporter`), not regex.

**Deck-ID/URL parsing convention:** `DeckFlow.Core/Integration/ArchidektApiUrl.cs` (verified read in full, 48 lines) is the house pattern for "parse-and-validate an Archidekt identifier before use" (`TryGetDeckId` — try absolute URI first, fall back to raw ID, validate segment shape). The manual-URL fallback (D-01) and the SSRF mitigation (RESEARCH Security Domain V5) both require an equivalent guard restricting the input to the `archidekt.com` domain before it is embedded in an outbound request — model this after `TryGetDeckId`'s `Uri.TryCreate` + segment-based validation, not raw string interpolation.

---

### Creator-scoped deck cache (NEW — shape TBD, D-02)

**Analog (shape only):** `DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs` (verified read in full, 218 lines) — idle-poll loop (`RunAsync`), hash-based change detection:
```csharp
// DeckFlow.Core/Knowledge/ArchidektDeckCacheSession.cs:190-200
var newHash = DeckCategoryCacheWriter.ComputeCanonicalHash(entries);
var storedHash = await _repository.GetContentHashAsync(deckId, cancellationToken);
if (storedHash is not null && string.Equals(storedHash, newHash, StringComparison.Ordinal))
{
    return (DeckCacheWriteResult.Unchanged, commanderName);
}
```

**CRITICAL — Pitfall 3 (RESEARCH, HIGH confidence):** Mirror the *shape* only (hash-based freshness check via `DeckCategoryCacheWriter.ComputeCanonicalHash`), **never** the *target table*. `ArchidektDeckCacheSession` writes into `card_category_observations`/`sources`/`deck_queue` via `_repository.ReplaceSourceRowsAsync`/`AddDeckIdsAsync`/`PersistDeckCategoryBatchAsync` — those are the SAME tables `CategoryKnowledgeRepository` reads for the D-07 global baseline. Writing the creator's 39 crawled decks into that table would inflate their own Pr(A)/Pr(B) denominator with their own numerator data. The new cache must be a separate creator-scoped store (new table, or a JSON blob on the creator-profile-source row per RESEARCH Open Question 1) that is never read by `CategoryKnowledgeRepository`. **Warning sign:** any call from the P95 crawler code path to `CategoryKnowledgeRepository.ReplaceSourceRowsAsync`/`PersistDeckCategoryBatchAsync`/`AddDeckIdsAsync`.

---

### `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/*.cs` (pure Core, transform)

**No direct existing analog for the module shape** — this is genuinely net-new pure statistics code (RESEARCH confirms). Follow the **pure static Core helper** convention already established by two siblings:

**Pure static filter helper** (`DeckFlow.Core/Reporting/CategoryFilter.cs`, verified read in full, 50 lines):
```csharp
public static class CategoryFilter
{
    private static readonly HashSet<string> ExcludedCategories = new(StringComparer.OrdinalIgnoreCase) { "Artifact", "Creature", /* ... */ };

    public static bool IsIncluded(string? category)
        => !string.IsNullOrWhiteSpace(category) && !ExcludedCategories.Contains(category);

    public static IReadOnlyList<string> IncludedOrFallback(IEnumerable<string> categories)
    {
        var items = categories.Where(category => !string.IsNullOrWhiteSpace(category)).ToList();
        var included = items.Where(IsIncluded).ToList();
        return included.Count > 0 ? included : items;
    }
}
```
Use this exact "static class, no DI, `HashSet<string>` allowlist/denylist, pure functions taking/returning plain collections" shape for `StapleStripper` (staple UNION >60%-frequency cut), `CategoryCounter` (multi-bucket counting — see D-06 note below), and `FolderWeighting`.

**Pure static math helper** (`DeckFlow.Core/Manabase/KarstenManabase.cs`, verified read lines 1-55 of 198):
```csharp
public static class KarstenManabase
{
    public static double SingletonLandTarget(int totalCards, int commanderCount, double averageManaValue, double rampAndDrawUnderThree, double fastMana = 0, double mdfcCommon = 0, double mdfcMythic = 0)
    {
        double scale = (totalCards - commanderCount) / 60.0;
        double interior = 19.59 + (1.90 * averageManaValue) + (0.27 * commanderCount);
        return (scale * interior) - (0.28 * rampAndDrawUnderThree) - fastMana - (0.74 * mdfcCommon) - (0.38 * mdfcMythic) - 1.35;
    }
}
```
Use this shape (static class, no `this`, plain numeric/collection parameters, doc comment explaining the formula's provenance) for `LiftCalculator` (`Pr(A∩B)/(Pr(A)·Pr(B))`, D-07).

**Multi-bucket category assignment is already correct — reuse, do not reinvent** (D-06's "every qualifying bucket" requirement): `DeckFlow.Core/Reporting/CategoryFilter.IncludedOrFallback` (above) already returns ALL non-generic categories, never `.First()`/`.Take(1)`. `CategoryCounter` should consume its output directly rather than re-filtering.

---

### `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` (MODIFIED — extend, not reuse)

**Pitfall 4 (RESEARCH, confirmed by direct read):** the file (verified read in full, 73 lines) has exactly three dimensions today — `Archetypes`, `Brackets`, `CardCategories` — **no staple/land/rock list exists**. D-05's "curated `ContentTagVocabulary` staple set" must be **authored net-new**, following the exact existing idiom:
```csharp
// DeckFlow.Core/Knowledge/ContentTagVocabulary.cs:9-27 (existing dimension to imitate)
public static readonly IReadOnlySet<string> Archetypes = new HashSet<string>(
    StringComparer.OrdinalIgnoreCase)
{
    "voltron", "aristocrats", "stax", "combo", "control", "tokens",
    "spellslinger", "reanimator", "blink", "tribal", "lands", "ramp",
    "aggro", "midrange", "value-engine"
};
```
Add a new `Staples` (or similar) `IReadOnlySet<string>` following this exact `HashSet<string>(StringComparer.OrdinalIgnoreCase)` idiom, seeded from the P88 prototype's observed staples (Command Tower, Sol Ring, basics, Exotic Orchard, Negate, Arcane Signet, Rogue's Passage per RESEARCH Pitfall 4) — this is card-name data, not a `ContentTagDimension` validation dimension, so it may warrant its own property rather than routing through `IsValid(dimension, value)`.

---

### `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` (MODIFIED — new global lift-baseline read)

**Analog (existing scoped-read pattern to extend):** `DeckFlow.Core/Knowledge/CardCategoryRepository.cs` (internal, verified `GetCategoriesAsync` lines 1-57 of 638) — the facade delegation pattern in `CategoryKnowledgeRepository.cs` (verified read in full, 274 lines):
```csharp
// DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs:65-66 — facade delegates to internal collaborator
public Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
    => _cardCategory.GetCategoriesAsync(cardName, cancellationToken);
```
```csharp
// DeckFlow.Core/Knowledge/CardCategoryRepository.cs:36-57 — internal collaborator does the Dapper query
internal async Task<IReadOnlyList<string>> GetCategoriesAsync(string cardName, CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(cardName);
    await _schema.EnsureSchemaAsync(cancellationToken);
    await using var connection = CreateConnection();
    await connection.OpenAsync(cancellationToken);
    var categories = await connection.QueryAsync<string>(new CommandDefinition(
        """
        SELECT o.category
        FROM card_category_observations o
        JOIN cards c ON c.id = o.card_id
        WHERE c.normalized_card_name = @normalized
        GROUP BY o.category
        ORDER BY LOWER(o.category), o.category
        """,
        new { normalized = CardNormalizer.Normalize(cardName) },
        cancellationToken: cancellationToken)).ConfigureAwait(false);
    return CategoryFilter.IncludedOrFallback(categories);
}
```
**Gap (RESEARCH, confirmed absent by direct read):** no existing method aggregates category presence across the WHOLE corpus (`Pr(A)`, `Pr(A∩B)`). Add a new `internal` method to `CardCategoryRepository` following this exact shape (Dapper `QueryAsync`/`QuerySingleAsync` over a `GROUP BY category` / self-join, server-side aggregation — per RESEARCH Pitfall 2, do NOT pull raw rows and aggregate client-side), then expose it through `CategoryKnowledgeRepository`'s one-line facade delegation idiom shown above.

---

### `DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs` (Web-host orchestrator)

**Analog (exact pattern to replicate):** `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs`, verified read lines 395-469 of 607:
```csharp
// DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:408-463
ScryfallCardNameIndex index = await ResolveCardsAsync(deckCards, cancellationToken).ConfigureAwait(false);
// ... resolve each DeckEntry to ScryfallCardData via index.TryResolve, falling back to
//     _scryfallCardResolver.SearchFallbackCardAsync(entry.Name, cancellationToken) on miss ...
var deckEntries = new List<DeckCardEntry>();
foreach (DeckEntry entry in deckCards)
{
    if (index.TryResolve(entry.Name, entry.SetCode, entry.CollectorNumber, out var card))
    {
        deckEntries.Add(new DeckCardEntry { Card = card, Quantity = entry.Quantity, IsCommander = ... });
    }
}
IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(deckEntries);
ManabaseDeck deck = ManabaseClassifier.Classify(facts, isSingleton: true, rampCreditV2: rampCreditV2, landRampSim: landRampSim);
// then: ManabaseAnalyzer.Analyze(deck, mode, importance, ...) — pure Core, no HttpClient
```
This is the D-11/D-09 "Web resolves external data → hands off to pure Core static classes" seam — `MeasuredStyleProfileBuilder` should replicate it verbatim for each creator deck: resolve Scryfall cards → `ScryfallCardFactMapper.ToCardFacts` → `ManabaseClassifier.Classify` → `ManabaseAnalyzer.Analyze` (pure, already unit-tested — do not modify these three Core classes; only compose them).

**Combo density call site (D-08, exact reuse):** `DeckFlow.Web/Services/CommanderSpellbookService.cs` (verified read in full, 317 lines) — DI interface:
```csharp
// DeckFlow.Web/Services/CommanderSpellbookService.cs:42-51
public interface ICommanderSpellbookService
{
    Task<CommanderSpellbookResult?> FindCombosAsync(
        IReadOnlyList<DeckEntry> entries,
        CancellationToken cancellationToken = default);
}
```
Note the graceful-degradation contract: `FindCombosAsync` catches its own HTTP/parse exceptions internally and **returns `null`** rather than throwing (`CommanderSpellbookService.cs:117-141`) — `MeasuredStyleProfileBuilder` must treat a `null` result as "no combo data available, continue without it" (matches D-11's degrade-gracefully expectation), not as an error condition.

---

## Shared Patterns

### Polly named resilience pipeline (D-02/CS-04b)
**Source:** `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs:22-31`
**Apply to:** `CreatorProfileDeckCrawler`'s two net-new Archidekt HTTP calls (owner-resolve, deck-list)
```csharp
public static class ResiliencePipelineFactory
{
    public static IServiceCollection AddDeckFlowResiliencePipelines(this IServiceCollection services)
    {
        DeckFlowResiliencePipelineRegistry.AddResiliencePipeline<string, RestResponse>(services, "banlist", builder => BuildBanList(builder));
        DeckFlowResiliencePipelineRegistry.AddResiliencePipeline<string, RestResponse>(services, "spellbook", builder => BuildSpellbook(builder));
        DeckFlowResiliencePipelineRegistry.AddResiliencePipeline<string, RestResponse>(services, "tagger", builder => BuildTagger(builder));
        DeckFlowResiliencePipelineRegistry.AddResiliencePipeline<string, RestResponse>(services, "tagger-post", builder => BuildTaggerPost(builder));
        DeckFlowResiliencePipelineRegistry.AddResiliencePipeline<string, RestResponse>(services, "scryfall", builder => BuildScryfall(builder));
    }
}
```
Either reuse an existing named pipeline (none is Archidekt-specific today — `ArchidektApiDeckImporter`/`ArchidektRecentDecksImporter` both use a static `AsyncRetryPolicy<RestResponse>` field, the legacy Polly idiom, NOT the named-pipeline registry) or register a new `"archidekt"` pipeline here and resolve it via `ResiliencePipelineProvider<string>` — planner's call; either is house-consistent, but the named-pipeline registry is the more current pattern (per CLAUDE.md "HTTP / Resilience Conventions").

### Multi-category, never first-match (D-06)
**Source:** `DeckFlow.Core/Reporting/CategoryFilter.cs:39-49` (`IncludedOrFallback`)
**Apply to:** `CategoryCounter` and any Tagger-tail merge logic
Already returns every non-generic category; never introduce `.First()`/`.Take(1)` anywhere in the category pipeline (RESEARCH anti-pattern, explicit).

### Dialect-guarded store + test-seam ctor (creator-profile-source table)
**Source:** `DeckFlow.Core/Content/CreatorStyleProfileStore.cs:47-64, 168-192`
**Apply to:** `CreatorProfileSourceStore` and the creator-scoped deck cache store
`internal` ctor overload with `Func<CancellationToken, Task<DbConnection>>? connectionFactoryOverride` + `SemaphoreSlim _schemaGate` + separate `PostgresCreateTableSql`/`SqliteCreateTableSql` `const string` fields.

### Web-host HTTP test double (`StubHttpMessageHandler` + `FakeHttpClientFactory`)
**Source:** `DeckFlow.Web.Tests/Services/CommanderSpellbookServiceTests.cs:32-41`
**Apply to:** `CreatorProfileDeckCrawlerTests` (fixture the two new Archidekt JSON endpoints)
```csharp
private static CommanderSpellbookService BuildService(StubHttpMessageHandler stub, IMemoryCache? cache = null)
{
    var factory = new FakeHttpClientFactory(new Dictionary<string, HttpMessageHandler>
    {
        ["commander-spellbook"] = stub
    });
    return TestServiceFactory.CreateCommanderSpellbookService(factory, cache ?? new MemoryCache(new MemoryCacheOptions()));
}
```
Note RESEARCH's Wave-0 gap list also allows `RichardSzalay.MockHttp` (already a `DeckFlow.Web.Tests` dependency) as an alternative to `StubHttpMessageHandler` — both are house-legitimate; `StubHttpMessageHandler`/`FakeHttpClientFactory` is the more-established in-repo idiom for named-`IHttpClientFactory` services like this crawler.

### Round-trip persistence test harness (P94, to extend not duplicate)
**Source:** `DeckFlow.Core.Tests/CreatorStyleProfileStoreTests.cs` + `CreatorStyleProfileTestData.cs` (verified read in full)
**Apply to:** CS-10's `MeasuredMetric[]` round-trip (extend `CreateFullProfile` fixtures with populated staple-stripped/lift/folder-weighted metrics; extend `AssertProfilesEqual`)
```csharp
[Fact]
public async Task UpsertAsync_ThenGetBySlug_RoundTripsFullShape()
{
    var expected = CreatorStyleProfileTestData.CreateFullProfile("full-round-trip");
    await _store.UpsertAsync(expected);
    var actual = await _store.GetBySlugAsync(expected.Slug);
    Assert.NotNull(actual);
    CreatorStyleProfileTestData.AssertProfilesEqual(expected, actual!);
}
```
`CreatorStyleProfileTestData.CreateFullProfile` already builds a `MeasuredMetric` with a populated `MetricDistribution` (`Mean`/`Min`/`Max`/`StdDev`) — when the planner adds the D-10 nested `EffectiveSampleSize` field to `MetricDistribution`, extend this fixture and `AssertProfilesEqual` in the SAME file rather than creating a parallel fixture builder.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `DeckFlow.Core/Knowledge/MeasuredStyleExtraction/*.cs` (the 4 pure extraction classes as a *module*) | utility/transform | transform | Genuinely net-new pure-statistics module — no prior Core code does staple-strip/lift-math/folder-weighting; closest sibling patterns (`CategoryFilter`, `KarstenManabase`) are cited above for the *static-class-shape* convention only, not the algorithm itself |
| Two net-new Archidekt HTTP endpoints (`/api/users/?username=`, `/api/decks/v3/?ownerUsername=&pageSize=&page=`) | integration | request-response | Zero existing callers/models/tests (confirmed via grep scan per RESEARCH); build from the `ArchidektApiDeckImporter`/`ArchidektRecentDecksImporter` `RestClient`+`JsonDocument.Parse` idiom, not a direct copy of either |
| Global `Pr(A)`/`Pr(A∩B)` corpus-wide aggregate query | repository | CRUD (aggregate) | Confirmed absent from `CategoryKnowledgeRepository`/`CardCategoryRepository` (every existing method is per-card or per-commander scoped); illustrative target SQL shape is in `95-RESEARCH.md` "Code Examples" section — treat as a new method, not a reuse |

---

## Metadata

**Analog search scope:** `DeckFlow.Core/Content/`, `DeckFlow.Core/Integration/`, `DeckFlow.Core/Knowledge/`, `DeckFlow.Core/Manabase/`, `DeckFlow.Core/Reporting/`, `DeckFlow.Web/Services/`, `DeckFlow.Web/Services/Http/`, `DeckFlow.Web/Services/Manabase/`, `DeckFlow.Core.Tests/`, `DeckFlow.Web.Tests/Services/`
**Files scanned (read in full or targeted-section):** `CreatorStyleProfileStore.cs`, `ArchidektApiDeckImporter.cs`, `ArchidektApiUrl.cs`, `ArchidektRecentDecksImporter.cs`, `ArchidektDeckCacheSession.cs`, `ContentTagVocabulary.cs`, `CreatorStyleProfile.cs`, `CategoryFilter.cs`, `CategoryKnowledgeRepository.cs`, `CommanderSpellbookService.cs`, `CreatorSourceStore.cs`, `CardCategoryRepository.cs` (targeted), `ManabaseAnalysisService.cs` (targeted), `CreatorStyleProfileStoreTests.cs`, `CreatorStyleProfileTestData.cs`, `KarstenManabase.cs` (targeted), `ManabaseAnalyzer.cs` (targeted), `ResiliencePipelineFactory.cs` (targeted), `CommanderSpellbookServiceTests.cs` (targeted)
**Pattern extraction date:** 2026-07-11

## PATTERN MAPPING COMPLETE

**Phase:** 95 - measured-style-extractor
**Files classified:** 12 (new/modified artifact groups)
**Analogs found:** 12 / 12 (all role-matched or exact; 3 explicitly flagged "extend, not reuse" — `ContentTagVocabulary` staple set, `CategoryKnowledgeRepository` global-baseline read, the pure `MeasuredStyleExtraction` module itself)

### Coverage
- Files with exact analog: 6 (`CreatorProfileSourceStore`, per-deck fetch step, `MeasuredStyleProfileBuilder`'s Scryfall→CardFact→Karsten chain, combo-density call site, crawler test double, round-trip test harness)
- Files with role-match analog: 4 (`CreatorProfileDeckCrawler` pagination shell, creator-scoped deck cache shape, pure extraction module shape, `ContentTagVocabulary` extension idiom)
- Files with no analog (net-new, gap-confirmed): 3 (two Archidekt HTTP endpoints, global lift-baseline aggregate query, the extraction module's actual algorithm content)

### Key Patterns Identified
- Every Web-host service that needs external data feeding pure Core math follows the same seam: resolve externally (Scryfall/Archidekt/Tagger) in `DeckFlow.Web/Services/*`, hand off plain DTOs to `static` HttpClient-free classes in `DeckFlow.Core/*` (`ManabaseAnalysisService.cs:408-463` is the canonical example; D-11's contract is this exact seam already proven twice in-repo).
- Dialect-guarded Dapper stores share one skeleton: public ctor(s) → `internal` test-seam ctor with `connectionFactoryOverride` → `SemaphoreSlim`-gated `EnsureSchemaAsync` → paired `PostgresCreateTableSql`/`SqliteCreateTableSql` consts → Dapper `CommandDefinition` for reads/writes (`CreatorStyleProfileStore.cs` is the freshest, most-relevant instance — P94, same cycle).
- Two Archidekt-specific "crawler" classes already coexist in `DeckFlow.Core/Integration/` for genuinely different purposes (`ArchidektApiDeckImporter` = single deck by ID; `ArchidektRecentDecksImporter` = site-wide recent-decks HTML scrape) — the new owner-scoped crawler is a **third**, distinct class; do not conflate any of the three.
- Category multi-bucket assignment (D-06's "every qualifying bucket" rule) is already implemented correctly and reusable as-is via `CategoryFilter.IncludedOrFallback` — no new filtering logic needed, only correct composition.

### File Created
`.planning/phases/95-measured-style-extractor/95-PATTERNS.md`

### Ready for Planning
Pattern mapping complete. Planner can now reference analog patterns in PLAN.md files, with explicit "extend not reuse" flags carried forward for `ContentTagVocabulary`, the global lift-baseline query, and the pure `MeasuredStyleExtraction` module.
