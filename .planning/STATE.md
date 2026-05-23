---
gsd_state_version: 1.0
milestone: v1.3
milestone_name: Frontend Hardening + AI-Agnostic Rename + Code Hygiene
status: Awaiting next milestone
stopped_at: "Phase 999.6 P03 complete — atomic F-PROD-CONTRACT fix commit d758609 (debug doc + `IHarvestRunStore.GetByIdAsync` + `HarvestRunStore` SQLite/Postgres impl with `_connectionInfo.IsPostgres ? (object)id : id.ToString()` binding + `ArchidektCacheJobService.GetJob` rewire + `FakeHarvestRunStore` async wrapper bundled per D-18) + SUMMARY commit 1adf65b. Build 0/0 warnings/errors. Focused gate 5/5 (Failed:0,Passed:14,Total:14). Full-suite gate 5/5 deterministic (Web Failed:0/Passed:440/Skipped:3/Total:443 + Core Failed:0/Passed:57/Total:57 — Failed delta 2→0; phase-wide 9→0). D-15 fired (real production bug shipped inside 999.6). D-23 STATE.md status:ready_to_ship restoration deferred to /gsd-verify-work 999.6 (NOT toggled by execute-plan agent)."
last_updated: "2026-05-23T14:21:12.981Z"
last_activity: 2026-05-23 — Milestone v1.3 completed and archived
progress:
  total_phases: 13
  completed_phases: 12
  total_plans: 51
  completed_plans: 50
  percent: 92
---

## Deferred Items

Reviewed 2026-05-13 via `/gsd-review-backlog`. Promoted to v1.3 candidates: harvest-killed-by-suggestion (debug), AiPlatform value object refactor. Removed 7 quick_tasks (all complete per SUMMARY, stale "missing" label). Closed 07-VERIFICATION as satisfied (v1.1 shipped 2026-05-08, no harvest crash incidents). Backfilled 09/10-VERIFICATION.md and Phase 9 SUMMARY frontmatter same session.

