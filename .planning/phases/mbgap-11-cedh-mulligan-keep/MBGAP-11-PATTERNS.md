# Phase MBGAP-11: cEDH Opening-Hand Keep Heuristic - Pattern Map

**Mapped:** 2026-07-14
**Files analyzed:** 9 work-units (Core sim + analyzer, Core models, calibration, Web flag wiring, view, view-model, prompt text, e2e)
**Analogs found:** 9 / 9 (all have in-repo analogs; this is a layered-onto-existing-code phase, not greenfield)

All work layers onto the existing manabase analyzer/simulator. There are **no** new Core/Web boundary crossings to invent — the plan-presence pass already threads Web-classified role data into Core. Every unit below has a direct analog to copy.

---

## File Classification

| Unit of work | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| cEDH three-shape keep gate | Core domain logic (sim pass) | batch/transform (Monte-Carlo tally) | `CastabilitySimulator.SimulatePlanPresence` `CastabilitySimulator.cs:483-705` | exact (same pass, extend) |
| Representative-opener rewrite (turn cap + commander) | Core domain logic | transform | `ManabaseAnalyzer.ComputeMulliganEvaluation` opener block `ManabaseAnalyzer.cs:1509-1533` | exact |
| Turn-cap / bridge-threshold constants + pin tests | Core config + test | n/a | `CedhCalibration` `CedhCalibration.cs:56-66` + `CedhCalibrationTests.cs`; live threshold consts `ManabaseAnalyzer.cs:16-17` | role-match |
| New plan-keepable % + labels (models) | Core model (DTO) | n/a | `ManabaseMulliganEvaluation` `ManabaseModels.cs:1452-1488`; `ManabasePlanPresence` `:1535-1571` | exact |
| Casual curve-coverage metric | Core domain logic | transform | per-turn castability already walked in `SimulateGame` (called at `CastabilitySimulator.cs:626`); tally like plan-presence `:642-656` | role-match |
| New feature flag wiring | Web config | request-response | `MulliganEvalFlagKey`/`PlanPresenceFlagKey` `ManabaseAnalysisService.cs:207,221`; catalog `FeatureFlagCatalog.cs:99-115`; seed `FeatureFlagStore.cs:228-230,273-275` | exact |
| Prompt-artifact text | Core text builder | transform | `AppendMulliganEvaluationBlock` `ManabaseReportTextBuilder.cs:293-338` | exact |
| UI copy (view) | Razor view | request-response | mulligan lens block `Manabase.cshtml:628-699` | exact |
| e2e panel specs + flag-restore | test (Playwright) | n/a | `manabase-mulligan.spec.ts`; flag-restore hardening commit `f8f58586` | exact |

---

## Pattern Assignments

### 1. cEDH three-shape keep gate (Core `CastabilitySimulator`)

**Where it lives:** Core, alongside the existing plan-presence pass — **not** the per-spell `Simulate` loop. The shape gate needs per-hand role knowledge + in-hand acceleration at keep time, which is exactly what `SimulatePlanPresence` already computes per trial.

**Primary analog — the role-tally trial loop:** `CastabilitySimulator.SimulatePlanPresence` `CastabilitySimulator.cs:483-705`. This pass already:
- deals a London-mulligan hand per trial and buckets by kept size (`DealHand` + `depthIdx` switch, `:572-576`);
- for each plan card, checks "drawn by its on-curve turn?" via `posInPrefix` (`:604-621`) AND "castable then?" via `SimulateGame` reusing the board model (`:624-640`) — this is precisely the **Shape A "deployable by turn ≤N counting in-hand acceleration"** primitive (acceleration is already in the board sim: `ritualBurst`, ramp);
- OR's roles into `rolesThisHand` (`:633`) and tallies `roleCounts` per single role (`:642-656`) — the **role-counting substrate for Shapes B (Engine) and C (≥2 Interaction)**.

**Keep-band / schedule analog (the mana floor that stays):** `LondonMulligan` `CastabilitySimulator.cs:2134-2206` — `hiCap` (`:2140`), the per-depth `(Keep, Bottom, Lo, Hi, RampGate)` schedule (`:2149-2162`), `landsOk`/`colorOk` gates (`:2182-2190`). CONTEXT confirms this floor is preserved; the shape gate layers on top of its keep verdict.

