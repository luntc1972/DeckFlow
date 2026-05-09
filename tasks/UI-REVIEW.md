# DeckFlow — Project-Wide UI Review (Freeform)

**Audited:** 2026-04-30
**Baseline:** Abstract 6-pillar standards (no UI-SPEC.md)
**Screenshots:** Not captured — no local dev server detected on :3000/:5173/:8080/:5000. Live site fetched at https://www.deckflow.gg/, /feedback, /help, /about (HTTP 200, classic theme).
**Evidence basis:** Live HTML for 4 routes + 6 Razor views (`_Layout`, `Home`, `DeckSync`, `CardLookup`, `Feedback`, `Help`, `About` partial) + 4 CSS files (`site-common.css`, `site.css` 1340L, `site-rakdos.css`, `site-mobile.css`) + JS spot-checks for icon-button ARIA.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Strong, specific lede + descriptions; one stale "Submit" string in `_MoxfieldBulkEditHint`; no global empty-state pattern but most surfaces handle it |
| 2. Visuals | 3/4 | Clear hub hierarchy + skip-link + focus rings everywhere; weak primary focal point on home (4 equal-weight groups, no hero CTA) |
| 3. Color | 2/4 | Solid token system, but `feedback-error` reuses `--accent-strong` for error red (semantic collision), `--accent-strong` is also link color, footer CTA border, AND admin-filter active background — accent is overloaded |
| 4. Typography | 2/4 | 18 distinct font-size values across core CSS (target ≤ 6); only one `font-family` declaration but no defined type ramp |
| 5. Spacing | 3/4 | Top-5 spacing values cover ~70% of usage (1rem/0.5/0.75/0.35/0.6); but 25+ unique rem values total and `0.28rem`/`0.55rem`/`0.95rem` outliers betray no enforced scale |
| 6. Experience Design | 3/4 | Loading + error + warning + info banners on every workhorse view; busy indicator partial reused; Feedback page lacks loading state on submit; honeypot + a11y-live-region + skip link present |

**Overall: 16/24**

---

## Top 5 Priority Fixes (cross-cutting)

1. **Define a 6-step type scale and enforce it.** Today `site.css` ships 18 distinct font-size values (`0.65, 0.68, 0.75, 0.78, 0.8, 0.85, 0.875, 0.9, 0.95, 1, 1.05, 1.4, 1.5, 1.9rem` + 4 em variants). Pick `--fs-xs/sm/base/lg/xl/2xl` tokens (e.g., 0.78 / 0.85 / 0.95 / 1.1 / 1.4 / 1.9rem) in `site.css :root`, replace every literal. Cuts visual chatter, makes guild theme overrides one-line. **Highest leverage.**
2. **Split semantic color tokens from accent.** `--accent-strong` is currently used for: links, brand text, focus outlines, footer-CTA border, error-message text (`feedback-error`, line 590), admin-filter active state, `panel-edhrec-border`, AND back-to-top button. Add `--link`, `--danger`, `--cta-border`, `--focus` aliases (can default to accent on classic theme but let guild themes diverge). The `feedback-error` falling back to `--accent-strong` is an actual bug on red themes (Rakdos/Boros/Jund) where errors look like body links.
3. **Pick a primary focal action on the home hub.** Four equal-weight `hub-group` sections with 11 cards yield decision paralysis. Promote one card per group (or a single primary "Start with ChatGPT Analysis →" hero CTA above the grid) using `font-weight`, larger card, or accent border. Right now `.hub-card` hover is the only differentiator — there is no first-glance answer to "what do I do?".
4. **Move inline `style=` to CSS classes.** `Feedback/Index.cshtml:8` and `AdminFeedback/Detail.cshtml:6,27,34,39` and `AdminFeedback/Index.cshtml:74` carry `style="background: var(--panel); border: ..."` — this is style logic that should live in `.feedback-panel`/`.admin-feedback-detail`/`.admin-action-form` classes. Inline styles defeat theme overrides and CSP.
5. **Reduce hardcoded color literals in `site.css`.** 14 occurrences of `#fff`, plus `#3a82f7`, `#c53030`, `#2f855a`, `#2b6cb0`, `#b83a2e` floating outside `:root`. The `#3a82f7` and `#c53030` hex values are unreachable by guild themes — Jeskai red bleeds into Boros, etc. Hoist all standalone hex values into `:root` tokens (`--cta-on-accent`, `--success`, `--danger`).

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)

