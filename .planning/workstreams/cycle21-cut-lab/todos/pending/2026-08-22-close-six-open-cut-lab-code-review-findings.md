---
created: 2026-08-22T22:27:24.374Z
title: Close six open Cut Lab code-review findings
area: ui
severity: major
files:
  - DeckFlow.Web/wwwroot/ts/cut-lab.ts:494-524,556,571
  - DeckFlow.Web/wwwroot/ts/site.ts:294-310
  - DeckFlow.Web/Views/Deck/CutLab.cshtml:304,337,868,1128,1131,1362
  - DeckFlow.Web/Views/Shared/_WorkflowStepTabs.cshtml:10-12,24-45
  - DeckFlow.Web/ts-tests/cut-lab-step-tabs.test.ts:52-89
  - DeckFlow.Web/e2e/cut-lab-workflow-ux.spec.ts:183-207
  - DeckFlow.Web/e2e/cut-lab-theme-readability.spec.ts:10-46,486-499
  - DeckFlow.Web/wwwroot/css/site-grixis.css:4,6
  - DeckFlow.Web/wwwroot/css/site-planeswalker-dark.css:4,6
  - DeckFlow.Web/wwwroot/css/site-jund.css:4,6
  - DeckFlow.Web/Models/Api/CutLabUiPatchDto.cs:28
  - DeckFlow.Web/Services/CutLab/CutLabUiPatchBuilder.cs:160-166
  - DeckFlow.Web.Tests/CutLabFunctionalTwinsDensityTests.cs:119,134,140,152
---

## Problem

Six findings from two Cut Lab code-review gates are still open, plus two vacuous tests carried
forward from the earlier gate. Sources:

- `.planning/reviews/2026-08-22-cycle21-0706-code-gate.md` (Round 2 — R2-1, R2-2)
- `.planning/reviews/2026-08-16-cycle21-cutlab-code-gate.md` (F-5, F-6, F-7, density tests)

All eight items were re-verified against the working tree at `fb7f146c` on 2026-08-22 before this
capture was written; every `file:line` below is current.

### Goal

Close all eight items so the Cut Lab branch leaves its code gate with zero open findings and no test
that certifies behaviour it cannot observe. Each fix must ship with a test whose named killing
mutation actually kills it.

### The eight items

**R2-1 [HIGH] — arrow navigation activates the reserved, aria-disabled Plan tab.**
`cut-lab.ts:556` builds the roving-focus list as `getStepTabs().filter(tab => !tab.disabled)`.
The F-2 fix removed the native `disabled` attribute from step tabs (deliberately — a disabled
button is unfocusable and dead-ends arrow traversal), so `tab.disabled` is now `false` for every
tab and the reserved, `aria-disabled="true"` Plan tab (step 3) joins the list. Arrow-Right onto it
runs `nextTab.focus(); activateStepTab(nextTab, false);` at `:571`, which selects the tab and
reveals the empty reserved panel that F-3 exists to keep shut. The capture-phase guard at
`site.ts:298-310` only swallows *clicks* on `[role="tab"][aria-disabled="true"]`; it cannot see a
direct `activateStepTab` call, so it never fires on this path. Net effect: keyboard users can open
a step the mouse path refuses to open.

**R2-2 [MEDIUM] — the F-1 regression guard is vacuous.**
`e2e/cut-lab-workflow-ux.spec.ts:183` (`G-6 lets a workflow control beneath the pinned proposal
receive a click`) creates a `position: fixed` `<button>` in JavaScript, appends it to
`.cutlab-proposal__body`, and asserts it receives a click. Production markup never emits such an
element. The guard is not inert — making `.cutlab-proposal--pinned` intercept pointer events can
fail it — but passing it certifies nothing about whether a real workflow control stays clickable,
and F-1's original defect was exactly "tests went green against a live defect".

**F-5 [MEDIUM] — five tabpanels visible while one tab claims selection.**
`CutLab.cshtml` renders five `role="tabpanel"` sections (`:337`, `:868`, `:1128`, `:1131`, `:1362`)
and none carries `hidden`. `_WorkflowStepTabs.cshtml:10-12` picks a server-side `currentStep` and
marks exactly one tab `aria-selected="true"`, but `attachStepTabHandler` (`cut-lab.ts:531-541`)
only toggles the `is-active` class on load — it never calls `activateStepTab`, so no panel is
hidden until the user clicks a tab. Keyboard and screen-reader users meet five visible tabpanels
under a tablist that claims one is selected. **The defect is locked in by assertion:**
`ts-tests/cut-lab-step-tabs.test.ts:56` asserts `toHaveLength(5)` for
`[role="tabpanel"]:not([hidden])` after initialization.

