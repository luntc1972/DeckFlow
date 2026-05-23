# Phase 1 Context — WDG-04 Focus-Trapped Modal

**Phase:** 1 — WDG-04 Focus-Trapped Modal
**Goal (from ROADMAP.md):** Admin destructive-action confirm via native `<dialog>` focus-trapped modal, closing v1.3 carry-over (WDG-04 override 2026-05-16).
**Date:** 2026-05-23
**Milestone:** v1.4 (Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup)

## Domain

Replace the deferred inline `onsubmit="return confirm(...)"` browser-native confirm at `DeckFlow.Web/Views/AdminFeedback/Detail.cshtml:41` (Delete feedback button) with a styled, focus-trapped, accessible modal dialog. The modal must:
- Use the native HTML `<dialog>` element with `showModal()` (browser handles focus-trap baseline + ESC + ARIA `role="dialog"`)
- Display admin-themed styling consistent with v1.3 WDG-01 admin focus ring
- Restore focus to the invoking button on close
- Be reusable for future admin destructive operations (Phase 7 ContentSources delete, etc.)

This phase ships the FIRST reusable admin-modal primitive; Phase 7 (Content KB Admin UI) will consume it for ContentSources delete confirm without rewrite.

## Canonical Refs

- `.planning/ROADMAP.md` — Phase 1 row (Goal + REQ-IDs + SCs)
- `.planning/REQUIREMENTS.md` — MODAL-01 row
- `.planning/research/ARCHITECTURE.md` — Cluster A (WDG-04 Focus-Trapped Modal) component table; note: `admin-feedback-modal.ts` name OVERRIDDEN to `admin-modal.ts` (generic) per this discussion
- `.planning/research/SUMMARY.md` — Cross-Cutting Invariants #10 (native `<dialog>`, no npm dep) + #11 (CSS scoped to `.admin-shell` parent class)
- `.planning/research/FEATURES.md` — Feature 6 (WDG-04 modal) detail
- `.planning/research/PITFALLS.md` — recurring v1.3 R-6 (formatting paranoia) applies
- `.planning/milestones/v1.3-phases/11-web-design-guidelines-audit-fixes/11-VERIFICATION.md` — WDG-04 override audit trail (2026-05-16)
- `.planning/quick/260513-wdg-web-design-guidelines-audit-findings/260513-wdg-FINDINGS.md` (if still present) — finding D + BB original WDG-04 context
- `DeckFlow.Web/Views/AdminFeedback/Detail.cshtml` — line 39-44 (deferred onsubmit block)
- `DeckFlow.Web/wwwroot/css/admin.css` — modal CSS lands here in Phase 1 (Phase 3 will factor to `admin-common.css`; Phase 1 ships in `admin.css` directly)
- `DeckFlow.Web/wwwroot/ts/admin-feedback.ts` — existing feedback page TS (will import `showConfirm` from new admin-modal.ts)
- `DeckFlow.Web/wwwroot/ts/admin-harvest.ts`, `admin-analytics.ts` — sibling admin TS modules (reference for module shape conventions)
- `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml` — admin layout (admin.css link at line 14; Scripts RenderSection at line 36)
- `CLAUDE.md` — formatting paranoia (`{ get; init; }` preservation, raw-string LF preservation, touch-only lines that need touching)

## Code Context (from scout)

**Existing patterns to mirror:**
- Existing admin TS modules in `DeckFlow.Web/wwwroot/ts/admin-*.ts` (admin-analytics.ts, admin-feedback.ts, admin-harvest.ts) — `module: "none"` per `tsconfig.json`, output to `wwwroot/js/`, no bundler, no npm deps for runtime
- Admin views structure: per-area `Views/AdminFeedback/`, `Views/AdminAnalytics/`, `Views/AdminFlags/`, `Views/AdminHarvest/`, `Views/AdminLanding/`; `_ViewStart.cshtml` per area selects `_AdminLayout.cshtml`
- All admin pages already include `admin.css` via `_AdminLayout.cshtml` line 14

**Files to modify:**
- `DeckFlow.Web/Views/AdminFeedback/Detail.cshtml` — replace inline `onsubmit` with explicit `<button type="button">` triggering modal via partial
- `DeckFlow.Web/wwwroot/css/admin.css` — add `.admin-modal`, `.admin-modal__backdrop`, `.admin-modal__panel` classes scoped to `.admin-shell`
- `DeckFlow.Web/wwwroot/ts/admin-feedback.ts` — import `showConfirm` from new admin-modal.ts; attach to delete button

