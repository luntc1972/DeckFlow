# Foreman Ledger — Cut Lab sticky bar: locked + current/target counts
BASELINE: 74b456ba0926d3176c99130b575d18b2a2fa6e11 | clean except unrelated stale screenshot PNGs under .planning/ui-design/cut-lab/screenshots/ (not touched) | 2026-07-25T10:10:00-06:00

## Plan
1. [WORKHORSE] Extend CutLabStickyBarView + BuildStickyBar (CutLabViewModel.cs) with LockedCount + CurrentCount; wire the call site.
2. [WORKHORSE, same ticket] Razor: always render .cutlab-sticky-bar (already inside Model.HasResult gate); add Locked + Current/Target spans always-visible; keep round/remaining/accepted spans gated on HasStickyBar.
3. [WORKHORSE, same ticket] TS: add getters for the 2 new spans; STOP removing the whole bar on terminal state in patchStickyBar (currently `getStickyBar()?.remove()`) — only clear/hide round-specific spans; keep Locked/Current always updated (Locked via existing updateLockedCountChip DOM-scan, Current via patch.currentCount which the DTO already carries).
4. [WORKHORSE, same ticket] CSS: new __locked/__current span styles mirroring existing __count/__accepted; verify/fix mobile crowding (up to 5 items in a 44px single-row flex bar when a round is active).
5. [WORKHORSE, same ticket] Tests: xUnit (CutLabViewModelWordingTests.cs or new), vitest (fix cut-lab-proposal.test.ts:752 which currently asserts the bar is REMOVED on terminal — now false), e2e spot-check.

## Routing
- All 5 sub-tasks as ONE Codex ticket (WORKHORSE, gpt-5.4 medium) — single coherent cross-layer feature, not independent/parallelizable (same files feed each other: view model fields -> Razor -> TS getters -> CSS -> tests).
- Verification: Claude foreman-verifier (blind, cross-family), focused on the terminal-state behavior change (highest regression risk) + mobile CSS.

## Tasks
| id | lifecycle | owned paths (WRITE SET) | job id |
|---|---|---|---|
| T1 | DISPATCHED | DeckFlow.Web/Models/CutLabViewModel.cs, DeckFlow.Web/Views/Deck/CutLab.cshtml, DeckFlow.Web/wwwroot/ts/cut-lab.ts, DeckFlow.Web/wwwroot/css/site-common.css, DeckFlow.Web.Tests/CutLabViewModelWordingTests.cs (or new xUnit file), DeckFlow.Web/ts-tests/cut-lab-proposal.test.ts, DeckFlow.Web/ts-tests/cut-lab-adjust.test.ts, DeckFlow.Web/ts-tests/cut-lab-whatif.test.ts, DeckFlow.Web/ts-tests/cut-lab-structural-cardtext.test.ts, DeckFlow.Web/ts-tests/cut-lab-structural-evidence-lock.test.ts, DeckFlow.Web/e2e/cut-lab-structure.spec.ts, DeckFlow.Web/e2e/cut-lab-nav-themes.spec.ts | bash bg job bmgtwd535 |

## Attempts
- T1 | attempt 1 | Codex WORKHORSE gpt-5.4 medium | rev1 | dispatched 2026-07-25T10:12 — pending

## Decisions
- Scope decision: sticky bar stays structurally where it is (inside the Cut rounds/Decide step-panel), NOT hoisted to a page-global fixed header. User approved "sticky bar version" of a proposal that described extending the existing component; a page-global header would be a materially bigger layout change not what was pitched.
- Locked count: computed CLIENT-SIDE (reuse existing updateLockedCountChip DOM-scan), NOT added to the server patch DTO — avoids new DTO/builder plumbing since the pool table rows are already in the DOM and this is more real-time than a round-trip value anyway.
- Current count: reuses the patch's EXISTING CurrentCount field (already transmitted, confirmed via CutLabUiPatchBuilder.cs:112,145) — no DTO change needed for this either.
- Consent/mode: Codex-boosted, consent already granted this session.
- Found during investigation (not yet fixed): patchStickyBar (cut-lab.ts:2794-2797) currently calls `getStickyBar()?.remove()` when a round goes terminal — this must change or "always visible" breaks the moment a round finishes. Flagged explicitly in the ticket.

## Scratch
(none yet)