**F-6 [MEDIUM] — `--muted` fails WCAG on `--panel` in three themes.**
Measured on the intake summary (`.cutlab-intake-summary`, `--muted` text on `--panel`):

| Theme | `--muted` | `--panel` | Ratio | |
|---|---|---|---|---|
| grixis | `#91889d` | `#2e3244` | 3.75:1 | fail |
| planeswalker-dark | `#8b92a8` | `#2e3450` | 3.93:1 | fail |
| jund | `#a08d7d` | `#352b24` | 4.34:1 | fail |

The blind spot is encoded in the test matrix as well as in the fix: `cut-lab-theme-readability.spec.ts`
runs its *named-element* sweep over all 24 themes (`:10-35`), but the **workflow-component** test —
the one that actually asserts the intake summary and progress strip at 4.5:1, `:486-499` — iterates
`workflowComponentThemes` at `:37-41`, which is only `classic`, `azorius`, `nyx`. `efee8599` raised
only `site-nyx.css` for the same reason.

**F-7 [LOW] — `nextProposal` declared non-null, serializes null.**
`Models/Api/CutLabUiPatchDto.cs:28` declares
`public CutLabDecideNextProposalDto NextProposal { get; init; } = new();` and
`wwwroot/ts/cut-lab.ts:174` mirrors it as non-nullable `nextProposal: CutLabDecisionNextProposal`.
`CutLabUiPatchBuilder.cs:160-166` (`BuildAdjustPatch`) emits `null!` whenever
`projection.CanBuildExport` is false — i.e. any adjust on a list that is not exactly 100 cards. TS
call sites already guard defensively, so the declared contract is looser than reality rather than
actively broken. Align the declaration with what the builder actually emits.

**Two vacuous density tests (four assertion sites).**
`DeckFlow.Web.Tests/CutLabFunctionalTwinsDensityTests.cs`:
- `FunctionalTwins_OnDiverse130CardPool_ProducesNoLandGroups` — nested `foreach` over findings and
  evidence wrapping `Assert.DoesNotContain` (`:119` outer / `:134` inner). Zero findings ⇒ zero
  iterations ⇒ green.
- `FunctionalTwins_OnDiverse130CardPool_TotalEvidenceCardsStayBounded` — `distinctEvidenceCount <= 40`
  (`:140` / `:152`). Zero findings ⇒ `0 <= 40` ⇒ green.

Killing mutation for all four: make `FunctionalTwins` return an empty list. The sibling test in the
same file already models the fix — it opens with
`Assert.True(twins.Count > 0, "Expected at least one FunctionalTwins finding …")` before its bound.

## User-visible behaviour after the fix

- **Keyboard.** Arrow-Left / Arrow-Right / Home / End still move focus onto the reserved Plan tab —
  a roving tablist must not strand a tab — but focus landing there does **not** select it: the tab's
  `aria-selected` stays `false`, the previously visible panel stays visible, and the empty reserved
  panel stays shut. Selection and focus decouple. Mouse behaviour is unchanged (already blocked by
  the `site.ts` capture-phase guard).
- **Page load.** Exactly one Cut Lab tabpanel is visible on arrival, matching the tab the server
  marked `aria-selected="true"`. Today all five are visible until the first click.
- **Themes.** Intake-summary and progress-strip copy is legible at 4.5:1 or better on grixis,
  planeswalker-dark and jund — plus any other theme the widened matrix turns up as failing.
- **Everything else is invisible to the user.** R2-2, F-7 and the density tests change test and
  contract declarations only; no rendered output moves.

## Non-goals

- **Not fixing the same F-5 defect on the four sibling tools.** `CedhMetaGap.cshtml:99,208,389`,
  `DeckComparison.cshtml:228,356,578` and `DeckAnalysis.cshtml:150,200,520,886,966` render their
  tabpanels without `hidden` too, and `DeckPrimer.cshtml:120` shares the same partial. The named
  scope is Cut Lab. **Record the spillover as a follow-up; do not widen into it.**
- **Not refactoring `_WorkflowStepTabs.cshtml`.** A separate pending todo,
  `.planning/todos/pending/2026-08-02-split-workflow-step-tabs-into-tablist-and-jump-nav.md`, already
  owns splitting the partial's two incompatible interaction patterns and consolidating the keyboard
  contract that is implemented four times in TypeScript. This work must not pre-empt or contradict it.
- **Not re-theming.** F-6 raises three (or more) `--muted` tokens to clear the floor; it does not
  restyle the intake summary, change which token the summary uses, or touch the other 21 themes
  except where the widened matrix proves a failure.
