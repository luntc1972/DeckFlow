---
phase: 106-partial-quantity-tuning
plan: 03
type: execute
wave: 3
depends_on: ["106-02"]
autonomous: true
requirements: [EDIT-01, EDIT-02, EDIT-03]
files_modified:
  - DeckFlow.Web/Services/CutLab/CutLabAdjustmentApplier.cs
  - DeckFlow.Web/Models/Api/CutLabAdjustApiRequest.cs
  - DeckFlow.Web/Models/Api/CutLabAdjustApiResponse.cs
  - DeckFlow.Web/Controllers/Api/CutLabApiController.cs
  - DeckFlow.Web/Controllers/CutLabController.cs
  - DeckFlow.Web/Models/CutLabRequest.cs
  - DeckFlow.Web.Tests/CutLabAdjustmentApplierTests.cs
  - DeckFlow.Web.Tests/CutLabApiControllerTests.cs
  - DeckFlow.Web.Tests/CutLabControllerTests.cs

must_haves:
  truths:
    - "A state-mutating POST applies a signed copy delta (or add-basic) and returns the updated state plus the new remaining-to-100 count"
    - "The server rejects a quantity above 1 for any card that is not a basic or a recognized any-number card, regardless of client payload"
    - "add-basic is accepted only for names in the basics constants whitelist"
    - "A crafted extreme Delta (int.MaxValue / int.MinValue / repeated large posts) cannot overflow the accumulated net delta or unbound CardsRemaining — the net is accumulated in long and clamped to a finite cap before casting back to int"
    - "A no-JS form POST applies the same adjustment and full-page re-renders with updated counts"
  artifacts:
    - path: "DeckFlow.Web/Services/CutLab/CutLabAdjustmentApplier.cs"
      provides: "Pure fold of a delta into CutLabState.QuantityAdjustments with server-side legality and overflow-safe (long) accumulation"
      contains: "IsLegalMultiple"
    - path: "DeckFlow.Web/Controllers/Api/CutLabApiController.cs"
      provides: "POST /api/cut-lab/adjust"
      contains: "adjust"
    - path: "DeckFlow.Web/Controllers/CutLabController.cs"
      provides: "POST /cut-lab/adjust no-JS fallback"
  key_links:
    - from: "CutLabApiController.PostAdjustAsync"
      to: "SameOriginRequestValidator.IsValid"
      via: "CSRF guard mirrored from decide/whatif"
      pattern: "SameOriginRequestValidator.IsValid"
    - from: "CutLabAdjustmentApplier.Apply"
      to: "CutLabLegality.IsLegalMultiple / CutLabBasicLands"
      via: "server-side singleton cap + added-basic whitelist"
---

<objective>
Add the write-path: a pure `CutLabAdjustmentApplier` that folds a signed copy delta (or add-basic) into
`CutLabState.QuantityAdjustments` with server-enforced legality and overflow-safe accumulation, exposed through a
JSON endpoint `POST /api/cut-lab/adjust` and a no-JS `POST /cut-lab/adjust` fallback that mirror the existing
decide/goals dual-post contract and progressive-enhancement pattern.

Purpose: Let the user apply copy edits and add basics (EDIT-01/02) with singleton legality enforced
server-side (EDIT-03) — never trusting the client to cap, and never letting a crafted extreme Delta overflow.
Output: Applier + request/response models + two endpoints + threat model + tests.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-DESIGN.md
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-01-SUMMARY.md
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-02-SUMMARY.md
@./CLAUDE.md

<interfaces>
Mirror these existing patterns exactly:

JSON endpoint (CutLabApiController.PostDecideAsync, lines 44-149):
- Attributes `[HttpPost("decide")] [FeatureFlagGate("tool.cut-lab.enabled")] [RequestSizeLimit(2*1024*1024)]`
- First line: `if (!SameOriginRequestValidator.IsValid(Request)) return StatusCode(403, new { Message = SameOriginRequestValidator.GetForbiddenMessage() });`
- Null/blank guards → BadRequest; deserialize via CutLabStateSerializer; `state.Pool.Count == 0` → BadRequest(InvalidStateMessage)
- On success returns serialized state + CardsRemaining computed from the round plan on the adjustment-derived list.
- catch (InvalidOperationException or ArgumentException) → BadRequest(CutLabMessages.NoChangeMessage).

