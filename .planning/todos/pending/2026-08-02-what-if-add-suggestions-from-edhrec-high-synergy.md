---
created: 2026-08-02T20:50:31.813Z
title: What-if add suggestions from EDHREC high-synergy
area: general
files:
  - DeckFlow.Web/Services/CutLab/CutLabWhatifService.cs
  - DeckFlow.Core/Integration/EdhrecCardLookup.cs
---

## Problem

Cut Lab's what-if swap flow only works with cards the user already named — it cannot
propose ADD candidates. Users cutting a deck often want "what should replace this?"
answered deterministically without leaving the tool. EDHREC per-commander
high-synergy/top-cards lists provide a ranked candidate pool that Cut Lab never taps.

Depends on the EDHREC commander-page fetcher planned for the checkbox plan-selection
feature (same JSON payload carries theme card lists and high-synergy lists — one
fetch, cached per commander, serves both features).

## Solution

After a cut is accepted, offer top-N EDHREC high-synergy cards not already in the
deck (filtered by color identity, banlist, checked plan themes) as what-if swap
candidates. Reuse existing what-if metric-delta pipeline to show impact before user
commits. TBD: N, ranking blend (synergy score vs plan-theme match).
