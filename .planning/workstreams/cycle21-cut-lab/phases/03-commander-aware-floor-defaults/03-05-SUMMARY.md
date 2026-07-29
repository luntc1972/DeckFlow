# Plan 03-05 — Role-floors table: four columns to six

**Status:** complete. Tasks 1-3 implemented and blind-verified; Task 4 (blocking human-verify
checkpoint) approved by the developer on 2026-07-29.

## What was built

The Cut Lab role-floors table now renders six columns — `Role | In pool | Bracket | Commander | Floor
| Source` — surfacing both the bracket-derived floor and the commander-derived floor side by side
(RFLR-08), rather than only the effective number.

| Commit | Task | Files |
|---|---|---|
| `df995081` | 1 — view-model fields + the two empty-cell states | `CutLabViewModel.cs` |
| `f16614a4` | 2 — six columns rendered, layout CSS scoped | `CutLab.cshtml`, `site-common.css` |
| `9f3ff879` | 3 — tests, moved SourceLabel assertions, README | `CutLabViewModelTests.cs`, `CutLabPageServiceTests.cs`, `README.md` |
| `af8c7c6c` | post-verification fix — desktop-only column sizing | `site-common.css` |

## The two empty states

The Commander column has two distinct empty renderings, and conflating them would answer the user's
question wrongly:

- **`n/a`** — the role is *structurally* out of scope (`lands`, `interaction-mass`, `protection`).
  Commander data will never exist for these roles.
- **`—`** (em dash) — the role IS in scope, this commander simply has no measured data for it.

`BuildFloorRows_OutOfScopeRole_ShowsNotApplicable` pins the first by supplying **non-null** commander
values (40 / 9 / 8) for the three out-of-scope roles and still demanding `n/a` — so it proves
suppression of data that genuinely exists, not merely the absence of data.
`BuildFloorRows_GoRoleWithNoCommanderMatch_ShowsEmptyMarker` asserts both `== "—"` and `!= "n/a"`.

## Defect found and fixed after implementation

The blind verifier returned **FAIL** on the first pass. The plan instructed placing the new
column-sizing rule "OUTSIDE the existing mobile media query, so the stacked layout is unaffected" —
but a rule outside a `max-width` query applies at *all* widths, including mobile.

The role-floors table carries BOTH `data-prompt-cedh-reference-table` and
`data-cut-lab-role-floors-table` (`CutLab.cshtml:779`), so inside `@media (max-width: 600px)`
(`site-common.css:1044`) the generic stacked rule at `site-common.css:1116-1124` applies to it:
`display: grid; grid-template-columns: 6.5rem 1fr`. That rule has higher specificity but declares no
`width`, so the new unmediated `width: 6rem` won uncontested — narrower than the 6.5rem label track
alone, collapsing the value track.

Fixed in `af8c7c6c` by wrapping the block in `@media (min-width: 601px)` and rewriting the comment,
which had asserted the opposite of what the selector did.

**This defect shipped past 2,138 passing Web tests.** xUnit cannot see a collapsed grid track. It is
the reason this plan carried a human-verify gate.

## Task 4 — visual verification (developer-approved)

Run headless with `DECKFLOW_DISABLE_AUTO_BROWSER=true` and headless Playwright; no browser window was
opened on the Windows host. Fixture: **Yuriko, the Tiger's Shadow at bracket 4**, chosen because the
shipped snapshot carries 5 of the 6 adopted roles for it (ramp absent), so all three Commander-column
states appear on one screen.

Observed, and asserted:

| Role | Bracket | Commander | Floor | Note |
|---|---|---|---|---|
| lands | 34 | `n/a` | 34 | out of scope |
| ramp | 10 | `—` | 10 | GO role, no data for this commander |
| draw | 14 | 7 | 14 | commander loses, number still displayed |
| interaction-targeted | 7 | **10** | **10** | commander raises the floor |
| interaction-mass | 3 | `n/a` | 3 | out of scope |
| protection | 4 | `n/a` | 4 | out of scope |
| engines | 6 | 5 | 6 | commander loses, number still displayed |
| payoffs | 6 | 2 | 6 | commander loses, number still displayed |
| wincons | 3 | 3 | 3 | tie |

- `Floor == max(Bracket, Commander)` held on all nine rows, and `data-cut-lab-floor-default` matched
  the max on all nine.
- **Reset to default** on `interaction-targeted` restored **10** (the max), not 7 (the bracket).
- Mobile ≤600px: the row stacks into label/value pairs with both new `Bracket` and `Commander` labels
  present and no collapsed value track — confirming the `af8c7c6c` fix by rendering, not just by
  cascade analysis.
- Themes: verified on Classic (light) and Nyx (dark); `n/a`, the em dash, and the `Commander` source
  label all hold contrast.
- The `Source` column reads *Commander* on the single row where commander data won, *Bracket*
  elsewhere.

Screenshots (untracked): `.foreman/scratch/task4-shots/`.

## Gates

- Build: 0 errors, exactly 9 pre-existing `CS8629` warnings, all in
  `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs` (untouched by this phase).
- `DeckFlow.Web.Tests`: 2138 passed / 16 skipped / 2154 total (from 2130 / 16 / 2146).
- Full solution: 4575 passed, 0 failed.
- Line endings preserved per file; `git diff --stat` agrees with `git diff --ignore-all-space --stat`
  on every touched file.

## Fences honoured

- Layout CSS lives only in `site-common.css`. `site.css` and every guild theme file are unchanged, and
  no new CSS custom property was introduced.
- No `Html.Raw` anywhere; the new cells render via plain `@` expressions, which HTML-encode. Commander
  *names* are not rendered into the new cells — only integers and the fixed `n/a` / em-dash literals.
- No TypeScript or `wwwroot/js/` change.

## Accepted, not fixed

`min-width: 601px` is the first of its kind in `site-common.css`, which has five `max-width: 600px`
blocks. At fractional widths (600 < w < 601, reachable via browser zoom or fractional DPI) neither
query matches, so the table renders as a normal auto-width six-column table. Benign — unsized, not
broken.

Two cosmetic observations raised at the checkpoint and explicitly accepted by the developer: desktop
`Bracket | Commander | Floor` sit fairly tight, and the mobile `Source` row has a slightly larger gap
than the other label/value pairs.
