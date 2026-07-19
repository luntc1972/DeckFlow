# Phase 102: Structural Analysis & Role Floors - Research

**Researched:** 2026-07-19
**Domain:** Brownfield ASP.NET MVC — Cut Lab structural composition view (slot competition, structural findings, configurable role floors) built entirely on existing DeckFlow role/category inference
**Confidence:** HIGH (all findings grounded in direct codebase reads; zero external dependencies)

## Summary

Phase 102 turns the Phase 101 intake (parsed pool + declared intent + locks) into a structural read of the pool: cards grouped by the functional slot they compete for (SLOT-01), evidence-backed structural findings (SLOT-02), and bracket/plan-derived role floors the user can adjust and that later cut suggestions must never silently break (FLOOR-01/02). The success criteria pin the hard constraint: **"using existing role/category inference, with no new classification model."** The codebase fully supports this — the classification pipeline Cut Lab needs is exactly the one `ManabaseAnalysisService.TagPlanRolesAsync` already runs (batched `ICategoryKnowledgeStore.GetCategoriesForNamesAsync` crowd tags → `ICommanderSpellbookService.FindCombosAsync` combo pieces → `PlanRoleClassifier.Classify` oracle-text heuristic, first-hit-wins, all fail-open), plus the pure `DeckStatClassifier` statics (`IsRampCard`, `IsDrawCard`, `IsInteractionCard`, `IsProtectionCard`, `IsClosingPowerCard`, ...) that `DeckStatAggregator` uses for the analysis-prompt tallies.

The one taxonomy fact the planner must internalize: **`PlanRole` (Payoff/Engine/TutorCombo/Interaction) deliberately EXCLUDES ramp, lands, and filler draw** ("that is resource/velocity, a different axis" — `ManabaseModels.cs:156-163`). FLOOR-01's eight roles (lands, ramp, draw, interaction, protection, engines, payoffs, win conditions) are therefore WIDER than `PlanRole` — Cut Lab must compose a role-assignment layer from BOTH sources: `PlanRole` flags for engines/payoffs/interaction, `DeckStatClassifier.IsRampCard`/`IsDrawCard`/`IsProtectionCard` for ramp/draw/protection, `CutLabLockRules.IsLand` for lands, and `IsClosingPowerCard` + Spellbook combos for win conditions. That composition is a new *mapping table*, not a new *classification model* — every underlying signal already exists.

For floor defaults, two existing bracket-derived sources cover lands/ramp/draw: `IManabaseBaselineProvider.TryGetBracketBaseline(bracket)` (per-bracket community avg lands, brackets 2-5) and `ManabaseRampDrawBudgetCalculator` (24-slot ramp/draw split keyed off commander mana value). The remaining five roles (interaction, protection, engines, payoffs, win conditions) have NO existing bracket-derived targets anywhere in the codebase — their defaults are a new small static table whose numbers are a product decision (low-stakes, since FLOOR-02 makes every floor user-adjustable). The FLOOR-02 "no silent floor break" guarantee is a *contract for Phase 103*: Phase 102 must persist floors in the `CutLabState` envelope and ship a pure floor-evaluation rule (`role counts − proposed cut → which floors break`) that Phase 103's cut rounds are required to call.

**Primary recommendation:** Add a structural-analysis stage to `CutLabPageService.ProcessAsync` after commander resolution: map the already-resolved `ScryfallCardData` to `CardFact` via `ScryfallCardFactMapper`, run the Manabase-mirrored classification I/O (batched categories + Spellbook, both fail-open), assign each card to the eight-role taxonomy via a new pure `CutLabRoleAssigner` composed from existing classifiers, compute findings via a new pure `CutLabStructuralFindings`, derive floor defaults via a new `CutLabFloorDefaults` (bracket baseline + ramp/draw budget + static table), and persist ONLY user-adjusted floors (never derived role data) as an additive `RoleFloors` extension of `CutLabState`. Render as new sections of `CutLab.cshtml` + `cut-lab.ts`, gated behind the existing `tool.cut-lab.enabled` flag (still seeded OFF).

## Project Constraints (from CLAUDE.md)

- **Tech stack pinned:** ASP.NET 10 + Razor; no framework migration. TS sources in `DeckFlow.Web/wwwroot/ts/`, compiled `wwwroot/js/*.js` is gitignored — never commit compiled JS.
- **Theme CSS rules:** layout CSS goes in `site-common.css`, NOT `site.css`; new tokens go in `:root` of EACH theme file (guild themes are full standalone forks).
- **HTTP resilience:** RestSharp + direct Polly v8 named pipelines; do NOT migrate to the standard resilience handler; never `new HttpClient()`; all Scryfall calls behind `ScryfallThrottle`.
- **Hosting:** Render 512 MB web tier — mind allocations (150-card pool × per-POST classification is well within the Manabase 500-card precedent).
- **Public repo:** no secrets in commits ever.
- **Testing:** VSTest unreliable in WSL; `dotnet build` clean is the baseline; UI testing must NEVER open a browser on the Windows host — use `scripts/run-web-test.sh` (`DECKFLOW_DISABLE_AUTO_BROWSER=true`) + `npx --no-install playwright test`.
- **Formatting:** `.editorconfig` changed-lines gate; five carve-outs — never convert `{ get; init; }` to `{ get; }` (System.Text.Json skips get-only props — directly relevant to the `CutLabState` extension), never inline attributes, never re-indent raw-string literals, preserve switch expressions, preserve LF endings.
- **Commits:** plain default-author (luntc1972), no Co-Authored-By trailer; one logical change per commit; README updated when behavior changes (Cut Lab still flag-OFF, so README impact is minimal until launch).
- **No new dependencies** without explicit user approval — this phase needs none.
- **GSD workflow enforcement:** work flows through GSD commands; Codex writes implementation code, Claude plans/reviews.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SLOT-01 | Pool cards grouped by functional slot competition using existing role/category inference | Eight-role taxonomy composed from `PlanRoleClassifier` + `DeckStatClassifier` + `CutLabLockRules.IsLand` (see "Role taxonomy map"); classification I/O mirrors `ManabaseAnalysisService.TagPlanRolesAsync` (batched categories + Spellbook + heuristic fallback, fail-open) |
| SLOT-02 | Structural findings with evidence: curve congestion, stranded subthemes, redundant finishers, weak floor cases, enabler-starved cards | Per-finding signal map (see "Structural findings signal map"): MV buckets (`DeckStatAggregator` curve precedent), crowd-category clusters, `IsClosingPowerCard`/`WinConMap`, floor-vs-count comparison, `SpellbookAlmostCombo` near-combos |
| FLOOR-01 | Default role floors (8 roles) derived from declared bracket and plan | Lands: `IManabaseBaselineProvider.TryGetBracketBaseline` (+ optional `ICedhLandBaselineProvider` commander range); ramp/draw: `ManabaseRampDrawBudgetCalculator` 24-slot split; other 5 roles: new static bracket-banded default table (product numbers, user-adjustable) |
| FLOOR-02 | Adjustable floors; no later cut suggestion silently breaks a floor — always an explicit warning | Persist floors in `CutLabState` (additive JSON extension, serializer defaults precedent `SpellRequirement.PlanRoles`); new pure `CutLabFloorRules` evaluation contract consumed by Phase 103; server-side clamping mirrors `EnforceCommanderLock` tamper defense |
</phase_requirements>

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Role/category classification I/O (crowd tags, Spellbook) | Backend service (`CutLabPageService` stage) | — | Mirrors `TagPlanRolesAsync`: Web layer does I/O, passes pure data down (`PlanRole` doc: "keeping Core I/O-free") |
| Eight-role assignment (slot grouping) | Pure Web-service statics (new `CutLabRoleAssigner`) | — | Pure composition of existing classifiers over `CardFact` + categories + combo set; unit-testable without HTTP |
| Structural findings detection | Pure Web-service statics (new `CutLabStructuralFindings`) | — | Deterministic rules over role assignments + MV + categories + near-combos; "defensible comparison rules" is the milestone's stated new effort |
| Floor defaults derivation | Backend service (bracket baseline provider + budget calculator + static table) | — | `IManabaseBaselineProvider` is a Web-layer singleton; `ManabaseRampDrawBudgetCalculator` is Core-pure |
| Floor persistence + adjustment | Backend (state envelope round-trip) | Browser (floor editor inputs, re-serialize on submit) | Same `CutLabStateJson` hidden-field mechanism as locks; floors are user data, role assignments are derived data (recomputed per POST, never persisted) |
| Floor-break warning contract | Pure Web-service statics (new `CutLabFloorRules`) | — | Phase 103 must call it before proposing any cut; shipping it here with tests makes "no silent break" enforceable |
| Slot-group / findings / floors UI | Browser (Razor sections + `cut-lab.ts` extensions) | — | Extends the existing Cut Lab page; layout CSS → `site-common.css`, tokens → each theme `:root` |

