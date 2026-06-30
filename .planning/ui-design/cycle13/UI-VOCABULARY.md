# DeckFlow UI Vocabulary — Cycle 13 design foundation

Reuse these existing patterns faithfully. Do NOT invent new visual language. Layout/cross-cutting CSS lives in `DeckFlow.Web/wwwroot/css/site-common.css` (loaded first, all 22 themes inherit). Theme color/spacing lives in per-theme forks (`site.css` = Jeskai default, `site-azorius.css`, `site-nyx.css`, …). Never hardcode colors — use tokens.

## Theme tokens (defined in every theme `:root`)
Palette: `--bg`, `--panel`, `--panel-soft-bg`, `--ink`, `--muted`, `--line`, `--accent`, `--accent-strong`.
Semantic: `--warning`, `--error`, `--info`, `--success` (global #2f855a), `--danger` (#c53030), `--link`, `--cta-border`, `--focus`, `--on-accent` (#fff), `--gold-warning` (#c8a040).
Type scale: `--fs-xs` .85rem, `--fs-sm` .85rem, `--fs-base` .95rem, `--fs-lg` 1.05rem, `--fs-xl` 1.5rem, `--fs-2xl` 1.9rem.

## Page skeleton (every tool page)
`.hero` (h1 + `.page-lede` + optional `details.hero-detail` "How it works") → `_DeckToolTabs` (`.tool-nav`) → `.error-banner`/`.warning-banner`/`.info-banner` as needed → `form.deck-form` or `section.result-panel`.
Panels: `.result-panel` / `.deck-form` = border 1px `--line`, radius 10px, bg `--panel`, padding 1rem, subtle shadow.

## Reusable components
- **Chips/health:** `.manabase-chip` + `--low/--ok/--good`; `.manabase-health--{excellent,solid,workable,needswork}`.
- **Segmented radio pills:** `fieldset.manabase-segmented > legend + .manabase-pills > label.manabase-pill > input[radio] + span`. Checked state auto-styles via `:has(> input:checked)`.
- **Two-lens metric cards:** `.manabase-twolens` (grid 1fr 1fr) > `.manabase-lens` (soft card) containing `.manabase-lens-label` (uppercase eyebrow), `.manabase-lens-row` (flex space-between stat rows), `.manabase-lens-big` (2.4rem headline + small unit span), `.manabase-lens-pill` (info pill), `.manabase-lens-met`/`--short` (success/warning value colors).
- **Verdict block:** `.manabase-verdict` + `--issues`/`--fine` (left border gold/green), `.manabase-verdict-heading` (uppercase eyebrow), `.manabase-verdict-list`.
- **Callout (required/notice):** `.bracket-callout` (left border 4px `--accent-strong`, soft bg) + `.bracket-callout__label` (uppercase eyebrow).
- **Cross-tool notice (dismissible):** `.deck-restored-notice` (flex, left border 3px `--accent`) + `.deck-restored-notice__clear` (pill button). Reuse for "deck changed" stale banner.
- **Collapsible:** `details.deck-analysis-overrides`/`.manabase-overrides` (`> summary` cursor pointer, bold); `.info-tooltip` (i-circle); `.chatgpt-helper-panel` (banner with ▸/▾).
- **Table:** `.manabase-table-wrap` (overflow-x auto) > `table.manabase-table`; row mods `--short` (danger), `--weakest` (left border), `--commander` (soft bg bold).
- **Buttons:** `.run-button` (primary, has `:disabled`), `.copy-button`, `.clear-cache-button` ("Start over"), `.feedback-submit--busy` (spinner state). Footer CTA pill `.page-footer__link--cta`.
- **Workflow steps:** `_WorkflowStepTabs` → `.chatgpt-step-nav` (ARIA tablist) > `.chatgpt-step-tab`; panels `section.result-panel.chatgpt-step-panel` with `.chatgpt-step-heading` (`.chatgpt-step-eyebrow` "Step N" + h2 + `.chatgpt-step-badge`).
- **A11y:** universal `:focus-visible` outline `--focus`; `.sr-only`; `role=alert/status/note` on banners.

## Per-surface slot guidance

### /manabase  (Views/Deck/Manabase.cshtml, ManabaseViewModel, ManabaseDisplay.cs)
Result area uses `.manabase-twolens` (Karsten lens | Simulated cast-rate lens), then `.manabase-context`, health chip, verdict, ramp/draw, command-zone castability, then `.manabase-table`.
**Tap Analyzer slot:** add a THIRD `.manabase-lens` card (untapped-source frequency overall + per-color via `.manabase-lens-row`; turn-1 untapped availability as a `.manabase-lens-big` headline + `.manabase-lens-pill`). Place it directly under the two-lens grid (its own `.manabase-lens` full-width, or extend the grid). Must not contradict cast-rate numbers (single source of truth). Same metrics also append to the manabase paste-artifact text.

### /deck-analysis  (Views/Deck/DeckAnalysis.cshtml, DeckAnalysisViewModel)
5-step workflow. Score belongs in **Step 3 (results)**, above the per-category breakdown.
**Score slot:** a 4-card grid `.chatgpt-score-grid` (grid repeat(4,1fr), gap 1rem) of `.chatgpt-score-card` (soft card, centered) each with `.chatgpt-score-label` (uppercase: Power/Speed/Control/Consistency), `.chatgpt-score-value` (0-5 band, `--accent-strong`), and a tiny rationale line (`--muted`, `--fs-xs`). Coarse labeled bands, NO decimals. Also folds into all 3 prompt variants.

### /deck-primer  (Views/Deck/DeckPrimer.cshtml, DeckPrimerViewModel)
Step 2 has `.bracket-callout` (target bracket select) + primer-style pills + section checkboxes.
**Stale banner slot:** reuse `.deck-restored-notice` shape at top of Step 2 (or Step 3 results) — "Deck changed since this primer was generated. [Regenerate]" with `.deck-restored-notice__clear`-style action. Stale-FLAG only; never auto-rebuild. Name the changed-card count.

### NET-NEW /bracket tool  (Phase 76)
Plug into the tool system. Checklist:
1. `Services/Tools/ToolRegistry.cs` — add `Create(key:"bracket", label:"Bracket", route:"/bracket", section:ToolNavSection.Analyze, flagKey:"tool.bracket.enabled", core:false, tileTitle, tileDescription, helpSlug:"bracket", tab:DeckPageTab.Bracket, isPrimaryTile:false)`.
2. `Models/DeckPageTab.cs` — add `Bracket`.
3. `BracketController.cs` + `Views/Deck/Bracket.cshtml` (@model BracketViewModel); include `_DeckToolTabs`.
4. `_ToolTileIcon.cshtml` — add `bracket` SVG case.
5. Optional help: `Views/Help/Bracket.*` + helpSlug.
6. Tile auto-renders on Home once registered; flag seeded OFF (page byte-identical when off).
Page shape: `.hero` → deck input (`.sync-column__body` URL/paste like other tools) → target-bracket `.manabase-pills` (B1-B5) → result: classified tier as a big `.manabase-chip`/health-style badge + reasons (`.manabase-verdict-list`: Game Changers, combos, mass-land-denial/extra-turns — NO tutors), floor-violations list, starter cuts, copy-prompt button (`.copy-button`). 3 decoupled prompt variants.

## Mockup rules (for the HTML deliverables)
- Save to `.planning/ui-design/cycle13/`. Reference the REAL css with relative paths from that dir: `../../../DeckFlow.Web/wwwroot/css/site-common.css` then the theme file.
- Include a small fixed theme switcher (top-right) that swaps the theme `<link>` href between `site.css` (default/Jeskai), `site-azorius.css`, `site-nyx.css`; keep `site-common.css` always loaded first, then theme, then optionally `site-theme-overrides.css`, `site-mobile.css`.
- Wrap content in `.page-shell > .page-frame > main.content-shell` so inherited layout applies. Use ONLY existing classes + the 2-3 net-new classes the spec introduces (define those new classes inline in a `<style>` block, tokenized with `var(--…)`, so they preview).
- Populate with realistic Commander data (e.g. a Najeela cEDH list, Game Changers like Thassa's Oracle / Demonic Tutor / Mana Crypt).
