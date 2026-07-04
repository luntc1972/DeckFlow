---
gsd_state_version: 1.0
milestone: Cycle 15
milestone_name: Cleanup, Refactor & Visual Polish
status: planning
last_updated: "2026-07-04T13:30:00.000Z"
last_activity: 2026-07-04
progress:
  total_phases: 5
  completed_phases: 0
  total_plans: 0
  completed_plans: 0
  percent: 0
---

# Project State

## Project Reference

See: .planning/PROJECT.md

**Core value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip — without the user reformatting anything.
**Current focus:** Phase 82 — Refactor-Review Sweep (Cycle 15 roadmap just created; not yet planned).

**Cycle 14 close (2026-07-03):** Phases 79-81 (Interaction & Answers Audit / Win-Condition & Combo Map / Opening-Hand Mulligan Evaluator) squash-merged to `main` (`701ec2fa`), CI green (`28694830980`), milestone audit PASSED 13/13 requirements + 5/5 integration flows. All three flags (`analysis.interaction-audit`, `analysis.wincon-map`, `analysis.mulligan-eval`) seeded OFF. Full detail archived at `.planning/milestones/cycle14-ROADMAP.md`. Operator prod-deploy + flag-flip debt for Cycles 12-14 confirmed **DONE** 2026-07-04 (see PROJECT.md Current State).

## Current Position

Phase: 82 — Refactor-Review Sweep
Plan: — (not yet planned)
Status: Roadmap created, awaiting `/gsd-plan-phase 82`
Last activity: 2026-07-04 — Cycle 15 roadmap created (Phases 82-86, 15/15 requirements mapped)

## Roadmap Summary

| # | Phase | Requirements | Status |
|---|-------|-------------|--------|
| 82 | Refactor-Review Sweep | REVIEW-01, REVIEW-02 | Not started |
| 83 | Packet-Service SRP Split | PKTSVC-01, PKTSVC-02, PKTSVC-03, PKTSVC-04 | Not started |
| 84 | Theme Semantic-Token Migration | THEME-01, THEME-02, THEME-03 | Not started |
| 85 | `chatgpt-*` Naming Cleanup | AICLEAN-01, AICLEAN-02, AICLEAN-03 | Not started |
| 86 | UI Audit Re-Score & Studio Stage 4 Closeout | UIAUDIT-01, UIAUDIT-02, UIAUDIT-03 | Not started |

**Phase ordering rationale:**

- **82 first**: the review sweep's triage findings can surface additional risk/candidates before the rest of the cycle's scope locks in; it operates on files (`deck-sync.ts`, `Harvest.razor.cs`) disjoint from Phases 83-86, so it carries no sequencing dependency on them.
- **83 second**: the packet-service SRP split is the largest single refactor in the cycle (4 god-services, ~5265 combined LOC) and is purely backend/C# — no CSS/theme surface, so it proceeds independently of 84/85.
- **84 third**: the `--accent-strong` semantic-token migration fixes the exact color-pillar bug the UI audit (86) will re-score against — must land before the re-score to be reflected in the new score.
- **85 fourth**: the `chatgpt-*` rename touches the same 25 theme fork files as 84 (sequenced after to avoid same-file churn for two unrelated reasons in parallel) but is a pure identifier rename with no semantic/color change, so it does not need to precede the audit re-score.
- **86 last**: re-scoring the UI audit only makes sense once the color-token fix (84) has landed; also closes the owed DirectPush Stage 4 verification.

## Performance Metrics

**Velocity (Cycle 14 reference — most recent shipped):**

- Phases 79-81; build 0/0; Core 1053 pass, Web 1158 (12 PG-skip)
- Claude (Opus 4.8) implements + reviews code; Codex (gpt-5.4 medium) reviews plans + code (delegation rule per CLAUDE.md)

## Accumulated Context

### Resolved Decisions (Cycle 15 — do NOT re-open)

