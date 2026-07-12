# Phase 97: Profile Fusion + Conflict Ledger - Pattern Map

**Mapped:** 2026-07-12
**Files analyzed:** 8 new/modified files (+ 1 modified interface)
**Analogs found:** 8 / 8

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|--------------------|------|-----------|-----------------|----------------|
| `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` (MODIFY: `FusedTarget`/`FusedConflict`) | model | transform (additive record extension) | itself (P94 shape) + `CreatorStyleProfileSections.cs` JSON round-trip | exact |
| `DeckFlow.Core/Knowledge/ProfileFusion/StatedRuleRecencyCollapser.cs` (NEW) | utility | transform (pure reduce) | `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRuleReducer.cs` | exact |
| `DeckFlow.Core/Knowledge/ProfileFusion/StatedMetricKeyMapper.cs` (NEW) | utility | transform (pure lookup) | `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRulesMetricVocabulary.cs` | role-match |
| `DeckFlow.Core/Knowledge/ProfileFusion/ProfileFusionEngine.cs` (NEW) | service (pure Core) | CRUD-free transform (join + resolve) | `StatedRuleReducer.cs` (pure static class pattern) + `MeasuredStyleProfileBuilder.cs` (metric-key emission source) | role-match |
| `DeckFlow.Core/Content/{IContentVideoStore,ContentVideoStore}.cs` (EXTEND: `GetStatedRulesBySourceSlugAsync`) | model/service (persistence, read) | CRUD (read, joined query) | `ContentVideoStore.ListVideosPendingDistillAsync` (existing joined/filtered SELECT) + `ContentSourceStore.GetSourceAsync` (slug-keyed SELECT) | exact |
| `DeckFlow.CLI/{Program.cs,ContentKbCommandRunners.cs}` (NEW `fuse-profile` command) | route/controller (CLI command) | request-response (one-shot operator trigger) | `distillCommand` / `ContentKbCommandRunners.RunDistillAsync` | exact |
| `DeckFlow.Studio/Pages/CreatorStyleLedger.razor` (NEW) | component (Blazor page) | request-response (read-only render) | `DeckFlow.Studio/Pages/CreatorSources.razor` | exact |
| `DeckFlow.Studio/Program.cs` (EXTEND: DI registration) | config | — | existing `IContentSourceStore`/`IContentVideoStore` singleton registrations, lines 90-91 | exact |
| `DeckFlow.Studio/Shared/NavMenu.razor` (EXTEND: nav link) | component | — | existing `<NavLink>` list | exact |
| `DeckFlow.Core.Tests/ProfileFusion/*Tests.cs` (NEW) | test | — | `DeckFlow.Core.Tests/{CreatorStyleProfileStoreTests,CreatorStyleProfileTestData}.cs` | role-match |

## Pattern Assignments

### `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` (model, additive record extension)

**Analog:** itself (current shape) — this file is *modified*, not replaced.

**Current shape to extend (lines 81-133 — read directly this session):**
```csharp
public sealed record FusedTarget
{
    public required string Metric { get; init; }
    public required double Value { get; init; }
    public required double Weight { get; init; }
    public required string Source { get; init; }
    public FusedConflict? Conflict { get; init; }
}

public sealed record FusedConflict
{
    public required double StatedValue { get; init; }
    public required double MeasuredValue { get; init; }
    public required double Delta { get; init; }
}

public sealed record MetricDistribution
{
    public required double Mean { get; init; }
    public required double Min { get; init; }
    public required double Max { get; init; }
    public required double StdDev { get; init; }
    /// <summary>D-10 folder-weighted effective sample size (fractional), distinct from raw <see cref="MeasuredMetric.NumDecks"/>.</summary>
    public double? EffectiveSampleSize { get; init; }
}
```

**Rule to follow (additive-only, D-07):** add only new `init`-only, nullable/defaulted properties. Do
NOT rename/remove `Value`/`Weight`/`Source`/`Conflict` or `StatedValue`/`MeasuredValue`/`Delta` —
`CreatorStyleProfileTestData.AssertProfilesEqual` and `CreatorStyleProfileStoreTests
.UpsertAsync_ThenGetBySlug_RoundTripsFullShape` assert full record equality on these exact fields
today and MUST stay green. New fields default to `null` on both `expected`/`actual` sides of every
existing assertion (System.Text.Json round-trips new nullable properties transparently — see
`CreatorStyleProfileSections.SerializeSection<T>`/`DeserializeSection<T>`, the JSON-column
(de)serializer this record is persisted through, no custom converters).

