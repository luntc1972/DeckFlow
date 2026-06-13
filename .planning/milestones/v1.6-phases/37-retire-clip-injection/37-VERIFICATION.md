---
phase: 37-retire-clip-injection
verified: 2026-06-10T22:55:00Z
status: verified
score: 6/6 must-haves verified
overrides_applied: 0
resolution:
  - truth: "RET-05 gap CLOSED by fix commit 48c273e (2026-06-10)"
    fix: >
      Re-added "32-expert-context.json" and "33-expert-selection.json" to
      PacketArtifactStore.PacketAllowedNames (read-only tolerance; write side no
      longer emits them). ReadEntries now accepts-and-ignores the legacy entries
      instead of throwing. Verified GREEN: PacketLegacyZipBackCompatTests passes
      (targeted run 6/6, full suite confirmed). Build 0/0.
gaps_resolved:
  - truth: "A pre-retire packet zip carrying ExpertSelectionJson / ExpertContextJson loads without throwing (RET-05)"
    status: resolved_by_48c273e
    reason: >
      Plan 37-01 deleted "32-expert-context.json" and "33-expert-selection.json"
      from PacketArtifactStore.PacketAllowedNames, but ReadEntries enforces the
      allow-list as a STRICT REJECT — it throws InvalidOperationException
      ("Imported zip contains an unsupported entry: ...") for any entry name not
      in the set (PacketArtifactStore.cs:764-767). It does NOT silently skip
      unknown entries. The plan's interface note (37-01-PLAN.md:137) assumed
      "ReadEntries' allow-list filter makes leftover legacy entries a no-op" —
      that assumption is false for this codebase. Git confirms both names WERE in
      PacketAllowedNames before retire (0e91a29~1) and were removed by 0e91a29.
      Net effect: a real pre-retire packet zip now throws on the first legacy
      entry instead of loading. The RET-05 regression test
      (PacketLegacyZipBackCompatTests) DOES include the two legacy entries and
      asserts Assert.Null(exception) — so the test must be RED at runtime; the
      build-only gate (VSTest unreliable in WSL) masked it.
    artifacts:
      - path: "DeckFlow.Web/Services/PacketArtifactStore.cs"
        issue: >
          PacketAllowedNames (lines 27-41) no longer contains
          "32-expert-context.json" / "33-expert-selection.json"; ReadEntries
          (764-767) throws on any non-allow-listed entry, so LoadFromZip (265)
          rejects legacy packets.
      - path: "DeckFlow.Web.Tests/PacketLegacyZipBackCompatTests.cs"
        issue: >
          Asserts no-throw for a zip containing 32-expert-context.json +
          33-expert-selection.json; will fail against the current strict
          allow-list (test not actually executed — build-only gate).
    missing:
      - >
        Re-add "32-expert-context.json" and "33-expert-selection.json" to
        PacketArtifactStore.PacketAllowedNames so legacy entries pass the strict
        allow-list and are tolerated (read into the dictionary and simply never
        consumed). Write side already stopped emitting them, so this is
        read-only back-compat only.
      - >
        OR change ReadEntries to skip (continue) unknown entries instead of
        throwing — broader blast radius (affects all 4 zip families' tamper
        posture T-37-02); the targeted allow-list re-add is the safer fix.
      - >
        After the fix, run PacketLegacyZipBackCompatTests (CI push-and-watch,
        since VSTest is unreliable in WSL) to confirm it is GREEN.
human_verification:
  - test: "Production /Admin/Flags content.kb.enabled flip"
    expected: "Live prod row for content.kb.enabled set to ON so /content-kb is reachable in production (seed change only affects fresh DBs)"
    why_human: "Out of code scope — operator action on the live Render instance; cannot be verified from the repo."
  - test: "Manual /content-kb browse smoke (RET-03 / RET-04)"
    expected: "Flip flag ON, load /content-kb, distilled entries render, no expert pin/follow tray, harvested clip/summary text HTML-encoded (no markup injection)"
    why_human: "Live flag-gated HTML render + visual XSS confirmation; not verifiable by grep/build."
  - test: "End-to-end deck-analysis prompt (RET-01 / RET-06)"
    expected: "Generate ChatGPT/Claude/Gemini analysis packet; artifact has no '## Expert Context' block; DeckAnalysis page shows the KB pointer note + working /content-kb link"
    why_human: "Full request-path artifact generation; build/grep proves the static surface but not the rendered end-to-end output."
---

# Phase 37: Retire Clip-Injection + Un-Dark KB Browse — Verification Report

