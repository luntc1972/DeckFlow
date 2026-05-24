# Phase 16: WDG-04 Focus-Trapped Modal — Research

**Researched:** 2026-05-23
**Domain:** Native HTML `<dialog>` + TypeScript (module:none / IIFE) + Razor partial + scoped CSS, all admin-shell
**Confidence:** HIGH (DeckFlow conventions verified against codebase at HEAD 65f2fe4; `<dialog>` behavior verified against MDN)

## Summary

Phase 16 ships a reusable `showConfirm()` admin-modal primitive that replaces the deferred `onsubmit="return confirm(...)"` at `AdminFeedback/Detail.cshtml:41`. The technical surface is small and tightly bounded: one new IIFE TypeScript module exposing `window.DeckFlowAdminModal.showConfirm`, one structural-only Razor partial in `Views/Shared/`, modal CSS scoped to the existing `.admin-shell` wrapper, and one wire-up edit to `Detail.cshtml`. **Three CONTEXT decisions need a planner note** — D-03 (backdrop click closes) and D-06 (focus restoration is hand-rolled, ESC is native) overstate what native `<dialog>` does automatically, and D-01 (TS `export`/`import`) cannot be taken literally because `tsconfig.json` is `"module": "none"`. Each is a small mechanical correction, not a design rethink.

The `.admin-shell` parent class already exists in `_AdminLayout.cshtml` line 18, so CSS scoping requires zero layout markup change. The `Scripts` RenderSection convention from `AdminFeedback/Index.cshtml:100-103` is the canonical hook for adding `admin-modal.js` + `admin-feedback.js` to `Detail.cshtml`. Manual UAT will rely on observable browser behavior (Tab cycling, Escape, focus return) since VSTest is unreliable in WSL and no automated browser test framework is wired into the repo.

**Primary recommendation:** Build `admin-modal.ts` as an IIFE that attaches `showConfirm` to `window.DeckFlowAdminModal`, mirroring `df-select.ts:837-839` `window.DeckFlow.attachDfSelect` pattern. Hand-roll a 1-line backdrop-click handler (D-03 requires it) and a 2-line restore-focus fallback (browser does this for the trigger element automatically, but a stored-ref fallback covers edge cases where the trigger was detached or hidden during the await).

## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-01: Reusable `showConfirm()` helper (NOT inline).** Build `admin-modal.ts` exposing a generic `showConfirm({title, message, confirmLabel, danger}): Promise<boolean>` helper. AdminFeedback Delete uses it; Phase 22 ContentSources delete + future admin destructive ops reuse without rewrite.

**D-02: New `admin-modal.ts` (generic; overrides ARCHITECTURE.md `admin-feedback-modal.ts` name).** New file `DeckFlow.Web/wwwroot/ts/admin-modal.ts` exposes `showConfirm`. `admin-feedback.ts` consumes it.

**D-03: Default `<dialog>` dismiss behavior (ESC + backdrop click both cancel).** Click-outside cancels. ESC cancels. Cancel button explicit. No double-click-to-confirm or click-outside-suppression for v1.4.

**D-04: `_AdminConfirmModal.cshtml` partial in `Views/Shared/`.** `Detail.cshtml` consumes via `@await Html.PartialAsync("_AdminConfirmModal")`. Phase 22 reuses the same partial.

**D-05: Modal CSS lands in `admin.css` (Phase 18 will factor).** Phase 16 ships modal CSS additions in `admin.css` directly. Phase 18 (Admin Mobile Sweep) extracts during the `admin-common.css` factoring.

**D-06: ARIA + native `<dialog>` semantics (no npm focus-trap dep).** Use native `<dialog>` + `showModal()`. Browser handles role/aria-modal/initial-focus/ESC/backdrop-rendering. Hand-rolled: `aria-labelledby`, `aria-describedby`, restore-focus to invoking button on close.

**D-07: Confirm/Cancel API shape.**
```typescript
export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;     // default: "Confirm"
  cancelLabel?: string;      // default: "Cancel"
  danger?: boolean;           // default: false — adds .danger class to confirm button
}
export async function showConfirm(opts: ConfirmOptions): Promise<boolean>;
```

**D-08: Razor partial signature.** `_AdminConfirmModal.cshtml` is structural-only — modal exists in DOM hidden; TS populates title/message/buttons at call time. Partial accepts NO model. Phase 16 placement: `Detail.cshtml` includes partial inline. Phase 22 may move the include to `_AdminLayout.cshtml` if every admin page needs it.

**D-09: NO scope creep.** In-scope: single Delete confirm at `Detail.cshtml:41`. Out-of-scope: any other confirm site, theming across guild themes, prompt()/alert() helpers, animation/transitions.

### Claude's Discretion

Within the locked surface — wiring details for `_AdminConfirmModal.cshtml` markup, TS module exposure pattern, CSS class names beyond the three listed in CONTEXT (`admin-modal`, `admin-modal__backdrop`, `admin-modal__panel`), exact insertion point in `admin.css`, focus-management implementation details.

### Deferred Ideas (OUT OF SCOPE)

- v1.5 backdrop suppression on destructive ops (revisit if UAT shows misclick problems)
- v1.5 `showPrompt({title, message, placeholder}): Promise<string|null>` helper
- v1.5 partial include moved into `_AdminLayout.cshtml`
- v2.0 npm focus-trap dep (only if native `<dialog>` proves insufficient)

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MODAL-01 | Admin can confirm destructive feedback actions via a styled focus-trapped native `<dialog>` modal (replaces deferred inline `onsubmit` confirm in `AdminFeedback/Detail.cshtml`; closes v1.3 WDG-04 override 2026-05-16) | All sections below — single REQ-ID maps to entire phase scope |

## Project Constraints (from CLAUDE.md)

| Constraint | Source | Applies to Phase 16? |
|------------|--------|---------------------|
| Codex codes, Claude reviews | Global CLAUDE.md (`Delegation` section) | YES — implementation routed through Codex |
| Codex reviews plans via `/gsd-review` before execute-phase | Global CLAUDE.md | YES — PLAN.md must pass Codex peer review before dispatch |
| Side-effects report before any code change | Global side-effects-check.md | YES — Codex must produce report before edits |
| No "Format Document" / no `[Attribute]` inline / no raw-string re-indent / no auto `init`→`get` | Project CLAUDE.md (formatting paranoia) | YES — Razor + TS + CSS edits MUST touch only the lines being added/changed |
| LF line endings preserved | `.gitattributes` (`*.cshtml`/`*.ts`/`*.css` text eol=lf) | YES — new files MUST be LF |
| Plain default-author commits, NO Co-Authored-By trailer | Project CLAUDE.md | YES |
| Public repo — no secrets in commits | Project CLAUDE.md | NO — Phase 16 has no secrets |
| 2-space indent for .ts/.css; 4-space for .cs/.cshtml/.razor | `.editorconfig` | YES |
| `{ get; init; }` preservation (System.Text.Json silent skip) | Project CLAUDE.md + .editorconfig | N/A — Phase 16 introduces no C# record types (D-08 partial is parameter-less) |
| Native `<dialog>`, NO focus-trap npm dep | SUMMARY.md invariant #10 | YES — locked by D-06 |
| Admin CSS scoped to `.admin-shell` parent, no unscoped element selectors | SUMMARY.md invariant #11, PITFALLS.md P10 | YES — locked by D-05 |
| VSTest unreliable in WSL → manual UAT only | Project CLAUDE.md | YES — see Manual UAT Steps section |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Modal DOM shell (dialog + buttons + ARIA attrs) | Razor partial (server-rendered HTML) | — | Single source of truth in `_AdminConfirmModal.cshtml`; structural only, no view model |
| Modal styling (backdrop, panel, focus ring, danger button variant) | Browser-side CSS (`admin.css`, scoped `.admin-shell`) | — | Standard CSS cascade; admin-only surface (zero guild-theme bleed risk) |
| Modal lifecycle (open, populate text, await user choice, close, return Promise) | Browser-side TypeScript (IIFE module) | — | All interaction is client-side; no server roundtrip — confirm is a UI gate before form submit |
| Form submission after confirm | Browser (existing `<form method="post">`) | API/Backend (`AdminFeedbackController.Apply`) | Existing server handler unchanged; client intercepts submit, runs confirm, calls `form.submit()` on confirm=true |
| Anti-forgery validation | API/Backend (`[ValidateAntiForgeryToken]` on `AdminFeedbackController.Apply`) | — | Form already includes `@Html.AntiForgeryToken()` (Detail.cshtml:42); confirm flow MUST preserve the existing token submission |

