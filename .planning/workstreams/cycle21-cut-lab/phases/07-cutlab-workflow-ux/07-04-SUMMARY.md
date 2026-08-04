# Plan 07-04 Summary

## Built

- **Task 1 — intake summary.** When `Model.HasResult`, the intake form renders inside a
  closed `<details>` whose `<summary>` is a `.cutlab-intake-summary` row (commander, card
  count, bracket, play experience, board breakdown). The form stays in the DOM and stays
  submittable — `CutLabStateJson`, the antiforgery token and every intent field survive the
  collapse, so re-import from the summary still works. Summary text is assembled in
  `CutLabViewModel`, not in Razor, and covered by a new `CutLabViewModelWordingTests` case.
  (`98f6f745`)
- **Task 2 — unified progress strip.** `.cutlab-progress` now carries `{baseline} → 100`, the
  bar, `{n} to cut`, `{n} cut`, `{n} locked` and the round label, folding in the sticky bar,
  the round banner, the pool sticky bar and the lock count. The export count was deliberately
  left alone — it gates a form, it is not progress. **No DTO or patch-builder change was
  needed**, as the plan predicted; the complete `data-cut-lab-*` selector contract was carried
  onto the unified component rather than migrating its consumers. (`16dd7bd3`)
- **Task 3 — collapse defaults.** `defaultMobileCollapsedSectionIds` (3 of 13, mobile only)
  is replaced by a both-viewport default that opens only the active step's primary section,
  mapped per wizard panel. localStorage persistence stays authoritative — a stored preference
  still wins, so only a first visit changes. Server-rendered `open` attributes were reduced to
  match, keeping `cut-lab-section-cut-rounds` open for the no-JS page. (`684279fd`)
- **Task 4 — theme + responsive verification.** `cut-lab-theme-readability.spec.ts` gained
  computed-contrast checks for the intake summary and progress strip at both viewports.
  (`5b505ff3`)
- Follow-on fixes in the same plan: round-banner coverage updated for the heading 07-04
  deleted (`d9b33a80`), and distinct lock-pool selectors restored (`1518dd7f`).

**Deviation from the plan's `files_modified`:** the work also touched
`DeckFlow.Web/ts-tests/cut-lab-proposal.test.ts` (−2 lines), which the frontmatter did not
list. Test-only, no contract effect.

## The round-banner heading was removed on purpose

`16dd7bd3` deleted `<p class="cutlab-finding__heading">@Model.Proposal.RoundLabel</p>` from
`.cutlab-round-banner` because the round label moved into the progress strip. This matches the
authorized 07-04 decision *"update the test assertions, do not restore the heading"*. The class
still exists elsewhere (findings groups, role groups); it is gone only inside the banner. The
client path agrees — `renderRoundBanner` emits an unclassed `<p>` — so no heading exists on
either the server render or the client update. Six stale e2e assertions and one vitest
assertion were retired accordingly (see Verification).

## Gate state — all four green, both viewports

| Gate | Exit | Note |
|---|---|---|
| G-1 | **0** | audited, found correct, untouched by the repair |
| G-2 | **0** | dead `[disabled]` filter replaced with `:not([aria-disabled="true"])` |
| G-3 | **0** | repaired; Decide panel **731 px** desktop / **1,099 px** mobile |
| G-4 | **0** | false-green closed (`if (!textarea) return true` → asserts existence) |

**8/8 across `chromium-desktop` + `chromium-mobile`.** Against the plan's limits of 3,000 px
desktop / 4,000 px mobile, the Decide panel comes in **4.1× and 3.6× under**.

## ⚠ Three of the four gates were incapable of failing — read before quoting any older number

07-04 spent two sessions looking blocked on G-3. It was not. `e2e/cut-lab-workflow-ux.spec.ts`
selected the Decide tab with `button.textContent?.trim() === 'Decide'`, but
`_WorkflowStepTabs.cshtml` renders a number span plus a label span, so the real `textContent`
is `"2Decide"`. The find returned `undefined`, a **silent guard** (`if (!decideTab) return;`)
swallowed the miss instead of failing, and the gate went on to measure
`document.documentElement.scrollHeight` — the whole scaffolded document, all five panels
present, with `importPool()`'s test-only expansions of lock-pool, competes and the Lands role
group inflating it further.

The audit that followed found two more defects of the same family:

- **G-4** had an *independent* false-green: `if (!textarea) return true` reported PASS when
  `#cut-lab-deck-text` was missing — precisely the regression the gate exists to catch.
- **G-2 and G-3** both filtered tabs on `[disabled]`, an attribute `_WorkflowStepTabs.cshtml`
  documents at length that it never emits (it uses `aria-disabled` so a disabled tab stays
  focusable for arrow traversal). The filter removed nothing.

**Consequence: every pre-`8ea1e41d` G-3 number in this phase's artifacts is uninformative** —
including `07-04-PLAN.md`'s own acceptance figures, 07-03-SUMMARY's `10662`, and the
"+63% / +100% over" table that briefly appeared in `.continue-here.md`. They measured the wrong
quantity. Do **not** conclude "07-04 failed" from them, and do not open a height-reduction
workstream on their strength. There is no height problem.

