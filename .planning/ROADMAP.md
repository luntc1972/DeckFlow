# Roadmap: DeckFlow

## Milestones

- ✅ **2026.07.2 Cycle 15 — Cleanup, Refactor & Visual Polish** — Phases 82–87 (shipped 2026-07-05) → see .planning/milestones/2026.07.2-ROADMAP.md
- ✅ **Cycle 14 — Deeper Deck Evaluation** — Phases 79-81 (shipped 2026-07-03, `2026.07.1`) — see `.planning/milestones/cycle14-ROADMAP.md`
- ✅ **Cycle 13 — Deck Evaluation & Creator Output** — Phases 75-78 (shipped 2026-06-30, `2026.06.10`) — see `.planning/milestones/cycle13-ROADMAP.md`
- ✅ **Cycle 12 — Manabase Accuracy, Command-Zone Awareness & Cross-Tool Persistence** — Phases 70-74 + flag-key namespacing (shipped 2026-06-27, `2026.06.9`)
- ✅ **Cycle 11 — Security, Visibility Control & Creator-Lens** — Phases 64-69 (shipped 2026-06-25, `2026.06.8`) — see `.planning/milestones/cycle11-ROADMAP.md`
- ✅ **Cycle 10 — Studio Automation, Sync & Polish** — Phases 59-63 (shipped 2026-06-21, `2026.06.6`) — see `.planning/milestones/cycle10-ROADMAP.md`
- ✅ **Cycle 9 — Content Pipeline & Publish-Tracking** — Phases 55-58 (shipped 2026-06-19, `2026.06.5`) — see `.planning/milestones/cycle9-ROADMAP.md`
- ✅ **Cycle 8 — Hardening & Backlog Burn-down** — Phases 51-54 (shipped 2026-06-17, `2026.06.4`) — see `.planning/milestones/cycle8-ROADMAP.md`
- ✅ **v1.7 Local Harvest & Publish Studio** — Phases 41-50 (shipped 2026-06-17) — see `.planning/milestones/v1.7-ROADMAP.md`
- ✅ **v1.6 Content KB Retrieval Fix + Value Re-Validation** — Phases 34-40 (shipped 2026-06-12) — see `.planning/milestones/v1.6-ROADMAP.md`
- ✅ **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** — Phases 28-33 (shipped 2026-06-10) — see `.planning/milestones/v1.5-ROADMAP.md`
- ✅ **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) — see `.planning/milestones/v1.4-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`

## Phases

<details>
<summary>✅ 2026.07.2 Cycle 15 (Phases 82–87) — SHIPPED 2026-07-05</summary>

- [x] Phase 82 — Refactor-Review Sweep & UI Baseline Audit (completed 2026-07-04)
- [x] Phase 83 — Packet-Service SRP Split (completed 2026-07-04)
- [x] Phase 84 — Theme Semantic-Token Migration (completed 2026-07-05)
- [x] Phase 85 — `chatgpt-*` Naming Cleanup (completed 2026-07-05)
- [x] Phase 86 — UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout (completed 2026-07-05)
- [x] Phase 87 — Creator-Source Model Hardening (completed 2026-07-05)

</details>

### 📋 Next milestone (not yet planned)

Cycle 16 (Creator-Style) and Cycle 17 (KB-Sync Hardening) are pre-planned on separate branches; run /gsd-new-milestone or /gsd-review-backlog to start the next active cycle. ADMIN-01 (/Admin/Flags on/off sorting) is descoped to backlog.

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 82. Refactor-Review Sweep & UI Baseline Audit | 2026.07.2 | 3/3 | Complete | 2026-07-04 |
| 83. Packet-Service SRP Split | 2026.07.2 | 7/7 | Complete | 2026-07-04 |
| 84. Theme Semantic-Token Migration | 2026.07.2 | 2/2 | Complete | 2026-07-05 |
| 85. `chatgpt-*` Naming Cleanup | 2026.07.2 | 5/5 | Complete | 2026-07-05 |
| 86. UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout | 2026.07.2 | 5/5 | Complete | 2026-07-05 |
| 87. Creator-Source Model Hardening | 2026.07.2 | 1/1 | Complete | 2026-07-05 |

---

## Carry-forward backlog (not in Cycle 15)

- Scheduled/bulk harvest (AUTO-03/04)
- SEO/growth lane (SEO-01..05)
- Matchup / meta-threat read (deferred — deepens cedh-meta-gap, a separate lane)
- **ADMIN-01** — `/Admin/Flags` sortable by on/off (enabled) state (descoped from Cycle 15, user decision 2026-07-05; view-only, no flag semantics change)
- Manabase engine refactor (CastabilitySimulator / ManabaseAnalyzer / ManabaseClassifier SRP split) — deferred out of Cycle 15: behavior-critical Monte-Carlo + Karsten scoring, no byte-identical gate, just heavily worked in Cycles 12/14. Needs a numeric-parity harness built FIRST. Candidate for a dedicated future refactor cycle.
- **KB "commander advice" content class for filtered videos** — the distill classifier filters out videos that lack actionable deckbuilding decisions (slot/cut/synergy on a real list), discarding them entirely. But many are still valuable *general commander advice*: meta/format philosophy, budget-building mindset, card evaluations. Give these a distinct KB content type/home instead of dropping them, so they can be surfaced (and pasted into ChatGPT) as advice rather than deckbuilding lessons. Needs: a second classifier verdict ("advice" vs "filtered"), its own artifact shape/prompt, and a browse surface. Observed 2026-07-04 re-distill filtered 3 such videos: `D5XXv7BzmZw` (The Midrange-ification of Commander — format meta essay), `GGoQxBP3DcE` (budget-deck pep talk / "Rock Lee of Commander"), `s_B1wCIWGR0` (Top 10 Lands for EDH — card eval + pricing).
