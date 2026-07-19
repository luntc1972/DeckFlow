# Phase 103: Simulation Engine & Guided Cut Rounds - Pattern Map

**Mapped:** 2026-07-19
**Files analyzed:** 12 (new) + 4 (extended)
**Analogs found:** 15 / 16

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs` | service (pure logic) | transform | `DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs` | exact (same tier: pure static classifier over `CutLabAnalyzedCard`/`CutLabFinding` data) |
| `DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs` | service (orchestrator) | request-response / batch | `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` (resolve→classify→analyze pipeline) | exact |
| `DeckFlow.Web/Services/CutLab/CutLabDeltaCache.cs` | service (cache wrapper) | CRUD (get/set, TTL) | `DeckFlow.Web/Services/PacketSessionCache.cs` | exact |
| `DeckFlow.Web/Services/CutLab/CutLabResolvedCardCache.cs` | service (cache wrapper) | CRUD (get/set, TTL) | `DeckFlow.Web/Services/PacketSessionCache.cs` | exact |
| `DeckFlow.Web/Services/CutLab/CutLabBaselineSnapshot.cs` | service (pure builder) | transform | `DeckFlow.Web/Services/CutLab/CutLabFloorDefaults.cs` (resolve-once-at-intake, compact record shape) | role-match |
| `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` | controller (JSON API) | request-response | `DeckFlow.Web/Controllers/Api/DeckSyncApiController.cs` | exact |
| `DeckFlow.Web/Models/Api/CutLabApiRequest.cs` / `CutLabApiResponse.cs` | model (DTO) | request-response | `DeckFlow.Web/Models/Api/DeckSyncApiRequest.cs` / `DeckSyncApiResponse.cs` (referenced by controller; not separately opened — same folder/pattern) | role-match |
| `DeckFlow.Web/Models/CutLab/CutLabState.cs` (EXTEND: add `Decisions`, `BaselineSnapshot`) | model (state envelope) | CRUD (serialize/deserialize) | itself (Phase 102 `RoleFloors` extension precedent) | exact |
| `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` (EXTEND: clamp/bound new fields) | service (serializer) | transform | itself (existing `ClampFloors`/`Take(MaxPackages)` bounding pattern) | exact |
| `DeckFlow.Web/Services/CutLab/CutLabPageService.cs` (EXTEND: populate resolved-card cache at intake) | service (orchestrator) | request-response | itself (`ResolveEntriesAsync`/`ResolveCardsAsync`) | exact |
| `DeckFlow.Web/Models/CutLabViewModel.cs` (EXTEND: rounds/proposal/compare sections) | model (view model) | transform | itself (`CutLabViewModel.From`) | exact |
| `DeckFlow.Web/Views/Deck/CutLab.cshtml` (EXTEND: proposal card, sticky bar, before/after panel sections) | view (Razor) | request-response | itself (existing accordion `<details class="cutlab-role-group">` sections) | exact |
| `DeckFlow.Web/wwwroot/ts/cut-lab.ts` (EXTEND: fetch/patch, sticky bar) | hook/module (browser TS) | streaming (fetch + DOM patch) | `DeckFlow.Web/wwwroot/ts/deck-sync.ts` (`submitDeckSyncApi`) | exact (only existing fetch+JSON-POST+DOM-patch precedent in the codebase) |
| `DeckFlow.Web/wwwroot/css/site-common.css` (EXTEND: sticky bar / proposal card / compare panel) | config (layout CSS) | — | itself (`.cutlab-*` block at line 4146+) | exact |
| `DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs` | test | transform | `DeckFlow.Web.Tests/CutLabStructuralFindingsTests.cs` | exact |
| `DeckFlow.Web.Tests/CutLabSimulationServiceTests.cs` | test | request-response | `DeckFlow.Web.Tests/CutLabPageServiceTests.cs` (fakes: `FakeLoader`, `FakeResolver`, `FakeBanListService`) | exact |
| `DeckFlow.Web.Tests/CutLabApiControllerTests.cs` | test | request-response | `DeckFlow.Web.Tests/CutLabControllerTests.cs` | role-match (no existing `*ApiController` test found in this pass — see No Analog Found) |
| `DeckFlow.Web.Tests/CutLabBaselineSnapshotTests.cs` | test | transform | `DeckFlow.Web.Tests/CutLabFloorDefaultsTests.cs` | role-match |
| `DeckFlow.Web/ts-tests/cut-lab-proposal.test.ts` | test (TS unit) | streaming | `DeckFlow.Web/ts-tests/cut-lab-lock-interactions.test.ts` | exact |
| `DeckFlow.Web/e2e/cut-lab-structure.spec.ts` (EXTEND: rounds/proposal/compare flow) | test (e2e) | request-response | itself | exact |

## Pattern Assignments

### `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` (controller, request-response)

**Analog:** `DeckFlow.Web/Controllers/Api/DeckSyncApiController.cs` (148 lines, full file read)

**Imports pattern** (lines 1-12):
```csharp
using System.Linq;
using DeckFlow.Core.Exporting;
using DeckFlow.Core.Models;
using DeckFlow.Core.Parsing;
using DeckFlow.Core.Reporting;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace DeckFlow.Web.Controllers.Api;
```
For `CutLabApiController`, swap `DeckFlow.Web.Services` for `DeckFlow.Web.Services.CutLab` and `DeckFlow.Web.Models` for `DeckFlow.Web.Models.CutLab` / `DeckFlow.Web.Models.Api`.

**Feature-flag + same-origin + [ApiController] pattern** (lines 19-53):
```csharp
[ApiController]
[Route("api/deck")]
public sealed class DeckSyncApiController : ControllerBase
{
    ...
    [HttpPost("diff")]
    [FeatureFlagGate("tool.deck-sync.enabled")]
    [ProducesResponseType(typeof(DeckSyncApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DeckSyncApiResponse>> PostDiffAsync([FromBody] DeckSyncApiRequest request, CancellationToken cancellationToken)
    {
        if (!SameOriginRequestValidator.IsValid(Request))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
        }
        ...
```
Copy verbatim for `POST /api/cut-lab/decide`: `[Route("api/cut-lab")]`, `[HttpPost("decide")]`, `[FeatureFlagGate("tool.cut-lab.enabled")]` (per RESEARCH.md "Project Constraints" — flag must gate the API route exactly as it gates `CutLabController`), same-origin check as literally the first statement in the action body (Pitfall 4 in RESEARCH.md).

**Error handling pattern** (lines 73-110):
```csharp
try
{
    ...
    return Ok(response);
}
catch (Exception exception) when (exception is DeckParseException or InvalidOperationException or HttpRequestException)
{
    _logger.LogWarning(exception, "Deck sync API request failed.");
    return BadRequest(new { Message = BuildUserFacingErrorMessage(request, exception) });
}
```
For the cut-lab decide endpoint, catch `InvalidOperationException` (floor-break / state-size overflow) the same way and return `{ Message }`. `CutLabFloorRules.Evaluate` returns warnings (not an exception) — those go in the 200 OK response body, not an error path (see `CutLabFloorRules` pattern below).

**Validation pattern** (lines 56-71): guard `request is null` → `BadRequest(new { Message = ... })` before any processing. Apply the same null/shape guard to the new `CutLabApiRequest` (card name, decision kind, working-list hash).

---

### `DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs` (service, request-response/batch)

**Analog:** `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` (resolve→classify→analyze pipeline, lines 466-477 and 775-819)

**Core pipeline pattern** (lines 783-796, verified):
```csharp
IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(deckEntries);
ManabaseDeck deck = ManabaseClassifier.Classify(
    facts,
    isSingleton: true,
    rampCreditV2: rampCreditV2,
    landRampSim: landRampSim,
    payLifeUntapped: payLifeUntapped,
    checkLandUntapped: checkLandUntapped,
    restrictedLands: restrictedLands);

if (classifyPlanRoles)
{
    deck = await TagPlanRolesAsync(deck, facts, deckCards, mode, cancellationToken).ConfigureAwait(false);
}
```

**Analyze call pattern** (lines 466-477):
```csharp
ManabaseReport report = ManabaseAnalyzer.Analyze(
    resolved.Deck, options.Mode, options.CommanderImportance, options.CostOverrides,
    useManaQuantity, colorAwareMulligan, gateRampOnCastable: true,
    ritualBurst: ritualBurst,
    ritualLandCredit: ritualLandCredit,
    scryCredit: scryCredit,
    colorlessSnow: colorlessSnow,
    keepShapes: keepShapes,
    interactionLens: interactionLens,
    useHealthBandCastability: useHealthBandCastability,
    useHealthBandHeadlineFloor: useHealthBandHeadlineFloor,
    cedhContext: cedhContext);
```
`CutLabSimulationService` must call this exact pipeline shape against the Cut Lab working list (facts resolved from `CutLabResolvedCardCache`, not re-fetched). Do NOT reimplement any step — SIM-01 forbids new simulation math (RESEARCH.md "Anti-Patterns to Avoid").

**Mode resolution reuse** — `DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs:40`:
```csharp
public static ManabaseMode ResolveMode(string? playExperience)
```
`CutLabPageService.cs:466` already calls `CutLabRoleAssigner.ResolveMode(playExperience)` to get the `ManabaseMode` for role assignment — `CutLabSimulationService` must resolve the same `ManabaseMode` the same way so the delta view stays consistent with the findings/role data already shown on the page.

**Floor-break contract to call before presenting any cut** — `DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs:95-142` (signature, full file read):
```csharp
public static IReadOnlyList<CutLabFloorWarning> Evaluate(
    IReadOnlyDictionary<string, int> roleCounts,
    IReadOnlyDictionary<string, int> floors,
    IReadOnlyCollection<string> candidateCutRoles,
    string cardName,
    int quantity = 1);
```
This is explicitly documented in its own XML doc (line 6-8) as the Phase 103 contract: "Phase 103's cut engine MUST route every proposed cut through Evaluate before presenting it." Call verbatim; do not reimplement.

---

### `DeckFlow.Web/Services/CutLab/CutLabDeltaCache.cs` / `CutLabResolvedCardCache.cs` (service, cache CRUD)

**Analog:** `DeckFlow.Web/Services/PacketSessionCache.cs` (203 lines, full file read)

**Dedicated-instance construction pattern** (lines 41-45):
```csharp
public PacketSessionCache(ILogger<PacketSessionCache>? logger = null)
{
    _logger = logger ?? NullLogger<PacketSessionCache>.Instance;
    _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10_000_000 });
}
```
Both new caches must use a **private `MemoryCache` instance**, not the shared `IMemoryCache` singleton (RESEARCH.md Pattern 4 / "Supporting" table: "Do NOT put the delta cache or the resolved-card cache directly on the shared singleton"). Size the `SizeLimit` modestly for each cache's payload (resolved-card cache: per-pool `ScryfallCardData`, well under the 10MB `PacketSessionCache` budget; delta cache: per-(hash, card) small metric records).

**Set with TTL + size accounting + eviction logging** (lines 91-115):
```csharp
public void Set<TResult>(string key, TResult result, int sizeBytes) where TResult : class
{
    ArgumentNullException.ThrowIfNull(key);
    ArgumentNullException.ThrowIfNull(result);

    var entry = new CachedEntry<TResult>(result, sizeBytes);
    var options = new MemoryCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        Size = sizeBytes,
    };

    options.RegisterPostEvictionCallback((evictedKey, evictedValue, _, _) =>
    {
        var evictedSize = (evictedValue as CachedEntry<TResult>)?.SizeBytes ?? 0;
        _logger.LogInformation(
            "Packet cache {Outcome} for {KeyPrefix} ({SizeBytes} bytes)",
            "evicted",
            ((string)evictedKey)[..KeyPrefixLength],
            evictedSize);
    });

    _cache.Set(key, entry, options);
    LogCacheEvent("write", key, sizeBytes);
}
```
Copy this shape for both new caches. Per CONTEXT D-12, the resolved-card cache TTL should survive "a normal cut session" (tens of minutes per RESEARCH.md Open Question 4) — longer than `PacketSessionCache`'s 5-minute TTL; the delta cache TTL is Claude's Discretion per CONTEXT.md (short enough to stay disposable).

**Deterministic key-hashing primitive** (lines 52-59):
```csharp
public static string ComputeKey(object fieldBag)
{
    ArgumentNullException.ThrowIfNull(fieldBag);

    var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(fieldBag, DeterministicJsonOptions);
    var hashBytes = SHA256.HashData(jsonBytes);
    return Convert.ToHexString(hashBytes).ToLowerInvariant();
}
```
Use `PacketSessionCache.ComputeKey(fieldBag)` directly (it is a public static method already available on the existing singleton) or mirror this exact static helper in the new caches, keyed by the sorted (card name, quantity) pool list per RESEARCH.md Open Question 4's recommendation.

**DI registration pattern** — `DeckFlow.Web/Extensions/PacketServiceCollectionExtensions.cs:41-95` (full file read):
```csharp
public static IServiceCollection AddDeckFlowPacketServices(this IServiceCollection services)
{
    ArgumentNullException.ThrowIfNull(services);

    services.AddSingleton<PacketSessionCache>();
    ...
    services.AddScoped<IDeckAnalysisPacketService>(sp =>
        new DeckAnalysisPacketService(
            ...
            sp.GetRequiredService<PacketSessionCache>(),
            ...));

    return services;
}
```
Called once from `Program.cs:173` as `builder.Services.AddDeckFlowPacketServices();`. Mirror this: add an `AddDeckFlowCutLabServices` extension (or extend the existing Cut Lab registration site at `Program.cs:181`) that registers `CutLabResolvedCardCache`/`CutLabDeltaCache` as **singletons** and wires them into `CutLabPageService`/`CutLabSimulationService`/`CutLabApiController` via constructor injection — do not register on the shared `AddMemoryCache()` singleton (`Program.cs:69`).

---

### `DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs` (service, pure transform)

**Analog:** `DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs` (349 lines, full file read)

**Static pure-function-over-records shape** (lines 75-156):
```csharp
public static class CutLabStructuralFindings
{
    // Why: <threshold rationale comment>
    private const double CongestionShareThreshold = 0.30;
    ...

    public static CutLabStructuralFindingsResult Compute(
        IReadOnlyList<CutLabAnalyzedCard> pool,
        IReadOnlyList<SpellbookAlmostCombo> nearCombos,
        IReadOnlyDictionary<string, int> floors,
        bool comboDataAvailable,
        bool categoryDataAvailable)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ...
        List<CutLabFinding> findings = [];
        findings.AddRange(ComputeCurveCongestion(pool));
        if (categoryDataAvailable) { findings.AddRange(ComputeStrandedSubthemes(pool)); }
        ...
        return new CutLabStructuralFindingsResult(findings, comboDataAvailable, categoryDataAvailable);
    }
    ...
}
```
`CutLabCutRoundEngine` should follow the same shape: a static class, `[Why:]`-commented constants for D-01/D-04 thresholds, a single `Compute`/`BuildQueues` entry point taking the analyzed pool + findings + role data and returning an ordered round-queue record. Per RESEARCH.md Pitfall 3, the per-card finding tally logic must explicitly decide whether `WeakFloorCase`/`RedundantFinishers` evidence (which flags entire role membership uniformly) counts toward D-01's "2+ findings" threshold the same way `CurveCongestion`/`StrandedSubtheme`/`EnablerStarved` do — this decision must be documented in-code with a `// Why:` comment mirroring this file's style.

