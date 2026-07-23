---
phase: mbgap-11-cedh-mulligan-keep
plan: 02
type: execute
wave: 2
depends_on: [MBGAP-11-01]
files_modified:
  - DeckFlow.Core/Manabase/CastabilitySimulator.cs
  - DeckFlow.Core/Manabase/ManabaseAnalyzer.cs
  - DeckFlow.Core.Tests/Manabase/CastabilitySimulatorKeepShapeTests.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerMulliganTests.cs
autonomous: true
requirements: [MBGAP-11-AC1, MBGAP-11-AC2, MBGAP-11-AC5, MBGAP-11-F01]
must_haves:
  truths:
    - "A cEDH keepable hand passes the plan-keep gate only when it satisfies Shape A, B, or C"
    - "Shape A credits in-hand acceleration: a hand with rocks that deploys a payoff by turn <=3 counts even if the payoff's printed MV lands later on a land-only curve"
    - "plan-keepable % <= mana-keepable % by construction (numerator is a subset of keepable hands, denominator is trials)"
    - "Shape C counts SpellRequirement.IsInteractionSpell (pre-gate), NOT the Interaction PlanRole, so non-permanent counterspells qualify (Codex HIGH-1)"
    - "Commander spells are excluded from the drawable library; commander keep-credit comes only from the command-zone premium path (Codex HIGH-2)"
    - "ComputeMulliganEvaluation and SimulatePlanPresence receive the deck mode + the keep-shapes flag; casual path is byte-identical when the flag is off"
  artifacts:
    - path: "DeckFlow.Core/Manabase/CastabilitySimulator.cs"
      provides: "Three-shape keep gate + plan-keepable tally + per-shape percents inside SimulatePlanPresence"
      contains: "ShapeExplosive"
    - path: "DeckFlow.Core/Manabase/ManabaseAnalyzer.cs"
      provides: "mode + keepShapes threaded into ComputeMulliganEvaluation and SimulatePlanPresence; Analyze gains the keepShapes param"
      contains: "keepShapes"
  key_links:
    - from: "ManabaseAnalyzer.Analyze"
      to: "SimulatePlanPresence / ComputeMulliganEvaluation"
      via: "mode + keepShapes arguments"
      pattern: "SimulatePlanPresence\\("
    - from: "SimulatePlanPresence shape gate"
      to: "ManabaseMulliganEvaluation.PlanKeepablePercent"
      via: "ManabasePlanPresence.PlanKeepablePercent surfaced up in cEDH"
      pattern: "PlanKeepablePercent"
---

<objective>
Implement the cEDH three-shape keep gate (F-01 option (a): extend `SimulatePlanPresence`) and thread
the deck `mode` + new `keepShapes` flag into the mulligan computation. A mana-keepable hand becomes
plan-keepable iff it satisfies Shape A (explosive), B (early engine), or C (interaction bridge),
each measured with the calibrated turn caps from plan 01.

