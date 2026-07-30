# Foreman Ledger — Cycle 21 Phase 4 plan-fix fold (2026-07-29)

**Run:** fold confirmed plan-review defects into the Phase 4 planning docs. No implementation code.
**Worktree:** `/mnt/c/users/chrislunt/source/personal/deckflow-role-floors`
**Branch:** `gsd/cycle21-cut-lab`
**Baseline commit:** `2f9ab5ef`
**Note:** the whole `phases/04-functional-twins-detector/` directory is currently **UNTRACKED**.
**Parallel-safety:** runs concurrently with the Phase 5 fold (`ledger-05-plan-review-2026-07-29.md`).
Write sets are disjoint — `04-*` here, `05-*` there. No worktree isolation needed.

## Provenance of the findings

Two independent Claude reviewers, dispatched in the pre-`/clear` session, agreed on the same two
defects without seeing each other's work:
- blind verifier (`foreman-verifier`): **PASS_WITH_NOTES** — build 0 err / 0 warn, HEAD unchanged, deploy-dark
  gating complete (all 5 finding-computation call sites derived independently), scope fence intact.
- plan-quality gate: **FAIL (BLOCK)** — report written to `phases/04-functional-twins-detector/04-PLAN-CHECK.md`,
  which also carries 2 MEDIUM + 2 LOW that the LEAD has not read.

Corroborated good news, recorded so it is not re-litigated: TWIN-01..04 are proven by tests carrying
**paired mutation checks** (D-14 exact-MV bucket substitution, TWIN-04 combo-filter insertion, TWIN-02
exclusion-set re-addition), which is stronger than the task brief's minimum bar. D-16 multi-role
double-count is well-specified with a named pinning test.

## Findings to fold

| ID | Sev | Claim | Evidence |
|----|-----|-------|----------|
| P4-1 | **BLOCK** | `04-03` T1 makes `IFeatureFlagCache` a REQUIRED ctor param on `CutLabUiPatchBuilder`/`CutLabApiController`, breaking compilation in 3 test files absent from its `files_modified`. Self-defeating: `04-03`'s own `<verification>` demands the suite green *including* `CutLabAjaxFloorByRoleRegressionTests`. | `CutLabApiControllerTests.cs:34` (an `Assert.Throws<ArgumentNullException>` null-guard whose arg list must also grow) and `:869`; `CutLabAjaxFloorByRoleRegressionTests.cs:356`; `CutLabWhatifTests.cs:528` |
| P4-2 | **BLOCK** (same finding, second half) | `04-03` T2 step 1 directs the executor to fix construction in `CutLabUiPatchBuilderTests.cs` — that file has **zero** `new CutLabUiPatchBuilder(`; its only touchpoint is a static call at `:793`. Wrong file named. | `grep -c 'new CutLabUiPatchBuilder(' CutLabUiPatchBuilderTests.cs` = 0 |
| P4-3 | **HIGH** | `DeckFlow.Web/Models/CutLabFindingPresenter.cs` does not exist; real path is `DeckFlow.Web/Models/CutLab/CutLabFindingPresenter.cs`. Wrong path repeated ~8× in `04-04` (frontmatter `files_modified`, `artifacts`, `key_links`, `read_first`, `<files>`, F-04-03) and in `04-RESEARCH.md`, including a `grep -n` acceptance criterion that would **error** rather than return zero hits. | `find` for the real path |
| P4-4 | LOW | `CutLabViewModel.cs:477` cited for `CardTypeLine.PrimaryType`; actual use is `:482`. | — |
| P4-5 | 2 MED + 2 LOW | Unread by LEAD — live in `04-PLAN-CHECK.md`. Worker must read that file and fold them. | `04-PLAN-CHECK.md` |

## Tasks

| ID | Task | Seat | Status |
|----|------|------|--------|
| F1 | Fold P4-1..P4-5 into `04-03-PLAN.md`, `04-04-PLAN.md`, `04-RESEARCH.md` | Claude worker (inherit) | DISPATCHED |
| F2 | LEAD verifies diff against each finding ID | LEAD | PENDING |

