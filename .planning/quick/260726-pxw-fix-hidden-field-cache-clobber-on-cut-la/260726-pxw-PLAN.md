---
phase: 260726-pxw
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Web/ts-tests/cut-lab-hidden-field-persistence.test.ts
  - DeckFlow.Web/wwwroot/ts/deck-sync.ts
autonomous: true
requirements: [QUICK-PXW-01]
must_haves:
  truths:
    - "A stale sessionStorage cache for the cut-lab form no longer overwrites the server-rendered CutLabStateJson hidden field on page load"
    - "Normal user-input fields on the same cut-lab form still hydrate from the sessionStorage cache"
    - "No other page's form persistence behavior changes"
  artifacts:
    - path: "DeckFlow.Web/ts-tests/cut-lab-hidden-field-persistence.test.ts"
      provides: "Regression test proving CutLabStateJson survives a stale cache restore"
      contains: "data-cache-key=\"cut-lab\""
    - path: "DeckFlow.Web/wwwroot/ts/deck-sync.ts"
      provides: "nonPersistedFieldNames exclusion for CutLabStateJson"
      contains: "'CutLabStateJson'"
  key_links:
    - from: "DeckFlow.Web/wwwroot/ts/deck-sync.ts"
      to: "nonPersistedFieldNames"
      via: "serializePersistedFormFields + restoreFormFields early-return guard"
      pattern: "nonPersistedFieldNames\\.has"
---

<objective>
Fix the third confirmed instance of the hidden-field cache-clobber bug class: Cut Lab's
server-authoritative `CutLabStateJson` hidden field is captured on `pagehide` and restored
unconditionally on load by the generic `data-cache-key` form persistence in
`deck-sync.ts`, silently reverting Cut Lab session state to a stale pre-submit snapshot.

Purpose: keep the server's computed Cut Lab state authoritative across navigations, the same
way `HistoryJson` (Deck History) and `WorkflowStep`/`FetchedEntriesJson`/`MetaGapPromptText`
(cEDH Meta Gap) were already fixed.

Output: one new vitest regression test plus a one-line addition to the `nonPersistedFieldNames`
Set in `deck-sync.ts`.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/debug/resolved/deck-history-page-bugs.md
@DeckFlow.Web/ts-tests/cedh-meta-gap-hidden-field-persistence.test.ts
@DeckFlow.Web/wwwroot/ts/deck-sync.ts

<verified_facts>
Confirmed during planning — do NOT re-derive these, they are already checked:

1. `nonPersistedFieldNames` is declared at `DeckFlow.Web/wwwroot/ts/deck-sync.ts:520-526`:
   `antiForgeryFieldName`, `'HistoryJson'`, `'WorkflowStep'`, `'FetchedEntriesJson'`,
   `'MetaGapPromptText'`. It is consulted on the capture side at line 552
   (`serializePersistedFormFields`) and on the restore side at line 806
   (`restoreFormFields`) — a single Set governs both directions, so one entry fixes both.

2. Storage keys are `decksync-form-state-<cacheKey>` and `decksync-form-state-<cacheKey>:savedAt`
   (prefix `formStateStoragePrefix` at line 512). For Cut Lab the cache key is `cut-lab`, so the
   test must seed `decksync-form-state-cut-lab` and `decksync-form-state-cut-lab:savedAt`.

3. `CutLab.cshtml` has EXACTLY ONE `CutLabStateJson` input inside the `data-cache-key="cut-lab"`
   form: the form spans lines 126-229 and the field is at line 134. The other seven
   `CutLabStateJson` inputs (lines 41, 67, 835, 896, 1086, 1186, 1375) live in separate sibling
   forms (`/cut-lab/decide`, `/cut-lab/adjust`, `/cut-lab/goals`, `/cut-lab/export`,
   `/cut-lab/whatif`, `/cut-lab/restart-rounds`, and the tuner add-basic form) that carry no
   `data-cache-key`, so persistence never touches them. No view change is needed.

