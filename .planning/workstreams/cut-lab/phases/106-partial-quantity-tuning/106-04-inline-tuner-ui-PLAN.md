---
phase: 106-partial-quantity-tuning
plan: 04
type: execute
wave: 4
depends_on: ["106-03"]
autonomous: true
requirements: [EDIT-01, EDIT-02, EDIT-03]
files_modified:
  - DeckFlow.Web/Models/CutLabViewModel.cs
  - DeckFlow.Web/Views/Deck/CutLab.cshtml
  - DeckFlow.Web/wwwroot/css/site-common.css
  - DeckFlow.Web/wwwroot/ts/cut-lab.ts
  - DeckFlow.Web/wwwroot/ts/__tests__/cut-lab.adjust.test.ts
  - DeckFlow.Web.Tests/CutLabViewModelWordingTests.cs

must_haves:
  truths:
    - "The +/- steppers and the tuned quantities render from an explicit adjustment-derived working-list collection (includes materialized added basics, excludes already-cut entries), NOT from the immutable Model.Pool"
    - "Each basic / legal-multiple working-list row shows +/- steppers that are disabled at the legal bounds (min 0, singleton max)"
    - "An add-basic control lets the user add any of the known basics not already in the pool"
    - "Applying a stepper or add-basic updates the sticky remaining-to-100 count and re-evaluates the Export tab enablement without a full reload (JS), and full-page re-renders without JS"
    - "No new step tab is added to the workflow"
  artifacts:
    - path: "DeckFlow.Web/Views/Deck/CutLab.cshtml"
      provides: "Inline +/- steppers + add-basic control + adjust form (JSON + no-JS), iterating the derived working-list rows"
      contains: "cut-lab/adjust"
    - path: "DeckFlow.Web/Models/CutLabViewModel.cs"
      provides: "Adjustment-derived WorkingListRows/TunableRows projection with per-row legality/cap flags"
      contains: "QuantityAdjustments"
    - path: "DeckFlow.Web/wwwroot/ts/cut-lab.ts"
      provides: "Stepper + add-basic wiring to /api/cut-lab/adjust with count + export-tab patch"
      contains: "adjust"
  key_links:
    - from: "CutLabViewModel WorkingListRows/TunableRows"
      to: "CutLabWorkingList.Derive(pool, decisions, QuantityAdjustments)"
      via: "rows built from the adjustment-derived working list, incl materialized added basics"
      pattern: "Derive\\([^)]*QuantityAdjustments"
    - from: "cut-lab.ts stepper handler"
      to: "/api/cut-lab/adjust"
      via: "fetch POST, then patch sticky count + updateExportTabState"
      pattern: "cut-lab/adjust"
    - from: "CutLab.cshtml stepper form"
      to: "/cut-lab/adjust"
      via: "no-JS form fallback mirroring data-cut-lab-decide-form"
---

<objective>
Add the inline copy-tuner to the Decide workspace: +/- steppers on basic / legal-multiple working-list rows
(disabled at legal bounds), an "add basic land" control for the known basics, and the JSON + no-JS adjust form.
Steppers and tuned quantities render from an explicit adjustment-DERIVED working-list collection — not the
immutable `Model.Pool` — so added basics appear and cut entries do not. JS patches the sticky remaining-to-100
count and re-evaluates the Export-tab enablement; no-JS full-page re-renders. No new step tab. Reuses the
existing decide/goals visual + interaction language and cutlab CSS tokens (per 106-DESIGN UI section and the
ui_contract_note).

Purpose: EDIT-01/02/03 surfaced to the user inline, mirroring the established progressive-enhancement contract.
Output: ViewModel derived-rows projection + Razor + CSS + TS + a vitest for the JS wiring.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-DESIGN.md
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-03-SUMMARY.md
@./CLAUDE.md

<interfaces>
Razor analogs (DeckFlow.Web/Views/Deck/CutLab.cshtml):
- `RenderDecisionForm(...)` local (lines 36-61): the JSON+no-JS form pattern — `<form method="post"
  action="~/cut-lab/decide" data-cut-lab-decide-form>` + `@Html.AntiForgeryToken()` + hidden CutLabStateJson +
  hidden CardName + a `data-cut-lab-*` submit button. Mirror this for the adjust form.