**Phase Goal:** The gate-condemned clip-injection into deck-analysis prompts is fully removed (the `## Expert Context` block, the expert-selection widget, the "What Experts Say" panel, the retriever services), the KB-as-reference (`/content-kb` browse) is kept and un-darked, and the deck-analysis page points users to the KB's copyable prompts.

**Verified:** 2026-06-10T22:55:00Z
**Status:** gaps_found
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth (RET) | Status | Evidence |
|---|-------------|--------|----------|
| 1 | RET-01: No `## Expert Context` in any of 3 prompt variants; DeckAnalysis has no accordion/panel/widget | ✓ VERIFIED | `grep "## Expert Context" PromptBuilders/Analysis/*.cs` → nothing; `EstimateExpertContextLength` gone; `IAnalysisPromptVariant.Build` (IAnalysisPromptVariant.cs:26-35) has no `kbExcerpts` param; DeckAnalysis.cshtml has no Expert Context / `_ContentKbPanel` / kb-selection / accordion. RET-01 test asserts `DoesNotContain` across all 3 variants using the post-removal signature. |
| 2 | RET-02: Injection code fully removed, build 0/0, no dead references | ✓ VERIFIED | Solution-wide sweep (ContentKbRelevanceService / IContentKbRelevanceService / ExpertSelection / ContentKbArchetypeDeriver / ContentKbClipSanitizer / ContentKbExcerpt / ContentKbSearchApiController / kb-selection / ExpertContextJson / ExpertSelectionJson / PinnedVideoIds / FollowedCreators) over `--include=*.cs,*.cshtml,*.ts` excluding bin/obj → NOTHING. All 8 deleted files confirmed absent. Build 0/0 (orchestrator + per-task). |
| 3 | RET-03: KB reference intact (harvest/distill + browse + admin curation) | ✓ VERIFIED | ContentKbController, ContentKb/Index.cshtml, ContentKb/Detail.cshtml, AdminContentKbController, AdminContentKb/Index.cshtml all present; admin grid keeps ReloadSeed / SetVisibility / SetEvergreen / BulkSetVisibility / data-kb-search. CLI harvest/distill untouched by fenced file sets. (Live render = human smoke.) |
| 4 | RET-04: `/content-kb` un-darked (seed ON) + harvested text XSS-safe | ✓ VERIFIED (code) | FeatureFlagStore.cs:180 `('content.kb.enabled', TRUE)` (Postgres), :190 `('content.kb.enabled', 1)` (SQLite); ContentKbController.cs:19-20 keeps `.UseAdvancedExtensions().DisableHtml()`; zero `Html.Raw`/`MarkupString`/`WriteLiteral`/`IHtmlContent` in `Views/ContentKb/`; `ContentKbMarkdigXssTests.cs` present. (Prod flag flip + visual = human.) |
| 5 | RET-05: Pre-retire packet zip with ExpertSelectionJson loads without error | ✗ FAILED | `PacketAllowedNames` (PacketArtifactStore.cs:27-41) no longer contains `32-expert-context.json` / `33-expert-selection.json`; `ReadEntries` (764-767) THROWS on any non-allow-listed entry. `LoadFromZip` (265) uses this strict reader. A legacy zip therefore throws "unsupported entry", contradicting RET-05. Git: both names were in the allow-list pre-retire (0e91a29~1) and removed by 0e91a29. The RET-05 test asserts no-throw and must be RED at runtime. |
| 6 | RET-06: DeckAnalysis carries note + link to `/content-kb`; nav copy accurate | ✓ VERIFIED | DeckAnalysis.cshtml:194 — "Knowledge Base note: Browse distilled creator advice in the Knowledge Base … copy a ready-to-paste prompt" linking `~/content-kb`. Home.cshtml:67-68 rewritten to accurate browse/copy copy (no inject promise). No `inject`-feature copy remains in Home/_DeckToolTabs (only `@inject` Razor directives). No `Html.Raw` in any of the three views. |

