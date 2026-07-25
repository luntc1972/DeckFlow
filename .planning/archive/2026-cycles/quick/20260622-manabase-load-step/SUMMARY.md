---
slug: manabase-load-step
status: complete
completed: 2026-07-05
---

# Summary: Manabase "Load deck" step before analysis

**Outcome:** Shipped. The mana-base page gained a **Load deck** step that resolves
the deck and surfaces the auto-detected reduced/alternative-cost overrides for
review *before* running the expensive Monte-Carlo analysis.

**Shipping commits:**
- `d851601b feat(manabase): add 'Load deck' step to review detected costs before analysis` — primary implementation (service `LoadAsync` resolve+classify without sim, `/manabase/load` action, `Loaded` view-model flag, view + busy-text wiring).
- `11a31e1a feat(manabase): Scryfall payload -> CardFact adapter` — supporting adapter used by the load path.

Closure record added 2026-07-05 during the Cycle 15 pre-close audit; the task
shipped in Cycle 12 manabase work. Verified via `git log`.