| Category | Item | Status |
|----------|------|--------|
| tech_debt | Gemini paste-limit workaround | flag-gated DECKFLOW_GEMINI_ENABLED |
| archive | v1.1 phase dirs (`06-admin-shell-flags-foundation`, `07-harvest-controls-stats`, `07.1-categories-feature-flag-sameorigin-ajax-fix`, `08-analytics`) | needs move to `.planning/milestones/v1.1-phases/`; quick-task candidate for v1.4 cleanup |
| tech_debt | Semantic-completeness guards for `DeckComparisonService.ParseComparisonResponse` + `MetaGapService.ParseResponse` (mirror `HasMeaningful*Content` pattern from `ResponseParsers.cs:99-130`) | **PROMOTED to Phase 999.5 P02** (planned 2026-05-21) — see CONTEXT D-09/D-10/D-11/D-12 in `.planning/phases/999.5-v1-3-backlog-catch-up-test-hardening/999.5-CONTEXT.md`; tracks the "valid JSON but semantically empty AI output" edge case (e.g., `{"deck_comparison":{}}` parses into a default-init DeckComparisonResponse and bypasses both the existing wrong-shape guard and 999.4's truncation UX). Closes 999.4 D-07 carve-out via 999.5 plans 02 |

### Acknowledged at v1.3 close (2026-05-23)

Audit-open scan surfaced 21 items at `/gsd-complete-milestone v1.3` pre-flight. All triaged and acknowledged for deferral per user `[A]` choice 2026-05-23.

| Category | Item | Status | Disposition |
|----------|------|--------|-------------|
| debug | 999.6-archidekt-cache-job | unknown | CLOSED — investigation resolved by 999.6 P03 commit d758609 (F-PROD-CONTRACT applied: `IHarvestRunStore.GetByIdAsync` added); audit-open scanner reads stale `unknown` status from debug doc frontmatter (housekeeping defer to v1.4) |
| debug | 999.6-basicauth-flaky | unknown | CLOSED — investigation resolved by 999.6 P02 commit a62f608 (F-ENV-COLLECTION: `AdminEnvCollection` + `EnvScope` helper); audit-open scanner reads stale `unknown` status; housekeeping defer to v1.4 |
| debug | v13-harvest-worker-stalled | investigation_inconclusive | DEFERRED — production harvester diagnostic from 2026-05-22; diagnostic logging committed (commit 4372de5) and deployed; v1.3 ship continues, post-ship monitoring + follow-up debug deferred to v1.4 |
| uat-gap | Phase 11/13/15/999.1-999.8 UAT files (10 phases) | "unknown" / "bypassed" / "superseded-by-999.6" | FALSE POSITIVE — audit-open scanner expects `status: complete` / `passed`, but project convention uses `passed` / `superseded-by-999.6` / `bypassed`. All 10 phases verified passed per `.planning/v1.3-MILESTONE-AUDIT.md` (audit status: passed, 22/22 reqs satisfied). 0 pending scenarios across all. Defer scanner-vocabulary alignment to v1.4 housekeeping. |
| quick_task | 260504-in1-fix-the-remaining-phase-07-1-ui-review-i | missing | DEFERRED — pre-v1.3 capture; out of v1.3 milestone scope (v1.1/v1.2 cleanup) |
| quick_task | 260506-hgd-chatgpt-artifact-local-download-upload-r | missing | DEFERRED — pre-v1.3 capture; ChatGPT artifact local round-trip; v1.4 backlog |
| quick_task | 260506-kwt-make-chatgpt-zip-download-button-more-pr | missing | DEFERRED — pre-v1.3 capture; ChatGPT zip download button polish; v1.4 backlog |
| quick_task | 260507-l7x-fix-chatgpt-packets-saved-session-upload | missing | DEFERRED — pre-v1.3 capture; ChatGPT packets saved-session upload; v1.4 backlog |
| quick_task | 260507-m8k-fix-admin-harvest-decks-counter-and-rece | missing | DEFERRED — pre-v1.3 capture; admin harvest decks counter + recent decks list; v1.4 backlog |
| quick_task | 260507-ner-add-admin-analytics-auto-refresh-via-met | missing | DEFERRED — pre-v1.3 capture; admin analytics auto-refresh via meta-refresh; v1.4 backlog |
| quick_task | 260507-o20-restore-full-round-trip-on-chatgpt-saved | missing | DEFERRED — pre-v1.3 capture; restore full round-trip on ChatGPT saved sessions; v1.4 backlog |
| quick_task | 260513-wdg-web-design-guidelines-audit-findings | missing | CLOSED — WDG-01..10 SHIPPED in v1.3 Phase 11 (10/10 plans, 11-VERIFICATION.md passed, REQUIREMENTS.md checkboxes flipped via 999.7-02). Quick-task index entry stale; v1.4 housekeeping. |

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-13 for v1.3)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Milestone complete

## Current Position

Phase: Milestone v1.3 shipped — PR #1 awaiting merge
Plan: —
Status: PR open https://github.com/luntc1972/DeckFlow/pull/1 (v1.3 → main); post-merge: Render auto-deploys; operator runs SC6 curl verification
Last activity: 2026-05-23 — v1.3 PR #1 created (370 commits ahead of main, +47,724 / -5,385 LOC across 386 files)

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
| 999.6 | 3 | - | - |

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
| Phase 999.6 P02 | 15m | 5 tasks | 5 files (1 Codex dispatch + 5/5 focused + 5/5 full-suite stability gates) |
| Phase 999.6 P03 | 20m | 4 tasks | 5 files (1 Codex dispatch + 5/5 focused + 5/5 full-suite PHASE EXIT GATE) |

## Accumulated Context

### Roadmap Evolution

