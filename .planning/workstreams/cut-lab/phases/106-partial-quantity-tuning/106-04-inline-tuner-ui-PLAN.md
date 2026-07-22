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
    - "CutLabViewModel.CurrentCount is sourced from the same 3-arg adjustment-derived working list as WorkingListRows, so the sticky count and the Export-enable gate reflect tuning"
    - "Both Export enablement gates (the Export step tab and the Build-export submit button) gate on currentCount == 100 (exact 100), so they never enable when the tuned list is under or over 100"
    - "The proposal panel renders a distinct 'under 100 cards' branch when currentCount < 100 (newly reachable in Phase 106), separate from the at-100 and over-100 states"
    - "Each basic / legal-multiple working-list row shows +/- steppers that are disabled at the legal bounds (min 0, singleton max)"
    - "An add-basic control lets the user add any of the known basics not already in the pool"
    - "Applying a stepper or add-basic updates the sticky remaining-to-100 count and re-evaluates the Export tab enablement without a full reload (JS), and full-page re-renders without JS"
    - "No new step tab is added to the workflow"
  artifacts:
    - path: "DeckFlow.Web/Views/Deck/CutLab.cshtml"
      provides: "Inline +/- steppers + add-basic control + adjust form (JSON + no-JS), iterating the derived working-list rows; both Export gates on currentCount == 100; under-100 proposal branch"
      contains: "cut-lab/adjust"
    - path: "DeckFlow.Web/Models/CutLabViewModel.cs"
      provides: "Adjustment-derived WorkingListRows projection + adjustment-aware CurrentCount"
      contains: "QuantityAdjustments"
    - path: "DeckFlow.Web/wwwroot/ts/cut-lab.ts"
      provides: "Stepper + add-basic wiring to /api/cut-lab/adjust with count + export-tab patch"
      contains: "adjust"
  key_links:
    - from: "CutLabViewModel WorkingListRows + CurrentCount"
      to: "CutLabWorkingList.Derive(pool, decisions, QuantityAdjustments)"
      via: "single 3-arg derivation reused for rows AND currentCount"
      pattern: "Derive\\([^)]*QuantityAdjustments"
    - from: "CutLab.cshtml Export tab + Build-export submit"
      to: "Model.CurrentCount == 100"
      via: "both Export gates key off exact-100, not floored remaining"
      pattern: "currentCount == 100"
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
immutable `Model.Pool` — so added basics appear and cut entries do not. `CurrentCount` is re-sourced from that
same derivation, BOTH Export enablement gates key off exact `currentCount == 100`, and a new under-100
proposal-panel branch covers the newly-reachable sub-100 state — so the sticky count and the Export gate actually
track tuning. JS patches the sticky remaining-to-100 count and re-evaluates the Export-tab enablement; no-JS
full-page re-renders. No new step tab. Reuses the existing decide/goals visual + interaction language and cutlab
CSS tokens (per 106-DESIGN UI section, 106-UI-SPEC.md, and the ui_contract_note).

Purpose: EDIT-01/02/03 surfaced to the user inline, mirroring the established progressive-enhancement contract.
Output: ViewModel derived-rows + adjustment-aware CurrentCount + Razor (steppers, add-basic, exact-100 gates,
under-100 branch) + CSS + TS + a vitest for the JS wiring.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-DESIGN.md
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-UI-SPEC.md
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
- Proposal-panel branch chain (lines 971-984): today `else if (Model.Proposal.IsAtTarget)` ("You're at 100
  cards") then `else if (Model.Proposal.IsNothingToCut)` — there is NO under-100 branch (pre-106 the list could
  never go under 100). Phase 106 makes the sub-100 state reachable, so a new `else if (currentCount < 100)` branch
  is required BEFORE the `IsAtTarget` branch (see 106-UI-SPEC.md Component 4 / Copywriting Contract).
