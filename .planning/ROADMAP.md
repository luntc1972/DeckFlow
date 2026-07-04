# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- ✅ **v1.4 Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup** — Phases 16-27 + 21.1/21.2 (shipped 2026-06-03) — see `.planning/milestones/v1.4-ROADMAP.md`
- ✅ **v1.5 Deck Primer Generator + Content KB Integration + Housekeeping** — Phases 28-33 (shipped 2026-06-10) — see `.planning/milestones/v1.5-ROADMAP.md`
- ✅ **v1.6 Content KB Retrieval Fix + Value Re-Validation** — Phases 34-40 (shipped 2026-06-12) — see `.planning/milestones/v1.6-ROADMAP.md`
- ✅ **v1.7 Local Harvest & Publish Studio** — Phases 41-50 (shipped 2026-06-17) — see `.planning/milestones/v1.7-ROADMAP.md`
- ✅ **Cycle 8 — Hardening & Backlog Burn-down** — Phases 51-54 (shipped 2026-06-17, `2026.06.4`) — see `.planning/milestones/cycle8-ROADMAP.md`
- ✅ **Cycle 9 — Content Pipeline & Publish-Tracking** — Phases 55-58 (shipped 2026-06-19, `2026.06.5`) — see `.planning/milestones/cycle9-ROADMAP.md`
- ✅ **Cycle 10 — Studio Automation, Sync & Polish** — Phases 59-63 (shipped 2026-06-21, `2026.06.6`) — see `.planning/milestones/cycle10-ROADMAP.md`
- ✅ **Cycle 11 — Security, Visibility Control & Creator-Lens** — Phases 64-69 (shipped 2026-06-25, `2026.06.8`) — see `.planning/milestones/cycle11-ROADMAP.md`
- ✅ **Cycle 12 — Manabase Accuracy, Command-Zone Awareness & Cross-Tool Persistence** — Phases 70-74 + flag-key namespacing (shipped 2026-06-27, `2026.06.9`)
- ✅ **Cycle 13 — Deck Evaluation & Creator Output** — Phases 75-78 (shipped 2026-06-30, `2026.06.10`) — see `.planning/milestones/cycle13-ROADMAP.md`
- ✅ **Cycle 14 — Deeper Deck Evaluation** — Phases 79-81 (shipped 2026-07-03, `2026.07.1`) — see `.planning/milestones/cycle14-ROADMAP.md`
- 🔵 **Cycle 15 — Cleanup, Refactor & Visual Polish** — Phases 82-86 (in planning, target `2026.07.2`) — see Phase Details below

---

## Cycle 15 — Cleanup, Refactor & Visual Polish

**Goal:** Pay down accumulated tech-debt and finish deferred polish without changing public behavior — every paste artifact byte-identical, every theme render unchanged except where a requirement explicitly corrects a visual bug.

**Cross-cutting gate (every phase):** No net-new user-facing feature. Paste artifacts (ChatGPT/Claude/Gemini variants) stay byte-identical; theme render stays unchanged except the explicit THEME-02/UIAUDIT corrections. ADR-0001 (prompt-variant decoupling) and ADR-0002 (CalVer, named cycle) hold. Phase numbering continues from Cycle 14's 81 → **82**.

### Phases

- [ ] **Phase 82: Refactor-Review Sweep & UI Baseline Audit** - Sweep the largest/most-duplicated files for SRP/duplication debt beyond the pre-identified families; triage and execute confirmed targets; take the baseline 6-pillar UI audit to feed Phases 84/85.
- [ ] **Phase 83: Packet-Service SRP Split** - Extract shared prompt-assembly + Scryfall-resolution collaborators from the four packet-building god-services.
- [ ] **Phase 84: Theme Semantic-Token Migration** - Finish migrating `--accent-strong` onto `--link`/`--danger`/`--focus`/`--cta-border` across all 27 theme forks.
- [ ] **Phase 85: `chatgpt-*` Naming Cleanup** - Rename ~1545 `chatgpt-*` identifiers (CSS/TS/views) to AI-agnostic names with byte-identical render.
- [ ] **Phase 86: UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout** - Re-run the 6-pillar UI audit to ≥20/24, close the owed DirectPush Stage 4 verification, and add on/off sorting to `/Admin/Flags`.

### Phase Details

