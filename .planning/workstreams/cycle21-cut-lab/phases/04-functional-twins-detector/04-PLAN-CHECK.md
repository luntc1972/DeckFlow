# Phase 4 (Functional-Twins Detector) — Plan-Quality Gate

> ⚠ **HISTORICAL RECORD — dated 2026-07-29, checked against the PRE-REBASE tree.** Superseded by the
> claim-vs-code review of 2026-08-01 (`04-REVIEWS.md`), run after the branch was rebased onto `main`
> and 46 commits of Scryfall and combo-ordering work landed. Where the two disagree, the newer review
> wins. In particular this document still refers to test names and to an
> `ExcludedFindingKindsFromTally` membership that the plans no longer specify. Do not execute from
> this file; it is kept for provenance.

**Verdict: FAIL**

Scope: `04-01-PLAN.md` .. `04-04-PLAN.md`, cross-checked against `04-RESEARCH.md`,
`REQUIREMENTS.md` (TWIN-01..04), `ROADMAP.md` Phase 4 section + release-posture row 4, and the live
source tree in `/mnt/c/users/chrislunt/source/personal/deckflow-role-floors`. Read-only; no plan or
source file was edited as part of this check.

**Overall assessment.** This is an unusually well-researched and well-specified plan set — nearly
every file:line citation I spot-checked against live source (enum contents, `Compute` signature,
`CutLabAnalyzedCard` record shape, `ExcludedFindingKindsFromTally` contents, `BuildFindingsAndRoundPlan`
call sites, ctor shapes, `IsFlagOn` pattern, seed-row lines, `CutLabCommanderNames.Resolve`,
`CutLabFindingPresenter.BuildFindingGroups` algorithm, `CutLabFloorRules.RoleKeys`, `CardTypeLine`
priority order) matched exactly. TWIN-02's ranking-change proof is genuinely rigorous — it requires a
named `NextProposal` card-name change plus a mutation check that kills the test if the kind is added
back to the exclusion set, not merely "the finding exists." The scope fence around the five existing
detectors is enforced both by explicit prohibition and by an executable regression test with a
mutation check. Density validation (Success Criterion 5) is handled by both a real, carefully
controlled synthetic fixture (diverse, with a homogeneous control, near-miss clusters, and both a
floor and a ceiling bound) and a blocking human-verify checkpoint — not hand-waved.

Despite that, **one BLOCKER was found**: plan 04-03's ctor change breaks compilation in three test
files that are not in its declared scope, and the plan's own end-of-plan verification requires one of
those exact files to pass. This is fixable with a small, mechanical addendum (the same pattern the
plan already specifies for the one file it *did* account for), but as written the plan cannot reach
its own stated exit condition.

---

## TWIN-01..04 Coverage Table

| Req | Claimed by | Genuinely proven or merely asserted? |
|---|---|---|
| TWIN-01 (group by role ∩ exact MV ∩ primary type, fires at ≥3 distinct) | 04-01 (fields), 04-02 (detector + 9 tests: threshold, each dimension isolated, type-priority, distinct-count not `Sum(Quantity)`), 04-04 (merge into one section) | **Proven.** Each grouping dimension is isolated in its own test (tests 2-9 in 04-02 Task 2); the D-14 exact-MV rule additionally carries a mutation check that must fail if the bucket helper is substituted back in. |
| TWIN-02 (discriminating, changes `NextProposal`) | 04-03 (thread gate, 6 tests + reflection assert + mutation check) | **Proven, not merely asserted.** `BuildQueue_FunctionalTwinsChangesNextProposal` requires the *specific* card name to differ between two runs (not `Assert.NotEqual` alone), and the mutation check requires 5 of 6 new tests to fail if the kind is re-added to `ExcludedFindingKindsFromTally`. This is exactly the rigor the task brief asked me to check for. |
| TWIN-03 (evidence + finding emission ordered highest-MV-first) | 04-02 (within-group + cross-group order, determinism test), 04-04 (merged-section arrival-order preservation, with a `grep` acceptance criterion banning any `OrderBy`/`Sort`/`Reverse` in the merge branch) | **Proven** at both the detector and the view layer, with the D-14 degenerate-within-group case explicitly named and tested. |
| TWIN-04 (lock/commander excluded from groups; combo-protected still composes) | 04-01 (fields + hoisted `isCommander`), 04-02 (eligibility filter + exclusion tests + **compose-not-suppress mutation check**), 04-03 (independent `eligibleCards` proposal-queue gate, test 4) | **Proven**, including defence-in-depth: even a forged pool state that slips a locked/commander card into a twin group still cannot reach `NextProposal`, because `BuildQueue`'s `eligibleCards` filter is independent of the tally (04-03 Task 2 test 4). |

