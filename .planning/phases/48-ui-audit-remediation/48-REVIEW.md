---
phase: 48-ui-audit-remediation
reviewed: 2026-06-16T18:45:00-06:00
depth: standard
files_reviewed: 30
files_reviewed_list:
  - DeckFlow.Web/Views/Deck/Home.cshtml
  - DeckFlow.Web/Views/Deck/CardLookup.cshtml
  - DeckFlow.Web/Views/Deck/JudgeQuestions.cshtml
  - DeckFlow.Web/Views/Shared/_ShortFormFooter.cshtml
  - DeckFlow.Web/wwwroot/css/site-common.css
  - DeckFlow.Web/wwwroot/css/site.css
  - DeckFlow.Web/wwwroot/css/site-jeskai.css
  - DeckFlow.Web/wwwroot/css/site-abzan.css
  - DeckFlow.Web/wwwroot/css/site-bant.css
  - DeckFlow.Web/wwwroot/css/site-esper.css
  - DeckFlow.Web/wwwroot/css/site-grixis.css
  - DeckFlow.Web/wwwroot/css/site-jund.css
  - DeckFlow.Web/wwwroot/css/site-mardu.css
  - DeckFlow.Web/wwwroot/css/site-naya.css
  - DeckFlow.Web/wwwroot/css/site-nyx.css
  - DeckFlow.Web/wwwroot/css/site-planeswalker-dark.css
  - DeckFlow.Web/wwwroot/css/site-sultai.css
  - DeckFlow.Web/wwwroot/css/site-commander-table.css
  - DeckFlow.Web/wwwroot/css/site-azorius.css
  - DeckFlow.Web/wwwroot/css/site-boros.css
  - DeckFlow.Web/wwwroot/css/site-dimir.css
  - DeckFlow.Web/wwwroot/css/site-golgari.css
  - DeckFlow.Web/wwwroot/css/site-gruul.css
  - DeckFlow.Web/wwwroot/css/site-izzet.css
  - DeckFlow.Web/wwwroot/css/site-orzhov.css
  - DeckFlow.Web/wwwroot/css/site-rakdos.css
  - DeckFlow.Web/wwwroot/css/site-selesnya.css
  - DeckFlow.Web/wwwroot/css/site-simic.css
  - DeckFlow.Web/wwwroot/css/site-temur.css
  - DeckFlow.Web/wwwroot/css/site-commander-table.css
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 48: Code Review Report

**Reviewed:** 2026-06-16T18:45:00-06:00
**Depth:** standard
**Files Reviewed:** 30
**Status:** issues_found

## Summary

Phase 48 covers two waves of UI audit remediation: a pure design-token pass across all 24 themes (Plans 01/02), and markup/CSS work adding inline-SVG icons, resting elevation, typography hierarchy, and the `_ShortFormFooter` partial (Plan 02).

No security vulnerabilities were found. All SVG markup is hand-authored static content with correct `aria-hidden="true" focusable="false"` decorative attributes. The `_ShortFormFooter` partial renders its model string through Razor's default HTML encoding (`@hint`) — no `Html.Raw` usage. Partial placement is correct (before `@section Scripts`, not inside it). The `Views/Shared/` location is correct for ASP.NET MVC's partial discovery.

CSS architecture constraints are respected: layout rules went into `site-common.css` only; the 22 standard theme files received nothing but `:root` token variable changes (2–4 lines each, all CSS custom properties); site.css received token changes only; no `!important` was added to the elevation rule. The `site-commander-table.css` full-fork is the only theme file with body-rule changes, and those changes correctly re-point two hardcoded `font-size: 0.75rem` literals to `var(--fs-xs)`.

Two warnings and three informational findings follow.

## Warnings

### WR-01: `--fs-xs` and `--fs-sm` now resolve to the same value, eliminating a scale step

**File:** `DeckFlow.Web/wwwroot/css/site.css:33` (and all 12 full-fork theme files at the same offset)
**Issue:** After the F3 remediation, `--fs-xs` was raised from `0.75rem` to `0.85rem`. `--fs-sm` was already `0.85rem`. Both tokens now resolve identically to `0.85rem` (13.6px at the 15px root). Any element using `var(--fs-xs)` — the pip badge and sync-column status pill in `site-commander-table.css`, plus any shared `site-common.css` selectors that use it — renders at the same size as elements using `var(--fs-sm)`. The token distinction exists in code but has no visual or behavioral effect, making the scale a 5-step scale (xs=sm, base, lg, xl, 2xl) rather than the documented 6-step scale. Future changes to either token may produce unexpected drift if a developer assumes the documented two-step gap still exists.

The plan's stated floor was `>= 12.75px`. The chosen value (0.85rem = 13.6px) clears that floor, but the arithmetic was available to choose `0.82rem` (12.3px) — which would also fail the floor — or `0.84rem` (12.6px, just under), making 0.85rem the minimum value that passes while preserving any separation. Since `--fs-sm` was already 0.85rem, the correct fix is to set `--fs-xs` to the floor value exactly: `0.85rem` cannot create separation from `--fs-sm` at 0.85rem unless `--fs-sm` is also raised.

**Fix:** Either accept the collapse (rename/remove `--fs-xs` to avoid misleading future maintainers), or increase `--fs-sm` to the next step (e.g. `0.9rem` or `0.95rem`) so the two-step gap is restored. A comment in the `:root` block noting the intentional collapse would at minimum prevent confusion:
```css
/* F3 (48-01): xs raised to floor; sm unchanged — xs == sm is intentional at this scale. */
--fs-xs:   0.85rem;
--fs-sm:   0.85rem;
```

---