### Phase 82: Refactor-Review Sweep & UI Baseline Audit
**Goal**: Surface and resolve the highest-risk remaining SRP/duplication debt beyond the pre-identified PKTSVC/THEME/AICLEAN families, and take the baseline 6-pillar UI audit measurement, before the rest of the cycle's scope locks in — so both refactor findings and per-pillar UI gaps shape the later phases rather than being discovered after them.
**Depends on**: Nothing (first phase; operates on files disjoint from Phases 83-86, so it carries no sequencing dependency on them)
**Requirements**: REVIEW-01, REVIEW-02, UIAUDIT-01
**Success Criteria** (what must be TRUE):
  1. A code-review sweep has been run over the largest/most-duplicated files in the codebase (including `deck-sync.ts` 2877 LOC and `Harvest.razor.cs` 1222 LOC), and every surfaced candidate has a recorded triage decision (in-scope-this-cycle vs. backlog) with reasoning.
  2. Every sweep target triaged as in-scope-this-cycle has been refactored under the byte-identical/behavior-neutral gate, with tests proving no observable behavior change.
  3. Any sweep target exceeding the cycle's risk budget is explicitly deferred to backlog with a written reason — not silently dropped.
  4. Build stays clean (0 warnings/errors) and the full test suite passes after every executed sweep refactor.
  5. A baseline 6-pillar UI audit (`tasks/UI-REVIEW.md`) has been re-run and scored with a per-pillar breakdown, and the concrete gap to ≥20/24 is enumerated and handed to Phases 84/85 as scoped work (not left for Phase 86 to discover).
**Plans**: 3 plans (2 waves)
- [ ] 82-01-PLAN.md — Refactor-review sweep + per-candidate triage decision (REVIEW-01) [wave 1]
- [ ] 82-02-PLAN.md — Baseline 6-pillar UI audit + gap-to-20 handoff (UIAUDIT-01) [wave 1]
- [ ] 82-03-PLAN.md — Execute in-scope refactors (byte-identical gate) + record deferrals (REVIEW-02) [wave 2, depends 82-01]

