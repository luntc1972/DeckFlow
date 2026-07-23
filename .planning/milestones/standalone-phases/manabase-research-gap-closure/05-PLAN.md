---
phase: manabase-research-gap-closure
plan: 05
type: execute
wave: 5
depends_on: ["04"]
files_modified:
  - DeckFlow.Core/Manabase/KarstenManabase.cs
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs
  - DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs
  - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs
  - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs
  - DeckFlow.CLI/CedhCalibrateCommandRunner.cs
  - DeckFlow.Core/Manabase/CedhCalibration.cs
  - DeckFlow.Core.Tests/Manabase/CedhLandTargetHybridTests.cs
  - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs
  - DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs
  - docs/manabase-analysis-rules.md
  - README.md
autonomous: false
requirements: [MBGAP-03]
must_haves:
  truths:
    - "KarstenManabase applies a cEDH-only ritual land-target credit computed from the deck's already-classified OneShots list (no re-classification)"
    - "The credit is a named calibration constant (RitualLandCreditWeight ~0.5 per net-positive ritual, capped), not a magic number"
    - "New flag analysis.manabase.ritual-land-credit is registered and seeded FALSE/0 in both dialects — NOT folded into ritual-burst-mana (D-10)"
    - "With the flag OFF, land targets are byte-identical to before"
    - "The cedh-land-calibrate CLI reports a third column (target with ritual credit) so the constant can be tuned against the 1597-deck corpus"
    - "The calibration harness is RUN against the 1597-deck corpus and the before/after under-flag% delta is documented (D-09: the data decides the constant) — BLOCKING; if the corpus is unavailable the plan halts at a checkpoint, it does not complete"
  artifacts:
    - path: "DeckFlow.Core/Manabase/KarstenManabase.cs"
      provides: "ritual land-target credit term + RitualLandCreditWeight const"
      contains: "RitualLandCreditWeight"
    - path: "DeckFlow.CLI/CedhCalibrateCommandRunner.cs"
      provides: "third target column with ritual credit"
      contains: "RitualCredit"
  key_links:
    - from: "KarstenManabase.CedhLandTarget"
      to: "deck.OneShots"
      via: "sum net-positive rituals × RitualLandCreditWeight, capped, subtract from target when flag+cEDH"
      pattern: "OneShots"
---

<objective>
Implement MBGAP-03 / RIT O-4 (D-09/D-10): a cEDH-only ritual land-target credit that
reduces the recommended land count for ritual-fueled lists, shipped behind a NEW flag
`analysis.manabase.ritual-land-credit` (OFF), calibrated via the existing
`cedh-land-calibrate` harness — following the exact `cedh-land-target` precedent that set
floor 22 / blend 0.5.

Critical (D-10): do NOT reuse `analysis.manabase.ritual-burst-mana` — it is already ON in
prod and folding land-target changes into it would move live decks' land counts the moment
the code deploys.

Critical (D-09): the credit constant is DATA-decided. Running the calibration harness against
the 1597-deck corpus and documenting the before/after under-flag% delta is a BLOCKING part of
this plan — shipping the default 0.5 with calibration deferred does NOT count as MBGAP-03
complete. If the corpus is unavailable in the execution environment, the plan halts at the
Task 4 checkpoint (autonomous: false); it does not silently complete.

Purpose: closes the deferred RIT O-4 land-target credit.
Output: KarstenManabase credit term + named constant, new flag OFF in both dialects,
analyzer/service threading, calibration-harness third column, a RUN calibration delta,
tests, docs + README.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/phases/manabase-research-gap-closure/CONTEXT.md
@.planning/phases/manabase-research-gap-closure/manabase-research-gap-closure-PATTERNS.md
@.planning/captures/manabase-ritual-burst-mana-spec.md

<interfaces>
<!-- Templates (extracted from source). -->

