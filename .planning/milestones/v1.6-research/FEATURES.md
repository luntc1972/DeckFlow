# Feature Research — v1.6 Content KB Retrieval Fix + Creator Philosophy-Profile

**Domain:** Content-grounded expert-lens injection for LLM deck analysis (RAG-style)
**Researched:** 2026-06-10
**Confidence:** HIGH (spike evidence + codebase inspection + RAG literature)

---

## What Separates a Useful Expert Lens from a Generic-Advice Dump

This is the central question, and Spike 001 gave a concrete answer: the current retriever is a
generic-advice dump. The distinction matters for every feature decision below.

**Generic-advice dump (what the live retriever currently produces):**
- Clips from a single high-scoring video that monopolizes all five slots.
- Content about *other* commanders: 3 of 5 Run 2 clips were about Kaalia and Animar, not Atraxa.
- Deckbuilding-101 maxims ("focus your deck", "protect your threats") that a capable LLM produces
  unprompted from its own training data — providing zero marginal lift.
- No diversity of perspective: one video, one theme, five clips.

**Useful expert lens (the target):**
- Each injected clip contributes a signal the LLM would NOT produce from training data alone — a
  creator-specific heuristic, a counterintuitive take, or an observation about this commander's
  known failure modes.
- Clips span distinct videos and distinct perspectives (per-video diversity cap).
- Content is topically matched to the deck being analyzed, not just tag-adjacent.
- Every injected passage is traceable to a verified source; nothing is synthesized from memory.
- Contradictory creator opinions are preserved and labeled, not averaged away.
- Recency is visible so the analyst can contextualize advice from different metagame eras.

**Research evidence for the negative-value risk:**
Irrelevant context causes measurable LLM degradation. Distracting passages (topically related but
contextually off) reduced Llama2 answer accuracy from 56% to 18%. GPT-4 flipped correct answers to
incorrect in 15% of cases even with a small number of irrelevant passages. The "lost in the middle"
effect means stuffing a context window with generic text actively degrades the LLM's ability to
find the signal (arxiv 2410.05983, arxiv 2505.18761). Spike 001 Run 2 independently confirmed this:
the real `ContentKbRelevanceService` scored WORSE than hand-picked generic clips.

---

## Table Stakes (Unconditional — Build Regardless of Gate Outcome)

