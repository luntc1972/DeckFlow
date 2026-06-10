# Research Summary — DeckFlow v1.6

**Project:** DeckFlow v1.6 — Content KB Retrieval Fix + Value Re-Validation
**Domain:** RAG-style expert-lens injection for LLM deck analysis; code quality (SRP)
**Researched:** 2026-06-10
**Confidence:** HIGH (retrieval fix, SRP split, gate mechanics); MEDIUM (philosophy-profile — conditional; outcome depends on gate clearing and ~82-video corpus quality)

---

## Executive Summary

Spike 001 Run 2 (2026-06-10) proved that the live `ContentKbRelevanceService` is actively harmful: the real scorer selected 5 clips from a single tangential video — 3 of 5 named unrelated commanders — producing rubric scores worse than hand-picked generic maxims (Specificity 1, Novel signal 1, Actionability 0–1, net quality: NEGATIVE). Two structural defects were identified: (1) `SelectTopClips` has no per-video diversity cap, so the highest-scoring video monopolizes all slots; (2) tag-overlap scoring rewards tag breadth over topical fit, letting a broad-tag video ("Glass Cannon Commanders") outscore directly relevant narrow-topic videos. The entire v1.6 milestone is structured around fixing these two defects, re-running the gold A/B gate blind, and only then deciding whether to proceed with the Creator Philosophy-Profile build.

The recommended approach is zero new dependencies throughout. Both retrieval defects are pure algorithmic fixes inside `ContentKbRelevanceService.cs` — a per-video diversity cap in `SelectTopClips` and a topical-relevance scoring reweight in `ScoreArtifact`. The re-validation is an in-process test execution using the existing `Spike001KbValueAbHarness`. If the gate clears, the Creator Philosophy-Profile follows existing patterns (`IRelationalDialect` for storage, the pluggable LLM-CLI backend for synthesis, optional DI injection into `DeckAnalysisPacketService`). If the gate fails, only the SRP split phase proceeds. The `OpenAI 2.10.0` SDK already present in `DeckFlow.Core.csproj` handles any LLM synthesis calls; no new packages are needed for any path.

The three key risks are: (1) the A/B gate is a binary, unconditional, BLIND decision point that gates ALL philosophy-profile work — building the profile on a broken or unvalidated foundation is explicitly prohibited; (2) provenance and prompt-injection mitigations are mandatory prerequisites before `content.kb.enabled` is flipped ON in production; (3) the ~82-video corpus is snail-heavy and cold-starts for commanders outside that coverage area — the gate itself must be validated across at least 3 distinct archetypes, not just Atraxa. The SRP split (DeckController 1,840 lines + CommandRunners 1,902 lines) is fully independent of KB state and can proceed regardless of the gate outcome.

---

## Key Findings

### Recommended Stack

All v1.6 work is deliverable within the existing package set. STACK.md confirms `OpenAI 2.10.0` is already in `DeckFlow.Core.csproj`, the `IRelationalDialect` / `ILlmDistillationService` / `ContentKbArtifactPathResolver` patterns already handle every new component's storage and synthesis requirements, and the internal-ctor test seam pattern in `ContentKbRelevanceService` already isolates scoring logic without new abstractions. Three alternative approaches were explicitly ruled out: BM25/Lucene.NET (IDF adds no signal at 80–200 docs), dense embeddings via ONNX Runtime (90–400MB model blows the 512MB Render Starter RAM cap), and pgvector/Qdrant (schema migration or sidecar for a corpus that fits in a `List<T>`).

**Core technologies (unchanged from existing stack):**
- `ContentKbRelevanceService.cs` (in-process): retrieval fix target — two surgical changes only, no new abstractions
- `IRelationalDialect` (SQLite/Postgres): storage for new `creator_philosophy_profiles` table if gate clears
- `OpenAI 2.10.0` / LLM-CLI backend: style-card synthesis if gate clears — already present, no new package
- `Spike001KbValueAbHarness` (xUnit): the re-validation gate harness — already built, extended not replaced
- ASP.NET Core MVC route attributes: mandatory for DeckController SRP split to preserve all URLs

**Zero new dependencies across all three work areas.** Any dependency addition is a scope violation.

### Expected Features

The feature landscape splits cleanly into gate-unconditional and gate-conditional work. The distinction is non-negotiable: FEATURES.md documents that irrelevant context causes measurable LLM degradation (GPT-4 flipped correct answers in 15% of cases from a small number of distracting passages), and Spike 001 Run 2 independently confirmed this on the live system. Building the philosophy-profile on an unfixed retriever is explicitly modeled as the highest-risk anti-feature.