KarstenManabase.cs:
- named constants CedhSafetyFloor=22.0, CedhDisabledFloor=28.0, CedhTargetCeiling=45.0, CedhBaselineBlendWeight=0.5 (Kar:36-42)
- public static double CedhLandTarget(int totalCards, int commanderCount, double averageManaValue, double rampAndDrawUnderThree, double fastMana, CedhLandContext context) (Kar:93-124) — insertion point for the credit term (apply before the final Math.Clamp)
- deck.OneShots already populated by DetectOneShotBurstMana (ManabaseModels.cs:427; OneShotMana record has NetMana/Colors/OwnCost)

Flag pattern (identical to plan 04, different key):
- FeatureFlagCatalog.cs — add ["analysis.manabase.ritual-land-credit"] = "<... cEDH only; off = byte-identical output.>"
- FeatureFlagStore.cs — PG `('analysis.manabase.ritual-land-credit', FALSE),` + SQLite `('analysis.manabase.ritual-land-credit', 0),`
- ManabaseAnalysisService.cs — RitualLandCreditFlagKey const + IsFlagOn read + thread into Analyze; ManabaseAnalyzer.Analyze trailing `bool ritualLandCredit = false`
- Note: CedhLandTarget needs the OneShots + flag; the analyzer already gates cEDH via `mode == ManabaseMode.Cedh` (Analyzer.cs:172 ritualBurstActive pattern) — mirror that for `ritualLandCreditActive = ritualLandCredit && mode == Cedh`

CedhCalibrateCommandRunner.cs (Runner:27-150) + CedhCalibration.cs:
- existing rows compute oldTarget vs newTarget; extend CedhCalibrationRow + Build + RenderMarkdown with a third column newTargetWithRitualCredit reading classifiedDeck.OneShots

Pitfall 1 (RESEARCH): double-crediting rituals (land-target credit AND sim burst). Decide explicitly in the plan whether a
ritual counted toward the land target stays eligible for the sim burst. DECISION: yes — land target is strategic, sim burst is
tactical per-cast; they address different objects (state this in docs).
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Ritual land-target credit term + tests</name>
  <behavior>
    - A cEDH deck with 3 net-positive rituals and flag ON → land target reduced by ~min(cap, 3 × RitualLandCreditWeight), still Math.Clamp'd to [22,45]
    - The same deck with flag OFF → land target unchanged (byte-identical to pre-phase)
    - A non-cEDH (singleton/60-card) deck → no credit applied regardless of flag
    - A cEDH deck with 0 rituals → no change
    - The credit never pushes the target below CedhSafetyFloor (22)
  </behavior>
  <read_first>
    - DeckFlow.Core/Manabase/KarstenManabase.cs (Kar:36-124 — constants + CedhLandTarget)
    - DeckFlow.Core/Manabase/ManabaseModels.cs (OneShotMana record + ManabaseDeck.OneShots :427)
    - DeckFlow.Core.Tests/Manabase/CedhLandTargetHybridTests.cs (existing hybrid-target constant tests to extend)
  </read_first>
  <action>
    (a) Add `private const double RitualLandCreditWeight = 0.5;` and a cap const (e.g. `RitualLandCreditCap = 3.0`, planner
    default — "capped" per D-09) to KarstenManabase.cs alongside the existing Cedh* constants.
    (b) Add an overload/parameter path so CedhLandTarget can receive the ritual count (or the OneShots list) and a
    `bool ritualLandCredit` gate; compute `credit = Math.Min(RitualLandCreditCap, netPositiveRitualCount × RitualLandCreditWeight)`
    and subtract it from `target` BEFORE the final `Math.Clamp(target, CedhSafetyFloor, CedhTargetCeiling)` so the floor still
    holds. Only apply when the gate is true AND cEDH context is enabled. Keep the existing signature working via a
    trailing-optional/overload so all current callers stay byte-identical.
    (c) Extend CedhLandTargetHybridTests.cs with the five <behavior> cases. Use the existing constant-based test style in that file.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CedhLandTargetHybrid" 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c "RitualLandCreditWeight" DeckFlow.Core/Manabase/KarstenManabase.cs` returns >= 1
    - credit is subtracted before Math.Clamp (floor 22 still enforced) — a test asserts target never < 22
    - flag-OFF and non-cEDH paths unchanged; all five behavior tests pass
    - `dotnet build DeckFlow.sln` 0/0
  </acceptance_criteria>
  <done>cEDH ritual land-credit term in place, capped, floor-safe, tested.</done>