- **Granularity = coarse:** 5 phases — one per requirement family (review sweep, packet-service split, theme migration, naming cleanup, audit re-score); derived from the 15 REQ-IDs, not padded.
- **Zero net-new features / byte-identical gate:** every phase's success criteria includes a behavior-neutral or byte-identical proof (paste artifacts unchanged; theme render unchanged except THEME-02/UIAUDIT's explicit visual corrections). ADR-0001 (prompt-variant decoupling — no shared prompt-prose helper) holds through the PKTSVC split.
- **Manabase engine untouched:** `CastabilitySimulator`/`ManabaseAnalyzer`/`ManabaseClassifier` are explicitly out of scope this cycle (deferred backlog item; needs a numeric-parity harness first).
- **DeckController split already done:** verified 2026-07-04; not reopened this cycle.

### Key Pitfalls to Watch (every phase)

- **Byte-identical / behavior-neutral proof required per phase** — refactor phases (82, 83) need an automated before/after comparison (paste artifacts across ChatGPT/Claude/Gemini, flag ON/OFF where applicable); CSS phases (84, 85) need a visual/render diff proving only the intended pixels changed.
- **ADR-0001 must survive PKTSVC-01** — the shared prompt-assembly collaborator must not become a shared prompt-*prose* helper; per-AI-variant text stays hand-authored and duplicated by design.
- **Theme files are full forks** — THEME and AICLEAN both touch all ~25-27 theme CSS files; layout CSS stays in `site-common.css`, new tokens go in each theme's own `:root`. Never fork layout into `site.css`.
- **AICLEAN blast radius (~1545 refs)** — treat as a large mechanical rename with real regression risk (25 theme forks + TS + views); grep-clean verification (AICLEAN-03) plus a full Playwright e2e run before considering it done.
- **UI audit re-score depends on THEME landing first** — sequencing violation risk if Phase 86 is planned/executed before Phase 84 ships.

### Pending Todos

- Run `/gsd-plan-phase 82` to begin Cycle 15 execution.

### Blockers/Concerns

- None yet — roadmap just created, no phase planning started.

### Carry-Forward (still open from prior cycles)

| Item | Status |
|------|--------|
| `deckflow_admin` credential deletion (password rotated) | Operator task |
| Full dual-dialect branch collapse (PG DDL parity prereq) | Backlog |
| SEO/growth lane (SEO-01..05) | Deferred |
| Scheduled/bulk harvest (AUTO-03/04) | Deferred |
| Matchup / meta-threat read (deepens cedh-meta-gap) | Deferred (separate lane) |
| Manabase engine refactor (needs numeric-parity harness first) | Deferred (own future cycle) |

## Session Continuity

Last session: 2026-07-04
Stopped at: Cycle 15 roadmap created (Phases 82-86, 15/15 requirements mapped, coverage validated).
Resume: `/gsd-plan-phase 82` to plan the Refactor-Review Sweep.

## Deferred Items

Carried from Cycle 14 close (2026-07-03) — none are Cycle-15 gaps, all pre-existing:

| Category | Item | Status |
|----------|------|--------|
| debug | calibration-measurement | unknown |
| debug | health-band-before-baseline | unknown |
| debug | health-band-flag-on-measurement | unknown |
| debug | health-band-headline-floor-spec | unknown |
| debug | moxfield-bridge-busy-stuck | root-cause-found |
| debug | pull-from-prod-sftp-decouple | unknown |
| quick_task | manabase-load-step | missing |
| quick_task | 260624-kpg-fix-dfc-transform-cards-excluded-from-se | missing |
| quick_task | 260624-opb-be-able-to-download-the-manabase-analysi | missing |
| quick_task | 260627-flag-key-namespacing | missing |
| quick_task | 260627-p55-deckflow-studio-safety-wins-loopback-bin | missing |
| quick_task | 260627-qyc-deckflow-studio-directpush-prod-write-in | missing |

## Operator Next Steps

- None blocking Cycle 15 start — Cycle 12-14 prod deploy + flag flips confirmed DONE 2026-07-04.
- Review the Cycle 15 roadmap and approve, or provide feedback for revision.