**Files to create:**
- `DeckFlow.Web/wwwroot/ts/admin-modal.ts` — reusable `showConfirm({title, message, confirmLabel, danger}): Promise<boolean>` helper using native `<dialog>` + hand-rolled extras (restore-focus on close, focus first interactive after `showModal()`)
- `DeckFlow.Web/Views/Shared/_AdminConfirmModal.cshtml` — reusable Razor partial rendering the `<dialog>` markup (consumed by Detail.cshtml; future Phase 7 ContentSources delete reuses)

**Files NOT to touch:**
- `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml` — Phase 3 (Admin Mobile Sweep) owns admin layout changes; Phase 1 does NOT modify
- Any non-admin Razor view, CSS, TS, controller, or test
- `admin.css` factoring into `admin-common.css` + `admin-mobile.css` — Phase 3 owns; Phase 1 ships modal CSS in `admin.css` first (Phase 3 will extract during factoring)

## Decisions

### D-01: Reusable `showConfirm()` helper (NOT inline)

Build `admin-modal.ts` exporting a generic `showConfirm({title, message, confirmLabel, danger}): Promise<boolean>` helper. AdminFeedback Delete uses it; Phase 7 ContentSources delete + future admin destructive ops reuse without rewrite.

**Why:** Phase 7 will need ≥3 delete-confirm sites (ContentSources, ContentHarvest cancel, possibly ContentSpend ack). Building reusable now is ~30 LOC more than inline; copy-paste later costs 3× rewrite. YAGNI fails the "rule of three" forecast.

### D-02: New `admin-modal.ts` (generic; overrides ARCHITECTURE.md `admin-feedback-modal.ts` name)

New file `DeckFlow.Web/wwwroot/ts/admin-modal.ts` exports `showConfirm`. `admin-feedback.ts` imports it.

**Why:** Generic name matches D-01 reusability decision. `admin-feedback-modal.ts` name from ARCHITECTURE.md was scoped to one consumer; this discussion expanded scope. ARCHITECTURE.md note: name overridden — update if/when re-read.

### D-03: Default `<dialog>` dismiss behavior (ESC + backdrop click both cancel)

Click-outside cancels (standard native `<dialog>` UX). ESC cancels. Cancel button explicit. No double-click-to-confirm or click-outside-suppression for v1.4 — admin-only single-operator surface; deliberate clicks are the norm, accidental dismiss on Delete is not a recoverable cost (operator just re-clicks Delete).

**Why:** Native `<dialog>` behavior is the documented standard; users expect it. Suppressing backdrop click on destructive ops adds non-standard friction without preventing real mistakes (Cancel button is one click anyway). If post-v1.4 UAT shows operator misclicks deleting feedback, revisit in v1.5.

### D-04: `_AdminConfirmModal.cshtml` partial in `Views/Shared/`

New `DeckFlow.Web/Views/Shared/_AdminConfirmModal.cshtml` renders the `<dialog>` markup once. `Detail.cshtml` consumes via `@await Html.PartialAsync("_AdminConfirmModal", new AdminConfirmModalViewModel(...))`. Phase 7 reuses same partial.

**Why:** Matches D-01 reusability. Partial is ~15 lines (dialog + 2 buttons + ARIA attrs). Inline duplication would require Phase 7 copy-paste OR a v1.5 refactor. Partial in `Views/Shared/` follows existing `_DeckToolTabs.cshtml`, `_AiSelector.cshtml`, `_WorkflowStepTabs.cshtml` conventions.

### D-05: Modal CSS lands in `admin.css` (Phase 3 will factor)

Phase 1 ships modal CSS additions in `admin.css` directly. Phase 3 (Admin Mobile Sweep) owns the `admin.css → admin-common.css + admin-mobile.css + admin.css shim` factoring; Phase 3 will extract Phase 1's modal CSS during factoring.

**Why:** Phase 1 lands FIRST per build order; `admin-common.css` doesn't exist yet. Adding CSS to a file that gets factored later is normal phase sequencing — Phase 3's factoring task includes "move Phase 1 modal CSS to admin-common.css" as part of its scope. Avoids speculative file creation in Phase 1.

### D-06: ARIA + native `<dialog>` semantics (no npm focus-trap dep)

Use native `<dialog>` + `showModal()`. Browser handles:
- `role="dialog"` (implicit)
- `aria-modal="true"` (implicit when opened with `showModal()`)
- Initial focus to first focusable inside dialog
- ESC closes (browser default)
- Backdrop rendering + click-to-close (native)

Hand-rolled additions:
- `aria-labelledby` pointing to title element
- `aria-describedby` pointing to message element
- Restore-focus to invoking button on close (store `document.activeElement` before `showModal()`; call `.focus()` on stored ref in `close` event handler)

