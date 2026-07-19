---
phase: 102
slug: structural-analysis-role-floors
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-19
---

# Phase 102 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Web.Tests, DeckFlow.Core.Tests) + Vitest (wwwroot/ts tests) + Playwright e2e |
| **Config file** | DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj, DeckFlow.Web/package.json, playwright.config |
| **Quick run command** | `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"` (Windows dotnet.exe from WSL per repo conventions) |
| **Full suite command** | `dotnet test` (full solution) + `npx --no-install vitest run` + `npx --no-install playwright test` (headless, DECKFLOW_DISABLE_AUTO_BROWSER=true) |
| **Estimated runtime** | quick ~30s · full ~5min · e2e ~1min |

---

## Sampling Rate

- **After every task commit:** Run the CutLab-filtered quick command
- **After every plan wave:** Run full Web suite; e2e at UI waves
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 300 seconds

---

## Per-Task Verification Map

*Populated by planner — one row per task; see PLAN.md `<automated>` fields.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| — | — | — | SLOT-01, SLOT-02, FLOOR-01, FLOOR-02 | — | — | — | — | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements — Phase 101 established CutLab test files (CutLabStateSerializerTests, CutLabControllerTests, CutLabLockRules tests, cut-lab Vitest suite, cut-lab-smoke.spec.ts e2e). New test files for role-floor rules and structural findings follow the same patterns; no framework install needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Theme/viewport visual review of slot groups + findings + floors UI | SLOT-01, SLOT-02, FLOOR-01 | Visual quality judgment | Screenshot 3 themes × 2 viewports per house rule |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 300s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