RESEARCH.md's proposed starting shape (Condition, StatedMin/StatedMax, MeasuredValue, NumDecks,
EffectiveSampleSize, Verdict, SourceClip, VideoDateUtc, Confidence on `FusedTarget`;
BandRelativePercent + Winner on `FusedConflict`) is a reasonable starting point — planner has
discretion on exact names per CONTEXT.md.

---

### `DeckFlow.Core/Knowledge/ProfileFusion/StatedRuleRecencyCollapser.cs` (NEW, utility, pure transform)

**Analog:** `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRuleReducer.cs` (full file, 65 lines)
— this is a near line-for-line template for D-09. Copy its shape almost verbatim: a static class with
one `Reduce`/`Collapse` method, a `Dictionary<Key,(Candidate,Index)>` bucket pass, an internal
`sealed record` key type, and a private `ShouldReplace` comparator.

**Core pattern to copy (full source, lines 1-65):**
```csharp
namespace DeckFlow.Core.Knowledge.StatedRulesExtraction;

public static class StatedRuleReducer
{
    public static IReadOnlyList<StatedRuleCandidate> Reduce(IReadOnlyList<StatedRuleCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0) return [];

        var buckets = new Dictionary<StatedRuleReducerKey, (StatedRuleCandidate Candidate, int Index)>();
        for (int index = 0; index < candidates.Count; index++)
        {
            StatedRuleCandidate candidate = candidates[index];
            var key = new StatedRuleReducerKey(candidate.Metric, candidate.Condition ?? string.Empty, candidate.Comparator);
            if (!buckets.TryGetValue(key, out var current) || ShouldReplace(current.Candidate, candidate))
            {
                buckets[key] = (candidate, index);
            }
        }
        return buckets.OrderBy(pair => pair.Value.Index).Select(pair => pair.Value.Candidate).ToList();
    }

    private static bool ShouldReplace(StatedRuleCandidate current, StatedRuleCandidate challenger)
    {
        if (challenger.Confidence > current.Confidence) return true;
        if (challenger.Confidence < current.Confidence) return false;
        return challenger.VideoDateUtc > current.VideoDateUtc;
    }
}

internal sealed record StatedRuleReducerKey(string Metric, string Condition, string Comparator);
```

**Adaptation for D-09:** key on `(Metric, Condition)` only (drop `Comparator` from the key — D-09
says same `(metric, condition)`, not `(metric, condition, comparator)`); `ShouldReplace` should be
recency-only (`challenger.VideoDateUtc > current.VideoDateUtc`), not confidence-first, since D-09 is
explicitly "keep the newest by `video_date`." **Critical divergence from the analog:** D-09 requires
the superseded rule to **stay visible in the ledger as history** — `StatedRuleReducer.Reduce` silently
drops losers. This collapser must return *both* the winner set and the shadowed/superseded set (e.g.
a tuple or a small result record `{ Active, Superseded }`), not just a winner list.

---

### `DeckFlow.Core/Knowledge/ProfileFusion/StatedMetricKeyMapper.cs` (NEW, utility, pure lookup)

**Analog:** `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRulesMetricVocabulary.cs` (full file,
36 lines) — same "static class, static readonly closed set/map, `OrdinalIgnoreCase`" idiom.

**Pattern to copy (full source):**
```csharp
using DeckFlow.Core.Knowledge;

namespace DeckFlow.Core.Knowledge.StatedRulesExtraction;

public static class StatedRulesMetricVocabulary
{
    public static readonly IReadOnlySet<string> Metrics = new HashSet<string>(
        ContentTagVocabulary.CardCategories,
        StringComparer.OrdinalIgnoreCase)
    {
        "karsten:target_lands", "karsten:land_delta", "karsten:health_score",
        "combo_density:included_per_deck",
        "land_count", "interaction", "opener_probability", "pip_distribution",
        "power_level_philosophy",
    };

    public static readonly IReadOnlySet<string> Comparators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gte", "lte", "eq", "range",
    };
}
```

