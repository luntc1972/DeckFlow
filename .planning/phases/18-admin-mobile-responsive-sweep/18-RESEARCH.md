# Phase 18: Admin Mobile-Responsive Sweep — Research

**Researched:** 2026-05-24
**Domain:** CSS responsive design, accessibility (WCAG 2.5.5), admin shell markup
**Confidence:** HIGH — all findings verified from live codebase inspection

---

## Summary

Phase 18 is a pure CSS + Razor markup phase. There are no new packages to install, no new
services, no schema changes, and no TypeScript to write. All work happens in three CSS files
(new `admin-common.css`, new `admin-mobile.css`, updated `admin.css` shim) and one Razor
partial (`_AdminLayout.cshtml` sidebar markup). The admin and public CSS pipelines are fully
isolated: `_AdminLayout.cshtml` loads only `admin.css`; `_Layout.cshtml` loads
`site-common.css` + a guild theme + `site-theme-overrides.css` + `site-mobile.css`. These
two CSS sets never share a browser page, so guild-theme bleed is a static-analysis concern
(misnamed selectors or unscoped globals in admin.css leaking conceptual intent), not a
live-cascade contamination risk.

The research uncovered one pre-existing CSS architecture gap: `admin-feedback-*`, `detail-grid`,
`type-badge`, `detail-actions`, and `detail-message` classes live in `site-common.css` (public
layout only) but are used exclusively in admin views (`AdminFeedback/Index.cshtml`,
`AdminFeedback/Detail.cshtml`). Because `_AdminLayout.cshtml` never loads `site-common.css`,
these rules are unreachable on admin pages. The admin feedback list/detail pages currently
render with browser default table/grid styling. The AMOB-04 factoring is the natural place to
correct this by moving those rules into `admin-common.css`.

The current `admin.css` has no `@media` rules at all — zero responsive handling. The
`.admin-shell` grid (`grid-template-columns: 220px 1fr`) and all button/link dimensions are
viewport-independent, making the admin completely broken at 320–768px. Every admin interactive
element below the already-passing Phase 16 modal buttons (`min-height: 44px`) fails the WCAG
2.5.5 touch-target floor. The Phase 16 `admin-modal__button` pattern is the template to follow.

**Primary recommendation:** Factor `admin.css` → `admin-common.css` + `admin-mobile.css` +
import-shim `admin.css`; scope all new selectors to `.admin-shell`; add
`@media (max-width: 768px)` rules for sidebar collapse (`<details>`/`<summary>`), table
`overflow-x: auto` + `tabindex="0"` wrappers for comparison tables, card-stack pattern for
scanning tables, and `min-height: 44px; min-width: 44px` on all admin interactive elements.

---

## Project Constraints (from CLAUDE.md)

- **Formatting:** Never run Format Document / Code Cleanup / ReSharper-style reformatting.
  Never auto-convert `{ get; init; }` to `{ get; }`. Never inline `[Attribute]` onto property
  line. Never re-indent C# raw-string literals. Preserve LF line endings. Touch only the lines
  that need touching.
- **Theme system:** Guild themes are full standalone CSS forks. Layout CSS goes in
  `site-common.css`, not `site.css`. Token additions go in `:root` of each theme file.
  For admin CSS: admin layout primitives go in `admin-common.css` (the admin-equivalent of
  `site-common.css`), not in `admin.css`.
- **No framework CSS:** Bootstrap / Tailwind / Fluent UI forbidden for admin mobile —
  fights the 25-guild-theme system (per FEATURES.md reject list, referenced in REQUIREMENTS.md).
- **No new packages:** No new NuGet, npm, or any other packages without explicit user approval.
- **Commits:** Plain default-author commits, no Co-Authored-By trailer. Commit per logical
  change. README updated when behavior changes.
- **Testing:** VSTest unreliable in WSL. Rely on `dotnet build` clean + targeted manual
  harness or push-and-watch CI. No new test framework. CSS changes have no xUnit coverage.

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| AMOB-01 | Admin shell renders correctly on viewports ≥320px wide; sidebar collapses to disclosure (`<details>`/`<summary>`) below 768px with no-JS fallback | `_AdminLayout.cshtml` sidebar is a plain `<aside>` with `<nav>` — needs `<details>`/`<summary>` wrapper below 768px; sidebar grid column collapses to 0 or `auto` in mobile rule |
| AMOB-02 | Admin tables remain usable on narrow viewports — per-table choice of `overflow-x: auto` (Analytics, HarvestRuns, ContentHarvest) or card-stack pattern (Feedback list, ContentSources list) | Five admin tables identified; two strategies verified as appropriate per-table |
| AMOB-03 | Admin forms render single-column on narrow viewports; all interactive elements meet ≥44×44px touch-target floor | All admin buttons currently FAIL 44px; sidebar nav links borderline; concrete CSS fix identified |
| AMOB-04 | `admin.css` factored into `admin-common.css` (layout primitives) + `admin-mobile.css` (`@media` rules) + `admin.css` import shim; CSS scoped to `.admin-shell` | Factoring strategy and selector inventory complete; pre-existing CSS gap (admin-feedback-* in site-common.css) also resolved by migration to admin-common.css |
</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Responsive layout (sidebar collapse) | CSS (`admin-common.css` + `admin-mobile.css`) | Razor markup (`_AdminLayout.cshtml`) | CSS `<details>`/`<summary>` disclosure is no-JS; markup only needs the structural element added |
| Touch-target sizing | CSS (`admin-common.css`) | — | `min-height: 44px; min-width: 44px` on button/link selectors; no JavaScript involved |
| Table overflow strategy | CSS (`admin-mobile.css`) | Razor (add `tabindex="0"` to wrapper) | `overflow-x: auto` wrapper in CSS; keyboard accessibility requires `tabindex="0"` in markup |
| Card-stack pattern | CSS (`admin-mobile.css`) | Razor (no-change — existing markup) | CSS `display: block` per-row reflow; no markup restructuring needed |
| CSS file factoring | CSS (file restructure) | Razor (`_AdminLayout.cshtml` `<link>` tags) | Import shim `admin.css` becomes two `@import` lines; `<link>` tags in layout unchanged |
| Visual regression guard | Manual browser screenshots | — | No Playwright available; CI has no workflow file; manual 375px DevTools screenshots before/after |
| Admin-feedback CSS gap (pre-existing) | CSS (`admin-common.css`) | — | Move misplaced rules from `site-common.css` to `admin-common.css`; remove dead code from `site-common.css` |

