# Manabase result page — UI Review

**Audited:** 2026-07-09
**Baseline:** Abstract 6-pillar standards (no UI-SPEC.md exists for this page)
**Screenshots:** Captured (CLI harness — pre-rendered full-page shots of a real Brago Azorius deck at desktop 1280w + mobile 390w, light "Classic" + dark "Dimir" themes; `ux-shots/verify-site-*.png`, `ux-shots/med-*.png`). The result panel only renders after a real Scryfall round-trip, so a plain URL screenshot of `/manabase` was not usable and these captured renders were audited instead.

**Audit note:** The tap-analyzer (`Untapped sources`) and opening-hand (`Opening hand`) lenses are flag-gated (`Model.ShowTapAnalyzer` / `Model.ShowMulliganEval`, Manabase.cshtml:304, 343) and did **not** render in the captured build, so those two lenses were scored from markup + CSS only, not visual evidence.

---

## Pillar Scores

| Pillar | Score | Key Finding |
|--------|-------|-------------|
| 1. Copywriting | 3/4 | Strong plain-language verdict + specific CTAs, but pervasive `card(s)`/`land(s)`/`source(s)` plural artifacts read as programmer output |
| 2. Visuals | 3/4 | Clear 90% focal point + good status hierarchy; left/right lens imbalance and long stack of near-identical bordered cards |
| 3. Color | 3/4 | Disciplined token + semantic-status system with documented WCAG health chips; `.manabase-chip--ok` amber+`--ink` needs dark-theme contrast verification |
| 4. Typography | 3/4 | Consistent uppercase eyebrow pattern + controlled weights, but ~8 distinct hardcoded rem font sizes bypass the `--fs` token scale |
| 5. Spacing | 3/4 | Rendered rhythm is clean; underlying scale is ad-hoc (0.05rem-granular magic numbers, negative-margin card-tie hacks), not tokenized |
| 6. Experience Design | 4/4 | Multi-step busy indicator, ARIA roles, non-color status pairing, responsive collapse, print region, progressive disclosure — genuinely thorough |

**Overall: 19/24**

---

## Top 3 Priority Fixes

1. **Kill the `(s)` plural artifacts in the verdict/summary/fix copy** (Manabase.cshtml:200-217) — WARNING. The verdict card is the trust-anchor of a beta tool, and it currently says "add ~4 land(s)", "3 demanding White card(s)", "add ~@primaryFix.Amount more White source(s)". This reads as un-finished programmer output on the single most-read block. Fix: reuse the exact pattern the page already uses one line down at Manabase.cshtml:227 (`report.DemandingCards.Count == 1 ? "card" : "cards"`) or a small `Pluralize(count, "land")` helper, applied to the summary-lands line and all three `primaryFix` switch arms.

2. **Consolidate the ad-hoc font-size + spacing values onto a token scale** (site-common.css:2662, 2707, 2715, 2723, 2729, 2745, 2790, 2799, 2826, 2834) — WARNING. This one component hardcodes ~8 discrete font sizes (0.72 / 0.75 / 0.78 / 0.8 / 0.85 / 1.05 / 1.6 / 2.4rem) and spacing on a 0.05rem-granular grid (0.35 / 0.45 / 0.55 / 0.85 / 1.15rem) plus negative-margin card-tie hacks (`.manabase-taplens`/`.manabase-mulliganlens` `margin: -0.25rem 0 1rem`, site-common.css:2927, 2949). Fix: add `--fs-eyebrow: 0.72rem` and `--fs-note: 0.8rem`, reuse `--fs-sm`, and snap paddings/margins to 0.25rem steps so rhythm can't drift per theme.

3. **Verify `.manabase-chip--ok` contrast on dark guild themes** (site-common.css:2604-2608) — WARNING. The "ok" chip is `color-mix(--warning 22%, transparent)` background with `color: var(--ink)`, and it carries the *majority* of castability rows (every 68–89% row in the screenshots). Unlike the health chips — which were deliberately given baked WCAG-AA hex pairs for exactly this reason (site-common.css:2610-2637) — the ok/low cast chips still ride theme surface tokens over a translucent fill. Fix: give the cast chips the same baked contrast treatment as the health chips, or run an AA check on the 3 darkest themes.

---

## Detailed Findings