**Ground truth for the mapping table this new file must encode** (RESEARCH.md "Common Pitfalls" #1,
verified live against `MeasuredStyleProfileBuilder.cs` metric-emission sites, lines 175/196/247):
```csharp
// Source: DeckFlow.Web/Services/CreatorStyle/MeasuredStyleProfileBuilder.cs
// line 175: Metric = $"category_ratio:{category}"                    (11 CardCategories keys — PREFIX map)
// line 196: Metric = $"lift:{item.CategoryA}|{item.CategoryB}"       (excluded from stated vocab — never joins)
// line 247: Metric = metricName                                     (karsten:*/combo_density:* — EXACT match)
```
Mapping to build: 11 category names (`ramp`,`removal`,`draw`,`finishers`,`win-cons`,`counter`,
`protection`,`board-wipe`,`tutor`,`recursion`,`utility`) → `category_ratio:{name}`; 4 keys
(`karsten:target_lands`,`karsten:land_delta`,`karsten:health_score`,`combo_density:included_per_deck`)
→ identity; `land_count` → derived (`karsten:target_lands` value + `karsten:land_delta` value, see
`ManabaseReport.LandDelta` at `DeckFlow.Core/Manabase/ManabaseModels.cs:654-660`); the remaining
5 stated-only keys (`interaction`,`opener_probability`,`pip_distribution`,`power_level_philosophy`
+ whichever category has no measured counterpart) → no mapping, routes to philosophy/stated-only
(D-06).

---

### `DeckFlow.Core/Knowledge/ProfileFusion/ProfileFusionEngine.cs` (NEW, service, pure Core transform)

**Analog:** `StatedRuleReducer.cs` for the static-class/pure-function shape; `MeasuredStyleProfileBuilder.cs`
only as the **source of the `MeasuredMetric.Metric` vocabulary** it must join against (do NOT copy its
HTTP/async patterns — that class lives in `DeckFlow.Web`, is NOT pure-Core, and CS-20 forbids any
Web/HTTP reference from the new engine).

