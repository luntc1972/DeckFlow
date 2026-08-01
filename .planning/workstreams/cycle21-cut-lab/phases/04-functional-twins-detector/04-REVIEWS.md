# Phase 4 (Functional-Twins Detector) — Claim-vs-Code Plan Review

**Round 1 — 2026-08-01. Reviewer: Codex `gpt-5.6-sol`, effort medium, `-s read-only`, rooted at the
rebased worktree. Verdict: CHANGES REQUIRED. All 14 findings folded; re-review pending.**

## Why this review was re-run

The earlier Phase 4 review output (2026-07-29, alongside `04-PLAN-CHECK.md`) was lost before it could
be folded. Re-running was the better path regardless: those plans were graded against a **pre-rebase**
tree, and `gsd/cycle21-cut-lab` has since been rebased onto `main`, absorbing 46 commits including the
Scryfall resolution rework and four rounds of combo-ordering fixes. A plan validated against code that
no longer exists is not validated.

That judgement was immediately vindicated — BLOCK 1 exists **only** because of a commit that landed in
that window.

## BLOCK

**B-1 — Wave 3 pinned the pre-rebase tally exclusion set and would delete a combo-protection fix.**
Confidence 10/10. Verified against source by Claude before folding.

`04-03-PLAN.md` asserted `ExcludedFindingKindsFromTally` contains *exactly* `WeakFloorCase`,
`RedundantFinishers`, `ComboProtected` — repeated at four sites, including a reflection test asserting
exact membership. HEAD has a **fourth** member, `EnablerStarved`, added by `31042a36` after the plan
was written. It is load-bearing: `ComboProtected` and `EnablerStarved` are emitted from the same
`nearCombos` input under the same threshold, so excluding only the protective one scored combo pieces
`+1` and sorted them *toward* the top of round 1.

Worse, the plan mandated a comment describing `EnablerStarved` as **excluded from** the set. Following
the plan literally either fails immediately or "fixes" the failure by deleting the guard — silently
reintroducing the defect, with a green test.

*Folded:* all four sites corrected; the reflection test now asserts **only** `FunctionalTwins`
absence, never exact membership, with the rationale recorded inline.

**B-2 — Wave 4's blocking human checkpoint requires a state the detector forbids.** Confidence 10/10.
Verified.

Wave 2 excludes locked cards from twins eligibility (`!card.IsLocked` in the eligible-set predicate).
Wave 4 nonetheless made "a locked evidence chip renders correctly **on twin evidence**" an acceptance
criterion, a UAT step, and user-facing help copy ("a card here may also be combo-protected or locked").
Production twins output can never supply a locked chip. The gate was unsatisfiable and the help text
described an unreachable state.

*Folded:* locked-chip verification moved to a non-twin finding kind; help copy corrected; acceptance
criterion rewritten.

## HIGH

**H-3 — Wave 1's six-detector scope fence was decorative.** The D-15 regression used only a
curve-congestion fixture, leaving `StrandedSubtheme`, `RedundantFinishers`, `WeakFloorCase` and
`ComboProtected` unexercised — so it survives the exact mutation it exists to catch.
*Folded:* composite fixture firing all five pool-consuming detectors, plus an assertion that all five
actually fire (so a degraded fixture fails loudly). `EnablerStarved`'s exclusion is now stated, not
left to be rediscovered.

**H-4 — the controller's own flag read had no coverage.** `CutLabApiController.DecideAsync` calls
`BuildFindingsAndRoundPlan` itself (`:100`) and feeds the result to `DetermineRoundKey` (`:106`),
which selects the **round recorded on the persisted decision**. The patch builder is a separate
invocation (`:118`). A controller-side regression changes which round a decision is filed under while
the returned patch still looks correct — invisible to every planned test.
*Folded:* tests 10-12 added (ON, OFF, missing-key), asserting the persisted round.

**H-5 — `PatchBuilder_TwinsFlagOff_DetectorIsNotInvoked` could not prove non-invocation.**
`IFeatureFlagCache.Snapshot()` takes **no key** and returns the whole dictionary, so no fake can
attribute a read to the twins key; and output-absence alone cannot separate "never ran" from "ran and
was filtered."
*Folded:* claim narrowed to observable OFF behavior, asserted as a **pair** (OFF yields nothing / the
same pool ON yields asserted output — the ON leg is what proves the fixture is not merely inert). A
production seam is offered as option (b) but explicitly requires operator sign-off.