**Concrete extension shape (F-01 option (a), preferred):** Extend `SimulatePlanPresence` to emit per-hand shape verdicts + a plan-keepable count, mirroring how it already emits `withPlan`/`keepable` (`:642-656, 687`). New turn-bounded checks replace the `planTurn = library[planIdx].PlanManaValue` bound (`:606`) with a calibrated cap (see §3). The mull-to-5 sampling/attempt-cap machinery (`:558-568, 658-680`) is the template for capping any new per-hand work.

**Note for planner:** `ComputeMulliganEvaluation` (`ManabaseAnalyzer.cs:1474`) does **not** currently receive `mode` — it is called at `ManabaseAnalyzer.cs:283` where `mode` is in scope (used at `:271, :285`). The cEDH gate needs `mode` threaded into both `ComputeMulliganEvaluation` and (if the shape verdicts are computed there) `SimulatePlanPresence`. `SimulatePlanPresence` also currently takes no `mode`/no calibration; it is invoked at `ManabaseAnalyzer.cs:254-257`.

---

### 2. F-01 — role-data threading (Core/Web crossing already exists)

**The crossing is already built — reuse it, do not invent a new one.** The path:

1. **Role classification (Web):** `PlanRoleClassifier.Classify(...)` `DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs:43` — the only cEDH-aware step today (counterspell → Interaction in cEDH, `:124-131`).
2. **Tagging into the Core deck (Web):** `ManabaseAnalysisService.TagPlanRolesAsync` `ManabaseAnalysisService.cs:694-767`. It fetches Spellbook combos + crowd categories, then writes roles onto each Core spell: `tagged.Add(spell with { PlanRoles = roles, IsInteractionSpell = interactionMeritPreGate })` (`:763`), returning `deck with { Spells = tagged }` (`:766`). Gated on `classifyPlanRoles` (`:659-662`), which is `showPlanPresence || showCedhInteractionLens` (`:335`).
3. **The DTO field that crosses the boundary (Core-owned):** `SpellRequirement.PlanRoles` `ManabaseModels.cs:201` (+ `IsInteractionSpell` `:209`). This is a **Core** property the **Web** layer populates — so role data reaches Core with zero new Web dependency in Core. `IsPlanCard => PlanRoles != PlanRole.None` `CastabilitySimulator.cs:115`.
4. **Consumption in Core:** the tagged deck flows into `SimulatePlanPresence` (`ManabaseAnalyzer.cs:254-257`) and the library carries `planRoles: spell.PlanRoles` `CastabilitySimulator.cs:861`.

**Implication for F-01:** Option (a) is the low-risk choice — the role data already lands in `SimulatePlanPresence`'s trial loop. Threading roles into the *separate per-spell* `Simulate` mulligan trials (option (b)) would duplicate the trial loop `SimulatePlanPresence` already owns. Prefer (a): add shape verdicts as outputs of the existing pass; the keep gate reads them.

`PlanRole` enum: `ManabaseModels.cs:144` (values Payoff/Engine/TutorCombo/Interaction used at `CastabilitySimulator.cs:509`).

---

### 3. CedhCalibration constant + pin-test pattern (turn-cap / bridge-threshold model)

**Constant-block analog:** `CedhCalibration.cs:56-66` — `MinCommanderSamples`, `SafetyFloor`, `TargetCeiling` as `private const`, plus a `static readonly` selector list. This is the model for new `TurnCapExplosive` (default 3), `RepresentativeLineTurnCap` (default 4), `BridgeInteractionMin` (default 2) constants.

**Live-in-analyzer threshold precedent (simpler, closer to a "turn cap" home):** `ManabaseAnalyzer.cs:16-17`
```csharp
private const int CasualSupportThreshold = 80;
private const int CedhSupportThreshold = 88;
```
selected by mode at `:938, :1315-1318` and passed to `ComputeInteractionLens` at `:285`. A cEDH turn cap can follow this exact `mode == ManabaseMode.Cedh ? cedh : casual` selection idiom. Mirror threshold also hard-coded in `ManabaseModels.cs:1075` (`supportThreshold = Mode == Cedh ? 88 : 80`) — keep the two in sync if you touch it.

**Pin-test model:** `CedhCalibrationTests.cs` — deterministic hand-built rows, exact-value asserts (`Build_ComputesOverallSegmentAndCommanderRollups` `:12-70`), boundary tests (`Build_CommanderFilterBoundary` `:192`), verbatim-string pins (`RenderMarkdown_PinsSummaryLinesAndSegmentTableHeader` `:267-278`), null guards (`:184-190`). New shape-gate/turn-cap constants get the same treatment: construct hands that straddle the cap, assert the shape verdict flips exactly at the boundary.

---

### 4. Representative-opener selection + RepresentativeOpeners DTO