- Export gates — there are TWO, plus a third already-correct check:
  - `var currentCount = Model.CurrentCount;` (line 11) and `var cardsRemainingToTarget = Model.StickyBar.CardsRemainingToCut;` (line 10).
  - Export STEP TAB (line 22): `new WorkflowStepTab(4, "Export", …, IsEnabled: cardsRemainingToTarget == 0, …)`.
  - Build-export SUBMIT button (line 768): `@(cardsRemainingToTarget == 0 ? null : "disabled")`.
  - The export-panel validation at line 716 already uses `currentCount != 100` (the correct exact-100 form) — mirror it in the two gates above.
  `cardsRemainingToTarget` floors at 0, so once Phase 106 lets the tuned list go UNDER 100 it can enable prematurely; both gates must move to exact `currentCount == 100`.

TS analogs (DeckFlow.Web/wwwroot/ts/cut-lab.ts):
- `cutLabDecisionApiEndpoint = '/api/cut-lab/decide'` (line 170) → add `cutLabAdjustApiEndpoint = '/api/cut-lab/adjust'`.
- `handleDecisionSubmit` (lines 1973-2010): fetch + JSON body + reads response, updates hidden state + sticky
  count via `getStickyRemaining()` (lines 1881-1883) → mirror for adjust.
- `updateExportTabState`/`getExportStepTab` (lines 1707-1714): toggles Export tab disabled by atTarget — call
  after an adjust response.
- The `[data-cut-lab-goals-form]` submit interception (line 2566) is the analog no-JS-form-intercept wiring.

ViewModel (DeckFlow.Web/Models/CutLabViewModel.cs): `pool = result.State?.Pool` (line 147) is the immutable pool.
`CurrentCount` (property line 123; computed at `:170` and assigned at `:224`) is currently
`CutLabWorkingList.Derive(pool, decisions).Sum(Quantity)` — the 2-arg overload, NO adjustments. Add a NEW
`WorkingListRows` collection built from `CutLabWorkingList.Derive(pool, decisions,
result.State.QuantityAdjustments)` (the 3-arg overload, so materialized added basics are included and cut entries
excluded), and re-source `currentCount` (line 170) from that SAME single derivation (derive once, reuse for both
rows and the count). Each row carries Name, CurrentQuantity (from the derived entry), `IsLegalMultiple` +
`LegalMax` (from CutLabLegality), and `IsAddedBasic`. Also expose `AddableBasics` = the canonical basics list
(Plains, Island, Swamp, Mountain, Forest, then Snow-Covered variants, then Wastes) FILTERED to names NOT already
present in WorkingListRows (a basic already in the working list is tuned with its steppers, so the add-basic
control only offers basics absent from the derived list). The after-106-02 role assignment covers added basics
(synthetic ScryfallCardData), so role labels on these rows work.

