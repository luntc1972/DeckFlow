# Phase 16 Discussion Log — WDG-04 Focus-Trapped Modal

**Date:** 2026-05-23
**Phase:** 1
**Mode:** discuss (default)

## Gray Area Selection

**Question:** Which gray areas to discuss for WDG-04 modal?
**Options presented:**
- Modal reusability — inline vs reusable showConfirm() helper
- TS module location — extend admin-feedback.ts vs new admin-modal.ts vs admin-feedback-modal.ts
- Backdrop / dismiss behavior — click-outside cancels vs force explicit confirm
- Razor markup placement — inline vs partial

**User selected:** All 4 (multi-select).

## Discussion

### 1. Modal Reusability

**Question:** Inline AdminFeedback-only OR reusable showConfirm() helper?

**Options:**
- Reusable showConfirm() helper (Recommended) — build admin-modal.ts now; Phase 22 ContentSources reuses
- AdminFeedback-only inline — smallest scope; Phase 22 rewrites
- Reusable + ship inline first — defer reusable to Phase 22

**User selected:** Reusable showConfirm() helper.

**Captured as:** D-01 — reusable `admin-modal.ts` exports `showConfirm({title, message, confirmLabel, danger}): Promise<boolean>`. Pays off across 3+ Phase 22 delete sites; "rule of three" forecast.

### 2. TS Module Location

**Question:** Where does the modal TS live?

**Options:**
- New admin-modal.ts (generic, matches reusable choice)
- Extend existing admin-feedback.ts
- admin-feedback-modal.ts (per ARCHITECTURE.md verbatim)

**User selected:** New admin-modal.ts.

**Captured as:** D-02 — overrides ARCHITECTURE.md `admin-feedback-modal.ts` name. Generic naming matches D-01.

### 3. Backdrop / Dismiss Behavior

**Question:** Click-outside-to-close on destructive Delete confirm?

**Options:**
- Click-outside cancels (default `<dialog>` behavior)
- Force explicit Confirm/Cancel (no backdrop dismiss)
- Click-outside cancels + double-click required to confirm

**User selected:** Click-outside cancels (default).

**Captured as:** D-03 — ESC + backdrop click both cancel; Cancel button explicit. Standard `<dialog>` UX; admin-only single-operator surface — no escalation needed in v1.4.

### 4. Razor Markup Placement

**Question:** Inline `<dialog>` in Detail.cshtml OR reusable partial?

**Options:**
- `_AdminConfirmModal.cshtml` partial (Recommended if reusable)
- Inline `<dialog>` in Detail.cshtml

**User selected:** `_AdminConfirmModal.cshtml` partial in `Views/Shared/`.

**Captured as:** D-04 — partial follows existing `_DeckToolTabs.cshtml`, `_AiSelector.cshtml`, `_WorkflowStepTabs.cshtml` conventions. Phase 22 reuses same partial.

## Deferred Ideas (out of Phase 16 scope)

- v1.5 backdrop suppression on destructive ops (if UAT shows misclick-dismiss problem)
- v1.5 `showPrompt()` helper for free-text input modals
- v1.5 move partial include to `_AdminLayout.cshtml` if Phase 22 confirms most admin pages need it
- v2.0 npm focus-trap dep (only if a real focus-trap bug surfaces native `<dialog>` can't handle)

## Claude's Discretion

Decisions Claude made without asking (implementation details, not gray areas):
- **D-05 CSS file placement** (admin.css now; Phase 18 factors to admin-common.css) — sequencing matter, follows ROADMAP order
- **D-06 ARIA + native semantics** (use browser defaults; hand-roll only restore-focus + aria-labelledby + aria-describedby) — research SUMMARY.md invariant #10 + smallest LOC delta
- **D-07 API shape** (`showConfirm({title, message, confirmLabel, danger}): Promise<boolean>`) — derived from D-01 reusable decision; idiomatic TS async pattern
- **D-08 partial signature** (structural-only, no model; singleton DOM template) — simplest shape; Phase 22 can add model later if needed
- **D-09 scope guard** (no other admin confirms in scope, no public-facing, no theming) — phase boundary enforcement
