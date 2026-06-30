# UI-SPEC — Phase 77: Multi-Axis Deck Score

**Folds into:** `/deck-analysis` (Views/Deck/DeckAnalysis.cshtml), Step 3 results.
**No new tool tile** (SCORE-04). The score block is additive markup inside the existing Step-3 results panel and additive text inside the three prompt artifacts.

---

## 1. Surface overview — where it sits

The score lives in **Step 3 (Analysis Results)**, inside the existing
`<section class="result-panel nested-panel summary-panel" data-chatgpt-result-anchor>`
block that only renders when `Model.AnalysisResponse is not null`
(DeckAnalysis.cshtml ~line 519).

Placement order inside that summary panel:

1. Download-session toolbar (existing)
2. `<h3>Analysis Summary</h3>` (existing)
3. **NEW — Multi-Axis Score block** (`.chatgpt-score`)
   - eyebrow heading + 4-card grid `.chatgpt-score-grid`
   - bracket cross-check note `.chatgpt-score-crosscheck`
4. Overview card (existing `<h4>Overview</h4>`)
5. Strengths / Weaknesses … the rest of the existing per-category breakdown (unchanged)

The score sits **above** the per-category Overview/Strengths/Weaknesses breakdown so the
reader gets the at-a-glance verdict first, then drills into prose. This matches the
UI-VOCABULARY `/deck-analysis` slot guidance ("Step 3 results, above the per-category breakdown").

It renders from new score fields on the deck-analysis view model (populated by the
Phase-77 scoring service from the same `deck_profile` signals already parsed). No new
HTTP call, no new round-trip — the score is computed locally from existing signals
(SCORE-02) and from the bracket classification produced alongside it.

---

## 2. ASCII wireframe

```
┌─ result-panel nested-panel summary-panel ─────────────────────────────────────┐
│  [ Download session (.zip) ]                                                   │
│  Analysis Summary                                                              │
│                                                                                │
│  DECK SCORE  ·  four coarse bands (0–5), from your decklist signals            │  ← .chatgpt-score__eyebrow
│  ┌───────────────┬───────────────┬───────────────┬───────────────┐            │  ← .chatgpt-score-grid (4 cols)
│  │ POWER         │ SPEED         │ CONTROL       │ CONSISTENCY   │            │  ← .chatgpt-score-label
│  │      4        │      3        │      4        │      3        │            │  ← .chatgpt-score-value (big 0–5)
│  │  ▓▓▓▓░         │  ▓▓▓░░         │  ▓▓▓▓░         │  ▓▓▓░░         │            │  ← .chatgpt-score-meter (5 pips)
│  │ [  High  ]    │ [Moderate]    │ [  High  ]    │ [Moderate]    │            │  ← .chatgpt-score-band (label pill)
│  │ 4 Game        │ avg MV 2.6 ·  │ 11 interaction│ 8 tutors ·    │            │  ← .chatgpt-score-rationale
│  │ Changers · 2  │ 9 fast-mana · │ pieces · 4    │ 2 combos ·    │            │     (--muted, --fs-xs)
│  │ combos · 9    │ 7 ramp/draw   │ board wipes · │ avg MV 2.6 —  │            │
│  │ fast mana     │ under 3 MV    │ 3 counters    │ redundant lines│            │
│  └───────────────┴───────────────┴───────────────┴───────────────┘            │
│                                                                                │
│  ┌─ .chatgpt-score-crosscheck (--agree) ──────────────────────────────────┐   │
│  │ ✓ CROSS-CHECK  Score aligns with the Bracket 4 classification           │   │  ← agreement state
│  │   (high Power + high Speed + 2 two-card combos are consistent with B4). │   │
│  └────────────────────────────────────────────────────────────────────────┘   │
│                                                                                │
│  ── (existing) Overview / Strengths / Weaknesses / Deck Needs / … ──           │  ← stub "per-category analysis"
└────────────────────────────────────────────────────────────────────────────────┘
```

