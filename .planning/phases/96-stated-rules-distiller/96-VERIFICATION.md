# Phase 96: Stated-Rules Distiller — Verification

**Verified:** 2026-07-12 (manual phase-gate verification by orchestrator; Codex executed all 8 plans, Claude reviewed + verified each)
**Status:** PASSED — substrate complete
**Delivery:** 8 plans, 5 waves, commits `92af6929` → `de0ca49b`

## Execution trace (Codex gpt-5.4, one commit per plan)
| Plan | Wave | Commit | Build | Tests |
|------|------|--------|-------|-------|
| 96-01 candidate DTO + vocabulary + reducer | 1 | 92af6929 | Core clean | 6 green |
| 96-02 chunker + content_type + grounder seam | 1 | 0a2a9dbf | Core clean | 9 green |
| 96-03 schemas + prompts + validation contract | 2 | dfc61786 | Core clean | 13 green (existing fixtures unchanged) |
| 96-05 content_stated_rules store + frontmatter | 2 | 7c89da21 | sln clean | 24 green (1 PG-gated skip) |
| 96-06 Web Scryfall grounder | 2 | 4e05a54f | Web clean | 4 green |
| 96-04 CLI Claimify stage methods | 3 | 4f0401ad | Core+Web clean | 29 green |
| 96-07 extractor + Snail golden | 4 | fa89956f | Core clean | 7 green |
| 96-08 orchestrator wiring | 5 | de0ca49b | sln clean | Core suite 1305 pass / 14 skip |

Full DeckFlow.Web.Tests: 1289 pass / 14 skip / **1 pre-existing flake** (`DeckAnalysisDownload_...MatchesBaseline` — ZIP DOS-mod-time byte, passes in isolation ×2; not in the Phase-96 code path).

## Success criteria (ROADMAP)
1. **stated_rules: YAML block + content_type: frontmatter, single re-distill populates both** — PASS. `ContentArtifactWriter.ToText` emits both additively (byte-stable gate held: pre-existing lines/body unchanged); `ContentKbOrchestrator.DistillVideoAsync` computes+persists them in one pass (96-05/96-08). Orchestrator test asserts both sections emitted + rows inserted.
2. **Map-reduce chunking + Claimify Select→Disambiguate→Decompose yields measurable rules, discards ambiguous** — PASS. `TranscriptChunker` (96-02), 4 CLI stages (96-04), `StatedRulesExtractor` 6-step sequence (96-07); golden proves ambiguity dropped at Disambiguate.
3. **Every rule validates against strict JSON schema + carries sourceClip, confidence, video date** — PASS. `DistillationSchemas` 4 constrained schemas + `ValidateStatedRules` allowlist/band/fail-closed-provenance (96-03); `video_date_utc` persisted queryably (HIGH-1); snake_case artifact contract locked (HIGH-3).
4. **Minimal Scryfall card-name grounding flags/rejects unrecognized names** — PASS. `ScryfallCardNameGrounder` fuzzy-correct-then-flag over the throttled resolver, cached, keep+flag on failure (96-06); wired structurally by CardReference in the extractor (96-07).
5. **Golden regression on new schema passes using UTF-8-safe harness** — PASS. `CliLlmDistillationStatedRulesGoldenTests` drives the full pipeline over a real Snail transcript fixture via canned CLI responses through the existing `BuildStartInfo` UTF-8 harness (96-07).

## Review notes (carried forward, non-blocking)
- **96-08 silent swallow:** the `catch (NotSupportedException)` around extraction degrades off-subscription fake distillers to empty rules. Correct + dead-in-production (subscription gate), but silent — a `_logger.LogDebug` in the catch would improve observability. Follow-up nit, not a bug.
- **CI-2 (see 97-CONTEXT):** the golden proves plumbing via canned responses; the REAL Select/Disambiguate/Decompose prompts are unexercised until an operator runs one live Snail re-distill (D-05-DEP). Do this before P97 locks fusion thresholds.
- **CI-1 (see 97-CONTEXT):** P94 `StatedRule`/`FusedTarget` records are too narrow for the band/condition/provenance P96 produces; P97 must extend `FusedTarget` additively and parse the P96 `stated_rules:` artifact contract as its stated input.

## Deferred (by design, D-05)
No mass backfill of the ~106 existing artifacts. The re-distill mechanism ships and is test-exercised; executing it corpus-wide is an operator action. P97 needs at minimum a Snail re-distill first (D-05-DEP).
