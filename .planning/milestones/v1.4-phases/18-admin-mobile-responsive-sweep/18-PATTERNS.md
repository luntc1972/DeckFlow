# Phase 18: Admin Mobile-Responsive Sweep — Pattern Map

**Mapped:** 2026-05-24
**Files analyzed:** 8 (3 CSS new/modified, 1 Razor layout, 4 Razor view modifications)
**Analogs found:** 8 / 8

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `wwwroot/css/admin-common.css` (NEW) | config/layout | request-response | `wwwroot/css/site-common.css` | exact-role |
| `wwwroot/css/admin-mobile.css` (NEW) | config/layout | request-response | `wwwroot/css/site-mobile.css` | exact-role |
| `wwwroot/css/admin.css` (MODIFY — import shim) | config | request-response | `wwwroot/css/site.css` (theme import pattern) | role-match |
| `Views/Shared/_AdminLayout.cshtml` (MODIFY) | view/layout | request-response | `Views/Shared/_Layout.cshtml` | exact-role |
| `Views/AdminFeedback/Index.cshtml` (MODIFY) | view/table | request-response | `Views/AdminFlags/Index.cshtml` (card-stack) | exact-role |
| `Views/AdminFeedback/Detail.cshtml` (MODIFY — token fix only) | view/detail | request-response | `Views/AdminFeedback/Detail.cshtml` itself | self-reference |
| `Views/AdminHarvest/Index.cshtml` (MODIFY) | view/table | request-response | `Views/AdminAnalytics/Index.cshtml` (overflow-x) | exact-role |
| `Views/AdminAnalytics/Index.cshtml` (MODIFY) | view/table | request-response | `Views/AdminHarvest/Index.cshtml` | exact-role |
| `Views/AdminFlags/Index.cshtml` (MODIFY) | view/table | request-response | `Views/AdminFeedback/Index.cshtml` | exact-role |

---

## Pattern Assignments

### `wwwroot/css/admin-common.css` (NEW — layout primitives)

**Analog:** `wwwroot/css/site-common.css`

**File header comment pattern** (`site-common.css` lines 1–12):
```css
:root {
  --shell-max-width: 1120px;
  color-scheme: light dark;
}

/* === Cross-cutting a11y foundation (WDG-08, Phase 11 Sweep 1) ===
   Source: ...
   Per CLAUDE.md D-07, cross-cutting layout/a11y CSS lives in site-common.css
   so all 22 guild themes inherit without per-fork edit.
   Do NOT duplicate these rules in site.css or theme forks. */
```
Admin equivalent comment: "Layout primitives and components. Mirrors site-common.css role
for admin shell. Loaded via @import from admin.css. No @media rules here — all responsive
overrides go in admin-mobile.css."

**`:root` token block to carry over** (`admin.css` lines 4–17):
```css
:root {
  --bg: #0f172a;
  --panel: #1e293b;
  --text: #e2e8f0;
  --muted: #94a3b8;
  --accent: #3b82f6;
  --border: #334155;
  --focus: var(--accent);
  color-scheme: dark;
}
```
New tokens to ADD to this block (resolves Open Question 3 from RESEARCH.md):
```css
  --danger: #dc2626;
  --on-accent: #fff;
```
These replace the hardcoded `#dc2626` already used in `admin.css` lines 203 and 208. The
modal block (currently in `admin.css` lines 195–307) moves into `admin-common.css` verbatim.

**Unscoped admin-only global resets** (`admin.css` lines 23–68 — must move into admin-common.css):
```css
/* admin-only global */
a:focus-visible,
button:focus-visible,
input:focus-visible,
select:focus-visible,
textarea:focus-visible,
summary:focus-visible,
[role="tab"]:focus-visible {
  outline: 2px solid var(--focus);
  outline-offset: 2px;
}

* { box-sizing: border-box; }

html, body {
  margin: 0;
  padding: 0;
  background: var(--bg);
  color: var(--text);
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
  font-size: 15px;
  line-height: 1.5;
}

a { color: var(--accent); text-decoration: none; }
a:hover { text-decoration: underline; }

.skip-link { ... }
.sr-only { ... }
```

**Shell layout block** (`admin.css` lines 71–193 — moves into admin-common.css):
```css
.admin-shell {
  display: grid;
  grid-template-columns: 220px 1fr;
  grid-template-rows: 48px 1fr;
  grid-template-areas:
    "sidebar topbar"
    "sidebar content";
  min-height: 100vh;
}
/* ... all .admin-sidebar, .admin-topbar, .admin-content,
   .admin-banner, .admin-table, .admin-action-form,
   .maintenance-page, .admin-analytics*, .admin-sparkline,
   .admin-empty blocks */
```

