---
phase: 108
slug: server-authored-cut-lab-ui-patch-contract
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-23
review_status: claude_plan_checker_passed
---

# Phase 108 — Validation Strategy

> Per-phase validation contract for server-authored Cut Lab UI patch planning.

## Test Infrastructure

| Property | Value |
|----------|-------|
| Framework | xUnit, Vitest, TypeScript compiler, Playwright |
| Config file | `DeckFlow.Web/vitest.config.ts`, `DeckFlow.Web/tsconfig.json`, `DeckFlow.Web/playwright.config.ts` |
| Quick run command | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabApiControllerTests` |
| Full suite command | `dotnet.exe test DeckFlow.Web.Tests --filter CutLab && (cd DeckFlow.Web && npm test -- --run) && (cd DeckFlow.Web && node_modules/.bin/tsc --noEmit)` |
| Estimated runtime | ~120-240 seconds locally |

## Sampling Rate

- After every task commit: run the task-specific xUnit or Vitest file named in the task.
- After every plan wave: run `dotnet.exe test DeckFlow.Web.Tests --filter CutLab && (cd DeckFlow.Web && npm test -- --run)`.
- Before `/gsd-verify-work`: run `dotnet.exe test DeckFlow.Web.Tests --filter CutLab`, `cd DeckFlow.Web && npm test -- --run`, and `cd DeckFlow.Web && node_modules/.bin/tsc --noEmit`.
- Max feedback latency: one task should not go more than one commit without an automated server or TS check.

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 108-01-01 | 01 | 1 | CLUP-01, CLUP-03 | T-108-01, T-108-02 | Patch DTO contains only server-authored display fields, including quantity tuner row state | compile/unit | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabApiControllerTests` | ❌ W1 | pending |
| 108-01-02 | 01 | 1 | CLUP-01, CLUP-03 | T-108-01, T-108-02 | Patch builder derives counts, export readiness, proposal, floor warnings, cuts, findings, what-if options, and quantity legality from C# services | unit | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabUiPatchBuilderTests` | ❌ W1 | pending |
| 108-01-03 | 01 | 1 | CLUP-01, CLUP-03 | T-108-01, T-108-02 | Patch-vs-`CutLabViewModel` parity proves no-JS visible state matches for counts, export eligibility, floor warnings, cuts made, what-if options, and quantity tuner rows | unit | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabUiPatchBuilderTests` | ❌ W1 | pending |
| 108-02-01 | 02 | 2 | CLUP-01, CLUP-03 | T-108-03 | Decide endpoint keeps same-origin/feature-flag/request-size gates and returns patch fields that mirror any legacy transition fields | unit | `dotnet.exe test DeckFlow.Web.Tests --filter "CutLabApiControllerTests|CutLabWhatifTests"` | ✅ | pending |
| 108-02-02 | 02 | 2 | CLUP-01, CLUP-03 | T-108-03 | Adjust endpoint returns server-authored counts, export eligibility, and quantity tuner row legality instead of requiring client recomputation | unit | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabApiControllerTests` | ✅ | pending |
| 108-02-03 | 02 | 2 | CLUP-01, CLUP-03 | T-108-04 | What-if preview stays non-mutating; commit remains atomic and returns patch only after restore-then-accept succeeds | unit | `dotnet.exe test DeckFlow.Web.Tests --filter "CutLabApiControllerTests|CutLabWhatifTests"` | ✅ | pending |
| 108-03-01 | 03 | 3 | CLUP-02, CLUP-03 | T-108-05, T-108-06 | TypeScript patch interface matches server DTO and renderer updates all visible patch fields, including tuner rows | compile | `cd DeckFlow.Web && node_modules/.bin/tsc --noEmit` | ✅ | pending |
| 108-03-02 | 03 | 3 | CLUP-02, CLUP-03 | T-108-05, T-108-06 | Decide/adjust handlers render patch data and do not call serialized-state count/export or optimistic row patch helpers in live success paths | unit | `cd DeckFlow.Web && npm test -- --run cut-lab-adjust cut-lab-proposal` | ✅ | pending |
| 108-03-03 | 03 | 3 | CLUP-02, CLUP-03 | T-108-05, T-108-06 | What-if commit renders patch data, preview behavior remains unchanged, and legacy derivation helpers are quarantined outside live mutation success paths | unit/compile | `(cd DeckFlow.Web && npm test -- --run cut-lab-whatif) && (cd DeckFlow.Web && node_modules/.bin/tsc --noEmit)` | ✅ | pending |

## Wave 0 Requirements

Existing infrastructure covers all phase requirements.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Full Cut Lab browser smoke after server patch rollout | CLUP-03 | Playwright coverage may not assert every visible row after each live mutation | Start the local server with auto-browser disabled, run a Cut Lab sample through decide, adjust, what-if commit, and compare visible counts/export readiness to a no-JS reload. |

## Validation Sign-Off

- [x] All tasks have automated verify or Wave 0 dependencies.
- [x] Sampling continuity: no 3 consecutive tasks without automated verify.
- [x] Wave 0 covers all missing references.
- [x] No watch-mode flags.
- [x] Feedback latency target recorded.
- [x] `nyquist_compliant: true` set in frontmatter.

Approval: approved 2026-07-23 by GSD plan checker.
