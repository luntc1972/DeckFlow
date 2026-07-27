# Foreman Ledger — Cut Lab classification bug fixes

BASELINE: e207ef73a28c5fd0fd2de95ded9ef30bfc20b5be | dirty (planning docs M, untracked .foreman/.codex-audit/wwwroot/js) | 2026-07-25
BRANCH: feat/personal-tools

## Plan
1. Scout (FAST, Claude foreman-scout) — locate role classifier, MDFC land detection, ADR 0003, Raugrin Triome data, "How Your Pool Competes" panel wiring.
2. Foreman plan (LEAD) — decide fix scope.
3. Implement (WORKHORSE, Codex gpt-5.4 medium) — code + tests.
4. Verify (WORKHORSE/FRONTIER, Claude foreman-verifier, cross-family) — blind, fresh context, original task verbatim.
5. Final review + commit (LEAD).

## Routing
- Task 1 → FAST → foreman-scout (Claude) — read-only investigation, cheap.
- Task 3 → WORKHORSE → Codex gpt-5.4 medium — well-specified implementation + tests, verified tier via echo call.
- Task 4 → foreman-verifier (Claude, fresh context) — cross-family verification of Codex output, default per skill.

## Codex consent
User confirmed keeping CLAUDE.md default: gpt-5.4 medium for all Codex work (review/plan/code). Login: ChatGPT subscription (not metered). Functional check `codex exec -m gpt-5.4 ... "Reply with exactly: ok"` → passed, 13,708 tokens.

## Tasks
| id | lifecycle | owned paths | job id |
|---|---|---|---|
| scout | REPORTED(DONE) | (read-only) | foreman-scout ac9904ab1f0d23b96 |
| plan-review-1 | REPORTED(FAIL, revised) | (read-only) | codex read-only, .foreman/scratch/codex-plan-review-1.txt |
| implement | DISPATCHED | see ticket v2 WRITE SET | codex workspace-write, worktree deckflow-cutlab-fixes |
| verify | PENDING | (read-only) | - |

## Attempts
| task | attempt | seat+effort | outcome | notes |
|---|---|---|---|---|
| scout | 1 | Claude foreman-scout (FAST) | DONE | full findings, 1 wrong claim (PlanRoleClassifier already-fixed) caught by plan review |
| plan-review-1 | 1 | Codex gpt-5.4 medium, read-only | FAIL (2 blocking findings) | v1 ticket missed PlanRoleClassifier.cs:176 (same precedence bug, real fix site) + CutLabPageServiceTests.cs:2017 exact-equality assertion + CutLabUiPatchBuilder.cs dup RoleDisplayLabels dict. Verified independently by Claude (read all 3 files), not just trusted. v2 ticket folds all 3 in. |
| implement | 1 | Codex gpt-5.4 medium, workspace-write | NEEDS_CONTEXT (ticket gap, not counted as failure) | Made all 9 in-scope file edits correctly; stopped before CutLab.cshtml lock-button (renders lock control for every RoleGroups entry incl. new "other" — outside WRITE SET, correctly refused to exceed fence). Also: codex sandbox can't invoke dotnet (WSL interop vsock error) — build/test not run by Codex. Dispatch had an operator (Claude) bug: double-backgrounded (manual `&` inside a run_in_background:true Bash call) causing a false-early "completed" notification while codex kept running orphaned; recovered by polling the real PID. |
| implement | 2 | Codex gpt-5.4 medium, workspace-write | DONE | CutLab.cshtml lock-button guarded for "other"; grep sweep confirmed no other special-casing needed (floor rows driven by untouched CutLabFloorRules.RoleKeys; TS is generic query-based). |
| build/test | 1 | Claude (real shell, Windows dotnet.exe via WSL interop) | PASS | Fresh worktree needed `npm install` in DeckFlow.Web first (TS build step). `dotnet build DeckFlow.sln`: 0 warnings, 0 errors. `dotnet test DeckFlow.Core.Tests`: 1630/1630 passed (incl. Manabase golden/byte-identity — no regression from draw-regex fix). `dotnet test DeckFlow.Web.Tests`: 2022/2038 passed, 16 skipped (Postgres-integration-only, expected). |

## Decisions
- Codex model: gpt-5.4 medium for all categories, per user confirmation 2026-07-25 (overrides session-hook's gpt-5.5 review/plan suggestion).
- User declined broader "Cut Lab wraps Manabase" architecture change mid-task; kept to the 4 named bugs (would've superseded ADR 0003 same-day).
- User: all future Cut Lab /gsd-quick fixes reuse this branch/worktree (feat/cutlab-fixes), not one per task.

## Outcome
COMMITTED: 48daa680 on feat/cutlab-fixes (off main), NOT pushed. 10 files, +126/-14.
Build 0/0, Core.Tests 1630/1630, Web.Tests 2022/2038+16 skipped. /simplify applied 1 fix (regex IndexOf guard), skipped 4 (1 would-be false positive verified against test fixtures, 3 out-of-scope/pre-existing).

## Scratch
- /mnt/c/users/chrislunt/source/personal/deckflow/.foreman/scratch/
