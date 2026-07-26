---
status: resolved
trigger: "on the deck history page start over doesn't clear out everthing is is it supposed to, also the restore of deck doesn't work quite right, and when importing the deck it a warning is displayed deck size as 141 which is 100 main and then 41 sideboard"
created: 2026-07-26
updated: 2026-07-26
---

# Debug Session: deck-history-page-bugs

## Symptoms

Three related issues reported on the Deck History page (`/deck-history`):

1. **Start Over does not fully reset state.**
   - Expected: clicking "Start Over" should reset everything — deck list, session/snapshot state, any cached comparison data, URL params — back to blank page.
   - Actual: some state persists after Start Over (user unsure exactly which fields/state survive).

2. **Restore of a deck doesn't work quite right.**
   - Expected: unknown/not yet verified by user — needs investigation of what "restore" is supposed to do (likely restoring a previously saved deck-history snapshot).
   - Actual: user has not pinned down exact wrong behavior yet ("not sure, test restore") — debugger should exercise the restore flow and characterize the actual vs expected behavior as part of investigation.

3. **Import warning shows deck size 141 (100 main + 41 sideboard).**
   - Expected: unclear whether sideboard cards should count toward the "deck size" warning total at all, or whether 100 main + 41 sideboard is a legitimate deck that shouldn't trigger a size warning.
   - Actual: warning fires showing combined count of 141, mixing main (100) and sideboard (41).
   - User's open question: is the sideboard being saved/persisted along with the 100-card main deck (i.e., is 141 the correct total for what's stored), or is the warning wrongly including sideboard cards in a "deck size" check that should only look at the main deck?

## Additional Context

- Errors: none observed by user; no console/network errors reported. Silent/wrong-behavior only.
- Timeline: unknown — user not sure if this is a long-standing issue or a recent regression (recent deck-history/parser/cutlab work has touched related code recently per project history).
- No repro steps beyond "use the Deck History page: import a deck, note the 141-card warning, then Start Over, then try Restore."

## Current Focus