- IMPORTANT (HIGH-2): the pool/lock table (lines 278-328, `@foreach (var card in Model.Pool)`, qty shown at
  `:297 @card.Quantity × @card.Name`) iterates the IMMUTABLE ORIGINAL pool — it includes already-cut cards and
  omits added basics, and its quantities are pre-adjustment. Do NOT attach the steppers there. The steppers +
  add-basic control must iterate a NEW adjustment-derived working-list collection on the view model (see below).
  The pool table's `data-cut-lab-*` attributes are a styling/markup analog only, not the data source.
- Sticky bar (lines 902-908): `[data-cut-lab-sticky-remaining]` reads "@count to cut".
- Export tab (lines 12-22): `WorkflowStepTab(4, "Export", IsEnabled: cardsRemainingToTarget == 0, ...)`.

TS analogs (DeckFlow.Web/wwwroot/ts/cut-lab.ts):
- `cutLabDecisionApiEndpoint = '/api/cut-lab/decide'` (line 170) → add `cutLabAdjustApiEndpoint = '/api/cut-lab/adjust'`.
- `handleDecisionSubmit` (lines 1973-2010): fetch + JSON body + reads response, updates hidden state + sticky
  count via `getStickyRemaining()` (lines 1881-1883) → mirror for adjust.
- `updateExportTabState`/`getExportStepTab` (lines 1707-1714): toggles Export tab disabled by atTarget — call
  after an adjust response.
- The `[data-cut-lab-goals-form]` submit interception (line 2566) is the analog no-JS-form-intercept wiring.

ViewModel (DeckFlow.Web/Models/CutLabViewModel.cs): `pool = result.State?.Pool` (line 147) is the immutable pool.
Add a NEW `WorkingListRows` (or `TunableRows`) collection built from `CutLabWorkingList.Derive(pool, decisions,
result.State.QuantityAdjustments)` (the 3-arg overload, so materialized added basics are included and cut entries
excluded). Each row carries Name, CurrentQuantity (from the derived entry), `IsLegalMultiple` + `LegalMax` (from
CutLabLegality), and `IsAddedBasic`. Also expose `AddableBasics` = the canonical basics list (Plains, Island,
Swamp, Mountain, Forest, then Snow-Covered variants, then Wastes) FILTERED to names NOT already present in
WorkingListRows (a basic already in the working list is tuned with its steppers, so the add-basic control only
offers basics absent from the derived list). The after-106-02 role assignment covers added basics (synthetic
ScryfallCardData), so role labels on these rows work.

