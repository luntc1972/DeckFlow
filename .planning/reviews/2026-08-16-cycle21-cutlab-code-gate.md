# Cycle 21 Cut Lab — Owed Code-Review Gate

**Date:** 2026-08-16
**Range:** `ea3dca2a..a8e4dc00` — 51 commits, 39 code files, +2635/-694
**Branch reviewed:** `gsd/cycle21-cut-lab` (worktree `deckflow-role-floors`), read-only
**Stage 1:** `codex review --base ea3dca2a`, `gpt-5.6-sol` @ low
**Stage 2:** `codex exec` written brief, `gpt-5.6-sol` @ medium, `-s read-only`

**Gate result: FAIL — 1 HIGH · 4 MEDIUM · 1 LOW · no BLOCK, plus 2 vacuous tests.**

## Why this gate was owed

The branch carries four `*-REVIEWS.md` files (`04`, `05`, `07`, `08`). **All four are PLAN reviews**
— each opens "Claim-vs-Code **Plan** Review" and was run `-s read-only` against the plan set before
execution. None of them reviewed the executed code. Their presence in the phase directories makes
the branch look well-reviewed while being the largest body of unreviewed executed code in the repo.

⚠ **Not covered by this gate:** phase 07-06 is parked in `stash@{0}` — uncommitted work is invisible
to a diff-based review and needs its own pass when unparked.

---

## Findings

### F-1 [HIGH] — Reverted pinned-proposal CSS was never re-landed · FIXED

`wwwroot/css/site-common.css` `.cutlab-proposal--pinned` (~:4854)

The sticky pinned proposal keeps `z-index: 19` with no `pointer-events`, so it intercepts pointer
input across its whole rectangle and blocks the workflow content beneath it.

**The commit archaeology is the finding:**

| Commit | CutLab.cshtml | ts-tests | e2e | cut-lab.ts | **site-common.css** |
|---|---|---|---|---|---|
| `d8dfa26a` fix | 70 | +42 | +6 | 4 | **+8/-1** |
| `27da7a5d` revert | 70 | −42 | −6 | 4 | **−8/+1** |
| `06b377d6` re-land | 70 | **+42** | **+6** | 4 | **+1 (comment only)** |

`06b377d6` restored the markup, the TS and all 48 lines of test — but not the CSS that made the fix
work. **48 lines of restored tests went green against the live defect**, because `pointer-events`
overlap is invisible to jsdom and the Playwright specs assert structure rather than clicking
*through* the sticky region.

### F-2 [HIGH — reported P2/MEDIUM, raised on verification] — Cut Lab overrides a documented a11y invariant · FIXED

`wwwroot/ts/cut-lab.ts:533-535` and `:2917`; contradicts `Views/Shared/_WorkflowStepTabs.cshtml:20-27`

`attachStepTabHandler` converted every `aria-disabled="true"` tab into a native `disabled` button.
The shared partial carries an explicit rationale comment against exactly this: a disabled button is
unfocusable and dead-ends arrow traversal, so state is carried by `aria-disabled` while the
capture-phase guard at `wwwroot/ts/site.ts:294-304` blocks activation.

That partial is shared by **five** tools (DeckComparison, CedhMetaGap, DeckPrimer, DeckAnalysis,
CutLab). Cut Lab was the only one overriding it, so a11y behavior differed per tool while the markup
looked identical.

⭐ **Compounding defect:** `cut-lab.ts:543-552` installed a capture-phase listener that fires only
when `event.isTrusted` is **false** — i.e. only for synthetic clicks. Added by `6d4de4ed` to
compensate for the native `disabled`. It made the tests pass while real keyboard users stayed locked
out: a code path that exists solely to satisfy tests.

⚠ Also at `:2917`, `setExportEnabled` toggled native `disabled` at runtime — so even removing the
init-time loop alone would have re-broken the tab once the deck reached 100 cards.

### F-3 [HIGH — reported P2, raised on verification] — Plan step renders "complete" over an empty panel · FIXED

`Views/Deck/CutLab.cshtml:22` declared `new WorkflowStepTab(3, "Plan", IsComplete: true)` — enabled
by default and marked complete — while `:1130-1131` is a literally empty `<section role="tabpanel">`.
The slot was deliberately reserved by `63eeec40` for Phase 8, which is planned but never executed.

User decision (2026-08-16): **disable the tab, do not remove it** — removing step 3 would renumber
Goals and Export and churn every step-index test, e2e selector and `data-cut-lab-step` attribute.

### F-4 [MEDIUM] — Successful what-if commit renders failure copy · OPEN

`Controllers/Api/CutLabApiController.cs:384` (client render `wwwroot/ts/cut-lab.ts:2626`)

A **successful** `POST /api/cut-lab/whatif/commit` returns `Patch = patch` without calling
`AddProposalGlance`, leaving `GlanceLine` empty — so a nonterminal next proposal displays
*"Couldn't recalculate this cut…"* after a recomputation that worked. The decide path shapes the
patch correctly at `:418`. A user-facing lie about success; one-line asymmetry.

### F-5 [MEDIUM] — All five tabpanels visible while one tab claims selection · OPEN

`Views/Deck/CutLab.cshtml:336`, `wwwroot/ts/cut-lab.ts:537`, `ts-tests/cut-lab-step-tabs.test.ts:55`

Server marks one tab `aria-selected="true"`, but no panel gets `hidden` and initialization does not
activate the selected tab. Keyboard and screen-reader users meet five visible `tabpanel`s until a tab
is clicked. **A test explicitly expects five visible panels** — the defect is locked in by assertion.