## Standard Stack

No new external packages — this phase is 100% internal reuse plus new pure rule code.

### Core (existing, reused)
| Component | Location | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| `PlanRoleClassifier.Classify(fact, categories, isComboPiece, mode, out preGate)` | `DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs:43` | Payoff/Engine/TutorCombo/Interaction flags; categories → combo → heuristic, first-hit-wins; permanent gate; pre-gate interaction signal | THE existing role inference SLOT-01 names; pure static, caller supplies I/O |
| `DeckStatClassifier` statics | `DeckFlow.Core/Analysis/DeckStatClassifier.cs` | `IsRampCard`, `IsDrawCard`, `IsInteractionCard`, `IsBoardWipeCard`, `IsTargetedRemovalCard`, `IsProtectionCard` (+`StaxProtectionCatalog`), `IsClosingPowerCard`, `IsTutorCard`, `IsCounterspellCard`, `IsFastManaCard` | The oracle-text role signals for the floor roles `PlanRole` deliberately excludes (ramp/draw) plus protection and closing power |
| `CutLabLockRules.IsLand(typeLine)` | `DeckFlow.Web/Services/CutLab/CutLabLockRules.cs:123` | MDFC-aware front-face land check | Already shipped in Phase 101 for bulk land lock; the lands role uses it verbatim |
| `ICategoryKnowledgeStore.GetCategoriesForNamesAsync(names, ct)` | `DeckFlow.Web/Services/Persistence/ICategoryKnowledgeStore.cs` (registered singleton, `Program.cs:172`) | ONE batched crowd-category lookup for the whole pool | The exact call `TagPlanRolesAsync` uses; per-card loops previously exhausted the request timeout (~65 sequential queries ≈ 20 s — comment at `ManabaseAnalysisService.cs:870-873`) |
| `ICommanderSpellbookService.FindCombosAsync(entries, ct)` | `DeckFlow.Web/Services/CommanderSpellbookService.cs:48` | `IncludedCombos` (combo-piece names) + `AlmostIncludedCombos` (`MissingCard`, `CardsInDeck`) | Combo-piece source for TutorCombo/win-condition evidence AND the enabler-starved near-combo signal; memory-cached per deck (`cacheKey = "spellbook:..."`, line 109-145); returns null on API failure (fail-open) |
| `ScryfallCardFactMapper.ToCardFact(card, quantity, isCommander)` | `DeckFlow.Core/Manabase/ScryfallCardFactMapper.cs:16` | `ScryfallCardData` → `CardFact` (oracle text, MV, faces, produced mana) | Cut Lab already resolves `ScryfallCardData` per POST (`CutLabPageService.ResolveCardsAsync`); this mapper is the bridge to `PlanRoleClassifier`'s `CardFact` input — do not hand-map |
| `IManabaseBaselineProvider.TryGetBracketBaseline(bracket)` | `DeckFlow.Web/Services/Manabase/ManabaseBaselineProvider.cs` (singleton, warm-loaded at startup, `Program.cs:95,310`) | Per-bracket community avg land count (brackets 2-5; fail-open null) | The shipped "bracket-graded land target" — the lands floor default source FLOOR-01 asks for |
| `ManabaseRampDrawBudgetCalculator` | `DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs:66` | 24-slot ramp/draw split: `targetRamp = f(threshold MV) ∈ [8,14]`, `targetDraw = 24 − targetRamp` | The only bracket/plan-adjacent ramp/draw target in the codebase; threshold = commander mana value (`DetermineThreshold`) — Cut Lab knows the commander. NOTE: `CalculateTargetRamp(threshold)` is `internal`; see Pitfall 4 |
| `ICedhLandBaselineProvider.TryGetBaseline(commanderNames, out mean, out n, out sd, ...)` | `DeckFlow.Web/Services/Manabase/CedhLandBaselineProvider.cs:66` (singleton) | Commander-keyed cEDH meta land mean/range | Optional refinement of the lands floor when bracket = 5 / play experience = cEDH (Manabase precedent: meta range supersedes the community line, `ManabaseAnalysisService.cs:594-601`) |
| `DeckStatAggregator` / `DeckStatSummary` | `DeckFlow.Core/Analysis/DeckStatAggregator.cs` | Curve buckets ("0-1","2","3","4","5+"), lands/ramp/draw/interaction/wipes/closing-power/tutor counts | Ready-made evidence tallies; its curve-bucket convention is the curve-congestion display precedent |
| `WinConMapAggregator` / `WinConMap` | `DeckFlow.Core/Analysis/WinConMapAggregator.cs:23` | Ranked combos + near-combos + closing-power cards + win-con band | Optional: the assembled win-condition read for redundant-finisher evidence (deterministic ranking already solved) |
| `CutLabState` / `CutLabStateSerializer` / `CutLabLockRules` / `CutLabPageService` | `DeckFlow.Web/Models/CutLab/`, `Services/CutLab/` | Phase 101 working-session envelope (1 MB cap, commander re-lock on deserialize), page orchestration | The intake this phase extends; `CutLabPoolCard` currently carries Name/Quantity/TypeLine/IsCommander/IsLocked/PackageId only |

