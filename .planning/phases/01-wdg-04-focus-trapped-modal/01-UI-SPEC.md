---
phase: 1
slug: wdg-04-focus-trapped-modal
status: draft
shadcn_initialized: false
preset: none
created: 2026-05-23
---

# Phase 1 — UI Design Contract: Admin Confirm Modal

> Visual + interaction contract for the WDG-04 focus-trapped destructive-confirm modal. Concrete pixel + token values only — planner copies these directly into `admin.css` and `_AdminConfirmModal.cshtml`. No visual decisions deferred to planner or executor.

---

## Design System

| Property | Value |
|----------|-------|
| Tool | none (DeckFlow uses hand-rolled CSS scoped per shell; no shadcn / Tailwind / Bootstrap — rejected v1.4 per REQUIREMENTS.md "Out of Scope") |
| Preset | not applicable |
| Component library | native HTML `<dialog>` + `showModal()` (D-06; no npm dependency per CONTEXT.md D-06 + research SUMMARY.md invariant #10) |
| Icon library | none for this phase (text-only buttons; no glyph in title or actions) |
| Font | system stack inherited from `admin.css:41`: `-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif` |
| Token source | `admin.css` `:root` block (`--bg`, `--panel`, `--text`, `--muted`, `--accent`, `--border`, `--focus`); admin shell does NOT inherit `site-common.css` so `--danger` is NOT defined in admin scope — modal hard-codes the destructive hex below |
| Scope | every selector MUST nest under `.admin-shell` parent OR begin with `.admin-modal*` class prefix (research invariant #11; zero bleed into 22 guild themes) |

---

## Spacing Scale (Phase 1 modal only)

Values are multiples of 4. Admin shell uses px (not rem) per `admin.css:42` baseline (15px body). Modal follows admin px convention for consistency, NOT site-common rem.

| Token | Value | Usage in modal |
|-------|-------|----------------|
| xs | 4px | Border-radius on backdrop-tinted overlays |
| sm | 8px | Gap between confirm + cancel buttons (`.admin-modal__actions` gap) |
| md | 16px | Gap between title block and message; message line bottom-margin |
| lg | 24px | `.admin-modal__panel` internal padding (top/right/bottom/left = 24px) |
| xl | 32px | (unused in modal; reserved for future modal variants) |

**Border-radius:** 4px on `.admin-modal__panel` corners (matches `admin.css:124` `.admin-banner { border-radius: 4px }` + `admin.css:137` `.admin-action-form button { border-radius: 3px }` — picking 4px to match the dominant admin convention). Buttons: 3px (matches existing `.admin-action-form button`).

**Exceptions:** Touch-target floor enforces `min-height: 44px` + `min-width: 44px` on `.admin-modal__button` (WCAG 2.5.5 — v1.3 WDG-08 carry-forward; Phase 3 admin-mobile.css will enforce shell-wide, Phase 1 enforces locally on modal buttons so phase ships standalone-compliant).

---

## Typography

Admin baseline: `font-size: 15px; line-height: 1.5;` (`admin.css:42-43`). Modal title bumps to 18px to give visual hierarchy without competing with `.admin-topbar__title` (16px) on the parent page.

| Role | Size | Weight | Line Height | Extra | Selector |
|------|------|--------|-------------|-------|----------|
| Title | 18px | 600 (semibold) | 1.3 | `text-wrap: balance` (WIG widow prevention) | `.admin-modal__title` |
| Message body | 15px | 400 (regular) | 1.5 | — | `.admin-modal__message` |
| Button label | 13px | 600 (semibold) | 1 | — | `.admin-modal__button` |

**Font-family:** all three roles inherit from `body` in `admin.css:41` — do NOT redeclare on modal classes. Button labels are **sentence-case** ("Delete", "Cancel", "Confirm") — matches existing `.admin-action-form button` ("Mark Read", "Archive", "Delete") + `.admin-feedback-filter` ("active"). NO all-caps. NO Title-Case.

**Default copy provided by `showConfirm` API (D-07):**
- `confirmLabel?: string` default = `"Confirm"` (sentence-case)
- `cancelLabel?: string` default = `"Cancel"` (sentence-case)
- AdminFeedback Delete call site overrides `confirmLabel: "Delete"` per D-07 example

---

## Color (admin dark theme)

Admin shell is dark-only (`color-scheme: dark`, `admin.css:16`). Modal inherits admin tokens; destructive variant uses an admin-local red because `admin.css` does NOT load `site-common.css` so `--danger` is undefined in admin scope.

| Role | Value | Token reference | Usage |
|------|-------|-----------------|-------|
| Dominant (60%) — page bg behind modal | `#0f172a` | `var(--bg)` (`admin.css:5`) | Visible only at edges; modal covers most |
| Secondary (30%) — modal panel surface | `#1e293b` | `var(--panel)` (`admin.css:6`) | `.admin-modal__panel` background |
| Panel border | `#334155` | `var(--border)` (`admin.css:10`) | `.admin-modal__panel` 1px border |
| Body text | `#e2e8f0` | `var(--text)` (`admin.css:7`) | `.admin-modal__title`, `.admin-modal__message` color |
| Muted body text | `#94a3b8` | `var(--muted)` (`admin.css:8`) | (unused in modal; reserved) |
| Accent (10%) — confirm `danger:false` | `#3b82f6` | `var(--accent)` (`admin.css:9`) | `.admin-modal__button--confirm` bg (non-destructive variant) + focus ring |
| Destructive — confirm `danger:true` | `#dc2626` | hard-coded (admin-scope `--danger` absent) | `.admin-modal__button--confirm.admin-modal__button--danger` bg |
| Destructive hover | `#b91c1c` | hard-coded (10% darker) | `.admin-modal__button--danger:hover` |
| Confirm-on-bg text | `#ffffff` | hard-coded `#fff` | Both `--accent` confirm AND `--danger` confirm label color |
| Cancel button bg | `transparent` | — | Text-only button per existing `.admin-feedback-filter` "ghost" convention |
| Cancel button border | `#334155` | `var(--border)` | 1px border so cancel reads as clickable |
| Cancel button text | `#e2e8f0` | `var(--text)` | |
| Cancel button hover bg | `rgba(255,255,255,0.04)` | matches `.admin-sidebar__link:hover` (`admin.css:103`) | |
| Backdrop | `rgba(15, 23, 42, 0.72)` | derived from `--bg` at 72% opacity | `dialog::backdrop` |

**Accent reserved for:** modal confirm button (non-destructive variant only), focus rings (`--focus = var(--accent)`). NOT used on cancel button, NOT used on title text, NOT used on borders.

**Why hex 72% on backdrop:** Matches admin "dimmed sibling page" convention. Lighter than v1 8.0 spec (`0.5`) because admin shell is already dark; 0.5 was visually muddy in WSL screenshot review. 0.72 keeps the page silhouette visible (operator can still see which deck/feedback the modal is acting on) while clearly de-emphasizing it.

**Why hard-coded `#dc2626` not `var(--danger)`:** `admin.css` is standalone (`admin.css:2` "NEVER imports any guild theme stylesheet"); `--danger` is defined in 22 guild theme files + site-common references but NOT in admin scope. Phase 1 hard-codes Tailwind `red-600` (`#dc2626`) — this is the de-facto destructive red and matches the `var(--danger)` value used by site-common feedback panel (`site-common.css:771` `color: var(--danger)`). Phase 3 (admin-common.css factoring) MAY introduce `--admin-danger: #dc2626;` as a new admin-scope token; until then, hard-coded is correct.

**WIG note — contrast margin:** Confirm button (`#3b82f6` accent bg + `#ffffff` text) = 4.5:1 contrast — exact WCAG AA threshold for normal-size text (≥13px). Spec passes but margin is thin. **CONSTRAINT FOR FUTURE MODAL VARIANTS:** button label font MUST stay ≥13px on accent variant; if reduced below 13px in future spec, recolor accent bg to a darker shade to maintain 4.5:1+. Danger variant (`#dc2626` + white = 4.83:1) has more headroom.

**WIG note — title element choice:** `<p class="admin-modal__title">` (not `<h2>`) avoids heading-hierarchy ambiguity when modal renders on a page without an `h1`. `aria-labelledby` on the `<dialog>` already exposes the title to screen readers; semantic `<h2>` adds no AT value here and risks WIG "headings must be hierarchical h1-h6" violation on hub pages where the admin shell only has `.admin-topbar__title` (which is NOT an `<h1>`).

---

## Copywriting Contract

| Element | Copy | Source |
|---------|------|--------|
| Title (delete variant) | `Delete Feedback` | Per D-07 example call site |
| Message (delete variant) | `Delete feedback #{id} permanently?` | Preserves existing `confirm()` string at `Detail.cshtml:41` verbatim — operator muscle-memory preserved |
| Confirm button (delete variant) | `Delete` | D-07 example; sentence-case verb only |
| Cancel button | `Cancel` | D-07 default |
| Title (generic default) | (caller-supplied; required) | `ConfirmOptions.title: string` (D-07; required, no default) |
| Message (generic default) | (caller-supplied; required) | `ConfirmOptions.message: string` (D-07; required, no default) |
| Confirm button (generic default) | `Confirm` | `confirmLabel?: string` default per D-07 |

**Tone:** declarative, no exclamation marks, no "Are you sure?" preamble. The message is the question; the buttons are the answers.

**Punctuation:**
- Title: NO trailing punctuation
- Message: trailing `?` if interrogative (Delete variant), `.` if declarative
- Buttons: NO trailing punctuation

**No empty/error states** for Phase 1 — modal is invoked, shown, dismissed; there is no fetch, no loading, no failure path. (Caller's form-POST handles its own error path AFTER `showConfirm` resolves true.)

---

## Visual Spec

### Modal sizing

| Property | Desktop (≥769px) | Mobile (≤768px) |
|----------|------------------|-----------------|
| `width` | `min(480px, calc(100vw - 32px))` | `calc(100vw - 32px)` |
| `max-width` | `480px` | `calc(100vw - 32px)` |
| `min-width` | `280px` | `280px` |
| Vertical position | centered (native `<dialog>` default via `margin: auto`) | centered |
| `max-height` | `min(80vh, 600px)` | `min(80vh, 600px)` |
| Horizontal margin | auto (centers via UA stylesheet for `<dialog>`) | auto |

**Why 480px max-width:** Common confirm-modal width — wide enough for "Delete feedback #1234 permanently?" on one line (≈40 chars at 15px in system stack ≈ 360px), narrow enough that focus doesn't wander across a wide reading line. Smaller than `.admin-feedback-detail` (`site-common.css:797` `max-width: 800px`) so modal reads as a discrete dialog, not a page panel.

**Why 32px viewport gutter on mobile:** 16px gutter each side — matches admin `--md` spacing. Below 312px viewport (rare, e.g. Galaxy Fold folded) modal pegs to `min-width: 280px` and will overflow viewport — acceptable graceful degradation (admin is desktop-first per AMOB-01 floor of 320px).

### Color tokens (BEM-scoped CSS variables)

Modal CSS may either inline values OR set scoped vars on `.admin-modal__panel`:
```css
.admin-modal__panel {
  --modal-bg: var(--panel);             /* #1e293b */
  --modal-border: var(--border);        /* #334155 */
  --modal-text: var(--text);            /* #e2e8f0 */
  --modal-confirm-bg: var(--accent);    /* #3b82f6 */
  --modal-confirm-text: #ffffff;
  --modal-danger-bg: #dc2626;
  --modal-danger-bg-hover: #b91c1c;
  --modal-cancel-border: var(--border); /* #334155 */
}
```
Planner may inline or scope-var — both are acceptable. Use scoped vars if Phase 3 admin-common.css factoring benefits.

### Spacing values (all px, admin convention)

| Property | Value |
|----------|-------|
| `.admin-modal__panel` padding | `24px` (all sides) |
| `.admin-modal__panel` border-width | `1px` |
| `.admin-modal__panel` border-radius | `4px` |
| `.admin-modal__title` margin | `0 0 16px 0` (zero top, 16px bottom) |
| `.admin-modal__message` margin | `0 0 24px 0` |
| `.admin-modal__actions` gap | `8px` (between buttons) |
| `.admin-modal__actions` justify-content | `flex-end` (buttons right-aligned per platform convention) |
| `.admin-modal__button` padding | `0 16px` (vertical handled by `min-height: 44px`) |
| `.admin-modal__button` border-radius | `3px` (matches `admin.css:137`) |
| `.admin-modal__button` border-width | `1px` (transparent on confirm so cancel + confirm align on bottom edge) |

### Box-shadow

`.admin-modal__panel`: `0 12px 32px rgba(0, 0, 0, 0.5)` — modest dark-shell elevation. Distinct from sticky-download shadow (`0 2px 6px rgba(0, 0, 0, 0.18)`, `site-common.css:1221`) because modal sits above page chrome and needs stronger lift.

---

## Interaction Spec

### Focus behavior

| Event | Behavior |
|-------|----------|
| `showModal()` invoked | Browser sets initial focus to first focusable inside `<dialog>` per native algorithm. Planner MUST verify by markup ordering that `.admin-modal__button--cancel` is the first focusable so destructive Confirm is NOT default-focused (safety: ENTER on focus shouldn't auto-delete). |
| Tab inside dialog | Native `<dialog>` cycles within `<dialog>`'s focusable descendants only (browser-enforced focus trap; SC2 requirement). |
| Shift+Tab inside dialog | Reverse cycle, same trap (browser). |
| `<dialog>` close (ESC, backdrop click, cancel, confirm) | TS handler restores focus to `document.activeElement`-snapshot captured BEFORE `showModal()`, per D-06 hand-rolled extra. |

### Animation + timing

Per CONTEXT.md D-09 "out-of-scope for fancy fade/scale" — minimal motion only.

| Animation | Value | Notes |
|-----------|-------|-------|
| Backdrop fade-in | `transition: opacity 120ms ease-out;` on `dialog::backdrop` (opacity 0 → 1) | Triggered by `[open]` attribute on `<dialog>` |
| Panel fade-in | `transition: opacity 120ms ease-out, transform 120ms ease-out;` (opacity 0 → 1, transform `translateY(-4px) → translateY(0)`) | Subtle "drop in" cue |
| Backdrop fade-out | NONE (browser removes element on `close`) — no transition because `<dialog>` removes from compositor synchronously | |
| Hover transition on buttons | `transition: background-color 120ms ease, border-color 120ms ease;` | Matches `.hub-card` timing (`site-common.css:244`) |

**`prefers-reduced-motion`:** site-common.css `@media (prefers-reduced-motion: reduce)` (line 16-25) SETS `animation-duration: 0.01ms !important; transition-duration: 0.01ms !important;` GLOBALLY — admin.css does NOT load site-common, so admin shell does NOT inherit this. Modal CSS MUST redeclare reduced-motion gate locally:

```css
@media (prefers-reduced-motion: reduce) {
  .admin-modal__panel,
  .admin-modal__panel::backdrop,
  dialog.admin-modal::backdrop {
    transition: none !important;
    animation: none !important;
  }
}
```

This is the SAME pattern admin.css already uses for `:focus-visible` (lines 19-32 — re-declares site.css's universal indicator because admin doesn't inherit). Treat reduced-motion as the third such site-common primitive that admin shell must re-declare.

### Dismiss behavior (D-03)

| Trigger | Result | Promise resolution |
|---------|--------|--------------------|
| Click Confirm button | `dialog.close('confirm')` → close event handler resolves promise true | `true` |
| Click Cancel button | `dialog.close('cancel')` → resolves false | `false` |
| Press ESC | Browser fires `close` event (returnValue empty) → resolves false | `false` |
| Click backdrop (outside `.admin-modal__panel`) | `click` handler checks `e.target === dialog`, calls `dialog.close('backdrop')` → resolves false | `false` |
| Click inside `.admin-modal__panel` | No close (click does not bubble to dialog element due to backdrop targeting) | (modal stays open) |

**Programmatic detection:** Use `dialog.returnValue` set via `<button value="confirm">` / `<button value="cancel">` form-method=dialog attributes — native pattern. Alternative: track via TS state flag. Planner picks either; both meet D-03.

---

## A11y Spec

### ARIA

| Attribute | Value | Source |
|-----------|-------|--------|
| `role` | `dialog` (implicit on `<dialog>`) | Browser-native — DO NOT manually add `role="dialog"`; it's redundant + risks AT double-announce |
| `aria-modal` | `true` (implicit when opened via `showModal()`) | Browser-native — DO NOT manually add |
| `aria-labelledby` | `id` of `.admin-modal__title` element (e.g. `admin-modal-title`) | Hand-rolled per D-06 |
| `aria-describedby` | `id` of `.admin-modal__message` element (e.g. `admin-modal-message`) | Hand-rolled per D-06 |
| `id` on `<dialog>` | `admin-confirm-modal` (singleton per page; D-08) | Stable for TS selector |
| `autofocus` on cancel button | YES — first-focus target so ENTER doesn't auto-confirm destructive op | Safety extension of D-03 |

### Focus-visible ring

Per v1.3 WDG-01 admin focus foundation (`admin.css:23-32`):
```css
button:focus-visible { outline: 2px solid var(--focus); outline-offset: 2px; }
```
Modal buttons inherit this rule. NO override. Outline color = `--accent` (`#3b82f6`) on BOTH cancel AND confirm buttons (including danger variant — Tailwind `red-600` background + blue focus ring is high-contrast and unambiguous; planner does NOT recolor focus ring to red for danger).

### Touch-target floor

WCAG 2.5.5 + v1.3 WDG-08 `--accent` button `min-height: 44px` precedent (`.ai-selector__option-label`, `site-common.css:1291`):

| Element | `min-height` | `min-width` |
|---------|--------------|-------------|
| `.admin-modal__button` | `44px` | `44px` |

Both buttons. Both variants. No exception for "compact" modals.

### Screen-reader announcement

Native `<dialog>` + `showModal()` automatically announces dialog role + label + description (NVDA, JAWS, VoiceOver — verified in MDN docs). No `aria-live` region needed. Initial focus on cancel button means SR speaks: `"{Title}, dialog, {Message}, Cancel, button"`.

### Keyboard map

| Key | Action |
|-----|--------|
| Tab / Shift+Tab | Cycle focusable inside `<dialog>` (browser trap) |
| Escape | Close, returnValue empty, promise → false |
| Enter (when confirm focused) | Activate confirm |
| Enter (when cancel focused) | Activate cancel (close, → false) |
| Space (any button focused) | Activate that button |

---

## CSS Class Naming (BEM)

| Class | Element | Required attributes |
|-------|---------|---------------------|
| `.admin-modal` | the `<dialog>` element itself | `id="admin-confirm-modal"` |
| `.admin-modal__backdrop` | NOT used — styled via `dialog.admin-modal::backdrop` pseudo-element | — |
| `.admin-modal__panel` | inner `<div>` wrapping content (required for padding + border-radius isolation from UA dialog defaults) | — |
| `.admin-modal__title` | `<p class="admin-modal__title">` (NOT `<h2>` — see WIG note below) | `id="admin-modal-title"` (referenced by `aria-labelledby`) |
| `.admin-modal__message` | `<p>` containing message body | `id="admin-modal-message"` (referenced by `aria-describedby`) |
| `.admin-modal__actions` | `<div>` wrapping the two buttons | — |
| `.admin-modal__button` | base button class | `type="button"` (NOT `type="submit"` — modal lives outside any form per D-08) |
| `.admin-modal__button--confirm` | confirm-variant modifier | `value="confirm"` |
| `.admin-modal__button--cancel` | cancel-variant modifier | `value="cancel"`, `autofocus` |
| `.admin-modal__button--danger` | destructive-variant modifier (combined with `--confirm`) | applied when `danger: true` |

**Modifier combination rule:** `.admin-modal__button--confirm.admin-modal__button--danger` (BOTH classes) for destructive confirm. CSS specificity tie-breaker: danger color wins via source-order (declare `--danger` AFTER `--confirm` in admin.css).

**Scoping discipline:** Every modal selector MUST be one of:
- `.admin-modal` (the dialog itself)
- `.admin-modal__*` or `.admin-modal__*--*` (descendants)
- `dialog.admin-modal::backdrop` (the backdrop pseudo)
- `.admin-modal__panel` nested rules (e.g. `.admin-modal__panel > h2` is ALLOWED because it's scoped to a modal-owned subtree)

NO bare-element selectors (`dialog { ... }`, `button { ... }`, `h2 { ... }`) at top level of admin.css's new section — research invariant #11 (CSS scoped to `.admin-shell` parent) is satisfied by these prefixes since `.admin-modal` only ever appears inside `_AdminLayout.cshtml`-rendered pages.

---

## Razor Partial Structure (D-08)

`_AdminConfirmModal.cshtml` is structural-only, no model. Approximate markup (planner finalizes exact attributes):

```cshtml
@* DeckFlow Admin Confirm Modal — singleton DOM template per page *@
@* Title + message + button labels populated at runtime by admin-modal.ts via textContent *@
<dialog id="admin-confirm-modal" class="admin-modal" aria-labelledby="admin-modal-title" aria-describedby="admin-modal-message">
    <div class="admin-modal__panel">
        <p id="admin-modal-title" class="admin-modal__title"></p>
        <p id="admin-modal-message" class="admin-modal__message"></p>
        <div class="admin-modal__actions">
            <button type="button" class="admin-modal__button admin-modal__button--cancel" value="cancel" autofocus>Cancel</button>
            <button type="button" class="admin-modal__button admin-modal__button--confirm" value="confirm">Confirm</button>
        </div>
    </div>
</dialog>
```

`admin-modal.ts` mutates `textContent` of title/message + confirm-button + toggles `admin-modal__button--danger` class before `showModal()`. NO `innerHTML` (XSS guard — title/message strings could one day come from i18n or user-supplied feedback content).

---

## Edge Cases

### Long message text

`.admin-modal__message` strategy: WRAP naturally (`word-wrap: break-word; overflow-wrap: anywhere;`). Panel `max-height: min(80vh, 600px)` + `overflow-y: auto` on `.admin-modal__panel` so very long messages scroll inside the modal instead of pushing buttons off-screen. NO truncation, NO ellipsis — operator must see full destructive-op context.

```css
.admin-modal__panel { max-height: min(80vh, 600px); overflow-y: auto; }
.admin-modal__message { word-wrap: break-word; overflow-wrap: anywhere; }
```

### Long button labels

`.admin-modal__button` strategy: WRAP to 2 lines max (`white-space: normal; line-height: 1.2;`). If two lines + padding exceed 44px minimum, button grows vertically (acceptable). NO truncation. Practical guidance: keep `confirmLabel` ≤ 12 chars ("Delete forever" fits; "Delete this very long thing forever" wraps awkwardly — caller responsibility).

### Long title

`.admin-modal__title` strategy: WRAP to N lines (no max — title is single-paragraph-ish; if a caller passes a 200-char title, the modal grows and `max-height` scroll catches it). Practical guidance: titles ≤ 40 chars.

### Mobile (≤768px viewport)

Phase 3 owns the admin mobile sweep; Phase 1's modal CSS must NOT break narrow viewport:

- Modal width: `calc(100vw - 32px)` (declared above)
- Vertical position: centered (browser default; works ≥320px tall)
- Buttons: stay side-by-side (no stack-to-column for Phase 1 — `.admin-modal__actions` keeps `flex-direction: row`); 8px gap fits two 44px-min-width buttons in `375px - 48px panel padding - 8px gap = 319px` available, which comfortably seats two `[Cancel][Delete]` buttons (each ~80px wide at 13px font).
- Touch targets: already 44px min via base rule — no mobile-specific bump needed.

**Phase 3 future work (NOT Phase 1 scope):** Phase 3 may add a `@media (max-width: 480px) { .admin-modal__actions { flex-direction: column-reverse; } .admin-modal__button { width: 100%; } }` rule if usability testing shows two narrow buttons feel cramped. `column-reverse` so Cancel sits BELOW Confirm on touch (operator's thumb hits Cancel naturally; mistake-confirm rate drops). Phase 1 does NOT pre-emptively add this — out of scope per D-09.

### Zero-height viewport (e.g. landscape phone keyboard open)

`max-height: 80vh` constrains panel; `overflow-y: auto` on panel keeps content reachable. Acceptable.

### Multiple modals open at once

D-08: partial is singleton per page. `showConfirm()` MUST queue or reject if called while already-open (planner decision; recommend reject-with-warning since admin operator can't realistically trigger two destructive ops simultaneously and queue adds state complexity).

---

## Acceptance

Planner can grep this section to confirm spec values landed in CSS:

| Property | Value | Grep target |
|----------|-------|-------------|
| Panel bg | `#1e293b` via `var(--panel)` | `grep "var(--panel)" admin.css \| grep -c admin-modal` ≥ 1 |
| Panel border-radius | `4px` | `grep "admin-modal__panel" admin.css \| grep "border-radius: 4px"` |
| Panel padding | `24px` | `grep "admin-modal__panel" admin.css \| grep "padding: 24px"` |
| Panel max-width | `480px` | `grep -E "max-width: 480px" admin.css \| grep -c "admin-modal"` ≥ 1 |
| Panel max-height | `min(80vh, 600px)` | `grep "min(80vh, 600px)" admin.css` |
| Danger bg | `#dc2626` | `grep -i "#dc2626" admin.css` |
| Danger hover | `#b91c1c` | `grep -i "#b91c1c" admin.css` |
| Confirm bg | `var(--accent)` | `grep "admin-modal__button--confirm" admin.css -A2 \| grep "var(--accent)"` |
| Backdrop opacity | `rgba(15, 23, 42, 0.72)` | `grep "rgba(15, 23, 42, 0.72)" admin.css` |
| Title size | `18px` | `grep "admin-modal__title" admin.css -A3 \| grep "font-size: 18px"` |
| Title weight | `600` | `grep "admin-modal__title" admin.css -A3 \| grep "font-weight: 600"` |
| Title line-height | `1.3` | `grep "admin-modal__title" admin.css -A3 \| grep "line-height: 1.3"` |
| Title text-wrap | `balance` (WIG) | `grep "admin-modal__title" admin.css -A3 \| grep "text-wrap: balance"` |
| Message size | `15px` | `grep "admin-modal__message" admin.css -A3 \| grep "font-size: 15px"` |
| Message line-height | `1.5` | `grep "admin-modal__message" admin.css -A3 \| grep "line-height: 1.5"` |
| Button size | `13px` | `grep "admin-modal__button" admin.css -A3 \| grep "font-size: 13px"` |
| Button weight | `600` | `grep "admin-modal__button" admin.css -A3 \| grep "font-weight: 600"` |
| Button min-height | `44px` | `grep "admin-modal__button" admin.css -A3 \| grep "min-height: 44px"` |
| Button min-width | `44px` | `grep "admin-modal__button" admin.css -A3 \| grep "min-width: 44px"` |
| Button border-radius | `3px` | `grep "admin-modal__button" admin.css -A3 \| grep "border-radius: 3px"` |
| Actions gap | `8px` | `grep "admin-modal__actions" admin.css -A3 \| grep "gap: 8px"` |
| Actions align | `flex-end` | `grep "admin-modal__actions" admin.css -A3 \| grep "justify-content: flex-end"` |
| Box-shadow | `0 12px 32px rgba(0, 0, 0, 0.5)` | `grep "0 12px 32px rgba(0, 0, 0, 0.5)" admin.css` |
| Animation timing | `120ms ease-out` | `grep "120ms ease-out" admin.css \| grep -c admin-modal` ≥ 1 |
| Reduced-motion gate | local re-declaration | `grep -B1 "prefers-reduced-motion" admin.css \| grep -c admin-modal` ≥ 1 |
| Cancel autofocus | `autofocus` on cancel button | `grep "autofocus" _AdminConfirmModal.cshtml \| grep -c "cancel"` ≥ 1 |
| ARIA labelledby | `aria-labelledby="admin-modal-title"` | `grep "aria-labelledby" _AdminConfirmModal.cshtml` |
| ARIA describedby | `aria-describedby="admin-modal-message"` | `grep "aria-describedby" _AdminConfirmModal.cshtml` |
| Scope discipline | every new selector prefixed `.admin-modal*` OR `dialog.admin-modal::backdrop` | manual diff review — no bare element selectors in the new admin.css section |

---

## Color (60/30/10 summary)

| Role | Value | Usage |
|------|-------|-------|
| Dominant (60%) | `#0f172a` (`--bg`) | Page bg visible behind backdrop |
| Secondary (30%) | `#1e293b` (`--panel`) | Modal panel surface |
| Accent (10%) | `#3b82f6` (`--accent`) | Confirm button bg (non-destructive), focus rings |
| Destructive | `#dc2626` (hard-coded) | Confirm button bg when `danger:true` |

Accent reserved for: modal confirm button (non-destructive variant), `:focus-visible` outlines. NOT used on cancel button, NOT on title text, NOT on body text, NOT on borders.

---

## Copywriting Contract (summary)

| Element | Copy |
|---------|------|
| Primary CTA (Delete variant) | `Delete` |
| Primary CTA (generic default) | `Confirm` |
| Cancel button | `Cancel` |
| Empty state | not applicable (modal has no list / no fetch) |
| Error state | not applicable (caller handles post-confirm POST errors) |
| Destructive confirmation | `Delete Feedback` / `Delete feedback #{id} permanently?` / `Delete` |

---

## Registry Safety

| Registry | Blocks Used | Safety Gate |
|----------|-------------|-------------|
| none | none | not applicable — no shadcn, no third-party UI registry, no npm runtime dep (D-06 native `<dialog>` only) |

---

## Pre-Populated From

| Source | Decisions used |
|--------|----------------|
| CONTEXT.md (D-01..D-09) | All 9 locked decisions — D-01 reusable helper, D-02 admin-modal.ts naming, D-03 dismiss behavior, D-04 partial location, D-05 admin.css landing, D-06 native dialog + ARIA, D-07 API shape, D-08 structural-only partial, D-09 scope discipline |
| REQUIREMENTS.md (MODAL-01) | Phase scope confirmation — single requirement, fully contained in Phase 1 |
| ROADMAP.md (Phase 1 SC1-SC4) | Native `<dialog>`, focus-trap, ESC + restore-focus + click-outside, scoped CSS no theme bleed |
| `admin.css` (Phase 6 + WDG audit fixes) | All color tokens (`--bg`, `--panel`, `--text`, `--muted`, `--accent`, `--border`, `--focus`), font stack, baseline 15px/1.5, focus-visible rule, scope-discipline pattern (admin re-declares site-common rules locally because admin standalone) |
| `site-common.css` (WDG-08) | Reduced-motion gate pattern (admin must re-declare locally — admin doesn't inherit site-common), `--danger` reference (admin doesn't have it, hard-coding `#dc2626` to match) |
| `_AdminLayout.cshtml` | Confirmed admin shell renders modal-host pages; modal CSS lands in single loaded stylesheet (`admin.css`); `Scripts` section available for `admin-modal.ts` + `admin-feedback.ts` |
| `AdminFeedback/Detail.cshtml:41` | Verbatim confirm copy preserved: `Delete feedback #{id} permanently?` |
| User input | None requested — all design questions answerable from upstream artifacts + existing admin token vocabulary |

---

## Checker Sign-Off

- [ ] Dimension 1 Copywriting: PASS
- [ ] Dimension 2 Visuals: PASS
- [ ] Dimension 3 Color: PASS
- [ ] Dimension 4 Typography: PASS
- [ ] Dimension 5 Spacing: PASS
- [ ] Dimension 6 Registry Safety: PASS

**Approval:** pending