---

## Existing Codebase Inventory

### Current `admin.css` Structure [VERIFIED: codebase]

**File:** `DeckFlow.Web/wwwroot/css/admin.css` (307 lines)
**Loaded by:** `_AdminLayout.cshtml` line 14 only — no other view loads it.
**Imports:** None (no `@import`).
**Responsive rules:** None. Zero `@media` blocks.

**Sections:**
1. `:root` — dark-mode token definitions (`--bg`, `--panel`, `--text`, `--muted`, `--accent`, `--border`, `--focus`). Note: uses `--border`, NOT `--line` (important for new rule compatibility).
2. Focus-visible block (lines 23–32) — unscoped element selectors (`a:focus-visible`, `button:focus-visible`, etc.).
3. `*`, `html, body`, `a`, `a:hover` — unscoped global resets (lines 34–47).
4. `.skip-link`, `.sr-only` — utility classes (lines 49–68).
5. `.admin-shell` grid (lines 71–79) — `grid-template-columns: 220px 1fr`, `grid-template-rows: 48px 1fr`, `grid-template-areas: "sidebar topbar" / "sidebar content"`.
6. `.admin-sidebar`, `.admin-sidebar__brand`, `.admin-sidebar__nav`, `.admin-sidebar__link` — sidebar (lines 81–108).
7. `.admin-topbar`, `.admin-topbar__title`, `.admin-topbar__version` — top bar (lines 110–120).
8. `.admin-content` — main content area (line 122).
9. `.admin-banner`, `.admin-table`, `.admin-action-form` — shared components (lines 124–138).
10. `.maintenance-page` — shared maintenance partial styles (lines 143–145).
11. `.admin-page-header`, `.admin-analytics.*`, `.admin-analytics-table.*`, `.admin-sparkline`, `.admin-empty` — analytics page (lines 148–193).
12. Phase 16 modal block (lines 195–307) — `.admin-modal*` selectors, mostly scoped to `.admin-shell` parent.

**Unscoped element selectors that must stay in admin.css (not be moved to public CSS):**
```css
/* These are safe because admin.css only loads on admin pages */
* { box-sizing: border-box; }
html, body { ... }
a { color: var(--accent); ... }
a:focus-visible, button:focus-visible, ... { outline: 2px solid var(--focus); }
```
These are intentional admin-only overrides (dark theme, different accent color than any guild
theme). They MUST remain in admin.css (or `admin-common.css` after factoring).

### Current `_AdminLayout.cshtml` Markup [VERIFIED: codebase]

```html
<div class="admin-shell">
  <aside class="admin-sidebar" aria-label="Admin sections">
    <div class="admin-sidebar__brand">DeckFlow Admin</div>
    <nav class="admin-sidebar__nav">
      <a class="admin-sidebar__link [--active]" ...>Feedback</a>
      <a class="admin-sidebar__link [--active]" ...>Harvest</a>
      <a class="admin-sidebar__link [--active]" ...>Analytics</a>
      <a class="admin-sidebar__link [--active]" ...>Flags</a>
    </nav>
  </aside>
  <header class="admin-topbar">
    <h1 class="admin-topbar__title">@ViewData["Title"]</h1>
    <span class="admin-topbar__version">v@(...)</span>
  </header>
  <main id="admin-content" class="admin-content" role="main">
    @RenderBody()
  </main>
</div>
```

The sidebar has no hamburger toggle, no `<details>`/`<summary>`, no ARIA `expanded` attribute.
At narrow viewports the 220px sidebar column simply overflows. The markup change for AMOB-01
is adding a `<details>`/`<summary>` disclosure wrapper around the `<nav>` below 768px.

### CSS Load Order (Public vs Admin) [VERIFIED: codebase]

**Public pages (`_Layout.cshtml`):**
```
site-common.css → site-{theme}.css (user-selected) → site-theme-overrides.css → site-mobile.css
```

**Admin pages (`_AdminLayout.cshtml`):**
```
admin.css (standalone — no imports, no shared CSS)
```

These never share a page. Guild-theme CSS never loads alongside `admin.css`. The Pitfall 10
"bleed" concern is therefore about conceptual contamination only:
- New admin selectors using generic names that COULD conflict if ever co-loaded.
- Admin-specific CSS accidentally placed in `site-common.css` (the current admin-feedback gap).

