---
gsd_state_version: 1.0
milestone: Cycle 9
milestone_name: — Content Pipeline & Publish-Tracking
status: shipped
stopped_at: Cycle 9 SHIPPED (2026.06.5) — planning next milestone
last_updated: "2026-06-19T16:00:00.000Z"
last_activity: 2026-06-19 -- Cycle 9 closed; all 4 phases (55-58) shipped + secured, tag 2026.06.5
progress:
  total_phases: 4
  completed_phases: 4
  total_plans: 11
  completed_plans: 11
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Planning next milestone (`/gsd-new-milestone`) — Cycle 9 shipped

## Current Position

Cycle 9 — Content Pipeline & Publish-Tracking: **SHIPPED 2026-06-19, `2026.06.5`**
Phase: 58 — COMPLETE (all 4 phases 55-58 done + secured)
Status: milestone closed; archived to `.planning/milestones/cycle9-{ROADMAP,REQUIREMENTS}.md`
Last activity: 2026-06-19 -- Cycle 9 closed (squash→main + tag 2026.06.5)

Progress: [██████████] 100% (4/4 phases)

## Roadmap Summary

| # | Phase | Requirements | Status |
|---|-------|-------------|--------|
| 55 | Publish-State Foundation | PUB-01, PUB-02 | Complete |
| 56 | Studio Surfaces | BROWSE-01, BROWSE-02, BROWSE-03, REM-01, REM-02, ADD-01, PUB-03 | Complete |
| 57 | Admin Surface + Distill Quality | SITE-01, DIST-01 | Complete |
| 58 | Dogfood | DOGFOOD-01 | Not started |

**Phase ordering rationale:**

- 55 first: `published_utc` migration + shared status-derivation engine is the foundational dependency; BROWSE-02, PUB-03, SITE-01 all consume it. Build once, reuse everywhere.
- 56 after 55: Studio surfaces (channel browse, per-video status, block/unblock, single-video add, Review/Publish display) all require the Phase 55 status engine.
- 57 after 55 (independent of 56): admin site display consumes the Phase 55 status engine; distill prompt quality is fully independent of publish tracking. Both land on non-overlapping surfaces so they can share a phase.
- 58 last: dogfood validation depends on all earlier phases being complete (runs real harvest + distill + publish with status surfacing in both apps).

## Performance Metrics

**Velocity (Cycle 8 reference — most recent shipped):**

- 4 phases, 11 plans, 46 commits (2026-06-17, single day)
- Cross-AI execution pattern: Codex codes, Claude reviews (TEMP OVERRIDE: Claude implements until 2026-06-18 11:00 MDT)
- Final test gate: Core 447/447, Web 633/644 (11 PG-skip); build 0/0

## Accumulated Context

### Key Decisions

- **Cycle 9 roadmap created 2026-06-18:** 4 phases (55-58), 12/12 requirements mapped.
- **Granularity:** Config = coarse; 4 phases is the natural minimum — the PUB-01/02 migration is a hard sequential dependency for everything that displays status, and DOGFOOD-01 must be last. Compressing below 4 would merge the foundation migration with consumers (unsafe) or merge dogfood with production code (skips the integration gate).
- **Phase 55 delivers a single shared `PublishStateDeriver`:** PUB-02's derived-state logic must live in exactly one place. BROWSE-02 (per-video status on the channel list) and PUB-03 (Studio Review/Publish display) both reuse this same engine; no duplicate status logic.
- **BROWSE-01/02/03 and ADD-01 co-locate in Phase 56 with REM/PUB-03:** All of these touch `DeckFlow.Studio/Pages/Harvest.razor` (or the same Studio shell). Grouping them avoids multiple passes over the same page in adjacent phases.
- **Phase 57 groups SITE-01 and DIST-01:** These have no shared implementation surface but are both logically "site admin + content quality improvements" — one is a Razor view column, the other is a Core orchestrator prompt change. Neither depends on Phase 56's Studio work, so they can execute in parallel with 56 or after it.
- **Both publish paths must stamp `published_utc` in Phase 55:** The DirectPush path (`UpsertContentColumnsOnlyAsync` in `ContentKbOrchestrator`) and the git Publish path (`ExportIndexAsync` / `CopyApprovedArtifactsToRepoAsync`) must both be updated together — a partial implementation would leave the status engine permanently in a `Never published` state for entries pushed via the other path.
- **Dialect-guarded idempotent migration pattern:** Follow the `approval_status` / `is_evergreen` / `is_hidden` precedent — SQLite `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` guarded, Postgres `DO $$ ... IF NOT EXISTS ... $$` guarded, called from `EnsureSchemaAsync`.
- **REM wires existing Core methods (no new domain logic):** `BlockVideoAsync` / `UnblockVideoAsync` / `ListBlockedAsync` / `DeleteVideoByYoutubeIdAsync` already exist in Core (Phase 37.6, CLI-only). Phase 56 is UI-wiring only; the Core behavior is not modified.
- **ADD-01 is a confirmation/polish task:** The Harvest paste-URL flow already partially exists. Planning should confirm whether it fully covers the single-video-by-URL case or needs a targeted polish pass; implementation effort is expected to be small.

