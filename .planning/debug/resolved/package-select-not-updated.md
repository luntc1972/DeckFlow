---
status: resolved
trigger: "after adding a package pool, the package assignment drop downs are not update with the new pool, keep using the cutlab-fixes branch"
created: 2026-07-25T09:30:00-06:00
updated: 2026-07-25T10:40:00-06:00
---

## Current Focus
<!-- OVERWRITE on each update - always reflects NOW -->

hypothesis: CONFIRMED. `select[data-cut-lab-package-card]` elements carry `data-df-select`
(CutLab.cshtml:431) and are enhanced by `df-select.ts` into a custom widget (visible
trigger button + `<ul>` listbox) that visually replaces the native `<select>` — the native
element is hidden off-screen via `.df-select__native` (site-common.css:1751,
`opacity:0; 1px x 1px; pointer-events:none`). `df-select.ts`'s `syncControllerOptions`
(the function that rebuilds the visible listbox from the native select's current
`<option>`s) only runs at widget creation (`renderSelect`, on page load) or when something
explicitly calls the exported `window.DeckFlow.refreshDfSelect(select)` hook
(df-select.ts:825-835). `addPackageOptionToSelects` (cut-lab.ts:3387-3402) and
`removePackageOptionFromSelects` (cut-lab.ts:3428-3438) mutate the native select's
`<option>`s directly (`select.add(...)`, `option.remove()`) but never call
`refreshDfSelect`/`attachDfSelect` afterward, so the change lands only on the invisible
native element — the visible custom listbox/trigger the user actually sees is never
rebuilt and keeps showing the stale option list until a full reload re-runs
`attachDfSelect` from scratch. This is not "never called" (the debug file's original
framing) and not the applyServerPatch reconciliation-gap pattern from
`cutlab-decide-ajax.md` either — it is a missing call to an existing, already-used
resync hook.
test: Confirmed by code reading + cross-file comparison (not runtime repro — sufficient
evidence found via static analysis, see Evidence). `content-kb.ts` (lines 60, 116) and
`deck-sync.ts` (lines 884, 1610) both correctly call
`window.DeckFlow?.refreshDfSelect?.(select)` after programmatically mutating a
`data-df-select` select's options — this is the established, correct pattern elsewhere in
the codebase. `cut-lab.ts` has zero occurrences of `refreshDfSelect` or `attachDfSelect`
(grep confirmed) despite three code paths that mutate `data-df-select` selects.
expecting: N/A — hypothesis confirmed via static evidence, not a live browser probe.
next_action: None for this investigation (goal = find_root_cause_only). Fix to be
dispatched to Codex per project convention.
reasoning_checkpoint: null
tdd_checkpoint: null

## Symptoms
<!-- Written during gathering, then immutable -->

expected: After creating a new package (via the "__new__" option on a card's package select,
naming it, and saving), every package-assignment dropdown on the page should immediately show
the new package as an option, without a page reload.
actual: New package does not appear in (some/all) package assignment dropdowns until the page
is fully reloaded.
errors: None observed in browser console or network tab.
reproduction: Add a new package pool via Cut Lab's package assignment UI. Without reloading,
check other cards' package dropdowns for the new package.
started: Always broken since the packages feature shipped — user also reports "most of the
rest of the page does not update when changes are made" (broader pattern, not package-select
specific — other UI sections likely also fail to refresh after AJAX/client mutations).

## Eliminated
<!-- APPEND only - prevents re-investigating after /clear -->