### Guild Themes [VERIFIED: codebase]

There are **24 theme options** in `_Layout.cshtml` (including `site.css` "Classic"):
`site.css`, `site-azorius.css`, `site-dimir.css`, `site-rakdos.css`, `site-gruul.css`,
`site-selesnya.css`, `site-orzhov.css`, `site-izzet.css`, `site-golgari.css`,
`site-boros.css`, `site-simic.css`, `site-bant.css`, `site-abzan.css`, `site-sultai.css`,
`site-mardu.css`, `site-temur.css`, `site-esper.css`, `site-grixis.css`, `site-jund.css`,
`site-naya.css`, `site-jeskai.css`, `site-nyx.css`, `site-planeswalker-dark.css`,
`site-commander-table.css`.

Requirements and roadmap say "22 guild themes." The CSS count in `/wwwroot/css/` (excluding
`site.css`, `site-common.css`, `site-mobile.css`, `site-theme-overrides.css`, `admin.css`)
is 20 named theme forks, plus `site.css` = 21 total. The number does not materially affect
Phase 18: the point is that ZERO of them load on admin pages.

Each theme `@import url('site.css')` then adds `:root` token overrides and optional extra
rules. No theme has any admin-specific selectors. There is no mechanism by which admin CSS
can bleed into a guild-theme page unless a selector is placed in `site-common.css`.

### Admin Views and Tables Inventory [VERIFIED: codebase]

| View | File | Table Class | Columns | Strategy | Rationale |
|------|------|------------|---------|----------|-----------|
| Harvest — Recent Runs | `AdminHarvest/Index.cshtml` | `admin-table` | Started, Completed, State, Decks Processed (4) | `overflow-x: auto` | Time + status comparison; data meaningless if rows collapse |
| Harvest — Run Log | `AdminHarvest/Index.cshtml` | `admin-table` | Started, Kind, State, Decks, Duration, Error (6) | `overflow-x: auto` | 6-column dense data; comparison-oriented |
| Analytics | `AdminAnalytics/Index.cshtml` | `admin-table admin-analytics-table` | Route, Hits, Unique IPs, Error rate, Sparkline (5) | `overflow-x: auto` | Numeric comparison across columns; sparkline column needs minimum width |
| Feedback list | `AdminFeedback/Index.cshtml` | `admin-feedback-table` | Created, Type, Message, Email, Status, Actions (6) | Card-stack | Each row is a scan-to-select item; reading one row at a time is natural; message preview benefits from full width |
| Flags | `AdminFlags/Index.cshtml` | `admin-table` | Key, Status, Action (3) | Card-stack | 3 columns; key + enable/disable toggle; each row is independent action |

**Note on ContentHarvest and ContentSources:** These are Phase 22 views that don't exist yet.
Their mobile strategy (overflow-x for ContentHarvest, card-stack for ContentSources) is already
decided in AMOB-02/AMOB-03 requirements and the roadmap. Phase 18 only needs to establish the
CSS patterns that Phase 22 will reuse — the patterns themselves are specified here.

**No tables in:** `AdminFeedback/Detail.cshtml` (uses `.detail-grid` `<dl>`), `AdminLanding/Index.cshtml` (no table), `AdminFlags` (small 3-column table → card-stack).

### Forms Inventory [VERIFIED: codebase]

| View | Form Purpose | Controls | Current Width |
|------|-------------|---------|---------------|
| `AdminHarvest/Index.cshtml` | RunNow | `<select>` + `<button>` | inline (no block layout) |
| `AdminHarvest/Index.cshtml` | Cancel | `<button>` | inline |
| `AdminHarvest/Index.cshtml` | SubmitUrl | `<input type="url">` + `<button>` | inline |
| `AdminHarvest/Index.cshtml` | SaveSchedule | `<select>` + `<button>` | inline |
| `AdminHarvest/Index.cshtml` | PauseSchedule | `<button>` | inline |
| `AdminFeedback/Index.cshtml` | Filter (type select) | `<select>` + submit-on-change | inline |
| `AdminFeedback/Detail.cshtml` | MarkRead / Archive / Delete | `<button>` each | `detail-actions` flex row |
| `AdminFlags/Index.cshtml` | Toggle per flag | `<button>` | `admin-action-form` inline |

All forms are currently `display: inline` (`.admin-action-form { display: inline; }`). The
Harvest page has 5 separate forms in sequence; on narrow viewports these need to stack
vertically and their inputs/selects need `width: 100%`.

### Touch-Target Audit [VERIFIED: calculated from admin.css]

| Element | Current Height | Meets ≥44px? | Fix Required |
|---------|---------------|--------------|--------------|
| `.admin-action-form button` | ~31.5px (6px pad + 13px font × 1.5) | NO | `min-height: 44px` |
| `.admin-sidebar__link` | ~42.5px (10px pad × 2 + 15px font × 1.5) | NO (borderline) | `min-height: 44px` |
| `.admin-analytics .admin-range-selector a` | ~30px (0.25rem pad × 2 + 15px font × 1.5) | NO | `min-height: 44px; display: flex; align-items: center` |
| `.admin-modal__button` | 44px (`min-height: 44px` already set) | YES | No change needed |

