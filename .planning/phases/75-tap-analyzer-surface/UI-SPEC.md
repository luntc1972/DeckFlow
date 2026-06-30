# UI-SPEC — Phase 75: Tap Analyzer Surface

Design contract for surfacing untapped-source frequency + turn-1 untapped availability
on `/manabase`. Additive only, flag-gated, single source of truth with the existing
cast-rate figures.

- **Requirements covered:** TAP-01 (untapped-source frequency, overall + per color),
  TAP-02 (turn-1 untapped availability), TAP-03 (read from the existing
  `CastabilitySimulator` single pass; additive; must never contradict the cast-rate
  numbers already shown), TAP-04 (renders on the page AND the paste artifact, behind flag
  `analysis.manabase.tap-analyzer` seeded OFF; page byte-identical when off).
- **Surface:** `DeckFlow.Web/Views/Deck/Manabase.cshtml`, `ManabaseViewModel`,
  `ManabaseDisplay.cs`, `ManabaseReportTextBuilder.cs`.

---

## 1. Surface overview

The Tap Analyzer is a **third metric card** that sits directly under the existing
`.manabase-twolens` grid (Karsten source check | Simulated cast rate), inside the same
`<section class="result-panel">`, before `.manabase-context`. It is a full-width
`.manabase-lens` card (the established two-lens "soft card" chrome) carrying:

- **Headline (TAP-02):** turn-1 untapped availability as a `.manabase-lens-big` figure +
  a `.manabase-lens-pill` qualifier.
- **Per-color breakdown (TAP-01):** an "Overall" summary row plus one `.manabase-lens-row`
  per color showing that color's untapped-source frequency, with the existing
  `.manabase-lens-met` (✓) / `.manabase-lens-short` (⚠) markers.

It reuses the two-lens vocabulary wholesale; only the full-width placement and the internal
"headline | colors" split are net-new layout, so the card reads as if it always belonged.

**Single source of truth (TAP-03):** every number on the card is read from the *same*
`CastabilitySimulator` pass that already produces the cast-rate lens and the castability
table. The card explains the *tapped-land timing dimension* that feeds cast rate; it never
re-states or re-derives a cast %, so it cannot contradict the figures above it.

---

## 2. ASCII wireframe (manabase result area, flag ON, Casual, multi-color)

