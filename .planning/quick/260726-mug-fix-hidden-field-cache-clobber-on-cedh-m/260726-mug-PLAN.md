---
phase: quick-260726-mug
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - DeckFlow.Web/wwwroot/ts/deck-sync.ts
  - DeckFlow.Web/ts-tests/cedh-meta-gap-hidden-field-persistence.test.ts
autonomous: true
requirements: [QUICK-260726-MUG-01]

must_haves:
  truths:
    - "A freshly server-rendered WorkflowStep hidden field is NOT reverted to a stale sessionStorage value on page load"
    - "A freshly server-rendered FetchedEntriesJson hidden field is NOT reverted to a stale sessionStorage value on page load"
    - "A freshly server-rendered MetaGapPromptText hidden field is NOT reverted to a stale sessionStorage value on page load"
    - "Ordinary user-input fields (e.g. CommanderName) still hydrate from the sessionStorage cache as before"
  artifacts:
    - path: "DeckFlow.Web/wwwroot/ts/deck-sync.ts"
      provides: "nonPersistedFieldNames exclusion set covering the three server-computed hidden fields"
      contains: "MetaGapPromptText"
    - path: "DeckFlow.Web/ts-tests/cedh-meta-gap-hidden-field-persistence.test.ts"
      provides: "vitest regression test proving the clobber no longer occurs"
      min_lines: 25
  key_links:
    - from: "DeckFlow.Web/wwwroot/ts/deck-sync.ts serializePersistedFormFields/restoreFormFields"
      to: "nonPersistedFieldNames"
      via: "Set.has(key) early-return guard"
      pattern: "nonPersistedFieldNames\\.has"
---

<objective>
Stop the generic `data-cache-key` form-state cache in `deck-sync.ts` from clobbering three
authoritative, server-computed hidden fields on the cEDH Meta Gap page (and the two sibling
prompt pages that share the `WorkflowStep` field name).

Purpose: the cache captures form state on `pagehide` — i.e. *before* a POST's response is
rendered — then restores it unconditionally on the next load. Any hidden field whose value is
computed by the server therefore gets silently reverted to a stale pre-submit snapshot. This is
the same confirmed bug class already fixed for `HistoryJson` on Deck History
(`.planning/debug/resolved/deck-history-page-bugs.md`), whose own blind-spots section explicitly
flagged these three fields as the remaining exposure.

Output: three field names added to the existing `nonPersistedFieldNames` exclusion Set, plus a
permanent vitest regression test mirroring the Deck History one.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.planning/debug/resolved/deck-history-page-bugs.md
@DeckFlow.Web/wwwroot/ts/deck-sync.ts
@DeckFlow.Web/ts-tests/deck-history-hidden-field-persistence.test.ts

<interfaces>
<!-- Contracts the executor needs. No codebase exploration required. -->

Current exclusion set — DeckFlow.Web/wwwroot/ts/deck-sync.ts:512-514
  const formStateStoragePrefix = 'decksync-form-state-';
  const antiForgeryFieldName = '__RequestVerificationToken';
  const nonPersistedFieldNames = new Set([antiForgeryFieldName, 'HistoryJson']);

Guard sites that consume the Set (both already exist; no new call sites needed):
  - serializePersistedFormFields (deck-sync.ts:531-552) — `if (nonPersistedFieldNames.has(key)) return;`
  - restoreFormFields (deck-sync.ts:790) — same guard on restore

Boot path exercised by the test: module import -> attachGenericPersistedForms (deck-sync.ts:952)
-> hydrateFormState (927) -> restoreFormFields (790).

