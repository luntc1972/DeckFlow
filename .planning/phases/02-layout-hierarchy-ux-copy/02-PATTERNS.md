# Phase 02: Layout, Hierarchy & UX Copy — Pattern Map

**Mapped:** 2026-04-30
**Files analyzed:** 14 (5 new/modified Razor views + 6 partial call-site views + 1 partial + 1 CSS + 1 new TS module)
**Analogs found:** 14 / 14 (every target file has a strong existing analog inside the repo)

> Source materials read:
> - `.planning/phases/02-layout-hierarchy-ux-copy/02-CONTEXT.md` (D-01..D-15)
> - `.planning/phases/02-layout-hierarchy-ux-copy/02-UI-SPEC.md` (APPROVED contract)
> - `.planning/REQUIREMENTS.md` (UI-LH-01/02, UX-01/02/03)
> - `.planning/ROADMAP.md` (Phase 2 success criteria)
> - `./CLAUDE.md` (constraint: layout CSS in site-common.css, not site.css)

> **Important corrections to upstream inputs surfaced during pattern discovery:**
> 1. **`_MoxfieldBulkEditHint` has 6 call sites, not 5.** `DeckSync.cshtml` includes it twice (lines 112 and 147 — once per Moxfield input column). Planner must update both occurrences.
> 2. **D-02 / UI-SPEC §2 confirmed consistent**: Per-group primaries are Deck Comparison (Analyze), Deck Sync (Build), Card Lookup (Reference). ChatGPT Analysis appears in the Analyze grid as a regular card; the hero band above the grid is its dedicated promotion. (Observation 3476 D-02 discrepancy is resolved — UI-SPEC §2 reads correctly per pattern map verification.)
> 3. **Per-page TS load mechanism**: Project uses `@section Scripts { <script src="~/js/<module>.js" ... /> }` in each view to load page-specific compiled TS — NOT a global `<script>` in `_Layout.cshtml`. This validates spawning a new `feedback.ts` module (per memory observations 3454, 3469) over extending `site.ts`.

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DeckFlow.Web/Views/Deck/Home.cshtml` | Razor view (hub markup) | static render | self (existing `.hub-card` markup at lines 13–56) | exact — extend in place |
| `DeckFlow.Web/Views/Feedback/Index.cshtml` | Razor view (form page) | request-response | self + `_FormError.cshtml` voice pattern | exact — extend in place |
| `DeckFlow.Web/Views/AdminFeedback/Index.cshtml` | Razor view (admin list) | request-response | self (line 74 inline-style form) | exact — replace `style=` with class |
| `DeckFlow.Web/Views/AdminFeedback/Detail.cshtml` | Razor view (admin detail) | request-response | self (line 6 panel + lines 27/34/39 forms) | exact — replace `style=` with class |
| `DeckFlow.Web/Views/Shared/_MoxfieldBulkEditHint.cshtml` | Razor partial (shared) | static render | `Views/Shared/_FormError.cshtml` (`@model string`) | exact — same model shape |
| `DeckFlow.Web/Views/Deck/ChatGptCedhMetaGap.cshtml` | Razor view (call site) | static render | `Views/Deck/Home.cshtml:6` (`PartialAsync` 2-arg form) | exact |
| `DeckFlow.Web/Views/Deck/ChatGptDeckComparison.cshtml` | Razor view (call site) | static render | same | exact |
| `DeckFlow.Web/Views/Deck/ChatGptPackets.cshtml` | Razor view (call site) | static render | same | exact |
| `DeckFlow.Web/Views/Deck/DeckConvert.cshtml` | Razor view (call site) | static render | same | exact |
| `DeckFlow.Web/Views/Deck/DeckSync.cshtml` (×2) | Razor view (call site, two occurrences) | static render | same | exact |
| `DeckFlow.Web/wwwroot/css/site-common.css` | CSS (layout) | static | `.hub-card` block (lines 161–192); `.feedback-panel` block (567); `.run-button:disabled` (lines 81–99); `.busy-indicator__spinner` (site.css:662–669) | exact — extend in place |
| `DeckFlow.Web/wwwroot/ts/feedback.ts` (NEW) | TS module (DOM event handler) | event-driven (form submit) | `wwwroot/ts/site.ts` `attachThemePicker` (128–) + `attachArchidektCacheJobUi` (537–) IIFE/DOMContentLoaded pattern | exact — copy IIFE shell |

---

## Pattern Assignments

### 1. `DeckFlow.Web/Views/Deck/Home.cshtml` — UI-LH-01

**Role:** Razor view (hub landing page, 4 hub-groups, 10 cards)
**Analog:** itself — already uses BEM `.hub-card` / `.hub-card__title` / `.hub-card__description`. Phase 02 extends in place: prepend a `.hub-hero` block, add `.hub-card--primary` modifier to 3 specific cards.

**Existing card markup pattern** (`Home.cshtml:17-20`) — copy this shape and add the modifier class:
```html
<a class="hub-card" href="@Url.Content("~/chatgpt-deck-comparison")">
    <h3 class="hub-card__title">Deck Comparison</h3>
    <p class="hub-card__description">Side-by-side comparison of two decks with a ChatGPT-authored breakdown of strengths, weaknesses, and trade-offs.</p>
