---
phase: mbgap-11-cedh-mulligan-keep
plan: 05
type: execute
wave: 5
depends_on: [MBGAP-11-03, MBGAP-11-04]
files_modified:
  - DeckFlow.Web/Models/ManabaseViewModel.cs
  - DeckFlow.Web/Controllers/ManabaseController.cs
  - DeckFlow.Web/Views/Deck/Manabase.cshtml
  - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderMulliganTests.cs
  - DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs
autonomous: true
requirements: [MBGAP-11-AC2, MBGAP-11-AC4, MBGAP-11-AC7, MBGAP-11-D01]
must_haves:
  truths:
    - "In cEDH keep-shapes mode the opening-hand panel shows TWO headline %s: mana-keepable and plan-keepable, side by side"
    - "Representative openers render their shape label (explosive/engine/bridge keep or no-plan mulligan)"
    - "Casual mode shows a curve-coverage line: 'plays a spell on ~N of first 5 turns'"
    - "The downloadable manabase artifact reflects the new reads when the flag is on and is byte-identical when off"
  artifacts:
    - path: "DeckFlow.Web/Views/Deck/Manabase.cshtml"
      provides: "Second headline (plan-keepable), shape-labeled openers, casual curve-coverage line, all gated on ShowKeepShapes"
      contains: "plan-keepable"
    - path: "DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs"
      provides: "includeCedhKeepShapes-gated plan-keepable + shape + curve-coverage prompt lines"
      contains: "includeCedhKeepShapes"
    - path: "DeckFlow.Web/Models/ManabaseViewModel.cs"
      provides: "ShowKeepShapes flag on the view model"
      contains: "ShowKeepShapes"
  key_links:
    - from: "ManabaseController"
      to: "ManabaseViewModel.ShowKeepShapes + ManabaseReportTextBuilder.Build includeCedhKeepShapes"
      via: "result.ShowKeepShapes wired to view + download"
      pattern: "ShowKeepShapes"
    - from: "Manabase.cshtml second headline"
      to: "mull.PlanKeepablePercent"
      via: "cEDH-gated big-number render"
      pattern: "PlanKeepablePercent"
---

<objective>
Surface the MBGAP-11 reads in the two user-facing artifacts: the on-page opening-hand panel
(`Manabase.cshtml`) and the downloadable paste artifact (`ManabaseReportTextBuilder`). Add the second
headline (plan-keepable %, D-01), shape-labeled representative openers, and the casual curve-coverage
line — all gated on `ShowKeepShapes` so off = byte-identical.

