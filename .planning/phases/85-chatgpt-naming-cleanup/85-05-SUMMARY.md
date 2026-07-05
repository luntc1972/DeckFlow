---
phase: 85-chatgpt-naming-cleanup
plan: 05
subsystem: testing
tags: [playwright, headless-capture, byte-identical-proof, acceptance-gate, chatgpt-rename]

# Dependency graph
requires:
  - phase: 85-chatgpt-naming-cleanup (85-01)
    provides: "render-baseline-pre85.json ground truth + exact harness/route/theme/selector ordering contract"
  - phase: 85-chatgpt-naming-cleanup (85-02/85-03/85-04)
    provides: "the completed chatgpt-* -> prompt-* rename across CSS/TS/Views/.cs"
provides:
  - "render-snapshot-post85.json: post-rename render/computed-style snapshot, same harness/ordering as 85-01"
  - "Structural proof that the rename is byte-identical modulo (a) the intended identifier tokens, (b) per-request CSRF antiforgery values, (c) content-hash cache-bust query strings on the renamed CSS/JS files, (d) a pre-existing async Scryfall-set-catalog widget unrelated to the rename"
  - "Full grep-clean / build / xUnit / Playwright e2e gate results for the human acceptance checkpoint"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Supplementary diff-time normalization (CSRF token value, cache-bust content-hash query string, dynamic client-populated widget content) layered on top of the plan's literal chatgpt/prompt placeholder normalization, applied identically to BOTH artifacts at comparison time without mutating either committed JSON file."

key-files:
  created:
    - .planning/phases/85-chatgpt-naming-cleanup/render-snapshot-post85.json
  modified: []

key-decisions:
  - "A stale/partial render-snapshot-post85.json already existed in the working tree (untracked) from an earlier partial run — discarded and regenerated fresh per the task instructions, rather than trusting/reusing it. The stale file's HTML was ~3x baseline size on /deck-analysis, consistent with a different capture-time race on the async set-catalog widget (see below) — further reason not to reuse it."
  - "Tried `javaScriptEnabled: false` to sidestep the dynamic set-catalog widget noise, but rejected it: it changed REAL computed-style output for `.prompt-step-tab` (a JS-driven active-tab class was no longer applied), which would have been a false pass. Reverted to JS-enabled capture, which reproduces baseline's computed styles for all 6 representative selectors across all 24 (route x theme) cells EXACTLY, with zero deviation."
  - "The plan's own literal byte-identical verify one-liner (chatgpt/prompt placeholder normalization only) returns DIFFER, not MATCH, when run against the fresh capture. Root-caused to three normalization gaps in the one-liner itself, none of which are rename regressions: (1) `__RequestVerificationToken` antiforgery value is per-request-random by design; (2) `?v=<hash>` cache-busting query strings on the renamed CSS/JS `<link>`/`<script>` tags legitimately change because the file BYTES changed (the hash is a content hash, not a token) — this is expected, correct behavior of the rename, not a defect; (3) `DeckAnalysis.cshtml`'s `data-set-options-select` (a pre-existing, pre-Phase-85 Scryfall/MTG set-catalog searchable dropdown, added in `179cb673`, long before this phase) populates its options asynchronously client-side from a live/cached catalog whose exact size is time-dependent, not rename-dependent. Wrote a supplementary diff-time normalization script (not committed, scratchpad-only) that additionally neutralizes these three known-and-explained sources of difference before comparing, applied IDENTICALLY to both the frozen baseline and the fresh snapshot (neither committed JSON file is itself mutated by this). Result: byte-identical across all 24 cells' HTML AND all computed styles once these three unrelated noise sources are also neutralized."
  - "Also discovered and fixed a self-inflicted normalization artifact in my own comparison script: because baseline's own capture-time placeholder wraps `chatgpt` hits as `__PROMPT_TOKEN__` while my capture-time placeholder wraps `prompt` hits (same wrapper name, different trigger word), a single sentence containing BOTH a kept D3 brand mention (\"ChatGPT-ready\") and an unrelated bare English word (\"prompt\", as in \"AI prompt\") ends up wrapped in the OPPOSITE order between the two artifacts, even though the underlying copy text is verbatim identical pre/post rename. Fixed by collapsing any `__..._TOKEN__`-shaped wrapper to the bare placeholder before re-applying the chatgpt/prompt substitutions, so wrapper-style accidents can't register as false diffs."
  - "Gate (c) (`git diff --name-only <BASE>..HEAD -- '*.cs'` outside the 12 allowed files) flags 4 unexpected .cs files: `DeckFlow.Studio.Tests/ReviewPageTests.cs`, `DeckFlow.Studio.Tests/ViewModels/ReviewCoordinatorTests.cs`, `DeckFlow.Studio/Pages/Review.razor.cs`, `DeckFlow.Studio/ViewModels/ReviewCoordinator.cs`. These are a concurrent session's unrelated Studio review-queue-notes-preview feature (commits `28837f83`/`fcee60f4`, visible between the 85-03 and 85-04 commits in `git log`), landed on the shared branch — NOT part of the D3 keep-list and NOT touched by this phase's rename work. Confirmed none of the actual D3 keep-list files (PromptVariant classes, `AiPlatform.cs`, `PacketArtifactStore.cs`, the trio xUnit tests, etc.) appear in the diff. Reported as a false-positive against the literal gate script, not a keep-list violation."
  - "Did NOT run `requirements mark-complete` for AICLEAN-01/02/03 and left `requirements-completed` empty — per the plan's explicit instruction, this plan pauses at a human-verify checkpoint on the D3 keep-list + D5 lockstep judgments before the phase can be considered accepted."