```
┌─ section.result-panel ────────────────────────────────────────────────────────┐
│  Result                                                                         │
│  Yidris cascade pile · Archidekt · 99 cards + 1 commander                       │
│                                                                                 │
│  ┌─ .manabase-twolens (grid 1fr 1fr) ───────────────────────────────────────┐  │
│  │ ┌ .manabase-lens ───────────────┐ ┌ .manabase-lens ───────────────────┐  │  │
│  │ │ KARSTEN SOURCE CHECK          │ │ SIMULATED CAST RATE               │  │  │
│  │ │ U  18.5 / 16 need        ✓    │ │   81%  avg on-curve               │  │  │
│  │ │ B  16.0 / 15 need        ✓    │ │   Joint mana + color, London…     │  │  │
│  │ │ R  14.0 / 15 need     ⚠ −1    │ │   [ avg across 41 tracked spells ]│  │  │
│  │ │ G  17.0 / 14 need        ✓    │ │                                   │  │  │
│  │ └───────────────────────────────┘ └───────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  ┌─ .manabase-lens.manabase-taplens  (full width) ──────────────────────────┐  │
│  │ UNTAPPED SOURCES                                                          │  │
│  │ ┌ .manabase-taplens-split (grid: headline | colors) ─────────────────┐   │  │
│  │ │  ┌ headline ───────────────┐  ┌ colors (.manabase-lens-row list) ─┐ │   │  │
│  │ │  │   76%  turn-1 untapped  │  │ Overall          82% untapped     │ │   │  │
│  │ │  │ [ share of games with   │  │ U   84% untapped  (15.5/18.5)  ✓  │ │   │  │
│  │ │  │  an untapped source of a│  │ B   78% untapped  (12.5/16.0)  ⚠  │ │   │  │
│  │ │  │ needed color on turn 1 ]│  │ R   71% untapped  (10.0/14.0)  ⚠  │ │   │  │
│  │ │  └─────────────────────────┘  │ G   88% untapped  (15.0/17.0)  ✓  │ │   │  │
│  │ │                               └───────────────────────────────────┘ │   │  │
│  │ └─────────────────────────────────────────────────────────────────────┘   │  │
│  │  How often a colored source can be spent the turn it's available —         │  │
│  │  tapped lands (Temples, tri-lands) push back your earliest castable turn.  │  │
│  │  Drawn from the same simulation as the cast-rate above.                    │  │
│  │  (gloss line — only when plain-language flag is also on)                    │  │
│  └───────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  Mode: Casual · Commander: Standard            ← .manabase-context (unchanged)  │
│  Lands: 34 vs ~35.2 recommended …  Health: [Solid]                              │
│  … verdict / ramp-draw / table / castability (all unchanged) …                  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Components reused (exact existing classes)

| Element | Existing class | Source |
|---|---|---|
| Card chrome (border, soft bg, radius, padding) | `.manabase-lens` | `site-common.css:2602` |
| Uppercase eyebrow ("UNTAPPED SOURCES") | `.manabase-lens-label` | `site-common.css:2609` |
| Turn-1 headline figure + unit | `.manabase-lens-big` (+ child `> span`) | `site-common.css:2648` |
| Qualifier pill under headline | `.manabase-lens-pill` | `site-common.css:2676` |
| Per-color / Overall stat rows | `.manabase-lens-row` | `site-common.css:2617` |
| Color name on a row | `.manabase-lens-color` | `site-common.css:2630` |
| Muted sub-figure ("(15.5/18.5)") | `.manabase-lens-muted` | `site-common.css:2634` |
| Met marker ✓ | `.manabase-lens-met` | `site-common.css:2638` |
| Below-threshold marker ⚠ | `.manabase-lens-short` | `site-common.css:2643` |
| Explanatory note | `.manabase-lens-note` | `site-common.css:2662` |
| Plain-language gloss (conditional) | `.manabase-lens-gloss` | `site-common.css:2668` |

### Net-new classes (2) — layout only, tokenized

| Class | Why it's needed | Definition (lives in `site-common.css`) |
|---|---|---|
| `.manabase-taplens` | The card is full-width (one card, not a 1fr/1fr pair), so it can't sit *in* `.manabase-twolens`. This adds the snug top margin tying it to the grid above and the full-width block flow. No colors — chrome comes from the composed `.manabase-lens`. | `.manabase-taplens { margin: -0.25rem 0 1rem; }` |
| `.manabase-taplens-split` | Two-up internal layout: turn-1 headline column beside the per-color list, collapsing to a single stacked column on narrow screens. Purely structural. | `.manabase-taplens-split { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr); gap: 1rem 1.5rem; align-items: start; }` + `@media (max-width: 640px) { .manabase-taplens-split { grid-template-columns: 1fr; } }` |

No net-new color/token introduced. Both new classes use only spacing; every visible color
flows from the reused lens classes (which already resolve `var(--panel-soft-bg)`,
`var(--line)`, `var(--success)`, `var(--gold-warning)`, `var(--accent-strong)`,
`var(--muted)`). Per the theme-CSS constraint, both new rules go in **`site-common.css`**
(layout/cross-cutting), not in any per-theme fork.

---

## 4. States

| State | Behavior |
|---|---|
| **Flag OFF** (`analysis.manabase.tap-analyzer` = false; default) | `ViewModel.ShowTapAnalyzer == false`. The entire `@if (Model.ShowTapAnalyzer && …)` block is skipped — **no element, no whitespace, no comment** emitted. Page is **byte-identical** to today. The paste-artifact builder is called with `tap: null` (or the `ShowTapAnalyzer` bool), so the "Untapped Sources" section is omitted from the `.txt` too. (Guarded by the `CarveOutGuard`-style byte-identity test pattern.) |
| **Result present, multi-color** | Full card: headline + Overall row + one row per color in `report.ColorFindings` order, ✓/⚠ per color. |
| **Result present, single-color deck** (`ColorFindings.Count == 1`) | Per-color list is **omitted** (it would duplicate "Overall"). Card shows the turn-1 headline + pill + a single "Overall NN% untapped" row only. Mono-color decks have no color-screw axis, so the per-color breakdown is noise. |
| **No result** (`HasResult == false`) | Card is inside the `@if (Model.HasResult && report is not null)` result panel, so it does not render — same as every other result element. |
| **cEDH mode** | The turn-1 headline (TAP-02) is sim-derived. If the sim pass runs in cEDH (see Open Questions), render the full card. If sim/cast-rate data is unavailable in cEDH (as the right cast-rate lens already is), render the **reduced** card: Overall + per-color untapped *composition* frequency (a deterministic land-composition figure that does not need the sim), and **omit the turn-1 headline**, replacing it with the Overall figure as the `.manabase-lens-big`. Decision deferred to plan-phase. |
| **Threshold marker** | ✓ when a color's untapped frequency ≥ 80%; ⚠ when < 80%. This is an *informational* tapped-density signal, intentionally a different axis from cast %, so a ⚠ here can co-exist with a healthy cast rate without contradiction. |

---

## 5. Exact microcopy / labels

- **Card eyebrow:** `Untapped sources`
- **Headline figure (TAP-02):** the turn-1 untapped availability percent, e.g. `76%`
- **Headline unit span:** `turn-1 untapped`
- **Headline pill:** `share of games with an untapped source of a needed color on turn 1`
- **Overall row:** label `Overall`, value `82% untapped`
- **Per-color row:** color glyph/name (left) · `NN% untapped` + muted `(actual / total sources)` + ✓/⚠ (right). Example: `U` … `84% untapped (15.5 / 18.5) ✓`
- **Note (`.manabase-lens-note`, always shown when card renders):**
  `How often a colored source can be spent the turn it's available — tapped lands (Temples, tri-lands, taplands) can't make mana the turn they enter, so they push back your earliest castable turn. Drawn from the same simulation as the cast rate above, so it never contradicts it.`
- **Plain-language gloss (`.manabase-lens-gloss`, only when `ShowPlainLanguage` is also true):**
  new `ManabaseDisplay.TapAnalyzerGloss` constant, written in the existing gloss voice:
  `"Tapped lands (Temples, tri-lands, taplands) can't tap for mana the turn they enter, so they push back your first castable turn. Higher untapped % = faster, smoother starts."`

Units: percentages are whole numbers with a trailing `%` and the word `untapped`; source
counts render to one decimal (`F1`) to match the existing per-color Sources figures.

New `ManabaseDisplay` helpers (mirroring `AvgOnCurve` / `KarstenMet` patterns, unit-testable):
- `const string TapAnalyzerGloss` (above).
- `(string Css, string Marker) TapMarker(int untappedPercent)` → `("manabase-lens-met","✓")`
  at ≥80, else `("manabase-lens-short","⚠")`. Mirrors `CastChip`.

---

## 6. Theme tokens used

All via reused classes (no new tokens):
`--panel-soft-bg`, `--line`, `--surface` (card chrome); `--accent-strong` / `--accent`
(headline figure, pill text); `--muted` (eyebrow, units, note, gloss); `--success`
(✓ met); `--gold-warning` / `--warning` (⚠ short); `--info` (pill bg). The card therefore
re-skins automatically across all 22 themes exactly like the two-lens cards. Verify on
`site.css` (Jeskai), `site-azorius.css`, `site-nyx.css`.

---

## 7. Responsive (mobile stacking)

- `.manabase-taplens` is full-width at every breakpoint (it is not part of the 1fr/1fr
  grid that collapses).
- `.manabase-taplens-split` is `1.0fr | 1.4fr` on desktop; at `max-width: 640px` (the same
  breakpoint the existing `.manabase-twolens` collapse already uses, `site-common.css:2769`)
  it becomes a single column: turn-1 headline on top, per-color list beneath.
- Rows use the existing `.manabase-lens-row` flex `space-between`, which already wraps
  gracefully on narrow widths.

---

## 8. A11y

- The card needs no new landmark; it lives inside the existing `result-panel` `<section>`.
- Eyebrow `.manabase-lens-label` text "Untapped sources" provides the visible group label;
  add `aria-label="Untapped sources"` to the card root (`<div class="manabase-lens manabase-taplens" role="group" aria-label="Untapped sources">`) so the grouping is announced — matching how `.manabase-verdict` / `.manabase-cmd-castability` carry `aria-label`.
- ✓ / ⚠ markers are **never color-alone**: they pair a glyph + the `NN% untapped` text, same
  as the Karsten lens (`.manabase-lens-met` / `.manabase-lens-short`). Add
  `aria-hidden="true"` to the bare glyph and an `.sr-only` word (`<span class="sr-only">meets target</span>` / `<span class="sr-only">below target</span>`) so screen readers get a word, not a symbol.
- Headline figure: the visible "76%" + "turn-1 untapped" unit + pill sentence together read
  as a complete clause; no extra aria needed.
- Inherits the universal `:focus-visible` outline (no focusable controls added).

---

## 9. Paste-artifact (`.txt`) rendering

Append a new **"Untapped Sources"** block in `ManabaseReportTextBuilder.Build(...)`, gated
by the flag (a new optional `ManabaseTapAnalysis? tap = null` parameter; when null the block
is skipped, so the artifact is byte-identical when the flag is off). Place it directly after
the "Color Sources:" table (the natural sibling), using the same fixed-width column style:

```
Untapped Sources:
Turn-1 untapped availability: 76% (share of games with an untapped source of a needed color on turn 1)
Overall: 82% of colored sources enter untapped
Color        Untapped   Sources
------------------------------------------------------------
U                  84%   15.5 of 18.5
B                  78%   12.5 of 16.0
R                  71%   10.0 of 14.0
G                  88%   15.0 of 17.0
```

For a single-color deck, omit the per-color table and emit only the
`Turn-1 untapped availability` + `Overall` lines. The numbers are the *identical* values
shown on the page (one computation, two render targets) so the artifact and the page never
disagree — satisfying the "paste-ready, no reformatting" core value and TAP-03.

---

## 10. Open questions for plan-phase

1. **Does the `CastabilitySimulator` pass run in cEDH mode?** The cast-rate lens and
   castability table are Casual-only (`ShowCastability`). If the sim does not run in cEDH,
   TAP-02 (turn-1 untapped availability) has no source there → use the reduced cEDH card
   (Section 4). Confirm whether the sim already executes in cEDH for the Karsten findings.
2. **Exact definition of "turn-1 untapped availability" (TAP-02).** Proposed: share of
   simulated opening games where the player has ≥1 source able to produce mana on turn 1
   (i.e. an untapped land/rock available to spend). Alternative: per-color ("all needed
   colors available untapped T1"). Pick one and name it consistently page + artifact.
3. **Untapped frequency denominator.** Is "untapped %" computed over land sources only, or
   over all colored sources (rocks/dorks count as untapped-on-entry)? Must match how the sim
   already credits sources so the figure is consistent with the cast-rate model (TAP-03).
4. **✓/⚠ threshold (80%).** Confirm 80% is the right informational cutoff, or tie it to the
   mode (e.g. stricter in cEDH where tempo matters more). It must stay *informational* and
   never flip the health verdict (health is computed elsewhere and is the single authority).
5. **Plumbing the tap data.** New `ManabaseTapAnalysis` record on `ManabaseReport` (Core),
   populated in the same sim pass; `ShowTapAnalyzer` bool on `ManabaseViewModel`; flag wired
   in `FeatureFlagCatalog` (`analysis.manabase.tap-analyzer`, seeded OFF) + `FeatureFlagStore`
   key mapping. Confirm the additive Core surface is `{ get; init; }` (carve-out: never
   convert to get-only — STJ skips get-only props).
```