Features the expert-lens block must have to not actively harm prompt quality. Missing these means
the block is worse than nothing — the state Spike 001 proved is the current reality.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Per-video diversity cap in `SelectTopClips` | Without it, one video monopolizes all slots (Run 2 defect #1). Users expect varied perspectives, not 5 clips from one tangential video. | LOW | Track seen video IDs during selection; max N clips per video (suggested cap: 2). `SelectTopClips` inner loop at line 469 of `ContentKbRelevanceService.cs` — add `videoClipCount` tracking. |
| Topical relevance scoring that filters commander-specific noise | Tag-overlap rewarded "Glass Cannon Commanders" (broad tags: midrange/combo/ramp/aggro) over directly relevant "Too Much Ramp" (Run 2 defect #2). Tag breadth must not outrank topical fit. | MEDIUM | Apply a commander-name exclusion penalty: if a clip's title/summary names a specific commander that is NOT the current deck's commander, reduce its score (or gate it out). Also weight on-topic signals: summary keyword overlap against the deck's archetypes and commander name. Does not require embeddings at current corpus size. |
| Harvest date rendered in injected clips prompt block | Users need to know if advice is from 2021 or 2025; Commander format changes (bans, power shifts) affect older clip validity. | LOW | `HarvestDate` is already on `ContentKbExcerpt` and propagated through the service. Verify it appears in the formatted `## Expert Context` prompt block. |
| Minimum two-dimension AND gate preserved | Already implemented: `MinSelectionScore = 2.0`, `dimensionsHit >= 2`. Single-dimension matches are generic noise. | LOW | Do NOT weaken this gate during the fix. It is the primary guard against purely generic content. |
| Value re-validation A/B gate | Spike 001 established the contract: clear lift on fixed retriever = proceed; still marginal = pivot or retire. Must re-run `Spike001KbValueAbHarness` `EmitRealRetrievalPrompt` fact against the fixed scorer. External ChatGPT paste confirmation recommended. | LOW | Run the existing harness unchanged; only the retriever implementation changes. Assess against same rubric dimensions: Specificity, Creator-voice, Novel signal, Actionability. |
| KB un-dark (`content.kb.enabled` ON) after gate passes | The entire reason for v1.6. The flag has been OFF since Phase 30 UAT 2026-06-07. | LOW | Prerequisite: gate passes. SEL-02 expert-pin live-pin re-confirm should happen in the same window (carried from v1.5). |

---

## Differentiators — Conditional on Gate Passing [GATED]

These features are worth building only if the fixed retriever proves the expert-context block adds
net positive value. Every feature in this section is marked **[GATED]** and must not be built if
the A/B gate returns marginal or negative.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Per-creator philosophy profile — distilled style-card **[GATED]** | Conditions the LLM to reason through a specific creator's lens: their recurring heuristics, biases, counterintuitive takes. This is the original "creator-as-lens" KB vision. A creator's distinctive deckbuilding philosophy is not in the LLM's training data — it is the only content that could provide genuine marginal lift. | HIGH | Synthesizes principles across the whole channel corpus (not per-video). New `profile-synthesizer` distillation step reading existing clip excerpts + summaries and extracting recurring, non-generic claims. New `creator_profiles` table. Profile injected as a persona block in the analysis prompt. |
| Provenance per injected principle **[GATED]** | Each principle in the style-card carries its source video ID + publish date. Every stated principle traces to a verified transcript passage, not synthesized from model memory. Prevents "citation-shaped hallucination": an opinion that looks grounded but is invented. | MEDIUM | Schema: `creator_principle(id, creator_slug, principle_text, source_video_id, source_timestamp_s, publish_date)`. Distillation prompt must require verbatim or close-paraphrase evidence per principle; reject unsupported assertions. At query time, inject only principles whose `source_video_id` is in the published corpus. |
| Contradiction preservation **[GATED]** | Creator contradicts themselves across videos? Surface the tension — "generally favors X, but argued against it for aggro decks (video Y)". Averaging contradictions produces a false consensus that misrepresents what the creator actually argued. A capable LLM handles labeled tension better than a smoothed falsehood. | MEDIUM | Distillation prompt detects conflicting principles on the same topic; stores them as a `conflict_pair` row with both source references. Prompt serializer renders as: "[Creator] generally argues X (v1) but argued against it in [context] (v2)." |
| Recency weighting in profile refresh **[GATED]** | Principles from 2021 may be obsolete (pre-ban metagame, power-curve shifts). Default: recency-weight so recent videos carry more weight; older principles stay but are dated so the era is visible. | MEDIUM | Weight = `1 / (1 + months_since_publish / 12)` applied during profile synthesis. Stale principles are kept with their `publish_date` visible, not deleted. Incremental: fold new videos into the profile on harvest refresh (reuse the existing 5-day pipeline). |
| Video-level curation granularity **[GATED]** | Admin can exclude individual videos rather than toggling a whole channel. Needed for creators like trinket-mage whose ~690-video "Ranking All Legends" rating series should be excluded from KB while strategy content is included. | LOW | `content_videos.excluded` boolean column + admin UI toggle. Already implied by the philosophy-profile seed note on trinket-mage. Low implementation cost, high corpus-quality impact. |
| User-supplied creator sources (on-demand) | Let the user name a YouTube channel or video at analysis time; DeckFlow retrieves and distills it inline. Removes the admin-curated-source dependency. | HIGH | Requires on-demand harvest + distill pipeline inline in a web request. Token budget, rate limit, and latency concerns are significant. **Defer to v1.7+.** Do not build in v1.6. |

---

## Anti-Features

These appear attractive but must be explicitly avoided. The "generic-advice dump" failure mode is
the through-line: every anti-feature below is a path back to it.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Whole-channel generic content without video-level filtering | More content = more coverage (intuition). Simple to implement. | Produces the Run 2 failure: a broad-topic video with wide tags outscores narrow on-topic videos. "Glass Cannon Commanders" (tags: midrange/combo/ramp/aggro/Upgraded/cEDH) monopolized all 5 slots over "Too Much Ramp" and "5 Most Common Mistakes" which were directly relevant to the Atraxa deck. | Video-level curation: admin marks excluded videos; harvest respects the flag. Per-video diversity cap in `SelectTopClips`. |
| Embedding-based semantic similarity scoring (pgvector) | Sounds like the "correct" RAG solution; embeddings capture meaning beyond keyword overlap. | At 82 visible rows (Run 2 corpus size), embeddings add infrastructure cost (Render Starter: 512MB RAM — no room for an in-process model) without improving relevance over a well-designed keyword + metadata scorer. Semantic similarity can still return topically adjacent but content-distinct passages. The fundamental problem (broad-tag scoring) is not an embedding problem — it is a feature-engineering problem. | Fix the tag-overlap scorer with content-based signals: commander-name exclusion filter, summary keyword overlap against deck archetypes. Revisit embeddings when corpus exceeds ~500 videos and the simpler fix is validated. |
| Averaging creator contradictions into a single clean principle | Seems cleaner; contradictions confuse the LLM. | Averaging produces a false consensus that misrepresents what the creator argued in context. A capable model handles labeled tension. A smoothed falsehood that confidently misattributes a position is worse than a hedged contradiction. | Store contradictions as labeled conflict pairs with both source references (see Differentiators section). |
| Philosophy profile without hallucination gate (provenance) | Profiles are high-value if grounded; easy to synthesize if not gated. | An ungrounded profile is a fabricated persona. The LLM treats injected principles as facts the creator holds. If those principles were synthesized without verified transcript evidence, the analysis is polluted by invented opinions. This is the "citation-shaped hallucination" failure mode: output that looks grounded but is not. | Require provenance per principle at distillation time. Schema enforces `source_video_id` as a non-nullable FK. No principle stored without a specific excerpt anchor. |
| Evergreen generic deckbuilding advice as a fallback tier | "Some context is better than none." | Spike 001 Run 1 proved generic maxims ("focus your deck", "protect your threats") provide marginal-to-zero lift over what the LLM already knows. If misapplied — the glass-cannon frame was applied to a grindy goodstuff pile — they degrade quality. The tier-4 evergreen slot is a footgun if it admits generic content. | Tier-4 (evergreen) is acceptable only for creator-distinctive, non-generic content. Do not admit generic deckbuilding maxims to the corpus; gate the evergreen flag in admin to creator-specific observations only. |
| Injecting clips about commanders other than the one being analyzed | Corpus breadth; adjacent commanders share archetypes. | This is exactly defect #3 of Run 2: 3 of 5 clips were about Kaalia and Animar. The LLM either ignores them (wasted budget) or misapplies the frame (quality loss). Evidence: distracting topically-adjacent passages caused GPT-4 to flip correct answers in 15% of cases even in small numbers (arxiv 2505.18761). | Commander-name exclusion filter: if a clip's title or summary names a specific other commander, apply a scoring penalty sufficient to deprioritize it unless no on-topic alternatives exist. |
| Fine-tuning the LLM on creator content | Theoretically the purest creator-voice solution. | Fine-tuning requires model weight access, is cost-prohibitive, and is irreversible for a specific creator. The philosophy-profile seed explicitly rules this out. Prompt-time persona injection achieves the same goal without those constraints. | Distilled style-card + RAG grounding at inference time. |

---

## Feature Dependencies

```
[Per-video diversity cap in SelectTopClips]  ← unconditional
    └──required by──> [Value re-validation A/B gate]

[Topical relevance scoring fix]  ← unconditional
    └──required by──> [Value re-validation A/B gate]

[Value re-validation A/B gate — PASS]
    └──unlocks──> [KB un-dark (content.kb.enabled ON)]
    └──unlocks──> [SEL-02 expert-pin live-pin re-confirm]
    └──unlocks──> all [GATED] features below

[Video-level curation granularity]  ← GATED, low cost, build before or with gate-pass deploy
    └──improves quality of──> [Value re-validation A/B gate corpus]

[Provenance per principle schema]  ← GATED
    └──required by──> [Philosophy profile distillation]
    └──required by──> [Hallucination gate: no principle without source]

[Philosophy profile distillation pipeline]  ← GATED
    └──requires──> [Provenance per principle schema]
    └──requires──> [Existing transcript corpus (already built in v1.4)]
    └──requires──> [Existing harvest/refresh pipeline (already built in v1.4)]
    └──optional enhances──> [Contradiction preservation]
    └──optional enhances──> [Recency weighting in profile refresh]

[Persona block injection into deck-analysis prompt]  ← GATED
    └──requires──> [Philosophy profile distillation pipeline]
```

### Dependency Notes

- **Retrieval fix is an unconditional prerequisite.** No downstream feature is worth building until
  the retriever selects on-topic, diverse clips. These are the two defects identified in Run 2.
- **Gate pass is the branch point.** If the fixed retriever still fails the A/B, the correct path
  is scoping down or retiring the KB — not building the philosophy profile on a broken foundation.
  The seed explicitly states: "Do NOT green-light Content KB v2 / philosophy-profile on current
  evidence."
- **Video-level curation can precede or accompany the gate.** It improves the corpus quality
  available to the A/B test and is low cost, so building it unconditionally makes the gate signal
  more meaningful.
- **Provenance must precede the philosophy profile.** A profile without provenance is a hallucination
  vector. Build the provenance schema first, then the synthesizer, then prompt injection.
- **Contradiction preservation and recency weighting layer onto the profile.** They share the
  provenance schema and can be a single phase with the profile or an immediate follow-on.

---

## MVP Definition for v1.6

### Gate-Unconditional (Build Regardless of Gate Outcome)

- [ ] Per-video diversity cap in `SelectTopClips` — defect #1 fix
- [ ] Topical relevance scoring: commander-name exclusion filter + content-based topical signal — defect #2 fix
- [ ] Re-run `Spike001KbValueAbHarness` against fixed retriever; external ChatGPT paste for confirmation
- [ ] KB un-dark (`content.kb.enabled` ON) if gate passes
- [ ] SEL-02 expert-pin live-pin re-confirm in the KB-enable window (carried from v1.5)
- [ ] DeckController / CommandRunners SRP split (final phase; long-deferred; independent of KB)

### Gate-Conditional [GATED] — Build Only if Gate Passes

- [ ] Video-level curation admin toggle (`content_videos.excluded` column + UI)
- [ ] Provenance schema: `creator_principle(id, creator_slug, principle_text, source_video_id, source_timestamp_s, publish_date)` — prerequisite for all profile work
- [ ] Philosophy profile distillation pipeline (`profile-synthesizer`) — core GATED feature
- [ ] Persona block injection into deck-analysis prompt
- [ ] Contradiction preservation (conflict pairs in distillation)
- [ ] Recency weighting in profile refresh

### Defer to v1.7+

- [ ] User-supplied creator sources (on-demand harvest + distill) — HIGH complexity, unproven value
- [ ] Embedding-based semantic similarity scorer — premature at current corpus size; revisit at ≥500 videos
- [ ] Multi-creator profile merge (analyzing a deck against multiple creators simultaneously) — token budget risk

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority | Gate |
|---------|------------|---------------------|----------|------|
| Per-video diversity cap | HIGH | LOW | P1 | Unconditional |
| Topical relevance scoring fix | HIGH | MEDIUM | P1 | Unconditional |
| Value re-validation A/B gate | HIGH (decision point) | LOW | P1 | Unconditional |
| KB un-dark | HIGH | LOW | P1 | Gate pass |
| Video-level curation granularity | MEDIUM | LOW | P2 | Gate pass (low cost, do early) |
| Provenance per principle | HIGH | MEDIUM | P2 | Gate pass |
| Philosophy profile distillation | HIGH | HIGH | P2 | Gate pass |
| Persona block prompt injection | HIGH | MEDIUM | P2 | Gate pass |
| Contradiction preservation | MEDIUM | MEDIUM | P3 | Gate pass |
| Recency weighting refresh | MEDIUM | MEDIUM | P3 | Gate pass |
| DeckController SRP split | LOW user / HIGH code quality | HIGH | P2 | Unconditional |
| Embedding similarity scorer | LOW (corpus too small) | HIGH | Deferred | n/a |
| User-supplied creator sources | MEDIUM | HIGH | Deferred | n/a |

---

## Provenance / Contradiction / Recency: Hallucination Gate Detail

These three attributes are the integrity properties of the philosophy profile. Without them the
profile is a liability, not an asset.

### Provenance (Hallucination Gate — Required for Any Profile Feature)

The failure mode is "citation-shaped hallucination": a principle that looks grounded because it has
a source label, but the source passage does not actually support the stated claim. Research confirms
this: AIS (Attributable to Identified Sources) attribution requires sentence-level traceability
where every factual statement links to a cited snippet that *actually supports* the claim
(UBOS attribution survey). The `StrictCitations` RAG strategy enforces explicit provenance and
constrains models to verifiable retrieved evidence.

Implementation requirements:
- Distillation prompt must require verbatim or close-paraphrase evidence per principle. No principle
  without a specific excerpt anchor.
- Schema: `creator_principle(id, creator_slug, principle_text, source_video_id,
  source_timestamp_s, publish_date, confidence)`.
- At query time: inject only principles where `source_video_id` is in the published corpus (no
  orphaned principles from deleted or hidden videos).

### Contradiction Preservation

A creator who argued "minimize mana rocks" in 2022 and "add more rocks for this specific deck" in
2024 is context-sensitive, not inconsistent. Averaging to "mana rocks: neutral" loses both signals.

Preservation rule:
- If two principles for the same creator share the same topic keyword and have opposing polarity
  (detected at distillation time), store as a `conflict_pair` row with both source references.
- Prompt serializer renders: "[Creator] generally argues X (source A) but argued Y in [context]
  (source B)."

### Recency

Commander format changes make temporal context essential: bans, power-curve shifts, new staples.
An injected principle from 2021 about format norms may be stale post-bracket-guidance updates.

Mitigation:
- `publish_date` on every principle (derivable from the video's published timestamp — already
  available in `content_videos.published_utc`).
- Default sort: newer principles surface first within a topic cluster.
- Profile refresh re-evaluates whether a principle is still supported by recent transcript content
  (incremental synthesis pass on new videos, not full channel re-distillation). Reuses the
  existing 5-day harvest refresh pipeline.

---

## Sources

- Spike 001 A/B verdict: `.planning/spikes/001-kb-value-ab/VERDICT.md`
- Creator philosophy profile seed: `.planning/seeds/creator-philosophy-profile.md`
- `ContentKbRelevanceService` implementation: `DeckFlow.Web/Services/ContentKbRelevanceService.cs`
- RAG diversity / MMR / Vendi-RAG: https://arxiv.org/pdf/2502.11228
- Irrelevant context degradation (Llama2 56%→18%, GPT-4 15% flip rate): https://arxiv.org/pdf/2505.18761
- "Lost in the middle" / context flooding: https://arxiv.org/pdf/2410.05983
- Provenance / AIS attribution in RAG: https://ubos.tech/attribution-techniques-for-mitigating-hallucinated-information-in-rag-systems-a-survey-4/
- Per-author RAG personalization (author features + contrastive examples): https://arxiv.org/pdf/2504.08745
- Temporal recency in RAG (freshness priors): https://arxiv.org/pdf/2509.19376
- Context engineering vs raw RAG (structured injection vs unstructured dump): https://productleadersdayindia.org/blogs/context-engineering-vs-prompt-engineering/context-engineering-vs-rag.html
- Context poisoning / stale KB content: https://www.elastic.co/search-labs/blog/context-poisoning-llm

---

*Feature research for: DeckFlow v1.6 Content KB Retrieval Fix + Creator Philosophy-Profile*
*Researched: 2026-06-10*
