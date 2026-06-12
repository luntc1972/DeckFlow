---
gsd_state_version: 1.0
milestone: v1.6
milestone_name: Content KB Retrieval Fix + Value Re-Validation
status: executing
stopped_at: ROADMAP.md + STATE.md written; REQUIREMENTS.md traceability updated.
last_updated: "2026-06-12T16:23:17.134Z"
last_activity: 2026-06-12
progress:
  total_phases: 7
  completed_phases: 5
  total_plans: 16
  completed_plans: 10
  percent: 63
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-06-10 after v1.5 milestone)

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 38 — controller-srp-split

## Current Position

Phase: 38 (controller-srp-split) — EXECUTING
Plan: 1 of 6
Status: Executing Phase 38
Reqs: SRP-01/02/03 pending.
Next: `/gsd-plan-phase 38` (Codex-reviewed) → execute (Codex impl / Claude review).

FOLLOW-ONS (operator, not dev blockers):

- Prod KB go-live: deploy new content-kb/ artifacts + index-seed.json → prod /Admin publish 7 survivors (from Phase 37.5).
- Phase 37.5 prod seed + publish still OPEN.

## Session 2026-06-12 — UI quick-fixes + E2E (all shipped to origin/v1.6)

Codex-impl / Claude-review across the board: primer copy buttons (29cf22e), primer result-scroll (50b3b16), mobile busy-overlay bfcache lockup (0993da0), /about mobile overflow (0d99f2d), admin nav toggle names section (9e771ad), admin KB per-entry delete + Phase-37 confirm/filter regression restore (e3b6ea4), Playwright E2E smoke suite 68/68 + CI (6e8ddff). See `.planning/quick/260611-p9x`, `260612-kb1`, `260612-e2e`.
Last activity: 2026-06-12

## Accumulated Context — Key Decisions (v1.6)

- **D-v1.6-gate (2026-06-10):** Content KB clip-injection re-validation = **MARGINAL**. On the Phase-34-fixed retriever across 5 bracket-spanning decks, lift was cosmetic (soft dims only; 0/5 decks changed a cut/add; 3/5 mild noise). Binding constraint = the corpus (single-creator generic philosophy, `[00:00]` intro-only clips, no deck/card-specific content) — retrieval polish can't reach it. **Decision: skip Phase 36 (no philosophy-profile, no un-dark); RETIRE the whole-channel clip-injection feature; KB stays dark.** Evidence: `.planning/phases/35-value-re-validation-gate/35-GATE-VERDICT.md`.
- **Retire = DONE in Phase 37** (2026-06-10, RET-01/02/05, verified). Clip-injection + expert-selection paths removed. KB rehabilitated to browse-only + un-darked. (This note previously read "not yet done / Phase 37 = SRP" — stale; corrected 2026-06-12.) The remaining planned phase is **Phase 38 (Controller SRP Split)**.

## Performance Metrics

**Velocity (v1.5 reference — most recent shipped):**

- 25 plans across 6 phases (2026-06-03 → 2026-06-09, 7 days)
- Cross-AI execution pattern sustained: Codex codes, Claude reviews
- Final test gate: Core 282/282, Web 657 pass / 5 PG-skips

**v1.6 Phase Forecast:**

| Phase | Req-IDs | Gate | Notes |
|-------|---------|------|-------|
| 34 — KB Retrieval Fix | KBR-01..04 | Unconditional | Algorithmic fix to ContentKbRelevanceService; prompt-injection mitigation; regression tests |
| 35 — Value Re-Validation Gate | KBV-01..04 | Unconditional | Blind A/B across 3+ decks; binary VALIDATED/MARGINAL verdict; routes Phase 36 |
| 36 — Creator Philosophy-Profile + KB Un-Dark | PHIL-01..04, KBD-01..02 | **CONDITIONAL on Phase 35 = VALIDATED** | Skipped entirely if gate = MARGINAL |
| 37 — Controller SRP Split | SRP-01..03 | Unconditional, independent | Runs regardless of gate outcome |

## Accumulated Context

### Decisions

- **Gate-driven milestone structure:** Phase 35 is a binary branch point, not a checklist item. VALIDATED → Phase 36 proceeds. MARGINAL → Phase 36 is skipped; pivot decision (fix-again / per-deck pivot / retire) is recorded in VERDICT.md; milestone closes after Phase 37.
- **Phase 36 is conditional and must not begin before Phase 35 = VALIDATED.** Building the philosophy-profile on an unvalidated retriever is the highest-risk anti-feature identified by Spike 001 research.
- **Phase 37 is fully independent.** It can run after any gate outcome. Sequenced last to isolate the high-touch DeckController extraction from KB gate work.
- **Zero new dependencies.** All v1.6 work is deliverable within the existing package set. `OpenAI 2.10.0` already in `DeckFlow.Core.csproj`. Any dependency addition is a scope violation.
- **Prompt-injection mitigation (KBR-03) must land in Phase 34** — before `content.kb.enabled` is flipped ON. KBD-01 explicitly requires injection mitigation to be live before un-dark.
- **Blind protocol for Phase 35:** score baseline AI answers first, record scores, then score with-context. Gate-pass criteria: ≥3/4 rubric dimensions score 3+ for majority of decks; no quality loss vs. baseline; at least one dimension 4+; at least 2 distinct video sources for the Atraxa deck.
- **Phase 36 provenance first:** `ICreatorPhilosophyProfileStore` with non-nullable `source_video_id` + `source_timestamp_s` schema is the first deliverable inside Phase 36. No synthesis before provenance schema is in place.
- **SRP split discipline:** two-commit discipline on CommandRunners (shared helpers extracted first, then class split, build + test green after each commit). All moved DeckController actions must carry explicit `[Route]` attributes — conventional routing would silently reroute URLs.
- **Topical-scoring algorithm constants:** threshold constants (commander-name penalty multiplier, relevance floor) should be named constants with rationale, not magic numbers — specified at plan time, not left to implementer judgment.

