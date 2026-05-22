---
gsd_state_version: 1.0
milestone: v1.3
milestone_name: Frontend Hardening + AI-Agnostic Rename + Code Hygiene
status: planning
stopped_at: Phase 999.6 opened 2026-05-22 — ship-gate test residual cleanup; v1.3 ready_to_ship REVOKED until full suite reports Failed 0 per new no-ship-failing-tests rule. 9 residual failures from 999.5 close-out (6 stale, 1 flaky, 2 ambiguous) must be triaged + fixed before ship restoration.
last_updated: "2026-05-22T15:01:00.000Z"
last_activity: 2026-05-22 -- v1.3 ship blocked by new memory rule [[feedback-no-ship-failing-tests]] established during 999.5 UAT. Phase 999.6 added to ROADMAP to triage + fix 9 residual failures: PacketArtifactStoreTests msg-string drift (1), AiPlatformPhase10RoundTripTests compare2→comparison rename (4), PacketArtifactStoreRoundTripTests dev-machine fixture path (1), BasicAuthMiddlewareTests state-leak between facts (1), ArchidektCacheJobServiceTests background-service race-or-real-bug (2). Acceptance: full DeckFlow.sln test gate exits Failed 0 before ship sequence resumes.
progress:
  total_phases: 11
  completed_phases: 10
  total_plans: 43
  completed_plans: 43
  percent: 91
---

## Deferred Items

Reviewed 2026-05-13 via `/gsd-review-backlog`. Promoted to v1.3 candidates: harvest-killed-by-suggestion (debug), AiPlatform value object refactor. Removed 7 quick_tasks (all complete per SUMMARY, stale "missing" label). Closed 07-VERIFICATION as satisfied (v1.1 shipped 2026-05-08, no harvest crash incidents). Backfilled 09/10-VERIFICATION.md and Phase 9 SUMMARY frontmatter same session.

| Category | Item | Status |
|----------|------|--------|
| tech_debt | Gemini paste-limit workaround | flag-gated DECKFLOW_GEMINI_ENABLED |
| archive | v1.1 phase dirs (`06-admin-shell-flags-foundation`, `07-harvest-controls-stats`, `07.1-categories-feature-flag-sameorigin-ajax-fix`, `08-analytics`) | needs move to `.planning/milestones/v1.1-phases/`; quick-task candidate for v1.4 cleanup |
| tech_debt | Semantic-completeness guards for `DeckComparisonService.ParseComparisonResponse` + `MetaGapService.ParseResponse` (mirror `HasMeaningful*Content` pattern from `ResponseParsers.cs:99-130`) | **PROMOTED to Phase 999.5 P02** (planned 2026-05-21) — see CONTEXT D-09/D-10/D-11/D-12 in `.planning/phases/999.5-v1-3-backlog-catch-up-test-hardening/999.5-CONTEXT.md`; tracks the "valid JSON but semantically empty AI output" edge case (e.g., `{"deck_comparison":{}}` parses into a default-init DeckComparisonResponse and bypasses both the existing wrong-shape guard and 999.4's truncation UX). Closes 999.4 D-07 carve-out via 999.5 plans 02 |

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-13 for v1.3)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 999.6 — drive full DeckFlow.sln test suite to Failed 0 so v1.3 can ship. New memory rule [[feedback-no-ship-failing-tests]] (established 2026-05-22) blocks ship sequence while any tests fail.

## Current Position

