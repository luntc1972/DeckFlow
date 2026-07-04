# Requirements: Cycle 15 — Cleanup, Refactor & Visual Polish

**Milestone goal:** Pay down accumulated tech-debt and finish deferred polish without changing public behavior — every paste artifact byte-identical, every theme render unchanged.

**Cross-cutting gate (applies to every requirement):** No net-new user-facing feature. Paste artifacts (ChatGPT/Claude/Gemini variants) remain byte-identical and theme render is unchanged except where a requirement explicitly corrects a visual bug. ADR-0001 (prompt-variant decoupling — no shared prompt-prose helper) and ADR-0002 (CalVer, named cycle) hold.

---

## Cycle 15 Requirements

### PKTSVC — Packet-Service SRP Split

The four parallel packet-building services (`DeckAnalysisPacketService` 2372 LOC, `DeckComparisonService` 1033, `MetaGapService` 956, `DeckPrimerPacketService` 904) share a god-file smell: prompt assembly and Scryfall reference resolution are inlined and duplicated. Extract shared collaborators; keep artifacts byte-identical.

- [ ] **PKTSVC-01**: Shared prompt-assembly orchestration is extracted into a reusable collaborator that each of the four packet services delegates to, without collapsing the per-variant prompt prose (ADR-0001 preserved).
- [ ] **PKTSVC-02**: Scryfall reference-resolution logic is extracted into a single reusable resolver consumed by all four packet services; no service retains a duplicate resolution code path.
- [ ] **PKTSVC-03**: Each of the four packet services is reduced to an orchestration shell (no single service file materially larger than its collaborators; extracted methods honor the ≤30-line intention-revealing guideline where practical), with the extracted collaborators unit-tested in isolation.
- [ ] **PKTSVC-04**: A byte-identical regression guard proves the analysis, comparison, meta-gap, and primer artifacts are unchanged pre/post refactor across all three AI variants (ChatGPT / Claude / Gemini), flag ON and OFF.

### THEME — `--accent-strong` Semantic-Token Migration

`--accent-strong` (143 refs across 27 theme files) is overloaded across link, brand, focus, error, and CTA roles; semantic tokens (`--link`/`--danger`/`--focus`/`--cta-border`) exist but the migration was never finished. Error text reads as a link in red guild themes.

- [ ] **THEME-01**: Every `--accent-strong` usage is reclassified onto the correct semantic token (`--link` / `--danger` / `--focus` / `--cta-border`) by role across all 27 theme files; token additions live in each theme's `:root` and layout stays in `site-common.css`.
- [ ] **THEME-02**: Error/danger text no longer resolves to the link color in red guild themes; the fix is visually verified across affected themes at desktop and mobile viewports.
- [ ] **THEME-03**: No unintended visual regression on non-error surfaces — the theme render diff is limited to the intended semantic corrections.

### UIAUDIT — UI Re-Score + Studio Stage 4 Closeout

The 6-pillar UI audit (`tasks/UI-REVIEW.md`) last scored 16/24 at v1.0 and was never re-measured. DirectPush Stage 4 has an owed live desktop+mobile verify and a flagged no-op success-copy warning.

- [ ] **UIAUDIT-01**: The 6-pillar UI audit is re-run against the current site and scored; the gap to ≥20/24 is enumerated with concrete per-pillar fixes.
- [ ] **UIAUDIT-02**: The enumerated gaps are fixed and a re-score confirms the site clears ≥20/24.
- [ ] **UIAUDIT-03**: DirectPush Stage 4 card is verified live at desktop + mobile; the no-op success copy (`DirectPush.razor:441`) is corrected to not claim a push that did not happen, phrasing is unified with the committed variant, and the commit SHA is short-form to avoid mobile overflow.

### AICLEAN — `chatgpt-*` Naming Cleanup

~1545 `chatgpt-*` identifiers (1072 CSS class refs across 25 theme forks, 224 TS, 249 views) predate the AI-agnostic rename and should be renamed to match. Render must stay byte-identical.

- [ ] **AICLEAN-01**: All `chatgpt-*` CSS class names are renamed to AI-agnostic names across the 25 theme forks + `site-common.css` + `site.css`, with the rendered output byte-identical.
- [ ] **AICLEAN-02**: The matching `chatgpt-*` TypeScript constants, `data-*` attributes, and Razor view references are renamed in lockstep; no dead or duplicated selectors remain.
- [ ] **AICLEAN-03**: No `chatgpt-*` identifier remains in `css/`, `ts/`, or `Views/` (grep-clean); page render and the Playwright e2e suite are unchanged.

### REVIEW — Refactor-Review Sweep

A code-review pass surfaces remaining SRP/duplication targets beyond the pre-identified ones (candidates: `deck-sync.ts` 2877 LOC, `Harvest.razor.cs` 1222), and confirmed in-scope items are executed under the same behavior-neutral gate.

- [ ] **REVIEW-01**: A code-review sweep is run over the largest/most-duplicated files; each surfaced target is triaged as in-scope-this-cycle or backlog, with the decision recorded.
- [ ] **REVIEW-02**: In-scope targets confirmed by the sweep are refactored under the byte-identical/behavior-neutral gate, with tests, or explicitly deferred to backlog with reasoning if they exceed the cycle's risk budget.

---

## Future Requirements (Deferred)

- **Manabase engine refactor** — `CastabilitySimulator` (1539) / `ManabaseAnalyzer` (1077) / `ManabaseClassifier` (1073) SRP split. Deferred: behavior-critical Monte-Carlo + Karsten scoring with no byte-identical gate, heavily worked in Cycles 12/14. Needs a numeric-parity harness built FIRST. Candidate for its own future refactor cycle.
- **Scheduled/bulk harvest** (AUTO-03/04), **SEO/growth lane** (SEO-01..05), **matchup / meta-threat read** (cedh-meta-gap lane) — feature lanes, not tech-debt.

## Out of Scope (This Milestone)

- **DeckController god-class split** — already DONE (verified 2026-07-04; split across `DeckPacketController`/`DeckLookup`/`DeckSync`/`DeckPrimer`). Not a live debt item.
- **Manabase engine numeric behavior / refactor** — excluded (see Future Requirements); this cycle must not touch manabase scoring or simulation logic.
- **Any net-new user-facing feature** — this is a tech-debt cycle by definition.
- **Framework migration** — ASP.NET 10 + Razor pinned.

## Traceability

<!-- Filled by roadmap: REQ-ID → Phase mapping. -->
