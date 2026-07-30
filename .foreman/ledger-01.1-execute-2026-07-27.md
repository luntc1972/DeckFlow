# Foreman Ledger — Cycle 21 Phase 01.1 Execute (PlanRoleClassifier Heuristic Fixes)

**Run start:** 2026-07-27
**Mode:** Codex-boosted (Agent + real shell + consented Codex, ChatGPT-sub login; user kept session defaults 2026-07-27)
**Baseline commit:** d63ba7b1 (`docs(01.1-02): add Mother of Runes to the D-06 known-limitation list`)
**Branch:** `gsd/cycle21-cut-lab`
**Worktree:** `/mnt/c/users/chrislunt/source/personal/deckflow-role-floors`
**Scope (user-confirmed):** Phase 01.1 ONLY. Stop before Phase 2.
**Roles:** Codex codes each wave (`gpt-5.4` @ medium); LEAD (Opus 5) verifies (build+test gates + blind `foreman-verifier`). Cross-family verify is the default.

## Baseline notes (pre-existing, NOT this run's work)

Untracked at baseline, deliberately left alone — all Phase 2 territory:

- `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.{md,json}` — fixture output, ROADMAP marks these **delete-not-amend**.
- `.../role-floor-research-run.exit` (0 bytes), `_role-floor-research/`, `.foreman/scratch/`.

**Correction to workstream STATE.md:** it claims the Phase 2 harness
(`RoleFloorResearchCommandRunner.cs` 985 LOC, `RoleFloorDivergenceStats.cs`, the `boardFilter`
repo change) is *uncommitted in this worktree*. It is not on disk and not in git history — it is in
**`stash@{0}`** ("cycle21 P2: role-floor research harness ... unreviewed"). Not lost. STATE.md
should be corrected when Phase 2 is re-planned.

## Dependency graph (strictly linear — file ownership, not logic)

Both plans edit `PlanRoleClassifier.cs` and `PlanRoleClassifierTests.cs`. Write sets overlap
completely, so **no parallelism is possible**. Serialize.

- Wave 1 = Plan 01.1-01 (Counters/counterspell substring collision) — `depends_on: []`
- Wave 2 = Plan 01.1-02 (wire `IsProtectionCard` into the oracle heuristic) — `depends_on: [01.1-01]`

## Task rows

| ID | Plan | Wave | Write set | Status |
|----|------|------|-----------|--------|
| T1 | 01.1-01 | 1 | `DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs`, `DeckFlow.Web.Tests/Manabase/PlanRoleClassifierTests.cs`, `01.1-01-SUMMARY.md` | PENDING |
| T2 | 01.1-02 | 2 | same two files + `01.1-02-DELTA.md`, `01.1-02-SUMMARY.md`. `ManabaseHealthBandRegressionTests.cs` is **read-only** until the Task 3 checkpoint authorizes an edit. | PENDING |
| T3 | 01.1-02 Task 3 | — | BLOCKING human checkpoint (LEAD presents DELTA to user; user decides) | PENDING |

## Hard fences carried into every ticket

- Per-file EOL preservation (repo is mixed; verify `grep -c $'\r'` against `git show HEAD:<path>`).
- **No golden / byte-identity / health-band expectation may be edited to make a test pass.** If
  `ManabaseHealthBandRegressionTests` moves off `("Solid","Solid")`, leave it failing and report.
- No new packages. No edits to `DeckStatClassifier.cs` / `StaxProtectionCatalog.cs` / `REQUIREMENTS.md`.
- Workers never spawn workers.

## Attempts (append-only)

### T1 / Plan 01.1-01 — Wave 1
- Attempt 1: Codex `gpt-5.4` medium (~72k tok) → STATUS **DONE**. RED run 6 failing rows (all
  `FromCategories_CountersSynergyTag_IsNotACounterspell`, `Expected: None / Actual: Interaction`),
  then GREEN. Commits `773cc458` (test) → `44e12c90` (fix), TDD order correct.
- LEAD verify: scope fence OK (exactly 3 paths), **zero EOL churn** (`git diff --stat` ==
  `git diff --ignore-all-space --stat`, 101 ins / 4 del both ways; all three files LF at HEAD and LF
  now), forbidden-region grep over the diff = 0 hits.