**No new tier crossings.** The modal is a pure browser-side intercept on a form whose server handler is unchanged. Anti-forgery token is already present and must remain present after wire-up.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Native HTML `<dialog>` | Baseline 2022 | Focus trap, ESC handling, ARIA role/modal, backdrop rendering | Locked by D-06; SUMMARY.md invariant #10; FEATURES.md Feature 6; eliminates focus-trap library dependency |
| TypeScript | 6.0.2 (already installed via `DeckFlow.Web/package.json`) | Type-safe IIFE module | Existing build pipeline (`tsc` invoked from `DeckFlow.Web.csproj` `CompileTypeScriptAssets` target) |
| Razor / ASP.NET 10 | net10.0 (pinned) | Partial view rendering via `@await Html.PartialAsync(...)` | Existing convention; matches `_DeckToolTabs.cshtml`, `_AiSelector.cshtml`, `_WorkflowStepTabs.cshtml` usage |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| None — Phase 16 adds ZERO new packages | — | — | Per D-06 + invariant #10 |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Native `<dialog>` | `focus-trap` npm package | Rejected by D-06 + SUMMARY.md invariant #10 — adds dep, increases bundle, fights the established pattern |
| Native `<dialog>` | Custom `<div role="dialog">` + hand-rolled focus walker | Rejected by PITFALLS.md Pitfall 9 — leaks focus through `df-select`/`df-typeahead` custom elements |
| `closedby="any"` HTML attribute (declarative light-dismiss) | Hand-rolled `dialog.addEventListener('click', e => …)` | `closedby` is too new to assume baseline support (per MDN); hand-rolled handler is 3 lines and works everywhere |
| Razor partial with model + typed view-component | Structural-only partial (D-08) | D-08 locks structural-only; matches D-04 reusability for Phase 22 without recompile |

**Installation:** None — Phase 16 adds zero packages.

**Version verification:** N/A — no new packages.

## Package Legitimacy Audit

**Skipped — Phase 16 installs zero new packages.** All capabilities use the existing stack (native browser APIs, in-repo TypeScript 6.0.2, ASP.NET 10 Razor). No npm/NuGet/PyPI/crates verification required.

## Native `<dialog>` Baseline (HIGH confidence)

Verified against MDN `<dialog>` reference (fetched 2026-05-23) and current PITFALLS.md Pitfall 9.

### What the browser handles automatically when opened via `showModal()`

| Behavior | Native? | Notes |
|----------|---------|-------|
| Sets `role="dialog"` implicitly | YES | No ARIA attribute needed |
| Sets `aria-modal="true"` implicitly | YES | Only when opened via `showModal()` (NOT `show()`) |
| Initial focus to first focusable element OR `[autofocus]` | YES | `[autofocus]` attribute on the element user should hit first overrides default |
| Tab + Shift+Tab cycle stays inside dialog | YES | Browser-enforced focus trap; works across all interactive descendants including custom elements |
| Escape key closes dialog | YES | Fires `close` event |
| Backdrop rendered above page content | YES | `::backdrop` pseudo-element available for styling |
| `inert` applied to rest of page (assistive tech sees only dialog) | YES | When using `showModal()` |
| Focus restored to invoking element when closed | YES | Browser remembers the element that had focus pre-`showModal()` and returns focus on close |

### What the browser does NOT do automatically (must hand-roll)

| Behavior | Why hand-roll | Lines of code |
|----------|---------------|---------------|
| **Backdrop click closes dialog** | `showModal()` defaults to `closedby="closerequest"` (ESC + explicit `.close()` only); backdrop click does NOTHING natively | ~3 lines: `dialog.addEventListener('click', e => { if (e.target === dialog) dialog.close('cancel'); })` (works because the dialog element itself fills the viewport above content; clicks on inner panel bubble from panel, clicks on backdrop area target the dialog itself) |
| `aria-labelledby` → title element id | Required for screen-reader announce of dialog title | 1 attribute |
| `aria-describedby` → message element id | Required for screen-reader announce of dialog body | 1 attribute |
| Confirm/Cancel button click → resolve Promise | Promise wrapper around `close` event | ~5 lines (see API skeleton below) |
| Differentiating confirm vs cancel close reason | `dialog.returnValue` is empty unless set; need to distinguish "user clicked Confirm" from "user pressed ESC / clicked Cancel / clicked backdrop" | Set `dialog.returnValue = 'confirm'` or `'cancel'` before calling `dialog.close()`; read on close event |

### Browser baseline (HIGH confidence — MDN verified 2026-05-23)

| Browser | Minimum version | Date |
|---------|-----------------|------|
| Chrome | 37 | 2014 |
| Firefox | 98 | 2022-03 |
| Safari | 15.4 | 2022-03 |

Baseline marker: "Widely available" since March 2022 — exceeds DeckFlow's "evergreen browsers ≥2 years old" target by a comfortable margin. **NO polyfill needed.**

### Correction to CONTEXT D-03 wording

CONTEXT D-03 says *"Click-outside cancels (standard native `<dialog>` UX)"* — this is wrong. Native `<dialog>` opened via `showModal()` does **not** close on backdrop click by default. The behavior D-03 describes (and the user expects) requires either a hand-rolled `click` handler on the dialog element OR the new `closedby="any"` attribute (which lacks broad baseline support as of 2026-05). The hand-rolled handler is the correct path; it's 3 lines and ships in `admin-modal.ts`. **This is not a design change — the user's intent (D-03) is preserved; only the implementation is one line larger than D-06 implies.**

### Correction to CONTEXT D-06 wording

CONTEXT D-06 lists *"Restore-focus to invoking button on close (store `document.activeElement` before `showModal()`; call `.focus()` on stored ref in `close` event handler)"* as a hand-rolled add. Native `<dialog>` **already restores focus to the invoking element** when opened via `showModal()`. The hand-rolled store-and-restore is a defensive fallback for edge cases (trigger element removed from DOM during the await, or trigger hidden via CSS) but is not strictly required for the happy path. **Recommendation: keep the hand-rolled store-and-restore as a safety net — it is ~2 lines and protects against future re-render edge cases — but document it as belt-and-suspenders, not as filling a browser gap.**

## TS Module Sharing Pattern (DeckFlow convention) [VERIFIED: codebase grep at HEAD 65f2fe4]

`DeckFlow.Web/tsconfig.json` is `"module": "none"`. This means:
- ES module `import`/`export` keywords compile-fail (no module loader at runtime)
- Every `wwwroot/ts/*.ts` file emits a standalone `wwwroot/js/*.js` script
- Files share code via the global `window` object using a declaration-merge pattern

### Canonical pattern (verified in `df-select.ts:1-6, 837-839`, `deck-sync.ts:2542-2548`, `category-suggestions.ts:16-18`)

```typescript
// admin-modal.ts (NEW)

interface Window {
  DeckFlowAdminModal?: {
    showConfirm?: (opts: ConfirmOptions) => Promise<boolean>;
  };
}

interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
}

((): void => {
  'use strict';

  const showConfirm = async (opts: ConfirmOptions): Promise<boolean> => {
    // ... implementation reads the partial-rendered <dialog> from DOM, populates,
    //     showModal(), awaits close, returns boolean.
  };

  window.DeckFlowAdminModal = window.DeckFlowAdminModal ?? {};
  window.DeckFlowAdminModal.showConfirm = showConfirm;
})();
```

### Consumer pattern in `admin-feedback.ts` (additive edit; existing IIFE preserved)

