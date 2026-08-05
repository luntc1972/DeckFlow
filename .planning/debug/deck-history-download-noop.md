---
slug: deck-history-download-noop
status: fixed
trigger: "in prod deck-history download deck history button doesn't download anything at least when creating one, havent been able to test a new version yet"
created: 2026-08-04
updated: 2026-08-04
---

# Deck History download button is a no-op in prod

## Symptoms

- **Expected behavior:** Clicking "Download deck history (.json)" saves a `.json` history file to the user's machine.
- **Actual behavior:** Nothing at all happens. No file, no page reload, no visible change — the button appears dead.
- **Error messages:** None surfaced in the UI. No error banner shown. (Browser console not yet inspected.)
- **Timeline:** Observed in production. Unknown whether it ever worked in prod. Not yet retested against a newer build.
- **Reproduction:** On https://www.deckflow.gg/deck-history — fresh path: import a deck (no prior history file uploaded), then press "Download deck history (.json)". This is the "creating a new history" case.
- **Environment:** Production only (local not yet tested). Desktop Chrome/Edge.

## Orchestrator recon (pre-investigation, unverified)

- Button markup — `DeckFlow.Web/Views/Deck/DeckHistory.cshtml:188`:
  `<button type="submit" class="run-button" form="deck-history-compare-form" formaction="~/deck-history/download" data-no-busy data-prompt-download-submit>`
  Note: the button is associated to the form by the `form=` attribute (not by nesting), overrides the action via `formaction`, and carries a `data-prompt-download-submit` hook that some TypeScript almost certainly binds to.
- Endpoint — `DeckFlow.Web/Controllers/DeckHistoryController.cs:76` `[HttpPost("/deck-history/download")]` → `Download(DeckHistoryRequest request)`; at `:86` it returns the history view with error "Nothing to download yet — import a deck or upload a history file first." when the hidden round-trip field is empty.
- **"Nothing at all" (no reload) is the load-bearing symptom:** a server-side empty-state would produce a page re-render with that error banner, and a server 500 would produce an error page. Neither happened, so the strongest initial hypothesis is that the POST never left the browser — a `data-prompt-download-submit` handler calling `preventDefault()` and then failing (or a JS exception earlier in page init leaving the handler broken/unbound), or the `form=`/`formaction` association not resolving.
- Prod-vs-local matters: `wwwroot/js/*.js` is gitignored and recompiled by `tsc` in the Docker build, so a prod-only TS/build difference is in scope. Feature flags are also in scope (`FeatureFlagCatalog` / `FeatureFlagStore` both reference deck history) — memory records deck history as having had an owed prod flag flip.

## Current Focus

