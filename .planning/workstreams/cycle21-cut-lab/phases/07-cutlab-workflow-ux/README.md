# Phase 7 — Cut Lab Workflow UX

**Goal:** Cut Lab's primary navigation works, the document reads in workflow
order, and the decide loop is on the first screen instead of 87% down the page.

**Not in scope (D-3):** the cut engine, the metrics, proposal ordering, any API
contract. No file under `Services/CutLab/` is edited except round-label string
constants in 07-06.

## Evidence

`07-CONTEXT.md` — measured live 2026-08-02 against `scripts/run-web-test.sh`.
Headlines: all four step tabs inert at import time; Export renders 1,544px above
Decide; 10,453px desktop / 15,896px mobile with a **17-row** pool.

## Mockups

`.planning/ui-design/cut-lab/proposed/` — three options rendered against the real
site CSS at 1280 and 390.

| Option | Desktop | Mobile | vs today |
|---|---|---|---|
| Today | 10,453px | 15,896px | — |
| 1 true wizard | 1,022px | 1,440px | −90% |
| 2 soft fix | 1,596px | 1,929px | −85% |
| 3 wizard + pinned proposal | 1,107px | 1,588px | −89% |

## Plans

| Plan | Wave | Depends | What |
|---|---|---|---|
| 07-01 | 1 | — | Regression gate spec; must FAIL on HEAD |
| 07-02 | 2 | 01 | DOM reorder to Process→Decide→Plan→Goals→Export, with required selector migration |
| 07-03 | 3 | 02 | Step-tab handler + panel visibility + ARIA keyboard |
| 07-04 | 4 | 03 | Intake summary, unified progress strip, collapse defaults |
| 07-05 | 5 | 04 | Pinned proposal — selected Option 3 |
| 07-06 | 6 | 04 | Copy, mobile tab labels, help + README |

07-02 is independently shippable on its own: it fixes the no-JS reading order
without touching a line of TypeScript.

## Decisions

- **D-1 Step model — RESOLVED 2026-08-03 as Option 3: wizard + pinned proposal
  (~1,107px desktop).** 07-03 keeps runtime panel-hiding and G-2's exactly-one-
  visible assertion; 07-05 exists and executes. The wizard has five slots, with
  `cut-lab-step-panel-3` at index 3 reserved for Phase 8's plan panel.
- **D-4 Branch — RESOLVED 2026-08-02, superseding the original recommendation.**
  This phase was adopted into Cycle 21 as **Phase 7** and runs on
  `gsd/cycle21-cut-lab` like every other phase in the milestone. The original
  text recommended a separate `feat/cutlab-workflow-ux` branch off `main`
  "independent of `gsd/cycle21-cut-lab`"; that reasoning assumed `main` did not
  contain Cycle 21, which stopped being true when the branch was rebased and
  `main` fast-forwarded to it (`77a69bf0`).

  **Hard sequencing constraint:** Phase 7 must not start until plan `04-04` is
  committed. `04-04` rewrites `CutLab.cshtml` and `wwwroot/ts/cut-lab.ts` for the
  presenter merge and the D-24 combo-badge repair — the same two files 07-02,
  07-03 and 07-04 reorder and re-wire. Running them concurrently on separate
  branches is how this milestone earns a third rebase with real conflicts.

## Follow-ups deliberately excluded

- Auto-recompute Goals / Compare on accept instead of manual Recalculate
  (engine + perf change, not copy).
- Merge the three pool browse surfaces (role / type / subtype) plus the
  JS-only lock-table filter into one faceted explorer.
- Remove the duplication between the anchor nav and the tablist.
- Defer the four Export "⚠ pending" status rows until Build export runs.
