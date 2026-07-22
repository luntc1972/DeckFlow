---
phase: 106-partial-quantity-tuning
plan: 05
type: execute
wave: 5
depends_on: ["106-04"]
autonomous: true
requirements: [EDIT-01, EDIT-02, EDIT-03]
files_modified:
  - DeckFlow.Web/Services/CutLab/CutLabExportService.cs
  - DeckFlow.Web.Tests/CutLabExportServiceTests.cs
  - DeckFlow.Web.Tests/CutLabWhatifTests.cs
  - DeckFlow.Web.Tests/CutLabStateSerializerTests.cs
  - DeckFlow.Web/e2e/cut-lab-tuning.spec.ts

must_haves:
  truths:
    - "Adjustments serialize into CutLabState and survive scenario save/reload (P104), and what-if preview/keep + goal recompute run on the adjustment-derived list"
    - "An added basic is a valid what-if cardOut and carries a land role + simulation facts (composition of the 106-02 no-Scryfall guarantee), so the guided surfaces treat added basics like any other land"
    - "Restore composes deterministically with adjustments: decisions apply first, then adjustments"
    - "The export patch reflects add/cut copies and added basics in BOTH Moxfield and Archidekt dialects, with no spurious 'metadata unavailable' warning for an intentionally added basic"
    - "End to end: import → cut near 100 → trim/add basics to exactly 100 → export shows tuned counts, and a scenario reload preserves adjustments"
  artifacts:
    - path: "DeckFlow.Web/e2e/cut-lab-tuning.spec.ts"
      provides: "Playwright e2e for the tune-to-100 flow + scenario reload + theme×viewport screenshots"
      contains: "adjust"
    - path: "DeckFlow.Web/Services/CutLab/CutLabExportService.cs"
      provides: "Added-basic entries export as ADD without a reconstruction warning"
  key_links:
    - from: "cut-lab-tuning.spec.ts"
      to: "/cut-lab (import) + /api/cut-lab/adjust + /cut-lab/export"
      via: "full tune-to-exactly-100 then export assertion"
---

<objective>
Verify the adjustment layer composes correctly with the Phase 104 (what-if / goals / scenarios / restore) and
Phase 105 (export patch) surfaces, polish the export so an intentionally added basic is not mislabeled as
missing-metadata, and add a Playwright e2e for the full tune-to-100 flow with scenario reload and theme ×
viewport screenshots. Composition includes confirming an added basic behaves as a first-class land (role + sim +
what-if selectable) — the no-Scryfall unit guarantee is proven in 106-02; here it is exercised end to end.

Purpose: Close the 106-DESIGN "Interactions to verify" list and the phase Success Criteria end to end.
Output: Interaction tests + export polish + e2e spec.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-DESIGN.md
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-04-SUMMARY.md
@./CLAUDE.md

<interfaces>
Export reconstruction (DeckFlow.Web/Services/CutLab/CutLabExportService.cs, ReconstructFinalEntries lines
182-240): for a kept card with no matching CutLabOriginalEntry it appends a mainboard DeckEntry AND a warning
"Original export metadata was unavailable for {name}; exported it as mainboard." An added basic is intentionally
new, so this warning must be suppressed for names that are known basics (CutLabBasicLands) — the entry still
exports (appears as ADD in the patch via DiffEngine vs OriginalEntries), just without the misleading warning.

Export flow already derives with adjustments after 106-02 (keptWorkingList uses the 3-arg Derive), and
CutLabExportComposer / DiffEngine.Compare(finalEntries, originalEntries) already emit quantity deltas, so
cut/add copies flow to both dialects with no composer change.

Added-basic land behavior (from 106-02): synthetic ScryfallCardData is injected so an added basic gets a land
role + simulation facts and is a valid what-if cardOut WITHOUT any Scryfall call. The 106-02 unit tests assert
"no network call" via the throwing resolver fake; 106-05 verifies the same added basic composes through what-if
preview/keep and shows its land role in the guided UI.

e2e analogs: DeckFlow.Web/e2e/cut-lab-export.spec.ts (import + reach-100 + export assertions) and
cut-lab-scenarios.spec.ts (save/reload scenario). Run headless per CLAUDE.md: start via scripts/run-web-test.sh
(DECKFLOW_DISABLE_AUTO_BROWSER=true), drive with `npx --no-install playwright test`. Never open a browser on the
Windows host.