No-JS action (CutLabController.Decide, lines 83-120):
- `[HttpPost("/cut-lab/decide")] [FeatureFlagGate("tool.cut-lab.enabled")] [ValidateAntiForgeryToken] [RequestSizeLimit(2*1024*1024)]`
- Deserialize → apply → RehydrateIntakeRequestFromState → re-serialize → _pageService.ProcessAsync → View("CutLab", ...).

Decision applier analog (CutLabDecisionApplier.Apply, lines 15-64): pure static, returns new CutLabState via
`state with { ... }`, ends with CutLabLockRules.EnforceCommanderLock.

Overflow note (MED-2): the API request `Delta` is an unbounded int and an existing persisted `Delta` is also an
int, so summing `existingDelta + postedDelta` in int can wrap BEFORE any `Math.Clamp`, and the just-computed
response value is not protected by the serializer's on-deserialize clamp. Accumulate the net in `long`, clamp to
a finite cap (reuse the 106-01 MaxCopyDelta / LegalMax bound), then cast back to int.

Legality helpers (106-01): CutLabLegality.IsLegalMultiple/LegalMax, CutLabBasicLands.TryResolve/Names/Contains.
Request model analog: DeckFlow.Web/Models/Api/CutLabDecideApiRequest.cs.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: CutLabAdjustmentApplier (overflow-safe) + request/response models</name>
  <read_first>
    - DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs (analog: pure static applier returning new state)
    - DeckFlow.Web/Services/CutLab/CutLabLegality.cs + CutLabBasicLands.cs (106-01: legality + whitelist + MaxCopyDelta/LegalMax bound)
    - DeckFlow.Web/Models/Api/CutLabDecideApiRequest.cs (analog request record)
  </read_first>
  <behavior>
    - Apply(+1) on "Forest" (existing 10) records/merges a +1 adjustment for Forest.
    - Apply(-1) merges into net delta; a name whose net delta returns to 0 drops the adjustment entry (no dead rows).
    - Apply(+1) on "Sol Ring" (singleton, not legal-multiple) is rejected — state unchanged / throws the shared no-change signal.
    - Apply add-basic on "Island" with delta +2 records an IsAddedBasic=true adjustment; add-basic on a non-basic name is rejected.
    - The added-basic net delta clamps at CutLabLegality.LegalMax and never goes below 0.
    - Overflow safety: a posted Delta of int.MaxValue (or int.MinValue), and repeated large +posts that would sum past int range, do NOT wrap — the accumulated net is computed in long, clamped to the finite cap, then cast to int; the resulting CardsRemaining stays bounded and legal.
  </behavior>
  <action>
    Create CutLabAdjustmentApplier.cs (pure static) with `Apply(CutLabState state, string cardName, int delta,
    bool isAddedBasic)` returning a new CutLabState. Validate server-side: if `delta > 0` (or resulting quantity
    would exceed 1) require `CutLabLegality.IsLegalMultiple(cardName)`; if `isAddedBasic` require
    `CutLabBasicLands` contains the name — otherwise reject via `throw new InvalidOperationException(
    CutLabMessages.NoChangeMessage)` (the shared no-change contract the endpoints already catch). Merge the delta
    into `state.QuantityAdjustments` by ordinal-normalized name: compute the net in `long`
    (`(long)existingDelta + delta`), clamp to a finite range (lower bound = the removable floor / -MaxCopyDelta,
    upper bound = min(CutLabLegality.LegalMax(name), MaxCopyDelta)), THEN cast back to int — do not sum in int
    first. Set IsAddedBasic when materializing a basic not in Pool. Drop entries whose net delta is 0, and return
    `EnforceCommanderLock(state with { QuantityAdjustments = ... })`. Add CutLabAdjustApiRequest `{ [Required]
    string CutLabStateJson; [Required] string CardName; int Delta; bool IsAddedBasic; }` and CutLabAdjustApiResponse
    `{ string CutLabStateJson; int CardsRemaining; }` (records, `{ get; init; }`), mirroring the decide models.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabAdjustmentApplierTests" 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - CutLabAdjustmentApplier.Apply merges/drops deltas by name and returns a new state via `with`.
    - A +1 on a singleton (non-legal-multiple) is rejected server-side; a +1 on a basic/any-number card is accepted.
    - add-basic is accepted only for a name in CutLabBasicLands; a non-basic add-basic is rejected.
    - Overflow tests prove int.MaxValue, int.MinValue, and repeated large +posts clamp to the finite cap without wrapping (net accumulated in long); the resulting adjustment Delta and the derived CardsRemaining stay bounded.
    - Request/response records exist with the specified fields and `{ get; init; }` accessors.
    - CutLabAdjustmentApplierTests green.
  </acceptance_criteria>
  <done>Server-side adjustment application with legality + whitelist + overflow-safe (long) accumulation is a tested pure function.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: JSON /api/cut-lab/adjust + no-JS /cut-lab/adjust endpoints</name>
  <read_first>
    - DeckFlow.Web/Controllers/Api/CutLabApiController.cs (PostDecideAsync — copy the guard/deserialize/round-plan/serialize shape; reuse GetCommanderNames, BuildFloorMap)
    - DeckFlow.Web/Controllers/CutLabController.cs (Decide — copy the no-JS deserialize→apply→rehydrate→ProcessAsync→View shape)
    - DeckFlow.Web/Models/CutLabRequest.cs (add the posted CardName/Delta/IsAddedBasic fields for the no-JS form)
  </read_first>
  <behavior>
    - JSON: valid same-origin +2 Island adjustment returns 200 with updated CutLabStateJson and CardsRemaining reduced by 2.
    - JSON: cross-origin request returns 403; missing state/cardName returns 400; empty pool returns 400.
    - JSON: a +1 on a singleton returns 400 with the no-change message (server refuses, not the client).
    - JSON: a crafted int.MaxValue Delta returns 200 with a bounded CardsRemaining (no wrap / no negative overflow).
    - No-JS: posting the adjust form applies the delta and returns the full CutLab view with the updated sticky count; missing anti-forgery token is rejected by the framework.
  </behavior>
  <action>
    In CutLabApiController add `PostAdjustAsync([FromBody] CutLabAdjustApiRequest request, CancellationToken ct)`
    at route `[HttpPost("adjust")]` with the same three attributes as decide. Mirror decide's control flow:
    SameOrigin 403 guard, null/blank guards, deserialize, empty-pool guard, then
    `state = CutLabAdjustmentApplier.Apply(state, request.CardName, request.Delta, request.IsAddedBasic);` derive
    the adjustment-derived working list, build the round plan (reuse the decide helpers) to get
    `CardsRemainingToTarget`, and return `new CutLabAdjustApiResponse { CutLabStateJson =
    CutLabStateSerializer.Serialize(state), CardsRemaining = roundPlan.CardsRemainingToTarget }`. Same catch as
    decide → BadRequest(NoChangeMessage). In CutLabController add `Adjust(CutLabRequest request, string cardName,
    int delta, bool isAddedBasic)` at `[HttpPost("/cut-lab/adjust")]` with `[ValidateAntiForgeryToken]` and the
    other decide attributes, mirroring the Decide body (deserialize → CutLabAdjustmentApplier.Apply →
    RehydrateIntakeRequestFromState → re-serialize → ProcessAsync → View). Add the three posted fields to
    CutLabRequest if not already present.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabApiControllerTests|FullyQualifiedName~CutLabControllerTests" 2>&1 | tail -6</automated>
  </verify>
  <acceptance_criteria>
    - `POST /api/cut-lab/adjust` exists with `[FeatureFlagGate("tool.cut-lab.enabled")]`, SameOrigin 403 guard, and returns updated state + CardsRemaining.
    - A controller test proves a cross-origin adjust POST returns 403 and a singleton +1 returns 400 (server-enforced).
    - A test proves a +2 basic adjustment lowers CardsRemaining by 2 in the response.
    - A test proves an int.MaxValue Delta yields a 200 with bounded CardsRemaining (no overflow).
    - `POST /cut-lab/adjust` no-JS action exists with `[ValidateAntiForgeryToken]` and re-renders the CutLab view.
    - Named test filters green; `dotnet build DeckFlow.Web` clean.
  </acceptance_criteria>
  <done>Both the JSON and no-JS adjustment endpoints apply deltas with legality + CSRF + overflow guards and update the count.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| browser → /api/cut-lab/adjust | Untrusted JSON copy-delta payload crosses into state mutation |