### Pillar 1: Copywriting (3/4)
Strengths: CTAs are specific and task-named — "Analyze Mana Base", "Load deck & detect costs", "Download analysis (.txt)", "Print results", "Start over" (Manabase.cshtml:159-166, 682). No generic "Submit/OK/Save". The verdict copy is genuinely plain-language and actionable: "Lands: 33 vs ~36.4 recommended (−4 under the Karsten count, but ramp covers it)" and "Biggest fix: … 3 demanding White card(s) still cast late (worst: Grand Abolisher) — trim the top end or add early ramp" (visible in every desktop shot). Empty/loaded/error states are all written: "Deck loaded" hint (Manabase.cshtml:67-84), error banner with `role="alert"` (26-28), not-applied-overrides advisory (111-118), unresolved-card note (658-661), beta expectation-setter (422-424).

Findings justifying the dock:
- **`(s)` plural artifacts** throughout the verdict/summary/fix (Manabase.cshtml:203 "land(s)", 211 "source(s)", 211 "card(s)"... , 214 "land(s)", 217 "card(s)"). The page *knows* how to do this right — line 227 resolves singular/plural inline — so the artifacts are an inconsistency, not a constraint.
- **Punctuation drift:** the beta notice uses an ASCII hyphen "guide, not gospel - numbers may be inaccurate" (Manabase.cshtml:423) while the rest of the page uses em dashes. Minor.
- Mild redundancy: the summary card states the biggest fix (line 211-217) and then immediately restates demanding-card counts (line 226-229); both mention "demanding … Grand Abolisher".

### Pillar 2: Visuals (3/4)
Strengths: a clear single focal point — the `90%` simulated cast rate renders at 2.4rem in accent color (site-common.css:2706-2711), the largest element on the page, exactly where the eye should land. The verdict chip ("Solid", blue) is the second anchor. Status is reinforced redundantly: the weakest color is a red table row + red "Short by 0.4 ⚠" + red chip simultaneously (visible in `verify-site-desktop.png`). Icon-only markers are all labelled — commander ★ has `title` + `aria-label` (Manabase.cshtml:474, 562, 626), tap/keep markers carry `sr-only` text (329, 356).

Findings justifying the dock:
- **Two-lens imbalance:** the right lens is anchored by one big 2.4rem number; the left "Karsten source check" lens is all small text rows with no headline figure (Manabase.cshtml:256-283). Side by side (see `med-desktop.png`) the left card reads visually "empty/heavy-text" against the right's bold number — the two columns don't balance.
- **Card monotony / density:** the result panel stacks many full-width bordered cards of near-identical treatment — summary, two-lens, beta notice, mode/context line, verdict, ramp-draw, command-zone, then two wide tables, then formula disclosures. On mobile (`verify-site-mobile.png`) this is a very long, low-contrast scroll of similar rounded panels; sectioning between them is weak. The `.manabase-lens-big--soft` down-weighting (site-common.css:2722) is the right instinct but only fixes one of the stacked headlines.

### Pillar 3: Color (3/4)
Strengths: color is token-driven and semantic — `--accent-strong` for figures/links, `--muted` for support text, `--success`/`--warning`/`--danger` for status (site-common.css throughout). A roughly 60/30/10 split holds: neutral `--panel-soft-bg` fields (60), ink text (30), accent blue + status chips as the 10. The four health chips are a model example — deliberately baked to theme-independent WCAG-AA hex (`#166534`/`#1d4ed8`/`#f59e0b`/`#b91c1c`) with a documented rationale that per-theme tokens bled the text to near-white (site-common.css:2610-2637). Status is never color-alone: cast chips always carry a text label (`58% · low`, `90% · good`) and ✓/⚠ markers pair with `sr-only` text.

