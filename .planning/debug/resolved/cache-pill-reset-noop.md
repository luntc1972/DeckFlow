# Debug: "Restored from cache" Reset button is a visual no-op

- **Reported:** 2026-08-05 by user, during UAT of `fix/deck-history-silent-fresh-start`
- **Status:** RESOLVED. Fix shipped to main in `61bbc6d6` (fix(ui): make the cache pill Reset
  button actually reset the page). The "Fix applied" section below is the record of that work;
  this header previously still said "No fix applied" and was corrected 2026-08-17.
- **Reported symptom:** "the reset button doesn't change this" — screenshot showed the Deck
  History results section (timeline, Save your history, Compare versions, AI prompt) still
  fully rendered after pressing `Reset`.

## The two controls are not the same thing

| Control | Markup | Behavior |
| --- | --- | --- |
| **Start over / Clear** | `[data-clear-cache]` in the toolbar | Works when it carries `href` (anchor) or `data-clear-href` — navigates to a clean GET |
| **Reset** | injected by `showCachePill()` inside the "Restored from cache · just now" chip | **Broken on every tool** |

## Root cause

`DeckFlow.Web/wwwroot/ts/deck-sync.ts:948-952`

```ts
const resetButton = document.createElement('button');
resetButton.textContent = 'Reset';
resetButton.addEventListener('click', () => {
  clearPersistedFormState(form);
  form.reset();
});
```

`form.reset()` restores every control to its **HTML default** — the `value=` attribute or the
textarea's inner text. On a POST-rendered page the server writes the submitted values *into*
those defaults:

- `<input name="DeckName" value="@Model.Request.DeckName" />`
- `<textarea name="DeckText">@Model.Request.DeckText</textarea>`
- `<input type="hidden" name="HistoryJson" value="@Model.SerializedJson" />`

So `form.reset()` resets each field **to the very value the user wanted cleared**. It is a
no-op on exactly the fields that matter. The handler also never touches the rendered
`.deck-history-results` section, which is server-side gated on `Model.HasResult`.

Net user-visible effect: the chip disappears and nothing else changes.

## Reproduction (headless, localhost:5173, Deck History flag On)

Script: `repro-reset.mjs` (scratchpad). Two submits, reload to make the pill appear, submit
again, click `Reset`:

```
BEFORE RESET            AFTER RESET
  results section: 1      results section: 1     <-- unchanged
  version rows: 1         version rows: 1        <-- unchanged
  HistoryJson len: 820    HistoryJson len: 820   <-- unchanged
  DeckText len: 84        DeckText len: 84       <-- unchanged
  DeckName: "Repro Deck"  DeckName: "Repro Deck" <-- unchanged
  cache pill: 1           cache pill: 0          <-- only thing that changed
  sessionStorage:         sessionStorage:
    deckflow.last-deck      deckflow.last-deck   <-- NOT cleared
    decksync-form-state-…   (cleared)
```

Contrast — `Start over` on the same page (`repro-startover.mjs`) DOES clear the results
section and `HistoryJson`, because the anchor's native `href` navigation wins.

## Blast radius

`showCachePill()` is generic: it fires for every `form[data-cache-key]`. **14 views**:

CommanderCategories, Bracket, CardLookup, CedhMetaGap, CutLab, DeckAnalysis, DeckComparison,
DeckConvert, DeckHistory, DeckPrimer, DeckSync, Manabase, MechanicLookup, SuggestCategories.

The Reset button is broken on all 14.

## Second, related defect (same `form.reset()` fallback)

`deck-sync.ts:1003-1020` and `:1293-1313` fall back to `form.reset()` when the clear control
has no `data-clear-href`. Four views ship a `<button data-clear-cache>` with no
`data-clear-href`, so their **Clear** button has the identical no-op after a POST:

- `Commander/CommanderCategories.cshtml:41`
- `Deck/DeckSync.cshtml:75`
- `Deck/MechanicLookup.cshtml:40`
- `Deck/SuggestCategories.cshtml:51`

