---
gsd_state_version: 1.0
milestone: v1.5
milestone_name: Deck Primer Generator + Content KB Integration + Housekeeping
status: executing
stopped_at: Phase 31 planned + Codex READY (6 plans/4 waves; 4-round convergence)
last_updated: "2026-06-08T19:11:56.478Z"
last_activity: 2026-06-08 -- Phase 32 closed + 8 dogfood UX fixes + 2 enhancements (prompt-size warn, set-upgrade follow-up); full regression green
progress:
  total_phases: 7
  completed_phases: 4
  total_plans: 23
  completed_plans: 17
  percent: 57
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-06-03 for v1.5 milestone start)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 30 — content-kb-integration

## Current Position

Phase: 32 (expert-context-selection) — COMPLETE (4/4 plans; SEL-01..06 done; visual-verify approved 2026-06-08)
Next: secure-phase 32 (open), or next milestone phase. Phases 30+32 both have open secure-phase debt.
Last activity: 2026-06-08 -- Phase 32 closed + 8 dogfood UX fixes + 2 enhancements (prompt-size warn, set-upgrade follow-up); full regression green

Progress: [██████████] 100%

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

### Pending Todos

1 pending todo file from v1.4 audit (review at v1.5 scoping). See `.planning/todos/pending/`.

### Blockers/Concerns

- None at roadmap creation. Branch is `v1.5`.
- Phase 30 pre-implementation: live KB tag-distribution audit must precede relevance matching code. Not a blocker until Phase 30 planning begins.
- Phase 31 Gemini risk: 20+ section primer with Spellbook + EdhTop16 may hit 60-100KB; Gemini web UI caps at 30-60KB. Spike (PRM-01) must measure prompt size and gate accordingly.

## Deferred Items

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| tech_debt | Gemini paste-limit workaround | DEFERRED to v1.6 (third consecutive deferral — stays flag-gated via `DECKFLOW_GEMINI_ENABLED`) | v1.5 scoping 2026-06-03 |
| verification_gap | 7 v1.4 phases missing VERIFICATION.md files | v1.5 Phase 28 scope | v1.4 close 2026-06-03 |
| uat_gap | Stale UAT labels (human_needed / partial / unknown) in v1.4 phases | v1.5 Phase 28 scope | v1.4 close 2026-06-03 |
| artifact_hygiene | P26 missing SUMMARYs, P24 quick-fix artifact chain, dual artifact-tree drift | v1.5 Phase 28 scope | v1.4 close 2026-06-03 |
| tech_debt | KB-12 codex distill backend (NotSupportedException stub) | v1.5 Phase 28 scope (promoted from backlog) | v1.4 close 2026-06-03 |
| ops | `content.kb.enabled` prod flag still OFF | v1.5 Phase 30 prerequisite step | v1.4 close 2026-06-03 |
| tech_debt | DeckFlow.Core 186 undocumented XML-doc sites | v1.5 Phase 29 scope | v1.4 Phase 23 close |

## Session Continuity

Last session: 2026-06-08T19:11:56.448Z
Stopped at: Phase 31 planned + Codex READY (6 plans/4 waves; 4-round convergence)
Resume file: .planning/phases/31-deck-primer-generator/31-01-PLAN.md