**WCAG reference:** WCAG 2.5.5 (Level AA in WCAG 2.2) requires minimum 44×44 CSS pixels for
target size. WCAG 2.5.8 (Level AA) requires 24×24 or 24px spacing from adjacent targets.
This project targets 44×44px as the floor per AMOB-03. [ASSUMED: WCAG 2.5.5 is the
applicable standard; confirm with user if 2.5.8 spacing rules also apply.]

### Pre-existing CSS Architecture Gap [VERIFIED: codebase]

The following classes appear in `site-common.css` (lines 777–810) but are ONLY used in admin
views (`AdminFeedback/Index.cshtml`, `AdminFeedback/Detail.cshtml`). Because `_AdminLayout.cshtml`
never loads `site-common.css`, these rules are **unreachable on admin pages**:

```
.admin-feedback          (max-width, margin, padding)
.admin-feedback-filters  (flexbox filter bar)
.admin-feedback-filter   (pill link styling)
.admin-feedback-table    (border-collapse, cell padding)
.admin-feedback-pagination
.admin-feedback-empty
.admin-feedback-detail   (panel styling)
.admin-action-form       (display: inline — duplicate of admin.css:136)
.type-badge              (inline badge)
.detail-grid             (dt/dd grid)
.detail-grid dt          (bold)
.detail-message          (pre-wrap)
.detail-actions          (flex row)
.detail-actions button.danger
```

These rules use `--line`, `--link`, `--panel`, `--fs-sm` tokens which are NOT defined in
`admin.css :root`. The admin feedback pages currently render without these styles — the
admin feedback table has no collapse, no cell padding, and no border styling from CSS.

**Resolution for AMOB-04:** Move these rules into `admin-common.css`. Adapt token references
from `--line`→`--border` (which IS defined in `admin.css :root`), `--link`→`--accent`,
`--fs-sm`→a fixed size (e.g. `0.85em`) since `--fs-sm` is not in admin.css `:root`. Remove
the dead copies from `site-common.css`.

---

## Standard Stack

### Core (no new packages)

| Tool | Version | Purpose | Source |
|------|---------|---------|--------|
| Plain CSS (`@media`) | — | Responsive breakpoints | [VERIFIED: codebase] |
| HTML `<details>`/`<summary>` | — | No-JS sidebar disclosure | [VERIFIED: HTML5 native] |
| CSS `overflow-x: auto` + `tabindex="0"` | — | Scrollable comparison tables | [ASSUMED] |
| CSS display reflow (card-stack) | — | Scanning table pattern | [ASSUMED] |

**No packages to install.** This phase is CSS + Razor markup only.

### CSS Factoring Plan

**New files to create:**
- `DeckFlow.Web/wwwroot/css/admin-common.css` — layout primitives (mirrors `site-common.css` role)
- `DeckFlow.Web/wwwroot/css/admin-mobile.css` — `@media (max-width: 768px)` rules

**File to replace:**
- `DeckFlow.Web/wwwroot/css/admin.css` → becomes an import shim:
  ```css
  /* DeckFlow Admin — import shim. Do not add rules here.
     Layout primitives → admin-common.css
     Responsive overrides → admin-mobile.css */
  @import url('admin-common.css');
  @import url('admin-mobile.css');
  ```

**`_AdminLayout.cshtml` `<link>` tag stays unchanged** — it references `~/css/admin.css`
which then pulls in both files via `@import`. No HTML change needed for the split itself.
(The sidebar markup change is separate.)

---

## Package Legitimacy Audit

**No external packages are installed in this phase.** This section is not applicable.

---

## Architecture Patterns

### System Architecture Diagram

```
Browser request to /Admin/*
         │
         ▼
_AdminLayout.cshtml  ──────────────────────────────────
│  <link href="admin.css">                             │
│    → @import admin-common.css (layout primitives)   │
│    → @import admin-mobile.css (@media rules)        │
│                                                      │
│  <div class="admin-shell">        ← scope anchor    │
│    <aside class="admin-sidebar">                     │
│      [desktop: always visible]                       │
│      [mobile ≤768px: <details>/<summary> toggle]    │
│    </aside>                                          │
│    <header class="admin-topbar">...</header>         │
│    <main class="admin-content">                      │
│       @RenderBody()  ← per-page view                 │
│         [tables: overflow-x wrapper OR card-stack]  │
│         [forms: single-column + min-height:44px]    │
│    </main>                                           │
│  </div>                                              │
└──────────────────────────────────────────────────────┘
         │
         ▼  (no JavaScript for sidebar — pure CSS/HTML)
         ▼  (no theme CSS on this page — fully isolated)
```

### Recommended File Structure After Factoring

```
DeckFlow.Web/wwwroot/css/
├── admin.css                  # BECOMES: @import shim only (2 lines)
├── admin-common.css           # NEW: dark-mode tokens + shell layout + components
│                              #   (mirrors site-common.css role for admin shell)
├── admin-mobile.css           # NEW: all @media (max-width: 768px) admin rules
├── site-common.css            # MODIFIED: remove dead admin-feedback-* rules
├── site.css                   # UNCHANGED
├── site-{theme}.css (×23)     # UNCHANGED
├── site-mobile.css            # UNCHANGED
└── site-theme-overrides.css   # UNCHANGED
```

### Pattern 1: Sidebar Disclosure (`<details>`/`<summary>`) — AMOB-01

**What:** Replace the always-visible `<aside>` with a `<details>`/`<summary>` element at
narrow viewports. At ≥769px the `<details>` is forced open via CSS (`details[open]` is
always true via `open` attribute rendered server-side or via CSS `details { display: block }`;
the `<summary>` is hidden above 768px).