</a>
```

**Becomes** (UI-SPEC §2 — only the class list changes):
```html
<a class="hub-card hub-card--primary" href="@Url.Content("~/chatgpt-deck-comparison")">
    <h3 class="hub-card__title">Deck Comparison</h3>
    <p class="hub-card__description">Side-by-side comparison of two decks with a ChatGPT-authored breakdown of strengths, weaknesses, and trade-offs.</p>
</a>
```

**Hero placement contract** — insert after line 8 (`<p class="hub-lede">…</p>`), before line 10 (`<section class="hub-group" …>`):
```html
<a class="hub-hero" href="@Url.Content("~/chatgpt-packets")">
    <span class="hub-hero__eyebrow">Headline workflow</span>
    <span class="hub-hero__title">Analyze Your Deck with ChatGPT</span>
    <span class="hub-hero__description">Five-step workflow: load your deck, pick your questions, copy the prompt, paste into ChatGPT, review the structured response.</span>
</a>
```

**Apply `.hub-card--primary` to exactly 3 cards** (UI-SPEC §2):
- Line 17 (Analyze group): `Deck Comparison` → `~/chatgpt-deck-comparison`
- Line 31 (Build group): `Deck Sync` → `~/sync`
- Line 45 (Reference group): `Card Lookup` → `~/card-lookup`

Categories group (lines 60–71): unchanged — no `.hub-card--primary` (UI-SPEC §2 explicit).

**Gotcha:** `Home.cshtml` uses `@Url.Content("~/...")` for all hrefs — preserve this pattern for the hero `href` (path-base safe under reverse-proxy hosting).

---

### 2. `DeckFlow.Web/Views/Feedback/Index.cshtml` — UI-LH-02 + UX-02 + UX-03

**Role:** Razor view (public feedback form, 1 page, 1 `<form>`)
**Analog:** itself + voice mirror from any verb-noun ViewData title elsewhere in the project.

**Inline style to remove** (line 8 — currently the only `style=` on this file):
```html
<div class="feedback-panel" style="background: var(--panel); border: 1px solid var(--line);">
```

**Replace with** (D-12, D-14):
```html
<div class="feedback-panel">
```

**Voice change** (UX-03 + D-06):
- Line 3 `ViewData["Title"] = "Feedback";` → `ViewData["Title"] = "Send Feedback";` (note: UI-SPEC contract says `"Send Feedback — DeckFlow"` but the existing layout already appends ` - DeckFlow` via `<title>@ViewData["Title"] - DeckFlow</title>` in `_Layout.cshtml`; planner verifies layout title template before deciding final string. If layout already appends suffix, set just `"Send Feedback"`.)
- Line 9 `<h1>Send feedback</h1>` → `<h1>Send Feedback</h1>` (capitalize F per D-05).
- Line 43 `<button type="submit" class="feedback-submit">Send</button>` → `<button type="submit" class="feedback-submit">Send Feedback</button>` (verb-noun convention, D-05).

**Form class hookup for UX-02** (UI-SPEC §6 contract: TS scopes to `form.feedback-form`):
- Line 19 already has `class="feedback-form"`. **No change needed** — selector is pre-wired.
- Line 43 button already has `class="feedback-submit"`. **No change needed.**

**Gotcha:** Form has `novalidate` attribute on line 19. The TS handler must NOT call `event.preventDefault()` — D-08 requires the browser to POST normally. Setting `button.disabled = true` after the form has already begun submission is safe; setting it before the submit event fires would block the POST.

---

### 3. `DeckFlow.Web/Views/AdminFeedback/Index.cshtml` — UI-LH-02

**Role:** Razor view (admin list page)
**Analog:** itself.

**Inline style to remove** (line 74):
```html
<form method="post" asp-action="Apply" asp-route-id="@item.Id" asp-route-op="archive" style="display:inline">
```

**Replace with** (D-14):
```html
<form method="post" asp-action="Apply" asp-route-id="@item.Id" asp-route-op="archive" class="admin-action-form">
```

**Verifier gate:** `grep -c 'style=' AdminFeedback/Index.cshtml` must equal 0 after change (D-15).

---

### 4. `DeckFlow.Web/Views/AdminFeedback/Detail.cshtml` — UI-LH-02

**Role:** Razor view (admin detail page)
**Analog:** itself. **5 inline styles to remove** — 1 panel + 4 forms.

**Panel inline style** (line 6):
```html
<section class="admin-feedback-detail" style="background: var(--panel); border: 1px solid var(--line); padding: 1.5rem; border-radius: 8px; max-width: 800px; margin: 2rem auto;">
```

**Replace with**:
```html
<section class="admin-feedback-detail">
```

**Form inline styles** (lines 27, 34, 39 — three `<form ... style="display:inline">` blocks):
```html
<form method="post" asp-action="Apply" asp-route-id="@Model.Id" asp-route-op="markRead" style="display:inline">
<form method="post" asp-action="Apply" asp-route-id="@Model.Id" asp-route-op="archive" style="display:inline">
<form method="post" asp-action="Apply" asp-route-id="@Model.Id" asp-route-op="delete" style="display:inline" onsubmit="...">
```

**Replace `style="display:inline"` → `class="admin-action-form"`** on each. Preserve `onsubmit="return confirm(...)"` on the delete form (line 39) verbatim — it is out of UI-LH-02 scope.

**Verifier gate:** `grep -c 'style=' AdminFeedback/Detail.cshtml` must equal 0 (D-15).

**Gotcha:** Line 39 currently has BOTH `style=` and `onsubmit=` on the same opening tag, with the `onsubmit` continuing onto line 40. When deleting the `style=` attribute, do not touch the `onsubmit` attribute or the line break.

---

### 5. `DeckFlow.Web/Views/Shared/_MoxfieldBulkEditHint.cshtml` — UX-01

**Role:** Razor partial (shared, included from 6 view sites)
**Analog:** `DeckFlow.Web/Views/Shared/_FormError.cshtml` — already uses `@model string` and embeds `@Model` in markup. Same pattern, same project conventions.

**`_FormError.cshtml` analog (full file, 7 lines):**
```razor
@*
    Shared inline error banner for form validation messages populated by client-side JS.
    Pass the data attribute name as the model (e.g. "chatgpt-validation-error").