- Blind `foreman-verifier` (fresh context, given the plan verbatim, no worker narrative):
  **PASS_WITH_NOTES**. A–G all confirmed independently — including a hand-trace of `HasWord`'s index
  arithmetic against D-01's truth table rather than trusting the worker's own tests, and confirmation
  that `CutLabStructuralFindings.cs:227` really does make a `Counters` theme newly eligible for
  `StrandedSubtheme`. Gates reproduced: build 0 warn / 0 err; Core.Tests 1630/0/0;
  Web.Tests 2091 passed / 0 failed / 16 skipped; `PlanRoleClassifierTests` filter 69/0;
  `format-check-changed.sh ci` exit 0.
- **Finding (LOW, H):** plan Task 2 action item 4 required updating the `FromCategories` **method**
  `<summary>` sentence ("A "counter" tag earns Interaction only in cEDH") to record the plural
  exclusion. That sentence (`PlanRoleClassifier.cs:117`) is unchanged; Codex updated the
  class-level xmldoc instead. No acceptance criterion checked that text, so it slipped the gate.
  Cosmetic, zero behavioral impact.
  → **Batched into T2** as an explicitly authorized doc-only exception to plan 01.1-02's
  "do not change `FromCategories`" fence. Not worth its own dispatch.
- Status: **DONE**, `44e12c90`.

### T2 / Plan 01.1-02 — Wave 2
- Attempt 1: Codex `gpt-5.4` medium, foreground with a 10-min tool timeout → **LOST**. Killed by the
  orchestrator's timeout (exit 143 / SIGTERM), not by any Codex fault. My error: T1 fit inside 10
  minutes, T2 does not — it runs the full suite twice plus fixture measurement.
  - **Proof the process stopped:** `pgrep -af codex` shows only the long-lived `codex mcp-server`;
    no `codex exec` process alive.
  - **Reconciliation against baseline** (`git log 44e12c90..HEAD` + `git status`):
    - `db4d3359` `test(01.1-02): ...` — Task 1 RED tests, COMMITTED. Keep.
    - `d30b3141` `docs(01.1-01): ...` — the wave-1 carry-forward doc fix, COMMITTED. Keep.
    - `PlanRoleClassifier.cs` — modified, UNCOMMITTED. LEAD read the orphaned diff in full against
      plan Task 2's six action items: signature `(name, typeLine, oracle, mode)` ✓; `IsProtectionCard`
      disjunct placed after `IsBoardWipeCard`/`IsTargetedRemovalCard` ✓; `GrantsInteraction` xmldoc ✓;
      `// Why:` recording D-02 + D-03 and naming spike 002 ✓; call site passes `fact.Name` ✓;
      `FromHeuristic` xmldoc ✓. Complete and correct → **KEPT, not discarded.**
    - No `01.1-02-DELTA.md`, no GREEN commit — the gate/measure/commit tail never ran.
    - `stash@{0}` (Phase 2 harness) intact. No golden or fixture file touched.
  - Not counted as a failed attempt on the seat — the seat never returned a bad result, the
    orchestrator cut it off. No escalation warranted.
- Attempt 2 (resume, not restart): same seat, **backgrounded** so no timeout can kill it. Ticket
  states the inherited commits and tells the worker the orphaned edit is already correct and must not
  be rewritten. Remaining scope: run the gate sequentially, measure and write `01.1-02-DELTA.md`
  (5 sections), EOL-verify, commit GREEN. Task 3 explicitly withheld — it is the human checkpoint.
  - Returned **DONE_WITH_CONCERNS**. Commit `34e92cc3` `fix(01.1-02): wire IsProtectionCard into the
    oracle interaction heuristic`. `01.1-02-DELTA.md` written, all 5 sections. Gates: build 0 warn /
    0 err; Core.Tests 1630/0/0; **Web.Tests 2093 passed / 2 failed / 16 skipped**. Zero EOL churn,
    `format-check-changed.sh staged` exit 0, scope fence = 3 files. Task 3 correctly WITHHELD, no
    SUMMARY written, no golden touched.
  - **The feared breakage did NOT happen:** `ManabaseHealthBandRegressionTests` on
    `.manabase-arch-7084567-facts.json` still reads `("Solid","Solid")` — it passed in the real
    suite run. Protection permanents counting as interaction did not move the band.
