---
slug: deck-history-version-replaces
status: resolved
trigger: "I am not able to add a new version of a deck to a saved file on deck version"
created: 2026-08-05
updated: 2026-08-05
goal: find_root_cause_only
---

# Debug Session: deck-history-version-replaces

## Symptoms

**Expected behavior**
Adding a new version of a deck to an already-saved Deck History entry should APPEND a
new snapshot/version to that deck's version list, leaving prior versions intact and
diffable against the new one.

**Actual behavior**
The new version becomes the ONLY version. Prior versions are not retained (or not
displayed) after the add.

**Repro**
Deck History page, "add version" on an existing saved deck. User is not fully certain
they are driving the flow correctly — the UI affordance for "add version to THIS deck"
vs "save a new deck" may itself be unclear.

**User hypothesis (from reporter)**
"Part of the problem could be fixed by not loading the previous deck" — i.e. the deck
input is pre-populated with the previously saved decklist, so the user may be re-saving
the same content, or the pre-load may be participating in the overwrite.

**Errors**
None reported. No visible error message; the save appears to succeed.

**Environment**
Production — https://www.deckflow.gg. Deck History feature flag ON in prod since
2026-07-17.

**Timeline**
Unknown whether it ever worked. Reporter unsure. Feature has been live since
2026-07-17, so a long-standing latent defect is plausible (cf. the deck-history
download button, which was a no-op from launch — `.planning/debug/deck-history-download-noop.md`).

## Current Focus

status: ROOT CAUSE CONFIRMED (diagnose-only session — no fix applied per session constraints)

hypothesis: CONFIRMED — see Resolution.root_cause below.
next_action: none — RESOLVED. Fix shipped to main in `4a4cca61` (fix(deck-history): warn when
  a version silently starts a new history). `DeckHistoryPageService.cs:205` captures
  `startedNewHistory` before the `??=` fills `file` in; it flows to `DeckHistoryViewModel`
  and renders a caveat at `DeckHistory.cshtml:133`, guarded by 4 tests. Status corrected
  2026-08-17 — this doc still read `diagnosed` months after the fix landed.

