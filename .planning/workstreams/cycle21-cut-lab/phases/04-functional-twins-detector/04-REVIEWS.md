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

# Round 5 — 2026-08-01. Fresh blind dispatch, `gpt-5.6-sol`, medium. Verdict: CHANGES REQUIRED.

0 BLOCK · 3 HIGH · 2 MEDIUM · 1 LOW. **NOT folded yet.** The reviewer confirmed the round-4 F3
correction and independently extended it (see H3). It also confirmed the eight-construction blast
radius is exactly eight, that no interface gains a member, and that the named APIs otherwise exist
with compatible shapes.

**H1 — HIGH. "Distinct cards" is actually implemented as distinct pool ENTRIES. VERIFIED AGAINST
SOURCE.** `04-02` D-17 rule 4 says the threshold counts "distinct pool entries", while the section
heading and the surrounding prose say "distinct cards". Those differ whenever the pool holds two
entries with the same normalized name. Nothing dedupes: `CutLabStateSerializer.Deserialize` filters
and clamps `Packages`/`Decisions`/`QuantityAdjustments`/`OriginalEntries` but **never touches `Pool`**
(`CutLabStateSerializer.cs:56-81`), and `CutLabWorkingList.Derive` appends each pool entry
individually (`:33-52`). So three entries naming the same card — casing variants, printing variants,
DFC long/short forms that normalize identically — make `group.Count() == 3` and manufacture a false
twins finding. The specified test uses ONE entry with `Quantity = 3`, which does not exercise this at
all and survives the mutation. → Fix: dedupe each candidate group by `CutLabCardNames.Normalize` under
`CutLabCardNames.Comparer` before thresholding and before emitting evidence; add a test with three
same-card entries in normalize-identical variants asserting NO finding. Confidence 10/10.

**H2 — HIGH. `04-04`'s flag-OFF human gate demands an identity the same plan makes impossible.**
`04-04:503-506` says that with the flag OFF the panel returns to "exactly its pre-Phase-4 content".
But `04-04` deliberately replaces `BuildFindingGroups`' capture-index-then-insert with a single-pass
build, correcting a `WeakFloorCase`/`ComboProtected` reverse-order defect that is **flag-independent**
and stays active when twins is OFF (`CutLabFindingPresenter.cs:31-91`). Consistent with this phase's
own round-3 decision, which adopted that refactor precisely because the reversal is live today. → Fix:
narrow the checkpoint to "no Functional twins section, no twins help note, no twins contribution to
ranking", and explicitly permit the intended merged-section order correction. Confidence 10/10.

**H3 — HIGH. `04-01`'s T-04-02 claims a server-resolved commander identity that does not exist.
VERIFIED AGAINST SOURCE.** The row says clearing `IsCommander` is insufficient because a
"server-resolved commander-name set" still excludes the commander and `EnforceCommanderLock`
independently re-locks it. Neither holds. `CutLabCommanderNames.Resolve` derives the set from
`state.Pool` where `card.IsCommander` — client-supplied — and falls back to `state.Commander`, also
client-supplied (`CutLabCommanderNames.cs:9-23`); a forged state clears both. `EnforceCommanderLock`
only re-locks entries **still carrying `IsCommander == true`** and infers nothing
(`CutLabLockRules.cs:12-25`). → Fix: move T-04-02 `mitigate` → `accept` on the same per-session
residual-risk rationale now used by T-04-07 and T-04-15, or add a genuinely server-authoritative
derivation. Do not call the current set server-resolved or independent. Confidence 10/10.

**M1 — MEDIUM. `04-03`'s transport fixture cannot reach the code it tests.** A 12-20 entry fixture is
specified, but page intake rejects pools of 100 or fewer non-commander cards
(`CutLabPoolValidator.cs:31-46`) and `BuildQueue` returns an empty queue with a null proposal when no
cuts are required (`CutLabCutRoundEngine.cs:209-218`). → Fix: require a legal oversized fixture
totalling 101-150 non-commander cards while keeping 12-20 distinct entries, using a high-quantity
locked basic-land filler so only the intended twins move ranking. Confidence 9/10.

**M2 — MEDIUM. `04-03:688` T-04-12 cites the wrong missing-key tests.** Tests 3 and 6 cover the page
service and patch builder; the third newly injected consumer is `CutLabApiController`, whose
missing-key guard is test 12. The controller has its own pre-round-plan flag read
(`CutLabApiController.cs:100-104`), so patch-builder coverage cannot substitute. → Fix: cite tests 3,
6 **and 12**, and require the `IsEnabled` mutation check on the controller too. Confidence 10/10.

