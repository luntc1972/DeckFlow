# Foreman Ledger — Cycle 21 Phase 1: Interaction Taxonomy Split

BASELINE: 77bdbd4e | clean except 4 untracked (RESEARCH-FINDINGS.md/.json, role-floor-research-run.exit, _role-floor-research/) — all pre-existing, out of scope | 2026-07-26

MODE: Codex-boosted (Agent tool + real shell + consented Codex CLI 0.145.0)
LEAD SEAT: Opus 5 (1M) — FRONTIER, confirmed
WORKTREE: /mnt/c/users/chrislunt/source/personal/deckflow-role-floors
PLAN: .planning/workstreams/cycle21-cut-lab/phases/01-interaction-taxonomy-split/01-01-PLAN.md

## Plan

| # | Task | Class |
|---|------|-------|
| 1 | Codex plan review → convergence (gate before code) | FRONTIER (Codex top tier) |
| 2 | Execute the split: 6 production sites + tests + e2e | WORKHORSE (Codex mid tier) |
| 3 | Blind verification, fresh context, no executor reasoning | FRONTIER (Claude foreman-verifier) |
| 4 | EOL + changed-lines format gate | LEAD (Claude, deterministic) |

## Routing

| Task | Seat | Effort | Why |
|------|------|--------|-----|
| 1 | Codex `gpt-5.4` read-only | medium | Project rule: Codex is authoritative plan reviewer. Cross-family second read on a plan Claude authored. |
| 2 | Codex `gpt-5.4` danger-full-access | medium | Project rule: Codex codes, Claude reviews. Plan is fully specified — WORKHORSE work, not judgment. |
| 3 | Claude `foreman-verifier` | — | Cross-family verification: Claude grades Codex output. Blind by construction. |
| 4 | Claude LEAD inline | — | Deterministic shell checks; no judgment, no delegation warranted. |

Note: `-codex` model variants 400 on this ChatGPT-account login (established 2026-06-09). Plain `gpt-5.4` for all seats.

## Tasks

| id | state | owned paths | job id |
|----|-------|-------------|--------|
| T1 | **DONE — CONVERGED** | (read-only) | 3 dispatches |
| T2 | **DONE** — build 0/0, xUnit 2058+1630, vitest 120, format clean, EOL LF→LF | plan files_modified + `CutLabWhatifTests.cs` (grep-surfaced) | bg bmvtnzujf |
| T2b | **DONE** — 7 sites → `[data-cut-lab-sticky-target]`, test-only | `e2e/cut-lab-{structure,theme-readability,nav-themes}.spec.ts` | bg bmbhcs11l |
| T4 | **DONE — PASS** | (normalization + gates only) | inline |
| T3 | **DONE — VERDICT: FAIL** (1 HIGH confirmed, 3 LOW) | (read-only) | agent |
| T5 | **DONE** — BLOCKER fixed TDD; Web 2058→2061 | `CutLabRoleAssigner.cs`, `CutLabFloorDefaults.cs`, `CutLabRoleAssignerTests.cs` | bg bwk3a102k |
| T6 | **DONE** — bracket-ratio LOW fixed TDD; Web 2061→2067 | `CutLabFloorRules.cs`, `CutLabStateSerializer.cs`, `CutLabPageService.cs`, 2 test files | bg burq6px0h |
| T7 | **DONE** — help-doc line (LEAD, trivial doc edit) | `DeckFlow.Web/Help/cut-lab.md` | inline |
| T8 | PENDING — blind RE-verification of the committed fixes | (read-only) | — |
| T3 | PENDING | (read-only) | — |
| T4 | PENDING | (normalization only) | — |

## Attempts