sessionStorage keys used by the cache (prefix + form's data-cache-key):
  'decksync-form-state-prompt-cedh-meta-gap'
  'decksync-form-state-prompt-cedh-meta-gap:savedAt'   (millisecond epoch string)
Stored value shape: Record<string, string[]>  (each field name -> array of values)

Target form + fields — DeckFlow.Web/Views/Deck/CedhMetaGap.cshtml:47,58,59,60,118
  <form ... data-cache-key="prompt-cedh-meta-gap" data-prompt-cedh-form ...>
  <input type="hidden" name="WorkflowStep"       value="@currentStep" data-prompt-cedh-workflow-step />
  <input type="hidden" name="FetchedEntriesJson" value="@Model.Request.FetchedEntriesJson" />
  <input type="hidden" name="MetaGapPromptText"  value="@Model.PromptText" />
  <input ... name="CommanderName" value="@Model.Request.CommanderName" />   (real user input — must still hydrate)

Shared field name (one exclusion covers all three pages):
  WorkflowStep — CedhMetaGap.cshtml:58, DeckAnalysis.cshtml:107, DeckComparison.cshtml:189
  FetchedEntriesJson / MetaGapPromptText — CedhMetaGap.cshtml only

Why excluding WorkflowStep is safe: no TypeScript reads the persisted value by name. The hidden
input is written client-side only by showPromptStep (deck-sync.ts:1292-1296),
showPromptComparisonStep (1715) and showPromptCedhStep (1864), each of which resolves the input
via its `data-prompt-*-workflow-step` attribute and derives the step from the server-rendered
`data-prompt-*-current-step` form attribute plus in-page navigation. The value is therefore always
re-derived on load; it is never sourced from sessionStorage.
</interfaces>

<project_gotchas>
- WSL `dotnet` is NOT on PATH. Use the absolute Windows path: `/mnt/c/Program Files/dotnet/dotnet.exe`.
- vitest runs from `DeckFlow.Web/`: `npx vitest run` (config: `DeckFlow.Web/vitest.config.ts`, jsdom, include `ts-tests/**/*.test.ts`).
- LF line endings are enforced by `.gitattributes`. Both touched files currently have 0 CR bytes — keep it that way.
- `DeckFlow.Web/wwwroot/js/*.js` is gitignored compiled output. Never stage compiled JS.
- Do NOT touch CutLab's `CutLabStateJson` — explicitly out of scope for this task.
</project_gotchas>
</context>

<tasks>

<task type="auto" tdd="true">
  <name>Task 1: Add regression test, then exclude the three server-computed hidden fields</name>
  <files>DeckFlow.Web/ts-tests/cedh-meta-gap-hidden-field-persistence.test.ts, DeckFlow.Web/wwwroot/ts/deck-sync.ts</files>
  <behavior>
    New test file `DeckFlow.Web/ts-tests/cedh-meta-gap-hidden-field-persistence.test.ts`, modelled
    line-for-line on the existing `deck-history-hidden-field-persistence.test.ts` (same imports,
    same `afterEach` teardown resetting mocks, `document.body.innerHTML`, localStorage and
    sessionStorage).

    Single test: "does not restore stale WorkflowStep/FetchedEntriesJson/MetaGapPromptText while
    still hydrating other fields".
    - Arrange: seed `sessionStorage['decksync-form-state-prompt-cedh-meta-gap']` with a JSON
      Record<string,string[]> holding STALE values — WorkflowStep `['1']`,
      FetchedEntriesJson `['[{"stale":true}]']`, MetaGapPromptText `['stale prompt']`,
      CommanderName `['Stale Commander']`. Seed the companion
      `...:savedAt` key with `Date.now().toString()` so the cache is considered fresh.
    - Arrange: render a form with `data-cache-key="prompt-cedh-meta-gap"` containing three hidden
      inputs (WorkflowStep=`3`, FetchedEntriesJson=`[{"fresh":true}]`, MetaGapPromptText=`fresh
      prompt`) plus a visible `CommanderName` input holding `Fresh Commander`, simulating the page
      as it renders immediately after a successful POST. Do NOT add `data-prompt-cedh-form` to the
      form — the test targets the generic persistence engine in isolation, exactly as the Deck
      History test does.
    - Act: `vi.resetModules()` then `await import('../wwwroot/ts/deck-sync')` to trigger boot-time
      attachGenericPersistedForms -> hydrateFormState.
    - Assert (the three regressions): each hidden input still holds its FRESH value — `3`,
      `[{"fresh":true}]`, `fresh prompt`.
    - Assert (the control, proving the cache still works): `CommanderName` has been hydrated from
      the cache to `Stale Commander`, i.e. the exclusion is narrow and normal user-input
      restoration is untouched.

    Test must FAIL before the deck-sync.ts edit (on all three hidden-field assertions) and PASS
    after it.
  </behavior>
  <action>
    Write the test file first and confirm it fails for the stated reason (the three hidden fields
    come back holding the stale cached values), then apply the one-line production fix.

    Production fix — DeckFlow.Web/wwwroot/ts/deck-sync.ts line 514: extend the existing
    `nonPersistedFieldNames` Set literal with the three additional field-name strings
    `WorkflowStep`, `FetchedEntriesJson` and `MetaGapPromptText`, following the exact pattern of the
    existing `HistoryJson` entry. Keep the `antiForgeryFieldName` const reference as the first
    element. Add a short `// Why:` comment above the Set explaining that these names are
    server-computed authoritative state (not recoverable user input), so restoring them from a
    pagehide-time snapshot silently reverts the freshly rendered response; cite
    `.planning/debug/resolved/deck-history-page-bugs.md` as the precedent.

    Do NOT change serializePersistedFormFields, restoreFormFields, hydrateFormState or any other
    function — the existing `nonPersistedFieldNames.has(...)` guards at both the serialize and the
    restore site already do the work. Do NOT add a per-field or per-form special case. Do NOT touch
    any `.cshtml` view, any C# file, or CutLab's `CutLabStateJson`.

    Line endings: both touched files are LF with 0 CR bytes. Preserve each file's existing endings
    exactly — do not convert LF to CRLF, do not normalize or reflow untouched lines. Change only
    the lines whose content actually changes; every other line must stay byte-for-byte identical.
    The new test file must be written with LF endings.
  </action>
  <verify>
    <automated>cd DeckFlow.Web &amp;&amp; npx vitest run ts-tests/cedh-meta-gap-hidden-field-persistence.test.ts ts-tests/deck-history-hidden-field-persistence.test.ts</automated>
    <automated>grep -v '^\s*//' DeckFlow.Web/wwwroot/ts/deck-sync.ts | grep -c "nonPersistedFieldNames = new Set" | grep -q '^1$' &amp;&amp; grep -q "'MetaGapPromptText'" DeckFlow.Web/wwwroot/ts/deck-sync.ts &amp;&amp; grep -q "'FetchedEntriesJson'" DeckFlow.Web/wwwroot/ts/deck-sync.ts &amp;&amp; grep -q "'WorkflowStep'" DeckFlow.Web/wwwroot/ts/deck-sync.ts</automated>
    <automated>test "$(grep -c $'\r' DeckFlow.Web/wwwroot/ts/deck-sync.ts)" = "0" &amp;&amp; test "$(grep -c $'\r' DeckFlow.Web/ts-tests/cedh-meta-gap-hidden-field-persistence.test.ts)" = "0"</automated>
  </verify>
  <done>
    New test passes; the pre-existing Deck History persistence test still passes; the three field
    names appear exactly once each in the `nonPersistedFieldNames` Set; both touched files still
    have 0 CR bytes.
  </done>
</task>

<task type="auto">
  <name>Task 2: Run the full project gates (build, full vitest suite, format, EOL churn)</name>
  <files>(no file changes — verification only)</files>
  <action>
    Run the project's standing Definition-of-Done gates against the change and report each result
    explicitly in the SUMMARY. If any gate fails, fix the cause inside the two files already
    touched by Task 1 — do not widen scope to other files.

    1. Solution build must be clean (0 errors, 0 new warnings). The TypeScript compile runs as part
       of the MSBuild `CompileTypeScriptAssets` target, so this is also the tsc strict-mode gate.
    2. Full vitest suite from `DeckFlow.Web/` must be green (previous baseline: 111/111 before this
       change; expect 112 with the new test).
    3. Changed-lines format gate must be clean.
    4. EOL churn check: `git diff --stat` must match `git diff --ignore-all-space --stat` for the
       touched files, and each touched file's CR count must match its committed counterpart
       (`git show HEAD:<path> | grep -c $'\r'`), which is 0 for `deck-sync.ts`.
    5. Confirm no compiled output under `DeckFlow.Web/wwwroot/js/` is staged (it is gitignored).

    Do not commit unless the developer has been asked to test first, per project commit conventions;
    report gate results and stop.
  </action>
  <verify>
    <automated>"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln</automated>
    <automated>cd DeckFlow.Web &amp;&amp; npx vitest run</automated>
    <automated>bash scripts/format-check-changed.sh ci</automated>
    <automated>diff &lt;(git diff --stat) &lt;(git diff --ignore-all-space --stat)</automated>
    <automated>git status --porcelain DeckFlow.Web/wwwroot/js | grep -q . &amp;&amp; exit 1 || exit 0</automated>
  </verify>
  <done>
    Build clean (0 errors / 0 new warnings); full vitest suite green including the new test;
    format-check-changed clean; whitespace-insensitive diff identical to the plain diff (no EOL
    churn); nothing from `wwwroot/js/` appears in git status.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| browser sessionStorage -> form field values | Client-controlled storage rehydrating form inputs that are later POSTed to the server |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-MUG-01 | Tampering | `restoreFormFields` rehydrating `FetchedEntriesJson` / `MetaGapPromptText` from sessionStorage | mitigate | This change removes those fields from the restore path entirely, so their values now come only from the server-rendered response. Strictly reduces client-controllable input surface. |
| T-MUG-02 | Tampering | Server-side trust in POSTed hidden fields | accept | Unchanged by this task — the fields were already client-submittable and are validated/clamped server-side (`Math.Clamp(Model.Request.WorkflowStep, 1, 3)`); no new exposure introduced. |
| T-MUG-SC | Tampering | npm/NuGet installs | accept | No new packages are added by this plan; nothing installed, so the legitimacy gate does not apply. |
</threat_model>

<verification>
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln` — 0 errors, 0 new warnings
- `cd DeckFlow.Web && npx vitest run` — full suite green, new regression test included
- `bash scripts/format-check-changed.sh ci` — clean
- LF preserved (0 CR bytes) on both touched files; no whitespace/EOL churn in the diff
- Exactly two files changed; no `.cshtml`, no C#, no `CutLabStateJson`
</verification>

<success_criteria>
- `nonPersistedFieldNames` in `deck-sync.ts` contains `__RequestVerificationToken`, `HistoryJson`,
  `WorkflowStep`, `FetchedEntriesJson`, `MetaGapPromptText` — nothing else added
- `DeckFlow.Web/ts-tests/cedh-meta-gap-hidden-field-persistence.test.ts` exists and proves all three
  fresh hidden-field values survive a stale cache while `CommanderName` still hydrates from cache
- Deck History's existing persistence regression test still passes (no behavior regression on the
  shared engine)
- All gates in `<verification>` pass and are reported explicitly in the SUMMARY
</success_criteria>

<output>
Create `.planning/quick/260726-mug-fix-hidden-field-cache-clobber-on-cedh-m/260726-mug-SUMMARY.md` when done
</output>
</content>
</invoke>