**L1 — LOW, and it ADJUDICATES the item the round-4 fold deliberately left open.** The reviewer ruled
the flag-OFF claims at `04-03:26` and `04-03:714` **safe** — so they correctly stay unfolded. But the
*rationale* in `04-01:60-68` names the wrong resolver: AJAX paths use `CutLabCommanderNames.Resolve`
(`CutLabApiController.cs:84`), while initial page intake uses the separate `ResolveCommanderSelection`
(`CutLabPageService.cs:284, 733-782`) and builds both the pool flags and `state.Commander` from that
one result (`:865-902`). Outcome safe, stated reason inaccurate. → Fix: state both paths accurately.
Confidence 10/10.

## Status

Round 5 **not folded.** Round 6 owed after folding. Note H3 is the *same* class of error as round 4's
F3 — a threat row asserting an independent server-side gate that is really client-supplied data — in a
row neither of us checked last round because F3 drew all the attention.

---

# Round 6 — 2026-08-01. Fresh dispatch, `gpt-5.6-sol`, medium. Verdict: CHANGES REQUIRED.

0 BLOCK · 2 HIGH · 3 MEDIUM · 2 LOW. **All seven folded** at `daa6add2`.

**HIGH-1 — the round-5 H1 fix was incomplete in a way that created a NEW defect.** The identity
collapse emits one normalized representative per group, but `BuildFindingTallies` keys tallies by raw
name under `OrdinalIgnoreCase` (`CutLabCutRoundEngine.cs:401`) and `TallyFor` looks up the raw
working-list name (`:441`). A DFC long form beside its short form therefore produces a **visible twins
finding while one of the two entries gets no tally and stays in round 3** — UI and ranking openly
disagree, the exact divergence class T-04-14 exists to prevent. The engine's own comment at `:240-244`
documents this hazard and says raw keying is safe *only because* `ComboProtected` is excluded from
tallies; `FunctionalTwins` is deliberately not excluded, so Phase 4 is the first change to break that
precondition. Tests 9b/9c cannot catch it — they assert on detector output and never reach the queue.
→ Folded as **D-23** (a `FunctionalTwins`-only normalized tally join, every other kind byte-identical),
with the production instruction in 04-03 Task 1, test 4b in Task 2, and a mutation check that also
requires **no other test to change result**. Flagged for operator override rather than settled.
Confidence 10/10.

**HIGH-2 — the round-4 T-04-15 correction never reached Task 2 test 4**, which still called `BuildQueue`
"the second, independent gate" holding "even if a forged pool state slipped a locked card into a twin
group". Same claim, same plan, corrected in the threat row and missed in the test. Rewritten as an
honest-state invariant. Confidence 10/10.

**MEDIUM-1 — the round-5 H1 fold missed the `REQUIREMENTS.md` amendment text** it mandates, which still
said "3 or more distinct cards". Confidence 10/10.

**MEDIUM-2 — T-04-03 understated fabricated `TypeLine`.** It said the effect is "advisory finding text
only"; because twins feeds the tally, it also moves round assignment and proposal order. Split into
T-04-03a (blank, `mitigate`, fail-closed) and T-04-03b (fabricated, `accept`). Confidence 10/10.

**MEDIUM-3 — T-04-21 claimed the manual session leaves no committed artifact**, contradicted by Task 3
requiring the deck used and two screenshots in the committed summary. Now requires a public decklist
and sanitized or out-of-repo screenshots. Confidence 9/10.

**LOW-1 — the obsolete universal-resolver claim sat immediately above its own correction.** Deleted.
**LOW-2 — T-04-11 cited tests 1 and 4** for hardcoded-ON coverage, missing the controller's own read;
now cites 1, 4 and 11. Confidence 9-10/10.

**Caught while folding, not by the reviewer:** the new decision initially collided with 04-04's existing
D-21. Renumbered to D-23; D-14 through D-23 verified unique across all four plans.

## Status

Round 7 owed. **Convergence trend is worth watching:** round 5 found 6, round 6 found 7, and round 6's
most serious finding was created by round 5's fix.

---

# 2026-08-01 — Plan-structure consolidation