| task | # | seat + effort | ticket rev | outcome | checks | evidence | timestamp |
|------|---|---------------|-----------|---------|--------|----------|-----------|
| T1 | 1 | Codex gpt-5.4 read-only, medium | r1 | NEEDS-CHANGES — 1 BLOCKER, 1 HIGH, 2 MEDIUM | consumer grep, ClampFloors read, floor arithmetic | .foreman/scratch/c21p1-planreview.out.txt | 2026-07-26 |
| T1 | 2 | Codex gpt-5.4 read-only, medium | r2 (4 findings folded) | NEEDS-CHANGES — BLOCKER not fully resolved (category-path wipe leak), 1 NEW MEDIUM (unreachable test), 1 NEW LOW | re-ran discovery greps, DeckStatClassifier read | .foreman/scratch/c21p1-planreview-r2.out.txt | 2026-07-26 |
| T1 | 3 | Codex gpt-5.4 read-only, medium | r3 (all folded) | **CONVERGED** — no BLOCKER, no HIGH; 2 MEDIUM + 1 LOW non-blocking, all fixed post-verdict | all 3 discovery greps run by reviewer | .foreman/scratch/c21p1-planreview-r3.out.txt | 2026-07-26 |
| T2 | 1 | Codex gpt-5.4 danger-full-access, medium | ticket v1 | **DONE** | build 0 err/0 warn; xUnit 2058+1630 pass/0 fail; vitest 120 pass; format-check-changed exit 0; `--stat` == `--ignore-all-space --stat` (no EOL churn); scope fence clean | .foreman/scratch/c21p1-t2-final.txt | 2026-07-26 |
| T4a | 1 | Claude LEAD inline (user authorized e2e) | — | **6 FAIL — all PRE-EXISTING, not T2** | CI-mirror `CI=1` run: 46 pass / 6 fail / 14 masked. All 6 = `.cutlab-sticky-bar` strict-mode ambiguity. Proof: pool bar added `91999d07` AFTER locators written `2d47b756`; both `<div>`s present at HEAD; T2 diff does not touch sticky markup (`HEAD=9 WORK=9` occurrences). `cut-lab-export.spec.ts:104` failed only under parallel workers, passes in isolation → known decide-starvation flake | (inline) | 2026-07-26 |
| T2b | 1 | Codex gpt-5.4 danger-full-access, medium | ticket v1 | **DONE** | 7 bare `.cutlab-sticky-bar` sites across 3 specs → `[data-cut-lab-sticky-target]`; per-site intent justified from surrounding assertions; test-only, no production file touched | .foreman/scratch/c21p1-t2b-final.txt | 2026-07-26 |
| T3 | 1 | Claude `foreman-verifier`, blind fresh context | commits `8722a753`+`74e7f924` | **FAIL** | 1 HIGH: `HasWipeCategoryTag` uses `string.Equals` but the `IsInteractionCategory` it claims to mirror uses `Contains` (`PlanRoleClassifier.cs:252-263`), so a `"Board Wipe"`-tagged card sets the pre-gate but not `isMass` → routed to **interaction-targeted**. Violates truth #3 + Regression Risk row 1. Confirmed independently by LEAD. 3 LOW: orphaned xmldoc on `CutLabFloorDefaults.cs:99`, migration reads `state.Intent.Bracket` not request bracket (sum holds, ratio stale), vitest legacy-payload assertion unachievable (migration is server-side). Gates re-run clean by verifier: build 0 err / 9 pre-existing CS8629, Core 1630, Web 2058, vitest 120 | (agent report) | 2026-07-26 |
| T5 | 1 | Codex gpt-5.4 danger-full-access, medium | ticket v1 (TDD) | **DONE** | Failing first: 3 theory cases (`"Board Wipe"`, `"Board Wipes"`, `"BOARD WIPE"`) returned `["interaction-targeted"]` vs expected `["interaction-mass"]`. Fix: `HasWipeCategoryTag` → `category.ToLowerInvariant().Contains("wipe")`, mirroring `Has`. After: 30/30. Gates build 0/0, Core 1630, Web 2061, vitest 120. LOW-7 xmldoc restored to `GetBracketBand`; LOW-8 reverted `internal`→`private` (no external caller) | .foreman/scratch/c21p1-t5-final.txt | 2026-07-26 |
| T6 | 1 | Codex gpt-5.4 danger-full-access, medium | ticket v1 (TDD) | **DONE** | Failing first: legacy 15 at request-bracket 4 gave 10, expected 11. Fix: optional `int? bracketOverride` threaded `Deserialize`→`ClampFloors`→`MigrateLegacyInteractionFloor`; only `CutLabPageService.cs:266` passes `request.Bracket`; other ~13 `Deserialize` call sites untouched. D-04 order-independence tests (`:153`, `:170`) passed WITHOUT edits. Gates build 0/0, Core 1630, Web 2067, vitest 120 | .foreman/scratch/c21p1-t6-final.txt | 2026-07-26 |
| T4c | 1 | Claude LEAD inline | — | **PASS** | Post-T5/T6 gates: EOL `--stat` == `--ignore-all-space --stat` (10 files, +100/-25), per-file CR-count vs HEAD zero mismatches, `format-check-changed.sh staged` exit 0 via temp index | inline | 2026-07-26 |
| T4b | 1 | Claude LEAD inline | — | **PASS — 66/66 e2e green** | CI-mirror rerun after T2b: 66 passed, 0 failed, 0 masked (was 46/6/14) | inline | 2026-07-26 |
| T4 | 1 | Claude LEAD inline | — | **PASS** | EOL: `--stat` == `--ignore-all-space --stat` (78 files, +572/-148) AND per-file CR-count vs `git show HEAD:<path>` — zero mismatches. Scope fence: zero out-of-scope files in diff. Format gate: `format-check-changed.sh staged` exit 0 via temp `GIT_INDEX_FILE`; real index verified still clean | inline | 2026-07-26 |

## Decisions

- Codex consent: pre-granted this session — user selected Codex model defaults explicitly and CLAUDE.md mandates Codex-codes routing. No separate consent prompt required.
- Sequential dispatch throughout. T1→T2 is a hard gate (no code before plan converges); T2→T3 is a hard gate (nothing verified before it exists). No parallelism available or wanted.
- DO NOT COMMIT (user instruction). Changes left staged for user review.
- Pre-existing untracked artifacts (fabricated RESEARCH-FINDINGS, bulk card JSON) are OUT of every write set. They belong to Phase 2 and must survive this phase untouched.

## Scratch

- Ticket + output paths: `.foreman/scratch/`
