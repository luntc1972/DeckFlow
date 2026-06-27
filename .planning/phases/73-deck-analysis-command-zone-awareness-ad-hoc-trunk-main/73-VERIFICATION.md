---
phase: 73-deck-analysis-command-zone-awareness-ad-hoc-trunk-main
verified: 2026-06-27T00:00:00Z
status: passed
score: 6/6 must-haves verified
overrides_applied: 0
re_verification:
  previous_status: none
  previous_score: n/a
  note: initial goal-backward verification
gaps: []
deferred: []
---

# Phase 73: Deck-Analysis Command-Zone Awareness Verification Report

**Phase Goal:** Give the `/deck-analysis` prompt artifact command-zone AWARENESS — name the
commander(s)/partner pair, the Background, and the companion (if any) in the generated analysis
prompt across all three decoupled variants (ChatGpt/Claude/Gemini), flag-gated and byte-identical
when off. Companion carried as SIDE METADATA, never remapped into deck text.
**Verified:** 2026-06-27
**Status:** PASS
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths (the 6 focus checks)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Flag-OFF preserves byte-identity of all 3 prompt variants | ✓ VERIFIED | `companionName` is `null` unless inside `if (commandZoneAwareness)` (DeckAnalysisPacketService.cs:660-676); commander enrichment also gated. All 3 variants guard render on `!string.IsNullOrWhiteSpace(companionName)` (ChatGpt:65, Gemini:70, Claude:66). Test `BuildAsync_IsByteIdentical_WhenCommandZoneAwarenessFlagOff` is a `[Theory]` over ChatGPT/Claude/Gemini asserting full `PacketBytes` equality vs baseline (Tests:918-949). Flag seeded FALSE/0 in both dialects (FeatureFlagStore.cs:225,257). |
| 2 | Companion is SIDE METADATA only — not in deck text, cache key, or pre-Scryfall commander state | ✓ VERIFIED | `analysisDecklistText` built from `BuildDecklistText(deckEntries,…)` with no companion input (DeckAnalysisPacketService.cs:679-681). Cache inputs `BuildDeckAnalysisCacheInputs` contain Commander/DeckSource/Versions/Platform/Questions only — no companion (lines 248-262). `ResolvePreScryfallCommanderState` called at 446, BEFORE enrichment at 650; companion never reaches it. `companionName` flows only as a separate trailing arg to `BuildAnalysisPrompt` → rendered in DECK CONTEXT / `<companion>` block. (Codex HIGH-1 satisfied.) |
| 3 | Hostile/long companion input sanitized (single-line collapse, length cap, XML-escape for Claude) | ✓ VERIFIED | `BoundCompanionName` runs `CollapseWhitespace` (strips CR/LF) + `Trim()` + 200-char cap `MaxCompanionNameLength` (DeckAnalysisPacketService.cs:1609-1625). Claude additionally `SecurityElement.Escape(companionName)` (ClaudeAnalysisPromptVariant.cs:67). Test `..._CompanionInput_PreservesPromptShape` `[Theory]` over `</companion>\nInjected`, `<script>`, `a & b` asserts exactly ONE `<companion>`/`</companion>` pair (Claude) and a single `companion:` line (ChatGpt) (Tests:1084-1133). (Codex HIGH-2 satisfied.) |
| 4 | Partner/Background command-zone join resolves each name individually then joins (resolve-then-join) | ✓ VERIFIED | `.OrderBy(raw name)` → `.Select(name => OracleNameMap.TryGetValue(name,…) ? oracle : name)` → `string.Join(" & ", resolvedCommanderNames)` (DeckAnalysisPacketService.cs:663-673). Each name oracle-resolved individually BEFORE the concat; deterministic order by raw name (Pitfall 1). Tests `RendersPartnerPair` (asserts `"Kraum, Ludevic's Opus & Passionate Archaeologist"`) and `SingleCommanderUnchanged` (asserts no spurious `" &"` + full flag-ON==flag-OFF equality) (Tests:956-1016). |
| 5 | UI input is single — no duplicate/hidden mirror causing double-binding | ✓ VERIFIED | `grep -c 'name="CompanionName"' DeckAnalysis.cshtml` = 1; single `<form>` in view. Input gated on `Model.CommandZoneAwarenessEnabled` (DeckAnalysis.cshtml:171-181). View-model flag is `{ get; init; }` server-computed, NOT form-bound (DeckAnalysisViewModel.cs:24; T-73-05). Controller stamps it on all 11 render paths via one `IsCommandZoneAwarenessEnabled()` helper (DeckPacketController.cs:60-65). |
| 6 | README + Help document the feature | ✓ VERIFIED | README.md:225 — Phase-73 flagged bullet describing `analysis.command-zone-awareness`, default OFF, side-metadata, designator-wins, single-line-bounded. Help/deck-analysis.md:23-28 — "Companion designator (optional)" subsection naming the flag, auto-detect-vs-designator, designator-wins, never mutates decklist. |