Historical correction annotations were removed from the operative plan text and retained here as
review archaeology. The resulting canonical homes are the D-14–D-23 records for decisions, the
individual Task acceptance criteria for tests and mutation checks, and the STRIDE rows for threat
dispositions. The substantive history moved from the plans includes the resolver correction,
normalized-identity collapse and tally-join corrections, controller-owned flag-read coverage,
honest-state-only lock invariants, the fabricated-TypeLine consequence, the merge-order correction,
and decklist/screenshot handling. Live traps remain inline where an executor could otherwise make the
wrong change: exact-MV rather than CurveCongestion buckets, no exact membership pin for the exclusion
set, Snapshot's inability to prove non-invocation, D-23's narrowly scoped tally join, the shared
client-supplied lock value, single-pass merged-group ordering, and sanitized verification evidence.

---

# Round 7 — 2026-08-01. First review of the RESTRUCTURED plans. `gpt-5.6-sol`, medium, read-only.
# Verdict: CHANGES REQUIRED. 1 BLOCK · 1 HIGH · 4 MEDIUM · 0 LOW. NOT folded yet.

**The restructure passed its audit.** The reviewer explicitly confirmed: *"All other round 1-6 folds
remain present. D-14 through D-23 each have exactly one definition"*, and independently re-derived
every count — detector tests 21, flag tests 12, presenter tests 7, density tests 6, constructor blast
radius 8, `BuildFindingsAndRoundPlan` four call sites plus declaration, no interface gains a member.
So consolidation lost nothing, which was the main risk it carried.

**BLOCK-1 — D-23 contradicts its own acceptance gate, and test 4b does not guarantee failure without
the join.** Two distinct defects in the round-6 fold:
(a) `04-03:446` requires `FunctionalTwins` to appear in the engine **only** inside the
`ExcludedFindingKindsFromTally` comment — but D-23's tally-join branch necessarily names
`FunctionalTwins` in engine code, so the plan forbids the thing it mandates. That criterion predates
D-23 and was not updated when D-23 landed: the incomplete-fold pattern, one more time.
(b) Test 4b calls `BuildQueue` directly, so no detector "fires" and the test never constrains what
evidence contains. If the fixture supplies **both** the long and short DFC forms as evidence, the
existing raw implementation tallies both and **4b passes without D-23** — it cannot fail for the
defect it exists to catch. Also: `BuildFindingTallies` currently takes only findings
(`CutLabCutRoundEngine.cs:401-435`) and `TallyFor` does a raw lookup (`:441-444`), so the join needs an
explicit signature/data-flow change the plan never specifies.
→ Fix: replace `:446` with an initializer-specific assertion (absent from the exclusion set, while
occurrences in the D-23 branch are required); specify the implementation shape, e.g.
`BuildFindingTallies(findings, workingList)`, normalizing only for `FunctionalTwins` and retaining the
raw branch verbatim for every other kind; make 4b construct evidence explicitly as
`["Malakir Rebirth", "Card B", "Card C"]` — omitting the long form — while the working list holds
both, then assert each raw entry has `FindingCount == 1` and the same round; and add a **negative
control on a non-twins kind** proving the long form still does not inherit the short form's raw tally.
"No other test changes result" does not prove general tallies were left un-normalized. Confidence 10/10.

**HIGH-1 — two DoS threat rows cite a server gate that does not cover the AJAX path.** `04-02:624`
and `04-04:643` cite `CutLabPoolValidator.MaxPoolCards` as the bound on pathological detector input.
But `CutLabStateSerializer.Deserialize` never filters or caps `Pool` (`:56-79`), and `PostDecideAsync`
accepts that pool and begins derivation without `ValidateCardCount` (`CutLabApiController.cs:78-100`).
The validator runs on **page intake only** (`CutLabPageService.cs:289`, `:370`). The real remaining
bound on the AJAX path is the 2 MB request limit. → Fix: either validate the deserialized pool before
every AJAX analysis path, or move T-04-09/T-04-19 to `accept` and document the 2 MB residual honestly.
Do not cite `MaxPoolCards` as an AJAX defense unless it is enforced there. Confidence 10/10.

**MEDIUM-1 — the registration precondition cannot detect a duplicate.** `04-03:301-308`, `:437`, `:445`
claim the gate finds *exactly one* `AddDeckFlowFeatureFlags` call, but the shell is `grep ... && build`
and grep exits 0 for one **or many** matches. → Fix: assert an exact count,
`test "$(grep -Ec '<pattern>' DeckFlow.Web/Program.cs)" -eq 1`. Confidence 10/10.