- LEAD verify — **two factual errors in the worker's report, both corrected:**
  1. DELTA section (a) and the worker's summary both attribute the failure to the *artifact* oracle
     (`"...gains hexproof..."`). **Wrong.** LEAD reproduced the filtered run: both failures are at
     `PlanRoleClassifierTests.cs:line 207`, the **third** assertion in the theory — the Enchantment
     reading `"...permanents you control phase out until your next turn."`. The artifact case
     demonstrably PASSES (the sibling `Classify_ProtectionPermanent_IsInteractionAndNothingElse`
     uses the identical fact and is green).
  2. DELTA (b) was measured via a scratch harness because an xUnit display-name filter didn't match.
     Weaker evidence than what was already on hand — the real health-band test passed inside the full
     suite run. Same conclusion, stronger proof.
- **Root cause: the TEST is defective, not the fix.** `DeckStatClassifier.IsProtectionCard:231`
  matches the needle `"phases out"` (singular-subject verb); the plan's synthetic enchantment oracle
  uses `"phase out"` (plural subject). That is *precisely* the singular/plural verb-agreement gap the
  plan documents as **D-06 and declares out of scope** — the plan author wrote a test case that lands
  inside the gap they were documenting. The production fix is correct: `"gains hexproof"` and
  `"gains indestructible"` both match and pass.
- Blind `foreman-verifier` (fresh context, plan verbatim, told the suite was red and explicitly asked
  to confirm-or-REFUTE the LEAD diagnosis rather than accept it): **PASS_WITH_NOTES**.
  - **A: CONFIRMED** the diagnosis independently — stack trace at line 207, enchantment case; the
    artifact (199-201) and creature (203-205) assertions both pass; `"phases out"` needle at
    `DeckStatClassifier.cs:231` vs the oracle's `"phase out"`, literal substring absent.
  - **B/D/E/F/G/H/I: PASS.** Fix matches Task 2 items 1-6; D-02 disjunct ordering and
    non-mode-gating preserved; D-03 respected (no type gate); scope fence exactly 3 files; no golden
    or `ManabaseHealthBandRegressionTests.cs` in the diff; `Classify`/`PermanentOnlyRoles`/
    `CountersASpell`/`HasWord`/`IsCounterCategory`/category predicates byte-identical;
    `FromCategories` differs in xmldoc text only; build 0/0; Core 1630/1630; Web 2093 pass / 2 fail
    (only the two known) / 16 skipped — **no third failure**; zero EOL churn (`--stat` ==
    `--ignore-all-space --stat`); Task 3 correctly withheld, no SUMMARY written.
  - **Finding MEDIUM:** DELTA section (a) misattributes the failure to the artifact case. Independent
    repro confirms the LEAD correction. A developer reading only the DELTA at the checkpoint would
    have formed a wrong model of what was broken.
  - **Finding LOW:** DELTA section (b)'s methodology note claims the health-band test was unreachable
    by filter. It is reachable —
    `--filter "FullyQualifiedName~ManabaseHealthBandRegressionTests"` → **18/18 passed**. The
    conclusion (`("Solid","Solid")` holds) is correct; only the justification was wrong. **The band
    is now confirmed twice, once via the real test.**
- Status: **DONE_WITH_CONCERNS**, `34e92cc3`. Both DELTA errors routed into T4 for correction.

### T4 / Apply the checkpoint decision
- Dispatched Codex `gpt-5.4` medium, backgrounded (`btnrh64mm`). Tests + docs only — the ticket
  forbids touching `PlanRoleClassifier.cs`, since two verifications agree the production fix is
  correct.
- Scope: (1) replace the theory's third oracle with `Fact("Artifact", "{T}: Target creature phases
  out.")` — real Vodalian-Illusionist-shaped phrasing that exercises the `phases out` needle as it
  actually exists, with the plural-subject gap left reported under D-06 rather than asserted;
  (2) correct DELTA (a) and (b) per the two verifier findings and record the accept disposition in
  (e); (3) write `01.1-02-SUMMARY.md` with delta disposition + D-06 follow-up + requirement gap +
  the cross-needle verb-agreement observation; (4) full green suite, commit.