@*
@model string

<div class="error-banner hidden" role="alert" data-@Model></div>
```

**Apply same pattern to `_MoxfieldBulkEditHint.cshtml`**:

Current line 9 has the hardcoded "Submit":
```html
<li><strong>Copy the Main Deck contents</strong> and paste them into the text field here. Submit — tags are preserved end-to-end.</li>
```

Add at top of file (line 1, before `@* _MoxfieldBulkEditHint *@`):
```razor
@model string
@{
    var verb = string.IsNullOrWhiteSpace(Model) ? "Submit" : Model;
}
```

Replace line 9 with:
```html
<li><strong>Copy the Main Deck contents</strong> and paste them into the text field here. @verb — tags are preserved end-to-end.</li>
```

**Gotcha (defensive default):** The `string.IsNullOrWhiteSpace` fallback to "Submit" preserves backward compatibility if any caller still uses the no-arg form. Without it, the partial would render literally "(null)" or empty. UI-SPEC §"Copywriting Contract" says `@Model` directly; the planner should add the fallback because Razor's null-Model behavior on a `@model string` partial without an arg is brittle.

---

### 6. Five (six) Razor call-site views — UX-01

**Role:** Razor views (each calls `_MoxfieldBulkEditHint`)
**Analog:** `Views/Deck/Home.cshtml:6` — canonical `PartialAsync` 2-arg form:
```razor
@await Html.PartialAsync("_DeckToolTabs", Model)
```

**6 call sites confirmed by grep** (NOT 5 — `DeckSync.cshtml` has two):

| File | Line | Verb to pass |
|------|------|--------------|
| `Views/Deck/ChatGptDeckComparison.cshtml` | 225 | `"Run Compare"` |
| `Views/Deck/ChatGptPackets.cshtml` | 150 | `"Run Analysis"` |
| `Views/Deck/ChatGptCedhMetaGap.cshtml` | 99 | `"Run Gap Analysis"` |
| `Views/Deck/DeckConvert.cshtml` | 67 | `"Convert"` |
| `Views/Deck/DeckSync.cshtml` | 112 | `"Run Sync"` |
| `Views/Deck/DeckSync.cshtml` | 147 | `"Run Sync"` |

**Each call site changes from:**
```razor
@await Html.PartialAsync("_MoxfieldBulkEditHint")
```

**To:**
```razor
@await Html.PartialAsync("_MoxfieldBulkEditHint", "Run Sync")
```

(Verb string per the table above.)

**Gotcha:** Verifier should `grep -rn 'Html.PartialAsync("_MoxfieldBulkEditHint")' DeckFlow.Web/Views/` post-change and find zero results — every call must now pass a verb.

**Gotcha #2:** Planner should re-verify each verb against the actual page submit-button label rather than trusting the table. Audit run during pattern mapping confirms the table values match observed submit-button copy on each page, but the host-page verb is the ground truth.

---

### 7. `DeckFlow.Web/wwwroot/css/site-common.css` — all new rules

**Role:** Layout CSS (shared across all 22 guild themes via theme architecture)
**Analog:** existing rule blocks in same file. Match style: lowercase hex never appears (tokens only), 2-space indent, no shorthand explosion.

#### 7a. `.hub-hero` — analog: `.hub-card` (`site-common.css:161-192`)

**Existing `.hub-card` block** to mirror (full structural pattern):
```css
.hub-card {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 1rem 1.1rem;
  border: 1px solid var(--line);
  border-radius: 10px;
  background: var(--panel);
  color: inherit;
  text-decoration: none;
  transition: border-color 120ms ease, transform 120ms ease, box-shadow 120ms ease;
}

