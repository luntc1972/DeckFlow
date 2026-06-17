# Phase 35: Value Re-Validation Gate - Context

**Gathered:** 2026-06-10
**Status:** Ready for planning
**Source:** discuss-phase (interactive)

<domain>
## Phase Boundary

Re-run the Spike 001 KB-value A/B against the **fixed** retriever (Phase 34) and record a binary VALIDATED / MARGINAL verdict. This is the gate that decides the rest of v1.6: VALIDATED unlocks Phase 36 (Philosophy-Profile + KB un-dark); MARGINAL skips Phase 36 (KB stays dark) and records a pivot decision, leaving only Phase 37 (SRP split).

Scope = extend `Spike001KbValueAbHarness` to multiple decks, generate baseline-vs-with-context prompts per deck via the REAL fixed `ContentKbRelevanceService`, judge them on the rubric, record the verdict. REQ-IDs: KBV-01..04.

NOT in scope: building the philosophy-profile (Phase 36), un-darking the flag (Phase 36), changing the retriever (Phase 34 is done — if the gate fails, the fix-again decision is recorded, not executed here), the SRP split (Phase 37).
</domain>

<decisions>
## Implementation Decisions

### KBV-01 — Deck set (5 decks, bracket-spanning)
- **5 decks total**, deliberately spanning BRACKETS and ARCHETYPES — bracket is an explicit test dimension (the scorer uses `BracketWeight` and clips carry bracket tags; testing only Bracket 3 would not exercise bracket-aware relevance). Each deck must be a real ~99-card Commander list with real Scryfall card data (reuse the `fetch-deck-cards.py` pattern from the spike to bake oracle text into the harness, OR a per-deck fixture), run through the real `ContentKbRelevanceService.GetRelevantClipsAsync` over the rebuilt corpus.
- **Deck 1 (fixed):** Atraxa, Praetors' Voice — GWUB proliferate/superfriends goodstuff — **Bracket 3 Upgraded** (the existing gold deck; reuse).
- **Decks 2-5 (new):** chosen to cover distinct corpus archetypes AND different brackets. Target coverage (concrete commanders chosen at plan/impl time, but must satisfy these slots):
  - an **aggro/voltron** deck at **Bracket 4 Optimized** (corpus has "Aggro Ideology / Spee-DH", voltron content),
  - a **combo** deck at **Bracket 5 cEDH** (corpus has "EDH's Combo Conundrum", "Does Power Level Matter"),
  - a **control/stax** deck at **Bracket 2 Core or 3 Upgraded** (corpus has control/removal/interaction content),
  - a **lands/ramp** deck at **Bracket 2 Core** (corpus has "Too Much Ramp", "More Lands", land content).
- Brackets across the 5 should span at least Core(2) / Upgraded(3) / Optimized(4) / cEDH(5).
- For each deck, capture the deck's archetype profile terms (as done for Atraxa) so retrieval is scored consistently.

### KBV-02 — Judging protocol (Claude proxy, isolated passes)
- **Claude (the analyst-target AI) answers each prompt**, in ISOLATED passes: for each deck, answer the baseline prompt fully first, then the with-context prompt, scoring each against the rubric.
- **Non-blind caveat is documented** (Claude can see which prompt carries the Expert Context block) — this is the accepted PITFALLS P10 limitation; mitigate by answering each on its own merits and recording the caveat in the verdict. The user may optionally spot-confirm one deck in real ChatGPT, but that is not required for the gate.
- Judge the AI **answers**, NOT the prompts (PITFALLS P11) — score the quality of the analysis each prompt produces.

