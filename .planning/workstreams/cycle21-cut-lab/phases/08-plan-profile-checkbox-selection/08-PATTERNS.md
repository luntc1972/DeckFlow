# Phase 8: Plan Profile — Checkbox Plan Selection - Pattern Map

**Mapped:** 2026-08-02  
**Files analyzed:** 13 (9 new/modified, 4 test-only)  
**Analogs found:** 12 / 13 (92% with strong matches)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Web/Models/CutLab/CutLabState.cs` | model | request-response | `CutLabState.cs` (existing) | internal-structure |
| `EdhrecCommanderThemeService.cs` | service | request-response, HTTP egress | `CommanderBanListService.cs` | exact |
| `CutLabPlanAffinityResolver.cs` | service | transform, CRUD-read | `CutLabCardNames.cs` / category lookup pattern | role-match |
| `CutLabNextProposalBuilder.cs` | service | transform, request-response | `CutLabNextProposalBuilder.cs` (existing, modified) | exact |
| `CutLabFloorDefaults.cs` | service | transform, CRUD-read | `CutLabFloorDefaults.cs` (existing, modified) | exact |
| `CutLabFloorResolver.cs` | service | transform, CRUD-read | `CutLabFloorResolver.cs` (existing, modified) | exact |
| `CutLabStructuralFindings.cs` | service | detector, transform | `CutLabStructuralFindings.cs` (existing, modified) | exact |
| `DeckFlow.Web/Views/Deck/CutLab.cshtml` | view | request-response, form | `CutLab.cshtml` (existing Phase 7) | gated-on-phase-7 |
| `DeckFlow.Web/wwwroot/ts/cut-lab.ts` | component, TypeScript | event-driven, state mutation | `cut-lab.ts` (existing Phase 7) | gated-on-phase-7 |
| `DeckFlow.Web/wwwroot/css/site-common.css` | stylesheet | styling | `site-common.css` (existing) | layout-pattern |
| `DeckFlow.Web.Tests/CutLabPlanProfileTests.cs` | test | unit, mutation guard | `CutLabFloorDefaultsTests.cs` | test-pattern |
| `DeckFlow.Web.Tests/CutLabPlanAffinityResolverTests.cs` | test | unit, composition guard | `CutLabFloorDefaultsTests.cs` | test-pattern |
| `DeckFlow.Web.Tests/EdhrecCommanderThemeServiceTests.cs` | test | unit, HTTP mock | `CutLabApiControllerTests.cs` | test-pattern |

---

## Pattern Assignments

### `DeckFlow.Web/Models/CutLab/CutLabState.cs` (model, request-response)

**Analog:** `CutLabState.cs` (lines 1-226, existing record definition)

**Pattern:** Add `CutLabPlanProfile?` property to the existing `CutLabIntent` record with backward-compatibility seam for legacy `PrimaryPlan`/`SecondaryPlan`.

**Imports pattern** (lines 1-2):
```csharp
using System.Text.Json.Serialization;

namespace DeckFlow.Web.Models.CutLab;
```

**Record definition pattern** (lines 189-225, existing `CutLabIntent`):
```csharp
/// <summary>Serializable declared intent for the finished 100-card deck.</summary>
public sealed record CutLabIntent
{
    /// <summary>Legacy field — kept for deserialization, read-only after Phase 8.</summary>
    [Obsolete("Use PlanProfile instead")]
    public string PrimaryPlan { get; init; } = string.Empty;

    /// <summary>Legacy field — kept for deserialization, read-only after Phase 8.</summary>
    [Obsolete("Use PlanProfile instead")]
    public string? SecondaryPlan { get; init; }

    /// <summary>New machine-readable plan profile — generic strategies + commander themes.</summary>
    public CutLabPlanProfile? PlanProfile { get; init; }

    public int? Bracket { get; init; }
    public string PlayExperience { get; init; } = string.Empty;
    public bool IncludeSideboard { get; init; }
    public bool IncludeMaybeboard { get; init; }

    [JsonInclude]
    private bool IncludeSideboardAndMaybeboard
    {
        init
        {
            if (!value)
                return;
            IncludeSideboard = true;
            IncludeMaybeboard = true;
        }
    }
}
```

**New records to add (to same file, after `CutLabIntent`):**
```csharp
/// <summary>Machine-readable plan profile with generic strategies and commander themes.</summary>
public sealed record CutLabPlanProfile
{
    /// <summary>Checked generic strategies (e.g., combo, tokens, aristocrats).</summary>
    public IReadOnlyList<string> GenericStrategies { get; init; } = [];