```typescript
// Within the existing IIFE at admin-feedback.ts
const showConfirm = window.DeckFlowAdminModal?.showConfirm;
if (!showConfirm) { return; }  // admin-modal.js not loaded — bail gracefully

// Attach to delete form submit
deleteForm.addEventListener('submit', async (e: SubmitEvent) => {
  e.preventDefault();
  const confirmed = await showConfirm({
    title: 'Delete Feedback',
    message: `Delete feedback #${id} permanently?`,
    confirmLabel: 'Delete',
    danger: true,
  });
  if (confirmed) { deleteForm.submit(); }
});
```

### Correction to CONTEXT D-01 wording

CONTEXT D-01 says *"`admin-feedback.ts` will import `showConfirm`"* using `import` syntax in D-07's pseudo-code. **There is no `import`/`export` in this codebase** because `module: "none"`. The functionally equivalent pattern is:
- `admin-modal.ts` exposes `showConfirm` on `window.DeckFlowAdminModal`
- `admin-feedback.ts` reads `window.DeckFlowAdminModal?.showConfirm` defensively
- Razor view loads `admin-modal.js` **before** `admin-feedback.js` via two `<script>` tags in the `Scripts` RenderSection

The D-01 *intent* (reusable helper consumable from multiple admin TS modules) is preserved; only the literal `import` keyword cannot be used.

### Script include order in `AdminFeedback/Detail.cshtml` (new `Scripts` section)

Current `Detail.cshtml` has NO `@section Scripts`. Phase 16 adds one matching `AdminFeedback/Index.cshtml:100-103`:

```razor
@section Scripts
{
    <script src="~/js/admin-modal.js" asp-append-version="true"></script>
    <script src="~/js/admin-feedback.js" asp-append-version="true"></script>
}
```

Order matters: `admin-modal.js` declares `window.DeckFlowAdminModal.showConfirm` and must load first. `admin-feedback.js` reads it inside `DOMContentLoaded` (which fires after both scripts parse).

## Partial Convention [VERIFIED: codebase grep + .editorconfig]

`Views/Shared/_AdminConfirmModal.cshtml` follows existing shared-partial conventions verified in:
- `_DeckToolTabs.cshtml` — takes model (`@model DeckPageTab`)
- `_AiSelector.cshtml` — takes model (`@model string`) + injects services via `@inject`
- `_WorkflowStepTabs.cshtml` — partial pattern, layout chrome
- `_BusyIndicator.cshtml`, `_FormError.cshtml` — likely structural-only candidates (closest analogs to D-08 model-less pattern)

### D-08 structural-only signature

```razor
@* _AdminConfirmModal.cshtml — singleton modal DOM template.
   Structural only; TS (admin-modal.ts) populates title/message/buttons at call time.
   Used by AdminFeedback/Detail.cshtml v1.4 Phase 16; reusable by Phase 22 ContentSources delete. *@
<dialog id="admin-confirm-modal" class="admin-modal" aria-labelledby="admin-confirm-modal__title" aria-describedby="admin-confirm-modal__message">
    <div class="admin-modal__panel">
        <h2 id="admin-confirm-modal__title" class="admin-modal__title"></h2>
        <p id="admin-confirm-modal__message" class="admin-modal__message"></p>
        <div class="admin-modal__actions">
            <button type="button" class="admin-modal__cancel" data-admin-modal-cancel>Cancel</button>
            <button type="button" class="admin-modal__confirm" data-admin-modal-confirm autofocus>Confirm</button>
        </div>
    </div>
</dialog>
```

Key points:
- No `@model` directive (D-08).
- No `@Html.AntiForgeryToken()` inside the dialog — modal is a UI gate, not a form; the *outer* `<form>` in `Detail.cshtml:40-44` already carries the token (line 42), and `form.submit()` ships it. **Anti-forgery semantics are preserved without touching the partial.**
- `id` attributes on title + message power `aria-labelledby` / `aria-describedby` (D-06 hand-rolled requirement).
- `data-admin-modal-cancel` + `data-admin-modal-confirm` attributes are the TS query hooks (consistent with WDG-04 `data-admin-feedback-submit-on-change` data-attribute convention from `admin-feedback.ts:13`).
- `autofocus` on Confirm makes the initial focus deterministic. **Note for danger variant:** when `danger=true`, consider moving `autofocus` to Cancel button instead so destructive default isn't pre-selected — flag for planner discussion. CONTEXT.md does not lock this; falls under Claude's Discretion.
- `<dialog>` element placement: include the partial **once** per page that needs confirms. Phase 16 = `Detail.cshtml`. Future phases may move the include to `_AdminLayout.cshtml` (D-08 explicit forecast); Phase 16 does NOT do that (CONTEXT D-09 "no scope creep" + "Files NOT to touch" list).

### Razor `PartialAsync` call site in `Detail.cshtml`

Replace the current Delete form block (lines 39-44):

```razor
@* CURRENT lines 39-44 — DEFERRED v1.3 WDG-04 *@
@* Deferred: inline onsubmit confirm() retained per Phase 11 D-05; v1.4 will replace... *@
<form method="post" asp-action="Apply" asp-route-id="@Model.Id" asp-route-op="delete" class="admin-action-form"
      onsubmit="return confirm('Delete feedback #@Model.Id permanently?');">
    @Html.AntiForgeryToken()
    <button type="submit" class="danger">Delete</button>
</form>
```

with (after Phase 16):

```razor
<form method="post" asp-action="Apply" asp-route-id="@Model.Id" asp-route-op="delete"
      class="admin-action-form" data-admin-confirm-delete data-admin-feedback-id="@Model.Id">
    @Html.AntiForgeryToken()
    <button type="submit" class="danger">Delete</button>
</form>
```

…and at the bottom of the `<section>` (or just before its closing tag):

```razor
@await Html.PartialAsync("_AdminConfirmModal")
```

The `data-admin-confirm-delete` attribute is the `admin-feedback.ts` hook for attaching the confirm-then-submit handler. `data-admin-feedback-id` carries `@Model.Id` into the JS without inline `@`-interpolation in a `<script>` block (CSP-clean per WDG-04 lineage). The `@Html.AntiForgeryToken()` stays exactly where it is — preserved across the refactor — so `[ValidateAntiForgeryToken]` on `AdminFeedbackController.Apply` keeps working unchanged (verified existing behavior — see PITFALLS.md Pitfall 11 for the "two CSRF mechanisms" discipline).

## .admin-shell CSS Scoping [VERIFIED: _AdminLayout.cshtml:18 + admin.css:71-79]

**Key finding: `.admin-shell` wrapper ALREADY EXISTS in `_AdminLayout.cshtml` at line 18.**

```html
<div class="admin-shell">
    <aside class="admin-sidebar" aria-label="Admin sections">...</aside>
    <header class="admin-topbar">...</header>
    <main id="admin-content" class="admin-content" role="main">
        @RenderBody()
    </main>
