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

## See the pool's structure

Once the pool is loaded and protected, Cut Lab reads the deck's functional composition. It surfaces structural findings such as role and slot competition, plus weak or unmet role floors that would make later cuts risky.

You can also set configurable minimums per role. Those floors become hard guardrails for the trimming process, so the deck does not accidentally cut below the baseline you want to preserve for lands, ramp, interaction, or any other tracked role.

## Guided cut rounds

Cut Lab then walks you through iterative cut rounds. As you make decisions, the simulation and metrics engine recalculates the deck's consistency numbers so the workspace stays current instead of showing stale advice from the opening snapshot.

The important part is the stance: the tool does not tell you that a card is bad. It shows the measurable tradeoff of removing that card. You see what each proposed cut changes in the deck's numbers, which makes Cut Lab a decision-support workspace rather than a judgment engine.

## Goals and what-if swaps

If you care about specific timing goals, pin them directly in the workspace. For example, you can track whether a payoff is online by a target turn and see those goal results recalculated by the same engine after each change.

You can also save named scenarios locally in your browser, which makes it easy to compare different cut paths without losing your current line. For one-off experiments, run a what-if swap to replace card A with card B and immediately see every tracked goal and consistency metric recomputed before you decide whether to keep or discard the change.

## Tune to exactly 100

When you are close to the finish line, the decide workspace gives you direct quantity controls for basics. Inline `+/-` steppers let you raise or lower basic-land counts without leaving the page, and you can add basics from the fixed supported set to land exactly on 100 cards.

Singleton legality is still enforced, so this tuning step helps you finish the list cleanly rather than papering over legality rules.

## Export

Once the deck is a validated 100, export the finished list plus an add / cut patch in both Moxfield and Archidekt text formats. That makes the last step practical: copy the final list or patch straight back into your builder instead of manually reconstructing the cuts.

Cut Lab is behind the `tool.cut-lab.enabled` feature flag. On small screens, the Packages, Scenarios, and What-if sections collapse into expandable panels so the workspace stays usable without dropping the shipped functionality.