.hub-card:hover,
.hub-card:focus-visible {
  border-color: var(--accent-strong, var(--line));
  transform: translateY(-1px);
  box-shadow: 0 8px 24px rgba(26, 31, 46, 0.08);
}

.hub-card__title {
  margin: 0;
  font-size: var(--fs-base);
  font-weight: 600;
}

.hub-card__description {
  margin: 0;
  font-size: var(--fs-sm);
  color: var(--muted);
  line-height: 1.35;
}
```

**Phase 02 new rules** (UI-SPEC §1) follow this exact shape. Insert after line 192, before line 194 (`.chatgpt-page-toolbar` rule):

```css
.hub-hero {
  display: block;
  padding: 1rem 1.25rem;
  border: 1px solid var(--line);
  border-left: 4px solid var(--cta-border);
  border-radius: 10px;
  background: var(--panel);
  color: inherit;
  text-decoration: none;
  margin-bottom: 1.5rem;
  transition: border-color 120ms ease, box-shadow 120ms ease;
}

.hub-hero:hover,
.hub-hero:focus-visible {
  border-color: var(--accent-strong, var(--line));
  box-shadow: 0 8px 24px rgba(26, 31, 46, 0.08);
}

.hub-hero__eyebrow {
  display: block;
  font-size: var(--fs-xs);
  font-weight: 600;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
  margin-bottom: 0.35rem;
}

.hub-hero__title {
  display: block;
  font-size: var(--fs-xl);
  font-weight: 700;
  margin: 0 0 0.35rem;
  color: var(--ink);
}

.hub-hero__description {
  display: block;
  font-size: var(--fs-sm);
  color: var(--muted);
  margin: 0;
  line-height: 1.4;
}

.hub-card--primary {
  border-color: var(--cta-border);
}