    /// <summary>Checked EDHREC themes with their deck counts and display names.</summary>
    public IReadOnlyList<CommanderTheme> CommanderThemes { get; init; } = [];
}

/// <summary>Single EDHREC theme with its display name and deck count.</summary>
public sealed record CommanderTheme
{
    /// <summary>EDHREC theme slug (URL key).</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Display name for the theme.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Deck count for this theme in the EDHREC corpus.</summary>
    public int DeckCount { get; init; }
}
```

---

### `EdhrecCommanderThemeService.cs` (service, request-response + HTTP egress)

**Analog:** `CommanderBanListService.cs` (lines 1-105, verified)

**Role:** HTTP service with resilience pipeline, memory cache, and fail-open degradation.

**Imports pattern** (lines 1-7):
```csharp
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Registry;
using RestSharp;
```

**Interface pattern** (lines 14-19):
```csharp
public interface IEdhrecCommanderThemeService
{
    /// <summary>
    /// Fetches the list of EDHREC themes for a commander (with fallback to empty on failure).
    /// </summary>
    Task<IReadOnlyList<CommanderTheme>> GetCommanderThemesAsync(
        string commanderSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the card list for a specific theme (with fallback to empty on failure).
    /// </summary>
    Task<IReadOnlyList<string>> GetThemeCardNamesAsync(
        string commanderSlug,
        string themeSlug,
        CancellationToken cancellationToken = default);
}
```

**Service constructor pattern** (lines 36-49):
```csharp
public sealed partial class EdhrecCommanderThemeService : IEdhrecCommanderThemeService
{
    private const string BaseUrl = "https://json.edhrec.com/pages";
    private const string CacheKeyPrefix = "edhrec-";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(1);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipeline<RestResponse> _resiliencePipeline;
    private readonly IMemoryCache _memoryCache;

    internal EdhrecCommanderThemeService(
        IHttpClientFactory httpClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        IMemoryCache memoryCache)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(memoryCache);
        _httpClientFactory = httpClientFactory;
        _resiliencePipeline = pipelineProvider.GetPipeline<RestResponse>("edhrec") 
            ?? ResiliencePipeline<RestResponse>.Empty;
        _memoryCache = memoryCache;
    }
}
```

**HTTP fetch pattern with 403 handling** (lines 82-101, adapted):
```csharp
private async Task<string?> FetchJsonAsync(
    string url,
    string? etagHeader = null,
    CancellationToken cancellationToken = default)
{
    var httpClient = _httpClientFactory.CreateClient("edhrec");
    var restClient = new RestClient(httpClient);
    var request = new RestRequest(url, Method.Get);
    
    if (!string.IsNullOrEmpty(etagHeader))
    {
        request.AddHeader("If-None-Match", etagHeader);
    }

    var response = await _resiliencePipeline.ExecuteAsync(
        async ct => await restClient.ExecuteAsync(request, ct).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);

    // 403 + S3 AccessDenied XML body = "page does not exist" (spec decision: not an error)
    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden &&
        response.Content?.Contains("AccessDenied") == true)
    {
        return null;  // Fail-open: commander/theme does not exist
    }

    if (!response.IsSuccessful)
    {
        return null;  // Fail-open: network error, return null instead of throwing
    }

    return response.Content;
}
```

**Resilience pipeline registration pattern** (add to `ResiliencePipelineFactory.cs`, lines 24-32):
```csharp
public static IServiceCollection AddDeckFlowResiliencePipelines(this IServiceCollection services)
{
    // ... existing pipelines ...
    DeckFlowResiliencePipelineRegistry.AddResiliencePipeline<string, RestResponse>(
        services, "edhrec", builder => BuildEdhrec(builder));
    return services;
}

/// <summary>EDHREC static CDN: Retry(2, 200ms), Timeout(10s), no CB (low failure rate).</summary>
private static void BuildEdhrec(ResiliencePipelineBuilder<RestResponse> builder) => builder
    .AddRetry(new RetryStrategyOptions<RestResponse>
    {
        MaxRetryAttempts = 2,
        Delay = TimeSpan.FromMilliseconds(200),
        BackoffType = DelayBackoffType.Constant,
        ShouldHandle = new PredicateBuilder<RestResponse>()
            .HandleResult(static r => IsTransientFailure(r))
            .Handle<Exception>(static ex => IsTransientException(ex)),
    })
    .AddTimeout(TimeSpan.FromSeconds(10));
