---
slug: setupgrade-remove-text-block
status: complete
date: 2026-07-15
commit: b5284f59
---

# Quick Task: Remove discussion_summary.txt text block from set-upgrade prompt

**Ask:** The deck-analysis Step 4 (set-upgrade) prompt instructed the AI to
create a second fenced text block named "discussion_summary.txt" alongside the
set_upgrade_report JSON. Remove the instructions for that text output.

## What changed
- **ChatGptSetUpgradePromptVariant.cs** — removed step D (the discussion_summary.txt block).
- **GeminiSetUpgradePromptVariant.cs** — removed step D; kept step C JSON + ResultWrapInstruction.
- **ClaudeSetUpgradePromptVariant.cs** — removed the "discussion_summary.txt-style notes" bullet.
- **DeckAnalysisPacketServiceTests.cs** — flipped 5 stale Contains asserts to
  DoesNotContain (discussion_summary, fenced-text tag, "per-set analysis in condensed form")
  to regression-lock the removal.
- **DeckAnalysis.cshtml** — updated Step 4 instruction + Step 5 paste hint that named the removed block.
- **README.md** — Release Notes (Unreleased) bullet.

Kept: the human-readable per-set analysis and the set_upgrade_report JSON.
The removed text file duplicated the readable analysis and was never parsed by the app.

## Verification
- dotnet build DeckFlow.Web.Tests — 0 errors (1 pre-existing MetaGap warning, unrelated).
- Targeted tests: 244 passed (touched class + all prompt-variant / invariant / golden / execute-now / set-upgrade suites).
- Format gate (format-check-changed.sh staged) — exit 0.
- EOL: all touched files LF, no churn (git diff --stat == --ignore-all-space --stat).

## Delegation
Code authored by Codex (gpt-5.4 medium); Claude planned, reviewed, tested, wrote README/SUMMARY.

## Owed
- User pushes quick/setupgrade-remove-text-block (ff to main) -> autodeploy.