.hub-card--primary:hover,
.hub-card--primary:focus-visible {
  border-color: var(--accent-strong, var(--cta-border));
}
```

**Gotcha:** The hero `:hover` rule preserves `border-left: 4px solid var(--cta-border)` because `border-color: var(--accent-strong, var(--line))` only sets the per-side colors when the side has the same width — the explicit `border-left: 4px solid …` on the base rule wins for the left side. Planner should manually verify in browser that the left stripe stays accent on hover. (UI-SPEC §"Interaction States — Hub hero" makes this guarantee explicit.)

#### 7b. `.feedback-panel` amend — analog: itself (line 567)

Existing rule (line 567):
```css
.feedback-panel { padding: 1.5rem; border-radius: 8px; }
```

Replace with (D-14 — amend, do not duplicate):
```css
.feedback-panel {
  padding: 1.5rem;
  border-radius: 8px;
  background: var(--panel);
  border: 1px solid var(--line);
}
```

**Gotcha:** The existing rule is single-line; Phase 02 expansion to multi-line is a style improvement consistent with the rest of `site-common.css` (e.g., `.feedback-submit` at lines 579–588 is multi-line). Planner should match multi-line style for the amended rule.

#### 7c. `.admin-feedback-detail` — analog: `.admin-feedback` (line 596)

Existing related rule:
```css
.admin-feedback { max-width: 1100px; margin: 2rem auto; padding: 0 1rem; }
```

`.admin-feedback-detail` does NOT yet exist as a rule block (only as a markup class on Detail.cshtml line 6 with inline style supplying all properties). **New rule** — insert after line 615 (end of admin-feedback rule cluster, before line 617 Card Lookup section):
```css
.admin-feedback-detail {
  background: var(--panel);
  border: 1px solid var(--line);
  padding: 1.5rem;
  border-radius: 8px;
  max-width: 800px;
  margin: 2rem auto;
}
```

**Gotcha:** Values copied verbatim from the inline style on Detail.cshtml line 6 — no token substitutions beyond `var(--panel)` / `var(--line)` which were already token-driven inline. Visual rendering is identical pre/post migration.

#### 7d. `.admin-action-form` — new (D-12 explicit purpose-named class)

Insert near the admin-feedback cluster (after `.admin-feedback-detail`):
```css
.admin-action-form {
  display: inline;
}
```

That is the entire rule. D-12 forbids generalizing this to a shared `.inline-form` utility.

#### 7e. `.feedback-submit--busy` — analog: `.run-button:disabled` (lines 81–99) + `.busy-indicator__spinner` (`site.css:662-669`)

**Disabled-button visual analog** (`site-common.css:81-90`):
```css
.run-button:disabled,
.run-button[aria-disabled="true"],
.clear-cache-button:disabled,
.clear-cache-button[aria-disabled="true"] {
  opacity: 0.55;
  cursor: not-allowed;
  filter: saturate(0.35);
  box-shadow: none;
  pointer-events: none;
}
```

**Spinner ring analog** (`site.css:662-669` — DO NOT modify; reuse `busy-spin` keyframes which live in `site.css:1107-1115`):
```css
.busy-indicator__spinner {
  width: 1.25rem;
  height: 1.25rem;
  border: 3px solid var(--panel-soft-bg);
  border-top-color: var(--accent);
  border-radius: 999px;
  animation: busy-spin 0.9s linear infinite;
}
```

**Phase 02 new rules** (UI-SPEC §6, all in `site-common.css`):
```css
.feedback-submit--busy {
  opacity: 0.75;
  cursor: not-allowed;
  position: relative;
  padding-left: 2.25rem;
}