- **Not flipping the Cut Lab production flag.** `tool.cut-lab.enabled` stays OFF in production; this
  is defect closure inside the existing gate.
- **Not re-opening closed findings.** F-1, F-2, F-3, F-4 are FIXED. R2-1 is the *remainder* of F-2/F-3,
  not a re-litigation of them.

## Affected existing behaviour

| Surface | Change |
|---|---|
| `cut-lab.ts` arrow-key handler | roving list membership + activation predicate |
| `cut-lab.ts` init (`attachStepTabHandler`) | now hides non-active panels where it previously only set `is-active` |
| `CutLab.cshtml` | may gain server-rendered `hidden` on non-current panels (no-JS path) |
| `cut-lab-step-tabs.test.ts:56` | the `toHaveLength(5)` assertion asserts the bug and must change |
| `cut-lab-step-tabs.test.ts:70` | must keep asserting focus reaches Plan; gains an `aria-selected === 'false'` assertion |
| `cut-lab-step-tabs.test.ts:83` | the "server selected no tab" fallback — decide and state whether all-visible remains correct there |
| `CutLabUiPatchDto` / TS `CutLabUiPatch` | `nextProposal` nullability, and every consumer of it |
| three theme CSS files | `--muted` affects **every** element using it in that theme, not only the intake summary (`site-common.css` alone has 49 `var(--muted)` uses, `site.css` 14) |

## Constraints

- **Preserve each touched file's existing line endings exactly.** Detect per file — some are LF, some
  CRLF, some mixed. Do not convert LF↔CRLF, do not normalize, do not assume a repo-wide style.
  Change only the lines whose content actually changes; leave every other line and its ending
  byte-for-byte identical. Verify with `git diff --stat` vs `git diff --ignore-all-space --stat`.
- **Guild themes are standalone CSS forks.** Any theme fix needs a census across all 24
  (`site.css` + 23 `site-*.css`, excluding `site-common.css`, `site-mobile.css`,
  `site-theme-overrides.css`, `admin-*.css`). Fixing three files by hand and calling it done is the
  exact failure F-6 records.
- **Layout CSS goes in `site-common.css`, never `site.css`.** Token values go in each theme's `:root`.
- **`aria-disabled`, not native `disabled`.** `_WorkflowStepTabs.cshtml:24-32` documents why at
  length, and the `type`/`form` binding must stay unconditional — making it conditional previously
  left the Export tab looking enabled while submitting nothing (`cut-lab-export.spec.ts:141`).
- **The partial is shared by five tools.** Any change inside `_WorkflowStepTabs.cshtml` or `site.ts`
  reaches CedhMetaGap, DeckPrimer, DeckComparison, DeckAnalysis and CutLab. Prefer changes scoped to
  `cut-lab.ts` / `CutLab.cshtml`; if a shared file must change, census all five consumers first.
- **`UseMobileLabeledTabs` is Cut Lab-only** — verified: only `CutLab.cshtml:18` passes `true`.
- **Testing.** VSTest is unreliable in WSL; rely on a clean `dotnet build` plus targeted runs.
  Run vitest **from `DeckFlow.Web`**, never the repo root (the root sweep pulls in Playwright e2e and
  produces false failures). Green vitest is 33 files / 131 tests at this HEAD; Web xUnit baseline is
  2302 passed / 0 failed / 16 skipped; build baseline is 0 errors / 15 warnings (9 pre-existing
  CS8629 + NU1903 advisories).
- **UI testing must never open a browser on the Windows host** — start the app with
  `scripts/run-web-test.sh` and drive with `npx --no-install playwright test`.
- **Every new or changed test needs a named killing mutation**, run and shown to fail. This branch
  has produced vacuous tests repeatedly; a green test is not evidence.
- Changed C# lines must satisfy the changed-lines format gate; the five `.editorconfig` carve-outs
  apply (notably: never convert `{ get; init; }` to `{ get; }` — System.Text.Json silently skips
  get-only properties).

## Edge cases

- **Focus vs selection must stay decoupled.** The existing test at `cut-lab-step-tabs.test.ts:70`
  asserts Plan *receives focus* on ArrowRight and that is correct roving-tablist behaviour — its own
  recorded killing mutation is "skip aria-disabled tabs during ArrowRight traversal", i.e. the naive
  form of the R2-1 fix would break it. The fix must keep focus moving and add the missing
  `aria-selected` assertion, not remove the tab from traversal.
- **Home / End and wrap-around** hit the same predicate as ArrowLeft/ArrowRight. If Plan is the End
  target or the wrap target, the same focus-without-selection rule applies.