</div>
```

This means:
1. **NO new wrapper markup needed in Phase 16.** Phase 18 does not need to add `.admin-shell` first (it's already there since the Phase 21 v1.1 admin shell).
2. **`<dialog id="admin-confirm-modal">` rendered via `@RenderBody()` lives INSIDE `.admin-shell`** in the DOM tree — selectors `.admin-shell .admin-modal { … }` work as expected.
3. **`admin.css` is admin-only by load path** (only `_AdminLayout.cshtml:14` links it; `_Layout.cshtml` does not) — so technically unscoped `.admin-modal { … }` selectors are safe for guild-theme bleed, BUT SUMMARY.md invariant #11 and PITFALLS.md Pitfall 10 require `.admin-shell`-scoped selectors as the discipline regardless, because Phase 18's `admin-common.css` factoring may eventually be loaded outside `_AdminLayout` in some scenarios.

### Scoping pattern for new CSS

```css
/* admin.css — scoped to admin-shell parent per SUMMARY.md invariant #11 + PITFALLS.md P10 */
.admin-shell .admin-modal {
    padding: 0;
    border: 1px solid var(--border);
    border-radius: 6px;
    background: var(--panel);
    color: var(--text);
    max-width: 480px;
    /* … */
}
.admin-shell .admin-modal::backdrop {
    background: rgba(0, 0, 0, 0.6);
}
.admin-shell .admin-modal__panel { padding: 20px 24px; }
.admin-shell .admin-modal__title { margin: 0 0 12px; font-size: 16px; font-weight: 600; }
.admin-shell .admin-modal__message { margin: 0 0 20px; color: var(--text); }
.admin-shell .admin-modal__actions { display: flex; gap: 8px; justify-content: flex-end; }
.admin-shell .admin-modal__cancel,
.admin-shell .admin-modal__confirm {
    /* match .admin-action-form button styles from admin.css:137 */
    background: var(--accent); color: #fff; border: none; padding: 6px 12px;
    border-radius: 3px; cursor: pointer; font-size: 13px;
}
.admin-shell .admin-modal__confirm.danger { background: #b91c1c; /* danger red — verify against existing token */ }
```

**Token reuse:** `admin.css:4-17` already defines `--bg`, `--panel`, `--text`, `--muted`, `--accent`, `--border`, `--focus`. New modal CSS uses these directly. No new tokens added in Phase 16 (Phase 18 may add tokens during `admin-common.css` factoring).

**`:focus-visible` inheritance:** `admin.css:23-32` already applies `outline: 2px solid var(--focus); outline-offset: 2px;` to `button:focus-visible`. Modal buttons inherit this for free — no additional focus-ring CSS needed.

**Danger button styling:** The existing `Detail.cshtml` Delete button uses `class="danger"` (line 43), but **`admin.css` has NO `.danger` rule** (`grep -c "\.danger" admin.css` returns 0). The existing Delete button is currently just a default `.admin-action-form button`. Phase 16 needs to either (a) introduce a `.admin-shell .admin-action-form button.danger { background: #b91c1c; }` rule to make the inline Delete button visually distinct, OR (b) accept that `.danger` is dormant. Recommend (a) for Phase 16 since the modal's `confirmLabel: 'Delete'` confirm button uses `danger: true`; consistent danger styling between the in-page Delete button and the modal Confirm button is a UX expectation. **Flag for planner.**

## ARIA + Focus Management Checklist

Verified against MDN `<dialog>` + WCAG 2.1.2 (No Keyboard Trap) + 2.4.3 (Focus Order) + PITFALLS.md Pitfall 9.

| Requirement | Source | How satisfied | Notes |
|-------------|--------|---------------|-------|
| `role="dialog"` | WCAG | Native (implicit on `<dialog>`) | Do not add explicit `role` attribute |
| `aria-modal="true"` | WCAG | Native when opened via `showModal()` | Do not add explicit attribute |
| `aria-labelledby` → title element id | WCAG SC 4.1.2 | Hand-rolled — partial sets `aria-labelledby="admin-confirm-modal__title"` | Required so SR announces title on open |
| `aria-describedby` → message element id | WCAG SC 4.1.2 | Hand-rolled — partial sets `aria-describedby="admin-confirm-modal__message"` | Required so SR announces body on open |
| Initial focus to a sensible element | WCAG 2.4.3 | Native — `[autofocus]` on Confirm button | **Discretion:** consider Cancel for danger variants |
| Tab cycles within dialog | WCAG 2.1.2 | Native | Works through `df-select`/`df-typeahead` custom elements (per Pitfall 9 fix — native `<dialog>` handles them) |
| Shift+Tab from first element cycles to last | WCAG 2.1.2 | Native | Verified by Pitfall 9 manual UAT step 3 |
| ESC closes dialog | WCAG 2.1.2 | Native — fires `close` event | TS reads `dialog.returnValue` ('cancel' if not set) and resolves Promise as `false` |
| Cancel button closes dialog | UX | Hand-rolled click listener on `[data-admin-modal-cancel]` → `dialog.returnValue = 'cancel'; dialog.close()` | Promise resolves `false` |
| Backdrop click closes dialog (per D-03) | UX (D-03) | Hand-rolled — `dialog.addEventListener('click', e => { if (e.target === dialog) { dialog.returnValue='cancel'; dialog.close(); } })` | Native default is "do nothing"; D-03 wants close — must hand-roll. Works because clicks on inner `.admin-modal__panel` target the panel, clicks on backdrop area target the dialog itself |
| Confirm button submits | UX | Hand-rolled click listener on `[data-admin-modal-confirm]` → `dialog.returnValue = 'confirm'; dialog.close()` | Promise resolves `true` |
| Focus returns to invoking element on close | WCAG 2.4.3 | Native (browser remembers pre-`showModal()` activeElement) | **Defense-in-depth:** also store `document.activeElement` ref before `showModal()` and `.focus()` it on close in case trigger was detached during the await. ~2 lines |
| Page behind dialog inert to assistive tech | WCAG | Native `inert` semantics when `showModal()` | No explicit `inert` attribute needed on other content |
| `prefers-reduced-motion` respected | WCAG 2.3.3 | Phase 16 adds no animation (D-09 explicit out-of-scope) | If a future phase adds `transition: opacity`, it must wrap in `@media (prefers-reduced-motion: no-preference) { … }` |

### Pitfall 9 specific verification (from PITFALLS.md:242-268)

Pitfall 9's failure mode: hand-rolled focus walkers leak focus through custom-element descendants like `df-select` and `df-typeahead`. Native `<dialog>` does NOT have this bug because the focus trap is enforced by the browser's top-layer engine, not by a userland walker. **No additional mitigation needed beyond using `showModal()`** — but the manual UAT must still verify this (see Manual UAT Steps below) because Phase 16's Detail.cshtml DOES contain a `<dl>` with form buttons; future Razor views consuming `_AdminConfirmModal.cshtml` may nest `df-select`/`df-typeahead` inside the dialog (e.g., a future Phase 22 "Edit ContentSource → Confirm rename" flow), and the partial must work correctly in that case.

## admin.css Insertion-Point Safety

`admin.css` is 193 lines, structured chronologically by phase (Phase 21 admin shell foundations → Phase 23 Analytics → Phase 11 WDG audit fixes). Reading the file:

| Lines | Section | Last touched |
|-------|---------|--------------|
| 1-3 | File header | Phase 21 |
| 4-32 | `:root` tokens + universal `:focus-visible` ring | Phase 11 (WDG-01 Sweep 2) |
| 34-44 | `* { box-sizing }` + html/body | Phase 21 |
| 46-47 | Anchor styles | Phase 21 |
| 49-53 | `.skip-link` | Phase 21 |
| 55-68 | `.sr-only` utility | Phase 11 (WDG-06 Sweep 6) |
| 70-122 | `.admin-shell` grid + sidebar + topbar + content | Phase 21 |
| 124-125 | `.admin-banner` | Phase 21 |
| 127-138 | `.admin-table` + `.admin-action-form` | Phase 21 + Phase 11 (Sweep 2 tabular-nums) |
| 140-145 | `.maintenance-page` (FLAG-05) | Phase 21 |
| 147-193 | Phase 23 Analytics (`.admin-page-header`, `.admin-analytics`, `.admin-analytics-table`, `.admin-sparkline`, `.admin-empty`) | Phase 23 |

### Recommended insertion point

**Append at the end of the file (after line 193), as a new section.** Rationale:
1. **Zero risk of formatter re-indent on unrelated rules.** Adding lines at file-end touches no existing block.
2. **Chronological convention.** Existing file is organized by phase; v1.4 Phase 16 additions land after v1.1 Phase 23 additions.
3. **Section header comment matches existing pattern.** Use a `/* === Phase 16 (v1.4) — WDG-04 Focus-Trapped Modal === */` header mirroring `/* === WDG audit fixes (WDG-01, Phase 11 Sweep 2) === */` style at lines 11-14, 19-22, 55-57, 130-133.
4. **Phase 18 extraction is mechanical.** When Phase 18 factors `admin.css → admin-common.css + admin-mobile.css + admin.css shim` (per D-05), Phase 16's modal block at file-end is a contiguous, easily-grep-and-move section.

**Anti-pattern to avoid:** inserting in the middle of the file (e.g., next to `.admin-action-form` at line 136) — this risks formatter re-indent on adjacent lines and violates CLAUDE.md "touch only lines that need touching".

### Formatter safety checks for the planner

- [ ] All new CSS lines use 2-space indent (per `.editorconfig` `[*.{css,...}] indent_size = 2`)
- [ ] No `Format Document` run on `admin.css` after edit
- [ ] LF line endings preserved (`.gitattributes` `*.css text eol=lf`)
- [ ] `grep -cE '^\.[a-z]' admin.css` count BEFORE and AFTER — delta should equal exactly the number of new top-level selectors added (no unintended deletions / duplicates)

## Manual UAT Steps [LOCKED — VSTest unreliable in WSL per CLAUDE.md]

All Phase 16 acceptance verification is manual UAT. Steps below cover ROADMAP Phase 16 SCs #1-4 plus PITFALLS.md Pitfall 9 detection list.

### Pre-UAT setup

1. User starts the dev server (per MEMORY: never auto-launch): `dotnet run --project DeckFlow.Web` from a Windows shell or `scripts/run-web.sh` from WSL.
2. Browser: latest evergreen Chrome / Firefox / Safari (per phase's stated baseline; minimum versions per Browser baseline table above).
3. Navigate to `https://localhost:7173/Admin/Feedback` (or `http://localhost:5173/Admin/Feedback`), authenticate via BasicAuth, click into any feedback row's Detail page.

### UAT-1 — Confirm dialog opens via `<dialog>` showModal (SC #1)

- Click the Delete button.
- VERIFY: a styled modal appears with title "Delete Feedback", message "Delete feedback #N permanently?", Cancel button, Delete button.
- VERIFY (DevTools): the `<dialog id="admin-confirm-modal">` element has `open` attribute set and is in the top layer (Elements panel shows it elevated).
- VERIFY (DevTools Network): NO new request for any focus-trap npm package; only `admin.css`, `admin-modal.js`, `admin-feedback.js` from `~/css/`, `~/js/` paths.

### UAT-2 — Tab cycling stays inside modal (SC #2, Pitfall 9 detection step 2-3)

- With modal open, press Tab repeatedly.
- VERIFY: focus cycles through Cancel → Delete → Cancel → Delete (or includes the dialog itself per browser quirk); focus never leaves the modal to reach the underlying page Back link, Mark Read / Archive form buttons, or any other interactive element on the Detail page.
- Press Shift+Tab from the first focusable element.
- VERIFY: focus moves to the last focusable element inside the modal (reverse cycle works).
- VERIFY (DevTools Console): `document.activeElement` always returns a descendant of `<dialog id="admin-confirm-modal">`.

### UAT-3 — ESC closes + restores focus (SC #3, Pitfall 9 detection step 4)

- With modal open, press ESC.
- VERIFY: modal closes.
- VERIFY: focus returns to the Delete button that opened the modal (verify via DevTools Console: `document.activeElement === document.querySelector('[data-admin-confirm-delete] button[type=submit]')`).
- VERIFY: no `POST /Admin/Feedback/Apply/...?op=delete` was sent (Network panel shows no delete request).

### UAT-4 — Cancel button closes (SC #3)

- Click Delete → modal opens.
- Click Cancel.
- VERIFY: modal closes, focus returns to Delete button, no POST sent.

### UAT-5 — Backdrop click closes (SC #3, D-03)

- Click Delete → modal opens.
- Click anywhere outside the panel (in the dimmed backdrop area).
- VERIFY: modal closes, focus returns to Delete button, no POST sent.
- VERIFY (DevTools): clicks on the panel content (title / message / Cancel button area) do NOT close the dialog.

### UAT-6 — Confirm submits (functional happy path)

- Click Delete → modal opens.
- Click Delete (the modal's confirm button).
- VERIFY: modal closes.
- VERIFY: form POST goes to `/Admin/Feedback/Apply/{id}?op=delete` with valid `__RequestVerificationToken`.
- VERIFY: feedback record is deleted (page redirects to `/Admin/Feedback` list, deleted row no longer present).

### UAT-7 — CSS scope verification (SC #4, Pitfall 10 detection)

- In DevTools, find any non-admin guild-themed page (homepage `/` rendered in Rakdos / Azorius / Boros / Gruul theme).
- VERIFY: page does NOT contain any element matching `.admin-modal`, `.admin-shell .admin-modal`, or related selectors (sanity — modal CSS scoped to admin-shell which doesn't exist on public pages).
- Open homepage → DevTools → Elements → Inspect `<body>`.
- VERIFY: no inherited / cascaded styling from any new `.admin-modal*` rules (Computed panel shows no `border-radius: 6px` or similar modal-specific properties on `<body>`, `<button>`, or `<table>` from the new CSS).
- VERIFY visual: homepage renders identically pre- and post-Phase-1 deployment (no pixel diff in any guild theme).

### UAT-8 — Screen-reader smoke (SC #2 a11y, Pitfall 9 detection step 5)

- Optional but recommended: enable NVDA (Windows) or VoiceOver (Mac).
- Open Detail page, navigate to Delete button via Tab, press Enter.
- VERIFY: SR announces "Delete Feedback, dialog" (or browser-equivalent) on open — confirms `aria-labelledby` is firing.
- VERIFY: SR announces the message "Delete feedback #N permanently?" — confirms `aria-describedby`.
- VERIFY: SR ignores Mark Read / Archive form buttons while modal open (page behind is inert).
- Press ESC.
- VERIFY: SR returns to reading Delete button context.

### UAT-9 — Nested custom-element interaction (Pitfall 9 forward-looking)

Phase 16's `Detail.cshtml` does NOT nest `df-select` / `df-typeahead` inside the modal. This UAT step is **forward-looking** for Phase 22 reuse — document the test plan now so Phase 22 inherits it. Skip for Phase 16 UAT execution. (Phase 22 plan-checker SHOULD verify the partial works correctly when consumed by a view containing nested custom elements.)

## Runtime State Inventory

Phase 16 is a UI replacement, not a rename / refactor / migration. No runtime state has the term "deferred confirm" or `onsubmit` stored elsewhere.

| Category | Items found | Action required |
|----------|-------------|------------------|
| Stored data | None — no database stores "onsubmit" or "confirm" strings | None |
| Live service config | None — no external service config references the modal | None |
| OS-registered state | None — no Task Scheduler / cron / pm2 reference | None |
| Secrets/env vars | None — modal needs no secrets, no env vars | None |
| Build artifacts | `wwwroot/js/admin-feedback.js` will be regenerated by the MSBuild `CompileTypeScriptAssets` target on next build; `wwwroot/js/admin-modal.js` is a NEW file the build will produce. **Both must be committed** (project convention: `wwwroot/js/*.js` is git-tracked per CLAUDE.md's note that TypeScript output is tracked) | Verify `git status` shows both `wwwroot/ts/admin-modal.ts` AND `wwwroot/js/admin-modal.js` after build; commit both |

**Nothing else found** — verified by `grep -rin "deferred\|onsubmit\|confirm(" .planning/ DeckFlow.Web/` produces only the planning artifacts + the literal target line.

## Common Pitfalls

### Pitfall 1: Hand-rolling focus trap instead of using `<dialog>` showModal()
**What goes wrong:** Tab leaks through custom elements (`df-select`, `df-typeahead`); ESC handler clashes with child component's own ESC handler; SR reads page behind modal.
**Source:** PITFALLS.md Pitfall 9.
**How to avoid:** Locked by D-06 — use native `<dialog>` + `showModal()`. The Pitfall 9 failure mode is structurally impossible with native browser implementation.
**Warning sign:** Any new `wwwroot/ts/*` file introducing a `keydown` Tab handler or focus-walker logic.

### Pitfall 2: Unscoped CSS bleeds into 22 guild themes
**What goes wrong:** A selector like `dialog { padding: 0; }` or `.modal { … }` in `admin.css` matches a hypothetical future public page's `<dialog>` element.
**Source:** PITFALLS.md Pitfall 10, SUMMARY.md invariant #11.
**How to avoid:** Every new admin selector starts with `.admin-shell` (verified pattern); BEM block names use `admin-` prefix; element-level selectors like `dialog { … }` are FORBIDDEN.
**Warning sign:** Plan-checker grep `grep -E '^[a-z]' admin.css | grep -v '\.admin-shell'` returns non-zero on new lines.

### Pitfall 3: `module: "none"` ES import compile failure
**What goes wrong:** Following D-01 / D-07 pseudo-code literally and writing `export async function showConfirm(...)` in `admin-modal.ts` — tsc compile fails because there's no module loader at runtime.
**Source:** `tsconfig.json:4`, codebase grep (zero `export` statements in `wwwroot/ts/`).
**How to avoid:** Use the `window.DeckFlowAdminModal` IIFE pattern documented above; mirror `df-select.ts:837-839`.
**Warning sign:** First `tsc` build run fails with `error TS1148: Cannot compile modules unless the '--module' flag is provided.`

### Pitfall 4: Script include order in `Detail.cshtml`
**What goes wrong:** `admin-feedback.js` loads before `admin-modal.js`; `window.DeckFlowAdminModal` is undefined when `admin-feedback.ts` reads it on `DOMContentLoaded`; delete button is silently broken (early-return).
**Source:** D-01 ordering implication.
**How to avoid:** Two `<script>` tags in the `@section Scripts` block, `admin-modal.js` FIRST. `admin-feedback.ts` defensively checks `if (!showConfirm) return;`.
**Warning sign:** Manual UAT step 1 fails — Delete button does nothing (no modal opens, no form submit).

### Pitfall 5: Stripping `@Html.AntiForgeryToken()` during refactor
**What goes wrong:** Refactor of the Delete form drops the token; POST returns 400 from `[ValidateAntiForgeryToken]`; feedback delete silently broken.
**Source:** PITFALLS.md Pitfall 11 ("two CSRF mechanisms"), existing `AdminFeedbackController:69`.
**How to avoid:** UAT step 6 explicitly verifies the POST body contains `__RequestVerificationToken`; diff review checks `@Html.AntiForgeryToken()` is preserved in the new form markup.
**Warning sign:** Manual UAT step 6 fails with a 400 page on Delete click.

### Pitfall 6: `closedby="any"` attribute used instead of hand-rolled backdrop handler
**What goes wrong:** Implementer reads MDN, sees `closedby="any"` provides declarative light-dismiss, uses it. Older browsers in DeckFlow's stated evergreen baseline don't yet support it; feature silently degrades to ESC-only close on some browsers.
**Source:** MDN — `closedby` is newer than core `<dialog>` baseline.
**How to avoid:** Use the 3-line hand-rolled `dialog.addEventListener('click', ...)` handler; works in every browser that supports `showModal()` (i.e., the full baseline).
**Warning sign:** Backdrop click silently doesn't close on one browser but works on another during UAT step 5.

### Pitfall 7: Phase 16 reaches into `_AdminLayout.cshtml`
**What goes wrong:** Implementer "helpfully" moves the partial include into `_AdminLayout.cshtml` so all admin pages get it. Phase 18 ALSO touches `_AdminLayout.cshtml` for sidebar disclosure work. Merge conflict / scope creep.
**Source:** CONTEXT.md D-08 explicit "Phase 16 placement: `Detail.cshtml` includes partial inline"; CONTEXT.md "Files NOT to touch" lists `_AdminLayout.cshtml`.
**How to avoid:** Include partial ONLY in `Detail.cshtml`. Phase 22 (per D-08 forecast) may move it later.
**Warning sign:** Plan-checker sees a diff in `_AdminLayout.cshtml` for Phase 16.

## Code Examples

Verified patterns from in-codebase sources at HEAD 65f2fe4.

### IIFE module exposing globals (mirrors `df-select.ts:1-6, 837-839`)

```typescript
// admin-modal.ts — Phase 16 v1.4
// Reusable admin confirm-dialog primitive built on native HTML <dialog>.
// Consumers: admin-feedback.ts (v1.4 Phase 16), future Phase 22 ContentSources delete.

interface Window {
  DeckFlowAdminModal?: {
    showConfirm?: (opts: ConfirmOptions) => Promise<boolean>;
  };
}

interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  danger?: boolean;
}

((): void => {
  'use strict';

  const showConfirm = (opts: ConfirmOptions): Promise<boolean> => {
    return new Promise<boolean>((resolve) => {
      const dialog = document.querySelector<HTMLDialogElement>('#admin-confirm-modal');
      if (!dialog) {
        // Partial not rendered on this page — fail closed (treat as cancel).
        resolve(false);
        return;
      }

      const titleEl = dialog.querySelector<HTMLElement>('#admin-confirm-modal__title');
      const messageEl = dialog.querySelector<HTMLElement>('#admin-confirm-modal__message');
      const confirmBtn = dialog.querySelector<HTMLButtonElement>('[data-admin-modal-confirm]');
      const cancelBtn = dialog.querySelector<HTMLButtonElement>('[data-admin-modal-cancel]');

      if (!titleEl || !messageEl || !confirmBtn || !cancelBtn) {
        resolve(false);
        return;
      }

      titleEl.textContent = opts.title;
      messageEl.textContent = opts.message;
      confirmBtn.textContent = opts.confirmLabel ?? 'Confirm';
      cancelBtn.textContent = opts.cancelLabel ?? 'Cancel';
      confirmBtn.classList.toggle('danger', opts.danger === true);

      // Defense-in-depth focus restore (browser does this natively too).
      const previouslyFocused = document.activeElement as HTMLElement | null;

      const onConfirm = (): void => {
        dialog.returnValue = 'confirm';
        dialog.close();
      };
      const onCancel = (): void => {
        dialog.returnValue = 'cancel';
        dialog.close();
      };
      const onBackdropClick = (event: MouseEvent): void => {
        if (event.target === dialog) { onCancel(); }
      };
      const onClose = (): void => {
        confirmBtn.removeEventListener('click', onConfirm);
        cancelBtn.removeEventListener('click', onCancel);
        dialog.removeEventListener('click', onBackdropClick);
        dialog.removeEventListener('close', onClose);
        previouslyFocused?.focus();
        resolve(dialog.returnValue === 'confirm');
      };

      confirmBtn.addEventListener('click', onConfirm);
      cancelBtn.addEventListener('click', onCancel);
      dialog.addEventListener('click', onBackdropClick);
      dialog.addEventListener('close', onClose);

      dialog.returnValue = '';
      dialog.showModal();
    });
  };

  window.DeckFlowAdminModal = window.DeckFlowAdminModal ?? {};
  window.DeckFlowAdminModal.showConfirm = showConfirm;
})();
```

### Consumer wire-up (additive edit to `admin-feedback.ts`)

```typescript
// Within the existing IIFE at admin-feedback.ts, after the existing
// data-admin-feedback-submit-on-change wiring (which stays unchanged):

document.querySelectorAll<HTMLFormElement>('[data-admin-confirm-delete]').forEach((form) => {
  form.addEventListener('submit', async (event: SubmitEvent) => {
    event.preventDefault();
    const showConfirm = window.DeckFlowAdminModal?.showConfirm;
    if (!showConfirm) {
      // admin-modal.js not loaded; fail closed.
      return;
    }
    const id = form.dataset.adminFeedbackId ?? '?';
    const confirmed = await showConfirm({
      title: 'Delete Feedback',
      message: `Delete feedback #${id} permanently?`,
      confirmLabel: 'Delete',
      danger: true,
    });
    if (confirmed) {
      form.submit();
    }
  });
});
```

## State of the Art (HIGH confidence — MDN verified 2026-05-23)

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `<div role="dialog">` + JS focus-trap library (focus-trap, react-modal) | Native `<dialog>` + `showModal()` | Baseline March 2022 | Zero npm deps; correct focus-trap across custom elements; native ESC + `inert` semantics |
| Backdrop close via custom event handler | `closedby="any"` declarative attribute | 2024-2025 (newer, lower baseline) | Use hand-rolled handler in 2026 for broad compatibility; revisit declarative attr in v1.5+ |
| `inert` polyfill (`wicg-inert`) | Native `inert` attribute + auto-applied to non-dialog content when `showModal()` | 2022 baseline | No polyfill needed for DeckFlow's evergreen target |

**Deprecated / outdated:**
- `dialog.show()` (non-modal) — Phase 16 explicitly uses `showModal()` for the focus-trap + `inert` semantics.
- `tabindex="0"` on `<dialog>` — MDN explicitly says "Do not add `tabindex` property to `<dialog>` element" (the partial does not).

## Assumptions Log

| # | Claim | Section | Risk if wrong |
|---|-------|---------|---------------|
| A1 | The `<form>` element wrapping the Delete button at `Detail.cshtml:40-44` is still posting to a `[ValidateAntiForgeryToken]`-decorated controller action (`AdminFeedbackController.Apply`) | Partial Convention; Architectural Responsibility Map | If wrong, the refactor would silently break CSRF protection. Mitigation: UAT step 6 explicitly verifies token POST body; Codex side-effects report MUST inventory the controller action attributes before editing |
| A2 | The danger-red color for `.danger` confirm button can use a literal `#b91c1c` value if no `--danger` token exists | .admin-shell CSS Scoping; Code Examples | Low — Phase 16 may add one new color value; planner can verify by `grep` `--danger\|--error\|--destructive` against `admin.css` and `site-common.css` before locking the exact hex |
| A3 | The browser's native focus-restoration on `<dialog>` close is reliable enough that the hand-rolled `previouslyFocused?.focus()` is genuinely belt-and-suspenders rather than a real gap-filler | ARIA + Focus Management Checklist; Code Examples | Low — keeping it costs 2 lines and provides defensive value; removing it would not regress UAT step 3 |
| A4 | Phase 16 does not need to add the partial include to any view other than `Detail.cshtml` because no other admin Razor view in the current tree contains a destructive-action form requiring confirm (`grep onsubmit Views/Admin*/` returns only the literal target line) | D-09 NO scope creep; Pitfall 7 | If wrong (some other admin view has an `onsubmit` confirm), the scope creep concern surfaces in Phase 22. Phase 16 should NOT proactively fix any other site |
| A5 | `wwwroot/js/admin-modal.js` will be checked in to git after build (per the existing pattern where `wwwroot/js/*.js` is git-tracked) | Runtime State Inventory | If the project later switches to gitignoring `wwwroot/js/*`, the Phase 16 commit would carry an unnecessary `.js` file; harmless |
| A6 | The Detail.cshtml currently has NO `@section Scripts` block (verified by `grep -n "Scripts" Detail.cshtml` returning nothing) so adding one is purely additive | Partial Convention | None — verified by grep |