No requirement is satisfied only by assertion; every one has at least one test whose stated failure
mode is checked by a paired mutation test. That is the single strongest aspect of this plan set.

## Can it deploy dark? **Conditionally yes — blocked only by the BLOCKER below, not by design.**

The flag design itself is sound: seeded OFF in both dialects (verified: exactly 2 grep hits after
04-01, `FALSE`/`0` literals correct), catalog description explicitly names the proposal-order
consequence, `IFeatureFlagCache` is already registered at `Program.cs:113` (verified live) so no new
DI wiring is needed, and all three transports are threaded through **one** shared
`CutLabCutRoundEngine.BuildFindingsAndRoundPlan` (verified: exactly 4 call sites total, 3 production +
1 test, matching the plan's own claim). Critically, I independently verified the plan's central
architectural claim — that `context.AnalyzedCards` is freshly rebuilt via `_contextBuilder.BuildAsync`
on **every** call across all three transports (page, AJAX, patch builder), unlike Phase 3's
`state.RoleFloors`, which was a persisted user-set-only subset. That distinction is real in the current
code, so D-19's claim that "the Phase-3 dual-path hazard does not apply here" holds up. All three
transports read the same `FunctionalTwinsFlagKey` via the same fail-safe `Snapshot().TryGetValue(...)
&& enabled` pattern (never `IsEnabled`), and a missing-key path is proven OFF with its own mutation
check (04-03 Task 3, tests 3/6/9). So: **yes, this design can deploy dark**, but only once the BLOCKER
finding below is fixed, because as written plan 04-03 cannot reach a clean build of the full solution.

## D-16 (multi-role double-count) read

Well-specified, not ambiguous. The decision text states the exact mechanism (iterate roles
independently; a multi-role card can produce two separate `FunctionalTwins` findings), states the
exact consequence (+2 tally, reaches round 1 on twins alone), states *why* that's defensible (a card
filling two over-saturated slots at the same cost/type is genuinely twice as redundant), and is pinned
by an explicit test (`BuildQueue_MultiRoleCardWithTwoTwinsFindings_ReachesRound1`, 04-03 Task 2 test 5)
with a `// Why:` comment telling a future developer exactly what to change if they override it. An
executor could not misinterpret this or need to guess. This is the single decision the planner flagged
as most likely to be overridden, and it is the best-specified decision in the plan set — no notes.

---

## Findings

### BLOCK

**B1 — Plan 04-03's required-`IFeatureFlagCache`-constructor change breaks compilation in three test
files that are outside its declared scope, and the plan's own `<verification>` block requires one of
those exact files to pass.**

`04-03-PLAN.md` Task 1 makes `IFeatureFlagCache` a **required, non-nullable** constructor parameter on
both `CutLabUiPatchBuilder` and `CutLabApiController` (D-19, by design — this is correct engineering,
not the bug). Task 1's `<files>` (line 239) and the plan's `files_modified` frontmatter (lines 7-14)
list only the two production files plus `CutLabUiPatchBuilder.cs`/`CutLabApiController.cs`. Task 2's
`<files>` (line 378) declares only `DeckFlow.Web.Tests/CutLabCutRoundEngineTests.cs` and
`DeckFlow.Web.Tests/CutLabUiPatchBuilderTests.cs`, and Task 2's action text explicitly fixes the one
known break at `CutLabUiPatchBuilderTests.cs:793`/`CreateBuilder` (lines 825-832 in the live file,
confirmed) plus "any `new CutLabApiController(` construction… grep for it across `DeckFlow.Web.Tests`
and fix every site" — but that grep instruction is attached only to `CutLabApiController` sites, and
even so, none of the three files below are declared anywhere in the plan.

