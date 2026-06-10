---
phase: 30
slug: content-kb-integration
status: validated
nyquist_compliant: false
wave_0_complete: true
created: 2026-06-09
---

# Phase 30 — Validation Strategy

> Retroactively reconstructed from PLAN/SUMMARY artifacts (State B). All four plans
> shipped `complete`; this audits Nyquist coverage of the six KBI requirements.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Web.Tests`) |
| **Config file** | `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --filter ContentKb` |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests` |
| **Estimated runtime** | ~30–60 seconds (build-clean gate is primary; VSTest unreliable in WSL — push-and-watch CI is the binding test run per CLAUDE.md) |

---

## Sampling Rate

- **After every task commit:** Windows `dotnet build DeckFlow.Web` clean (0 errors / 0 new warnings) + targeted `--filter`
- **After every plan wave:** full `DeckFlow.Web.Tests` suite (or push-and-watch CI)
- **Before `/gsd-verify-work`:** full suite green
- **Max feedback latency:** ~60 seconds (build) / CI for authoritative test run

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 30-01-01 | 01 | 1 | KBI-01 | T-30-01/02/03 | Admin flag flip behind BasicAuth; read-only prod audit | manual | — (operator-confirmed live) | N/A | ✅ green (operator) |
| 30-02-01 | 02 | 2 | KBI-02 | T-30-07 | Clip parse + 150-word truncation; JSON round-trip | unit | `... --filter ContentKbClipParser` | ✅ ContentKbExcerptTests, ContentKbClipParserTests | ✅ green |
| 30-02-02 | 02 | 2 | KBI-02, KBI-05 | T-30-04/05 | Flag-gate first; traversal-guarded reads; ≥2-dim AND gate; budget trim | unit | `... --filter ContentKbRelevance` | ✅ ContentKbRelevanceServiceTests (12), ContentKbArchetypeDeriverTests (3) | ✅ green |
| 30-02-03 | 02 | 2 | KBI-02 | T-30-06 | Closed-DTO zip round-trip; allowlist-throws | unit | `... --filter PacketArtifactStore` | ✅ PacketArtifactStoreTests (10) | ✅ green |
| 30-03-01 | 03 | 3 | KBI-02, KBI-03 | T-30-08 | Block-quote attribution; third-party-evidence hardening; no-empty-header; Gemini paste-cap guard | unit | `... --filter ExpertContext` | ✅ AnalysisPromptVariantExpertContextTests (2F/4T) | ✅ green |
| 30-03-02 | 03 | 3 | KBI-02, KBI-03 | T-30-08/09/10 | Replay-first restore; single-set prompt==zip==panel; corrupt-replay degrades | unit | `... --filter ExpertContext` | ✅ DeckAnalysisPacketServiceExpertContextTests (5) | ✅ green |
| 30-04-01 | 04 | 4 | KBI-04, KBI-05 | T-30-12 | Fresh-path view-model mapping; Razor HTML-encode; panel-hide on null/empty | unit + manual | `... --filter DeckController` (mapping); panel render manual | ✅ DeckControllerTests (ExpertContextClips mapping) | ✅ green (unit) / ✅ human-verify Task 3 |
| 30-04-02 | 04 | 4 | KBI-06 | T-30-11/13 | Bracket allowlist validation; commander normalize; GET read-only | unit | `... --filter AdminContentKb` | ✅ AdminContentKbControllerTests (15) | ✅ green |
| 30-04-03 | 04 | 4 | KBI-04, KBI-05 | T-30-12 | Panel grouping/deep-link/collapsed at 2 viewports; re-upload replay | manual | — (visual checkpoint) | N/A | ✅ green (human-verify PASSED 2026-06-07) |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing xUnit infrastructure (`DeckFlow.Web.Tests`) covers all automatable phase
requirements. No new framework, fixtures, or stubs required. Test doubles reused:
`FakeFeatureFlagCache`, `FakeContentSiteIndexStore`, fake `ICategoryKnowledgeStore`.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `content.kb.enabled` ON in prod + ≥1 curated clip live on public browse page | KBI-01 | Live prod feature-flag + DB state; not reproducible in a unit test | Operator: `/Admin/Flags` shows ON; open `/content-kb`, confirm a clip renders (30-01 Task 2, confirmed) |
| "What Experts Say" panel renders grouped/attributed/deep-linked, collapsed by default, at desktop + mobile viewports; survives zip re-upload | KBI-04, KBI-05 | Razor visual rendering + responsive layout; not unit-testable (controller mapping IS unit-tested) | Run a matching DeckAnalysis, expand panel, verify grouping + timestamp deep-link jump; re-upload zip; screenshot @1280px & @390px (30-04 Task 3, PASSED 2026-06-07) |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify, manual-checkpoint, or are inherently manual (KBI-01 prod, panel visual)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (none — zero MISSING automatable gaps)
- [x] No watch-mode flags
- [x] Feedback latency < 60s (build) / CI authoritative
- [ ] `nyquist_compliant: true` — **NOT SET.** PARTIAL: KBI-01 + the Razor panel-render slice of KBI-04/05 are manual-by-nature (operator/human-verified). All automatable behavior is COVERED and green.

**Approval:** approved 2026-06-09 (PARTIAL — manual-only items operator/human-verified)

---

## Validation Audit 2026-06-09

| Metric | Count |
|--------|-------|
| Requirements | 6 (KBI-01..06) |
| COVERED (automated) | 4 (KBI-02, KBI-03, KBI-05, KBI-06) |
| PARTIAL (auto + manual) | 1 (KBI-04 — mapping auto, panel render manual) |
| MANUAL-ONLY | 1 (KBI-01 — live prod) |
| MISSING (automatable, unfilled) | 0 |
| Gaps found | 0 fillable |
| Resolved | 0 (none to fill) |
| Escalated | 0 |
| KB test files present | 8 (60 facts + 5 theories) |
