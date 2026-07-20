---
title: Cut Lab
summary: Bring an oversized 101-150 card Commander pool into a workspace, declare build intent, and lock cards, packages, and roles that must never be cut.
order: 101
requires_flag: tool.cut-lab.enabled
---

# Cut Lab

The Cut Lab page (`/cut-lab`) is where you take an oversized Commander pool and turn it into a structured trimming workspace. Start from a public URL or paste a list directly, then load the commander plus a 101-150 card non-commander pool that still needs cuts.

## Intake for oversized pools

Cut Lab is meant for the stage before the list is legal. You bring in the full candidate pool, confirm the commander, and let DeckFlow normalize that input into a workspace built for reduction rather than final deck review.

A typical setup is:

- Load a public list URL or paste the pool text directly.
- Include the commander and an oversized 101-150 card non-commander pool.
- Optionally include the deck's sideboard, considering list, or both when the real candidate pool lives outside the mainboard, such as Commander options or extra cuts that turn an exact-100 list into a trim-ready 101-150 pool.
- Review the per-board breakdown after import: Main, Sideboard, and Considering/Maybe counts are shown so you can see where the pool size is coming from.
- If the selected boards push the pool above 150 non-commander cards, Cut Lab tells you the Main, Sideboard, and Considering/Maybe counts in the error so you know what to deselect.
- Review the parsed workspace before deciding what must stay.

## Declare build intent

The page also captures what the deck is trying to do before any cut logic starts. You can set the primary and secondary plan, note the intended bracket, and describe the target play experience so future cut recommendations are evaluated against the right goal.

## Protect what cannot move

Some cards or groups should never be offered up as cuts. The commander is always locked. Beyond that, you can lock individual cards, protect named packages that should survive together, or keep the lands role group out of the cut pool when the mana base is already spoken for.

## Staged rollout

Cut Lab is rolling out in phases. Today, the page focuses on oversized-pool intake, intent capture, and protection rules. Automated cut recommendations arrive in later phases, so this first release is about building a reliable workspace that preserves the parts of the deck you already know are non-negotiable.