Divergence variant of the cross-check line:

```
┌─ .chatgpt-score-crosscheck (--diverge) ───────────────────────────────────┐
│ ⚠ CROSS-CHECK  Score and bracket disagree — verify with your AI.          │
│   Power reads 5 (Extreme) but the deck classified as Bracket 2. Re-check   │
│   Game Changers membership and combo count before trusting either figure.  │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. The 0–5 band-label vocabulary

One **consistent intensity vocabulary** is used for all four axes. Each word describes
*how much of that axis* the deck has — it is deliberately a magnitude scale, **not** a
good/bad scale (a "Power: None" deck is not "bad", it is simply low-power; that is correct
and intended). Using one shared ladder keeps the grid scannable: the reader learns six
words once and reads all four cards the same way.

| Band | Label    | Meaning (reads naturally on every axis)                         |
|:----:|----------|----------------------------------------------------------------|
| 0    | **None**     | Signal essentially absent (e.g. zero interaction, zero combos) |
| 1    | **Low**      | Minimal presence; well below a focused list                    |
| 2    | **Modest**   | Some presence but not a priority of the build                  |
| 3    | **Moderate** | Solid, expected level for a focused casual/mid deck            |
| 4    | **High**     | Clearly emphasized; an above-average pillar of the deck        |
| 5    | **Extreme**  | Maxed-out / cEDH-tier saturation of this axis                  |

Justification of word choice:
- **None / Low / Modest / Moderate / High / Extreme** is monotonic and unambiguous in
  ascending order; no two adjacent words could be confused for the same level.
- They are axis-agnostic: "Speed: Extreme", "Control: None", "Consistency: Moderate",
  "Power: High" all read as plain English.
- They avoid value-laden words ("Weak", "Strong", "Good", "Bad") that would wrongly imply
  high-on-an-axis = better. A control deck that is "Power: Modest" is working as designed.
- "Extreme" (not "Maximum"/"Max") signals the cEDH ceiling without implying a hard cap the
  scorer can't actually prove.

### Signal → band mapping per axis (SCORE-02)

Bands are coarse buckets over the already-computed signals — **no decimals are ever shown**
(SCORE-01). The thresholds below are illustrative band edges for the spec; the executor
tunes exact cutpoints, but the *shape* (count/ratio → 0–5 bucket) is fixed.

| Axis | Source signals (already computed) | Band derivation (coarse buckets) |
|------|-----------------------------------|----------------------------------|
| **Power** | Game Changers count + two-card-combo density + fast-mana count (proxy signals, no card-quality DB) | Weighted bucket: 0 GC & 0 combos & little fast mana → low band; many GC + multiple combos + heavy fast mana → 4–5 |
| **Speed** | avg MV + fast-mana count + ramp/draw pieces castable under 3 MV + combo density | Low avg MV + lots of fast mana + early ramp/draw → high band; high curve + little acceleration → low band |
| **Control** | NEW interaction/removal classifier over deck categories (spot removal, board wipes, counters, stax, protection) | Count of interaction pieces → bucket; 0 → None, a few → Modest, a dense interactive shell → High/Extreme |
| **Consistency** | tutor count + combo redundancy + avg MV (smoothness) + ramp/draw density | Many tutors + redundant lines + smooth curve → high band; few tutors + high variance → low band |

Each card shows the **specific signal values** that produced its band as the rationale line
(SCORE-03), so the band is never an unexplained number.

---

## 4. Components reused + net-new classes

### Reused (no new CSS)
- `.result-panel.nested-panel.summary-panel` — the host container (existing Step-3 panel).
- Eyebrow pattern mirrors `.manabase-lens-label` / `.chatgpt-step-eyebrow` (uppercase, letter-spaced, `--muted`).
- Card shell mirrors `.manabase-lens` (soft card: `--panel-soft-bg`, 1px `--line`, radius 12px).
- Big value mirrors `.manabase-lens-big` (large headline in `--accent-strong`).
- Cross-check callout mirrors `.bracket-callout` (left-border accent, soft bg).

### Net-new classes (defined in `site-common.css`; previewed inline in the mockup)
| Class | Role |
|-------|------|
| `.chatgpt-score` | Wrapper block (margin + eyebrow + grid + crosscheck) |
| `.chatgpt-score__eyebrow` | Uppercase eyebrow heading ("Deck Score · …") |
| `.chatgpt-score-grid` | `display:grid; grid-template-columns:repeat(4,1fr); gap:1rem` |
| `.chatgpt-score-card` | Soft card, centered text; one per axis |
| `.chatgpt-score-label` | Uppercase axis name (Power/Speed/Control/Consistency) |
| `.chatgpt-score-value` | Big `0`–`5` numeral, `--accent-strong`, no decimals |
| `.chatgpt-score-meter` | 5-pip strip (non-color redundancy for the band level) |
| `.chatgpt-score-pip` / `.chatgpt-score-pip--filled` | Individual pip; filled count == band |
| `.chatgpt-score-band` | Band-label pill (None…Extreme); carries band color token |
| `.chatgpt-score-band--0 … --5` | Per-band color modifier (sets bg + ink from band tokens) |
| `.chatgpt-score-rationale` | Tiny `--muted`, `--fs-xs` signal line under the band |
| `.chatgpt-score-crosscheck` | Bracket cross-check note (bracket-callout shape) |
| `.chatgpt-score-crosscheck--agree` / `--diverge` | Agreement (green left-border) vs divergence (gold left-border) |

### Net-new band-color tokens (proposed, added to every theme `:root`)

A **sequential single-hue intensity ramp** (light → dark), NOT a red=bad/green=good ramp —
because a high band is "more of this axis", not "better". Each token is a fixed,
theme-independent fill **plus a baked-in legible ink color** (same proven pattern as
`.manabase-health--*`), so a band pill reads on both light themes (site.css) and dark
themes (site-nyx.css) because it carries its own background + text contrast and never relies
on the per-theme surface `--info`/`--warning` tokens (which are surface, not status, colors).

```css
:root {
  /* Phase 77 multi-axis score bands — sequential intensity, theme-independent */
  --score-band-0-bg: #cbd5e1; --score-band-0-ink: #1c1917; /* None     */
  --score-band-1-bg: #93c5fd; --score-band-1-ink: #1c1917; /* Low      */
  --score-band-2-bg: #60a5fa; --score-band-2-ink: #0b1220; /* Modest   */
  --score-band-3-bg: #3b82f6; --score-band-3-ink: #ffffff; /* Moderate */
  --score-band-4-bg: #2563eb; --score-band-4-ink: #ffffff; /* High     */
  --score-band-5-bg: #1e3a8a; --score-band-5-ink: #ffffff; /* Extreme  */
}
```

Rationale for the ramp: light→dark blue encodes magnitude ("more filled = more of this axis")
without the good/bad connotation of a green→red heat ramp. The pip meter + numeral + word
label all encode the same level, so color is purely reinforcing (see A11y).

---

## 5. States

| State | Condition | Render |
|-------|-----------|--------|
| **No result yet** | `Model.AnalysisResponse is null` (Steps 1–2, or Step 3 before paste) | Score block not rendered at all (it lives inside the `AnalysisResponse is not null` panel). No empty placeholder. |
| **Scored** | analysis JSON pasted + score fields present | Full 4-card grid + cross-check note. |
| **Flag-OFF / absent path** | If gated behind a namespaced flag seeded OFF, or score fields absent on the model | Block omitted entirely; page byte-identical to today (no eyebrow, no grid). The existing Overview/Strengths breakdown renders exactly as before. The prompt artifacts also omit the score section so the paste output is unchanged. |
| **Cross-check: agreement** | score axes consistent with the bracket classification | `.chatgpt-score-crosscheck--agree` — green left-border, "✓ Score aligns with the Bracket N classification". |
| **Cross-check: divergence** | a score axis contradicts the bracket (e.g. Power Extreme but B2) | `.chatgpt-score-crosscheck--diverge` — gold left-border, names the contradiction and tells the user to verify with their AI; never silently hides either figure. |

The flag-OFF byte-identical requirement matches the cycle-wide pattern (TAP-04 / BRACKET
flag) — if the executor chooses to gate this, the OFF path produces identical bytes for both
the page and all three artifacts.

---

## 6. Exact microcopy

- **Eyebrow:** `Deck Score · four coarse bands (0–5) from your decklist signals`
- **Axis labels (uppercase):** `POWER`  `SPEED`  `CONTROL`  `CONSISTENCY`
- **Band labels:** `None` · `Low` · `Modest` · `Moderate` · `High` · `Extreme`
- **Rationale phrasing** (terse, signal-led, no sentence case verbs — "what fed this band"):
  - Power: `4 Game Changers · 2 two-card combos · 9 fast-mana sources`
  - Speed: `avg MV 2.6 · 9 fast-mana · 7 ramp/draw under 3 MV`
  - Control: `11 interaction pieces · 4 board wipes · 3 counters`
  - Consistency: `8 tutors · 2 redundant combo lines · smooth 2.6 curve`
- **Cross-check (agree):** `Score aligns with the Bracket 4 classification — high Power + high Speed + 2 two-card combos are consistent with B4.`
- **Cross-check (diverge):** `Score and bracket disagree — verify with your AI. Power reads 5 (Extreme) but the deck classified as Bracket 2. Re-check Game Changers membership and combo count before trusting either figure.`
- **Cross-check label (eyebrow inside callout):** `CROSS-CHECK`
- Microcopy uses plain hyphens, no em/en dashes in the *artifact* text (note: page UI may keep `·` separators; artifacts use ` - ` to stay paste-safe).

---

## 7. Theme tokens

Reused existing per-theme tokens: `--panel-soft-bg`, `--line`, `--muted`, `--accent-strong`,
`--ink`, `--success`, `--warning`/`--gold-warning`, `--fs-xs`, `--fs-sm`.

New tokens (added to every theme `:root`, identical fixed values across themes — they are
status colors, not surface colors): `--score-band-{0..5}-bg` and `--score-band-{0..5}-ink`
(see §4). Because they are theme-independent and carry baked ink, they do **not** need a
per-theme override; defining them once in the shared `:root` (or duplicated identically per
theme to match the repo's "each theme is a full fork" convention) is sufficient.

The cross-check note borrows `--success` (agree, green left-border) and
`--gold-warning`/`--warning` (diverge, gold left-border), matching `.manabase-verdict--fine`
/ `--issues`.

---

## 8. Responsive (4 → 2 → 1)

```css
.chatgpt-score-grid { grid-template-columns: repeat(4, 1fr); }          /* desktop */
@media (max-width: 860px) { .chatgpt-score-grid { grid-template-columns: repeat(2, 1fr); } }  /* tablet */
@media (max-width: 520px) { .chatgpt-score-grid { grid-template-columns: 1fr; } }             /* phone  */
```

- Desktop (≥861px): 4 columns, one axis each.
- Tablet (521–860px): 2×2 grid.
- Phone (≤520px): single column, cards stack; the big numeral + meter + band pill stay
  centered. Rationale lines wrap; no horizontal scroll (the score block has no table).
- The cross-check note is full-width at every breakpoint.

---

## 9. Accessibility (not color-only)

The band level is encoded **four** redundant ways so it is never color-dependent:
1. The **numeral** `0`–`5` (`.chatgpt-score-value`).
2. The **word** label None…Extreme (`.chatgpt-score-band` text).
3. The **pip meter** — N of 5 filled pips (shape redundancy, `.chatgpt-score-pip--filled`).
4. (color) the band-color pill — reinforcing only.

Additional a11y:
- Each card is a `role="group"` with `aria-label="Power score: 4 of 5, High"` (full text
  in the accessible name; screen readers never need the color).
- The pip meter is decorative (`aria-hidden="true"`) since the value+word already convey level.
- Band pills carry baked-in ink contrast (WCAG-AA against their own fill) so low-vision and
  high-contrast users read them on any theme.
- Cross-check note uses a leading glyph (`✓` / `⚠`) **and** the `--agree`/`--diverge` class
  AND text — not color alone — and is wrapped `role="note"`.
- Inherits the universal `:focus-visible` outline (`--focus`).

---

## 10. How the score renders in all three prompt variants (SCORE-04)

The score folds into the existing analysis paste artifact for ChatGpt / Claude / Gemini.
Per ADR-0001 the three variants are **intentionally decoupled** — there is **no shared
helper**; each variant's builder emits its own score section, and a **parity test** asserts
all three contain the same axes, bands, and rationale figures.

Each artifact gains a plain-text score section near the deck-summary header, e.g.:

```
DECK SCORE (coarse 0-5 bands - magnitude, not quality)
  Power:       4/5  High      (4 Game Changers, 2 two-card combos, 9 fast-mana sources)
  Speed:       3/5  Moderate  (avg MV 2.6, 9 fast-mana, 7 ramp/draw under 3 MV)
  Control:     4/5  High      (11 interaction pieces, 4 board wipes, 3 counters)
  Consistency: 3/5  Moderate  (8 tutors, 2 redundant combo lines, smooth 2.6 curve)