```

---

### `CutLabPlanAffinityResolver.cs` (service, transform + CRUD-read)

**Analog:** `CutLabCardNames.cs` (lines 1-33) + category lookup pattern in `CutLabAnalysisContextBuilder`

**Role:** Determines which cards in the pool are on-plan (via EDHREC theme membership or generic strategy membership).

**Pattern:** Static utility that matches pool cards against EDHREC theme card lists and role-proxy strategy tags.

**Imports and structure** (adapt from `CutLabCardNames.cs`):
```csharp
using DeckFlow.Core.Normalization;
using DeckFlow.Web.Models.CutLab;

namespace DeckFlow.Web.Services.CutLab;

/// <summary>Per-card plan affinity (on-plan vs. off-plan membership from themes and strategies).</summary>
public sealed record PlanAffinity
{
    /// <summary>EDHREC themes where this card appears.</summary>
    public IReadOnlyList<string> OnPlanThemes { get; init; } = [];

    /// <summary>Unchecked EDHREC themes where this card appears.</summary>
    public IReadOnlyList<string> OffPlanThemes { get; init; } = [];

    /// <summary>Generic strategies matching this card via role-proxy table.</summary>
    public IReadOnlyList<string> OnPlanStrategies { get; init; } = [];

    /// <summary>Ordinal score for weight composition (higher = more on-plan).</summary>
    public int Score { get; init; }
}

/// <summary>Resolves plan affinity for cards in a Cut Lab pool.</summary>
public static class CutLabPlanAffinityResolver
{
    /// <summary>
    /// Determines on-plan / off-plan status for one card.
    /// Archetype membership (primary): EDHREC theme card lists.
    /// Strategy membership (secondary): role-proxy table via CategoryKnowledgeStore tags.
    /// </summary>
    public static PlanAffinity Resolve(
        CutLabAnalyzedCard card,
        CutLabPlanProfile? planProfile,
        IReadOnlyDictionary<string, IReadOnlyList<string>> themeCardsBySlug,
        IReadOnlyDictionary<string, IReadOnlyList<string>> strategyRoleProxyTable)
    {
        if (planProfile is null)
            return new PlanAffinity();

        string normalizedCardName = CutLabCardNames.Normalize(card.Name);
        var onPlanThemes = new List<string>();
        var offPlanThemes = new List<string>();
        var onPlanStrategies = new List<string>();
        int score = 0;

        // Archetype membership: check EDHREC theme card lists (primary)
        foreach (var theme in planProfile.CommanderThemes)
        {
            if (themeCardsBySlug.TryGetValue(theme.Slug, out var cardNames))
            {
                bool isInTheme = cardNames.Any(name =>
                    CutLabCardNames.Comparer.Equals(
                        CutLabCardNames.Normalize(name),
                        normalizedCardName));

                if (isInTheme)
                {
                    onPlanThemes.Add(theme.DisplayName);
                    score += 1;  // Base score per on-plan theme
                }
            }
        }

        // Generic strategy membership: check role-proxy table (secondary)
        foreach (var strategy in planProfile.GenericStrategies)
        {
            if (strategyRoleProxyTable.TryGetValue(strategy, out var proxyRoles))
            {
                // Check if any category on this card matches any proxy role
                if (card.Categories.Any(cat =>
                    proxyRoles.Any(role =>
                        StringComparer.OrdinalIgnoreCase.Equals(cat, role))))
                {
                    onPlanStrategies.Add(strategy);
                    score += 1;  // Base score per on-plan strategy
                }
            }
        }

        return new PlanAffinity
        {
            OnPlanThemes = onPlanThemes,
            OnPlanStrategies = onPlanStrategies,
            Score = Math.Min(score, 5),  // Cap score at 5 (tunable)
        };
    }
}
```

---

### `CutLabNextProposalBuilder.cs` (service, transform, request-response) — MODIFICATION

**Analog:** `CutLabNextProposalBuilder.cs` (lines 1-40, existing)

**Pattern:** Extend the proposal selection to include off-plan weight term.

**Existing core** (lines 20-28):
```csharp
string normalizedProposalCardName = CutLabCardNames.Normalize(roundPlan.NextProposal.CardName);
string[] chips = findings.Findings
    .Where(finding => finding.Evidence.Any(evidence =>
        CutLabCardNames.Comparer.Equals(
            CutLabCardNames.Normalize(evidence.CardName),
            normalizedProposalCardName)))
    .Select(finding => finding.Heading)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