**Finding/evidence record shape to build the per-card tally from** (lines 24-47):
```csharp
public sealed record CutLabFindingEvidence(string CardName, double? ManaValue);

public sealed record CutLabFinding(
    CutLabFindingKind Kind,
    string Heading,
    string Lead,
    IReadOnlyList<CutLabFindingEvidence> Evidence);

public sealed record CutLabStructuralFindingsResult(
    IReadOnlyList<CutLabFinding> Findings,
    bool ComboDataAvailable,
    bool CategoryDataAvailable);
```
`CutLabCutRoundEngine` consumes `CutLabStructuralFindingsResult.Findings[].Evidence` (a flat per-card list) to build a `Dictionary<string cardName, int findingCount>` — there is no pre-built index today (RESEARCH.md Code Examples, "Existing structural-findings shape Phase 103 must aggregate per-card").

---

### `DeckFlow.Web/Models/CutLab/CutLabState.cs` (model, EXTEND) + `CutLabStateSerializer.cs` (EXTEND)

**Analog:** itself — the Phase 102 `RoleFloors` extension precedent (full file read, 90 lines)

**Record-extension pattern** (lines 7-26):
```csharp
public sealed record CutLabState
{
    public string Commander { get; init; } = string.Empty;
    public IReadOnlyList<CutLabPoolCard> Pool { get; init; } = [];
    public IReadOnlyList<CutLabPackage> Packages { get; init; } = [];

    /// <summary>
    /// User-adjusted role floors plus their user-set flags. Derived defaults are recomputed per POST,
    /// never persisted, and the empty initializer keeps pre-102 JSON blobs deserializing cleanly.
    /// </summary>
    public IReadOnlyList<CutLabRoleFloor> RoleFloors { get; init; } = [];

    public CutLabIntent Intent { get; init; } = new();
}
```
Add `Decisions` (compact cut-history records — card name/ID + decision kind + ordinal, NOT full card data, per RESEARCH.md Pitfall 5) and `BaselineSnapshot` (D-12's "compact numeric snapshot") the same way: `IReadOnlyList<T> Property { get; init; } = [];` with an empty-default initializer so old JSON blobs keep deserializing, and an XML doc explaining what's NOT persisted here (mirroring the `RoleFloors` doc comment's "Derived defaults are recomputed per POST, never persisted" framing).

**Bounding/clamp pattern on deserialize** — `CutLabStateSerializer.cs:38-62` (full file read):
```csharp
public static CutLabState Deserialize(string? json)
{
    if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaxUploadBytes)
    {
        return new CutLabState();
    }

    try
    {
        var state = JsonSerializer.Deserialize<CutLabState>(json, Options) ?? new CutLabState();
        state = state with
        {
            Packages = state.Packages
                .Where(package => !string.IsNullOrWhiteSpace(package.Name))
                .Take(MaxPackages)
                .ToArray(),
        };

        return CutLabFloorRules.ClampFloors(CutLabLockRules.EnforceCommanderLock(state));
    }
    catch (JsonException)
    {
        return new CutLabState();
    }
}
```
Follow this exact shape for the new `Decisions` list: bound it with a `Take(MaxDecisions)`-style clamp (RESEARCH.md Pitfall 5: "Keep the D-16 decision history to compact records... Size-budget this explicitly"), and keep `MaxUploadBytes = 262_144` as the hard cap check that already runs first (`CutLabStateSerializer.MaxUploadBytes`, line 11).

---

### `DeckFlow.Web/wwwroot/ts/cut-lab.ts` (browser TS, EXTEND — fetch/patch)

**Analog:** `DeckFlow.Web/wwwroot/ts/deck-sync.ts` (`submitDeckSyncApi`, lines 1128-1179 — the only existing fetch+JSON-POST+DOM-patch precedent in the codebase; `cut-lab.ts` itself has no fetch usage today)

**Fetch + JSON body + typed error payload pattern** (lines 1128-1179, verified):
```typescript
const submitDeckSyncApi = async (form: HTMLFormElement): Promise<void> => {
  const endpoint = form.dataset.deckSyncApi;
  if (!endpoint) {
    return;
  }

  const error = document.getElementById('deck-sync-error');

  try {
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(serializeFormFields(form))
    });

    if (!response.ok) {
      let payload: { message?: string; Message?: string; title?: string; errors?: Record<string, string[]> } | null = null;
      try {
        payload = await response.json() as { message?: string; Message?: string; title?: string; errors?: Record<string, string[]> };
      } catch {
        payload = null;
      }

      if (error) {
        error.textContent = payload?.message ?? payload?.Message ?? ... ?? 'Unable to run deck sync.';
        error.classList.remove('hidden');
      }
      window.hideBusyIndicator?.();
      return;
    }

    renderDeckSyncResponse(await response.json() as DeckSyncApiResponse);
    window.hideBusyIndicator?.();
  } catch (requestError) {
    if (error) {
      error.textContent = requestError instanceof Error ? requestError.message : 'Unable to run deck sync.';
      error.classList.remove('hidden');
    }
    window.hideBusyIndicator?.();
  }
};
```
For the accept/reject/defer action, copy this exact shape: `fetch('/api/cut-lab/decide', { method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(payload) })`, `response.ok` guard reading `{ Message }`/`{ message }`, then a `renderXxxResponse(...)`-style function that patches the proposal card + sticky bar + metrics DOM in place (D-09's "TS patches the proposal card + metrics in place"). Note `window.hideBusyIndicator?.()` / a busy-indicator show call (see `DeckFlow.Web/wwwroot/ts/busy-indicator.ts`) should wrap the request per the D-11 spinner requirement (~1s target, 3s hard cap).

**Existing lock-interaction DOM-patch precedent already in `cut-lab.ts`** — use `grep -n "cutlab" DeckFlow.Web/wwwroot/ts/cut-lab.ts` to locate the current lock/package toggle handlers (850 lines total) as the in-file precedent for how `cut-lab.ts` currently mutates `.cutlab-*` DOM nodes and re-serializes the hidden `CutLabStateJson` field — the new proposal-card patch logic must keep that hidden field in sync per D-09's no-JS fallback requirement.

---

### `DeckFlow.Web/wwwroot/css/site-common.css` (EXTEND — sticky bar / proposal card / compare panel)

**Analog:** itself — existing `.cutlab-*` block (lines 4146-4243+, verified)

**Theme-token + accordion pattern** (lines 4159-4181, 4200-4235):
```css
.cutlab-role-group {
  margin-top: 1rem;
  border: 1px solid var(--line);
  border-radius: 10px;
  background: var(--panel);
}

.cutlab-role-group__summary {
  cursor: pointer;
  min-height: 44px;
  padding: 0.75rem 1rem;
  font-size: var(--fs-sm);
  font-weight: 600;
  list-style: revert;
}

.cutlab-finding {
  margin-top: 1rem;
  padding: 0.85rem 1rem;
  border-left: 3px solid var(--gold-warning, var(--warning, #c8a040));
  background: color-mix(in srgb, var(--gold-warning, var(--warning, #c8a040)) 10%, var(--panel-soft-bg, var(--panel)));
  color: var(--ink, inherit);
}

.cutlab-findings-count {
  display: inline-flex;
  align-items: center;
  padding: 0.3rem 0.65rem;
  border: 1px solid var(--accent);
  border-radius: 999px;
  background: color-mix(in srgb, var(--accent) 14%, var(--panel));
  color: var(--ink, inherit);
  box-shadow: 0 1px 0 color-mix(in srgb, var(--accent) 24%, var(--panel));
  font-weight: 600;
}
```
New rules (sticky progress bar D-14, proposal card, before/after compare panel D-13) go in **`site-common.css`** immediately after this block, using the same `var(--line)`/`var(--panel)`/`var(--accent)`/`var(--ink, inherit)` token references and `min-height: 44px` touch targets (44px touch-safe requirement is already the established convention here, e.g. `.cutlab-role-group__summary`). Never put layout rules in `site.css`; any NEW tokens (e.g. a directional-color red/green pair for D-06) go in each theme file's `:root` per CLAUDE.md's theme-system constraint — not in `site-common.css` itself.

---

### Tests

**Analog for C# unit tests:** `DeckFlow.Web.Tests/CutLabPageServiceTests.cs` (fakes shown, lines 932-994 — `FakeLoader`, `FakeResolver`, `FakeBanListService`, `ThrowingLoader`, `ThrowingBanListService`) and `DeckFlow.Web.Tests/CutLabStructuralFindingsTests.cs`. Follow the `Fake*`/`Throwing*` naming convention from CLAUDE.md's Naming Patterns section for any new test doubles (`FakeResolvedCardCache`, `ThrowingSimulationService`, etc.).

**Analog for TS unit tests:** `DeckFlow.Web/ts-tests/cut-lab-lock-interactions.test.ts` — Vitest, DOM fixture setup, dispatch events, assert DOM/hidden-field state. New `cut-lab-proposal.test.ts` should mock `fetch` and assert the same DOM-patch + hidden-field-sync behavior for accept/reject/defer.

**Analog for e2e:** `DeckFlow.Web/e2e/cut-lab-structure.spec.ts` (lines 1-60 read) — Playwright, `test.describe.configure({ mode: 'serial' })`, `acquireAdminLockForTest`/`releaseAdminLockForTest` for the shared admin-gated flag lock, three-theme × two-viewport screenshot matrix, `importPool` helper filling `#cut-lab-deck-text` etc. Extend this file (not a new one) to add the round/proposal/compare flow, reusing `importPool` and the existing theme/viewport loops.

## Shared Patterns

### Feature flag gate
**Source:** `DeckFlow.Web/Controllers/CutLabController.cs:24,30` — `[FeatureFlagGate("tool.cut-lab.enabled")]`
**Apply to:** `CutLabApiController`'s new `POST /api/cut-lab/decide` action, exactly matching the existing controller's gate key (RESEARCH.md: "must gate the new `CutLabApiController` route exactly as it gates `CutLabController`").

### Same-origin CSRF guard (JSON API, not antiforgery token)
**Source:** `DeckFlow.Web/Security/SameOriginRequestValidator.cs` (full file read)
```csharp
if (!SameOriginRequestValidator.IsValid(Request))
{
    return StatusCode(StatusCodes.Status403Forbidden, new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
}
```
**Apply to:** Every action in the new `CutLabApiController` — this REPLACES `[ValidateAntiForgeryToken]` for JSON API routes; do not add both (RESEARCH.md Pitfall 4).

### Dedicated `MemoryCache` instance (not the shared `IMemoryCache` singleton)
**Source:** `DeckFlow.Web/Services/PacketSessionCache.cs:41-45`
**Apply to:** `CutLabResolvedCardCache` and `CutLabDeltaCache` — both must own a private `MemoryCache` with an explicit `SizeLimit`, registered as `AddSingleton<T>()` via a DI extension method mirroring `PacketServiceCollectionExtensions.AddDeckFlowPacketServices`.

### `{ Message }` JSON error body
**Source:** `DeckFlow.Web/Controllers/Api/DeckSyncApiController.cs:53,58,64,70,109`
**Apply to:** Every non-2xx response from `CutLabApiController` — `BadRequest(new { Message = ... })`, `StatusCode(403, new { Message = ... })`. TS side already expects this shape (`deck-sync.ts:1146`: `payload?.message ?? payload?.Message ?? ...`).

### Floor-break evaluation before presenting a cut
**Source:** `DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs:95-142` (`Evaluate`)
**Apply to:** `CutLabApiController`'s decide action and `CutLabCutRoundEngine`'s round-queue construction — every proposed/accepted cut must pass through `Evaluate` first; warnings ride in the response body, never a silent break (FLOOR-02, per the method's own XML doc).

### Deterministic seeding — already solved, do not re-implement
**Source:** `DeckFlow.Core/Manabase/CastabilitySimulator.cs:2812-2828` (`StableSeed`)
**Apply to:** Nothing new — `CutLabSimulationService` inherits determinism for free by calling `ManabaseAnalyzer.Analyze` on an unchanged `ManabaseDeck`. D-08's only new surface is the noise-floor threshold in the delta-display layer (RESEARCH.md Pattern 2).

### 256KB state-size cap enforcement
**Source:** `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs:11,22-33` (`MaxUploadBytes = 262_144`, thrown as `InvalidOperationException`)
**Apply to:** Every new field added to `CutLabState` (`Decisions`, `BaselineSnapshot`) — keep records compact (RESEARCH.md Pitfall 5 sizing guard: "150 cards × ~40 bytes/decision record ≈ 6KB").

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `DeckFlow.Web.Tests/CutLabApiControllerTests.cs` | test (controller) | request-response | No existing `*ApiController` test file was found in this pass (`DeckSyncApiController` itself appears to lack a dedicated unit-test analog in the directories searched — `CutLabControllerTests.cs` is the closest available shape, testing the form-POST `CutLabController`, not a JSON `[ApiController]`). Planner should pattern the new test on `CutLabControllerTests.cs`'s fake-dependency wiring plus `DeckSyncApiController`'s action shape directly (same-origin 403 case, `[FromBody]` model-binding case, floor-warning-in-response case) rather than a single copied test file. |
| `DeckFlow.Web/Models/Api/CutLabApi*.cs` DTOs | model (DTO) | request-response | Not opened directly in this pass; `DeckSyncApiRequest`/`DeckSyncApiResponse` (same `Models/Api/` folder, referenced at `DeckSyncApiController.cs:8,49,83`) are the structural analog by folder convention and are a role-match, but their exact field shape wasn't read — planner should open them before drafting the new DTOs' property list. |

## Metadata

**Analog search scope:** `DeckFlow.Web/Controllers/Api/`, `DeckFlow.Web/Services/CutLab/`, `DeckFlow.Web/Services/Manabase/`, `DeckFlow.Web/Services/PacketSessionCache.cs`, `DeckFlow.Web/Models/CutLab/`, `DeckFlow.Web/Security/`, `DeckFlow.Web/Extensions/`, `DeckFlow.Web/wwwroot/ts/`, `DeckFlow.Web/wwwroot/css/site-common.css`, `DeckFlow.Web.Tests/`, `DeckFlow.Web/e2e/`, `DeckFlow.Web/ts-tests/`, `DeckFlow.Web/Program.cs`.
**Files scanned:** 19 read directly (full or targeted ranges), plus `grep`/`wc -l` surveys of ~15 more.
**Pattern extraction date:** 2026-07-19
