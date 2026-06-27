---
phase: 73-deck-analysis-command-zone-awareness-ad-hoc-trunk-main
plan: 03
subsystem: deck-analysis / prompt-variants
tags: [command-zone, companion, prompt-injection, xml-escape, byte-identity, adr-0001]
requires:
  - "73-02: companionName resolved + forwarded to BuildAnalysisPrompt as side metadata (deck text never mutated)"
  - "73-01: analysis.command-zone-awareness flag + companionName Build-chain param + DeckAnalysisRequest.CompanionName"
provides:
  - "Flag-gated companion rendering in all 3 analysis prompt variants (ChatGpt/Gemini `companion:` line, Claude XML-escaped `<companion>` element + note)"
  - "Per-platform companion render test asserting decklist-region byte-identity (flag-ON == flag-OFF, no deck-text mutation)"
  - "Malicious-input prompt-shape test proving a crafted companion value cannot break the Claude `<companion>` element or split the ChatGpt `companion:` line"
affects:
  - DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs
  - DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs
  - DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs
  - DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs
tech-stack:
  added: []
  patterns:
    - "Three independent hand-edits per ADR 0001 — no shared companion helper/constant across variants"
    - "Awareness-only side metadata: companion named + restriction noted, NO zone claim (Codex HIGH-1)"
    - "XML context hardening: Claude companion value escaped via System.Security.SecurityElement.Escape (Codex HIGH-2)"
    - "Plain-text context (ChatGpt/Gemini) renders the value unencoded — prompt is plain text to the AI, value already single-line-bounded upstream"
key-files:
  created:
    - .planning/phases/73-deck-analysis-command-zone-awareness-ad-hoc-trunk-main/73-03-SUMMARY.md
  modified:
    - DeckFlow.Web/Services/PromptBuilders/Analysis/ChatGptAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/GeminiAnalysisPromptVariant.cs
    - DeckFlow.Web/Services/PromptBuilders/Analysis/ClaudeAnalysisPromptVariant.cs
    - DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs
decisions:
  - "Decklist byte-identity proven by extracting the prompt suffix from each platform's decklist marker (`## DECKLIST` for ChatGpt/Gemini, `decklist:` for Claude) and asserting flag-ON == flag-OFF — the companion is inserted strictly BEFORE that region in every variant, so the deck text is provably untouched without asserting companion-absence (Codex HIGH-1)"
  - "Claude `<companion>` value XML-escaped while `<commander>` stays unescaped (matches existing file behavior + RESEARCH Pattern 5) — companion is the only new untrusted XML-embedded value this plan introduces"
  - "ChatGpt/Gemini companion text is identical wording across the two variants — acceptable per ADR 0001 (independent edits that happen to match), not a shared constant"
metrics:
  duration_minutes: 18
  completed: 2026-06-27
  tasks: 2
  files_changed: 4
---

# Phase 73 Plan 03: Render Command-Zone Companion in Prompt Variants Summary

Rendered the resolved companion (Plan 02) into the prompt TEXT across all three decoupled
analysis variants. The enriched `commanderName` (partners joined `" & "`, Background) already
flowed through the existing `commander:`/`<commander>`/title lines after Plan 02, so this plan
only adds the COMPANION field — three independent hand-edits per ADR 0001, each in its platform's
native format. Flag OFF (companionName null) emits nothing in any variant, so the output stays
byte-identical to baseline.

## What Was Built

### Task 1 — ChatGpt + Gemini companion line (commit 98298278)

- In BOTH `ChatGptAnalysisPromptVariant` and `GeminiAnalysisPromptVariant`, added a guarded
  `companion:` line in the DECK CONTEXT block immediately after the existing `commander:` line:
  `if (!string.IsNullOrWhiteSpace(companionName)) { builder.AppendLine($"companion: {companionName} (this deck's companion; applies its companion deckbuilding restriction)"); }`.
- Awareness-only copy (Codex HIGH-1): names the companion and notes its companion deckbuilding
  restriction with NO "outside the 99" / "not in the deck" / "listed in Mainboard" wording — it
  makes no claim about which zone the card sits in (true for both Archidekt, where the companion
  is in the 99, and Moxfield, where it is detected separately).