- Phase 999.7 inserted after Phase 999.6: v1.3 audit cleanup: STATE arithmetic + WDG checkboxes + stale doc-comments (audit findings F-01, F-02) (URGENT)

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
- Phase 999.6 P02 (2026-05-22): F-ENV-COLLECTION applied — shared `EnvScope` helper at `DeckFlow.Web.Tests/Infrastructure/EnvScope.cs` + `AdminEnvCollection` with `[CollectionDefinition(DisableParallelization=true)]` serialize both env-mutating test classes (`BasicAuthMiddlewareTests` + `Security/AdminBruteForceTrackerStoreTests`). Closes the BasicAuth flaky-fact root cause (cross-class race on process-wide `FEEDBACK_ADMIN_USER`/`FEEDBACK_ADMIN_PASSWORD`). H2 falsified (middleware already reads env vars fresh per InvokeAsync — no SUT touched). 10/10 deterministic stability gate (focused 5/5 Failed:0,Passed:5,Total:5; full-suite 5/5 Failed:2,Passed:438,Skipped:3,Total:443). 2 ArchidektCacheJob failures carry forward to P03 (commit a62f608 fix + 360dee0 SUMMARY).
- Phase 999.6 P03 (2026-05-22): F-PROD-CONTRACT applied (D-15 fired — real production bug ships inside 999.6). Added `Task<HarvestRunRow?> GetByIdAsync(Guid, CancellationToken)` to `IHarvestRunStore` + production impl in `HarvestRunStore` (provider-specific Guid binding via `_connectionInfo.IsPostgres ? (object)id : id.ToString()` matching three precedents at lines 117-119 / 164-166 / 200-202) + rewired `ArchidektCacheJobService.GetJob` from 16-line `GetActiveAsync`+id-filter to 5-line `GetByIdAsync` delegate + extended inline `FakeHarvestRunStore` with async wrapper. SUT's own forward-reference comment ("Plan 04 will add one") resolved by THIS plan. H2 (`_activeJob` field race) stays falsified — field does not exist; only `_activeJobCts` (lifecycle CTS) exists. 10/10 deterministic PHASE EXIT GATE (focused 5/5 Failed:0/Passed:14/Total:14; full-suite 5/5 Web Failed:0/Passed:440/Skipped:3/Total:443 + Core Failed:0/Passed:57/Total:57). Phase-wide net delta 9 → 0 failures across 3 plans (P01: 6 stale closed, P02: 1 flaky closed, P03: 2 ambiguous closed). Atomic commit d758609 (debug doc + 4 code files) per D-18; SUMMARY commit 1adf65b. STATE.md status:ready_to_ship restoration deferred to /gsd-verify-work 999.6 per D-23.

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

Last session: 2026-05-22T18:09:00.000Z

Stopped at: Phase 999.6 P03 complete — atomic F-PROD-CONTRACT fix commit d758609 (debug doc + `IHarvestRunStore.GetByIdAsync` + `HarvestRunStore` SQLite/Postgres impl with `_connectionInfo.IsPostgres ? (object)id : id.ToString()` binding + `ArchidektCacheJobService.GetJob` rewire + `FakeHarvestRunStore` async wrapper bundled per D-18) + SUMMARY commit 1adf65b. Build 0/0 warnings/errors. Focused gate 5/5 (Failed:0,Passed:14,Total:14). Full-suite gate 5/5 deterministic (Web Failed:0/Passed:440/Skipped:3/Total:443 + Core Failed:0/Passed:57/Total:57 — Failed delta 2→0; phase-wide 9→0). D-15 fired (real production bug shipped inside 999.6). D-23 STATE.md status:ready_to_ship restoration deferred to /gsd-verify-work 999.6 (NOT toggled by execute-plan agent).

Next action on resume: `/gsd-verify-work 999.6` — re-validate the closing full-suite gate (`Failed: 0`), confirm STATE.md + ROADMAP.md reflect 3/3 plans complete, and atomically transition `.planning/STATE.md` `status` field from `executing` back to `ready_to_ship` per CONTEXT D-23. Then `/gsd-audit-milestone v1.3` → `/gsd-complete-milestone v1.3` → `/gsd-ship v1.3` per Operator Next Steps.

**Resume guidance:**

- Read `.planning/phases/999.6-v1-3-ship-gate-test-residual-cleanup/999.6-03-SUMMARY.md` for P03 closure detail + phase-level closure note (9 → 0 failure delta, Codex review-feedback blocker resolution table, commit hash sequence).
- Read `.planning/phases/999.6-v1-3-ship-gate-test-residual-cleanup/999.6-CONTEXT.md` D-23 for the STATE.md status-toggle protocol.
- Read `.planning/debug/999.6-archidekt-cache-job.md` for the verdict + the SOLID review proving F-PROD-CONTRACT's additive interface method is correct.
- Branch: `v1.3` (created 2026-05-13 from `main` at `7ed0cde`).

## Operator Next Steps

- Start the next milestone with /gsd-new-milestone
