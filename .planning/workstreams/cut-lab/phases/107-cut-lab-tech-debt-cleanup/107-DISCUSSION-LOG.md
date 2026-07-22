# Phase 107: Cut Lab Tech-Debt Cleanup - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-07-22
**Phase:** 107-cut-lab-tech-debt-cleanup
**Areas discussed:** Dead fields, Live-patch, Theme scope, Batching

---

## Item 1 — Dead fields (CutLabPageService)

| Option | Description | Selected |
|--------|-------------|----------|
| Remove | Delete unused fields + ctor params; update DI-probe tests | ✓ |
| Keep + justify | Leave, add xmldoc note for future use | |
| Claude decides | Inspect usage, pick during planning | |

**User's choice:** Remove
**Notes:** Scope removal to CutLabPageService only — `_spellbook`/`_categoryKnowledge` are legitimately used in CutLabAnalysisContextBuilder and must NOT be touched there.

---

## Item 6 — Structural-analysis table live-patch

| Option | Description | Selected |
|--------|-------------|----------|
| Close as documented | Keep server-render refresh, add comment | |
| Implement live-patch | Add JS to live-update table on decide; new e2e | ✓ |
| Claude decides | Weigh effort vs UX during planning | |

**User's choice:** Implement live-patch
**Notes:** Heaviest item; isolate in its own plan/wave. Must augment (not replace) server-render refresh so the no-JS fallback still works.

---

## Item 3 — Dark-theme delta contrast overrides

| Option | Description | Selected |
|--------|-------------|----------|
| All dark guild themes | Override --cutlab-delta-up/down in every dark theme | ✓ |
| Only measured failures | Fix only proven sub-AA themes | |
| Claude decides | Contrast-check each, override where failing | |

**User's choice:** All dark guild themes
**Notes:** Token seam exists (site-common.css defines, only site-nyx.css overrides). Contrast-check confirms need, but all dark themes get overrides for consistency.

---

## Phase batching

| Option | Description | Selected |
|--------|-------------|----------|
| All 6, one phase | Multi-plan/wave, closes whole debt tail | ✓ |
| Split heavy item out | Carve live-patch to follow-up, do 5 now | |

**User's choice:** All 6, one phase
**Notes:** Isolate item 6 in its own plan for independent verification; other five are mechanical.

---

## Claude's Discretion

- Item 2 (pool-status chip reconciliation) — pick canonical count matching the chip label.
- Item 4 (xmldoc garble, Manabase copy leak, Nyx badge overlap, Lock-all-lands contrast, mobile label truncation) — mechanical fixes per code.
- Item 5 (cacheKey→data-attr, path-base safety, shared pluralizer) — mechanical.

## Deferred Ideas

None — discussion stayed within phase scope.