**Approach (no-JS, keyboard + screen-reader operable):**

```html
<!-- _AdminLayout.cshtml: sidebar markup change -->
<aside class="admin-sidebar" aria-label="Admin sections">
    <details class="admin-sidebar__disclosure" open>
        <summary class="admin-sidebar__toggle">
            <span class="sr-only">Navigation menu</span>
            <span aria-hidden="true">☰ Menu</span>
        </summary>
        <div class="admin-sidebar__brand">DeckFlow Admin</div>
        <nav class="admin-sidebar__nav">
            <!-- links unchanged -->
        </nav>
    </details>
</aside>
```

```css
/* admin-common.css: desktop — hide toggle, keep open */
.admin-sidebar__toggle {
    display: none;   /* hidden on desktop */
}
.admin-sidebar__disclosure {
    /* no special state needed — always shows content on desktop */
}

/* admin-mobile.css: mobile — show toggle, collapse by default */
@media (max-width: 768px) {
    .admin-shell {
        grid-template-columns: 1fr;
        grid-template-rows: auto auto 1fr;
        grid-template-areas:
            "topbar"
            "sidebar"
            "content";
    }

    .admin-sidebar {
        border-right: none;
        border-bottom: 1px solid var(--border);
    }

    .admin-sidebar__toggle {
        display: flex;   /* shown on mobile */
        align-items: center;
        padding: 12px 20px;
        cursor: pointer;
        min-height: 44px;
        font-weight: 600;
    }

    .admin-sidebar__disclosure:not([open]) .admin-sidebar__nav,
    .admin-sidebar__disclosure:not([open]) .admin-sidebar__brand {
        display: none;
    }
}
```

**Why `<details>`/`<summary>` and not checkbox-hack or JS toggle:**
- No-JS fallback by design — `<details>` is native HTML disclosure.
- Screen-reader operable: `<summary>` is announced as a button with expanded/collapsed state.
- Keyboard operable: Space/Enter toggle, focus management is native.
- The `open` attribute on `<details>` is rendered server-side (always-open on desktop is
  CSS-enforced by hiding the `<summary>` and never letting the `<details>` be collapsed).

**Alternative considered:** CSS-only hamburger via hidden `<input type="checkbox">` hack.
Rejected: less accessible, requires more CSS complexity, `<details>` is the standard pattern.

### Pattern 2: Table Overflow-X Strategy — AMOB-02 (comparison tables)

**What:** Wrap the table in a scrollable div; add `tabindex="0"` for keyboard panning;
add `role="region"` + `aria-label` for screen-reader discoverability.

```html
<!-- Razor: wrap each overflow-x table -->
<div class="admin-table-scroll" role="region" aria-label="Recent harvest runs — scroll horizontally to see all columns" tabindex="0">
    <table class="admin-table">...</table>
</div>
```

```css
/* admin-common.css */
.admin-table-scroll {
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
}

/* admin-mobile.css */
@media (max-width: 768px) {
    .admin-table-scroll {
        /* ensure scrollbar is visible on iOS */
        padding-bottom: 4px;
    }
}
```

**When to use:** Analytics table, HarvestRuns tables (comparison-dense; losing columns
makes data meaningless).

### Pattern 3: Card-Stack Strategy — AMOB-02 (scanning tables)

**What:** At narrow viewports, each `<tr>` becomes a block card; each `<td>` shows its
column header as a `::before` pseudo-element label via `data-label` attribute.

```html
<!-- Razor: add data-label attributes to each <td> -->
<td data-label="Created (UTC)">@item.CreatedUtc.ToString(...)</td>
<td data-label="Type">...</td>
```

```css
/* admin-mobile.css */
@media (max-width: 768px) {
    .admin-table--card thead {
        display: none;           /* hide column headers */
    }
    .admin-table--card tr {
        display: block;
        border: 1px solid var(--border);
        border-radius: 4px;
        margin-bottom: 8px;
        padding: 8px 12px;
    }
    .admin-table--card td {
        display: flex;
        justify-content: space-between;
        gap: 8px;
        padding: 4px 0;
        border: none;
        font-size: 0.9em;
    }
    .admin-table--card td::before {
        content: attr(data-label);
        font-weight: 600;
        color: var(--muted);
        flex-shrink: 0;
    }
}
```

**Markup change required:** Add `class="admin-table--card"` to scanning tables; add
`data-label="..."` to each `<td>` in those tables.

**When to use:** Feedback list table, Flags table, ContentSources list (future).

### Pattern 4: Touch Target Floor — AMOB-03

**What:** Add `min-height: 44px; min-width: 44px` to all admin interactive elements.
The Phase 16 `admin-modal__button` already does this — use the same pattern.

```css
/* admin-common.css: apply to all admin interactive elements */
.admin-shell .admin-action-form button,
.admin-shell .admin-sidebar__link,
.admin-shell .admin-analytics .admin-range-selector a {
    min-height: 44px;
    display: inline-flex;
    align-items: center;
}
```

**Note:** `admin-action-form button` currently has `padding: 6px 12px` — must keep padding,
add `min-height: 44px` so it grows if content is smaller.

### Pattern 5: Form Single-Column — AMOB-03

