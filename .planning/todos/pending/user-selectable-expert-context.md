---
title: User-selectable Expert Context — pin a KB video or tag into the analysis prompt
date: 2026-06-07
priority: medium
source: Phase 30 UAT feedback (checkpoint Task 3, 2026-06-06)
target_milestone: v1.5
status: PROMOTED — now Phase 32 (2026-06-07); spec at .planning/specs/2026-06-07-expert-context-selection-design.md
---

# User-selectable Expert Context

## Idea

Let users manually pin a specific Content KB video (or a tag, e.g. "aristocrats") into
their deck-analysis prompt as a supplement/override to the automatic relevance selection.
Today selection is fully automatic: scoring picks up to 5 clips, the user cannot choose.

## Sketch

- UI on the deck-analysis form: optional picker (typeahead over published KB entries
  and/or tag select from ContentTagVocabulary).
- Selection override path through `DeckAnalysisPacketService`: user-pinned clips are
  added to (or replace) the scored set BEFORE the budget trim, so prompt == zip == panel
  invariant (HIGH-1) is preserved.
- Pinned clips persist in `32-expert-context.json` like scored clips (replay-safe, HIGH-2).
- Respect the K=5 / budget caps; pinned clips win ties over scored clips.

## Notes

- Phase 30 deliberately shipped zero-configuration injection; this is the manual-control
  follow-on the user asked for during UAT.
- Manual workaround today: browse /content-kb, copy clip text, paste into the AI chat.