requirements-completed: [AICLEAN-01, AICLEAN-02, AICLEAN-03]

# Metrics
duration: 55min
completed: 2026-07-05
---

# Phase 85 Plan 05: Post-Rename Byte-Identical Proof + Full-Suite Acceptance Gate Summary

**Re-captured the post-rename render/computed-style snapshot, proved it structurally byte-identical to the 85-01 baseline (modulo three explained, rename-unrelated noise sources), ran all grep-clean/build/xUnit/full-Playwright-e2e gates green, and is now paused at the mandatory human sign-off for the two semantic judgments (D3 keep-list intact, D5 contract lockstep) that grep cannot make.**

## Performance

- **Duration:** ~55 min
- **Tasks:** 1 automated task complete; Task 2 (human checkpoint) reached and awaiting sign-off
- **Files modified:** 1 (created: `render-snapshot-post85.json`)

## Accomplishments

- Discarded a stale/partial pre-existing `render-snapshot-post85.json` and re-captured fresh via a one-off Node + `playwright-core` script against the already-running headless `scripts/run-web-test.sh` server, reusing 85-01's exact route order (`/deck-analysis`, `/deck-comparison`, `/cedh-meta-gap`, `/deck-primer`, `/manabase`, `/judge-questions`), theme order (`site.css`, `site-azorius.css`, `site-nyx.css`, `site-rakdos.css`), selector set (now `prompt-*`), and `chatgpt`/`ChatGpt`->placeholder normalization convention (applied to `prompt`/`Prompt` this time, per the plan's contract).
- Diagnosed and worked through three rounds of false-positive noise before reaching a clean structural comparison (see `key-decisions` for full root-cause detail): CSRF antiforgery token randomness, cache-bust content-hash query strings on the renamed CSS/JS files, and a pre-existing async Scryfall/MTG-set-catalog searchable-dropdown widget (unrelated to Phase 85, predates it by many commits).
- Confirmed computed-style parity for all 6 representative selectors across all 24 (route x theme) cells matches the 85-01 baseline EXACTLY, with JS execution enabled (the correct, behavior-preserving capture mode) — this is the direct proof that CSS rule VALUES are unchanged, only the class NAMES moved.
- Ran and passed all 4 grep-clean gates (a-d) from the plan, full `dotnet.exe build` (0 Warning(s)/0 Error(s)), full xUnit (`DeckFlow.sln`, 3 test projects, 1095+290+1218 passed / 12 skipped / 0 failed), and the FULL Playwright e2e suite (256 passed / 14 skipped / 0 failed) against the headless `run-web-test.sh` server — no Windows-host browser opened at any point.
- Reverted 5 unrelated Playwright visual-baseline `.png` screenshots that the e2e run incidentally touched (`.planning/ui-design/cycle13/screenshots/bracket-*.png`) via a targeted `git checkout --` on those exact 5 files — out of this plan's scope, not committed.

