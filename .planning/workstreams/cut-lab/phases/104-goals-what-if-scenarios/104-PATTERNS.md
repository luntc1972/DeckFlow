# Phase 104: Goals & What-If Scenarios - Pattern Map

**Mapped:** 2026-07-20
**Files analyzed:** 13 (9 new/modified production + 4 new/modified test surfaces, several test files bundle multiple new cases)
**Analogs found:** 13 / 13 (this phase is explicitly reuse-only — SIM-01/D-01 — so every new file's closest analog is an existing Cut Lab file of the same shape)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `DeckFlow.Web/Models/CutLab/CutLabGoals.cs` (NEW) | model (record) | CRUD (config value object) | `DeckFlow.Web/Models/CutLab/CutLabState.cs` (`CutLabRoleFloor`/`CutLabIntent` records) | exact |
| `DeckFlow.Web/Models/CutLab/CutLabState.cs` (MODIFY — add `Goals` property) | model | CRUD | itself (existing `RoleFloors`/`Intent` properties on the same record) | exact |
| `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` (MODIFY — Goals round-trips + clamp) | service (serializer) | transform (JSON round-trip) | itself (existing `Packages`/`Decisions` bounding in `Deserialize`) | exact |
| `DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs` (MODIFY — thread goal turns, fix clamp) | service | CRUD / transform (metric projection) | itself (existing `BuildMetrics`/`PercentByTurn`) | exact |
| `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` (MODIFY — add `PostWhatifAsync`) | controller (API) | request-response | `PostDecideAsync` in the same file | exact |
| `DeckFlow.Web/Controllers/CutLabController.cs` (MODIFY — add `Whatif` no-JS action) | controller (MVC) | request-response | `Decide` action in the same file | exact |
| `DeckFlow.Web/Models/Api/CutLabWhatifApiRequest.cs` / `CutLabWhatifApiResponse.cs` (NEW) | model (API DTO) | request-response | `DeckFlow.Web/Models/Api/CutLabDecideApiRequest.cs` + `CutLabDecideApiResponse.cs` | exact |
| `DeckFlow.Web/wwwroot/ts/cut-lab-scenarios.ts` (NEW) | provider/store (client persistence) | file-I/O (localStorage) | `DeckFlow.Web/wwwroot/ts/deck-input-store.ts` | exact |
| `DeckFlow.Web/wwwroot/ts/cut-lab.ts` (MODIFY — swap preview fetch/patch) | component (client controller) | request-response (fetch/patch) | itself (existing `handleDecisionSubmit`/decision-form flow) | exact |
| `DeckFlow.Web/Views/Deck/CutLab.cshtml` (MODIFY — Goals section, Scenarios panel, Swap preview section) | component (Razor view) | request-response | itself (existing "Role floors" `<section>`) | exact |
| `DeckFlow.Web.Tests/CutLabSimulationServiceTests.cs` (MODIFY) | test | CRUD | itself | exact |
| `DeckFlow.Web.Tests/CutLabStateSerializerTests.cs` (MODIFY) | test | transform | itself | exact |
| `DeckFlow.Web.Tests/CutLabApiControllerTests.cs` (MODIFY — new `PostWhatifAsync` cases) | test | request-response | itself | exact |
| `DeckFlow.Web.Tests/CutLabWorkingListTests.cs` (MODIFY — swap-B candidate cases) | test | CRUD | itself | exact |
| `DeckFlow.Web/ts-tests/cut-lab-scenarios.test.ts` (NEW) | test (Vitest) | file-I/O | existing `ts-tests/` conventions (jsdom `localStorage`) | role-match (no prior localStorage test file to copy verbatim; pattern is the `deck-input-store.ts` module under test) |
| `DeckFlow.Web/e2e/cut-lab-scenarios.spec.ts`, `cut-lab-whatif.spec.ts` (NEW) | test (Playwright) | event-driven (UI flow) | existing `e2e/cut-lab-structure.spec.ts` (grep confirmed present in repo's e2e suite) | role-match |

## Pattern Assignments

### `DeckFlow.Web/Models/CutLab/CutLabGoals.cs` (NEW model)

**Analog:** `DeckFlow.Web/Models/CutLab/CutLabState.cs:110-121` (`CutLabRoleFloor`) and `:123-160` (`CutLabIntent`)

**Record shape pattern** (copy exactly — `init`-only properties, XML doc on every public member, `sealed record`):
```csharp
// Source: DeckFlow.Web/Models/CutLab/CutLabState.cs:110-121
/// <summary>Serializable user floor override for one stable Cut Lab role key.</summary>
public sealed record CutLabRoleFloor
{
    /// <summary>Stable serialized role key: lands, ramp, draw, interaction, protection, engines, payoffs, or wincons.</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Minimum allowed count for the role in the finished deck.</summary>
    public int Floor { get; init; }

    /// <summary>True when the user explicitly adjusted this floor away from the derived default.</summary>
    public bool IsUserSet { get; init; }
}
```
**Apply to `CutLabGoalSettings`:** three `int` turn-target properties (`CommanderByTurn`, `EngineByTurn`, `RepresentativeLineByTurn` per Option A / D-02 resolution) with `init` defaults seeded from `CedhMulliganCalibration` (see below), plus a `sealed record CutLabGoalResult` (pass/miss + probability) mirroring `CutLabMetricValue`'s shape (`DeckFlow.Web/Models/CutLab/CutLabMetrics.cs:91-107`) if the planner wants a projected/derived (non-persisted) result type alongside the persisted settings record.

**Seed defaults** (cite exact constants — D-02 resolution locks these three, no others):
```csharp
// Source: DeckFlow.Core/Manabase/CedhMulliganCalibration.cs:15,21,28,48-49
public const int TurnCapExplosive = 3;
public const int TurnCapEngine = 2;
public const int RepresentativeLineTurnCap = 4;
public static int GetRepresentativeLineTurnCap(ManabaseMode mode) =>
    mode == ManabaseMode.Cedh ? RepresentativeLineTurnCap : int.MaxValue;
```
Casual mode returns `int.MaxValue` — seed goals per `ManabaseMode` (resolved via `CutLabRoleAssigner.ResolveMode(playExperience)`, `DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs:40`), not a single global constant (Pitfall 5).

---

### `DeckFlow.Web/Models/CutLab/CutLabState.cs` (MODIFY — add `Goals` property)

**Analog:** itself — the existing `RoleFloors`/`Intent` properties on the same record (`CutLabState.cs:40,43`)

```csharp
// Source: DeckFlow.Web/Models/CutLab/CutLabState.cs:36-43
/// <summary>
/// User-adjusted role floors plus their user-set flags. Derived defaults are recomputed per POST,
/// never persisted, and the empty initializer keeps pre-102 JSON blobs deserializing cleanly.
/// </summary>
public IReadOnlyList<CutLabRoleFloor> RoleFloors { get; init; } = [];

/// <summary>Declared target intent for the finished 100-card deck.</summary>
public CutLabIntent Intent { get; init; } = new();
```
**Apply:** add `public CutLabGoalSettings Goals { get; init; } = new();` with the same doc-comment convention noting the empty/default initializer keeps pre-104 JSON blobs deserializing cleanly (mirrors the `RoleFloors` comment's back-compat framing exactly).

---

### `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` (MODIFY — Goals bounding + round-trip)

**Analog:** itself (`Deserialize`, `CutLabStateSerializer.cs:41-69`)

```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs:48-64
try
{
    var state = JsonSerializer.Deserialize<CutLabState>(json, Options) ?? new CutLabState();
    state = state with
    {
        Packages = state.Packages
            .Where(package => !string.IsNullOrWhiteSpace(package.Name))
            .Take(MaxPackages)
            .ToArray(),
        Decisions = state.Decisions
            .Where(decision => !string.IsNullOrWhiteSpace(decision.CardName))
            .Take(MaxDecisions)
            .ToArray(),
    };

    return CutLabFloorRules.ClampFloors(CutLabLockRules.EnforceCommanderLock(state));
}
catch (JsonException)
{
    return new CutLabState();
}
```
**Apply:** `CutLabGoalSettings` needs no allow-list bounding (it's 3 ints, not a list) but DOES need range clamping (V5 in RESEARCH.md — clamp turn inputs server-side to e.g. 1–15) before being threaded into `PercentByTurn`. Follow the `CutLabFloorRules.ClampFloors` precedent (`DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs:27-40`, `MaxFloor = CutLabPoolValidator.MaxPoolCards + 1`) — add an equivalent `CutLabGoalRules.ClampGoals(state)` static helper (or inline clamp in the record's `init`) rather than validating ad hoc in the controller, so both the JSON and no-JS paths share one clamp.

---

### `DeckFlow.Web/Services/CutLab/CutLabSimulationService.cs` (MODIFY — thread goals, fix clamp)

**Analog:** itself (`BuildMetrics`, `PercentByTurn`, `CutLabSimulationService.cs:268-293,382-399`)

**Current fixed-constant read (imports/core pattern, lines 284-286):**
```csharp
Metric(CutLabMetricKind.CommanderByTurn, CutLabMetricFamily.CategoryByTurn, "Commander by turn 3",
    PercentByTurn(commander, CutLabCategoryByTurnDefaults.CommanderByTurn), CutLabMetricUnit.Percent),
Metric(CutLabMetricKind.EngineByTurn, CutLabMetricFamily.CategoryByTurn, "Engine by turn 2",
    MaxPercentByTurn(engineRows, CutLabCategoryByTurnDefaults.EngineByTurn), CutLabMetricUnit.Percent),
Metric(CutLabMetricKind.RepresentativeLineByTurn, CutLabMetricFamily.CategoryByTurn, "Representative line by turn 4",
    MaxPercentByTurn(lineRows, CutLabCategoryByTurnDefaults.RepresentativeLineByTurn), CutLabMetricUnit.Percent),
```
**Becomes (thread a `CutLabGoalSettings goals` parameter through `BuildMetrics`/the private static `BuildSnapshot(deckEntries, playExperience, trialsOverride, goals)` overload — call sites at lines 137 and (via the public `BuildSnapshot` at 86-96) must pass `state.Goals` down):**
```csharp
Metric(CutLabMetricKind.CommanderByTurn, CutLabMetricFamily.CategoryByTurn, $"Commander by turn {goals.CommanderByTurn}",
    PercentByTurn(commander, goals.CommanderByTurn), CutLabMetricUnit.Percent),
```

**Error-handling / clamp-bug fix pattern (Pitfall 1, exact current code, lines 385-399):**
```csharp
private static double PercentByTurn(CardCastability? row, int turn)
{
    if (row is null)
    {
        return 0;
    }

    if (row.EarlyCastPercents.Count == 0)
    {
        return turn >= row.OnCurveTurn ? row.CastPercent : 0;
    }

    int index = Math.Clamp(turn - 1, 0, row.EarlyCastPercents.Count - 1);
    return row.EarlyCastPercents[index];
}
```
Add a branch before the clamp: `if (turn >= row.OnCurveTurn) { return row.CastPercent; }` — mirrors the existing `EarlyCastPercents.Count == 0` branch's own logic, so the fix is idiomatic to the function, not a bolt-on.

**Call-site plumbing:** `BuildSnapshot` (public API, `CutLabSimulationService.cs:19-24,86-96`) already accepts `workingList`/`playExperience`; the goal turns must ride on `CutLabState.Goals` and be passed by the caller (controller), since `BuildSnapshot`'s signature takes a working list, not a full `CutLabState` — do not thread `CutLabState` itself into the simulation service (keeps the existing dependency direction: service knows about pool cards/mode, not the state envelope).

---

### `DeckFlow.Web/Controllers/Api/CutLabApiController.cs` (MODIFY — add `PostWhatifAsync`)

**Analog:** `PostDecideAsync`, same file, `CutLabApiController.cs:36-143`

**Imports pattern (lines 1-9):**
```csharp
using DeckFlow.Core.Manabase;
using DeckFlow.Web.Infrastructure;
using DeckFlow.Web.Models.Api;
using DeckFlow.Web.Models.CutLab;
using DeckFlow.Web.Security;
using DeckFlow.Web.Services.CutLab;
using Microsoft.AspNetCore.Mvc;
```

**Route/flag/size-limit + same-origin pattern (lines 40-56):**
```csharp
[HttpPost("decide")]
[FeatureFlagGate("tool.cut-lab.enabled")]
[RequestSizeLimit(2 * 1024 * 1024)]
[ProducesResponseType(typeof(CutLabDecideApiResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public async Task<ActionResult<CutLabDecideApiResponse>> PostDecideAsync([FromBody] CutLabDecideApiRequest request, CancellationToken cancellationToken)
{
    if (!SameOriginRequestValidator.IsValid(Request))
    {
        return StatusCode(StatusCodes.Status403Forbidden, new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
    }

    if (request is null)
    {
        return BadRequest(new { Message = "Request body is required." });
    }

    if (string.IsNullOrWhiteSpace(request.CutLabStateJson) || string.IsNullOrWhiteSpace(request.CardName))
    {
        return BadRequest(new { Message = "Cut Lab state and card name are required." });
    }
    ...
}
```
**Apply verbatim** to a new `[HttpPost("whatif")]` action taking a new `CutLabWhatifApiRequest { CutLabStateJson, CardOut, CardIn }`: same feature-flag gate, same `[RequestSizeLimit(2 * 1024 * 1024)]`, same `SameOriginRequestValidator.IsValid(Request)` guard, same 400-on-missing-fields pattern, same `catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)` → `BadRequest(new { Message = CutLabMessages.NoChangeMessage })` (lines 138-142).

**Preview-without-mutation core pattern** (composed from RESEARCH.md's Pattern 2/3 + existing `CutLabWorkingList`/`CutLabDecisionApplier` — cite the exact swap-B source, `CutLabWorkingList.cs:29-37`):
```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs:29-37
public static IReadOnlySet<string> AcceptedCardNames(IReadOnlyList<CutLabDecision> decisions) =>
    LatestDecisionsByCard(decisions)
        .Where(entry => entry.Value.Kind == CutLabDecisionKind.Accepted)
        .Select(entry => entry.Key)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
```
Preview action calls `_simulationService.BuildSnapshot(...)` twice (before/after list surgery — never `CutLabDecisionApplier.Apply`) and diffs with `CutLabMetricDelta.Between` (`DeckFlow.Web/Models/CutLab/CutLabMetrics.cs:150-183`) exactly as `ComputeProposalDeltas` already does at `CutLabSimulationService.cs:165-173`.

**Commit ("Keep") composition pattern (new atomic action, e.g. `PostWhatifCommitAsync`):**
```csharp
// Source: composition of DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs (existing methods)
CutLabState afterRestore = CutLabDecisionApplier.Apply(state, cardB, CutLabDecideAction.Restore, roundKey: string.Empty);
CutLabState afterSwap = CutLabDecisionApplier.Apply(afterRestore, cardA, CutLabDecideAction.Accept, roundKey: "whatif-swap");
```
Register `"whatif-swap"` as a known round key + label in `CutLabCutRoundEngine` (`DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs:56,97,124-125` — `Round1Key`/`LabelFor`/`IsKnownRoundKey` all switch on a fixed set of string keys) so the Cuts-made list renders "What-if swap" instead of falling back to the raw key string.

**Guard for locked card A (Pitfall 4)** — re-validate server-side exactly as `CutLabDecisionApplier.Apply` already no-ops for locked cards:
```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs:26-30
CutLabPoolCard? poolCard = state.Pool.FirstOrDefault(card => string.Equals(card.Name, cardName, StringComparison.OrdinalIgnoreCase));
if (poolCard is not null && poolCard.IsLocked)
{
    return CutLabLockRules.EnforceCommanderLock(state);
}
```

---

### `DeckFlow.Web/Controllers/CutLabController.cs` (MODIFY — add `Whatif` no-JS action)

**Analog:** `Decide`, same file, `CutLabController.cs:59-101`

```csharp
// Source: DeckFlow.Web/Controllers/CutLabController.cs:64-101
[HttpPost("/cut-lab/decide")]
[FeatureFlagGate("tool.cut-lab.enabled")]
[ValidateAntiForgeryToken]
[RequestSizeLimit(2 * 1024 * 1024)]
public async Task<IActionResult> Decide(CutLabRequest request, string cardName, CutLabDecideAction decision, string? roundKey = null)
{
    request ??= new CutLabRequest();

    if (string.IsNullOrWhiteSpace(request.CutLabStateJson) || string.IsNullOrWhiteSpace(cardName))
    {
        return CutLabView(request, error: CutLabMessages.NoChangeMessage);
    }

    try
    {
        CutLabState state = CutLabStateSerializer.Deserialize(request.CutLabStateJson);
        string resolvedRoundKey = DetermineRoundKey(state, cardName, decision, roundKey);
        state = CutLabDecisionApplier.Apply(state, cardName, decision, resolvedRoundKey);
        RehydrateIntakeRequestFromState(request, state);
        request.CutLabStateJson = CutLabStateSerializer.Serialize(state);

        var result = await _pageService.ProcessAsync(request, HttpContext.RequestAborted);
        return View("CutLab", CutLabViewModel.From(request, result));
    }
    catch (InvalidOperationException exception)
    {
        return CutLabView(request, error: exception.Message);
    }
    catch (OperationCanceledException)
    {
        return CutLabView(request, error: "The request timed out. Try again.");
    }
    catch (Exception exception)
    {
        _logger.LogError(exception, "Cut Lab decision fallback failed.");
        return CutLabView(request, error: CutLabMessages.NoChangeMessage);
    }
}
```
**Apply:** new `Whatif(CutLabRequest request, string cardOut, string cardIn)` action following the identical structure — same route attributes, same `CutLabStateJson`/required-field guard, same `Serialize`→`ProcessAsync`→`View("CutLab", ...)` tail, same three-tier `catch` (`InvalidOperationException` / `OperationCanceledException` / generic). The action computes deltas AND (if the form posts a "Keep" intent) commits via the `CutLabDecisionApplier.Apply(Restore)+Apply(Accept)` composition above, full page re-render either way — this is the no-JS fallback per D-05/the system diagram in RESEARCH.md.

**Round-key resolution helper reused as-is:**
```csharp
// Source: DeckFlow.Web/Controllers/CutLabController.cs:103-111
private static string DetermineRoundKey(CutLabState state, string cardName, CutLabDecideAction decision, string? postedRoundKey)
{
    if (CutLabCutRoundEngine.IsKnownRoundKey(postedRoundKey))
    {
        return postedRoundKey!;
    }

    return CutLabDecisionApplier.LatestRoundForCard(state, cardName);
}
```

---

### `DeckFlow.Web/Models/Api/CutLabWhatifApiRequest.cs` / `CutLabWhatifApiResponse.cs` (NEW)

**Analog:** `DeckFlow.Web/Models/Api/CutLabDecideApiRequest.cs` (full file, 34 lines) and `CutLabDecideApiResponse.cs` (full file, 130 lines)

```csharp
// Source: DeckFlow.Web/Models/Api/CutLabDecideApiRequest.cs:1-18
using System.ComponentModel.DataAnnotations;

namespace DeckFlow.Web.Models.Api;

/// <summary>JSON request payload for applying one Cut Lab cut decision.</summary>
public sealed record CutLabDecideApiRequest
{
    /// <summary>Serialized Cut Lab working-session state envelope.</summary>
    [Required]
    public string CutLabStateJson { get; init; } = string.Empty;

    /// <summary>Display card name receiving the decision.</summary>
    [Required]
    public string CardName { get; init; } = string.Empty;

    /// <summary>Decision to apply to the named card.</summary>
    public CutLabDecideAction Decision { get; init; }
}
```
**Apply:** `CutLabWhatifApiRequest { [Required] CutLabStateJson, [Required] CardOut, [Required] CardIn }`. Response DTO mirrors `CutLabDecideApiResponse`'s delta-list shape exactly — reuse `CutLabDecideMetricDeltaDto` (`CutLabDecideApiResponse.cs:72-97`) verbatim for the swap deltas rather than declaring a parallel DTO (same `Kind`/`Label`/`Before`/`After`/`Delta`/`Unit`/`Direction`/`IsMeaningful` shape is exactly what `CutLabMetricDelta.Between` already produces).

---

### `DeckFlow.Web/wwwroot/ts/cut-lab-scenarios.ts` (NEW)

**Analog:** `DeckFlow.Web/wwwroot/ts/deck-input-store.ts` (full file, 225 lines)

**Try/catch silent-fail persistence pattern (lines 35-47):**
```typescript
// Source: DeckFlow.Web/wwwroot/ts/deck-input-store.ts:35-47
const setLastDeck = (state: LastDeckState): void => {
  try {
    const storedState: LastDeckState = {
      inputSource: state.inputSource,
      deckUrl: state.deckUrl,
      deckText: getDeckTextBytes(state.deckText) > DECK_TEXT_MAX_BYTES ? '' : state.deckText,
    };

    window.sessionStorage.setItem(LAST_DECK_KEY, JSON.stringify(storedState));
  } catch {
    // sessionStorage may be disabled or quota-limited; skip persistence silently.
  }
};
```

**Read/parse-with-fallback pattern (lines 49-69):**
```typescript
// Source: DeckFlow.Web/wwwroot/ts/deck-input-store.ts:49-69
const getLastDeck = (): LastDeckState | null => {
  try {
    const raw = window.sessionStorage.getItem(LAST_DECK_KEY);
    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw) as Partial<LastDeckState> | null;
    if (!parsed || typeof parsed !== 'object' || typeof parsed.inputSource !== 'string') {
      return null;
    }

    return { /* ...defensive field-by-field reconstruction... */ };
  } catch {
    return null;
  }
};
```

**Namespace-attach pattern used to expose the module to `cut-lab.ts`/inline script (lines 202-206):**
```typescript
// Source: DeckFlow.Web/wwwroot/ts/deck-input-store.ts:202-206
const win = window as DeckFlowWindow;
win.DeckFlow = win.DeckFlow ?? {};
win.DeckFlow.getLastDeck = getLastDeck;
win.DeckFlow.setLastDeck = setLastDeck;
```
**Apply:** build `saveScenario`/`loadScenario`/`deleteScenario`/`listScenarios` on `window.localStorage` (not `sessionStorage`) using the identical try/catch-and-degrade shape, IIFE module wrapper (`((): void => { 'use strict'; ... })();`, line 1), and a `win.DeckFlow.*` (or `win.DeckFlowCutLab.*`, matching `cut-lab.ts`'s own namespace at `cut-lab.ts:112-121`) attach point. Concrete index+slot key schema already drafted in RESEARCH.md's Pattern 4 (`SCENARIO_INDEX_KEY`, `SCENARIO_SLOT_PREFIX`, `MAX_SCENARIO_SLOTS = 20`, `QuotaExceededError`/`error.code === 22` detection) — reuse that draft verbatim as the starting point.

**Anti-pattern to avoid (Pitfall 2):** do not build a form-POST fallback for save/load — there is no server-side scenario store. Wrap the Scenarios panel in `<noscript>` messaging instead (see CutLab.cshtml pattern below).

---

### `DeckFlow.Web/wwwroot/ts/cut-lab.ts` (MODIFY — swap preview fetch/patch)

**Analog:** itself — `handleDecisionSubmit` + `extractDecisionPayload` + `setDecisionButtonsBusy`, `cut-lab.ts:997-1131`

**Fetch/patch/busy-state/timeout pattern (lines 1073-1130, the exact flow the swap preview must mirror):**
```typescript
// Source: DeckFlow.Web/wwwroot/ts/cut-lab.ts:1086-1120
const antiForgeryToken = getAntiForgeryToken(form);
const restoreBusyState = setDecisionButtonsBusy(form, submitter);
const controller = new AbortController();
const timeoutId = window.setTimeout(() => controller.abort(), cutLabDecisionTimeoutMs);

try {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  if (antiForgeryToken !== '') {
    headers.RequestVerificationToken = antiForgeryToken;
  }

  const response = await fetch(cutLabDecisionApiEndpoint, {
    method: 'POST',
    headers,
    body: JSON.stringify(payload),
    signal: controller.signal,
  });

  if (!response.ok) {
    renderDecisionError(form, await readErrorMessage(response));
    return;
  }

  const data = await response.json() as CutLabDecisionResponse;
  clearRestoreConfirmation();
  writeDecisionStateToHiddenInputs(data.cutLabStateJson);
  patchStickyBar(data);
  renderRoundBanner(data.nextProposal);
  renderProposalCard(data, antiForgeryToken);
} catch (error) {
  renderDecisionError(form, error instanceof DOMException && error.name === 'AbortError'
    ? cutLabDecisionTimeoutCopy
    : cutLabDecisionErrorCopy);
} finally {
  window.clearTimeout(timeoutId);
  restoreBusyState();
}
```
**Apply:** new `handleWhatifPreview`/`handleWhatifCommit` functions following this exact shape — new `cutLabWhatifApiEndpoint = '/api/cut-lab/whatif'` constant alongside `cutLabDecisionApiEndpoint` (line 130), same `AbortController`/timeout guard, same JSON `Content-Type` + `RequestVerificationToken` header pattern, same busy-state pattern reusing `setSubmitterBusyState` (lines 1018-1035). Preview response renders deltas only (no `writeDecisionStateToHiddenInputs` call — state must NOT mutate until Keep); Keep reuses `writeDecisionStateToHiddenInputs`/`patchStickyBar`/`renderRoundBanner`/`renderCutsMade` identically to `handleDecisionSubmit`'s post-success block (lines 1111-1120).

**Form-construction pattern for the no-JS-compatible swap form** (mirrors `createDecisionForm`/`buildDecisionFormBase`, `cut-lab.ts:659-710`):
```typescript
// Source: DeckFlow.Web/wwwroot/ts/cut-lab.ts:659-688
const buildDecisionFormBase = (
  cardName: string,
  roundKey: string,
  decisionValue: CutLabDecisionAction,
  serializedState: string,
  antiForgeryToken: string,
): HTMLFormElement => {
  const form = document.createElement('form');
  form.method = 'post';
  form.action = '/cut-lab/decide';

  const appendHiddenInput = (name: string, value: string): void => {
    const input = document.createElement('input');
    input.type = 'hidden';
    input.name = name;
    input.value = value;
    form.appendChild(input);
  };

  if (antiForgeryToken !== '') {
    appendHiddenInput(cutLabAntiForgeryFieldName, antiForgeryToken);
  }

  appendHiddenInput('CutLabStateJson', serializedState);
  appendHiddenInput('CardName', cardName);
  appendHiddenInput('RoundKey', roundKey);
  appendHiddenInput('Decision', decisionValue);

  return form;
};
```

**State-snapshot builder pattern** (`buildCutLabStateJson`, `cut-lab.ts:176-208`) — extend `CutLabStateSnapshot`/`buildSnapshotFromDom` (line 447) to include the 3 goal turn inputs, following the identical `snapshot.field → normalizedSnapshot.field` mapping shape already used for `intent`/`roleFloors`.

---

### `DeckFlow.Web/Views/Deck/CutLab.cshtml` (MODIFY — Goals section, Scenarios panel, Swap preview section)

**Analog:** the existing "Role floors" `<section class="result-panel">`, `CutLab.cshtml:485-540`

```cshtml
@* Source: DeckFlow.Web/Views/Deck/CutLab.cshtml:485-540 *@
<section class="result-panel">
    <div class="panel-heading">
        <div>
            <h2>Role floors</h2>
            <p>Minimum counts the finished 100 should keep, derived from your bracket and play experience. Adjust any floor — later cut suggestions will warn you before breaking one, never silently.</p>
        </div>
    </div>
    <div class="history-timeline__wrap">
        <table class="conflicts-table" data-prompt-cedh-reference-table role="table">
            <thead>
                <tr><th scope="col">Role</th><th scope="col">In pool</th><th scope="col">Floor</th><th scope="col">Source</th></tr>
            </thead>
            <tbody>
                @foreach (var row in Model.FloorRows)
                {
                    <tr data-cut-lab-floor-row="@row.RoleKey" data-cut-lab-floor-count="@row.InPoolCount"
                        data-cut-lab-floor-default="@row.DefaultValue" data-cut-lab-floor-user-set="@(row.IsUserSet ? "true" : "false")">
                        <td data-label="Role"><strong>@row.DisplayLabel</strong></td>
                        <td data-label="In pool">
                            <span data-cut-lab-floor-count-label>@row.InPoolCount in pool</span>
                            <span class="cutlab-floor-state--at @(row.AtFloor ? string.Empty : "hidden")" data-cut-lab-floor-at-marker>· at floor</span>
                        </td>
                        <td data-label="Floor">
                            <input type="number" min="0" max="@Model.CardCount" step="1"
                                   data-cut-lab-floor="@row.RoleKey" value="@row.Floor" />
                        </td>
                        <td data-label="Source">
                            <span class="cutlab-floor-source-default @(row.IsUserSet ? "hidden" : string.Empty)" data-cut-lab-floor-source-default>@row.SourceLabel</span>
                            <span class="kb-chip cutlab-floor-badge--adjusted @(row.IsUserSet ? string.Empty : "hidden")" data-cut-lab-floor-adjusted-badge>Adjusted</span>
                            <button type="button" class="cutlab-floor-reset @(row.IsUserSet ? string.Empty : "hidden")"
                                    data-cut-lab-floor-reset="@row.RoleKey" data-cut-lab-floor-default="@row.DefaultValue">Reset to default</button>
                        </td>
                    </tr>
                }
            </tbody>
        </table>
    </div>
    <div class="toolbar">
        <button type="button" class="run-button" data-cut-lab-recalculate>Recalculate analysis</button>
    </div>
</section>
```
**Apply to Goals section:** same `<section class="result-panel">` + `panel-heading` + `<table class="conflicts-table">` shape, one `<tr>` per goal (Commander/Engine/Representative-line), `<input type="number">` per turn target with `data-cut-lab-goal="@kind"` (mirrors `data-cut-lab-floor="@row.RoleKey"`), reuse the same `cutlab-floor-source-default`/`Reset to default` visual convention for "seeded from cEDH defaults" vs. user-edited state. Casual-mode representative-line row: hide/relabel per Pitfall 5 using the same `hidden` class-toggle idiom already used for `IsUserSet`.

**No-JS-form root pattern** (top of file, existing decide form, `CutLab.cshtml:24`):
```cshtml
@* Source: DeckFlow.Web/Views/Deck/CutLab.cshtml:24 *@
<form method="post" action="@Url.Content("~/cut-lab/decide")">
```
**Apply to Swap preview no-JS form:** `<form method="post" action="@Url.Content("~/cut-lab/whatif")">` with `<select>` for card A (working list, excluding locked cards per Pitfall 4) and card B (cut pile / `state.Pool - workingList`), same `[ValidateAntiForgeryToken]` token field convention.

**Scenarios panel:** wrap in `<noscript>` messaging ("Named scenarios require JavaScript") per Pitfall 2 — no `<form action="...">` exists for this panel since there is no server endpoint; the panel is JS-only markup (buttons/inputs with `data-cut-lab-scenario-*` attributes) that `cut-lab-scenarios.ts` wires up entirely client-side.

## Shared Patterns

### Same-origin validation (all new POST endpoints)
**Source:** `DeckFlow.Web/Security/SameOriginRequestValidator.cs` — consumed at `CutLabApiController.cs:48-51`
```csharp
if (!SameOriginRequestValidator.IsValid(Request))
{
    return StatusCode(StatusCodes.Status403Forbidden, new { Message = SameOriginRequestValidator.GetForbiddenMessage() });
}
```
**Apply to:** the new `/api/cut-lab/whatif` action (and any new API action). Not needed for `cut-lab-scenarios.ts` (no network call — pure localStorage).

### Feature flag gate (all new controller actions)
**Source:** `[FeatureFlagGate("tool.cut-lab.enabled")]` — used on every existing Cut Lab action (`CutLabController.cs:26,32,65`; `CutLabApiController.cs:41`)
**Apply to:** every new action (`Whatif`, `PostWhatifAsync`) — same flag key, no new sub-flag per RESEARCH.md Assumption A2.

### Request size limit (all new POST bodies)
**Source:** `[RequestSizeLimit(2 * 1024 * 1024)]` — `CutLabController.cs:34,67`; `CutLabApiController.cs:42`
**Apply to:** `Whatif`/`PostWhatifAsync`; do not raise the cap, and keep `CutLabStateSerializer.MaxUploadBytes` (262,144 bytes, `CutLabStateSerializer.cs:11`) as the inner payload ceiling.

### Error handling: `InvalidOperationException`/`ArgumentException` → BadRequest, generic → logged 500-equivalent
**Source:** `CutLabApiController.cs:138-142` (API) and `CutLabController.cs:88-100` (no-JS, three-tier catch: `InvalidOperationException` → user message, `OperationCanceledException` → timeout copy, generic → logged + `CutLabMessages.NoChangeMessage`)
**Apply to:** both new actions, verbatim catch shape.

### Decision-state composition (no new enum/decision kind)
**Source:** `CutLabDecisionApplier.Apply`/private `Restore`, `CutLabDecisionApplier.cs:15-52,68-78`
**Apply to:** swap commit = `Apply(state, cardB, Restore, "")` then `Apply(afterRestore, cardA, Accept, "whatif-swap")`. Never introduce `CutLabDecisionKind.Swapped`.

### Metric delta computation (no new diff math)
**Source:** `CutLabMetricDelta.Between`, `DeckFlow.Web/Models/CutLab/CutLabMetrics.cs:150-183`; consumed identically at `CutLabSimulationService.cs:165-173` (`ComputeProposalDeltas`)
**Apply to:** swap preview deltas and (if surfaced) goal pass/miss-vs-target comparisons.

### localStorage try/catch silent-degrade (client persistence)
**Source:** `deck-input-store.ts:27-47` (see full excerpt above)
**Apply to:** every `cut-lab-scenarios.ts` read/write function.

## No Analog Found

None. Every file in this phase's file list has an exact same-file or same-role analog already in the Cut Lab codebase, confirming RESEARCH.md's central finding: Phase 104 is 100% compositional reuse of 101–103 primitives (SIM-01 compliant by construction). The two genuinely new client/server surfaces (`cut-lab-scenarios.ts`, the `/api/cut-lab/whatif` + `/cut-lab/whatif` action pair) still have a same-shape sibling to copy structurally (`deck-input-store.ts`; `PostDecideAsync`/`Decide`), even though their *content* (localStorage scheme, before/after diff without commit) is new glue code per RESEARCH.md's summary.

## Metadata

**Analog search scope:** `DeckFlow.Web/Models/CutLab/`, `DeckFlow.Web/Services/CutLab/`, `DeckFlow.Web/Controllers/`, `DeckFlow.Web/Controllers/Api/`, `DeckFlow.Web/Models/Api/`, `DeckFlow.Web/wwwroot/ts/`, `DeckFlow.Web/Views/Deck/CutLab.cshtml`, `DeckFlow.Web.Tests/CutLab*.cs`
**Files scanned:** 24 existing Cut Lab production files + 20 existing Cut Lab test files (via `find`/`grep`), 8 read in full or targeted sections
**Pattern extraction date:** 2026-07-20
