# Phase 8: Plan Profile — Checkbox Plan Selection - Research

**Researched:** 2026-08-02
**Domain:** Machine-readable deck plan profile with EDHREC integration, deterministic engine effects
**Confidence:** HIGH

## Summary

Phase 8 replaces Cut Lab's deterministically-inert free-text `PrimaryPlan`/`SecondaryPlan` with a machine-readable profile: fixed generic strategy checkboxes plus commander-specific EDHREC themes. The existing codebase is well-prepared: session serialization on `CutLabIntent` (verified `CutLabState.cs:189`), HTTP egress follows RestSharp + direct Polly v8 consistently, combo-protected pattern is established in `CutLabStructuralFindings`, card-name normalization is DFC-aware via `CardNormalizer`, and test infrastructure uses xUnit with `Fake*` stateful doubles. EDHREC endpoints are static S3/CloudFront with no bot defense. No new packages required. All four engine effects (protect, reorder, floor deltas, stranded-package finding) have precedent in the codebase and can leverage existing architectural patterns without deviation.

**Primary recommendation:** Engine plans (P1, P2, P3) are independent and ship behind the existing `tool.cut-lab.enabled` flag. Implement `CutLabPlanProfile` record on `CutLabIntent`, follow the existing ResiliencePipeline pattern for `EdhrecCommanderThemeService`, compose plan affinity via role-proxy table + EDHREC card lists, and register the stranded-package detector alongside existing `CutLabFindingKind` detectors. Plan-panel UI plan defers to Phase 7 (reserves slot in wizard).

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- New `CutLabPlanProfile` record on `CutLabIntent` (replacing free-text fields in intake form).
- Generic strategies: fixed enum-backed list (~12 items) — combo, aristocrats, voltron, tokens, spellslinger, stax, reanimator, landfall, lifegain, +1/+1 counters, combat/battlecruiser, control.
- Commander themes: checked EDHREC themes as `{ Slug, DisplayName, DeckCount }`.
- EDHREC service: RestSharp + direct Polly v8 pattern (no standard-handler migration).
- Commander theme endpoint: `$.panels.taglinks[]` for theme list; lazy fetch per checked theme.
- Disk cache per commander and per commander/theme, beside role-floor corpus; etag revalidation honoring `cache-control`.
- Plan affinity resolver: archetype membership (primary) from EDHREC theme card lists; generic strategy membership from static role-proxy table.
- Engine effects composition: **union** of protections, **max** of floor deltas per role, **additive-with-cap** of ordering weights.
- Protect: on-plan cards join existing combo-protected pattern — pushed to back, still cuttable.
- Reorder: `CutLabNextProposalBuilder` gains off-plan weight term.
- Floors: plan→floor-delta table beside `CutLabFloorDefaults`; deltas clamped by existing `CutLabFloorRules` validation.
- Finding: "stranded off-plan package" detector in `CutLabStructuralFindings` — ≥N cards (default 4) supporting unchecked theme.
- Layout CSS in `site-common.css` (guild-theme constraint).
- All work ships behind existing `tool.cut-lab.enabled` flag (prod currently OFF).

### Claude's Discretion
- Exact service directory placement: `DeckFlow.Web/Services/CutLab/` vs `Services/Http/` — follow existing egress layout.
- Stranded-package threshold N: default 4; planning may tune.
- Pre-check share: top 3 themes at ≥5% each; planning may tune, default stands.
- Concrete values in plan→floor-delta table and additive ordering-weight cap.
- Role-proxy table contents beyond the two spec examples (aristocrats, tokens).

### Deferred Ideas (OUT OF SCOPE)
- Bracket-derived plan presets (bracket auto-checks strategies).
- What-if ADD suggestions from EDHREC high-synergy lists.

</user_constraints>

---

<phase_requirements>
## Phase Requirements

Proposed requirement IDs for Phase 8 (to be ratified into `REQUIREMENTS.md` before closeout):

| ID | Description | Research Support |
|----|-------------|------------------|
| PLPR-01 | `CutLabPlanProfile` model with generic strategies and commander themes replaces free-text fields; old fields kept read-only for deserialize. | `CutLabIntent` record defined at `CutLabState.cs:189-225`; existing session-state serialization verified. |
| PLPR-02 | Engine behaves identically when zero checkboxes checked — all four effects are no-op; panel states this. | `CutLabFindingKind` enum can accommodate no-op detectors; existing floor/proposal infrastructure supports conditional application. |
| PLPR-03 | On-plan cards protected via existing combo-protected pattern; off-plan cards surface first via ordering weight term in `CutLabNextProposalBuilder`. | `ComboProtected` finding kind documented at `CutLabStructuralFindings.cs:22`; proposal builder accesses `CutLabCardNames.Normalize` for card matching. |
| PLPR-04 | EDHREC themes resolved from commander page `$.panels.taglinks[]`; 403/unreachable degrades to "unavailable" while generic layer keeps working. | EDHREC endpoints confirmed static S3/CloudFront; existing ResiliencePipeline pattern in place for HTTP services; fail-open pattern documented in repo CLAUDE.md. |
| PLPR-05 | Overlapping selections compose as union (protection), max (floor deltas), additive-with-cap (ordering); each proven by mutation tests on constants. | Existing composition patterns in `CutLabFloorRules`, `CutLabFloorDefaults` provide precedent; constant mutation guards located in role-floor test suite. |
| PLPR-06 | "Stranded off-plan package" finding fires at threshold boundary and phrases message against selection. | `CutLabStructuralFindings` detector registration pattern verified; `CutLabFindingKind` enum extensible. |