- hypothesis: CONFIRMED — see Resolution.root_cause
- test: static code trace (button DOM position vs. `closest('form')` resolution) — deterministic, no live browser needed
- expecting: n/a — confirmed
- next_action: return ROOT CAUSE FOUND (goal: find_root_cause_only — Claude does not implement the fix in this project)
- reasoning_checkpoint:
    hypothesis: "The click on `[data-prompt-download-submit]` on /deck-history never sends a request because `registerPromptDownloadHandler` (DeckFlow.Web/wwwroot/ts/deck-sync.ts:427) both (a) unconditionally sets `button.type = 'button'`, which strips all native/implicit form submission via the `form=` attribute, and (b) resolves the target form with `button.closest('form')`, which only walks DOM ancestors and does not honor the HTML5 `form=` attribute — and on DeckHistory.cshtml:188 the button is NOT a descendant of `deck-history-compare-form` (defined at :194, after the button's enclosing `<section>` closes), so `closest('form')` returns null and the handler `return`s before calling `preventDefault()` or `fetch()`."
    confirming_evidence:
      - "DeckHistory.cshtml has exactly two `<form>` elements: lines 54-112 and 194-209. The button at line 188 sits in a separate `<section class=\"result-panel\">` between them, associated only via `form=\"deck-history-compare-form\"`."
      - "deck-sync.ts:439-442: `const form = button.closest('form'); if (!form) { return; }` runs before `event.preventDefault()` at :443 — so on this page nothing happens: no preventDefault, no fetch, and (because type was already forced to 'button' at :436) no native submission either."
      - "Confirmed this is unique to DeckHistory by checking all 6 other `data-prompt-download-submit` usages (CardLookup, DeckPrimer, Manabase, CedhMetaGap, DeckAnalysis, DeckComparison) — every one nests the button as a descendant of its target `<form>`, so `closest('form')` resolves fine there. Manabase.cshtml even has a comment (:1076) noting the download button needed its OWN small wrapping `<form>` for exactly this reason."
      - "git blame: the `closest('form')` handler pattern was written 2026-05-10 (commit e0a66577) for the pre-existing nested-button pages. DeckHistory.cshtml's cross-form `form=` attribute button was added later (commit 929344dbb, feat(deck-history)) without adapting the shared handler — a consumer/producer mismatch, not a recent regression (bug has existed since the feature shipped)."
      - "Existing test coverage does not exercise the real handler: e2e/deck-history-smoke.spec.ts:129-143 manually replicates a fetch using `submitter.form` (the native HTMLButtonElement.form property, which DOES resolve `form=`) inside `page.evaluate`, never dispatching an actual `button.click()` through the registered listener. e2e/form-correctness-batch-g.spec.ts G1 checks the same demotion-to-type-button behavior but its path list is `/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap` only — /deck-history is not included. ts-tests/deck-history-hidden-field-persistence.test.ts covers unrelated hidden-field-restore behavior, not the download button at all. All three tests bound the wrong population for this defect."
    falsification_test: "If the button at DeckHistory.cshtml:188 were a DOM descendant of `deck-history-compare-form` (or `closest('form')` were replaced with `button.form`), `closest('form')` would resolve non-null and the fetch/blob-download path would proceed — this would disprove the hypothesis. It is not: verified by direct read of the file, no ambiguity (only two `<form>` tags in the file, both closed/opened outside the button's position)."
    fix_rationale: "n/a in this session — find_root_cause_only; proposed fix direction included in return payload, not applied (Codex is the implementer)."
    blind_spots: "Did not perform a live browser click-through (Playwright) to visually confirm zero network activity — static trace is unambiguous and a live check would need permission to run scripts/run-web-test.sh, which the prompt says to ask about first (recommended as a follow-up, not performed). Did not check prod feature-flag state for deck history via Render MCP (irrelevant to this specific no-op symptom since the user already reaches the results page)."
    candidate_causes:
      - "code (client-side JS): `button.closest('form')` does not honor the HTML5 `form=` attribute — wrong API for cross-DOM form association"
      - "code (markup): DeckHistory.cshtml places the download button outside its target `<form>`, a structural choice every sibling view avoided"
    and_gate: "no — a single-condition fix (either restructure the markup to nest the button, or fix the JS to use `button.form` instead of `button.closest('form')`) fully resolves the symptom; the two candidate causes are two equally-valid fix *locations* for the one mismatch, not two conditions that must co-occur to trigger the bug."
- tdd_checkpoint:

## Evidence

- timestamp: 2026-08-04
  checked: DeckFlow.Web/wwwroot/ts/deck-sync.ts:427-466 (registerPromptDownloadHandler, the click handler bound to `data-prompt-download-submit`)
  found: On registration, every matching button has `button.type` forced to `'button'` (:436). The click listener then computes `const form = button.closest('form');` (:439) and returns immediately if null (:440-442), *before* `event.preventDefault()` (:443) or the `fetch()` call (:457).
  implication: If `closest('form')` cannot resolve a form for a given button, the click is a total no-op — no preventDefault, no fetch, and (since type is already 'button') no native submission fallback either. Exactly matches "nothing at all happens."

- timestamp: 2026-08-04
  checked: DeckFlow.Web/Views/Deck/DeckHistory.cshtml (full form/button structure, lines 54-209)
  found: Two `<form>` elements exist: `deck-history-form` (54-112) and `deck-history-compare-form` (194-209). The download button (188) sits inside `<section class="result-panel">` (184-190), which is between the two forms — not a descendant of either. It targets `deck-history-compare-form` solely via the `form="deck-history-compare-form"` HTML attribute.
  implication: `button.closest('form')` walks DOM ancestors only and does not resolve the `form=` attribute association, so on this page it returns `null`, triggering the early return identified above.

