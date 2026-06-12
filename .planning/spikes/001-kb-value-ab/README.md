# Spike 001 — kb-value-ab

**Question:** Does injecting expert-context creator clips into the deck-analysis prompt produce a
meaningfully better/different ChatGPT answer than the baseline prompt? (The gate before building the
creator philosophy-profile redesign.)

**Verdict:** MARGINAL → leaning NEGATIVE (2026-06-10). Two runs on a real ~99-card Atraxa deck:
(1) hand-picked clips = marginal lift; (2) **real `ContentKbRelevanceService` scorer = WORSE** —
selected 5 clips from one tangential video, 3 about unrelated commanders, ignored the on-point
ramp/focus videos. Gate NOT cleared. Do NOT green-light Content KB v2 / philosophy-profile without
fixing retrieval first. Full reasoning + rubric + mechanism defects in `VERDICT.md`.
(Earlier "PARTIAL" was a 3-card stub, superseded.)

## Given / When / Then
- **Given** a sample Commander deck + real salubrious-snail clips from the KB,
- **When** `DeckAnalysisPacketService.BuildAsync` builds the analysis prompt with expert-context ON vs OFF,
- **Then** we get two pasteable prompts differing only in the expert-context block — and the
  with-context ChatGPT answer surfaces creator-voice/specific signal the baseline lacks.

## Research (seam map, from read-only investigation)
- Entry: `DeckFlow.Web/Services/DeckAnalysisPacketService.cs:391` — `BuildAsync(DeckAnalysisRequest, ct)`.
- Expert-context fetch: `DeckAnalysisPacketService.cs:699` via `IContentKbRelevanceService` (nullable, optional DI).
- Toggle: pass a relevance service returning clips (ON) vs returning null / inject null (OFF).
  Prod gate is `content.kb.enabled` (`ContentKbRelevanceService.cs:190`).
- Render: `PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs:251` — expert block appended only when `kbExcerpts.Count > 0`.
- **Cheapest seam:** `DeckFlow.Web.Tests/DeckAnalysisPacketServiceExpertContextTests.cs:117` `CreateService(...)`
  already builds the real service with HTTP fakes + REAL prompt registries + an injectable
  `FakeContentKbRelevanceService`. Copy this; feed real snail clips for ON, null for OFF.

## Approach
Throwaway xUnit harness (copy of the existing test pattern): one sample deck, BuildAsync twice
(real snail clips vs null), write both analysis prompts to `with-context.txt` / `baseline.txt` here.
Manual A/B: paste both into ChatGPT, score with the rubric below (blind if possible).

## Scoring rubric (manual)
For each variant's ChatGPT answer, rate 1-5:
1. **Specificity** — concrete, deck-specific advice vs generic.
2. **Creator-voice** — does it reflect snail's heuristics (focus, protect win-cons, anti-glass-cannon)?
3. **Novel signal** — anything the baseline answer lacked.
4. **Actionability** — clear cuts/adds.
Verdict: clear lift on 2-3 + no quality loss → VALIDATED (green-light profile build). Marginal → reconsider whole KB.

## Results
- Harness: `DeckFlow.Web.Tests/Spike001KbValueAbHarness.cs` (throwaway; copies the real
  `CreateService` test pattern, drives `DeckAnalysisPacketService.BuildAsync` twice). Build+test pass.
- Emitted: `with-context.txt` (10,430 B) and `baseline.txt` (9,209 B). Only delta = the
  `## Expert Context` block (4 real snail clips, cited, with the injection-defense preamble).
- Two runs judged 2026-06-10 (real ~99-card Atraxa deck): Run 1 hand-picked clips, Run 2 the REAL
  scorer. See `VERDICT.md`. Verdict MARGINAL → leaning NEGATIVE; gate NOT cleared.
- **Harness is NOW the v1.6 re-validation gate** — keep it (was a throwaway; promoted). Re-run after
  the retrieval fix and compare against `with-context-real.txt`.

## Reproducing (v1.6 gate)
The corpus inputs live under gitignored `artifacts/` (`spike-rows.json` + regenerated `content-kb/`
markdown), so they must be rebuilt before running the gold Fact:
1. `python3 .planning/spikes/001-kb-value-ab/gen-artifacts.py` — rebuilds artifact `.md` from
   `artifacts/uat-content-kb.db` + visibility from `artifacts/content-site-index.db`, writes
   `artifacts/spike-rows.json`.
2. `dotnet test DeckFlow.Web.Tests --filter Spike001KbValueAbHarness` — `EmitAbPrompts` (hand-picked)
   + `EmitRealRetrievalPrompt` (real scorer). `fetch-deck-cards.py` documents how the baked-in deck
   card data was pulled from Scryfall.