**MEDIUM-2 — two flag threat rows describe a failure that cannot occur.** `04-01:514` and `04-03:795`
say a missing seed row or dropped DI registration could silently ship twins **ON** via `IsEnabled`'s
default-ON. `IsEnabled` does default missing keys ON (`IFeatureFlagCache.cs:14-20`), but the planned
consumers deliberately use `Snapshot().TryGetValue(...) && enabled`, so a missing row stays **OFF**;
and dropping DI makes the two new **required** constructor dependencies fail resolution — a loud
startup failure, not a silent enable. → Fix: describe a missing seed row as loss of admin
visibility/controlled enablement, and required-DI removal as fail-loud. Reserve silent-ON for a
regression to `IsEnabled` or a hardcoded `true`. Confidence 9/10.

**MEDIUM-3 — the human checkpoint and the phase success criterion disagree.** `04-04:506-510` lets the
checkpoint pass when `NextProposal` is unchanged provided some round assignment moved, while `:668`
requires an end-to-end next-proposal change. Queue ordering can legitimately keep the same first card
while a twins tally moves another card between rounds (`CutLabCutRoundEngine.cs:268-292`). → Fix: pick
one — require an actual proposal change, or amend the criterion to accept a recorded round-assignment
change. The unit-level proof stays in 04-03. Confidence 10/10.

**MEDIUM-4 — the follow-up count disagrees with its own list.** `04-04:669` claims six follow-ups;
only five exist (F-04-01, F-04-02, F-04-04, F-04-05, F-04-06). F-04-03 was resolved in-phase by the
presenter rewrite (see this file, Round 1 H-6) and cannot sit under "deliberately NOT fixed".
Confidence 10/10.

## Status

Round 7 not folded. Per the delegation split adopted this session, **Codex folds; Claude reviews.**
Round 8 owed after the fold.

---

# Round 8 — 2026-08-01. Fold of five plan-review findings.
# Verdict: FOLDED. 2 HIGH · 1 HIGH gate class · 2 MEDIUM.

**HIGH-1 — stale AJAX trust-boundary protection claim.** `04-03`'s client-to-decide row no longer
claims `CutLabPoolValidator` protects the AJAX boundary. It now names
`CutLabStateSerializer.MaxUploadBytes` (256 KiB), bounded state collections, and the controller's
non-empty-pool check, and explicitly says the 101-150 card validator rule is not reapplied there.
The existing T-04-09 and T-04-19 rows were already aligned and required no new correction.

**HIGH-2 — combo badges were broken in production, and scope widened by user decision on
2026-08-01.** `04-04` now specifies the line-708 lookup repair to
`CutLabCardNames.Normalize(evidenceCard.Name)`, adds `CutLab.cshtml` and
`CutLabViewRenderTests.cs` to the applicable modified-file lists, and adds the real-view render
mutation proof using `Heliod, Sun-Crowned`. The former one-Razor-edit restriction and automatic-badge
claim were removed wherever they survived. The plan also records the raw-key wording-fixture caution:
it is not production-keying evidence and is not changed in this phase.

**HIGH-3 — filtered test gates could pass without the planned tests.** The six executor-owned gates
now run `dotnet test --list-tests` first and require each named planned method exactly once before the
filtered run. The detector gate lists all 21 names individually; the flag, presenter, and density
standalone classes additionally require totals of 12, 7, and 6. Each gate now documents all four exit
states: intended inventory -> 0; absent/misnamed planned test -> nonzero; unrelated build failure ->
nonzero; zero matching tests -> nonzero before the filtered command.

**MEDIUM-1 — stale default-OFF comment.** `04-02` now says a missing seed row or key and an unwired
direct `Compute` caller land OFF, while removal of the required cache registration fails loudly during
DI activation. This aligns with `04-03` T-04-12.

**MEDIUM-2 — categorical proposal wording.** The catalog copy and its acceptance phrase now say the
flag can change which card Cut Lab proposes next by changing card-to-round assignment. `04-04` now says
the kind contributes to the tally and can change the next proposal, matching the existing human gate
that permits an unchanged proposal when a round assignment moved.

## Status

Round 8 folded. No production code was changed by this documentation fold.

---

# Round 9 — 2026-08-01. Fold of renderer parity, duplicate tally, and fixture-count findings.
# Verdict: FOLDED. 2 HIGH · 1 LOW.

