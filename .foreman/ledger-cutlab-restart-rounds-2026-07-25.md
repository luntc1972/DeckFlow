# Foreman Ledger — Cut Lab: restart rounds 1 & 2
BASELINE: 74b456ba0926d3176c99130b575d18b2a2fa6e11 | worktree busy: sticky-bar ticket (job bmgtwd535) actively running | 2026-07-25T10:20:00-06:00

## User intent (clarified via AskUserQuestion — do NOT reinterpret)
User does NOT want to clear/reset everything. Quote: "do not clear I just want to have a
chance to run through the rounds again to try to find additional cuts in the first and
second round." So: re-surface Round 1 + Round 2 REJECTED/DEFERRED cards for fresh
reconsideration under current findings — WITHOUT touching any already-Accepted (actually
cut) cards, and WITHOUT touching Round 3 / deferred-second-pass / rejected-second-pass /
whatif-swap decisions.

## Mechanism (verified by reading the actual round engine, not assumed)
- CutLabCutRoundEngine.BuildQueue (DeckFlow.Web/Services/CutLab/CutLabCutRoundEngine.cs
  ~line 148-239) recomputes round1/round2/round3 buckets FRESH on every call, from
  `firstPassCards` = cards in `eligibleCards` with NO decision at all (accepted cards are
  already excluded from `eligibleCards` via `acceptedCardNames`, line 181). Bucket
  assignment (round1 = 2+ findings, round2 = 1 finding, round3 = 0 findings) is based on
  CURRENT finding tallies, not tallies at original-decision time.
- `CutLabDecision` (DeckFlow.Web/Models/CutLab/CutLabState.cs ~line 78-91) has a `Round`
  field recording which round key the decision was made under, and `Kind`
  (Accepted/Rejected/Deferred).
- `CutLabDecisionApplier.Restore(state, cardName)` (~line 80-90) already shows the pattern:
  strip decisions by predicate, return `state with { Decisions = remaining }` through
  `CutLabLockRules.EnforceCommanderLock`. The new bulk operation is the same shape, just a
  different (multi-card) predicate: `Round is Round1Key or Round2Key AND Kind is Rejected
  or Deferred` — remove those, keep everything else (Accepted cuts, Round3 decisions,
  deferred/rejected-second-pass decisions, whatif-swap decisions all untouched).
- Dual-path convention already established for every other Cut Lab mutation: JSON API
  (`CutLabApiController`, `/api/cut-lab/*`, AJAX + `CutLabUiPatch` response consumed by
  `applyServerPatch`) AND a no-JS full-page-POST fallback (`CutLabController`,
  `/cut-lab/*`). New feature must follow the same dual-path shape for consistency with
  existing e2e no-JS coverage.