**Touch target pattern — existing analog** (`admin.css` lines 266–278, the Phase 16 modal button):
```css
.admin-modal__button {
  min-height: 44px;
  min-width: 44px;
  padding: 0 16px;
  border-radius: 3px;
  border: 1px solid transparent;
  font-size: 13px;
  font-weight: 600;
  line-height: 1.2;
  white-space: normal;
  cursor: pointer;
  transition: background-color 120ms ease, border-color 120ms ease;
}
```
Copy this `min-height: 44px; min-width: 44px; display: inline-flex; align-items: center`
pattern to `.admin-shell .admin-action-form button` and `.admin-shell .admin-sidebar__link`
in `admin-common.css`.

**Admin-feedback rules to migrate from `site-common.css`** (lines 776–810):
```css
/* site-common.css lines 776–810 — MOVE TO admin-common.css, adapting tokens: */
/* --line  -> --border   (admin token name) */
/* --link  -> --accent   */
/* --fs-sm -> 0.85em     (not defined in admin :root) */
/* --on-accent -> --on-accent (now defined, see above) */
/* --danger -> --danger  (now defined, see above) */
/* --panel -> --panel    (already defined in admin :root) */

.admin-feedback { max-width: 1100px; margin: 2rem auto; padding: 0 1rem; }
.admin-feedback-filters { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 1rem; }
.admin-feedback-filter {
    padding: 0.3rem 0.75rem;
    border: 1px solid var(--border);   /* was --line */
    border-radius: 4px;
    text-decoration: none;
    color: var(--accent);              /* was --accent-strong, inherit */
}
.admin-feedback-filter.active { background: var(--accent); color: var(--on-accent); }  /* was --link, --on-accent */
.admin-feedback-table { width: 100%; border-collapse: collapse; }
.admin-feedback-table th,
.admin-feedback-table td { padding: 0.5rem; border-bottom: 1px solid var(--border); text-align: left; vertical-align: top; }  /* was --line */
.admin-feedback-pagination { margin-top: 1rem; display: flex; gap: 0.75rem; align-items: center; }
.admin-feedback-empty { margin: 2rem 0; font-style: italic; }

.admin-feedback-detail {
    background: var(--panel);
    border: 1px solid var(--border);   /* was --line */
    padding: 1.5rem;
    border-radius: 8px;
    max-width: 800px;
    margin: 2rem auto;
}

.type-badge { display: inline-block; padding: 0.15rem 0.5rem; border-radius: 4px; font-size: 0.85em; background: var(--panel); border: 1px solid var(--border); }  /* --fs-sm -> 0.85em, --line -> --border */
.detail-grid { display: grid; grid-template-columns: max-content 1fr; gap: 0.4rem 1rem; }
.detail-grid dt { font-weight: 600; }
.detail-message { white-space: pre-wrap; word-wrap: break-word; background: rgba(0,0,0,0.05); padding: 1rem; border-radius: 4px; }
.detail-actions { margin-top: 1.5rem; display: flex; gap: 0.5rem; flex-wrap: wrap; }
.detail-actions button.danger { background: var(--danger); color: var(--on-accent); border: none; padding: 0.4rem 0.8rem; border-radius: 4px; cursor: pointer; }
```
After migration, REMOVE these rules from `site-common.css` lines 776–810.
Note: `.admin-action-form { display: inline; }` at `site-common.css:801` is a duplicate of
`admin.css:136` — remove it from `site-common.css` only; keep in admin-common.css.

**New: `admin-harvest__panel` gap fix** (no CSS currently; Open Question 2 from RESEARCH.md):
```css
/* admin-common.css — basic panel styling, no existing analog */
.admin-harvest__panel {
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 4px;
    padding: 16px 20px;
    margin-bottom: 16px;
}
.admin-harvest__panel h2 {
    margin: 0 0 12px;
    font-size: 15px;
    font-weight: 600;
}
```

**New: `admin-sidebar__disclosure` desktop default** (desktop: hide toggle, content always visible):
```css
/* admin-common.css */
.admin-sidebar__toggle {
    display: none;   /* hidden on desktop; shown in admin-mobile.css */
}
```

