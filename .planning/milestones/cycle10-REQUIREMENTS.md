# Requirements Archive: cycle10 Cycle 10 — Studio Automation, Sync & Polish

**Archived:** 2026-06-21
**Status:** SHIPPED

For current requirements, see `.planning/REQUIREMENTS.md`.

---

# Requirements: DeckFlow — Cycle 10 (Studio Automation, Sync & Polish)

**Defined:** 2026-06-20
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT / Claude / Gemini and get back a useful answer in one round-trip — without the user reformatting anything. (This cycle serves the core by making the operator-local Content-KB pipeline faster and more trustworthy.)

> REQ-ID prefixes are new this cycle (AUTO / SYNC / SRC / HSEL / SUI) to avoid collision with prior milestones' IDs (SEL, KBI, KBUX, PRM, PUB, REM, SITE, DIST already used). Numbering starts at 01 within each new category.

## v1 Requirements

Requirements for Cycle 10. Each maps to exactly one roadmap phase.

### Pipeline Automation (AUTO)

- [ ] **AUTO-01**: Operator harvest of a video automatically runs distillation — no separate manual "distill" step. Harvest yields a distilled, review-ready entry in one action.
- [ ] **AUTO-02**: Distills whose quality/confidence signal is at or above a configurable threshold are auto-approved; distills below the threshold remain in the manual review queue. The threshold is operator-adjustable and auto-approval can be turned off.

### Pull-from-Prod Reconcile (SYNC)

- [ ] **SYNC-01**: Operator can pull the live prod `content_site_index` rows (and their published artifacts) down to local from Studio, so Studio reflects what is actually live.
- [ ] **SYNC-02**: Studio surfaces prod↔local differences per entry — classified as prod-newer, missing-locally, local-only, or diverged — so the operator can see exactly what is out of sync.
- [ ] **SYNC-03**: For each surfaced diff, the operator can choose a resolution (adopt prod / keep local) from Studio without dropping to the CLI or hand-editing the DB.

### Creator / Source Management (SRC)

- [ ] **SRC-01**: Operator can maintain a persisted list of curated creators/channels in Studio — add, view, and remove entries.
- [ ] **SRC-02**: When browsing videos to harvest, the operator selects a creator from a dropdown of the saved list instead of pasting a channel URL every time. (Paste-URL remains available as a fallback for one-off channels.)

### Harvest Video Selection (HSEL)

- [ ] **HSEL-01**: The creator video-selection list defaults to showing only not-yet-harvested videos, with a toggle to show all (including harvested/distilled/published).
- [ ] **HSEL-02**: Operator can skip/ignore a candidate video so it no longer appears in the selection list — distinct from Block: no artifact hard-delete and no harvest blocklist entry, just "don't surface this candidate again."
- [ ] **HSEL-03**: Operator can view the list of skipped/ignored videos and un-skip one to bring it back into the selection list (parity with the existing Block/Unblock pair).

### Studio UI Polish (SUI)

- [x] **SUI-01**: Pipeline status is clear at a glance on the main Studio pages — consistent status badges (harvested / distilled / approved / publish-state) reusing the Cycle 9 `PublishStateDeriver` / `VideoStatusResolver`.
- [x] **SUI-02**: Harvest → review → publish flow is tightened for fewer clicks (multi-select ergonomics, sensible defaults, less back-and-forth between pages).
- [x] **SUI-03**: Loading, error, and success feedback states are improved across Studio actions (including harvest/distill spend warnings and failure messages).
- [x] **SUI-04**: Studio layout and inter-page navigation are cleaned up for density and clarity.
- [x] **SUI-05**: Video and entry lists can be filtered/grouped by creator, so it is easy to see which videos belong to which creator.
- [x] **SUI-06**: The `MainLayout.razor` "About" link scaffold placeholder (currently points at `docs.microsoft.com/aspnet/`) is fixed to a real, relevant target.

### Distribution (DIST)

- [x] **DIST-01**: DeckFlow.Studio is packaged as a self-contained, single-file `win-x64` executable the operator can run on a clean Windows machine with no .NET runtime installed. A repeatable publish profile/script produces the artifact, and the build + run steps are documented. _(Complete 2026-06-20 — verified PASS 7/7; operator clean-machine smoke passed; + crash logging + browser auto-open.)_

## v2 Requirements

Deferred to a future cycle. Tracked but not in this roadmap.

### KB Value Validation (KBVAL)

- **KBVAL-01**: A/B harness that runs deck-analysis prompts with vs without injected KB context and scores output lift.
- **KBVAL-02**: Decision gate on `content.kb.enabled` prod flip + philosophy-profile build, driven by KBVAL-01 results.

### Pipeline Automation (future)

- **AUTO-03**: Scheduled/cron harvest cadence (operator explicitly prefers manual curation this cycle).
- **AUTO-04**: Bulk/at-scale creator-source onboarding.

## Out of Scope

Explicitly excluded this cycle. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| KB-value A/B experiment (KBVAL-01/02) | Operator chose to defer; KB stays dark, `content.kb.enabled` flip deferred again. |
| Scheduled / cron harvest | Operator wants to control exactly which videos enter; manual curation by design. |
| Bulk / auto creator-source scraping | Same — only operator-curated videos enter the pipeline. |
| SEO / growth / ops lane (SEO-01..05) | Separate lane; off-Render setup (Search Console, Bing, backlinks) is operator/ops work, not this cycle. |
| Distill provider/model swap | AUTO-01/02 reuse existing distill providers; no `LlmDistillationProviderFactory` change. |
| Public-app (deckflow.gg) behavior changes | Cycle 10 is Studio operator-tooling + a prod read for SYNC; no end-user-facing app change beyond what publish already does. |

## Traceability

Which phases cover which requirements. Populated at roadmap creation (2026-06-20).

| Requirement | Phase | Status |
|-------------|-------|--------|
| AUTO-01 | Phase 59 — Pipeline Automation | Pending |
| AUTO-02 | Phase 59 — Pipeline Automation | Pending |
| SYNC-01 | Phase 60 — Pull-from-Prod Reconcile | Pending |
| SYNC-02 | Phase 60 — Pull-from-Prod Reconcile | Pending |
| SYNC-03 | Phase 60 — Pull-from-Prod Reconcile | Pending |
| SRC-01 | Phase 61 — Creator Sources & Selection | Pending |
| SRC-02 | Phase 61 — Creator Sources & Selection | Pending |
| HSEL-01 | Phase 61 — Creator Sources & Selection | Pending |
| HSEL-02 | Phase 61 — Creator Sources & Selection | Pending |
| HSEL-03 | Phase 61 — Creator Sources & Selection | Pending |
| SUI-01 | Phase 62 — Studio UI Polish | Complete |
| SUI-02 | Phase 62 — Studio UI Polish | Complete |
| SUI-03 | Phase 62 — Studio UI Polish | Complete |
| SUI-04 | Phase 62 — Studio UI Polish | Complete |
| SUI-05 | Phase 62 — Studio UI Polish | Complete |
| SUI-06 | Phase 62 — Studio UI Polish | Complete |
| DIST-01 | Phase 63 — Studio Self-Contained Executable | Complete |

**Coverage:**
- v1 requirements: 17 total
- Mapped to phases: 17 ✓ (every requirement in exactly one phase)
- Unmapped: 0 ✓

---
*Requirements defined: 2026-06-20*
*Last updated: 2026-06-20 after roadmap creation (traceability populated, 16/16 mapped)*