- Returned **DONE**. Commit `b8ec09f3` `fix(01.1-02): test the phases-out needle with singular oracle
  phrasing`. Gates: build 0/0; Core.Tests 1630/0/0; **Web.Tests 2095 passed / 0 failed / 16 skipped**;
  `ManabaseHealthBandRegressionTests` 18/18. `PlanRoleClassifier.cs` NOT modified, as fenced.
- LEAD verify: scope exactly 3 files; zero EOL churn (`--stat` == `--ignore-all-space --stat`);
  test diff is the third assertion + its explanatory comment only, first two assertions untouched;
  SUMMARY records all four required items (delta disposition, D-06 follow-up, requirement gap,
  cross-needle verb-agreement observation).
- Status: **DONE**, `b8ec09f3`.

### T5 / LEAD — roadmap + memory (no dispatch; planning is the LEAD's role per CLAUDE.md)
- `b741b56a` `docs(cycle21): resolve Phase 2 decisions, insert Phase 01.2, close Phase 01.1`.
  Marks Phase 1 + 01.1 complete; inserts **Phase 01.2** (protection-vocabulary widening) on the spine
  ahead of Phase 2, flagged as a judgment call rather than a hard dependency; records the developer's
  **D-A hybrid corpus / D-B P25 / D-C lands+ramp** decisions with the coupling between D-A and D-B
  spelled out; adds Phase 2 success criteria 7-10 incl. the requirement that a lands result never be
  presented as novel without an explicit verdict against the 2026-07-16 prior; corrects the
  harness-location error (`stash@{0}`, not the worktree).
- Memory: recorded the scoped lands exception in `project_manabase_commander_cost_floor` so a future
  session does not block the decision citing the old ALL-REJECTED verdict; updated the Cycle 21
  topic file and index.

## Run closed — Phase 01.1 COMPLETE

5 code commits (`773cc458` → `b8ec09f3`) + 1 planning commit (`b741b56a`). Suite green.
Two blind verifications, both PASS_WITH_NOTES, both catching real defects the gates missed.

**What this run should be remembered for:** the gates were green at every stage and still missed
things twice. Wave 1's plan required a method-level xmldoc edit that no acceptance criterion checked,
so it slipped. Wave 2's *plan* contained a defective test oracle that landed inside the very
known-limitation gap the plan documented three sections earlier — and the worker's report then
misattributed the resulting failure to the wrong assertion, which would have produced a wrong
decision at the human checkpoint had the LEAD not reproduced it. **Grade the evidence, not the
verdict.** Also: the orchestrator was itself a failure source (a 10-minute timeout killed a healthy
worker mid-flight); reconciling against `git log`/`git status` rather than the dispatch's status is
what made that recoverable.

### T3 / Plan 01.1-02 Task 3 — BLOCKING human checkpoint
- **Presented to the developer 2026-07-27. Both decisions given:**
  1. **Red test → fix the test oracle.** Replace the enchantment's plural
     `"...permanents you control phase out..."` with real singular phrasing the predicate genuinely
     supports — `Fact("Artifact", "{T}: Target creature phases out.")` (Vodalian Illusionist shape).
     Suite goes green on behavior that actually exists; the plural-subject gap stays *reported* in
     DELTA (d) as D-06 rather than *asserted* as expected behavior.
  2. **D-06 → open a follow-up phase.** Record "follow-up phase requested" in
     `01.1-02-SUMMARY.md`. Rationale: Phase 2 measures interaction floors through this same
     classifier, so an under-detecting `IsProtectionCard` biases the research corpus.
- Remaining work (T4): apply the oracle fix, correct DELTA (a)'s misattribution, write
  `01.1-02-SUMMARY.md` (delta disposition + D-06 follow-up + requirement traceability gap), full
  suite green, commit. **Held until the blind verifier returns** — mutating the tree mid-verification
  would void it.
- Cycle-level follow-up owed by LEAD after T4: add the `IsProtectionCard`-widening phase to
  `.planning/workstreams/cycle21-cut-lab/ROADMAP.md`.