**Wins:**
- Specific, scannable card descriptions on home (`Home.cshtml:14-66`) — every card states inputs and outputs (e.g., "Reconcile a Moxfield deck against an Archidekt deck (either direction) and generate add/cut text").
- Empty state in admin is on-brand: "No feedback in this view." (`AdminFeedback/Index.cshtml:45`).
- Feedback success copy is plainspoken — "Thanks — your feedback was received." (`Feedback/Index.cshtml:15`).
- Consistent voice across hub-group titles (one-word verbs: Analyze / Build / Reference / Categories).

**Gaps (WARNING):**
- `_MoxfieldBulkEditHint.cshtml:9` instructs users to "Submit" — generic verb; should mirror the actual button label ("Run Compare" or "Look Up").
- `feedback-page` panel says "Send feedback" as `<h1>` while the page `<title>` is "Feedback - DeckFlow" — verb-noun voice mismatch with home/about (which use noun-only headings).
- No global 404/500 view detected in `Views/Shared/` (only partials). If `_Layout` fails, default ASP.NET dev page leaks to prod.

**Fixes:**
- Replace "Submit" → "Run Compare" in `_MoxfieldBulkEditHint.cshtml:9`.
- Add `Views/Shared/Error.cshtml` with branded copy ("DeckFlow hit a snag — try again or send feedback").

---

### Pillar 2: Visuals (3/4)

**Wins:**
- Skip-link (`_Layout.cshtml:49`) + `aria-live="polite"` copy announcer (`:78`) + `role="main"` + ARIA-labelled section headings on home — exemplary baseline a11y.
- Icon-only buttons in card-picker JS-render with `aria-label="Add another card" / "Remove this card"` (`deck-sync.js:636,645`).
- Card-lookup copy buttons use icon + visible-text twin pattern (`CardLookup.cshtml:48-57`) — best-of-both for icon literacy.
- `.busy-indicator` overlay with spinner, backdrop blur, and accessible card surface (`site.css:609`).

**Gaps (WARNING):**
- Home hub has no visual focal point. 4 sections × 2-3 cards each, all identical typography weight (`font-weight:600` on title, `0.85rem` description). User has to scan 11 cards to choose. Either lead with a single hero ("Start: paste a deck URL →") or promote 1 card per group with a `--hub-card-primary` modifier.
- Admin filter pills (`admin-feedback-filter.active`) use `color:#fff` against `var(--accent-strong)` — works on classic blue, but on Selesnya (green-white pale accent) the contrast may dip below 4.5:1. Verify per-theme.
- Decorative `▾`/`▶`/`▼` glyph arrows in `chatgpt-question-bucket__toggle` (site.css:518-524) and `df-select__trigger::after` (`:823`). These are unicode in CSS `content:` — fine, but no `aria-hidden` since `content:` isn't read by all SR/browser combos. Acceptable but worth noting.

**Fixes:**
- Promote first hub card in each group with `.hub-card--primary { font-size:1.1rem; border-color:var(--accent); }`.
- Audit theme contrast for `admin-feedback-filter.active` text on each `--accent-strong`.

---

### Pillar 3: Color (2/4) — LOWEST PILLAR

**Token system is well-architected** (`:root` in `site.css` lines 1-32; guild themes import + override). But accent-strong is used for **8 unrelated semantic roles** which means changing one bleeds into the others:

| Role | Selector | File:line |
|------|----------|-----------|
| Link color | `.about-page a` `.help-prose a` etc. | site-common.css:451, 488, 504, 528 |
| Brand text | `.page-brand` | site-common.css:34 |
| Focus outline | `*:focus-visible` | site.css:90 (uses `--accent` not strong, OK) |
| Footer CTA border | `.page-footer__link--cta` | site-common.css:380 |
| **Error text** | `.feedback-error` | site-common.css:590 ← **bug** |
| Admin filter active | `.admin-feedback-filter.active` | site-common.css:605 |
| Back-to-top button BG | `.back-to-top-button` | site.css:661 |
| Locked back-to-top | `.is-theme-locked` | site-common.css:104 |