CSS: layout goes in site-common.css only (never site.css); reuse existing `cutlab-*` tokens/classes and the
`--cutlab-*` theme seam. Do not invent a new visual language.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: ViewModel adjustment-derived WorkingListRows + adjustment-aware CurrentCount + legality/addable-basics</name>
  <read_first>
    - DeckFlow.Web/Models/CutLabViewModel.cs (line 147 `pool=result.State.Pool`; :170 currentCount computation; :224 CurrentCount assignment; :123 CurrentCount property; :627 working-list rows helper — build rows AND currentCount from the derived list, NOT Model.Pool)
    - DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs (the 3-arg Derive overload from 106-01)
    - DeckFlow.Web/Services/CutLab/CutLabLegality.cs + CutLabBasicLands.cs (106-01: legality predicate + basics names)
  </read_first>
  <action>
    In CutLabViewModel.From, derive the working list ONCE via `CutLabWorkingList.Derive(pool,
    result.State?.Decisions ?? [], result.State?.QuantityAdjustments ?? [])` and reuse it for two things:
    (1) re-source `currentCount` (line 170) as `derivedWorkingList.Sum(card => card.Quantity)` so `CurrentCount`
    (assigned at :224) is adjustment-aware — replace the 2-arg `Derive(pool, decisions)` call there; and
    (2) project a new `IReadOnlyList<CutLabTunableRowView> WorkingListRows` where each row = { Name,
    CurrentQuantity (the derived entry's Quantity), IsLegalMultiple + LegalMax (via CutLabLegality), IsAddedBasic,
    plus the role label from `result.RoleAssignmentsByCardName` }. This collection INCLUDES materialized added
    basics and EXCLUDES cut entries — it is the render source for steppers, not Model.Pool. Add `AddableBasics` =
    CutLabBasicLands.Names in canonical order (Plains, Island, Swamp, Mountain, Forest, then Snow-Covered
    variants, then Wastes) FILTERED to exclude any basic already present in WorkingListRows (ordinal-normalized
    name match), so the add-basic control never offers a duplicate of a basic the user can already tune with
    steppers. Add the `CutLabTunableRowView` record (mirror existing row view records) and surface both new
    members on CutLabViewModel without removing existing fields. Keep `{ get; init; }` accessors.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - `CutLabViewModel.CurrentCount` is computed from the SAME 3-arg adjustment-derived working list as `WorkingListRows` (single derivation reused); a test proves that after a +2 Island adjustment `Model.CurrentCount` increases by 2 and the sticky remaining-to-100 reflects it.
    - No production 2-arg `CutLabWorkingList.Derive(pool, result.State?.Decisions ?? [])` remains for the currentCount computation in CutLabViewModel.
    - CutLabViewModel exposes `WorkingListRows` built from the 3-arg Derive — a test proves it includes a materialized added basic and excludes an accepted-cut card, with per-row CurrentQuantity reflecting the tuned quantity.
    - Each row exposes `IsLegalMultiple`, `LegalMax`, `CurrentQuantity`, `IsAddedBasic`.
    - `AddableBasics` excludes any basic already present in WorkingListRows (a test proves e.g. Island — already a working-list row — is absent from AddableBasics, while an absent basic like Wastes is offered), preserving canonical order.
    - `dotnet build DeckFlow.Web` clean; existing CutLabViewModel tests still green.
  </acceptance_criteria>
  <done>CurrentCount and the tuner rows both come from the single adjustment-derived working list; add-basic offers only basics not already present.</done>
</task>

<task type="auto">
  <name>Task 2: Both Export enablement gates key off exact currentCount == 100</name>
  <read_first>
    - DeckFlow.Web/Views/Deck/CutLab.cshtml (line 10-11 the two locals; line 22 Export step tab IsEnabled; line 768 Build-export submit disabled; line 716 the already-correct `currentCount != 100` validation to mirror)
  </read_first>
  <action>
    Change BOTH Export enablement gates from the floored `cardsRemainingToTarget == 0` to exact
    `currentCount == 100` so they stay consistent with the line-716 validation and never enable when the tuned
    list is under or over 100: (1) line 22 — `new WorkflowStepTab(4, "Export", …, IsEnabled: currentCount == 100,
    …)`; (2) line 768 — `@(currentCount == 100 ? null : "disabled")` on the Build-export submit button. `Model`'s
    `currentCount` local is already declared at line 11. Do NOT change the Decide step tab's `IsComplete` (line 20)
    or the sticky-bar text. Touch only the two gate expressions; leave surrounding markup byte-for-byte identical.
  </action>
  <verify>
    <automated>cd DeckFlow.Web && grep -n "IsEnabled: currentCount == 100\|currentCount == 100 ? null" Views/Deck/CutLab.cshtml; dotnet build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | tail -3</automated>
  </verify>
  <acceptance_criteria>
    - The Export step tab (line 22) and the Build-export submit (line 768) both gate on `currentCount == 100` (grep confirms both).
    - At currentCount = 95 both the Export tab and the Build-export submit are disabled; at exactly 100 both enable; over 100 both disabled — proven by a view-model/rendering test or e2e assertion (e2e coverage lands in 106-05).
    - No other gate (Decide `IsComplete`, sticky text) is changed.
    - `dotnet build DeckFlow.Web` clean (Razor compiles).
  </acceptance_criteria>
  <done>Tab + submit Export gates are consistent and exact-100, immune to the under-100 premature-enable bug.</done>
</task>

<task type="auto">
  <name>Task 3: Razor steppers + add-basic control + under-100 proposal branch + adjust form + CSS (iterate WorkingListRows)</name>
  <read_first>
    - DeckFlow.Web/Views/Deck/CutLab.cshtml (RenderDecisionForm local + sticky bar; the proposal-panel branch chain at lines 971-984; the Model.Pool table is a MARKUP analog only — iterate Model.WorkingListRows for steppers)
    - .planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-UI-SPEC.md (Component 4 + Copywriting Contract — exact under-100 copy strings)
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
    adjust form with IsAddedBasic=true and Delta=+1. UNDER-100 PROPOSAL BRANCH (additive, Razor-only): in the
    proposal-panel branch chain (lines 971-984), insert an `else if (currentCount < 100)` branch BEFORE the
    existing `else if (Model.Proposal.IsAtTarget)` branch, rendering the exact spec copy — heading `You're under
    100 cards`, body `Add copies or a basic land in the Tune quantities section below to reach exactly 100.` —
    using the same `cutlab-proposal` / `cutlab-proposal__heading` markup as the sibling branches. Add CSS for the
    stepper group + add-basic control to site-common.css using existing `cutlab-*` naming and `--cutlab-*` tokens
    (never site.css). No new WorkflowStepTab.
  </action>
  <verify>
    <automated>cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit 2>&1 | tail -5; grep -c "cut-lab/adjust" Views/Deck/CutLab.cshtml; grep -n "You're under 100 cards" Views/Deck/CutLab.cshtml</automated>
  </verify>
  <acceptance_criteria>
    - The tuner iterates `Model.WorkingListRows` (grep the view for the WorkingListRows loop) — NOT `Model.Pool` — so added basics appear and cut cards do not.
    - Steppers render on legal-multiple rows only; `+` disabled at LegalMax, `−` disabled at quantity 0 (server still enforces regardless).
    - An add-basic control posts IsAddedBasic=true with the selected basic name, offering only Model.AddableBasics (basics not already in the working list).
    - The proposal panel shows the under-100 copy (`You're under 100 cards` / `Add copies or a basic land in the Tune quantities section below to reach exactly 100.`) when `currentCount < 100`, as a distinct branch from the at-100 (`IsAtTarget`) and over-100 states, inserted before the `IsAtTarget` branch (grep confirms the exact heading string).
    - Stepper/add-basic CSS lives in site-common.css using `cutlab-*` classes; site.css is untouched.
    - `dotnet build DeckFlow.Web` clean (Razor compiles).
  </acceptance_criteria>
  <done>The inline tuner renders from the derived working list with correct enable/disable states, a no-JS-capable adjust form, and a dedicated under-100 proposal-panel message.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 4: cut-lab.ts adjust wiring + vitest</name>
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
- `CutLabViewModel.CurrentCount` is adjustment-derived (single derivation reused with WorkingListRows).
- Both Export gates (tab line 22 + submit line 768) key off `currentCount == 100` — grep-confirmed.
- The under-100 proposal branch renders the exact spec copy and precedes the `IsAtTarget` branch — grep-confirmed.
- Steppers render from `Model.WorkingListRows` (adjustment-derived), not `Model.Pool` — grep-confirmed in the view.
- AddableBasics excludes basics already present in WorkingListRows (unit test).
- UI change gate: verify both guild themes and mobile+desktop viewports render the steppers/add-basic correctly
  (screenshots captured in 106-05 e2e). Layout CSS confirmed in site-common.css, not site.css.
- LF endings preserved; changed-lines format-gate clean; do NOT stage compiled wwwroot/js/*.js.
</verification>

<success_criteria>
The Decide workspace shows inline +/- steppers (disabled at legal bounds) and an add-basic control rendered from
the adjustment-derived working list (added basics included, cut cards excluded; add-basic offers only basics not
already present); CurrentCount is adjustment-aware, both Export gates key off exact currentCount == 100, and a
dedicated under-100 proposal branch covers the sub-100 state; applying a tuner action updates the sticky
remaining-to-100 count and Export-tab enablement live (JS) or via full re-render (no-JS), with no new step tab and
reusing the established cutlab visual language.
</success_criteria>

<line_endings>
Preserve each touched file's existing line endings exactly (LF via .gitattributes). New files (vitest spec) use
LF. Change only the lines whose content changes; leave the rest byte-for-byte identical. Never stage compiled
`wwwroot/js/*.js` (gitignored; rebuilt at deploy).
</line_endings>

<output>
Create `.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-04-SUMMARY.md` when done.
</output>
