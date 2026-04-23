---
title: ChatGPT Deck Comparison
summary: Compare two Commander decklists side by side with a structured ChatGPT prompt.
order: 20
---

# ChatGPT Deck Comparison

The Deck Comparison page (`/Deck/ChatGptDeckComparison`) generates structured ChatGPT prompts for comparing two Commander decklists side by side. It lives under the **ChatGPT** dropdown alongside the Analysis page.

## Step 1 — Deck Setup

Paste two decklists (Moxfield / Archidekt URL or plain-text export) and select a Commander Bracket for each deck. Optionally name each deck — the service falls back to the commander name if left blank.

## Step 2 — Generate Comparison Packet

The service builds a comparison context document with bracket definitions, role counts (ramp, draw, interaction, wipes, recursion, closing power), mana curves, color identity, category overlap, and combo gaps. It generates a structured comparison prompt with sections for task, rules, comparison axes, output format, deck sections, and comparison context. The prompt instructs ChatGPT to produce both a human-readable comparison and a fenced `json` block matching a `deck_comparison` schema. A follow-up prompt is also generated for iterative refinement.

Comparison axes include: commander role and game plan, speed and setup tempo, ramp, draw, spot interaction, sweepers, recursion, closing power (including combos), resilience, consistency, mana stability, commander dependence, table fit, major overlap/differences, and five concrete cards or packages that best explain the gap.

## Step 3 — Review Results

Paste ChatGPT's JSON response back into the form. The page parses the `deck_comparison` JSON and renders a formatted view with:

- Game plans and bracket labels for each deck
- Strengths and weaknesses per deck
- Key combos per deck
- Verdict panel: speed, resilience, interaction, mana consistency, closing power, and combo comparisons
- Shared themes and major differences
- Key gap cards or packages
- Recommended-for notes per deck
- Confidence notes (when ChatGPT flags uncertainty)

If you continue asking follow-up questions in the same ChatGPT thread, use the follow-up prompt saved alongside the initial comparison to have ChatGPT revise the readable comparison and regenerate the full `deck_comparison` JSON block.

## Artifact saving

Check **Save artifacts to disk** to write generated files to:

```
Documents\DeckFlow\ChatGPT Deck Comparison\<timestamp>\
```