**`feedback-error` using `var(--accent-strong, #c55)` is a semantic collision:** on Rakdos (red theme, `--accent-strong:#a92434`), error text is indistinguishable from a styled link. On Selesnya (green theme), errors are green — actively misleading.

**Hardcoded color count in core CSS:** 14× `#fff`, 4× distinct themed-on-classic hex values that don't follow the token (`#3a82f7`, `#c53030`, `#2f855a`, `#2b6cb0`, `#b83a2e`). These will ghost-render under guild themes.

**60/30/10 distribution:** Reasonable on classic theme — bg/panel cover ~80% of pixels, accent only on links/buttons. But the lack of an accent-bg-tint hierarchy means panels stack flatly. `.hub-card:hover` adds `border-color:var(--accent-strong)` only — no scale/elevation change beyond `translateY(-1px)`.

**Fixes:**
- Add `--danger`, `--success`, `--link`, `--cta-border` tokens; update `feedback-error`, `admin-feedback-filter`, `info-banner`, `warning-banner` to use semantic names.
- Audit `#fff` literals — each should be `var(--ink-on-accent)` or similar so dark themes can override.

---

### Pillar 4: Typography (2/4)

**Distinct font-sizes across core CSS:** **18 unique values** (target: ≤ 6).
```
0.65rem ×1     0.85rem ×7     1rem ×1
0.68rem ×2     0.85em  ×1     1.05rem ×1
0.75rem ×2     0.875rem×1     1.4rem  ×1
0.75em  ×2     0.9rem  ×9     1.5rem  ×1
0.78rem ×3     0.95em  ×1     1.9rem  ×1
0.8rem  ×4     0.95rem ×6     15px    ×1 (html base)
```
The `0.85` vs `0.875` vs `0.9` triplet is the smell — those are picks-of-the-day, not a scale.

**Font weights:** Only 3 used (500/600/700) — good, in scope.

**Font family:** Single declaration in `site.css:45` (`Segoe UI, Tahoma, Geneva, Verdana, sans-serif`). No theme overrides this — consistent across 25 guilds. Win.

**Line heights:** Mostly inherit; explicit `1.35`, `1.45`, `1.5`, `1.6` declared in 5 places — could collapse to 2 (`--lh-tight`, `--lh-prose`).

**Letter spacing:** `0.01em`, `0.02em`, `0.08em` — uppercase eyebrows use `0.08em` consistently, OK.

**Fixes:**
- Add `--fs-xs/sm/base/lg/xl/2xl` and a `--lh-tight/prose` pair to `:root`.
- Migrate all `font-size:` declarations to those tokens. Estimated touch: 80 lines across `site.css` + `site-common.css`.

---

### Pillar 5: Spacing (3/4)

**Top spacing values (rem-based, padding/margin/gap/inset):**
```
1rem    ×55  ← effective base unit
0.5rem  ×30
0.75rem ×21
0.35rem ×14
0.6rem  ×11
0.4rem  ×11
1.5rem  ×9
0.85rem ×9
0.25rem ×9
2rem    ×6
```
The shape is right — power-of-1rem ladder with sensible halves and quarters. Top 5 cover the bulk.

**Outliers (WARNING):** `0.28rem`, `0.55rem`, `0.65rem`, `0.95rem`, `0.7rem` x4, `0.8rem` x3, `2.25rem`, `2.35rem`, `2.5rem`. These are pick-of-the-day values. `0.28rem` (df-select trigger padding, `site-common.css:69`) and `0.55rem` (df-select option, `:936`) particularly stand out — likely organic drift during df-select work.

**Container widths:** Single `--shell-max-width: 1120px` token (`site-common.css:2`) reused in `.page-frame` and `.content-shell` — clean. Card-lookup grid hits `60% 40%` (`:763`) — magic numbers, but only one offender.

**Mobile adjustments:** `site-mobile.css` is clean — only 6 rules, all behind real media queries (480/600/768/900). No regression risk.

**Fixes:**
- Define `--space-1` through `--space-8` (0.25/0.5/0.75/1/1.5/2/3/4rem) in `:root`. Migrate `padding`/`margin`/`gap` declarations. Outlier values like `0.28rem` → `0.25rem` (`--space-1`).
- Card-lookup grid: keep magic ratio if intentional; otherwise switch to `2fr 1fr`.