**Signature shape to follow (mirrors `StatedRuleReducer.Reduce`'s static, single-entry-point style):**
```csharp
namespace DeckFlow.Core.Knowledge.ProfileFusion;

public static class ProfileFusionEngine
{
    public static IReadOnlyList<FusedTarget> Fuse(
        IReadOnlyList<MeasuredMetric> measured,
        IReadOnlyList<StatedRuleCandidate> statedRules)
    {
        ArgumentNullException.ThrowIfNull(measured);
        ArgumentNullException.ThrowIfNull(statedRules);
        // 1. StatedRuleRecencyCollapser.Collapse(statedRules) -> (Active, Superseded)  [D-09]
        // 2. For each active stated rule: StatedMetricKeyMapper.TryMap(rule.Metric, out measuredKey)  [D-08/Pitfall 1]
        // 3. Classify measuredKey is null ? philosophy : observable                     [D-06]
        // 4. Join on (measuredKey, rule.Condition) against measured                     [D-08]
        // 5. ConflictCalculator.Evaluate(...) -> verdict + FusedConflict                [D-04/D-05]
        // 6. Resolve Value = observable ? measured.Value : rule.Value ?? band midpoint  [D-06]
        ...
    }
}
```

**Zero Web/HTTP references (CS-20 enforcement):** verify at plan/executor time with
`dotnet build DeckFlow.Core` and a `grep -rn "using DeckFlow.Web\|using RestSharp\|using Microsoft.AspNetCore" DeckFlow.Core/Knowledge/ProfileFusion/` — must return nothing. This mirrors how
`StatedRulesExtraction/*.cs` and `StatedRuleReducer.cs` themselves have zero such references today
(confirmed: `StatedRuleReducer.cs`'s only `using` is the implicit namespace, no Web/HTTP import at all).

---

### `DeckFlow.Core/Content/{IContentVideoStore,ContentVideoStore}.cs` (EXTEND, model/service, CRUD read)

**Analog:** `ContentVideoStore.ListVideosPendingDistillAsync` (joined/filtered SELECT with EXISTS
subqueries) for the query-shape convention, and `ContentSourceStore.GetSourceAsync`/its
`source_slug`-keyed SELECT for the slug-join convention.

**Interface addition pattern** (mirror the existing optional-with-default-throw convention used
throughout `IContentVideoStore.cs`, e.g. lines 143-148 `InsertStatedRuleAsync`):
```csharp
// Source: DeckFlow.Core/Content/IContentVideoStore.cs:143-148 (existing sibling method, exact shape to mirror)
Task<long> InsertStatedRuleAsync(
    long videoId,
    StatedRuleCandidate rule,
    int sortOrder,
    CancellationToken cancellationToken = default)
    => throw new NotSupportedException("This content video store does not support stated-rule inserts.");
```
New method should follow the identical `=> throw new NotSupportedException(...)` default-body idiom
so other `IContentVideoStore` implementations (if any exist beyond the SQLite/Postgres one) don't
break:
```csharp
Task<IReadOnlyList<StatedRuleCandidate>> GetStatedRulesBySourceSlugAsync(
    string sourceSlug,
    CancellationToken cancellationToken = default)
    => throw new NotSupportedException("This content video store does not support stated-rule reads.");
```

**Query pattern to copy (join shape, adapted from `ListVideosPendingDistillSql`,
`ContentVideoStore.cs` lines 527-554, and `content_stated_rules`/`content_videos`/`content_sources`
schema at lines 762-780 Postgres / 835-853 SQLite):**
```csharp
// Source: DeckFlow.Core/Content/ContentVideoStore.cs:527-554 (existing joined+filtered SELECT, shape to mirror)
private const string ListVideosPendingDistillSql = """
    SELECT v.id, v.source_id, v.youtube_video_id, v.rss_guid, v.title, v.video_url,
           v.published_utc, v.transcript_status, v.created_utc
      FROM content_videos v
     WHERE v.source_id = @sourceId
       AND v.transcript_status IN ('captions','whisper')
       AND EXISTS (SELECT 1 FROM content_transcripts t WHERE t.video_id = v.id)
     ORDER BY v.id;
    """;

// NEW read (adapt the join to key on content_sources.source_slug instead of a passed-in sourceId,
// mirroring ContentSourceStore.GetSourceAsync's source_slug SELECT):
private const string GetStatedRulesBySourceSlugSql = """
    SELECT sr.category, sr.metric, sr.value, sr.value_min, sr.value_max, sr.comparator,
           sr.condition, sr.clip_ts AS ClipTimestampSeconds, sr.source_clip, sr.confidence,
           sr.card_reference, sr.card_grounded, sr.video_date_utc AS VideoDateUtc
      FROM content_stated_rules sr
      JOIN content_videos v ON v.id = sr.video_id
      JOIN content_sources s ON s.id = v.source_id
     WHERE s.source_slug = @sourceSlug
     ORDER BY sr.video_id, sr.sort_order;
    """;
```
Parameterize via Dapper `new { sourceSlug }` (never string-concatenate — matches every existing store
method in this file, e.g. `InsertVideoAsync`'s `new { sourceId, youtubeVideoId, ... }` at line 101).

**Insert-side shape already shipped** (`InsertStatedRuleSql`, `ContentVideoStore.cs` lines 615-649) —
useful for confirming exact column names/casing the new SELECT must match:
```csharp
private const string InsertStatedRuleSql = """
    INSERT INTO content_stated_rules (
      video_id, category, metric, value, value_min, value_max, comparator, condition,
      clip_ts, source_clip, confidence, card_reference, card_grounded, video_date_utc, sort_order)
    VALUES (@videoId, @Category, @Metric, @Value, @ValueMin, @ValueMax, @Comparator, @Condition,
      @clipTs, @SourceClip, @Confidence, @CardReference, @CardGrounded, @videoDateUtc, @sortOrder)
    RETURNING id;
    """;
```

---

### `DeckFlow.CLI/{Program.cs,ContentKbCommandRunners.cs}` (NEW `fuse-profile` command)

**Analog:** the `distill` command end-to-end wiring.

**Command declaration + option pattern** (`DeckFlow.CLI/Program.cs`, mirrors lines 94-98, 160-163,
222, 322-325):
```csharp
// Source: DeckFlow.CLI/Program.cs:94-98 (existing distill command declaration, shape to mirror)
var distillCommand = new Command("distill", "Distill harvested transcripts into Content KB artifacts.");
var distillDbOption = new Option<FileInfo?>("--db") { Description = "Path to the content KB database. Defaults to artifacts/content-kb.db." };
var distillLimitOption = new Option<int>("--limit", () => 5) { Description = "Videos to distill per enabled source." };
...
// line 222: rootCommand.AddCommand(distillCommand);
// lines 322-325:
distillCommand.SetHandler((FileInfo? db, int limit, bool dryRun, string? videoIds) =>
{
    Environment.ExitCode = ContentKbCommandRunners.RunDistillAsync(db, limit, dryRun, Log.Logger, CancellationToken.None, ContentKbCommandRunners.ParseVideoIds(videoIds)).GetAwaiter().GetResult();
}, distillDbOption, distillLimitOption, distillDryRunOption, distillVideoIdsOption);
```
New command needs a `--slug` (or reuse `--db`) option instead of `--limit`/`--dry-run`/`--video-ids`,
since fusion operates on one creator profile, not a batch of pending videos.

**Runner method pattern** (`ContentKbCommandRunners.RunDistillAsync`, full method,
`DeckFlow.CLI/ContentKbCommandRunners.cs` lines 80-122):
```csharp
public static async Task<int> RunDistillAsync(
    FileInfo? db, int limit, bool dryRun, Serilog.ILogger logger, CancellationToken ct,
    IReadOnlyList<string>? videoIds = null)
{
    ArgumentNullException.ThrowIfNull(logger);
    try
    {
        var dbPath = ContentKbCliPaths.ResolveDatabasePath(db);
        // ... construct store(s), run pure operation, persist result ...
        return 0;
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        logger.Error(exception, "Content KB distill failed.");
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}
```
New `RunFuseProfileAsync(FileInfo? db, string slug, Serilog.ILogger logger, CancellationToken ct)`
should follow the identical try/catch(Exception when not OperationCanceledException)/exit-code
convention: construct `CreatorStyleProfileStore` + `ContentVideoStore` against `ContentKbCliPaths
.ResolveDatabasePath(db)`, call `GetBySlugAsync`/`GetStatedRulesBySourceSlugAsync`, call
`ProfileFusionEngine.Fuse(...)`, `UpsertAsync` the profile `with { FusedTargets = ... }`.

---

### `DeckFlow.Studio/Pages/CreatorStyleLedger.razor` (NEW, component, read-only render)

**Analog:** `DeckFlow.Studio/Pages/CreatorSources.razor` (full file read, 261 lines).

**Page skeleton to copy (imports + inject + lifecycle, lines 1-6, 101-144 adapted):**
```razor
@page "/creator-style-ledger"
@inherits StudioCancellableComponentBase
@using DeckFlow.Core.Content
@using DeckFlow.Core.Knowledge

<PageTitle>Creator Style Ledger</PageTitle>
<h1 class="h4 fw-semibold">Creator Style Ledger</h1>

@code {
    [Inject]
    private ICreatorStyleProfileStore ProfileStore { get; set; } = default!;

    private CreatorStyleProfile? _profile;
    private bool _loading = true;
    private string _error = string.Empty;

    protected override async Task OnInitializedAsync() => await ReloadAsync();

    private async Task ReloadAsync()
    {
        try
        {
            // Why: Task.Run moves the store call off the Blazor sync context (existing convention).
            _profile = await Task.Run(() => ProfileStore.GetBySlugAsync("salubrioussnail", Cts.Token), Cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            // Why: never echo exception.Message (may leak the DB path) — generic operator-safe copy only.
            _error = "Could not load the fused profile. Try again.";
        }
        finally
        {
            _loading = false;
            await SafeStateHasChangedAsync();
        }
    }
}
```

**Loading/error/empty-state UI pattern to copy** (`CreatorSources.razor` lines 37-62):
```razor
@if (!string.IsNullOrEmpty(_error))
{
    <div class="alert alert-danger py-2">@_error</div>
}
@if (_loading)
{
    <div class="d-flex align-items-center gap-2 mt-3">
        <span class="spinner-border spinner-border-sm text-primary" role="status" aria-label="Operation in progress">
            <span class="visually-hidden">Loading...</span>
        </span>
        <span class="text-muted">Loading...</span>
    </div>
}
else if (@* rows empty *@)
{
    <p class="text-muted mt-3">No fused targets yet.</p>
}
else
{
    <div class="table-responsive mt-3">
        <table class="table table-sm table-hover align-middle">
            <thead class="table-light"><tr><!-- th per D-12 column --></tr></thead>
            <tbody>@* foreach FusedTargets row, D-12: stated band · measured + coverage · resolved · verdict badge · source-clip + date *@</tbody>
        </table>
    </div>
}
```
Read-only, so this page needs **no** add/remove/`_operationInFlight` mutation plumbing that
`CreatorSources.razor` has (lines 13-35, 180-259) — those belong to the write-capable analog and
should NOT be copied.

**Nav registration pattern** (`DeckFlow.Studio/Shared/NavMenu.razor`, mirrors the existing 10-item
`<NavLink>` list, e.g. line 24-26):
```razor
<NavLink class="nav-link" href="creators">
    Creators
</NavLink>
```
Add a new `<NavLink class="nav-link" href="creator-style-ledger">Style Ledger</NavLink>` entry.

---

### `DeckFlow.Studio/Program.cs` (EXTEND, config, DI registration)

**Analog:** the existing `IContentSourceStore`/`IContentVideoStore` singleton registrations
(`DeckFlow.Studio/Program.cs` lines 90-91, confirmed no `ICreatorStyleProfileStore` registration
exists anywhere in this file — RESEARCH.md Pitfall 3).

```csharp
// Source: DeckFlow.Studio/Program.cs:90-91 (existing, exact pattern to mirror)
builder.Services.AddSingleton<IContentSourceStore>(_ => new ContentSourceStore(contentKbDatabasePath));
builder.Services.AddSingleton<IContentVideoStore>(_ => new ContentVideoStore(contentKbDatabasePath));

// NEW — add alongside these two:
builder.Services.AddSingleton<ICreatorStyleProfileStore>(_ => new CreatorStyleProfileStore(contentKbDatabasePath));
```

---

### `DeckFlow.Core.Tests/ProfileFusion/*Tests.cs` (NEW, test)

**Analog:** `DeckFlow.Core.Tests/CreatorStyleProfileStoreTests.cs` +
`DeckFlow.Core.Tests/CreatorStyleProfileTestData.cs` for the round-trip/assertion-helper convention;
`DeckFlow.Core.Tests/StatedRulesExtraction/` (implied sibling, per RESEARCH's project-structure
recommendation) for pure-Core unit-test shape.

**Round-trip test to keep GREEN (already exists, must not need modification per D-07):**
```csharp
// Source: DeckFlow.Core.Tests/CreatorStyleProfileStoreTests.cs:48-58
[Fact]
public async Task UpsertAsync_ThenGetBySlug_RoundTripsFullShape()
{
    var expected = CreatorStyleProfileTestData.CreateFullProfile("full-round-trip");
    await _store.UpsertAsync(expected);
    var actual = await _store.GetBySlugAsync(expected.Slug);
    CreatorStyleProfileTestData.AssertProfilesEqual(expected, actual!);
}
```
New xUnit test files (`StatedMetricKeyMapperTests.cs`, `ConflictCalculatorTests.cs`,
`ProfileFusionEngineTests.cs`, `StatedRuleRecencyCollapserTests.cs`) should follow the plain
`[Fact]`/`[Theory]` xUnit convention already used throughout `DeckFlow.Core.Tests`, grounded on the
D-02 prototype table (`docs/research/p89-p90-prototype-snail.md`: land 37-42 vs avg 37.4 ✅; ramp
7-12 vs avg 12.0 ✅; draw 13-18 vs avg 11.1 ⚠; wipes 3-5 vs ~1.2 ✅-philosophy; counters ≥8 vs
control-only ⚠) as calibration fixtures, per CI-2/D-02/D-03.

## Shared Patterns

### Pure-Core static-class transform (no DI, no I/O)
**Source:** `DeckFlow.Core/Knowledge/StatedRulesExtraction/StatedRuleReducer.cs` (full file)
**Apply to:** `StatedRuleRecencyCollapser`, `StatedMetricKeyMapper`, `ProfileFusionEngine`,
`ConflictCalculator`, `MetricClassification` — all pure Core, `static class` + one entry-point method
+ `ArgumentNullException.ThrowIfNull` guards, zero Web/HTTP/AspNet references (CS-20).

### Additive record extension over a JSON-column store
**Source:** `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` (current `FusedTarget`/`FusedConflict`)
+ `CreatorStyleProfileSections.SerializeSection<T>`/`DeserializeSection<T>`
**Apply to:** `FusedTarget`/`FusedConflict` extension — new properties must be nullable/`init`-only so
existing tests default both sides to `null` and stay green (D-07).

### Dialect-guarded, slug-parameterized SQL reads
**Source:** `DeckFlow.Core/Content/ContentVideoStore.cs` (`ListVideosPendingDistillSql`) +
`DeckFlow.Core/Content/ContentSourceStore.cs` (`source_slug`-keyed SELECT)
**Apply to:** the new `GetStatedRulesBySourceSlugAsync` — Dapper `DynamicParameters`/anonymous-object
params only, never string-concatenated SQL (ASVS V5, matches every existing store method in this
codebase).

### CLI command + runner pair
**Source:** `DeckFlow.CLI/Program.cs` (`distillCommand` declaration/registration/handler) +
`DeckFlow.CLI/ContentKbCommandRunners.cs` (`RunDistillAsync`)
**Apply to:** the new `fuse-profile` command — `Command` + `Option`s declared at top of `Program.cs`,
registered via `rootCommand.AddCommand(...)`, wired via `.SetHandler(...)` calling a
`ContentKbCommandRunners.RunFuseProfileAsync` static method with the same try/catch(exception is not
OperationCanceledException)/exit-code convention.

### Studio read-only page + DI registration
**Source:** `DeckFlow.Studio/Pages/CreatorSources.razor` (page skeleton, minus its write actions) +
`DeckFlow.Studio/Program.cs` lines 90-91 (registration pattern) +
`DeckFlow.Studio/Shared/NavMenu.razor` (nav link pattern)
**Apply to:** `CreatorStyleLedger.razor` + its `ICreatorStyleProfileStore` DI registration + nav entry.

## No Analog Found

None — every file in this phase's surface has a strong, directly-read, in-repo analog. The only
genuinely novel engineering (the join/classification/conflict math itself) still composes existing
idioms (`StatedRuleReducer`'s static pure-function shape); there is no structural gap requiring
RESEARCH.md's external code examples as a fallback.

## Metadata

**Analog search scope:** `DeckFlow.Core/Knowledge/`, `DeckFlow.Core/Knowledge/StatedRulesExtraction/`,
`DeckFlow.Core/Content/`, `DeckFlow.CLI/`, `DeckFlow.Studio/Pages/`, `DeckFlow.Studio/Shared/`,
`DeckFlow.Studio/Program.cs`, `DeckFlow.Core.Tests/`
**Files scanned (fully or targeted-range read this session):** `CreatorStyleProfile.cs`,
`StatedRuleCandidate.cs`, `StatedRulesMetricVocabulary.cs`, `StatedRuleReducer.cs`,
`ContentVideoStore.cs` (imports + SQL-constant section), `IContentVideoStore.cs` (full),
`CreatorStyleProfileStore.cs` (full), `CreatorSources.razor` (full), `Studio/Program.cs` (DI section),
`NavMenu.razor` (nav-link grep), `StudioCancellableComponentBase.cs` (Cts/SafeStateHasChangedAsync
grep), `CLI/Program.cs` + `ContentKbCommandRunners.cs` (distill command + runner sections),
`ContentSourceStore.cs` (source_slug SELECT grep), `MeasuredStyleProfileBuilder.cs` (Metric-emission
grep), `CreatorStyleProfileStoreTests.cs` + `CreatorStyleProfileTestData.cs` (round-trip section)
**Pattern extraction date:** 2026-07-12
