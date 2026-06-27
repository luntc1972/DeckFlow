---
phase: 73
slug: deck-analysis-command-zone-awareness-ad-hoc-trunk-main
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-27
---

# Phase 73 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from 73-RESEARCH.md "Validation Architecture" section.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 |
| **Config file** | `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |
| **Quick run command** | `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "DeckAnalysisPacketServiceTests" -q` |
| **Full suite command** | `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -q` |
| **Estimated runtime** | ~30 seconds (filtered) / ~3 min (full suite) |
| **Current passing** | 875 passing, 12 skipped (verified 2026-06-27) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "DeckAnalysisPacketServiceTests" -q`
- **After every plan wave:** Run `dotnet.exe test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -q`
- **Before `/gsd:verify-work`:** Full suite green (875+ passing) AND `dotnet.exe build` clean (no new warnings) AND format-gate clean on changed lines
- **Max feedback latency:** ~30 seconds (filtered run)

---

## Per-Task Verification Map

| Behavior | Requirement | Test Type | Automated Command | File Exists | Status |
|----------|-------------|-----------|-------------------|-------------|--------|
| Flag OFF byte-identity (all 3 variants) | When `analysis.command-zone-awareness=false`, prompt is byte-identical to current output — `[Theory]` parameterized over `TargetAiPlatform` ChatGPT/Claude/Gemini (MED-2) | unit | `--filter "BuildAsync_IsByteIdentical_WhenCommandZoneAwarenessFlagOff"` | ❌ W0 | ⬜ pending |
| Partner pair rendering | Flag ON + 2 command-zone commanders → both names in `AnalysisPromptText` ("A & B") | unit | `--filter "BuildAsync_CommandZoneAwareness_RendersPartnerPair"` | ❌ W0 | ⬜ pending |
| Companion rendered (side metadata) | Flag ON + companion → `companion:` (ChatGpt/Gemini) / `<companion>` (Claude) present; decklist section byte-identical to flag-OFF (NO deck-text mutation; companion NOT asserted absent) | unit | `--filter "BuildAsync_CommandZoneAwareness_RendersCompanion"` | ❌ W0 | ⬜ pending |
| Companion injection resistance | Flag ON + malicious `CompanionName` (`</companion>\n…`, `<script>`, `a & b`) → Claude keeps exactly one well-formed `<companion>` element; ChatGpt `companion:` stays single-line (HIGH-2) | unit | `--filter "BuildAsync_CommandZoneAwareness_CompanionInput_PreservesPromptShape"` | ❌ W0 | ⬜ pending |
| Single commander no regression | Flag ON + 1 commander → commander name unchanged, no spurious `&` | unit | `--filter "BuildAsync_CommandZoneAwareness_SingleCommanderUnchanged"` | ❌ W0 | ⬜ pending |
| Companion leak guard (existing) | `DetectedCompanionName` on load result does NOT affect deck text when flag OFF | unit | `--filter "BuildAsync_DoesNotLeakCompanionDeckContent"` | ✅ line 851 | ⬜ regress |
| Manabase flag unchanged (existing) | `manabase.commander-castability` still byte-identical for deck-analysis (separate flag used) | unit | `--filter "BuildAsync_IsByteIdentical_WhenCommanderCastabilityFlag"` | ✅ line 879 | ⬜ regress |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `BuildAsync_IsByteIdentical_WhenCommandZoneAwarenessFlagOff` — flag-OFF byte-identity, `[Theory]` parameterized across ChatGPT / Claude / Gemini (`TargetAiPlatform`) so all 3 variants are proven (MED-2)
- [ ] `BuildAsync_CommandZoneAwareness_RendersPartnerPair` — partner enrichment ("A & B")
- [ ] `BuildAsync_CommandZoneAwareness_RendersCompanion` — companion field per platform (ChatGpt/Gemini line, Claude XML) + decklist byte-identical to flag-OFF (no deck-text mutation)
- [ ] `BuildAsync_CommandZoneAwareness_CompanionInput_PreservesPromptShape` — malicious-input prompt-shape (single well-formed `<companion>`; single-line `companion:`)
- [ ] `BuildAsync_CommandZoneAwareness_SingleCommanderUnchanged` — solo-commander regression

*New tests live in `DeckFlow.Web.Tests/DeckAnalysisPacketServiceTests.cs`. Byte-identity tests MUST flatten via `FlattenPacketText` which already excludes non-deterministic `TimingSummary` (research §byte-identity; Phase 72 flaky-test fix precedent). The flag-OFF byte-identity test sets `request.TargetAiPlatform` per `[InlineData]` so ChatGPT, Claude, and Gemini are each compared with the flag OFF.*

---

## Manual-Only Verifications

| Behavior | Why Manual | Test Instructions |
|----------|------------|-------------------|
| Companion designator UI field (`CompanionName` on `DeckAnalysisRequest`, single input, no hidden mirror) | UI input parity with manabase commander-callout; live render across themes/mobile | Load `/deck-analysis`, Step 1, enter Archidekt deck with companion, set companion designator, generate → confirm companion named in artifact (DECK CONTEXT / `<companion>`) and no duplicate-binding error. Playwright smoke `e2e/deck-analysis-command-zone.spec.ts` covers ON/OFF render; cross-theme/mobile is the operator checkpoint. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (5 new unit tests)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