### F-6 [MEDIUM] — Nyx contrast fix left three other themes below the WCAG floor · OPEN

`efee8599` raised only `site-nyx.css`. Intake summaries use `--muted` on `--panel`; measured:

| Theme | Ratio | |
|---|---|---|
| Grixis (`#91889d`) | **3.75:1** | fail |
| Planeswalker Dark (`#8b92a8`) | **3.93:1** | fail |
| Jund (`#a08d7d`) | **4.34:1** | fail |

The new workflow-component contrast test exercises only classic, Azorius and Nyx — the test matrix
encodes the same blind spot as the fix. Guild themes are standalone forks, so every visual fix needs
a census across all 24.

### F-7 [LOW] — `nextProposal` declared non-null but serializes `null` · OPEN

`Models/Api/CutLabUiPatchDto.cs:28`, `Services/CutLab/CutLabUiPatchBuilder.cs:160`,
`wwwroot/ts/cut-lab.ts:174`. Adjusting a list that is not exactly 100 cards emits
`nextProposal: null` while the TS contract promises an object. Call sites guard defensively today,
so the compile-time contract is looser than reality rather than actively broken.

## Vacuous tests

- `DeckFlow.Web.Tests/CutLabFunctionalTwinsDensityTests.cs:134` — nested `Assert.DoesNotContain`
  executes zero times if the detector returns no findings. Mutation: make FunctionalTwins return
  `[]`; the test stays green.
- `:152` — `distinctEvidenceCount <= 40` passes with zero findings. Same mutation, same result.

⭐ **This is a pattern on this branch, not a slip.** Four vacuous gates were already found and
repaired by `8ea1e41d test(cut-lab): repair four workflow-ux gates that could not fail`; add the
`!event.isTrusted` shim (F-2), the 48 re-landed tests that couldn't see F-1, the fixture that
modelled markup production never emits (below), and F-5's test that asserts the bug. Tests on this
branch have repeatedly certified behavior they could not observe.

## Claims verified — what this gate positively closed

- **Combo protection is non-starving and agrees with `main`.** `CutLabCutRoundEngine.cs:256` builds
  eligibility independently of finding tallies; `:453` skips only the FunctionalTwins increment. A
  protected card with no other findings gets tally zero and stays a round-three candidate. Complete-
  piece demotion and second-pass rotation match `main`/`fix/cutlab-combo-seam`. **This is the seam
  that took four rounds on `main` and starved combo pieces twice — it is clean here.**
- **Combo-badge dictionary casing is correct on both sides.** Server rekeys normalized membership to
  every raw pool spelling under `StringComparer.Ordinal`; JS looks up with the same raw rendered
  name. DFC long/short forms and case-distinct keys covered by non-vacuous tests.
- **No get-only JSON trap.** All inspected Cut Lab request/response properties are `{ get; init; }`.
- **Feature gating complete.** All five `CutLabApiController` actions — decide, adjust,
  restart-rounds, what-if preview, what-if commit — carry `[FeatureFlagGate("tool.cut-lab.enabled")]`,
  and `ToolRouteGateCoverageTests`' sibling rule reaches them.
- **Layout CSS placement correct.** All new workflow/layout rules are in `site-common.css`; none
  leaked into `site.css`.
- **Remaining new tests are mutation-sensitive** — presenter, Razor render, wording, patch-builder,
  Playwright workflow and step-tab additions all have nonempty/exact-count prerequisites.

---

## Fixes applied

Branch **`fix/cycle21-blockers`**, worktree `deckflow-cycle21-fix`, branched off
`gsd/cycle21-cut-lab` at `a8e4dc00`. **Handed over as a patch — not committed onto the cycle21
agent's branch**, per the one-agent-owns-a-branch rule.

- **F-1** — restored `z-index: 1` + `pointer-events: none` + the interactive-child exception.
  ⭐ Verified faithful by blob hash: the resulting `site-common.css` is `a56e6643`, byte-identical to
  the post-image of the original `d8dfa26a`.
- **F-2** — removed the native-disabled loop, removed the `!event.isTrusted` shim, and stopped
  `setExportEnabled` toggling native `disabled`. The `type`/`form` binding was left untouched, as
  `_WorkflowStepTabs.cshtml:24-27` requires.
- **F-3** — `new WorkflowStepTab(3, "Plan", IsComplete: false, IsEnabled: false)`. Both flags flipped:
  leaving `IsComplete: true` would still emit `is-complete`, rendering a finished-looking step the
  user cannot open. Empty panel 3 retained because the partial emits `aria-controls` to it.
  Verified `_WorkflowStepTabs.cshtml:10-12` still selects the same server-side `currentStep`, so the
  no-JS keyboard entry point is unchanged.

**Three tests depended on the removed native-disabled behavior.** Codex rewrote two
(`cut-lab-step-tabs.test.ts`, `cut-lab-adjust.test.ts`); the third only surfaced by running the
suite: `cut-lab-proposal.test.ts:186` built its fixture with `disabled aria-disabled="true"`, markup
**the production partial never emits**. The old cleanup line masked it. Fixture corrected and both
assertions re-pointed at the real invariant — native `disabled` false in *both* directions while
`aria-disabled` carries the state.

⚠ **Codex could not verify its own work** — the worktree's `node_modules` was a WSL symlink, which
Windows `node`/`dotnet` cannot follow (`Cannot find module ...\typescript\bin\tsc`). Replaced with a
real Windows junction (`mklink /J`) and all suites were run by the lead.