**Must have — gate-unconditional (build regardless of gate outcome):**
- Per-video diversity cap in `SelectTopClips` — defect #1 fix; without it, one video monopolizes all retrieval slots
- Topical relevance scoring: commander-name exclusion + content-based signal — defect #2 fix; tag breadth must not outrank topical fit
- Re-run `Spike001KbValueAbHarness` against fixed retriever, BLIND, across at least 3 test decks — the gate itself
- DeckController / CommandRunners SRP split — long-deferred code quality; fully independent of KB state
- Prompt-injection structural wrapper + regex sanitizer in the injected KB block — mandatory before `content.kb.enabled` ON
- Harvest-date annotation on injected clips — already on `ContentKbExcerpt`; verify it appears in the prompt block

**Should have — gate-conditional (only if gate clears):**
- Creator Philosophy-Profile distillation pipeline (`ContentKbPhilosophyDistiller`, `creator_profiles` table, `synthesize-philosophy` CLI command)
- Provenance per principle (`source_video_id` + `source_timestamp_s` as non-nullable schema fields) — prerequisite for all profile work; without it the profile is a hallucination vector
- Persona block injection into deck-analysis prompt (`## Creator Heuristics` sub-section under `## Expert Context`)
- Contradiction preservation (structured `contradictions` array with dual provenance, not narrative smoothing)
- Recency weighting and `principle_era` date annotation on every injected principle
- Video-level curation admin toggle (`content_videos.excluded`) — low cost, improves gate corpus quality
- `content.kb.enabled` ON in production + SEL-02 expert-pin live re-confirm (carried from v1.5)

**Defer to v1.7+:**
- User-supplied creator sources (on-demand harvest + distill) — HIGH complexity, unproven value, latency risk
- Embedding-based semantic similarity scorer — premature at current corpus size; revisit at 500+ videos
- Multi-creator profile merge in a single analysis prompt — token budget risk; validate single-creator first

### Architecture Approach

The retrieval fix is entirely in-process: two surgical changes to `ContentKbRelevanceService.cs` with no interface changes, no new service registrations, and no change to the `GetMergedClipsAsync` 4-tier merge structure. The philosophy-profile (if gate clears) follows a strict additive pattern: a new optional `ICreatorPhilosophyProfileService?` dependency on `DeckAnalysisPacketService` mirrors the existing `IContentKbRelevanceService?` optional injection; offline synthesis goes through a new `ContentKbCommandRunners` class that splits from `CommandRunners.cs` in Phase 4 anyway. The SRP split is mechanical extraction: action methods move verbatim, route attributes are added explicitly, no logic changes.

**Major components — new or modified:**

1. `ContentKbRelevanceService` (modified) — per-video cap in `SelectTopClips`; topical-fit scorer reweight in `ScoreArtifact`; regex prompt-injection sanitizer added inline
2. `Spike001KbValueAbHarness` (extended) — add 2 additional test deck facts beyond Atraxa before the re-run gate
3. `ICreatorPhilosophyProfileStore` + `CreatorPhilosophyProfileStore` (new, Core) — `creator_philosophy_profiles` table via `IRelationalDialect`; only built if gate clears
4. `ICreatorPhilosophyProfileService` (new, Web) — scores and returns relevant principles; optional DI, flag-gated; only built if gate clears
5. `DeckToolsController` + `DeckPacketController` + `DeckPrimerController` (new, Web) — mechanical extraction from `DeckController`; all original URLs preserved via explicit `[Route]` attributes
6. `ContentKbCommandRunners` (new, CLI) — all content KB runners extracted from `CommandRunners.cs`; includes `RunSynthesizePhilosophyAsync` if gate clears

### Critical Pitfalls

1. **Tag-breadth beats topical fit — one video monopolizes all slots** — fix `SelectTopClips` with a soft per-video cap (recommend 2, not hard-1) plus a relevance floor so forced diversity does not inject noise when only one relevant video exists. Confirm clips span at least 2 distinct video sources with zero commander-name noise for the Atraxa deck after the fix. (PITFALLS P1 + P2)

2. **A/B gate cleared non-blind or on a single deck** — score `baseline.txt` FIRST, record rubric scores before reading `with-context-real.txt`. Run against at least 3 test decks (not just Atraxa). Record blind protocol explicitly in VERDICT.md before unblinding. Any gate clearance based only on "prompt looks correct" (not AI answer quality) is invalid. (PITFALLS P10 + P11 + P12)

