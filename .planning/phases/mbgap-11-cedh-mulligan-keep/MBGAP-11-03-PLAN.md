---
phase: mbgap-11-cedh-mulligan-keep
plan: 03
type: execute
wave: 3
depends_on: [MBGAP-11-02]
files_modified:
  - DeckFlow.Core/Manabase/CastabilitySimulator.cs
  - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseCommanderCentralityTests.cs
  - DeckFlow.Core.Tests/Manabase/CastabilitySimulatorCurveCoverageTests.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerMulliganTests.cs
autonomous: true
requirements: [MBGAP-11-AC1, MBGAP-11-AC3, MBGAP-11-AC4, MBGAP-11-D02, MBGAP-11-D03]
must_haves:
  truths:
    - "A cEDH payoff whose on-curve turn is >=5 is never surfaced as a workable representative line"
    - "Representative openers carry a shape label (explosive/engine/bridge keep, or 'no plan by turn 4 - mulligan')"
    - "For a commander-central cEDH deck the commander can be the representative opener; for a non-central deck it is not force-surfaced"
    - "Casual mode computes a curve-coverage metric: average count of turns 1-5 with >=1 castable play from hand"
  artifacts:
    - path: "DeckFlow.Core/Manabase/ManabaseAnalyzer.cs"
      provides: "Commander-centrality heuristic (D-02) + representative-opener rewrite (turn cap, commander-in-pool, shape labels)"
      contains: "IsCommanderCentral"
    - path: "DeckFlow.Core/Manabase/CastabilitySimulator.cs"
      provides: "SimulateCurveCoverage pass + shape label on plan-presence openers"
      contains: "SimulateCurveCoverage"
  key_links:
    - from: "ManabaseAnalyzer opener selection"
      to: "RepresentativeLineTurnCap"
      via: "turn-cap filter on demanding rows"
      pattern: "RepresentativeLineTurnCap"
    - from: "IsCommanderCentral"
      to: "opener pool (commander included when central)"
      via: "mode + centrality gate on the nonCommanderRows exclusion"
      pattern: "IsCommanderCentral"
    - from: "SimulateCurveCoverage"
      to: "ManabaseMulliganEvaluation.CurveCoverageTurns"
      via: "wired in ComputeMulliganEvaluation"
      pattern: "CurveCoverageTurns"
---

<objective>
Rewrite the representative-opener selection for cEDH (turn cap + commander-in-pool + shape-labeled
copy), define the auto-detected commander-centrality heuristic (D-02), and add the casual curve-
coverage metric (D-03). This is the "surfacing" layer on top of plan 02's keep gate.

