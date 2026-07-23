# MBGAP-04 Threshold Decision

## 1. Shipped formula

DeckFlow currently ships the Karsten consistency threshold as:

```csharp
public static double ConsistencyThreshold(int manaValue)
{
    int pct = 89 + Math.Max(1, manaValue);
    return Math.Clamp(pct / 100.0, 0.0, 0.99);
}
```

Source: `DeckFlow.Core/Manabase/KarstenManabase.cs` (`ConsistencyThreshold`).

That is the `(89 + max(1, MV))%` rule: 90% at mana value 1, 91% at mana value 2,
and so on up to 96% at mana value 7.

## 2. Karsten 2022 verification

### Live re-fetch attempt

I attempted to re-fetch Frank Karsten's 2022 TCGplayer article, "How many
sources do you need to consistently cast your spells? A 2022 update," from the
live site on 2026-07-12.

Result: the direct fetch available in this execution environment did not expose
the article body. The returned page was the TCGplayer JavaScript shell with the
message "We're sorry but content doesn't work properly without JavaScript
enabled. Please enable it to continue." This run therefore could not recover the
exact threshold paragraph directly from the live page body.

### What was still verified live

The live search snippet for the same TCGplayer article did corroborate the
baseline target language: it states that "roughly 90 percent consistency" is a
good number to aim for. That supports the article's general framing, but it is
not enough by itself to reconstruct the full escalated `(89 + M)%` sentence
verbatim.

### Authority used for the exact escalation

Because the live body fetch was blocked, the exact authority for the escalation
remains the existing in-repo primary-source capture in
`.planning/research/manabase-math.md`, which explicitly records:

- "AUTHORITATIVE — verbatim from Karsten's TCGplayer 2022 update (fetched via
  headless browser 2026-06-20; the two tables below are his actual published
  numbers)."
- "Threshold: Karsten targets (89 + M)% consistency, where M = the spell's mana
  value: 90% for 1-drops, 91% for 2-drops, 92% for 3, … up to 96% for 7-drops."

That capture directly resolves the contradiction with
`.planning/captures/manabase-efficacy-findings-r2.md` line 14, which had marked
the escalation as "unconfirmed" without presenting any counter-citation.

### Verdict

Verdict on the L14 doubt: **confirmed — no code change needed.**

The shipped `ConsistencyThreshold` formula matches the existing headless-browser
capture of Karsten's 2022 article, and this spike found no contradictory primary
source evidence. The efficacy note should therefore be treated as stale doubt,
not as an open mathematical question.

## 3. `(85+M)%` multiplayer-relaxation evaluation

### Proposal under review

The proposal came from `.planning/manabase-mode-research.md`, which suggested
relaxing the threshold for casual multiplayer from `(89+M)%` to roughly
`(85+M)%`, or alternatively granting a "games run long" cards-seen bonus.

### Additive or double-counted?

For DeckFlow's current simulator, lowering the threshold is not an isolated
"Commander adjustment." The simulator already gives multiplayer decks a more
generous draw model:

- `CastabilitySimulator` draws every turn, including turn 1.
- That means singleton Commander analysis already sees more cards by a given turn
  than Karsten's on-the-play 1v1 baseline.

Those two levers affect overlapping parts of the same probability problem:

- draw model = how many cards the deck sees by the target turn
- threshold = how much success probability the analyzer demands

Applying both at once would therefore risk double-counting the multiplayer
benefit: once by making the deck see more cards, and again by asking for a lower
success bar.

### Evidence standard

This spike found no primary-source Karsten publication endorsing an `(85+M)%`
Commander threshold. The proposal is DeckFlow-authored reasoning, not a verified
Karsten alternate table. Without a calibration-backed reason to stack a second
multiplayer leniency on top of the simulator's already-more-generous draw model,
the change would be speculative.

### Verdict

Verdict on `(85+M)%`: **do not implement.**

Recommendation: keep the shipped `(89+M)%` threshold, keep the simulator's
existing Commander draw model, and close MBGAP-04 as a doc-only resolution. If
future work wants to revisit this, it should be a small gated follow-up backed by
calibration data, not a threshold tweak justified only by intuition.

## 4. Docs change summary

Update `docs/manabase-analysis-rules.md` to state that the `(89+M)%` escalation
is confirmed by this decision and remove the residual "unconfirmed" doubt.
