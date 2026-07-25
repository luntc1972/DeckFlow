---
status: resolved
trigger: "I'm down to 109 cards but card count shows 41 [Image #1]" (screenshot shows Export
panel: "Card count = 141" — user's typed "41" appears to be a truncation/typo of the
screenshot's "141"; treat 141 as the actual displayed value)
created: 2026-07-25T10:45:00-06:00
updated: 2026-07-25T11:40:00-06:00
---

## Current Focus
<!-- OVERWRITE on each update - always reflects NOW -->

hypothesis: CONFIRMED — real bug, category "stale render, needs reload/re-patch", not a
labeling ambiguity and not a double-counting/computation bug. The Export panel's
"Card count = N" status line (`Model.CurrentCount`, rendered at `CutLab.cshtml:874-882`) is
only recomputed on a full server round-trip (initial GET `/cut-lab`, or a full-page POST
fallback: `/cut-lab/export`, `/cut-lab/decide` no-JS, `/cut-lab/goals`, `/cut-lab/whatif`).
The fast in-page AJAX decide/adjust flow does compute a fresh, correct count on every
`/api/cut-lab/decide` / `/api/cut-lab/adjust` response (`CutLabUiPatchBuilder
.BuildWorkingListProjection`, `CutLabUiPatchBuilder.cs:329`, same
`CutLabWorkingList.Derive(...).Sum(card => card.Quantity)` formula used everywhere) and ships
it to the client as `CutLabUiPatch.currentCount` — but the client
(`applyServerPatch`/`patchStickyBar` in `cut-lab.ts`) only uses that value to update the
sticky bar's live counter (`getStickyCurrent()` → `stickyCurrent.textContent`,
`cut-lab.ts:2811-2813`) and to enable/disable the Export tab + submit button
(`setExportEnabled(patch.canBuildExport)`, `cut-lab.ts:2549-2561`, called from both
`patchStickyBar` and `applyServerPatch`). Nothing in the AJAX patch pipeline ever writes
`patch.currentCount` into the Export panel's own `.cutlab-export__status` text
(`CutLab.cshtml:874-882`). So after the user makes cuts/adjustments through the AJAX flow
without triggering a full page reload or an actual Export-tab click (which POSTs
`/cut-lab/export` and re-renders the whole page via `CutLabViewModel.From(...)`), the Export
panel keeps displaying whatever card count was present at the LAST full server render —
apparently 141 in the user's session — while the true current count (what the sticky bar
would show, and what a reload or a real export attempt would compute) is different. This is
the same architectural gap already documented in `.planning/debug/cutlab-decide-ajax.md`
Symptom 2 ("sections outside `applyServerPatch`'s covered set stay frozen until a full
reload"), recurring here in the Export panel's own status line instead of the
compare-to-baseline table.
test: DONE via static trace + existing e2e coverage (no live repro needed): traced
`CutLabViewModel.cs:293/309` (page-level derive+sum), `CutLabExportService.cs:55`
(export-service derive+sum — same function, confirms no double-counting/divergent-logic
possibility), `CutLabUiPatchBuilder.cs:321-341` (AJAX-patch derive+sum — same function again,
confirms the number sent to the client on every AJAX decide/adjust is correct), and
`cut-lab.ts:2549-2561, 2797-2836, 2838-2859` (confirms the client never applies
`patch.currentCount` to the Export panel's own status text, only to the sticky bar and the
tab-enabled state). Independently corroborated by
`DeckFlow.Web/e2e/cut-lab-export.spec.ts:116` and its own comment at lines 135-136 ("Before
the export POST the panel is still server-stale: the JS decision only unlocks the tab...") —
an existing, passing test that already documents (for the finished-list textareas) the exact
same "Export panel stays frozen until the export POST" behavior now shown to also apply to
the Card-count text. No test in `CutLabUiPatchBuilderTests.cs` or the e2e suite asserts that
`.cutlab-export__status` "Card count" updates after an AJAX-only accept/adjust — confirmed
test-coverage gap consistent with this being a real, previously undetected bug rather than
intended behavior.
expecting: N/A — root cause confirmed without needing the live user repro. (Original
hypothesis A — unique pool-row count vs quantity-sum — is NOT eliminated as a possible
secondary/contributing factor to the user's own mental "109" estimate, but it is NOT the
primary explanation: the code demonstrably has a real staleness defect independent of any
row-count-vs-quantity-sum semantics, and that defect alone fully explains an Export-panel
number that lags the true current state.)
next_action: Handed to Codex for fix per project convention (Claude investigates, does not
fix). Suggested fix direction for the fix author: extend the AJAX patch pipeline
(`applyServerPatch`/`patchStickyBar` in `cut-lab.ts`, backed by the already-correct
`patch.currentCount` field) to also update the Export panel's `.cutlab-export__status`
"Card count = N" text (and its ✅/❌ icon swap) on every decide/adjust response, mirroring how
`getStickyCurrent()` is patched today — so the Export panel never shows a number older than
the sticky bar's. Optionally also confirm with the user (not required to close this
investigation): "hard-reload the Export tab (or reopen /cut-lab) and tell me if the Card
count number changes" — if it changes, that is a live, independent confirmation of this root
cause with zero code changes needed to observe it.
reasoning_checkpoint: null
tdd_checkpoint: null

## Symptoms
<!-- Written during gathering, then immutable -->

expected: Export panel's "Card count" should equal the user's actual current pool size
(what they call "109 cards" — down from their original import size after making cuts).
actual: Export panel shows "Card count = 141" (screenshot), a 32-card discrepancy from the
user's expected 109.
errors: None reported; screenshot shows the standard "reach 100 cards" export-gate warnings
(color-identity/banlist/could-not-verify all "pending", i.e. export has never been
successfully built yet for this state).
reproduction: User's live Cut Lab session, mid-cutting (has made cuts down to what they
believe is 109 cards), viewing the Export tab. Not yet reproduced independently by Claude —
investigation is code-first (read CutLabWorkingList/CutLabExportService/CutLabViewModel
call graph) since a live browser repro isn't available in this session.
started: Noticed just now while user was testing (same session as today's Cut Lab work on
feat/cutlab-fixes); unknown whether this predates today's changes or not — today's sticky-bar
commit (2d47b756) touched CutLabViewModel.cs but did NOT change the currentCount computation
itself (only added it as a new parameter to BuildStickyBar) — needs confirming this is not a
regression from that commit before assuming pre-existing.