</task>

<task type="auto">
  <name>Task 2: Register ritual-land-credit flag + thread + parity</name>
  <read_first>
    - DeckFlow.Web/Services/FeatureFlags/FeatureFlagCatalog.cs, FeatureFlagStore.cs (same locations as plan 04)
    - DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs (flag consts + Analyze call)
    - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs (Analyze overload :138-149, cEDH gating :172)
    - DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs, DeckFlow.Web.Tests/Manabase/ManabaseAnalysisServiceTests.cs
  </read_first>
  <action>
    (a) Add catalog description for "analysis.manabase.ritual-land-credit" (cEDH only; "Off = byte-identical output."), explicitly
    noting it is SEPARATE from ritual-burst-mana (D-10).
    (b) Seed FALSE (PG) + 0 (SQLite) in FeatureFlagStore.cs.
    (c) RitualLandCreditFlagKey const + IsFlagOn read in ManabaseAnalysisService.cs; thread `ritualLandCredit:` into the Analyze
    call. Add trailing-optional `bool ritualLandCredit = false` to ManabaseAnalyzer.Analyze and compute
    `ritualLandCreditActive = ritualLandCredit && mode == ManabaseMode.Cedh` mirroring the ritualBurstActive pattern; feed it to
    the CedhLandTarget credit path from Task 1.
    (d) FeatureFlagCatalogTests InlineData for the new key + seed-parity test if FeatureFlagStoreSeedTests exists.
    (e) ManabaseAnalysisServiceTests parity: ritual-heavy cEDH deck byte-identical land target with flag OFF, reduced with flag ON.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~FeatureFlagCatalog|FullyQualifiedName~ManabaseAnalysisService" 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c "ritual-land-credit" DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` returns >= 2 (PG + SQLite)
    - the flag is distinct from ritual-burst-mana (both keys present, different names)
    - parity test proves flag-OFF byte-identical land target, flag-ON reduced; passes
    - `dotnet build DeckFlow.sln` 0/0
  </acceptance_criteria>
  <done>New flag OFF in both dialects, threaded cEDH-gated, parity proven.</done>
</task>

