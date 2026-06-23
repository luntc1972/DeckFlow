# Manabase Analysis — Efficacy Findings & Scoped Fixes

**Captured:** 2026-06-22
**Source:** Codex (gpt-5.4) end-to-end efficacy audit of `DeckFlow.Core/Manabase/` pipeline
(classifier → Karsten regression → castability simulator → 4-tier verdict).
**Overall grade:** **C-** — trustworthy as a rough heuristic for normal 2-color / midrange
shells; NOT sound enough to fully trust on the mana patterns that define many Commander decks.

Shipped same session (context for the audit):
- Verdict: a raw land-count shortfall (`LandDelta <= -2`) only escalates to *Needs work* when
  the sim corroborates it (color issue or broad under-support). Commit `4fcb8bdf`.
- `RampSourceCount` display counts only genuine rocks/dorks. Commit `26cb0034`.
- Per-color source composition breakdown (direct + shared + conditional), display-only. Commit `4e93cc9f`.

> Note finding #1 below partly UNDERCUTS the verdict change: the corroborating sim itself
> under-counts burst mana, so a ramp-saturated deck reads even cleaner than it should.

---

## Finding #1 — Model mana QUANTITY per source  *(High, broad — biggest accuracy win)*

### Problem
Every source is modeled as exactly **1 mana**. Sol Ring / Mana Vault = +1 (not +2),
Ancient Tomb = 1 (not 2), Gilded Lotus = 1 (not 3); Cabal Coffers / Nykthos / Cradle scaling
ignored. Output is systematically **pessimistic** on burst-mana decks — exactly the decks where
mana quantity matters most.

### Root cause
- `ManaSource` (`ManabaseModels.cs`) carries colors only, no output amount.
- Sim quantity = `onlineLandMasks.Count + onlineRamp.Count` (`CastabilitySimulator.cs:433,459`);
  color coverage assigns one source per pip (`CastabilitySimulator.cs:466+`).

### Fix (layered)
1. **Capture amount.** Add `int ManaAmount` to `ManaSource` (default 1). Source it from Scryfall
   or parse oracle `Add {C}{C}` / "Add three mana" / `{R}{R}{R}`. Touches `CardFact` /
   `ScryfallCardData` / `ScryfallCardFactMapper` / `ManabaseClassifier`.
2. **Sim accounting.** `LibraryCard` carries amount; affordability becomes `Σ amount >= effectiveCost`
   instead of `.Count`; the pip-assignment loop becomes a small greedy/flow over amounts so a
   2-of-one-color source can pay two of that pip. This is the hard part.
3. **Karsten basis decision.** Keep color-SOURCE counting Karsten-style (a 2-mana rock is still
   ONE green source) and apply amount ONLY to the quantity/curve side of the sim. Preserves the
   color math; removes the affordability pessimism. (Recommended over mana-weighting the color
   counts, which would force re-basing `SimRequiredSources`.)

### Files
`CardFact` / `ScryfallCardData` / `ScryfallCardFactMapper`, `ManabaseClassifier`,
`ManabaseModels` (`ManaSource`, `LibraryCard`), `CastabilitySimulator` (core accounting).

### Tests
Sol Ring deck casts a 2-drop turn-1; Ancient Tomb counts 2; multi-mana rock affordability;
regression re-baseline of existing cast% (expected to shift UP).

### Risk / effort
**Large blast radius** — shifts every cast% upward, re-tunes deficits and verdicts. Needs
golden-deck validation vs Salubrious Snail. **~2-3 days. High risk.** Flag-gate or baseline-diff
before shipping. **Do #3 first** (cheap, independent).

---

## Finding #2 — Ramp-credit inconsistency  *(High)*

`RampAndDrawUnderThree` (`ManabaseClassifier.cs:87,475` — matches "Add ", land-search, or
"create a Treasure") lowers the Karsten land TARGET, but rituals / Treasures / land-ramp
sorceries are NOT added to `deck.Sources` (`:109,288` add only rocks/dorks/MDFC-backs/granted).
So the deck gets land-target credit for mana the source model and sim never represent as
persistent access. Produces "land count OK / softer verdict" on Treasure/ritual decks.