**Selection analog (the code to rewrite):** `ManabaseAnalyzer.cs:1509-1533`. Current logic:
- `nonCommanderRows` excludes the commander (`:1480`, comment `:1516-1517` — the exclusion MBGAP-11 reverses for commander-central cEDH);
- `demandingRows` = `ManaValue >= 1` (`:1509`);
- ordered `OrderBy(ManaValue).ThenBy(OnCurveTurn)`, first sample per `Decision`, `Take(3)` (`:1526-1533`) — **no turn cap** (the defect: a turn-6 payoff surfaces as "workable").
- When plan-presence ran, `planPresence.RepresentativeOpeners` wins (`:1524-1525`).

**DTO it feeds — `OpeningHandSample`:** `ManabaseModels.cs:315-357`. Fields the new shape-labeled copy will read/extend: `Decision` (`:333`), `TrackedSpellName` (`:340`), `TrackedOnCurveTurn` (`:343`), `OnCurveCastable` (`:349`), `HasPlan` (`:356`), composition counts `Lands/Colors/RampPieces/OtherCards/KeptCards` (`:318-330`). Dual-producer note at `:359-369` — respect it: per-spell vs plan-presence producers set these fields with different semantics.

**Container:** `ManabaseMulliganEvaluation.RepresentativeOpeners` `ManabaseModels.cs:1480`; plan-presence variant `ManabasePlanPresence.RepresentativeOpeners` `:1570`; builder `BuildPlanOpenerSample` `CastabilitySimulator.cs:711`.

**Commander availability in the opener pool (for reversing the exclusion):** commander rows are in `castability` with `IsCommander = true` (`CastabilitySimulator.cs:460`); filtered out at `ManabaseAnalyzer.cs:1480`. To let the commander join the pool in cEDH, gate that `.Where(r => !r.IsCommander)` on mode.

---

### 5. Two-headline-% precedent (add plan-keepable %)

**Existing headline #1 — mana-keepable %:** `KeepableHandPercent` `ManabaseModels.cs:1455`, computed `ManabaseAnalyzer.cs:1489-1492` (`kept7Percent + mulliganTo6Percent`, band switch `:1494-1499`). Surfaced: view `Manabase.cshtml:638-640`; prompt `ManabaseReportTextBuilder.cs:297`.

**Existing headline #2 — payoff/plan %:** `ManabasePlanPresence.PayoffPercent` `ManabaseModels.cs:1542` + `PlanPresencePercent` `:1548`, computed `CastabilitySimulator.cs:687-704`. Surfaced: view `Manabase.cshtml:653-671` (payoff leads, per-role sub-line drops Payoff to avoid double-count `:658-660`); prompt `ManabaseReportTextBuilder.cs:306-314`.

