# UI Review — Primer Style Toggle + Full cEDH option

Scope: the "Primer style" radio fieldset added to `DeckFlow.Web/Views/Deck/DeckPrimer.cshtml`
(Standard / Moxfield-style rich / Full cEDH), its client behavior in
`wwwroot/ts/primer-selection.ts`, and the reused `manabase-*` component CSS in
`wwwroot/css/site-common.css`. Code-grounded audit (no Playwright-MCP in session;
live screenshots deferred). Quick task, not a planned phase.

**Overall: 22/24**

| Pillar | Score | Notes |
|--------|-------|-------|
| Copywriting | 3/4 | Clear labels + helper text; redundant double-help at bracket 5 |
| Visuals | 4/4 | Reuses proven segmented-pill component; focus states intact |
| Color | 4/4 | Pure theme-token driven; inherits all guild themes |
| Typography | 4/4 | `--fs-sm` tokens + weights match the component |
| Spacing | 4/4 | Inherits component rem scale + existing mobile block |
| Experience Design | 3/4 | Strong bracket-gated reveal + fallback; dead class + stacked help |

## Findings

### HIGH-VALUE FIX — dead `.is-selected` class contradicts component contract
The new pill labels render `class="manabase-pill ... is-selected"` conditionally, but
`site-common.css:2383-2388` explicitly documents that `.is-selected` is intentionally NOT
used for `.manabase-pill`: selection is driven purely by `.manabase-pill:has(> input:checked)`
plus the server-rendered `checked` attribute. There is no `.manabase-pill.is-selected` rule,
so the class is dead markup. Harmless to render today, but it misleads future edits (looks
like it drives selection) and duplicates intent.
- Fix: drop the `is-selected` conditional from all three pill labels; keep only the
  `checked="..."` on the radio (already present), matching the manabase page's own usage.

### MEDIUM — redundant help text stacks at bracket 5
At the cEDH bracket both `.manabase-help` paragraphs are visible: the generic rich-format
help and the Full cEDH help. They overlap in content (both mention TOC/callouts/visuals vs
"all sections + depth"). Two stacked muted paragraphs read as redundant.
- Fix: show the cEDH help in place of (not in addition to) the generic help when the cEDH
  option is in context, or merge into one paragraph.

### LOW — no transition on bracket-gated reveal (polish)
The Full cEDH pill appears/disappears via the `hidden` attribute on bracket change with no
visual transition. Acceptable; a subtle fade/slide would smooth it. Low priority.

## What's good (keep)
- **Component reuse over bespoke CSS.** The toggle reuses `manabase-segmented` / `manabase-pills`
  / `manabase-pill` / `manabase-help` — a proven, accessible segmented control. Zero new
  layout CSS, so it inherits every guild theme and the existing mobile block
  (`site-common.css:2724-2740`) for free. This is the correct pattern per the project's
  "layout in site-common, tokens per theme" constraint.
- **Color via tokens only.** Selected state uses `--accent` / `--on-accent`; borders `--line`;
  help `--muted`. No hardcoded colors in the markup → 24-theme + light/dark safe.
- **Accessibility.** `role="radiogroup"` + `<legend>` + real radio inputs + a visible
  `:focus-visible` outline on the hidden-radio pattern. Keyboard and SR friendly.
- **UX of the gating.** Bracket-5-only reveal is enforced in three consistent layers
  (server-side `hidden`, client show/hide + auto-fallback off FullCedh, and server
  effective-style normalization), so an invalid FullCedh can never be posted or rendered.

## Verification still owed
- Live screenshots at desktop + a 390px mobile viewport across ≥2 guild themes
  (project rule for UI changes). The new Playwright spec `e2e/primer-style-toggle.spec.ts`
  covers the toggle, bracket-5 reveal, mobile viewport, and a second theme, but has NOT been
  run live — execute it (headless WSL server) or eyeball before merge.