**Note:** Phase 8 has no existing requirement IDs in `REQUIREMENTS.md` — these six are proposed and require ratification before closeout.

</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Plan profile data model | Backend / API | Database (session state) | Serialized on `CutLabIntent` per existing session-state pattern |
| Generic strategy enum & role-proxy table | Backend / Core | — | Core domain logic; existing `CategoryKnowledgeStore` pattern applies |
| EDHREC theme fetching & caching | Backend / HTTP | Disk cache | Static CDN, no bot defense; follows existing RestSharp + Polly v8 pattern |
| Plan affinity resolution (card membership) | Backend / Core | — | Card-name normalization via existing `CardNormalizer` (DFC-aware) |
| Engine effect composition | Backend / Core | — | Existing `CutLabFloorRules`, `CutLabFloorDefaults`, `CutLabNextProposalBuilder` provide integration points |
| Stranded-package finding detector | Backend / Core | — | New detector in `CutLabStructuralFindings`; follows existing detector registration pattern |
| Plan-panel UI | Frontend / Razor | Backend | Gated on Phase 7; inserts into wizard step slot Phase 7 reserves |

---

## Standard Stack

### Core
| Component | Location | Version/Pattern | Purpose | Why Standard |
|-----------|----------|-----------------|---------|--------------|
| Session serialization | `CutLabState.cs:189` | xUnit + in-memory | Store `CutLabIntent` including new `CutLabPlanProfile` | Existing session-state pattern; no framework changes needed |
| HTTP egress (EDHREC) | `Services/Http/ResiliencePipelineFactory.cs` | RestSharp + Polly v8 | Fetch commander themes & card lists from EDHREC | Established repo pattern; repo constraint forbids standard-handler migration |
| Card-name normalization | `DeckFlow.Core/Normalization/CardNormalizer.cs` | DFC-aware | Match card names across EDHREC lists and pool cards | Existing implementation handles `Front // Back` syntax correctly |
| Detector registration | `CutLabStructuralFindings.cs:7-29` | `CutLabFindingKind` enum | Register new stranded-package finder | Existing detector pattern: `ComboProtected`, `EnablerStarved`, `FunctionalTwins` all use this enum |
| Test infrastructure | `DeckFlow.Web.Tests/` | xUnit + `Fake*` doubles | Unit and integration tests per CLAUDE.md conventions | Repo standard for CutLab tests; mutation guards on constants demonstrated in floor tests |

### Supporting
| Component | Location | When to Use | Notes |
|-----------|----------|-------------|-------|
| `IMemoryCache` | `Program.cs` (composition root) | EDHREC fetch failures → in-memory result | Existing cache pattern; `CommanderBanListService` example shows precedent |
| `Polly.Registry.ResiliencePipeline<T>` | `Services/Http/` | Per-service resilience policy | Five pipelines already registered; new EDHREC pipeline mirrors existing pattern |
| `DeckFlow.Core.Reporting.CategoryKnowledgeRow` | Core knowledge layer | Role-proxy table lookup | Existing row structure carries role name and count; suitable for membership resolution |
| Disk cache (etag revalidation) | Role-floor corpus pattern | EDHREC theme member cache | `RoleFloorBaselineProvider.cs` demonstrates precedent beside corpus files |

### Alternatives Considered
| Instead of | Could Use | Why Not |
|------------|-----------|---------|
| EDHREC static S3 fetch | EDHREC GraphQL API | No API tier exists for theme membership; static JSON is simpler and proven |
| Role-proxy table in Web layer | Push to Core as data file | Core layer is domain-pure; maintaining the static table as production code is the pattern |
| Generic-strategy membership via oracle text heuristic | Hand-authored role-proxy table | Heuristic brittleness is known issue (Phase 01.1 and 01.2 repair classifier defects); static curated table is more maintainable |
| One shared on-plan protection list | Composition (union) of generic + EDHREC | Spec decision: explicit composition semantics for overlapping selections, proven by mutation tests |

---

## Architecture Patterns

### System Architecture Diagram

