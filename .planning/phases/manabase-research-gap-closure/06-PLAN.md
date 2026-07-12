---
phase: manabase-research-gap-closure
plan: 06
type: execute
wave: 6
depends_on: ["04", "05"]
files_modified:
  - DeckFlow.Core/Manabase/ManabaseVerdictSynthesizer.cs
  - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs
  - DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs
  - DeckFlow.Web/Views/Deck/Manabase.cshtml
  - DeckFlow.Core.Tests/Manabase/ManabaseVerdictSynthesizerTests.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs
  - DeckFlow.Core.Tests/Manabase/ManabaseSwapPromptBuilderTests.cs
  - DeckFlow.Web/e2e/manabase-verdict.spec.ts
  - docs/manabase-analysis-rules.md
  - README.md
autonomous: true
requirements: [MBGAP-05a, MBGAP-05b, MBGAP-05c, MBGAP-05d]
must_haves:
  truths:
    - "05a: a 1.05-land shortfall no longer reads 'add ~2 land(s)' — both Math.Ceiling overstatement sites (VS:63 land-delta AND VS:107 color-source-short) are fixed to not overstate by up to 1"
    - "05b: when the verdict has more than 3 issues it appends a '…plus N more' line instead of silently dropping them, on the page AND the .txt artifact"
    - "05c: no '(s)' plural artifacts remain in the synthesizer or the Razor view (real singular/plural chosen by count)"
    - "05d: the per-color deficit is explicitly labeled heuristic guidance on the page, the .txt artifact, and the swap prompt (no math change)"
  artifacts:
    - path: "DeckFlow.Core/Manabase/ManabaseVerdictSynthesizer.cs"
      provides: "05a rounding fix (both sites), 05b truncation-with-count, 05c pluralization, 05d heuristic label"
      contains: "plus"
  key_links:
    - from: "ManabaseVerdictSynthesizer truncation"
      to: "ManabaseVerdict.Lines consumers (page + .txt)"
      via: "…plus N more line propagates to every surface that renders Lines"
      pattern: "plus"
---

<objective>
MBGAP-05a-d verdict-polish batch (D-12), copy/labeling only — no math or threshold change:
- 05a: fix `Math.Ceiling` overstatement at BOTH `ManabaseVerdictSynthesizer.cs:63`
  (land delta) AND `:107` (color-source-short) — a 1.05 shortfall must not read "add ~2".
