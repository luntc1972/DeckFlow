---
title: Deck Comparison
summary: Compare two Commander decklists side by side with a structured AI prompt.
order: 20
---

# Deck Comparison

The Deck Comparison page (`/deck-comparison`) generates structured AI prompts for comparing two Commander decklists side by side. It lives alongside the Deck Analysis page in the Deck Tools tabs.

## Step 1 — Deck Setup

Paste two decklists (Moxfield / Archidekt URL or plain-text export) and select a Commander Bracket for each deck. Optionally name each deck — the service falls back to the commander name if left blank.

## Step 2 — Generate Comparison Packet

The service builds a comparison context document with bracket definitions, role counts (ramp, draw, interaction, wipes, recursion, closing power), mana curves, color identity, category overlap, and combo gaps. It generates a structured comparison prompt with sections for task, rules, comparison axes, output format, deck sections, and comparison context. The prompt instructs your AI to produce both a human-readable comparison and a fenced `json` block matching a `deck_comparison` schema. A follow-up prompt is also generated for iterative refinement.

Comparison axes include: commander role and game plan, speed and setup tempo, ramp, draw, spot interaction, sweepers, recursion, closing power (including combos), resilience, consistency, mana stability, commander dependence, table fit, major overlap/differences, and five concrete cards or packages that best explain the gap.

## Step 3 — Review Results

Paste your AI's JSON response back into the form. The page parses the `deck_comparison` JSON and renders a formatted view with:

- Game plans and bracket labels for each deck
- Strengths and weaknesses per deck
- Key combos per deck
- Verdict panel: speed, resilience, interaction, mana consistency, closing power, and combo comparisons
- Shared themes and major differences
- Key gap cards or packages
- Recommended-for notes per deck
- Confidence notes (when your AI flags uncertainty)

If you continue asking follow-up questions in the same AI conversation, use the follow-up prompt saved alongside the initial comparison to have your AI revise the readable comparison and regenerate the full `deck_comparison` JSON block.

## Artifact saving

Use **Download comparison session (.zip)** in the sticky bar at the top of the page (always available, regardless of step) or in the Step 3 results panel to save the current artifacts locally.

The zip can contain: `00-comparison-input-summary.txt`, `10-deck-a-list.txt`, `11-deck-b-list.txt`, `12-deck-a-combos.txt`, `13-deck-b-combos.txt`, `20-comparison-context.txt`, `30-comparison-prompt.txt`, `31-comparison-schema.json`, `32-comparison-follow-up-prompt.txt`, and `40-deck-comparison-response.json`.

Use **Resume from a saved session (.zip)** at the top of the page to upload the same zip later. Re-import only reads `40-deck-comparison-response.json`; the other files remain in the archive for your records.
