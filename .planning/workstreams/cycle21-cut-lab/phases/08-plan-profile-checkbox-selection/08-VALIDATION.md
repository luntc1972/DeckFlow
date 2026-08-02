---
phase: 8
slug: plan-profile-checkbox-selection
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-08-02
---

# Phase 8 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (DeckFlow.Core.Tests, DeckFlow.Web.Tests) + Playwright e2e |
| **Config file** | existing test projects — no Wave 0 install |
| **Quick run command** | `dotnet build` (clean build is the WSL-reliable gate) + targeted `dotnet test --filter` where VSTest cooperates |
| **Full suite command** | `dotnet test` (or push-and-watch CI per repo constraint — VSTest unreliable in WSL) |
| **Estimated runtime** | ~120 seconds build; CI for full suite |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build` clean (0 errors, warning baseline 9 CS8629)
- **After every plan wave:** Run full test suite (CI if WSL VSTest misbehaves)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~180 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| (filled by planner) | — | — | PLPR-01..06 | — | — | unit | — | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

To be populated by gsd-planner from the plans' acceptance criteria. Validation
architecture (integration points, sampling strategy, mutation-check requirements) is in
`08-RESEARCH.md` § Validation Architecture.

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. No new framework, no new test
project — resolver/detector/floor tests land in `DeckFlow.Core.Tests` where the logic is
Core-side, service and UI tests in `DeckFlow.Web.Tests`, e2e in `DeckFlow.Web/e2e`.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Plan panel visual check, 2 viewports | PLPR-UI | Guild-theme rendering judgment | Headless server via `scripts/run-web-test.sh`; Playwright screenshots at ~1440px and 390x844; ask user before any browser |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 180s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