**The new `plan-keepable %` copies this exact two-number pattern:** add a field to `ManabaseMulliganEvaluation` (`ManabaseModels.cs:1452`) beside `KeepableHandPercent`, compute it as `plan-keepable / total` in the same place `KeepableHandPercent` is derived (or emit from the extended `SimulatePlanPresence`), and render it as a second big-number in the view split (`Manabase.cshtml:633-673`) + a second line in the prompt block. By construction `plan-keepable ≤ mana-keepable` (Acceptance #2) — mirror the "derived so numbers reconcile" note at `ManabaseAnalyzer.cs:1483-1492`. Gate the second headline on `report.Mode == ManabaseMode.Cedh` (mode enum `ManabaseMode.cs:8-15`).

---

### 6. Feature-flag + PromptMutatingAnalysisFlags registration

**Flag-key constant + resolution (the pattern to copy):** `ManabaseAnalysisService.cs`
- constants: `MulliganEvalFlagKey = "analysis.manabase.mulligan-eval"` `:207`, `PlanPresenceFlagKey` `:221`, `CedhInteractionLensFlagKey` `:214`;
- resolution: `bool showMulliganEval = IsFlagOn(MulliganEvalFlagKey)` `:311`; dependent gating `showPlanPresence = IsFlagOn(PlanPresenceFlagKey) && showMulliganEval` `:317`; `IsFlagOn` returns false for missing keys `:501`;
- surfaced onto result: `ShowMulliganEval`/`ShowPlanPresence` `:359-360, 463-464`.

**Catalog description (required or `FeatureFlagCatalogTests` fails):** `FeatureFlagCatalog.cs:99-140` — add a one-line entry keyed by the new flag (model the "cEDH-only … Off = byte-identical output" phrasing of `:112-115`). Guard test: `FeatureFlagCatalogTests.cs:42-43`.

**Seed (default OFF this phase, flip after UAT):** `FeatureFlagStore.cs` — Postgres seed `:228-230`, SQLite seed `:273-275`. Existing manabase flags seed `TRUE`; **seed the new one `FALSE`/`0`** per CONTEXT. Rename-migration precedent at `:34-36`. Seed test: `FeatureFlagStoreSeedTests.cs:41-42`.

**PromptMutatingAnalysisFlags registry:** `DeckAnalysisPacketService.cs:159-166` (constants `:101-148`). **Discrepancy the planner must resolve:** this registry currently contains **only deck-analysis packet flags** — the existing manabase display flags (`mulligan-eval`, `plan-presence`, `cedh-interaction-lens`) are **NOT** in it, and the deck-analysis packet does **not** embed manabase text (grep of `DeckAnalysisPacketService.cs` for "manabase"/"mulligan" finds only unrelated comments). So the manabase paste artifact is regenerated per-analyze, not served from the deck-analysis packet cache. CONTEXT §5 and the `followup_packet_cache_flag_replay` memory nonetheless direct the new flag to join `PromptMutatingAnalysisFlags`. **Planner action:** confirm whether the manabase artifact shares any packet/session cache before assuming registration is load-bearing; if it does not, document that the registration is precautionary (or that the rule targets a different cache). Do not silently skip it — the memory rule is explicit.

---

### 7. Prompt-artifact text builder + tests

**Builder analog (the block to modify):** `ManabaseReportTextBuilder.AppendMulliganEvaluationBlock` `ManabaseReportTextBuilder.cs:293-338`. Structure to mirror:
- headline line `:296-297` (`Keepable hands: {band} (~{pct}%)`) — the plan-keepable second headline is a sibling `AppendLine`;
- flag-gated plan line `:306-314` (`if (includePlanPresence && mull.PlanPresence is { } plan)`) — this `includePlanPresence` bool is the template for gating new cEDH copy behind the new flag; off appends zero bytes (byte-identity, Acceptance #7);
- representative-opener loop `:316-335` with the `TrackedSpellName`/`OnCurveCastable`/`HasPlan` → copy mapping (`:324-333`) — the new shape-labeled templates ("explosive keep" / "engine keep" / "bridge keep" / "no plan by turn 4 — mulligan") slot here.

**Test coverage:** `DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderMulliganTests.cs` (verbatim-line pins for this block) — the model for pinning the new cEDH copy. Byte-identity excision proof lives in `DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs` (`OffState_IsByteIdenticalToOnWithMulliganCardExcised`, referenced in the e2e header comment).

---

### 8. Commander-centrality inputs (D-02 heuristic)

All candidate inputs already exist and are readable in Core at analysis time:

- **Command-zone castability** ("casts on curve 88% turn 4"): commander cast% lives in `report.Castability` rows where `IsCommander == true` — `CardCastability.CastPercent` + `IsCommander` `CastabilitySimulator.cs:458-460`. Read pattern: `report.Castability.Where(c => c.IsCommander)` `ManabaseCommandZoneFormatter.cs:20`; min-commander pick `ManabaseAnalyzer.cs:1669`.
- **Commander PlanRole:** `SpellRequirement.IsCommander` + `PlanRoles` on the tagged deck (`ManabaseModels.cs:201`); commander spells identifiable via `spell.IsCommander` (`ManabaseAnalyzer.cs:769`).
- **CommanderImportance** (existing "how central is the commander" axis): enum `ManabaseModels.cs:706-712` (`Critical`/`Standard`/`Low`), threaded through `ManabaseAnalyzer.cs:163, 672, 769` (`commanderDriver = spell.IsCommander && importance != Low`). This is the closest existing "centrality" signal — the D-02 heuristic can combine it with command-zone cast% rather than inventing a new axis.
- **Commander colors:** `CommanderColors(deck)` `ManabaseAnalyzer.cs:1324`.

The commander is only removed from the *drawable library* (`CastabilitySimulator.cs:968`), not from castability reporting — so command-zone cast% is available to the keep gate without new simulation.

---

### 9. e2e Opening-Hand panel specs + flag-restore hardening

**Panel specs to churn:** `DeckFlow.Web/e2e/manabase-mulligan.spec.ts` (LOW-8/9 opening-hand card, `analysis.manabase.mulligan-eval` gate) and `DeckFlow.Web/e2e/manabase-lens-visual.spec.ts`. Live-UX smoke at desktop 1280 + mobile 390 under chromium-desktop/chromium-mobile projects; result-dependent asserts guarded by `test.skip` when Scryfall is unreachable (`manabase-mulligan.spec.ts:1-20`).

**Flag-restore hardening to reuse (commit `f8f58586`):** `manabase-mulligan.spec.ts` already implements the correct pattern — capture the flag's pre-test state in `beforeEach` (`captureOriginalFlagEnabled`, `:49`) and restore it in `afterEach` (`restoreFlagEnabled`, `:54`) with one retry + warn-and-continue so the admin lock always releases. **Copy this verbatim for the new cEDH flag** — do NOT hard-restore to `false` (the pre-`f8f58586` bug that left the shared SQLite store OFF and contaminated later specs). Serialization + admin lock: `test.describe.configure({ mode: 'serial' })` (`:44`) + `acquireAdminLockForTest`/`releaseAdminLockForTest` from `./support/admin-lock` (`:2, :47, :57`); synthetic CF-Connecting-IP convention noted `:19-20`. Toggle helpers: `setFlagEnabled` (`:79`).

**New e2e work:** a cEDH-mode spec asserting (a) two headline %s render, (b) a turn-≥5 payoff is NOT called workable, (c) commander surfaces as a representative line for a commander-central fixture (Winota) — reuse the `PASTE_DECK` fixture shape at `manabase-mulligan.spec.ts:31-41` and add a cEDH-mode selection + a commander-central decklist.

---

## Shared Patterns

### Mode-branch idiom (Casual vs cEDH)
**Source:** `ManabaseAnalyzer.cs:16-17` + `:938` (`mode == ManabaseMode.Cedh ? CedhSupportThreshold : CasualSupportThreshold`); enum `ManabaseMode.cs:8-15`.
**Apply to:** every new cEDH-only branch (shape gate, turn cap, commander-in-pool, two headlines). Casual path must stay byte-identical except the new curve-coverage metric (D-03).

### Byte-identical-when-off
**Source:** flag-gated append `ManabaseReportTextBuilder.cs:306` (`includePlanPresence`); null-modeled additive fields (`ManabaseMulliganEvaluation.PlanPresence` nullable `ManabaseModels.cs:1487`, "a null here leaves the existing opener block byte-identical").
**Apply to:** all new prompt/UI copy — gate behind the new flag; off appends zero bytes / renders nothing. Proven by `ManabaseViewRenderTests` excision test + `ManabaseReportTextBuilderMulliganTests` pins.

### Additive DTO fields with safe defaults
**Source:** `ManabaseMulliganEvaluation` / `OpeningHandSample` — all `{ get; init; }` with defaults (`ManabaseModels.cs:1450, 305`).
**Apply to:** new plan-keepable %, shape-verdict, curve-coverage fields — add as `init` with safe defaults so serialization/construction elsewhere is unaffected. Respect the CLAUDE.md carve-out: never convert `{ get; init; }` → `{ get; }` (breaks System.Text.Json).

### Pin-test-over-hand-built-inputs (VSTest unreliable in WSL)
**Source:** `CedhCalibrationTests.cs`; `ComputeMulliganEvaluationForTest` seam `ManabaseAnalyzer.cs:1561-1565` (bypasses Monte-Carlo by feeding hand-constructed `CardCastability` rows).
**Apply to:** shape-gate/turn-cap constants + the new metric. Add a matching internal test seam if the shape verdict is computed inside `SimulatePlanPresence` (mirror `ComputeMulliganEvaluationForTest`).

---

## No Analog Found

None. Every unit layers onto existing manabase code. The one open structural question is the F-01 fork (§2) and the PromptMutatingAnalysisFlags applicability question (§6) — both are *decisions for the planner*, not missing analogs.

---

## Metadata

**Analog search scope:** `DeckFlow.Core/Manabase/`, `DeckFlow.Web/Services/Manabase/`, `DeckFlow.Web/Services/FeatureFlags/`, `DeckFlow.Web/Services/DeckAnalysisPacketService.cs`, `DeckFlow.Web/Models/`, `DeckFlow.Web/Views/Deck/Manabase.cshtml`, `DeckFlow.Web/e2e/`, `DeckFlow.Core.Tests/Manabase/`, `DeckFlow.Web.Tests/`.
**Files scanned:** ~20 (Core sim/analyzer/models/calibration/text-builder/labels/mode, Web analysis-service/classifier/flag-catalog/flag-store/view-model/view, 2 e2e specs, 2 test files).
**Pattern extraction date:** 2026-07-14