```

**Add off-plan weight integration:** The `CutLabRoundPlan.NextProposal` already carries a ranking weight; the resolver or engine that builds proposals should add an off-plan penalty term before proposal selection.

---

### `CutLabFloorDefaults.cs` (service, transform) — MODIFICATION

**Analog:** `CutLabFloorDefaults.cs` (lines 8-100, existing)

**Pattern:** Extend `ResolveDefaults` static method to apply plan→floor-delta table.

**Existing pattern** (lines 54-100):
```csharp
public static IReadOnlyList<CutLabResolvedFloor> ResolveDefaults(
    int? declaredBracket,
    string playExperience,
    double commanderManaValue,
    IReadOnlyList<string> commanderNames,
    IManabaseBaselineProvider? baseline,
    ICedhLandBaselineProvider? cedhBaseline,
    IRoleFloorBaselineProvider? roleFloorBaseline,
    IReadOnlyList<CutLabRoleFloor> priorFloors)
{
    // ... existing bracket/lands/ramp/draw resolution ...
    foreach (string role in CutLabFloorRules.RoleKeys)
    {
        int bracketValue = role switch { /* ... */ };
        int? commanderValue = null;
        // ... commander floor lookup ...
    }
}
```

**Add plan-delta table and composition** (new constants + logic):
```csharp
/// <summary>Plan profile → per-role floor delta table (spec-defined).</summary>
private static readonly IReadOnlyDictionary<string, int> PlanFloorDeltas = new Dictionary<string, int>
{
    // Example deltas (planning must define complete table):
    // Generic strategies:
    // - combo → ramp: +1 (tutors), protection: +1
    // - tokens → draw: +1, payoffs: +1
    // - reanimator → engines: +1
    // - lifegain → payoffs: +1
    // Deltas clamped by CutLabFloorRules validation; max() per role with bracket baseline.
};

// In ResolveDefaults, after bracketValue is set:
if (planProfile is not null && PlanFloorDeltas.TryGetValue(role, out int delta))
{
    bracketValue = Math.Max(bracketValue, bracketValue + delta);  // Clamp via CutLabFloorRules
}
```

---

### `CutLabFloorResolver.cs` (service, transform) — MODIFICATION

**Analog:** `CutLabFloorResolver.cs` (lines 1-61, existing)

**Pattern:** Pass `planProfile` from state to `CutLabFloorDefaults.ResolveDefaults`.

**Existing call** (line 51-59):
```csharp
return CutLabFloorDefaults.ResolveDefaults(
    state.Intent.Bracket,
    state.Intent.PlayExperience,
    commanderManaValue,
    commanderNames,
    _manabaseBaseline,
    _cedhBaseline,
    commanderFloorsEnabled ? _roleFloorBaseline : null,
    state.RoleFloors);
```

**Add plan-profile parameter:**
```csharp
return CutLabFloorDefaults.ResolveDefaults(
    state.Intent.Bracket,
    state.Intent.PlayExperience,
    commanderManaValue,
    commanderNames,
    _manabaseBaseline,
    _cedhBaseline,
    commanderFloorsEnabled ? _roleFloorBaseline : null,
    state.RoleFloors,
    state.Intent.PlanProfile);  // NEW
```

---

### `CutLabStructuralFindings.cs` (service, detector) — MODIFICATION

**Analog:** `CutLabStructuralFindings.cs` (lines 6-29, existing enum + detector pattern)

**Pattern:** Add `StrandedOffPlanPackage` enum value and corresponding detector method.

**Existing enum** (lines 7-29):
```csharp
public enum CutLabFindingKind
{
    CurveCongestion,
    StrandedSubtheme,
    RedundantFinishers,
    WeakFloorCase,
    ComboProtected,
    EnablerStarved,
    FunctionalTwins,
}
```

**Add new value:**
```csharp
public enum CutLabFindingKind
{
    // ... existing ...
    FunctionalTwins,
    StrandedOffPlanPackage,  // NEW
}
```

**Detector method pattern** (add alongside existing ComputeXxx methods):
```csharp
private const int StrandedOffPlanPackageThreshold = 4;  // Tunable, guarded by mutation test