**New: touch target additions** (pattern from `admin-modal__button`, `admin.css` lines 266–278):
```css
/* admin-common.css */
.admin-shell .admin-action-form button {
    min-height: 44px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
}

.admin-shell .admin-sidebar__link {
    min-height: 44px;
    display: flex;
    align-items: center;
}

.admin-analytics .admin-range-selector a {
    min-height: 44px;
    display: inline-flex;
    align-items: center;
}
```

**Scroll-wrapper base rule** (defined in common so mobile can override):
```css
/* admin-common.css */
.admin-table-scroll {
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
}
```

**`@media (prefers-reduced-motion)` for modal** (from `admin.css` lines 300–305 — moves verbatim):
```css
@media (prefers-reduced-motion: reduce) {
  .admin-modal,
  dialog.admin-modal::backdrop {
    transition: none;
  }
}
```
This is the ONE permitted `@media` block in `admin-common.css` — it is an a11y preference query,
not a viewport breakpoint. The no-`@media`-in-common rule applies to viewport (`max-width`) rules only.

---

### `wwwroot/css/admin-mobile.css` (NEW — all viewport @media rules)

**Analog:** `wwwroot/css/site-mobile.css`

**File structure pattern** (`site-mobile.css` lines 1–3):
```css
/* Mobile responsive overrides — loaded after theme to win the cascade.
   Rules targeting selectors that themes redefine live here. */
```
Admin equivalent comment: "Admin responsive overrides — all @media (max-width:…) rules for
the admin shell. Loaded via @import in admin.css after admin-common.css. No non-@media rules
here. Breakpoints: 768px (sidebar + form stack), 900px (table scroll tweak if needed)."

**Breakpoint structure pattern** (`site-mobile.css`):
```css
/* site-mobile.css uses three breakpoints: 900px, 768px, 600px, 480px */
/* admin-mobile.css uses: 768px primary (sidebar, forms, tables), 900px if needed */

@media (max-width: 900px) {
  /* ...toolbar collapses in site-mobile.css lines 5–19 */
}
@media (max-width: 768px) {
  /* ...sync-column, page-frame collapses in site-mobile.css lines 21–25 */
}
```

**Shell grid collapse** (new, no direct analog — derived from RESEARCH.md Pattern 1):
```css
/* admin-mobile.css */
@media (max-width: 768px) {
    .admin-shell {
        grid-template-columns: 1fr;
        grid-template-rows: auto auto 1fr;
        grid-template-areas:
            "topbar"
            "sidebar"
            "content";
    }
}
```

**Sidebar disclosure toggle** (new, no direct analog):
```css
/* admin-mobile.css */
@media (max-width: 768px) {
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

    .admin-sidebar__disclosure:not([open]) .admin-sidebar__nav,
    .admin-sidebar__disclosure:not([open]) .admin-sidebar__brand {
        display: none;
    }
}
```

**Desktop forced-open guard** (Pitfall 1 from RESEARCH.md — prevents closed state persisting on resize):
```css
/* admin-mobile.css — MUST be in the mobile file to override any narrow-viewport state */
@media (min-width: 769px) {
    .admin-sidebar__toggle { display: none; }
    .admin-sidebar__disclosure .admin-sidebar__nav,
    .admin-sidebar__disclosure .admin-sidebar__brand { display: block !important; }
}
```

**Form single-column** (analog: `site-mobile.css` lines 14–18 toolbar collapse):
```css
/* site-mobile.css toolbar pattern:
@media (max-width: 900px) {
  .toolbar { align-items: stretch; }
  .toolbar button { width: 100%; }
}
*/

/* admin-mobile.css — same intent, applied to .admin-action-form */
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
    }
}
```

**Table overflow-x** (for Analytics and HarvestRuns — comparison-dense tables):
```css
/* admin-mobile.css */
@media (max-width: 768px) {
    .admin-table-scroll {
        padding-bottom: 4px;   /* iOS scrollbar visibility */
    }
}
```

**Card-stack pattern** (for Feedback list and Flags — scanning tables):
```css
/* admin-mobile.css */
@media (max-width: 768px) {
    .admin-table--card thead {
        display: none;
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

---

### `wwwroot/css/admin.css` (MODIFY — becomes import shim)

**Analog:** guild theme CSS files (e.g. `site-azorius.css`) which `@import url('site.css')` then
add overrides. The shim pattern is the same: one file becomes purely an import entry point.

**Exact shim content** (RESEARCH.md CSS Factoring Plan):
```css
/* DeckFlow Admin Shell — v1.4 Phase 18.
   Import shim only. Do not add rules here.
   Layout primitives and components  ->  admin-common.css
   Responsive overrides (@media)     ->  admin-mobile.css */
