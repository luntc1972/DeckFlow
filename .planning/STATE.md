---
gsd_state_version: 1.0
milestone: v1.5
milestone_name: Deck Primer Generator + Content KB Integration + Housekeeping
status: milestone-complete-pending-close
stopped_at: Phase 33 closed (visual-verify passed); Phase 31 planned + Codex READY (6 plans/4 waves) is the only remaining v1.5 phase
last_updated: "2026-06-09T17:30:00.000Z"
last_activity: 2026-06-09 -- Phase 31 COMPLETE: 31-06 human visual-verify APPROVED (desktop+mobile); ROADMAP 31 [x]; 3 verify-found bugs fixed (9fd1c65/779affe/abbeedd); Web.Tests 654/0/5. All 7 v1.5 phases done.
progress:
  total_phases: 6
  completed_phases: 6
  total_plans: 25
  completed_plans: 25
  percent: 100
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-06-03 for v1.5 milestone start)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 30 — content-kb-integration

## Current Position

Phase: 31 (deck-primer-generator) — COMPLETE 2026-06-09. ALL 6 v1.5 phases (28/29/30/32/33/31) now [x]. v1.5 milestone functionally complete, pending close (/gsd-complete-milestone or PR).
Phase 31 recap (all Codex impl / Claude review): 31-01 spike (combo=sufficient→priority-rank; Gemini cap=32000; EdhTop16=meta-query-available); 31-02 foundation models; 31-03 DeckPrimerPacketService (D-2 combos+null disclosure, PRM-07 category omit, PRM-06 bracket-5 via EdhTop16Client.GetTopArchetypesAsync meta-wide query, build-once/render-each-enabled gate, cache key; review-fix abfffab); 31-04 three ADR-0001 decoupled variants (ChatGPT/Claude/Gemini) + DI; 31-05 zip round-trip (PacketArtifactStore.BuildPrimerZip/LoadPrimerFromZip/SuggestPrimerZipFileName); 31-06 controller/tab/page/primer-selection.ts/site-common.css + human visual-verify APPROVED. Verify caught+fixed 3 bugs: 9fd1c65 (data-preset-ids @Html.Raw attr break), 779affe (category graceful-degrade + test), abbeedd (DeckControllerTests ctor — interface-change/test-project trap). Browser UAT (desktop+mobile): tab, D-3 per-bracket presets, gating, localStorage persistence, badges, end-to-end build (13.2KB primer, live Spellbook+EdhTop16), zip download (22,533B application/zip). Full DeckFlow.Web.Tests 654/0/5; Core 281/281.
Open debt (carry to v1.5 close / v1.6): secure-phase 30, 31, 32, 33 (all open); content.kb.enabled prod flag still OFF; primer not prod-smoked; SpellbookCombo PieceCount/ManaValueNeeded/Popularity capture deferred (31-03 ranks on CardNames.Count + Results text only — documented follow-up). (Distill-sanitizer fix + content-kb/ data committed 4015634, 2a6ea8c.)
Last activity: 2026-06-09 -- Phase 31 visual-verify APPROVED; ROADMAP 31 [x]; v1.5 all phases complete

Progress: [██████████] 100% (all v1.5 phases complete)

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

Last session: 2026-06-09T17:30:00.000Z
Stopped at: Phase 31 COMPLETE + APPROVED; ROADMAP 31 [x]; STATE 25/25 / 6-of-6 phases. All v1.5 phases done. Tracking committed.
Resume: v1.5 milestone close — clear open debt first (secure-phase 30/31/32/33; flip content.kb.enabled prod flag; prod-smoke the primer) OR run /gsd-complete-milestone / open the v1.5→main PR. Nothing pushed to main; no deploy.