## Attempts (append-only)

- 2026-07-29 ~17:17 — F1 dispatched. Write set fenced to the three docs; `04-PLAN-CHECK.md` is read-only input.
- 2026-07-29 ~17:26 — F1 returned **DONE_WITH_CONCERNS**. All of P4-1..P4-5 applied; 3 files modified;
  `CR=0` on all three (LF held); no tracked file outside the write set moved.

## ⚠ THE TICKET'S OWN PREMISE WAS WRONG, AND THE WORKER WAS RIGHT TO REFUSE IT

P4-2 instructed the worker to **delete** `04-03` T2's instruction to fix construction in
`CutLabUiPatchBuilderTests.cs`, on the stated ground that the file contains zero constructions. It does
contain one. `CreateBuilder` (`:825-832`) constructs via **target-typed `=> new(`** at `:829`.
**LEAD verified this personally:** `grep -c 'new CutLabUiPatchBuilder(' CutLabUiPatchBuilderTests.cs`
returns **0** on a file that constructs the type. Applying the ticket as written would have deleted a
correct instruction and re-introduced the very defect it was meant to remove. The worker kept and
sharpened it instead.

**Both reviewers made the same error, and the LEAD propagated it into the ticket.** This is the **sixth**
instance of this cycle's signature defect — a substring count standing in for a structural property —
and the first that fooled reviewers and LEAD rather than only a plan. Sharper generalization to keep:
`grep 'new TypeName('` is **structurally blind to C# target-typed `new(...)`** and therefore
under-reports constructor call sites by design. Prior five were all inside plan acceptance criteria
(`"interaction",`, `cycle21-cut-lab == 2`, `DECKFLOW_ROLE_FLOOR_CONNECTION_STRING == 0`,
`CutLabRoleAssigner.AssignRoles == 1`, the Phase 5 `must_haves` proxies).

**Consequence for the plan:** true count is **7 constructions across 5 files**, not 4. The reviewers cited
only the nested `new CutLabUiPatchBuilder(` lines (`:869`, `:356`, `:528`) and never saw the *controller*
constructions at `CutLabApiControllerTests.cs:866`, `CutLabAjaxFloorByRoleRegressionTests.cs:353`,
`CutLabWhatifTests.cs:525` — all LEAD-verified. The worker wrote 7 into the plan and required the
executor to report divergence.

Also corrected: `04-PLAN-CHECK.md` mis-names the label helper as `Describe`; it is
`CutLabRoleAssigner.DisplayLabelFor` at `:89-90` (LEAD-verified).

- 2026-07-29 ~17:30 — F2 (LEAD verification) **PASS**: `:829` target-typed construction confirmed;
  the 3 controller sites confirmed; real presenter path `DeckFlow.Web/Models/CutLab/CutLabFindingPresenter.cs`
  confirmed; wrong path purged from `04-04` + `04-RESEARCH` (only residual is `04-PLAN-CHECK.md:128`,
  which quotes the defect — correctly left); `CR=0` on all three.
- 2026-07-29 ~17:31 — LEAD applied the three residual fixes in files that had been fenced out of F1:
  `04-02-PLAN.md:177` `CutLabViewModel.cs:477`→`:482`; `04-02-PLAN.md:289` now mandates reusing
  `CutLabRoleAssigner.DisplayLabelFor(roleKey)` instead of re-implementing the `RoleDisplayLabels`
  fallback inline; `04-03-PLAN.md:596`'s cumulatively-false "`git status` shows only the one new test
  file" criterion rewritten to scope the assertion to `DeckFlow.Web`/`DeckFlow.Core` and to state why a
  whole-tree assertion is false by the time Task 3 runs. Intent preserved, nothing softened.
- **Phase 4's BLOCK is cleared at the plan level.** Not executed, not committed.