### WR-02: `hub-group__title` is fragmented across three declaration blocks — F4 redeclares a no-op property

**File:** `DeckFlow.Web/wwwroot/css/site-common.css:278`, `site-common.css:2079`, `site-common.css:2100`
**Issue:** `.hub-group__title` is declared in three separate blocks:

1. **Original (line 278):** `margin / font-size / font-weight: 600 / letter-spacing: 0.08em / text-transform / color`
2. **F4 addition (line 2079):** `font-weight: 700; letter-spacing: 0.08em` (grouped with `.hub-hero__eyebrow`)
3. **F1 addition (line 2100):** `display: flex; align-items: center; gap: 0.4em`

The F4 block at line 2079 sets `letter-spacing: 0.08em` — which is already the value in the original block at line 278. That property redeclaration is a no-op and adds noise. The `font-weight` change (600 → 700) is the only real effect of that block; but because it comes from a cascade override rather than updating the canonical block, the original block at line 278 still shows `font-weight: 600` — a stale value that would mislead a developer inspecting just the primary block.

Additionally, the comment on the F4 block says "color: var(--accent) so icons inherit the theme accent color" in the adjacent F1 section — but `.hub-group__icon` uses `color: var(--muted)` (not `--accent`). That comment belongs to `.hub-card__icon`.

**Fix:** Consolidate all `.hub-group__title` properties into the existing block at line 278; remove the F4 and F1 split-out declarations for this selector. Update the `font-weight` in the original block from 600 to 700. Add `display: flex; align-items: center; gap: 0.4em` to the same block. This also corrects the misleading stale `font-weight: 600` for any developer reading the primary block:
```css
/* site-common.css:278 — consolidated */
.hub-group__title {
  display: flex;
  align-items: center;
  gap: 0.4em;
  margin: 0 0 0.6rem;
  font-size: var(--fs-sm);
  font-weight: 700;           /* was 600; raised to 700 in 48-02 F4 */
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
}
```
And correct the misplaced comment on the F1 section to say `color: var(--muted)` for `.hub-group__icon`, `color: var(--accent)` for `.hub-card__icon`.

---

## Info

### IN-01: "Analyze" and "Reference" section headers share identical SVG icon paths

**File:** `DeckFlow.Web/Views/Deck/Home.cshtml:20` and `Home.cshtml:84`
**Issue:** The `hub-group-analyze` section header (line 20) and the `hub-group-reference` section header (line 84) both use the exact same SVG path: `<circle cx="7" cy="7" r="4.5"/><line x1="10.5" y1="10.5" x2="14" y2="14"/>` — a magnifier/search icon. "Analyze" and "Reference" are semantically distinct sections: Analyze covers AI-powered deck analysis and comparison; Reference covers card lookup, mechanic rules, and judge questions. Identical icons undermine the scannability that F1 was added to provide.

**Fix:** Use a distinct icon for the "Analyze" section. Suitable candidates that fit the existing stroke-based style:
```html
<!-- Analyze: bar-chart icon -->
<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor"
     stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"
     aria-hidden="true" focusable="false">
  <rect x="2" y="9" width="3" height="5"/>
  <rect x="6.5" y="5" width="3" height="9"/>
  <rect x="11" y="2" width="3" height="12"/>
</svg>
```
The existing magnifier is already used on Card Lookup hub cards (`home.cshtml:91`) and suits the Reference section semantically.

---

### IN-02: `_ShortFormFooter.cshtml` usage comment documents a ViewData pattern that callers do not use

**File:** `DeckFlow.Web/Views/Shared/_ShortFormFooter.cshtml:4-7`
**Issue:** The comment block in the partial reads:
```
Usage: @await Html.PartialAsync("_ShortFormFooter", (string)ViewData["ShortFormFooterHint"])
The model string is the page-specific hint text; callers set
ViewData["ShortFormFooterHint"] before invoking the partial, or pass a
string model directly.
```
Neither caller sets `ViewData["ShortFormFooterHint"]`; both pass the string directly as the second argument to `PartialAsync`. The ViewData path described in the comment is a dead pattern — it would produce a null model (the cast of a missing ViewData key) that silently falls through to the generic fallback text. A future developer following the comment would get the wrong behavior.

**Fix:** Update the usage comment to match the actual calling pattern:
```csharp
@* Usage: @await Html.PartialAsync("_ShortFormFooter", "Page-specific hint text here.")
   The model string is the hint text shown in the EXAMPLE panel.
   Falls back to a generic prompt if null or whitespace. *@
```

---

### IN-03: `.short-form` CSS class (F6 cap/center) is defined but not applied to any element

**File:** `DeckFlow.Web/wwwroot/css/site-common.css:2126-2130`
**Issue:** The F6 remediation added a `.short-form` rule:
```css
.short-form {
  max-width: 64ch;
  margin-left: auto;
  margin-right: auto;
}
```
Neither `CardLookup.cshtml` nor `JudgeQuestions.cshtml` — the two "short tool pages" the summary identifies as the target — uses `class="short-form"` on any element. The plan notes F6 as "cap/center content column," but the audit summary records the F6 verdict as "improved" via the closing `short-form-footer` panel reducing the dead band, not via a content-column cap. The CSS class is therefore unused dead code. It is benign (no specificity side-effects) but adds noise and may confuse future maintainers who see a layout class with no callsite.

**Fix:** Either apply `.short-form` to the relevant wrapper element in `CardLookup.cshtml` and `JudgeQuestions.cshtml` if the width-cap effect is desired, or remove the class from `site-common.css` if the `_ShortFormFooter` panel alone is deemed sufficient for F6.

---

_Reviewed: 2026-06-16T18:45:00-06:00_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
