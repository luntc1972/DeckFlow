---
phase: 02-role-floor-divergence-research
plan: 01
status: complete
completed: 2026-07-27
commits:
  - 2fb5d4a4 docs(02-01) correct Phase 2 starting-state claims and the stash location error
  - 0d3f8fea refactor(02-01) move boardFilter ahead of CancellationToken
baseline: 4b4e6c81
verification: PASS_WITH_NOTES (blind verifier, 12/12 checks pass)
---

# Phase 2 Plan 01 — Summary

Three unrelated pieces of cleanup, all of which had to land before anyone edits the harness, because
each would otherwise have been carried forward as truth.

## 1. Harness location — corrected

The ROADMAP claimed the Phase 2 harness lived in `stash@{0}` and that `git stash pop` was the first
step of the phase. **Both were false.** `git stash list` in this worktree shows exactly one entry,
belonging to the unrelated branch `feat/manabase-source-list`. An executor obeying the ROADMAP would
have popped another branch's work into this worktree — and because `git stash` is repo-global across
this repository's four worktrees, it would have silently taken uncommitted changes from wherever the
developer happened to be standing.

The harness was on disk as untracked and modified working-tree files, and is now committed as an
unrepaired baseline at **`27e25459`**. ROADMAP and STATE now say so, the `git stash pop` instruction
is deleted rather than softened, and a hard constraint forbidding `git stash` anywhere in this phase
is recorded.

**Why the baseline commit happened at all** — worth keeping, because it is a reusable lesson. Codex
refused to execute this plan on its first dispatch, correctly: every plan in the phase asserts a
task-scoped `git diff --name-only HEAD` gate, and six tracked files carried pre-existing harness
modifications belonging to no task, so the gate was unsatisfiable on arrival. The gate was right; the
uncommitted harness was the problem. Committing it unrepaired makes the phase's repair work a legible
diff against a known starting point instead of code that appears fully-formed in a later commit.

## 2. Fixture artifacts — deleted, not amended

Four files removed from the phase folder:

- `RESEARCH-FINDINGS.md`
- `RESEARCH-FINDINGS.json`
- `role-floor-research-run.log` (0 bytes)
- `role-floor-research-run.exit` (0 bytes)

The two findings files were output from `WriteSyntheticVerificationOutputs`, not from a run — their
commanders are literally named Alpha/Beta/Gamma/Delta, and their `ClearsBar` column contradicts its
own inputs, because `BuildSyntheticRoleStat` takes `clearsBar` as an independently hardcoded `bool`
decoupled from the hardcoded ratio/z/d literals beside it. That contradiction is impossible via the
real computation path. Nothing in them was salvageable as evidence, so they were deleted rather than
amended. Deleting the *writer* is plan `02-04`'s job.

`_role-floor-research/cards_full.json` (8.2 MB resumable Scryfall cache) was preserved, verified at
8,220,503 bytes after the fact.

## 3. `boardFilter` moved ahead of `CancellationToken`

The token is now the last parameter on both `CardCategoryRepository.GetCategoryDeckMembershipForCommanderAsync`
and the `CategoryKnowledgeRepository` passthrough, per project convention.

**This was not a mechanical reorder.** Four call sites had to be repaired:

| Call site | Note |
|---|---|
| `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs:104` | passthrough; passed the token positionally |
| `DeckFlow.Web/Services/Persistence/CategoryKnowledgeStore.cs:282` | **production** — see below |
| `DeckFlow.Core.Tests/CategoryCacheSchemaParityTests.cs:305` | test call, now fully named |
| `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs:113` | must still receive `boardFilter: "mainboard"` |

`CategoryKnowledgeStore.cs:282` deserves calling out: **the phase brief explicitly listed it as
unaffected.** It was not — it called positionally, so the reorder would have bound a
`CancellationToken` into a `string? boardFilter` parameter. The plan caught what the brief missed.
It now passes no board filter (named argument), so `CommanderCategoryService` output is unchanged:
`CardCategoryRepository` builds `filterClause = boardFilter is null ? string.Empty : …`, giving
byte-identical SQL for every existing caller.