```css
/* admin-mobile.css */
@media (max-width: 768px) {
    .admin-shell .admin-action-form {
        display: block;
        margin-bottom: 8px;
    }
    .admin-shell .admin-action-form input,
    .admin-shell .admin-action-form select {
        width: 100%;
        box-sizing: border-box;
        margin-bottom: 8px;
    }
    .admin-shell .admin-action-form button {
        width: 100%;
    }
    .admin-shell .admin-harvest__panel {
        padding: 12px;
        margin-bottom: 16px;
        border: 1px solid var(--border);
        border-radius: 4px;
    }
}
```

### Pattern 6: Scoping Discipline — AMOB-04

**Decision: `.admin-shell` parent scoping (not `@layer admin`).**

Rationale:
- Phase 16 already established the `.admin-shell` scoping pattern (lines 202–214 of
  current `admin.css`). Consistency with existing code is important.
- `@layer admin { }` would reduce all rules inside the layer in cascade priority — rules
  in a layer lose to unlayered rules at equal specificity, which is unexpected for
  admin-specific overrides.
- `.admin-shell` scoping adds one level of specificity (class selector), making admin
  rules take precedence over browser defaults without fighting other specificity concerns.
- Admin-only globals (`html, body`, `* { box-sizing }`, `a { color }`) must remain
  unscoped because they apply to the whole admin page — these go in `admin-common.css`
  as they are now, with a comment that they are intentional admin-only globals.

**All new selectors in `admin-common.css` must be:**
1. Scoped to `.admin-shell .selector` (for component rules), OR
2. Explicitly a global admin reset (documented with `/* admin-only global */` comment).

### Anti-Patterns to Avoid

- **Do not put `@media` rules in `admin-common.css`** — all responsive rules go in `admin-mobile.css`. This mirrors the `site-common.css` / `site-mobile.css` split.
- **Do not use `!important` to fix specificity issues** — scope with `.admin-shell` instead.
- **Do not copy token variable names from public themes** — `admin.css` uses `--border`, `--text` etc., not `--line`, `--ink`. New rules must use admin tokens only.
- **Do not modify `site-common.css` to add admin rules** — move existing dead admin rules OUT of site-common.css, don't add more.
- **Do not use `:has()` for the sidebar toggle** — browser support is good (2023+) but using native `<details>` is simpler and more accessible.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Sidebar toggle | JS-controlled hamburger menu with ARIA `expanded` state machine | `<details>`/`<summary>` | Native HTML provides keyboard + screen-reader support, no-JS fallback, zero code |
| Table responsiveness | Virtualized scroll / JS-reflow | CSS `overflow-x: auto` OR CSS card-stack via `data-label` + `::before` | Two proven CSS patterns cover all cases; no JS needed |
| Touch target sizing | JS to measure and pad | CSS `min-height: 44px; min-width: 44px; display: inline-flex; align-items: center` | CSS is the right tool; Phase 16 already uses this approach |
| Focus management in sidebar | JS focus trap | None needed — `<details>`/`<summary>` is native; no modal, no trap needed | Trap is for modals only |

---

## Common Pitfalls

### Pitfall 1: `<details>` Desktop Forced-Open via `open` Attribute Loses to CSS

**What goes wrong:** Adding `open` to `<details>` in server-rendered HTML works on initial
load, but if a user closes the `<details>` on mobile, navigates to a wider viewport, and
the CSS hides the `<summary>`, the `<details>` can appear closed on desktop with no way
to open it.

**How to avoid:** On desktop (≥769px), hide the `<summary>` AND force `display: block` on
the `<nav>` content regardless of `[open]` attribute state. Use JavaScript to set `open`
attribute on resize if needed (progressive enhancement), but the CSS no-JS fallback must
work. The server should always render `open` on the `<details>`.

**CSS fix:**
```css
@media (min-width: 769px) {
    .admin-sidebar__toggle { display: none; }
    .admin-sidebar__disclosure .admin-sidebar__nav,
    .admin-sidebar__disclosure .admin-sidebar__brand { display: block !important; }
}
```

### Pitfall 2: Token Mismatch Between Admin CSS and Public CSS

**What goes wrong:** Copying a rule from `site-common.css` that uses `--line`, `--ink`,
`--fs-sm` etc. into `admin-common.css` — these tokens are NOT defined in admin.css `:root`
(which uses `--border`, `--text`, `--muted` instead). The rule silently falls back to
browser defaults.

**How to avoid:** Before using any CSS custom property in `admin-common.css`, verify it
is defined in admin.css's `:root` block:
- Defined in admin: `--bg`, `--panel`, `--text`, `--muted`, `--accent`, `--border`, `--focus`
- NOT defined: `--line`, `--ink`, `--link`, `--fs-*`, `--on-accent`, `--danger`

When migrating admin-feedback-* rules from `site-common.css`, replace:
- `--line` → `--border`
- `--link` → `--accent`
- `--fs-sm` → `0.85em` or `13px`
- `--panel` → `--panel` (this one IS defined)

### Pitfall 3: `overflow-x: auto` Without `tabindex="0"` Blocks Keyboard Panning

**What goes wrong:** `overflow-x: auto` wrapper lets mouse/touch users scroll horizontally,
but keyboard users cannot pan the table content because the wrapper is not focusable.

**How to avoid:** Always add `tabindex="0"` AND `role="region"` + `aria-label` to the
scroll wrapper. This makes it keyboard-focusable and discoverable by screen readers.

### Pitfall 4: `data-label` Card-Stack Requires Markup Change in Views