Purpose: Deliver the visible product of the redesign (Acceptance #2 two headlines, #4 casual line)
and prove the flag-off byte-identity (Acceptance #7) via the existing excision + verbatim-pin tests.

Output: ShowKeepShapes on the view model + controller wiring; two-headline + shape-opener +
curve-coverage view markup; the parallel prompt-artifact lines; updated pin/excision tests.
(The manabase swap prompt does NOT embed the mulligan block — verified — so only the downloadable
artifact via ManabaseReportTextBuilder changes.)
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md

CODEX DISPATCH NOTE (line endings): MIXED LF/CRLF repo — preserve each touched file's existing line
endings exactly (per-file detect; never normalize). `.cshtml` and `.cs` — verify per file. This is a
web-page change: per CLAUDE.md it needs xUnit coverage here AND Playwright + theme/mobile coverage in
plan 06. Do not touch layout CSS in `site.css` (theme constraint — layout goes in `site-common.css`
only, and this plan should need no new layout CSS; reuse the existing `manabase-mulliganlens-split`
structure).
</execution_context>

<context>
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-CONTEXT.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-PATTERNS.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-03-SUMMARY.md
@.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-04-SUMMARY.md

<interfaces>
<!-- Verified render + builder contracts. -->

ManabaseViewModel.cs:60-66 — ShowMulliganEval / ShowPlanPresence / ShowCedhInteractionLens init props
  (add ShowKeepShapes beside them). report.Mode == ManabaseMode.Cedh gates cEDH-only UI.
ManabaseController.cs:118-120 — view-model construction sets ShowMulliganEval/ShowPlanPresence/
  ShowCedhInteractionLens from result (add ShowKeepShapes = result.ShowKeepShapes).
ManabaseController.cs:153-160 — Download builds text via ManabaseReportTextBuilder.Build(...) with
  includePlanPresence: result.ShowPlanPresence (add includeCedhKeepShapes: result.ShowKeepShapes).

Manabase.cshtml:628-701 — the opening-hand block. :633-673 the mulliganlens-split (headline #1 at
  :638-640 KeepableHandPercent; keep-size at :651; plan-presence at :653-671). :674-699 the openers
  <ul> (:680 Decision, :681 composition, :683-694 on-curve read, :695 HasPlan "workable line"/"no
  clear line" — the ShapeLabel slots here).

ManabaseReportTextBuilder.cs:51-62 — Build(..., bool includePlanPresence = false) (add
  bool includeCedhKeepShapes = false). :193 AppendMulliganEvaluationBlock(sb, mulligan,
  includePlanPresence) — thread includeCedhKeepShapes. :293-338 the block: :296-297 headline #1
  (Keepable hands); :306-314 plan-presence line (the includePlanPresence idiom to mirror);
  :316-335 openers loop (ShapeLabel + turn-capped read slot here); off appends zero bytes.

Plan 01/02/03 DTO reads: mull.PlanKeepablePercent, mull.PlanKeepableBand, mull.CurveCoverageTurns;
  opener.ShapeLabel; plan.ShapeExplosivePercent/ShapeEnginePercent/ShapeBridgePercent (on PlanPresence).
ManabaseDisplay.cs — KeepableMarker(band) (:123), AvgManaValueText (:145), PlanRoleLabel (:149);
  add a CurveCoverage helper if useful.
Byte-identity proof: DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs
  (OffState_IsByteIdenticalToOnWithMulliganCardExcised); verbatim pins:
  DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderMulliganTests.cs.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: View model + controller wiring for ShowKeepShapes and download</name>
  <files>DeckFlow.Web/Models/ManabaseViewModel.cs, DeckFlow.Web/Controllers/ManabaseController.cs</files>
  <action>
Add `public bool ShowKeepShapes { get; init; }` to `ManabaseViewModel` beside ShowMulliganEval
(ManabaseViewModel.cs:60-66), with xmldoc. In `ManabaseController`, set `ShowKeepShapes =
result.ShowKeepShapes` at the view-model construction (:118-120). In the `Download` action, pass
`includeCedhKeepShapes: result.ShowKeepShapes` to `ManabaseReportTextBuilder.Build(...)` (:153-160),
alongside the existing `includePlanPresence: result.ShowPlanPresence`. No behavior change when the
flag is off (ShowKeepShapes=false).
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>ShowKeepShapes flows result -> view model and result -> Build(includeCedhKeepShapes); Web builds clean.</done>
</task>

<task type="auto">
  <name>Task 2: Opening-hand panel — second headline, shape openers, casual curve-coverage</name>
  <files>DeckFlow.Web/Views/Deck/Manabase.cshtml, DeckFlow.Web/Models/ManabaseDisplay.cs</files>
  <action>
In the opening-hand block (Manabase.cshtml:628-701), all new markup gated on `Model.ShowKeepShapes`
so off = byte-identical:

(a) Second headline (D-01, Acceptance #2), cEDH only — gate on `Model.ShowKeepShapes && report.Mode
== ManabaseMode.Cedh`: render `mull.PlanKeepablePercent` + `mull.PlanKeepableBand` as a second
big-number beside the existing mana-keepable headline (:638-640), reusing the
`manabase-mulliganlens-split` / `manabase-lens-big--soft` structure and `ManabaseDisplay.KeepableMarker`.
Label it "plan-keepable hands" vs the existing "keepable hands" (rename the first to "mana-keepable"
in cEDH-keep-shapes mode only, or add a clarifying sub-pill — keep the casual/off label exactly as
today). Add a short pill explaining plan-keepable = passed a cEDH keep shape (explosive / early engine
/ interaction bridge).

(b) Shape-labeled openers (Acceptance #1 copy): in the openers <ul> (:674-699), when
`Model.ShowKeepShapes` and `opener.ShapeLabel` is non-empty, render the shape label (e.g. append
"— explosive keep" / "— engine keep" / "— bridge keep" / "— no plan by turn 4 — mulligan") in place
of / alongside the existing "workable line"/"no clear line" muted span (:695). Keep the existing
span when ShapeLabel is empty (casual/off) so nothing changes there.

(c) Casual curve-coverage line (D-03, Acceptance #4): when `Model.ShowKeepShapes` and `report.Mode ==
ManabaseMode.Casual` and `mull.CurveCoverageTurns > 0`, render a row "plays a spell on ~@N of first 5
turns" where N = rounded `mull.CurveCoverageTurns`. Add a `ManabaseDisplay.CurveCoverageText(double)`
helper that formats the rounded count (0–5) for reuse by the prompt builder wording in Task 3.

Do not introduce new layout CSS in site.css; if a token/utility is genuinely needed put it in
site-common.css per the theme constraint. Keep the note line (:700) unchanged.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>cEDH shows two headlines + shape-labeled openers; casual shows the curve-coverage line; all gated on ShowKeepShapes; off renders identically; Web builds clean.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 3: Prompt-artifact lines + byte-identity/pin tests</name>
  <files>DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs, DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderMulliganTests.cs, DeckFlow.Web.Tests/Manabase/ManabaseViewRenderTests.cs</files>
  <behavior>
    - includeCedhKeepShapes=false: AppendMulliganEvaluationBlock output is byte-identical to today (verbatim pins unchanged).
    - includeCedhKeepShapes=true, cEDH: a "Plan-keepable hands: {band} (~{pct}%)" line and shape-labeled representative-opener reads appear; plan-keepable line sits beside the existing "Keepable hands" line.
    - includeCedhKeepShapes=true, casual: a "Plays a spell on ~N of first 5 turns" line appears.
  </behavior>
  <action>
Add `bool includeCedhKeepShapes = false` to `ManabaseReportTextBuilder.Build` (:51-62) and thread it
to `AppendMulliganEvaluationBlock` (:193, :293). Inside the block, mirror the flag-gated append idiom
(:306 `if (includePlanPresence ...)` — off appends zero bytes):
  - cEDH second headline: when `includeCedhKeepShapes` and the evaluation carries a plan-keepable read
    (PlanKeepableBand non-empty), append a sibling line after the "Keepable hands" headline (:297):
    "Plan-keepable hands: {mull.PlanKeepableBand} (~{mull.PlanKeepablePercent}%) - passed a cEDH keep
    shape (explosive / early engine / interaction bridge); <= mana-keepable by construction."
  - Shape-labeled openers: in the openers loop (:316-335), when `includeCedhKeepShapes` and
    `opener.ShapeLabel` is non-empty, append the shape label to the opener read line (replacing the
    "workable line"/"no clear line" tail with the ShapeLabel, e.g. "... - explosive keep." /
    "... - no plan by turn 4 - mulligan.").
  - Casual curve-coverage: when `includeCedhKeepShapes` and `mull.CurveCoverageTurns > 0`, append
    "Plays a spell on ~{ManabaseDisplay-equivalent rounded N} of first 5 turns." (use an invariant-
    culture format; keep the wording matching the view's CurveCoverageText).
Use `string.Create(CultureInfo.InvariantCulture, ...)` like the surrounding lines. Off path (default
false) appends nothing — byte-identity preserved.

Tests: extend `ManabaseReportTextBuilderMulliganTests.cs` with verbatim pins for (i) the cEDH
plan-keepable line, (ii) a shape-labeled opener line, (iii) the casual curve-coverage line, and a
`KeepShapesOff_ByteIdenticalToBaseline` pin proving the default-false output equals the existing
pinned text. Update `ManabaseViewRenderTests.cs` excision test so the OFF state remains byte-identical
to ON-with-the-keep-shapes-markup-excised (extend the excision to strip the new gated spans), keeping
Acceptance #7's byte-identity proof intact.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug 2>&1 | grep -E "Build succeeded|error" | head</automated>
  </verify>
  <done>Builder emits the three new gated lines when includeCedhKeepShapes is on and zero bytes when off; verbatim pins + excision test cover on/off; solution builds clean.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| deck paste (untrusted) -> rendered view / paste artifact | Existing boundary; spell/commander names already flow through the current render + HTML encoding. No new raw interpolation of user text is introduced (shape labels are fixed strings; percents/counts are numeric). |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap11-08 | Tampering (XSS) | New view spans | mitigate | Razor auto-encodes; new outputs are numeric (percents, rounded counts) and fixed shape-label strings — no user-controlled string is newly emitted unencoded. |
| T-mbgap11-09 | Repudiation | Flag-off byte-identity | mitigate | Excision test + verbatim pins prove OFF == today; the new prompt lines are behind includeCedhKeepShapes. |
| T-mbgap11-SC | Tampering | package installs | n/a | No package installs this phase. |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean; Core + Web + both test projects build.
- Byte-identity: OFF-state view excision test + OFF verbatim prompt pin both green (Acceptance #7).
- EOL: per-file `\r` counts unchanged vs `git show HEAD:<path>`; `git diff --stat` ==
  `git diff --ignore-all-space --stat` for `.cshtml` and `.cs`.
</verification>

<success_criteria>
- Two headlines in cEDH (AC2), shape-labeled openers (AC1 copy), casual curve-coverage line (AC4).
- Downloadable artifact reflects the reads when on, byte-identical when off (AC7).
- No layout CSS added to site.css; solution builds; pins + excision tests updated.
</success_criteria>

<output>
Create `.planning/phases/mbgap-11-cedh-mulligan-keep/MBGAP-11-05-SUMMARY.md` when done.
</output>
