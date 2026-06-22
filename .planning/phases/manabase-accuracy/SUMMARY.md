# Summary — Manabase accuracy pass (mulligan / verdict / delay)

**Status:** SHIPPED (reconstructed on reconcile 2026-06-22) · **Date executed:** 2026-06-21

> Reconstructed during a main-branch planning reconcile. Implemented + committed to `main`,
> deployed to prod; SUMMARY never written. Closed from git history + `VALIDATION.md`.

## What shipped

A follow-on accuracy pass over the Monte-Carlo castability model (Codex APPROVE-WITH-CHANGES on
the plan, `7c9b9be4`):

- **Commander free first mulligan** modeled in the sim (`1213994a`).
- **Mulligan-aware, sim-derived source requirement** — required sources now come from the sim
  rather than a purely analytic Karsten target where the two disagree (`f5d36be0`).
- **Two-tier health verdict** — deck manabase summarized with a coarse verdict tier (`791dd15e`).
- **Average cast-delay metric per spell** — alongside on-curve cast%, an avg turns-late figure
  closer to Salubrious Snail's "average delay" presentation (`6bd44b1a`).
- Brago validation refresh + help/README copy for the verdict & delay (`898fece7`).

## Validation

See `VALIDATION.md` — Brago cross-check held (weakest color Blue, ordering preserved); the delay
metric tracks Snail's delay column more closely than the strict on-curve % alone.

## Notes

- ⚠ Done directly on `main` (milestone-branch deviation; already prod-deployed).
- Codex review-only per the active override; review nits fixed in `7c408385`, `9b98d8bf`.
