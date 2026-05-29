---
title: Deck Primer Generator workflow
trigger_condition: v1.5 planning / requirements review
planted_date: 2026-05-29
source: /gsd-explore session 2026-05-29
target_milestone: v1.5
---

# Seed: Deck Primer Generator workflow

## Idea

New paste-ready workflow + tab: user supplies a decklist (and bracket), DeckFlow emits a
ChatGPT-ready prompt that produces a complete **Moxfield deck primer** in one round-trip.
Peer of DeckAnalysis / DeckComparison / CedhMetaGap.

## Why

Writing a primer by hand is slow; combo lines and matchup sections hurt most. DeckFlow
already holds the data needed to ground both (Spellbook combos, category/tagger data,
EdhTop16 meta). Directly serves the core value: paste → useful answer, no reformatting.

## Shape (v1)

- New `Views/Deck/DeckPrimer.cshtml` + `DeckController` action + `DeckPrimerPacketService`
  following the existing packet/artifact-store pattern.
- Inputs: decklist (existing loader), **bracket selector** (routes matchup source AND
  preset defaults), **per-section selection** (see below).
- **31-section catalog** (union of 4 real primers) — full list + grounding map in the note.
- **Section selection = B+C hybrid**: bracket preset pre-checks a sane set, rendered as 5
  collapsible groups (Identity / Combos / Gameplay / Matchups / Maintenance) with per-section
  on/off. v1 presets: **cEDH** + **Casual/Upgraded** (Minimal later).
- Prompt injects only the selected sections: combos (ground truth) + speculative-combo ask
  (fenced), category-derived engine/interaction/mulligan context, bracket-routed archetype
  list.
- Output: prompt artifact stored via PacketArtifactStore, paste-ready.

## Decisions already made (see note)

- Combos: inject known (Spellbook) + ask AI to extend cautiously, speculative flagged.
- Matchups: bracket 5 → EdhTop16 named archetypes; brackets 1–4 → 5 generic strategy
  buckets. No EDHREC integration in v1.
- cEDH-only sections (#24 Must-Counter, #25 Counter Cheat Sheet) vs casual-only (#26 Meta
  Positioning) gate on bracket — encoded in the presets.

## Trigger

Promote to a phase when v1.5 requirements/roadmap are being assembled. Likely sits after
Phase 21 (Content KB Distillation) clears live UAT.

## Related

- [[deck-primer-prompt-design]] — full design notes + section skeleton