## Plan
1. [WORKHORSE] New `CutLabDecisionApplier.RestartRounds(CutLabState state, IReadOnlyList<string> roundKeys)` (or similarly named) — strips Rejected/Deferred decisions whose `Round` is in `roundKeys` (call site passes `[Round1Key, Round2Key]`). Reuse `CutLabLockRules.EnforceCommanderLock` wrap like `Restore` does.
2. [WORKHORSE, same ticket] New JSON endpoint on `CutLabApiController` (e.g. `POST /api/cut-lab/restart-rounds`, request = just `CutLabStateJson` + antiforgery, no cardName needed) that applies the new applier method then returns through the SAME `CutLabUiPatchBuilder`/`CutLabUiPatch` machinery the decide endpoint uses, so the client can reuse `applyServerPatch` unchanged.
3. [WORKHORSE, same ticket] New no-JS form endpoint on `CutLabController` (e.g. `POST /cut-lab/restart-rounds`) mirroring the `/cut-lab/decide` pattern (full page re-render via `CutLabViewModel.From`).
4. [WORKHORSE, same ticket] Razor: new button near the "Cut rounds" heading (CutLab.cshtml, inside `#cut-lab-step-panel-2`, near the existing round-banner/proposal area) — "Restart rounds 1 & 2" or similar copy (Codex's call on exact wording, keep terse matching existing style e.g. "Recalculate goals", "Delete package"). Both a JS-driven form (posts to the new API endpoint via fetch, like other decide actions) and the no-JS `<form>` fallback posting to the new no-JS endpoint.
5. [WORKHORSE, same ticket] TS: fetch handler for the new action (mirror the existing decide-submit fetch pattern in cut-lab.ts), lightweight `window.confirm(...)` before submit (mirror `deletePackage`'s confirm pattern) — non-scary wording since nothing destructive happens (no cuts undone), something like "Reconsider rejected/deferred cards from Round 1 & 2 with today's findings?" (Codex's call on exact copy). On success, `applyServerPatch` as usual.
6. [WORKHORSE, same ticket] Tests: xUnit for `CutLabDecisionApplier.RestartRounds` (accepted cards untouched, round-3/second-pass/whatif decisions untouched, round-1/2 rejected+deferred cleared) + controller test for the new endpoint(s). vitest for the new TS handler (confirm-then-submit, cancel-does-nothing). e2e: extend an existing cut-lab decide-flow spec (e.g. cut-lab-tuning.spec.ts or cut-lab-structure.spec.ts) with a scenario: reject a round-1 card, defer a round-2 card, accept an unrelated card, restart rounds 1&2, confirm the accepted card stays cut, the rejected/deferred cards reappear in the queue.

## Routing
- Single Codex WORKHORSE ticket (gpt-5.4 medium) — coherent cross-layer feature (C# applier + 2 endpoints + Razor + TS + CSS-adjacent button styling reuse + tests), not parallelizable across sub-tasks (all touch the same request/response contract).
- Verification: Claude foreman-verifier (blind, cross-family) — focus on the exclusion logic (Accepted cards + Round 3 + second-pass + whatif MUST remain untouched) since that's the highest-value correctness property the user explicitly called out ("do not clear").

## Tasks
| id | lifecycle | owned paths (WRITE SET) | job id |
|---|---|---|---|
| T1 | DISPATCHED | DeckFlow.Web/Services/CutLab/CutLabDecisionApplier.cs, DeckFlow.Web/Controllers/Api/CutLabApiController.cs, DeckFlow.Web/Controllers/CutLabController.cs, DeckFlow.Web/Models/Api/*.cs (new request/response DTOs if needed), DeckFlow.Web/Views/Deck/CutLab.cshtml, DeckFlow.Web/wwwroot/ts/cut-lab.ts, DeckFlow.Web/wwwroot/css/site-common.css (button styling only, reuse existing classes if possible), DeckFlow.Core.Tests or DeckFlow.Web.Tests (new xUnit), DeckFlow.Web/ts-tests/*.ts (new/extended vitest), DeckFlow.Web/e2e/*.spec.ts (extended) | bash bg job btbb66t78 |

## Attempts
- T1 | attempt 1 | Codex WORKHORSE gpt-5.4 medium | rev1 | dispatched 2026-07-25T12:05 — pending

## Decisions
- Excluded Round 3 from scope per explicit user wording ("first and second round").
- Excluded Accepted decisions from scope per explicit user wording ("do not clear").
- New DEDICATED endpoint chosen over overloading the existing per-card `/api/cut-lab/decide`
  endpoint, because this operates on many cards with no single `cardName`, and the existing
  `CutLabDecideAction` enum / `CutLabDecisionApplier.Apply` signature is fundamentally
  per-card (`ArgumentException.ThrowIfNullOrWhiteSpace(cardName)`) — forcing this into that
  shape would need an awkward sentinel cardName. A parallel small endpoint reusing the same
  patch-response plumbing is cleaner and lower-risk.
- Confirm dialog: included by default (matches app's existing pattern for state-changing
  bulk actions), but framed non-destructively since nothing is actually lost.

## Scratch
(none yet)