Purpose: Fix the architectural root cause — one Karsten mana-functionality heuristic serving two
formats. The mana floor stays (necessary but insufficient); this layers the cEDH plan-quality gate
on top and produces the second headline (plan-keepable %, D-01 / Acceptance #2), crediting in-hand
acceleration so a rock-fueled turn-3 payoff counts (Acceptance #5).

Output: shape-gated per-hand verdicts + plan-keepable/shape percents on `ManabasePlanPresence`,
surfaced as `ManabaseMulliganEvaluation.PlanKeepablePercent` in cEDH. Opener/representative-line copy
and casual curve-coverage come in plan 03; view/prompt/flag come in plans 04–05.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md

CODEX DISPATCH NOTE (line endings): MIXED LF/CRLF repo — preserve each touched file's existing line
endings exactly (detect per file; do NOT normalize; do NOT assume repo-wide style). `.cs` files are
LF per `.gitattributes` but verify per file. Change only the lines whose content changes.
`CastabilitySimulator.cs` and `ManabaseAnalyzer.cs` are the hottest, most-tested files in the
analyzer — keep diffs surgical. CLAUDE.md carve-outs: preserve switch expressions, never re-indent
C# raw-string literals, never convert `{ get; init; }` → `{ get; }`.
</execution_context>

<context>
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-CONTEXT.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-PATTERNS.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-01-SUMMARY.md

<interfaces>
<!-- Extracted contracts — implement directly, no exploration needed. -->

Plan 01 constants (CedhMulliganCalibration.cs): TurnCapExplosive=3, TurnCapEngine=2,
  BridgeInteractionMin=2, BridgeDevelopmentMin=2, RepresentativeLineTurnCap=4 (used in plan 03).

Plan 01 DTO fields:
  ManabasePlanPresence: + PlanKeepablePercent, PlanKeepableBand, ShapeExplosivePercent,
    ShapeEnginePercent, ShapeBridgePercent (all int / string, defaulted).
  ManabaseMulliganEvaluation: + PlanKeepablePercent, PlanKeepableBand (int/string).

CastabilitySimulator.cs:483-705 — SimulatePlanPresence(deck, librarySize, trials, useManaQuantity,
  colorAwareMulligan, gateRampOnCastable, ritualBurst, colorlessSnow). The trial loop already:
  - deals a London hand + buckets by kept size (:572-576);
  - per plan card, tests "drawn by on-curve turn" via posInPrefix (:604-621) AND "castable then"
    via SimulateGame reusing the board (:624-640) — SimulateGame signature at :626 takes
    (library, shuffled, active, handCount, targetTurn, planTurn, pips, availableColors, null,
    onlineLandMasks, gateRampOnCastable, ritualBurst, out ..., out int firstCastableTurn, out ...);
  - OR's roles into rolesThisHand (:633); tallies roleCounts per single role (:642-656).
  Library card: LibraryCard.PlanRoles, .PlanPips, .PlanName, .PlanManaValue, .IsPlanCard.

  COMMANDER-IN-LIBRARY (Codex plan-review HIGH-2): the `source.IsCommander` skip at
  CastabilitySimulator.cs:962 applies ONLY to ManaSource cards. `BuildLibrary` (:847) adds ANY
  plan-tagged `SpellRequirement` with NO commander exclusion, and commanders ARE in `deck.Spells`
  (ManabaseClassifier.cs:160). So today a plan-tagged commander is BOTH drawable in the library AND
  reachable via the command-zone path -- Task 1b must exclude commander spells from the drawable
  library to avoid double-counting / false commander keeps.

  INTERACTION ROLE IS GATED (Codex plan-review HIGH-1): `PlanRoleClassifier` STRIPS
  `PlanRole.Interaction` from non-permanent instants/sorceries via the permanent gate
  (PlanRoleClassifier.cs:72,89) -- so Force of Will / Swan Song / counterspells do NOT carry the
  Interaction role. The pre-gate truth survives as `SpellRequirement.IsInteractionSpell`
  (ManabaseModels.cs:203, populated by Web at ManabaseAnalysisService.cs:763). Shape C MUST count
  `IsInteractionSpell`, NOT the Interaction PlanRole. `LibraryCard` does NOT carry IsInteractionSpell
  today -- Task 1b adds it. SpellRequirement.IsInteractionSpell = ManabaseModels.cs:203 (bool).

ManabaseModels.cs:144-159 — PlanRole (Payoff=1, Engine=2, TutorCombo=4, Interaction=8).

ManabaseAnalyzer.cs:254-257 — SimulatePlanPresence call site (mode IS in scope here).
ManabaseAnalyzer.cs:283 — ComputeMulliganEvaluation call site (mode in scope, used at :271/:285).
ManabaseAnalyzer.cs:160-175 — Analyze(...) signature (mode param already present; add keepShapes).
ManabaseAnalyzer.cs:1474-1547 — ComputeMulliganEvaluation body.
ManabaseAnalyzer.cs:1561-1565 — ComputeMulliganEvaluationForTest seam (add mode+keepShapes params).
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Thread mode + keepShapes through Analyze -> ComputeMulliganEvaluation -> SimulatePlanPresence</name>
  <files>DeckFlow.Core/Manabase/ManabaseAnalyzer.cs, DeckFlow.Core/Manabase/CastabilitySimulator.cs</files>
  <action>
Add a `bool keepShapes = false` parameter to `ManabaseAnalyzer.Analyze` (ManabaseAnalyzer.cs:160-175),
placed after `cedhContext` or grouped with the other feature bools — keep it defaulted so existing
callers (and the `Analyze(deck)` / `Analyze(deck, mode)` overloads) stay valid. Thread it to the two
computations below. The Web service passes the resolved flag in plan 04.

Change `ComputeMulliganEvaluation` (ManabaseAnalyzer.cs:1474) to accept `ManabaseMode mode` and
`bool keepShapes` (add both params). Update the production call at :283 to pass the in-scope `mode`
and the new `keepShapes`. Update the `ComputeMulliganEvaluationForTest` seam (:1561-1565) to accept
and forward `mode` + `keepShapes` (default them so existing per-test callers can opt in). Do the same
for `SimulatePlanPresence`: add `ManabaseMode mode` and `bool keepShapes` params (defaulted), and
pass them at the :254-257 call site. When `keepShapes` is false OR `mode != ManabaseMode.Cedh`, the
shape gate in Task 2 must be a no-op (compute nothing new, leave the new fields at their defaults) so
the artifact is byte-identical to today — mirror the mode-branch idiom at ManabaseAnalyzer.cs:16-17
(`mode == ManabaseMode.Cedh ? ... : ...`).

Grep for every other caller of `ComputeMulliganEvaluation`, `ComputeMulliganEvaluationForTest`, and
`SimulatePlanPresence` across the solution (Core, Web, both test projects) and update each to the new
signatures (defaulted params minimize churn). List them in the SUMMARY.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>Analyze/ComputeMulliganEvaluation/SimulatePlanPresence/test-seam all accept mode+keepShapes; whole solution builds clean; no caller left on the old signature.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 1b: Fix library membership -- carry IsInteractionSpell into LibraryCard, exclude commander spells from drawable library (Codex HIGH-1 + HIGH-2)</name>
  <files>DeckFlow.Core/Manabase/CastabilitySimulator.cs</files>
  <behavior>
    - LibraryCard exposes IsInteractionSpell copied from the source SpellRequirement (pre-gate truth), so a non-permanent counterspell (Interaction role stripped) is still countable for Shape C.
    - A plan-tagged commander spell is NOT added to the drawable library: it cannot be "drawn" into an opening hand, and is handled solely via the command-zone premium path in Task 2.
    - Casual / flag-off behavior is byte-identical: the library-membership change (commander exclusion) applies unconditionally BUT commander plan-cards being drawable was itself a latent bug; add a pin test asserting the commander spell is absent from the built library so the exclusion is provably scoped and does not alter non-commander library contents.
  </behavior>
  <action>
Add a `bool IsInteractionSpell` field/property to the `LibraryCard` struct/record (alongside PlanRoles)
and populate it in `BuildLibrary` (CastabilitySimulator.cs:847) from the source
`SpellRequirement.IsInteractionSpell` (ManabaseModels.cs:203). This is the pre-gate interaction truth
Shape C needs (HIGH-1) — the Interaction PlanRole is unreliable for non-permanent instants/sorceries.

In `BuildLibrary` (:847), when enumerating plan-tagged `SpellRequirement`s to add to the drawable
library, SKIP any `spell.IsCommander` card (HIGH-2). The commander is never drawn into an opening hand;
it is only ever deployed from the command zone, which Task 2 handles via the always-available
commander-premium `SimulateGame` path. Leaving the commander in the library lets it be "drawn" as a
plan filler AND counted again by the command-zone path -> double-count / false commander keeps. Keep
the exclusion surgical (a single `if (spell.IsCommander) continue;` at the add site); do NOT touch the
ManaSource path at :962 (already correct for mana sources).

Update every existing consumer of LibraryCard construction so the new field compiles (defaulted where
a call site legitimately has no interaction context).
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>LibraryCard carries IsInteractionSpell; commander plan-cards excluded from the drawable library; Core builds clean.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Three-shape keep gate + plan-keepable tally in SimulatePlanPresence</name>
  <files>DeckFlow.Core/Manabase/CastabilitySimulator.cs</files>
  <behavior>
    - Shape A: a hand holding a Payoff OR TutorCombo plan card whose SimulateGame first-castable-turn <= TurnCapExplosive (3) passes — even when the card's printed MV would land it later on a land-only curve (in-hand rocks/rituals credited by the existing board sim). Pin: hand with Sol Ring + a 4-MV payoff casts it turn 3 -> Shape A true.
    - Shape B: a hand with an Engine-role card whose first-castable-turn <= TurnCapEngine (2) passes.
    - Shape C: a hand holding >= BridgeInteractionMin (2) distinct cards with IsInteractionSpell==true (pre-gate truth; NOT the Interaction PlanRole -- see HIGH-1) drawn/holdable within the window AND (lands + rampPieces) >= BridgeDevelopmentMin (2) passes. Pin: a hand with 2 non-permanent counterspells (Interaction role stripped by the permanent gate) + 2 lands is Shape C.
    - A mana-keepable hand with none of A/B/C does NOT increment planKeepable.
    - PlanKeepablePercent = round(100 * planKeepableHands / trials); by construction <= mana-keepable %.
    - Flag/mode off: all new tallies stay 0 and no extra SimulateGame calls run (byte-identical, no perf regression).
  </behavior>
  <action>
Inside `SimulatePlanPresence`, when `keepShapes && mode == ManabaseMode.Cedh`, compute a per-hand
shape verdict for each KEEPABLE hand (countsForStats, keptSize >= 6), reusing the existing per-plan-
card loop machinery (:604-640) rather than adding a second trial loop (F-01 option (a) — no new
Core/Web crossing; roles already arrive via LibraryCard.PlanRoles).

Shape A (explosive): for each plan card whose role includes Payoff or TutorCombo, call the existing
`SimulateGame` board primitive (:626) but with the target turn set to `CedhMulliganCalibration.TurnCapExplosive`
(NOT the card's printed planTurn) and accept the card when `firstCastableTurn <= TurnCapExplosive`.
This is the acceleration-crediting change (Acceptance #5): the board sim already deploys in-hand
rocks/dorks/rituals, so a 4-MV payoff powered out turn 3 qualifies. ALSO evaluate the commander-
premium path: read the commander requirement(s) from `deck.Spells.Where(s => s.IsCommander)` (the
commander is not in the library but its cost/pips are on the deck), build its pips, and call
`SimulateGame` for the commander with target turn `min(TurnCapExplosive, commanderMv - 1)` — the
commander is always available from the command zone, so no drawn-by-turn gate applies; if castable by
that turn the hand is Shape A ("commander >=1 turn ahead of printed curve"). (Commander-centrality
gating — whether the commander premium is force-surfaced as the representative line — is plan 03's
job; here the commander-premium path simply contributes to the Shape-A tally.)

Shape B (early engine): a plan card whose role includes Engine and whose SimulateGame first-castable-
turn <= `TurnCapEngine` (target turn = TurnCapEngine).

Shape C (interaction bridge): count distinct plan cards with `IsInteractionSpell == true` (the pre-gate
truth carried onto LibraryCard in Task 1b — NOT the Interaction PlanRole, which is stripped from
non-permanent counterspells, HIGH-1) the hand holds/draws within the window (reuse the posInPrefix
drawn-by-turn check, bounded by RepresentativeLineTurnCap so a card only counts if it arrives in the
relevant window); if that count >= `BridgeInteractionMin` AND the kept hand's (lands + rampPieces) >=
`BridgeDevelopmentMin` (reuse TallyHandComposition, already called in BuildPlanOpenerSample at :721),
the hand is Shape C.

Tally: increment `planKeepable` once per keepable hand where `shapeA || shapeB || shapeC`; also keep
three separate counters (explosive/engine/bridge) for the per-shape percents.

W2 (per-hand shape handoff to plan 03): retain the WINNING shape for each sampled hand as a loop
local. Compute a `KeepShape` enum/flag value (None / Explosive / Engine / Bridge — precedence
Explosive > Engine > Bridge when a hand satisfies more than one) inside this loop, and thread it to
`BuildPlanOpenerSample` (:721) so plan 03 can stamp the representative-opener `ShapeLabel` WITHOUT
recomputing the gate. Cover the mull-to-5 depth bucket too: where `countsForStats` and `wantSample`
diverge, still compute the shape for a sampled hand even if it does not count toward stats, so the
representative line is always labeled. Add a `KeepShape` field to the opener-sample DTO/struct that
BuildPlanOpenerSample fills (plan 01 added the sample DTO; if the field is missing, add it here as a
defaulted addition and note it in the SUMMARY for plan 03). After the loop compute
`PlanKeepablePercent = trials > 0 ? round(100 * planKeepable / trials) : 0` (denominator = trials, so
it is directly comparable to and <= mana-keepable %), a band via the same high>=85/medium>=70/low
switch used for KeepableHandPercent, and the three shape percents over `keepable` (mirror the
rolePercents computation at :688-692). Emit them on the returned `ManabasePlanPresence`
(PlanKeepablePercent, PlanKeepableBand, ShapeExplosivePercent, ShapeEnginePercent, ShapeBridgePercent).
When `!keepShapes || mode != Cedh`, skip all of the above — the new fields stay at their defaults and
no extra SimulateGame calls run. Add xmldoc citing the shape spec and CONTEXT §5.1.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core/DeckFlow.Core.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>SimulatePlanPresence emits PlanKeepablePercent + shape percents in cEDH keep-shapes mode; off/casual path unchanged; Core builds clean.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 3: Surface plan-keepable in cEDH + keep-shape unit tests</name>
  <files>DeckFlow.Core/Manabase/ManabaseAnalyzer.cs, DeckFlow.Core.Tests/Manabase/CastabilitySimulatorKeepShapeTests.cs, DeckFlow.Core.Tests/Manabase/ManabaseAnalyzerMulliganTests.cs</files>
  <behavior>
    - In cEDH keep-shapes mode, ManabaseMulliganEvaluation.PlanKeepablePercent == planPresence.PlanKeepablePercent and PlanKeepablePercent <= KeepableHandPercent.
    - In casual or flag-off, PlanKeepablePercent == 0 / PlanKeepableBand empty (byte-identical read).
  </behavior>
  <action>
In `ComputeMulliganEvaluation`, when `keepShapes && mode == ManabaseMode.Cedh` and `planPresence` is
non-null, populate the new `PlanKeepablePercent` / `PlanKeepableBand` on the returned
`ManabaseMulliganEvaluation` from `planPresence`. Otherwise leave them at defaults (0 / empty).

Write `CastabilitySimulatorKeepShapeTests.cs` (namespace DeckFlow.Core.Tests) as deterministic
hand-built / small-deck tests over `SimulatePlanPresence` in cEDH keep-shapes mode:
  - `ShapeA_CreditsInHandAcceleration_PayoffByTurn3`: a deck/hand where a Payoff plan card is only
    castable by turn 3 BECAUSE of an in-hand rock -> ShapeExplosivePercent > 0, PlanKeepablePercent > 0.
  - `ShapeA_SlowPayoffTurn5_DoesNotCount`: a Payoff whose earliest castable turn is 5 (no acceleration)
    -> that hand is NOT Shape A (Acceptance #1 substrate).
  - `ShapeB_EngineByTurn2_Counts` and `ShapeB_EngineTurn3_DoesNotCount` (boundary flip at TurnCapEngine).
  - `ShapeC_TwoInteractionPlusDevelopment_Counts` and `ShapeC_OneInteraction_DoesNotCount`
    (boundary flip at BridgeInteractionMin).
  - `ShapeC_NonPermanentCounterspells_Count` (HIGH-1 regression): a hand with 2 instant-speed
    counterspells (Interaction PlanRole stripped by the permanent gate, IsInteractionSpell==true) +
    development -> Shape C true. Assert it FAILS if counting were done on PlanRole.Interaction.
  - `Commander_NotDrawnAsLibraryFiller` (HIGH-2 regression): a plan-tagged commander is absent from
    the built library and does not inflate any shape tally via a "drawn" path; commander credit comes
    only from the command-zone premium path. (Build the library for a Winota-style deck and assert the
    commander SpellRequirement is not among drawable LibraryCards.)
  - `PlanKeepable_NeverExceedsManaKeepable`: assert PlanKeepablePercent <= (kept7+to6)-derived
    mana-keepable for the same deck.
  - `KeepShapesOff_LeavesPlanFieldsAtDefault`: cEDH but keepShapes=false -> new fields all 0/empty.
Extend `ManabaseAnalyzerMulliganTests.cs` (create if absent, mirroring the existing mulligan test
that uses `ComputeMulliganEvaluationForTest`) with `Cedh_KeepShapes_SurfacesPlanKeepable` and
`Casual_OrFlagOff_PlanKeepableIsZero`. Use the deterministic StableSeed reproducibility already in
SimulatePlanPresence; assert on exact percents where the small deck makes them deterministic, else on
inequalities/monotonicity.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>PlanKeepablePercent surfaced on the evaluation in cEDH keep-shapes mode; test files build; boundary + monotonicity + off-path facts present. (CI runs the asserts — VSTest unreliable in WSL.)</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
No new trust boundary. Pure Core simulation over an already-validated deck model; no input surface,
no I/O, no packages.

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap11-02 | Denial of Service | Extra SimulateGame calls per keepable hand | mitigate | Shape checks run ONLY when keepShapes && cEDH; reuse the existing per-hand loop + board model (no second trial loop); commander-premium adds at most one SimulateGame per keepable hand. Off path adds zero calls. |
| T-mbgap11-03 | Tampering | plan-keepable arithmetic | mitigate | Denominator = trials, numerator = subset of keepable hands, so PlanKeepablePercent <= mana-keepable by construction; pinned by NeverExceedsManaKeepable test. |
| T-mbgap11-SC | Tampering | package installs | n/a | No package installs this phase. |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean; no old-signature callers remain.
- EOL check: `git diff --stat` == `git diff --ignore-all-space --stat` for both Core files;
  each touched file's `\r` count matches `git show HEAD:<path>`.
- Casual / flag-off: SimulatePlanPresence output byte-identical to pre-change (new fields default).
</verification>

<success_criteria>
- Shape A/B/C gate implemented in SimulatePlanPresence, crediting in-hand acceleration (AC5).
- plan-keepable % surfaced on the evaluation in cEDH, <= mana-keepable (AC2), 0 in casual/off.
- mode + keepShapes threaded cleanly; solution builds; keep-shape unit tests present.
</success_criteria>

<output>
Create `.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-02-SUMMARY.md` when done.
</output>
