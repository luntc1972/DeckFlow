# Foreman Ledger — Cut Lab anchor-nav + collapse for 5 sections
BASELINE: 48daa68012b8937b3365aa4847d417cf1d213705 | dirty (uncommitted: cut-lab.ts, cut-lab-nav-themes.spec.ts, misc regenerated e2e screenshots; untracked: .planning/debug/package-select-not-updated.md) | 2026-07-25T09:42:48-06:00

## Plan
1. [WORKHORSE] Wrap 5 sections (How your pool competes / By type / By subtype / Goals / Export) in `<details class="cutlab-collapsible" data-cutlab-mobile-collapse open id="cut-lab-section-{slug}">`, mirroring the existing Structural-findings/Lock-your-pool pattern. Pure additive HTML in CutLab.cshtml, no TS/JS logic changes.
2. [WORKHORSE, same ticket] Add anchor-nav `<li>` entries for competes/by-type/by-subtype; retarget existing Goals/Export nav `<li>` hrefs from `#cut-lab-step-panel-{3,4}` to the new `#cut-lab-section-{goals,export}` details ids.

## Routing
- Task 1+2 (single ticket, mechanical structural HTML mirroring an established pattern) → Codex WORKHORSE (gpt-5.4, effort medium — session default, confirmed with user this session) — well-specified, no architectural judgment, matches an in-file precedent exactly.
- Verification → cross-family: Claude foreman-verifier (blind, fresh context) reads the diff against the original task text.

## Tasks
| id | lifecycle | owned paths (WRITE SET) | job id |
|---|---|---|---|
| T1 | VERIFIED | DeckFlow.Web/Views/Deck/CutLab.cshtml | bash bg job b81bo9ot4 |

## Attempts
- T1 | attempt 1 | Codex WORKHORSE gpt-5.4 medium | rev1 | DONE — dotnet build clean, vitest 7/7, e2e nav-themes 8/8, diff --ignore-all-space 20+/2-, EOL 0/0 unchanged
- T1 | verify 1 | Claude foreman-verifier (blind, cross-family) | — | PASS_WITH_NOTES — 9/10 criteria clean; the 1 flagged item (git status not scoped to only this file) is expected shared-worktree state from the sibling debug-fix task, not a defect in this diff. No code issues found.

## Sibling work (not this ledger's task, same worktree)
- Debug fix "package-select-not-updated" (job bj3gsbaqo) landed 2026-07-25T10:15, uncommitted,
  awaiting user manual test before commit (see .planning/debug/package-select-not-updated.md).
  T1 was held until that job finished to avoid a shared-worktree collision; it has now
  completed and the tree was quiescent at T1 dispatch time.

## Decisions
- 2026-07-25T09:42 — HELD dispatch of T1. Rationale: this worktree (`deckflow-cutlab-fixes`, branch `feat/cutlab-fixes`) currently has a DIFFERENT Codex worker actively running in the background (job `bj3gsbaqo`, dispatched from the parent session for the package-select-not-updated bugfix), editing `cut-lab.ts` + `cut-lab-nav-themes.spec.ts`, not yet committed. Even though T1's write set (CutLab.cshtml) is disjoint from that job's files, both processes share one physical working tree/git index/obj-bin output — running `dotnet build`/vitest/playwright concurrently from two uncoordinated processes in the same directory risks a shared-worktree collision (per project's own recorded incident, see feedback_shared_worktree_collision memory). Sequencing instead: wait for job `bj3gsbaqo` to report and its changes to land (committed or at least settled), THEN dispatch T1 into the now-quiescent tree.
- Codex consent: already granted this session (user confirmed "keep defaults" for gpt-5.5 review/plan + gpt-5.4 coding on prior Codex model question this session) — no re-ask needed for T1.
- Mode: Codex-boosted (Agent tool + real shell + consented working Codex, confirmed this session via the earlier debug-session-manager + codex exec dispatches).

## Scratch
(none yet)
