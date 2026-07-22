---
phase: 106-partial-quantity-tuning
plan: 02
type: execute
wave: 2
depends_on: ["106-01"]
autonomous: true
requirements: [EDIT-01, EDIT-03]
files_modified:
  - DeckFlow.Web/Models/CutLabViewModel.cs
  - DeckFlow.Web/Services/CutLab/CutLabPageService.cs
  - DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs
  - DeckFlow.Web/Services/CutLab/CutLabExportService.cs
  - DeckFlow.Web/Services/CutLab/CutLabWhatifPreviewService.cs
  - DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs
  - DeckFlow.Web/Controllers/Api/CutLabApiController.cs
  - DeckFlow.Web/Controllers/CutLabController.cs
  - DeckFlow.Web.Tests/CutLabWorkingListTests.cs
  - DeckFlow.Web.Tests/CutLabDecisionApplierTests.cs
  - DeckFlow.Web.Tests/CutLabPageServiceTests.cs
  - DeckFlow.Web.Tests/CutLabApiControllerTests.cs
  - DeckFlow.Web.Tests/CutLabAnalysisContextBuilderTests.cs
  - DeckFlow.Web.Tests/CutLabWhatifTests.cs

must_haves:
  truths:
    - "Every consumer that derives the working list reads the adjustment-derived list, so count, analysis, roles/floors, simulation and export all agree"
    - "The sticky remaining-to-100 count and the round engine's CardsRemainingToTarget reflect quantity adjustments"
    - "The role-floor DISPLAY counts in the view model are computed from the adjustment-derived list, not the immutable original pool"
    - "The overshoot guard in the decision applier measures the budget against the adjustment-derived list"
    - "State reconstruction in CutLabPageService.BuildState carries QuantityAdjustments forward, so a full-page (no-JS) ProcessAsync render never wipes adjustments"
    - "An added basic gets a land role and simulation facts, and is selectable as a what-if cardOut, WITHOUT any Scryfall network call — synthetic ScryfallCardData is injected into the resolved-card set"
  artifacts:
    - path: "DeckFlow.Web/Models/CutLabViewModel.cs"
      provides: "Count + role-floor display counts derived with QuantityAdjustments"
      contains: "QuantityAdjustments"
    - path: "DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs"
      provides: "Synthetic added-basic ScryfallCardData injected into the resolved-card set before network resolution"
      contains: "CutLabBasicLands"
    - path: "DeckFlow.Web/Services/CutLab/CutLabExportService.cs"
      provides: "Export working list derived with adjustments"
    - path: "DeckFlow.Web/Services/CutLab/CutLabPageService.cs"
      provides: "BuildState preserves QuantityAdjustments across reconstruction"
      contains: "QuantityAdjustments = priorState.QuantityAdjustments"
  key_links:
    - from: "CutLabViewModel.From"
      to: "CutLabWorkingList.Derive(pool, decisions, adjustments)"
      via: "currentCount + role-floor counts + working-list rows use the 3-arg overload"
      pattern: "Derive\\([^,]+, [^,]+, [^)]*QuantityAdjustments"
    - from: "CutLabDecisionApplier.Apply"
      to: "CutLabWorkingList.Derive(pool, decisions, adjustments)"
      via: "overshoot budget uses adjustment-derived list"
    - from: "CutLabPageService.BuildState"
      to: "CutLabState.QuantityAdjustments"
      via: "reconstruction copies the field forward (with { } or explicit initializer)"
      pattern: "QuantityAdjustments = priorState.QuantityAdjustments"
    - from: "CutLabAnalysisContextBuilder"
      to: "CutLabBasicLands.SyntheticCardData"
      via: "inject synthetic land card data for added basics before Scryfall resolution"
---

<objective>
Thread `state.QuantityAdjustments` through every place that derives the Cut Lab working list, so the
adjustment-derived list is the single source of truth for count, structural analysis, role/floor evaluation +
display, simulation, what-if, the overshoot guard, and export. Also (a) fix the one state-reconstruction site
(`CutLabPageService.BuildState`) that hand-rolls a `new CutLabState { … }` and would otherwise reset the new
field to `[]`, and (b) inject synthetic `ScryfallCardData` for added basics into the resolved-card set so an
added basic gets a land role + simulation facts and is a valid what-if target WITHOUT a Scryfall lookup. This is
the read-path + reconstruction fold; the write-path endpoints come in 106-03.