**HIGH-1 — the round-8 combo-badge repair covered Razor but not the AJAX renderer, and the AJAX
renderer emitted no twins help note.** Folded as D-24 in `04-04`: `CutLab.cshtml` retains its normalized
view-model lookup, while `CutLabUiPatchBuilder.BuildComboBadgeByCardName` re-keys normalized combo
membership once server-side onto raw pool names with `StringComparer.OrdinalIgnoreCase`, matching
`CardTextByCardName`. This emits an entry for each raw DFC form. By user decision on 2026-08-01, the
reviewed proposal to port a normalizer into TypeScript was rejected: duplicating star stripping, `*f*`
stripping, DFC splitting, punctuation collapse, and space collapse in a second language would drift
silently. `cut-lab.ts` therefore receives no badge normalizer or badge lookup change. The review's
prescribed TypeScript fixture change from raw to normalized key was deliberately not folded because it
is wrong under the chosen raw-keyed DTO shape. Task 1 now also adds the identical `FunctionalTwins`
`p.manabase-help` copy to `renderStructuralFindings`, a scoped vitest AJAX-survival test, the two C# DTO
re-key mutation tests, the narrowed `wwwroot` scope fence, and live AJAX verification of both badge and
help-note content.

**HIGH-2 — D-23 could count one twins finding twice when duplicate raw pool entries share a tally
key.** `04-03` now requires matching normalized targets to be deduplicated by raw name with
`Distinct(StringComparer.OrdinalIgnoreCase)` before a tally increment. The named sibling test
`BuildQueue_TwinsDuplicateRawPoolEntries_IncrementEachRawTallyOnce` asserts every resulting queue item
has `FindingCount == 1`, not 2; it is required in the inventory gate and recorded as D-23 mutation
proof.

**LOW-1 — `BuildTimingFacts` is 147 cards, not approximately 130.** Every description of that specific
fixture now says 147 cards and records its `1 + 40 + 20 + 20 + 25 + 24 + 17` composition. The planned
diverse density fixture remains a 120-140-entry requirement.

## Status

Round 9 folded. No production code was changed by this documentation fold.

---

# Round 10 - 2026-08-01. Fold of the remaining D-24 renderer and DTO-contract findings.
# Verdict: FOLDED. 2 HIGH.

**HIGH-1 - the card-text JSON reader was still broken.** `CutLab.cshtml:234-275` builds
`cardTextData` from the union of raw `CardTextByCardName` keys and normalized
`ComboBadgeByCardName` keys. For `Heliod, Sun-Crowned`, that emitted both the raw and punctuation-free
JSON keys, then attached `comboContext` only to the normalized entry; the TypeScript modal reads the raw
entry and therefore misses the context. This reader had been incorrectly cleared as correct in round 9
by both the reviewer and the fold brief on the reasoning that it "iterates dictionary keys." Round 10
overturned that: it iterates the union of two dictionaries with different key conventions. Folded in
`04-04` by requiring card-text JSON entries to come only from raw rendered pool/card-text names, the
badge lookup to normalize `cardName`, and a render regression that asserts the raw Heliod JSON entry has
`"comboContext":"Infinite damage"` with no punctuation-free duplicate key. The test is explicitly
required to fail before the fix and was added to Task 1's inventory gate.

**HIGH-2 - the DTO contract and stale existing test indexing contradicted the raw re-key.**
`CutLabUiPatchDto.cs` still documented normalized keys and defaulted with `CutLabCardNames.Comparer`,
which is `StringComparer.Ordinal`; it was absent from Task 1's file inventories. The builder instruction
also permitted `workingList` despite the sibling `CardTextByCardName` using `state.Pool`, and the three
pre-existing Heliod/Walking Ballista assertions indexed normalized keys that would throw after the re-key.
Folded in `04-04` by adding the DTO file to both inventories; requiring
`BuildComboBadgeByCardName(state.Pool, context.Classification.CardComboMembership)` with first parameter
`pool`; requiring raw-rendered-pool-name xmldoc plus `StringComparer.OrdinalIgnoreCase` for both DTO and
empty adjust-patch defaults; and directing the three existing assertions to use direct raw keys. The two
round-9 DTO mutation tests and the four-case inventory-gate behavior remain unchanged.

## Status

Round 10 folded. No production code was changed by this documentation fold.