**These assumptions are non-blocking** — Phase 16 should not need user input before plan-time. They are documented for the planner and Codex peer-review pass to validate during plan-check.

## Open Questions

1. **`autofocus` on Cancel for danger variant?**
   - What we know: D-08's partial puts `autofocus` on the Confirm button; D-07's API defaults `confirmLabel: 'Delete'` for danger=true.
   - What's unclear: WCAG accessibility guidance recommends destructive-action defaults should NOT auto-focus the destructive button (avoid accidental Enter-key confirm). NN/g, Material, and Apple HIG all suggest Cancel as the safer default.
   - Recommendation: Make the TS dynamically set `autofocus` (or call `.focus()`) on the Cancel button when `danger=true`. Falls under Claude's Discretion per CONTEXT. ~2 lines of TS, ~0 lines of partial change.

2. **Should `data-admin-feedback-id` be sanitized?**
   - What we know: `Model.Id` from `FeedbackItem` is currently a Guid or int (verify in Models — out of phase scope to read).
   - What's unclear: If `Model.Id` is ever user-supplied or non-trivial, the `${id}` interpolation in `admin-feedback.ts` template literal needs to be `textContent`-safe.
   - Recommendation: Since the modal uses `messageEl.textContent = opts.message`, the assignment is XSS-safe regardless of `id` content. No action needed.