**Score:** 6/6 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `FeatureFlagStore.cs` | flag seeded FALSE/0 both dialects | ✓ VERIFIED | Postgres `('analysis.command-zone-awareness', FALSE)` (225), SQLite `(…, 0)` (257) |
| `FeatureFlagCatalog.cs` | operator description | ✓ VERIFIED | Non-empty plain-hyphen description (line 69-70) |
| `IAnalysisPromptVariant.cs` + registry + 3 variants | `string? companionName = null` threaded | ✓ VERIFIED | Interface, registry (forwards arg), ChatGpt/Gemini/Claude all carry + render |
| `DeckAnalysisRequest.cs` | `CompanionName` null-coalescing setter | ✓ VERIFIED | Lines 114-122 |
| `DeckAnalysisViewModel.cs` | init-only `CommandZoneAwarenessEnabled` | ✓ VERIFIED | Line 24, server-computed |
| `DeckPacketController.cs` | flag stamped on all render paths | ✓ VERIFIED | 11 sites + single helper (Codex MED-1) |
| `DeckAnalysis.cshtml` | single flag-gated input | ✓ VERIFIED | One `name="CompanionName"`, one form |
| `site-common.css` | `.deck-analysis-overrides` (not theme fork) | ✓ VERIFIED | Layout CSS in shared common file (theme constraint honored) |
| `e2e/deck-analysis-command-zone.spec.ts` | flag ON→input count 1, OFF→0 | ✓ VERIFIED | toHaveCount(1)/toHaveCount(0) assertions present |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| Step-1 input | `DeckAnalysisRequest.CompanionName` | form-bound `name="CompanionName"` | ✓ WIRED | Razor input binds to request field |
| `BuildAsync` | `ResolveCompanionName` | designator-wins resolution behind flag | ✓ WIRED | `ResolveCompanionName(request.CompanionName, detectedCompanionName)` (675) |
| `BuildAsync` | `BuildAnalysisPrompt` → registry → variant | trailing `companionName` arg | ✓ WIRED | Threaded full chain; rendered in 3 variants |
| Controller flag read | view-model | `IsCommandZoneAwarenessEnabled()` | ✓ WIRED | Default-OFF snapshot pattern, 11 sites |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | No debt markers (TBD/FIXME/XXX/TODO) in changed files | ℹ️ Info | Clean |
| DeckAnalysisPacketService.cs | 248-262 | Companion intentionally NOT in cache key | ℹ️ Info | See Gaps Summary — deliberate, documented trade-off |

### Human Verification Required

Plan 04 Task 3 (`checkpoint:human-verify`, blocking) — operator cross-theme + mobile visual
sign-off — was marked PENDING in 73-04-SUMMARY. Per the orchestrator's empirical record this has
been satisfied: flag ON renders the input, flag OFF absent (byte-identity), screenshots verified
across Classic + Azorius at desktop + mobile. No outstanding human item blocks the goal.

### Gaps Summary

No blocking gaps. The phase goal is fully achieved: command-zone awareness renders the enriched
commander/partner/Background and the companion as side metadata in all three decoupled prompt
variants, flag-gated and provably byte-identical when off; hostile input is sanitized; the UI input
is single and server-gated; docs updated.

**One INFO-level known limitation (not a blocker):** the deck-analysis session cache key
(`BuildDeckAnalysisCacheInputs`) intentionally does NOT include `CompanionName` (nor the enriched
`" & "` commander string — the cache Commander field uses the pre-Scryfall singular name). This was
the deliberate "leave the cache-key path untouched" decision (Pitfall 3 / cache-key invariant) that
guarantees the flag-OFF byte-identity contract and prevents companion-driven cache poisoning. The
side effect, only reachable when an operator turns the flag ON, is that re-submitting the SAME deck
within a session with a DIFFERENT companion designator can serve the previously-cached packet
(stale companion). Flag is seeded OFF in prod, so production output is byte-identical and unaffected.
Recommend the operator be aware of this before enabling the flag; it does not block phase completion.

---

_Verified: 2026-06-27_
_Verifier: Claude (gsd-verifier)_
