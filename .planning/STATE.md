---
gsd_state_version: 1.0
milestone: v1.5
milestone_name: Deck Primer Generator + Content KB Integration + Housekeeping
status: executing
stopped_at: Phase 33 closed (visual-verify passed); Phase 31 planned + Codex READY (6 plans/4 waves) is the only remaining v1.5 phase
last_updated: "2026-06-09T16:15:00.000Z"
last_activity: 2026-06-09 -- Phase 31-03 (service) + 31-04 (3 variants + DI) DONE (Codex impl/Claude review); full Web.Tests 648/0/5; Gemini cap=32000 per spike; NEXT 31-05/06
progress:
  total_phases: 7
  completed_phases: 5
  total_plans: 25
  completed_plans: 23
  percent: 92
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-06-03 for v1.5 milestone start)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 30 — content-kb-integration

## Current Position

Phase: 33 (admin-content-kb-curation-ux) — COMPLETE (2/2 plans; KBUX-01/02; browser visual-verify passed desktop+mobile 2026-06-09; source-wrap nit fixed 16fac7e)
In progress: Phase 31 (deck-primer-generator) — 31-01 SPIKE + 31-02 models + 31-03 service + 31-04 variants all DONE 2026-06-09 (Codex impl/Claude review). 31-03: DeckPrimerPacketService (D-2 two-block combos + null disclosure, PRM-07 category omit-on-empty, PRM-06 bracket-5 via EdhTop16Client.GetTopArchetypesAsync meta-wide query, priority-rank combo branch, build-once/render-each-enabled platform gate, cache key w/ GeminiEnabled; IPrimerPromptVariant + registry; review-fix abfffab abstract interface member). 31-04: 3 decoupled variants (ChatGPT/Claude/Gemini, ADR 0001) + Program.cs DI (3 variants + registry + AddScoped factory); Gemini DefensivePromptCharCap=32000 per spike. Full DeckFlow.Web.Tests 648 pass/0 fail/5 PG-skip. NEXT: 31-05 (zip round-trip, PRM-09) wave 3 + 31-06 (controller/view/TS, PRM-02/03/04/10/11/12, autonomous:false → human checkpoint) wave 4.
Open debt: secure-phase 30, 32, 33 (all open); content.kb.enabled prod flag still OFF. (Distill-sanitizer fix + content-kb/ data are committed — 4015634, 2a6ea8c — not orphaned; earlier "uncommitted" note was a stale session-start snapshot.)
Last activity: 2026-06-09 -- Phase 33 browser visual-verify PASSED; source word-break nit fixed (16fac7e); ROADMAP/STATE flipped

Progress: [██████████] 100% (Phase 33)

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
| tech_debt | Gemini paste-limit workaround | DEFERRED to v1.6 (third consecutive deferral — stays flag-gated via `DECKFLOW_GEMINI_ENABLED`) | v1.5 scoping 2026-06-03 |
| verification_gap | 7 v1.4 phases missing VERIFICATION.md files | v1.5 Phase 28 scope | v1.4 close 2026-06-03 |
| uat_gap | Stale UAT labels (human_needed / partial / unknown) in v1.4 phases | v1.5 Phase 28 scope | v1.4 close 2026-06-03 |
| artifact_hygiene | P26 missing SUMMARYs, P24 quick-fix artifact chain, dual artifact-tree drift | v1.5 Phase 28 scope | v1.4 close 2026-06-03 |
| tech_debt | KB-12 codex distill backend (NotSupportedException stub) | v1.5 Phase 28 scope (promoted from backlog) | v1.4 close 2026-06-03 |
| ops | `content.kb.enabled` prod flag still OFF | v1.5 Phase 30 prerequisite step | v1.4 close 2026-06-03 |
| tech_debt | DeckFlow.Core 186 undocumented XML-doc sites | v1.5 Phase 29 scope | v1.4 Phase 23 close |
| tech_debt | SRP refactor: split `DeckController` (1592 lines / 11 deps) + `CommandRunners` (1893-line static god class) into feature-scoped handlers | BACKLOG — dedicated refactor phase, NOT mid-milestone (Copilot review 2026-06-08; #1/#4/#2). Working shipped code; high regression risk. Program.cs manual-wiring (#3) and codex-throw factory (#5) reviewed + dismissed as by-design | 2026-06-08 (Copilot SOLID review) |

## Session Continuity

Last session: 2026-06-09T16:15:00.000Z
Stopped at: Phase 31-03 (service) + 31-04 (variants + DI) DONE + reviewed; full Web.Tests 648/0/5 green; tracking committed. 23/25 plans.
Resume file: .planning/phases/31-deck-primer-generator/31-05-PLAN.md (zip round-trip) — then 31-06 (controller/view/TS, human checkpoint)
