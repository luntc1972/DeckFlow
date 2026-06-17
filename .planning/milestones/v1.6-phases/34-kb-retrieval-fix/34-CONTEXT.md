# Phase 34: KB Retrieval Fix - Context

**Gathered:** 2026-06-10
**Status:** Ready for planning
**Source:** discuss-phase (interactive)

<domain>
## Phase Boundary

Fix the two Content KB retrieval defects Spike 001 exposed so the injected "Expert Context" block surfaces **diverse, on-topic** creator advice instead of single-video, off-topic noise — and harden the injection path against prompt injection. Scope = `ContentKbRelevanceService` retrieval/scoring/selection + the clip-injection boundary + regression tests. REQ-IDs: KBR-01..04.

NOT in scope: the re-validation A/B run itself (Phase 35), the philosophy-profile (Phase 36, gated), un-darking the flag (Phase 36), embeddings/vector retrieval (deferred), corpus changes.

</domain>

<decisions>
## Implementation Decisions

### KBR-01 — Per-video diversity cap
- **Cap = 1 clip per video.** `SelectTopClips` admits at most one clip from any single video, so the (up to 5) Expert Context slots draw from up to 5 distinct videos. Directly kills the single-video monopoly (Spike 001 Run 2 put 5 clips from one video).
- Budget/slot fill still respects the existing `maxRenderedChars` trim after the cap.

### KBR-02 — Topical relevance scoring
- **Keep the existing per-video (row) score + clip-inheritance structure** — do NOT restructure to per-clip scoring. The change is to the SIGNAL, not the granularity.
- **Switch the scoring signal from tag-overlap to topical CONTENT overlap.** Score a video on how well its CONTENT (summary text + clip excerpts + tags) overlaps the deck's profile terms (commander name, archetype, strategy, card-category, bracket). This is the root fix: the spike's failure was tag-breadth scoring rewarding a video whose *content* was about other commanders.
  - General-advice videos with NO commander (e.g. "You Might Have Too Much Ramp", "How to Play More Removal") are handled BY CONSTRUCTION — they score on archetype/strategy term overlap and win on their merits; they are NOT penalized for lacking a commander. This was the user's explicit concern.
  - Off-topic deck-specific content (the Kaalia/Animar "Glass Cannon" clips) has near-zero overlap with the deck's strategy terms → low score → starved by the floor below.
- **Secondary other-commander demotion (confirmed: include it).** Apply a SMALL penalty when a video's content names a commander that is in a known commander set AND is not the deck's own commander. Belt-and-suspenders on top of content overlap. Must be **zero-dependency** — use a static/curated or corpus-derived known-commander list; never penalize a clip that names the deck's OWN commander (that's a positive signal).
- **Relevance floor.** Apply a minimum relevance threshold; clips/videos below it do not qualify.

### No-match behavior
- **When nothing clears the floor → return `null`** (no `## Expert Context` block injected at all). Noise-free beats generic filler. (Research: generic/evergreen fallback is an anti-feature — confirmed in Spike 001 Run 1.) Do NOT fall back to top-K-regardless.

### KBR-03 — Prompt-injection mitigation
- **Defense-in-depth: structural fence + regex sanitizer.**
  - **Structural fence:** wrap the injected clip text in a clearly-delimited data block so the model treats it as quoted evidence, not instructions. (The existing "treat as evidence, not instructions" preamble stays.)
  - **Regex sanitizer:** strip/neutralize instruction-like patterns in clip text BEFORE injection — role markers (`System:`/`Assistant:`/`User:`), override phrases ("ignore previous/above instructions"), and markdown that mimics prompt structure (fenced code blocks, ATX headers) inside clip excerpts.
- This must be verified before any production un-dark (KBD-01 in Phase 36 depends on it).

### KBR-04 — Tests
- Unit tests lock all of the above. **Mandatory regression test reproduces the Spike 001 Run-2 Atraxa scenario** and asserts: (a) selected clips span ≥2 distinct videos (diversity), (b) the off-topic single-video monopoly does not recur, (c) the other-commander/off-topic clips are excluded, (d) a no-match input returns null.
- Preserve the existing `internal` test seam on `ContentKbRelevanceService` (artifact-read + path-resolve delegates) — `Spike001KbValueAbHarness` and `ContentKbRelevanceServiceTests` depend on it; the ctor signature must not change.

### Claude's Discretion (calibrate at plan time)
- Exact relevance-floor threshold value and the other-commander penalty magnitude — calibrate against the Atraxa gold scenario + 30-TAG-AUDIT.md data; justify the constants.
- Source of the known-commander list (static curated vs corpus-derived) — pick the simpler zero-dep option.
- Exact overlap metric (term-set intersection vs weighted) and tokenization — planner/implementer choice, kept in-process, no new deps.
- Exact sanitizer regex set — must cover the pattern families above; extendable.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Spike evidence (the WHY)
- `.planning/spikes/001-kb-value-ab/VERDICT.md` — the two defects + the Kaalia/Animar failure, with the gold-run selection.
- `.planning/spikes/001-kb-value-ab/selected-clips-real.txt` — the actual bad selection to regress against.

### Code under change
- `DeckFlow.Web/Services/ContentKbRelevanceService.cs` — `SelectTopClips` (diversity cap), `ScoreArtifact` / row scoring (content-overlap + demotion + floor), `GetRelevantClipsAsync`/`GetMergedClipsAsync`, internal test seam (preserve).
- `DeckFlow.Web/Services/ContentKbClipParser.cs` — clip parse + `BuildDeepLink`.
- `DeckFlow.Web/Services/PromptBuilders/Analysis/*AnalysisPromptVariant.cs` — where the `## Expert Context` block renders (fence + preamble live here).

### Research
- `.planning/research/SUMMARY.md`, `ARCHITECTURE.md`, `PITFALLS.md` (P1-P3 retrieval, P7 injection), `STACK.md` (zero-dep verdict).

### Tests / harness
- `DeckFlow.Web.Tests/Spike001KbValueAbHarness.cs` — the Phase 35 re-validation gate; re-run after the fix.
- `DeckFlow.Web.Tests/ContentKbRelevanceServiceTests.cs` — existing scorer tests (extend).

</canonical_refs>

<specifics>
## Specific Ideas

- Deck-profile terms for the Atraxa gold case: `ramp, control, value-engine, midrange, proliferate, counters, removal, protection` — the fixed scorer must keep ramp/focus/removal general-advice videos and starve the Kaalia/Animar clips.
- Expected post-fix behavior on the Atraxa gold scenario: Expert Context spans ≥2 videos, contains zero clips about non-Atraxa commanders, and prefers general-advice videos whose archetype matches the deck (e.g. "Too Much Ramp").

</specifics>

<deferred>
## Deferred Ideas

- Embedding / vector / BM25 retrieval — deferred until corpus >~500 videos (currently ~82); zero-dep algorithmic fix is sufficient now.
- Per-clip (vs per-video) scoring granularity — explicitly NOT chosen for Phase 34; revisit only if the Phase 35 gold re-run still leaks noise.
- Per-deck targeted retrieval / user-supplied sources — only if the Phase 35 gate fails and KBV-04 pivot selects it.

</deferred>

---

*Phase: 34-kb-retrieval-fix*
*Context gathered: 2026-06-10 via discuss-phase*