### Supporting
| Component | Location | Purpose | When to Use |
|-----------|---------|---------|-------------|
| `ManabaseMode` (Casual/Focused/Cedh) | `DeckFlow.Core/Manabase/ManabaseMode.cs` | Classifier mode gate (pure counterspell = Interaction only in Cedh) | Map Cut Lab's `PlayExperience` string ("Casual"/"Focused"/"cEDH", `CutLab.cshtml:86-91`) → mode; fallback mapping bracket→mode exists at `ManabaseAnalysisService.ResolveBaseline` (Cedh→5, Focused→3, else 2 — invert for the reverse direction) |
| `FakeCategoryKnowledgeStore`, `CommanderSpellbookService` internal test ctor | `DeckFlow.Web.Tests` | Existing test doubles for the classification I/O | Page-service tests; Manabase tests already exercise this exact seam |
| `manabase-pill` / `manabase-segmented` CSS | `site-common.css` (used by `CutLab.cshtml:62-99`) | Segmented pill controls | Floor stepper/editor styling precedent — Cut Lab already borrows these classes |
| `[FeatureFlagGate("tool.cut-lab.enabled")]` | `CutLabController` | Whole-tool gate, seeded OFF both dialects | No new flag needed — the tool has never launched; Phase 102 ships dark behind the same flag |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Composing `PlanRoleClassifier` + `DeckStatClassifier` per role | Running full `ManabaseAnalysisService` and reading its `ManabaseDeck` | The full pipeline computes castability/color math Cut Lab doesn't need, is built around `ManabaseRequest`, and hides ramp/draw counts inside `ManabaseClassifier` internals; direct composition of the pure statics is smaller, testable, and honors "no new classification model" by reusing the same primitives |
| `ICategoryKnowledgeStore` batch API for categories | `ICategorySuggestionService` (cached/reference/tagger/all modes) | `CategorySuggestionService` is a UI-facing suggestion tool (per-request modes, tagger scraping I/O); `TagPlanRolesAsync` deliberately uses the store's batch API — one DB query, fail-open. Follow Manabase, not suggest-categories |
| New static floor-default table for the 5 uncovered roles | Deriving from crawled category-knowledge percentages (`RolePercents` exists at `ManabaseModels.cs:1631`) | Crawl-derived numbers are casual-population-dominated and commander-specific; a transparent bracket-banded static table is defensible, explainable in the UI, and user-adjustable anyway. (Memory precedent: "casual-avg = wrong yardstick" killed a prior type-mix feature) |
| Persisting floors in `CutLabStateJson` | New DB draft table | DB persistence is explicitly deferred to Phase 104 (GOAL-02 saved scenarios) per the resolved Phase 101 open question; floors are a few dozen bytes — trivially inside the 1 MB serializer cap |

**Installation:** None — no new packages.

**Version verification:** Not applicable (no new dependencies). Existing stack confirmed: .NET 10, ASP.NET Core MVC 10.0, RestSharp 114.0.0, Polly 8.x (root `CLAUDE.md` + csproj).

## Package Legitimacy Audit

Not applicable — this phase introduces zero new npm/NuGet packages. All functionality is built on existing in-repo services.

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
Browser (Cut Lab page — Phase 101 intake + NEW Phase 102 sections)
  |
  |  POST /cut-lab  (DeckInputSource/DeckUrl/DeckText + intent + CutLabStateJson
  |                  now also carrying RoleFloors edits)
  v
CutLabController  --[FeatureFlagGate tool.cut-lab.enabled]-->  CutLabPageService.ProcessAsync
  |
  |-- (Phase 101, unchanged) load -> validate 101-150 -> Scryfall batch resolve
  |     -> commander resolve -> banlist -> deserialize prior state -> EnforceCommanderLock
  |
  |-- NEW A. CardFact projection: ScryfallCardData -> ScryfallCardFactMapper.ToCardFact
  |     (extend ResolvedCutLabEntry to carry the resolved ScryfallCardData, per-POST only)
  |
  |-- NEW B. Classification I/O (mirror TagPlanRolesAsync, ManabaseAnalysisService.cs:828-901):
  |     b1. ICommanderSpellbookService.FindCombosAsync(analyzedEntries)   [fail-open null]
  |     b2. ICategoryKnowledgeStore.GetCategoriesForNamesAsync(poolNames) [ONE batch, fail-open]
  |
  |-- NEW C. CutLabRoleAssigner (pure): per card -> RoleSet over 8 roles
  |     lands=IsLand | ramp=IsRampCard | draw=IsDrawCard
  |     interaction=PlanRole.Interaction preGate OR IsInteractionCard/IsBoardWipeCard/IsTargetedRemovalCard
  |     protection=IsProtectionCard | engines=PlanRole.Engine | payoffs=PlanRole.Payoff
  |     wincons=IsClosingPowerCard OR member of an IncludedCombo
  |
  |-- NEW D. CutLabFloorDefaults (bracket + play experience -> 8 default floors)
  |     lands  <- IManabaseBaselineProvider.TryGetBracketBaseline(bracket).AvgLands
  |               (cEDH: ICedhLandBaselineProvider commander mean when available)
  |     ramp/draw <- ManabaseRampDrawBudgetCalculator target split (threshold = commander MV)
  |     other 5 <- static bracket-banded table (new, product-decided defaults)
  |     merge: user-adjusted floors in prior CutLabState.RoleFloors WIN over defaults
  |
  |-- NEW E. CutLabStructuralFindings (pure): findings with per-card evidence
  |     curve congestion | stranded subthemes | redundant finishers
  |     weak floor cases (role count at/under floor) | enabler-starved (near-combos etc.)
  |
  |-- NEW F. CutLabFloorRules (pure, the Phase 103 contract):
  |     Evaluate(roleCounts, floors, candidateCut) -> broken floors -> explicit warnings
  |     + server-side clamp of client-submitted floors (tamper defense)
  |
  |-- serialize CutLabState (now + RoleFloors) -> hidden field  [derived data NOT serialized]
  v
Views/Deck/CutLab.cshtml (+ cut-lab.ts)
  |-- slot-competition groups (8 role sections, cards may appear in >1)   SLOT-01
  |-- structural findings panel with evidence lists                        SLOT-02
  |-- floor editor per role: default badge, stepper, count-vs-floor state  FLOOR-01/02
  |-- floor edits re-serialized into CutLabStateJson on submit             FLOOR-02