**What goes wrong:** The CSS `::before { content: attr(data-label) }` technique requires
`data-label` attributes on every `<td>`. Forgetting this means the card stack shows
no column labels.

**How to avoid:** Add `data-label="Column Name"` to every `<td>` in every table that
uses `admin-table--card`. Also add `class="admin-table--card"` to the `<table>`. This
requires editing the Razor views (AdminFeedback/Index.cshtml, AdminFlags/Index.cshtml).

### Pitfall 5: `@import` in `admin.css` Shim May Cause FOUC

**What goes wrong:** CSS `@import` in a stylesheet causes the browser to fetch the imported
files sequentially, potentially causing a Flash of Unstyled Content if the imports are slow.

**How to avoid:** The shim is acceptable here because: (1) admin pages are password-protected
(no public performance SLA), and (2) both imported files will be served from the same origin
with `.asp-append-version` cache-busting. The FOUC risk is negligible for an admin-only UI.
If desired, the planner could instead use TWO `<link>` tags in `_AdminLayout.cshtml` directly,
eliminating `@import` entirely. This is a valid alternative — document in plan.

### Pitfall 6: Dead `admin-feedback-*` Rules in `site-common.css` Affect Public Pages If Not Cleaned Up

**What goes wrong:** Leaving `admin-action-form`, `detail-grid`, `type-badge` etc. in
`site-common.css` after copying them to `admin-common.css` means these class names are
styled in BOTH CSS files. If any public view ever accidentally uses these class names,
it gets admin-styled elements.

**How to avoid:** After moving rules to `admin-common.css`, remove them from `site-common.css`.
The `admin-action-form` duplicate in `site-common.css` (line 801) is the most dangerous —
it could affect any public view that includes a form with this class.

---

## Code Examples

### Sidebar Disclosure — Desktop/Mobile CSS Split [ASSUMED — pattern from WCAG/HTML spec]

```css
/* admin-common.css — desktop default: summary hidden, nav always visible */
.admin-sidebar__toggle {
    display: none;
}

/* admin-mobile.css — mobile: show toggle, collapse nav when <details> closed */
@media (max-width: 768px) {
    .admin-shell {
        grid-template-columns: 1fr;
        grid-template-rows: auto auto 1fr;
        grid-template-areas:
            "topbar"
            "sidebar"
            "content";
    }
    .admin-sidebar {
        border-right: none;
        border-bottom: 1px solid var(--border);
        padding: 0;
    }
    .admin-sidebar__toggle {
        display: flex;
        align-items: center;
        padding: 12px 20px;
        min-height: 44px;
        cursor: pointer;
        font-weight: 700;
        list-style: none;
        user-select: none;
        color: var(--text);
    }
    .admin-sidebar__toggle::-webkit-details-marker { display: none; }
    .admin-sidebar__toggle::marker { content: ""; }
}

/* Force nav visible on desktop regardless of [open] state */
@media (min-width: 769px) {
    .admin-sidebar__brand,
    .admin-sidebar__nav { display: block !important; }
}
```

### Touch Target Fix [VERIFIED pattern: admin-modal__button from admin.css Phase 16]

```css
/* admin-common.css */
.admin-shell .admin-action-form button {
    min-height: 44px;
    min-width: 44px;
    /* existing: background: var(--accent); color: #fff; border: none;
       padding: 6px 12px; border-radius: 3px; cursor: pointer; font-size: 13px; */
    /* ADD: */
    display: inline-flex;
    align-items: center;
    justify-content: center;
}

.admin-shell .admin-sidebar__link {
    min-height: 44px;
    display: flex;
    align-items: center;
}
```

### Import Shim [ASSUMED — CSS @import standard]

```css
/* DeckFlow Admin Shell — v1.4 Phase 18.
   Import shim only. Do not add rules here.
   Layout primitives and components → admin-common.css
   Responsive overrides (@media) → admin-mobile.css */
@import url('admin-common.css');
@import url('admin-mobile.css');
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|-----------------|--------------|--------|
| Checkbox-hack hamburger menus | `<details>`/`<summary>` disclosure | HTML5 / widely adopted ~2020 | Native keyboard + SR support, no JS |
| JS-only mobile menus | CSS `@media` + `<details>` | Ongoing | No-JS fallback requirement met |
| `display: none; @media display: table-row` for responsive tables | `overflow-x: auto` OR card-stack via `data-label` | ~2018 onward | Two patterns cover distinct use cases |
| WCAG 2.1 2.5.5 advisory | WCAG 2.2 2.5.5 normative (AA) | WCAG 2.2 published Oct 2023 | 44×44px touch target is now Level AA requirement |

**Deprecated / not applicable:**
- Bootstrap grid for admin responsive layout: rejected per project constraints (fights theme system).
- CSS Grid `repeat(auto-fill)` for sidebar: not needed; sidebar is fixed-width on desktop, full-width on mobile.

---

## Open Questions

1. **`@import` shim vs two `<link>` tags**
   - What we know: CSS `@import` in `admin.css` works but adds a serial fetch chain.
   - What's unclear: Whether the planner prefers the shim approach (no layout change) or two
     `<link>` tags (no `@import`, cleaner cascade).
   - Recommendation: Plan should document both; implement shim approach (matches roadmap spec)
     with a note that `<link>` variant is available if desired.

2. **`admin-harvest__panel` CSS gap**
   - What we know: `AdminHarvest/Index.cshtml` uses `class="admin-harvest__panel"` but there
     is NO CSS rule for this class in `admin.css` or anywhere.
   - What's unclear: Is this intentional (unstyled) or a pre-existing gap from Phase 7?
   - Recommendation: Phase 18 should add basic panel styling for `admin-harvest__panel`
     in `admin-common.css` as part of the layout-primitives work.

3. **Token additions for admin `:root`**
   - What we know: admin.css `:root` lacks `--fs-sm`, `--danger`, `--on-accent`, `--link`.
     Moving admin-feedback rules requires these or token substitutions.
   - What's unclear: Add these tokens to admin `:root` (more correct but touches `:root`),
     or substitute with literal values (simpler but harder to theme later).
   - Recommendation: Add `--danger: #dc2626; --on-accent: #fff;` to admin `:root` since
     they're already used by the Phase 16 modal rules via hardcoded literals. Avoids
     parallel maintenance of the same color.

