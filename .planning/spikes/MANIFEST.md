# Spike Manifest

## Idea
Validate whether the Content KB actually improves ChatGPT's deck analysis before investing in the
creator philosophy-profile redesign (see `.planning/seeds/creator-philosophy-profile.md`). The KB is
still OFF in prod (`content.kb.enabled`) and unproven. Prove the lift with an A/B: the SAME deck's
analysis prompt built WITH vs WITHOUT expert-context creator clips, pasted into ChatGPT, judged blind.

## Requirements
- A/B must use the REAL prompt builder (`DeckAnalysisPacketService.BuildAsync`), not a hand-built
  approximation — the test is only valid if it exercises the prompt the app actually ships.
- "With" variant grounds on REAL creator clips from the KB (salubrious-snail, in `artifacts/uat-content-kb.db`).
- Value judgment is manual (ChatGPT is external); the spike's job is to emit two pasteable prompts +
  a scoring rubric, not to auto-decide.

## Spikes

| # | Name | Type | Validates | Verdict | Tags |
|---|------|------|-----------|---------|------|
| 001 | kb-value-ab | standard | Given a sample deck + real snail clips, when BuildAsync runs with vs without expert-context, then two pasteable prompts differ only in the expert block — and the with-context ChatGPT answer shows creator-signal the baseline lacks | PARTIAL (prompts emitted; awaiting manual A/B) | content-kb, eval, prompt |