@import url('admin-common.css');
@import url('admin-mobile.css');
```

**`<link>` tag in `_AdminLayout.cshtml` stays unchanged** (`_AdminLayout.cshtml` line 14):
```html
<link rel="stylesheet" href="~/css/admin.css" asp-append-version="true" />
```
The `asp-append-version="true"` cache-busting remains on `admin.css`. The two imported files
do NOT get individual `asp-append-version` treatment (CSS `@import` bypasses the ASP.NET
tag helper pipeline). Acceptable for admin-only UI per RESEARCH.md Pitfall 5 analysis.

---

### `Views/Shared/_AdminLayout.cshtml` (MODIFY — sidebar markup)

**Analog:** `Views/Shared/_Layout.cshtml` lines 40–48 (CSS link pattern) and lines 50–68 (shell structure).

**Current sidebar markup** (`_AdminLayout.cshtml` lines 19–27 — the exact block to modify):
```html
<aside class="admin-sidebar" aria-label="Admin sections">
    <div class="admin-sidebar__brand">DeckFlow Admin</div>
    <nav class="admin-sidebar__nav">
        <a class="@ActiveClass("AdminFeedback")"  aria-current="@ActiveAria("AdminFeedback")"  href="@Url.Content("~/Admin/Feedback")">Feedback</a>
        <a class="@ActiveClass("AdminHarvest")"   aria-current="@ActiveAria("AdminHarvest")"   href="@Url.Content("~/Admin/Harvest")">Harvest</a>
        <a class="@ActiveClass("AdminAnalytics")" aria-current="@ActiveAria("AdminAnalytics")" href="@Url.Content("~/Admin/Analytics")">Analytics</a>
        <a class="@ActiveClass("AdminFlags")"     aria-current="@ActiveAria("AdminFlags")"     href="@Url.Content("~/Admin/Flags")">Flags</a>
    </nav>
</aside>
```

**Target markup** (adds `<details>`/`<summary>` wrapper; `<a>` links and Razor helpers unchanged):
```html
<aside class="admin-sidebar" aria-label="Admin sections">
    <details class="admin-sidebar__disclosure" open>
        <summary class="admin-sidebar__toggle">
            <span class="sr-only">Navigation menu</span>
            <span aria-hidden="true">&#9776; Menu</span>
        </summary>
        <div class="admin-sidebar__brand">DeckFlow Admin</div>
        <nav class="admin-sidebar__nav">
            <a class="@ActiveClass("AdminFeedback")"  aria-current="@ActiveAria("AdminFeedback")"  href="@Url.Content("~/Admin/Feedback")">Feedback</a>
            <a class="@ActiveClass("AdminHarvest")"   aria-current="@ActiveAria("AdminHarvest")"   href="@Url.Content("~/Admin/Harvest")">Harvest</a>
            <a class="@ActiveClass("AdminAnalytics")" aria-current="@ActiveAria("AdminAnalytics")" href="@Url.Content("~/Admin/Analytics")">Analytics</a>
            <a class="@ActiveClass("AdminFlags")"     aria-current="@ActiveAria("AdminFlags")"     href="@Url.Content("~/Admin/Flags")">Flags</a>
        </nav>
    </details>
</aside>
```
Key points: `open` attribute rendered server-side always; Razor `ActiveClass`/`ActiveAria`
helpers on the `<a>` tags are unchanged; `&#9776;` is the hamburger character (HTML entity,
no font dependency).

**viewport meta tag** — already present at `_AdminLayout.cshtml` line 12:
```html
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
```
No change needed.

---

### `Views/AdminFeedback/Index.cshtml` (MODIFY — card-stack table)

**Analog:** `Views/AdminFlags/Index.cshtml` (same table structure, same card-stack strategy)

**Current table tag** (`AdminFeedback/Index.cshtml` line 49):
```html
<table class="admin-feedback-table">
```

**Target table tag** (add `admin-table--card` modifier class):
```html
<table class="admin-feedback-table admin-table--card">
```

**Current `<td>` rows** (`AdminFeedback/Index.cshtml` lines 65–83 — no `data-label`):
```html
<td><time datetime="@item.CreatedUtc.ToString("o")" ...>@item.CreatedUtc.ToString("yyyy-MM-dd HH:mm")</time></td>
<td><span class="type-badge type-@item.Type.ToString().ToLower()">@item.Type</span></td>
<td>@preview</td>
<td>@item.Email</td>
<td>@item.Status</td>
<td>
    <a asp-action="Detail" asp-route-id="@item.Id">View</a>
    ...
</td>
```

