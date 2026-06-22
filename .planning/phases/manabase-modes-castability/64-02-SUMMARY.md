# 64-02 Summary — Manabase Web: mode selector, castability table, formula panels

**Status:** SHIPPED (reconstructed on reconcile 2026-06-22) · **Date executed:** 2026-06-21

> Reconstructed during a main-branch planning reconcile (2026-06-22). Implemented and
> committed directly to `main`, then deployed to prod; SUMMARY never written. Closed
> from git history + plan must-haves. Not a live execution log.

## What shipped (requirements MODE-03, CAST-03, COMMANDER-02, FORMULA-01)

`DeckFlow.Web` (`ManabaseRequest/ViewModel/Controller`, `Manabase.cshtml`, `site-common.css`):

- **Casual/cEDH selector** bound to `Request.Mode`, Casual default, re-renders selected on postback;
  selecting cEDH drops the land target into the ~28–32 band and the page states the mode used.
- **`ManabaseAnalysisOptions { Mode, CommanderImportance }`** options object on
  `AnalyzeAsync(...)` (defaulted) so existing call sites compile — stops param-list telescoping.
- **Castability table** (casual-only via `ShowCastability`), columns `Card | MV | Cast on curve |
  Limiting`, sorted worst-cast% first, only real spells (no Sol Ring/Birds/lands as rows), with a
  per-row limiting factor and the "estimate, on the play, on curve" caveat.
- **Formula panels (FORMULA-01)** — two native `<details>`: "How the analysis works" (static
  methodology, always rendered) and "This deck's numbers" (per-deck terms, only with a result).
- **Responsive** — castability table wrapper in `site-common.css` (not `site.css`); no horizontal
  overflow on mobile/desktop across themes (e2e scrollWidth ≈ clientWidth assertion).

## Subsequent UX additions (same surface, later commits)

- **"Load deck" review step** before analysis (`d851601b`) + Start-over button / analyze waiting
  indicator (`d1ae8e69`); mobile hero blurb trimmed above the fold (`05164ba7`).
- **Alt/reduced cost overrides** box + applied-cost marker — tracked in `phases/manabase-alt-cost/`.

## Tests

- xUnit: `ManabaseAnalysisServiceTests` (cEDH lower target), `ManabaseControllerModeTests`
  (mode flows through; cEDH report present but castability hidden), friendly limiting-factor mapping.
- Playwright `e2e/manabase-castability.spec.ts`: casual table present + sorted + no rock/land rows;
  cEDH toggle drops target + hides table; no horizontal scroll at desktop + mobile. Green across
  viewports/themes at execution time.

## Commits (on `main`)

`59798e20` (modes + castability + formula panels) · `da85f257`, `811cffb3`, `32ae9659` (cost
overrides web plumbing/UI) · `d851601b` (Load-deck step) · `d1ae8e69`, `05164ba7` (start-over /
hero trim) · `8950c511`, `fef95bff` (e2e theme/overflow guards + explicit Analyze click).

## Notes / deviations

- ⚠ Implemented directly on `main`, not a milestone branch (recorded for honesty; already prod-deployed).
- README + in-app help updated for modes / castability / cost overrides (`1d8f2ac7`, `fca3d0ea`, `898fece7`).
- Feature is flag-gated (`feature.manabase.enabled`); tab visibility fix shipped separately.