The other ten either use an anchor with `href` (Bracket, CutLab, DeckHistory, Manabase) or
carry `data-clear-href` (CardLookup, CedhMetaGap, DeckAnalysis, DeckComparison, DeckConvert,
DeckPrimer) and are fine.

## Third, lesser defect found while reproducing

After `Start over` on Deck History, `DeckName`/`Label`/`DeckText` come back populated and
`decksync-form-state-deck-history` reappears in sessionStorage. The fallback branch of the
clear handler does **not** set `form.dataset.skipPersistence = 'true'` (only the
`data-clear-href` branch does), so the `pagehide` listener re-persists the still-populated
form during the native anchor navigation, and the fresh GET hydrates from it.

## Fix applied — branch `fix/cache-pill-reset-noop`

All three defects, TDD (RED confirmed on both new test files before any implementation).

1. `showCachePill`'s Reset delegates to the form's own `[data-clear-cache]` control instead
   of calling `form.reset()`. Delegation rather than a direct call is deliberate: the
   control is what routes through `deck-input-store.ts`'s document-level listener (which
   clears `deckflow.last-deck`), and the four anchor-based tools navigate only from a real
   click on the anchor.
2. `data-clear-href` added to the four button-only views, using each controller's actual GET
   route. `DeckSync`'s is **`/sync`**, not `/deck-sync` — the view name and the route
   diverge, so the URL cannot be guessed from the filename.
3. Both clear handlers now share `clearFormByNavigating()`, which arms `skipPersistence` for
   natively-navigating anchors as well as scripted navigations.

### Verification

- Mutation-tested each guard independently. Reverting the delegation fails only
  "delegates to the form clear control"; removing `skipPersistence` fails only
  "suppresses re-persistence"; stripping one `data-clear-href` fails the C# guard with
  `MechanicLookup.cshtml:40`.
- Live headless, post-refactor: Reset now clears the results section, `HistoryJson`,
  `DeckText`, `DeckName` and all sessionStorage keys (previously every one unchanged).
  Start over no longer resurrects the deck fields. All four Clear buttons land on their
  own route with fields cleared.
- 4747 xUnit / 126 vitest passing, format gate exit 0, no EOL churn.
- NOT yet done: user UAT, and push.

### Rejected during the /simplify review

Deriving the clear URL from `form.action` and deleting all 14 `data-clear-href` attributes
plus the guard test. The census falsifies the premise: `CardLookup`'s `data-cache-key` form
posts to `/card-lookup/download` (a file-download endpoint) while its clear URL is
`/card-lookup`, and `DeckSync`'s form carries no `action` at all. The attribute holds real
information, which is exactly why the guard test is worth keeping.

## Follow-ups (not done here)

1. **Three systems share the `decksync-form-state-` namespace.** `category-suggestions.ts`
   uses the same prefix as `deck-sync.ts` and binds its *own* `[data-clear-cache]` handler;
   `deck-input-store.ts` adds a third, document-level delegate. `SuggestCategories` and
   `CommanderCategories` load both scripts, so one click now runs a navigation and a
   competing in-place reset. Verified harmless live (navigation wins), but the ownership
   should be collapsed to one handler.
2. **Six hand-rolled repo-root walkers** across the test projects (`SitemapControllerTests`,
   `HelpContentServiceTests`, `HelpFlagHeaderConsistencyTests`, `DeckPrimerPacketServiceTests`,
   `CarveOutGuardTests`, and this new guard), under two different conventions
   (`Directory.GetCurrentDirectory()` walk vs `AppContext.BaseDirectory` + `../../../..`).
   One `TestPaths` helper would retire all of them.
3. **`ts-tests/` has no shared setup file.** The same five-line `afterEach` teardown is
   copied into 10 of 33 spec files, and the `decksync-form-state-` prefix is restated in
   five. `vitest.config.ts` declares no `setupFiles`.
4. **Leaving a tool page persists an empty form**, so the next visit shows a "Restored from
   cache" pill over fields that hold nothing. Pre-existing on all 14 tools, unrelated to
   this fix.