```

### Recommended Project Structure
```
DeckFlow.Web/
├── Services/CutLab/
│   ├── CutLabPageService.cs          # EXTEND: stages A-F wired after commander resolution
│   ├── CutLabRoleAssigner.cs         # NEW pure: 8-role assignment from existing signals
│   ├── CutLabFloorDefaults.cs        # NEW: bracket/plan -> default floors (+ static table)
│   ├── CutLabStructuralFindings.cs   # NEW pure: five finding detectors with evidence
│   └── CutLabFloorRules.cs           # NEW pure: floor evaluation + clamping (Phase 103 contract)
├── Models/CutLab/
│   └── CutLabState.cs                # EXTEND: + RoleFloors (additive, init-defaulted)
├── Models/
│   └── CutLabViewModel.cs            # EXTEND: role groups, findings, floors for the view
│                                     #   (also: delete/consolidate dead PoolStatusText — open item 1)
├── Views/Deck/CutLab.cshtml          # EXTEND: three new sections
└── wwwroot/ts/cut-lab.ts             # EXTEND: floor editor + include floors in buildCutLabStateJson
```

Keep the role taxonomy enum + role-set record in `DeckFlow.Web` (not Core): `PlanRoleClassifier` already lives in Web, and the assignment consumes Web-layer I/O results. Core placement is only warranted if Phase 103 planning decides its simulation deltas need it in Core — flag for the planner, not required now.

### Pattern 1: Classification I/O mirrored from `TagPlanRolesAsync` (fail-open, batched)
**What:** Fetch Spellbook combos once and crowd categories in ONE batched query; wrap both in try/catch that degrades to empty results; classify with pure statics.
**When to use:** Stage B. This is the canonical "existing role/category inference" SLOT-01 names.
**Example:**
```csharp
// Source: DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:828-901 (pattern)
var comboNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
CommanderSpellbookResult? combos = null;
try
{
    combos = await _spellbook.FindCombosAsync(analyzedEntries, ct).ConfigureAwait(false);
    if (combos is not null)
        foreach (var combo in combos.IncludedCombos)
            foreach (var name in combo.CardNames) comboNames.Add(name);
}
catch (OperationCanceledException) { throw; }
catch (Exception ex) { _logger.LogWarning(ex, "Cut Lab: Spellbook fetch failed; continuing without combo roles."); }

IReadOnlyDictionary<string, IReadOnlyList<string>> categoriesByName =
    await GetCategoriesFailOpenAsync(poolNames, ct).ConfigureAwait(false); // ONE query, never per-card

PlanRole roles = PlanRoleClassifier.Classify(fact, categories, comboNames.Contains(fact.Name), mode,
    out bool interactionPreGate);
```

### Pattern 2: Eight-role assignment composed from existing signals only
**What:** A pure static that maps one card (`CardFact` + categories + combo membership + `PlanRole` result) to a set of the eight FLOOR-01 roles. Multi-membership is allowed (a card can be both draw and engine); floor accounting counts a card in every role it fills, and a cut decrements every one of them.
**When to use:** Stage C. This is the ONLY new "classification" code and it introduces no new model — every predicate already exists.
**Role taxonomy map (all signals verified in source):**

| Floor role | Existing signal(s) | Source |
|------------|--------------------|--------|
| Lands | `CutLabLockRules.IsLand(typeLine)` (front-face, MDFC-aware) | `CutLabLockRules.cs:123` |
| Ramp | `DeckStatClassifier.IsRampCard(typeLine, oracle)` | `DeckStatClassifier.cs:16` |
| Draw | `DeckStatClassifier.IsDrawCard(oracle)` | `DeckStatClassifier.cs:29` |
| Interaction | `PlanRoleClassifier.Classify` `interactionMeritPreGate` (pre-permanent-gate, includes cEDH counterspell rule) OR `IsBoardWipeCard` OR `IsTargetedRemovalCard` | `PlanRoleClassifier.cs:43-87,196-208` |
| Protection | `DeckStatClassifier.IsProtectionCard(name, oracle)` (curated `StaxProtectionCatalog` + hexproof/indestructible/phases-out text) | `DeckStatClassifier.cs:180-186` |
| Engines | `PlanRole.Engine` flag (categories or repeatable-permanent-draw heuristic) | `PlanRoleClassifier.cs:136-139,177-184` |
| Payoffs | `PlanRole.Payoff` flag (win/finisher/payoff tags or `IsClosingPowerCard` heuristic, permanent-gated) | `PlanRoleClassifier.cs:112-115,162-165` |
| Win conditions | `IsClosingPowerCard(typeLine, oracle)` (ungated) OR membership in a Spellbook `IncludedCombo` | `DeckStatClassifier.cs:78`, `CommanderSpellbookService.cs:16` |

Payoffs vs win conditions overlap by construction (see Open Question 1 — the recommended distinction: *win conditions* = closing-power/combo cards that actually end the game (ungated); *payoffs* = the permanent-gated `PlanRole.Payoff` plan read; a card may be both, and the UI should say so rather than pretend the buckets are disjoint).

### Pattern 3: Bracket-derived floor defaults with user-override merge
**What:** Defaults computed per POST from declared bracket + play experience + commander MV; any floor the user has adjusted (stored in `CutLabState.RoleFloors`) wins over the recomputed default. Track per-role whether the value is default or user-set so re-declaring a bracket updates untouched floors but never stomps user edits.
**Example (lands + ramp/draw sources):**
```csharp
// Source: DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:563-629 (bracket resolution + baseline read)
ManabaseBracketBaseline? row = _manabaseBaseline.TryGetBracketBaseline(bracket); // null for B1 / missing file
int landsFloor = row is null ? FallbackLands : (int)Math.Round(row.AvgLands);

// Source: DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs:114-125 (24-slot split; internal — see Pitfall 4)
// threshold = commander mana value (Cut Lab always knows the commander after Phase 101 resolution)
// targetRamp: <=2 MV -> 8 ... >6 MV -> 14;  targetDraw = 24 - targetRamp
```

### Pattern 4: Additive `CutLabState` extension (JSON round-trip compatible)
**What:** New `RoleFloors` property with an init default so pre-Phase-102 blobs (already in users' open tabs) deserialize cleanly; keep `{ get; init; }` (formatting carve-out: System.Text.Json silently skips get-only properties).
**Example precedent:**
```csharp
// Source: DeckFlow.Core/Manabase/ManabaseModels.cs:216-230 — "Additive — defaults to None so existing
// construction and JSON round-trips are unaffected."
public PlanRole PlanRoles { get; init; } = PlanRole.None;
```
```csharp
// Shape for CutLabState (planner to finalize):
public IReadOnlyList<CutLabRoleFloor> RoleFloors { get; init; } = [];
public sealed record CutLabRoleFloor
{
    public string Role { get; init; } = string.Empty;  // stable role key, not display text
    public int Floor { get; init; }
    public bool IsUserSet { get; init; }               // default-vs-adjusted merge flag
}
```
Serializer already re-runs `EnforceCommanderLock` on deserialize (`CutLabStateSerializer`); add floor clamping (non-negative, ≤ pool size) at the same choke point.

### Pattern 5: Structural findings signal map (SLOT-02)
**What:** Five deterministic detectors, each returning a finding + the card list that is its evidence. All inputs already exist after stages A-C; no detector may hard-fail the page (findings degrade to "unavailable" when their upstream source failed open).

| Finding | Signal (existing) | Evidence shown |
|---------|-------------------|----------------|
| Curve congestion | Per-MV non-land counts from `CardFact.ManaValue` (curve-bucket convention: `DeckStatAggregator` "0-1","2","3","4","5+") — flag buckets holding an outsized share of the pool | The cards in the congested bucket(s), with MV |
| Stranded subthemes | Crowd-category clusters from `GetCategoriesForNamesAsync`: a non-role tag shared by only a small number of pool cards (theme present but under-supported) | The tag + its few member cards |
| Redundant finishers | Win-condition role count (Pattern 2) well above the win-con floor; `WinConMapAggregator` ranking available for ordering | The finisher list, count vs floor |
| Weak floor cases | Role count ≤ floor (or within 1 of it) — every card in that role is effectively cut-protected already | Role, count, floor, member cards |
| Enabler-starved cards | `SpellbookAlmostCombo` (`MissingCard`, `CardsInDeck`) — combo pieces whose partners aren't in the pool; plus cards whose only role came from combo membership in a combo that is now incomplete | The starved card + what it's missing |

Thresholds (what counts as "congested", "small cluster", "well above") are product constants — see Assumptions Log A3; make them named constants in `CutLabStructuralFindings` with `// Why:` comments, not magic numbers.

