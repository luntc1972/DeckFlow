---
title: Spike — combo data richness for primer pilot lines
date: 2026-05-29
priority: medium
source: /gsd-explore session 2026-05-29
target_milestone: v1.5
blocks: deck-primer-generator
---

# Spike: combo data → primer grounding

## Goal

De-risk the hardest primer section before committing to a full plan. Confirm
`CommanderSpellbookService.FindCombosAsync` output is rich enough to ground **readable
step-by-step pilot lines**, and decide how to fence the "extend cautiously" speculative
combos so AI-invented interactions stay visibly separated from ground truth.

## Tasks

1. Run `FindCombosAsync` against 2–3 real decks (incl. the Jeskai example from the explore
   note) and dump prerequisites + steps + produces.
2. Assess: does the step text read as a usable loop, or just IDs/card names needing
   narration? Identify any missing fields the prompt would need.
3. Draft a prompt fragment that (a) injects known combos as ground truth and (b) asks AI to
   add speculative synergies under a clearly-labeled "speculative — verify" heading.
4. Eyeball one ChatGPT round-trip to confirm grounded lines stay accurate and speculative
   ones are visibly fenced.

## Done when

- Decision recorded: combo data is sufficient as-is / needs enrichment / needs fallback.
- Reusable prompt fragment captured for the Deck Primer Generator plan.

## Related

- [[deck-primer-prompt-design]]
- [[deck-primer-generator]]