3. **Philosophy-profile hallucination — principles without provenance** — every emitted principle must carry `source_video_id` as a non-nullable FK; no principle stored without a specific excerpt anchor. Add unit test `StyleCardSynthesizer_NoCitableEvidence_EmitsNoPrinciples`. This is a first-class schema requirement, not a post-ship hardening. (PITFALL P4)

4. **Prompt injection via untrusted transcript text** — add regex sanitizer for common injection patterns + structural context-boundary wrapper in the injected `## Expert Context` block BEFORE `content.kb.enabled` is flipped ON. The mitigation cost is low; the risk is present every time transcript-derived text reaches the LLM prompt. (PITFALL P7)

5. **DeckController split silently breaks URLs** — every action method moved to a new controller must carry an explicit `[Route]` attribute preserving the original path. Conventional routing derives URLs from controller class names; without explicit routes, every moved action silently changes its URL. Audit the Bridge extension (`background.js`) for hard-coded paths before splitting. (PITFALL P13)

---

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Retrieval Fix
**Rationale:** Prerequisite for everything else. The live retriever is worse than nothing (Run 2: NEGATIVE quality). No downstream KB work has any value until the two mechanism defects are corrected. This is also where prompt-injection mitigations must land — before `content.kb.enabled` goes ON.
**Delivers:** Fixed `SelectTopClips` (per-video soft cap + relevance floor); reweighted `ScoreArtifact` (topical-fit signal, commander-name exclusion penalty); regex sanitizer + structural context-boundary wrapper in the injected block; updated `ContentKbRelevanceServiceTests` calibration suite.
**Addresses:** Per-video diversity (table stakes), topical relevance (table stakes), prompt-injection mitigation (mandatory security prerequisite), harvest-date annotation verification.
**Avoids:** P1 (video monopoly), P2 (forced-diversity noise), P7 (prompt injection). Sets up clean inputs for the gate run.

### Phase 2: Re-Validation Gate (BLIND)
**Rationale:** The gate is an unconditional, binary branch point. It must be evaluated before any philosophy-profile work begins and before `content.kb.enabled` is flipped ON. The gate is not a formality — Spike 001 Run 2 confirmed that a reasonable-looking retriever can produce NEGATIVE quality. The gate must be run BLIND (score baseline first, record scores, then score with-context) and must cover at least 3 test decks.
**Delivers:** Updated `Spike001KbValueAbHarness` with 2 additional test deck facts (aggro/voltron + cEDH/combo); VERDICT.md addendum with blind rubric scores across all 3 decks; branch decision: PASS (proceed to Phase 3) or FAIL (proceed only to Phase 4; KB scope reduction deferred to v1.7).
**Addresses:** Value re-validation A/B gate (table stakes), KB un-dark conditional on gate pass.
**Avoids:** P10 (non-blind scoring), P11 (judging prompt not answer), P12 (single-deck overfit), P3 (corpus cold-start rate measured across 3 archetypes).
**Gate-PASS exit criteria:** at least 3 of 4 rubric dimensions score 3+; no quality loss vs. baseline; at least one dimension 4+; at least 2 distinct video sources for the Atraxa deck; confirmed blind.
**Gate-FAIL pivot:** Phase 3 is skipped entirely. SRP split (Phase 4) runs regardless. KB scope reduction or retirement planning deferred to v1.7.

### Phase 3: Creator Philosophy-Profile [CONDITIONAL — gate must pass]
**Rationale:** Only built if Phase 2 confirms the fixed retriever delivers net-positive value. Building the profile on a broken or unvalidated retriever is explicitly the highest-risk failure mode. If built, follows strict additive architecture: new optional service, new table, new CLI command — all following existing patterns. Provenance schema and hallucination gate are non-negotiable first steps within this phase.
**Delivers:** `ICreatorPhilosophyProfileStore` + `CreatorPhilosophyProfileStore` (Core); `ICreatorPhilosophyProfileService` (Web, optional DI); `CreatorPhilosophyContext` + `PhilosophyPrinciple` sealed records; `ContentKbCommandRunners.RunSynthesizePhilosophyAsync`; `synthesize-philosophy` CLI command; `## Creator Heuristics` prompt sub-section across all 3 AI variants; `content.kb.enabled` ON in production; SEL-02 expert-pin re-confirm; combined KB block cap enforced at 6,000 characters.
**Addresses:** Philosophy profile distillation (gated), provenance per principle (gated), persona block injection (gated), contradiction preservation (gated), recency weighting (gated), video-level curation toggle (gated, low cost, build early in this phase), prompt budget cap enforcement.
**Avoids:** P4 (hallucinated principles — provenance schema is the first deliverable in this phase), P5 (stale/contradictory opinions — contradiction array + `principle_era`), P6 (recency drift — 18-month demotion filter at injection), P8 (prompt-size blowup — 6,000-char combined cap), P9 (attribution errors — single-creator-per-prompt constraint in v1.6).
**Research flags:** Topical-scoring algorithm for principle relevance at query time (keyword overlap vs. LLM re-ranking) is an open question; recommend starting with Option 1 (in-process keyword match) and treating Option 2 (LLM re-ranking) as a follow-on if Option 1 proves insufficient. Corpus feasibility for per-creator profile synthesis from ~82 snail-heavy videos is MEDIUM confidence — the profile may be thinly evidenced for creators with fewer than 10 substantive harvested videos. Ops prerequisite: confirm at least one creator has 10+ substantive non-rating-series videos before the distillation run.

