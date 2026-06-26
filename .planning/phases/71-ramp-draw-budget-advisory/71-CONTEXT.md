# Phase 71: Manabase Plain-Language Verdict & Ramp/Draw Advisory - Context

**Gathered:** 2026-06-26
**Status:** Ready for planning
**Source:** PRD Express Path (71-SPEC.md + user clarity ask 2026-06-26)

<domain>
## Phase Boundary

Make the `/manabase` result understandable to non-experts and mirror that into the
manabase prompt artifact. Three deliverables, all advisory/explanatory on top of
the EXISTING computed report — no change to land count, color counts, castability
verdict, or health band:

A. Friendly one-line gloss for each surfaced metric (Karsten source check,
   simulated cast rate, weakest color, demanding cards).
B. A synthesized "Reading your deck" verdict: prioritized add/remove guidance when
   there are issues (ordered land/color → ramp → draw), or a specific
   why-it's-fine when there aren't.
C. A ramp/draw slot-budget check (counts ramp vs draw, compares to a
   threshold-derived target) folded into the summary as one input — Casual only.

Deterministic templated prose. NO LLM call for the on-page or builder text.
</domain>

<decisions>
## Implementation Decisions (locked)

### Delivery
- Surface in BOTH the on-page UI (`/manabase`) AND the manabase prompt artifact —
  EXCEPT deliverable A (metric glosses), which is **UI-only** (the prompt's reader
  is an LLM that needs no gloss; mirroring them bloats the artifact). B (verdict) +
  C (ramp/draw budget) go to both surfaces. (Scope decision 2026-06-26.)
- Prompt artifact = the SINGLE builder `ManabaseSwapPromptBuilder.Build(...)`
  (`Model.ChatGptSwapPrompt`). Manabase has ONE prompt builder — NOT the 3
  decoupled variants used by deck-analysis. Edit only this builder.

### Mode coverage
- Casual mode gets the full read (glosses + verdict + ramp/draw budget).
- cEDH mode gets the metric glosses, but the ramp/draw budget block is SUPPRESSED
  (its numbers are casual-shaped; 38-land floor breaks cEDH).

### Classification & targets
- Reuse existing ramp/draw predicates in `ManabaseClassifier.cs`
  (`IsRockOrDork` / `ProducesMana` / land-ramp / repeatable-draw / cantrip). NO
  new tagger, NO extra Scryfall calls.
- Overlap cards (cantrip rocks, Mystic Remora, wheels): count 0.5 to each bucket,
  list in both, show "(N do both)".
- Threshold = commander MV; fallback = deck nonland-curve median (75th-percentile
  MV of nonland spells). Copy must state the proxy explicitly.
- Ramp/draw target split (total 24) interpolates anchors: MV≤2 → 8/16; MV4 →
  12/12; MV≥6 → 14/10; draw = 24 − ramp. Labeled "community heuristic, not
  Karsten math".

### Synthesis behavior
- Build only from already-computed report data (color findings, weakest color,
  cast rate / health band, demanding cards, ramp/draw counts).
- Issue path: order by land/color → ramp → draw (Liebig minimum; matches
  health-band priority). Cap at top ~3 issues. Each = plain instruction + rough
  quantity + example lever.
- No-issue path: explicit, specific reasons drawn from the actual numbers — name
  the colors that cleared + the cast rate + why acceptable for the mode. Not a
  generic "looks good".

### Plumbing
- Flag `manabase.plain-language-verdict`, seeded OFF both dialects (SQLite +
  Postgres), MQ-flag pattern + fail-safe read via `Snapshot().TryGetValue` (NOT
  IsEnabled). Catalog description added in `FeatureFlagCatalog.cs`.
- Ramp/draw budget may share this flag (lean: one flag) — planner decides.

### Web-page-change obligations (project rule)
- New/changed page → add xUnit + Playwright AND verify desktop + mobile across
  themes. Layout CSS in `site-common.css` (theme tokens), NOT `site.css`.
- README + `Help/manabase.md` updated in the same change.

### Claude's Discretion
- Exact ramp-slot interpolation formula + rounding/clamps.
- One shared flag vs verdict-flag + ramp/draw subflag.
- Issue-priority thresholds + nudge deadband (±2 slots = "balanced", ±1 source =
  "fine").
- Copy tone/length per surface (UI terse vs prompt fuller).
- Verdict-block placement in the view and within `ManabaseSwapPromptBuilder`.
- No-issue read = one sentence vs short bulleted "why".
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Spec / theory
- `.planning/phases/71-ramp-draw-budget-advisory/71-SPEC.md` — full phase scope, non-goals, done-when
- `.planning/captures/ramp-draw-unit-of-value-theory.md` — theory evaluation the advisory is derived from

### Code anchors (read before planning)
- `DeckFlow.Core/Manabase/ManabaseClassifier.cs` — ramp/draw predicates to reuse (no new tagger)
- `DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs` — THE single manabase prompt builder to augment
- `DeckFlow.Web/Models/ManabaseDisplay.cs` — display helpers (`AvgOnCurve`, `KarstenMet`) feeding the view
- `DeckFlow.Web/Models/ManabaseViewModel.cs` — view model (`ChatGptSwapPrompt`, `ShowCastability`, mode, report)
- `DeckFlow.Web/Services/Manabase/ManabaseAnalysisService.cs` — where the report + prompt are assembled and flags read
- `DeckFlow.Web/Views/Deck/Manabase.cshtml` — two-lens band (`.manabase-twolens` ~line 169), swap-prompt block (~line 437)
- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` + `FeatureFlagCatalog.cs` — flag seed (both dialects) + description
- `DeckFlow.Web/wwwroot/css/site-common.css` — layout/theme-token CSS (NOT site.css)
- `DeckFlow.Web/Help/manabase.md` + `README.md` — docs to update

### Test anchors
- `DeckFlow.Core.Tests/` manabase tests + `DeckFlow.Web.Tests/Manabase/` (display/plumbing); flag-seed tests; `DeckFlow.Web/e2e/manabase*.spec.ts`
</canonical_refs>

<specifics>
## Specific Ideas

- Gloss examples: "need −3 = about 3 short"; cast rate = "how often spells are
  castable on/before their ideal turn, higher = smoother".
- Issue example: "You're ~3 white sources short — add ~3 white-producing
  lands/rocks; consider cutting a colorless utility land."
- No-issue example: "Both colors clear their Karsten targets and your 87% avg
  on-curve cast rate is healthy for Casual — no changes needed."
</specifics>

<deferred>
## Deferred Ideas

- cEDH ramp/draw budget (needs a lower-land/fast-mana-aware table) — out of v1.
- Adopting the fungible "unit of value" as a scoring number — explicitly rejected.
- Fixed 38-land / 24-slot HARD rule — advisory only, never enforced.
</deferred>

---

*Phase: 71-ramp-draw-budget-advisory*
*Context gathered: 2026-06-26 via PRD Express Path*
