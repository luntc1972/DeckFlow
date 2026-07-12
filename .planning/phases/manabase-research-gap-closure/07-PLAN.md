---
phase: manabase-research-gap-closure
plan: 07
type: execute
wave: 7
depends_on: ["06"]
files_modified:
  - .planning/phases/manabase-research-gap-closure/MBGAP-04-threshold-decision.md
  - docs/manabase-analysis-rules.md
autonomous: true
requirements: [MBGAP-04]
must_haves:
  truths:
    - "A decision doc re-verifies Karsten 2022's escalating (89+M)% threshold and resolves the manabase-math.md '[H, verbatim]' vs efficacy-r2 L14 'unconfirmed' contradiction"
    - "The doc evaluates the (85+M)% multiplayer-relaxation proposal and gives an explicit implement / do-not-implement verdict with rationale (double-count vs additive analysis)"
    - "docs/manabase-analysis-rules.md is updated to remove the residual threshold doubt (D-11 'regardless' doc fix)"
    - "No engine code changes ship in this plan (spike only, unless the verdict is implement — then a follow-up plan, not this one)"
  artifacts:
    - path: ".planning/phases/manabase-research-gap-closure/MBGAP-04-threshold-decision.md"
      provides: "the consistency-threshold research-spike decision document"
      contains: "89"
  key_links:
    - from: "MBGAP-04-threshold-decision.md"
      to: "docs/manabase-analysis-rules.md §3.1 threshold section"
      via: "the decision doc's citation flows into the doc contradiction fix"
      pattern: "threshold"
---

<objective>
MBGAP-04 (D-11): a research-spike DECISION DOC — no engine code by default. Two deliverables:
1. Re-verify Karsten 2022's escalating (89+M)% consistency threshold and settle the corpus
   contradiction: `.planning/research/manabase-math.md` tags it "[H, verbatim]"; efficacy-r2
   L14 calls it "unconfirmed." RESEARCH.md's analysis concludes L14 is an unresolved doubt
   with no counter-citation and the (89+M)% formula in `KarstenManabase.ConsistencyThreshold`
   (Kar:151) is very likely already correct — confirm with a citation.