reasoning_checkpoint:
  hypothesis: |
    Deck History has NO server-side persistence for a deck's version history at all
    (verified: no DbContext/table anywhere in the repo; the page's own copy says
    "DeckFlow never stores your history"). The ONLY way prior versions carry forward
    into a new "Update history" POST is via the hidden `HistoryJson` form field
    (DeckHistory.cshtml:61) being echoed back into the same still-open results page,
    or the user manually re-selecting their previously-downloaded .json file in the
    `history-file` input. On 2026-07-26 (commit c6b64f5f6), `HistoryJson` was
    deliberately EXCLUDED from deck-sync.ts's generic sessionStorage form-state cache
    to fix a different bug (stale HistoryJson clobbering a freshly-rendered response).
    That fix was correct for its own symptom, but its side effect is that HistoryJson
    now ALWAYS resets to empty on any page reload / navigate-away-and-back / new tab
    (before the fix, sessionStorage gave an imperfect but non-empty carry-forward).
    When a returning user lands on a blank `/deck-history` page (Index() renders
    `new DeckHistoryRequest()` — DeckHistoryController.cs:28) and imports their deck
    again WITHOUT re-uploading the old file, `DeckHistoryPageService.ProcessAsync`
    (DeckHistoryPageService.cs:190-213) hits `file is null` + `load is not null` and
    silently calls `DeckHistoryAppender.CreateNew(...)` — a brand-new file starting
    at version 1 — with no warning, no confirmation, and a success message
    ("Started a new history — version 1 saved.", DeckHistoryViewModel.cs:135-137)
    that is textually indistinguishable from a legitimate first-time save. The user
    then downloads/views this and perceives their prior versions as "replaced",
    because from their perspective they were continuing the same deck.
    This is primed by a second, independent gap: `deck-input-store.ts`'s
    `deckflow.last-deck` sessionStorage key is NOT scoped to Deck History — it
    auto-restores the last deck URL/text entered in ANY DeckFlow tool into the
    (empty) DeckUrl/DeckText fields on load. This makes a fresh page look like it
    "remembered" the user's deck, reinforcing the false belief that history also
    carried forward, when only the deck input did. This matches the reporter's own
    stated hypothesis ("not loading the previous deck") almost exactly, even though
    the mechanism is the reverse of what they guessed (the deck IS being reloaded
    from an unrelated cache, but the history is NOT).
  confirming_evidence:
    - "DeckHistoryController.cs:28 — `Index() => HistoryView(new DeckHistoryRequest(), null);` — GET always renders a fully empty request; nothing restores prior HistoryJson."
    - "No DbContext, migration, or table exists anywhere in the repo for deck history (`grep -rln DbContext` repo-wide returns nothing); DeckHistory.cshtml:45 explicitly states 'DeckFlow never stores your history — download the file, keep it with your deck.'"
    - "deck-sync.ts:539-546 `nonPersistedFieldNames` includes 'HistoryJson' (added by commit c6b64f5f6, 2026-07-26, verified via `git log -S nonPersistedFieldNames`), and its own code comment (lines 533-538) states the field is deliberately excluded from the generic sessionStorage cache."
    - "git show c6b64f5f6: before this commit, HistoryJson was NOT excluded and WAS captured/restored by the generic cache — the diff adds it to the exclusion set for the first time. Both this commit and the download-noop fix (0926bb8ec) are confirmed ancestors of origin/main via `git merge-base --is-ancestor`, i.e. both are live in production today (2026-08-05), 10 days before this report."
    - "DeckFlow.Web.Tests/DeckHistoryPageServiceTests.cs:22-48 `ProcessAsync_DeckOnly_CreatesNewFileAppendsSnapshotAndLeavesPromptEmpty` proves — and asserts as INTENDED behavior — that a deck-only submission with no HistoryJson always produces a single-version file. No test exists asserting a warning/confirmation is surfaced when this branch fires for a deck that plausibly already has history (e.g. same deck name previously seen)."
    - "DeckHistoryPageService.cs:195-213: `file ??= DeckHistoryAppender.CreateNew(...)` only reachable when `file` was never populated (no HistoryJson resolved) — confirms the branch, and DeckHistoryAppender.cs:65-81 `Append` is provably additive (only ever `.Append()`s to `file.Versions`, never truncates) when `file` IS populated, ruling out a destructive-merge bug in the Core layer itself."
    - "DeckHistorySerializer.cs Parse/NormalizeVersions: no truncation of `Versions` on parse (only id-repair reordering) — ruling out an upload-path parsing bug as an alternative explanation."
    - "deck-input-store.ts:1-69 `deckflow.last-deck` sessionStorage key is set/read globally (not namespaced per tool) and DeckHistory.cshtml's DeckUrl/DeckText fields participate in `attachSplitFields()` like every other deck-input tool — confirms the cross-tool auto-restore that primes the false 'it remembered my deck' impression."
  falsification_test: |
    Would be falsified if: (a) HistoryJson survived a plain page reload/back-forward
    navigation in current production code (it does not — deliberately excluded since
    c6b64f5f6), or (b) DeckHistoryAppender ever discarded existing Versions when given
    a non-null `file` (it does not — Append only appends), or (c) the "Started a new
    history" success message differentiated a true first save from a state-loss
    fresh-start (it does not — same string either way).
  fix_rationale: |
    Not applying a fix this session (Codex out of credits, diagnose-only per
    session constraints). See "Fix Specification" below for the intended repair,
    aimed at the actual defect (silent, undifferentiated fresh-start) rather than
    re-adding the old stale-clobber bug.
  blind_spots: |
    - Not verified live in a browser (session constraints: no browser on Windows
      host; Playwright headless run was not executed this session — recommend
      running scripts/run-web-test.sh + headless Playwright before/after any fix
      to visually confirm the "Started a new history" success banner appears when
      a returning user forgets to re-upload).
    - Did not verify whether ASP.NET Core's default form-value length limits could
      ever silently truncate a very large HistoryJson hidden field for decks with
      many accumulated versions (no evidence found or expected to be relevant;
      flagged only as a low-probability alternative not fully excluded).
    - Reporter's exact repro steps were not confirmed interactively (symptoms were
      prefilled from a prior gathering pass) — root cause is inferred from precise
      code-level tracing plus a directly relevant, dated, already-resolved sibling
      debug session, not from a live-reproduced session recording.
  candidate_causes:
    - "code: deck-sync.ts c6b64f5f6 excludes HistoryJson from the only implicit cross-navigation carry-forward path that existed (category: code)"
    - "design/product: Deck History has zero server-side persistence by design — continuity depends entirely on either staying on one page or manually re-uploading a downloaded file, with no in-app reminder/gate for the latter (category: config/design)"
    - "code: DeckHistoryPageService.ProcessAsync has no branch/warning distinguishing 'legitimate first save' from 'accidental fresh start because prior history wasn't supplied' (category: code)"
    - "code: deck-input-store.ts's unscoped deckflow.last-deck cache auto-fills DeckUrl/DeckText across tools, priming a false sense that the page 'remembered' full context including history (category: code — contributing/priming factor, not required for the core failure)"
  and_gate: |
    Yes — the user-visible failure requires multiple independent conditions to hold
    together: (1) the user must reach the page in a state where HistoryJson is empty
    (fresh load/reload/new tab — guaranteed since 07-26), AND (2) the user must not
    manually re-upload their previously-downloaded file, AND (3) the server must
    silently accept this as a valid "start new" with no warning (true in all cases
    today). All three are independently necessary; none alone reproduces "prior
    versions disappear" — e.g. condition (1) alone, immediately followed by a
    correct manual re-upload, produces no bug at all. Recorded as a joined
    root_cause below per the AND-gate branching guidance.

