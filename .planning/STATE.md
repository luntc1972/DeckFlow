---
gsd_state_version: 1.0
milestone: v1.3
milestone_name: Frontend Hardening + AI-Agnostic Rename + Code Hygiene
status: executing
stopped_at: Phase 999.6 P01 complete (4 atomic mechanical fix commits + 1 SUMMARY commit, full-suite 443/437/3/3; 6/9 residual failures resolved; D-06 Branch B taken)
last_updated: "2026-05-22T17:18:00.000Z"
last_activity: 2026-05-22 -- Phase 999.6 P01 mechanical stale-test cleanup complete
progress:
  total_phases: 11
  completed_phases: 9
  total_plans: 46
  completed_plans: 43
  percent: 93
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
**Current focus:** Phase 999.6 — v1-3-ship-gate-test-residual-cleanup

## Current Position

Milestone: v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene
Phase: 999.6 (v1-3-ship-gate-test-residual-cleanup) — EXECUTING
Plan: 2 of 3 (P01 complete, P02 BasicAuth flaky-test debug+fix next)
Status: Executing Phase 999.6 — P01 closed; full-suite 9 → 3 residuals (P02 + P03 carry-forwards enumerated in 999.6-01-SUMMARY.md)
Last activity: 2026-05-22 -- Phase 999.6 P01 mechanical stale-test cleanup complete
Progress: [█████████░] 93%

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
- Phase 999.6 P01 (2026-05-22): D-06 Branch B taken — `PacketArtifactStoreRoundTripTests.cs` deleted because both authorized probe paths (`/tmp/arna-test/` and `/mnt/c/tmp/arna-test/`) missing the Arna fixture files. Branch B is an authorized fallback per D-06; the integration assertion is fixture-less and has no value without the source data. Total dropped 445 → 443.
- Phase 999.6 P01 (2026-05-22): Mechanical stale-test fixes catch tests up to shipped production renames (PacketArtifactStore msg generalization at D-04, Phase 12 `compare2`→`comparison` page rename at D-05/D-07). 6 of 9 residual failures closed. Codex authored every edit via shell-pipe `codex exec`; orchestrator ran Windows-dotnet gate + committed (hybrid pattern inherited from 999.5 P01).

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

Last session: 2026-05-22T17:18:00.000Z

Stopped at: Phase 999.6 P01 complete — 4 atomic mechanical fix commits (d9aeb65 D-04 → 9379356 D-05 → 420fbfb D-07 → 01534e3 D-06 Branch B) + 1 SUMMARY commit (905ef8f). Full-suite 443/437/3/3 (Failed 9 → 3; Total 445 → 443 under D-06 Branch B). 6/9 residual failures closed; 3 residuals (1 BasicAuth + 2 ArchidektCacheJob) carry forward to P02 + P03.

Next action on resume: P02 dispatch — `999.6-02-PLAN.md` (BasicAuth flaky-test root-cause + fix). Per CONTEXT D-09, Claude runs `/gsd-debug 999.6-basicauth-flaky` first; Codex applies fix per the debug verdict.

**Resume guidance:**

- Read `.planning/phases/999.6-v1-3-ship-gate-test-residual-cleanup/999.6-CONTEXT.md` for D-09/D-10/D-11 P02 binding decisions.
- Read `.planning/phases/999.6-v1-3-ship-gate-test-residual-cleanup/999.6-02-PLAN.md` for P02 task-level binding contract.
- Read `.planning/phases/999.6-v1-3-ship-gate-test-residual-cleanup/999.6-01-SUMMARY.md` for the 3 residual failures explicitly enumerated.
- Branch: `v1.3` (created 2026-05-13 from `main` at `7ed0cde`).

## Operator Next Steps

- P02 dispatch: `/gsd-debug 999.6-basicauth-flaky` (Claude investigation) → Codex applies fix → orchestrator runs Windows-dotnet gate + commits.
- P03 dispatch (after P02 lands): `/gsd-debug 999.6-archidekt-cache-job` (1 shared session per D-12) → Codex applies fix (test-only or SUT real-bug per D-15).
- After P03 lands and full-suite hits `Failed: 0`: `/gsd-verify-work 999.6` to restore STATE.md `ready_to_ship` per D-23 → `/gsd-audit-milestone` → `/gsd-complete-milestone` → `/gsd-ship` to publish v1.3 to main.
- Other deferred (NOT in 999.6 scope): Gemini paste-limit workaround (flag-gated), v1.1 phase-dir archive move (v1.4 cleanup), edhtop16 filter-defaults backlog row.
