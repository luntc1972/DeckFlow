---
phase: 99
slug: creator-style-artifact-engine
status: verified
threats_open: 0
asvs_level: 1
created: 2026-07-19
---

# Phase 99 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| user-submitted deck text → IDeckEntryLoader | Untrusted paste/URL parsed into DeckEntry[]; reuses the hardened loader path shared by every packet tool | Untrusted free text |
| user-submitted request strings → assembled artifact | CreatorStyleRequest fields (creator slug, deck source) echoed into or driving the paste-ready artifact | Untrusted free text |
| creator-sourced content (cached decks, whitelist) → artifact text | Exemplar/whitelist card names from the cached Archidekt crawl flow into prompt text a user pastes into an LLM | Creator-controlled corpus |
| DeckFlow.Web → Scryfall / Commander Spellbook (HTTP) | Card resolution, combo lookup, and card-grounding validation over existing resilience-piped clients | Card names / deck lists |
| DeckFlow.Web → local category-knowledge DB | Card-category reads via CategoryKnowledgeRepository | Card names |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-99-01-01 | Tampering | CreatorStyleRubricScorer metric-key join | mitigate | Scorer keys exclusively off `StatedMetricKeyMapper.TryMapToMeasuredKey`; unknown/malformed metric ⇒ `insufficient-measured` row, never a fabricated delta. Enforced by Pitfall-1 regression test (`Score_FullyMappedFixtureProfile_ProducesNoInsufficientMeasuredRows` + stated-only/missing-stat cases). | closed |
| T-99-01-02 | DoS | Scorer/selector over large inputs | accept | Inputs bounded upstream (fixed per-creator FusedTarget[]; capped cached crawl corpus); pure LINQ, no recursion. | closed |
| T-99-01-SC | Tampering | package installs | accept | No packages added in 99-01. | closed |
| T-99-02-01 | Tampering | submitted deck names → DeckCardNames/stats | mitigate | Every card name normalized via `CardNormalizer.Normalize` before entering the deck-context set (`SubmittedDeckStatsBuilder` deck-context construction); builder emits stats/keys only, never raw strings into prompt text. | closed |
| T-99-02-02 | DoS | huge submitted deck → unbounded Scryfall/combo calls | mitigate | Resolution chunked at 75-card `cards/collection` batches with distinct-identifier dedup (`ScryfallBatchSize`, `ResolveCardsAsync`); one Spellbook call per build (WR-03 fix removed the duplicate). | closed |
| T-99-02-03 | Info. Disclosure | numeric drift producing wrong deltas | mitigate | `ManabaseMode.Casual` + `isSingleton:true` pinned to the fused-profile path with `// Why:` note; karsten parity test asserts equality with `ManabaseAnalyzer.Analyze` output. Strengthened by CR-02 fix: unresolvable deck omits karsten keys (⇒ `insufficient-measured`) and sets `DeckResolutionDegraded` instead of emitting fabricated zeros. | closed |
| T-99-02-SC | Tampering | package installs | accept | No packages added in 99-02. | closed |
| T-99-03-01 | Spoofing | hallucinated/illegal/stale card name in artifact | mitigate | Whitelist pre-validated by `CreatorWhitelistPoolBuilder`'s internal guard batch; exemplar/combo names pass exactly ONE additional `ICardGroundingGuard.ValidateAllAsync` (distinct union minus whitelist). Only `Accepted` canonical names reach result/text; `CreatorStyleExemplarDeck` DTO exposes accepted names only (raw `Entries` never rendered — grep-gated). Verdict-count mismatch throws (WR-02) — no silent truncation. | closed |
| T-99-03-02 | Tampering | prompt-injection via crafted free-text | mitigate | `SanitizeUserText`: length-cap const + newline/tab collapse before interpolation; card names are guard-canonicalized; `CreatorStyleRequest` treated as unsanitized. Covered by free-text-cap test. | closed |
| T-99-03-03 | Info. Disclosure | silently shipping a shrunken card set | mitigate | `GroundingDegraded` OR-ed from {additional-batch upstream failure, any exclusion, whitelist diagnostics `HasUpstreamFailure` (`BuildWithDiagnosticsAsync`), deck-resolution degradation (CR-02)}; visible "Grounding caveat" line in artifact + Notice. Exclusion-only leg covered by IN-10 test. | closed |
| T-99-03-04 | DoS | large card-name union → guard batch cost | mitigate | Whitelist capped 25 (pre-validated); exemplars capped 3 decks; additional batch deduplicated and whitelist-subtracted before the single call; 75-card chunking inside the guard. No per-card HTTP loop (`TryValidateAsync` grep == 0). | closed |
| T-99-03-SC | Tampering | package installs | accept | No packages added in 99-03. | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-99-01 | T-99-01-02 | Pure in-process computation over upstream-bounded inputs; no allocation amplification. | Plan 99-01 threat model (converged SHIP) | 2026-07-18 |
| AR-99-02 | T-99-01-SC, T-99-02-SC, T-99-03-SC | Phase introduces zero new NuGet/npm packages; no supply-chain surface. | Plan threat models (converged SHIP) | 2026-07-18 |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-07-19 | 12 | 12 | 0 | Claude orchestrator (plan-time register; mitigations verified via code review 99-REVIEW.md + fix commit 927f2c2a + gsd-verifier 9/9) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter
