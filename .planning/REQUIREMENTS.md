# Requirements — v1.6 Content KB Retrieval Fix + Value Re-Validation

**Milestone goal:** Make the Content KB earn its keep — fix the retrieval defects Spike 001 exposed, prove lift through a blind A/B gate, then *conditionally* build the Creator Philosophy-Profile; close with the long-deferred DeckController/CommandRunners SRP split.

**Driver:** Spike 001 verdict = MARGINAL → leaning NEGATIVE (`.planning/spikes/001-kb-value-ab/VERDICT.md`). Two confirmed defects: (1) `SelectTopClips` has no per-video diversity; (2) tag-overlap scoring rewards tag breadth over topical fit. KB ships dark today.

**Gate semantics:** The re-validation gate (KBV) is **unconditional, binary, and blind**. PHIL-* and KBD-* requirements are **CONDITIONAL** — they activate only if the gate shows clear lift. If the gate stays marginal, the milestone closes after SRP with the KB still dark (or retired), per the pivot decision recorded in KBV-04.

---

## v1.6 Requirements

### KB Retrieval Fix (KBR) — unconditional

- [ ] **KBR-01**: A user's Expert Context block draws from multiple distinct videos — `SelectTopClips` enforces a per-video clip cap so no single video monopolizes the slots.
- [ ] **KBR-02**: Clip selection scores by topical fit to the deck (commander/archetype/bracket), not tag breadth — a video whose content is about unrelated commanders is penalized/excluded (fixes the Kaalia/Animar leakage).
- [ ] **KBR-03**: Injected clip text from third-party transcripts cannot act as instructions — a structural boundary + sanitizer neutralizes prompt-injection before any clip reaches the LLM prompt.
- [ ] **KBR-04**: The retrieval changes are locked by unit tests, including a regression test reproducing the Spike 001 Run-2 Atraxa scenario (asserts diversity + topical exclusion).

### KB Value Re-Validation Gate (KBV) — unconditional, binary

- [ ] **KBV-01**: The Spike 001 harness runs the *fixed* retriever across ≥3 representative decks (varied commanders/archetypes), emitting baseline vs with-context prompts per deck.
- [ ] **KBV-02**: A blind A/B is scored on the AI *answers* (not the prompts) against the existing rubric (specificity, creator-voice, novel signal, actionability) for each deck.
- [ ] **KBV-03**: The verdict is recorded in the spike (`VERDICT.md`) with per-deck rubric scores and a clear VALIDATED / MARGINAL outcome.
- [ ] **KBV-04**: The gate outcome routes the milestone explicitly — VALIDATED → proceed to PHIL/KBD; MARGINAL → recorded pivot decision (fix-again / per-deck retrieval pivot / retire), KB stays dark.

### Creator Philosophy-Profile (PHIL) — CONDITIONAL on KBV-04 = VALIDATED

- [ ] **PHIL-01**: A `creator_philosophy_profiles` store persists per-creator principles with SQLite+Postgres parity; every principle has a non-nullable provenance link to a source video + passage.
- [ ] **PHIL-02**: A CLI synthesizer (reusing the existing LLM-CLI distill backend) produces a creator style-card of distinctive heuristics — each principle traced to a verified transcript passage; generic deckbuilding-101 maxims are excluded (anti-feature).
- [ ] **PHIL-03**: The deck-analysis prompt can inject a creator's grounded principles (RAG over the profile) as an attributed sub-section, behind a subordinate flag (`content.kb.profiles.enabled`), null-graceful when absent.
- [ ] **PHIL-04**: A principle that cannot be traced to a source passage is never injected (hallucination gate); contradictory creator opinions are preserved (not silently merged) and recency is weighted.

### KB Un-Dark + Follow-ups (KBD) — CONDITIONAL on KBV-04 = VALIDATED

- [ ] **KBD-01**: `content.kb.enabled` is flipped ON in production only after the gate passes and the prompt-injection mitigation (KBR-03) is verified live.
- [ ] **KBD-02**: The SEL-02 expert-pin fix is re-confirmed live in the KB-enabled window (pinned video appears in the Expert Context block).

### Controller SRP Split (SRP) — unconditional, independent

- [ ] **SRP-01**: `DeckController` is decomposed into focused feature controllers with **all routes preserved** (explicit `[Route]`/action attributes; URLs unchanged), and the workflow-tab active-state set correctly on every new controller.
- [ ] **SRP-02**: `DeckFlow.CLI/CommandRunners` is split at the content-KB boundary (deck-domain vs content-KB runners), shared helpers extracted first (two-commit discipline), all commands still registered.
- [ ] **SRP-03**: Behavior is unchanged — existing tests pass against the split (logger-generic references updated), with no new warnings; the split adds no user-facing change.

---

## Future Requirements (deferred)

- Embedding/vector retrieval (pgvector / ONNX sentence-transformers) — deferred until corpus >~500 videos; overkill + RAM-cap risk at current ~82 rows.
- Gemini paste-limit unblock (`DECKFLOW_GEMINI_ENABLED`) — deferred from v1.5/v1.6; stays flag-gated.
- SpellbookCombo ranking fields (PRM-08 priority ranking) — deferred from v1.5.

## Out of Scope

- Framework migration (pinned ASP.NET 10 + Razor).
- New NuGet/npm dependencies for retrieval (proven unnecessary — algorithmic fix).
- Per-deck targeted retrieval / user-supplied sources — only revisited if the gate fails and the pivot decision (KBV-04) selects it.
- Re-harvesting / expanding the creator corpus — out of scope; work the existing corpus.

## Traceability

<!-- Filled by the roadmapper: REQ-ID → Phase. -->

| REQ-ID | Phase |
|--------|-------|
| (pending roadmap) | |