**Target `<td>` rows** (add `data-label` matching the `<th>` text):
```html
<td data-label="Created (UTC)"><time datetime="@item.CreatedUtc.ToString("o")" ...>@item.CreatedUtc.ToString("yyyy-MM-dd HH:mm")</time></td>
<td data-label="Type"><span class="type-badge type-@item.Type.ToString().ToLower()">@item.Type</span></td>
<td data-label="Message">@preview</td>
<td data-label="Email">@item.Email</td>
<td data-label="Status">@item.Status</td>
<td data-label="Actions">
    <a asp-action="Detail" asp-route-id="@item.Id">View</a>
    ...
</td>
```
No other changes to this view.

---

### `Views/AdminFlags/Index.cshtml` (MODIFY — card-stack table)

**Analog:** `Views/AdminFeedback/Index.cshtml` (same card-stack treatment)

**Current table tag** (`AdminFlags/Index.cshtml` line 19):
```html
<table class="admin-table">
```

**Target table tag:**
```html
<table class="admin-table admin-table--card">
```

**Current `<td>` rows** (`AdminFlags/Index.cshtml` lines 31–43 — no `data-label`):
```html
<td><code>@flag.Key</code></td>
<td>@(flag.Enabled ? "On" : "Off")</td>
<td>
    <form method="post" ... class="admin-action-form">
        ...
        <button type="submit">@(flag.Enabled ? "Disable" : "Enable")</button>
    </form>
</td>
```

**Target `<td>` rows** (add `data-label` matching `<th>` text):
```html
<td data-label="Key"><code>@flag.Key</code></td>
<td data-label="Status">@(flag.Enabled ? "On" : "Off")</td>
<td data-label="Action">
    <form method="post" ... class="admin-action-form">
        ...
        <button type="submit">@(flag.Enabled ? "Disable" : "Enable")</button>
    </form>
</td>
```

---

### `Views/AdminHarvest/Index.cshtml` (MODIFY — overflow-x scroll wrapper)

**Analog:** `Views/AdminAnalytics/Index.cshtml` (same overflow-x strategy)

**Current table markup — Recent Runs** (`AdminHarvest/Index.cshtml` lines 140–162):
```html
<table class="admin-table">
    <caption class="sr-only">Recent harvest runs ...</caption>
    <thead>...</thead>
    <tbody>...</tbody>
</table>
```

**Target markup** (wrap in scroll div; add `role`, `aria-label`, `tabindex`):
```html
<div class="admin-table-scroll" role="region" aria-label="Recent harvest runs — scroll horizontally to see all columns" tabindex="0">
    <table class="admin-table">
        <caption class="sr-only">Recent harvest runs ...</caption>
        <thead>...</thead>
        <tbody>...</tbody>
    </table>
</div>
```

**Current table markup — Run Log** (`AdminHarvest/Index.cshtml` lines 170–196):
```html
<table class="admin-table">
    <caption class="sr-only">Harvest run log ...</caption>
    ...
</table>
```

**Target markup:**
```html
<div class="admin-table-scroll" role="region" aria-label="Harvest run log — scroll horizontally to see all columns" tabindex="0">
    <table class="admin-table">
        <caption class="sr-only">Harvest run log ...</caption>
        ...
    </table>
</div>
```
No `data-label` changes needed — overflow-x tables keep their `<thead>`.

---

### `Views/AdminAnalytics/Index.cshtml` (MODIFY — overflow-x scroll wrapper)

**Analog:** `Views/AdminHarvest/Index.cshtml` (same overflow-x strategy)

**Current table markup** (`AdminAnalytics/Index.cshtml` lines 31–55):
```html
<table class="admin-table admin-analytics-table">
    <caption class="sr-only">Top routes by hit count ...</caption>
    <thead>...</thead>
    <tbody>...</tbody>
</table>
```

**Target markup:**
```html
<div class="admin-table-scroll" role="region" aria-label="Page analytics — scroll horizontally to see all columns" tabindex="0">
    <table class="admin-table admin-analytics-table">
        <caption class="sr-only">Top routes by hit count ...</caption>
        <thead>...</thead>
        <tbody>...</tbody>
    </table>
</div>
```
The `.admin-sparkline` spark column needs `min-width` protection so it does not collapse:
```css
/* admin-common.css addition alongside existing .admin-analytics-table .spark rule */
.admin-analytics-table .spark { width: 130px; min-width: 80px; }
```

