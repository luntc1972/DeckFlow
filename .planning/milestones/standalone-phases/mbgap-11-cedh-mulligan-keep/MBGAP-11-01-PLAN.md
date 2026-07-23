---
phase: mbgap-11-cedh-mulligan-keep
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Core/Manabase/CedhMulliganCalibration.cs
  - DeckFlow.Core/Manabase/ManabaseModels.cs
  - DeckFlow.Core.Tests/Manabase/CedhMulliganCalibrationTests.cs
autonomous: true
requirements: [MBGAP-11-AC6, MBGAP-11-D01, MBGAP-11-AC2, MBGAP-11-AC4]
must_haves:
  truths:
    - "cEDH keep turn-caps and bridge thresholds are named constants with pin tests, not magic numbers"
    - "The mulligan DTOs carry the new plan-keepable %, per-shape %, shape label, and casual curve-coverage fields with safe defaults"
    - "Adding the new fields does not change any existing serialization or construction (all additive init-only with defaults)"
  artifacts:
    - path: "DeckFlow.Core/Manabase/CedhMulliganCalibration.cs"
      provides: "TurnCapExplosive / TurnCapEngine / RepresentativeLineTurnCap / BridgeInteractionMin / BridgeDevelopmentMin constants + mode-selected accessor"
      contains: "TurnCapExplosive"
    - path: "DeckFlow.Core/Manabase/ManabaseModels.cs"
      provides: "New additive fields on ManabasePlanPresence, OpeningHandSample, ManabaseMulliganEvaluation"
      contains: "PlanKeepablePercent"
    - path: "DeckFlow.Core.Tests/Manabase/CedhMulliganCalibrationTests.cs"
      provides: "Verbatim pin tests for the new constants"
      contains: "TurnCapExplosive"
  key_links:
    - from: "CedhMulliganCalibration"
      to: "SimulatePlanPresence + ComputeMulliganEvaluation (consumed in plan 02/03)"
      via: "constant references"
      pattern: "CedhMulliganCalibration\\."
---

<objective>
Establish the CONTRACTS for the MBGAP-11 cEDH keep redesign: the calibration constants (turn
caps + bridge thresholds) and the additive DTO fields every downstream plan writes to and reads
from. This is the interface-first plan — plans 02–05 implement against these types with zero
codebase exploration.