### Anti-Patterns to Avoid
- **Running classification client-side:** all inference is server-side; `cut-lab.ts` only renders and serializes floor edits.
- **Per-card category queries:** the batched call exists precisely because the per-card loop serially exhausted the request timeout (`ManabaseAnalysisService.cs:870-873`).
- **Trusting client-submitted floors or role data:** mirror the commander-lock tamper defense; server recomputes roles and clamps floors every POST.
- **Putting derived data (roles, findings, defaults) into `CutLabStateJson`:** it bloats the blob toward the 1 MB cap, goes stale when inference improves, and creates a second source of truth. Persist only user decisions (locks, packages, intent, adjusted floors).
- **New nav/flag/SEO surface:** no new `ToolRegistry` entry, no new feature flag, no `SeoPaths` addition (deferred to Phase 105) — Phase 102 changes the existing dark page only.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Role/category inference | Any new tagger, keyword model, or card database | `PlanRoleClassifier` + `DeckStatClassifier` + crowd categories + Spellbook (Patterns 1-2) | Success criterion 1 explicitly forbids a new classification model; these are battle-tested across Manabase/analysis prompts |
| Card facts (oracle/MV/faces) | Custom Scryfall field mapping | `ScryfallCardFactMapper.ToCardFact` | Handles MDFC faces, land-face oracle, produced mana, power parsing |
| Bracket land baseline | New data file or hardcoded per-bracket land numbers | `IManabaseBaselineProvider` (+ `ICedhLandBaselineProvider` for cEDH commander ranges) | Shipped, warm-loaded, fail-open, provenance-labeled |
| Ramp/draw targets | New community heuristics | `ManabaseRampDrawBudgetCalculator` 24-slot split | Deterministic, already documented as advisory-only; threshold logic (commander MV) matches Cut Lab's known commander |
| Combo/near-combo data | Scraping or local combo DB | `ICommanderSpellbookService.FindCombosAsync` | Memory-cached per deck list; fail-open null contract already handled everywhere |
| Curve tallies | New curve bucketing | `DeckStatAggregator` conventions / `CardFact.ManaValue` | Keeps Cut Lab's curve read consistent with analysis/comparison prompts |
| Floor persistence | DB table, ASP.NET Session | `CutLabState` hidden-field envelope (Phase 101 resolved decision) | DB persistence deliberately deferred to Phase 104 (GOAL-02) |

**Key insight:** The genuinely new engineering in this phase is exactly three pure rule sets — role-assignment composition, structural-finding detectors, and floor evaluation/defaults — plus their UI. Everything they consume already exists and is already fail-open.

## Common Pitfalls

### Pitfall 1: Treating `PlanRole` as the whole taxonomy
**What goes wrong:** Ramp, lands, and one-shot draw NEVER earn a `PlanRole` by design ("Ramp, lands, and filler card draw deliberately never earn a role" — `PlanRoleClassifier.cs:16-18`). A role assigner built only on `PlanRole` produces empty ramp/draw/lands groups and floors that can never be counted.
**How to avoid:** Compose per the Pattern 2 table — `DeckStatClassifier.IsRampCard`/`IsDrawCard` and `CutLabLockRules.IsLand` cover the resource axis.
**Warning signs:** Ramp group renders empty for a deck full of Cultivate/Sol Ring.

### Pitfall 2: The permanent gate hides one-shot interaction
**What goes wrong:** `Classify` strips `Interaction` (and `Payoff`) from non-permanent front faces (`PlanRoleClassifier.cs:81-85`) — Swords to Plowshares would land in NO interaction group if you read post-gate flags only, and users will immediately notice their removal suite "missing."
**How to avoid:** Use the `out bool interactionMeritPreGate` overload (exists precisely for this: the cEDH early-interaction lens) for the interaction FLOOR role; also note `ManabaseMode` gates pure counterspells (Casual mode: Counterspell earns nothing). Decide the mode mapping from `PlayExperience`/bracket explicitly and document it in the UI copy.
**Warning signs:** Counterspell/Swords absent from the interaction group; interaction count differs wildly between Casual and cEDH play-experience submissions without explanation.

### Pitfall 3: Persisting derived data in the state envelope
**What goes wrong:** Serializing role assignments, findings, or resolved oracle text into `CutLabStateJson` bloats every subsequent POST (cap: `CutLabStateSerializer.MaxUploadBytes` = 1 MB), goes stale, and lets a tampered client feed fake roles into floor math.
**How to avoid:** Recompute stages A-F on every POST (the Manabase precedent — it re-resolves everything per POST); persist only `RoleFloors` (+ existing locks/packages/intent). Scryfall/Spellbook/category costs are all batched/cached/fail-open, and 150 cards is well under Manabase's proven 500-card ceiling on the same 512 MB tier.
**Warning signs:** `CutLabStateJson` growing beyond a few tens of KB; serializer cap errors.

### Pitfall 4: `ManabaseRampDrawBudgetCalculator.CalculateTargetRamp` is `internal`
**What goes wrong:** The public `Calculate(ManabaseDeck)` needs a fully classified `ManabaseDeck` Cut Lab doesn't have; the useful piece — `CalculateTargetRamp(double threshold)` (`ManabaseRampDrawBudget.cs:114`) — is `internal static` in Core, invisible to `DeckFlow.Web`.
**How to avoid:** Planner decision, mirroring Phase 101's `IsLandType` call: either promote `CalculateTargetRamp` to `public` (preferred — one-word diff, keeps the two consumers from diverging, matches the phase-101 recommendation pattern) or duplicate the 6-line switch in `CutLabFloorDefaults` with a source comment. Do NOT reflect into it.
**Warning signs:** CS0122 accessibility error, or a copy of the switch with silently different breakpoints.

### Pitfall 5: Bracket edge cases — B1 offered, B1 unsupported; bracket optional
**What goes wrong:** The Cut Lab intake offers "B1 Exhibition" (`CutLab.cshtml:65-72`) but the baseline snapshot covers brackets 2-5 only ("Exhibition/B1 is unsupported" — `ManabaseCommunityBaseline.cs:44`), and `CutLabIntent.Bracket` is `int?` (user may skip it). `TryGetBracketBaseline(1)` and `TryGetBracketBaseline(null-path)` both yield no row; naive code crashes or renders a zero lands floor.
**How to avoid:** `CutLabFloorDefaults` must define fallbacks: no bracket → derive from `PlayExperience` (invert `ResolveBaseline`: cEDH→5, Focused→3, else 2 — `ManabaseAnalysisService.cs:567-575`); B1/missing row → fall through to the nearest supported bracket (B2) or a named static default. Every fallback is fine because floors are user-adjustable — but it must be deliberate and tested.
**Warning signs:** NullReferenceException on a B1 submission; lands floor of 0.

### Pitfall 6: Findings hard-fail when a fail-open source failed
**What goes wrong:** Spellbook returns null on API failure and the category store degrades to empty (both by contract). Enabler-starved and stranded-subtheme detectors that assume data exists will either throw or — worse — silently report "no stranded subthemes" as a confident finding when the source was simply down.
**How to avoid:** Thread source-availability flags (`comboDataAvailable` pattern — `WinConMapAggregator.cs:49-60`) through `CutLabStructuralFindings`; render "combo data unavailable" rather than a false-negative finding.
**Warning signs:** A Spellbook outage makes every deck look enabler-healthy.