reasoning_checkpoint:
  hypothesis: |
    Three independent, confirmed root causes:
    (1) Start Over anchor on DeckHistory.cshtml is missing `data-clear-cache`,
        so neither of the page's two sessionStorage caches gets cleared.
    (2) The generic `data-cache-key="deck-history"` form-state cache
        (deck-sync.ts hydrateFormState/restoreFormFields) unconditionally
        restores ALL named fields on every page load, including the hidden
        `HistoryJson` field -- which is authoritative server-computed
        round-trip state, not user-recoverable input. Because persistence
        also fires on `pagehide` (captured pre-navigation, i.e. before a
        POST's response is rendered), each page load can silently clobber
        the freshly-rendered HistoryJson with a stale pre-submit copy.
    (3) DeckHistoryPageService.ProcessAsync's 100-card warning counts all
        entries "not maybeboard" (i.e. mainboard + commander + sideboard),
        while the snapshot that actually gets saved
        (DeckHistoryAppender.BuildSnapshot) only ever includes
        mainboard + commander. A 100-main + 41-sideboard deck is legit but
        triggers a false "141 cards" warning.
  confirming_evidence:
    - "DeckHistory.cshtml Start Over anchor: `<a href=\"~/deck-history\" class=\"clear-cache-button\" role=\"button\" data-no-busy>` -- no `data-clear-cache`. CutLab.cshtml:226 and Manabase.cshtml:232 use the identical `<a href=... class=\"clear-cache-button\" role=\"button\" data-clear-cache data-no-busy>` pattern with the attribute present."
    - "deck-sync.ts attachGenericPersistedForms only wires a clear-cache click handler to `form.querySelector('[data-clear-cache]')` -- absent on DeckHistory, so `clearPersistedFormState` never runs, and deck-input-store.ts's document-level `[data-clear-cache]` listener (clearLastDeck) never fires either."
    - "Empirically reproduced via a jsdom vitest scratch test (removed after use): seeded sessionStorage `decksync-form-state-deck-history` with a stale HistoryJson (1 version), rendered a fresh form with a HistoryJson hidden field holding 2 versions (simulating the page right after a successful Update-history POST), imported deck-sync.ts to trigger its boot-time hydrateFormState. Result: the hidden field's value was overwritten back to the 1-version stale copy. Test failed the assertion expecting the fresh 2-version value, proving the clobber."
    - "DeckHistoryPageService.cs:180-186 -- `count = load.Entries.Where(e => !Equals(e.Board,\"maybeboard\")).Sum(Quantity)`; DeckHistoryAppender.cs:31-47 BuildSnapshot doc comment: \"mainboard entries become cards; maybeboard and sideboard entries are dropped\" -- the warning filter and the actual persisted snapshot use different board sets."
  falsification_test: |
    (1) would be falsified if the anchor already had data-clear-cache (it doesn't).
    (2) would be falsified if the jsdom test showed the fresh HistoryJson value
        survived hydrate (it didn't -- test above proves the clobber).
    (3) would be falsified if DeckHistoryAppender also included sideboard cards
        in the saved snapshot (its own doc comment says it explicitly drops them).
  fix_rationale: |
    (1) Add `data-clear-cache` to the anchor, matching the established
        CutLab/Manabase pattern exactly -- makes Start Over actually clear
        both sessionStorage caches instead of leaving them to repopulate the
        "fresh" page.
    (2) Exclude the `HistoryJson` field name from deck-sync.ts's generic
        serialize/restore, mirroring the existing antiForgeryFieldName
        exclusion. This is the field uniquely responsible for the clobber and
        is unique to this one page/form, so the fix has no effect on any
        other `data-cache-key` form (CutLab, DeckAnalysis, etc. keep their
        current behavior). Addresses the root cause (authoritative
        server-state being treated as recoverable user input) rather than
        papering over a symptom.
    (3) Change the warning's Sum filter to match BuildSnapshot's board set
        (mainboard + commander) instead of "not maybeboard" -- warning will
        now report exactly the count that gets saved, matching what "100
        cards" is supposed to mean for this feature.
  blind_spots: |
    - Did not fix the analogous hidden-field-clobber risk on CutLab
      (CutLabStateJson), DeckAnalysis/DeckComparison/CedhMetaGap
      (WorkflowStep/FetchedEntriesJson/MetaGapPromptText) -- same generic
      engine, same class of risk, but out of scope for this DeckHistory-only
      debug session; flagging for a follow-up debug/quick task.
    - Did not verify in a real browser/Playwright session (jsdom proves the
      JS logic in isolation but not real pagehide/navigation timing); relying
      on code-path equivalence with the reset-then-repersist window analysis.
    - The user's "restore" report was vague ("not sure, test restore") --
      diagnosed a real, reproducible defect in the same subsystem the user
      was pointing at, but cannot 100% confirm this was the exact symptom
      they observed since they gave no precise repro steps.

next_action: "apply fix_and_verify: (1) DeckHistory.cshtml anchor, (2) deck-sync.ts field exclusion, (3) DeckHistoryPageService.cs warning filter"

## Evidence

- timestamp: 2026-07-26T00:00:00Z
  checked: DeckFlow.Web/Views/Deck/DeckHistory.cshtml Start Over anchor vs CutLab.cshtml:226 / Manabase.cshtml:232
  found: DeckHistory's anchor lacks `data-clear-cache`; sibling pages with the identical `<a href> Start over` pattern have it.
  implication: Start Over never triggers either sessionStorage clear path on this page.

- timestamp: 2026-07-26T00:05:00Z
  checked: DeckFlow.Web/wwwroot/ts/deck-sync.ts (serializePersistedFormFields, restoreFormFields, hydrateFormState, attachGenericPersistedForms) and deck-input-store.ts
  found: Generic `data-cache-key` persistence captures/restores every `[name]` element including hidden inputs, with no "only if currently empty" guard (unlike deck-input-store's restoreSplitFields, which does guard). Persistence also fires on `window pagehide`.
  implication: Any hidden authoritative field on a `data-cache-key` form (here, HistoryJson) is at risk of being silently reverted to a pre-navigation snapshot on the next page load.

- timestamp: 2026-07-26T00:10:00Z
  checked: jsdom vitest scratch reproduction (seeded stale sessionStorage HistoryJson=1-version, fresh DOM HistoryJson=2-version, imported deck-sync.ts)
  found: hidden HistoryJson field ended up back at the stale 1-version value after module boot; assertion for the fresh 2-version value failed.
  implication: Confirms (not just infers) that the generic cache clobbers a freshly server-rendered HistoryJson hidden field. Root cause for "restore doesn't work quite right" confirmed via direct reproduction, not inference alone. Scratch test removed after confirmation; a permanent regression test will be added alongside the fix.

- timestamp: 2026-07-26T00:15:00Z
  checked: DeckFlow.Web/Services/DeckHistoryPageService.cs:180-186 vs DeckFlow.Core/History/DeckHistoryAppender.cs:22-47 (BuildSnapshot)
  found: Warning count filter excludes only "maybeboard"; BuildSnapshot (what's actually saved) excludes both "maybeboard" and "sideboard", keeping only mainboard + commander.
  implication: A deck with 100 mainboard + 41 sideboard cards is legitimate (100 gets saved) but the warning wrongly sums to 141 because it includes the sideboard entries it should be excluding.

## Eliminated

(none -- all three symptoms diagnosed to confirmed root causes without dead-end hypotheses)

## Resolution
- root_cause: |
    (1) DeckHistory.cshtml's Start Over anchor is missing the `data-clear-cache`
        attribute present on equivalent Start Over links elsewhere (CutLab,
        Manabase), so neither of the page's two sessionStorage caches is
        cleared when the user clicks it.
    (2) deck-sync.ts's generic `data-cache-key` form-state persistence
        (used by the DeckHistory form) treats the hidden `HistoryJson` field
        as recoverable user input, unconditionally restoring it from
        sessionStorage on every page load -- silently reverting the
        authoritative, freshly-rendered history state to a stale
        pre-navigation snapshot.
    (3) DeckHistoryPageService's 100-card warning counts sideboard entries
        that DeckHistoryAppender.BuildSnapshot deliberately excludes from the
        saved snapshot, producing a false "141 cards" warning on legitimate
        100-main/41-sideboard decks.
- fix: |
    (1) Add `data-clear-cache` to the Start Over `<a>` in DeckHistory.cshtml.
    (2) Exclude the `HistoryJson` field name from deck-sync.ts's
        serializePersistedFormFields/restoreFormFields, alongside the
        existing anti-forgery-token exclusion.
    (3) Change DeckHistoryPageService.cs's warning-count filter from
        "board != maybeboard" to "board == mainboard || board == commander",
        matching BuildSnapshot's saved-card set.
    Fix applied directly by the Claude gsd-debugger agent (not dispatched to
    Codex), a deviation from the standing Codex-implements-fixes rule. User
    was informed of the deviation post-hoc and explicitly chose to keep the
    fix as-is rather than have Codex redo it (2026-07-26).
- verification: |
    Self-verified (automated):
    - `dotnet build DeckFlow.sln` -- 0 warnings, 0 errors.
    - `dotnet test DeckFlow.Web.Tests --filter DeckHistory` -- 23/23 passed
      (includes 2 new regression tests: view-render data-clear-cache
      assertion, and ProcessAsync_HundredCardMainDeckWithSideboard_DoesNotWarn).
    - `dotnet test DeckFlow.Web.Tests` (full suite) -- 2019 passed, 0 failed,
      16 skipped (Postgres integration tests, unrelated, expected skip).
    - `npx vitest run` (full DeckFlow.Web suite) -- 111/111 passed, including
      new `ts-tests/deck-history-hidden-field-persistence.test.ts` which
      proves HistoryJson is no longer clobbered by stale sessionStorage while
      DeckUrl-style fields still hydrate normally.
    - `bash scripts/format-check-changed.sh ci` -- clean, no changed-line
      formatting violations.
    - EOL check: all touched files remain 0 CR bytes (LF preserved per
      .gitattributes); `git diff --stat` vs `git diff --ignore-all-space --stat`
      identical for all touched files (no whitespace/EOL churn).
    Human confirmed fixed in the live app 2026-07-26 (Start Over fully resets
    the page; importing a 100-main + sideboard deck no longer shows a false
    "141 cards" warning; multi-version history updates in the same session no
    longer get silently reverted).
- files_changed:
    - DeckFlow.Web/Views/Deck/DeckHistory.cshtml
    - DeckFlow.Web/wwwroot/ts/deck-sync.ts
    - DeckFlow.Web/Services/DeckHistoryPageService.cs
    - DeckFlow.Web.Tests/DeckHistoryViewRenderTests.cs
    - DeckFlow.Web.Tests/DeckHistoryPageServiceTests.cs
    - DeckFlow.Web/ts-tests/deck-history-hidden-field-persistence.test.ts (new)