Findings justifying the dock:
- **`.manabase-chip--ok` dark-theme risk** (site-common.css:2604-2608): amber-22%-transparent fill + `color: var(--ink)` — the one chip the health system deliberately did *not* fix, yet it colors most castability rows. Needs an AA check on dark themes (see fix #3). It renders legibly in `verify-site-dimir-desktop.png` but that is not proof across all 24 forks.
- **Chip-text inconsistency:** `--chip--low` sets text to the danger color (2601) while `--chip--ok` sets text to `--ink` (2607) — sibling chips use two different text-color strategies. Justified by contrast, but a small system inconsistency.

### Pillar 4: Typography (3/4)
Strengths: the uppercase micro-eyebrow pattern is applied consistently across every section header — VERDICT, KARSTEN SOURCE CHECK, SIMULATED CAST RATE, lens labels — all at 0.72rem / `letter-spacing: 0.06em` / `--muted` (site-common.css:2660-2666, 2789-2795, 2849-2856). Font weight is controlled: 600 for emphasis, 700 for big figures/glyphs, normal for body — 2–3 weights, within standard.

Findings justifying the dock:
- **Font-size proliferation:** ~8 distinct hardcoded rem sizes in this component — 0.72 (2662), 0.75 (2834), 0.78em (2528), 0.8 (2715/2729/2826), 0.85 (2736 + `--fs-sm`), 1.05 (2799), 1.6 (2723), 2.4 (2707). Several are magic values that bypass the `--fs` token scale rather than deriving from it. The abstract standard flags >4 sizes; this clusters in a 0.72–0.85 "small" band that a 2-token scale (`--fs-eyebrow`, `--fs-note`) would cover.

### Pillar 5: Spacing (3/4)
Strengths: the *rendered* result is clean — cards breathe, table cells are padded (`0.35rem 0.6rem`, site-common.css:2323), the two-lens grid gap (0.85rem, 2649) and split grids (1rem 1.5rem, 2934/2956) are consistent, and no cramped or broken layout appears at any of the four captured viewport/theme combinations. Responsive gaps collapse cleanly at 640px (2913-2922).

Findings justifying the dock:
- **Ad-hoc, non-tokenized scale:** margins/paddings are hand-tuned on a 0.05rem grid — 0.35 / 0.45 / 0.55 / 0.85 / 1.15rem recur (e.g. 2312, 2323, 2650, 2657, 2728, 2775-2776, 2786, 2806) — with no spacing token or 4px/0.25rem rhythm. This is maintainability/rhythm-drift debt rather than a visible defect today.
- **Negative-margin card-tie hacks:** `.manabase-taplens` and `.manabase-mulliganlens` use `margin: -0.25rem 0 1rem` (2927, 2949) to pull each lens up under the card above it — a fragile way to express "these belong together" that will fight any future gap change.

### Pillar 6: Experience Design (4/4)
This is the strongest pillar and genuinely exceeds a typical bar:
- **Loading:** the analyze form drives a staged busy indicator — `data-busy-progress="Loading the deck|Resolving cards via Scryfall|Simulating castability|Scoring colors"`, `data-busy-hold-final-step`, `data-busy-min-ms` (Manabase.cshtml:30-35), with a separate progress set for the load step (160-163).
- **Error / status:** `role="alert"` error banner (26), `role="status"` loaded hint (67) and not-applied-overrides advisory (113), `role="note"` beta notice (422).
- **Accessibility:** `role="radiogroup"` segmented controls with `:focus-visible` outline on the visually-hidden radios (site-common.css:2499-2503), `scope="col"` on every `th`, `sr-only` companions to every ✓/⚠/marker (Manabase.cshtml:329, 356), `aria-hidden` on decorative markers, `title` on truncated card names (623).
- **Responsive:** two-lens + both split grids collapse to one column at 640px and the horizontal-scroll cue (`.manabase-scroll-hint`, "Scroll sideways for all columns →") is revealed only when tables can overflow (site-common.css:2913-2922) — verified in `med-mobile.png`.
- **Progressive disclosure:** deep detail is collapsed — "How the analysis works", "This deck's numbers", "Ramp", unsupported-interactions, swap prompt — so the verdict stays first-screen.
- **Print + expectation-setting:** `data-print-region` with dedicated print CSS, plus the beta notice framing the numbers as a guide.

Finding justifying (not docking) the score: the two flag-gated lenses (tap + opening-hand) could not be visually verified in the captured build, and the result panel is a long single scroll where a sticky/jump-to-verdict affordance would help on mobile — an enhancement, not a task-breaking defect. No BLOCKER-class experience gap was found.

---

## Finding Classification Summary

- **BLOCKERS:** none — no pillar scored 1, and no defect breaks task completion (the verdict, tables, and CTAs all function and render across both themes and both viewports).
- **WARNINGS:** (1) `(s)` plural artifacts in the verdict/summary copy; (2) ~8 hardcoded font sizes bypassing the `--fs` scale; (3) ad-hoc 0.05rem spacing scale + negative-margin hacks; (4) `.manabase-chip--ok` dark-theme contrast unverified; (5) two-lens visual imbalance; (6) stacked-card density/monotony on mobile.

---

## Files Audited
- `/mnt/c/users/chrislunt/source/personal/deckflow-manabase-ux/DeckFlow.Web/Views/Deck/Manabase.cshtml`
- `/mnt/c/users/chrislunt/source/personal/deckflow-manabase-ux/DeckFlow.Web/wwwroot/css/site-common.css` (result-panel + `manabase-*` blocks, ~L2302-2980, plus chip/health L2588-2643 and print L3610-3656)
- Screenshots: `ux-shots/verify-site-desktop.png`, `ux-shots/verify-site-dimir-desktop.png`, `ux-shots/verify-site-mobile.png`, `ux-shots/verify-site-dimir-mobile.png`, `ux-shots/med-desktop.png`, `ux-shots/med-mobile.png`
