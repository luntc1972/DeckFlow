# Cut Lab — deferred follow-ups (captured 2026-07-22)

Surfaced during the post-Cycle-18 quality pass (milestone simplify audit + UI review). All three were **deliberately deferred** — verified real, but out of scope for a mechanical cleanup and/or carrying regression surface that warrants its own scoped change. Cycle 18 (phases 101–107) is complete and green; these are future work.

---

## 1. [High] Client controller re-derives server domain state — `cut-lab.ts`
**Source:** simplify audit finding F1 (DRY + SOLID/SRP), confirmed against code.
**Evidence:** `cut-lab.ts` rebuilds serialized `CutLabState` from DOM, computes accepted cuts, current count, what-if options, cards-remaining — all of which have authoritative server equivalents (`CutLabWorkingList.Derive`, `CutLabDecisionApplier`, `CutLabLegality.LegalMax`). Concrete drift symptom: `cut-lab.ts:1800` clamps `Math.max(qty + delta, 0)` (no upper bound) while `CutLabWorkingList.cs:44` clamps `Math.Clamp(..., CutLabLegality.LegalMax(name))`.
**Impact:** browser can display export-eligibility / counts / what-if options that disagree with server acceptance rules; every new state field or quantity rule needs coordinated C# + TS edits.
**Why deferred:** the drift can't be cheaply fixed — porting `LegalMax` to TS *worsens* DRY; the correct fix is the server returning complete `CutLabUiPatch` DTOs (serialized state + counts + export eligibility + what-if options + proposal/finding rows) for every mutation so TS renders instead of derives. That's an architecture change with real regression surface across decide/adjust/what-if. **Own phase.**
**Rough scope:** new `CutLabUiPatch` DTO; adjust/decide/what-if endpoints return it; strip client-side domain derivation; contract tests + e2e. ~1 phase.

## 2. [Medium] What-if preview/commit logic split across 3 places
**Source:** simplify audit finding F2 (DRY + SRP), confirmed.
**Evidence:** validation + accept/restore orchestration duplicated in `CutLabApiController` (whatif action), `CutLabController` (no-JS `/cut-lab/whatif`), and `CutLabWhatifPreviewService`. A future change to swap eligibility / commander-lock / partial-quantity handling must touch 3 sites; a missed edit diverges JS vs no-JS.
**Why deferred:** consolidation touches both the JS and no-JS keep flows (both e2e-covered) — moderate regression surface, better as a focused change than bundled into a mechanical pass.
**Rough scope:** expand `ICutLabWhatifPreviewService` → `ICutLabWhatifService` with `PreviewAsync` + `CommitAsync`; controllers bind/authorize/log/delegate only; commit result `{ Applied, State, Message }`. ~½ phase.

## 3. [UX] Mobile single-page scroll — sticky step-nav / jump-to-section
**Source:** UI review observation (Claude + Codex cross-AI confirmed).
**Evidence:** Cut Lab renders the whole workflow as one continuous scroll (~14,000px on 430px mobile). The `Process/Decide/Goals/Export` step-tabs submit forms / advance server-side; they do **not** scroll to sections (`cut-lab.ts` has no `scrollIntoView` wiring), so a tab that looks like jump-nav doesn't jump.
**Already done (Phase-107 follow-up, 2026-07-22):** collapsed the 3 auxiliary sections (Packages/Scenarios/What-if) on mobile via `<details>` — correct + green, but only ~3% height reduction (those sections are small; the 14k is dominated by core tables that must stay visible).
**Why deferred / real fix:** collapse alone can't cut the scroll without hiding primary content. The impactful fix = **sticky step-nav + click→`scrollIntoView`** so mobile users skip past the long core tables without hiding them. Deferred because it touches the shared `_WorkflowStepTabs.cshtml` partial (used by all Deck tools) + the tabs' existing submit semantics — needs careful scoping to avoid regressing other tools. Must preserve the no-JS fallback.
**Rough scope:** section anchors + a Cut-Lab-scoped sticky step-nav (position:sticky) + progressive-enhancement scroll-on-tab-click that doesn't fight submit tabs; themes × mobile verification. ~½ phase.

---
*Captured after Cycle 18 close. Not blocking. Promote via the milestone backlog when Cut Lab work resumes.*