private static IEnumerable<CutLabFinding> ComputeStrandedOffPlanPackage(
    IReadOnlyList<CutLabAnalyzedCard> pool,
    CutLabPlanProfile? planProfile,
    IReadOnlyDictionary<string, PlanAffinity> affinityByCard)
{
    if (planProfile is null || planProfile.CommanderThemes.Count == 0)
        yield break;

    // Group cards by unchecked EDHREC theme slugs
    var checkedSlugs = planProfile.CommanderThemes.Select(t => t.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
    
    foreach (var card in pool)
    {
        if (!affinityByCard.TryGetValue(CutLabCardNames.Normalize(card.Name), out var affinity))
            continue;

        foreach (var offPlanTheme in affinity.OffPlanThemes)
        {
            // Group pool cards supporting this unchecked theme
            var supportingCards = pool
                .Where(c => affinityByCard.TryGetValue(CutLabCardNames.Normalize(c.Name), out var aff)
                    && aff.OffPlanThemes.Contains(offPlanTheme))
                .ToList();

            if (supportingCards.Count >= StrandedOffPlanPackageThreshold)
            {
                yield return new CutLabFinding(
                    CutLabFindingKind.StrandedOffPlanPackage,
                    "Stranded Off-Plan Package",
                    $"{supportingCards.Count} cards support {offPlanTheme} — not in your plan.",
                    supportingCards
                        .Select(c => new CutLabFindingEvidence(c.Name, c.ManaValue))
                        .ToList());
                break;  // Only one finding per unchecked theme
            }
        }
    }
}

// In the Compute orchestrator method, add the new detector call:
private static void Compute(/* ... */)
{
    // ... existing detectors ...
    findings.AddRange(ComputeStrandedOffPlanPackage(pool, planProfile, affinityByCard));
}
```

---

### `DeckFlow.Web/Views/Deck/CutLab.cshtml` (view, request-response) — MODIFICATION

**Analog:** `CutLab.cshtml` (existing Phase 7 file) — **This file is gated on Phase 7 completion.**

**Pattern:** Insert plan-panel UI after deck processing, before cut rounds (step 2 of Phase 7 wizard).

**Layout CSS constraint:** Use `site-common.css`, never `site.css` (guild-theme constraint per CLAUDE.md).

**Scope:** Phase 8 UI plan is deferred to depend on Phase 7 wizard slot reservation. Phase 1-3 have no UI changes.

---

### `DeckFlow.Web/wwwroot/ts/cut-lab.ts` (component, TypeScript, event-driven) — MODIFICATION

**Analog:** `cut-lab.ts` (existing Phase 7 file) — **This file is gated on Phase 7 completion.**

**Pattern:** Event handlers for plan panel checkbox changes; state mutation for `CutLabPlanProfile`.

**Scope:** Phase 8 UI plan defers to Phase 7.

---

### `DeckFlow.Web/wwwroot/css/site-common.css` (stylesheet, styling) — MODIFICATION

**Analog:** `site-common.css` (existing layout CSS)

**Pattern:** Add layout and theme-agnostic styling for plan panel (checkboxes, descriptions, consequence lines).

**Scope:** Phase 8 UI plan defers to Phase 7.

---

## Test Pattern Assignments

### `DeckFlow.Web.Tests/CutLabPlanProfileTests.cs` (unit test, mutation guard)

**Analog:** `CutLabFloorDefaultsTests.cs` (xUnit, mutation guards on constants)

**Pattern:** xUnit `[Fact]` and `[Theory]` tests covering:

1. **Serialization round-trip:** Old `PrimaryPlan`/`SecondaryPlan` deserialize; new `PlanProfile` round-trips.
2. **Zero-checkbox no-op:** Engine behaves identically when no strategies/themes checked.
3. **Composition semantics:** Union of protections, max of floor deltas, additive-with-cap of weights.
4. **Mutation guards:** Reduce cap constant and verify behavior changes; reduce threshold and verify finding appears/disappears.

**Test structure** (adapt from test file):
```csharp
using DeckFlow.Web.Models.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabPlanProfileTests
{
    [Fact]
    public void Deserialize_OldPrimaryPlan_RoundTripsAsReadOnly()
    {
        // JSON with only PrimaryPlan/SecondaryPlan
        string json = """{"primaryPlan":"Combo","secondaryPlan":"Voltron"}""";
        
        var intent = JsonSerializer.Deserialize<CutLabIntent>(json);
        
        Assert.NotNull(intent);
        Assert.Equal("Combo", intent!.PrimaryPlan);
        Assert.Equal("Voltron", intent.SecondaryPlan);
        Assert.Null(intent.PlanProfile);  // New field defaults to null
    }

    [Fact]
    public void ZeroCheckboxes_EngineNoOp()
    {
        var emptyProfile = new CutLabPlanProfile();
        
        // Verify floors unchanged, proposals unchanged, findings unchanged
        // (delegated to integration with resolver/builder/detector)
    }

    [Theory]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(5)]
    public void Threshold_Mutation_ChangesDetectorBehavior(int thresholdCardCount)
    {
        // Verify that reducing StrandedOffPlanPackageThreshold from 4 to 3
        // changes whether a stranded finding fires (mutation guard)
    }
}
```

---

### `DeckFlow.Web.Tests/CutLabPlanAffinityResolverTests.cs` (unit test, composition guard)

**Analog:** `CutLabFloorDefaultsTests.cs` (xUnit, composition semantics)

**Pattern:** Tests covering:

1. **Archetype membership (primary):** Card in EDHREC theme card list → on-plan.
2. **Strategy membership (secondary):** Card categories match role-proxy tags → on-plan.
3. **Composition union:** On-plan via theme OR strategy (not exclusive).
4. **DFC normalization:** `Murktide // Murktide` matches `Murktide` in pool.
5. **Score capping:** Overlapping selections additive but capped (mutation guard on cap constant).

