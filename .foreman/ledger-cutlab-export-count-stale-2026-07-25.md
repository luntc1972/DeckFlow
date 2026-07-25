# Foreman Ledger — Cut Lab: Export panel card-count stale after AJAX cuts
BASELINE: 2d47b756 | clean except unrelated stale screenshots + 2 queued ledger files | 2026-07-25T11:42:00-06:00

## Root cause (confirmed by gsd-debugger investigation, .planning/debug/export-cardcount-mismatch.md)
Export panel's "Card count = N" (CutLab.cshtml:874-882, `.cutlab-export__status`) only
recomputes on a full server round-trip. The AJAX decide/adjust flow already computes and
ships the correct number every time as `CutLabUiPatch.currentCount` (CutLabUiPatchBuilder.cs
~321-341, confirmed identical formula to every other call site — NOT a computation bug) —
but `cut-lab.ts`'s `applyServerPatch`/`patchStickyBar` only apply that value to the sticky
bar's counter and to enable/disable the Export tab (`setExportEnabled`), never to the Export
panel's own status text. So after any AJAX-only cut, the Export panel silently shows a
stale, too-high count until a full reload or a real export submission. Same architectural
class as the already-documented `.planning/debug/cutlab-decide-ajax.md` Symptom 2
(compare-to-baseline staleness) and today's earlier Goals-recalculate discussion — a
recurring pattern of "sections outside applyServerPatch's covered set go stale."

## Plan (single small Codex ticket)
1. Add a stable hook to the Export panel's status block (CutLab.cshtml ~874-882), e.g.
   `data-cut-lab-export-count` on the `.cutlab-export__status` div (or on the `<strong>`/
   `<span>` children individually — Codex's call on the cleanest DOM shape), so both the
   ✅/❌ icon+text AND the "Reach 100 cards..." helper line can be swapped client-side.
2. In cut-lab.ts, add a function (mirror `patchStickyBar`'s shape) that updates this block
   from `patch.currentCount`: when `=== 100`, render "✅ Card count = 100" and hide/remove
   the helper span; when `!== 100`, render "❌ Card count = N" + the helper span. Reuse
   whatever emoji/string constants already exist if any (check for existing
   "Card count" string constants before hardcoding a duplicate).
3. Call this new function from `applyServerPatch` (alongside the existing
   `setExportEnabled(patch.canBuildExport)` call, since both react to the same
   `patch.currentCount`/`patch.canBuildExport` signal) so it fires on every decide/adjust
   AJAX response, not just sticky-bar updates.
4. Verify the no-JS fallback path is unaffected (that one already gets a correct count via
   full page re-render, per the investigation) — no changes needed there, just don't break it.

## Tests
- vitest: extend whatever test file covers `applyServerPatch`/`setExportEnabled` (search for
  `canBuildExport`/`setExportEnabled` in ts-tests/) with a case asserting the Export panel's
  status text updates after a patch with `currentCount` crossing the 100 boundary in both
  directions (over→100, and 100→under via an undo/restore).
- e2e: `DeckFlow.Web/e2e/cut-lab-export.spec.ts` already has a comment (~line 135-136)
  documenting the OLD "still server-stale" assumption for the finished-list textareas —
  check whether that comment/test needs updating now that the count itself is live-patched
  (the finished-list text blocks themselves may still legitimately require a real export
  submission — don't over-scope this fix to also live-patch the finished-list text unless
  asked; ONLY the "Card count = N" status line is in scope). Add a new e2e assertion:
  accept a cut via the AJAX decide flow, confirm the Export panel's Card-count text updates
  WITHOUT a page reload or an Export-tab submission.
- xUnit: none needed — this is a pure client-side patching gap; the server payload
  (`CutLabUiPatch.currentCount`) already existed and is already tested as correct.

## Routing
- Single Codex WORKHORSE ticket (gpt-5.4 medium) — small, self-contained, client-side only
  (Razor hook + TS function + call-site wiring), same shape as work already proven today
  (sticky bar patching).
- Verification: Claude foreman-verifier (blind, cross-family) — confirm the Export panel
  text actually updates via the patch path (not just that a function was added but never
  wired to fire), and that the 100/not-100 boundary (icon + helper text swap) is handled
  both directions.

## Tasks
| id | lifecycle | owned paths (WRITE SET) | job id |
|---|---|---|---|
| T1 | DISPATCHED | DeckFlow.Web/Views/Deck/CutLab.cshtml, DeckFlow.Web/wwwroot/ts/cut-lab.ts, DeckFlow.Web/ts-tests/*.ts (extended), DeckFlow.Web/e2e/cut-lab-export.spec.ts (extended) | pending |

## Attempts
- T1 | attempt 1 | Codex WORKHORSE gpt-5.4 medium | rev1 | dispatched 2026-07-25T11:45 — pending

## Decisions
- Reprioritized AHEAD of the queued restart-rounds (T2) and popup-P/T (T3) tickets — this is
  a confirmed live bug the user hit while testing, not a feature request; fix it first.
- Also bundling the 2 cosmetic nits from the sticky-bar verifier report (stale
  `HasStickyBar` XML doc comment, dead `getStickyBar` helper) into this same dispatch since
  it's touching the same two files — cheap to fold in, avoids a separate trivial dispatch.

## Scratch
(none yet)
