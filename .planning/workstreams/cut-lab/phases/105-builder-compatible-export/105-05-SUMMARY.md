---
phase: 105-builder-compatible-export
plan: 05
status: complete
commits: [a90c9272, a6e79d52, a2c3a4b4, 7cb68348]
---

# 105-05 Summary — Export e2e, gate, and the defect it surfaced

**Built (Task 1):** `DeckFlow.Web/e2e/cut-lab-export.spec.ts` — headless Playwright coverage
of the Export surface (2 tests × desktop + mobile = 4): below-100 the Export tab is disabled
with the reach-100 hint; cutting to exactly 100 unlocks the tab live (JS path); activating it
POSTs `cut-lab-export-form` and the panel renders both-dialect finished lists, CUT/ADD patches,
and a green count=100 validation summary. The loop waits for the async decide re-render to
settle (`networkidle`) before each accept and syncs on the proposal heading.

## Defect surfaced by the e2e (the reason this wave mattered)

Writing the e2e exposed a real integration defect that unit tests + blind verify had missed:
**JS-cutting a multi-copy entry to reach 100 overshot the target and the Export tab never
re-enabled.**

- **Root cause:** Cut Lab decisions are name-keyed with **no per-copy quantity**;
  `CutLabWorkingList.Derive` removes a whole pool entry on accept. `CutLabCutRoundEngine.BuildQueue`
  proposed any eligible card regardless of quantity, so accepting a 35-copy basic to trim 2 cards
  overshot (102 → 67). A `Math.Max(remaining, 0)` clamp masked the overshoot as "at target," and
  the JS decide path never re-enabled the server-rendered Export tab.

- **Fix (Option A + tab-wire):**
  - `CutLabCutRoundEngine.BuildQueue` excludes eligible cards whose `Quantity > cardsRemainingToTarget`.
  - `CutLabDecisionApplier.Apply` defense-in-depth guard ignores an Accept that would overshoot.
  - `cut-lab.ts` toggles the Export tab enabled state on `cardsRemaining === 0` after each JS
    decision and what-if keep/restore.
  - Atomic what-if keep: reject (NoChange) instead of half-applying when the overshoot guard
    refuses the replacement cut (Codex-review MED, both JSON + no-JS paths).

- **Deferred:** partial-copy cuts (cut N of a stack) — logged to roadmap backlog as **Option B**
  (a ~9-file P103 model change, not a bug fix).

**Verification (Task 2, human-verify approved):**
- e2e cut-lab-export 4/4; Cut Lab e2e regression clean (1 known cold-server flake → 20/20 isolated).
- Core.Tests 1612/0; Web.Tests 1874/0 (16 skipped); vitest 69/69; tsc clean; build 0/0.
- EOL LF-preserved on all touched files (per-file CR == HEAD; churn gate = ignore-space).
- Export-panel screenshots captured for azorius + nyx × desktop + mobile (eyeballed).
- UAT approved by the user.

**Commits:** a90c9272 (engine/applier/tab-wire + unit/vitest + fixture updates), a6e79d52
(e2e spec), a2c3a4b4 (roadmap backlog), 7cb68348 (atomic what-if keep). NOT pushed.
