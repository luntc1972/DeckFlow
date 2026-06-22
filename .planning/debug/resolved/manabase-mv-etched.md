---
status: fixing
trigger: "Manabase castability shows MV 1 for 0-cost cards (kobolds, Shield Sphere, Ornithopter, Rograkh); Lotus Petal / Mox Opal skipped as unresolvable (etched *E* printings)"
created: 2026-06-21
updated: 2026-06-21
---

# Debug: manabase MV-of-0 + etched-card resolution

## Symptoms (from user screenshot)
- Castability MV column shows 1 for 0-mana cards: Crimson/Crookshank Kobolds,
  Kobolds of Kher Keep, Ornithopter, Rograkh, Shield Sphere. Should be 0.
- "Skipped 2 card(s) Scryfall could not resolve: Lotus Petal (P30M) 2 *E*,
  Mox Opal (SLD) 1072 *E*" — both are real 0-cost mana rocks.

## Root cause
- Bug A: ManabaseClassifier.AddSpellRequirement sets
  `ManaValue = Math.Max(1, round(card.ManaValue))`, so a 0-cost card displays as 1.
  The min-1 turn floor is already enforced downstream (ManabaseAnalyzer.EffectiveTurn
  floor = Max(1, totalPips); CastabilitySimulator effectiveCost = Max(1,...) and
  turn = Max(1, effectiveTurn)), so the displayed MV does not need the clamp.
- Bug B: MoxfieldParser/ArchidektParser strip `*F*` (foil) and `★` but not `*E*`
  (etched). PrintingRegex anchors the collector at end (`\)\s+(?<collector>\S+)$`),
  so a trailing ` *E*` breaks the match — the entire "(SET) collector *E*" stays in
  the name, so both the printing and the name lookups miss.

## Fix
- ManabaseClassifier.cs: `Math.Max(1, ...)` -> `Math.Max(0, ...)` for SpellRequirement.ManaValue.
- MoxfieldParser.cs + ArchidektParser.cs: strip `*E*` like `*F*` (etched is a foil
  finish -> set IsFoil), and add `*E*` to CleanName's replace chain.

## Tests
- Core: 0-MV card -> SpellRequirement.ManaValue == 0 and CardCastability.ManaValue == 0.
- Core: Moxfield + Archidekt line with `*E*` -> clean name + set + collector, IsFoil true.

## Follow-up (not in this fix)
- ResolveCardsAsync drops a card when its exact printing 404s with no name fallback.
  Add a second name-only pass for printings Scryfall returns as not_found.