Cross-check: score aligns with the Bracket 4 classification.
(These bands are DeckFlow heuristic estimates from decklist signals - re-check and refine.)
```

- Artifact text uses ASCII only (`/5`, ` - `, no em/en dashes) to stay paste-safe.
- The wording is duplicated across the three variant builders (decoupled), guarded by the
  parity test that the *figures* match — the surrounding prose may differ per variant.
- The artifact instructs the AI that the bands are heuristic estimates to refine, mirroring
  the bracket artifact's "re-confirm" framing (BRACKET-05 pattern) so a wrong band degrades
  gracefully into an AI correction rather than a silent error.

---

## 11. Open questions

1. **Control classifier approach (SCORE-02).** The new interaction/removal classifier needs
   a source of truth for "what counts as interaction". Options:
   (a) keyword/category matching over the parsed `deck_profile` categories + oracle text
   (spot removal / board wipe / counter / stax / protection buckets) — no new data, fast,
   but fuzzy; (b) a small versioned interaction-keyword seed file (like the Game Changers
   seed in BRACKET-02) — auditable and tunable, but new data to maintain; (c) delegate the
   raw categorization to the AI and only *band* it locally — cheapest but moves the signal
   out of DeckFlow's control and weakens the "from signals DeckFlow already computes" promise.
   **Recommendation:** (a) for v1 (reuse existing category/oracle signals), with the bucket
   keyword list factored so it can graduate to (b) a seed file later if accuracy demands.
2. **Band cutpoints.** Exact count/ratio → 0–5 thresholds per axis are not fixed by this
   spec; needs a small calibration pass against a handful of known decks (a cEDH list should
   land Power 5 / Speed 4–5; a precon should land Power 1 / Speed 1–2).
3. **Weighting within Power.** Game Changers vs combo density vs fast mana — equal weight, or
   GC-dominant? Suggest GC-dominant (GC count is the bracket-defining signal) with combos and
   fast mana as tie-breakers.
4. **Flag or always-on?** Cycle pattern gates new fold-ins behind a namespaced flag seeded
   OFF (byte-identical). Decide whether Phase 77 ships behind `analysis.multi-axis-score`
   (recommended, consistent with TAP-04/BRACKET) or always-on.
```
