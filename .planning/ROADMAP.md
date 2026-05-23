# Roadmap: DeckFlow

## Milestones

- ✅ **v1.0 Polish & Quality** — Phases 1-5 (shipped 2026-05-02) — see `.planning/milestones/v1.0-ROADMAP.md`
- ✅ **v1.1 Admin Console** — Phases 6-8 (shipped 2026-05-08)
- ✅ **v1.2 Multi-AI Prompts** — Phases 9-10 (shipped 2026-05-13) — see `.planning/milestones/v1.2-ROADMAP.md`
- ✅ **v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene** — Phases 11-15 + 999.1-999.8 (shipped 2026-05-23) — see `.planning/milestones/v1.3-ROADMAP.md`
- 📋 **v1.4** — TBD (start via `/gsd-new-milestone`)

## Phases

<details>
<summary>✅ v1.0 Polish & Quality (Phases 1-5) — SHIPPED 2026-05-02</summary>

- [x] Phase 1: Visual System Tokens — 3/3 plans (UI-VS-01..04)
- [x] Phase 2: Layout, Hierarchy & UX Copy — 3/3 plans (UI-LH-01..02, UX-01..03)
- [x] Phase 3: Tech-Debt Cleanup — 4/4 plans (TD-01..04)
- [~] Phase 4: Security & Bug Fixes — 4/4 plans, ABANDONED 2026-05-02 (rerouted to Phase 5)
- [x] Phase 5: Security & Bug Fixes v2 — 3/3 plans (BUG-01, BUG-02, TD-04 patch + integration test)

Verification: 27/27 must-haves passed. 15/15 v1 requirements shipped.
Full archive: `.planning/milestones/v1.0-ROADMAP.md`

</details>

<details>
<summary>✅ v1.1 Admin Console (Phases 6-8) — SHIPPED 2026-05-08</summary>

- [x] Phase 6: Admin Shell + Flags Foundation — 7/7 plans (ADMIN-01..05, FLAG-01..05)
- [x] Phase 7: Harvest Controls + Stats — 7/7 plans (HARV-01..07)
- [x] Phase 7.1: Categories Flag + SameOrigin AJAX Fix — 2/2 plans (inserted hotfix)
- [x] Phase 8: Analytics — 5/5 plans (ANL-01..05)

</details>

<details>
<summary>✅ v1.2 Multi-AI Prompts (Phases 9-10) — SHIPPED 2026-05-13</summary>

- [x] Phase 9: Bracket UX + AI Selector Foundation — 3/3 plans (BRKT-01, AISEL-01, AISEL-04 Packets portion)
- [x] Phase 10: Claude + Gemini Artifact Optimization — 5/5 plans (AISEL-02, AISEL-03, AISEL-04 Comparison + CedhMetaGap)

Full archive: `.planning/milestones/v1.2-ROADMAP.md`
Audit: `.planning/milestones/v1.2-MILESTONE-AUDIT.md` — documentation-only gaps, all 5 v1.2 reqs functionally satisfied via manual T1-T8 + filename verify.

</details>

<details>
<summary>✅ v1.3 Frontend Hardening + AI-Agnostic Rename + Code Hygiene (Phases 11-15, 999.1-999.8) — SHIPPED 2026-05-23</summary>

**Production phases (5):**
- [x] Phase 11: Web Design Guidelines Audit Fixes — 10/10 plans (WDG-01..10)
- [x] Phase 12: AI-Agnostic URL + Page Rename — 5/5 plans (RENAME-01..03)
- [x] Phase 13: ChatGpt* Class Rename + Summary Doc Comments — 4/4 plans (CLASSRENAME-01..03)
- [x] Phase 14: Broader Codebase Name-vs-Behavior Audit — 4/4 plans (AUDIT-01..03)
- [x] Phase 15: AiPlatform Value Object Refactor — 3/3 plans (AIPLATFORM-01..03)

