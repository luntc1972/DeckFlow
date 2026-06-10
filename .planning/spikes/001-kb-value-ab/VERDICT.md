# Spike 001 — kb-value-ab — VERDICT

**Date:** 2026-06-10
**Judge:** Claude (Opus 4.8) acting as the analysis-target AI. NOT blind (saw both prompts).
Recommend an independent real-ChatGPT paste to confirm.

## What was tested

Real ~99-card Atraxa, Praetors' Voice deck (proliferate / +1/+1 counters / superfriends
goodstuff, Bracket 3 target) run through `DeckAnalysisPacketService.BuildAsync` twice — once with
the Expert Context block (4 Salubrious Snail clips), once without. Real Scryfall oracle text for all
91 unique cards. Only prompt delta = the `## Expert Context` block (verified by diff).

Supersedes the original 3-card-stub A/B (Atraxa + Sol Ring + Arcane Signet), which was too trivial
to judge analysis lift.

## The two answers (summarized)

### Baseline (deck only)
- **Strengths:** dense premium interaction (StP/Path/Anguished Unmaking/Despark + 3 wipes + Cyclonic
  Rift); strong card-advantage engines (Rhystic, Remora, Sylvan, Esper Sentinel); a coherent
  proliferate sub-theme (Atraxa + Evolution Sage/Flux Channeler/Inexorable Tide/Karn's Bastion/
  Deepglow Skate/Vorinclex/Doubling Season accelerating both +1/+1 counters AND walker loyalty);
  solid 4-color ramp + fixing.
- **Weaknesses (top, unprompted):** (1) **No clear win condition** — durdles, no overrun/combo/
  evasive recurring threat; walker ultimates are slow and telegraphed; est. win turn ~8-9. (2)
  **Identity split** — simultaneously a counters deck, a superfriends deck, AND a goodstuff control
  pile; the axes don't reinforce and the +1/+1 creature package is too thin to win. (3) Thin board →
  walkers undefended. (4) Light protection for Atraxa/walkers (only Counterspell + Swan Song). (5)
  4-color pip greed.