### Pitfall 7: Floor warnings that only exist in the UI
**What goes wrong:** FLOOR-02's guarantee is cross-phase: "no LATER cut suggestion may silently break a floor." If Phase 102 implements floor checking as view logic, Phase 103's cut engine has nothing to call and the guarantee dies quietly.
**How to avoid:** Ship `CutLabFloorRules` as a pure, unit-tested service API (inputs: role counts, floors, candidate cut's role memberships; output: broken-floor warnings) and state in its xmldoc that Phase 103 MUST route every proposed cut through it. Phase 102's own UI uses the same rule for the "weak floor case" finding and the count-vs-floor display, proving the contract works.
**Warning signs:** Floor math duplicated between the view model and TS.

### Pitfall 8: Client desync between server-rendered groups and lock toggles
**What goes wrong:** Phase 101's lock checkboxes live in the pool table; Phase 102 renders the same cards again inside role groups. Two DOM representations of one lock state will drift unless bound to a single source.
**How to avoid:** Either render lock state once (role groups display-only, locks stay in the pool table) or drive both from the same `cut-lab.ts` state object that already feeds `buildCutLabStateJson` (camelCase contract tested in `cut-lab-lock-interactions.test.ts`). Also note the pre-existing, out-of-scope `deck-input-store.ts` restore-desync bug (memory: `followup_deck_input_store_restore_desync`) so e2e failures aren't misattributed.
**Warning signs:** Locking a card in the pool table doesn't update its role-group row; resubmit drops an edit.

### Pitfall 9: Phase 101 open items left to rot
**What goes wrong:** 101-VERIFICATION recorded five non-blocking items and recommended folding 1-4 into Phase 102's first plan: dead `CutLabViewModel.PoolStatusText` + count-chip copy triplication, hard-coded `form[action="/cut-lab"]` in `cut-lab.ts:103`, misleading Manabase-verbatim castability copy at `CutLab.cshtml:100`, xmldoc garble at `CutLabPoolValidator.cs:26`. Phase 102 rewrites exactly these surfaces — skipping the cleanup now means merge-conflicting with it later.
**How to avoid:** Make plan 102-01 (or the first UI plan) explicitly include the four fixes.

### Pitfall 10: Theme/format/test conventions (standing, but each has bitten before)
- Layout CSS → `site-common.css`; any new tokens → `:root` of EVERY theme file (guild themes are full forks); checkbox styling must stay `appearance:none` + token checkmark (memory: native-chrome bug).
- Keep `{ get; init; }` on all new state records (JSON carve-out), never inline attributes, LF endings.
- UI change ⇒ xUnit + Playwright across desktop+mobile viewports and themes, screenshots before done; run e2e via `scripts/run-web-test.sh`, never a Windows browser; probe for a stale Windows server on 5173 first (memory: `reference_stale_windows_server_5173`).
- Compiled `wwwroot/js/cut-lab.js` is gitignored — never stage it.

## Code Examples

### Fail-open batched category lookup (the exact reuse target)
```csharp
// Source: DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:905-926
private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetCategoriesFailOpenAsync(
    IReadOnlyCollection<string> cardNames, CancellationToken cancellationToken)
{
    if (_categoryKnowledge is null || cardNames.Count == 0) return EmptyCategories;
    try
    {
        return await _categoryKnowledge.GetCategoriesForNamesAsync(cardNames, cancellationToken).ConfigureAwait(false);
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception exception)
    {
        _logger.LogWarning(exception, "Plan-presence: batch category lookup failed; using heuristics only.");
        return EmptyCategories;
    }
}
```

### Ramp/draw target split (source for the ramp+draw floors)
```csharp
// Source: DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs:114-125 (internal — promote or duplicate, Pitfall 4)
internal static int CalculateTargetRamp(double threshold)
{
    double rampTarget = threshold switch
    {
        <= 2.0 => 8.0,
        <= 4.0 => 8.0 + (2.0 * (threshold - 2.0)),
        <= 6.0 => 12.0 + (threshold - 4.0),
        _ => 14.0,
    };
    return (int)Math.Round(rampTarget, MidpointRounding.AwayFromZero);
}
// targetDraw = 24 - targetRamp  (Calculate, line 79)
```

### Bracket resolution fallback (invert for bracket-less floors)
```csharp
// Source: DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:567-575
private static (int Bracket, ManabaseBracketSource Source) ResolveBaseline(ManabaseAnalysisOptions options)
    => options.Bracket is int explicitBracket
        ? (explicitBracket, options.BracketSource ?? ManabaseBracketSource.Override)
        : (options.Mode switch
        {
            ManabaseMode.Cedh => 5,
            ManabaseMode.Focused => 3,
            _ => 2,
        }, ManabaseBracketSource.Fallback);
```

### Near-combo record (the enabler-starved evidence source)
```csharp
// Source: DeckFlow.Web/Services/CommanderSpellbookService.cs:26-30
public sealed record SpellbookAlmostCombo(
    string MissingCard,
    IReadOnlyList<string> CardsInDeck,
    IReadOnlyList<string> Results,
    string Instructions);
```

### Tamper defense choke point (extend for floor clamping)
```csharp
// Source: DeckFlow.Web/Services/CutLab/CutLabLockRules.cs:12-27 — EnforceCommanderLock re-applied by
// CutLabStateSerializer.Deserialize; add CutLabFloorRules.ClampFloors at the same point so a DOM-edited
// RoleFloors entry (negative, absurd, unknown role key) is corrected before any rendering or evaluation.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Per-card category DB queries | Single batched `GetCategoriesForNamesAsync` | Fixed after plan-presence timeout incident (comment `ManabaseAnalysisService.cs:870-873`) | Cut Lab MUST batch — the failure mode (20 s of sequential Postgres round-trips) is documented in-code |
| Bracket-blind land advice | Bracket-graded community baseline + cEDH commander meta range | Manabase bracket baseline shipped 2026.07.7 (live, flag flipped) | The lands floor default source FLOOR-01 asks for already exists and is warm-loaded |
| Manabase-only role read | `PlanRole` flags carried as pure data on `SpellRequirement` + pre-gate interaction signal | Plan-presence + cEDH interaction lens cycles | The exact "existing role/category inference" this phase reuses, including the out-param added for the interaction-visibility problem Cut Lab will also hit |
| — | Commander-ability land adjustments (cost-floor/mana-producer commander heuristics) | REJECTED via EDHREC study (memory: `project_manabase_commander_cost_floor` — "ALL DEAD, DON'T re-attempt") | Do not resurrect commander-ability-based floor adjustments; bracket-graded targets are the only sanctioned bracket feature |

**Deprecated/outdated:** None — Phase 102 extends a not-yet-launched tool.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | "Derived from declared bracket and plan" (FLOOR-01) means bracket + play-experience drive the numeric defaults, with the free-text `PrimaryPlan` shown as context only — free text cannot parameterize floors without new classification (which the phase forbids) | Pattern 3, Pitfall 5 | If the product intent is plan-text-sensitive floors (e.g. "combo deck → higher tutor floor"), that needs a user-facing plan-archetype picker (a new intent control), not text inference — scope addition the planner should surface in discuss-phase [ASSUMED] |
| A2 | Payoffs and win conditions are allowed to overlap (one card may count toward both floors), with win conditions = ungated closing-power/combo cards and payoffs = the permanent-gated `PlanRole.Payoff` read | Pattern 2, Open Question 1 | If the product wants disjoint buckets, a precedence rule must be decided; overlap is the honest reading of the existing signals [ASSUMED] |
| A3 | Default floor numbers for interaction/protection/engines/payoffs/win-conditions and the finding thresholds (congestion share, small-cluster size, redundancy margin) are new product constants — no codebase or verified external source dictates them; community templates (e.g. "~10 interaction") are training-data folklore | Standard Stack alternatives, Pattern 5 | Wrong defaults mislead until adjusted; mitigated because FLOOR-02 makes every floor editable and the UI labels defaults. Numbers need explicit sign-off in planning/discuss-phase [ASSUMED] |
| A4 | Recomputing classification + findings on every POST (lock toggle, floor tweak) is acceptable on the 512 MB tier given batching + Spellbook/banlist caching and the 150-card cap | Pitfall 3 | If POST latency proves annoying (Spellbook cold call ~1 network round-trip), the mitigation is memory-caching classification per pool hash — an optimization, not a redesign [ASSUMED] |
| A5 | Floor-role keys serialized as stable strings (not enum ints) in `CutLabStateJson`, so Phase 103/104 schema evolution doesn't renumber user data | Pattern 4 | Low — planner may choose an enum with `JsonStringEnumConverter`; either works if decided once [ASSUMED] |

## Open Questions (RESOLVED)

1. **Payoffs vs win-conditions boundary (two of the eight floors)**
   - What we know: `PlanRole.Payoff` is permanent-gated (`Torment of Hailfire` earns nothing post-gate); `IsClosingPowerCard` is ungated; Spellbook combo membership marks combo wins. The requirement lists both roles separately.
   - What's unclear: The product definition distinguishing them for floor counting and slot grouping.
   - Recommendation: Win conditions = `IsClosingPowerCard` OR included-combo member (ungated — a win con doesn't need to be a permanent); payoffs = `PlanRole.Payoff` (permanent-gated plan read). Overlap allowed and labeled. Lock in planning; cheap to change before UI copy exists.
   - **RESOLVED (planning):** Locked per the recommendation in the DECISIONS blocks of 102-01-PLAN and 102-02-PLAN — wincons = `IsClosingPowerCard` OR included-combo member (ungated); payoffs = `PlanRole.Payoff` (permanent-gated); overlap allowed, no precedence rule.

2. **Where the interaction-mode gate reads from (bracket vs play experience)**
   - What we know: `ManabaseMode` gates whether pure counterspells count as interaction; Cut Lab captures BOTH `Bracket` (1-5, optional) and `PlayExperience` ("Casual"/"Focused"/"cEDH"). Manabase maps mode→bracket (Cedh→5/Focused→3/else 2) but Cut Lab needs the reverse.
   - What's unclear: Which intent field wins when they disagree (e.g. B4 + "Casual").
   - Recommendation: `PlayExperience` → `ManabaseMode` for classification (it IS the mode vocabulary), bracket → numeric floor defaults. Document the mapping in help copy.
   - **RESOLVED (planning):** Locked per the recommendation in the DECISIONS blocks of 102-01/102-02/102-03-PLAN — `PlayExperience` drives `ManabaseMode` for classification (PlayExperience wins on disagreement); bracket drives the numeric floor defaults.

3. **UI shape for slot groups vs the Phase 101 pool table**
   - What we know: The pool table with lock checkboxes shipped in 101; role groups re-present the same cards; UI hint = yes; every UI change needs theme×viewport e2e screenshots.
   - What's unclear: Replace the flat table with grouped sections, or add groups alongside it (duplication → Pitfall 8).
   - Recommendation: Planner decides with the UI design pass; prefer one canonical card row (grouped view) over duplicated rows, and keep `data-cut-lab-role` attributes as the grouping key the existing bulk-land-lock TS already uses.
   - **RESOLVED (UI design pass):** 102-UI-SPEC Component Contract 1 — role groups render as display-only accordions ALONGSIDE the pool table, which stays the sole lock surface (per-group pills drive the pool-table checkboxes; multi-role token `data-cut-lab-role` attribute).

## Environment Availability

Not applicable — no new external tools, services, or runtimes. All dependencies (Scryfall, Commander Spellbook backend, category-knowledge DB, baseline data files) are already integrated, DI-registered, and warm-loaded per `Program.cs`.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (.NET), Vitest ^3.2.7 (TS units), @playwright/test ^1.60 (e2e) |
| Config file | `DeckFlow.Web/playwright.config.ts`; xUnit via csproj |
| Quick run command | `dotnet build` clean (WSL baseline) + `dotnet test --filter CutLab` |
| Full suite command | `dotnet test DeckFlow.Web.Tests` + `dotnet test DeckFlow.Core.Tests` + `npx --no-install playwright test cut-lab-*.spec.ts` via `scripts/run-web-test.sh` (never a Windows browser) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SLOT-01 | 8-role assignment from existing signals (incl. pre-gate interaction, MDFC lands, multi-membership) | unit | `dotnet test --filter CutLabRoleAssignerTests` | ❌ Wave 0 |
| SLOT-02 | Five finding detectors incl. fail-open degradation (Spellbook null, empty categories) | unit | `dotnet test --filter CutLabStructuralFindingsTests` | ❌ Wave 0 |
| FLOOR-01 | Defaults per bracket incl. B1/no-bracket fallbacks, cEDH commander range, user-override merge | unit | `dotnet test --filter CutLabFloorDefaultsTests` | ❌ Wave 0 |
| FLOOR-02 | Floor evaluation contract (break → warning, never silent), clamping of tampered floors, state round-trip of adjusted floors | unit + e2e | `dotnet test --filter CutLabFloorRulesTests` + page-service round-trip test; e2e adjusts a floor, resubmits, asserts persistence + warning visibility | ❌ Wave 0 |
| SLOT/FLOOR UI | Groups, findings, floor editors render across themes/viewports; edits survive resubmit | e2e | `npx --no-install playwright test cut-lab-structure.spec.ts` (or extend `cut-lab-smoke.spec.ts`) | ❌ Wave 0 |

Regression guards that must stay green: `CutLabPageServiceTests`, `CutLabLockRulesTests` (state extension must not break lock semantics), `CutLabStateSerializer` round-trip tests (old blobs without `RoleFloors` still deserialize), `cut-lab-lock-interactions.test.ts` (camelCase JSON contract), `ToolRegistryTests`/`FeatureFlagCatalogTests` (untouched — no new tool/flag).

### Sampling Rate
- **Per task commit:** `dotnet build` clean + `dotnet test --filter CutLab`
- **Per wave merge:** both full test projects + Vitest + Cut Lab e2e via `scripts/run-web-test.sh`
- **Phase gate:** full suite green incl. e2e theme×viewport screenshots before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `DeckFlow.Web.Tests/CutLab/CutLabRoleAssignerTests.cs` — SLOT-01
- [ ] `DeckFlow.Web.Tests/CutLab/CutLabStructuralFindingsTests.cs` — SLOT-02
- [ ] `DeckFlow.Web.Tests/CutLab/CutLabFloorDefaultsTests.cs` — FLOOR-01
- [ ] `DeckFlow.Web.Tests/CutLab/CutLabFloorRulesTests.cs` — FLOOR-02
- [ ] e2e spec (new or extended) — UI + persistence
- [ ] Framework install: none — all frameworks already configured

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Public anonymous tool (unchanged) |
| V3 Session Management | Partial | Client-carried hidden-field state; `[ValidateAntiForgeryToken]` already on POST (Phase 101) |
| V4 Access Control | Yes | Existing `[FeatureFlagGate("tool.cut-lab.enabled")]` on GET+POST — unchanged, still seeded OFF |
| V5 Input Validation | Yes | Existing `RequestSizeLimit` + `MaxUploadBytes` (1 MB) + source-length caps; NEW: clamp client-submitted `RoleFloors` (non-negative ints, known role keys, ≤ pool size) at deserialize |
| V6 Cryptography | No | None |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Tampered `RoleFloors` in `CutLabStateJson` (negative floors, junk role keys, floors of 10⁹ to spam warnings) | Tampering | Server-side clamp at the `CutLabStateSerializer.Deserialize` choke point, mirroring `EnforceCommanderLock`; never trust client floor values for the Phase 103 warning contract |
| Tampered role/finding data | Tampering | Not persisted at all — recomputed server-side every POST (Pitfall 3) |
| Oversized state blob DoS | DoS | Existing 1 MB `MaxUploadBytes` + 2 MB `RequestSizeLimit`; keeping derived data out of the blob preserves headroom |
| Upstream abuse via repeated POSTs (Spellbook/category DB) | DoS | Spellbook memory cache per deck list; single batched category query; Scryfall behind `ScryfallThrottle`; all fail-open |

## Sources

### Primary (HIGH confidence — direct codebase reads this session, file:line cited)
- `DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs` — full read: role precedence, permanent gate, pre-gate out-param, mode-gated counterspells
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs:563-629, 820-929` — bracket resolution, community baseline read, `TagPlanRolesAsync`, fail-open batch category lookup
- `DeckFlow.Core/Analysis/DeckStatClassifier.cs` — all role predicates incl. `IsProtectionCard`/`StaxProtectionCatalog`; `DeckStatAggregator.cs` — curve buckets + tallies
- `DeckFlow.Core/Manabase/ManabaseModels.cs:140-237` — `PlanRole` (excludes ramp/lands/filler draw), `SpellRequirement` additive-JSON precedent
- `DeckFlow.Core/Manabase/ManabaseRampDrawBudget.cs` — 24-slot split, `internal CalculateTargetRamp`
- `DeckFlow.Core/Manabase/ManabaseCommunityBaseline.cs`, `DeckFlow.Web/Services/Manabase/ManabaseBaselineProvider.cs`, `CedhLandBaselineProvider.cs` — bracket/commander land baselines, fail-open
- `DeckFlow.Web/Services/CutLab/*` + `Models/CutLab/CutLabState.cs` + `Models/CutLabRequest.cs` + `Views/Deck/CutLab.cshtml:54-99` — Phase 101 shipped shape (pool card fields, bracket B1-B5 pills, play-experience pills, hidden-field round-trip)
- `DeckFlow.Web/Services/CommanderSpellbookService.cs` — `SpellbookCombo`/`SpellbookAlmostCombo`/`FindCombosAsync` contract, memory cache, null-on-failure
- `DeckFlow.Web/Services/Persistence/ICategoryKnowledgeStore.cs` + `Program.cs:94-95,172,181,310` — DI registrations and warm-loading
- `DeckFlow.Core/Analysis/WinConMapAggregator.cs` — `comboDataAvailable` degradation pattern, deterministic finisher ranking
- `DeckFlow.Core/Manabase/ScryfallCardData.cs`, `ScryfallCardFactMapper.cs`, `CardFact.cs` — the data bridge from Cut Lab's existing resolution to classifier input
- `.planning/workstreams/cut-lab/{PROJECT,REQUIREMENTS,ROADMAP,STATE}.md`, `phases/101-.../101-{RESEARCH,VERIFICATION}.md` — constraints, requirement IDs, Phase 101 open items and resolved decisions
- Root `CLAUDE.md` — project constraints (stack, themes, testing, formatting carve-outs)

### Secondary (MEDIUM confidence)
- Project memory: manabase bracket baseline live 2026.07.7; commander-ability land adjustments rejected ("don't re-attempt"); deck-input restore-desync bug queued out-of-scope; stale-5173-server e2e hazard.

### Tertiary (LOW confidence)
- Community floor-number folklore (e.g. "~10 interaction", Command Zone templates) — deliberately NOT used as verified sources; flagged [ASSUMED] in A3 for product sign-off.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — every reused component verified by direct read with line numbers; zero new dependencies
- Architecture: HIGH — the classification pipeline, state envelope, and page shape are all shipped patterns; new code is three pure rule sets + UI
- Pitfalls: HIGH — each grounded in an in-code comment, a verified access modifier, a shipped view detail (B1 pill vs 2-5 baseline), or a recorded verification open item
- Floor default numbers / finding thresholds: LOW by nature — product constants, not derivable facts; explicitly routed to planning sign-off (A3)

**Research date:** 2026-07-19
**Valid until:** Stable (internal-codebase research). Re-verify only if `PlanRoleClassifier`, `CutLabState`/serializer, or the baseline providers are refactored before planning executes.