CSS: layout goes in site-common.css only (never site.css); reuse existing `cutlab-*` tokens/classes and the
`--cutlab-*` theme seam. Do not invent a new visual language.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: ViewModel adjustment-derived WorkingListRows + legality/cap flags + addable-basics</name>
  <read_first>
    - DeckFlow.Web/Models/CutLabViewModel.cs (line 147 `pool=result.State.Pool`; :170 currentCount; :627 working-list rows helper — build the new rows from the derived list, NOT Model.Pool)
    - DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs (the 3-arg Derive overload from 106-01)
    - DeckFlow.Web/Services/CutLab/CutLabLegality.cs + CutLabBasicLands.cs (106-01: legality predicate + basics names)
  </read_first>
  <action>
    In CutLabViewModel.From, derive the working list once via `CutLabWorkingList.Derive(pool,
    result.State?.Decisions ?? [], result.State?.QuantityAdjustments ?? [])` and project a new
    `IReadOnlyList<CutLabTunableRowView> WorkingListRows` where each row = { Name, CurrentQuantity (the derived
    entry's Quantity), IsLegalMultiple + LegalMax (via CutLabLegality), IsAddedBasic, plus the role label from
    `result.RoleAssignmentsByCardName` }. This collection INCLUDES materialized added basics and EXCLUDES cut
    entries — it is the render source for steppers, not Model.Pool. Add `AddableBasics` = CutLabBasicLands.Names
    in canonical order (Plains, Island, Swamp, Mountain, Forest, then Snow-Covered variants, then Wastes)
    FILTERED to exclude any basic already present in WorkingListRows (ordinal-normalized name match), so the
    add-basic control never offers a duplicate of a basic the user can already tune with steppers. Add the
    `CutLabTunableRowView` record (mirror existing row view records) and surface both on CutLabViewModel without
    removing existing fields. Keep `{ get; init; }` accessors.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - CutLabViewModel exposes `WorkingListRows` built from `CutLabWorkingList.Derive(pool, decisions, QuantityAdjustments)` — a test proves the collection includes a materialized added basic and excludes an accepted-cut card, with per-row CurrentQuantity reflecting the tuned quantity.
    - Each row exposes `IsLegalMultiple`, `LegalMax`, `CurrentQuantity`, `IsAddedBasic`.
    - `AddableBasics` excludes any basic already present in WorkingListRows (a test proves e.g. Island — already a working-list row — is absent from AddableBasics, while an absent basic like Wastes is offered), preserving canonical order.
    - `dotnet build DeckFlow.Web` clean; existing CutLabViewModel tests still green.
  </acceptance_criteria>
  <done>The view renders steppers from the tuned working list (incl added basics), not the raw pool, and add-basic offers only basics not already present.</done>
</task>

<task type="auto">
  <name>Task 2: Razor steppers + add-basic control + adjust form + CSS (iterate WorkingListRows)</name>
  <read_first>
    - DeckFlow.Web/Views/Deck/CutLab.cshtml (RenderDecisionForm local + sticky bar; the Model.Pool table is a MARKUP analog only — iterate Model.WorkingListRows for steppers)
    - DeckFlow.Web/wwwroot/css/site-common.css (existing cutlab-* classes to extend)
  </read_first>
  <action>
    Render a tuner section that iterates `Model.WorkingListRows` (NOT `Model.Pool`). For each row where
    `IsLegalMultiple`, render a `−` and `+` stepper pair inside an adjust form mirroring `RenderDecisionForm`:
    `<form method="post" action="~/cut-lab/adjust" data-cut-lab-adjust-form>` + `@Html.AntiForgeryToken()` +
    hidden CutLabStateJson + hidden CardName + hidden Delta (−1 / +1) + hidden IsAddedBasic=false, with a submit
    button carrying `data-cut-lab-adjust`, `data-cut-lab-card`, and `data-cut-lab-delta`. Show the row's
    `CurrentQuantity`. Disable `−` when CurrentQuantity == 0 and `+` when CurrentQuantity >= LegalMax (add
    `disabled` + `aria-disabled`). Do NOT render steppers on rows where IsLegalMultiple == false. Add an "Add
    basic land" control (a `data-df-select` dropdown of Model.AddableBasics + an add button) posting the same
    adjust form with IsAddedBasic=true and Delta=+1. Add CSS for the stepper group + add-basic control to
    site-common.css using existing `cutlab-*` naming and `--cutlab-*` tokens (never site.css). No new
    WorkflowStepTab — the count gate + Export tab from Phase 105 stay unchanged.
  </action>
  <verify>
    <automated>cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit 2>&1 | tail -5; grep -c "cut-lab/adjust" Views/Deck/CutLab.cshtml</automated>
  </verify>
  <acceptance_criteria>
    - The tuner iterates `Model.WorkingListRows` (grep the view for the WorkingListRows loop) — NOT `Model.Pool` — so added basics appear and cut cards do not.
    - Steppers render on legal-multiple rows only; `+` disabled at LegalMax, `−` disabled at quantity 0 (server still enforces regardless).
    - An add-basic control posts IsAddedBasic=true with the selected basic name, offering only Model.AddableBasics (basics not already in the working list).
    - Stepper/add-basic CSS lives in site-common.css using `cutlab-*` classes; site.css is untouched.
    - `dotnet build DeckFlow.Web` clean (Razor compiles).
  </acceptance_criteria>
  <done>The inline tuner renders from the derived working list with correct enable/disable states and a no-JS-capable adjust form.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 3: cut-lab.ts adjust wiring + vitest</name>
  <read_first>
    - DeckFlow.Web/wwwroot/ts/cut-lab.ts (handleDecisionSubmit, getStickyRemaining, updateExportTabState/getExportStepTab, goals-form interception)
    - DeckFlow.Web/wwwroot/ts/__tests__/ (existing vitest specs for the analog pattern; match the harness)
  </read_first>
  <behavior>
    - Submitting an adjust form via JS POSTs to /api/cut-lab/adjust, writes the returned CutLabStateJson back into the hidden state inputs, sets the sticky remaining text to `${cardsRemaining} to cut`, and calls the export-tab enablement update.
    - When cardsRemaining reaches 0 after an adjust, the Export tab becomes enabled; when it moves off 0, it is disabled.
    - A non-OK adjust response surfaces the server message (mirroring decide error handling) and does not corrupt the hidden state.
  </behavior>
  <action>
    Add `cutLabAdjustApiEndpoint = '/api/cut-lab/adjust'` and an adjust submit handler mirroring
    handleDecisionSubmit: intercept `form[data-cut-lab-adjust-form]` submits, POST the serialized body, on success
    patch every hidden `CutLabStateJson` input, update `[data-cut-lab-sticky-remaining]` via getStickyRemaining,
    and call the Export-tab enablement updater (updateExportTabState) with atTarget = (cardsRemaining === 0). On
    error, reuse the existing readErrorMessage/error-banner path. Keep the no-JS form working when JS is absent
    (progressive enhancement — do not `preventDefault` unless the fetch path is taken). Add a vitest under
    wwwroot/ts/__tests__ covering the success count-patch + export-tab toggle and the error path, matching the
    existing test harness (no new framework/package).
  </action>
  <verify>
    <automated>cd DeckFlow.Web && npx --no-install vitest run 2>&1 | tail -8</automated>
  </verify>
  <acceptance_criteria>
    - cut-lab.ts intercepts `data-cut-lab-adjust-form`, POSTs to /api/cut-lab/adjust, and patches the sticky count from the response.
    - After an adjust that yields cardsRemaining === 0, the Export tab enablement updater is invoked with atTarget true.
    - vitest covers success (count patch + export-tab toggle) and error (message surfaced, state uncorrupted); all vitest green.
    - `tsc -p tsconfig.json --noEmit` clean (strict); no new npm dependency added.
  </acceptance_criteria>
  <done>JS enhances the adjust form with live count + export-tab updates; no-JS still full-renders.</done>
</task>

</tasks>

<verification>
- `dotnet build DeckFlow.sln` clean; Razor compiles.
- `tsc -p tsconfig.json --noEmit` clean; `npx --no-install vitest run` green.
- Steppers render from `Model.WorkingListRows` (adjustment-derived), not `Model.Pool` — grep-confirmed in the view.
- AddableBasics excludes basics already present in WorkingListRows (unit test).
- UI change gate: verify both guild themes and mobile+desktop viewports render the steppers/add-basic correctly
  (screenshots captured in 106-05 e2e). Layout CSS confirmed in site-common.css, not site.css.
- LF endings preserved; changed-lines format-gate clean; do NOT stage compiled wwwroot/js/*.js.
</verification>

<success_criteria>
The Decide workspace shows inline +/- steppers (disabled at legal bounds) and an add-basic control rendered from
the adjustment-derived working list (added basics included, cut cards excluded; add-basic offers only basics not
already present); applying one updates the sticky remaining-to-100 count and Export-tab enablement live (JS) or
via full re-render (no-JS), with no new step tab and reusing the established cutlab visual language.
</success_criteria>

<line_endings>
Preserve each touched file's existing line endings exactly (LF via .gitattributes). New files (vitest spec) use
LF. Change only the lines whose content changes; leave the rest byte-for-byte identical. Never stage compiled
`wwwroot/js/*.js` (gitignored; rebuilt at deploy).
</line_endings>

<output>
Create `.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-04-SUMMARY.md` when done.
</output>