- **The reserved panel 3 must stay in the DOM** — `_WorkflowStepTabs.cshtml` emits `aria-controls`
  pointing at `cut-lab-step-panel-3`; a dangling `aria-controls` is its own a11y defect.
- **`activateStepTab` has a side effect on init.** It calls
  `if (readCollapsedSectionIds() === null) applyDefaultSectionCollapseState(step)` (`cut-lab.ts:506-508`),
  which force-opens the step's primary `<details>` and closes the others. Reaching F-5's panel hiding
  by calling `activateStepTab` on load would run that on **every** page load for a user with no stored
  collapse state, changing which sections are open on arrival. `applyDefaultSectionCollapseState`
  does not itself write to `localStorage`, so nothing is persisted — but the visual default changes.
  Decide deliberately whether initialization should hide panels *without* the collapse side effect.
- **The no-tab-selected fallback** (`cut-lab-step-tabs.test.ts:83`, `attachStepTabHandler` returns
  early when no tab is `aria-selected="true"`) currently leaves all five panels visible. That is
  defensible as a progressive-enhancement fallback — content must never be stranded behind a tablist
  that has no selection. State the chosen behaviour explicitly rather than changing it by accident.
- **No-JS path.** If panels gain server-rendered `hidden`, a user with JavaScript disabled sees only
  the server-chosen step and can never reach the others. If they do not, JS-off is unchanged but the
  first paint still shows five panels. This is a genuine fork — pick one and record the reasoning.
- **Theme census may find more than three failures.** The workflow-component matrix has only ever run
  three themes; widening it to 24 is expected to surface additional failures. Those are in scope —
  the point of the widening is that the fix and the test stop sharing a blind spot.
- **Raising `--muted` affects more than the intake summary.** Check the raised value against `--bg`
  as well as `--panel`, and against the other named elements the 24-theme sweep already asserts, so
  a fix for one element does not regress another.
- **`nextProposal` null shape.** `BuildAdjustPatch` emits `null!` only on the not-yet-at-100 path; the
  at-100 path emits a terminal marker (`IsTerminal = true, IsAtTarget = true`). Enumerate every other
  builder path and every TS consumer before changing the declaration — the census, not a sample.
- **Density tests: the precondition must bound the right population.** `twins.Count > 0` alone is the
  minimum; evidence-level assertions also need at least one evidence entry, or the inner loop stays
  vacuous even with a nonempty outer list.

## Authorization implications

None. No authentication, authorization or CSRF surface changes.

For the record, verified at this HEAD: all five `CutLabApiController` actions (decide, adjust,
restart-rounds, what-if preview, what-if commit) carry `[FeatureFlagGate("tool.cut-lab.enabled")]`
and `ToolRouteGateCoverageTests`' sibling rule reaches them. `tool.cut-lab.enabled` is **OFF in
production**. If F-7 changes the patch DTO, re-confirm the gate attributes survive the edit —
nothing else here should touch a controller.

## Persistence implications

**No database, schema, migration or seed changes.** No new configuration keys, no feature-flag rows.

Two client-side storage touchpoints to be aware of, neither of which should change:
- `CUT_LAB_SECTION_STORAGE_KEY` — collapsed-section ids in `localStorage`. F-5's fix must not begin
  writing it at load time (see edge cases).
- `CutLabStateJson` — the working-session state round-tripped through the form and the patch DTO.
  F-7 changes only the declared nullability of a sibling property; the serialized state string is
  untouched.

## Likely MVC surfaces

- **Views:** `Views/Deck/CutLab.cshtml` (five tabpanels; the reserved panel 3), and read-only
  reference to `Views/Shared/_WorkflowStepTabs.cshtml` (the `aria-disabled` / `currentStep` contract).
- **Models:** `Models/Api/CutLabUiPatchDto.cs`.
- **Services:** `Services/CutLab/CutLabUiPatchBuilder.cs` (`BuildAdjustPatch` and any sibling builder
  the census turns up).
- **Controllers:** none expected. If a controller must change, that is a signal the scope drifted.
- **Client:** `wwwroot/ts/cut-lab.ts` (`activateStepTab`, `attachStepTabHandler`, the
  `CutLabUiPatch` interface); `wwwroot/ts/site.ts` only if the shared guard must change — prefer not.
- **Static assets:** `wwwroot/css/site-grixis.css`, `site-planeswalker-dark.css`, `site-jund.css`,
  plus whatever the 24-theme census adds. `wwwroot/js/*.js` is gitignored — never stage compiled JS.