4. Repo-wide grep confirms `CutLabStateJson` appears only in Cut Lab code paths
   (CutLab controllers/models/services/tests/e2e, `CutLab.cshtml`, `wwwroot/ts/cut-lab.ts`).
   No other `data-cache-key` form uses that field name, so the exclusion is Cut-Lab-scoped.

5. Fields available on the cut-lab form for the "normal input still hydrates" assertion:
   `DeckInputSource` (select), `DeckUrl` (input), `DeckText` (textarea), `PrimaryPlan` (textarea).
   Use `DeckUrl` — plain text input, simplest jsdom assertion.

6. Prior analogous fix (`1ea6b89d`) touched only the test file, then the source file. No README
   or view changes were part of that fix; do not add any here.
</verified_facts>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Add failing regression test for cut-lab hidden-field clobber</name>
  <files>DeckFlow.Web/ts-tests/cut-lab-hidden-field-persistence.test.ts</files>
  <behavior>
    - Seed `sessionStorage['decksync-form-state-cut-lab']` with
      `{ CutLabStateJson: ['{"stale":true}'], DeckUrl: ['https://archidekt.com/decks/stale'] }`
      and `sessionStorage['decksync-form-state-cut-lab:savedAt']` = `Date.now().toString()`.
    - Render a form with `data-cache-key="cut-lab"` containing
      `<input type="hidden" name="CutLabStateJson" value='{"fresh":true}' />` and
      `<input name="DeckUrl" value="https://archidekt.com/decks/fresh" />`.
    - `vi.resetModules()` then `await import('../wwwroot/ts/deck-sync')` to trigger boot-time
      form-state hydration.
    - Assert `CutLabStateJson` still equals `{"fresh":true}` (server value survives).
    - Assert `DeckUrl` equals the STALE cached value — proving normal cache hydration is intact
      and the test is not passing for the trivial reason that hydration never ran.
  </behavior>
  <action>
    Create `DeckFlow.Web/ts-tests/cut-lab-hidden-field-persistence.test.ts` as a direct structural
    mirror of `DeckFlow.Web/ts-tests/cedh-meta-gap-hidden-field-persistence.test.ts`: same imports
    (`afterEach, describe, expect, it, vi` from `vitest`), same `afterEach` teardown
    (`vi.restoreAllMocks()`, `vi.unstubAllGlobals()`, clear `document.body.innerHTML`, clear
    `localStorage` and `sessionStorage`), one `describe` / one `it`.

    Name the suite `cut lab hidden field persistence` and the case
    `does not restore stale CutLabStateJson while still hydrating other fields`.

    Use cache key `cut-lab` (NOT `prompt-cut-lab`) — verified fact 2 above. Write the file with LF
    line endings only; `.gitattributes` enforces LF and the repo must stay at 0 CR bytes.

    Run the test and confirm it FAILS on the `CutLabStateJson` assertion (observed value will be
    the stale `{"stale":true}`), for exactly the reason described. Do not proceed to Task 2 until
    the failure is observed and matches that reason. Commit the failing test on its own, mirroring
    prior commit `1ea6b89d`: `test(quick-260726-pxw): add failing test for cut lab hidden field cache clobber`.

    Scope fence: create only this one file. Do not edit `deck-sync.ts`, `CutLab.cshtml`, any other
    test, or the README in this task.
  </action>
  <verify>
    <automated>cd DeckFlow.Web &amp;&amp; npx vitest run ts-tests/cut-lab-hidden-field-persistence.test.ts</automated>
  </verify>
  <done>Test file exists, runs, and fails with `CutLabStateJson` reported as `{"stale":true}` instead of `{"fresh":true}`; the `DeckUrl` assertion passes. Failing test committed alone.</done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: Exclude CutLabStateJson from generic form persistence</name>
  <files>DeckFlow.Web/wwwroot/ts/deck-sync.ts</files>
  <behavior>
    - `ts-tests/cut-lab-hidden-field-persistence.test.ts` goes green.
    - `ts-tests/deck-history-hidden-field-persistence.test.ts` and
      `ts-tests/cedh-meta-gap-hidden-field-persistence.test.ts` stay green.
    - No other vitest test regresses.
  </behavior>
  <action>
    In `DeckFlow.Web/wwwroot/ts/deck-sync.ts`, add `'CutLabStateJson',` to the
    `nonPersistedFieldNames` Set literal at lines 520-526, following the existing entries
    (`'MetaGapPromptText'`) with identical quoting, 2-space indentation and trailing comma.

    This is a one-line addition. Do NOT refactor the Set, do not rename it, do not change
    `serializePersistedFormFields` or `restoreFormFields`, and do not touch any other file
    (no view change is needed — verified fact 3).

    Preserve the file's existing LF line endings exactly; change only the inserted line and leave
    every other line byte-for-byte identical.

    Then run the gates below. If `git diff --stat` and `git diff --ignore-all-space --stat` report
    different totals for the touched files, line endings were churned — restore LF with
    `sed -i 's/\r$//' &lt;path&gt;` before committing.

    Commit as `fix(quick-260726-pxw): stop form cache clobbering cut lab state`.
  </action>
  <verify>
    <automated>cd DeckFlow.Web &amp;&amp; npx vitest run</automated>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln</automated>
    <automated>scripts/format-check-changed.sh staged</automated>
    <automated>git diff --stat; git diff --ignore-all-space --stat; grep -c $'\r' DeckFlow.Web/wwwroot/ts/deck-sync.ts DeckFlow.Web/ts-tests/cut-lab-hidden-field-persistence.test.ts</automated>
  </verify>
  <done>Full vitest suite green (including the new test and both prior hidden-field tests); `dotnet build DeckFlow.sln` clean with no new warnings; format-check clean; `git diff --stat` equals `git diff --ignore-all-space --stat`; both touched files report 0 CR bytes.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| browser sessionStorage → form DOM | Client-controlled cached values are written back into form fields, then POSTed to the server |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-PXW-01 | Tampering | `restoreFormFields` writing stale `CutLabStateJson` into the cut-lab form | mitigate | Add `'CutLabStateJson'` to `nonPersistedFieldNames` so the server-rendered value is never replaced from client cache |
