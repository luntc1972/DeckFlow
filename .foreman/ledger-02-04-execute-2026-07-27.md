# Foreman Ledger — Cycle 21 Phase 2, Plan 02-04

**Run:** 2026-07-27
**Worktree:** /mnt/c/users/chrislunt/source/personal/deckflow-role-floors
**Branch:** gsd/cycle21-cut-lab
**Baseline commit:** b0212a28
**Baseline untracked (permanent set, do not touch):**
`.foreman/ledger-01.1-execute-2026-07-27.md`, `.foreman/ledger-cycle21-p2-plan-2026-07-27.md`,
`.foreman/scratch/`, `_edhrec-brackets/`, `_role-floor-research/`

**Mode:** Codex-boosted (Agent tool + real shell + Codex CLI, consent standing per CLAUDE.md)
**Plan:** `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/02-04-PLAN.md`

**Routing:** all three tasks are WORKHORSE-class well-specified implementation → Codex `gpt-5.4`,
`model_reasoning_effort=medium`, `-s danger-full-access`, `approval_policy=never`.
**Serialization:** all three tasks write `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs`.
Write sets overlap → strictly sequential, no parallel wave.

## Write sets

| Task | Files it may touch |
|---|---|
| 1 | `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs`, `DeckFlow.Core/Research/RoleFloorGuards.cs` (new), `DeckFlow.Core.Tests/RoleFloorGuardsTests.cs` (new) |
| 2 | `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs` |
| 3 | `DeckFlow.CLI/Program.cs`, `DeckFlow.CLI/RoleFloorResearchCommandRunner.cs` |

## Tasks

| ID | Task | Seat | Status |
|---|---|---|---|
| T1 | Delete synthetic fixture writer; widen TargetRoles 5→9; taxonomy drift guard + `RoleFloorGuards` + tests | codex:gpt-5.4/medium | DONE_WITH_CONCERNS → `196b71f7`, blind verify in flight |
| T1b | Make `ValidateTaxonomyAgainstAssigner` internal; add `DeckFlow.Core.Tests` coverage for the reflection branch + nine probes (user rule) | codex:gpt-5.4/medium | **ACCEPTED** → `398a3f4e`, blind verify PASS |
| T2 | Stamp run provenance (host/timestamp/SHA + degraded warnings); exit 2 on zero qualifying commanders | codex:gpt-5.4/medium | **ACCEPTED** → `3f301738`, blind verify PASS |
| T2b | Widen credential-shape regression coverage of `DescribeDatabaseHost` (verifier LOW finding) | codex:gpt-5.4/medium | **ACCEPTED** → `19994733`, blind verify PASS |
| T3 | Repoint `--out`/`--out-json` defaults; document exit codes in `--help`; accept connection string from environment via a tested Core resolver | codex:gpt-5.4/medium | **ACCEPTED** → `28ab041b`, blind verify PASS |
| T3b | Drop tautological credential assertions from the whitespace test (verifier LOW finding) | claude (review exception) | **DONE** → `4342b146` |
| T4 | `02-04-SUMMARY.md`; correct D-06's false premise in `02-04-PLAN.md` and `02-08-PLAN.md`; replace two self-contradictory acceptance greps | claude (planning role) | **DONE** → `ba54ba31` |

## Outcome — plan 02-04 COMPLETE

Seven commits, `196b71f7`..`ba54ba31`, on `gsd/cycle21-cut-lab`. **Not pushed.**
Final gates: build 0 errors / 9 pre-existing warnings (0 new); Core.Tests **1708** passed / 0 failed
(1659 at wave start, +49); Web.Tests 2095 passed / 16 skipped / 0 failed, unchanged throughout.
Zero EOL churn on every commit — every touched file LF before and after, including the small edits
into the large `Program.cs`.

Four independent blind verifications, fresh context each, every one PASS. Two went beyond static
review and executed the built assemblies out-of-repo: one mutated `TargetRoles` reflectively and
watched the guard name the missing key; another ran `DescribeDatabaseHost` against 11
connection-string shapes and confirmed no credential fragment escapes on any path, including
exception paths.

### Carried forward

- **RFLR-12 is still unwritten.** The taxonomy widening has no requirement ID; `REQUIREMENTS.md` is a
  cycle-wide governance doc and no plan in this phase may edit it. Proposed wording is in
  `02-04-SUMMARY.md` §9.
- **Latent, pre-existing, out of scope:** `RoleFloorResearchCommandRunner.cs:343` writes
  `exception.Message` to `Console.Error`. Npgsql names only `host:port` today, but a future driver
  embedding connection-string fragments in exception text would surface them. Worth a later plan given
  D-07's subject matter.
- **Plan 02-08 depends on facts pinned in `02-04-SUMMARY.md` §7** — the resolver precedence (flag beats
  environment) and the exact both-sources-missing error text — so its wrapper can be written against
  them without re-reading the code.
- **Lesson for future plan authors:** two acceptance criteria in this plan were greps that the plan's
  own action steps guaranteed would fail. Assert on the construct, not on a file-wide substring count.

## Standing rule added mid-run (user, 2026-07-27 15:02)

> "things added to the console app must use core and have tests added to core for anything new"

**This overrides plan 02-04's decision D-06 where the two conflict.** D-06's stated premise is
factually wrong: it claims CLI additions "are covered by grep and code-reading alone" because
there is no `DeckFlow.CLI` test project. There is no such project, but:

- `DeckFlow.Core.Tests.csproj:31` already has `<ProjectReference Include="..\DeckFlow.CLI\...>`
- `DeckFlow.CLI/AssemblyInfo.cs:3` already has `[assembly: InternalsVisibleTo("DeckFlow.Core.Tests")]`
- `RoleFloorResearchCommandRunner` is `internal static`, so `DeckFlow.Core.Tests` can reach it
- Precedents already doing this: `CommandRunnerHarvestTests.cs`,
  `EdhrecDataDownloadCommandRunnerTests.cs`, `RunDistillAsyncTests.cs`

**Genuine constraint that survives:** `DeckFlow.Core.csproj` has ZERO `<ProjectReference>` entries,
so Core cannot see `CutLabRoleAssigner` / `CardFact` / `PlanRole` (all in `DeckFlow.Web`). The nine
probes and the reflection call therefore CANNOT move into Core.

**Applied policy for the rest of this run:**

1. Pure logic (no `DeckFlow.Web` types) → `DeckFlow.Core/Research/`, tested in `DeckFlow.Core.Tests`.
2. Web-coupled glue → stays in `DeckFlow.CLI` but is declared `internal` (not `private`) so
   `DeckFlow.Core.Tests` can test it. Never assert it is untestable.
3. Every new member from any task in this plan carries `DeckFlow.Core.Tests` coverage.
4. No new test project (CLAUDE.md forbids it on AI initiative, and none is needed).

Follow-up owed by Claude (not Codex — `.planning/` is outside every scope fence): correct D-06's
rationale in `02-04-PLAN.md`, and check whether sibling plans `02-05`..`02-09` repeat the same
"CLI is untestable" premise.

## Attempts (append-only)

- 2026-07-27 — ledger opened, baseline recorded, no dispatch yet.
- 2026-07-27 — T1 dispatched to codex gpt-5.4/medium, attempt 1, background job `ba9v527bv`.
- 2026-07-27 15:21 — T1 ACCEPTED. Codex reported `DONE_WITH_CONCERNS` → commit `196b71f7`
  (3 files, +314/−93). Foreman deterministic checks passed. Blind verifier (`foreman-verifier`,
  fresh context, given the plan text not the worker's narrative) returned **PASS** on all items
  A–H, having loaded the built DLLs and invoked the real private `ValidateTaxonomyAgainstAssigner`
  against a corrupted in-memory `TargetRoles` — it correctly named `protection`, then `wincons`,
  and returned clean on the committed state. Verified from a committed state: `HEAD` unchanged,
  working tree clean.
  Two LOW findings, neither an implementation defect:
  (a) plan acceptance criterion `grep -c '"interaction",' == 0` is self-contradictory — the plan's
      own action step B orders the `// Why:` comment that trips it (runner :38). Plan defect.
  (b) probe comments for `engines`/`wincons`/`payoffs` cite tests that pass non-empty `categories`
      while the guard passes `[]`; the oracle text independently satisfies the heuristic path, so
      it works, but the citations overstate the correspondence. Queued into T1b step 4.
- 2026-07-27 15:2x — **T1b dispatch FAILED. Codex CLI returned:
  `ERROR: Your workspace is out of credits. Ask your workspace owner to refill in order to continue.`**
  Retried once per CLAUDE.md "Cross-AI dispatch failures"; the retry returned the identical error,
  confirming it is a deterministic account state, not a transient fault. No partial edits: `HEAD`
  still `196b71f7` and every `git status --porcelain` path is in the permanent untracked set.
  **Escalated to the user.** Per CLAUDE.md, Claude does NOT silently take over implementation —
  the user must explicitly authorize either (a) Claude executing T1b/T2/T3, or (b) pausing until
  Codex credits are refilled.
- 2026-07-27 15:02 — user added the Core-logic/Core-tests rule above. T1 left running: its
  deliverables (`RoleFloorGuards.cs` + `RoleFloorGuardsTests.cs`) are a strict subset of what the
  rule requires, so the rule is additive, not contradictory. A follow-up task T1b will add the
  missing `DeckFlow.Core.Tests` coverage for the CLI-resident guard, and T2/T3 tickets are amended
  before dispatch.