Purpose: Without the fold, adjustments persist but do not affect counts/metrics/export; without the BuildState
fix, no-JS renders wipe adjustments; without synthetic injection, materialized added basics get no role/sim
facts and break what-if pre-seeding (real code keys off ScryfallCardData, not CutLabPoolCard.TypeLine).
Output: All 10 production Derive call sites pass adjustments, BuildState carries the field forward, synthetic
basics are injected, and regression tests prove counts/metrics/export/role/what-if reflect adjustments.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-DESIGN.md
@.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-01-SUMMARY.md
@./CLAUDE.md

<interfaces>
The 10 production `CutLabWorkingList.Derive` call sites (found via grep; each currently passes only
(pool, decisions)):
- DeckFlow.Web/Controllers/Api/CutLabApiController.cs:79 (beforeWorkingList), :99 (afterWorkingList), :443 (ValidateWhatifPair)
- DeckFlow.Web/Controllers/CutLabController.cs:342 (IsValidWhatifPair)
- DeckFlow.Web/Models/CutLabViewModel.cs:170 (currentCount), :627 (working-list rows helper)
- DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs:42 (overshoot budget)
- DeckFlow.Web/Services/CutLab/CutLabExportService.cs:55 (keptWorkingList)
- DeckFlow.Web/Services/CutLab/CutLabWhatifPreviewService.cs:71 (beforeWorkingList)
- DeckFlow.Web/Services/CutLab/CutLabPageService.cs:265 (derivedWorkingList — feeds analysis + count)
(The other "Derive" grep matches are the xmldoc comment in CutLabState.cs and the method definition/overload in
CutLabWorkingList.cs — not call sites.)

Role-floor DISPLAY count desync (MED-1): CutLabViewModel.From (~lines 163-164) computes `countsByRole =
CountRoles(pool, …)` where `pool = result.State?.Pool` (line 147) and CountRoles sums `card.Quantity` off the
ORIGINAL pool (~lines 693-705); the floor UI renders these (CutLab.cshtml ~519-530, `row.InPoolCount`). After
adjustments these desync from the tuned list — must be computed from the same adjustment-derived working list.

Count source (already correct once the working list includes adjustments): CutLabCutRoundEngine computes
`CardsRemainingToTarget = workingList.Sum(Quantity) - 100` (~line 303) from the passed working list.

Overshoot guard (DecisionApplier.cs:42-49): `remaining = workingList.Sum(Quantity) - 100`; must derive with
adjustments so an added basic counts toward the budget.

BuildState reconstruction hazard (CutLabPageService.cs ~line 706): `BuildState(priorState, …)` returns a
hand-rolled `new CutLabState { Commander=…, Pool=…, Packages=…, Decisions=…, OriginalEntries=…, Goals=…,
BaselineSnapshot=…, RoleFloors=…, Intent=… }` — the ONLY reconstruction site that does NOT use `priorState with
{ … }`, and it has no QuantityAdjustments line. Because `CutLabState.QuantityAdjustments` defaults to `= []`,
every BuildState result silently resets adjustments to empty. `ProcessAsync → BuildState` runs on all full-page
render paths (initial import, no-JS Decide, no-JS Goals, no-JS what-if keep, the new no-JS Adjust action in
106-03, and scenario reload via Process). The `with { … }` sites (DecisionApplier.Apply/Restore,
EnforceCommanderLock) already carry the field correctly — BuildState is the lone exception.

Synthetic-basics injection (HIGH-1) — real code keys off ScryfallCardData, not CutLabPoolCard.TypeLine:
- CutLabAnalysisContextBuilder.cs:143-186 assigns a role + roleCounts + analyzed-card facts ONLY when
  `cardsByName.TryGetValue(name, out card)` (from `resolvedCards`) hits; a missing card gets roles = [] and is
  routed to Scryfall batch resolution (~:279-303, EnumerateMissingPoolCards → ScryfallReferenceResolver). So a
  materialized added basic with no ScryfallCardData gets NO land role, NO mana/flood sim facts, and triggers a
  network lookup.