- **Bracket:** 3 (Upgraded) — quality + interaction but slow, unprotected, no closing line.
- **Adds:** a finisher (Craterhoof / Finale of Devastation / Cathars' Crusade), protection (Heroic
  Intervention / Teferi's Protection), counter-payoffs that *close* (Walking Ballista). **Cuts:**
  cute non-closing counter payoffs, low-impact walkers, Painful Truths.

### With-context (deck + 4 snail clips)
Substantively **the same analysis**, with three deltas:
- (+) The "unfocused / too many directions" weakness — already the baseline's #1 — gets **creator
  attribution** ("Snail's Mistake #5 applies directly") and slightly more emphasis on picking ONE
  axis.
- (+) The "durability = protection, not speed" clip reinforces the protection weakness the baseline
  already flagged; lets the analyst lean harder on Heroic Intervention / Teferi's Protection.
- (−) The "glass cannon folds to a single piece of interaction → build redundancy" clip **slightly
  misfits**: this is a grindy control pile, not a glass cannon. An honest analyst notes it doesn't
  apply; a weaker one could misframe the deck.

No strategic signal appeared that the baseline lacked.

## Rubric scores (with-context lift over baseline; 1-5)

| Dimension | Score | Note |
|-----------|:----:|------|
| 1. Specificity | **1** | Clips are generic deckbuilding maxims; add no deck-specific concreteness. |
| 2. Creator-voice | **3** | Attribution + "mistakes" framing surface, but it's generic voice, not distinctive snail heuristics. |
| 3. Novel signal | **1** | Nothing baseline didn't already produce; one clip slightly misleads. |
| 4. Actionability | **2** | Marginally sharpens focus + protection emphasis already present. |
| Quality loss | minor | Glass-cannon clip is a mild misapplication risk. |

## Verdict: **MARGINAL — does NOT clear the gate**

Rubric bar = "clear lift on 2-3 dimensions + no quality loss → VALIDATED; marginal → reconsider the
whole KB." This is marginal.

## Why it's marginal — and what it does/doesn't imply

The low lift is driven by **clip genericity**, not by "expert content is worthless." The injected
clips are top-of-funnel deckbuilding-101 maxims (focus your deck, protect your threats) that a
capable model (ChatGPT/Claude/Gemini) already produces unprompted. Two further structural points:

1. **The clips were hardcoded in the harness**, not retrieved by the live `ContentKbRelevanceService`
   for this commander. So this measures "do these 4 generic clips help," not "does the relevance
   service surface *deck-relevant, creator-distinctive* passages." A true end-to-end KB-value test
   must run the real relevance service against the prod corpus.
2. The result therefore **does not, on its own, green-light the full Creator Philosophy-Profile
   build.** It argues:
   - **Against** shipping the current generic-clip retrieval as-is (low lift vs. maintenance cost).
   - **Conditionally for** the philosophy-profile redesign ONLY IF that redesign produces
     deck-specific, creator-distinctive conditioning rather than generic maxims — which is an
     unproven hypothesis, not validated here.

---

# Run 2 (GOLD) — real `ContentKbRelevanceService` end-to-end (2026-06-10)

Reconstructed the prod corpus locally (82 visible site-index rows, snail-heavy, regenerated artifact
`.md` from `uat-content-kb.db`), wired the **real** `ContentKbRelevanceService` (real scorer +
`SelectTopClips` + budget trim, flag forced on) over it, and let it select clips for the Atraxa deck
(archetypes: ramp/control/value-engine/midrange). Harness Fact `EmitRealRetrievalPrompt`; outputs
`selected-clips-real.txt` + `with-context-real.txt`. Build 0/0, test green.

## What the real scorer selected

**5 clips, ALL from a single video** — *"The Problem with Glass Cannon Commanders"* (score 5.06 each):
- 3 of 5 are about **other commanders** (Kaalia, Animar) — pure noise for an Atraxa deck.
- 1 mentions Atraxa only in passing ("Atraxa gives proliferates" → payoff commander) — no action.
- 1 pushes a **glass-cannon frame that misfits** this grindy goodstuff pile.
- The scorer **ignored** the genuinely on-point snail videos in the corpus — *"You Might Have Too
  Much Ramp"* (this deck over-ramps), *"5 Most Common Deckbuilding Mistakes"* (focus), *"How to Play
  More Removal."*

## This is WORSE than Run 1's hand-picked clips

| | Run 1 (hand-picked) | Run 2 (real scorer) |
|---|---|---|
| Clip relevance | generic-but-applicable maxims | 3/5 about unrelated commanders |
| Diversity | 4 distinct ideas | 5 clips, 1 video, 1 theme |
| Net effect | marginal lift | **negative** — injects noise + a misleading frame |

Rubric (Run 2 vs baseline): Specificity **1**, Creator-voice **2**, Novel signal **1**,
Actionability **0-1**, Quality **NEGATIVE** (a careful analyst would flag "these clips are about
other decks").

## Two mechanism defects exposed (root cause of the negative result)

1. **No per-video diversity in selection.** Clips inherit their site-index row's score, so the single
   highest-scoring video monopolizes every slot. The expert block is 5 clips from one tangential
   video instead of 5 distinct perspectives.
2. **Tag-overlap scoring matched the wrong video.** "Glass Cannon Commanders" (broad tags:
   midrange/combo/value-engine/ramp/aggro + Upgraded/Optimized/cEDH) outscored directly-relevant
   "Too Much Ramp" / "5 Mistakes", even though its *content* is about specific other commanders.
   Scoring rewards tag breadth, not topical fit.

## FINAL VERDICT: **MARGINAL → leaning NEGATIVE. Gate NOT cleared.**

The KB clip-retrieval mechanism, as built, does not earn its keep on a real deck — and the real
scorer is *worse* than hand-picked generic maxims. Critically, a per-creator **philosophy-profile
redesign would inherit the same corpus + relevance weaknesses** (whole-channel generic content +
tag-overlap scoring + no diversity) unless retrieval is fixed first.

## Recommendation

**Do NOT green-light Content KB v2 / philosophy-profile on current evidence.** Options, in order:
1. **Fix retrieval first, then re-test** — add per-video diversity to `SelectTopClips`, replace
   tag-overlap scoring with topical relevance that filters commander-specific noise, and re-run this
   gold A/B. Only build the profile if a fixed retriever shows clear lift.
2. **Reconsider KB scope** — per-deck targeted retrieval or user-supplied sources instead of
   whole-channel pre-distillation.
3. **Retire the clip-injection feature** — it ships dark today; the maintenance cost may exceed value.

Independent confirmation still worthwhile: paste `baseline.txt`, `with-context.txt`, and
`with-context-real.txt` into real ChatGPT and compare blind.

---

**v1.6 re-validation gate (AFTER the Phase 34 fix):** the fixed retriever was re-judged across 5
bracket-spanning decks — see `.planning/phases/35-value-re-validation-gate/35-GATE-VERDICT.md`.
Outcome: **MARGINAL** (monopoly + injection fixed, but lift stayed cosmetic — soft dims only, 0/5
decks changed any cut/add, 3/5 carried mild noise). This frozen Run-1/Run-2 evidence is the "before".