**Score:** 5/6 truths verified (RET-05 FAILED)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `DeckFlow.Web.Tests/AnalysisPromptVariantNoExpertContextTests.cs` | RET-01 regression, 3 `DoesNotContain` | ✓ VERIFIED | 3 `[Fact]`s, post-removal Build signature (9 args), Ordinal `DoesNotContain("## Expert Context")` |
| `DeckFlow.Web.Tests/PacketLegacyZipBackCompatTests.cs` | RET-05 regression, legacy zip loads no-throw | ⚠️ PRESENT but ASSERTS A FALSE OUTCOME | Includes 32-/33- legacy entries and asserts `Assert.Null(exception)`; the production code under test throws → test is RED (masked by build-only gate) |
| `DeckFlow.Web.Tests/ContentKbMarkdigXssTests.cs` | RET-04 XSS regression | ✓ VERIFIED | File present; pins `.DisableHtml()` posture |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| DeckAnalysisPacketService → IAnalysisPromptVariant.Build | analysis prompt | `kbExcerpts` param removed | ✓ WIRED | Build signature carries no `kbExcerpts`; no variant references it |
| PacketArtifactStore read path → legacy 32-/33- entries | graceful ignore | allow-list filter | ✗ NOT_WIRED | Allow-list filter is a STRICT REJECT (throw), not a graceful ignore; legacy names removed from the set → legacy zip rejected |
| DeckAnalysis.cshtml → /content-kb | RET-06 pointer | note + link | ✓ WIRED | DeckAnalysis.cshtml:194 links `~/content-kb` |
| ContentKbController Markdig → Detail render | XSS-safe | `.DisableHtml()` preserved | ✓ WIRED | ContentKbController.cs:20 retains `.DisableHtml()` |

### Requirements Coverage

| Requirement | Source Plan | Status | Evidence |
|-------------|-------------|--------|----------|
| RET-01 | 37-01 | ✓ SATISFIED | Truth 1 |
| RET-02 | 37-01 | ✓ SATISFIED | Truth 2 |
| RET-03 | 37-02 | ✓ SATISFIED (code) / human smoke | Truth 3 |
| RET-04 | 37-02 | ✓ SATISFIED (code) / human smoke + prod flag flip | Truth 4 |
| RET-05 | 37-01 | ✗ BLOCKED | Truth 5 — strict allow-list rejects legacy entries |
| RET-06 | 37-02 | ✓ SATISFIED | Truth 6 |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| PacketArtifactStore.cs | 27-41 / 764-767 | Removed back-compat allow-list entries against a strict-reject reader | 🛑 Blocker | Breaks RET-05 — legacy packets throw instead of loading |

No `TBD`/`FIXME`/`XXX` debt markers in phase-modified files.

### Behavioral Spot-Checks

VSTest is unreliable in WSL (project constraint) — test runner NOT executed per instructions. Build evidence: `dotnet build DeckFlow.sln -c Debug` = 0/0 (orchestrator-confirmed after both plans). Note: build-green does NOT exercise xUnit assertions; the RET-05 test failure is a runtime/assertion failure invisible to the build gate. Recommend CI push-and-watch on the surviving suite after the RET-05 fix.

### Human Verification Required

1. **Prod `/Admin/Flags content.kb.enabled` flip** — operator action; seed change only affects fresh DBs.
2. **Manual `/content-kb` browse smoke (RET-03/04)** — entries render, no pin/follow tray, harvested text encoded.
3. **End-to-end deck-analysis prompt (RET-01/06)** — no `## Expert Context`; KB pointer note + working link.

### Gaps Summary

Five of six RET requirements are genuinely delivered in code: the injection path, the three retriever services, the expert-selection types/endpoints/TS, the `## Expert Context` blocks, the DeckAnalysis accordion/panel, the admin score-preview, the browse-page selection strip, and the dead CSS are all gone (sweep clean, build 0/0); `/content-kb` is seeded ON with `.DisableHtml()` preserved and zero raw-HTML sinks; and the DeckAnalysis page now points to `/content-kb` with accurate nav copy.

The single BLOCKER is **RET-05**. The plan removed `32-expert-context.json` / `33-expert-selection.json` from `PacketArtifactStore.PacketAllowedNames` on the (incorrect) assumption that the allow-list reader would silently ignore unknown entries. The reader instead **throws** `InvalidOperationException` for any non-allow-listed entry, so a real pre-retire packet zip now fails to load — the opposite of the requirement. The RET-05 regression test was written to assert no-throw and therefore must be RED at runtime; the WSL build-only gate masked it. No later phase (37.5 corpus rebuild, 38 SRP split) covers this, so it is not deferrable.

**Fix:** re-add the two legacy names to `PacketAllowedNames` (write side already stopped emitting them, so this is pure read-only tolerance), then confirm `PacketLegacyZipBackCompatTests` is GREEN via CI push-and-watch.

---

_Verified: 2026-06-10T22:55:00Z_
_Verifier: Claude (gsd-verifier)_