**Backlog phases (8) — closed v1.3 quality debt:**
- [x] Phase 999.1: AI-Agnostic Prose Adaptation in Razor Views — 7/7 plans
- [x] Phase 999.2: Claude `<result>` Wrapper — Direct JSON Output — 1/1 plan
- [x] Phase 999.3: Packet Download Session Cache — 3/4 plans (P01 rolled into P02-04)
- [x] Phase 999.4: Truncated-JSON Response UX — 1/1 plan
- [x] Phase 999.5: v1.3 Backlog Catch-up + Test Hardening — 4/4 plans
- [x] Phase 999.6: v1.3 Ship-Gate Test Residual Cleanup — 3/3 plans (9→0 failures; 8/8 SECURITY threats CLOSED)
- [x] Phase 999.7: v1.3 Audit Cleanup — 4/4 plans (closed audit findings F-01, F-02, WDG checkbox + STATE arithmetic drift, 999.5-UAT status)
- [x] Phase 999.8: Remove Legacy `chatgpt-*` 301 Redirects — 1/1 plan (22 lines deleted, 0 added)

**Final test gate:** `Failed: 0, Passed: 497, Skipped: 3, Total: 500` preserved across all closure phases.
**Requirements coverage:** 22/22 SATISFIED (10 WDG + 3 RENAME + 3 CLASSRENAME + 3 AUDIT + 3 AIPLATFORM).
**Final audit:** PASSED (re-audit 2026-05-23 supersedes 2026-05-22 tech_debt; all 7 prior findings closed by 999.7 + 999.8).

Full archive: `.planning/milestones/v1.3-ROADMAP.md`
Requirements archive: `.planning/milestones/v1.3-REQUIREMENTS.md`
Audit archive: `.planning/milestones/v1.3-MILESTONE-AUDIT.md`

</details>

### 📋 v1.4 (Planned)

To be defined via `/gsd-new-milestone`. v1.4 candidates surfaced during v1.3:

- WDG-04 styled focus-trapped modal (deferred from Phase 11 WDG-04 override 2026-05-16)
- DeckFlow.Web.csproj NoWarn 1591;1573;1587 deferred backlog (~88 v1.1-era undocumented Web types)
- IN-01 _AiSelector vs view-level Normalize Gemini-flag fallback divergence (pre-existing latent inconsistency)
- v1.1 phase-dir archive move (06, 07, 07.1, 08 → `.planning/milestones/v1.1-phases/`)
- CSS-class / data-attribute / TS-constant chatgpt-* cleanup (AI-agnostic styling backlog)
- Gemini paste-limit workaround (flag-gated DECKFLOW_GEMINI_ENABLED; needs paste-cap raise OR API-key strategy)
- v13-harvest-worker-stalled debug follow-up (diagnostic logging deployed 2026-05-22; monitor post-v1.3-ship)
- audit-open scanner vocabulary alignment (status field naming convention drift)

## Backlog

### edhtop16 Filter Defaults vs DeckFlow Filter Defaults (BACKLOG — unnumbered, was 999.3 before collision with active Packet Download Session Cache phase; renumber when promoted)

**Goal:** [Captured for future planning]
**Requirements:** TBD
**Plans:** 0 plans

Captured 2026-05-17 during Phase 13 UAT T5. cEDH Meta-Gap fails to find Plagon, Lord of the Beach decks even though edhtop16.com shows multiple recent entries (2025-05 through 2026-01). DeckFlow filters (Six Months + Top Performing + minEventSize) return zero matches; edhtop16.com site UI likely uses different default filter window/event-size threshold/standing cutoff.

Repro (2026-05-17 14:18:57 + 14:19:09 in `web-20260517.log`):

- Commander: "Plagon, Lord of the Beach"
- Filters: SixMonths, TopPerforming, minEventSize=default, maxStanding=default
- Result: `InvalidOperationException` at `MetaGapService.cs:160` — "No EDH Top 16 decks matched your filters..."
- edhtop16.com browser shows entries from 2026-01-04, 2026-01-18, 2025-09-27, 2025-05-24

Pre-existing — predates Phase 13 (MetaGapService logic unchanged by rename). Investigate:

1. edhtop16 GraphQL `commander(name)` lookup: does "Plagon, Lord of the Beach" match the stored canonical name exactly?
2. Default DeckFlow form filter values vs site UI defaults — alignment audit.
3. minEventSize=50 default may be too restrictive — site UI may use 30.
4. timePeriod=SixMonths may map to ≤180 days where site uses calendar months (sometimes 183-184 days).

Plans:

- [ ] TBD (promote with /gsd:review-backlog when ready)

---

*v1.0 shipped 2026-05-02 | v1.1 shipped 2026-05-08 | v1.2 shipped 2026-05-13 | v1.3 shipped 2026-05-23*