## Eliminated
<!-- APPEND only - prevents re-investigating after /clear -->

- hypothesis: (B, computation variant) `CutLabWorkingList.Derive` or its `Sum(card =>
  card.Quantity)` produces different results at different call sites (page render vs export
  vs AJAX patch), e.g. via a caching layer (`CutLabResolvedCardCache`) or double-application
  of `QuantityAdjustments`.
  evidence: All three call sites — `CutLabViewModel.cs:293/309`, `CutLabExportService.cs:55`,
  and `CutLabUiPatchBuilder.cs:323/329` — call the exact same static
  `CutLabWorkingList.Derive(pool, decisions, adjustments)` function with a fresh, request-local
  `state`/`pool` and independently `.Sum(card => card.Quantity)` the result; there is no shared
  mutable/memoized state between them. `CutLabResolvedCardCache` (confirmed by reading
  `CutLabExportService.cs:59-70,153-180`) only caches resolved Scryfall card data keyed by a
  pool-content hash (`ComputePoolKey`) for the analysis/legality pipeline — it plays no role in
  card-count arithmetic. `QuantityAdjustments` folding (`CutLabWorkingList.cs:32,123-151`) sums
  each card's adjustments exactly once per normalized name before applying to the base
  `card.Quantity`; no double-application path found.
  timestamp: 2026-07-25T11:15:00-06:00
- hypothesis: (A, as sole/primary explanation) The discrepancy is purely the user
  eyeballing unique pool rows (~109) vs the code correctly summing total quantity (141,
  inflated by stacked basic lands).
  evidence: Not eliminated as a contributing nuance to the user's own mental count, but ruled
  out as the PRIMARY explanation because a separate, confirmed code defect (Export panel
  frozen outside the AJAX patch pipeline — see Resolution) independently and fully explains a
  panel number that lags the true current state, regardless of how the user is counting.
  timestamp: 2026-07-25T11:35:00-06:00

## Evidence
<!-- APPEND only - facts discovered during investigation -->

- timestamp: 2026-07-25T10:44:00-06:00
  checked: CutLabViewModel.cs:308-310 (baselineCount/currentCount/lockedCount computation)
  and CutLab.cshtml Export block (~line 843-880, `var currentCount = Model.CurrentCount;`,
  `<strong>Card count = @currentCount</strong>`)
  found: `currentCount = derivedWorkingList.Sum(card => card.Quantity)` — sums QUANTITY, not
  row count. Commander pools are singleton except basics, which can carry quantity > 1 per
  row.
  implication: A "109 unique rows vs 141 total quantity" explanation is numerically
  plausible and would fully explain the gap without any code defect — needs confirming
  against the user's actual pool composition before concluding a bug exists.