**Why:** Native `<dialog>` covers ~90% of focus-trap UX out of box (Chrome 37+, Firefox 98+, Safari 15.4+). Hand-rolled restore-focus is ~5 LOC. Avoiding an npm dep keeps `DeckFlow.Web/package.json` minimal (TypeScript-only currently). MV3 CSP-clean — no eval, no inline JS.

### D-07: Confirm/Cancel API shape

```typescript
// admin-modal.ts
export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;     // default: "Confirm"
  cancelLabel?: string;      // default: "Cancel"
  danger?: boolean;           // default: false — adds .danger class to confirm button
}

export async function showConfirm(opts: ConfirmOptions): Promise<boolean> {
  // Returns true if user clicked confirm, false otherwise (ESC, backdrop click, Cancel button)
}
```

`admin-feedback.ts` usage:
```typescript
deleteForm.addEventListener('submit', async (e) => {
  e.preventDefault();
  const confirmed = await showConfirm({
    title: 'Delete Feedback',
    message: `Delete feedback #${id} permanently?`,
    confirmLabel: 'Delete',
    danger: true,
  });
  if (confirmed) deleteForm.submit();
});
```

### D-08: Razor partial signature

`_AdminConfirmModal.cshtml` is **structural-only** — modal exists in DOM hidden; TS populates title/message/buttons at call time via `data-*` attributes OR direct DOM manipulation. Partial accepts NO model — it's a singleton DOM template injected once per page (or once in `_AdminLayout.cshtml` Phase 3 if reused).

**Phase 1 placement:** `Detail.cshtml` includes partial inline (`@await Html.PartialAsync("_AdminConfirmModal")`). Phase 7 may move the include to `_AdminLayout.cshtml` if every admin page needs it.

### D-09: NO scope creep

In-scope for Phase 1: WDG-04 modal closure (single Delete confirm at `Detail.cshtml:41`).

Out-of-scope (deferred / future phases):
- Replacing other admin confirms (no other `onsubmit` in admin tree currently — grep verified)
- Replacing public-facing confirms (`deck-sync.ts` `onsubmit` is user-facing, not admin; out of v1.4 scope per ROADMAP)
- Theming the modal across guild themes (admin shell only)
- Promise-based prompt() / alert() helpers (only confirm() needed for v1.4)
- Animation / transition / micro-interactions (CSS may add minimal `transition: opacity`; out-of-scope for fancy fade/scale)

## Open Questions

None. All 4 selected gray areas decided. Researcher can proceed.

## Deferred Ideas

- **v1.5 backdrop suppression on destructive ops** — if post-v1.4 UAT shows misclick-dismiss problem on Delete, add `dialog.addEventListener('click', e => { if (e.target === dialog) e.preventDefault(); })` to suppress backdrop close on destructive variants
- **v1.5 prompt() helper** — if admin needs free-text input modal (e.g., "Reason for archive?"), add `showPrompt({title, message, placeholder}): Promise<string|null>` to admin-modal.ts
- **v1.5 Razor partial in `_AdminLayout.cshtml`** — if Phase 7 confirms most admin pages need the modal, move the include from per-page to `_AdminLayout.cshtml` for single-render
- **v2.0 npm focus-trap dep** — only if a real focus-trap bug surfaces native `<dialog>` cannot handle (none expected — modern browsers ≥2-year-old satisfy DeckFlow's evergreen baseline)

## Constraints (carried forward from PROJECT.md + CLAUDE.md + research)

- **MUST** use native HTML `<dialog>` element + `showModal()` (research SUMMARY.md invariant #10)
- **MUST NOT** add npm focus-trap dependency (research invariant #10)
- **MUST** scope all modal CSS to `.admin-shell` parent class — zero bleed into 22 guild themes (research invariant #11)
- **MUST** preserve LF line endings (CLAUDE.md formatting paranoia)
- **MUST NOT** auto-format unrelated lines (CLAUDE.md "touch only lines that need touching")
- **MUST** keep TypeScript strict mode (existing `DeckFlow.Web/tsconfig.json` `strict: true`)
- **MUST** preserve `{ get; init; }` on any new C# record types (none expected in Phase 1 — pure TS + Razor + CSS)
- **MUST** use plain default-author commits (no Co-Authored-By trailer)
- **MUST** route plan through Codex peer review (`/gsd-review`) before execute-phase dispatch (research R-4)
- **MUST** use `grep -cE` anchored to specific line/method patterns in SCs, NOT loose grep (research R-3)

## Acceptance Criteria (from ROADMAP.md Phase 1 SCs)

Locked at roadmapper time; planner uses these verbatim in PLAN.md acceptance gates. Re-read `.planning/ROADMAP.md` Phase 1 Success Criteria section for exact wording.