### Phase 4: SRP Split
**Rationale:** Independent of all KB phases. Long-deferred code quality work. Runs last to minimize blast radius during KB work — the DeckController extraction is high-touch and the routing risk (P13) is best contained in its own phase. CommandRunners split follows the same phase for logical cohesion.
**Delivers:** `DeckToolsController`, `DeckPacketController`, `DeckPrimerController` extracted from `DeckController.cs` (1,840 lines split into 4 focused controllers); `ContentKbCommandRunners` extracted from `CommandRunners.cs` (1,902 lines split into ~800-line deck-domain class + ~600-line KB class); explicit `[Route]` attributes on all moved actions; pre/post URL diff verification; updated controller test class references.
**Addresses:** DeckController/CommandRunners SRP split (unconditional).
**Avoids:** P13 (URL regression — explicit routes mandatory, pre-split URL list required), P14 (`_DeckToolTabs` controller-name coupling — pre-split audit of `_Layout.cshtml` and all shared partials), P15 (shared helper duplication — two-commit discipline: `CommandRunnerHelpers` extraction first, class split second; build + test green after each commit).
**Research flags:** Standard mechanical refactor; no deeper research needed. Pre-split checklist: grep `RouteData.Values["controller"]` in all shared partials; audit `background.js` for hard-coded `/deck` paths; generate pre-split URL list for post-split diff.

### Phase Ordering Rationale

- Phase 1 must precede Phase 2: the gate tests the fixed retriever; running it on the broken retriever would produce the same NEGATIVE result as Spike 001 Run 2.
- Phase 2 must precede Phase 3: the gate outcome is a binary branch; building the profile before the gate is explicitly prohibited by the milestone scope and the seed document.
- Phase 3 is conditional: if the gate fails, the milestone closes after Phase 4 with KB dark and scope reduction deferred to v1.7.
- Phase 4 is fully independent: isolates the high-touch DeckController extraction from the KB work; any regression in the SRP split cannot contaminate the gate run or KB deployment.
- Within Phase 3: `ICreatorPhilosophyProfileStore` (Core) must be built before `ICreatorPhilosophyProfileService` (Web); `CreatorPhilosophyContext` record must exist before `DeckAnalysisPacketService` changes compile; provenance schema must precede the synthesizer implementation.

### Research Flags

Phases needing deeper research or judgment calls during planning:
- **Phase 2 (Re-Validation Gate):** The topical-scoring reweight in `ScoreArtifact` needs design validation — specifically, whether the commander-name exclusion penalty should be a hard gate or a score multiplier, and whether the relevance floor threshold needs calibration against the live corpus. The planner should specify these as named constants with rationale rather than leaving them to implementer judgment.
- **Phase 3 (Philosophy-Profile, if triggered):** Three open questions require planning-time resolution before execution: (a) topical-scoring algorithm for principle relevance at query time (keyword overlap Option 1 vs. LLM re-ranking Option 2); (b) corpus feasibility — confirm at least one creator has 10+ substantive non-rating-series videos before committing to synthesis; (c) gate-fail-within-Phase-3 pivot definition — what the plan looks like if the gate clears but the profile synthesizer produces thin output on the available corpus.