- **Tests:** `ts-tests/cut-lab-step-tabs.test.ts`, `e2e/cut-lab-workflow-ux.spec.ts`,
  `e2e/cut-lab-theme-readability.spec.ts`, `DeckFlow.Web.Tests/CutLabFunctionalTwinsDensityTests.cs`,
  plus any test asserting `nextProposal`'s shape.

## Acceptance criteria

1. **R2-1.** With the Plan tab reserved (`aria-disabled="true"`), ArrowRight/ArrowLeft/Home/End move
   focus onto it, and after that keypress: `document.activeElement` is the Plan tab, the Plan tab's
   `aria-selected` is `"false"`, the previously visible panel is still visible, and
   `cut-lab-step-panel-3` is still hidden. Clicking the Plan tab remains blocked as today.
2. **R2-1 test.** `ts-tests/cut-lab-step-tabs.test.ts:70` keeps its focus assertion and gains the
   `aria-selected === 'false'` and panel-visibility assertions. Named killing mutation — remove the
   new activation guard — is run and shown to fail the test.
3. **R2-2.** `e2e/cut-lab-workflow-ux.spec.ts:183` no longer injects synthetic markup. It targets a
   control the page actually renders, positioned under the sticky/pinned region, clicks through at a
   point `.cutlab-proposal--pinned` overlaps, and asserts the real control received the event.
   Killing mutation — remove `pointer-events: none` from `.cutlab-proposal--pinned` — fails it.
4. **F-5.** After initialization on `/cut-lab`, exactly one `[role="tabpanel"]:not([hidden])` exists
   and it is the panel whose `aria-labelledby` tab carries `aria-selected="true"`. The
   `toHaveLength(5)` assertion at `cut-lab-step-tabs.test.ts:56` is corrected, not deleted, and the
   no-tab-selected fallback behaviour is stated explicitly in the test name or a comment.
5. **F-5 side effect.** Collapsed-section default state on first load is either unchanged from today,
   or changed deliberately with the reasoning recorded. `localStorage` is not written at load time.
6. **F-6.** grixis, planeswalker-dark and jund reach ≥ 4.5:1 for `--muted` on `--panel`. A census of
   all 24 themes is recorded (theme → `--muted` → `--panel` → ratio → pass/fail), every failure it
   finds is fixed, and each raised value is also checked against `--bg`.
7. **F-6 test.** `workflowComponentThemes` in `cut-lab-theme-readability.spec.ts:37` covers every
   theme the census identifies as needing coverage — at minimum the three failing themes are added.
   Reverting one raised `--muted` to its old value fails the widened test.
8. **F-7.** The declared contract matches the emitted payload: `CutLabUiPatchDto.NextProposal` and the
   TypeScript `CutLabUiPatch.nextProposal` agree with what `CutLabUiPatchBuilder` actually produces,
   `null!` is gone from `CutLabUiPatchBuilder.cs:160-166`, and every consumer found by census
   compiles and handles the declared shape.
9. **Density tests.** Both tests gain a nonempty precondition. The empty-list mutation — make
   `FunctionalTwins` return `[]` — is run and shown to **fail** both tests; today it leaves both green.
10. **Regression.** `dotnet build` clean with no new errors and no new warnings against the
    0 errors / 15 warnings baseline. Web xUnit ≥ 2302 passed / 0 failed. Vitest run from
    `DeckFlow.Web`: 33 files / ≥ 131 tests, 0 failed. Playwright specs touched by items 3 and 7 pass
    headless.
11. **Line endings.** `git diff --stat` and `git diff --ignore-all-space --stat` agree; zero
    unintended CR churn across every touched file.
12. **Follow-up recorded.** The sibling-tool F-5 spillover (CedhMetaGap, DeckComparison, DeckAnalysis,
    DeckPrimer) is captured as its own todo, cross-referenced to the existing
    `2026-08-02-split-workflow-step-tabs-into-tablist-and-jump-nav.md`.
13. **Gate closed.** Both review files' "Open work leaving this gate" tables are updated to reflect
    the closures.

## Solution

TBD — this is a research-and-plan input, not a prescription. Two decisions are deliberately left to
planning, and both must be stated explicitly rather than defaulted into:

- **F-5:** hide panels client-side on init, server-side in `CutLab.cshtml`, or both — and what
  happens with JavaScript off.
- **F-7:** the review's phrasing ("align the declared contract with reality") points at declaring
  `nextProposal` nullable on both sides, since `null` is what the builder emits. The alternative —
  making `BuildAdjustPatch` always emit a non-null marker — changes the wire payload and what the
  client renders, so it is a behaviour change, not a contract correction. Prefer the former unless
  the consumer census argues otherwise.