| T-PXW-02 | Spoofing | `__RequestVerificationToken` persistence | accept | Already excluded via `antiForgeryFieldName`; unchanged by this plan |
| T-PXW-03 | Tampering | server-side trust of posted `CutLabStateJson` | accept | Pre-existing design — Cut Lab state is already validated/parsed server-side in `CutLabController`; this plan does not widen that surface |
| T-PXW-SC | Tampering | npm/dotnet installs | mitigate | No new packages added; if any install is proposed, stop and ask the user (project rule) |
</threat_model>

<verification>
1. `cd DeckFlow.Web && npx vitest run` — full suite green, including
   `cut-lab-hidden-field-persistence`, `deck-history-hidden-field-persistence`,
   `cedh-meta-gap-hidden-field-persistence`, and all `cut-lab-*` tests.
2. `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` — clean, no new warnings.
3. `scripts/format-check-changed.sh staged` — clean (no C# changed, so trivially so).
4. EOL: `git diff --stat` totals equal `git diff --ignore-all-space --stat` totals; `grep -c $'\r'`
   returns 0 for both touched files.
5. Exactly two files changed: the new test and `deck-sync.ts`. No view, README, or other source
   file touched.
</verification>

<success_criteria>
- Fresh server-rendered `CutLabStateJson` survives a stale `decksync-form-state-cut-lab` cache.
- Normal cut-lab form fields (`DeckUrl` et al.) still restore from cache.
- Two commits: failing test, then fix — matching the pattern of `1ea6b89d`.
- All gates in `<verification>` pass.
</success_criteria>

<output>
Create `.planning/quick/260726-pxw-fix-hidden-field-cache-clobber-on-cut-la/260726-pxw-SUMMARY.md` when done
</output>
