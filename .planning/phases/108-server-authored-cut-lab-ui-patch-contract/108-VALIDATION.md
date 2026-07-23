---
phase: 108
slug: server-authored-cut-lab-ui-patch-contract
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-23
review_status: pending_claude_review
---

# Phase 108 — Validation Strategy

> Per-phase validation contract for server-authored Cut Lab UI patch planning.

## Test Infrastructure

| Property | Value |
|----------|-------|
| Framework | xUnit, Vitest, TypeScript compiler, Playwright |
| Config file | `DeckFlow.Web/vitest.config.ts`, `DeckFlow.Web/tsconfig.json`, `DeckFlow.Web/playwright.config.ts` |
| Quick run command | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabApiControllerTests` |
| Full suite command | `dotnet.exe test DeckFlow.Web.Tests --filter CutLab && npm test -- --run && node_modules/.bin/tsc --noEmit` |
| Estimated runtime | ~120-240 seconds locally |

## Sampling Rate

- After every task commit: run the task-specific xUnit or Vitest file named in the task.
- After every plan wave: run `dotnet.exe test DeckFlow.Web.Tests --filter CutLab && npm test -- --run`.
- Before `/gsd-verify-work`: run `dotnet.exe test DeckFlow.Web.Tests --filter CutLab`, `npm test -- --run`, and `node_modules/.bin/tsc --noEmit`.
- Max feedback latency: one task should not go more than one commit without an automated server or TS check.

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 108-01-01 | 01 | 1 | CLUP-01 | T-108-01 | Reject invalid state through existing API validation before patch building | unit | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabUiPatchBuilderTests` | ❌ W1 | pending |
| 108-01-02 | 01 | 1 | CLUP-01, CLUP-03 | T-108-02 | Patch exposes only derived display state, not secrets or arbitrary request echo | unit | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabUiPatchBuilderTests` | ❌ W1 | pending |
| 108-02-01 | 02 | 2 | CLUP-01, CLUP-03 | T-108-03 | Same-origin and feature-flag gates remain on mutation endpoints | unit | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabApiControllerTests` | ✅ | pending |
| 108-02-02 | 02 | 2 | CLUP-01, CLUP-03 | T-108-04 | What-if commit remains atomic; rejected half-swaps do not return success patches | unit | `dotnet.exe test DeckFlow.Web.Tests --filter CutLabApiControllerTests` | ✅ | pending |
| 108-03-01 | 03 | 3 | CLUP-02, CLUP-03 | — | Client renders server patch fields and does not recompute live mutation counts/options | unit | `npm test -- --run cut-lab-adjust cut-lab-whatif cut-lab-proposal` | ✅ | pending |
| 108-03-02 | 03 | 3 | CLUP-02, CLUP-03 | — | TypeScript API interfaces match server response shape | compile | `node_modules/.bin/tsc --noEmit` | ✅ | pending |

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

Approval: pending Claude/GSD review after Claude availability resumes.
