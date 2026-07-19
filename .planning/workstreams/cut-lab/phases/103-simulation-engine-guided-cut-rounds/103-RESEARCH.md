# Phase 103: Simulation Engine & Guided Cut Rounds - Research

**Researched:** 2026-07-19
**Domain:** Reuse of DeckFlow's existing Monte Carlo manabase/mulligan/plan-presence engines to drive a guided, evidence-first cut-round UI over the Cut Lab working pool.
**Confidence:** MEDIUM-HIGH — the reused engines and API/state patterns are directly read from source (HIGH); several structural questions (metric mapping for "flood/screw/curve risk" and "per-goal category-by-turn", and card-data persistence across the async endpoint) surfaced real gaps that need planner/user decisions (flagged below, MEDIUM-LOW).

## Summary

Phase 103 does not need to write any new simulation math. `DeckFlow.Core/Manabase/CastabilitySimulator.cs` and `ManabaseAnalyzer.cs` already compute every metric SIM-01 asks for except two: "flood/screw/curve risk" and "per-goal category-by-turn probability" have no single existing field with that exact name — they must be assembled from existing report fields (documented below), and "per-goal" cannot mean user-defined goals yet because GOAL-01..03 (turn-based goal definitions) is Phase 104, not 103. This is a real scope ambiguity in the roadmap wording that the planner must resolve explicitly (see Open Questions).

The two biggest engineering findings that change how Phase 103 must be planned:

1. **Determinism already exists — D-08 is nearly free.** `CastabilitySimulator` seeds every Monte Carlo run from a stable FNV-1a hash of a fixed string (the spell name, or a fixed sentinel like `"__deckflow_plan_presence__"`), never from wall-clock or a global mutable `Random`. Given the same `ManabaseDeck` + same spell composition + same trial count, results are already byte-identical across runs and machines. D-08's "fixed seed" decision is already satisfied by the existing engine; the only new work is the noise-floor threshold for the delta *display* layer (comparing two already-deterministic numbers and deciding when the diff is "meaningful").

2. **Resolved card data (`ScryfallCardData`) is NOT persisted across requests today — this is the load-bearing gap for D-09/D-10/D-11.** `CutLabPageService.ProcessAsync` re-resolves every pool card from Scryfall (`cards/collection`, batched 75/call) on every full-page POST, then discards the `ScryfallCardData` — only `TypeLine` survives into `CutLabPoolCard`/`CutLabState`. But `CastabilitySimulator`/`ManabaseAnalyzer` need full `CardFact` (mana cost, oracle text, produced mana, etc.) built from `ScryfallCardData` via `ScryfallCardFactMapper.ToCardFact`. A ~1s-budget async JSON decision endpoint (D-09) cannot afford a fresh Scryfall round-trip per accept/reject. Phase 103 must add a server-side resolved-card cache (session/pool-hash keyed, `IMemoryCache`, matching the `PacketSessionCache` precedent) populated once at pool intake/lock and reused by every simulation call thereafter.

**Primary recommendation:** Build a single `CutLabSimulationService` in `DeckFlow.Web/Services/CutLab/` that (a) resolves the working list's `CardFact`s once via a pool-hash-keyed `IMemoryCache` entry populated at intake, (b) classifies them through the *existing* `ManabaseClassifier.Classify(facts, isSingleton: true, ...)` → `ManabaseAnalyzer.Analyze(deck, mode, ...)` pipeline exactly as `ManabaseAnalysisService` does for the `/manabase` page, and (c) projects `ManabaseReport` fields into the 7 delta-display metrics, caching per-(working-list-hash, card) deltas in a second `IMemoryCache` per D-10/D-12.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Cut-round sequencing (obvious/structural/preference) | API / Backend (`DeckFlow.Web/Services/CutLab/`) | — | Pure ordering logic over Phase 102 findings + role data; no simulation needed to decide order. |
| Floor-break evaluation per proposed cut | API / Backend (`CutLabFloorRules.Evaluate`, existing) | — | Already built in Phase 102 explicitly as the Phase 103 contract; reuse verbatim. |
| Metric recalculation (7 families) | API / Backend (`DeckFlow.Core/Manabase/*` via new `CutLabSimulationService`) | — | CPU-only, no HTTP; must stay in Core/Web-service tier, never in the browser. |
| Card data resolution (Scryfall) | API / Backend (`IScryfallCardResolver`, cached) | — | External HTTP call; must happen once per session, not per decision. |
| Delta cache (per working-list hash + card) | API / Backend (`IMemoryCache`, dedicated instance) | — | Matches `PacketSessionCache` precedent; must stay server-side (512MB RAM cap awareness). |
| Async accept/reject/defer action | API / Backend (new JSON controller under `Controllers/Api/`) | Browser / Client (`cut-lab.ts` fetch + DOM patch) | D-09 explicitly splits transport (JSON POST) from rendering (TS patches the proposal card + metrics in place). |
| Baseline snapshot (pool-lock time) | API / Backend (computed once, stored in `CutLabState`) | — | D-12: must survive restarts, so it belongs in the persisted state envelope, not a volatile cache. |
| Sticky progress bar / proposal card UI | Browser / Client (`cut-lab.ts`, `CutLab.cshtml`, theme CSS) | — | Presentation-only; no business logic. |
| No-JS fallback (form POST) | Frontend Server (Razor `CutLabController.Process`) | API / Backend | Existing full-page POST flow stays as the resilience fallback per D-09. |

## Standard Stack

### Core

No new external packages. Every simulation dependency is already in-tree:

| Component | Location | Purpose | Why Standard (for this phase) |
|-----------|----------|---------|--------------------------------|
| `CastabilitySimulator` | `DeckFlow.Core/Manabase/CastabilitySimulator.cs` | Seeded Monte Carlo per-spell castability, plan-presence sim, curve-coverage sim | The mandated reuse target (D-01..D-16, SIM-01: "no new simulation math"). |
| `ManabaseAnalyzer` | `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` | Orchestrates castability rows → color findings → mulligan evaluation → plan presence → tap analysis → interaction lens into one `ManabaseReport` | Single entry point already used by the `/manabase` page; Phase 103 must call the same pipeline shape against the Cut Lab working list. |
| `ManabaseClassifier.Classify` | `DeckFlow.Core/Manabase/ManabaseClassifier.cs` | `IReadOnlyList<CardFact>` → `ManabaseDeck` | Required input-shaping step before `ManabaseAnalyzer.Analyze` will run. |
| `ScryfallCardFactMapper.ToCardFacts` | `DeckFlow.Core/Manabase/ScryfallCardFactMapper.cs` | `DeckCardEntry` (with full `ScryfallCardData`) → `CardFact` | The mapping step that needs full card data, not just `TypeLine` — see the D-09/D-12 gap above. |
| `CedhMulliganCalibration` | `DeckFlow.Core/Manabase/CedhMulliganCalibration.cs` | Fixed turn-cap constants (Shape A=3, Shape B=2, representative-line cap=4) for cEDH keep-shape gating | Not a simulator itself; these constants already encode "cast commander/engine by turn N" style thresholds that the "per-goal category-by-turn" family can borrow as *default* (non-user-editable) turn caps for Phase 103. |
| `PacketSessionCache` | `DeckFlow.Web/Services/PacketSessionCache.cs` | Dedicated `MemoryCache` with SHA-256 field-bag key, TTL, size accounting, eviction logging | Direct precedent for the D-10/D-12 delta cache — copy the pattern (private `MemoryCache` instance, not the shared `IMemoryCache` singleton, to avoid LRU contention under the 512MB Render cap). |
| `SameOriginRequestValidator` + `DeckSyncApiController` | `DeckFlow.Web/Security/`, `DeckFlow.Web/Controllers/Api/` | JSON-API CSRF guard + reference controller shape | The exact pattern D-09 calls for; see Code Examples. |

### Supporting

| Component | Purpose | When to Use |
|-----------|---------|-------------|
| `ManabaseMode` enum (`Casual`/`Focused`/`Cedh`) | Selects land-target baseline + color thresholds | `CutLabRoleAssigner.ResolveMode(playExperience)` already resolves this from the Cut Lab intent; reuse the same call. |
| `IManabaseBaselineProvider` / `ICedhLandBaselineProvider` | Bracket/commander land-count baselines | Already wired into `CutLabPageService`; needed if Phase 103's before/after view wants the same baseline context the `/manabase` page shows. |
| `IMemoryCache` (shared, `AddMemoryCache()` in `Program.cs:69`) | ASP.NET Core built-in memory cache | Do NOT put the delta cache or the resolved-card cache directly on the shared singleton — follow `PacketSessionCache`'s dedicated-instance pattern so Cut Lab's cache pressure never evicts other features' cached data (and vice versa) under the 512MB cap. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Server-side resolved-card cache (recommended) | Extend `CutLabState` to carry full `ScryfallCardData` per pool card | State would balloon well past the existing 256KB `CutLabStateSerializer.MaxUploadBytes` cap for a 150-card pool (mana cost + oracle text + produced_mana per card is much larger than the current `TypeLine`-only footprint) — rejected; D-12 explicitly keeps the persisted state to a "compact numeric snapshot" + role floors, not raw card data. |
| Full `ManabaseAnalyzer.Analyze` per decision action | A cut-down, Cut-Lab-specific mini analyzer that only recomputes the changed metric families | Explicitly forbidden by SIM-01 ("no new simulation math") and by D-11's stated fallback (reduce iteration counts, not fork the algorithm). Use the real analyzer with reduced `trials`, not a reimplementation. |

**Installation:** None — no `npm install` / `dotnet add package` needed for this phase. If the Package Legitimacy Audit gate is skipped for a phase, note why:

## Package Legitimacy Audit

**Not applicable.** This phase introduces zero external packages (NuGet, npm, or otherwise); it is a pure reuse-and-orchestration phase over existing `DeckFlow.Core`/`DeckFlow.Web` code. No `slopcheck`/registry verification was run because there is nothing to verify.

## Architecture Patterns

### System Architecture Diagram

```text
Browser (cut-lab.ts)
   │  1. Load Cut Lab page → CutLabController.Index/Process (existing, unchanged)
   │     renders working pool + Phase 102 structural findings
   ▼
Round Engine (new, pure — DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs)
   │  2. Compute round queues from CutLabStructuralFindings + CutLabRoleAssigner:
   │     Round 1 = cards flagged by 2+ findings
   │     Round 2 = cards flagged by exactly 1 finding
   │     Round 3 = remaining unlocked cards, weakest-delta-first
   │     Loop = deferred, then rejected, cards if still >100
   ▼
Proposal presented one card at a time (server-rendered + JS-hydrated)
   │  3. Browser POSTs accept/reject/defer via fetch()
   ▼
POST /api/cut-lab/decide  (new — SameOriginRequestValidator-guarded JSON controller)
   │  4. Validate same-origin, floor-check via CutLabFloorRules.Evaluate
   │  5. Apply decision to in-memory working-list projection (CutLabState.Pool minus cuts)
   ▼
CutLabSimulationService (new — DeckFlow.Web/Services/CutLab/)
   │  6. Resolve CardFacts for working list from resolved-card cache (populated at intake)
   │  7. ManabaseClassifier.Classify(facts, isSingleton:true) → ManabaseDeck
   │  8. ManabaseAnalyzer.Analyze(deck, mode, ...) → ManabaseReport   [reduced trials for in-loop]
   │  9. Project ManabaseReport → 7-family delta view (vs current working list, D-07)
   │     — cached by (working-list hash, next-proposed-card) in a dedicated IMemoryCache (D-10)
   ▼
JSON response { proposal, deltas, floorWarnings, cardsRemaining, roundBanner }
   │  10. cut-lab.ts patches proposal card + sticky bar + metrics in place (no page reload)
   ▼
On accept: CutLabState updated (Pool entry removed/flagged cut, decision appended to history) →
   re-serialized (still under 256KB) → hidden field kept in sync for the no-JS form-POST fallback
```

### Recommended Project Structure