Scenario save/reload posts the serialized CutLabStateJson back through the intake form (CutLabController.Process
state-only restore path, lines 51-58) — adjustments ride in QuantityAdjustments and must survive the round trip.
</interfaces>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Interaction tests (scenarios/what-if/goals/restore, added-basic land behavior) + export added-basic polish</name>
  <read_first>
    - DeckFlow.Web/Services/CutLab/CutLabExportService.cs (ReconstructFinalEntries — suppress warning for known basics)
    - DeckFlow.Web/Services/CutLab/CutLabBasicLands.cs (whitelist for the suppression check)
    - DeckFlow.Web.Tests/CutLabWhatifTests.cs, CutLabStateSerializerTests.cs, CutLabExportServiceTests.cs (analog fixtures)
  </read_first>
  <behavior>
    - Serializer round-trip preserves a state carrying both an existing-entry delta and an added-basic adjustment (scenario save/reload).
    - What-if preview and keep run against the adjustment-derived working list, and an ADDED BASIC (e.g. +1 Island not in the original pool) is a valid what-if cardOut whose metrics recompute on the adjusted list — no "could not pre-seed" failure, no Scryfall call.
    - Restore of a whole-entry cut composes with adjustments: applying decisions then adjustments yields the same derived list regardless of restore ordering.
    - Export: an added basic (e.g. +3 Wastes not in the original list) appears in BOTH the Moxfield and Archidekt patch text as an ADD, and produces NO "metadata unavailable" reconstruction warning.
    - Export: a trimmed basic (e.g. Island 35 → 32 via −3) shows a CUT of 3 in both dialect patches.
  </behavior>
  <action>
    In CutLabExportService.ReconstructFinalEntries, when a kept card has no original match, suppress the
    "metadata unavailable" warning if the name is a known basic (CutLabBasicLands) — still append the mainboard
    DeckEntry so it exports as ADD. Add xUnit tests: serializer round-trip with mixed adjustments; what-if
    preview/keep on an adjustment-derived list USING an added basic as cardOut (assert it is selectable and
    metrics recompute); restore-compose determinism (decisions then adjustments); export patch shows add-basic as
    ADD in both dialects with no spurious warning; export patch shows a trimmed basic as CUT in both dialects.
    Reuse existing fixtures; no new framework/package.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabExportServiceTests|FullyQualifiedName~CutLabWhatifTests|FullyQualifiedName~CutLabStateSerializerTests" 2>&1 | tail -6</automated>
  </verify>
  <acceptance_criteria>
    - A serializer test proves a state with an existing-entry delta + an added-basic adjustment round-trips intact.
    - A what-if test proves an added basic is a valid cardOut and preview/keep operate on the adjustment-derived list.
    - A restore test proves decisions-then-adjustments compose deterministically.
    - Export tests prove: added basic → ADD in both dialects with no reconstruction warning; trimmed basic → CUT of N in both dialects.
    - All named filters green; `dotnet build DeckFlow.Web` clean.
  </acceptance_criteria>
  <done>The adjustment layer is proven to compose with P104 and P105 surfaces, added basics behave as lands, and added basics export cleanly.</done>
</task>

<task type="auto">
  <name>Task 2: Playwright e2e — tune to exactly 100 + scenario reload + theme×viewport screenshots</name>
  <read_first>
    - DeckFlow.Web/e2e/cut-lab-export.spec.ts (analog: import → reach 100 → export)
    - DeckFlow.Web/e2e/cut-lab-scenarios.spec.ts (analog: save/reload scenario)
    - scripts/run-web-test.sh (headless server start; DECKFLOW_DISABLE_AUTO_BROWSER=true)
  </read_first>
  <action>
    Add cut-lab-tuning.spec.ts: import an oversized pool with a large basic stack, run guided cuts to near 100,
    then use the +/- steppers and the add-basic control to land on EXACTLY 100 (e.g. trim Islands and/or add a
    Wastes). Assert the sticky remaining reads "0 to cut" and the Export tab becomes enabled; assert the added
    basic row appears in the tuner with a land role label (confirms the synthetic-card land behavior in the UI).
    Build the export and assert the patch text reflects the tuned counts (CUT/ADD of the adjusted copies) in a
    builder dialect. Save the scenario (serialized state), reload it through the intake form, and assert the
    adjustments persist (tuned counts still present, still exactly 100). Capture theme × viewport screenshots
    (both guild themes, mobile + desktop) of the tuner region, matching the existing e2e screenshot convention.
    Run headless via scripts/run-web-test.sh; never open a browser on the Windows host.
  </action>
  <verify>
    <automated>bash scripts/run-web-test.sh >/tmp/claude-1000/cutlab-web.log 2>&1 & sleep 8; cd DeckFlow.Web && env -u DISPLAY -u WAYLAND_DISPLAY npx --no-install playwright test e2e/cut-lab-tuning.spec.ts 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - cut-lab-tuning.spec.ts drives import → guided cuts → stepper trim + add-basic → exactly 100 → sticky reads "0 to cut" and Export tab enabled.
    - The spec asserts the added-basic row shows a land role label in the tuner.
    - The spec asserts the export patch reflects the tuned copy counts (CUT and ADD) in a builder dialect.
    - The spec saves + reloads the scenario and asserts adjustments persist at exactly 100.
    - Theme × viewport screenshots (2 themes × mobile+desktop) are captured for the tuner region.
    - The spec passes headless; no browser opens on the Windows host.
  </acceptance_criteria>
  <done>The end-to-end tune-to-100 + persistence + export flow is proven in a browser, across themes and viewports.</done>
</task>

</tasks>

<verification>
- `dotnet build DeckFlow.sln` clean; Web.Tests green (existing + new interaction/export tests).
- `tsc -p tsconfig.json --noEmit` clean; Playwright cut-lab-tuning.spec.ts green headless.
- All three phase Success Criteria demonstrated end to end (cut/add copies one at a time; add new basics from
  constants; singleton legality enforced + reach exactly 100 by tuning counts).
- LF endings preserved; changed-lines format-gate clean; do NOT stage compiled wwwroot/js/*.js.
</verification>

<success_criteria>
Adjustments compose with scenarios/what-if/goals/restore and the Phase 105 export patch (both dialects, added
basics as ADD without spurious warnings), added basics behave as first-class lands (role/sim/what-if), and a
Playwright e2e proves import → tune to exactly 100 → export tuned counts → scenario reload preserves adjustments,
with theme × viewport screenshots.
</success_criteria>

<line_endings>
Preserve each touched file's existing line endings exactly (LF via .gitattributes). New files (e2e spec) use LF.
Change only the lines whose content changes; leave everything else byte-for-byte identical.
</line_endings>

<output>
Create `.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-05-SUMMARY.md` when done.
</output>