Purpose: Pin the cEDH keep tuning knobs behind named, tested constants (Acceptance #6) and add
the null-safe DTO surface for the two-headline read (D-01), the shape verdicts, and the casual
curve-coverage metric (D-03) so the producer plans never have to touch model shape and gate copy
in the same diff.

Output: `CedhMulliganCalibration.cs` (new), extended `ManabaseModels.cs`, and a pin-test file.
No behavior change yet — nothing constructs the new fields, so build + existing suites stay green
and every existing artifact is byte-identical.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md

CODEX DISPATCH NOTE (line endings): This repo is MIXED LF/CRLF per file. Every touched file must
keep its own existing line endings byte-for-byte — detect per file, never normalize, never assume
a repo-wide style. `CedhMulliganCalibration.cs` is a NEW file: create it LF (`.gitattributes`
enforces LF for `.cs`). Change only lines whose content actually changes in the two existing files.
CLAUDE.md carve-out: never convert `{ get; init; }` → `{ get; }` (System.Text.Json drops get-only
props in .NET 9+); never inline `[Attribute]` onto the property line; preserve xmldoc single-space
indent.
</execution_context>

<context>
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-CONTEXT.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-PATTERNS.md

<interfaces>
<!-- Existing shapes the new fields sit beside. Do not restructure these records; add fields only. -->

CedhCalibration.cs:56-66 — the constant-block + selector-list pattern to mirror:
  private const int MinCommanderSamples = 10;
  private const double SafetyFloor = 22.0;
  private const double TargetCeiling = 45.0;

ManabaseModels.cs:144-159 — PlanRole flags enum (Payoff=1, Engine=2, TutorCombo=4, Interaction=8).
ManabaseModels.cs:706 — CommanderImportance enum (Central / Standard / Low).
ManabaseModels.cs:8-15 (ManabaseMode.cs) — ManabaseMode enum (Casual / Cedh).

ManabaseModels.cs:315-357 — OpeningHandSample (init-only, all defaulted): Lands, Colors, RampPieces,
  OtherCards, KeptCards, Decision, TrackedSpellName, TrackedOnCurveTurn, OnCurveCastable, HasPlan.

ManabaseModels.cs:1452-1488 — ManabaseMulliganEvaluation: KeepableHandPercent, KeepableBand,
  Kept7Percent, MulliganTo6Percent, MulliganTo5Percent, ColorCount, AverageManaValue,
  RepresentativeOpeners, PlanPresence (nullable).

ManabaseModels.cs:1535-1571 — ManabasePlanPresence: PayoffPercent, PayoffBand, PlanPresencePercent,
  Band, RolePercents (IReadOnlyDictionary<PlanRole,int>), KeepableTrials, RepresentativeOpeners.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Create CedhMulliganCalibration with turn caps + bridge thresholds</name>
  <files>DeckFlow.Core/Manabase/CedhMulliganCalibration.cs</files>
  <action>
Create a new static class `CedhMulliganCalibration` in `namespace DeckFlow.Core.Manabase`,
modeled on the `CedhCalibration` constant-block pattern (CedhCalibration.cs:56-66). Expose these
as `public const` (public so the pin test in Task 3 and the sim/analyzer in plans 02–03 read them
directly): `TurnCapExplosive = 3` (Shape A — a Payoff/TutorCombo plan card or the commander must be
deployable by this turn counting in-hand acceleration, per CONTEXT shape spec), `TurnCapEngine = 2`
(Shape B — Engine-role card castable by this turn), `RepresentativeLineTurnCap = 4` (never surface a
plan card whose on-curve turn is ≥5 as a workable representative line — the live defect), and
`BridgeInteractionMin = 2` (Shape C requires ≥2 Interaction-role cards in hand). Add
`BridgeDevelopmentMin = 2` — the minimum count of (lands + ramp pieces) a Shape-C hand must hold so
it can keep making land/rock drops while the interaction bridges (CONTEXT: "continued development").
Add xmldoc on the class and each constant citing the cEDH doctrine (median win ~turn 5, so a first
payoff later than turn 3–4 is a mulligan) and referencing D-03/CONTEXT §5. Do NOT wire these into
any caller in this plan — they are consumed in plans 02–03.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>CedhMulliganCalibration.cs exists with the five public const values and xmldoc; DeckFlow.Core builds clean.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Add additive DTO fields for plan-keepable %, shapes, and curve-coverage</name>
  <files>DeckFlow.Core/Manabase/ManabaseModels.cs</files>
  <behavior>
    - New fields are all `{ get; init; }` with safe defaults; a default-constructed record leaves every new field at 0 / empty (no throw, no required).
    - Existing construction sites (ComputeMulliganEvaluation, SimulatePlanPresence) still compile unchanged (fields are optional).
  </behavior>
  <action>
Add these additive `{ get; init; }` fields (safe defaults; NEVER `required`, NEVER get-only):

On `OpeningHandSample` (ManabaseModels.cs:315-357): add `string ShapeLabel { get; init; } = string.Empty;`
— the cEDH shape-tagged copy token ("explosive keep" / "engine keep" / "bridge keep" /
"no plan by turn 4 — mulligan"); empty in casual / when the keep-shapes read is off, so existing
openers render byte-identically.

On `ManabasePlanPresence` (ManabaseModels.cs:1535-1571): add `int PlanKeepablePercent { get; init; }`
(share of ALL trials that were mana-keepable AND passed ≥1 cEDH keep shape — denominator is trials,
so it is ≤ mana-keepable by construction, Acceptance #2), `string PlanKeepableBand { get; init; } = string.Empty;`
(high/medium/low band over PlanKeepablePercent), and three per-shape shares over keepable hands:
`int ShapeExplosivePercent`, `int ShapeEnginePercent`, `int ShapeBridgePercent`, all `{ get; init; }`
defaulting to 0. Add xmldoc on each explaining the shape (A explosive / B engine / C bridge).

On `ManabaseMulliganEvaluation` (ManabaseModels.cs:1452-1488): add `int PlanKeepablePercent { get; init; }`
and `string PlanKeepableBand { get; init; } = string.Empty;` (the cEDH second headline, mirrored up
from PlanPresence so the view/prompt read one DTO — populated only in cEDH by plan 02, else 0/empty),
and `double CurveCoverageTurns { get; init; }` (casual D-03 metric: average count of turns 1–5 with
≥1 castable play from hand; 0.0 default). Add xmldoc citing D-01 (two headlines) and D-03
(curve-coverage) and note "0/empty here leaves the existing block byte-identical" per the additive-
field pattern (ManabaseModels.cs:1485).

Do not change any existing field, existing xmldoc indentation, or attribute placement.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>All new fields present with defaults; DeckFlow.Core builds clean; no existing field altered (git diff shows only additions inside the three records).</done>
</task>

<task type="auto" tdd="true">
  <name>Task 3: Pin tests for the calibration constants</name>
  <files>DeckFlow.Core.Tests/Manabase/CedhMulliganCalibrationTests.cs</files>
  <behavior>
    - Test asserts each constant equals its calibrated default (TurnCapExplosive==3, TurnCapEngine==2, RepresentativeLineTurnCap==4, BridgeInteractionMin==2, BridgeDevelopmentMin==2).
    - Ordering invariant test: TurnCapEngine < TurnCapExplosive <= RepresentativeLineTurnCap (a shape that is stricter than the representative-line cap can never surface a non-workable line as workable).
  </behavior>
  <action>
Create `CedhMulliganCalibrationTests.cs` in `namespace DeckFlow.Core.Tests` mirroring the
verbatim-value pin style of `CedhCalibrationTests.cs`. Add `[Fact]` methods:
`Constants_MatchCalibratedDefaults` (exact-value asserts on all five constants — these are the pins
that force a deliberate test edit if anyone retunes a cap) and `Constants_SatisfyOrderingInvariant`
(assert `TurnCapEngine < TurnCapExplosive`, `TurnCapExplosive <= RepresentativeLineTurnCap`, and
`BridgeInteractionMin >= 1`, `BridgeDevelopmentMin >= 1`). Use `Assert.Equal` / `Assert.True`.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>Test file builds; the two facts assert the five constants and the ordering invariant. (VSTest is unreliable in WSL — build-clean is the gate; CI runs the assertions.)</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
No new trust boundary. This plan adds pure in-memory constants and DTO fields — no input surface,
no I/O, no package installs.

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap11-01 | Tampering | New DTO defaults deserialization | accept | All new fields are init-only value/string defaults; System.Text.Json round-trips safely (CLAUDE.md carve-out: no get-only props). No external data shapes these. |
| T-mbgap11-SC | Tampering | package installs | n/a | No package installs this phase (CLAUDE.md: no new deps without approval). |
</threat_model>

<verification>
- `dotnet build` clean on DeckFlow.Core and DeckFlow.Core.Tests.
- `git diff --stat` vs `git diff --ignore-all-space --stat` show equal line counts (no EOL churn).
- No existing field, xmldoc indent, or attribute placement changed (diff is additions only).
</verification>

<success_criteria>
- Five calibration constants exist, tested, and satisfy the ordering invariant.
- New additive DTO fields exist with safe defaults; Core + Core.Tests build clean.
- Zero behavior/artifact change (nothing constructs the new fields yet).
</success_criteria>

<output>
Create `.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-01-SUMMARY.md` when done.
</output>