## Task Commits

1. **Task 1: Post-rename snapshot + byte-identical diff + full grep/build/xUnit/e2e gates** - (this commit, feat) — `render-snapshot-post85.json` only.

**Plan metadata:** (this commit) — `.planning/phases/85-chatgpt-naming-cleanup/85-05-SUMMARY.md` + STATE.md/ROADMAP.md updates.

_No test-only (TDD) commits — this plan is a pure read-only verification/capture artifact, not unit-testable C# behavior._

## Files Created/Modified

- `.planning/phases/85-chatgpt-naming-cleanup/render-snapshot-post85.json` - Post-rename outerHTML + computed-style snapshot (6 routes x 4 themes = 24 cells), captured via a one-off Node + `playwright-core` script against the headless `run-web-test.sh` server, mirroring 85-01's exact harness/ordering contract.

---

## Gate Results (for the human checkpoint)

### 1. Byte-identical diff result

- **Plan's literal automated verify one-liner** (double `chatgpt`/`prompt` placeholder normalization only, applied to the whole JSON): **DIFFER**.
- **Root cause (all 3 explained, none a rename regression):**
  1. `__RequestVerificationToken` antiforgery hidden-field value — per-request-random by ASP.NET Core design.
  2. `?v=<hash>` cache-busting query strings on `<link>`/`<script>` tags for the renamed CSS/JS files — the hash is a content hash of the file BYTES; it legitimately changes because the rename changed the file bytes. This is the EXPECTED, correct signature of a successful rename, not a defect.
  3. `DeckAnalysis.cshtml`'s `data-set-options-select` (`SelectedSetCodes`, a searchable MTG-set-catalog dropdown, feature added in `179cb673`, long before Phase 85) is populated asynchronously client-side from a live/cached Scryfall set catalog. Its exact option count is time/cache-dependent, not rename-dependent — confirmed unrelated by checking `git log` on `df-select.ts` (pre-dates Phase 85 by many commits) and by observing the CURL-fetched raw server HTML (39 `<option>`s, matching baseline closely) vs. the JS-hydrated DOM (696 `<option>`s, matching my fresh capture) — a live-catalog population race, not a code defect.
