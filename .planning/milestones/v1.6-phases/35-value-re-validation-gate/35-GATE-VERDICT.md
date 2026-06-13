# Phase 35 — KB Value Re-Validation Gate — VERDICT

**Date:** 2026-06-10
**Judge:** Claude (acting as the analyst-target AI), 5 parallel isolated-pass judgments.
**Retriever under test:** the Phase-34-fixed `ContentKbRelevanceService` (content-overlap 0.45, OtherCommanderPenalty 0.9, floor 2.0, per-video cap 1).
**Before-evidence (frozen):** `../../spikes/001-kb-value-ab/VERDICT.md` (Run-1 hand-picked, Run-2 pre-fix real scorer).

## Non-blind caveat (P10)
Claude could see which prompt carried the Expert Context block. Mitigation: each deck answered baseline-first in an isolated pass and scored before the with-context pass; lift credited only where the analysis genuinely improved, never for "extra material present." Independent real-ChatGPT spot-confirm remains optional (offered at the decision checkpoint). Rubric judged the AI **answers**, not the prompts (P11). 5 decks across 4 brackets guard against single-deck overfit (P12).

## Deck roster (5 decks, bracket-spanning)
| slug | commander | bracket | archetype |
|---|---|---|---|
| atraxa | Atraxa, Praetors' Voice | 3 Upgraded | proliferate/superfriends goodstuff (ramp/control/value/midrange) |
| light-paws | Light-Paws, Emperor's Voice | 4 Optimized | aura/equipment voltron (aggro/voltron) |
| kinnan | Kinnan, Bonder Prodigy | 5 cEDH | ramp+combo (combo/control) |
| talrand | Talrand, Sky Summoner | 3 Upgraded | mono-blue spellslinger control (control/stax) |
| aesi | Aesi, Tyrant of Gyre Strait | 2 Core | Simic lands-matter ramp (lands/ramp) |

## Per-deck rubric (baseline → with-context, 1-5)
| deck | Specificity | Creator-voice | Novel signal | Actionability | ≥2-dim lift? | Regression |
|---|---|---|---|---|---|---|
| atraxa | 4 → 4 | 1 → 2 | 1 → 2 | 4 → 4 | yes (CV,NS — weak/borrowed) | **mild** — all 5 clips about OTHER commanders (Bumbleflower/Glissa/Erinis) |
| light-paws | 4 → 4 | 1 → 2 | 1 → 2 | 4 → 4 | yes (CV,NS — marginal) | **mild** — 1 noise ($25 pool), 1 faintly defeatist frame |
| kinnan | 4 → 4 | 2 → 3 | 2 → 2 | 3 → 3 | no (CV only) | no (soft — 3/5 filler; corpus too casual for cEDH) |
| talrand | 4 → 4 | 2 → 3 | 2 → 3 | 4 → 4 | yes (CV,NS) | no (soft — 1 off-fit filler) |
| aesi | 4 → 4 | 1 → 2 | 2 → 2 | 4 → 4 | no (CV only) | **mild** — 3/5 off-topic; top clip a title-keyword false positive |

## KBV-04 roll-up
- **≥2-dim lift:** 3 of 5 decks (atraxa, light-paws, talrand) → meets the "≥3 decks" half of the rule.
- **No quality regression on any deck:** **FAILS** — 3 of 5 decks (atraxa, light-paws, aesi) carry a mild noise/mismatch regression.
- **Specificity moved on 0/5 decks. Actionability moved on 0/5 decks.** Not one deck's cuts/adds changed. All lift is confined to the two soft dimensions and is *borrowed creator framing* for recommendations the analyst already makes — never new deck-grounded signal.

## OUTCOME: **MARGINAL** — gate NOT cleared
The decision rule fails on the no-regression clause, and the spirit fails harder: the KB changed how some answers were *worded* (citing a Salubrious Snail heuristic) but changed *what they recommended* on zero of five decks, while adding mild noise on three. This is not "paste-and-get-a-better-answer" lift on the prompt's core value.