Blind verification enumerated all 19 references to the method across the solution and confirmed no
call binds an argument to a parameter the author did not intend. The seven interface-level callers
(`CommanderCategoryService`, `ICategoryKnowledgeStore`, five test doubles) bind the unchanged 2-param
overload and were untouched.

## 4. ROADMAP criterion-1 wording aligned

Success criterion 1 said "grep for `Synthetic` in `DeckFlow.CLI` returns nothing"; the gate plan
`02-04` runs is `grep -rni synthetic DeckFlow.CLI --include=*.cs`. The `--include=*.cs` narrowing is
defensible — the only non-source hit is generated build output — but it was a silent relaxation of
the stated criterion. The ROADMAP now names `.cs` sources explicitly, so criterion and gate mean the
same thing.

## 5. Wave-1 serialization recorded

The ROADMAP now records that `02-01` runs to completion before `02-03` starts, and that `02-02` is
the only plan safe to run concurrently with either. Both `02-01` and `02-03` run `dotnet build` plus
both test projects and then commit; run concurrently in a shared worktree they would contend on
`obj/`/`bin/` and on `index.lock`, and each plan's pinned test counts would go non-deterministic
while the other adds test members. `02-02` is Python-and-docs only, touches disjoint files, and runs
no `dotnet`.

## Gates

| Gate | Result |
|---|---|
| `dotnet build DeckFlow.sln` | 0 errors, 9 warnings — all CS8629 in `ManabaseBaselineWeightingTests.cs`, the exact pre-existing baseline set. No new warning. |
| `DeckFlow.Core.Tests` | Failed 0, Passed **1650**, Skipped 0 |
| `DeckFlow.Web.Tests` | Failed 0, Passed **2095**, Skipped 16 |
| EOL | `--stat` and `--ignore-all-space --stat` byte-identical (118 ins / 47 del). All 7 touched files LF, unchanged. |
| Scope fence | `git diff --name-only 4b4e6c81..HEAD` = exactly the 7 authorized paths. `RoleFloorResearchCommandRunner.cs` changed in one 4-line hunk only, out of 985 lines. |
| Golden/expectation files | None edited. The only test-file change is two argument lines; every assertion is unchanged. |

## Deliberately NOT done

- **`.gitignore` was not edited.** It is on the project's Do-Not-Modify list. Whether
  `_role-floor-research/` and `_edhrec-brackets/` should be ignored is demoted to a developer
  follow-up **outside this phase's plans** — no plan may edit that file.
- **`REQUIREMENTS.md` was not edited.** See the traceability gap below.
- No CalVer bump, no tag, no release step. This phase ships no user-visible change and is not a
  release.

## Requirement traceability gap

Tasks 1 and 2 carry **RFLR-09**. **Task 3 (the `boardFilter` reorder) has no requirement ID** — it is
ROADMAP known defect 4 and success criterion 6, but `REQUIREMENTS.md` maps no ID to it. No ID was
invented. Proposed for developer ratification at milestone closeout:

> **RFLR-10** — The role-floor research harness's repository surface follows the project's
> `CancellationToken`-last parameter convention, and no production runtime path changes behavior as a
> result.

RFLR-11, RFLR-12 and RFLR-13 are proposed by plans `02-02`/`02-06`, `02-04` and `02-09` respectively.
All four remain proposals; none is cited anywhere as ratified.

## Open notes for the developer

- The 16 skipped Web tests include all six `PostgresStorageTests.CategoryKnowledgeRepository_*` cases,
  so the reorder is exercised against SQLite only. The SQL template and its binding are textually
  unchanged, but no live Postgres query was run against the new signature.
- `STATE.md`'s section heading "Uncommitted Work In This Worktree" is now factually stale — that work
  is committed at `27e25459`. The plan explicitly ordered the section left intact, so this is
  plan-authored rather than executor error. Worth tidying at closeout.
- `STATE.md` frontmatter says `(wave 1 of 6)` while the ROADMAP says 7 waves. The plan dictated the
  literal string, so the executor was correct to write it. Also worth tidying at closeout.