- timestamp: 2026-07-25T10:44:30-06:00
  checked: CutLabExportService.BuildExportAsync ~line 55-60
  found: Export independently calls `CutLabWorkingList.Derive(state.Pool, state.Decisions,
  state.QuantityAdjustments)` — the SAME derivation function used by `CutLabViewModel.From()`
  for the page-level `currentCount`. Same inputs should yield the same sum; no separate
  export-specific quantity logic found yet.
  implication: If hypothesis (B) is correct, the divergence is more likely in WHICH `state`
  object reaches each call (stale snapshot) than in the derivation logic itself, since both
  paths share one derive function.
- timestamp: 2026-07-25T11:00:00-06:00
  checked: `CutLabViewModel.From` (CutLabViewModel.cs:282-374) — both `CurrentCount` and
  `StickyBar` (`BuildStickyBar(...)`, line 318) are built from the SAME `derivedWorkingList`
  / `result.State` within a single call to `From`.
  found: No cross-call staleness is possible WITHIN one full-page render — `CurrentCount` and
  the sticky bar's initial value always agree at render time because they share one
  `derivedWorkingList` computed once per `From()` invocation.
  implication: Any observed divergence must come from asking "when was the page/section last
  fully rendered", not from two different numbers being computed inconsistently in the same
  render pass. Points investigation toward the AJAX patch pipeline (what gets updated
  in-place vs what stays server-rendered-only) rather than the pure derivation math.
- timestamp: 2026-07-25T11:05:00-06:00
  checked: `.planning/debug/cutlab-decide-ajax.md` (prior root-caused session, same feature,
  same day)
  found: Symptom 2 in that file is an already-CONFIRMED root cause of the same shape: the
  AJAX decide-patch (`applyServerPatch` in `cut-lab.ts`) re-renders proposal, sticky bar,
  cuts-made, structural findings, quantity tuners, addable basics, and export-enabled state —
  but NOT the "Compare to baseline" table or goals baseline-trend, which stay frozen at
  page-load values until a full reload.
  implication: Directly suggests checking whether the Export panel's OWN card-count text
  (distinct from "export-enabled state", which IS patched) is on the frozen side of that same
  line.
- timestamp: 2026-07-25T11:10:00-06:00
  checked: `cut-lab.ts` — `applyServerPatch` (2838-2859), `patchStickyBar` (2797-2836),
  `setExportEnabled` (2549-2561), and every reference to `currentCount`/`canBuildExport`/
  `Card count`/`cutlab-export__status` in the file.
  found: `patch.currentCount` is used ONLY at `cut-lab.ts:2812`
  (`stickyCurrent.textContent = \`${patch.currentCount}/100 cards\`;`, the sticky bar).
  `patch.canBuildExport` is used ONLY to toggle the Export step-tab's `disabled`/
  `aria-disabled`/`is-disabled` state and the build-export submit button's `disabled` state
  (`setExportEnabled`). No code path anywhere writes to `.cutlab-export__status` or otherwise
  updates the "Card count = N" text inside the Export panel.
  implication: CONFIRMS the Export panel's own card-count text is never touched by the AJAX
  patch pipeline — it can only change on a full server-rendered page load.
- timestamp: 2026-07-25T11:20:00-06:00
  checked: `CutLabUiPatchBuilder.cs:321-341` (`BuildWorkingListProjection`) — the source of
  `patch.currentCount` sent to the client on every `/api/cut-lab/decide` and
  `/api/cut-lab/adjust` response.
  found: `int currentCount = workingList.Sum(card => card.Quantity);` where `workingList =
  CutLabWorkingList.Derive(state.Pool, state.Decisions, state.QuantityAdjustments)` — same
  function, same formula as the page-level and export-service call sites, computed fresh from
  the request's deserialized `state` every time.
  implication: The number the server COMPUTES and sends on every AJAX round-trip is correct
  and current; the defect is purely that the client never displays it in the Export panel's
  own status line (a missing DOM-patch target, not a computation or staleness-of-input bug).
- timestamp: 2026-07-25T11:25:00-06:00
  checked: `DeckFlow.Web/e2e/cut-lab-export.spec.ts` (existing, passing Playwright coverage)
  found: Line 116 asserts `Card count = 104` immediately after import (before any accepted
  cuts) — i.e. only validates the pre-cut, freshly-rendered value. The second test's own
  comment at lines 135-136 states: "Before the export POST the panel is still server-stale:
  the JS decision only unlocks the tab, so the finished-list textareas are absent and the
  patch is empty" — after driving several AJAX-accepted cuts (`cutToTarget`) down to exactly
  100, the test explicitly expects the Export panel's finished-list textareas to remain EMPTY
  and only becomes populated (and the "reach 100 cards" hint disappears, `Card count = 100`
  appears) AFTER `exportTab.click()` triggers the real `/cut-lab/export` full-page POST.
  implication: This is independent, already-existing, already-passing test evidence of the
  exact same "Export panel is server-stale until a real export POST" behavior for the
  finished-list content — it just wasn't previously connected to the Card-count TEXT staying
  stale too, because no assertion checks the Card-count text mid-flow (after some but not all
  AJAX cuts, before the export POST). Confirms this class of staleness is a known, accepted
  design property of the Export panel today, not something the test suite would have caught
  as wrong for the count line specifically — i.e. a genuine test-coverage gap around the
  Card-count text, not a passing regression test that would have caught this as "already
  fixed".
