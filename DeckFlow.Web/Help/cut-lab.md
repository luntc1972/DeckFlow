---
title: Cut Lab
summary: Take an oversized 101-150 card Commander pool from intake through measured cut decisions, exact-100 tuning, and builder-compatible export.
order: 101
requires_flag: tool.cut-lab.enabled
---

# Cut Lab

The Cut Lab page (`/cut-lab`) is where you take an oversized Commander pool and work it down to a validated 100-card list. The workflow is deterministic and decision-support driven: Cut Lab measures what each cut changes in the deck's numbers, rather than making a judgmental claim that a card is "objectively worse." AI is optional here at most, as an explanation layer, not as a requirement to use the tool.

## Intake

Start from a public URL or paste the pool directly. Cut Lab expects a commander plus an oversized 101-150 card non-commander pool that still needs trimming.

You can independently choose whether to fold in the sideboard, the considering / maybeboard, or both when those boards are where the real candidate cards live. After import, the page shows a Main / Sideboard / Considering breakdown so you can see exactly where the pool size came from.

If the selected boards push the pool out of range, Cut Lab does not fail vaguely. It tells you that the pool is too large and names the per-board counts in the error so you know what to trim or deselect before moving on.

## Declare build intent

Before you start cutting, declare what the deck is trying to do. You can capture the primary plan, secondary plan, intended bracket, and target play experience.

That context matters because Cut Lab measures tradeoffs against the deck's actual goal. A cut that is acceptable in a slower value shell may be wrong for a deck trying to hit a specific payoff window, so the workflow starts by making that intent explicit.

## Protect what cannot move

Some parts of the pool are non-negotiable. The commander is always locked. Beyond that, you can lock individual cards, protect named packages that must survive together, and lock whole role groups out of the cut pool when those cards are already settled.

Use this step to remove "not up for debate" cards from the trimming conversation before the cut engine starts surfacing options.

If a pool card is part of a known Commander Spellbook combo, Cut Lab also marks it with a **Combo piece** badge and an inline disclosure describing which combo it belongs to and what that combo does. This is advisory decision-support, not an automatic lock and not a hidden engine rule: the card can still be considered for cuts, but the workspace makes the combo context explicit so you do not cut one half unknowingly.

## See the pool's structure

Once the pool is loaded and protected, Cut Lab reads the deck's functional composition. It surfaces structural findings such as role and slot competition, plus weak or unmet role floors that would make later cuts risky.

The pool view is also explorable in two lighter-weight ways: a **By type** section groups every card into one primary card-type bucket, and a **By subtype** search lets you ask "how many Allies / Lessons / Legendary cards do I have?" from the rendered pool itself.

You can also set configurable minimums per role. Those floors become hard guardrails for the trimming process, so the deck does not accidentally cut below the baseline you want to preserve for lands, ramp, targeted removal, mass removal, or any other tracked role.

Role counts are deliberately generous, since they are a decision aid rather than a verdict: ramp includes mana-symbol producers (rocks and dorks, not just "add one mana" text), draw counts you-anchored card draw of any size plus clue/connive card-advantage, and the engines role is limited to repeatable permanents so a one-shot "draw two" spell is not mistaken for an engine. These Cut Lab role tallies are intentionally separate from the Commander Mana Base Analyzer tool's mana-source math, which is tuned for castability rather than counting.

## Inspect any card

Anywhere a card appears in the workspace — a role pill, a structural findings chip, a pool-row name, or a cut proposal — you can click it to open a card popup. The popup shows the card's oracle text so you can check what it actually does without leaving Cut Lab, and it carries **Lock / Unlock** and **Close** so you can protect or release the card on the spot. Card text is served from the page itself, so opening a card is instant and needs no extra lookup.

## Guided cut rounds

Cut Lab then walks you through iterative cut rounds. As you make decisions, the simulation and metrics engine recalculates the deck's consistency numbers so the workspace stays current instead of showing stale advice from the opening snapshot.

The important part is the stance: the tool does not tell you that a card is bad. It shows the measurable tradeoff of removing that card. You see what each proposed cut changes in the deck's numbers, which makes Cut Lab a decision-support workspace rather than a judgment engine.

## How it works

The loop is straightforward once the pool is loaded:

1. Lock the cards that must stay. Ticking a card protects it from future cut proposals, and the commander is always locked automatically.
2. Cut Lab chooses the proposed cut. You do not pick the next card yourself; the engine surfaces one candidate at a time based on the current protected pool, findings, floors, and goals.
3. Work through the proposal queue with **Accept**, **Reject**, or **Defer**. Accept removes that card and moves to the next proposal, Reject keeps it, and Defer pushes it back to revisit later.
4. When you are close to 100, use the quantity tuner to adjust basics and other legal multiple-copy cards so the final list lands exactly where you want it.

Structural findings also use two combo-related labels that mean different things. **Combo-protected** means all pieces for that combo line are present, so the workspace is warning you that the card is part of a currently live line. **Enabler-starved** means that specific line is missing a partner, but the card may still be fully live in another combo or still show up elsewhere as combo-protected.

## Goals and what-if swaps

If you care about specific timing goals, pin them directly in the workspace. Goal probabilities and the **Compare to baseline** table are point-in-time snapshots: use **Recalculate goals** and **Recalculate analysis** to refresh them after cuts, while cut counts and structural findings update live per cut.

You can also save named scenarios locally in your browser, which makes it easy to compare different cut paths without losing your current line. For one-off experiments, run a what-if swap to replace card A with card B and immediately see every tracked goal and consistency metric recomputed before you decide whether to keep or discard the change.

## Save and move sessions

Named scenarios keep alternative cut paths side by side, but they live only in the browser you saved them in. When you want to continue on another device — or keep a durable copy of where a build stands — download the current session as a `.json` file. Loading that file elsewhere restores the same run through the same scenario-restore path. Sessions stay on your machine; nothing is stored server-side.

## Tune to exactly 100

When you are close to the finish line, the decide workspace gives you direct quantity controls for basics. Inline `+/-` steppers let you raise or lower basic-land counts without leaving the page, and you can add basics from the fixed supported set to land exactly on 100 cards.

Singleton legality is still enforced, so this tuning step helps you finish the list cleanly rather than papering over legality rules.

## Export

Once the deck is a validated 100, export the finished list plus an add / cut patch in both Moxfield and Archidekt text formats. That makes the last step practical: copy the final list or patch straight back into your builder instead of manually reconstructing the cuts.

Cut Lab is behind the `tool.cut-lab.enabled` feature flag. On small screens, the Packages, Scenarios, and What-if sections collapse into expandable panels so the workspace stays usable without dropping the shipped functionality. The shipped workspace is also verified readable and keyboard-accessible across all supported guild themes.