### Key Pitfalls to Watch

- **`published_utc` must be set by BOTH publish paths:** git Publish and DirectPush. If only one stamps it, the status engine will show incorrect `Never published` for entries pushed via the other path.
- **`PublishStateDeriver` must be in Core, not Studio:** Pure logic that reads across stores must live in `DeckFlow.Core` so it can be unit-tested without inverting project dependencies (same rationale as `VideoStatusResolver` in Phase 45-02).
- **`Local-newer` requires a reliable local timestamp:** The derived state compares `published_utc` against the local index/distill time. Ensure the comparison uses the same timezone convention (UTC throughout) to avoid false positives.
- **Block action must hard-delete artifacts before adding to blocklist:** If the blocklist write succeeds but artifact delete fails, the KB is left with orphaned content. Prefer delete-first + blocklist-second, or a compensating read on the block-list endpoint.
- **Distill prompt change is not a provider swap:** DIST-01 touches only the prompt template/instructions in the Core orchestrator distill slice. No changes to `LlmDistillationProviderFactory`, CLI invocation, or spend ledger.

### Pending Todos

- 15 pre-v1.5 open artifacts (stale 999.x/v13 debug sessions, May quick-task refs, empty todos) — acknowledged cruft; clean via `/gsd-cleanup` when convenient.

### Blockers/Concerns

- None at roadmap creation.

### Carry-Forward from Cycle 8

| Item | Status |
|------|--------|
| `deckflow_admin` credential deletion (password rotated; deletion owed by operator) | Operator task — not a code requirement |
| Operator live Gemini paste + `DECKFLOW_GEMINI_ENABLED` prod flip | Operator manual verification — not in Cycle 9 scope |
| Full dual-dialect branch collapse (gated on PG DDL parity test) | Backlog — deferred |
| Prod-DB dedup SQL for @salubrioussnail duplicate grid rows | Operator task — tracked in `project_cycle8_contentkb_cleanup.md` |
| Based Deck Department 20 videos stuck `approval_status=pending` | Resolved by Phase 56 publish-tracking surfaces |

## Deferred Items

**Resolved in Cycle 8:**

- ✅ Architecture backlog burn-down (Phase 53, ARCH-01/02)
- ✅ SpellbookCombo ranking fields + Deck Primer priority-rank (Phase 54, FEAT-01/02)
- ✅ F-51-PG-01: `AddDeckIdsAsync` Postgres `42883` TEXT-vs-`timestamptz` cast (Phase 51)
- ✅ v1.7 live prod-publish verification (Phase 52)

**Open / carried forward:**

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| tech_debt | Gemini paste-limit workaround | DEFERRED (flag-gated `DECKFLOW_GEMINI_ENABLED`) | v1.5 scoping |
| arch | Finding B: Split CategoryKnowledgeRepository | DEFERRED (backlog) | v1.6 Phase 39 |
| arch | Findings D-K | DEFERRED (backlog) | v1.6 Phase 39 |
| housekeeping | 15 pre-v1.5 open artifacts | ACKNOWLEDGED — clean via `/gsd-cleanup` | v1.5 close 2026-06-10 |
| ops | SEL-02 expert-pin live-pin re-confirm | PENDING — needs KB-enable window | v1.5 close |
| ops | Studio "About" link is the Blazor scaffold placeholder (points at ASP.NET docs) | TODO — low priority | v1.7 Phase 45 dogfood |
| ops | `deckflow_admin` credential deletion | Operator task (password already rotated) | Cycle 8 Phase 52 |

## Session Continuity

Last session: 2026-06-18
Stopped at: Phase 56 executed + verified (PASS 7/7)
Resume: Phases 55-56 complete. Next: `/gsd-plan-phase 57` (Admin Surface + Distill Quality)

## Operator Next Steps

- Plan Phase 55: `/gsd-plan-phase 55`