<task type="auto">
  <name>Task 3: Calibration-harness third column + BLOCKING run + docs/README</name>
  <read_first>
    - DeckFlow.CLI/CedhCalibrateCommandRunner.cs (RunAsync deck-replay loop :27-150)
    - DeckFlow.Core/Manabase/CedhCalibration.cs (CedhCalibrationRow / Build / RenderMarkdown)
    - docs/manabase-analysis-rules.md, README.md
    - .planning/captures/manabase-cedh-land-target-phaseB-PLAN.md (acceptance-bar precedent: no re-opened under-flag regression, grindy decks stay healthy)
  </read_first>
  <action>
    (a) Extend CedhCalibrationRow + CedhCalibration.Build + RenderMarkdown with a third target column
    `newTargetWithRitualCredit`, computed from classifiedDeck.OneShots via the Task-1 credit path. Extend
    CedhCalibrateCommandRunner.RunAsync to populate it. Do not build a parallel harness (reuse the existing 1597-deck replay).
    (b) RUN the harness against the cached 1597-deck corpus and record the before/after under-flag% delta in the SUMMARY. Per
    D-09 the credit constant is DATA-decided, so this run is a BLOCKING requirement of the plan — do NOT ship the default 0.5 and
    call MBGAP-03 complete without the documented delta. If the cached corpus/data files are UNAVAILABLE in the execution
    environment, do NOT mark this as a deferred manual verification: stop and surface the blocker at the Task 4 checkpoint
    (MBGAP-03 stays open, the flag stays OFF, no completion).
    (c) Update docs/manabase-analysis-rules.md: document the ritual land-target credit (constant, cap, cEDH-only, flag OFF,
    SEPARATE from ritual-burst-mana), the calibration delta, and the double-credit decision (ritual stays eligible for the sim
    burst — different objects). Update README where manabase flags are listed. Changed lines only, LF.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.sln 2>&1 | tail -5; dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CedhCalibration" 2>&1 | tail -10</automated>
  </verify>
  <acceptance_criteria>
    - CedhCalibration report renders a third ritual-credit target column (`grep -c "RitualCredit" DeckFlow.Core/Manabase/CedhCalibration.cs` >= 1)
    - Harness RUN before/after under-flag% delta recorded in SUMMARY (BLOCKING — if the corpus is unavailable the plan halts at the Task 4 checkpoint, it does NOT complete)
    - docs/manabase-analysis-rules.md documents the credit + calibration delta + double-credit decision; README updated
    - `dotnet build DeckFlow.sln` 0/0; no EOL churn
  </acceptance_criteria>
  <done>Calibration harness reports the ritual-credit column; docs+README updated; before/after delta captured (or blocked at checkpoint if corpus unavailable).</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <what-built>The cEDH-only ritual land-target credit (RitualLandCreditWeight, default 0.5, capped) shipped behind a new OFF flag analysis.manabase.ritual-land-credit (SEPARATE from ritual-burst-mana per D-10), and the cedh-land-calibrate harness was extended with the third ritual-credit target column and RUN against the 1597-deck cEDH corpus.</what-built>
  <how-to-verify>
    1. Open the plan-05 SUMMARY and confirm it records the harness before/after under-flag% delta against the 1597-deck corpus — this is the D-09 calibration evidence proving the credit constant is data-decided, not assumed.
    2. Confirm the delta shows no re-opened under-flag regression and grindy decks stay healthy (the cedh-land-target acceptance bar).
    3. If the corpus/data files were UNAVAILABLE: the SUMMARY must say so explicitly. In that case MBGAP-03 is NOT complete — the credit stays OFF and the calibration run is an outstanding BLOCKER, not a deferred nicety. Do not sign off as complete.
  </how-to-verify>
  <resume-signal>Type "approved" if the calibration delta is captured and within the acceptance bar, or "blocked" if the corpus was unavailable (MBGAP-03 remains open pending the run).</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| operator → flag store | ritual-land-credit seeded OFF; only an operator flip changes live land targets (D-10) |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap03-01 | Tampering | folding credit into live ritual-burst-mana flag | mitigate | new SEPARATE flag OFF (D-10); parity test proves OFF byte-identical |
| T-mbgap03-02 | Repudiation | double-credit inflates resource base | mitigate | explicit double-credit decision documented; calibration delta reviewed at the blocking checkpoint before flip |
| T-mbgap03-SC | Tampering | NuGet installs | accept | No new packages this plan |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean; full `dotnet test DeckFlow.sln` green.
- Flag OFF byte-identical land targets; ON reduces for ritual-heavy cEDH lists, floor 22 held.
- Calibration harness reports the third column AND is run; under-flag% delta recorded (no re-opened regression per acceptance bar) — verified at the blocking checkpoint.
</verification>

<success_criteria>
cEDH-only ritual land-target credit shipped behind a new OFF flag (separate from ritual-burst-mana), byte-identical when off, floor-safe, calibrated via the extended harness with the before/after delta captured and human-reviewed at the blocking checkpoint (or the plan explicitly blocked if the corpus was unavailable), docs + README updated. MBGAP-03 complete (pending operator flip) only after checkpoint approval.
</success_criteria>

<output>
Create `.planning/phases/manabase-research-gap-closure/05-SUMMARY.md` when done.
</output>