- 05b: replace the silent 3-line truncation (`VS:94-97`) with an appended "…plus N more".
- 05c: remove all `(s)` plural artifacts in the synthesizer and the Razor view.
- 05d: label the per-color deficit as heuristic guidance on the page, the .txt artifact,
  and the swap prompt (the parked EF1 #4 "label, don't rebuild" branch).

Exact copy is at Claude's discretion (D-12); the constraints above are not.

Purpose: closes efficacy findings L1/L2 and the parked EF1 #4 decision.
Output: synthesizer/text/swap-prompt wording fixes, view plural fixes, tests, e2e, docs+README.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/phases/manabase-research-gap-closure/CONTEXT.md
@.planning/phases/manabase-research-gap-closure/manabase-research-gap-closure-PATTERNS.md

<interfaces>
<!-- Exact anchors (extracted from source). -->

ManabaseVerdictSynthesizer.cs:
- VS:59-64 land-delta line: `Add ~{Math.Ceiling(-report.LandDelta):F0} more land(s) - ...` (05a site 1, 05c "land(s)")
- VS:94-97 truncation: `if (issues.Count > 3) { issues.RemoveRange(3, issues.Count - 3); }` (05b)
- VS:102-120 BuildColorIssue: `int shortfall = (int)Math.Ceiling(finding.Deficit);` then `... {finding.Color} source(s) short ...` (05a site 2, 05c "source(s)", 05d label target)
- VS:119 `spell(s)`, VS:135 BuildBudgetIssue `piece(s)` (05c)

.txt builder — ManabaseReportTextBuilder.cs: `land(s)` (:92,:150), `source(s)` (:145), `card(s)` (:155,:281). Also renders the verdict Lines (05b propagation) and the per-color deficit (05d label).
swap prompt — ManabaseSwapPromptBuilder.cs:95 per-color deficit line `(add ~{Math.Ceiling(f.Deficit):F0})` and existing "community heuristic, not Karsten math" note at :152 — 05d adds an equivalent heuristic label to the per-color deficit block (:84-95).

Manabase.cshtml: 6 `(s)` literals (grep count = 6) — 05c view fixes.

05a fix approach (Claude discretion): round-to-nearest or show the raw one-decimal delta so a 1.05 shortfall reads "~1"
not "~2". Apply the SAME approach to both VS sites for consistency (Q2 Addendum: both in scope).
05c approach: choose singular/plural by the actual count (helper or inline ternary), not a literal "(s)".
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: 05a rounding + 05b truncation-count + 05c/05d in synthesizer</name>
  <behavior>
    - LandDelta = -1.05 → verdict says "~1" more land, not "~2" (both VS sites use the same rounding)
    - Deficit = 1.2 → "~1 X source" (singular), Deficit = 2.6 → "~3 X sources" (plural), never "(s)"
    - 5 collected issues → 3 shown + a final "…plus 2 more" line (Lines.Count reflects the appended note)
    - the per-color deficit line carries a heuristic-guidance label (e.g. "(heuristic guidance)")
    - a verdict with <=3 issues has no "plus N more" line
  </behavior>
  <read_first>
    - DeckFlow.Core/Manabase/ManabaseVerdictSynthesizer.cs (whole file, ~196 lines)
    - DeckFlow.Core.Tests/Manabase/ManabaseVerdictSynthesizerTests.cs
  </read_first>
  <action>
    (a) 05a: change BOTH `Math.Ceiling` sites (VS:63 land-delta, VS:107 BuildColorIssue) so a fractional shortfall no longer
    rounds up by up to a whole unit — use Math.Round (MidpointRounding.AwayFromZero) or show the raw one-decimal delta; apply the
    identical treatment to both sites. Keep CultureInfo.InvariantCulture + string.Create shape.
    (b) 05b: replace the RemoveRange truncation (VS:94-97) with: keep first 3, and if more remain append a single line
    "…plus {N} more" (N = original count - 3) to `issues` before returning, so downstream Lines consumers see it.
    (c) 05c: replace "land(s)", "source(s)", "spell(s)", "piece(s)" with count-driven singular/plural (helper or ternary).
    (d) 05d: append a short heuristic-guidance label to the per-color deficit sentence in BuildColorIssue (no math change).
    (e) Extend ManabaseVerdictSynthesizerTests.cs with the five <behavior> cases.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseVerdictSynthesizer" 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c "(s)" DeckFlow.Core/Manabase/ManabaseVerdictSynthesizer.cs` returns 0
    - `grep -c "Math.Ceiling" DeckFlow.Core/Manabase/ManabaseVerdictSynthesizer.cs` returns 0 (both sites changed)
    - `grep -c "plus" DeckFlow.Core/Manabase/ManabaseVerdictSynthesizer.cs` returns >= 1 (truncation note)
    - all five behavior tests pass; `dotnet build DeckFlow.sln` 0/0
  </acceptance_criteria>
  <done>Synthesizer 05a/b/c/d fixed and tested.</done>
</task>

<task type="auto">
  <name>Task 2: Propagate to .txt builder + swap prompt + Razor view</name>
  <read_first>
    - DeckFlow.Core/Manabase/ManabaseReportTextBuilder.cs (verdict Lines render + :92/:145/:150/:155/:281 plural sites)
    - DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs (:84-95 per-color deficit, :152 heuristic note)
    - DeckFlow.Web/Views/Deck/Manabase.cshtml (6 "(s)" literals)
    - DeckFlow.Core.Tests/Manabase/ManabaseReportTextBuilderTests.cs, ManabaseSwapPromptBuilderTests.cs
  </read_first>
  <action>
    (a) 05b propagation: confirm the .txt builder renders the appended "…plus N more" line (it renders Lines — verify it is not
    re-truncated; if it applies its own cap, apply the same append-count treatment). Add/extend a ManabaseReportTextBuilderTests
    case asserting the "plus N more" line appears in the .txt artifact.
    (b) 05c: replace the "(s)" plural literals in ManabaseReportTextBuilder.cs and the 6 in Manabase.cshtml with count-driven
    singular/plural. Apply the same 05a rounding fix to the .txt builder's `Math.Ceiling(-delta)` (:92) and the swap prompt's
    `Math.Ceiling(f.Deficit)` (:95) so all surfaces agree.
    (c) 05d: add the heuristic-guidance label to the per-color deficit block in ManabaseSwapPromptBuilder.cs (:84-95) and the .txt
    builder and the page, matching the "community heuristic, not Karsten math" tone already used at swap :152. Extend
    ManabaseSwapPromptBuilderTests.cs to assert the label is present.
    (d) Update docs/manabase-analysis-rules.md (verdict wording section: rounding, truncation-with-count, heuristic labeling) and
    README where the verdict/paste artifacts are described. Changed lines only, LF.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~ManabaseReportTextBuilder|FullyQualifiedName~ManabaseSwapPromptBuilder" 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - `grep -c "(s)" DeckFlow.Web/Views/Deck/Manabase.cshtml` returns 0
    - .txt builder test asserts "plus N more" appears; swap prompt test asserts the heuristic label appears
    - rounding fix consistent across synthesizer/.txt/swap (no Math.Ceiling on the land-delta/deficit lines in these three files)
    - docs + README updated; `dotnet build DeckFlow.sln` 0/0; no EOL churn
  </acceptance_criteria>
  <done>All three surfaces carry the 05a/b/c/d fixes consistently.</done>
</task>

<task type="auto">
  <name>Task 3: Playwright verdict spec (2 viewports)</name>
  <read_first>
    - DeckFlow.Web/e2e/manabase-verdict.spec.ts (existing verdict e2e to extend)
    - CLAUDE.md testing constraints (run-web-test.sh; headless; env -u DISPLAY)
  </read_first>
  <action>
    Extend manabase-verdict.spec.ts: submit a deck that produces >3 verdict issues and assert the rendered verdict shows the
    "…plus N more" line (05b) and contains no "(s)" literal (05c), at desktop and mobile viewports. Reuse the project's existing
    viewport-parameterization. Headless only, no host browser.
  </action>
  <verify>
    <automated>scripts/run-web-test.sh &amp; sleep 8; env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test manabase-verdict 2>&1 | tail -20</automated>
  </verify>
  <acceptance_criteria>
    - manabase-verdict.spec.ts asserts the "plus N more" line and absence of "(s)" at 2 viewports
    - spec passes headless
  </acceptance_criteria>
  <done>Verdict e2e green at desktop + mobile.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| verdict → pasted ChatGPT artifact | Core-value surface: the paste must not silently omit information (05b) |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap05-01 | Information disclosure | silent truncation loses paste info | mitigate | 05b appended "…plus N more" across page + .txt (Task 1/2) |
| T-mbgap05-02 | Repudiation | overstated land/source counts mislead | mitigate | 05a rounding fix at both sites + all surfaces |
| T-mbgap05-SC | Tampering | NuGet installs | accept | No new packages this plan |
</threat_model>

<verification>
- `dotnet build DeckFlow.sln` clean; full `dotnet test DeckFlow.sln` green.
- No "(s)" literals in synthesizer or view; no Math.Ceiling overstatement on the verdict lines.
- Verdict e2e green at 2 viewports.
</verification>

<success_criteria>
Verdict no longer overstates (both sites), no longer silently truncates (page + .txt), has correct pluralization (synthesizer + view), and labels per-color deficit as heuristic guidance across page/.txt/swap. MBGAP-05a-d complete.
</success_criteria>

<output>
Create `.planning/phases/manabase-research-gap-closure/06-SUMMARY.md` when done.
</output>