Purpose: Kill the live defect (a turn-6 payoff shown as a "workable line") by capping the
representative line at RepresentativeLineTurnCap=4 and labeling every opener by its keep shape;
invert the commander exclusion for commander-central decks so the strongest cEDH keep signal (the
commander deployed ahead of curve) can surface (Acceptance #1/#3); and give casual mode its
"plays a spell on ~N of first 5 turns" frame (Acceptance #4).

Output: shape-labeled, turn-capped representative openers with commander eligibility gated on a
tested centrality heuristic; a `SimulateCurveCoverage` pass feeding
`ManabaseMulliganEvaluation.CurveCoverageTurns`. All gated so casual-non-coverage and cEDH-flag-off
paths stay byte-identical.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md

CODEX DISPATCH NOTE (line endings): MIXED LF/CRLF repo — preserve each touched file's existing line
endings exactly (per-file detect; never normalize; never assume repo-wide style). `.cs` are LF per
`.gitattributes` — verify per file. Surgical diffs in the two hot Core files. CLAUDE.md carve-outs:
preserve switch expressions; never re-indent raw-string literals; never convert init to get-only.
</execution_context>

<context>
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-CONTEXT.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-PATTERNS.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-02-SUMMARY.md

<interfaces>
<!-- Contracts to implement against — from plans 01/02 and existing code. -->

Plan 01 constants: RepresentativeLineTurnCap=4, TurnCapExplosive=3, TurnCapEngine=2.
Plan 01 DTO: OpeningHandSample.ShapeLabel (string, default empty);
  ManabaseMulliganEvaluation.CurveCoverageTurns (double, default 0.0).
Plan 02: SimulatePlanPresence now takes (mode, keepShapes) and emits shape verdicts; Analyze has a
  keepShapes param; ComputeMulliganEvaluation takes (mode, keepShapes).

ManabaseAnalyzer.cs:1480 — nonCommanderRows = castability.Where(r => !r.IsCommander) (the exclusion
  to gate on mode+centrality).
ManabaseAnalyzer.cs:1509-1533 — opener selection: demandingRows (ManaValue >= 1), ordered
  OrderBy(ManaValue).ThenBy(OnCurveTurn), first sample per Decision, Take(3); planPresence
  RepresentativeOpeners preferred when present (:1524-1525). NO turn cap today (the defect).
ManabaseAnalyzer.cs:163,672,769,1324 — CommanderImportance enum values are (Central/Standard/Low)
  [Codex LOW-1: NOT "Critical" — that value does not exist; ManabaseModels.cs:706]. commanderDriver
  = spell.IsCommander && importance != Low; CommanderColors(deck) at :1324.
CardCastability: .IsCommander (CastabilitySimulator.cs:458-460), .CastPercent, .ManaValue,
  .OnCurveTurn, .RepresentativeOpeners.
ManabaseCommandZoneFormatter.cs:20 — read pattern report.Castability.Where(c => c.IsCommander).
ManabaseAnalyzer.cs:1669 — min-commander cast% pick precedent.

CastabilitySimulator.cs:711-721 — BuildPlanOpenerSample + TallyHandComposition (add ShapeLabel here).
CastabilitySimulator.cs:483-705 — SimulatePlanPresence trial loop (DealHand + SimulateGame per-turn).
CastabilitySimulator.cs:626 — SimulateGame board primitive (walks per-turn castability; out
  firstCastableTurn) — the substrate for the curve-coverage per-turn tally.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Commander-centrality heuristic (D-02)</name>
  <files>DeckFlow.Core/Manabase/ManabaseAnalyzer.cs, DeckFlow.Core.Tests/Manabase/ManabaseCommanderCentralityTests.cs</files>
  <behavior>
    - A deck is commander-central when the commander is a meaningful early engine: CommanderImportance != Low AND the commander's command-zone cast% at its on-curve turn >= a threshold (reuse CedhSupportThreshold=88, the existing cEDH support bar) AND the commander carries a win-directed PlanRole (Payoff/Engine/TutorCombo). The bare `commanderDriver` axis (IsCommander && importance != Low) is NOT sufficient on its own -- it duplicates the importance clause and would mark any castable Standard commander central (Codex MED-3).
    - Winota-style fixture (importance Central, commander casts ~on curve, payoff/engine role) -> central == true.
    - A deck whose commander is Low importance, rarely castable on curve, OR has no win-directed role (goodstuff/value commander) -> central == false.
  </behavior>
  <action>
Add a private static helper `IsCommanderCentral(ManabaseDeck deck, IReadOnlyList<CardCastability> castability, CommanderImportance importance, ManabaseMode mode)`
to `ManabaseAnalyzer` that auto-detects commander centrality from ALREADY-computed inputs (no new
sim). Definition (make it concrete + testable): returns false when `mode != ManabaseMode.Cedh`.
Otherwise true iff ALL of: (a) `importance != CommanderImportance.Low` (the existing commanderDriver
axis, ManabaseAnalyzer.cs:769); (b) the commander's command-zone castability is strong — read
`castability.Where(c => c.IsCommander)` (pattern at ManabaseCommandZoneFormatter.cs:20) and require
its `CastPercent >= CedhSupportThreshold` (=88, the existing cEDH support bar at :17) at/by its
on-curve turn; (c) the commander carries a WIN-DIRECTED role — `deck.Spells.Any(s => s.IsCommander &&
(s.PlanRoles has Payoff|Engine|TutorCombo))`. Do NOT fall back to bare `commanderDriver` (Codex MED-3:
`commanderDriver` = IsCommander && importance != Low duplicates clause (a) and would flag any castable
Standard commander as central — keep-shapes already widens role classification, so a win-directed role
should be present). The ONLY permitted fallback is a genuinely classification-unavailable path (roles
never tagged for the whole deck, i.e. `deck.Spells.All(s => s.PlanRoles == PlanRole.None)`); in that
narrow case require clause (a)+(b) only and note the degraded read. Handle multi-commander (partners)
by taking the strongest commander row. Add xmldoc citing D-02 and the
candidate inputs it combines (command-zone cast%, commander PlanRole, CommanderImportance). Expose an
`internal static` test wrapper (mirror the ComputeMulliganEvaluationForTest seam) so it is unit-
testable over hand-built castability rows.

Write `ManabaseCommanderCentralityTests.cs` (namespace DeckFlow.Core.Tests): build a Winota-like row
set (IsCommander, high CastPercent, Payoff/Engine role, importance Central) -> assert central; build
a low-importance / low-cast% commander -> assert not central; assert casual mode is always non-central.
Boundary test: CastPercent exactly at 88 vs 87 flips centrality. Add `NonCentral_ValueCommander_NoWinRole`:
a Standard-importance, on-curve-castable commander with NO win-directed PlanRole -> NOT central (MED-3
regression — the bare commanderDriver fallback must not rescue it).
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>IsCommanderCentral helper + internal test seam exist; centrality tests build and cover the central / non-central / casual / boundary cases.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Representative-opener rewrite — turn cap, commander-in-pool, shape labels</name>
  <files>DeckFlow.Core/Manabase/ManabaseAnalyzer.cs, DeckFlow.Core/Manabase/CastabilitySimulator.cs</files>
  <behavior>
    - cEDH keep-shapes ON: no representative opener whose plan/tracked on-curve turn is >= 5 is emitted as workable; a hand with no shape gets Decision-appropriate copy "no plan by turn 4 - mulligan".
    - Each cEDH opener carries ShapeLabel: "explosive keep" (A) / "engine keep" (B) / "bridge keep" (C) / "no plan by turn 4 - mulligan".
    - Commander-central cEDH deck: commander is eligible in the opener pool and preferred as the representative line when deployable ahead of curve; non-central: commander not force-surfaced (existing exclusion preserved).
    - Casual / flag-off: opener selection byte-identical to today (no ShapeLabel, no turn cap change, commander excluded).
  </behavior>
  <action>
In `BuildPlanOpenerSample` (CastabilitySimulator.cs:711), set the new `ShapeLabel` on the emitted
`OpeningHandSample` from the hand's winning shape. Plan 02 (W2) threads the per-hand `KeepShape` value
(None/Explosive/Engine/Bridge, precedence Explosive>Engine>Bridge) INTO `BuildPlanOpenerSample` and
onto the opener-sample DTO — consume that value here; do NOT recompute the gate. Map: Explosive ->
"explosive keep", Engine -> "engine keep", Bridge -> "bridge keep", None -> "no plan by turn 4 -
mulligan". Only populate ShapeLabel when `keepShapes && mode == Cedh`; leave it empty otherwise so
casual/off openers are unchanged. (If plan 02's SUMMARY reports the KeepShape field was not added,
add it as a defaulted field here and fill it from the loop verdict — but the handoff is plan 02's
responsibility per W2.)

In `ComputeMulliganEvaluation` opener selection (ManabaseAnalyzer.cs:1509-1533): when `keepShapes &&
mode == Cedh`, (1) apply the representative-line turn cap — exclude any candidate row/sample whose
on-curve turn is `>= RepresentativeLineTurnCap + 1` (i.e. >=5) from being surfaced as a workable line
(the live defect: a turn-6 payoff must not read as workable); a hand with no in-cap shape surfaces
with the "no plan by turn 4 - mulligan" label instead of a false-positive workable line. (2) Gate the
`nonCommanderRows` exclusion (ManabaseAnalyzer.cs:1480) on centrality: when `IsCommanderCentral(...)`
is true, INCLUDE commander rows in the opener pool and PREFER the commander as the representative line
when it is deployable ahead of curve (Shape-A commander-premium from plan 02); when centrality is
false, keep the existing non-commander-only exclusion. Preserve the plan-presence-openers-win
precedence (:1524-1525) — the plan-presence samples already carry shape labels + honor the cap.

Casual and cEDH-flag-off paths: no turn cap change, no commander inclusion, no ShapeLabel — the
selection stays exactly as today (mode-branch idiom, ManabaseAnalyzer.cs:16-17). Extend
`ManabaseAnalyzerMulliganTests.cs` with: `Cedh_Turn6Payoff_NotSurfacedAsWorkable` (Acceptance #1),
`Cedh_Central_CommanderSurfacesAsOpener` + `Cedh_NonCentral_CommanderNotForced` (Acceptance #3),
`Casual_OpenerSelection_Unchanged` (byte-identity of the opener list vs flag-off).
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>Openers are turn-capped + shape-labeled in cEDH keep-shapes mode; commander eligibility gated on centrality; casual/off unchanged; solution builds; opener tests present.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 3: Casual curve-coverage metric (D-03)</name>
  <files>DeckFlow.Core/Manabase/CastabilitySimulator.cs, DeckFlow.Core/Manabase/ManabaseAnalyzer.cs, DeckFlow.Core.Tests/Manabase/CastabilitySimulatorCurveCoverageTests.cs</files>
  <behavior>
    - For a deck with cheap plays, CurveCoverageTurns is a value in [0,5] approximating average turns 1-5 with >=1 castable play from hand; a deck full of uncastable-early spells scores lower than a low-curve deck.
    - Metric runs independent of plan-role tagging (casual decks are not role-tagged) — it must NOT depend on SimulatePlanPresence.
    - keepShapes off: CurveCoverageTurns stays 0.0 (byte-identical read).
  </behavior>
  <action>
Add a dedicated pass `SimulateCurveCoverage(deck, librarySize, trials, useManaQuantity,
colorAwareMulligan, gateRampOnCastable, ritualBurst, colorlessSnow)` to `CastabilitySimulator`,
mirroring the DealHand + board-walk structure of SimulatePlanPresence but WITHOUT any plan-role
dependency (casual decks carry no PlanRoles, so this must stand alone — do not fold it into
SimulatePlanPresence). Per trial: deal a London hand, then for turns 1..5 determine whether the hand
+ simulated draws can cast at least one spell that turn.

LOW-2 (Codex — curve-coverage under-specified): `SimulateGame` (:626) tests ONE supplied cost/pip
requirement — it does NOT answer "any spell castable this turn" by itself. So the per-turn "covered"
test must iterate the deck's castable candidates: for each turn T in 1..5, consider the set of
eligible spells (non-commander, non-mana-source `deck.Spells` with `ManaValue <= T`, since a spell
above the turn's max available mana can never be the T-turn play) that are in-hand-or-drawn by turn T
(reuse the DealHand + posInPrefix drawn-by-turn check), and mark turn T covered if AT LEAST ONE such
spell is castable that turn via the board sim (color + quantity). Count each turn AT MOST ONCE (a turn
is covered or not — do not sum multiple castable spells within a turn, and do not let one spell mark
multiple turns covered). To bound cost, short-circuit on the first castable spell for the turn. Tally
covered turns (0..5) per trial; return the average across trials as a double. Keep the same StableSeed
reproducibility convention SimulatePlanPresence uses.

Wire it into `ComputeMulliganEvaluation`: when `keepShapes` is on (D-03 rides the same flag; runs in
BOTH modes but is the CASUAL-facing headline frame), compute and set
`ManabaseMulliganEvaluation.CurveCoverageTurns`. When off, leave it 0.0. (The Analyze call site must
pass the deck/sim params through — reuse the same useManaQuantity/colorAwareMulligan/ritualBurst/etc.
values already passed to SimulatePlanPresence at ManabaseAnalyzer.cs:254-257.) Keep the extra sim
gated so flag-off adds no work.

Write `CastabilitySimulatorCurveCoverageTests.cs`: a low-curve deck (many 1-2 MV castable spells)
scores high (near 4-5); a top-heavy deck (few early plays) scores materially lower; assert the
ordering (low-curve coverage > top-heavy coverage) and that the value is within [0,5]. Add a
`CurveCoverage_FlagOff_IsZero` fact via ComputeMulliganEvaluationForTest.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>SimulateCurveCoverage exists, is role-independent, feeds CurveCoverageTurns when the flag is on; 0.0 when off; ordering + range + off tests build.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
No new trust boundary. Pure Core computation over the validated deck model.

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap11-04 | Denial of Service | SimulateCurveCoverage extra pass | mitigate | Runs only when keepShapes on; single lightweight DealHand+board walk per trial, same trial count as existing passes; off adds zero work. |
| T-mbgap11-05 | Information disclosure | Commander surfaced in opener when central | accept | Commander name is already public deck data the user pasted; centrality gate only changes which existing row is surfaced, not what data exists. |
| T-mbgap11-SC | Tampering | package installs | n/a | No package installs this phase. |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean.
- EOL: `git diff --stat` == `git diff --ignore-all-space --stat` for both Core files; per-file `\r`
  counts match `git show HEAD:<path>`.
- Casual + cEDH-flag-off: opener list, ShapeLabel (empty), and CurveCoverageTurns (0.0) identical to
  pre-change output.
</verification>

<success_criteria>
- No >=5-turn payoff surfaces as workable in cEDH keep-shapes mode (AC1).
- Openers shape-labeled; commander eligibility gated on the tested centrality heuristic (AC3/D-02).
- Casual curve-coverage metric computed, role-independent, flag-gated (AC4/D-03).
- Solution builds; casual/off paths byte-identical.
</success_criteria>

<output>
Create `.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-03-SUMMARY.md` when done.
</output>
