---
phase: 101
slug: intake-protection-foundation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-19
---

# Phase 101 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from `101-RESEARCH.md` §Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (.NET), `@playwright/test` ^1.60.0 (e2e), Vitest ^3.2.7 (TS unit) |
| **Config** | `DeckFlow.Web/playwright.config.ts`; xUnit via `.csproj` |
| **Quick run** | `dotnet build` clean (WSL baseline — VSTest unreliable in WSL) |
| **Full suite** | `dotnet test DeckFlow.Web.Tests` + `dotnet test DeckFlow.Core.Tests` + `npx playwright test cut-lab-*.spec.ts` via `scripts/run-web-test.sh` (never open a browser on the Windows host) |

## Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|--------------|
| INTAKE-01 | Submit 101–150 pool via URL/paste; card count + legality summary | unit + e2e | `dotnet test --filter CutLabPageServiceTests` / `npx playwright test cut-lab-smoke.spec.ts` | ❌ Wave 0 |
| INTAKE-02 | Intent declaration persists with working session | unit + e2e | `dotnet test --filter CutLabRequestTests` + reload-and-restore e2e assertion | ❌ Wave 0 |
| INTAKE-03 | ≤100 or >150 pool → clear actionable message | unit | `dotnet test --filter CutLabPoolValidatorTests` | ❌ Wave 0 |
| LOCK-01 | Individual locks; commander auto-locked, cannot unlock | unit + e2e | `dotnet test --filter CutLabLockStateTests` + disabled/checked commander checkbox e2e | ❌ Wave 0 |
| LOCK-02 | Named packages lock/unlock as unit | unit | `dotnet test --filter CutLabPackageTests` | ❌ Wave 0 |
| LOCK-03 | Bulk-lock role group (all lands) | unit | `dotnet test --filter CutLabRoleGroupLockTests` (land detect via `CardTypeLine`) | ❌ Wave 0 |

Regression guard (must stay green): `ToolRegistryTests`, `FeatureFlagCatalogTests`, `FeatureFlagStoreSeedTests`. `SeoPathsTests` untouched — Cut Lab NOT added to `SeoPaths.Indexable` until Phase 105.

## Sampling Rate

- **Per task commit:** `dotnet build` clean + `dotnet test --filter CutLab*`
- **Per wave merge:** full both-project `dotnet test` + `cut-lab-*.spec.ts` e2e via `scripts/run-web-test.sh`
- **Phase gate:** full suite green, e2e desktop+mobile viewports, ≥2 themes, before `/gsd-verify-work`

## Wave 0 Gaps

- [ ] `DeckFlow.Web.Tests/CutLabPageServiceTests.cs` — INTAKE-01, INTAKE-02
- [ ] `DeckFlow.Web.Tests/CutLabPoolValidatorTests.cs` — INTAKE-03 (both branches)
- [ ] `DeckFlow.Web.Tests/CutLabLockStateTests.cs` — LOCK-01, LOCK-02
- [ ] `DeckFlow.Web.Tests/CutLabRoleGroupLockTests.cs` — LOCK-03
- [ ] `DeckFlow.Web/e2e/cut-lab-smoke.spec.ts` — mirrors `deck-history-smoke.spec.ts` (flag admin lock, theme×viewport screenshots)
- [ ] Framework install: none — all frameworks already configured