```
User Input (Plan Selection)
    ↓
CutLabPlanProfile (new record on CutLabIntent)
    ├─→ GenericStrategies (enum-backed checkboxes)
    │   ↓
    │   Role-Proxy Table (Core)
    │   ↓
    │   CategoryKnowledgeStore (role-level tags)
    │
    └─→ CommanderThemes (EDHREC slugs)
        ↓
        EdhrecCommanderThemeService (new HTTP service)
        ├─→ GET /pages/commanders/<slug>.json (theme list)
        └─→ GET /pages/commanders/<slug>/<theme-slug>.json (card lists)
            ↓
            Disk Cache (beside role-floor corpus)

Plan Affinity Resolver (CutLabPlanAffinityResolver)
    ├─→ Archetype Membership (EDHREC card lists, primary)
    └─→ Generic Strategy Membership (role-proxy table)
        ↓
        PlanAffinity { OnPlanThemes, OffPlanThemes, Score }

Engine Effects (four parallel applications):
    ├─→ Protect: on-plan cards → existing ComboProtected pattern (pushed to back)
    ├─→ Reorder: off-plan weight → CutLabNextProposalBuilder term
    ├─→ Floors: plan→floor-delta table → max(bracket, commander) via CutLabFloorDefaults
    └─→ Finding: stranded-package detector → CutLabStructuralFindings + CutLabFindingKind
```

### Pattern 1: RestSharp + Direct Polly v8 (HTTP Resilience)
**What:** Per-service `ResiliencePipeline<RestResponse>` registered at composition time, resolved via `ResiliencePipelineProvider<string>`. Five named pipelines tuned per consumer (banlist, spellbook, tagger, scryfall). Direct `Polly.Registry` API, no standard `Microsoft.Extensions.Http.Resilience`.

**When to use:** All HTTP egress in DeckFlow; non-negotiable repo constraint per CLAUDE.md.

**Example:**
```csharp
// Source: CommanderBanListService.cs:36-49, verified at codebase
public sealed partial class CommanderBanListService : ICommanderBanListService
{
    private readonly ResiliencePipeline<RestResponse> _resiliencePipeline;

    internal CommanderBanListService(
        IHttpClientFactory httpClientFactory,
        ResiliencePipelineProvider<string> pipelineProvider,
        IMemoryCache memoryCache,
        Func<CancellationToken, Task<string>>? fetchPageAsync = null)
    {
        _resiliencePipeline = pipelineProvider.GetPipeline<RestResponse>("banlist") ?? ResiliencePipeline<RestResponse>.Empty;
        // ...
    }

    private async Task<string> FetchPageAsync(CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("commander-banlist");
        var restClient = new RestClient(httpClient);
        var request = new RestRequest(BannedListUrl, Method.Get);

        var response = await _resiliencePipeline.ExecuteAsync(
            async ct => await restClient.ExecuteAsync(request, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        // ...
    }
}
```

For `EdhrecCommanderThemeService`, implement identical pattern: register pipeline at composition root (suggest "edhrec" key in `ResiliencePipelineFactory`), resolve via provider, wrap `restClient.ExecuteAsync`.

### Pattern 2: Session-State Serialization on Intent Record
**What:** `CutLabIntent` is a serializable `record` with `[JsonInclude]` backward-compat seams for deprecated fields. New properties serialize alongside existing ones in session state.

**When to use:** Adding fields to the user's declared intent.

**Example from codebase** (`CutLabState.cs:189-225`):
```csharp
public sealed record CutLabIntent
{
    public string PrimaryPlan { get; init; } = string.Empty;
    public string? SecondaryPlan { get; init; }
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

**For Phase 8**, add `CutLabPlanProfile? PlanProfile { get; init; }` and keep `PrimaryPlan`/`SecondaryPlan` as read-only properties so in-flight sessions deserialize without exception.

### Pattern 3: Detector Registration via Enum + Compute Method
**What:** `CutLabFindingKind` enum lists detector types; each detector has a `ComputeXxx` static method in `CutLabStructuralFindings.cs`. Detectors are invoked in deterministic order from `Compute` orchestrator method.

**When to use:** Adding a new structural finding type.

**Example from codebase** (`CutLabStructuralFindings.cs:7-29`):
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

Detector methods: `ComputeCurveCongestion`, `ComputeStrandedSubtheme`, etc. (verified at `CutLabStructuralFindings.cs` ~329, ~426).

**For Phase 8**, add `StrandedOffPlanPackage` to the enum, implement `ComputeStrandedOffPlanPackage` method, and invoke from the orchestrator `Compute` method alongside existing detectors.

### Pattern 4: Card-Name Normalization (DFC-Aware)
**What:** `CutLabCardNames.Normalize` delegates to `CardNormalizer.Normalize` in Core, which handles `Front // Back` syntax correctly. Used for comparison via `CutLabCardNames.Comparer` (Ordinal).