- timestamp: 2026-08-04
  checked: All 6 other `[data-prompt-download-submit]` usages — CardLookup.cshtml:112-113 (form 85-116), DeckPrimer.cshtml:88 (form 76-368), Manabase.cshtml:1093 (dedicated form 1079-1094, with an explanatory comment at :1076), CedhMetaGap.cshtml:50/413 (form 47-677), DeckAnalysis.cshtml:99/563/990 (form 96-1163), DeckComparison.cshtml:181/602 (form 178-772)
  found: Every one of these buttons is a DOM descendant of the form it submits.
  implication: DeckHistory.cshtml is the sole exception to the "button nested inside its target form" convention the rest of the codebase follows for this shared handler — confirms the bug is page-specific, not a general defect in the handler for the common case.

- timestamp: 2026-08-04
  checked: git log -p -L on the `closest('form')` handler (commit e0a66577, 2026-05-10) vs. git log --diff-filter=A on DeckHistory.cshtml (commit 929344dbb, feat(deck-history))
  found: The shared handler predates the DeckHistory page. DeckHistory's button used the cross-form `form=` attribute pattern from the moment it was added, never adapted for the pre-existing `closest('form')`-based handler.
  implication: Root cause is a producer/consumer mismatch introduced when the deck-history feature was built — not a recent regression against previously-working code. Consistent with symptom note "unknown whether it ever worked in prod."

- timestamp: 2026-08-04
  checked: e2e/deck-history-smoke.spec.ts:126-143, e2e/form-correctness-batch-g.spec.ts:114-124, ts-tests/deck-history-hidden-field-persistence.test.ts (full file)
  found: The smoke spec never calls `button.click()` — it uses `page.locator(...).evaluate()` to hand-run a corrected fetch using `submitter.form` (the native property, which DOES resolve `form=`), bypassing the real registered listener entirely. The batch-G spec checks the same type='button' demotion but only for `/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap` — `/deck-history` is absent from its path list. The hidden-field-persistence test covers a different concern (stale sessionStorage restore) and never touches the download button.
  implication: No existing automated test exercises a real click on the DeckHistory download button through the actual registered handler — all three tests "bound the wrong population" for this defect, which is why it shipped and stayed unnoticed.

## Eliminated

- hypothesis: Server-side rejection (antiforgery / SameOriginRequestValidator / empty hidden round-trip field causing the "Nothing to download yet" banner)
  evidence: The failure is proven to be upstream of any network request — `registerPromptDownloadHandler`'s early return at deck-sync.ts:440-442 fires before `fetch()` is ever called, so no POST reaches the server at all. A server-side rejection would either re-render the page with an error banner (contradicts "no page reload") or the native/noscript submit would produce a full navigation — neither is possible here because `button.type` is already forced to `'button'`, which never triggers native submission regardless of server behavior.
  timestamp: 2026-08-04

## Resolution