## Evidence

- timestamp: 2026-08-05T00:05:00Z
  checked: DeckFlow.Core/History/DeckHistoryAppender.cs (Append, BuildSnapshot, CreateNew)
  found: |
    Append() only ever does `file.Versions.Append(candidate...)` — provably additive,
    never truncates or replaces an existing Versions list. Dedup guard (IsIdentical)
    only blocks adding an exact-duplicate snapshot; it never removes prior versions.
  implication: The Core append algorithm itself is not the defect — rules out a
    destructive-merge bug in the pure domain logic.

- timestamp: 2026-08-05T00:10:00Z
  checked: DeckFlow.Web/Controllers/DeckHistoryController.cs, DeckFlow.Web/Services/DeckHistoryPageService.cs
  found: |
    There is no server-side store for deck history at all — GET Index() always
    renders `new DeckHistoryRequest()`. The entire feature is stateless: history
    round-trips solely through the hidden `HistoryJson` form field
    (DeckHistoryRequest.cs:32) echoed back by DeckHistoryViewModel.From (line 121:
    `HistoryJson = result.SerializedJson ?? request.HistoryJson`), or via the
    `historyFile` upload input on the "Update history" form.
  implication: Continuity across "add a version" actions depends entirely on either
    (a) staying on the same already-rendered results page (server keeps echoing the
    hidden field), or (b) the user manually re-selecting their downloaded .json file.
    Any other path back to the page starts from zero.

- timestamp: 2026-08-05T00:15:00Z
  checked: DeckFlow.Web/wwwroot/ts/deck-sync.ts lines 531-546, 860-1029; git show c6b64f5f6; git log -S nonPersistedFieldNames
  found: |
    `HistoryJson` was added to `nonPersistedFieldNames` (excluded from the generic
    data-cache-key sessionStorage persistence) by commit c6b64f5f6 on 2026-07-26,
    to fix a real, separately-confirmed bug (see
    .planning/debug/resolved/deck-history-page-bugs.md) where a fresh server
    response's HistoryJson was being silently reverted to a stale pre-submit
    snapshot restored from sessionStorage. Before that commit, HistoryJson WAS
    captured/restored by the generic cache (imperfectly, but non-empty across a
    same-tab reload/navigate-back). Confirmed via `git merge-base --is-ancestor
    c6b64f5f6 origin/main` that this exclusion is live in production today
    (2026-08-05), 10 days before the current report.
  implication: The 07-26 fix correctly solved its own symptom but removed the only
    (imperfect) implicit safety net for a returning user who does not manually
    re-upload their file — converting a "sometimes stale" experience into an
    "always empty" one for any page reload / new tab / navigate-away-and-back.
    This is a plausible proximate trigger for the reported regression, timed
    consistently with a report 10 days after the fix shipped.