3. **Should Phase 16 introduce a `.danger` rule for the in-page Delete button as well as the modal Confirm button?**
   - What we know: `Detail.cshtml:43` `<button type="submit" class="danger">` exists but `admin.css` has no `.danger` rule (grep returns 0).
   - What's unclear: Whether the modal Confirm button (red) should match the in-page Delete button (currently same-as-other-action) visually, requiring a `.danger` rule applicable to both.
   - Recommendation: Add a single `.admin-shell .admin-action-form button.danger, .admin-shell .admin-modal__confirm.danger { background: #b91c1c; }` rule. Closes a latent UI inconsistency (the existing in-page `.danger` class was dead).

## Environment Availability

| Dependency | Required by | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | Razor build, app run | ✓ (verified — `dotnet --list-sdks` shows 10.x; project builds at HEAD per recent commits) | net10.0 | — |
| Node 20+ | `tsc` invocation via MSBuild target | ✓ (verified — `DeckFlow.Web/node_modules/typescript` is restored; existing TS files compile clean) | TypeScript 6.0.2 | — |
| Evergreen browser (Chrome/Firefox/Safari ≥2 years) | Manual UAT | ✓ (user has dev environment) | per UAT setup | — |
| NVDA / VoiceOver (optional) | UAT step 8 SR smoke test | Likely ✓ on dev machine | — | UAT step 8 is OPTIONAL; UAT-1..7 are sufficient for SC verification |
| Codex CLI | Implementation per CLAUDE.md cross-AI rule | Available (per established v1.3 pattern, used across 6+ phase closures) | — | `--no-cross-ai` flag if Codex unavailable |