- root_cause: |
    In DeckFlow.Web/wwwroot/ts/deck-sync.ts, `registerPromptDownloadHandler()` (:427-466) resolves the
    button's target form via `button.closest('form')` (:439) and forces `button.type = 'button'` (:436)
    on every matching button up front. `closest('form')` only walks DOM ancestors — it does not honor
    the HTML5 `form="..."` attribute, which is how DeckFlow.Web/Views/Deck/DeckHistory.cshtml:188
    associates its "Download deck history (.json)" button with `deck-history-compare-form` (defined at
    :194, after the button's enclosing `<section>` has already closed at :190). On every other page using
    this handler, the button is nested inside its target `<form>`, so `closest('form')` happens to work;
    DeckHistory.cshtml is the only page where the button lives outside its target form. Because the form
    resolves to `null` on /deck-history, the click listener returns at :440-442 before calling
    `event.preventDefault()` or `fetch()` — and because `button.type` was already forced to `'button'`,
    there is no native/noscript submission fallback either. The click is therefore a complete no-op:
    no network request, no page reload, no error — exactly the reported symptom. This has been true since
    the deck-history feature was built (commit 929344dbb), not a recent regression.
  fix: |
    NOT APPLIED in this session (find_root_cause_only; Claude does not author production code in this
    project — Codex is the implementer). Two equally valid fix locations, either sufficient alone:
      (a) Markup fix — restructure DeckHistory.cshtml so the download button (:188) is a DOM descendant
          of `deck-history-compare-form`, matching the convention every other page already follows
          (e.g. move the button inside the form, or wrap it in its own small dedicated `<form>` the way
          Manabase.cshtml:1079-1094 does for its download button that also renders outside the main form).
      (b) TypeScript fix — in deck-sync.ts:439, resolve the form via the button's native `.form` property
          (`const form = button.form ?? button.closest('form');`) instead of `closest('form')` alone.
          `HTMLButtonElement.form` correctly resolves the `form=` attribute association per the HTML5
          spec (this is exactly what e2e/deck-history-smoke.spec.ts:131 already relies on to make its
          test pass) and is a strict superset of `closest('form')`'s behavior, so it would not regress
          the 6 other pages.
    Recommend (b) as the minimal, general fix (one-line change, fixes the class of bug for any future
    cross-form download button), with (a) as a belt-and-suspenders markup cleanup.
    Test gap to close alongside the fix: e2e/deck-history-smoke.spec.ts should dispatch a real
    `button.click()` (not a hand-rolled `page.evaluate` fetch) so the actual registered handler is
    exercised, and e2e/form-correctness-batch-g.spec.ts's G1 path list should include `/deck-history`.
  applied: |
    Fix (b) applied on branch fix/deck-history-download-noop, with one deviation from the
    recommendation above: the `?? button.closest('form')` fallback was DROPPED after the /simplify
    pass. `.form` is not quite a strict superset — when a `form="<id>"` attribute resolves to no
    element, the spec says the form owner is null even if an ancestor `<form>` exists. Falling back
    to `closest('form')` there would silently post to the wrong action, so `const form = button.form;`
    stands alone. A `console.warn` was added to the `if (!form)` branch: the silent return is what
    let this sit dead in production for months.
    Fix (a) (markup restructure) was NOT applied — unnecessary once the shared handler is correct.
  verification: |
    - `dotnet build DeckFlow.Web` — 0 errors, 0 warnings (tsc recompiled).
    - e2e/deck-history-smoke.spec.ts — 6/6 pass, chromium-desktop + chromium-mobile.
    - MUTATION PROOF: reverting deck-sync.ts to `closest('form')` and rebuilding made
      chromium-mobile fail at exactly the download step — `waitForResponse` timed out with no POST
      and no `download` event. The new test genuinely catches the defect; the old one did not.
      (The chromium-desktop failure in that run was the known admin-e2e lock-contention flake.)
    - Regression: e2e/form-correctness-batch-g.spec.ts + e2e/manabase-download.spec.ts — 38/38 pass
      combined with the deck-history spec, so the other 6 download pages are unaffected.
    - vitest: 122/123 pass. The single failure (ts-tests/cut-lab-proposal.test.ts, export-tab toggle)
      reproduces on a clean tree with these changes stashed — pre-existing, already ticketed.
    - EOL: `git diff --stat` matches `git diff --ignore-all-space --stat`; both files 0 CRs in
      working tree and at HEAD. No churn.
    - NOT verified: production itself. Needs a deploy + user UAT on www.deckflow.gg.
  files_changed:
    - DeckFlow.Web/wwwroot/ts/deck-sync.ts
    - DeckFlow.Web/e2e/deck-history-smoke.spec.ts

## Follow-ups (found during the /simplify pass, deliberately NOT in this fix)

Two other sites resolve a **form-associable element's** owner with an ancestor-only walk, so they
carry the identical latent defect. Neither is live today — no view puts a `form="<id>"` attribute on
these elements — so they were left out rather than widening a bug-fix branch with speculative edits.

- `DeckFlow.Web/wwwroot/ts/deck-sync.ts:2343` — `action.closest('form')` where `action` is a
  `[data-default-action]` `<button>`. If it ever gained `form="<id>"`, the button would drop out of
  the `forms` Set and Enter-key routing would silently degrade to native browser behavior.
- `DeckFlow.Web/wwwroot/ts/admin-feedback.ts:17` — `target.closest('form')` where `target` is the
  `#typeSelect` `<select>`. Same class.

Not affected: `deck-sync.ts:708` — `container.closest('form')` operates on a plain `<div>`, which is
not form-associable and has no `.form` property. `closest('form')` is the only correct lookup there.

Also deliberately not done: adding `/deck-history` to `e2e/form-correctness-batch-g.spec.ts`'s G1
path list, as the original diagnosis suggested. G1 does a bare `page.goto(path)`, but the
deck-history download button only renders inside a `.result-panel` that requires an existing history,
so the locator would never resolve. The equivalent `type="button"` assertion was added to
`deck-history-smoke.spec.ts` instead, at the point where the button actually exists.