```
DeckFlow.Core/Manabase/                         # UNCHANGED — reused verbatim, no new files needed here
DeckFlow.Web/Services/CutLab/
├── CutLabStructuralFindings.cs                  # existing (Phase 102) — read, not modified
├── CutLabRoleAssigner.cs                        # existing (Phase 102) — read, not modified
├── CutLabFloorRules.cs                          # existing (Phase 102) — Evaluate() is the Phase 103 contract
├── CutLabPageService.cs                         # existing — extend BuildState/ProcessAsync to populate the resolved-card cache at intake
├── CutLabCutRoundEngine.cs                      # NEW — pure round-queue + ordering logic (D-01..D-04, D-15)
├── CutLabSimulationService.cs                   # NEW — CardFact resolution + ManabaseClassifier/Analyzer orchestration + metric projection
├── CutLabDeltaCache.cs                          # NEW — dedicated IMemoryCache wrapper, PacketSessionCache-style (D-10)
├── CutLabResolvedCardCache.cs                   # NEW — dedicated IMemoryCache for ScryfallCardData per pool-hash (fills the D-09 gap)
└── CutLabBaselineSnapshot.cs                    # NEW — computes + shapes the compact D-12 snapshot stored in CutLabState
DeckFlow.Web/Controllers/Api/
└── CutLabApiController.cs                       # NEW — POST /api/cut-lab/decide (accept/reject/defer), SameOriginRequestValidator-guarded
DeckFlow.Web/Models/CutLab/
└── CutLabState.cs                               # EXTEND — add Decisions history + BaselineSnapshot fields (keep under 256KB)
DeckFlow.Web/Models/Api/
└── CutLabApi*.cs                                # NEW — request/response DTOs for the decision endpoint
DeckFlow.Web/wwwroot/ts/
└── cut-lab.ts                                   # EXTEND — proposal card fetch/patch, sticky bar, before/after panel
DeckFlow.Web/wwwroot/css/
└── site-common.css                              # EXTEND — sticky bar layout (theme tokens stay in each theme's :root)
```

### Pattern 1: Reuse the `/manabase` resolve→classify→analyze pipeline verbatim

**What:** `ManabaseAnalysisService.LoadDeckAsync` (excerpted below) is the canonical shape: resolve `DeckEntry` → `ScryfallCardData` → `DeckCardEntry` list → `ScryfallCardFactMapper.ToCardFacts` → `ManabaseClassifier.Classify(facts, isSingleton: true, ...)` → `ManabaseAnalyzer.Analyze(deck, mode, ...)`.
**When to use:** Every time Phase 103 needs a `ManabaseReport` for the current working list (baseline computation, proposal deltas, round summaries, before/after view).
**Example:**
```csharp
// Source: DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:783-791 (existing, verified)
IReadOnlyList<CardFact> facts = ScryfallCardFactMapper.ToCardFacts(deckEntries);
ManabaseDeck deck = ManabaseClassifier.Classify(
    facts,
    isSingleton: true,
    rampCreditV2: rampCreditV2,
    landRampSim: landRampSim,
    payLifeUntapped: payLifeUntapped,
    checkLandUntapped: checkLandUntapped,
    restrictedLands: restrictedLands);

// Then, elsewhere (ManabaseAnalysisService.cs:466):
ManabaseReport report = ManabaseAnalyzer.Analyze(deck, mode, /* ...options... */);
```
Cut Lab's `CutLabPageService` already builds `CardFact`-equivalent classification for role assignment (`ScryfallCardFactMapper.ToCardFact` — singular — per entry, `CutLabPageService.cs:479-496`); Phase 103's simulation service should reuse the SAME resolved `ScryfallCardData` objects that call already touches, rather than re-resolving.

### Pattern 2: Deterministic seeding is already built in — do not add a second seed layer

**What:** Every `CastabilitySimulator` entry point seeds from `StableSeed(name)`, an FNV-1a hash of a fixed string — never `Environment.TickCount`, `Guid.NewGuid()`, or a shared mutable `Random`.
**When to use:** Whenever the planner is tempted to thread a "seed" parameter into `CastabilitySimulator.Simulate`/`SimulatePlanPresence`/`SimulateCurveCoverage` for D-08 — don't; the seed is already a pure function of (spell name | fixed sentinel string). Re-running `ManabaseAnalyzer.Analyze` on an unchanged `ManabaseDeck` at the same `trials` count already reproduces byte-identical output.
**Example:**
```csharp
// Source: DeckFlow.Core/Manabase/CastabilitySimulator.cs:2812-2828 (existing, verified)
// Deterministic, stable across runs and platforms (NOT string.GetHashCode, which is randomized
// per-process). FNV-1a over the UTF-16 code units.
private static int StableSeed(string name)
{
    unchecked
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        uint hash = offset;
        foreach (char c in name)
        {
            hash ^= c;
            hash *= prime;
        }
        return (int)hash;
    }
}
```
**What D-08 actually needs new work for:** the noise-floor threshold in the *delta display* layer — deciding, given two already-deterministic numbers (metric before cut vs. metric after cut), when `|after - before|` is small enough to render as "no meaningful change" rather than a jittery-looking single point of movement. This is UI/formatting logic, not a simulator change.

### Pattern 3: Async JSON decision endpoint (D-09)

**What:** Copy `DeckSyncApiController`'s shape exactly: `[ApiController]`, `[Route("api/...")]`, no `[ValidateAntiForgeryToken]` (JSON APIs use `SameOriginRequestValidator.IsValid(Request)` instead, checked as the FIRST line of the action), `[FeatureFlagGate("tool.cut-lab.enabled")]`, structured `{ Message }` error bodies, `[FromBody]` request DTO, domain-exception catch → `BadRequest`.
**When to use:** The new `POST /api/cut-lab/decide` endpoint.
**Example:**
```csharp
// Source: DeckFlow.Web/Controllers/Api/DeckSyncApiController.cs:49-59 (existing, verified pattern to copy)
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
    // ...validate, execute, return Ok(response) or BadRequest(new { Message = ... })
}
```

### Pattern 4: Dedicated `IMemoryCache` instance for the delta/resolved-card caches (D-10, D-12)

**What:** `PacketSessionCache` is the exact precedent: a private `MemoryCache` (not the shared DI singleton) with a `SizeLimit`, SHA-256 field-bag cache keys via `PacketSessionCache.ComputeKey(object)`, `AbsoluteExpirationRelativeToNow` TTL, and post-eviction logging.
**When to use:** Both the D-10 per-(working-list-hash, card) delta cache AND the resolved-`ScryfallCardData` cache this research identifies as necessary for D-09 to hit its latency budget.
**Example:** See the full `PacketSessionCache` source at `DeckFlow.Web/Services/PacketSessionCache.cs:21-125` — register two purpose-built instances (or one service exposing two typed regions) the same way, sized modestly given the 512MB Render cap (a 150-card pool's resolved `ScryfallCardData` is small — well under `PacketSessionCache`'s existing 10MB budget — but still deserves its own cap so it cannot starve the packet cache).

### Anti-Patterns to Avoid