| browser → /cut-lab/adjust | Untrusted form post (no-JS) crosses into state mutation |
| client-supplied CutLabStateJson | Round-tripped session state can be tampered before re-post |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-106-01 | Spoofing/Tampering | adjust endpoints (cross-origin forged POST) | mitigate | `SameOriginRequestValidator.IsValid` 403 guard on the JSON endpoint; `[ValidateAntiForgeryToken]` on the no-JS action — identical to decide/goals/what-if |
| T-106-02 | Tampering/Elevation | CutLabAdjustmentApplier (singleton-cap bypass via crafted Delta) | mitigate | Server-side `CutLabLegality.IsLegalMultiple` check before accepting any quantity > 1; reject with NoChangeMessage regardless of client-sent flags |
| T-106-03 | Denial of Service | CutLabAdjustmentApplier + serializer (Delta overflow / unbounded QuantityAdjustments growth) | mitigate | Applier accumulates the net delta in `long` and clamps to a finite cap (min(LegalMax, MaxCopyDelta)) BEFORE casting back to int, so `existingDelta + postedDelta` cannot wrap and the response CardsRemaining stays bounded; serializer additionally bounds the collection (MaxQuantityAdjustments) + clamps per-entry Delta (MaxCopyDelta) from 106-01; applier drops zero entries |
| T-106-04 | Spoofing | CutLabAdjustmentApplier (added-basic name spoofing) | mitigate | `isAddedBasic` accepted only for names in the CutLabBasicLands whitelist; any other name rejected |
| T-106-05 | Tampering | CutLabStateJson oversize payload | accept | Existing `MaxUploadBytes` cap + `[RequestSizeLimit]` already bound the payload; no new surface |

No package-manager installs in this plan → no supply-chain (T-106-SC) threat.
Block on any HIGH-severity finding (T-106-01, T-106-02, T-106-04 are the singleton-legality/CSRF core; T-106-03
overflow is a tested clamp).
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean, no new warnings.
- Web.Tests green: CutLabAdjustmentApplierTests, CutLabApiControllerTests, CutLabControllerTests.
- Manual/threat checks encoded as tests: cross-origin 403, singleton +1 → 400, add-basic whitelist, int.MaxValue/int.MinValue overflow clamp.
- LF endings preserved; changed-lines format-gate clean.
</verification>

<success_criteria>
The user can apply a signed copy delta or add a basic through a same-origin JSON endpoint (JS) or an
anti-forgery-protected form (no-JS); the server enforces singleton legality and the basics whitelist, accumulates
the net delta overflow-safely in long, updates QuantityAdjustments, and returns a bounded remaining-to-100 count.
</success_criteria>

<line_endings>
Preserve each touched file's existing line endings exactly (LF via .gitattributes). New files use LF. Change only
the lines whose content changes; leave everything else byte-for-byte identical.
</line_endings>

<output>
Create `.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-03-SUMMARY.md` when done.
</output>