.feedback-submit--busy::before {
  content: "";
  position: absolute;
  left: 0.65rem;
  top: 50%;
  transform: translateY(-50%);
  width: 0.9rem;
  height: 0.9rem;
  border: 2px solid rgba(255, 255, 255, 0.35);
  border-top-color: var(--on-accent);
  border-radius: 999px;
  animation: busy-spin 0.75s linear infinite;
}
```

**Gotcha (CRITICAL):** `@keyframes busy-spin` is already declared in `site.css:1107-1115`. Adding a second `@keyframes busy-spin` to `site-common.css` would cause a duplicate-keyframe collision. The Phase 02 rule must REUSE the existing keyframe — do not redeclare. Reference UI-SPEC §6 confirms.

**Gotcha #2:** The opacity 0.75 + filter approach diverges from the `.run-button:disabled` opacity 0.55 + saturate(0.35) baseline. UI-SPEC §6 chose 0.75 (not 0.55) deliberately — the busy state is shorter-lived than a true disabled state, so visual contrast can be milder. Planner should use UI-SPEC values, not match `.run-button:disabled` exactly.

**Gotcha #3:** `padding-left: 2.25rem` on the busy class shifts the button text right when the class is added, making room for the spinner without DOM mutation. Without this padding, the spinner would overlap the "Sending…" text. UI-SPEC §6 captures this.

**Insertion point:** Append all `.feedback-*` busy rules adjacent to existing `.feedback-submit` rule cluster (around line 590, after `.feedback-submit:hover { filter: brightness(1.1); }`). Group by feature for grep-ability.

---

### 8. `DeckFlow.Web/wwwroot/ts/feedback.ts` (NEW) — UX-02

**Role:** TypeScript module (single function, attaches submit handler on the feedback form)
**Analog:** `wwwroot/ts/site.ts` `attachThemePicker` function (lines 128–) and `attachArchidektCacheJobUi` function (lines 537–) — both follow the same IIFE + DOMContentLoaded + readyState-aware pattern. `attachThemePicker` is the smaller, cleaner analog.

**Decision rationale (memory observations 3454, 3469):** `site.ts` is 730 lines. Per project convention (one feature per TS file in `wwwroot/ts/` — `card-lookup.ts`, `deck-sync.ts`, `category-suggestions.ts`, `judge-questions.ts`, `commander-search.ts`, `card-search.ts`, `df-select.ts`, `df-typeahead.ts` are all separate modules), **a new `feedback.ts` module is the correct choice over extending `site.ts`.**

**Analog: `site.ts:1-2, 128-149, 718-728` IIFE shell pattern**:
```typescript
((): void => {
  'use strict';

  // ... feature-scoped helpers ...

  let themePickerInitialized = false;

  const attachThemePicker = (): void => {
    if (themePickerInitialized) {
      return;
    }
    themePickerInitialized = true;

    const themeLink = document.getElementById('theme-stylesheet');
    const themeSelect = document.getElementById('theme-picker');
    if (!(themeLink instanceof HTMLLinkElement) || !(themeSelect instanceof HTMLSelectElement)) {
      return;
    }
    // … attach event listeners …
  };

  document.addEventListener('DOMContentLoaded', attachThemePicker);
  if (document.readyState !== 'loading') {
    attachThemePicker();
  }
})();
```

**`feedback.ts` skeleton** (planner pastes this; UI-SPEC §6 defines the contract precisely):
```typescript
((): void => {
  'use strict';

  let initialized = false;

  const attachFeedbackBusyState = (): void => {
    if (initialized) {
      return;
    }
    initialized = true;

    const form = document.querySelector<HTMLFormElement>('form.feedback-form');
    if (!form) {
      return; // No-op on every non-feedback page
    }

    form.addEventListener('submit', () => {
      const button = form.querySelector<HTMLButtonElement>('button.feedback-submit');
      if (!button) {
        return;
      }
      // D-08: do NOT preventDefault — let the browser POST normally.
      // D-11: disabled flag prevents double-submit.
      button.disabled = true;
      button.classList.add('feedback-submit--busy');
      // D-09: text swap.
      button.textContent = 'Sending…';
    });
  };

  document.addEventListener('DOMContentLoaded', attachFeedbackBusyState);
  if (document.readyState !== 'loading') {
    attachFeedbackBusyState();
  }
})();
```

**Gotchas:**
1. **No `import`/`export`**: project's `tsconfig.json` is `"module": "none"` — every TS file compiles as a script, not a module. Use IIFE; do not `export`. (Same as `site.ts`, `card-lookup.ts`, etc.)
2. **`'use strict';` first line inside IIFE** — every existing module has this. Match.
3. **`null`-checking the form**: when `feedback.ts` is loaded on a non-feedback page (which it currently isn't, since planner will scope the `<script src>` to `Feedback/Index.cshtml` only), the early return on `!form` is a safety net. Cheap, idiomatic.
4. **Unicode ellipsis in "Sending…"**: existing project convention is to use the literal `…` character or escape `…`. UI-SPEC §"Copywriting Contract" uses the literal `…`; either works in TS string literals. Planner picks; both compile to the same UTF-8 output.
5. **Event listener registration**: `submit` event fires on the form, not the button. Listening on the form (not the button) means the handler runs even if the user submits via Enter key in a text field — same UX guarantee.

**Wiring contract** (per project pattern from CardLookup.cshtml:118–120):

Add to `DeckFlow.Web/Views/Feedback/Index.cshtml` at the bottom (new section, after line 47):
```razor
@section Scripts {
    <script src="~/js/feedback.js" asp-append-version="true"></script>
}
```

**Gotcha:** Do NOT add `<script src="~/js/feedback.js">` to `_Layout.cshtml`. Per-page TS modules are loaded inline via `@section Scripts { … }` in the consuming view. `_Layout.cshtml:91` renders `@RenderSection("Scripts", required: false)` after the global trio (`site.js`, `df-select.js`, `df-typeahead.js`) — the per-page section runs after the globals, so any DOM manipulation in `feedback.ts` sees a fully-attached `site.ts`-driven page.

**Build pipeline:** `DeckFlow.Web/tsconfig.json` has `"include": ["wwwroot/ts/**/*.ts"]` (verify before plan), and the MSBuild `BeforeTargets="Build"` target picks up new `.ts` files automatically. No csproj changes required.

---

## Shared Patterns

### Theme-token discipline (applies to all CSS rules)

**Source:** `site.css :root` block (Phase 01 outcome — 20 tokens, no new ones permitted).
**Apply to:** every new CSS rule in `site-common.css`.

| Token | Use in Phase 02 |
|-------|-----------------|
| `--cta-border` | `.hub-hero` left stripe; `.hub-card--primary` border |
| `--accent-strong` | hover state border (with `--line` fallback) |
| `--panel` | `.hub-hero`, `.feedback-panel`, `.admin-feedback-detail` background |
| `--line` | default border for `.hub-hero`, `.feedback-panel`, `.admin-feedback-detail` |
| `--ink` | `.hub-hero__title` foreground |
| `--muted` | `.hub-hero__eyebrow`, `.hub-hero__description` foreground |
| `--on-accent` | `.feedback-submit--busy::before` spinner top color |
| `--fs-xs / --fs-sm / --fs-xl` | `.hub-hero__*` typography |

**No `:root` additions.** UI-SPEC verifier check #4 enforces.

### Inline-style → class migration verifier (applies to UI-LH-02)

**Source:** UI-SPEC §"Verification Checklist" item 2 + D-15.
**Apply to:** `Feedback/Index.cshtml`, `AdminFeedback/Index.cshtml`, `AdminFeedback/Detail.cshtml`.

```bash
cd /mnt/c/users/chrislunt/source/personal/decksyncworkbench/DeckFlow.Web/Views
grep -c 'style=' Feedback/Index.cshtml AdminFeedback/Index.cshtml AdminFeedback/Detail.cshtml
# All three files MUST report 0
```

### Razor partial 2-arg form (applies to UX-01)

**Source:** `Views/Deck/Home.cshtml:6` and 13 other call sites in the project.
**Pattern:** `@await Html.PartialAsync("_PartialName", modelArg)`.
**Apply to:** every `_MoxfieldBulkEditHint` call site (6 total).

### TS IIFE + DOMContentLoaded module shape

**Source:** every file in `wwwroot/ts/` (verified 9 files all share this shape).
**Apply to:** new `feedback.ts`.

Pattern boilerplate:
```typescript
((): void => {
  'use strict';
  // helpers + state
  const attachX = (): void => { /* … */ };
  document.addEventListener('DOMContentLoaded', attachX);
  if (document.readyState !== 'loading') {
    attachX();
  }
})();
```

### Verb-noun voice convention (applies to UX-03 + future)

**Source:** D-05 (CONTEXT.md) + UI-SPEC §"Copywriting Contract".
**Apply to (this phase scope):** Feedback page `<title>`, `<h1>`, submit-button label.
**Convention:** every CTA button + page title/h1 starts with a verb. Acceptable patterns: "Send Feedback", "Run Compare", "Look Up". Drop noun-only labels.

---

## No Analog Found

None. Every Phase 02 target file has a strong existing repo analog (often itself + an existing structural sibling). This is unsurprising — the phase is layout/copy polish on a mature codebase, not greenfield.

---

## Metadata

**Analog search scope:**
- `DeckFlow.Web/Views/**` (all .cshtml)
- `DeckFlow.Web/wwwroot/css/site.css`, `site-common.css`, `site-mobile.css`
- `DeckFlow.Web/wwwroot/ts/**/*.ts`
- `DeckFlow.Web/Views/Shared/_Layout.cshtml`

**Files scanned:** ~85 (all Views + 3 CSS files + 9 TS files + Layout)
**Pattern extraction date:** 2026-04-30
**Phase:** 02 — layout-hierarchy-ux-copy