### Phase 83: Packet-Service SRP Split
**Goal**: Split the four parallel packet-building god-services (`DeckAnalysisPacketService` 2372 LOC / `DeckComparisonService` 1033 / `MetaGapService` 956 / `DeckPrimerPacketService` 904) into orchestration shells over shared, independently-tested collaborators, without altering any paste artifact.
**Depends on**: Nothing structurally (backend/C# only, no CSS/theme surface); sequenced second because it is the largest single refactor in the cycle.
**Requirements**: PKTSVC-01, PKTSVC-02, PKTSVC-03, PKTSVC-04
**Success Criteria** (what must be TRUE):
  1. A single reusable prompt-assembly collaborator is shared across all four packet services, while each AI-variant's prompt prose remains hand-authored per ADR-0001 (no shared prompt-prose helper).
  2. A single reusable Scryfall reference-resolution collaborator is shared across all four packet services; no service retains a duplicate resolution code path.
  3. Each of the four packet services is reduced to an orchestration shell delegating to tested collaborators, with the collaborators covered by their own unit tests, and no service file materially larger than its collaborators.
  4. An automated byte-identical regression guard proves the analysis, comparison, meta-gap, and primer artifacts are unchanged pre/post refactor across all three AI variants (ChatGPT/Claude/Gemini), with the Cycle 12-14 analysis flags both ON and OFF.
**Plans**: TBD

### Phase 84: Theme Semantic-Token Migration
**Goal**: Finish migrating `--accent-strong` onto the correct semantic tokens across all 27 theme forks, fixing the error-reads-as-link bug in red guild themes without any other visual drift.
**Depends on**: Nothing structurally; sequenced third (before the UI audit re-score in Phase 86, which needs this fix landed to score against).
**Requirements**: THEME-01, THEME-02, THEME-03
**Success Criteria** (what must be TRUE):
  1. Every `--accent-strong` usage across all 27 theme files is reclassified onto `--link`/`--danger`/`--focus`/`--cta-border` by its actual role; new token additions live in each theme's own `:root`, and layout CSS stays in `site-common.css`.
  2. Error/danger text visibly renders in the danger color (not the link color) in red guild themes, verified live at both desktop and mobile viewports.
  3. A visual spot-check across all 27 themes confirms no non-error surface changed color — the only visible delta is the intended semantic correction.
**Plans**: TBD
**UI hint**: yes

### Phase 85: `chatgpt-*` Naming Cleanup
**Goal**: Rename the ~1545 `chatgpt-*` identifiers (1072 CSS class refs across 25 theme forks, 224 TS, 249 views) to AI-agnostic names, with byte-identical render.
**Depends on**: Phase 84 (sequenced after to avoid churning the same 25 theme fork files for two different reasons in parallel); not a hard technical dependency since this is a pure identifier rename with no semantic/color change.
**Requirements**: AICLEAN-01, AICLEAN-02, AICLEAN-03
**Success Criteria** (what must be TRUE):
  1. Every `chatgpt-*` CSS class name across the 25 theme forks + `site-common.css` + `site.css` is renamed to an AI-agnostic equivalent, and the rendered HTML/CSS output is byte-identical to before the rename.
  2. Every matching `chatgpt-*` TypeScript constant, `data-*` attribute, and Razor view reference is renamed in lockstep, with no dead or duplicated selector left behind.
  3. A grep across `css/`, `ts/`, and `Views/` returns zero `chatgpt-*` hits.
  4. The full Playwright e2e suite and page renders are unchanged after the rename.
**Plans**: TBD
**UI hint**: yes

### Phase 86: UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout
**Goal**: Fix the gaps enumerated by the Phase 82 baseline audit, run the final 6-pillar re-score against the corrected themes to clear ≥20/24, close out the owed DirectPush Stage 4 verification, and add on/off sorting to the `/Admin/Flags` list.
**Depends on**: Phase 82 (baseline audit findings), Phase 84 (the `--accent-strong` fix must land before the final re-score reflects it), Phase 85 (naming cleanup lands before the final re-score); last phase in the cycle.
**Requirements**: UIAUDIT-02, UIAUDIT-03, ADMIN-01
**Success Criteria** (what must be TRUE):
  1. Every gap enumerated by the Phase 82 baseline audit has a concrete fix applied.
  2. A final 6-pillar re-score (`tasks/UI-REVIEW.md`), run after the theme-token migration and naming cleanup have landed, confirms the site clears ≥20/24 with a per-pillar breakdown.
  3. DirectPush Stage 4 has been verified live at both desktop and mobile viewports.
  4. The DirectPush Stage 4 no-op success copy (`DirectPush.razor:441`) no longer claims a push that did not happen, phrasing matches the committed-variant wording, and the commit SHA renders short-form so it doesn't overflow on mobile.
  5. The `/Admin/Flags` list can be sorted by on/off (enabled) state, grouping enabled flags together; the sort is view-only and changes no flag key, default, or persisted semantics.
**Plans**: TBD
**UI hint**: yes

### Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 82. Refactor-Review Sweep & UI Baseline Audit | 0/3 | Not started | - |
| 83. Packet-Service SRP Split | 0/? | Not started | - |
| 84. Theme Semantic-Token Migration | 0/? | Not started | - |
| 85. `chatgpt-*` Naming Cleanup | 0/? | Not started | - |
| 86. UI Audit Re-Score, Studio Stage 4 & Admin Flags Closeout | 0/? | Not started | - |

---

## Carry-forward backlog (not in Cycle 15)

- Scheduled/bulk harvest (AUTO-03/04)
- SEO/growth lane (SEO-01..05)
- Matchup / meta-threat read (deferred — deepens cedh-meta-gap, a separate lane)
- Manabase engine refactor (CastabilitySimulator / ManabaseAnalyzer / ManabaseClassifier SRP split) — deferred out of Cycle 15: behavior-critical Monte-Carlo + Karsten scoring, no byte-identical gate, just heavily worked in Cycles 12/14. Needs a numeric-parity harness built FIRST. Candidate for a dedicated future refactor cycle.
- **KB "commander advice" content class for filtered videos** — the distill classifier filters out videos that lack actionable deckbuilding decisions (slot/cut/synergy on a real list), discarding them entirely. But many are still valuable *general commander advice*: meta/format philosophy, budget-building mindset, card evaluations. Give these a distinct KB content type/home instead of dropping them, so they can be surfaced (and pasted into ChatGPT) as advice rather than deckbuilding lessons. Needs: a second classifier verdict ("advice" vs "filtered"), its own artifact shape/prompt, and a browse surface. Observed 2026-07-04 re-distill filtered 3 such videos: `D5XXv7BzmZw` (The Midrange-ification of Commander — format meta essay), `GGoQxBP3DcE` (budget-deck pep talk / "Rock Lee of Commander"), `s_B1wCIWGR0` (Top 10 Lands for EDH — card eval + pricing).
