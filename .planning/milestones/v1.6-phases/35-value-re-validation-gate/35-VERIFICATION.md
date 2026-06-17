---
phase: 35-value-re-validation-gate
status: passed
date: 2026-06-10
---

# Phase 35 Verification — Value Re-Validation Gate

**Phase goal:** Record a blind, multi-deck A/B verdict against the fixed retriever that routes the milestone.

The phase goal was to *produce a trustworthy verdict + routing* — NOT to make the KB pass. A MARGINAL outcome with a sound, evidence-backed verdict fully satisfies the phase. The phase passes; the KB feature did not.

| SC / REQ | Evidence | Status |
|---|---|---|
| KBV-01 — fixed retriever, ≥3 bracket-spanning decks, baseline+with-context per deck | 35-01: `EmitRealRetrievalPromptAllDecks` emitted 10 deck files (5 decks, brackets 2/3/4/5) via the real `ContentKbRelevanceService` over the rebuilt 82-row corpus; force-regen verify proved the two-connection corpus rebuild. | PASS |
| KBV-02 — score AI answers, isolated passes, blind protocol documented | 35-02: 5 isolated-pass judgments (baseline-first), non-blind caveat documented, answers judged not prompts. | PASS |
| KBV-03 — per-deck rubric scores + single binary outcome recorded | `35-GATE-VERDICT.md`: 5-deck rubric table + roll-up + binary MARGINAL + Deeper Diagnosis + What This Implies. | PASS |
| KBV-04 — outcome routes milestone explicitly | Gate = MARGINAL → Phase 36 SKIPPED, pivot = retire (user-ratified); recorded in STATE.md + ROADMAP.md. | PASS |
| SC: judged the answers; blind protocol; not single-deck | 5 decks, 4 brackets; P10/P11/P12 explicitly guarded. | PASS |

**Outcome:** Gate verdict = **MARGINAL**. Retriever/injection fixes from Phase 34 confirmed working; KB clip-injection value remains cosmetic and corpus-bound. Phase 36 gated off; retire pivot recorded. Phase 37 (SRP split) is the remaining v1.6 work.

**Build/test:** 35-01 harness builds clean; `EmitRealRetrievalPromptAllDecks` + the legacy Facts pass; gen-artifacts two-connection rebuild verified (force-regen non-empty). No production code changed; `content.kb.enabled` not flipped.