### KBV-03 — Rubric + recorded verdict
- Reuse the existing 4-dimension rubric (1-5): **Specificity, Creator-voice, Novel signal, Actionability**, plus a quality-regression check.
- Record **per-deck rubric scores** (baseline vs with-context) + a one-line per-deck observation in a gate verdict doc (`35-GATE-VERDICT.md` in the phase dir; cross-link from the spike `VERDICT.md`). Keep the spike's original Run-1/Run-2 evidence intact (frozen "before"); the gate is the "after".
- **Deeper diagnosis (REQUIRED, beyond the scores):** the verdict must include a `## Deeper Diagnosis` section — per-deck ROOT-CAUSE (why with-context lifted or not, traced to specific injected clips; whether retrieval surfaced the right videos for the deck's archetype AND bracket; what the Phase-34 fix still did not solve) + a cross-deck FAILURE-MODE synthesis (the dominant pattern, whether lift correlates with bracket/archetype, which corpus/retrieval limitation bounds the value) — and a `## What This Implies` section translating that root-cause into the next move (VALIDATED → what Phase 36 should lean into; MARGINAL → which pivot the evidence points to). The gate is not a bare pass/fail: it must produce an actionable, evidence-cited diagnosis that informs the routing/pivot decision either way.

### KBV-04 — Decision rule + routing (binary)
- **VALIDATED** iff with-context shows clear lift on **≥2 rubric dimensions in ≥3 of the 5 decks**, with **no quality regression** on any deck. Otherwise **MARGINAL**.
- **VALIDATED →** record "proceed to Phase 36"; the milestone continues to Philosophy-Profile + KB un-dark.
- **MARGINAL →** record an explicit pivot decision (fix-again / per-deck targeted retrieval / retire the clip feature), KB stays dark, Phase 36 is skipped, milestone continues to Phase 37 only.
- The verdict + routing decision are written into `35-GATE-VERDICT.md` and reflected in STATE/ROADMAP.

### Executor assignment (no Codex this week)
- **Codex is unavailable (user weekly limit).** Per CLAUDE.md, this is the Codex-unavailable exception: **Claude writes the harness-extension code** (the KBV-01 task) and performs the judgment (KBV-02/03). No Codex dispatch in Phase 35. Plan frontmatter/tasks must reflect Claude as executor for the code task; do NOT route to Codex.

### Claude's Discretion (decide at plan/impl time)
- Concrete commander + decklist choice for decks 2-5 (must satisfy the bracket/archetype slots above) — prefer well-known real lists so Scryfall lookups resolve.
- Whether to bake all 5 decks into one `[Fact]` emitting 10 files (5×baseline + 5×with-context) or one Fact per deck — implementer choice; keep deterministic + offline-corpus.
- Whether to extend the spike `gen-artifacts.py` corpus rebuild as-is (it already reconstructs the 82-row corpus) — reuse it.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### The gate mechanism (reuse, don't rebuild)
- `DeckFlow.Web.Tests/Spike001KbValueAbHarness.cs` — `EmitRealRetrievalPrompt` (real-scorer path) is the template to extend to 5 decks; `EmitAbPrompts` (hand-picked) stays.
- `.planning/spikes/001-kb-value-ab/gen-artifacts.py` — rebuilds the gitignored corpus (artifacts/spike-rows.json + content-kb/*.md) from `artifacts/uat-content-kb.db` + `artifacts/content-site-index.db`. MUST run before the harness Fact.
- `.planning/spikes/001-kb-value-ab/fetch-deck-cards.py` — pattern for pulling real Scryfall oracle data for a decklist into baked C# `ScryfallCard` initializers.
- `.planning/spikes/001-kb-value-ab/README.md` (rubric + reproduce steps), `VERDICT.md` (Run-1/Run-2 frozen evidence + the rubric).

### The fixed retriever under test (Phase 34 — do not change)
- `DeckFlow.Web/Services/ContentKbRelevanceService.cs` — fixed scorer (content-overlap 0.45, OtherCommanderPenalty 0.9, floor 2.0, per-video cap 1).
- `.planning/phases/34-kb-retrieval-fix/34-VERIFICATION.md` — what Phase 34 proved.

### Project + research
- `.planning/PROJECT.md`, `.planning/REQUIREMENTS.md` (KBV-01..04), `.planning/research/PITFALLS.md` (P10 non-blind, P11 judge-the-answer, P12 single-deck overfit), `./CLAUDE.md`.
</canonical_refs>

<specifics>
## Specific Ideas

- Atraxa expected post-fix behavior (already verified by Phase 34 tests): Expert Context spans ≥2 videos, zero non-Atraxa-commander clips, prefers archetype-matched general-advice videos. The gate must confirm this lifts the ANSWER, not just the selection.
- The honest expectation worth stating in the verdict: even with fixed retrieval, the corpus is generic deckbuilding-philosophy content — lift may still be modest. The gate decides on evidence, not hope.
</specifics>

<deferred>
## Deferred Ideas

- Embeddings/vector retrieval, per-deck targeted retrieval, philosophy-profile — all Phase 36+ or out of scope; Phase 35 only judges the current fixed retriever.
- Automated/LLM-as-judge scoring harness — out of scope; manual Claude-proxy rubric scoring is the Phase 35 protocol.
- Expanding/re-harvesting the corpus — out of scope; judge the existing ~82-row corpus.
</deferred>

---

*Phase: 35-value-re-validation-gate*
*Context gathered: 2026-06-10 via discuss-phase*