2. Evaluate the (85+M)% multiplayer-relaxation proposal (manabase-mode-research.md §4 #2):
   is it additive to DeckFlow's already-every-turn-draw model or does it double-count the
   multiplayer benefit? Give an explicit implement / do-not-implement verdict.

D-11 requires the `docs/manabase-analysis-rules.md` contradiction fix REGARDLESS of the verdict.

Purpose: closes the L14 doubt with evidence and gives a documented recommendation on relaxation.
Output: decision doc + docs threshold-section fix. No engine code.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/phases/manabase-research-gap-closure/RESEARCH.md
@.planning/research/manabase-math.md
@.planning/manabase-mode-research.md

<interfaces>
<!-- Anchors. -->
- KarstenManabase.cs:151 ConsistencyThreshold(int manaValue) → pct = 89 + Math.Max(1, manaValue) — the implemented formula being verified (DO NOT change in this plan)
- .planning/research/manabase-math.md §1-2 — Table 1/2 "[H, verbatim]" escalating threshold (fetched via headless browser 2026-06-20)
- .planning/captures/manabase-efficacy-findings-r2.md L14 — "unconfirmed against Karsten 2022 (flat ~90%?)" (the doubt to resolve)
- .planning/manabase-mode-research.md §4 point 2 — the (85+M)% / "games run long" relaxation proposal ([ASSUMED], DeckFlow-authored)
- CastabilitySimulator.cs §4.4 — DeckFlow already draws every turn including turn 1 (Commander is multiplayer); the pre-existing "multiplayer relaxation" the spike must weigh against a second relaxation
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Write the consistency-threshold decision doc</name>
  <read_first>
    - .planning/research/manabase-math.md (§1-2 threshold tables + sourcing/confidence tags)
    - .planning/captures/manabase-efficacy-findings-r2.md (L14 entry)
    - .planning/manabase-mode-research.md (§4 point 2 relaxation proposal)
    - .planning/phases/manabase-research-gap-closure/RESEARCH.md (MBGAP-04 section — the pre-resolved analysis)
    - DeckFlow.Core/Manabase/KarstenManabase.cs (ConsistencyThreshold :151 — confirm the shipped formula)
  </read_first>
  <action>
    Create .planning/phases/manabase-research-gap-closure/MBGAP-04-threshold-decision.md with sections:
    (1) The shipped formula (quote ConsistencyThreshold Kar:151, 89 + max(1,MV)).
    (2) Karsten 2022 verification: attempt a live re-fetch of the TCGplayer 2022 article's exact threshold sentence via a
    headless-browser method (the article is JS-rendered and blocks plain WebFetch); if the live fetch is blocked in the
    execution environment, cite the existing manabase-math.md capture which already documents the verbatim headless-browser
    fetch (2026-06-20) and state that as the authority. Conclude L14 as "confirmed — no code change needed" WITH the citation,
    OR, if the fetch surfaces a genuine contradiction, document it precisely and flag a follow-up.
    (3) (85+M)% multiplayer-relaxation evaluation: analyze whether lowering 89→85 is additive to DeckFlow's already-more-generous
    every-turn-draw model (CastabilitySimulator §4.4) or double-counts the multiplayer benefit (draw model = more cards seen;
    threshold = required certainty — argue whether these are distinct or overlapping). End with an explicit "implement" or
    "do not implement" verdict; if "implement," propose a flag name following the D-04/D-10 new-flag-OFF pattern and defer the
    implementation to a follow-up plan (NOT this one).
    (4) A one-line summary of what changes in docs (Task 2).
  </action>
  <verify>
    <automated>test -f .planning/phases/manabase-research-gap-closure/MBGAP-04-threshold-decision.md &amp;&amp; grep -Eqc "confirmed|do not implement|implement" .planning/phases/manabase-research-gap-closure/MBGAP-04-threshold-decision.md &amp;&amp; echo OK</automated>
  </verify>
  <acceptance_criteria>
    - Decision doc exists with all four sections
    - It states an explicit verdict on both the L14 threshold (confirmed/contradicted) and the (85+M)% relaxation (implement/do-not)
    - It contains a citation for the Karsten threshold (live fetch or the manabase-math.md capture)
    - No engine .cs file is modified by this plan (verify: `git diff --name-only` shows no DeckFlow.Core/DeckFlow.Web .cs files)
  </acceptance_criteria>
  <done>Decision doc complete with cited verdicts on both questions.</done>
</task>

<task type="auto">
  <name>Task 2: Fix the threshold contradiction in docs/manabase-analysis-rules.md</name>
  <read_first>
    - docs/manabase-analysis-rules.md (threshold / consistency section, ~§3.1)
    - .planning/phases/manabase-research-gap-closure/MBGAP-04-threshold-decision.md (Task 1 output)
  </read_first>
  <action>
    Update the consistency-threshold section of docs/manabase-analysis-rules.md to state the confirmed (89+M)% escalation with
    the citation from the decision doc, removing any residual "unconfirmed"/doubt language (D-11 requires this fix regardless of
    the relaxation verdict). If the relaxation verdict was "implement," add a one-line forward-pointer to the deferred follow-up
    plan and its proposed flag name. Changed lines only, LF endings.
  </action>
  <verify>
    <automated>grep -niq "unconfirmed" docs/manabase-analysis-rules.md &amp;&amp; echo "STILL HAS unconfirmed - FIX" || echo "clean"</automated>
  </verify>
  <acceptance_criteria>
    - docs/manabase-analysis-rules.md threshold section cites the confirmed (89+M)% formula
    - No residual "unconfirmed"/doubt language remains in that section
    - No EOL churn (git diff --stat vs --ignore-all-space --stat)
  </acceptance_criteria>
  <done>Docs threshold contradiction resolved with citation.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| research source → docs | Doc-only; no runtime surface |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap04-01 | Repudiation | acting on an unverified threshold claim | mitigate | decision doc requires a citation before closing L14 |
| T-mbgap04-SC | Tampering | NuGet installs | accept | No packages; docs only |
</threat_model>

<verification>
- Decision doc exists with cited verdicts.
- docs/manabase-analysis-rules.md threshold section fixed, no "unconfirmed" residue.
- No engine code changed.
</verification>

<success_criteria>
The Karsten threshold contradiction is resolved with a citation, the (85+M)% relaxation has an explicit documented verdict, and docs/manabase-analysis-rules.md no longer carries the doubt. MBGAP-04 spike complete (implementation, if recommended, is a deferred follow-up).
</success_criteria>

<output>
Create `.planning/phases/manabase-research-gap-closure/07-SUMMARY.md` when done.
</output>
