# Phase 105 Context: Builder-Compatible Export

**Workstream:** cut-lab (Cycle 18) · **Phase:** 105 · **Status:** Pending → planning
**Requirements:** EXPORT-01, EXPORT-02, EXPORT-03
**Depends on:** Phase 103 (produces the working list), Phase 104 (goal-satisfying final state)

## Goal

A builder walks away with a validated, finished 100-card list plus an add/cut patch
they can paste back into the builder (Moxfield/Archidekt) they started from.

## Success Criteria (must be TRUE)

1. Export the finished 100-card list in **Moxfield-compatible AND Archidekt-compatible**
   text formats.
2. Export an **add/cut patch** describing exactly which cards to remove/add relative to
   the builder's **original imported list**.
3. Validate before export: **exactly 100 cards, color-identity legal, Commander-banlist
   clean** — reusing existing diff/export/banlist infrastructure.

## Core value alignment

Cut Lab's whole loop ends here: the builder must be able to copy output and paste it
straight back into their builder with **zero reformatting**. Output correctness is the
phase; polish is secondary.

## Locked Decisions (from discuss-phase, 2026-07-21)

### D1 — Export surface: **New "Export" step tab**
- Add an `Export` entry to the Cut Lab `_WorkflowStepTabs` strip:
  `Process → Decide → Goals → Export`.
- The Export tab **unlocks only when the working list is exactly 100 cards** (commander(s)
  + 99). Before that it is visible but disabled/greyed with a "reach 100 to export" hint.
- New controller action on `CutLabController` (e.g. `Export`) + a rendered section in
  `Views/Deck/CutLab.cshtml` (follow the existing Goals/Decide rendering pattern — the
  single-view, action-per-step pattern this controller already uses).
- Must obey existing CSRF / `SameOriginRequestValidator` and no-auto-browser test
  constraints (CLAUDE.md).

### D2 — Validation gate: **Show status, hard-block only on count ≠ 100**
- Always render the validation summary with three checks:
  - ✅/❌ **Card count = 100** (commander(s) + 99). This is the objective size.
  - ✅/⚠ **Color-identity legal** — every card inside the commander's color identity.
  - ✅/⚠ **Banlist clean** — no card on the Commander banlist.
- **Hard-block the final-list copy/export only when count ≠ 100.** Color-identity and
  banlist problems surface as **prominent warnings** but do NOT disable export — a builder
  may knowingly run a house-ruled/proxy card. Warnings must name the offending card(s).
- Rationale: cEDH/Commander builders sometimes run rule-zero cards; blocking the whole
  export on one flagged card is more annoying than useful. Count is non-negotiable because
  an off-count list is simply broken.

### D3 — Add/cut patch (EXPORT-02): **Readable CUT/ADD +/- list, both dialects**
- Patch is computed relative to the **original imported list** (the pool as the builder
  first imported it — the baseline the research agent is pinning down; the planner must
  cite the exact stored list).
- Render grouped as **CUT (remove)** and **ADD (keep/added)** sections, each line in
  builder syntax so it pastes into the edit view.
- Provide the patch in **both Moxfield and Archidekt line syntax** (two copy blocks) so
  either builder's user can paste directly.
- Cut Lab is primarily subtractive (oversized pool → 100); if only cuts happened, the ADD
  section is empty/noted. What-if swaps (Phase 104) can introduce adds, so both sections
  must be supported.

### D4 — Codex model defaults (session): **Keep**
- Plan review = `gpt-5.5` (reasoning medium); coding/impl = `gpt-5.4` (reasoning medium).
- Applies to the Codex plan-review at the end of this planning run and any later Codex
  dispatch this session.

## Reuse-first mandate

New effort = wiring + a validation summary + one UI surface. NOT new export/diff/banlist
math. Existing components to compose (research agent is producing the exact reuse map in
`RESEARCH.md`):
- `DeckFlow.Core/Exporting/MoxfieldTextExporter.cs`, `DeltaExporter.cs`,
  `FullImportExporter.cs`, `CategoryNormalization.cs`
- `DeckFlow.Core/Diffing/DiffEngine.cs` (the add/cut diff → patch)
- `DeckFlow.Web/Services/DeckConvertService.cs` (Moxfield↔Archidekt conversion — candidate
  for producing Archidekt text if no dedicated Archidekt exporter exists)
- `DeckFlow.Web/Services/CommanderBanListService.cs` (banlist check)
- `DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs`, `CutLabStateSerializer.cs`,
  `CutLabPageService.cs` (live finished-list + original-pool state)
- **Gap flag for planner:** confirm whether an Archidekt *text exporter* exists; if not,
  the gap is a small `ArchidektTextExporter` mirroring `MoxfieldTextExporter`, or reuse of
  `DeckConvertService`. Confirm where color-identity legality is checked anywhere today.

## Testing constraints (CLAUDE.md)

- Add xUnit tests in the tested projects (Core exporter/diff logic, Web service logic).
  Prefer extracting patch/validation logic into `DeckFlow.Core` so it is unit-testable
  (surface testable coverage gaps rule).
- UI: no auto-browser; drive with Playwright headless if a live check is needed.
- Web-page change → tests + all themes + desktop/mobile (new Export tab is a UI change).
- Preserve per-file LF line endings; changed-lines format gate.
