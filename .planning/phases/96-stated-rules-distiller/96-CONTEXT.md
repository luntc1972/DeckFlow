# Phase 96: Stated-Rules Distiller - Context

**Gathered:** 2026-07-12
**Status:** Ready for planning

<domain>
## Phase Boundary

Turn a creator's transcripts into **structured, measurable stated rules** — each rule tied to a source clip, a `confidence`, and the source video's date (recency/provenance) — plus a `content_type:` frontmatter field on the artifact. Extends the existing Content-KB distill pipeline (`DistillationSchemas`/`DistillationValidation`/`ContentArtifactWriter`), emitting a `stated_rules:` YAML block. **Substrate only** — feeds Phase 97 (fusion); no user-visible surface, no page, no flag this phase.

Requirements: CS-11, CS-11a, CS-11b, CS-11c, CS-12, CS-13, CS-14, CS-15 (locked — this discussion decided HOW, not WHAT).
</domain>

<decisions>
## Implementation Decisions

### Claimify extraction pipeline (CS-11, CS-12)
- **D-01:** **Literal multi-pass Claimify — 3 sub-calls per chunk + a reduce.** Per transcript chunk: (1) **Select** candidate claim-y sentences, (2) **Disambiguate** references ("it", "that deck", pronoun/anaphora), (3) **Decompose** into atomic rules. Then a **reduce** pass dedupes rules across chunks. Chosen for fidelity/falsifiability over the cheaper single-stage prompt. Map-reduce hierarchical chunking (CS-11) supplies the per-chunk inputs; the reduce is the "merge" step.
- **D-01a:** Irreducibly ambiguous statements are **dropped** at the Disambiguate/Decompose stage (CS-12) — a rule that cannot be made atomic + measurable does not ship.
- **Cost note:** 3 calls/chunk is the token-heavy choice; combined with D-05 (no mass backfill), the LLM spend lands mostly on NEW distills + the golden test, not a 106-artifact sweep.

### Stated-rule schema & metric vocabulary (CS-11a, CS-13, CS-14)
- **D-02:** **Lock the SHAPE now, defer the metric allowlist to the planner.** Rule shape = `{category, metric, value|band, comparator, condition, clip_ts}` plus `sourceClip`, `confidence`, and `video_date` (D-04). `value` supports a **band** (min/max) so "37–42 lands" is ONE rule with `comparator: range`, not two. `condition` carries per-archetype/per-curve conditionality (the P97 CS-16a `applies_when` seed — capture it now even though fusion consumes it).
- **D-02a:** The exact `metric` controlled-vocabulary allowlist is **derived at plan time** from the Phase 95 `MeasuredMetric` metric KEYS (so P97 fusion can join stated↔measured on the same key) UNION the ~27 Snail prototype rules in `docs/research/p89-p90-prototype-snail.md`. Aligning stated `metric` names to `MeasuredMetric` names is a hard planner requirement, not optional.
- **D-03:** Every rule validates against a **strict JSON schema via constrained decoding**, extending `DistillationSchemas` (add a `StatedRulesSchema` + system prompt) and `DistillationValidation` (add `ValidateStatedRules`/`SanitizeStatedRules`), mirroring the existing summary/clips/tags stages. `sourceClip` reuses the clip the KB pipeline already emits; `confidence` is per-rule.

### Recency / provenance (CS-11c)
- **D-04:** **Carry the source video's date on each rule; DO NOT resolve superseding here.** Stamp each `stated_rule` with the video's publish date (already in KB metadata). Newer-supersedes-older reconciliation is **Phase 97's** job — this phase only records `video_date` + `clip_ts`. Honors the "substrate only" boundary (computing supersedes now was explicitly rejected as scope creep).

### content_type frontmatter (CS-11b)
- **D-05a:** **Derive `content_type` from a heuristic over existing signals — no new LLM call.** `content_type ∈ {deckbuilding-theory, deck-tech, meta-commentary, gameplay}` computed from existing tags + keep/drop classifier verdict + clip density (e.g. deck-tech when archetype tags + a decklist context, theory when principle-heavy, etc.). Reuses the classification work already done in the distill pipeline.