Milestone: v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene
Phase: 999.6 v1.3-ship-gate-test-residual-cleanup — PLANNING (added 2026-05-22; reverses 999.5 D-22 "out-of-scope deferral" of 9 residual failures)
Plan: 0 plans yet — running /gsd-discuss-phase 999.6 to capture triage decisions before plan-phase.
Status: v1.3 ship REVOKED. Phase 999.5 stays closed (its scope was met) but the milestone ship gate now requires 999.6 closure first.
Last activity: 2026-05-22 -- During 999.5 UAT, user established hard rule: no ship with failing tests. Triage of 9 residuals: 6 stale (`PacketArtifactStoreTests.LoadFromZip_throws_when_no_response_json_present` msg-string drift; 3× `AiPlatformPhase10RoundTripTests.SuggestComparisonZipFileName_includes_lowercased_ai_name(Claude|ChatGPT|Gemini)` compare2→comparison rename; `AiPlatformPhase10RoundTripTests.SuggestComparisonZipFileName_includes_compare2_page_segment` dead premise; `PacketArtifactStoreRoundTripTests.LoadFromZip_AlsoRestoresUserInputs_FromArnaFixture` hard-coded `C:\tmp\arna-test\` dev path). 1 flaky (`BasicAuthMiddlewareTests.CorrectCredentials_InvokesNext` passes 5/5 isolated, fails in full suite — state leak). 2 ambiguous (`ArchidektCacheJobServiceTests.BackgroundService_SucceedsAndUpdatesProcessedCounts` + `GetActiveJob_ReturnsNullAfterCompletedJob` — likely race; 2nd has name-vs-assertion conflict suggesting real bug).
Progress: [█████████░] 91%

## Performance Metrics

**Velocity (v1.2 reference):**

- Total plans completed in v1.2: 8 (Phases 9 + 10)
- Average duration: ~1 day per plan (parallel waves where possible)

**By Phase (v1.2):**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 9 — Bracket UX + AI Selector | 3 | 3 | ~1 day |
| 10 — Claude + Gemini Optimization | 5 | 5 | ~1 day |
| 12 | 5 | - | - |
| 13 | 4 | - | - |
| 15 | 3 | - | - |
| 999.1 | 7 | - | - |

**v1.3 Forecast:**

| Phase | Plans (est.) | Notes |
|-------|--------------|-------|
| 11 — WDG Audit Fixes | 10 | One plan per sweep PR in `260513-wdg-FINDINGS.md` |
| 12 — URL + Page Rename | 2-3 | Slug + redirect + filename sanitizer + view sweep |
| 13 — ChatGpt* Class Rename | 2-3 | DTO/service/viewmodel rename + doc comment backfill + DI/test fixture update |
| 14 — Broader Audit | 2-3 | Per-project sweep + doc comment backfill + build clean |
| 15 — AiPlatform Refactor | 3-4 | Value object + per-builder registries + DI wiring + T1-T8 reverify |
| Phase 14-broader-codebase-name-vs-behavior-audit P02 | 30m | 2 tasks | 14 files |
| Phase 14-broader-codebase-name-vs-behavior-audit P04 | 30m | 3 tasks | 7 files |

## Accumulated Context

### v1.1 Shipped

v1.1 Admin Console shipped 2026-05-08. All 27 REQ-IDs complete across Phases 6–8 + Phase 7.1.
Phase 8 SC #5 (p95 baseline delta) deferred — no pre-deploy baseline was captured.

### v1.2 Shipped

v1.2 Multi-AI Prompts shipped 2026-05-13. All 5 REQ-IDs (BRKT-01, AISEL-01..04) functionally complete across Phases 9-10 (8 plans). Gemini flag-gated behind `DECKFLOW_GEMINI_ENABLED` because full packet exceeds gemini.google.com paste cap. Audit gap: documentation-only (see `.planning/milestones/v1.2-MILESTONE-AUDIT.md`).

### v1.3 Roadmap Created (2026-05-13)

22 v1.3 REQ-IDs mapped across 5 phases:

- **Phase 11 (WDG-01..10)**: 10 sweep PRs from `260513-wdg-FINDINGS.md`. Highest leverage: Sweep 1 (`site-common.css` cross-cutting a11y — avoids 22× theme fork) + Sweep 2 (admin focus-visible foundation).
- **Phase 12 (RENAME-01..03)**: AI-agnostic URL slugs + permanent 301 redirects + H1/nav/hub labels + filename sanitizer. Source: `.planning/AI-AGNOSTIC-RENAME-BRAINSTORM.md` Option A (Mock A).
- **Phase 13 (CLASSRENAME-01..03)**: Strip `ChatGpt` prefix from all C# types; backfill `<summary>` doc comments; update DI + InternalsVisibleTo + test fixtures + Razor `@model` directives.
- **Phase 14 (AUDIT-01..03)**: Broader name-vs-behavior pass across all 5 projects; doc-comment backfill; Release build clean.
- **Phase 15 (AIPLATFORM-01..03)**: Sealed `AiPlatform` record value object per `10-AISEL-PLATFORM-DESIGN.md`. OCP 3/10 → 8/10. T1-T8 reverify for zero behavior change.

### Decisions

- Phase order locked by user 2026-05-13: WDG first (visible quality wins, low-risk) → URL rename → class rename (paired conceptually but separable in execution) → broader audit → refactor last (sits on clean class names).
- Phase 11 stays as ONE phase with 10 plans rather than splitting. The 10 sweep PRs in FINDINGS.md are already sequenced by leverage; no hard dependency forces a split.
- Phases 12 and 13 stay separate (not combined). User intent is URL + class rename ship together conceptually but execution is cleaner with two phases — URL rename ships first to validate user-facing terminology, class rename follows to bring code in line.
- Phase 15 acceptance: `AiPlatform.All` extension test must prove adding a hypothetical 4th platform requires no switch-expression / request-setter / Razor partial / context-parser edits.

### Constraints Carried Forward (per PROJECT.md + CLAUDE.md)

- ASP.NET 10 + Razor pinned — no framework migration in v1.3.
- Cross-cutting CSS lands in `site-common.css`, NOT `site.css` or per-theme forks (WDG-08 in Phase 11).
- 22 guild theme stylesheets are full forks — every CSS gap multiplied 22× if landed in wrong layer.
- VSTest unreliable in WSL — Phase 14 AUDIT-03 verification relies on `dotnet build` clean + push-and-watch CI.
- Plain default-author commits across all of v1.3 (no `Co-Authored-By` trailer).
- Public repo `luntc1972/DeckFlow` — no secrets in commits; Render dashboard holds env vars with `sync: false`.

### Blockers/Concerns

- None at roadmap creation. v1.3 branch is clean off `main` at `7ed0cde` (post-v1.2 merge).
- `harvest-killed-by-suggestion` debug remains parked at H1 hypothesis in `.planning/debug/` — deferred from v1.3 (root-cause investigation is gating, not parallel-shippable with WDG/rename/audit/refactor).

## Session Continuity

Last session: 2026-05-22T03:10:00.000Z

Stopped at: Phase 999.5 execute-phase complete (4 plans, 15 commits, full-suite 445/433/9/3)

Next action on resume: v1.3 ship sequence. Run `/gsd-verify-work 999.5` (orchestrator-led UAT — phase had no UI changes so verify is primarily test-suite + regression-fact attestation). Then `/gsd-audit-milestone` → `/gsd-complete-milestone` → `/gsd-ship` to publish v1.3 to main.

**Resume guidance:**

- Read `.planning/phases/999.5-v1-3-backlog-catch-up-test-hardening/999.5-CONTEXT.md` for the 28 locked decisions D-01..D-28 (binding contract for execute-phase).
- Read each `999.5-0X-PLAN.md` for task-level `<read_first>` + `<acceptance_criteria>` + `<action>` blocks Codex will consume cold.
- Read `.planning/PROJECT.md` for v1.3 milestone goals + ship-sequence rationale.
- Branch: `v1.3` (created 2026-05-13 from `main` at `7ed0cde`).

## Operator Next Steps

- v1.3 ship-sequence: `/gsd-verify-work 999.5` → `/gsd-audit-milestone` → `/gsd-complete-milestone` → `/gsd-ship`.
- Investigate 9 pre-existing test failures NOT in 999.5 scope when v1.4 opens: `PacketArtifactStoreTests` × 2, `ArchidektCacheJobServiceTests` × 2, `AiPlatformPhase10RoundTripTests` × 4, `BasicAuthMiddlewareTests` × 1. None touch shipped 999.x service code; CI baseline can absorb until a v1.4 hardening phase.
- Other deferred (NOT in 999.5 scope): Gemini paste-limit workaround (flag-gated), v1.1 phase-dir archive move (v1.4 cleanup), edhtop16 filter-defaults backlog row.
