---
title: Commander Bracket Checker
summary: Classify a Commander deck into its official 1–5 bracket using Game Changers, two-card combos, and mass land denial — computed locally, no AI needed for the bracket number.
order: 36
requires_flag: tool.bracket.enabled
---

# Commander Bracket Checker

The Commander Bracket Checker page (`/bracket`) classifies a Commander deck into the official WotC 1–5 bracket system using three measurable signals: Game Changers, two-card win combos, and mass land denial. The classification is **computed directly in DeckFlow** — no AI round-trip is needed for the bracket number itself.

## Step 1 — Load a Deck

Pick one of two inputs:

- **Public URL** — paste an Archidekt or Moxfield deck link.
- **Paste decklist** — paste a list, one card per line (e.g. `1 Sol Ring`).

Add an optional **Deck name** to label the report.

## Step 2 — Pick a Target Bracket (Optional)

Leave the target unset to get a pure classification. Pick a target (B1–B5) to also receive:

- The specific cards that push the deck over the target (**floor violations**).
- **Starter cuts** — a list of which over-bracket cards to remove and why.
- A **copy-ready AI prompt** that asks the AI to replace each over-bracket card with a power-equivalent legal swap and re-confirm the bracket.

## How Brackets Are Determined

The official Commander bracket system (WotC Brackets Beta, effective October 2025) classifies decks by what they *do*, not by raw card power:

| Signal | Effect |
|--------|--------|
| **Game Changers** | 0 = B2; 1–3 = B3; 4+ = B4; 10+ (product heuristic) = B5 territory |
| **Two-card win combo** | Hard-floors the deck at B4 regardless of Game Changer count |
| **Mass land denial** | Hard-floors the deck at B4 (e.g. Armageddon, Ravages of War) |
| **Extra turns** | Informational — does not change the bracket number per the current rubric |

Tutors are **not** counted — removed from the official rubric in October 2025.

Bracket 1 (Exhibition) is self-declared; zero signals defaults to Bracket 2 (Core).

## Combo Detection

Two-card win combo detection queries the Commander Spellbook database. If the service is temporarily unavailable, DeckFlow notes this on the result page and the copied prompt asks the AI to double-check for combos — the classification degrades gracefully rather than asserting "no combos."

## Game Changers List Freshness

The Game Changers list used for classification is dated and shown on the result page. The copied prompt asks the AI to re-confirm current Game Changers membership before suggesting swaps, so a slightly stale list degrades gracefully rather than misclassifying silently.

## AI Platform

If you request a balancer prompt (by selecting a target bracket), choose your AI platform — the prompt is pre-formatted for **ChatGPT** or **Claude**. Paste it directly into a new conversation to get fair, power-equivalent card swaps and a re-confirmed bracket.