- CutLabWhatifPreviewService.cs:133-155 (SeedResolvedSnapshotPool / BuildResolvedSubset): throws "could not
  pre-seed all resolved cards" when the resolved subset count != distinct target-pool count. An added basic not
  in the full-pool resolved cache makes the subset short → what-if throws, so an added basic cannot be a cardOut.
- CutLabApiController.TryBuildAfterPreResolvedCards / BuildPartialResolvedSubset (~:279-374) and
  CutLabExportService resolved-by-name building have the same "must be in the resolved set" assumption.
Fix mechanism (uses CutLabBasicLands.SyntheticCardData from 106-01): a single reusable augmentation that, given a
working list + a resolved-card set, appends `CutLabBasicLands.SyntheticCardData(name)` for every added-basic name
present in the working list but absent from the resolved set. Apply it in CutLabAnalysisContextBuilder BEFORE the
network/cache-subset logic (so roles + sim facts are assigned and no lookup fires) and in the what-if /
derived-pool seeding path (SeedResolvedSnapshotPool / TrySeedDerivedPool subset builders) so the subset count
matches. Prefer centralizing so all consumers benefit from the one augmentation.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Fold adjustments into every Derive consumer + role-floor display counts (MED-1)</name>
  <read_first>
    - DeckFlow.Web/Services/CutLab/CutLabWorkingList.cs (new 3-arg overload from 106-01)
    - DeckFlow.Web/Models/CutLabViewModel.cs (line 147 `pool=result.State.Pool`; :163-164 CountRoles; :170 currentCount; :627 working-list rows; :693-705 CountRoles impl)
    - DeckFlow.Web/Services/CutLab/CutLabPageService.cs (line ~265 — derivedWorkingList feeds analysis context, findings, simulation)
    - DeckFlow.Web/Services/CutLab/CutLabExportService.cs (line ~55), CutLabWhatifPreviewService.cs (line ~71), CutLabDecisionApplier.cs (line ~42)
    - DeckFlow.Web/Controllers/Api/CutLabApiController.cs (lines ~79/99/443), CutLabController.cs (line ~342)
  </read_first>
  <action>
    At each of the 10 production call sites, change `CutLabWorkingList.Derive(state.Pool, state.Decisions)` to
    pass the session's adjustments: `CutLabWorkingList.Derive(state.Pool, state.Decisions,
    state.QuantityAdjustments)` (or, in helpers that receive pool/decisions as parameters, thread the adjustments
    list through the signature from the owning state). In CutLabDecisionApplier.Apply, the overshoot-budget
    derive (line ~42) must use `state.QuantityAdjustments`. In CutLabPageService the single `derivedWorkingList`
    (line ~265) already fans out to analysis/findings/simulation — updating that one derive propagates adjustments
    downstream. MED-1: in CutLabViewModel.From compute the role-floor display counts from the adjustment-derived
    working list — derive the working list once (`CutLabWorkingList.Derive(pool, decisions,
    QuantityAdjustments)`) and pass THAT to `CountRoles(...)` (line ~163) and to `currentCount` (line ~170)
    instead of the raw `pool`. Do NOT change the round engine, simulation service, or analysis builder signatures.
    Touch only the lines that change; leave surrounding code byte-for-byte identical.
  </action>
  <verify>
    <automated>dotnet build DeckFlow.Web/DeckFlow.Web.csproj 2>&1 | tail -5</automated>
  </verify>
  <acceptance_criteria>
    - `grep -rn "CutLabWorkingList.Derive(" DeckFlow.Web --include=*.cs | grep -v ", state.QuantityAdjustments\|, adjustments\|, []\|, decisions)"` returns no production call passing only two arguments where a state is in scope.
    - CutLabViewModel role-floor display counts (`CountRoles`) are computed from the adjustment-derived working list; a test proves that after a +2 Island adjustment the displayed land-floor count reflects the tuned total.
    - `dotnet build DeckFlow.Web` clean, no new warnings.
  </acceptance_criteria>
  <done>Adjustments reach count, analysis, simulation, what-if, export, the overshoot guard, AND the role-floor display counts through one fold per consumer.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Inject synthetic added-basic ScryfallCardData into analysis + what-if resolved sets (HIGH-1)</name>
  <read_first>
    - DeckFlow.Web/Services/CutLab/CutLabAnalysisContextBuilder.cs (:133-196 role/sim assignment keyed off resolvedCards; :246-303 resolution/network path; TrySeedDerivedPool :207-216)
    - DeckFlow.Web/Services/CutLab/CutLabWhatifPreviewService.cs (:133-171 SeedResolvedSnapshotPool / BuildResolvedSubset count-must-match guard)
    - DeckFlow.Web/Services/CutLab/CutLabBasicLands.cs (SyntheticCardData factory from 106-01)
    - DeckFlow.Web.Tests/CutLabAnalysisContextBuilderTests.cs + CutLabWhatifTests.cs (analog fixtures; note the executeAsync/resolver fake seam)
  </read_first>
  <behavior>
    - An added basic (e.g. +2 Wastes / +1 Island) in the working list receives its land role and roleCounts contribution, and simulation facts (mana/flood) — with NO Scryfall call (a resolver fake that throws if invoked must not be hit for the basic).
    - The what-if pre-seed subset count includes the added basic, so an added basic is selectable as a what-if cardOut without the "could not pre-seed all resolved cards" throw.
    - A normal (non-basic) missing card still routes to resolution unchanged.
  </behavior>
  <action>
    Add a reusable augmentation (e.g. a private helper or a small static on CutLabBasicLands consumed here) that,
    given a working list and a resolved-card set, appends `CutLabBasicLands.SyntheticCardData(name)` for every
    added-basic name present in the working list but absent from the resolved set. In
    CutLabAnalysisContextBuilder, apply it to the resolved-card set BEFORE the missing-card/network resolution
    logic (so `cardsByName` contains the synthetic basic and role assignment + roleCounts + analyzed-card facts
    fire, and EnumerateMissingPoolCards no longer treats the basic as missing). In
    CutLabWhatifPreviewService.BuildResolvedSubset / SeedResolvedSnapshotPool, include synthetic basics so the
    subset count matches the distinct target-pool count. Keep the change centralized so the API controller /
    export subset builders inherit the same augmented resolved set where they flow through the context builder.
    Do not alter the network path for genuinely-unknown non-basic cards.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabAnalysisContextBuilderTests|FullyQualifiedName~CutLabWhatifTests" 2>&1 | tail -6</automated>
  </verify>
  <acceptance_criteria>
    - An xUnit test proves a +Wastes / +Island added basic has the "lands" role, contributes to roleCounts, and yields simulation facts — with a resolver/executeCollection fake that THROWS if hit, asserting no Scryfall call occurred for the basic.
    - An xUnit test proves an added basic is selectable as a what-if `cardOut` (no "could not pre-seed" throw).
    - A non-basic unknown card still routes to resolution (existing tests unaffected).
    - `dotnet build DeckFlow.Web` clean; named filters green.
  </acceptance_criteria>
  <done>Added basics analyze and simulate as lands and work in what-if with zero network dependency.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 3: Preserve QuantityAdjustments in CutLabPageService.BuildState</name>
  <read_first>
    - DeckFlow.Web/Services/CutLab/CutLabPageService.cs (the BuildState method, ~line 706 — the hand-rolled `new CutLabState { … }` that omits QuantityAdjustments)
    - DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs (analog: reconstruction via `state with { … }`, which carries every field forward automatically)
  </read_first>
  <behavior>
    - A ProcessAsync cycle on a state carrying a QuantityAdjustments entry returns a CutLabState that still contains that entry (BuildState no longer resets it to empty).
    - The serialized CutLabStateJson produced after a no-JS decide (or goals) cycle preserves the adjustments and the derived count stays exactly 100.
    - A full no-JS decide→adjust→reload sequence keeps the adjustment and does not silently drop it.
  </behavior>
  <action>
    In CutLabPageService.BuildState, add `QuantityAdjustments = priorState.QuantityAdjustments,` to the returned
    `new CutLabState { … }` object initializer (preferred: matches the surrounding explicit-initializer style).
    Alternatively convert the return to `priorState with { Commander = …, Pool = …, RoleFloors = …, Intent = … }`
    so every unlisted field (including QuantityAdjustments) is carried forward automatically — but if you keep the
    `new CutLabState { … }` form you MUST add the explicit QuantityAdjustments line. Do not change any other field
    mapping. Touch only the changed line(s); leave the rest byte-for-byte identical.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabPageServiceTests" 2>&1 | tail -6</automated>
  </verify>
  <acceptance_criteria>
    - BuildState's returned state sets QuantityAdjustments from priorState (explicit initializer line, or a `priorState with { … }` that omits QuantityAdjustments from the override list). `grep -n "QuantityAdjustments" DeckFlow.Web/Services/CutLab/CutLabPageService.cs` confirms preservation (or grep shows BuildState uses `priorState with`).
    - A new xUnit regression test exercises `ProcessAsync`/`BuildState` (NOT just the serializer) and proves a state with a QuantityAdjustments entry survives a no-JS decide-style ProcessAsync cycle, the entry is present in the returned CutLabState, and the derived count stays exactly 100.
    - `dotnet build DeckFlow.Web` clean; CutLabPageServiceTests filter all-green.
  </acceptance_criteria>
  <done>Full-page (no-JS) reconstruction preserves adjustments; the field can no longer be wiped by BuildState.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 4: Regression tests — counts, overshoot budget, and export reflect adjustments</name>
  <read_first>
    - DeckFlow.Web.Tests/CutLabDecisionApplierTests.cs (analog: overshoot-guard tests)
    - DeckFlow.Web.Tests/CutLabPageServiceTests.cs (analog: count/CardsRemaining assertions)
    - DeckFlow.Web.Tests/CutLabApiControllerTests.cs (analog: decide response CardsRemaining)
  </read_first>
  <behavior>
    - Adding a +2 Island adjustment raises the derived working-list count (and lowers CardsRemainingToTarget) by 2.
    - A -3 Island adjustment lowers the count by 3 (down to legal min 0, entry dropped at 0).
    - The overshoot guard treats an added-basic delta as consuming budget: it refuses an accept that would push the adjustment-derived list below 100.
    - Export's derived working list includes an added basic (it appears in the reconstructed final entries).
  </behavior>
  <action>
    Add xUnit tests asserting count/CardsRemainingToTarget deltas for +N / -N adjustments on an existing
    multi-copy basic, entry-drop at zero, the overshoot-guard interaction with an added basic, and that the
    export-service derived list includes a materialized added basic. Reuse existing test fixtures/builders in
    these files; do not add a new mocking library or framework.
  </action>
  <verify>
    <automated>dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~CutLabDecisionApplierTests|FullyQualifiedName~CutLabPageServiceTests|FullyQualifiedName~CutLabWorkingListTests" 2>&1 | tail -6</automated>
  </verify>
  <acceptance_criteria>
    - New tests prove count and CardsRemainingToTarget move by the adjustment delta.
    - A test proves the overshoot guard counts an added basic toward the 100 budget.
    - A test proves the export-derived working list contains an added basic.
    - All named filters green; no new package/framework introduced.
  </acceptance_criteria>
  <done>Adjustment effects on count, budget, and export are locked by regression tests.</done>
</task>

</tasks>

<verification>
- `dotnet build DeckFlow.sln` clean.
- Web.Tests suites green (existing + new synthetic-basics, BuildState-preservation, role-count, and regression tests).
- No production `CutLabWorkingList.Derive(pool, decisions)` two-arg call remains where adjustments are available.
- BuildState preserves QuantityAdjustments (grep-confirmed); an added basic analyzes/simulates and is a valid what-if cardOut with zero Scryfall calls.
- LF endings preserved; changed-lines format-gate clean.
</verification>

<success_criteria>
The adjustment-derived working list is the single source of truth: count, structural analysis, role/floor
evaluation + display, simulation, what-if, the overshoot guard, and export all reflect quantity adjustments;
full-page reconstruction preserves adjustments; and added basics analyze/simulate/what-if with no Scryfall
lookup — all proven by regression tests. No new UI or endpoints yet.
</success_criteria>

<line_endings>
Preserve each touched file's existing line endings exactly (LF via .gitattributes). Change only the lines whose
content changes; leave all other lines and their endings byte-for-byte identical. Do not reflow untouched code.
</line_endings>

<output>
Create `.planning/workstreams/cut-lab/phases/106-partial-quantity-tuning/106-02-SUMMARY.md` when done.
</output>