**H-6 — the mandated merge-insertion algorithm does not preserve first-occurrence order.** The
capture-index-then-ascending-insert scheme reverses merged sections whenever two share an insert
index. **This is a live defect today**, not one Phase 4 would introduce: `[Curve, WeakFloor,
ComboProtected]` renders as `[Curve, ComboProtected, WeakFloor]`. Nothing catches it because no test
calls `BuildFindingGroups` directly. A third merged kind compounds it.
*Folded:* replaced with a single-pass placeholder build (no indexes, no deferred inserts). Tests 5 and
7 added, both of which **fail on the pre-change tree**. Follow-up F-04-03, which had deferred exactly
this refactor as cosmetic, is marked RESOLVED IN-PHASE — the duplication was hiding the arithmetic
nobody had checked.

**H-7 — VSTest gates conflict with the WSL constraint.** *Partially accepted.* The reviewer overstated
it: all seven `<automated>` gates are already **filtered** and already use the Windows `dotnet.exe`
path, which runs reliably from WSL and was exercised repeatedly on 2026-08-01. The real hazard is
narrower and empirical — a Codex executor failed to complete the **full** suite five consecutive times
that day (hangs, orphaned `testhost.exe`, exit 143), correctly refusing to claim a pass each time.
*Folded:* full-suite runs reassigned from the executor to the reviewer (Claude-run or push-and-watch);
filtered gates remain executor-owned. A summary claiming an unobserved full-suite pass is a reportable
defect.

## MEDIUM

| # | Finding | Fold |
|---|---|---|
| M-8 | Wave 3's "exactly seven" constructor count; the bullets enumerate **eight** (nested patch builders were being counted with their controllers) | corrected to 8, all sites individually numbered |
| M-9 | `Compute_Evidence_IsOrderedByDescendingManaValueThenName` — within a group every member shares an MV *by definition*, so the descending-MV clause is degenerate and only the name tiebreak is mutation-sensitive | renamed to `..._IsOrderedByNameAscending`; the MV claim moved to test 11 (cross-group) |
| M-10 | Two determinism tests re-ran the *same* input; stable enumeration makes them pass with the tiebreaks deleted | both rewritten to compare **permuted** inputs and assert the exact expected order |
| M-11 | Wave 4's first-heading test used identical headings throughout, so "first", "last" and "any" are indistinguishable | sentinel headings required on later items |
| M-12 | Wave 1's "byte-identical runtime" claim was unconditional; the widened commander test does change behavior for a caller passing an inconsistent name set | narrowed to "every supported page and API flow", with the one deliberate, tested exception named |
| M-13 | Wave 1 frontmatter omitted `CutLabStructuralFindingsTests.cs`, which Task 3 modifies | added |

## LOW

**L-14 — stale source citations after the rebase.** `CutLabPageService.IsFlagOn` is now `:622-626`
(cited as `:507-511`), the engine call is `:505` (cited as `:391`), and the weak-floor merge coverage
is around `CutLabPageServiceTests.cs:985-1015` (cited as `:655-665`). *Folded:* refreshed across all
four plans and `04-RESEARCH.md`.

## Audits that passed

Recorded because they are the traps this codebase has actually sprung before, and they were checked:

- **No phantom APIs.** `CardTypeLine.PrimaryType`, `CutLabRoleAssigner.DisplayLabelFor`,
  `TypeGroupOrder`, `CutLabFloorRules.RoleKeys`, `CutLabCommanderNames.Resolve` and every named engine
  method exist with compatible signatures.
- **No interface member is added to `ICutLabAnalysisContextBuilder`.** HEAD has seven
  `FakeAnalysisContextBuilder` implementers; none needs a default-interface-member repair for this
  phase. (This was the CS0535 trap from the Scryfall work — it does not apply here.)
- **`CutLabAnalyzedCard` stays positional for its original five parameters**; the three additions are
  `init` properties, so every existing construction site remains source-compatible.
- **Wave sequencing is genuine** — `04-01 → 04-02 → 04-03 → 04-04`, no later-wave dependency consumed
  early.
- **The twins tally does not repeat the raw-name match-back defect.** Twins evidence originates from
  the same analyzed pool names `BuildInputs` uses; the normalized combo-demotion path
  (`CutLabCutRoundEngine.cs:245`) stays safe **provided B-1 is fixed** — which it now is.

## Status

All 14 findings folded 2026-08-01. Re-review with `gpt-5.6-sol` at medium effort is **owed** before
Phase 4 execution begins, per the plan-review convergence rule: a revised plan is not converged until
the reviewer says so.