**When to use:** Matching card names across different sources (deck lists, knowledge base, EDHREC theme lists).

**Example from codebase** (`CutLabCardNames.cs:9-14`):
```csharp
internal static class CutLabCardNames
{
    public static StringComparer Comparer { get; } = StringComparer.Ordinal;

    public static string Normalize(string cardName)
    {
        ArgumentNullException.ThrowIfNull(cardName);
        return CardNormalizer.Normalize(cardName);
    }
}
```

**For Phase 8**, use `CutLabCardNames.Normalize` consistently when matching EDHREC card list members against pool cards in the affinity resolver.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| HTTP resilience (retry, circuit break, timeout) | Custom retry loop with backoff | `Polly.Registry` + `ResiliencePipelineProvider<string>` (existing pattern) | Polly handles exponential backoff, jitter, circuit breaker state transitions, and per-pipeline tuning. Custom logic is error-prone and repo forbids standard handler. |
| Card-name matching across formats | Substring comparison or manual DFC splitting | `CardNormalizer.Normalize` + `CutLabCardNames.Comparer` (existing) | Handles `Front // Back`, curly apostrophes, and other normalization edge cases already discovered and fixed. |
| Session state persistence | Manual JSON serialization | `CutLabIntent` record with `[JsonInclude]` (existing pattern) | System.Text.Json handles init-only properties and backward compatibility automatically. |
| Disk cache with etag revalidation | Manual file I/O + header tracking | Role-floor corpus pattern (existing in repo) | Existing `RoleFloorBaselineProvider.cs` demonstrates cache location, `cache-control` parsing, and etag logic. |
| Plan-effect composition (union, max, cap) | Ad-hoc if-else per effect | Explicit composition semantics + mutation tests (spec requirement) | Overlapping selections must compose predictably; mutation tests on cap and threshold constants are the proven guard. |

**Key insight:** The repo has solved HTTP, normalization, and session serialization at scale. Leverage those solutions directly rather than re-deriving them for EDHREC.

---

## Common Pitfalls

### Pitfall 1: Ignoring EDHREC's 403 Response as "Page Not Found"
**What goes wrong:** Code treats HTTP 403 as a network error and retries indefinitely, eventually timing out.
**Why it happens:** S3 `AccessDenied` returns 403 with XML body; most APIs return 404 for missing pages.
**How to avoid:** Explicitly handle 403 + S3 `AccessDenied` body as "this page does not exist", not an error. Spec decision locked in CONTEXT.md.
**Warning signs:** Service timeout or hanging on commander that doesn't exist in EDHREC; logs showing retry backoff for 403.
**Verification:** Test with a non-existent commander slug (e.g., "asdfghjkl"); expect graceful "unavailable" state, not exception.

### Pitfall 2: DFC Name Mismatch in EDHREC Theme Lists
**What goes wrong:** Card "Murktide // Murktide" in EDHREC list fails to match "Murktide" (front face only) in pool.
**Why it happens:** EDHREC may include both front and back faces in some theme lists; pool normalization varies by source (Archidekt, Moxfield, Spellbook).
**How to avoid:** Always normalize both EDHREC card name and pool card name through `CutLabCardNames.Normalize` before comparison. Use ordinal (case-sensitive) comparer.
**Warning signs:** Cards marked as off-plan when they should be on-plan; thematic gaps that don't match EDHREC's theme documentation.
**Verification:** Test affinity resolver with known DFC commanders (Murktide in UR tempo, Solitude in white goodstuff); verify on-plan membership for both faces.