- **Re-implementing castability/mulligan math "just for Cut Lab":** SIM-01 explicitly forbids this. Every metric must trace to a `ManabaseReport` field.
- **Re-fetching Scryfall on every accept/reject:** Breaks the ~1s (D-11) and 512MB budgets, and defeats D-10's "no upfront round-wide sim burst" intent by adding network I/O to every single decision. Cache resolved cards at intake.
- **Storing full `ScryfallCardData` in `CutLabState`:** Blows the existing 256KB `CutLabStateSerializer.MaxUploadBytes` cap for a 150-card pool. Keep `CutLabState` to IDs/names/compact snapshot; keep resolved card data in a server-side cache.
- **Seeding a "new" RNG per decision for reproducibility:** Already solved by `StableSeed`; adding a second seed source would just make results LESS reproducible (two competing seed strategies).
- **Treating "flood/screw/curve risk" as one existing field:** No such single field exists in `ManabaseReport`. It must be assembled (see Open Questions) — do not invent a plausible-sounding property name during planning without checking `ManabaseModels.cs` first.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Commander-on-time probability | A new "cast by turn N" simulator | `CardCastability.EarlyCastPercents` (commander row only) / `CastPercent` at `OnCurveTurn` | `ManabaseAnalyzer.BuildCastability` already tracks this per commander row (`CastabilitySimulator.cs:320-321`, `trackEarlyCast`). |
| Keepable-hand rate | A new London-mulligan simulator | `ManabaseMulliganEvaluation.KeepableHandPercent`/`Kept7Percent`/`MulliganTo6Percent`/`MulliganTo5Percent` | Computed once per `ManabaseAnalyzer.Analyze` call from the same castability rows (`ComputeMulliganEvaluation`, no second sim pass). |
| Mana/color reliability | A new per-color source-adequacy check | `ManabaseReport.ColorFindings` (`ColorSourceFinding` per color: `Deficit`, `AverageCastPercent`, `UnderSupportedCount`) | This is the exact §6 Karsten scoring recipe already validated across many prior cycles (see `ManabaseAnalyzer.cs` doc comments). |
| Early-interaction availability | A new "holdable by turn 3" check | `ManabaseReport.InteractionLens` (`ManabaseInteractionLens.QualifyingCount`/`OnTargetCount`/`Rows[].HoldablePercent`) | cEDH-only lens already built from the same castability rows (`ComputeInteractionLens`); gated behind `interactionLens: true` + `mode == Cedh`. |
| Plan presence | A new "hand has a payoff/engine" sim | `ManabaseReport.MulliganEvaluation.PlanPresence` (`ManabasePlanPresence.PlanPresencePercent`, `RolePercents`, `ShapeExplosivePercent`/`ShapeEnginePercent`/`ShapeBridgePercent`) | Requires spells tagged with `PlanRole` via the Web-layer plan-role classifier (see `TagPlanRolesAsync` in `ManabaseAnalysisService.cs:828+`) — Cut Lab's `CutLabRoleAssigner` already calls `PlanRoleClassifier.Classify` per card, so the tagging inputs already exist. |
| Flood/screw/curve risk | A new variance/flood model | Assemble from: `ManabaseReport.LandDelta` (flood/screw proxy: actual vs. Karsten target), `MulliganEvaluation.MulliganTo5Percent` (screw proxy), Phase 102's `CutLabFindingKind.CurveCongestion` (curve proxy) | **No single existing field is named this** — flagged as an Open Question below; this is the recommended assembly, not a verified 1:1 mapping. |
| Per-goal category-by-turn probability | A new user-turn-goal engine | `CedhMulliganCalibration` turn caps (`TurnCapExplosive=3`, `TurnCapEngine=2`, `RepresentativeLineTurnCap=4`) as FIXED (non-user-editable) stand-ins for Phase 103; `CardCastability.EarlyCastPercents` for the by-turn curve where available | **GOAL-01 (user-defined turn goals) is Phase 104.** Phase 103 cannot offer true "per-goal" turn probabilities without either pulling GOAL-01 forward or scoping this metric family down to a fixed default. Flagged as an Open Question. |
| Floor-break detection on a proposed cut | A new floor-comparison check | `CutLabFloorRules.Evaluate(roleCounts, floors, candidateCutRoles, cardName, quantity)` | Built in Phase 102 *specifically* as the Phase 103 contract (see its XML doc: "Phase 103's cut engine MUST route every proposed cut through Evaluate before presenting it"). |
| Deterministic Monte Carlo seeding | A new seed-injection mechanism | `CastabilitySimulator`'s existing `StableSeed(name)` | Already deterministic; see Pattern 2 above. |

**Key insight:** Every metric in SIM-01 except two maps directly onto an existing `ManabaseReport` field with no new math. The two exceptions are naming/scoping gaps in the roadmap text, not missing engine capability — the underlying signals (`LandDelta`, mulligan-depth percentages, curve congestion, calibrated turn caps) already exist; they just were never assembled into fields called "flood/screw/curve risk" or "per-goal category-by-turn probability."

## Common Pitfalls

### Pitfall 1: Assuming `CutLabState.Pool[].TypeLine` is enough to run a simulation
**What goes wrong:** A plan that tries to call `CastabilitySimulator`/`ManabaseAnalyzer` using only what's already in `CutLabState` will fail — `CardFact` needs `ManaCost`, `OracleText`, `ProducedMana`, etc., none of which are persisted.
**Why it happens:** `CutLabPoolCard` (Phase 101/102) was designed for lock/package/role bookkeeping, not simulation. The full `ScryfallCardData` is resolved every request but discarded after `TypeLine` extraction.
**How to avoid:** Add a resolved-card cache populated at pool intake/lock (see Pattern 4); every simulation call reads from that cache keyed by pool hash, never re-derives from `CutLabState` alone.
**Warning signs:** A plan task that says "read mana cost from `CutLabPoolCard`" — there is no such field.