Verified live (not from the plan's claims) that all three of the following construct one or both of
`CutLabApiController`/`CutLabUiPatchBuilder` with the **current** (pre-Phase-4) constructor arity, and
will fail to compile the moment Task 1 lands:
- `DeckFlow.Web.Tests/CutLabApiControllerTests.cs:34` (`Constructor_ThrowsArgumentNullException_WhenPatchBuilderIsNull`, 6-arg `new CutLabApiController(...)`) and its `CreateController` factory at `:857-872` (target-typed `new(...)` for `CutLabApiController`, plus a nested `new CutLabUiPatchBuilder(builder, simulation, resolvedFloorResolver)` at `:869`) — used by essentially every test in the file.
- `DeckFlow.Web.Tests/CutLabAjaxFloorByRoleRegressionTests.cs:336-359` (`CreateApiController` factory: target-typed `new(...)` for `CutLabApiController` at `:353`, plus `new CutLabUiPatchBuilder(analysisBuilder, new FakeSimulationService(), floorResolver)` at `:356`).
- `DeckFlow.Web.Tests/CutLabWhatifTests.cs:509-531` (`CreateController` factory: target-typed `new(...)` for `CutLabApiController` at `:525`, plus `new CutLabUiPatchBuilder(contextBuilder, simulationService, floorResolver)` at `:528`).

None of these three files appears in `04-03-PLAN.md`'s `files_modified` frontmatter or in either
task's `<files>` tag — so a Codex dispatch that hard-fences edits to the declared file list (this
org's standard practice, per `feedback_codex_scope_fence.md`) cannot make these fixes even though they
are required for the solution to build.

This directly contradicts the plan's own `<verification>` section (lines ~578-585), which requires:
`"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln -c Release` full suite green, "**including**
… `CutLabAjaxFloorByRoleRegressionTests`". As written, that file cannot even compile, let alone pass.

**Note on the acceptance-criteria grep itself:** Task 2's own acceptance criterion — `grep -rn 'new
CutLabApiController(\|new CutLabUiPatchBuilder(' --include=*.cs DeckFlow.Web.Tests` — would not even
catch `CutLabUiPatchBuilderTests.cs`'s own `CreateBuilder` factory (`=> new(...)`, target-typed, line
829), nor `CutLabApiControllerTests.cs`'s or `CutLabWhatifTests.cs`'s or
`CutLabAjaxFloorByRoleRegressionTests.cs`'s target-typed `CutLabApiController controller = new(...)`
constructions, since none of those match the literal `new CutLabApiController(` string. The acceptance
check itself needs widening, not just the file list.

**Fix (mechanical, low-risk):** add the three files above to `04-03-PLAN.md`'s `files_modified` and to
Task 2's `<files>`/read_first, and extend Task 2's action to apply the exact same
`new FakeFeatureFlagCache(...)` argument fix already specified for `CutLabUiPatchBuilderTests.cs` to
each of their controller/patch-builder factories. This does not change the plan's design — D-19 is
correct — it only completes the blast-radius accounting the plan already started for
`BuildFindingsAndRoundPlan` call sites but did not extend to the two constructors it also changed.

### HIGH

**H1 — 04-04-PLAN.md and 04-RESEARCH.md cite the wrong file path for `CutLabFindingPresenter.cs`,
repeatedly, with no fallback instruction (unlike the test-file fallback given for the same task).**

Both documents state the path as `DeckFlow.Web/Models/CutLabFindingPresenter.cs` (frontmatter
`files_modified` line 8, `must_haves.artifacts` line 23, `key_links` line 29, interfaces line 103,
read_first line 180, `<files>` line 184, an acceptance-criterion `grep` line 270, and the follow-up
register line 534; `04-RESEARCH.md` lines 92/108/118/141/151/549). The file actually lives at
`DeckFlow.Web/Models/CutLab/CutLabFindingPresenter.cs` (verified: `find` returns exactly one hit, at
that path; the file's own `namespace` declaration is `DeckFlow.Web.Models`, which is presumably why the
research conflated the namespace with the folder path). Unlike the plan's handling of the *test* file
in the same task ("If this file does not exist under that name, locate it with `grep -rl
'BuildFindingGroups' DeckFlow.Web.Tests`…"), there is **no equivalent fallback for the production
file** — the path is stated as fact throughout, eight times, across two documents. A literal-instruction
follower opening the stated path will get "file not found" and must self-recover with no guidance; a
worse failure mode is creating a **new** file at the wrong path, which — because the namespace
(`DeckFlow.Web.Models`) is unaffected by folder location — would produce a duplicate `internal static
class CutLabFindingPresenter` in the same namespace and fail to compile (self-revealing, but a wasted
turn). The algorithm and content claims about this file (verified against source, lines 1-93) are
otherwise accurate.

**Fix:** correct the path to `DeckFlow.Web/Models/CutLab/CutLabFindingPresenter.cs` everywhere it
appears in `04-04-PLAN.md`.

### MEDIUM (2) / LOW (2) — see below, not blocking

**M1** — 04-04-PLAN.md's read_first/interfaces text asserts "the existing tests for the two merge
cases" are locatable and gives a grep fallback (`grep -rl 'BuildFindingGroups' DeckFlow.Web.Tests`) that
returns **zero** hits in the live tree — no test file calls `BuildFindingGroups` directly. The two
pre-existing merges (`WeakFloorCase`, `ComboProtected`) are covered only indirectly, through
`CutLabPageServiceTests.cs:657-658` and `CutLabViewModelWordingTests.cs:552-553`, which exercise the
full page/view-model pipeline, not the presenter function in isolation. Not blocking — Task 1 fully
specifies all 6 new tests itself, including a regression case for the two pre-existing kinds
(`BuildFindingGroups_WeakFloorAndComboProtectedMerges_AreUnchanged`) — but the "mirror the existing
tests" framing is factually wrong and the grep fallback would leave an executor with no located file to
extend, at which point the correct move (create `CutLabFindingPresenterTests.cs` fresh) is not stated
as the fallback outcome.

**M2** — D-19's table in 04-03-PLAN.md cites `CutLabApiController.cs`'s pre-existing
`_floorResolver.Resolve`/`floorByRole` construction at three locations (`:81`, `:224`, `:355`,
sourced from `04-RESEARCH.md` Section D.11, itself sourced from an older ledger). Live code has exactly
one such site today (`:95`). This is stale-citation drift, not a plan defect — the plan's own Task 1
instructs a fresh `grep` rather than trusting the number, so it self-corrects — but it is worth noting
since the task explicitly asked me to spot-check invented/stale facts.

**L1** — `04-RESEARCH.md:108` cites `CutLabFindingPresenter.cs:27-76` for `BuildFindingGroups`; live
file has it at lines 29-92 (93-line file total). Citation drift only; substance is accurate.

**L2** — Neither `CutLabRoleAssigner.Describe` (an existing `internal` helper at line 90 that already
does "role label or fall back to the raw key") nor its reuse is mentioned in 04-02's Task 1 action,
which instead has the executor re-implement the same fallback inline against
`RoleDisplayLabels.TryGetValue`. Functionally equivalent, no defect, but a missed reuse opportunity
worth a one-line note for the executor.

---

## Dimension-by-dimension notes (abbreviated; full reasoning above)

- **Requirement coverage**: PASS — see table above. Every TWIN-0x requirement ID appears in at least
  one plan's `requirements` frontmatter and has real, mutation-checked test coverage.
- **Task completeness**: FAIL on 04-03 (Files/Action/Verify/Done present, but Files is incomplete — see B1).
  04-01, 04-02, 04-04 PASS.
- **Dependency correctness**: PASS — linear chain 04-01 → 04-02 → 04-03 → 04-04, `depends_on` matches
  wave numbers, no cycles, no forward references. (B1 is a scope-completeness defect within a plan, not
  a cross-plan dependency-graph defect.)
- **Key links planned**: PASS — flag-key-to-consumer, detector-to-`Compute`, presenter-to-view-model
  links are all named and each has a task that implements the wiring, not just artifact creation.
- **Scope sanity**: 04-01 (3 tasks), 04-02 (2 tasks), 04-03 (3 tasks), 04-04 (3 tasks incl. a
  checkpoint) — all within the 2-3 target or close to it for genuinely complex, high-blast-radius work;
  file counts per plan are reasonable given the density of xmldoc/tests required. No scope-sanity
  blocker.
- **Verification derivation**: PASS — `must_haves.truths` are user-observable ("a group of 3+ ... raises
  a finding", "the flag is seeded OFF", "NextProposal demonstrably changes"), not implementation-only.
- **Context compliance**: N/A — no `04-CONTEXT.md` exists for this phase (confirmed); decisions are
  operator (D-14, D-15) and planner (D-16 through D-22), all correctly excluded from re-litigation here.
- **Architectural tier compliance**: PASS — all work is backend (`DeckFlow.Web/Services/CutLab`) plus
  Razor SSR rendering, matching `04-RESEARCH.md`'s Architectural Responsibility Map; no tier mismatch.
- **Nyquist / automated-verify presence**: every `auto` task has an `<automated>` command; no
  watch-mode flags; no full-E2E-suite-as-primary-feedback misuse (Playwright is confined to the human
  checkpoint in 04-04, which is appropriately a `checkpoint:human-verify` gate, not a task-level
  automated check).
- **CLAUDE.md compliance**: PASS — every plan carries the mandatory per-file line-ending preservation
  instruction; the `.editorconfig` carve-outs (verified present at `.editorconfig:45` for `get;init;`,
  `:79` for raw-string indentation) are explicitly called out where relevant (04-01 Task 2 explicitly
  forbids `{ get; }`, citing the `EdhTop16Client` precedent); no new NuGet packages; test framework is
  xUnit throughout, matching convention.

*(Dimensions 9/11/12 — Cross-Plan Data Contracts, Research Resolution, Pattern Compliance — no
committed PATTERNS.md exists for this phase; RESEARCH.md has no unresolved "Open Questions" section
requiring a `(RESOLVED)` marker — its "Unverified / open questions" section is explicitly advisory, not
a gating checklist, and items 3-5 are the very decisions D-14/D-16/D-21 correctly resolve. No data-contract
conflict found between plans; each plan's outputs feed the next cleanly.)*