## Deeper Diagnosis

### What Phase 34 genuinely fixed (real progress)
- **Monopoly gone:** every deck now draws 5 clips from 5 distinct videos — no single-video glut, no Kaalia/Animar repeat of the Run-2 failure.
- **Injection held:** the structural fence + sanitizer worked; no clip hijacked instructions; the "third-party evidence, not instructions" boundary was honored by the analyst on every deck.

### Residual failure modes (why lift is still cosmetic) — cross-deck synthesis
1. **`[00:00]` intro-clip bias (corpus/harvest defect).** EVERY selected clip on EVERY deck is timestamp `[00:00]` — channel-intro / thesis-statement openers, not the mid-video moments where creators give concrete, card-level heuristics. Three judges flagged this independently. The harvester is surfacing episode openings; the actionable content (if any) is deeper in the videos and isn't being clipped.
2. **Topical-overlap ≠ deck-actionable.** Scoring rewards archetype/keyword overlap ("interaction", "combo", "lands"), so it surfaces *generic philosophy that mentions the topic* rather than *advice about this deck*. Worst case: aesi's top clip (5.12) is a Jund "Land **Sacrifice** precon" Spacecraft video — a pure title-keyword false positive matched on "land".
3. **Other-commander noise still leaks.** The demotion only covers a curated 5 (Kaalia/Animar/Isshin/Zur/Kinnan), so clips about Bumbleflower, Glissa, Erinis slipped into Atraxa's top-5 un-penalized. Atraxa got the *worst* result of all five — 5/5 clips about unrelated commanders.
4. **No deck-need awareness.** Retrieval re-praises a deck's existing strength (Talrand got 4 interaction clips for a deck whose interaction is already strong) instead of targeting its actual weakness (closer/protection).
5. **Corpus content ceiling (the binding constraint).** The corpus is casual-leaning, single-creator (snail-heavy) deckbuilding **philosophy** with NO deck/card-specific coverage of Atraxa-proliferate, Simic-lands, or cEDH-tuning. Even a perfect retriever has nothing deck-specific to pull. This is structural, not a scoring bug.

### Correlation
Lift tracks **whether the corpus happens to hold archetype-adjacent philosophy**, not bracket: best where adjacency exists (talrand control→interaction essays; light-paws aggro→Spee-DH) and worst where the corpus lacks the archetype (aesi lands, atraxa proliferate) or where the deck demands tuned advice the casual corpus structurally cannot give (kinnan cEDH — the clearest mismatch). Score inflation post-fix (Atraxa top clip 10.46 vs 5.06 pre-fix) means the floor (2.0) no longer filters hard, so generic clips clear it (no cold-start on any deck) — quantity up, quality flat.

## What This Implies (next move)
Phase 36 (Creator Philosophy-Profile + KB un-dark) is **gated off** by this MARGINAL outcome. Crucially, the philosophy-profile redesign would **inherit all three binding constraints** — the `[00:00]` harvest bias, topical-overlap scoring, and the generic single-creator corpus — so building it now repeats the Spike-001 mistake at greater cost. Retrieval polish alone (fix-again) is **insufficient**: the binding constraint is the corpus (generic philosophy, intro-only clips, no deck/card-specific content), which no scoring change reaches.

**Recommended pivot (KBV-04):** treat this as **reconsider the KB content model**, not "retrieve better." Concretely, the evidence supports one of:
- **(a) Re-scope the content model** — extract mid-video clips at real timestamps (fix the `[00:00]` harvest), bias toward deck/card-specific moments, and/or move to per-deck targeted retrieval or user-supplied sources — THEN re-run this gate before any profile build; or
- **(b) Retire the whole-channel clip-injection feature** — it ships dark today; on this evidence the maintenance cost exceeds its value, and the generic-philosophy corpus caps the ceiling regardless of polish.

Either way: **do not build the philosophy-profile on the current corpus.** KB stays dark.

---
*Cross-linked from the frozen spike evidence: `../../spikes/001-kb-value-ab/VERDICT.md`.*