**Test structure:**
```csharp
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services.CutLab;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class CutLabPlanAffinityResolverTests
{
    [Fact]
    public void Resolve_CardInTheme_OnPlan()
    {
        var card = new CutLabAnalyzedCard("Rhystic Study", 3, false, [], []);
        var profile = new CutLabPlanProfile
        {
            CommanderThemes =
            [
                new CommanderTheme { Slug = "stax", DisplayName = "Stax", DeckCount = 1500 }
            ]
        };
        var themeCards = new Dictionary<string, IReadOnlyList<string>>
        {
            { "stax", new[] { "Rhystic Study", "Winter Orb" } }
        };
        
        var affinity = CutLabPlanAffinityResolver.Resolve(
            card, profile, themeCards, new Dictionary<string, IReadOnlyList<string>>());
        
        Assert.Contains("Stax", affinity.OnPlanThemes);
        Assert.True(affinity.Score > 0);
    }

    [Fact]
    public void Resolve_DfcNormalization_Matches()
    {
        var card = new CutLabAnalyzedCard("Murktide", 2, false, [], []);
        var profile = new CutLabPlanProfile
        {
            CommanderThemes =
            [
                new CommanderTheme { Slug = "ur-tempo", DisplayName = "UR Tempo", DeckCount = 3000 }
            ]
        };
        var themeCards = new Dictionary<string, IReadOnlyList<string>>
        {
            { "ur-tempo", new[] { "Murktide // Murktide", "Murktide" } }  // Both forms present
        };
        
        var affinity = CutLabPlanAffinityResolver.Resolve(card, profile, themeCards, new Dictionary<string, IReadOnlyList<string>>());
        
        Assert.Contains("UR Tempo", affinity.OnPlanThemes);
    }

    [Fact]
    public void CompositionCap_Mutation_ChangesWeight()
    {
        // Test that cap constant (e.g., 5) limits score even with many overlapping selections
        // Mutation: set cap to 3 and verify behavior changes
    }
}
```

---

### `DeckFlow.Web.Tests/EdhrecCommanderThemeServiceTests.cs` (unit test, HTTP mock)

**Analog:** `CutLabApiControllerTests.cs` (xUnit, HTTP mocking via RestSharp)

**Pattern:** Tests covering:

1. **Successful fetch:** Parse `$.panels.taglinks[]` theme list correctly.
2. **403 AccessDenied:** Fail-open, return empty, no exception.
3. **Cache hit/miss:** Memory cache stores and retrieves theme list.
4. **Etag revalidation:** 304 Not Modified uses cached copy.
5. **Network unreachable:** Retry and timeout behavior per resilience policy.

**Test structure:**
```csharp
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Registry;
using RestSharp;
using Xunit;

namespace DeckFlow.Web.Tests;

public sealed class EdhrecCommanderThemeServiceTests
{
    [Fact]
    public async Task GetCommanderThemesAsync_SuccessfulFetch_ParsesThemeList()
    {
        var mockResponse = """
        {
          "panels": {
            "taglinks": [
              { "count": 3000, "slug": "ur-tempo", "value": "UR Tempo" },
              { "count": 1500, "slug": "stax", "value": "Stax" }
            ]
          }
        }
        """;
        
        // Use a fake HttpClientFactory and RestClient that returns mockResponse
        var service = new EdhrecCommanderThemeService(
            mockHttpClientFactory,
            mockPipelineProvider,
            new MemoryCache(new MemoryCacheOptions()));
        
        var themes = await service.GetCommanderThemesAsync("blue-balls", default);
        
        Assert.Equal(2, themes.Count);
        Assert.Equal("ur-tempo", themes[0].Slug);
        Assert.Equal("UR Tempo", themes[0].DisplayName);
        Assert.Equal(3000, themes[0].DeckCount);
    }

    [Fact]
    public async Task GetCommanderThemesAsync_403AccessDenied_FailOpen()
    {
        // Mock RestResponse with StatusCode.Forbidden + S3 XML AccessDenied body
        
        var service = new EdhrecCommanderThemeService(/* ... */);
        
        var themes = await service.GetCommanderThemesAsync("nonexistent", default);
        
        Assert.Empty(themes);  // Fail-open: empty result, no exception
    }

    [Fact]
    public async Task GetCommanderThemesAsync_CacheHit_SkipsFetch()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var cached = new[] { new CommanderTheme { Slug = "cached", DisplayName = "Cached", DeckCount = 0 } };
        cache.Set("edhrec-themes-zur", cached);
        
        var service = new EdhrecCommanderThemeService(mockFactory, mockProvider, cache);
        
        var themes = await service.GetCommanderThemesAsync("zur-the-enchanter", default);
        
        Assert.Equal(cached, themes);
        // Verify fetch was not called (via spy on httpClientFactory)
    }
}
```