- timestamp: 2026-07-25T11:30:00-06:00
  checked: `CutLabExportService.cs` (`_resolvedCardCache` / `CutLabResolvedCardCache` usage,
  lines 27,39,59-70,153-180)
  found: `CutLabResolvedCardCache` is keyed by `ComputePoolKey(keptWorkingList)` (a hash of
  pool contents) and stores only `IReadOnlyList<ScryfallCardData>` (resolved card
  legality/identity data) for the analysis/color-identity pipeline. It is never read from or
  written to as part of computing a card COUNT.
  implication: Rules out `CutLabResolvedCardCache` as a source of stale-count behavior;
  confirms the earlier code-cache hypothesis in the original "next_action" is not the cause.

## Resolution
<!-- OVERWRITE as understanding evolves -->

root_cause: The Export panel's "Card count = N" status line (`Model.CurrentCount`,
`CutLab.cshtml:874-882`) is only recomputed on a full server-rendered page load (initial GET
`/cut-lab`, or a full-page POST fallback such as `/cut-lab/export`, `/cut-lab/decide`
no-JS, `/cut-lab/goals`, `/cut-lab/whatif` — all of which call
`CutLabViewModel.From(...)`). The fast in-page AJAX decide/adjust flow
(`/api/cut-lab/decide`, `/api/cut-lab/adjust`) DOES compute a fresh and correct count on
every response — `CutLabUiPatchBuilder.BuildWorkingListProjection`
(`CutLabUiPatchBuilder.cs:321-341`) derives the working list via the same
`CutLabWorkingList.Derive(state.Pool, state.Decisions, state.QuantityAdjustments)` used by
every other call site and sums it identically — but the client
(`cut-lab.ts`'s `applyServerPatch`/`patchStickyBar`) only applies that `patch.currentCount`
value to the sticky bar's live counter (`getStickyCurrent()`, `cut-lab.ts:2811-2813`) and
uses `patch.canBuildExport` only to enable/disable the Export tab and its submit button
(`setExportEnabled`, `cut-lab.ts:2549-2561`). Nothing in the AJAX patch pipeline writes the
current count into the Export panel's own `.cutlab-export__status` text. Consequently, after
a user makes AJAX-only cuts/adjustments (no full page reload, no actual Export-tab
submission), the Export panel keeps displaying the card count from the LAST full
server-rendered page — which is what the user's screenshot ("Card count = 141") captured —
while the true, current count is different (and is what the sticky bar / a reload / an
actual export attempt would show correctly). This is the same architectural gap already
identified in `.planning/debug/cutlab-decide-ajax.md` Symptom 2 (sections outside the AJAX
patch's coverage stay frozen until a full reload), recurring here in the Export panel's own
status text rather than the compare-to-baseline table. It is NOT a double-counting or
cross-call-divergent-derivation bug (all three call sites — page render, export service, and
AJAX patch builder — use the identical `CutLabWorkingList.Derive(...).Sum(Quantity)`
computation against a freshly-deserialized `state` every time) and it is NOT primarily the
unique-pool-row-count-vs-quantity-sum labeling ambiguity (that ambiguity is real and
independently documented but is not needed to explain the observed gap, since a confirmed
code defect already fully accounts for a stale Export-panel number).
fix: |
  Added `patchExportCountStatus` (cut-lab.ts), wired into `applyServerPatch` alongside
  `setExportEnabled`, rebuilding the `.cutlab-export__status` card-count block from
  `patch.currentCount` on every AJAX decide/adjust response. Applied by Codex (gpt-5.4,
  medium effort).
verification: |
  New vitest cases (cut-lab-proposal.test.ts) cover both boundary directions (N→100 and
  100→N); new e2e assertion (cut-lab-export.spec.ts) confirms the text updates with no page
  reload/export submit. Full vitest 112/112, e2e cut-lab-export.spec.ts 6/6, dotnet build
  clean. Blind cross-family verification: PASS. Committed d88c58c0.
files_changed: [DeckFlow.Web/wwwroot/ts/cut-lab.ts, DeckFlow.Web/Views/Deck/CutLab.cshtml, DeckFlow.Web/Models/CutLabViewModel.cs, DeckFlow.Web/ts-tests/cut-lab-proposal.test.ts, DeckFlow.Web/e2e/cut-lab-export.spec.ts]
