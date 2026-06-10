---
gsd_state_version: 1.0
milestone: v1.5
milestone_name: Deck Primer Generator + Content KB Integration + Housekeeping
status: Awaiting next milestone
stopped_at: Phase 31 COMPLETE + APPROVED; ROADMAP 31 [x]; STATE 25/25 / 6-of-6 phases. All v1.5 phases done. Tracking committed.
last_updated: "2026-06-10T01:23:17.528Z"
last_activity: 2026-06-10 — Milestone v1.5 completed and archived
progress:
  total_phases: 8
  completed_phases: 6
  total_plans: 25
  completed_plans: 25
  percent: 75
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-06-10 after v1.5 milestone)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Planning next milestone (`/gsd-new-milestone`) — v1.5 shipped 2026-06-10

## Current Position

Phase: Milestone v1.5 complete
Plan: —
Status: Awaiting next milestone
Last activity: 2026-06-10 — Milestone v1.5 completed and archived

## Performance Metrics

**Velocity (v1.4 reference — most recent shipped):**

- 31 plans across 14 phases (2026-05-23 → 2026-06-03, 11 days)
- Cross-AI execution pattern sustained: Codex codes, Claude reviews
- Final test gate: Core 257/257, Web 528 pass / 5 PG-skips

**v1.5 Forecast (per research SUMMARY.md):**

| Phase | Plans (est.) | Notes |
|-------|--------------|-------|
| 28 — Housekeeping Bundle | 2-3 | KB-12 bounded replace + VERIFICATION doc work |
| 29 — Core Doc Backfill | 4-6 | 186 sites, namespace-scoped; gate widen is final commit |
| 30 — Content KB Integration | 3-4 | Flag flip + relevance service + panel + admin score view |
| 31 — Deck Primer Generator | 6-10 | Split: models/routing + service + variants/UI + round-trip |
| **Total estimate** | **~15-23 plans** | Coarse granularity; 4 phases |

## Accumulated Context

### Decisions

- **Phase ordering (research-aligned):** 28 (housekeeping, no web surface) → 29 (Core doc, parallel-capable) → 30 (KB integration, validates prod flag) → 31 (Primer, milestone headline). Tracks A and B independent; either can reorder at user's discretion.
- **HSK-01 gate-widen rule:** editorconfig `[DeckFlow.Core/**.cs]` CS1591 gate must be the FINAL commit of Phase 29 — never widened before all 186 sites are documented.
- **PRM-01 spike is Phase 31's first execution unit:** combo-data richness + prompt-size measurement must complete before `DeckPrimerPacketService` is implemented. If Gemini paste cap is exceeded at full section count, gate Gemini on the primer the same way analysis does.
- **Phase 30 pre-implementation step:** live tag-distribution audit on prod KB (clips + content_tags) must run before `ContentKbRelevanceService` is specced — audit thresholds calibrated against actual data.
- **Prompt-variant decoupling invariant:** ChatGPT / Claude / Gemini primer prompt variants are intentionally decoupled (no shared prose); hand-edit all 3 for content changes (per ADR 0001 + a1fa5ad lesson).
- **`{ get; init; }` guard:** every new record type in Phases 30-31 must preserve `init` accessor — System.Text.Json silently drops get-only props in .NET 9+; include serialization round-trip test per record.
- **PrimerAllowedNames first:** implement `PrimerAllowedNames` as the first task in Phase 31 artifact store work — `PacketArtifactStore.ReadEntries` silently drops names not in the active allowlist.

### Roadmap Evolution

- Phase 33 added 2026-06-08 (Admin Content KB Curation UX) — inserted **before Phase 31** per user (higher priority than the Deck Primer). Filter/search the publish-unpublish curation list by tags, title/name, creator/source + readability. Origin: admin dogfooding — the curation list has grown long and is hard to scan.

### Pending Todos

1 pending todo file from v1.4 audit (review at v1.5 scoping). See `.planning/todos/pending/`.

### Blockers/Concerns

- None at roadmap creation. Branch is `v1.5`.
- Phase 30 pre-implementation: live KB tag-distribution audit must precede relevance matching code. Not a blocker until Phase 30 planning begins.
- Phase 31 Gemini risk: 20+ section primer with Spellbook + EdhTop16 may hit 60-100KB; Gemini web UI caps at 30-60KB. Spike (PRM-01) must measure prompt size and gate accordingly.

## Deferred Items

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
**Resolved during v1.5** (were deferred at v1.4 close): ✅ 7 v1.4 VERIFICATION backfill + UAT labels (Phase 28 HSK-03) · ✅ P26/P24/dual-tree artifact hygiene (Phase 28 HSK-04) · ✅ Core XML-doc backfill + gate widen (Phase 29 HSK-01) · ✅ KB-12 codex backend (re-demoted to backlog, D-03) · ✅ `content.kb.enabled` proven live at Phase 30 UAT (now OFF by design).

**Open / carried forward:**

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| tech_debt | Gemini paste-limit workaround | DEFERRED to v1.6 (flag-gated `DECKFLOW_GEMINI_ENABLED`) | v1.5 scoping |
| tech_debt | SRP refactor: split `DeckController` + `CommandRunners` god-classes | BACKLOG — dedicated refactor phase, not mid-milestone (high regression risk) | 2026-06-08 Copilot SOLID review |
| tech_debt | SpellbookCombo ranking fields dropped by parser → PRM-08 priority ranking degraded | DEFERRED to v1.6 | v1.5 Phase 31 |
| ops | SEL-02 expert-pin fix (`a106c6a`) live-pin re-confirmation | PENDING next `content.kb.enabled` ON window (TDD-covered, CI green) | v1.5 close |
| ops | `content.kb.enabled` OFF — Content KB ships dark | BY DESIGN (operator re-enables when ready) | v1.5 close |
| housekeeping | 15 pre-v1.5 open artifacts (stale 999.6/v13 debug sessions, 9 May quick-task refs status=missing, 3 empty todos) | ACKNOWLEDGED — cross-milestone cruft, clean via `/gsd-cleanup` | v1.5 close 2026-06-10 |

## Session Continuity

Last session: 2026-06-09T17:30:00.000Z
Stopped at: Phase 31 COMPLETE + APPROVED; ROADMAP 31 [x]; STATE 25/25 / 6-of-6 phases. All v1.5 phases done. Tracking committed.
Resume: v1.5 milestone close — clear open debt first (secure-phase 30/31/32/33; flip content.kb.enabled prod flag; prod-smoke the primer) OR run /gsd-complete-milestone / open the v1.5→main PR. Nothing pushed to main; no deploy.

## Operator Next Steps

- Start the next milestone with /gsd-new-milestone