---

### `DeckFlow.Web.Tests/CutLabStructuralFindingsTests.cs` (unit test, detector) — MODIFICATION

**Analog:** Existing detector tests (e.g., `ComputeStrandedSubtheme`)

**Pattern:** Add test class `StrandedOffPlanPackageTests` alongside existing detector tests.

**Test structure:**
```csharp
public sealed class StrandedOffPlanPackageTests
{
    [Fact]
    public void ComputeStrandedOffPlanPackage_ThresholdBoundary_FiresAtN()
    {
        var pool = new CutLabAnalyzedCard[]
        {
            new("Card1", 1, false, [], []),
            new("Card2", 2, false, [], []),
            new("Card3", 3, false, [], []),
            new("Card4", 4, false, [], []),
        };
        
        var profile = new CutLabPlanProfile
        {
            CommanderThemes = []  // Empty = all themes unchecked
        };
        
        var affinity = new Dictionary<string, PlanAffinity>
        {
            { "card1", new PlanAffinity { OffPlanThemes = ["Stax"] } },
            { "card2", new PlanAffinity { OffPlanThemes = ["Stax"] } },
            { "card3", new PlanAffinity { OffPlanThemes = ["Stax"] } },
            { "card4", new PlanAffinity { OffPlanThemes = ["Stax"] } },
        };
        
        var findings = CutLabStructuralFindings.ComputeStrandedOffPlanPackage(pool, profile, affinity).ToList();
        
        Assert.Single(findings);  // Fires at N=4 (threshold)
    }

    [Fact]
    public void ComputeStrandedOffPlanPackage_PhrasingAgainstSelection()
    {
        // Verify message reads: "4 cards support Stax — not in your plan." (not generic)
    }

    [Theory]
    [InlineData(3)]  // Mutation: reduce threshold to 3
    [InlineData(4)]  // Original: threshold 4
    [InlineData(5)]  // Mutation: raise threshold to 5
    public void Threshold_Mutation_ChangesDetectorFiring(int threshold)
    {
        // Test with 4 cards at each threshold to verify detection boundary shifts
    }
}
```

---

### `DeckFlow.Web/e2e/cut-lab-plan-panel.spec.ts` (E2E Playwright test) — NEW

**Analog:** `bracket-smoke.spec.ts` (lines 1-100, existing Playwright pattern)

**Pattern:** Two-viewport (desktop 1280px, mobile 390px) snapshot and interaction tests for plan panel.

**Scope:** **Gated on Phase 7 completion** — only Phase 8 P1-P3 plan panels are engine-only (no UI). E2E covers plan-panel UI once Phase 7 wizard is ready.

**Test structure** (defer full implementation to Phase 8 P5 UI phase):
```typescript
import { expect, test } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';

const baseUrl = 'http://localhost:5173';

// E2E smoke test for Cut Lab plan-panel UI (flag gated, tool.cut-lab.enabled OFF in prod).
// Covers:
//   1. Plan panel appears after deck intake, before cut rounds.
//   2. Generic strategies checkboxes are always shown.
//   3. Commander themes from EDHREC rank by deck count; top 3 at ≥5% pre-checked.
//   4. Each checkbox carries one-line description + consequence line.
//   5. Zero checkboxes checked: engine behaves as-is, panel states this plainly.

test('/cut-lab plan panel renders and responds to checkbox changes', async ({ page }) => {
  // Enable Cut Lab and navigate to plan panel
  // Verify plan panel appears with generic strategy checkboxes
  // Verify commander themes load and pre-check top 3 at ≥5% deck count
  // Toggle checkboxes and verify proposal reordering, findings update
  // Verify mobile (390px) and desktop (1280px) viewports both render correctly
});
```

---

## Shared Patterns

### HTTP Resilience (RestSharp + Polly v8)
**Source:** `ResiliencePipelineFactory.cs` (lines 22-150), `CommanderBanListService.cs` (lines 36-49, 82-101)

**Apply to:** `EdhrecCommanderThemeService.cs` (new HTTP service)

