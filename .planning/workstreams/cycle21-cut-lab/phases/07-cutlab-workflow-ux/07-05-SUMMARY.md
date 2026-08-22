# Plan 07-05 Summary

Written retroactively on 2026-08-22. The plan executed on 2026-08-04; the session that ran it
paused before writing a summary, and the phase was then picked up on a different branch.

## Built

- **Tasks 1-4 — the pinned decide loop.** The proposed card, its one-line glance summary and the
  Accept / Reject / Defer controls were lifted into `.cutlab-proposal--pinned`, a `position: sticky`
  region that stays in the viewport while the evidence below it scrolls. The decide POST contract
  was not touched: the same three forms with the same hidden fields, relocated. Accept is routed
  through `CutLabDecideApiResponse` so the card is patched in place rather than re-rendered by a
  navigation. (`a3c0408a`)
- **D-1 was resolved as Option 3** (wizard + pinned proposal, ~1,107px desktop) on 2026-08-03, so
  07-03's panel-hiding stayed in force and this plan executed unconditionally.

## The offset is measured at runtime, not hardcoded

The plan said to reuse `--cutlab-anchor-nav-stuck-height`. That variable is declared only on
`nav.cutlab-anchor-nav`, and custom properties inherit *down* the tree — `.cutlab-proposal` is a
sibling subtree, so the variable was unreachable there and would silently have resolved to its
fallback: a magic number wearing a token's name. `.cutlab-sticky-bar:5135` still carries that latent
bug.

Instead the offset is published as `--cutlab-pinned-offset` on `document.documentElement`, measured
at runtime, and `--cutlab-anchor-nav-stuck-height` was promoted to `:root`. Two sticky layers sit
above the proposal and the mobile one has variable height (`flex-wrap`, `max-height: none`), so no
static offset is correct.

## One fix was landed, reverted, and re-landed incompletely

`d8dfa26a` bundled one disproved fix (`pointer-events` + `z-index` 19→1) with four correct ones. It
was reverted wholesale by `27da7a5d` and its four good fixes restored inside `06b377d6` — but the
CSS half was not restored with them. That gap became **F-1** of the 2026-08-16 code gate: 48 lines
of re-landed tests went green against a live defect, because `pointer-events` overlap is invisible
to jsdom and the Playwright specs assert structure rather than clicking *through* the sticky region.
F-1 was fixed on 2026-08-16 by `1fb84893`, verified faithful by blob hash against `d8dfa26a`'s
post-image.

**Carry-forward lesson:** a revert-then-re-land across a mixed commit needs a per-file diff against
the original post-image, not a reading of the commit message.

## Task 5 — UAT

The plan is `autonomous: false`; Task 5 is a human checkpoint, folded with 07-04 Task 5 by user
decision on 2026-08-04 because 07-05 rewrites the Decide panel layout and would have invalidated a
separate 07-04 pass.

It was discharged by measurement rather than by eye — see `UAT-07-04-07-05.md`. All seven criteria
pass across `chromium-desktop`, `chromium-mobile` and `webkit-mobile`, each run isolated (one spec,
one project, `--workers=1`, admin lock cleared). Paint order measured at
`{ pinnedZ: 19, barZ: 20, position: sticky, stuck: true, offsetPx: 44 }`, so page content scrolls
under the pinned card and the card sits below the sticky bar.

**A human eyeball pass over the same criteria has not been run.** The automated record is evidence,
not a substitute, and the phase should not be declared complete on it alone without the developer
saying so.

## Also fixed in this plan's window

`06b377d6` synchronized scenario restore navigation. The scenarios failure it addresses was **not**
a regression from `a3c0408a` despite an initial A/B saying so: it is a pre-existing race where
scenario Load calls `form.requestSubmit()`, `expandCutLabSection` reads `open=null` on the outgoing
page, and the queued summary click lands on the rebuilt page after `restoreSectionCollapseState` has
already set `open` — toggling it closed. `a3c0408a` only shifted the timing so the race lost more
often. **A single run per side is not an A/B on a flaky test.**

Three mechanisms were disproved and must not be re-attempted: click interception by the pinned
header (elementFromPoint returns the summary itself), a timing window a wait would paper over
(`open` stayed false stably from t+400ms to t+2400ms), and the sticky positioning (forcing
`position: static` reproduces it identically).
