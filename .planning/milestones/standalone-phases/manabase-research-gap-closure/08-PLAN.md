---
phase: manabase-research-gap-closure
plan: 08
type: execute
wave: 8
depends_on: ["06", "07"]
files_modified:
  - DeckFlow.Web/Help/manabase.md
autonomous: true
requirements: [MBGAP-11]
must_haves:
  truths:
    - "Every factual claim in DeckFlow/Web/Help/manabase.md is cross-checked against docs/manabase-analysis-rules.md (the authoritative engine-behavior doc)"
    - "Any claim overstating the engine's precision/certainty (the M12 finding class) is rewritten in place to match what the engine actually computes"
    - "The help doc reflects the behavior shipped in this phase (restricted-lands disclosure, six untapped cycles, verdict wording) where user-facing"
  artifacts:
    - path: "DeckFlow.Web/Help/manabase.md"
      provides: "re-audited, overclaim-free in-app help"
      contains: "manabase"
  key_links:
    - from: "DeckFlow.Web/Help/manabase.md"
      to: "docs/manabase-analysis-rules.md"
      via: "each help claim verified against the authoritative rules doc"
      pattern: "Karsten"
---

<objective>
MBGAP-11 (D-13): re-audit `DeckFlow.Web/Help/manabase.md` (138 lines) line-by-line for
overclaims. The file was rewritten since EF2, so the original M12 finding's line citations
are dead — this is a fresh content audit, not a targeted edit. Cross-check every factual
claim against `docs/manabase-analysis-rules.md` (the "code wins" authoritative reference,
fully updated by plans 01-07 this phase) and rewrite any claim that overstates precision or
certainty beyond what the engine actually computes.

This plan is docs-only and READS (does not write) docs/manabase-analysis-rules.md. Because
plan 07 WRITES that doc, plan 08 depends on plan 07 (wave 8) to avoid a read-after-write
race — it audits Help against the fully-updated rules doc, not a half-written one.

Purpose: closes the M12 "Help/methodology overclaims" finding class.
Output: an overclaim-free, phase-accurate in-app help page.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/phases/manabase-research-gap-closure/RESEARCH.md
@docs/manabase-analysis-rules.md

<interfaces>
<!-- Authoritative reference to check against. -->
- docs/manabase-analysis-rules.md — the "code wins" engine-behavior doc (fully updated this phase by plans 01-07)
- Overclaim pattern to catch (M12 class): help text asserting the analysis is exact/definitive where the engine actually
  uses a Monte-Carlo estimate, a heuristic (community ramp/draw split, per-color deficit — now labeled heuristic in plan 06),
  or an approximation (restricted-land composition gate, Vivid charge approximation). Note: ELD threshold lands are resolved
  PER-TRIAL inside the simulation (not a static-census approximation) — describe them as part of the simulation, not as an estimate.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Line-by-line overclaim re-audit of Help/manabase.md</name>
  <read_first>
    - DeckFlow.Web/Help/manabase.md (all 138 lines)
    - docs/manabase-analysis-rules.md (full — the authority to check every claim against)
    - .planning/phases/manabase-research-gap-closure/RESEARCH.md (MBGAP-11 section)
  </read_first>
  <action>
    Read the current DeckFlow.Web/Help/manabase.md in full. For each factual claim, verify it against
    docs/manabase-analysis-rules.md. Where the help text overstates precision/certainty (asserts exactness where the engine
    estimates/approximates/uses a heuristic), rewrite the sentence in place to accurately describe the method (e.g. "estimates
    via a 20,000-trial simulation," "community heuristic, not Karsten math," "approximates restricted-source lands"). Ensure the
    page mentions the user-facing behavior shipped this phase where relevant (restricted-land disclosure marker, the expanded
    untapped-cycle handling, the verdict "…plus N more" behavior). Preserve the file's existing structure/voice; edit only the
    lines that overclaim. Preserve LF endings. Produce a short audit list (claim → verdict: kept / rewritten) in the SUMMARY.
  </action>
  <verify>
    <automated>test -f DeckFlow.Web/Help/manabase.md &amp;&amp; git diff --stat DeckFlow.Web/Help/manabase.md; git diff --ignore-all-space --stat DeckFlow.Web/Help/manabase.md</automated>
  </verify>
  <acceptance_criteria>
    - Every rewritten claim is traceable to a docs/manabase-analysis-rules.md statement (audit list in SUMMARY maps claim → rule)
    - No sentence asserts exactness where the engine estimates/approximates/uses a heuristic
    - `git diff --stat` vs `git diff --ignore-all-space --stat` on Help/manabase.md show no whole-file EOL churn
    - The page references at least one behavior shipped this phase (disclosure marker / untapped cycles / verdict truncation note)
  </acceptance_criteria>
  <done>Help/manabase.md re-audited; overclaims rewritten; audit list captured.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries
| Boundary | Description |
|----------|-------------|
| help content → user expectation | Overclaims mislead users about analysis certainty; doc-only, no runtime surface |

## STRIDE Threat Register
| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mbgap11-01 | Repudiation | help overclaims engine precision | mitigate | claim-by-claim cross-check against the authoritative rules doc |
| T-mbgap11-SC | Tampering | NuGet installs | accept | No packages; docs only |
</threat_model>

<verification>
- Help/manabase.md audited against docs/manabase-analysis-rules.md; overclaims rewritten.
- No EOL churn.
</verification>

<success_criteria>
DeckFlow.Web/Help/manabase.md carries no precision/certainty overclaims, matches the authoritative rules doc, and reflects this phase's user-facing behavior. MBGAP-11 complete.
</success_criteria>

<output>
Create `.planning/phases/manabase-research-gap-closure/08-SUMMARY.md` when done.
</output>
