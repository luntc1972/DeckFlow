# Plan 07-02 Summary

## Built

- Task 0 (`63eeec40`) established the five workflow slots: Process (1), Decide
  (2), reserved Plan (3), Goals (4), and Export (5), and migrated Export's
  tab/panel selector consumers from 4 to 5.
- Task 1 (`d34d0a91`) moved the Decide panel and Cuts made directly after the
  processing material.
- Task 2 (`5a5c2c15`) grouped Scenarios, What-if swap, and Compare to baseline
  with Goals.
- Task 3 (`d7bfc4c8`) reordered the anchor navigation to agree with the page.
- Task 4 (`3fd818b8`) recorded content-preservation checks.
- Task 5 (`806cf4e6`) completed the missed jump-nav `data-cut-lab-step` migration
  and removed two obsolete `is-disabled` checks from the Export e2e spec. The
  checks duplicated the maintained `disabled` and `aria-disabled` contract;
  `ea3dca2ab` had intentionally removed that CSS state.

## Selector-migration consumer census

The renumbered identities are Goals `tab/panel-3 -> tab/panel-4` and Export
`tab/panel-4 -> tab/panel-5`. A whole-repository search covered C#, Razor, TS,
e2e, Vitest, CSS, Help, and the generated JavaScript derivative.

| Old identity | Readers after the change | State |
| --- | --- | --- |
| `cut-lab-step-tab-3` / `cut-lab-step-panel-3` (Goals) | `DeckFlow.Web/Views/Deck/CutLab.cshtml:1108` | Reserved empty Plan slot; no former Goals consumer remains. |
| `cut-lab-step-tab-4` / `cut-lab-step-panel-4` (Export) | `DeckFlow.Web/Views/Deck/CutLab.cshtml:1111`; `DeckFlow.Web/e2e/cut-lab-workflow-ux.spec.ts:68` | Goals now owns slot 4. The gate is outside this plan's scope and correctly reads the canonical order. |
| `cut-lab-step-tab-4` / `cut-lab-step-panel-4` (Export) | `DeckFlow.Web/wwwroot/ts/cut-lab.ts:778`; `DeckFlow.Web/wwwroot/js/cut-lab.js:422`; `DeckFlow.Web/e2e/cut-lab-export.spec.ts:61,63`; `DeckFlow.Web/e2e/cut-lab-tuning.spec.ts:50,52`; `DeckFlow.Web/e2e/cut-lab-nav-themes.spec.ts:375-377`; `DeckFlow.Web/ts-tests/cut-lab-adjust.test.ts:210,340`; `DeckFlow.Web/ts-tests/cut-lab-jump-nav.test.ts:26,99,104`; `DeckFlow.Web/ts-tests/cut-lab-proposal.test.ts:185,281,962,1064`; `DeckFlow.Web/ts-tests/cut-lab-structural-cardtext.test.ts:128`; `DeckFlow.Web/ts-tests/cut-lab-structural-evidence-lock.test.ts:142`; `DeckFlow.Web/Views/Deck/CutLab.cshtml:1342` | All source consumers point to Export slot 5. `wwwroot/js/cut-lab.js` is the ignored generated derivative, outside the fence and not staged. |
| `#cut-lab-step-panel-N` anchors | `DeckFlow.Web/Views/Deck/CutLab.cshtml:307,314` | Process and Decide anchors retained and match DOM order. No old Goals/Export panel anchor reader remains. |

No C#, CSS, or `DeckFlow.Web/Help/` consumer was found. The two outside-fence
readers are explicitly flagged above: the phase gate and ignored generated JS;
neither was hand-edited.

## Preservation

- `data-cut-lab-*`: `103 -> 103`
- `@Html.AntiForgeryToken()`: `8 -> 8`
- `id="cut-lab-`: `40 -> 41`

The added id is `cut-lab-step-panel-3`, the intentionally empty reserved Plan
panel. Goals and Export changed numeric ids only.

## Verification

Build baseline: the current incremental build printed zero warnings and zero
errors. The authoritative rebuild baseline remains **0 errors / 9 pre-existing
CS8629 warnings** in
`DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs`; they are
unrelated to Phase 7 and were not modified.

```text
"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -v q --nologo

Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```text
npm run test

Test Files  32 passed (32)
     Tests  124 passed (124)
```

```text
npm run typecheck:e2e
npm notice run tsc -p e2e/tsconfig.json
```

```text
env -u DISPLAY npx --no-install playwright test e2e/cut-lab-workflow-ux.spec.ts \
  --grep 'G-1' --project=chromium-desktop --project=chromium-mobile --workers=1

2 passed (17.5s)
```

```text
env -u DISPLAY npx --no-install playwright test e2e/cut-lab-export.spec.ts \
  e2e/cut-lab-nav-themes.spec.ts e2e/cut-lab-tuning.spec.ts \
  --project=chromium-desktop --workers=1

Running 10 tests using 1 worker
  ✓ 1 keeps export disabled until the working list reaches exactly 100 cards
  ✓ 2 live-updates the export panel card count after an AJAX cut decision
  ✓ 3 builds the export once accepted cuts reach the target count
  ✓ 4 captures cross-theme mobile chrome coverage for Cut Lab navigation
```

The workflow gate was run individually because its serial configuration stops
after a failure:

| Gate | State | Assertion reached |
| --- | --- | --- |
| G-1 | PASS (desktop and mobile) | panel id array equals `1, 2, 3, 4, 5` in document order |
| G-2 | RED as expected | `expect(after).not.toEqual(before)` at `cut-lab-workflow-ux.spec.ts:91` |
| G-3 | RED as expected | `expect(height).toBeLessThan(limit)` at `:111`; received 10782 desktop / 16967 mobile |
| G-4 | RED as expected | `expect(intakeIsCollapsed).toBe(true)` at `:124` |

## Deviations

- Task 5 found and fixed a missed assertion migration in the allowed jump-nav
  test.
- The visual e2e spec rewrote tracked screenshot artifacts during execution;
  all nine were restored and were not staged.
- Playwright's runner stream did not emit its final footer after the tuning
  matrix in this environment. The clean rerun showed the export and nav
  scenarios passing and completed with no failed `.last-run.json` record.

## Known limitations

- G-2, G-3, and G-4 deliberately remain red for plans 07-03, 07-04, and 07-05.
- The generated `wwwroot/js/cut-lab.js` remains ignored and uncommitted by
  design; it is produced from the migrated TypeScript during the build.