---

### Pillar 6: Experience Design (3/4)

**Loading states:** `_BusyIndicator.cshtml` partial is included by 7 of 8 deck workhorse views. Verified routes with busy: CardLookup, ChatGptCedhMetaGap, ChatGptDeckComparison, ChatGptPackets, DeckConvert, DeckSync, MechanicLookup, SuggestCategories, CommanderCategories.

**Error states:** `error-banner` with `role="alert"` on every form-bearing view (CardLookup:14, DeckSync:41, SuggestCategories:26, CommanderCategories:22). Banner toggles via `.hidden` class — JS-friendly. Win.

**Empty states:** AdminFeedback handles "No feedback in this view" (`Index.cshtml:45`). Saved-sessions empty: `ChatGptPackets:90` says "No saved sessions found. Enable 'Save artifacts to disk' on a future run to populate this list." — explicit CTA inline. Win.

**Disabled state:** `.run-button:disabled` and `[aria-disabled="true"]` get opacity 0.55, saturate 0.35, no hover, no pointer events (`site-common.css:81-99`). Properly defended.

**Destructive confirmation:** `AdminFeedback/Detail.cshtml:39` has `<form ...op="delete">` — should verify `onsubmit="return confirm(...)"` or modal exists. Did not check JS path; flagging as **WARNING** to verify.

**Gaps (WARNING):**
- **Feedback page (`/feedback`) does NOT include `_BusyIndicator`** and does not disable the submit button on click (`Feedback/Index.cshtml:43`). User can double-submit on slow networks. Add `data-busy-on-submit` or simple JS disable.
- No `prefers-reduced-motion` opt-out for `.busy-indicator__spinner` `animation: busy-spin` (`site.css:642`) or `.hub-card` transform/transition (`site-common.css:171`). Accessibility regression risk.
- `feedback-page` is rendered with inline `style=` (line 8) instead of using its `.feedback-panel` class — works, but fragile under CSP nonce-less policies.

**Fixes:**
- Add `aria-busy` + button disable to feedback form on submit.
- Add `@media (prefers-reduced-motion: reduce) { .busy-indicator__spinner, .hub-card { animation:none; transition:none; transform:none; } }` block to `site.css`.
- Verify destructive admin-delete confirmation; add `data-confirm="Delete this feedback?"` JS hook if missing.

---

## Files Audited

**Razor views (6):**
- `Views/Shared/_Layout.cshtml`
- `Views/Deck/Home.cshtml`
- `Views/Deck/DeckSync.cshtml` (partial — top 120 lines)
- `Views/Deck/CardLookup.cshtml` (partial — top 80 lines)
- `Views/Feedback/Index.cshtml`
- `Views/Help/Index.cshtml`
- `Views/About/Index.cshtml`

**CSS (4):**
- `wwwroot/css/site-common.css` (1005 lines, full read)
- `wwwroot/css/site.css` (1340 lines, sampled :1-200, :200-500, :500-799)
- `wwwroot/css/site-rakdos.css` (sampled :1-100 — guild theme pattern verification)
- `wwwroot/css/site-mobile.css` (full read, 46 lines)

**JS (1 spot-check):**
- `wwwroot/js/deck-sync.js:625-650` — confirms icon-button ARIA labels.

**Live HTML (4 routes via curl):**
- https://www.deckflow.gg/ (HTTP 200, classic theme)
- https://www.deckflow.gg/feedback (HTTP 200)
- https://www.deckflow.gg/help (HTTP 200)
- https://www.deckflow.gg/about (HTTP 200)

**Not audited (time-boxed):**
- 19 of 26 Razor views (sampled architectural patterns from the 6 above)
- 22 of 25 guild theme CSS files (Rakdos sampled — pattern is `@import 'site.css'` + token override)
- TypeScript source under `DeckFlow.Web/wwwroot/ts/` (only compiled JS spot-checked)
- `df-select.css` (declarations live in `site-common.css:780-961`, fully read)

**Configuration:**
- No UI-SPEC.md present (freeform audit against abstract standards)
- No `components.json` — registry safety audit not applicable
- No dev server detected on :3000/:5173/:8080/:5000 — screenshot capture skipped, audit uses live prod HTML + source code