### Roadmap Evolution

- v1.6 roadmap created 2026-06-10 (4 phases, 18/18 REQ-IDs mapped, gate-driven branching at Phase 35).
- Phase 36 merges PHIL-* and KBD-* into one conditional phase — cleaner than two adjacent conditional phases; both have the same gate dependency.
- Phase 37.6 inserted after Phase 37.5: Harvest Video Block + Hard-Delete (VBLK-01..04): admin block-by-id + hard-delete; harvester skip-check prevents re-ingest

### Pending Todos

- Spike001KbValueAbHarness.cs (untracked in repo root `DeckFlow.Web.Tests/`) — delete after Phase 35 verdict is recorded (Phase 35 extends and runs it; file becomes test artifact, not throwaway spike).
- 15 pre-v1.5 open artifacts (stale 999.6/v13 debug sessions, May quick-task refs, empty todos) — acknowledged cross-milestone cruft; clean via `/gsd-cleanup` when convenient.

### Blockers/Concerns

- None at roadmap creation.
- Phase 35 pre-run: corpus feasibility check needed before Phase 36 planning — confirm via `content_videos` query how many substantive (non-rating-series, non-excluded) videos exist per creator. If no creator meets the ~10-video threshold, philosophy-profile scope must be revised at plan time.
- Phase 36 RAG algorithm: research recommends keyword overlap (Option 1) as the v1.6 baseline for principle relevance scoring at query time; LLM re-ranking (Option 2) is a follow-on. Planner must make this explicit in the Phase 36 plan so Codex does not default to the more expensive path.

### Quick Tasks Completed

| # | Description | Date | Commit | Directory |
|---|-------------|------|--------|-----------|
| 260611-p9x | Fix Primer copy buttons (port data-copy-target wiring into primer-selection.ts + Vitest test) | 2026-06-12 | 29cf22e | [260611-p9x-fix-primer-copy-buttons-port-data-copy-t](./quick/260611-p9x-fix-primer-copy-buttons-port-data-copy-t/) |
| 260612-kb1 | Admin KB per-entry Delete + restore Phase-37-broken page behaviors (filter/confirms/toast) | 2026-06-12 | e3b6ea4 | [260612-kb1-admin-kb-delete-entry](./quick/260612-kb1-admin-kb-delete-entry/) |
| 260612-e2e | Playwright E2E smoke suite (15 routes × desktop+mobile) + CI stage; 68/68 green | 2026-06-12 | 6e8ddff | [260612-e2e-playwright-smoke](./quick/260612-e2e-playwright-smoke/) |

## Deferred Items

**Resolved in v1.5:**

- ✅ 7 v1.4 VERIFICATION backfill + UAT labels (Phase 28 HSK-03)
- ✅ P26/P24/dual-tree artifact hygiene (Phase 28 HSK-04)
- ✅ Core XML-doc backfill + gate widen (Phase 29 HSK-01)
- ✅ KB-12 codex backend (re-demoted to backlog, D-03)
- ✅ `content.kb.enabled` proven live at Phase 30 UAT (now OFF by design — v1.6 KBD-01 flips it ON after gate)
- ✅ Expert-pin injection bug fixed + TDD-covered (`a106c6a`); CI green

**Open / carried forward:**

| Category | Item | Status | Deferred At |
|----------|------|--------|-------------|
| tech_debt | Gemini paste-limit workaround | DEFERRED (flag-gated `DECKFLOW_GEMINI_ENABLED`) | v1.5 scoping |
| tech_debt | SpellbookCombo ranking fields (PRM-08) | DEFERRED to v1.7+ | v1.5 Phase 31 |
| ops | SEL-02 expert-pin live-pin re-confirm | IN SCOPE v1.6 as KBD-02 (conditional on gate pass) | v1.5 close |
| ops | `content.kb.enabled` OFF — Content KB ships dark | IN SCOPE v1.6 as KBD-01 (conditional on gate pass) | v1.5 close |
| housekeeping | 15 pre-v1.5 open artifacts | ACKNOWLEDGED — clean via `/gsd-cleanup` | v1.5 close 2026-06-10 |

## Session Continuity

Last session: 2026-06-10 — v1.6 roadmap created (gsd-roadmapper).
Stopped at: ROADMAP.md + STATE.md written; REQUIREMENTS.md traceability updated.
Resume: Start Phase 34 with `/gsd:plan-phase 34`.