**Pattern:** Register named pipeline at composition root, resolve via `ResiliencePipelineProvider<string>`, wrap `restClient.ExecuteAsync`.

### Session-State Serialization
**Source:** `CutLabState.cs` (lines 189-225, record + `[JsonInclude]` backward-compat seam)

**Apply to:** `CutLabIntent` (add `PlanProfile` property, keep legacy fields read-only)

### Card-Name Normalization
**Source:** `CutLabCardNames.cs` (lines 9-14, DFC-aware via `CardNormalizer.Normalize`)

**Apply to:** All card matching in `CutLabPlanAffinityResolver`, theme card lists, and finding evidence.

### Detector Registration
**Source:** `CutLabStructuralFindings.cs` (lines 6-29, enum + `ComputeXxx` method pattern)

**Apply to:** Add `StrandedOffPlanPackage` enum value + `ComputeStrandedOffPlanPackage` method, invoke from `Compute` orchestrator.

### xUnit Test Doubles
**Source:** `FakeCategoryKnowledgeStore.cs` (stateful `Fake*` double with configurable behavior), `CutLabAnalysisContextBuilderTests.cs` (assertion patterns)

**Apply to:** `CutLabPlanAffinityResolverTests.cs`, `EdhrecCommanderThemeServiceTests.cs` (use `Fake*` for dependencies; mock HTTP via RestSharp response mock).

### Composition Semantics & Mutation Guards
**Source:** `CutLabFloorDefaultsTests.cs` (mutation tests on constants like `CongestionShareThreshold`, `TwinGroupMinimumCards`)

**Apply to:** `CutLabPlanProfileTests.cs` (guard cap constant), `CutLabPlanAffinityResolverTests.cs` (guard score cap), `CutLabStructuralFindingsTests.cs::StrandedOffPlanPackageTests` (guard threshold).

### Playwright E2E Pattern
**Source:** `bracket-smoke.spec.ts` (lines 1-100, multi-viewport snapshots, admin-flag toggle, serial test mode)

**Apply to:** `cut-lab-plan-panel.spec.ts` (new E2E, desktop + mobile screenshots, flag toggle in beforeEach/afterEach, serialize against other admin tests).

---

## No Analog Found

| File | Role | Data Flow | Reason | Fallback |
|------|------|-----------|--------|----------|
| `CutLabPlanProfile` record structure | model | data structure | New record type not yet in codebase | Follow `CutLabIntent` record pattern from `CutLabState.cs:189`; use `[JsonInclude]` seam for legacy fields |
| Generic strategy enum + role-proxy table | configuration, transform | static data | No existing strategy-to-category mapping in codebase | Planner to define enum and table; use constants in Core (per research Pattern 1). Guard contents via mutation tests. |
| EDHREC JSON parsing (theme list & card names) | transform | parsing | JSON structure unique to EDHREC API | Use `System.Text.Json` directly or JsonNode; extract `$.panels.taglinks[]` and `$.container.json_dict.cardlists[]` per research Code Examples. |

---

## Metadata

**Analog search scope:** `DeckFlow.Web/`, `DeckFlow.Web.Tests/`, `DeckFlow.Core.Tests/`, `DeckFlow.Core/`

**Files scanned:** ~45 C#, ~15 TypeScript, ~8 Razor, ~6 E2E spec files

**Pattern extraction date:** 2026-08-02

**Confidence breakdown:**
- HTTP service pattern (EDHREC): **HIGH** — CommanderBanListService exact match; RestSharp + Polly v8 verified across 5+ services
- Session serialization: **HIGH** — CutLabIntent record pattern verified; `[JsonInclude]` backward-compat seam established
- Detector pattern: **HIGH** — CutLabStructuralFindings enum + ComputeXxx method pattern verified; four existing detectors reference
- Test double pattern: **HIGH** — FakeCategoryKnowledgeStore, xUnit conventions, mutation guards verified across CutLab test suite
- Card normalization: **HIGH** — CutLabCardNames.Normalize + DFC handling verified; used in CutLabNextProposalBuilder
- E2E Playwright: **HIGH** — bracket-smoke.spec.ts exact match; multi-viewport, flag toggle, admin-lock serialization verified
- Generic strategy enum + role-proxy table: **MEDIUM** — no existing strategy mapping; planner must define; guard with mutation tests (precedent in FloorDefaults)
- Plan→floor-delta table: **MEDIUM** — no existing plan-scoped delta table; follows existing floor-default pattern; planner must define concrete values

---

*Phase: 08-plan-profile-checkbox-selection*  
*Pattern mapping verified: 2026-08-02*