The repair (`8ea1e41d`, test-only) makes G-3 select by the stable `aria-label`, fail hard when
the tab is missing or `aria-disabled`, assert the click took (`aria-selected="true"`), and
measure the panel resolved from `aria-controls` — never `document.documentElement`.

**The repaired gate was mutation-proven, not merely observed green.** Forcing the threshold to
`1` produced real failures reporting 731 px / 1,099 px, independently reproducing the prior
session's 733 / 1,101 diagnosis within 2 px. A gate that passes without ever having been shown
to fail is indistinguishable from the silent no-op it replaced.

## Verification

```text
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -t:Rebuild -v q --nologo
exit 0 — 0 errors, 9 unique CS8629 (pre-existing baseline, all in
         DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs)

"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --nologo -v q
exit 0 — 2297 passed, 0 failed, 16 skipped

npm run test              exit 0 — 127/127 across 33 files
npm run typecheck:e2e     exit 0 (run independently, not trusted from a Codex report)

G-1  -g "G-1"  chromium-desktop + chromium-mobile   exit 0
G-2  -g "G-2"  chromium-desktop + chromium-mobile   exit 0
G-3  -g "G-3"  chromium-desktop + chromium-mobile   exit 0   731 px / 1,099 px
G-4  -g "G-4"  chromium-desktop + chromium-mobile   exit 0
```

Each gate ran as its own process with `-g`; the verdict is the process **exit code**, not the
printed footer. The spec is `mode: 'serial'`, so a batched run halts on first failure and
reports the rest as "did not run" — which skims as a pass.

Two e2e blockers were cleared to get here, both test-only:

- **A** — `.check()` on a `.manabase-pill` radio is intercepted by its own label span (the
  1×1 `opacity:0` input lands at the pill centre under the span). Shared
  `clickManabasePillRadio` helper applied to all 24 call sites across 9 specs; the one
  `{ force: true }` band-aid removed. Not user-facing — a real click hits the label and
  label-association forwards it. (`bb4f9b71`)
- **B** — after import, step 1 is complete so the tabs select step 2, and the collapse
  defaults close `cut-lab-section-lock-pool`, hiding `Lock your pool` from the a11y tree.
  **This product behavior is intended**; the specs migrated to an idempotent
  `expandCutLabSection` helper and **no production file changed**. The rule learned the hard
  way: expand the section that *owns* the element you touch next, read from the view — not the
  section whose heading you assert. (`44741512`, `0826a714`, `fc226b9a`)

## Acceptance criteria

| Criterion | Verdict |
|---|---|
| G-3 passes both viewports; G-4 passes; G-1 and G-2 still pass | ✅ 8/8, exit 0 each |
| `CutLabUiPatchBuilderTests` pass unchanged, or the contract change is justified | ✅ unchanged — no 07-04 commit touches `CutLabUiPatchDto.cs` or `CutLabUiPatchBuilder.cs` |
| New CSS in `site-common.css`; no `--theme-surface` | ✅ both components present in `site-common.css`; 0 `--theme-surface` uses in their rules |
| Contrast + theme-readability pass on Classic, Azorius, Nyx at both viewports | ⚠ **NOT VERIFIED** — see below |

## Known limitations

- **Theme-readability is unverified against the current tree.** The Task 4 checks were written
  and landed (`5b505ff3`), but `cut-lab-theme-readability.spec.ts` has not been run since
  `8ea1e41d`. The 2026-08-03 full sweep that would have covered it was **invalidated by admin
  lockfile contention** and discarded. This criterion needs one isolated run
  (`--workers=1 --project=<one>`, per viewport) before the phase closes.
- **The other nine cut-lab specs were not re-measured** after the gate repair. G-2's selector
  change is local to one file but has not been swept against them.
- **Task 5 (human UAT checkpoint) is outstanding** and must not be self-approved. Two cautions
  carried into it: `07-04-PLAN.md` says 1440×900 but `playwright.config.ts` runs
  `chromium-desktop` at **1280×900**, and Task 5 as written measures
  `document.documentElement.scrollHeight` — the exact quantity that made G-3 lie. Report the
  Decide panel alongside it. The deferred blocker-B UX question belongs at this checkpoint too:
  the locking UI lives inside step 1, which import marks complete, so a user protecting cards
  must navigate back into a step the wizard has already closed. Judge it from screenshots.
- `e2e/cut-lab-tuning.spec.ts:321` (tuner screenshot matrix) still cannot pass locally by
  construction — capture-only, ~6 min against a 120 s budget, `test.skip`'d on CI.
  Pre-existing, ticketed, not a Phase 7 regression.
- Running the e2e suite rewrites tracked PNGs under
  `.planning/ui-design/cut-lab/screenshots/`. They were reverted rather than committed;
  regenerate once at phase close, after 07-05 and 07-06 stop changing the UI.