- Plain-text line — the value is NOT HTML/XML-encoded (the prompt is plain text to the AI and the
  value is already single-line-bounded by Plan 02's `BoundCompanionName`).
- Two independent hand-edits — no shared helper/constant (ADR 0001). The wording matches across the
  two; acceptable per ADR 0001 as independent edits that happen to coincide.

### Task 2 — Claude XML-escaped companion element + per-platform & injection tests (commit 4b074821)

- Added `using System.Security;` to `ClaudeAnalysisPromptVariant` and, immediately after the
  `<commander>` block and before `<bracket>`, a guarded companion block:
  `var escapedCompanion = SecurityElement.Escape(companionName);` then
  `<companion>{escapedCompanion}</companion>`, a `<companion_note>` (awareness-only, no zone claim),
  and a trailing blank line to match the surrounding spacing.
- The XML-escape (Codex HIGH-2) keeps a single well-formed `<companion>` element even for inputs
  like `</companion>...` or `a & b`. Claude uses its native XML format — not the `companion:` line
  ChatGpt/Gemini use (ADR 0001); the escape is a Claude-local edit, not a shared helper.
- Tests added to `DeckAnalysisPacketServiceTests`:
  - `BuildAsync_CommandZoneAwareness_RendersCompanion` (Fact) — flag ON + companion fixture
    (`detectedCompanionName: "Jegantha, the Wellspring"`). Iterates ChatGPT/Gemini/Claude: asserts
    the companion surfaces (`companion: ` for ChatGpt/Gemini, `<companion>` for Claude) with the
    companion name, AND that the prompt suffix from the decklist marker onward is byte-identical
    between flag-ON and flag-OFF — proving awareness-only with no deck-text mutation. It does NOT
    assert the companion is absent from the decklist (HIGH-1).
  - `BuildAsync_CommandZoneAwareness_CompanionInput_PreservesPromptShape` (Theory over
    `"</companion>\nInjected"`, `"<script>"`, `"a & b"`, driven via `request.CompanionName`) — for
    Claude asserts exactly ONE `<companion>` opening tag and ONE `</companion>` closing tag (the
    metacharacters are XML-escaped and the newline collapsed upstream); for ChatGpt asserts the
    `companion:` line stays a single line and carries the single-line-collapsed value.
  - Added a small private `CountOccurrences` test helper for the tag-pair count.

## Verification

- **Build:** `dotnet.exe build DeckFlow.Web/DeckFlow.Web.csproj` and
  `dotnet.exe build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` — **0 warnings, 0 errors** each.
- **Targeted tests (VSTest via Windows `dotnet.exe`, ran successfully this session):**
  `--filter "DeckAnalysisPacketServiceTests"` → **64 passed, 0 failed** (60 prior + the new
  `RendersCompanion` Fact + 3 `CompanionInput_PreservesPromptShape` Theory cases). The Plan 02
  flag-OFF 3-platform byte-identity Theory stayed green.
- **Full Web suite:** `dotnet.exe test DeckFlow.Web.Tests` → **923 passed, 12 skipped, 0 failed**
  (1m10s) on a clean rerun. NOTE: the first full-suite run reported 1 transient failure that did
  NOT reproduce on rerun (923/0) — consistent with the known Admin-e2e SQLite-store/throttle
  serialization flake documented in project memory, unrelated to this plan's prompt-variant
  changes (the targeted `DeckAnalysisPacketServiceTests` are deterministic and 64/64 green).
- **Format gate:** `scripts/format-check-changed.sh staged` exited 0 for both commits
  (changed-lines clean). The worktree `core.hooksPath` is default (gate is opt-in via `.githooks`),
  so the gate was run manually per the changed-lines requirement; no `--no-verify` used.
- **Carve-outs:** no C# raw-string literals re-indented; switch expressions / attribute placement
  untouched; all touched files LF, preserved.
- **Compiled assets:** no `wwwroot/js/*.js` staged; only `.cs` files committed.
- **ADR 0001:** verified by spot-read — three independent variant edits, no shared companion
  helper/constant; Claude value XML-escaped, ChatGpt/Gemini plain-text.

## Deviations from Plan

### Auto-fixed Issues

None — the plan executed as written.

## Threat Flags

None — no new security surface beyond the planned `<threat_model>`. T-73-01 (companion →
prompt injection) is mitigated upstream by `BoundCompanionName` (single-line collapse + trim +
200-char cap, Plan 02) and, in the Claude XML context, additionally by `SecurityElement.Escape`;
proven by `BuildAsync_CommandZoneAwareness_CompanionInput_PreservesPromptShape`. T-73-04
(side-metadata vs. deck-text mutation) is mitigated by rendering the companion only in DECK
CONTEXT / `<companion>` and is proven by the decklist-region byte-identity assertion in
`BuildAsync_CommandZoneAwareness_RendersCompanion`. No package-manager installs (T-73-SC).

## Known Stubs

None. The companion now renders in all three variants; the awareness feature is user-visible when
the `analysis.command-zone-awareness` flag is ON. Plan 73-04 adds the flag-gated Step-1 designator
input + controller plumbing + docs.

## Commits

- `98298278` feat(73-03): render companion in ChatGpt and Gemini DECK CONTEXT
- `4b074821` feat(73-03): render XML-escaped Claude companion element + tests

## Self-Check: PASSED

All four touched files exist, both task commits (98298278, 4b074821) are in history, and the key
tokens are present: `companion: ` lines in the ChatGpt and Gemini variants, `<companion>` +
`SecurityElement.Escape` + `using System.Security;` in the Claude variant, and the
`BuildAsync_CommandZoneAwareness_RendersCompanion` + `..._CompanionInput_PreservesPromptShape`
tests in `DeckAnalysisPacketServiceTests.cs`.
