---
created: 2026-08-02T20:50:31.813Z
title: Bracket-derived plan presets in Cut Lab
area: general
files:
  - DeckFlow.Web/Models/CutLabRequest.cs:9-40
  - DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs:150
---

## Problem

Cut Lab intake asks for bracket and (planned) checkbox plan selection separately, but
bracket already implies strategy tendencies: bracket 5/cEDH decks skew combo/stax/fast
mana, bracket 2 precon-level decks skew battlecruiser/combat. Users repeat information
the tool could infer, and low-effort users leave the plan panel unchecked, making the
plan-driven engine effects (protection, ordering, floors, off-plan findings) inert.

Depends on the checkbox plan-selection feature (Cut Lab plan profile — designed
2026-08-02, brainstorming in progress) landing first.

## Solution

When bracket is selected, pre-check a preset of generic strategy checkboxes
(e.g. bracket 5 → combo + stax; bracket 1-2 → combat/battlecruiser). User can
uncheck freely — preset is a starting point, not a lock. Preset table lives beside
CutLabFloorDefaults bracket handling. TBD exact preset mapping per bracket.
