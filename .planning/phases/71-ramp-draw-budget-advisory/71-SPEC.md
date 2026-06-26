# Phase 71 SPEC — Manabase Plain-Language Verdict & Ramp/Draw Advisory

Status: SPEC (scoping). Sources:
`.planning/captures/ramp-draw-unit-of-value-theory.md` (ramp/draw theory eval) +
user clarity ask 2026-06-26 ("the analysis is hard to understand").

Scoping decisions (user, 2026-06-26):
- **Primary ask:** the current manabase result is hard to read. Make it
  understandable — friendly explanation of *what each metric is*, plus a
  plain-language "reading your deck" that says what to add/remove to improve, or,
  when there are no issues, clearly explains *why* the deck is fine.
- Ramp/draw budget advisory (original Phase 71) folds in as one input.
- Delivery = **both UI + prompt**; ramp/draw target = **dynamic-from-threshold**;
  mode coverage = **Casual gets the full read in v1** (cEDH gets the metric
  explanations but the ramp/draw budget block is suppressed — its numbers are
  casual-shaped).

## What it delivers

Turn the manabase result from raw numbers into something a non-expert can act on:

1. **Friendly metric explanations** — each lens/number gets a one-line plain
   gloss so the user knows what it means without prior knowledge.
2. **"Reading your deck" summary** — a synthesized, prioritized plain-language
   verdict:
   - **If issues:** ordered, concrete add/remove suggestions ("you're ~3 white
     sources short — add about 3 white-producing lands/rocks; you could cut a
     colorless utility land for one").
   - **If no issues:** a clear, specific *why* ("both colors clear their Karsten
     targets and your average on-curve cast rate is 87%, healthy for Casual — no
     changes needed").
3. **Ramp/draw budget check** — counts ramp vs draw slots, compares to a
   threshold-derived target, feeds the summary as one more input (Casual only).

All advisory. It never changes the land count, color counts, castability verdict,
or health band — it *explains* and *recommends* on top of them.

## Why

DeckFlow already computes the right things (Karsten source counts, Monte-Carlo
castability, weakest color, demanding cards, ramp/draw classification) but
presents them as bare numbers ("White 23.6/26 need ⚠ −3", "87% avg on-curve")
with no gloss and no synthesized "so what." This is a presentation/synthesis gap,
not a math gap — and it directly serves the paste-into-AI core value when mirrored
into the prompt artifact.

## In scope

### A. Friendly metric explanations
- One-line plain gloss for each surfaced metric (reuse existing copy slots /
  help text where present; add where missing):
  - Karsten source check: "Enough lands/rocks of each color to reliably have that
    color when you need it. 'need −3' = about 3 short."
  - Simulated cast rate: "Across simulated games, how often your spells are
    castable on or before their ideal turn. Higher = smoother."
  - Weakest color / demanding cards: short gloss each.
- UI: inline under each lens (theme-token styled, `site-common.css`); must not
  break the two-lens layout or mobile stacking.

### B. "Reading your deck" synthesis (the headline)
- New synthesized block (UI section + prompt block) built from already-computed
  data: color findings, weakest color, cast rate / health band, demanding cards,
  ramp/draw counts.
- **Issue path:** prioritize by the established order — lands/color shortfall
  first, then ramp, then draw (Liebig minimum, matches health-band priority).
  Each item = plain instruction with a rough quantity and an example lever
  ("add ~N", "consider cutting a [type]"). Cap at the top ~3 issues.
- **No-issue path:** explicit, specific reasons drawn from the actual numbers (not
  a generic "looks good") — name the colors that cleared, the cast rate, and why
  that's acceptable for the chosen mode.
- Deterministic templated prose from the model — NO LLM call (this is the
  on-page/prompt text itself; the prompt artifact is what the user pastes into an
  LLM).

### C. Ramp/draw budget advisory (folded from original scope)
- Classification reuses existing ramp/draw predicates (`ramp-credit-v2` /
  `land-ramp-sim`); overlap cards (cantrip rocks, Mystic Remora) count 0.5 to each
  bucket, listed in both, "(N do both)".
- Threshold = commander MV; fallback = deck nonland-curve median; proxy stated in
  copy.
- Target split (total 24) interpolates the article anchors: MV≤2 → 8/16; MV4 →
  12/12; MV≥6 → 14/10. Labeled "community heuristic, not Karsten math".
- Feeds the "Reading your deck" summary; Casual only (cEDH suppresses this block).

### D. Plumbing
- Flag `manabase.plain-language-verdict` (seeded OFF both dialects, MQ-flag
  pattern + fail-safe read; catalog description added). Ramp/draw budget can share
  this flag or get its own sub-flag `manabase.ramp-draw-budget` — PLAN decides
  (lean: one flag, simpler).
- Prompt block added to the SINGLE manabase prompt builder
  `DeckFlow.Core/Manabase/ManabaseSwapPromptBuilder.cs` (surfaced as
  `Model.ChatGptSwapPrompt`, one "Copy for ChatGPT / Claude" box). NOTE: manabase
  has ONE prompt builder, not the 3 decoupled variants used by deck-analysis —
  edit only this builder. The verdict text augments the deterministic-report
  framing already in `Build(...)`.

## Tests
- Core: synthesis selects + orders the right issues per fixture deck (color-short,
  ramp-light, clean deck → no-issue path with correct reasons); ramp/draw bucket
  counts incl. overlap 0.5; threshold interpolation anchors + between; fallback
  median.
- Web: flag plumbing (on/off → present/absent), display-model strings.
- Playwright (throwaway/live, not CI): verdict + explanations render Casual
  desktop+mobile × 2 themes; cEDH → metric glosses present, ramp/draw block
  absent; no-issue deck shows the why-it's-fine read.

## Out of scope (explicit non-goals)
- NO change to land count, color counts, castability verdict, or health band —
  explanation/recommendation layer only.
- NO LLM call for the on-page text (it must work offline; the prompt artifact is
  the thing fed to an LLM).
- NO adoption of the fungible "unit of value" as a scoring number; NO 38/24 hard
  rule.
- NO cEDH ramp/draw budget in v1 (casual-shaped numbers).
- NO new tagger or extra Scryfall calls beyond what analysis already fetches.

## Open / ambiguity to resolve in PLAN
- One shared flag vs verdict-flag + budget-subflag.
- Exact issue-priority thresholds (how short before it's flagged; nudge deadband
  so a deck ±2 slots reads "balanced", ±1 source reads "fine").
- Copy tone/length per surface (UI terse vs prompt fuller).
- Prompt-block placement within each variant.
- Whether the no-issue read is one sentence or a short bulleted "why".

## Done when
- Flag OFF → prod byte-identical (UI + prompt unchanged).
- Flag ON, Casual: each metric has a plain gloss; a "Reading your deck" block
  shows either prioritized add/remove guidance OR a specific why-it's-fine; the
  ramp/draw budget line appears; all numbers trace to existing computed data.
- cEDH: glosses present, ramp/draw budget suppressed.
- Core + Web tests green; live Playwright screenshots Casual desktop+mobile × 2
  themes (issue deck + clean deck); build clean.
- README + `Help/manabase.md` document the verdict/advisory + that recommendations
  are heuristic.