### Backfill scope (CS-11a/CS-11b "re-distill pass")
- **D-05:** **Ship the pipeline re-distill-capable; run NO mass backfill this phase.** New distills emit `stated_rules` + `content_type` going forward. The re-distill MECHANISM exists and is runnable (satisfies CS-11a/CS-11b's "via one re-distill pass" = the pass exists), but executing it across the ~106 existing artifacts is an **operator-driven action, deferred** — matching how prior cycles treated re-distill (headless redistill harness; git-shipped bodies, operator runs the sweep). `content_type` append is likewise deferred (D-05a heuristic can append frontmatter without re-LLM when run).
- **D-05-DEP (⚠ downstream dependency for P97):** Because no backfill runs here, **P97 fusion will have NO stated_rules input until a Snail re-distill is executed.** P97 planning MUST run (or gate on an operator running) a Snail stated-rules re-distill before fusion has real stated data. The Phase 96 golden test (D-06) proves the pipeline on a real Snail transcript, but that is an in-memory/test validation, not a persisted corpus backfill.

### Card grounding (CS-15)
- **D-07:** **Fuzzy-correct then flag.** For any card NAME appearing inside a distilled rule, run a minimal Scryfall `fuzzy` lookup: on a confident single match, **rewrite** the card to the canonical name and set `card_grounded=true`; if still unresolved, **keep the rule and flag it** (`card_grounded=false`) rather than dropping it (protects real rules against auto-caption typos; the hard reject is Phase 98's guard). Most stated rules are metric-based (land counts, removal counts) and carry no card name — grounding only touches the minority that name a specific card. Reuse a cached Scryfall lookup + `ScryfallThrottle`.

### Golden regression (CS-15)
- **D-06:** Golden regression test runs the full multi-pass pipeline over a **real Salubrious Snail transcript fixture** and asserts the emitted `stated_rules` validate against the new schema, using the **existing UTF-8-safe harness** (the `CliLlmDistillationService` CP437 lesson — mandatory, non-negotiable). Snail is the seed/golden corpus (prototype-proven ~27 rules).

### Claude's Discretion
- Chunk size / map-reduce chunk boundaries for the hierarchical chunking (D-01).
- Exact dedupe key/threshold in the reduce pass (D-01) — planner picks a reasonable rule-identity key (likely `metric`+`condition`).
- Precise heuristic thresholds for `content_type` classification (D-05a).
- Concrete confidence scale/encoding for `confidence` (D-03).
- Exact `card_grounded` flag representation in the YAML block (D-07).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing distill pipeline (extend, do not fork)
- `DeckFlow.Core/Knowledge/DistillationSchemas.cs` — the summary/classification/clips/tags constrained-decoding schemas + system prompts; add a `StatedRulesSchema` + prompt here (D-03).
- `DeckFlow.Core/Knowledge/DistillationValidation.cs` — `Validate*`/`Sanitize*` surface to extend for stated rules (D-03).
- `DeckFlow.Core/Knowledge/ContentArtifactWriter.cs` — renders the artifact markdown + YAML frontmatter; the `stated_rules:` block and `content_type:` field are emitted here (locked layout — respect the byte-stable gate).
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — the locked artifact layout/tag serialization contract.
- `DeckFlow.Core/Integration/CliLlmDistillationService.cs` — the UTF-8/CP437 harness fix; golden test reuses it (D-06, CS-15).
- `DeckFlow.Core/Integration/LlmDistillationService.cs`, `LlmDistillationProviderFactory.cs` — LLM call plumbing the multi-pass stages plug into (D-01).
- `DeckFlow.Core/Orchestration/ContentKbOrchestrator.cs`, `DistilledVideoResult.cs` — orchestrator that sequences distill stages; the new stated-rules stage(s) wire in here.
- `DeckFlow.Core.Tests/DistillationPromptRegressionTests.cs` — existing distill golden/regression test to mirror (D-06).

### Phase 94 schema (locked substrate this phase's rules map into)
- `.planning/phases/94-style-profile-foundation/94-CONTEXT.md` — `StatedRule{category,targetMetric,targetValue,comparator,sourceClip,confidence}` field locks.
- `DeckFlow.Core/Knowledge/CreatorStyleProfile.cs` — the `StatedRule` record shape.

### Phase 95 metric names (stated `metric` allowlist MUST align to these — D-02a)
- `.planning/phases/95-measured-style-extractor/95-CONTEXT.md` — D-04..D-10 measured-metric decisions; the `MeasuredMetric` keys the stated vocabulary joins against in P97.

### Creator-style origin + prototype grounding
- `docs/research/creator-style-roadmap.md` §"P89 — Stated-Rules Distiller" (lines ~64-76, 171-176) — CS-11..CS-15 intent, the CS-11a/b/c gap analysis, the ~27-rule prototype result, Codex "ground card names earlier" MED.
- `docs/research/creator-style-llm-system.md` — origin report (Claimify/map-reduce rationale).
- `docs/research/p89-p90-prototype-snail.md` — the ~27 Snail stated rules (37-42 lands, 8-14 removal, ≥8 counters, 3-5 wipes) + say-vs-do findings; source for the D-02a metric allowlist and the D-06 golden expectations.

### Requirements
- `.planning/REQUIREMENTS.md` — CS-11..CS-15 requirement text + Codex-review resolutions.
- `.planning/ROADMAP.md` §"Phase 96" — goal + 5 success criteria (the phase gate).
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **Multi-call distill pattern** (`DistillationSchemas` const schemas + system prompts, one constrained call per dimension) — the 3 Claimify sub-calls + reduce follow this exact shape (D-01, D-03).
- **`ContentArtifactWriter.ToText`** — single place that assembles frontmatter + `## Summary`/`## Key Clips`/`## Tags`; extend it for `content_type:` + a `stated_rules:` block (D-05a, D-02). Byte-stable artifact gate applies.
- **UTF-8/CP437 harness** in `CliLlmDistillationService` — mandatory for the golden test (D-06).
- **Scryfall client + `ScryfallThrottle`** (Web) — the minimal fuzzy grounding pass reuses these, cached (D-07). NOTE layering: the grounding call touches a Web-host service; mirror the P95 D-11 pattern (pure rule-shaping in Core behind a narrow contract; the Scryfall call in the host) so the extractor's pure logic stays unit-testable.

### Established Patterns
- Constrained-decoding + `Validate*`/`Sanitize*` per LLM dimension (`DistillationValidation`) — the stated-rules stage adds one more dimension the same way.
- `ContentTagVocabulary` controlled allowlists — the `content_type` enum + (planner-derived) stated `metric` allowlist follow this closed-vocabulary style.
- Pure-Core-logic + Web-host-orchestrator seam (P95 D-11) — apply to the grounding call.

### Integration Points
- Writes `stated_rules:` + `content_type:` into the artifact via `ContentArtifactWriter`; the `StatedRule[]` maps onto the P94 `StatedRule` record for eventual P97 consumption.
- Video publish date sourced from existing KB metadata (D-04).
- **P97 depends on a persisted stated-rules corpus that this phase does NOT backfill (D-05-DEP)** — flag prominently to P97 planning.
</code_context>

<specifics>
## Specific Ideas

- `value` band example locked from the prototype: "37–42 lands" → one rule, `value:{min:37,max:42}`, `comparator: range` (D-02).
- `content_type` denominator motivation: ~14% of artifacts have zero deckbuilding signal — `content_type` gives fusion a clean coverage denominator (CS-11b).
- Grounding example: `'Dockside Extortonist'` (caption typo) → fuzzy → `'Dockside Extortionist'`, rewrite + `card_grounded=true` (D-07).
</specifics>

<deferred>
## Deferred Ideas

- **Mass re-distill backfill of the ~106 existing artifacts** for `stated_rules` + `content_type` — mechanism ships this phase, execution deferred to an operator-run sweep (D-05). P97 needs at minimum a Snail re-distill first (D-05-DEP).
- **Superseding / newer-wins conflict resolution** across same-metric rules — explicitly Phase 97 (fusion), rejected here as scope creep (D-04).
- **Multi-creator onboarding** of stated rules beyond Snail — manual/deferred, mirroring the P95 creator-profile-source manual mapping.

None of these are lost; each is tied to its owning phase above.
</deferred>

---

*Phase: 96-stated-rules-distiller*
*Context gathered: 2026-07-12*