4. **WCAG 2.5.8 spacing requirement**
   - What we know: WCAG 2.5.8 (Level AA) requires 24×24px minimum OR 24px spacing from
     adjacent targets. The `detail-actions` flex row has `gap: 0.5rem` between buttons,
     which may not satisfy 24px spacing if buttons are smaller than 44px.
   - What's unclear: Whether user requires 2.5.8 compliance or only 2.5.5.
   - Recommendation: Meeting 44×44px min-height/min-width per 2.5.5 with `gap: 8px`
     between buttons should satisfy 2.5.8 as well. [ASSUMED]

---

## Environment Availability

This phase has no external tool dependencies. All changes are CSS and Razor markup.

**Visual regression testing:**
No Playwright, no headless browser, no GitHub Actions workflow. The before/after
visual regression check (Phase 18 success criterion 5) must be performed via manual
browser DevTools mobile emulation at 375px in Rakdos, Azorius, Boros, and Gruul themes.

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|---------|
| `dotnet build` | Build verification | ✓ | .NET 10.0.300 | — |
| Browser DevTools mobile emulation | Visual regression | ✓ | Any modern browser | — |
| Playwright | Visual regression (automated) | ✗ | — | Manual screenshots |
| GitHub Actions | CI regression | ✗ | — | Manual push-and-verify |

**Missing dependencies with no fallback:** None that block execution.
**Missing dependencies with fallback:** Playwright → manual screenshots (acceptable per CLAUDE.md constraints).

---

## Validation Architecture

> `workflow.nyquist_validation` is explicitly `false` in `.planning/config.json`. Skipping this section.

---

## Security Domain

Phase 18 is CSS and HTML markup changes only. No server-side logic, no new endpoints, no
authentication changes, no data persistence. ASVS categories V2, V3, V4, V5, V6 are all
inapplicable. The admin shell is already protected by `BasicAuthMiddleware`.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | WCAG 2.5.5 is the applicable touch-target standard; 44×44px is the required floor | Touch-Target Audit | If 2.5.8 spacing is also required, gap widths between adjacent buttons need review |
| A2 | `overflow-x: auto` + `tabindex="0"` wrapper is sufficient for WCAG keyboard accessibility of comparison tables | Architecture Patterns / Pattern 2 | If ARIA role requirements are stricter, additional attributes may be needed |
| A3 | `@layer admin { }` is less appropriate than `.admin-shell` scoping for this codebase | Architecture Patterns / Scoping Discipline | If layer ordering becomes complex in future phases, `@layer` would have been preferable |
| A4 | 24px `gap: 8px` between `detail-actions` buttons satisfies WCAG 2.5.8 spacing | Open Questions | May need larger gap if 2.5.8 interpretation requires 24px measured center-to-center |
| A5 | Adding `--danger` and `--on-accent` to admin `:root` does not break existing Phase 16 modal rules | Open Questions / Token additions | Phase 16 modal uses hardcoded `#dc2626` for danger — adding the token makes both consistent |

---

## Sources

### Primary (HIGH confidence)
- Live codebase inspection: `admin.css`, `_AdminLayout.cshtml`, `site-common.css`, `_Layout.cshtml`, all admin Razor views — verified 2026-05-24
- `admin.css` Phase 16 modal block (lines 195–307) — pattern reference for `.admin-shell` scoping and `min-height: 44px`

### Secondary (MEDIUM confidence)
- WCAG 2.2 Success Criterion 2.5.5 (Target Size, Enhanced, Level AA) — 44×44 CSS pixel minimum [ASSUMED — from training knowledge, not fetched this session]
- HTML Living Standard `<details>`/`<summary>` disclosure pattern — native browser support confirmed as baseline since 2020 [ASSUMED — widely documented]

### Tertiary (LOW confidence)
- None.

---

## Metadata

**Confidence breakdown:**
- Codebase inventory: HIGH — direct file inspection of all CSS and admin Razor views
- CSS factoring plan: HIGH — mirrors existing `site-common.css` / `site-mobile.css` pattern
- Touch-target calculations: HIGH — computed from actual `admin.css` values
- WCAG standard applicability: MEDIUM — training knowledge, not fetched from official docs this session
- `<details>`/`<summary>` browser compatibility: MEDIUM — widely known, not fetched from MDN this session

**Research date:** 2026-05-24
**Valid until:** 2026-06-24 (CSS/HTML patterns are stable; no fast-moving dependencies)
