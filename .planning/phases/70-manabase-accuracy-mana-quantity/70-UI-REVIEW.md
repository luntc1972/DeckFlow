# Phase 70 — UI Review (Two-Lens Manabase Result Header)

**Audited:** 2026-06-24
**Baseline:** 70-06-PLAN.md Design section (locked contract) + abstract 6-pillar standards
**Screenshots:** Not captured — no dev server detected (code-only audit). Live-render screenshot
explicitly deferred per 70-06-PLAN OPEN note; confirmed as a known limitation, not a scoring
failure in itself. Code-visible risks that screenshots would normally catch are flagged below.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Caption text matches plan; right-lens caption adds "London" and "Monte-Carlo" prose that isn't in the spec but improves clarity |
| 2. Visuals | 3/4 | Single-lens layout (cEDH mode, colorless deck) renders a half-width card — no CSS rule widens it to full width |
| 3. Color | 4/4 | All status colors use tokens (--success, --gold-warning, --muted); no per-theme hardcoded hex in the lens block |
| 4. Typography | 3/4 | Five distinct font sizes in the lens block; arbitrary rem values not on a stated type scale, but internally consistent with site-wide pattern |
| 5. Spacing | 3/4 | Arbitrary rem values throughout (0.85, 0.55, 0.3) — consistent with site-wide pattern, no spacing token system exists to violate |
| 6. Experience Design | 2/4 | ✓/⚠ status glyphs carry no aria-label; no Playwright spec asserts two-lens presence, mobile stacking, or per-theme overflow |

**Overall: 18/24**

---

## Top 3 Priority Fixes

1. **Single-lens half-width layout** (cEDH or colorless deck) — A cEDH user sees the right lens
   only; it occupies the left half of a 2-column grid and looks broken. Add
   `.manabase-twolens:has(> .manabase-lens:only-child) { grid-template-columns: 1fr; }` to
   `site-common.css` (lines 2537-2625). `WARNING`

2. **✓/⚠ glyphs missing accessible text** — Screen readers get bare Unicode characters with no
   context. The `<span class="manabase-lens-met">✓</span>` has no `aria-label` or
   `role="img"`, while sibling elements like `.manabase-cmd-glyph` already use `aria-label`.
   Add `aria-label="met"` / `aria-label="short by @km.Deficit"` on the two spans in
   `Manabase.cshtml` lines 181 and 185. `WARNING`

3. **No Playwright spec asserts the two-lens band** — The plan's Done-when list requires
   "Playwright: two-lens band visible on a result; left lens ✓/⚠ matches; mobile stacks; no
   horizontal overflow across themes." None of the existing specs (`manabase.spec.ts`,
   `manabase-castability.spec.ts`, `ui-responsive.spec.ts`, `theming.spec.ts`) assert
   `.manabase-twolens`, its child counts, or the mobile-stack layout. The band could be silently
   removed or mis-rendered with zero CI signal. `WARNING`

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)

**Findings:**

- Left lens label: `"Karsten source check"` — matches plan exactly (line 170).
- Left lens caption: `"The validated colored-source target (Frank Karsten's math) — are there enough
  sources of each color?"` — plan specifies `"validated colored-source target (Frank Karsten's
  math)"`. The implementation expands with a trailing clarifying question. Additive, not a
  deviation; the core phrase is present.
- Right lens label: `"Simulated cast rate"` — matches plan (line 196).
- Right lens big-number label: `"avg on-curve"` — matches plan exactly (line 197).
- Right lens caption: `"Joint mana + color, with London mulligan, tapped-land timing, and ramp —
  our Monte-Carlo models the real game, not just color odds."` — plan specifies `"joint mana +
  color, with mulligan, tapped-land timing, and ramp"`. The implementation adds "London" and
  "Monte-Carlo" and a trailing clause. Both additions are accurate and add signal; not a defect.
- Pill label: `"avg across @castRows.Count tracked spells"` — matches plan exactly, including
  the plan's (Codex MED) correction from "non-land spells" to "tracked spells" (line 199).
- Left-lens per-row format: `@f.Color | @f.ActualSources.ToString("F1") / @f.RequiredSources
  need | ✓ or ⚠ −@km.Deficit` — matches plan format `{Color}  {have} / {RequiredSources} need
  ✓|⚠ −N` with F1 formatting as required (line 177-185).
- Generic labels `"OK"` appear in existing table columns (lines 212, 276) but predate this
  phase and are not within the 70-06 scope.

**Score justification:** Spec copy is faithfully implemented with only non-contradicting expansions.
Minor point held for the expansions being unconfirmed against live render.

---

### Pillar 2: Visuals (3/4)

**Findings:**