### Pitfall 2: Running full-trial (`DefaultTrials = 20_000`) simulations per decision action
**What goes wrong:** `ManabaseAnalyzer.Analyze` runs one `CastabilitySimulator.Simulate` call (20,000 trials) per non-land spell in the deck, PLUS binary-search calls at `SourceSearchTrials = 5,000` per color per unique pip/turn/threshold signature, PLUS a `SimulatePlanPresence` pass at `DefaultTrials` when plan roles are tagged. For a ~150-card pool (roughly 90-120 nonland spells), that is potentially 100+ full castability sims per `Analyze()` call. Running this on every single accept/reject/defer click will not hit the ~1s target (D-11) — no iteration-count or cost measurement exists yet in the codebase (no benchmarks found under `DeckFlow.Core.Tests`), so the planner must budget a Wave 0 timing spike rather than assume a number.
**Why it happens:** The engine was built for the `/manabase` page's one-shot "analyze this decklist" flow, not for a tight decision loop.
**How to avoid:** (1) Measure actual wall-clock cost for a ~120-card pool with a Wave 0 spike/benchmark before committing to a trial-count reduction strategy (D-11 explicitly authorizes reducing iterations for in-loop deltas). (2) Cache per-(working-list-hash, card) results (D-10) so re-rendering the SAME proposal never re-simulates. (3) Only compute the metrics that actually need re-running for the specific proposed card change, where the existing `ManabaseAnalyzer` architecture allows partial reruns (e.g., a single `CastabilitySimulator.Simulate` call for one spell's castability row is cheap; a full `Analyze()` re-run recomputes every row).
**Warning signs:** A plan that runs `ManabaseAnalyzer.Analyze` synchronously inside the JSON controller action with the default 20k trial count and no cache check first.

### Pitfall 3: Conflating "obvious cut" (D-01) with card-level structural findings that are actually role-level
**What goes wrong:** `CutLabFindingKind.WeakFloorCase` findings (and `RedundantFinishers`) attach evidence to EVERY card in a role, not to a specific "this card is weak" signal — e.g., if `interaction` is at floor+1, ALL interaction cards get evidence-listed on that single finding. A naive "count findings per card" implementation would double- or triple-count a card that happens to sit in a role near its floor, inflating its finding count without genuine distinguishing signal versus its role-mates.
**Why it happens:** `CutLabStructuralFindings.Compute` returns findings, and each finding carries an `Evidence` list of every qualifying card — it does not pre-aggregate "flagged-count per card". D-01 says "flagged by 2+ Phase-102 structural findings" but a `WeakFloorCase` finding flags an entire role's membership identically.
**How to avoid:** When building the per-card finding count for round population, decide explicitly (and document in the plan) whether a `WeakFloorCase`/`RedundantFinishers` evidence hit should count toward a card's "N findings" tally at all, since by construction every card in that role gets the same count and the finding doesn't discriminate between them. `CurveCongestion`, `StrandedSubtheme`, and `EnablerStarved` DO discriminate at the card level (their evidence lists are drawn from a specific subset, not "every card in a role"). This is exactly research question #6 from the phase brief and needs an explicit CONTEXT-level or plan-level decision — see Open Questions.
**Warning signs:** Round 1 ends up containing every card in a thin role instead of a small, genuinely "obvious" set.

### Pitfall 4: Breaking `SameOriginRequestValidator`'s trust model by skipping the antiforgery/CSRF story on the new endpoint
**What goes wrong:** JSON API endpoints in this codebase use `SameOriginRequestValidator` INSTEAD OF the cookie-based `[ValidateAntiForgeryToken]` used by the form-POST controller — mixing the two (e.g., forgetting the same-origin check because "it's an API controller") reopens a CSRF hole.
**Why it happens:** `[ApiController]` routes don't automatically inherit `CutLabController`'s `[ValidateAntiForgeryToken]`.
**How to avoid:** Copy `DeckSyncApiController`'s pattern exactly — `SameOriginRequestValidator.IsValid(Request)` as literally the first statement in the action body.
**Warning signs:** A new `[ApiController]` action with no same-origin check and no antiforgery token validation.

### Pitfall 5: 256KB `CutLabState` cap silently rejects a legitimate save once decisions/baseline are added
**What goes wrong:** `CutLabStateSerializer.Serialize` throws `InvalidOperationException` above `MaxUploadBytes = 262_144`. Adding a cuts-made history list (D-16: "every accepted cut is individually restorable") plus a baseline snapshot (D-12) to `CutLabState` for a 150-card pool, without bounding the history list, risks silently blowing the cap deep into a long cut session (100+ cut decisions each carrying enough data to support the D-16 restore-any UX).
**Why it happens:** `CutLabPageService.ProcessAsync` already calls `CutLabStateSerializer.Serialize(state)` and returns `Error(exception.Message, ...)` on overflow — this is enforced today for the Phase 101/102 fields alone.
**How to avoid:** Keep the D-16 decision history to compact records (card name/ID + decision + timestamp or ordinal — not full card data or full delta snapshots) and keep D-12's baseline to the "compact numeric snapshot" the decision literally specifies, not a full `ManabaseReport`. Size-budget this explicitly in the plan (e.g., "150 cards × ~40 bytes/decision record ≈ 6KB — safely under cap alongside the existing pool/package/floor data").
**Warning signs:** A plan that stores a full `ManabaseReport` (or its per-card castability rows) inside `CutLabState`.

## Code Examples

### Existing deterministic seed (D-08 baseline)
```csharp
// Source: DeckFlow.Core/Manabase/CastabilitySimulator.cs:313-314
var rng = new Random(StableSeed(spell.Name));
```

### Existing floor-evaluation contract Phase 103 must call before presenting any cut
```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs:95-142 (signature, verified)
public static IReadOnlyList<CutLabFloorWarning> Evaluate(
    IReadOnlyDictionary<string, int> roleCounts,
    IReadOnlyDictionary<string, int> floors,
    IReadOnlyCollection<string> candidateCutRoles,
    string cardName,
    int quantity = 1);
```

### Existing structural-findings shape Phase 103 must aggregate per-card (Pitfall 3)
```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs:34-38 (verified)
public sealed record CutLabFinding(
    CutLabFindingKind Kind,
    string Heading,
    string Lead,
    IReadOnlyList<CutLabFindingEvidence> Evidence);
// Evidence is a flat per-card list; there is no pre-built "findings per card" index today —
// Phase 103 must build one (e.g., Dictionary<string cardName, int findingCount>) by iterating
// Findings[].Evidence, with the WeakFloorCase/RedundantFinishers caveat from Pitfall 3.
```

### Existing dedicated-cache precedent for D-10/D-12
```csharp
// Source: DeckFlow.Web/Services/PacketSessionCache.cs:41-45, 91-115 (verified, abbreviated)
public PacketSessionCache(ILogger<PacketSessionCache>? logger = null)
{
    _logger = logger ?? NullLogger<PacketSessionCache>.Instance;
    _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10_000_000 });
}

public void Set<TResult>(string key, TResult result, int sizeBytes) where TResult : class
{
    var entry = new CachedEntry<TResult>(result, sizeBytes);
    var options = new MemoryCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        Size = sizeBytes,
    };
    _cache.Set(key, entry, options);
}
```

## State of the Art

| Old Approach (pre-Phase-103) | Current Approach (Phase 103) | When Changed | Impact |
|--------------------------------|-------------------------------|---------------|--------|
| Cut Lab renders structural findings once per full-page POST, no simulation | Cut Lab recalculates 7-family metrics after every accepted/deferred/rejected cut via reused manabase engines | This phase | New async endpoint + caching layer needed; existing full-page flow stays as fallback. |
| `ManabaseAnalyzer.Analyze` called once per `/manabase` page load (single decklist) | Same call, invoked repeatedly against a shrinking working list, requiring resolved-card caching to stay fast | This phase | First place in the codebase this pipeline is called in a tight interactive loop rather than a one-shot page render — no precedent to copy for the caching layer; it must be built new (though `PacketSessionCache` is a strong structural precedent). |

**Deprecated/outdated:** None — no existing Cut Lab or manabase code is being replaced; this phase is additive orchestration only.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | "Flood/screw/curve risk" should be assembled from `ManabaseReport.LandDelta` + `MulliganEvaluation.MulliganTo5Percent` + Phase 102's `CurveCongestion` finding, rather than requiring new engine output. | Don't Hand-Roll, Open Questions | If the user/planner wants a genuinely new composite "risk score," this assembly may read as three disconnected numbers instead of one coherent metric — needs explicit sign-off, not silent invention during planning. |
| A2 | "Per-goal category-by-turn probability" should use `CedhMulliganCalibration`'s fixed turn caps (3/2/4) as Phase 103 defaults, deferring true user-defined goals to Phase 104 (GOAL-01). | Don't Hand-Roll, Open Questions | If the roadmap intends Phase 103 to expose editable per-goal turns despite GOAL-01 being scoped to Phase 104, this assumption under-scopes the phase and the planner will build the wrong UI. |
| A3 | A ~120-card pool's full `ManabaseAnalyzer.Analyze()` call will NOT complete inside the ~1s D-11 budget at `DefaultTrials=20,000`/`SourceSearchTrials=5,000` without either trial-count reduction or per-card scoped reruns — no timing benchmark exists in the repo to confirm the actual wall-clock cost. | Common Pitfalls (Pitfall 2) | If the real cost turns out to be well under 1s already (e.g., trials are cheap integer-array simulations, not allocation-heavy), the planner may over-engineer a reduced-trial/partial-rerun strategy that isn't needed. A Wave 0 timing spike is the correct way to resolve this, not more research-time guessing. |
| A4 | `CutLabFindingKind.WeakFloorCase` and `RedundantFinishers` evidence should NOT count toward a card's D-01 "flagged by 2+ findings" tally on their own (or should count differently from `CurveCongestion`/`StrandedSubtheme`/`EnablerStarved`), because they flag entire role memberships uniformly rather than discriminating cards. | Common Pitfalls (Pitfall 3) | If the planner treats all 5 finding kinds identically for the D-01 tally, Round 1 ("obvious cuts") could end up containing every card in a thin role rather than a small, genuinely obvious set — directly undermining the "obvious cut" UX promise. |

## Open Questions

1. **Does "per-goal category-by-turn probability" mean user-defined goals (Phase 104's GOAL-01) or a fixed default set of category/turn pairs for Phase 103?**
   - What we know: ROADMAP.md Phase 103 success criteria literally include "per-goal category-by-turn probability" in its SIM-01 recalculation list, but GOAL-01 ("User can define turn-based goals") is explicitly scoped to Phase 104, which "depends on Phase 103 (reuses its simulation/metrics engine)."
   - What's unclear: Whether Phase 103 needs to build the category-by-turn machinery generically now (with Phase 104 only adding the UI to let users pick goals), or whether Phase 103 should use a small fixed set of categories/turns (e.g., ramp by turn 2, interaction by turn 2, commander by turn 3, engine+payoff by turn 6 — mirroring `CedhMulliganCalibration`'s constants) as a non-editable preview.
   - Recommendation: Plan for the FIXED-default interpretation (A2 above) since it reuses `CedhMulliganCalibration`'s existing constants and keeps "no new simulation math" true; flag this explicitly to the user/planner as a scope decision rather than resolving it silently, since it changes what "recalculates after every cut" concretely renders.

2. **What exactly composes "flood/screw/curve risk" as a single delta-displayable metric?**
   - What we know: No `ManabaseReport` field is named this. Candidate components exist: `LandDelta` (flood if positive/large, screw risk if negative), `MulliganEvaluation.MulliganTo5Percent` (mulligan-to-5 rate as a screw proxy), Phase 102's `CutLabFindingKind.CurveCongestion` (curve proxy, pool-level not working-list-delta-level today).
   - What's unclear: Whether these three should be shown as three separate changed-only lines (consistent with D-05's "changed-only compact deltas" — this may actually be the natural answer, since D-05 already expects multiple metric lines, not one composite number) or combined into one derived score.
   - Recommendation: Treat as three separate lines within the existing "7-family" delta table rather than inventing a composite score — this requires zero new math and fits D-05's display model directly.

