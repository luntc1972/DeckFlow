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
6 added, both of which **fail on the pre-change tree**. Follow-up F-04-03, which had deferred exactly
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

---

# Round 2 — 2026-08-01. Same reviewer/model. Verdict: CHANGES REQUIRED. All folded; round 3 owed.

**9 of 14 round-1 folds verified GOOD** (H-3, H-7, M-9, M-11, M-12, M-13, plus B-2's core fix and
H-6's diagnosis). **5 were INCOMPLETE in the same way, and it is worth naming the pattern:** I fixed
the *operative* instruction and left a contradicting copy in an acceptance criterion, a `<done>` line,
or a threat row. A plan that says one thing in the task and the opposite in its acceptance list hands
the executor a choice, and executors resolve contradictions by picking one.

| # | Leftover contradiction | Fixed |
|---|---|---|
| B-1 | threat row T-04-17 still claimed the test asserts the set's "exact contents" | rewritten to absence-only, with the reason |
| H-4 | acceptance still required "all nine tests" after 10-12 were added | → twelve |
| H-5 | acceptance + `<done>` still demanded proof of non-invocation | → the paired OFF/ON assertion |
| M-8 | the operative action still said "7 constructions" and called any other count a divergence | → 8 |
| L-14 | two stale citations survived (`~391`, `:111-117`) | → `:505`, `:119-125` |

**H-6's diagnosis was independently CONFIRMED** — the reviewer traced the index capture and insert
order itself and reached the same conclusion, so the live reversal defect is now double-sourced. But
the fold was internally contradictory: a paragraph further down still said *"do not refactor the three
near-identical branches."* Withdrawn explicitly; its stated reason ("would change the two pre-existing
kinds' behavior") is now the point of the change.

## New defects the folds introduced or exposed

1. **HIGH — the controller tests had an escape hatch that voids them.** The fold let tests 10-12 fall
   back to asserting `NextProposal`. Invalid *for these specific tests*: the controller's flag read
   feeds `beforeRoundPlan` → `DetermineRoundKey` (`CutLabApiController.cs:100`, `:414`), while the
   response's `NextProposal` comes from the **separately invoked** patch builder (`:118`) with its own
   flag read. The fallback passes whenever the patch builder is correct, even with the controller read
   hardcoded. *Fixed:* must assert the persisted `decision.Round`; `NextProposal` explicitly excluded.

2. **HIGH — the land-exclusion tests are double-gated and prove nothing.** Test 7's land cards sit in
   role `lands`, which is *also* absent from `TwinEligibleRoleKeys`. Delete `!card.IsLand` → the role
   filter still yields zero. Admit `lands` → the land filter still yields zero. Neither mutation is
   detectable. And it matters in production: `CutLabRoleAssigner` gates land-as-*ramp* but can still
   assign a land an eligible role such as draw (`:128`, `:141`). *Fixed:* new test 7b requires three
   `isLand` cards sharing one eligible **non-land** role.

3. **HIGH — three ordering fixtures could not detect their own mutations.** Test 10 never required
   non-alphabetical source order (deleting `ThenBy(Name)` passes); test 11 never required MV-2 to
   precede MV-5 (deleting the descending-MV sort passes); test 12 never required same-role/
   different-type ties (leaving `TypeGroupOrder` unexercised); density test 4 had the same gap and
   lacked an exact-order assertion. *Fixed: adversarial input order is now mandatory in each.*

   **Sub-finding worth keeping — the role-index tiebreak is STRUCTURALLY untestable.** Groups are
   collected by iterating the canonical `TwinEligibleRoleKeys` list and .NET's `OrderBy`/`ThenBy` are
   stable, so when MV and type tie, source order already equals role order. No input permutation can
   make its deletion fail — permuting the pool permutes cards, not the role loop. Kept as defence in
   depth, documented as deliberately unpinned. Do not manufacture a test that appears to cover it.

4. **MEDIUM — no test supplies two distinct non-merged kinds in adversarial order,** so "non-merged
   kinds keep encounter order" is preserved by the new design but unproven. Noted; tests 5 and 6 each
   carry only one non-merged group.

5. **LOW — presenter tests were numbered 1,2,3,4,5,7,6.** Renumbered; all cross-references updated.

## Status

Round 2 folded 2026-08-01. **Round 3 re-review owed** before execution — findings 1-3 changed test
semantics, and edits made under review pressure are themselves unreviewed. The convergence rule
stands: a revised plan is not converged until the reviewer says so.

---

# Round 3 — 2026-08-01. Same reviewer/model. Verdict: CHANGES REQUIRED (contradictions only).

**The shape of this failure is the important part: 9 findings, ALL contradictions, ZERO new
substantive defects.** Every round-2 substantive fix was verified correct against source:

- **test 7b genuinely defeats the land double-gate** — `draw` is in `TwinEligibleRoleKeys`
  (`CutLabFloorRules.cs:13`) and `CutLabRoleAssigner` assigns `lands` to a land while independently
  assigning `draw` when its oracle text qualifies (`:128`, `:141`), so the fixture is reachable;
- the ordering fixtures are now mutation-sensitive (10 non-alphabetical source order, 11 MV-2 before
  MV-5, 12 and density-4 same-role/different-type ties with permutation and exact expected order);
- the role-index-tiebreak-is-structurally-untestable claim is correct.

So the plan's **substance converged at round 2**. Round 3 failed purely on internal consistency.

## The repeating defect, named

Three rounds running, the same mistake: I corrected the *operative* instruction and left a
contradicting copy elsewhere in the same file — frontmatter `must_haves`, `<verification>`,
`<success_criteria>`, `<acceptance_criteria>`, a task `<name>`, a threat row, or a `read_first`
bullet. Nine such copies survived round 2:

| File | Stale copy | Now |
|---|---|---|
| 04-01 | frontmatter truth + verification claimed unconditional byte-identity | scoped to supported page/API flows |
| 04-02 | acceptance said "All 18 tests" after 7b made it 19 | 19, with an explicit do-not-omit-7b warning |
| 04-03 | completeness grep told the executor to report divergence from **7** | 8 |
| 04-03 | Task 3 `<name>` and the phase success criterion still demanded "not invoked" / "does not run" | paired OFF/ON output |
| 04-03 | tests 10-12 mandated `decision.Round` **and** allowed "another controller-owned outcome" | escape hatch closed; substitution now requires operator sign-off, and "no pool separates the round keys" is a finding to report |
| 04-03 | Task 1 acceptance pinned the initializer to exactly four members | assert nothing removed + `FunctionalTwins` not added; no count pin |
| 04-04 | `read_first` still called the old branches "the pattern to copy exactly" | marked as the defective pattern being replaced |
| 04-04 | action said "No indexes" then offered a parallel position-index dictionary | alternative withdrawn; one shape only |
| 04-04 | withdrawn-branch paragraph cited tests "5 and 7"; ordering is 5 and 6 | corrected |
| 04-04 | success criterion said the two pre-existing kinds are "behaviorally unchanged" | states the render-order correction explicitly |

**Process change applied:** counts are now verified mechanically rather than by eye. Actual list
lengths (04-02: 19, 04-03: 12, 04-04: 7) were grepped and reconciled against every numeric claim.

## Not blocking

The round-2 MEDIUM (no test supplies two distinct non-merged kinds in adversarial order) was
assessed as **not independently blocking** — the single-pass design and the acceptance contract
determine encounter order. Recorded as a prudent addition, not a gate.

## Status

Round 3 folded 2026-08-01. **Round 4 owed.** Convergence rule stands.

---

# Round 4 — 2026-08-01. Same reviewer/model. Verdict: CHANGES REQUIRED. **NOT folded — session paused.**

⚠ **The reviewer read the plans BEFORE two pause-time fixes landed. Its findings 1 and 5 are already
resolved — verify before acting on any finding here.**

- **F1 (already fixed)** — `04-03:448` "divergence if it is not the 7 enumerated above" → now **8**.
- **F5 (already fixed)** — `04-02:547` "at least 18 more executed tests" → now **19 (18 + 7b)**.

Both were caught from round 4's own reasoning trace during the pause and corrected in `2bf28b4b`.
They are the **fourth and fifth** instances of the incomplete-fold anti-pattern, and they reveal why
round 3's sweep missed them: the greps matched the exact sentences already fixed, not every phrasing
of the same count. **Sweep by number-word AND digit across all phrasings.**

## Five findings that remain OPEN

**F2 — HIGH. 04-03 Task 1 has an impossible `<automated>` gate.** Line 371 requires a solution-wide
build, but line 385 correctly states the test project **cannot compile until Task 2** repairs the
eight constructor sites. The prose permits deferral; the machine-readable gate does not. An executor
following the gate literally is blocked at Task 1. Confidence 10/10.
→ Fix: scope Task 1's automated gate to what is buildable at that point, or state the deferral in the
gate itself rather than only in prose.

**F3 — HIGH, and the most substantive finding of the round: the threat model asserts a defence that
does not exist.** `04-02:570` and `04-03:687` both claim `BuildQueue` **independently** blocks a
client-forged `IsLocked=false`. It does not. The analyzed card and the queue input inherit the *same*
client-influenced pool flag: `BuildInputs` copies `card.IsLocked` (`CutLabCutRoundEngine.cs:359`) and
the queue then checks that same value (`:256`). There is one gate, not two. The planned test only
proves an *honestly* locked queue input stays excluded — it cannot detect the forged case.
Confidence 10/10.
→ Fix: rewrite both threat rows to claim a single gate, and either add a real independent check or
record the residual risk honestly. **Do not leave a "defence in depth" claim that is one layer.**

**F4 — MEDIUM. 04-01's byte-identity narrowing did not reach three sites:** the constant
documentation (`:214-216`), the catalog copy (`:248-253`), and the objective's "zero behavior change"
for the shared extension (`:58-59`). Confidence 9/10.

**F6 — MEDIUM. Three smaller 04-03 verification contradictions.** (a) `:697` requires "all three
mutation checks" but only two are specified (exclusion-set insertion, `IsEnabled` substitution);
(b) Task 2 touches five test files (`:401`) while `:520` checks diff-stat parity for "both test
files"; (c) `:515` says the "only edits" are constructor arguments plus one null test, omitting the
six engine tests the same task mandates. Confidence 10/10.

**F7 — LOW. `04-03:623-625` is a stale sentence** telling the executor to add a comment explaining
why the round key was unusable — immediately after instructing them to stop and report instead.
Delete it. Confidence 9/10.

## Confirmed correct (do not re-litigate)

- 04-03 has 12 flag tests; 04-04 has 7 presenter tests; ordering pinned by tests 5 and 6; test 9 is
  paired OFF/ON; the exclusion assertion is absence-only; 04-04 mandates only the single-pass build.
- **None of the four historical ordering/normalization defects is reintroduced.** Both combo
  advisories stay excluded (`CutLabCutRoundEngine.cs:119`); second-pass rotation intact (`:301-320`);
  both normalization regressions still guarded by normalized combo-set construction and lookup
  (`:245-252`, `:449`). Twins evidence uses the same `entry.Name` as the queue inputs, so it adds no
  new cross-source raw-name match-back.
- Source citations have drifted (e.g. `CutLabUiPatchBuilderTests` `:793/:829` → `:820/:856`) —
  misleading but navigable by symbol, explicitly **not** blockers.

## Status

Round 4 **FOLDED 2026-08-01.** F1 and F5 were already fixed in `2bf28b4b`; F2, F3, F4, F6 and F7 were
folded in this pass. Round 5 owed — fresh blind dispatch (operator chose fresh over resume, so the
reviewer re-reads cold rather than grading its own prior conclusions).

| Finding | Fold |
|---|---|
| F2 | `04-03` Task 1's `<automated>` gate rescoped `build DeckFlow.sln` → `build DeckFlow.Web/DeckFlow.Web.csproj`, with an inline `Why:` recording that the test assembly cannot compile until Task 2. Acceptance criterion at the old `:385` rewritten to match and to state that the solution-wide build is **Task 2's** gate. |
| F3 | **Verified against source before folding.** `BuildInputs` copies `card.IsLocked` off the client-supplied `CutLabPoolCard` (`CutLabCutRoundEngine.cs:359`); `eligibleCards` checks that same copied value (`:256`). One gate read twice, not two. Both `04-02` T-04-07 and `04-03` T-04-15 flipped `mitigate` → **`accept`**, the "two independent gates" / "defence in depth" language deleted, and the residual risk recorded honestly on the T-04-06 rationale (client-held per-request state, forger only alters their own advisory output, no privilege crosses a user boundary). Both rows now state the planned test covers the *honest* case only. |
| F4 | `04-01` narrowing extended to all three missed sites: the objective's output claim, the key constant's xmldoc instruction (now "no finding produced / contributes nothing to proposal order", with an explicit ban on the broader "byte-identical to pre-Phase-4" phrasing), and the operator-facing catalog copy. Swept by phrase rather than by cited line — `:25` and `:546` already carried the scoped claim from round 3. |
| F6 | (a) `04-03:697` "all three mutation checks … plus Task 2's" → **two**, enumerated explicitly; the double-count was the exclusion-set check being listed twice. Verified by grep: `04-03` specifies exactly two (`:519`, `:662`); `04-02`'s separate pair is already consistent. (b) "both test files" → **all five**, named. (c) the "only edits" list now includes the six new `CutLabCutRoundEngineTests.cs` tests the same task mandates. |
| F7 | `04-03`'s stale "Say in a `// Why:` comment why the round key was not usable" deleted — it contradicted the stop-and-report instruction immediately above it. |

**Deliberately not folded, flagged for round 5 to adjudicate:** `04-03:26` and `04-03:710` still say
"byte-identical to pre-Phase-4" for the flag-OFF paths. Both are conditioned on the flag being off and
scoped to the three transports, all of which source commander names via `CutLabCommanderNames.Resolve`,
so the `04-01` Task 2 widening does not reach them. Left as-is rather than edited, to avoid
manufacturing diff noise the reviewer must re-verify. Call it if you disagree.

---

# Round 5 — owed. Fresh blind dispatch, `gpt-5.6-sol`, medium.
