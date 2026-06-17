---
phase: 49-dapper-data-access-adoption
plan: 01b
type: execute
wave: 1
depends_on: ["49-01"]
files_modified:
  - .planning/phases/49-dapper-data-access-adoption/49-GATE-ABORT.md
autonomous: false
requirements: [DAP-03]

must_haves:
  truths:
    - "The sweep (49-02/49-03/49-04) cannot be dispatched until this gate plan completes"
    - "On VERDICT: PASS the gate authorizes the sweep; on VERDICT: FAIL the phase is declared ABORTED and no sweep plan runs"
    - "The abort/authorize decision is recorded structurally, not just as prose in a downstream task"
  artifacts:
    - path: ".planning/phases/49-dapper-data-access-adoption/49-GATE-ABORT.md"
      provides: "Authorization-or-abort record derived from 49-GATE-VERDICT.md"
      contains: "GATE"
  key_links:
    - from: ".planning/phases/49-dapper-data-access-adoption/49-01b-PLAN.md"
      to: ".planning/phases/49-dapper-data-access-adoption/49-GATE-VERDICT.md"
      via: "read the VERDICT line written by 49-01 Task 4"
      pattern: "VERDICT"
---

<objective>
Be the structural Abort Gate between the FeedbackStore spike (49-01) and the full sweep (49-02/49-03/49-04).

This is a one-task gating plan. The GSD orchestrator dispatches plans by wave and `depends_on`; because 49-02/49-03/49-04 declare `depends_on: ["49-01b"]`, the orchestrator CANNOT spawn any sweep plan until this plan resolves. SPEC REQ-3 requires that on a FAIL spike "the sweep does not start" — this plan is the mechanism that enforces it, not the per-task prose in the sweep plans (that prose remains as defense-in-depth).

Purpose: Turn the spike verdict into an explicit, blocking authorize-or-abort barrier. PASS authorizes the sweep; FAIL aborts the phase at the spike with a written rationale.

Output: `49-GATE-ABORT.md` recording either AUTHORIZED (sweep may proceed) or ABORTED (phase stops at the spike).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/phases/49-dapper-data-access-adoption/49-SPEC.md
@.planning/phases/49-dapper-data-access-adoption/49-CONTEXT.md
@.planning/phases/49-dapper-data-access-adoption/49-GATE-VERDICT.md
</context>

<tasks>