---

## Shared Patterns

### CSS File Split Architecture

**Source:** `site-common.css` + `site-mobile.css` + `site.css` relationship (verified from
`_Layout.cshtml` lines 44–47)

**Apply to:** `admin-common.css` + `admin-mobile.css` + `admin.css` (shim)

Rule: layout primitives + component rules = common file; `@media (max-width:…)` rules = mobile
file; entry-point shim = `admin.css` with two `@import` lines. No viewport `@media` in common
file; no non-`@media` rules in mobile file (except the file-level comment).

One permitted exception: `@media (prefers-reduced-motion: reduce)` lives in `admin-common.css`
alongside the modal rules it guards — this is an a11y preference query, not a viewport query,
and the modal block it modifies belongs in `admin-common.css`.

### `.admin-shell` Scoping Discipline

**Source:** `admin.css` lines 202–214 (Phase 16 modal block scoping):
```css
.admin-shell .admin-action-form button.danger,
.admin-shell .admin-modal__button--danger { ... }
```

**Apply to:** All new component rules in `admin-common.css` that are not unscoped admin-only globals.

Rule: component rules use `.admin-shell .selector`; unscoped global resets (`html, body`, `*`,
`a`) stay unscoped but are marked `/* admin-only global */`.

### `asp-append-version` on `<link>` Tags

**Source:** `_Layout.cshtml` lines 44–47:
```html
<link rel="stylesheet" href="~/css/site-common.css" asp-append-version="true" />
<link id="theme-stylesheet" rel="stylesheet" href="@selectedThemeHref" ... />
<link rel="stylesheet" href="~/css/site-theme-overrides.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/site-mobile.css" asp-append-version="true" />
```

**Apply to:** `_AdminLayout.cshtml` line 14 — keep `asp-append-version="true"` on the single
`<link href="~/css/admin.css">` tag. The two `@import`-ed files inherit cache-busting from
`admin.css`'s version query string only (not independently versioned — acceptable per RESEARCH.md).

### Touch Target Floor (44px)

**Source:** `admin.css` lines 266–278 (Phase 16 `admin-modal__button`):
```css
.admin-modal__button {
  min-height: 44px;
  min-width: 44px;
  padding: 0 16px;
  ...
}
```

**Apply to:** Every admin interactive element in `admin-common.css`:
- `.admin-shell .admin-action-form button`
- `.admin-shell .admin-sidebar__link`
- `.admin-analytics .admin-range-selector a`

Pattern: `min-height: 44px; min-width: 44px; display: inline-flex; align-items: center`

### Token Reference Discipline

**Source:** `admin.css` `:root` block (lines 4–17)

**Apply to:** All new rules in `admin-common.css` and `admin-mobile.css`

Admin-defined tokens (safe to use): `--bg`, `--panel`, `--text`, `--muted`, `--accent`,
`--border`, `--focus`, `--danger` (new), `--on-accent` (new)

Banned tokens (public-theme only, NOT in admin `:root`): `--line`, `--ink`, `--link`,
`--fs-sm`, `--fs-*`, `--accent-strong`, `--on-accent` (before the new token addition)

---

## No Analog Found

All files have close analogs. No entries in this section.

---

## Metadata

**Analog search scope:**
- `DeckFlow.Web/wwwroot/css/` — all CSS files read
- `DeckFlow.Web/Views/Shared/` — `_Layout.cshtml`, `_AdminLayout.cshtml`
- `DeckFlow.Web/Views/Admin*/` — all five admin table views read

**Files scanned:** 12 (5 CSS, 7 Razor)
**Pattern extraction date:** 2026-05-24

**Key constraints confirmed from codebase:**
1. `_AdminLayout.cshtml` line 14: single `<link href="~/css/admin.css">` — no change required for the CSS split
2. `admin.css` has zero `@media` blocks — all responsive CSS is net-new
3. `site-common.css` lines 776–810: admin-feedback rules are dead code on admin pages (confirmed: `_AdminLayout.cshtml` never loads `site-common.css`)
4. Phase 16 modal block (`admin.css` lines 195–307) is the established `.admin-shell` scoping pattern and the `min-height: 44px` template
5. `admin-harvest__panel` class is used in `AdminHarvest/Index.cshtml` (lines 31, 71, 81, 107) but has no CSS definition anywhere — add basic panel styling in `admin-common.css`