**Missing dependencies with no fallback:** None.
**Missing dependencies with fallback:** None.

## Validation Architecture

Phase 16 ships under DeckFlow's manual-UAT model. `workflow.nyquist_validation` is not enabled in `.planning/config.json` for this project (verified by checking — no automated browser test framework wired into the repo; CLAUDE.md explicitly states "VSTest unreliable in WSL; rely on `dotnet build` clean + targeted manual harness or push-and-watch CI").

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.3 (server-side C# only — does NOT cover browser-side TS/CSS/Razor) |
| Config file | `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |
| Quick run command | `/mnt/c/Program\ Files/dotnet/dotnet.exe test DeckFlow.Web.Tests` (unreliable in WSL per CLAUDE.md; recommend push-and-watch CI) |
| Full suite command | `/mnt/c/Program\ Files/dotnet/dotnet.exe test` |

### Phase Requirements → Test Map

| REQ ID | Behavior | Test type | Automated command | File exists? |
|--------|----------|-----------|-------------------|-------------|
| MODAL-01 | Modal opens via `<dialog>.showModal()` | Manual UAT (browser-only behavior; no server-side seam to test) | UAT-1 | n/a |
| MODAL-01 | Tab cycling stays inside modal | Manual UAT (browser focus engine; no JS seam) | UAT-2 | n/a |
| MODAL-01 | ESC closes + restores focus | Manual UAT | UAT-3 | n/a |
| MODAL-01 | Backdrop click closes | Manual UAT | UAT-5 | n/a |
| MODAL-01 | Confirm button submits with anti-forgery token | Manual UAT (functional happy path) | UAT-6 | n/a |
| MODAL-01 | No CSS bleed into 22 guild themes | Manual UAT + plan-checker grep | UAT-7 + `grep -E '^[^\.\s/]' admin.css \| grep -v admin-shell` | n/a |
| MODAL-01 | `dotnet build` clean (zero warnings introduced) | Automated build gate | `dotnet build DeckFlow.Web -c Release` from clean obj/ | YES — existing |
| MODAL-01 | TypeScript strict compile (zero errors introduced) | Automated build gate (TS compile is part of MSBuild) | Same as above; MSBuild `CompileTypeScriptAssets` target | YES — existing |
| MODAL-01 | Existing test suite green (no regression) | Automated regression | `dotnet test` (in push-and-watch CI; WSL VSTest unreliable) | YES — 497 tests at HEAD per STATE.md |

### Sampling Rate

- **Per task commit:** `dotnet build DeckFlow.Web -c Release` clean (verifies TS compiles + Razor compiles + zero new warnings)
- **Per phase merge:** Full manual UAT-1..7 (+ UAT-8 SR smoke if available); `dotnet test` via push-and-watch CI on a remote branch
- **Phase gate:** UAT-1..7 PASS + `dotnet build -c Release` clean + push-CI `dotnet test` 497/497 PASS before `/gsd:verify-work`

### Wave 0 Gaps

None for Phase 16 — no test infrastructure setup needed. Phase 16's verification model is "browser-side manual UAT plus existing automated build + existing CI test suite (preserved)." This matches the documented v1.3 pattern across 51 plans / 13 phases.

## Security Domain

### Applicable ASVS Categories

| ASVS category | Applies | Standard control |
|---------------|---------|-----------------|
| V2 Authentication | yes (indirect) | `BasicAuthMiddleware` already gates `/Admin/*`; Phase 16 preserves the existing perimeter |
| V3 Session Management | no | Phase 16 introduces no session state |
| V4 Access Control | yes (indirect) | Existing admin role enforcement preserved; modal does not bypass any access check (server still re-checks on POST) |
| V5 Input Validation | yes | `messageEl.textContent = opts.message` (NOT `innerHTML`) — XSS-safe by construction; partial passes structural markup only, no model-bound interpolation |
| V6 Cryptography | no | Phase 16 introduces no cryptographic operations |
| V13 API and Web Service | yes | `[ValidateAntiForgeryToken]` on `AdminFeedbackController.Apply` MUST be preserved; PITFALLS.md Pitfall 11 ("two CSRF mechanisms") applies |

### Known Threat Patterns for ASP.NET 10 / Razor / browser-side TS

| Pattern | STRIDE | Standard mitigation |
|---------|--------|---------------------|
| XSS via dynamic title/message | Tampering / Information Disclosure | Use `textContent`, NEVER `innerHTML`, in `admin-modal.ts` (verified in code example above) |
| CSRF on Delete POST | Tampering / Spoofing | Preserve `@Html.AntiForgeryToken()` in the Delete form (PITFALLS.md Pitfall 11); UAT-6 verifies token in POST body |
| Confirm bypass via direct POST | Tampering | Server-side `[ValidateAntiForgeryToken]` is the actual security boundary; the modal is a UX guard only — not a security control. If an attacker submits POST directly, the anti-forgery filter blocks it |
| CSP `script-src 'self'` regression | Tampering | Phase 16 ADDS no inline `<script>` blocks; all JS is external (`~/js/admin-modal.js`, `~/js/admin-feedback.js`). The Delete form's previous `onsubmit="…"` is REMOVED, which improves CSP posture (continues the WDG-04 lineage that started in Phase 11 for AdminFeedback/Index.cshtml) |
| Click-jacking on admin pages | Tampering | Existing `X-Frame-Options` / CSP `frame-ancestors` headers via `SecurityHeadersApplicationBuilderExtensions` already cover this; modal is rendered inside the existing admin shell which inherits those headers |

## Open Decisions for Planner

These five items are framed for the planner / Codex peer reviewer. None blocks Phase 16 plan creation; each has a recommended default the planner can adopt or override.

1. **`autofocus` on Cancel for danger variant?** Recommended: YES, set focus to Cancel via TS when `danger=true`. Aligns with WCAG / NN/g destructive-action guidance. Cost: ~2 lines. Override: keep on Confirm if user prefers parity with non-danger.

2. **Add a `.danger` rule covering both in-page Delete button and modal Confirm button?** Recommended: YES, single rule `.admin-shell .admin-action-form button.danger, .admin-shell .admin-modal__confirm.danger { background: #b91c1c; }`. Closes a latent dead-class. Cost: 3 CSS lines. Override: scope to modal only and leave the in-page Delete inconsistent.

3. **Exact danger-red hex value?** Recommended: `#b91c1c` (Tailwind's "red-700" — common admin destructive default). Planner can `grep '\b#[bB]91' admin.css site*.css` to verify no pre-existing color collision and pick a different shade if needed.

4. **Defense-in-depth `previouslyFocused?.focus()`?** Recommended: YES, keep it. Browser does the same thing natively, but the 2-line fallback protects future-Phase-7 reuse where the trigger element might be re-rendered during the await. Cost: 2 TS lines.

5. **`closedby="any"` HTML attribute as a future-proofing addition (alongside hand-rolled handler)?** Recommended: NO. Keep ONLY the hand-rolled `dialog.addEventListener('click', ...)` handler. Adding both is redundant and risks double-close-event-firing on browsers that support both. Revisit declarative `closedby` in v1.5+ when baseline is universal.

## Sources

### Primary (HIGH confidence)

- `DeckFlow.Web/wwwroot/ts/df-select.ts:1-6, 837-839` — IIFE + `window.DeckFlow` global namespace pattern (canonical)
- `DeckFlow.Web/wwwroot/ts/deck-sync.ts:2542-2548` — second confirmation of the global-namespace pattern
- `DeckFlow.Web/wwwroot/ts/category-suggestions.ts:16-18` — third confirmation; `interface Window` declaration-merge convention
- `DeckFlow.Web/wwwroot/ts/admin-feedback.ts:1-26` — existing admin IIFE module shape; canonical reference for additive edit
- `DeckFlow.Web/wwwroot/ts/admin-analytics.ts:1-89`, `admin-harvest.ts:1-183` — sibling admin TS modules, IIFE+strict pattern
- `DeckFlow.Web/tsconfig.json:1-15` — `"module": "none"` (the load-bearing constraint that drives the IIFE pattern)
- `DeckFlow.Web/Views/Shared/_AdminLayout.cshtml:14, 18, 36` — confirms `.admin-shell` wrapper exists, admin.css link path, Scripts RenderSection
- `DeckFlow.Web/Views/AdminFeedback/Detail.cshtml:39-44` — target edit site
- `DeckFlow.Web/Views/AdminFeedback/Index.cshtml:100-103` — canonical `@section Scripts { <script src="~/js/...js" asp-append-version="true"></script> }` pattern
- `DeckFlow.Web/wwwroot/css/admin.css:1-193` (full file read) — token surface, existing class set, insertion point analysis
- `.editorconfig:7-25` — formatting rules (2-space TS/CSS, 4-space CS/CSHTML, LF, final-newline)
- `.gitattributes:21-23` — LF normalization for `.ts`/`.js`/`.css`
- `CLAUDE.md` (project, global) — formatting paranoia, cross-AI execution rule, no-Co-Authored-By
- `.planning/research/SUMMARY.md` §5 invariants 10-12 — native `<dialog>` + `.admin-shell` scoping + layout-CSS placement
- `.planning/research/PITFALLS.md` Pitfall 9 (lines 242-268) — hand-rolled focus-trap anti-pattern + manual UAT detection
- `.planning/research/PITFALLS.md` Pitfall 10 (lines 272-296) — admin CSS bleed into 22 guild themes
- `.planning/research/PITFALLS.md` Pitfall 11 (lines 300-322) — `[ValidateAntiForgeryToken]` discipline
- `.planning/research/FEATURES.md` Feature 6 (lines 348-403) — full WDG-04 modal feature analysis
- `.planning/research/ARCHITECTURE.md` Cluster A (lines 38-50) — component table (name `admin-feedback-modal.ts` overridden to `admin-modal.ts` per CONTEXT D-02)
- `.planning/milestones/v1.3-phases/11-web-design-guidelines-audit-fixes/11-VERIFICATION.md:35-46` — WDG-04 deferral audit trail
- `.planning/phases/01-wdg-04-focus-trapped-modal/16-CONTEXT.md` — nine locked decisions

### Secondary (HIGH confidence; MDN verified 2026-05-23)

- [MDN Web Docs — `<dialog>` element](https://developer.mozilla.org/en-US/docs/Web/HTML/Element/dialog) — browser baseline, `showModal()` behavior, `closedby` semantics, focus restoration, `aria-modal` implicit, focus-trap native, `tabindex` warning, `autofocus` recommendation

### Tertiary (LOW confidence; not relied on for any prescriptive recommendation)

- None — every prescriptive claim above is backed by either a codebase reference or MDN.

## Metadata

**Confidence breakdown:**
- DeckFlow stack/convention reuse: HIGH — verified at HEAD 65f2fe4 via direct file reads; `<dialog>` pattern matches existing IIFE + Window global convention
- Native `<dialog>` behavior: HIGH — MDN verified 2026-05-23; baseline March 2022 well beyond DeckFlow's evergreen target
- ARIA + focus management: HIGH — WCAG 2.1.2 / 2.4.3 cross-referenced; PITFALLS.md Pitfall 9 detection list reused verbatim
- CSS scoping: HIGH — `.admin-shell` wrapper presence verified in `_AdminLayout.cshtml:18`
- Manual UAT design: HIGH — modeled on existing v1.3 Phase 11 UAT pattern + Pitfall 9 detection steps
- Open decisions: MEDIUM — each is a small recommendation with stated tradeoff; the planner and Codex peer review should validate against any project conventions not surfaced here

**Research date:** 2026-05-23
**Valid until:** 2026-06-22 (30 days — stable domain; native `<dialog>` is unlikely to change behavior, DeckFlow convention is pinned, CLAUDE.md is pinned)