<task type="checkpoint:decision" gate="blocking">
  <name>Task 1: Abort Gate — authorize the sweep on PASS, abort the phase on FAIL</name>
  <files>.planning/phases/49-dapper-data-access-adoption/49-GATE-ABORT.md</files>
  <action>
    Automate the read of `49-GATE-VERDICT.md` (grep its top-line `VERDICT:` value), surface PASS/FAIL plus the (a)(b)(c) evidence, then resolve the decision below and WRITE `49-GATE-ABORT.md`: on PASS write `GATE: AUTHORIZED` and clear waves 2-4; on FAIL write `GATE: ABORTED`, paste the FAIL rationale, and state that 49-02/49-03/49-04 MUST NOT be dispatched. See `<decision>` for the full branch logic and the D-06 note that the 5th handler is NOT a FAIL trigger.
  </action>
  <decision>
    Read `.planning/phases/49-dapper-data-access-adoption/49-GATE-VERDICT.md` (written by 49-01 Task 4) and act on its top-line `VERDICT:` value. This is the structural barrier that satisfies SPEC REQ-3 "the sweep does not start" on a FAIL spike — it sits between wave 1 (the spike) and waves 2-4 (the sweep), which all depend on this plan.

    First, automate the read: grep the `VERDICT:` line out of 49-GATE-VERDICT.md and surface PASS or FAIL plus the recorded (a)(b)(c) evidence (handler set + coercion grep + feedback/round-trip test summary). Then:

    - **If `VERDICT: PASS`** → write `49-GATE-ABORT.md` with a top line `GATE: AUTHORIZED`, summarizing the PASS evidence, and explicitly state the sweep (49-02/49-03/49-04) is cleared to run. Resume the phase.
    - **If `VERDICT: FAIL`** → write `49-GATE-ABORT.md` with a top line `GATE: ABORTED`, paste the FAIL rationale from the verdict, and state that 49-02/49-03/49-04 MUST NOT be dispatched. Declare the phase aborted at the spike; do not authorize any sweep plan.

    Note on the 5th handler: the `DateTimeOffsetTypeHandler` added in the sweep is sanctioned by CONTEXT D-06 / amended SPEC REQ-2 (≤5). It is NOT a FAIL trigger and is NOT a developer-disagreement stop point — only an actual failure of REQ-3 criteria (a)/(b)/(c) on the FeedbackStore spike produces a FAIL. Do not abort over the handler count.
  </decision>
  <context>
    SPEC REQ-3 makes the FeedbackStore spike an objective pass/fail gate: PASS = (a) the small fixed handler set covers all coercion the spike exercises AND (b) the converted FeedbackStore has zero store-local coercion AND (c) feedback tests + the REQ-2 round-trip pass on SQLite. A FAIL means the global type-handler approach cannot absorb coercion without per-store conversion — in which case continuing the sweep would just spread a broken pattern across 13 stores. The gate exists so a FAIL costs one store, not fourteen.
  </context>
  <options>
    <option id="authorize">
      <name>AUTHORIZED — 49-GATE-VERDICT.md reads VERDICT: PASS</name>
      <pros>Spike proved the zero-per-store-coercion bar on the real FeedbackStore; the sweep template is validated; waves 2-4 may proceed</pros>
      <cons>None — this is the intended happy path when the spike passes</cons>
    </option>
    <option id="abort">
      <name>ABORTED — 49-GATE-VERDICT.md reads VERDICT: FAIL</name>
      <pros>Stops the phase after one store instead of propagating a broken mapping pattern to 13 stores; rationale is recorded for a future re-attempt</pros>
      <cons>Phase delivers only the spike conversion; the data-access modernization goal is deferred pending a different approach</cons>
    </option>
  </options>
  <verify>
    <automated>VERDICT=$(grep -Eo "VERDICT: (PASS|FAIL)" .planning/phases/49-dapper-data-access-adoption/49-GATE-VERDICT.md | head -1); echo "spike verdict: $VERDICT"; test -f .planning/phases/49-dapper-data-access-adoption/49-GATE-ABORT.md && grep -Ec "GATE: (AUTHORIZED|ABORTED)" .planning/phases/49-dapper-data-access-adoption/49-GATE-ABORT.md</automated>
  </verify>
  <resume-signal>On PASS: confirm "authorize" to clear waves 2-4. On FAIL: confirm "abort" — the phase stops at the spike and 49-02/49-03/49-04 are not dispatched.</resume-signal>
  <done>49-GATE-ABORT.md exists with a single `GATE: AUTHORIZED` or `GATE: ABORTED` top line consistent with the spike VERDICT; on ABORTED the sweep plans are not dispatched.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| spike verdict → sweep authorization | A wrong read of the VERDICT line would either propagate a broken pattern (FAIL read as PASS) or needlessly abort (PASS read as FAIL) |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-49-GATE | Elevation of Privilege | Sweep dispatched despite a FAIL verdict | mitigate | Structural barrier: 49-02/03/04 `depends_on: ["49-01b"]`, so the orchestrator cannot spawn them until this blocking `checkpoint:decision` plan resolves; the decision reads the machine-checkable `VERDICT:` line and records `GATE: AUTHORIZED`/`ABORTED`; per-task prose verdict-checks in the sweep plans remain as defense-in-depth |
</threat_model>

<verification>
- `49-GATE-VERDICT.md` VERDICT line read and surfaced
- `49-GATE-ABORT.md` written with a single `GATE: AUTHORIZED` or `GATE: ABORTED` top line matching the verdict
- On ABORTED: 49-02/49-03/49-04 are not dispatched (they depend on this plan)
</verification>

<success_criteria>
- A blocking decision gate sits structurally between the spike (wave 1) and the sweep (waves 2-4)
- PASS authorizes the sweep; FAIL aborts the phase at the spike with recorded rationale
- The 5th (DateTimeOffset) handler is explicitly NOT treated as a FAIL/stop point (D-06 / REQ-2 ≤5)
</success_criteria>

<output>
Create `.planning/phases/49-dapper-data-access-adoption/49-01b-SUMMARY.md` when done. Record the spike VERDICT, the GATE outcome (AUTHORIZED/ABORTED), and — on ABORTED — that the sweep was not dispatched.
</output>