- timestamp: 2026-08-05T00:20:00Z
  checked: DeckFlow.Web/Views/Deck/DeckHistory.cshtml lines 55-113 (main form), 115-137 (success/warning banners), 193-210 (compare-only mini form)
  found: |
    The main "Update history" form's hidden HistoryJson (line 61) round-trips the
    server-rendered value correctly. The success banner text (lines 118-124) reads
    identically ("Started a new history — version 1 saved." /
    DeckHistoryViewModel.cs:135-137) whether this is a genuine first-ever save or an
    accidental fresh start after state loss — no distinguishing warning exists. The
    separate "Compare versions" mini-form (lines 193-210) carries only HistoryJson +
    TargetAiPlatform + version-id selects, not DeckUrl/DeckText/DeckName/Notes/Label
    — ruled out as a source of confusion since it cannot itself trigger a
    deck-import/append path.
  implication: No UI signal exists anywhere to warn the user that an "Update
    history" submission is about to start a disconnected new file rather than
    extend an existing one.

- timestamp: 2026-08-05T00:25:00Z
  checked: DeckFlow.Web/wwwroot/ts/deck-input-store.ts (full file)
  found: |
    The `deckflow.last-deck` sessionStorage key is global/unscoped — set/read by
    every DeckFlow tool with a DeckUrl/DeckText pair (Manabase, Cut Lab, Deck
    History, etc. all share `attachSplitFields()`). On any fresh Deck History page
    load with empty DeckUrl/DeckText, this auto-fills the last deck URL/text used
    ANYWHERE in the app, unconditionally (only guarded by "only if currently
    empty", not by which tool last set it).
  implication: A returning user sees the deck input auto-populated (looks like "the
    page remembered me") while HistoryJson is silently blank — directly matching
    the reporter's own hypothesis about deck pre-load participating in the
    overwrite, though the actual causal direction is that the deck input auto-fills
    while the (unrelated) history does not, creating a false sense of continuity.

- timestamp: 2026-08-05T00:30:00Z
  checked: DeckFlow.Web.Tests/DeckHistoryPageServiceTests.cs lines 22-48
  found: |
    `ProcessAsync_DeckOnly_CreatesNewFileAppendsSnapshotAndLeavesPromptEmpty` locks
    in "deck import with no HistoryJson => brand-new single-version file" as
    INTENDED, tested behavior. No test exists for "deck import with no HistoryJson
    but a plausible pre-existing deck (e.g. same DeckName) => warn the user".
  implication: This is by design for a true first-time save, but the same code path
    is indistinguishable from — and silently fires for — the "forgot to re-upload"
    case. The gate that "should have caught" this class of defect (a UAT/UX review
    of the returning-user flow) never existed; only the first-time-save path was
    ever tested.

- timestamp: 2026-08-05T00:32:00Z
  checked: DeckFlow.Core/History/DeckHistorySerializer.cs (Parse, NormalizeVersions)
  found: Parse never truncates `Versions`; only repairs corrupt/unhealthy ids by
    renumbering in date order. Upload size cap is 1 MB (MaxUploadBytes), well above
    any realistic multi-version Commander deck history.
  implication: Rules out an upload/parsing-path defect as an alternative explanation
    for lost versions when the user DOES correctly re-upload their file.

## Eliminated

- hypothesis: DeckHistoryAppender.Append() (or RecomputeDeltas) destructively
    replaces the Versions list instead of appending, when given a valid existing
    `file`.
  evidence: Append() body is `file.Versions.Append(candidate with { Id = nextId })`
    — additive only; RecomputeDeltas rebuilds deltas in place without dropping any
    version. No code path removes an entry from `file.Versions`.
  timestamp: 2026-08-05T00:35:00Z

- hypothesis: DeckHistorySerializer.Parse() truncates/loses versions when parsing
    a correctly re-uploaded history .json file.
  evidence: NormalizeVersions only repairs ids (renumbers on corrupt/duplicate/
    out-of-order ids); it never drops entries from `file.Versions`.
  timestamp: 2026-08-05T00:36:00Z

- hypothesis: The "Compare versions" mini-form is being submitted by mistake in
    place of the main "Update history" form, causing an apparent data loss.
  evidence: The mini-form (DeckHistory.cshtml:193-210) carries no
    DeckUrl/DeckText/DeckName/Notes/Label fields at all — it cannot trigger a
    deck-import/append path, only a re-render of the existing HistoryJson with a
    different compare-pair selection. Submitting it cannot itself remove versions.
  timestamp: 2026-08-05T00:37:00Z

- hypothesis: The Deck History download (file the user is expected to re-upload
    next time) is still broken in production, so users never have a valid file to
    re-upload, forcing every session into a fresh start.
  evidence: `git merge-base --is-ancestor 0926bb8ec origin/main` confirms the
    download-button fix (form-owner resolution) is deployed on production main as
    of this session — the previously-tracked deck-history-download-noop bug is
    resolved and live, not a currently-contributing factor.
  timestamp: 2026-08-05T00:38:00Z

## Resolution

root_cause: |
  Deck History has no server-side persistence for a deck's saved version history
  (`HistoryJson`) — it exists only inside a single rendered page response, echoed
  through a hidden form field, by explicit design ("DeckFlow never stores your
  history"). Root cause is the AND of three independently-necessary gaps, all live
  in production as of this session:
  (1) deck-sync.ts commit c6b64f5f6 (2026-07-26, deployed) correctly fixed a stale-
      clobber bug by excluding `HistoryJson` from the generic sessionStorage
      form-state cache — but the side effect is that HistoryJson now ALWAYS resets
      to empty on any page reload, new tab, or navigate-away-and-back, with no
      substitute carry-forward mechanism (deck-sync.ts:539-546).
  (2) DeckHistoryPageService.ProcessAsync has no signal distinguishing "legitimate
      first-time save" from "accidental fresh start because the prior history file
      was not re-uploaded" — both produce an identical
      `DeckHistoryAppender.CreateNew(...)` → single-version file →
      "Started a new history — version 1 saved." success message
      (DeckHistoryPageService.cs:195-213; DeckHistoryViewModel.cs:135-137).
  (3) deck-input-store.ts's cross-tool, unscoped `deckflow.last-deck` sessionStorage
      key auto-fills the DeckUrl/DeckText fields on a fresh page load (from the last
      deck used in ANY DeckFlow tool), while HistoryJson stays blank — creating a
      false impression that "the page remembered my deck" and priming the user to
      submit an "Update history" that silently starts a disconnected new file.
  A user who updates their deck days/weeks after their last visit — the feature's
  own normal use case — will very likely land on a fresh page load (not the
  still-open results page from last time), see their deck URL apparently
  "remembered", import the current decklist, and get a brand-new single-version
  history with no warning that their prior versions were not loaded, matching the
  reported symptom exactly ("the new version becomes the ONLY version").
fix: |
  NOT APPLIED this session (Codex out of credits until 2026-08-10; session is
  diagnose-only per explicit constraints). See "Fix Specification" in the final
  report returned to the caller for the intended repair: (a) detect and surface a
  clear warning/confirmation when a deck import is about to start a new file with
  no prior HistoryJson present, rather than silently succeeding with an ambiguous
  message; (b) reinforce the "re-upload your file first" instruction at the point
  of failure, not just in first-visit help text; (c) scope or otherwise account for
  deck-input-store.ts's cross-tool auto-restore so it does not misleadingly imply
  history continuity on Deck History specifically. Do not simply re-add HistoryJson
  to the generic sessionStorage cache — that reintroduces the already-fixed
  stale-clobber bug (.planning/debug/resolved/deck-history-page-bugs.md).
verification: |
  Fix implemented 2026-08-05 on branch `fix/deck-history-silent-fresh-start` (user
  explicitly authorized Claude to write the production code, overriding the
  Codex-writes-code rule, because Codex credits do not reset until 2026-08-10).
  Approach: address gap (2) directly with a `StartedNewHistory` flag plus two
  server-rendered notices, and address gap (3)'s SYMPTOM server-side rather than
  touching the cross-tool `deck-input-store.ts` / `deck-sync.ts` restore. Gap (1) is
  deliberately left alone — re-adding HistoryJson to the sessionStorage cache would
  reintroduce the already-fixed stale-clobber bug.
  Evidence:
  - TDD: tests written first, RED confirmed (CS1061/CS0117 on the missing member).
  - Mutation-tested both directions — forcing the flag to `false` fails
    ProcessAsync_DeckOnly_CreatesNewFileAppendsSnapshotAndLeavesPromptEmpty; forcing it
    to `true` fails ProcessAsync_HistoryAndDeckAtTwoVersions_BuildsPrompt. The guards
    provably bite.
  - Full suite: 4749 xUnit passed / 0 failed / 20 skipped; 123 vitest passed.
    Solution builds with 0 errors (CS8629 warnings are the pre-existing baseline).
  - Live headless Playwright at 1280x900 and 390x844: the no-history notice renders on
    a fresh GET and disappears after submit; the fresh-start caveat renders in the
    success banner; no horizontal overflow at either viewport. Local feature flag was
    forced On for the run and restored to Off afterwards.
  - format-check-changed gate exit 0; no EOL or whitespace churn.
  NOT yet done: user UAT, and push.
files_changed:
  - DeckFlow.Web/Services/DeckHistoryPageService.cs
  - DeckFlow.Web/Models/DeckHistoryViewModel.cs
  - DeckFlow.Web/Views/Deck/DeckHistory.cshtml
  - DeckFlow.Web/wwwroot/css/site-common.css
  - DeckFlow.Web.Tests/DeckHistoryPageServiceTests.cs
  - DeckFlow.Web.Tests/DeckHistoryViewRenderTests.cs
  - README.md

## Follow-ups raised by the /simplify review (NOT done here)

1. `deck-input-store.ts`'s `deckflow.last-deck` sessionStorage key is unscoped across
   every deck tool, while `deck-sync.ts` already has a per-tool `data-cache-key`
   scoping mechanism and a generalized `nonPersistedFieldNames` exclusion list. That
   asymmetry is the real architectural cause. Scoping `deckflow.last-deck` per
   `data-cache-key` is a small change — the attribute is already on every consuming
   form — and would close it properly.
2. Cut Lab (`CutLabStateJson`) and cEDH Meta-Gap (`WorkflowStep` /
   `FetchedEntriesJson` / `MetaGapPromptText`) have the identical shape: a hidden
   server-computed field excluded from the cache while the deck fields restore. Needs
   a census for the same silent-reset-reads-as-success risk. Only Deck History was
   fixed here.
3. Every `*ViewRenderTests.cs` builds a fresh ServiceCollection + Razor view engine per
   test; no shared `IClassFixture` exists anywhere in the test project. A shared
   cached provider would cut render-test cost suite-wide. Pre-existing, not caused by
   this change.

## Constraints on this session

- Cross-AI: Codex is OUT OF CREDITS until 2026-08-10 09:48. Per project rules Codex
  writes production code; therefore this session is DIAGNOSE-ONLY. Produce a root
  cause and a fix specification (file:line + intended behavior + required tests).
  Do NOT apply production code edits without explicit user authorization.
- Prod reads only via Render MCP; never write to prod.
- Deck History snapshots are persisted JSON — treat any schema/serialization change as
  a backward-compatibility risk against already-saved user data.