- Original leading hypothesis ("rows are inserted fresh by a server-rendered AJAX patch,
  e.g. applyServerPatch, without package options") — REFUTED. Grepped every occurrence of
  `data-cut-lab-package-card` (cut-lab.ts + CutLab.cshtml): the only place a
  `<select data-cut-lab-package-card>` is ever created is the server-rendered pool table
  (CutLab.cshtml:407-442), rendered exactly once at initial page load. Traced every function
  `applyServerPatch` calls (`patchStickyBar`, `renderRoundBanner`, `renderProposalCard`,
  `renderCutsMade`, `renderStructuralFindings`, `reconcileQuantityTuners`,
  `reconcileAddableBasics`, `renderWhatifSelectOptions`) — none of them create, replace, or
  touch a `select[data-cut-lab-package-card]` element. The pool table itself is never
  re-rendered or patched after load (confirmed: no `createElement('tr')` / innerHTML/
  insertAdjacentHTML touches the `conflicts-table`/pool table in cut-lab.ts). So this is not
  a "new DOM elements missing reconciliation" bug — the same `<select>` elements exist for
  the life of the page and DO receive the new `<option>` synchronously via
  `addPackageOptionToSelects`'s `document.querySelectorAll` loop.
- Same root cause as `cutlab-decide-ajax.md` Symptom 2 (compare-to-baseline table never
  re-rendered by `applyServerPatch`) — REFUTED as the mechanism for *this* bug, though both
  are same general class ("client state and rendered UI diverge"). This bug's specific
  mechanism is a missing df-select widget resync call, not a missing section re-render in
  `applyServerPatch`.

## Evidence
<!-- APPEND only - facts discovered during investigation -->

- timestamp: 2026-07-25T09:28:00-06:00
  checked: cut-lab.ts addPackageOptionToSelects (line 3388) and savePendingNewPackage (line 3397)
  found: addPackageOptionToSelects loops `document.querySelectorAll('select[data-cut-lab-package-card]')`
  at call time and adds an <Option> to each match. This IS called synchronously right after a
  package is created, so selects present in the DOM at that moment should update immediately.
  implication: The bug is not "the function never fires" — it is scoped to selects that don't
  exist yet at creation time (i.e. rows added to the DOM later by a server-rendered patch).
  [Note: this framing was later refined — see next entries. The native `<select>` DOES update;
  the visible custom widget built on top of it does not.]
- timestamp: 2026-07-25T09:29:00-06:00
  checked: prior debug session .planning/debug/cutlab-decide-ajax.md (Symptom 2 — Compare to
  baseline doesn't change after cutting)
  found: Same feature area (Cut Lab AJAX patching) has a documented, confirmed root cause where
  `applyServerPatch` re-renders some sections (proposal, sticky bar, cuts-made, quantity tuners,
  addable basics, export state, what-if options) but NOT others (compare-to-baseline table,
  goals baseline-trend) — those stay stale until full reload.
  implication: Strong prior for a systemic pattern: server-rendered patch fragments do not
  reconcile with client-only mutable state (created packages, in this case). User's broader
  complaint ("most of the rest of the page does not update") corroborates applyServerPatch has
  multiple known gaps, not just compare-to-baseline. [Superseded — see refutation above; the
  actual mechanism for the package-select bug is unrelated to applyServerPatch's section
  coverage gaps.]
- timestamp: 2026-07-25T09:40:00-06:00
  checked: grep for `data-cut-lab-package-card` across .ts/.cshtml/.cs and read every function
  applyServerPatch invokes (cut-lab.ts:2807-2830)
  found: Only one server-rendered location ever creates this select
  (CutLab.cshtml:407-442, inside the static pool `<table class="conflicts-table">`); no JS
  function ever creates or replaces a pool-table `<tr>`/`<select>` after load; applyServerPatch
  never touches the pool table.
  implication: The "new row inserted after load without package options" hypothesis is
  physically impossible for this feature — the select is one persistent DOM node per card,
  present at load and never re-created.
- timestamp: 2026-07-25T09:50:00-06:00
  checked: CutLab.cshtml:431 markup for the package select
  found: `<select name="Package_@card.Name" data-df-select data-cut-lab-package-card="@card.Name">`
  — the select carries `data-df-select`, marking it for enhancement by `df-select.ts`.
  implication: This select is not a plain native `<select>` in the rendered UI — it is
  replaced visually by a custom widget. Any programmatic mutation of its `<option>`s needs to
  go through df-select's resync path to be visible to the user.
- timestamp: 2026-07-25T09:55:00-06:00
  checked: DeckFlow.Web/wwwroot/ts/df-select.ts in full (846 lines)
  found: `attachDfSelect()` (line 813) enhances every `select[data-df-select]` on
  `DOMContentLoaded` by building a trigger button + `<ul class="df-select__listbox">` from the
  select's options (`renderSelect` → `syncControllerOptions`, lines 550-639, 641-778) and hides
  the native select (`select.classList.add('df-select__native')`, line 686). The listbox is
  only rebuilt again when `refreshDfSelect(select)` (line 825) is explicitly called — it looks
  up the tracked controller and calls `syncControllerOptions(controller)` again. Both
  `attachDfSelect` and `refreshDfSelect` are exported on `window.DeckFlow` (lines 837-839)
  specifically so other TS modules can call them after mutating a df-select's options.
  implication: There is a documented, intentional two-step contract: (1) mutate the native
  `<select>`'s options, (2) call `window.DeckFlow.refreshDfSelect(select)` so the visible
  widget picks up the change. Step 2 is the piece that must not be skipped.
- timestamp: 2026-07-25T09:58:00-06:00
  checked: DeckFlow.Web/wwwroot/css/site-common.css:1751 `.df-select__native` rule
  found: `position:absolute; inline-size:1px; block-size:1px; opacity:0; pointer-events:none;`
  implication: Confirms the native `<select>` is completely invisible/non-interactive to the
  user once enhanced — the user's entire experience of the dropdown is the custom
  `.df-select__trigger`/`.df-select__listbox` UI built by `syncControllerOptions`. Adding an
  `<option>` to the hidden native element with no resync is therefore invisible by
  construction, not just "easy to miss."
- timestamp: 2026-07-25T10:00:00-06:00
  checked: `grep -n "refreshDfSelect\|attachDfSelect" DeckFlow.Web/wwwroot/ts/cut-lab.ts`
  found: Zero matches.
  implication: cut-lab.ts never calls into the df-select resync API at all, despite owning
  three code paths that mutate `data-df-select` select elements:
  (1) `addPackageOptionToSelects` (cut-lab.ts:3387-3402) — adds a new `<option>` to every
  `select[data-cut-lab-package-card]` (all of which are `data-df-select`) when a package is
  created. THIS IS THE PRIMARY ROOT CAUSE for the reported symptom.
  (2) `removePackageOptionFromSelects` (cut-lab.ts:3428-3438) — removes an `<option>` when a
  package is deleted; same missing-resync defect, inverse direction.
  (3) `clearPendingNewPackageUi` (cut-lab.ts:3317-3334) — sets `select.value` directly
  (reverting an aborted "__new__" pick back to "Unlocked pool") without dispatching a native
  `change` event or calling refreshDfSelect, so the visible trigger label can also go stale
  here (minor, same class, not the reported symptom's main path).
  (4) `reconcileAddableBasics` (cut-lab.ts:2732-2778, via `createAddBasicForm` at
  2663-2714 and `replaceChildren(select, ...)` at 2770-2773) — creates a brand-new
  `select[data-df-select][data-cut-lab-add-basic-select]` element (or repopulates its
  options) as part of every `applyServerPatch` cycle, but never calls `attachDfSelect()` (to
  enhance a newly-created select) or `refreshDfSelect()` (to resync an already-enhanced one).
  This is a second, independently-broken instance of the same missing-call defect and is
  strong supporting evidence for the user's broader complaint ("most of the rest of the page
  does not update when changes are made") — it is a distinct symptom with the identical root
  cause class, in the same file.
  implication: Root cause confirmed at the code level with exact call sites. Fix is
  mechanical: call `window.DeckFlow?.refreshDfSelect?.(select)` (or `attachDfSelect()` for
  newly-created selects) after each of the four mutation sites above.
- timestamp: 2026-07-25T10:05:00-06:00
  checked: DeckFlow.Web/wwwroot/ts/content-kb.ts:60,116 and deck-sync.ts:884,1610
  found: Both existing, unrelated features in this same codebase correctly call
  `window.DeckFlow?.refreshDfSelect?.(select)` immediately after programmatically changing a
  `data-df-select` select's options — this is the established, already-proven-correct
  pattern for exactly this scenario elsewhere in the app.
  implication: The fix has a direct in-repo precedent to copy; this is not a novel pattern
  that needs to be invented, lowering fix risk/scope.
- timestamp: 2026-07-25T10:10:00-06:00
  checked: DeckFlow.Web/e2e/cut-lab-nav-themes.spec.ts:144-147 (existing e2e coverage of the
  "create package, assign a different card to it" flow)
  found: The test drives the interaction via Playwright's
  `page.locator('select[data-cut-lab-package-card="Arcane Signet"]').selectOption({ label:
  'Fast mana' })`. Playwright's `selectOption` sets the native `<select>`'s value directly via
  its DOM API and dispatches `input`/`change` on that native element — it does not click
  through the visible custom `.df-select__trigger` UI a real user would use.
  implication: Explains why this bug was never caught by the existing e2e suite —
  `selectOption` operates on the (correctly-updated) hidden native select and is blind to
  whether the visible custom widget was resynced. The bug is real for actual users clicking
  the rendered dropdown, but invisible to this style of Playwright interaction. This is a test
  coverage gap worth flagging to Codex alongside the fix (a real click-driven Playwright
  interaction on the visible trigger/listbox would have caught this).

## Resolution
<!-- OVERWRITE as understanding evolves -->

root_cause: |
  `select[data-cut-lab-package-card]` elements (CutLab.cshtml:431) carry `data-df-select` and
  are enhanced into a custom dropdown widget by df-select.ts on page load — the native
  `<select>` is then visually hidden (`.df-select__native`, site-common.css:1751:
  opacity 0 / 1px×1px / pointer-events none) and the user only ever sees/interacts with a
  custom `.df-select__trigger` button + `.df-select__listbox` list built once by
  `syncControllerOptions` (df-select.ts:550-639). That listbox is only rebuilt again when
  `window.DeckFlow.refreshDfSelect(select)` (df-select.ts:825-835) is explicitly called.
  cut-lab.ts's `addPackageOptionToSelects` (cut-lab.ts:3387-3402) and
  `removePackageOptionFromSelects` (cut-lab.ts:3428-3438) mutate the native select's hidden
  `<option>` list directly but never call `refreshDfSelect`/`attachDfSelect` afterward — so
  the change is applied but invisible until a full reload re-runs `attachDfSelect` from
  scratch and rebuilds the widget from fresh markup. `clearPendingNewPackageUi`
  (cut-lab.ts:3317-3334) has the same missing-resync gap when it sets `select.value` directly.
  A second, independently-broken instance of the identical missing-call defect exists in
  `reconcileAddableBasics` (cut-lab.ts:2732-2778 / `createAddBasicForm` 2663-2714), which
  creates/repopulates a different `data-df-select` element (`cut-lab-add-basic-select`) on
  every `applyServerPatch` cycle without ever calling `attachDfSelect()`/`refreshDfSelect()` —
  this is likely (not yet separately confirmed at runtime) part of the user's broader "most of
  the rest of the page does not update" complaint. Two other established features in the same
  codebase (content-kb.ts:60,116 and deck-sync.ts:884,1610) already call
  `window.DeckFlow?.refreshDfSelect?.(select)` correctly after mutating a df-select's options,
  confirming this is a known, solved pattern that cut-lab.ts simply never adopted. The existing
  e2e coverage (cut-lab-nav-themes.spec.ts:144-147) does not catch this because Playwright's
  `selectOption()` manipulates the hidden native `<select>` directly rather than clicking
  through the visible custom widget.
fix: |
  Added `window.DeckFlow?.refreshDfSelect?.(select)` (and, for the add-basic select,
  `attachDfSelect?.()`) calls at all 4 confirmed sites — `addPackageOptionToSelects`,
  `removePackageOptionFromSelects`, `clearPendingNewPackageUi`, `reconcileAddableBasics` —
  mirroring the existing correct pattern in content-kb.ts/deck-sync.ts. Applied by Codex
  (gpt-5.4, medium effort) via strict TDD: new Playwright e2e test written and confirmed RED
  first, then fix applied, then confirmed GREEN.
verification: |
  New e2e test "shows a newly created package in another card's visible package widget
  without reload" (cut-lab-nav-themes.spec.ts) — RED before fix on both desktop+mobile
  Playwright projects (listbox missing the new package option), GREEN after (2 passed).
  ts-tests: 29/29 files + 110/110 tests passed. `dotnet build`: 0 errors, 0 warnings.
  Line endings verified unchanged (LF, both files): `git diff --stat` == `git diff
  --ignore-all-space --stat` (34 insertions, no churn); `grep -c $'\r'` = 0 before and after
  on both touched files.
  Full `cut-lab-*.spec.ts` e2e suite: 37 passed, 4 failed — ALL PRE-EXISTING and unrelated:
  caused by HEAD commit `48daa680` ("fix(cut-lab): correct role classification (MDFC lands,
  draw, other)", already landed on this branch before this debug session started) adding a
  new "Other" role-group fallback bucket. `cut-lab-structure.spec.ts:124` (expected 8 role
  groups, found 15) and two `cut-lab-tuning.spec.ts` assertions are stale goldens for that
  earlier commit, not this fix — confirmed because neither failing spec file appears in this
  fix's diff. Flagged as a separate pre-existing follow-up, not blocking.
files_changed: [DeckFlow.Web/wwwroot/ts/cut-lab.ts, DeckFlow.Web/e2e/cut-lab-nav-themes.spec.ts]