3. **What is the actual per-decision wall-clock cost of `ManabaseAnalyzer.Analyze` at default trial counts for a ~100-150 card pool?**
   - What we know: `DefaultTrials = 20_000` per spell's `CastabilitySimulator.Simulate` call; `SourceSearchTrials = 5_000` per color per unique (color, pips, turn, threshold) signature during the binary search for `RequiredSources`; `SimulatePlanPresence` runs one more full-trial pass when plan roles are tagged. No benchmark/timing test exists in `DeckFlow.Core.Tests` to cite a real number.
   - What's unclear: Whether this comfortably clears D-11's ~1s target/3s hard cap as-is, or needs the "reduce iterations for in-loop deltas" fallback D-11 explicitly authorizes.
   - Recommendation: Wave 0 timing spike — call `ManabaseAnalyzer.Analyze` against a realistic ~130-card Cut Lab pool fixture and record milliseconds before committing to a specific reduced-trial-count design. Do not guess a trial count in the plan without this measurement.

4. **Where does the resolved-card cache live in the DI/lifetime model, and what invalidates it?**
   - What we know: `CutLabPageService` is registered `Scoped` (`Program.cs:181`); Cut Lab intake happens via full-page POST today with no session/user identity concept beyond the hidden `CutLabStateJson` field round-trip (no auth, no server session store observed for Cut Lab).
   - What's unclear: What key uniquely and safely identifies "this browser's current Cut Lab working session" for a server-side `IMemoryCache` entry, given there's no login/session ID today — likely a hash of the pool's card-name+quantity set (deterministic, matches D-10's "working-list hash" language) rather than a server session ID.
   - Recommendation: Use a deterministic hash of the (sorted) pool card list as the cache key (mirroring `PacketSessionCache.ComputeKey`), scoped to a TTL long enough to survive a normal cut session (tens of minutes) but short enough not to accumulate unboundedly across many users/pools under the 512MB cap.

