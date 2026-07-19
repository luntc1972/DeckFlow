---
phase: 103
slug: simulation-engine-guided-cut-rounds
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-19
---

# Phase 103 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Core.Tests, DeckFlow.Web.Tests) + Vitest (DeckFlow.Web ts-tests) + Playwright (DeckFlow.Web/e2e) |
| **Config file** | DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj, DeckFlow.Web/package.json, DeckFlow.Web/playwright.config.ts |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"` |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln` + `npm test` + `npx tsc --noEmit` (in DeckFlow.Web) |
| **Estimated runtime** | quick ~60s; full ~5 min (.NET) + ~30s (Vitest/tsc) |

---

## Sampling Rate

- **After every task commit:** Run the quick CutLab-filtered xUnit command (plus Vitest when cut-lab.ts touched)
- **After every plan wave:** Run full .NET suite + Vitest + tsc
- **Before `/gsd:verify-work`:** Full suite must be green, e2e cut-lab specs green
- **Max feedback latency:** 300 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| (filled by planner per PLAN.md tasks) | | | CUT-01, CUT-02, CUT-03, SIM-01, SIM-02 | | | | | | pending |

---

## Wave 0 Requirements

- [ ] Timing spike: measure `ManabaseAnalyzer.Analyze` wall-clock at default trial counts on ~130-card pool (research Open Q3 — gates D-11 iteration-count decisions). Harness may be a temporary xUnit fact or CLI probe; result recorded in the plan/SUMMARY, not shipped.
- [ ] Determinism guard test: same working list twice → byte-identical metric snapshot (locks D-08 fixed-seed claim from research).

*Existing infrastructure covers all other phase requirements — no new framework installs.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Delta panel readability + neutral framing copy | CUT-02 | Visual/copy judgment | Load oversized pool behind flag, walk one round, eyeball delta card + copy on desktop + mobile |
| Perceived latency of accept/reject/defer loop | SIM-01 / D-11 | Wall-clock feel on real hardware | Time 5 consecutive decisions; each ≤ ~1s target, 3s cap w/ spinner |

*All other phase behaviors have automated verification (xUnit/Vitest/e2e).*

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 300s
- [x] `nyquist_compliant: true` set in frontmatter
- [ ] Wave 0 complete (timing spike + determinism guard run at execution — `wave_0_complete: false` until then)

**Approval:** pending