**Fix direction:** either model those effects as (transient) sources, or stop crediting them in
the regression. Tie to #1 — "if a card cuts the land target it must have a modeled mana path."
**Effort:** medium. **Coupled with #1.**

---

## Finding #3 — Commander leaks into the simulated library  *(High, narrow — DO FIRST)*

### Problem
Wrong cast% for commander-as-mana decks (Selvala, Marwyn, Esika, Jodah). A mana-producing
commander gets drawn as if it were in the 99. (For Bello: only the 0.25 granted-commander source
leaks — small.) **Verified in code.**

### Root cause
`ManabaseAnalyzer.cs:55` shrinks `librarySize` by commander count, but `BuildLibrary`
(`CastabilitySimulator.cs:230`) turns ALL `deck.Sources` into drawable cards. Commander sources
(mana-dork commander via `AddPartialSources`, or commander `(granted)` via `AddGrantedSources`)
sit in `deck.Sources` with **no commander flag**, so they're drawn and real cards get truncated.

### Fix
- Add `bool IsCommander` to `ManaSource` (`ManabaseModels.cs`).
- Set it in `ManabaseClassifier` where commander sources are created (`AddPartialSources`,
  `AddGrantedSources`, lands for completeness).
- `BuildLibrary`: skip `IsCommander` sources (not in the 99).
- **Decision:** keep commander sources in `EffectiveSources`/color counts (a commander mana source
  IS reliably castable) — only the drawable-library inclusion is the bug.

### Files
`ManabaseModels.cs`, `ManabaseClassifier.cs`, `CastabilitySimulator.cs`.
### Tests
Mana-creature commander not drawable; cast% invariant to deck size; granted-commander excluded.
### Risk / effort
**Small blast radius. ~half day. Low risk.**

---

## Finding #4 — `ActualSources` vs `SimRequiredSources` not apples-to-apples  *(Med)*

`ActualSources` = analytic weighted sum, any-color counted fully per color
(`ManabaseAnalyzer.cs:685,712`). `SimRequiredSources` = mono-color isolation sim + flat `+1 per
other color` bump, Karsten-clamped (`:381,387,487,567`). They measure different objects, so the
deficit is heuristic — weakest on 3-5c shared-fixer piles. The shipped composition breakdown makes
this VISIBLE but does not fix the math.

**Decision already made (Codex-reviewed):** do NOT re-base the deficit (would silently invalidate
the verdict; requires a joint multicolor-capacity model, not a calibration). Either build that
joint model deliberately or explicitly label the per-color deficit as "heuristic guidance."
**Effort:** large if pursued. **Low priority** unless 3-5c accuracy becomes a goal.

---

## Finding #5 — Mulligan model too crude  *(Med)*

London mulligan keeps on land-COUNT bands only (`CastabilitySimulator.cs:732`), ignores color
access / commander dependence / early fixing. Biases absolute cast% and weakens the
"mulligan-aware" claim for `SimRequiredSources`. **Fix:** add color-access to the keep heuristic.
**Effort:** medium.

---

## Finding #6 — Silent coverage gaps  *(Med)*

Mostly folded into normal-looking output instead of flagged:
- Hybrid / Phyrexian / twobrid pips dropped from hard requirements (`ManaCost.cs:21`).
- Variable-cost (X) spells skipped from spell requirements (`ManabaseClassifier.cs:217`).
- Not modeled: snow, devotion, "spend mana only to…", channel lands as sinks, one-shot mana,
  Treasure stockpiling, sac-for-mana engines, cost increasers, convoke/delve/improvise/affinity,
  commander tax.

**Cheap partial fix:** surface an "unsupported interactions on N cards" note rather than silently
absorbing them. **Effort:** low for the disclosure; high to actually model.

---

## Recommended order
1. **#3** — cheap, clean, independent. Do now.
2. **#1 (+#2)** — biggest accuracy win; own planned change with golden-deck validation + flag gate.
3. **#6 disclosure** — low-cost honesty improvement.
4. **#4 / #5** — only if 3-5c accuracy / mulligan realism becomes a milestone goal.