## Environment Availability

No new external tools, services, or runtimes are introduced by this phase — it is pure C#/TypeScript orchestration over already-running project infrastructure (Scryfall REST calls already flow through the existing `IScryfallCardResolver` + `ScryfallThrottle` + Polly pipeline). Skipped per the "no external dependencies" condition.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework (C#) | xUnit 2.9.3 (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`) |
| Framework (TS unit) | Vitest ^3.2.7 (`DeckFlow.Web/ts-tests/*.test.ts`) |
| Framework (e2e) | Playwright ^1.60.0 (`DeckFlow.Web/e2e/*.spec.ts`) |
| Config file | `DeckFlow.Web/vitest.config.ts`, `DeckFlow.sln`, `DeckFlow.Web/playwright.config.ts` (not yet inspected but referenced by existing `e2e/cut-lab-*.spec.ts`) |
| Quick run command (C# targeted) | `dotnet build DeckFlow.sln` (clean build gate per CLAUDE.md — VSTest is unreliable under WSL) plus a targeted test-class run via CI, not local VSTest |
| Quick run command (TS) | `npm run test` (Vitest, from `DeckFlow.Web/`) |
| Full suite command | `dotnet build` clean (all projects) + `npm run test` (Vitest) + `npm run e2e` (Playwright) — per CLAUDE.md, prefer push-and-watch CI for the full xUnit run given WSL VSTest instability |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| CUT-01 | Rounds execute in fixed order obvious → structural → preference, auto-advancing (D-03) | unit | new `CutLabCutRoundEngineTests.cs` | ❌ Wave 0 |
| CUT-02 | Every proposal shows deltas; never labels a card objectively worse (copy-level assertion) | unit | new `CutLabSimulationServiceTests.cs` (delta projection) + a Razor/view-model text-assertion test | ❌ Wave 0 |
| CUT-03 | Accept/reject/defer + running cards-remaining-to-100 count | unit + controller | new `CutLabApiControllerTests.cs` (mirrors `CutLabControllerTests.cs` pattern) | ❌ Wave 0 |
| SIM-01 | Metrics recalc reuses existing engines, no new sim math | unit | `CutLabSimulationServiceTests.cs` asserting delegation to `ManabaseAnalyzer.Analyze`/`CastabilitySimulator` outputs (not reimplemented math) | ❌ Wave 0 |
| SIM-02 | Before/after baseline comparison available at any point | unit + e2e | new `CutLabBaselineSnapshotTests.cs` + extend `e2e/cut-lab-structure.spec.ts` | ❌ Wave 0 (unit), existing file to extend (e2e) |

### Sampling Rate
- **Per task commit:** `dotnet build DeckFlow.sln` (clean, no new warnings) + targeted xUnit test class for the file just touched + `npm run test` if TS touched.
- **Per wave merge:** Full `dotnet build` + full Vitest run + full Playwright `npm run e2e` (per CLAUDE.md's "Run UI tests before merge" and "Web-page change → tests+themes+mobile" standing rules — this phase touches a UI page).
- **Phase gate:** Full suite green before `/gsd:verify-work`, plus a manual timing check against Open Question 3 (D-11 budget) since no automated perf assertion exists yet in this codebase's test style.

### Wave 0 Gaps
- [ ] `DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs` — round queue construction/ordering/loop-around (CUT-01, D-01..D-04, D-15)
- [ ] `DeckFlow.Web.Tests/CutLabSimulationServiceTests.cs` — CardFact resolution from cache, `ManabaseAnalyzer` delegation, 7-family projection (SIM-01)
- [ ] `DeckFlow.Web.Tests/CutLabApiControllerTests.cs` — same-origin guard, floor-warning wiring, accept/reject/defer state transitions (CUT-03, D-09)
- [ ] `DeckFlow.Web.Tests/CutLabBaselineSnapshotTests.cs` — baseline computed once at lock, survives `CutLabState` round-trip under 256KB (SIM-02, D-12)
- [ ] `DeckFlow.Web/ts-tests/cut-lab-proposal.test.ts` (new) — proposal card patch logic, sticky-bar counter update (D-09, D-14)
- [ ] A one-off timing spike (can live in a throwaway test or a `[Fact(Skip=...)]` benchmark stub) resolving Open Question 3 before the plan commits to a specific trial-count reduction

## Project Constraints (from CLAUDE.md)

- **Tech stack pinned:** ASP.NET 10 + Razor; no framework migration. This phase is server-orchestration + TS hydration only — consistent.
- **Hosting 512MB cap:** Every new cache (resolved-card cache, delta cache) must be explicitly size-bounded (`MemoryCacheOptions.SizeLimit`), matching `PacketSessionCache`'s pattern — do not use unbounded caches.
- **Theme system:** Any new CSS (sticky bar, proposal card, before/after panel) must put LAYOUT rules in `site-common.css`, never `site.css`; new tokens go in each theme file's `:root`.
- **HTTP resilience:** If the resolved-card cache needs a fresh Scryfall fetch (cache miss / TTL expiry), it MUST go through the existing `IScryfallCardResolver` + RestSharp + Polly v8 named-pipeline pattern — never a raw `HttpClient` call.
- **Public repo — no secrets:** Not directly implicated by this phase (no new external credentials).
- **Testing:** VSTest unreliable in WSL; rely on `dotnet build` clean + targeted class runs + CI for the full xUnit suite. UI testing must never open a browser on the Windows host — use `scripts/run-web-test.sh`/`.ps1` (`DECKFLOW_DISABLE_AUTO_BROWSER=true`) and `npx --no-install playwright test`.
- **Commits:** Plain default-author commits, no Co-Authored-By trailer, commit per logical change, README updated when behavior changes.
- **Formatting:** `.editorconfig` changed-lines gate; do not touch unrelated lines in existing files like `ManabaseAnalyzer.cs`/`CastabilitySimulator.cs` even when reading them for reuse — this phase should not need to MODIFY those files at all (pure consumer), which keeps this constraint moot for Core but still applies to any Web-layer edits.
- **Feature flag:** `tool.cut-lab.enabled` (currently OFF in prod) must gate the new `CutLabApiController` route exactly as it gates `CutLabController`.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CUT-01 | User works through cut rounds in a fixed order: obvious cuts → structural choices → preference calls | Round-queue construction from `CutLabStructuralFindings`/`CutLabRoleAssigner` output (see Architecture Patterns, Pitfall 3, Open Question re: finding-tally semantics per D-01/D-04). |
| CUT-02 | Every proposed cut shows its measurable consequences (tradeoff deltas); tool never labels a card objectively worse | `CutLabSimulationService` metric projection against `ManabaseReport` fields (see Don't Hand-Roll table); UI copy neutrality is a Web-layer/view concern, not a research finding, but D-06's directional-color decision is already locked in CONTEXT.md. |
| CUT-03 | User can accept, reject, or defer each proposed cut individually, with a running cards-remaining-to-100 count always visible | New `POST /api/cut-lab/decide` endpoint (Pattern 3) + `CutLabState` decision-history extension (Pitfall 5 sizing guard). |
| SIM-01 | After every accepted cut or swap, the working list's metrics recalculate across 7 families, reusing existing engines | Direct mapping documented in Don't Hand-Roll; two families ("flood/screw/curve risk", "per-goal category-by-turn") require explicit scope decisions (Open Questions 1-2) before implementation. |
| SIM-02 | User can view a before/after comparison between the original pool baseline and the current working list | D-12's baseline snapshot computed once via the same `ManabaseAnalyzer.Analyze` pipeline at pool-lock time, stored compactly in `CutLabState` (Pitfall 5). |
</phase_requirements>

## Sources

### Primary (HIGH confidence — read directly from repository source)
- `DeckFlow.Core/Manabase/CastabilitySimulator.cs` (2830 lines) — `StableSeed`, `DefaultTrials`, `SourceSearchTrials`, `Simulate`, `SimulatePlanPresence`, `SimulateCurveCoverage` entry points.
- `DeckFlow.Core/Manabase/ManabaseAnalyzer.cs` (1873 lines, partially read) — `Analyze` orchestration, `ComputeMulliganEvaluation`, `ComputeTapAnalysis`, `ComputeInteractionLens`.
- `DeckFlow.Core/Manabase/ManabaseModels.cs` — `ManabaseReport`, `CardCastability`, `ManabaseMulliganEvaluation`, `ManabaseInteractionLens`, `ManabasePlanPresence` field shapes.
- `DeckFlow.Core/Manabase/CardFact.cs`, `ScryfallCardFactMapper.cs`, `ManabaseClassifier.cs` — input-shaping pipeline.
- `DeckFlow.Core/Manabase/CedhMulliganCalibration.cs` — fixed turn-cap constants.
- `DeckFlow.Web/Services/CutLab/CutLabStructuralFindings.cs`, `CutLabRoleAssigner.cs`, `CutLabFloorRules.cs`, `CutLabPageService.cs`, `CutLabPoolValidator.cs` — Phase 101/102 reuse surface.
- `DeckFlow.Web/Models/CutLab/CutLabState.cs`, `DeckFlow.Web/Services/CutLab/CutLabStateSerializer.cs` — state envelope + 256KB cap.
- `DeckFlow.Web/Controllers/CutLabController.cs`, `DeckFlow.Web/Models/CutLabRequest.cs`, `DeckFlow.Web/wwwroot/ts/cut-lab.ts` — existing page flow.
- `DeckFlow.Web/Controllers/Api/DeckSyncApiController.cs`, `DeckFlow.Web/Security/SameOriginRequestValidator.cs` — D-09's mandated pattern.
- `DeckFlow.Web/Services/PacketSessionCache.cs` — D-10/D-12 cache precedent.
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — canonical resolve→classify→analyze pipeline (lines 1-100, 700-830 read).
- `DeckFlow.Web/Program.cs` — DI registrations (`AddMemoryCache`, `CutLabPageService` scoped lifetime).
- `DeckFlow.Web.Tests/CutLabPageServiceTests.cs` (fakes list) — existing test-double conventions for Cut Lab dependencies.
- `.planning/workstreams/cut-lab/phases/103-simulation-engine-guided-cut-rounds/103-CONTEXT.md`, `REQUIREMENTS.md`, `ROADMAP.md`, `research/2026-07-18-commander-feature-priorities.md` — requirement/decision source of truth.
- `DeckFlow.Web/package.json`, `DeckFlow.Web/vitest.config.ts`, existing `e2e/cut-lab-*.spec.ts`, `ts-tests/cut-lab-lock-interactions.test.ts` — test infra confirmation.
- `.planning/config.json` — confirms `workflow.nyquist_validation: true` (Validation Architecture section required).

### Secondary (MEDIUM confidence)
None used — all findings for this phase were verifiable directly against repository source; no WebSearch/Context7 lookups were needed since the phase is 100% internal-reuse-based.

### Tertiary (LOW confidence)
None.

## Metadata

**Confidence breakdown:**
- Standard stack (reused engines/patterns): HIGH — every claim is a direct source-code citation with line numbers.
- Architecture (pipeline reuse shape): HIGH — `ManabaseAnalysisService`'s existing resolve→classify→analyze flow is a proven, already-shipped pattern.
- Metric-family mapping (SIM-01's 7 families): MEDIUM — 5 of 7 map cleanly; 2 ("flood/screw/curve risk", "per-goal category-by-turn probability") require an explicit scope decision flagged in Open Questions, not a verified existing field.
- Performance/latency (D-11): LOW-MEDIUM — no benchmark exists in-repo; flagged as a required Wave 0 timing spike rather than guessed.
- Pitfalls: MEDIUM-HIGH — derived directly from reading the actual data shapes (`CutLabState`, `CutLabStructuralFindings`, `CutLabStateSerializer`), not speculation.

**Research date:** 2026-07-19
**Valid until:** 2026-08-18 (30 days — internal codebase reuse research; invalidated sooner only if `DeckFlow.Core/Manabase/*` or Cut Lab Phase 101/102 code changes before Phase 103 planning begins).
