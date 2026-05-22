---
gsd_state_version: 1.0
milestone: v1.3
milestone_name: Frontend Hardening + AI-Agnostic Rename + Code Hygiene
status: planning
stopped_at: Phase 999.5 context gathered — CONTEXT + DISCUSSION-LOG committed; ready for /gsd-plan-phase
last_updated: "2026-05-22T01:35:00.000Z"
last_activity: 2026-05-22 -- Phase 999.5 discuss-phase complete (commit 9327df4); 23 decisions D-01..D-23 captured across P01 test-fixes / P02 D-07 semantic guards / P03 harvest archival. P03 collapsed after grep confirmed fix already shipped 2026-05-03.
progress:
  total_phases: 10
  completed_phases: 9
  total_plans: 42
  completed_plans: 39
  percent: 92
---

## Deferred Items

Reviewed 2026-05-13 via `/gsd-review-backlog`. Promoted to v1.3 candidates: harvest-killed-by-suggestion (debug), AiPlatform value object refactor. Removed 7 quick_tasks (all complete per SUMMARY, stale "missing" label). Closed 07-VERIFICATION as satisfied (v1.1 shipped 2026-05-08, no harvest crash incidents). Backfilled 09/10-VERIFICATION.md and Phase 9 SUMMARY frontmatter same session.

| Category | Item | Status |
|----------|------|--------|
| tech_debt | Gemini paste-limit workaround | flag-gated DECKFLOW_GEMINI_ENABLED |
| debug | harvest-killed-by-suggestion (H1 hypothesis parked in `.planning/debug/`) | deferred from v1.3 — root-cause work is gating, not parallel-shippable |
| archive | v1.1 phase dirs (`06-admin-shell-flags-foundation`, `07-harvest-controls-stats`, `07.1-categories-feature-flag-sameorigin-ajax-fix`, `08-analytics`) | needs move to `.planning/milestones/v1.1-phases/`; quick-task candidate for v1.4 cleanup |
| tech_debt | Semantic-completeness guards for `DeckComparisonService.ParseComparisonResponse` + `MetaGapService.ParseResponse` (mirror `HasMeaningful*Content` pattern from `ResponseParsers.cs:99-130`) | deferred — surfaced in Codex pass-1 999.4 review as MED-2; tracks the "valid JSON but semantically empty AI output" edge case (e.g., `{"deck_comparison":{"deck_a_name":"Atraxa"}}` parses into a mostly-empty object and bypasses both the existing wrong-shape guard and 999.4's truncation UX). Phase 999.4 D-07 captures the carve-out; promote when next AI-response phase is planned |

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-13 for v1.3)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 999.5 — v1.3 backlog catch-up + test hardening (omnibus pre-ship cleanup).

## Current Position

Milestone: v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene
Phase: 999.5 v1.3-backlog-catchup-test-hardening — PLANNING (CONTEXT.md committed; ready for /gsd-plan-phase)
Plan: 3 plans (P01 test-fixes, P02 D-07 semantic guards, P03 harvest archival) — 23 decisions D-01..D-23 captured in CONTEXT.md
Status: Awaiting /gsd-plan-phase 999.5 → /gsd-review → /gsd-execute-phase
Last activity: 2026-05-22 -- Phase 999.5 discuss-phase complete (commit 9327df4). P03 collapsed to doc cleanup + regression facts after grep confirmed gate-starvation fix already shipped 2026-05-03.
Progress: [█████████░] 92%

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

Last session: 2026-05-22T01:25:25.040Z

Stopped at: Phase 999.5 context gathered

Next action on resume: `/gsd-discuss-phase 999.5` to gather plan-decisions (xunit isolation strategy for FeedbackStoreTests, throttle-test tolerance, semantic-guard wording, harvest H1 hypothesis resume), then `/gsd-plan-phase 999.5` → Codex peer review → `/gsd-execute-phase 999.5`.

**Resume guidance:**

- Read `.planning/PROJECT.md` for v1.3 milestone goals + execution order rationale.
- Read `.planning/REQUIREMENTS.md` for 22 v1.3 REQ-IDs with phase assignments.
- Read `.planning/quick/260513-wdg-web-design-guidelines-audit-findings/260513-wdg-FINDINGS.md` for the 10 sweep PRs that decompose Phase 11.
- Read `.planning/AI-AGNOSTIC-RENAME-BRAINSTORM.md` for Mock A naming decisions feeding Phase 12.
- Read `.planning/milestones/v1.2-phases/10-claude-gemini-artifact-optimization/10-AISEL-PLATFORM-DESIGN.md` for Phase 15 design.
- Branch: `v1.3` (created 2026-05-13 from `main` at `7ed0cde`).

## Operator Next Steps

- `/gsd-discuss-phase 999.5` — capture decisions for the 3-plan omnibus: xunit collection / WAL / temp-file-naming strategy for FeedbackStoreTests, throttle-test 429 retry-tolerance call, semantic-guard error-message wording for DeckComparison + MetaGap, harvest H1 hypothesis resume path.
- After discuss: `/gsd-plan-phase 999.5` produces PLAN.md across 3 plans; Codex peer-reviews via `/gsd-review`; `/gsd-execute-phase 999.5` ships.
- v1.3 ship-sequence (after 999.5 closes): `/gsd-audit-milestone` → `/gsd-complete-milestone` → `/gsd-ship`.
- Other deferred (NOT in 999.5 scope): #1 Gemini paste-limit workaround (flag-gated), #3 v1.1 phase-dir archive move (v1.4 cleanup), edhtop16 filter-defaults backlog row.
