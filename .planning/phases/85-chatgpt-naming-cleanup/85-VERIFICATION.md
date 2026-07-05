---
phase: 85
slug: chatgpt-naming-cleanup
status: passed
verified: 2026-07-05
verifier: orchestrator (execute-phase driven manually; 85-05 plan IS the phase acceptance gate)
requirements-verified: [AICLEAN-01, AICLEAN-02, AICLEAN-03]
---

# Phase 85 — Verification (chatgpt-* → prompt-* Naming Cleanup)

**Status: PASSED.** Retroactively captured — Phase 85 was executed via a manually-driven execute-phase; its
plan **85-05 was itself the phase acceptance gate** (byte-identical proof + full-suite gates + human
sign-off), which is stronger than a generic post-hoc verifier. This file records that evidence as the
phase VERIFICATION artifact (was missing from the milestone audit).

## Requirements coverage

| Requirement | Description | Status | Evidence |
|-------------|-------------|--------|----------|
| AICLEAN-01 | All `chatgpt-*` CSS class names renamed to AI-agnostic names across 25 theme forks + site-common + site.css, rendered output byte-identical | **satisfied** | 85-02 (25 CSS files, 39 stems + 4 attr selectors); 85-05 token-normalized render snapshot diffs byte-identical vs pre-85 baseline |
| AICLEAN-02 | TS symbols/selectors + behavior-critical surface renamed; D5 cache-key contract lockstep client+server | **satisfied** | 85-03 (deck-sync/bridge/busy-indicator + 4 views + tests); D5 lockstep verified (5 cache-key literals identical both sides) |
| AICLEAN-03 | Remaining generic chatgpt/ChatGpt identifiers renamed (ChatGptSwapPrompt→PromptSwapPrompt) + full e2e gate | **satisfied** | 85-04 (C# symbol + Manabase + views); 85-05 full Playwright e2e 256 pass / 0 fail |

## Acceptance evidence (85-05)
- **Byte-identical render**: post-rename snapshot matches pre-85 baseline across all 24 (route × theme) cells
  after normalizing 3 legit non-rename diffs (CSRF token, `?v=` content-hash cache-bust, async set-catalog).
- **Grep-clean gates** a–d: PASS (zero strict `chatgpt-` in css/ts/Views; 12 edited .cs files clean; keep-list
  untouched; no old cache-key literal survives).
- **Full xUnit**: 2603 pass / 12 skip (Postgres) / 0 fail. **Build**: 0 Warning(s) / 0 Error(s).
- **Full Playwright e2e**: 256 pass / 14 skip / 0 fail (headless, no Windows browser).
- **D3 keep-list intact** (human-confirmed): model-trio `*PromptVariant.cs`, `AiPlatform.ChatGpt`,
  `*-chatgpt-prompt.txt`, page titles/ledes/"ChatGPT-ready" copy all preserved. Ratified copy change: 4
  validation strings "ChatGPT"→"your AI" (matches site convention).
- **D5 contract lockstep** (human-confirmed): client + server cache-key values consistent.
- **Human sign-off**: recorded in 85-05-SUMMARY (final sign-off commit `10bab864`).

## Gaps
None. Requirements fully satisfied with proof.

## Notes
- Pre-existing theme UI bugs (step-tab active state, layout picker, checklist pill) surfaced during
  post-build dogfooding were proven NOT caused by this rename (phase diff token-only/byte-identical) and are
  handled separately in Phase 86 (UIAUDIT-02). See 85-05-SUMMARY.