**BLOCKER-CANDIDATE — Single-lens half-width layout.** The `.manabase-twolens` grid is always
`grid-template-columns: 1fr 1fr` (site-common.css line 2539). When only one lens renders (e.g.
cEDH mode hides the right lens; a colorless Commander deck hides the left lens), the single
`.manabase-lens` child still occupies one column track — 50% of the available width. This is a
layout defect visible to cEDH users on every analysis. The plan's "Colorless deck: hide the left
lens entirely" requirement is correctly gated in Razor (line 162), but the CSS does not compensate.
Fix: add a `:has(> .manabase-lens:only-child)` rule to make the grid `1fr` when only one child is
present. (`:has()` is broadly supported; alternatively, a server-side class toggle `manabase-
twolens--single` works without `:has()`.) `WARNING`

**Hierarchy — right lens big number.** The `2.4rem / 700` headline is visually strong and
provides the intended focal point. The supporting label `"avg on-curve"` renders at `0.85rem /
600` beside it (line 2597-2602), which is appropriate subsidiary sizing.

**Left-lens row layout.** `justify-content: space-between` pushes color name left and
sources/verdict right. With very long color names (e.g. hybrid color pairs rendered as full text)
this could cause the right-side content to be pushed off on narrow viewports before the 640px
breakpoint fires. Not confirmed without live render; flagged as a code-visible risk. `WARNING`

**Band placement.** The band correctly renders above the Lands/Health line (before line 205) as
the plan requires.

---

### Pillar 3: Color (4/4)

**Findings:**

All status colors in the two-lens block use CSS custom properties with appropriate fallbacks:

- `.manabase-lens-met`: `color: var(--success, #2f855a)` (line 2581) — fallback is a reasonable
  dark-green for themes that don't define `--success`.
- `.manabase-lens-short`: `color: var(--gold-warning, var(--warning, #c8a040))` (line 2586) —
  double-cascade through `--gold-warning` then `--warning`; the `#c8a040` fallback correctly
  avoids red (which would duplicate `--danger` semantics) for a "short but not catastrophic" state.
- `.manabase-lens-muted`: `color: var(--muted, #5a6472)` (lines 2577, 2601, 2607) — consistent
  with existing site-wide muted treatment.
- `.manabase-lens-big`: `color: var(--accent-strong, var(--accent, inherit))` (line 2594) — uses
  the theme's accent color for the headline number. This is the accent token, not a status color,
  and `inherit` is a safe terminal fallback.
- `.manabase-lens-pill`: `background: var(--info, var(--panel-soft-bg, transparent))` (line 2616)
  — appropriate use of informational surface token.
- `.manabase-lens`: `background: var(--panel-soft-bg, var(--surface, transparent))` (line 2545)
  — uses structural surface tokens correctly.

No hardcoded hex in any theme-critical color position in the lens block. The fallback values
(`#2f855a`, `#c8a040`, `#5a6472`) are only reached when a theme does not define the token and
serve as safe defaults.

The plan's requirement "tokens only — no per-theme hardcoded colors" is met. `PASS`

---

### Pillar 4: Typography (3/4)

**Findings:**

Five distinct font sizes appear in the two-lens block:
- `0.72rem` — lens label (ALLCAPS header) and pill (line 2553, 2612)
- `0.80rem` — lens note caption (line 2604)
- `0.85rem` — right-lens big number sub-label "avg on-curve" (line 2599)
- inherited body size — left-lens per-row content
- `2.4rem` — right-lens headline number (line 2591)

The type scale is internally coherent (three informational sizes + body + display) and follows
the DeckFlow site pattern (raw rem values — no Tailwind utility classes, no token-based type
scale). No explicit type scale is declared in site-common.css or UI-SPEC.md for this project;
the five sizes present are proportionate.

Font weights used: `600` (label, met, short, pill), `700` (big number), inherited for row content.
Two weights, which is appropriate.

`0.72rem` for the lens label cap-style header is very small (approx 11.5px at 16px root). No
contrast token override elevates its readable legibility — it relies on uppercase + letter-spacing
to compensate. Not a hard failure, but a contrast-risk on lighter themes that don't have a strong
`--muted` value. `WARNING (minor)`

---

### Pillar 5: Spacing (3/4)

**Findings:**

All spacing in the two-lens block uses raw rem values (0.15–2.4rem range):
- Grid gap: `0.85rem` (line 2540)
- Grid margin: `0.75rem 0 1rem` (line 2541)
- Lens card padding: `0.85rem 1rem` (line 2548)
- Label bottom margin: `0.55rem` (line 2552)
- Row padding: `0.3rem 0` (line 2564)
- Row gap: `0.5rem` (line 2563)
- Big-number sub-label margin-left: `0.4rem` (line 2598)
- Note top margin: `0.55rem` (line 2605, 2614)
- Pill padding: `0.15rem 0.6rem` (line 2613)

The site does not use a spacing token system (`--space-*` tokens are not defined in
site-common.css), so raw rem values are the established site pattern. The values are consistent
with the existing site component spacing (sampling: `0.35rem`, `0.75rem`, `0.85rem`, `1rem` used
widely elsewhere). No arbitrary pixel values or non-rem units appear in the block.

`0.85rem` appears for both the grid gap and the lens card padding top, creating a subtle
alignment consistency.

Score held at 3 (not 4) due to the absence of any declared spacing scale to positively verify
against and a handful of unique non-round values (`0.85`, `0.55`, `0.15`) that could drift from
future changes without a scale to anchor them. This is a systemic site issue, not a 70-06
introduction. `WARNING (minor)`

---

### Pillar 6: Experience Design (2/4)

**Findings:**

**BLOCKER-CANDIDATE — ✓/⚠ glyphs have no accessible text.** The `.manabase-lens-met` span
(Manabase.cshtml line 181) renders a bare `✓` Unicode character with no `aria-label`, `role`,
or `title`. The `.manabase-lens-short` span (line 185) renders `⚠ −@km.Deficit` with no
aria annotation. Screen readers may announce these as "check mark" / "warning sign" without any
color status context. Contrast: the sibling `.manabase-cmd-glyph` elements (lines 264, 342) have
both `title` and `aria-label` attributes. The inconsistency is a regression in the accessibility
pattern established by the same view. Fix: add `aria-label="met"` / `aria-label="short by
@km.Deficit source(s)"` or `role="img"` equivalents to the two spans. `WARNING`

**Missing Playwright coverage for the two-lens band.** The plan's Done-when list explicitly
requires Playwright tests asserting: two-lens band visible on a result, left lens ✓/⚠ correctness,
mobile stacking, no overflow across themes. None of the following specs cover `.manabase-twolens`:
- `manabase.spec.ts` — no lens assertions
- `manabase-castability.spec.ts` — general overflow check, no lens assertions (lines 56-59)
- `ui-responsive.spec.ts` — no manabase lens coverage
- `theming.spec.ts` — no manabase lens coverage

The `AvgOnCurve` and `KarstenMet` logic is well unit-tested in `ManabaseDisplayTests.cs`
(lines 117-157). But the rendered band has zero Playwright coverage, meaning:
- A typo removing `.manabase-twolens` from the view would not be caught
- The mobile-stack breakpoint is not verified in CI
- The per-theme overflow contract is not verified for the new elements
This is an explicit DoD gap (the CLAUDE.md rule: "adding/modifying a web page MUST include
xUnit + Playwright tests"). `WARNING`

**State coverage — empty states correctly gated.** `showRightLens` (line 161) uses the exact
gate from the plan (`Model.ShowCastability && castRows.Count > 0`). `showLeftLens` (line 162)
uses `report.ColorFindings.Count > 0`. Both gates match the contract. `PASS`

**Divide-by-zero protection.** `ManabaseDisplay.AvgOnCurve` returns 0 on empty input (line
109-111), and the right lens is hidden when `castRows.Count == 0`, so the 0-return is never
displayed. `PASS`

**Deficit clamp.** `KarstenMet` clamps deficit to `Math.Max(1, ...)` (line 133), preventing
`−0`. Verified by unit tests `[InlineData(16.6, 17, false, 1)]`. `PASS`

**Display/decision parity (Codex HIGH).** `ActualSources.ToString("F1")` is used in the display
(line 177) and `KarstenMet` compares the raw double `ActualSources >= RequiredSources` (line 132)
for the marker. Both use the same field; they cannot contradict. `PASS`

**Right-lens note adds "London" and "Monte-Carlo" clarifiers** not in the plan spec but not
incorrect — informational additions, not misleading copy. `PASS`

---

## Registry Safety

shadcn not initialized (`components.json` absent). Registry audit skipped.

---

## Files Audited

- `/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/70-manabase-accuracy-mana-quantity/70-06-PLAN.md`
- `/mnt/c/users/chrislunt/source/personal/deckflow/.planning/phases/70-manabase-accuracy-mana-quantity/70-CONTEXT.md`
- `/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/Views/Deck/Manabase.cshtml`
- `/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/wwwroot/css/site-common.css` (lines 2537-2636, plus broader context for patterns)
- `/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/Models/ManabaseDisplay.cs`
- `/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web.Tests/Manabase/ManabaseDisplayTests.cs`
- `/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/e2e/manabase.spec.ts`
- `/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/e2e/manabase-castability.spec.ts`
- `/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/e2e/ui-responsive.spec.ts`
- `/mnt/c/users/chrislunt/source/personal/deckflow/DeckFlow.Web/e2e/theming.spec.ts`