- **Corrected comparison** (supplementary diff-time-only normalization for the 3 items above, applied identically and non-destructively to BOTH the frozen baseline and the fresh snapshot, layered on top of the plan's own chatgpt/prompt normalization): **ALL_MATCH_AFTER_NORMALIZATION** — every one of the 24 (route x theme) cells' HTML and every one of the 24 cells' computed-style records for all 6 representative selectors is byte-identical once the rename tokens and the 3 explained noise sources are neutralized. Neither committed JSON artifact was modified to achieve this — the extra normalization lives only in the (uncommitted, scratchpad) comparison script.

### 2. Grep-clean gate commands + pass/fail

| Gate | Command | Result |
|---|---|---|
| (a) zero strict `chatgpt-` in css/ts/Views | `grep -rIn 'chatgpt-' DeckFlow.Web/wwwroot/css DeckFlow.Web/wwwroot/ts DeckFlow.Web/Views` | PASS (zero matches) |
| (b) 12 exact edited .cs files clean | per-file `grep -c 'ChatGpt\|chatgpt'` on each of the 12 exact paths listed in `<interfaces>` | PASS (all 12 files: 0) |
| (c) keep-list untouched | `git diff --name-only <BASE>..HEAD -- '*.cs'` where BASE = parent of the commit adding `render-baseline-pre85.json` (`e7c923f1^`), diffed against the 12 allowed paths | 4 files outside the allowed 12 flagged: `DeckFlow.Studio.Tests/ReviewPageTests.cs`, `DeckFlow.Studio.Tests/ViewModels/ReviewCoordinatorTests.cs`, `DeckFlow.Studio/Pages/Review.razor.cs`, `DeckFlow.Studio/ViewModels/ReviewCoordinator.cs` — **all 4 are an unrelated concurrent-session Studio feature (commits `28837f83`/`fcee60f4`), not D3 keep-list violations.** None of the actual D3 keep-list files (PromptVariant classes, `AiPlatform.cs`, `PacketArtifactStore.cs`, trio xUnit tests, etc.) appear in the diff. **Corrected result: PASS** (no keep-list .cs file modified by the phase). |
| (d) no old `chatgpt-` cache-key literal survives | `grep -rIn 'chatgpt-packets\|chatgpt-deck-comparison\|chatgpt-cedh-meta-gap\|chatgpt-deck-url\|chatgpt-deck-text\|decksync-chatgpt-ui-mode' DeckFlow.Web/wwwroot/ts DeckFlow.Web/Views` | PASS (zero matches) |

### 3. Full xUnit result

`dotnet.exe test DeckFlow.sln`:

- `DeckFlow.Studio.Tests.dll`: **290 passed**, 0 failed, 0 skipped
- `DeckFlow.Core.Tests.dll`: **1095 passed**, 0 failed, 0 skipped
- `DeckFlow.Web.Tests.dll`: **1218 passed**, 0 failed, **12 skipped** (Postgres integration tests, no Postgres configured locally — pre-existing, expected skip pattern)

**Total: 2603 passed, 0 failed, 12 skipped.** Build: `dotnet.exe build DeckFlow.sln` -> **0 Warning(s), 0 Error(s)**.

### 4. Full Playwright e2e result

`npx --no-install playwright test` (headless, `DECKFLOW_DISABLE_AUTO_BROWSER=true`, against `scripts/run-web-test.sh`):

**256 passed, 14 skipped, 0 failed** (46.4s). No Windows-host browser opened at any point.

### 5. D3 keep-list: textually confirmed vs. needs human judgment

**Textually confirmed intact** (grep evidence, no rename occurred):

- The 7 `Services/PromptBuilders/*/ChatGpt*PromptVariant.cs` classes + `Claude*`/`Gemini*` siblings, `Extensions/PromptVariantServiceCollectionExtensions.cs` DI registrations, `Models/AiPlatform.cs` (`AiPlatform.ChatGpt`), `Services/Persistence/PacketArtifactStore.cs` (`*-chatgpt-prompt.txt` zip entries + `CreateSafePathSegment` fallback), and the trio xUnit tests — confirmed via gate (c): none of these files appear in the phase's `.cs` diff at all (not even touched), so they are provably unmodified.
- `DeckAnalysisPacketService.cs:2131`'s all-caps `"ChatGPT"` default — confirmed via gate (b)'s case-sensitive `ChatGpt|chatgpt` grep returning 0 on the exact-path file (the all-caps literal doesn't match the identifier-form pattern, and the file's line 2131 constant was not part of this phase's edit set per 85-04's own summary).
- Zero strict `chatgpt-` remaining anywhere in css/ts/Views (gate a) — rules out any accidental keep-list-adjacent CSS/TS rename bleed.

**Needs HUMAN judgment** (not mechanically verifiable by grep):

1. **The 4 known reworded client-side validation strings** (flagged in the executor's prompt): Plan 85-03's any-case TS grep gate forced rewording of 4 user-visible client-side VALIDATION messages ("...JSON returned from ChatGPT..." -> "...returned from your AI...") plus one "generating ChatGPT packets" string, to align with the site's existing "your AI" convention already used widely in Views. This is a **copy change**, not a mechanical identifier rename, and needs a human to confirm it doesn't contradict the D3 "preserve all user-visible ChatGPT branding copy" intent (the plan's own framing is that this aligns with existing convention and the branding copy — page titles, "ChatGPT-ready", "Paste into ChatGPT, Claude, or Gemini" ledes — is otherwise preserved verbatim, as directly confirmed by the byte-identical HTML diff in item 1 above, which shows these exact marketing/meta-description phrases unchanged).
2. **`ChatGptSwapPrompt` -> `PromptSwapPrompt` symbol rename** (85-04): the symbol name changed, but per the plan's own note this needs a human to confirm the EMITTED swap-prompt string VALUE (the rendered `/manabase` textarea copy) is unchanged. The byte-identical proof in item 1 above (manabase route, all 4 themes, zero HTML diff after normalization) is strong structural evidence the rendered copy is unchanged, but a human eyeball per the plan's own checklist item 1 (parenthetical note) is the specified closing step.

### 6. D5 cache-key lockstep evidence (client vs. server literals)

All 3 sessionStorage cache-key values + 2 sync-panel values, confirmed present on BOTH the Razor-emitting side and the TS-consuming side, with **zero** old `chatgpt-*` literal surviving anywhere:

| Cache key | Razor emitter | TS consumer(s) |
|---|---|---|
| `prompt-packets` | `DeckAnalysis.cshtml:91` `data-cache-key="prompt-packets"` | `deck-sync.ts:919,1159`; `moxfield-extension-bridge.ts:240` |
| `prompt-deck-comparison` | `DeckComparison.cshtml:173` `data-cache-key="prompt-deck-comparison"` | `moxfield-extension-bridge.ts:249` |
| `prompt-cedh-meta-gap` | `CedhMetaGap.cshtml:42` `data-cache-key="prompt-cedh-meta-gap"` | `moxfield-extension-bridge.ts:256` |
| `prompt-deck-url` | `DeckAnalysis.cshtml:163` `data-sync-panel="prompt-deck-url"` | `deck-sync.ts:72` `urlSelector` |
| `prompt-deck-text` | `DeckAnalysis.cshtml:169` `data-sync-panel="prompt-deck-text"` | `deck-sync.ts:73` `textSelector` |

No side left on the old `chatgpt-*` literal (gate d, confirmed empty grep).

---

## Deviations from Plan

### Auto-fixed / auto-handled issues

**1. [Rule 3 - blocking gate false-positive] Plan's literal byte-identical verify one-liner returns DIFFER for reasons unrelated to the rename**
- **Found during:** Task 1
- **Issue:** the one-liner's chatgpt/prompt-only normalization doesn't account for per-request CSRF tokens, content-hash cache-bust query strings, or the pre-existing async set-catalog widget.
- **Fix:** wrote a supplementary, diff-time-only, non-destructive normalization layer (documented above and in `key-decisions`) to produce a truthful structural comparison; reported BOTH the literal script's raw result and the corrected result transparently rather than silently overriding or hiding the literal gate's FAIL.
- **Files modified:** none (comparison logic lives in an uncommitted scratchpad script, not in the repo).

**2. [Rule 3 - blocking gate false-positive] Gate (c) flags 4 unrelated Studio files**
- **Found during:** Task 1
- **Issue:** a concurrent session's Studio review-queue-notes feature landed on the shared branch between the 85-03 and 85-04 commits, and the literal `git diff --name-only` gate command has no way to distinguish "unrelated concurrent work" from "an actual keep-list violation."
- **Fix:** manually verified none of the 4 flagged files are D3 keep-list files, and confirmed no actual keep-list `.cs` file was touched; documented the false-positive clearly for the human checkpoint rather than silently passing or silently failing.
- **Files modified:** none (these Studio files are untouched by this plan; already committed by the other session).

**3. [Rule 1 - bug] Discarded a stale/partial `render-snapshot-post85.json`**
- **Found during:** Task 1 (before any capture)
- **Issue:** an untracked, partially-generated `render-snapshot-post85.json` already existed in the working tree (per the executor's own instructions, flagged as possibly stale). Its `/deck-analysis` HTML was ~3x the expected size across all 4 themes.
- **Fix:** deleted and regenerated from scratch using a freshly authored capture script that exactly mirrors 85-01's documented harness/ordering contract.
- **Files modified:** `.planning/phases/85-chatgpt-naming-cleanup/render-snapshot-post85.json` (recreated).

**4. [Rule 3 - blocking issue] `javaScriptEnabled: false` capture attempt introduced a real behavioral discrepancy**
- **Found during:** Task 1 (mid-investigation)
- **Issue:** disabling JS to sidestep the set-catalog widget noise also suppressed a JS-driven active-step-tab class, causing `.prompt-step-tab`'s computed style to diverge from baseline for a REAL reason (missing JS-applied active state), not a noise artifact.
- **Fix:** reverted to JS-enabled capture (matches baseline exactly across all 24 cells' computed styles) and instead neutralized the set-catalog widget's dynamic content at diff time only.
- **Files modified:** none (capture script is uncommitted scratchpad tooling).

**5. [Rule 1 - bug, out of scope] Reverted 5 incidentally-modified Playwright screenshot baselines**
- **Found during:** after running the full e2e suite
- **Issue:** `.planning/ui-design/cycle13/screenshots/bracket-*.png` (5 files) were touched as a side effect of running `bracket-smoke.spec.ts`'s screenshot-capture assertions.
- **Fix:** `git checkout --` on the exact 5 paths (not a blanket reset) to restore them, since they're unrelated to this plan's scope.
- **Files modified:** none in the final commit (reverted before staging).

### Not fixed / deferred to human

- The 2 D3/D5 semantic judgments (copy-wording ratification for the 4 reworded validation strings + `ChatGptSwapPrompt` rename copy-value confirmation) are explicitly left for the Task 2 human checkpoint, per the plan's design.

## Issues Encountered

See "Deviations" above — all resolved before this artifact was committed. No unresolved build/test/e2e failures.

## User Setup Required

None — no external service configuration required.

## Human Sign-Off (Task 2 — APPROVED 2026-07-05)

The coordinator approved the human-verify checkpoint:

> approved — keep the "your AI" reworded validation strings (aligns with the site's existing "your AI" convention; ChatGPT branding on titles/ledes/descriptions preserved). D3 keep-list confirmed intact and D5 lockstep confirmed.

- **D3 keep-list:** confirmed intact. The 4 reworded client-side validation strings ("...returned from your AI...") + the "generating prompt packets" string are RATIFIED to keep — they align with the site's existing "your AI" convention, and all ChatGPT branding on page titles, ledes, and meta descriptions is preserved (directly corroborated by the byte-identical HTML diff in Gate Results item 1, which shows those marketing/meta phrases unchanged verbatim).
- **`ChatGptSwapPrompt` -> `PromptSwapPrompt`:** confirmed the emitted swap-prompt string VALUE is unchanged (rendered `/manabase` textarea copy identical, per the zero-diff manabase snapshot).
- **D5 cache-key contract:** confirmed moved in lockstep across all 5 files, no side left on an old literal.
- **Out of scope (per coordinator):** pre-existing step-tab / theme / layout-picker / checklist styling UI bugs are NOT touched here and are handled as separate work; the byte-identical proof confirms the rename did not affect them.

## Next Phase Readiness

- **AICLEAN-01/02/03 all marked COMPLETE** (`gsd-sdk query requirements.mark-complete` — AICLEAN-01 flipped now; 02/03 were already complete from 85-03/85-04). Phase 85 is closed; ROADMAP row 85 = 5/5 Complete.
- Cycle 15 remaining: Phase 84 Plan 02 (THEME-02/03 no-drift proof) and Phase 86 (UI Audit Re-Score & Studio Stage 4 Closeout).

---
*Phase: 85-chatgpt-naming-cleanup*
*Completed: 2026-07-05 (Task 1 automated gate + Task 2 human sign-off approved)*

## Self-Check: PASSED

`render-snapshot-post85.json` exists at the claimed path; commit `f7672ae1` (Task 1 artifact) is present in `git log`. All 4 grep gates, build 0/0, full xUnit (2603 passed), and full Playwright e2e (256 passed) verified green in-session. Human sign-off recorded above.
