# Foreman Ledger — evolution card reference

## Run: 2026-07-17 evolution-card-reference
- Mode: Codex-boosted (codex-cli 0.144.3, ChatGPT-account login, user standing consent + session defaults confirmed: coding gpt-5.4 medium, review gpt-5.5 medium)
- Baseline: branch `feat/evolution-card-reference` off main `c2041334`, tree clean (untracked: .foreman/, .planning/ui-design/deck-history/)
- Prior branch parked: `chore/home-tiles-release-tag` @ 1a3cbcb4 (awaiting user eyeball/merge)
- Prior run archived: ledger-deck-history-2026-07-16.md

## Tasks
| ID | Task | Seat | Status |
|----|------|------|--------|
| T1 | Evolution prompt card-reference section (impl + tests) | Codex gpt-5.4 med | DISPATCHED |
| T2 | EOL churn check + build + full Web suite | Foreman shell | PENDING |
| T3 | Blind verify (foreman-verifier) | Claude verifier | PENDING |
| T4 | Foreman code review + commit | LEAD | PENDING |

## Attempts
- T1 attempt 1: codex exec gpt-5.4 medium, danger-full-access, repo root. Write set: DeckHistoryPageService.cs, PromptBuilders/Evolution/*, Program.cs (DI if needed), DeckHistoryController.cs (if needed), DeckFlow.Web.Tests deck-history/evolution tests, README.md.

## Progress 2026-07-17
- T1 attempt 1: DONE, ACCEPTED. d41a68dd (10 files, +463/-18). Scope exact, LF clean, format gate pass.
- T2: DONE. Independent Web suite 1523/0 (14 skip), build 0 err, no EOL churn (diff-stat == ignore-space-stat).
- T3: PASS_WITH_NOTES (verifier a656ebbfcb3917772). 3 LOW findings: (1) wrong-tool warning copy, (2) cut-then-re-add edge untested, (3) HttpRequestException-only catch (accepted — matches siblings).
- T1-fix attempt 1 (Codex gpt-5.4): DONE, ACCEPTED. 1bc40146 fixes findings 1+2. Targeted 13/13. Re-verify dispatched to same verifier.
- T3 re-verify: PASS (27/27 targeted, scope exact, LF clean). Run CLOSED. Final: feat/evolution-card-reference = d41a68dd + 1bc40146, awaiting user UAT + merge.

## Run: 2026-07-17 gemini-copy-alignment (branch chore/home-tiles-release-tag @ 1a3cbcb4)
- T5: Codex gpt-5.4 — conditional Gemini copy in views + help scrub. DISPATCHED.
- T5: DONE_WITH_CONCERNS → resolved. Copy alignment 210c3745 (+ my tile-fallout test fix 079dadaf + README note 286d018f). Fresh-build Web suite 1519/0. Blind verify PASS_WITH_NOTES (finding 1 fixed in 286d018f; 2-3 accepted). NOTE: earlier tile-commit suite run was --no-build stale-assembly false-green — lesson: never claim suite pass from --no-build after source edits. Run CLOSED.

## Run: 2026-07-18 cut-lab-milestone-init (resumed from quota-killed Opus Foreman b8d0c9fe)
- Mode: Full (Agent tool + shell; Codex available but no dispatch planned — planning is LEAD work)
- Lead: Fable (frontier — satisfies handoff constraint 2)
- Baseline: main @ 229362be, clean except untracked .codex-audit/ .foreman/ .planning/ui-design/deck-history/
- Routing: GSD_WORKSTREAM=cut-lab; ALL writes under .planning/workstreams/cut-lab/; root state.milestone-switch/phases.clear SKIPPED (root-clobber hazard confirmed live — init.new-milestone --ws returns root paths)
- Cycle 17 worktree ../deckflow-cycle17 UNTOUCHED (constraint 3)

## Tasks
| ID | Task | Seat | Status |
|----|------|------|--------|
| C1 | Preserve /tmp research -> workstream research/ + commit | LEAD inline (mechanical) | IN_PROGRESS |
| C2 | Workstream PROJECT.md + STATE.md (Cycle 18 identity) + commit | LEAD (judgment) | PENDING |
| C3 | REQUIREMENTS.md draft from approved scope -> user approval -> commit | LEAD (judgment) + user gate | PENDING |
| C4 | Roadmap via gsd-roadmapper (sonnet WORKHORSE), Phase 101+, workstream paths | Claude sonnet agent | PENDING |
| C5 | Roadmap user approval -> commit + todo scan | LEAD + user gate | PENDING |

## Progress 2026-07-18 cut-lab-milestone-init
- C1 DONE: research preserved 78b16c7b
- C2 DONE: PROJECT.md + STATE.md 0590dc90
- C3 DONE: 21 REQs approved by user, committed c13321b0
- C4 DONE: gsd-roadmapper (sonnet) ACCEPTED — isolation verified (3 workstream files only), 21/21 coverage, phases 101-105. LEAD diff review; blind verifier waived (planning docs, no logic; user approval gate passed)
- C5 DONE: roadmap approved by user, committed 88971f2c; no pending todos to link; consumed handoffs removed (next commit)
- Run CLOSED. Milestone Cycle 18 initialized. Next: /gsd-plan-phase 101 (workstream cut-lab)

## Run: 2026-07-18 plan-phase-101 (workstream cut-lab)
- Gates: user chose no-context / research-first / UI-SPEC-first
| ID | Task | Seat | Status |
|----|------|------|--------|
| P1 | Phase-101 research (gsd-phase-researcher) | sonnet WORKHORSE | DISPATCHED |
| P2 | UI-SPEC via gsd-ui-phase --auto | skill orchestration | PENDING |
| P3 | Planner (gsd-planner) | per agent default | PENDING |
| P4 | Plan-checker loop + Codex plan review | checker + Codex gpt-5.4 | PENDING |
- P1 DONE: research d445411b (sonnet, HIGH conf; ValidateCommanderDeckSize trap found)
- P2 DONE: UI-SPEC c0e78401 (researcher sonnet + checker sonnet VERIFIED 6/6, 2 FLAGs; focal-point edit applied by LEAD)
- P2b: VALIDATION.md 31ee4f58; PATTERNS.md f6de215b (mapper sonnet, 18/18 analogs)
- P3: planner (opus) 4 plans/3 waves; checker (sonnet) iter1 = 1 BLOCKER (lock state DOM->server round-trip missing) + 5 warnings; revision dispatched to same planner; iter2 re-check in flight
- Codex session defaults CONFIRMED by user: review gpt-5.5 med, coding gpt-5.4 med
- P4 DONE: checker iter2 APPROVED (blocker closed 3-layer). Codex gpt-5.5 convergence: r1 CHANGES (1H/1M/2L) -> folded; r2 CHANGES (new H: dup ctor) -> folded (single public ctor); r3 CHANGES (2 stale internal-ctor refs) -> LEAD inline doc fix; r4 SHIP. Plans committed.
- Run CLOSED. Next: /gsd-execute-phase 101 (Codex gpt-5.4 codes, own branch/worktree per rule).