### Pitfall 3: Confusing Role-Proxy Membership with Archetype Membership
**What goes wrong:** Role-proxy table (generic strategies) is treated as the primary archetype source; EDHREC theme lists are secondary. Results in off-plan cards marked as on-plan via role-proxy alone.
**Why it happens:** Spec explicitly states archetype membership is *primary* from EDHREC; role-proxy is *secondary* for generic strategies only.
**How to avoid:** Split `PlanAffinity` logic: EDHREC theme card lists are checked first and gate archetype status; role-proxy table is checked independently for generic strategy membership. Composition is union of both layers.
**Warning signs:** Commander-specific theme finding empty while generic-strategy finding lists many cards.
**Verification:** Test with a commander that has archetypes not in generic list (e.g., Rhystic Study in "Stax", which isn't a defined generic strategy). Expect it on-plan if theme is checked, off-plan if theme is unchecked; generic-strategy status should not override theme membership.

### Pitfall 4: Not Guarding the Additive Ordering-Weight Cap
**What goes wrong:** Two overlapping selections both add weight, exceeding the intended reordering severity; off-plan cards surface before on-plan even when they shouldn't.
**Why it happens:** Weight additive without a cap allows unbounded stacking; spec requires explicit cap + mutation test to guard it.
**How to avoid:** Define the cap constant in the resolver (spec decision: planning may tune value), apply `Math.Min(sum, cap)` in composition logic, add a mutation test that verifies reducing the cap changes the behavior.
**Warning signs:** Off-plan cards ranked higher than expected when two themes are checked; test mutation changes finding order.
**Verification:** Mutation test: set cap = 1 and verify off-plan weight never exceeds 1; reduce cap further and verify ordering changes appropriately.

### Pitfall 5: Losing In-Flight Sessions When Deserializing Old Intent
**What goes wrong:** User session from before Phase 8 has `PrimaryPlan`/`SecondaryPlan` but no `PlanProfile`; deserialization throws or silently drops the old fields.
**Why it happens:** New `record` field is not marked with `[JsonInclude]` or backward-compat seam; old fields are removed instead of kept read-only.
**How to avoid:** Add `PlanProfile` as `public`, keep `PrimaryPlan`/`SecondaryPlan` as public read-only properties (init-only or getter-only). Verify in unit tests that deserializing legacy JSON does not throw.
**Warning signs:** User in Cut Lab workflow reports "my session was lost" after deployment.
**Verification:** Test fixture: serialize `CutLabIntent` with only `PrimaryPlan`/`SecondaryPlan` set, deserialize into new code, verify all three properties round-trip correctly.

---

## Code Examples

Verified patterns from official sources:

### HTTP Service Registration and Resolution (Polly v8)
**Source:** `ResiliencePipelineFactory.cs:24-32, CommanderBanListService.cs:36-49`
```csharp
// In Program.cs (composition root) — ALREADY IN CODEBASE
services.AddDeckFlowResiliencePipelines();

// Register the new EDHREC pipeline — ADD TO ResiliencePipelineFactory.cs
public static IServiceCollection AddDeckFlowResiliencePipelines(this IServiceCollection services)
{
    // ... existing pipelines ...
    DeckFlowResiliencePipelineRegistry.AddResiliencePipeline<string, RestResponse>(
        services, "edhrec", builder => BuildEdhrec(builder));
    return services;
}

// In service constructor — PATTERN ESTABLISHED
private readonly ResiliencePipeline<RestResponse> _resiliencePipeline;

internal EdhrecCommanderThemeService(
    IHttpClientFactory httpClientFactory,
    ResiliencePipelineProvider<string> pipelineProvider,
    IMemoryCache? memoryCache = null)
{
    _resiliencePipeline = pipelineProvider.GetPipeline<RestResponse>("edhrec") 
        ?? ResiliencePipeline<RestResponse>.Empty;
    // ...
}

// In execution — PATTERN ESTABLISHED
private async Task<CommanderThemeDto[]> FetchThemesAsync(
    string commanderSlug, 
    CancellationToken cancellationToken)
{
    var httpClient = _httpClientFactory.CreateClient("edhrec");
    var restClient = new RestClient(httpClient);
    var request = new RestRequest($"https://json.edhrec.com/pages/commanders/{commanderSlug}.json");

    var response = await _resiliencePipeline.ExecuteAsync(
        async ct => await restClient.ExecuteAsync(request, ct).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);

    if (!response.IsSuccessful)
    {
        // Fail-open: commander data unavailable, generic strategies continue
        return Array.Empty<CommanderThemeDto>();
    }

    // Parse response.Content as JSON...
    return ParseThemes(response.Content);
}
```

### Session-State Record with Backward Compatibility
**Source:** `CutLabState.cs:189-225`
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

### Detector Registration in CutLabStructuralFindings
**Source:** `CutLabStructuralFindings.cs:7-29, ~329, ~426`
```csharp
public enum CutLabFindingKind
{
    // ... existing kinds ...
    StrandedOffPlanPackage,  // NEW for Phase 8
}

// In CutLabStructuralFindings, in the orchestrator Compute method:
private static void Compute(
    IReadOnlyList<CutLabAnalyzedCard> pool,
    // ... other params ...
    List<CutLabFinding> findings)
{
    // ... existing detector calls ...
    findings.AddRange(ComputeStrandedOffPlanPackage(pool, planProfile, /* threshold */ 4));
}

// New detector method:
private static IEnumerable<CutLabFinding> ComputeStrandedOffPlanPackage(
    IReadOnlyList<CutLabAnalyzedCard> pool,
    CutLabPlanProfile? planProfile,
    int thresholdCardCount)
{
    if (planProfile is null || thresholdCardCount <= 0)
        yield break;

    // Group pool cards by unchecked EDHREC theme
    var uncheckedThemes = GetUncheckedThemeSlugs(planProfile);
    
    foreach (var themeName in uncheckedThemes)
    {
        var cardsInTheme = pool
            .Where(card => card.CategoriesMatchTheme(themeName))
            .ToList();

        if (cardsInTheme.Count >= thresholdCardCount)
        {
            yield return new CutLabFinding(
                CutLabFindingKind.StrandedOffPlanPackage,
                "Off-plan theme package",
                $"{cardsInTheme.Count} cards support {themeName} — not in your plan.",
                cardsInTheme.Select(c => new CutLabFindingEvidence(c.Name, c.ManaValue)).ToList());
        }
    }
}
```

### Card-Name Normalization in Affinity Resolver
**Source:** `CutLabCardNames.cs`, verified usage pattern in `CutLabNextProposalBuilder.cs:20-28`
```csharp
// In CutLabPlanAffinityResolver
private bool IsCardInTheme(CutLabAnalyzedCard poolCard, string[] themeCardNames)
{
    string normalizedPoolCardName = CutLabCardNames.Normalize(poolCard.Name);
    
    return themeCardNames.Any(themeName =>
        CutLabCardNames.Comparer.Equals(
            CutLabCardNames.Normalize(themeName),
            normalizedPoolCardName));
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Free-text plan fields (`PrimaryPlan`, `SecondaryPlan`) | Machine-readable `CutLabPlanProfile` with checkboxes + EDHREC themes | Phase 8 | Engine can now act on intent (protect, reorder, adjust floors) instead of reading text and ignoring it |
| Single merged `interaction` role | Split into `interaction-targeted` and `interaction-mass` | Phase 1 (2026-07-26) | Enabled Phase 2 to measure per-commander role floors accurately; Phase 8 inherits the nine-role taxonomy |
| Classifier-based archetype detection (oracle heuristic) | Primary: EDHREC theme card lists; secondary: role-proxy table + classifier | Phase 8 | EDHREC is authoritative for archetypes; classifier fallback only for generic strategies and role-level tags |
| No on-plan protection in proposal queue | On-plan cards join existing `ComboProtected` pattern (pushed to back) | Phase 8 | Users cannot be forced to cut on-plan cards; emphasis shifts to reordering and findings |

**Deprecated/outdated:**
- Generic intent tags from Archidekt/Moxfield/TappedOut — EDHREC themes replace them entirely, machine-readable.
- Single `interaction` role — the split role taxonomy is now standard for measurement and proposals.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | EDHREC returns `$.panels.taglinks[]` in commander page JSON | Code Examples, Pitfall 1 | Affinity resolver cannot build theme list; service must parse different structure |
| A2 | EDHREC returns `$.container.json_dict.cardlists[]` in theme page JSON | Code Examples, Pitfall 1 | Cannot extract theme-scoped card membership; must fetch different endpoint or structure |
| A3 | S3 `AccessDenied` HTTP 403 is permanent (non-transient); safe to treat as "page does not exist" | Common Pitfalls, Pitfall 1 | May retry 403 indefinitely; timeout or log spam |
| A4 | Role-proxy table for generic strategies can be hand-authored; category knowledge is not the source of truth for generic strategy membership | Architectural Responsibility Map | Generic strategies match wrong cards; archetype misclassification |
| A5 | Disk cache can be colocated beside role-floor corpus without contention | Standard Stack, Supporting | Cache read/write latency or file system conflicts |
| A6 | Existing `CardNormalizer.Normalize` correctly handles all DFC syntax present in EDHREC | Code Examples, Pitfall 2 | Card name mismatches despite normalization |

**All other claims in this research are marked `[VERIFIED]` below.**

---

## Open Questions (RESOLVED)

All four questions were resolved during planning (2026-08-02); values below are canonical
in the named plans.

1. **Role-Proxy Table Vocabulary**
   - RESOLVED: full 12-strategy needle table authored in `08-01-PLAN.md`
     (`DeckPlanStrategyCatalog`, substring needles mirroring `PlanRoleClassifier.Has`,
     with the Counters/counterspell collision guard).
   - What we know: Spec examples show aristocrats → `Sac Outlet` | `Drain` | `Recursion` and tokens → `Tokens` | `Anthem`. `CategoryKnowledgeStore` is role-level; archetype names don't exist there.
   - What's unclear: Complete list of 12 generic strategies and their respective role-proxy tag mappings. Planning must curate the full table.
   - Recommendation: Draft the table in the plan; gather corpus evidence for each strategy's typical role distribution if needed.

2. **Plan→Floor-Delta Table Values**
   - RESOLVED: complete 12-row delta table authored in `08-04-PLAN.md`; composition is
     max-per-role, clamped by `CutLabFloorRules`, surfaced as a separate `PlanDelta` field.
   - What we know: Spec examples mention "combo → +tutor, +protection" and "combat → +wincon-creature". Deltas are clamped by existing `CutLabFloorRules` validation.
   - What's unclear: Exact floor delta values per strategy per role; whether combo's tutor delta applies to all roles or is ramp-specific.
   - Recommendation: Planner to define the delta table with rationale (corpus-backed or subject-matter reasoning).

3. **Additive Ordering-Weight Cap Value**
   - RESOLVED: `OnPlanScoreCap = 3`, defined in `08-02-PLAN.md`, consumed by `08-05-PLAN.md`
     (`PlanAffinityRank`); mutation-guarded.
   - What we know: Spec requires a cap on additive composition; mutation test must guard it.
   - What's unclear: What value the cap should be; how aggressive off-plan reordering should be in Rounds 1/2/3.
   - Recommendation: Planner to choose the cap; spec says planning may tune from default.

4. **Stranded-Package Threshold and Pre-Check Share**
   - RESOLVED: threshold 4 (`08-05-PLAN.md`) and pre-check top-3 at ≥5% (`08-03-PLAN.md`)
     — defaults stood; both mutation-guarded constants.
   - What we know: Default threshold is 4 cards; default pre-check is top 3 themes at ≥5% each. Both are tunable.
   - What's unclear: Whether these defaults will hold after UAT, or need adjustment based on real deck pools.
   - Recommendation: Planning to lock default values; Phase 8 implementation to surface them as constants (mutation-guarded).

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| EDHREC JSON API | `EdhrecCommanderThemeService` fetch | ✓ | Static S3/CloudFront | Fail-open: degrade to "unavailable" if unreachable or 403 |
| `.NET 10 / C# 12` | All code | ✓ | Current project version | — |
| xUnit | CutLab test suite | ✓ | Current project version | — |
| RestSharp | HTTP egress | ✓ | Current project version | — |
| Polly v8 | Resilience pipeline | ✓ | Current project version | — |
| `System.Text.Json` | Session serialization | ✓ | Built-in .NET | — |
| `CardNormalizer` | DFC name matching | ✓ | `DeckFlow.Core/Normalization/` | — |

**Missing dependencies with no fallback:**
- None — all required infrastructure is either built-in or already in codebase.

**Missing dependencies with fallback:**
- EDHREC availability: if unreachable, entire commander-theme section degrades to "unavailable"; generic strategies continue unaffected.

---

## Validation Architecture

**Nyquist Validation:** Enabled (`.planning/config.json` `workflow.nyquist_validation: true`)

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit (verified in CutLabFloorDefaultsTests.cs, CutLabCutRoundEngineTests.cs, etc.) |
| Config file | None — xUnit discovers `[Fact]` and `[Theory]` by convention |
| Quick run command | `dotnet test DeckFlow.Web.Tests.csproj -k CutLab --no-build` |
| Full suite command | `dotnet test DeckFlow.Web.Tests.csproj` |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PLPR-01 | `CutLabPlanProfile` serializes and deserializes; old fields survive in-flight sessions | unit | `dotnet test DeckFlow.Web.Tests -k "CutLabState\|Serialization"` | ❌ Wave 0 — new test class required |
| PLPR-02 | Zero checkboxes → engine no-op; floors, proposals, findings unchanged | unit | `dotnet test DeckFlow.Web.Tests -k "CutLabPlan.*NoIntent\|ZeroCheckboxes"` | ❌ Wave 0 — new parameterized test required |
| PLPR-03 | On-plan cards protected; off-plan cards reordered in Rounds 1/2/3 | unit | `dotnet test DeckFlow.Web.Tests -k "CutLabNextProposal.*OffPlan"` | ⚠️ Proposal tests exist; plan-affinity integration new |
| PLPR-04 | EDHREC 403 degrades gracefully; unreachable → "unavailable"; generic layer works | unit | `dotnet test DeckFlow.Web.Tests -k "EdhrecCommanderThemeService.*Fail"` | ❌ Wave 0 — new service test class required |
| PLPR-05 | Overlapping selections compose union/max/additive-cap; mutations prove cap and threshold guard | unit | `dotnet test DeckFlow.Web.Tests -k "CutLabPlanAffinity.*Composition\|MutationGuard"` | ❌ Wave 0 — new resolver + mutation test required |
| PLPR-06 | Stranded-package finding fires at threshold boundary; message phrases against selection | unit | `dotnet test DeckFlow.Web.Tests -k "CutLabStructuralFindings.*StrandedOffPlan"` | ❌ Wave 0 — new detector test required |

### Sampling Rate
- **Per task commit:** `dotnet test DeckFlow.Web.Tests -k CutLab --no-build` (covers affected domain)
- **Per wave merge:** `dotnet test DeckFlow.Web.Tests` (full suite, pre-rebase gate)
- **Phase gate:** Full suite green + E2E Playwright pass (2 viewports, headless) before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `DeckFlow.Web.Tests/CutLabPlanProfileTests.cs` — session serialization, backward compat (PLPR-01)
- [ ] `DeckFlow.Web.Tests/CutLabPlanAffinityResolverTests.cs` — membership, composition semantics (PLPR-02, PLPR-03, PLPR-05)
- [ ] `DeckFlow.Web.Tests/EdhrecCommanderThemeServiceTests.cs` — fetch, cache, 403 handling, fail-open (PLPR-04)
- [ ] `DeckFlow.Web.Tests/CutLabStructuralFindingsTests.cs::StrandedOffPlanPackageTests` — detector threshold, phrasing (PLPR-06)
- [ ] `DeckFlow.Web.Tests/e2e/ui-audit-batch-a.spec.ts` — plan-panel UI (gated on Phase 7; P1-P3 have no UI, so E2E covers plan-panel only)
- [ ] Framework install: xUnit is already present in project; no new install needed

*(If all gaps covered: test infrastructure ready for Phase 8 implementation)*

---

## Security Domain

**Note:** Phase 8 involves EDHREC HTTP fetch and disk caching. Threat model is low: EDHREC is public, no auth, no user data in request.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Not applicable — EDHREC is public |
| V3 Session Management | no | Session state is user's own intent; no privilege escalation |
| V4 Access Control | no | Cut Lab is feature-flagged (OFF in prod); access is via existing controller auth |
| V5 Input Validation | yes | EDHREC response parsing: validate JSON structure, commander slug (alphanumeric), theme slug; reject malformed card lists |
| V6 Cryptography | no | EDHREC is served over HTTPS (S3 CloudFront); cache is local disk (no sensitive data) |

### Known Threat Patterns for ASP.NET + RestSharp

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| JSON deserialization bomb (huge array) | Denial of Service | Limit JSON depth and array size; `System.Text.Json` defaults to reasonable limits |
| Malicious commander slug (injection) | Tampering | Validate commander slug against `[a-z0-9\-]+` regex; RestSharp URL-encodes by default |
| Cache poisoning (disk file overwrite) | Tampering | Cache files stored in app-owned directory; file permissions restrict write to app user only |
| EDHREC CDN outage (fail-closed) | Availability | Spec decision: fail-open (degraded UI); generic strategies continue unaffected |

---

## Sources

### Primary (HIGH confidence)
- **Codebase verification:** `CutLabState.cs:189-225` (Intent record structure with `[JsonInclude]` pattern), `CutLabFloorRules.cs:150-156` (interaction role handling), `CutLabFloorResolver.cs:51-59` (floor resolution call chain), `CutLabNextProposalBuilder.cs:6-40` (proposal ranking pattern), `CutLabStructuralFindings.cs:6-65` (detector enum and registration), `CutLabCardNames.cs:9-14` (normalization pattern), `CommanderBanListService.cs:36-49, 82-100` (RestSharp + Polly v8 pattern), `ResiliencePipelineFactory.cs:22-150` (pipeline registration), `CutLabFloorDefaultsTests.cs:10-35` (xUnit test conventions)
- **Project CLAUDE.md:** RestSharp + Polly v8 mandatory pattern, layout CSS in `site-common.css`, Fake* test doubles, no new packages without approval
- **Design spec:** `.planning/specs/2026-08-02-cutlab-plan-profile-design.md` (EDHREC endpoints, 403 handling, composition semantics)
- **Context document:** `.planning/workstreams/cycle21-cut-lab/phases/08-plan-profile-checkbox-selection/08-CONTEXT.md` (locked decisions, phase boundaries, IRC references)

### Secondary (MEDIUM confidence)
- **EDHREC JSON structure:** Verified via prior research docs (`.planning/specs/2026-08-02-cutlab-plan-profile-design.md` cites endpoint paths and field names; no live testing done in this research)
- **Phase 7 wizard slot:** Confirmed in ROADMAP.md Phase 7 description and Phase 8 gating language; wizard structure not yet reviewed (Phase 7 is unstarted)

### Tertiary (LOW confidence)
- Role-proxy table completeness for all 12 generic strategies — table stubs exist in spec examples (aristocrats, tokens) but full taxonomy not verified in codebase; requires planning

---

## Metadata

**Confidence breakdown:**
- Standard stack: **HIGH** — RestSharp + Polly v8 pattern verified across 5+ services; xUnit and session serialization established in existing CutLab tests
- Architecture: **HIGH** — detector registration, card normalization, and HTTP service patterns all verified in running code
- Pitfalls: **HIGH** — DFC mismatch and 403 handling derived from spec decisions and codebase patterns; composition guards from Phase 3 and Phase 4 precedent
- Environment: **HIGH** — all required tools (.NET 10, RestSharp, Polly, xUnit) confirmed present; EDHREC is public API with known endpoints

**Research date:** 2026-08-02
**Valid until:** 2026-08-30 (30 days — stack is stable; EDHREC structure unlikely to change)

---

*Phase: 08-plan-profile-checkbox-selection*
*Research verified: 2026-08-02*