Phases with standard patterns (minimal research needed):
- **Phase 1 (Retrieval Fix):** Pure in-process algorithmic change to a well-understood class. The existing internal-ctor test seam already supports deterministic testing. No new patterns needed.
- **Phase 4 (SRP Split):** Mechanical extraction. All patterns are established in the codebase. The primary risk is procedural (two-commit discipline, pre-split audit checklist), not technical.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack (zero-new-dependencies verdict) | HIGH | Both retrieval defects are pure algorithmic changes; `OpenAI 2.10.0` confirmed in `DeckFlow.Core.csproj`; all three work areas verified against installed package set |
| Features (gate-unconditional) | HIGH | Defects confirmed by Spike 001 Run 2 direct codebase execution; table-stakes features are verifiable algorithmic properties |
| Features (gate-conditional, philosophy-profile) | MEDIUM | Profile value hypothesis is explicitly what the gate tests; confidence is in the approach, not the outcome |
| Architecture (retrieval fix) | HIGH | `SelectTopClips` and `ScoreArtifact` are `internal static`, directly tested, well-understood; change is surgical |
| Architecture (philosophy-profile) | MEDIUM | Pattern follows existing `ContentKbRelevanceService` and `ContentSiteIndexStore` exactly; profile synthesis output quality depends on LLM + corpus, neither of which is under code control |
| Architecture (SRP split) | HIGH | Mechanical extraction with well-understood routing risk; the risk is procedural and has explicit mitigations |
| Pitfalls (retrieval + gate) | HIGH | All primary pitfalls grounded in Spike 001 Run 2 direct evidence |
| Pitfalls (philosophy-profile) | MEDIUM | No prior build to inspect; pitfalls derived from RAG literature, seed document requirements, and extrapolation from the retrieval defects |

**Overall confidence:** HIGH for retrieval fix and SRP split phases. MEDIUM for the philosophy-profile path, contingent on gate outcome and corpus composition.

### Gaps to Address

- **Topical-scoring algorithm constants:** STACK.md recommends a commander-name bonus multiplier + content-text overlap as the scorer reweight, but threshold constants (penalty multiplier magnitude, relevance floor percentage) need calibration against the live corpus. The planner should specify these as named constants with rationale rather than leaving them to implementer judgment.
- **Gate-fail pivot definition:** The research documents Options 2 (reconsider KB scope) and 3 (retire clip injection) as post-fail paths but does not specify which to pursue. The Phase 2 plan should include a written gate-fail decision protocol so the gate outcome immediately routes to a defined next step.
- **Corpus feasibility check:** The ~82-video corpus is snail-heavy. Before Phase 3 planning, confirm via a `content_videos` query how many substantive (non-rating-series, non-excluded) videos exist per creator. If no creator meets the 10-video threshold for profile synthesis, the philosophy-profile deliverable scope must be revised.
- **RAG grounding algorithm for Phase 3:** STACK.md recommends Option 1 (in-process keyword similarity) as the v1.6 baseline with Option 2 (LLM re-ranking) as a follow-on. The Phase 3 plan should make this explicit so Codex does not default to the more expensive option.

---

## Sources

### Primary (HIGH confidence)
- `.planning/spikes/001-kb-value-ab/VERDICT.md` — Spike 001 Run 1 + Run 2 results; root cause analysis of both retrieval defects; gate NOT cleared
- `.planning/research/STACK.md` (2026-06-10) — zero-new-dependency verdict across all three work areas; alternatives ruled out with RAM/IDF rationale
- `.planning/research/FEATURES.md` (2026-06-10) — table stakes vs. gated feature split; anti-features catalog; dependency graph
- `.planning/research/ARCHITECTURE.md` (2026-06-10) — component map, data flow diagrams, dependency-ordered build sequence
- `.planning/research/PITFALLS.md` (2026-06-10) — 15 pitfalls ordered by likelihood x impact; pitfall-to-phase mapping table
- `DeckFlow.Web/Services/ContentKbRelevanceService.cs` — live scorer; `SelectTopClips` defect confirmed by direct inspection
- `DeckFlow.Web/Controllers/DeckController.cs` — 1,840 lines confirmed; action method groupings confirmed
- `DeckFlow.CLI/CommandRunners.cs` — 1,902 lines confirmed; shared helper methods confirmed
- `DeckFlow.Core/DeckFlow.Core.csproj` — `OpenAI 2.10.0` confirmed present

### Secondary (MEDIUM confidence)
- `.planning/seeds/creator-philosophy-profile.md` — style-card shape, hallucination gate requirement, contradiction-preservation mandate
- arxiv 2505.18761 — distracting passages caused GPT-4 to flip correct answers in 15% of cases
- arxiv 2410.05983 — "lost in the middle" context flooding effect
- arxiv 2502.11228 — RAG diversity / MMR / Vendi-RAG patterns
- UBOS attribution survey — AIS attribution / sentence-level traceability requirement

### Tertiary (LOW confidence — informational only)
- arxiv 2504.08745 — per-author RAG personalization; directional evidence for philosophy-profile approach
- arxiv 2509.19376 — temporal recency in RAG; recency-weighting rationale

---

*Research completed: 2026-06-10*
*Ready for roadmap: yes*
