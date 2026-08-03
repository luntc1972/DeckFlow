# Phase 7 Context — Cut Lab Workflow UX

## Why this phase exists

Cut Lab's primary navigation control does nothing, and the page is ordered against
its own workflow. Both were measured live on 2026-08-02 against
`scripts/run-web-test.sh` on `:5173` with the `tool.cut-lab.enabled` flag ON.

## Measured evidence (do not re-derive; re-verify only if the view changes)

### E-1 — Step tabs 1-3 are inert; tab 4 is a disabled submit

Native `click` dispatched on each tab (a Playwright `.click()` auto-scrolls the
element into view first, which fakes a "something happened" signal — the probe
used `dispatchEvent` to avoid that artifact):

| Tab | `type` | scroll Δ | `aria-selected` Δ | panel `display` Δ | `location.hash` Δ |
|---|---|---|---|---|---|
| 1 Process | `button` | 0 | no | no | no |
| 2 Decide | `button` | 0 | no | no | no |
| 3 Goals | `button` | 0 | no | no | no |
| 4 Export | `submit` (`form=cut-lab-export-form`) | — | — | — | `disabled` |

Root cause: `CutLab.cshtml:17` passes `DataShowStepAttribute: "cut-lab-step"`, so
`_WorkflowStepTabs.cshtml:34` emits `data-cut-lab-step="N"`. Nothing reads it.
`grep cutLabStep` returns zero hits in both `wwwroot/ts/` and the compiled
`wwwroot/js/cut-lab.js`; the only step-tab reference in the compiled bundle is
`cut-lab.js:422`, which grabs `#cut-lab-step-tab-4`.

Every sibling tool wires its own equivalent attribute:
`deck-sync.ts:1666` (`promptShowStep`), `:1803` (`promptComparisonShowStep`),
`:2095` (`promptCedhShowStep`), plus `primer-selection.ts`. Cut Lab is the one
that never got a handler.

Two consequences beyond "the click is a no-op":

- `aria-selected` is frozen at `false,true,false,false` on every click.
  `_WorkflowStepTabs.cshtml:10-12` picks "first incomplete enabled step"
  server-side, which on a fresh import is Decide. The tablist permanently
  announces Decide selected while all four panels are on screen.
- All four `role="tabpanel"` elements report `display: block` simultaneously.
  The ARIA contract (`role="tab"` + `aria-controls` + `role="tabpanel"`) is
  advertised and not honored.

### E-2 — DOM order contradicts workflow order

`offsetTop` of the four panels, measured after import:

| Panel | desktop 1280 | mobile 390 |
|---|---|---|
| `cut-lab-step-panel-1` (Process) | 1739 | 1721 |
| `cut-lab-step-panel-3` (Goals) | 6658 | 10282 |
| `cut-lab-step-panel-4` (Export) | 7166 | 11328 |
| `cut-lab-step-panel-2` (Decide) | **8710** | **13448** |

Export renders 1,544px above Decide on desktop. Scrolling top-to-bottom, a user
meets the export box before cutting a single card. The anchor nav
(`CutLab.cshtml:303-320`) lists the sections in the correct logical order, so the
page and its own nav disagree.

The anchor nav does work — clicking its "Decide" link scrolled to y=8163
(desktop) / y=12402 (mobile). The only functioning navigation on the page is the
secondary one.

### E-3 — Page bulk

Measured with a **17-row** pool (1 commander, 3 stacked basic rows + 13 other rows), far smaller than
the 101-150 the tool is built for:

| | desktop 1280 | mobile 390 |
|---|---|---|
| `scrollHeight` | 10,453px | 15,896px |
| viewport | 900px | 844px |
| screens of scroll | 11.6 | 18.8 |
| proposal card depth | 87% | 88% |
| `details` sections open | 13 / 13 | 10 / 13 |

Section height is dominated by fixed chrome, not pool size: 13 always-open
panels, four export status rows, four separate browse surfaces. A real pool adds
~130 table rows on top of a page that is already 11 screens.

Mobile default-collapse (`cut-lab.ts:296-300`) covers only
`cut-lab-section-packages`, `-scenarios`, `-whatif` — 3 of 13.

### E-4 — Intake never collapses

`CutLab.cshtml:126-229` — deck URL/textarea, two plan textareas, a 5-pill bracket
group, a 3-pill experience group, two checkboxes — stays fully expanded above the
results for the whole session. Screenshot at 1280×900 after a successful import
shows the first viewport containing only the intake form; the step tablist is
below the fold. The navigation control for the workflow is off-screen when the
workflow begins.

### E-5 — Copy defects (no measurement needed)

- `CutLab.cshtml:1265` — "N of 7 metric families changed meaningfully." "Metric
  family" is internal vocabulary; "meaningfully" is undefined at the point of
  reading. This is the line a user reads on **every** cut.
- `CutLab.cshtml:1187` — "Restart rounds 1 & 2" reads destructive. It only drops
  reject/defer decisions from those rounds; accepted cuts survive
  (`CutLabDecisionApplier.cs:84-94`).
- Locking is explained four times on one panel (`:329-334`, `:472-475`).
- Mobile step tabs render as bare numerals `1 2 3 4` with labels suppressed —
  visible in the option mockups' mobile shots, inherited from `site-mobile.css`.

## Option mockups

Rendered against the app's real `site-common.css` + `site.css` +
`site-theme-overrides.css` + `site-mobile.css` at 1280 and 390.
Sources and screenshots: `.planning/ui-design/cut-lab/proposed/`.

| Option | Desktop height | Mobile height | vs today (desktop) |
|---|---|---|---|
| Today | 10,453px | 15,896px | — |
| 1 — true wizard | 1,022px | 1,440px | −90% |
| 2 — soft fix (one page, reordered, collapsed) | 1,596px | 1,929px | −85% |
| 3 — wizard + pinned proposal | 1,107px | 1,588px | −89% |

Heights are for the Decide step. Options 1 and 3 exclude steps 1/3/4 entirely;
option 2 includes every section as a collapsed summary row.

## Decisions

- **D-1 — Step model. RESOLVED 2026-08-03: Option 3, wizard + pinned proposal
  (~1,107px desktop).** Panel-hiding stays, 07-05 executes, and the five-slot
  contract reserves the Plan slot for Phase 8; the earlier OPEN rationale is superseded.
- **D-2 — No-JS is not a regression.** With scripts disabled every panel must
  render, in workflow order Process → Decide → Plan → Goals → Export. The wizard is progressive
  enhancement layered on a correct document, never a prerequisite for it. This
  is why the DOM reorder (07-02) lands *before* the handler (07-03) and is
  independently shippable.
- **D-3 — Scope fence.** This phase changes navigation, ordering, density and
  copy. It does **not** change the cut engine, the metrics, the proposal
  ordering, or any API contract. No file under `Services/CutLab/` is edited.
- **D-4 — Branch. RESOLVED 2026-08-02.** This phase runs on
  `gsd/cycle21-cut-lab`; the earlier separate-branch recommendation was
  superseded when the branch was rebased and `main` fast-forwarded to it.

## Reproducing the measurements

```bash
scripts/run-web-test.sh                 # never opens a Windows browser
cd DeckFlow.Web
env -u DISPLAY npx --no-install playwright test e2e/cut-lab-workflow-ux.spec.ts \
  --project=chromium-desktop --project=chromium-mobile
```

The probe specs used to produce E-1..E-4 were throwaway; 07-01 lands a permanent
version as the phase's regression gate.
