---
phase: 100
slug: creator-style-tool-surface
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-19
---

# Phase 100 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`) + Playwright (`@playwright/test`, `DeckFlow.Web/e2e`) |
| **Config file** | `DeckFlow.Web/playwright.config.ts` (projects: `chromium-desktop` 1280x900, `chromium-mobile` 390x844); xUnit via `.csproj` only |
| **Quick run command** | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CreatorStyle` |
| **Full suite command** | `dotnet build` (0 new warnings) → `dotnet test` (both projects) → `npx --no-install playwright test` (server via `scripts/run-web-test.sh`) |
| **Estimated runtime** | ~60s quick · ~8–12 min full (suites + e2e) |

---

## Sampling Rate

- **After every task commit:** `dotnet build` (0 warnings) + targeted `dotnet test --filter` for touched test classes
- **After every plan wave:** Full `dotnet test` (both projects) + full `npx --no-install playwright test` (desktop+mobile)
- **Before `/gsd:verify-work`:** Full suite green, plus manual UAT with flag flipped ON locally (D-100-15 degraded banners, D-100-16 empty-store state)
- **Max feedback latency:** ~120 seconds (quick loop)

---

## Per-Task Verification Map

*Task IDs filled by planner; requirement→test contract fixed here.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | CS-30 flag seeded OFF both dialects | — | Tool unreachable by default at ship | unit | `dotnet test --filter FullyQualifiedName~FeatureFlagStoreSeedTests` | ❌ W0 (add seed row) | ⬜ pending |
| TBD | TBD | TBD | CS-30 cache-bypass wiring | T-100-02 stale-packet replay | Flag in `PromptMutatingAnalysisFlags`; no stale packet across flip | unit | new `CreatorStylePacketServiceTests` bypass cases | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | CS-30 route 404 when flag OFF | T-100-01 access control | `[FeatureFlagGate]` on controller | e2e | `npx --no-install playwright test creator-style` (tool-toggles-style) | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | CS-31 desktop+mobile+themes render | — | — | e2e | `npx --no-install playwright test creator-style.spec.ts` (both projects) | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | CS-31 byte-identical prose gate | T-100-02 | Existing artifacts unchanged | unit | existing byte-identity test suites re-run unmodified | ✓ existing | ⬜ pending |
| TBD | TBD | TBD | D-100-08/IN-01 epsilon verdict compare | — | — | unit | `CreatorStyleRubricScorerTests` epsilon case | ✓ add case | ⬜ pending |
| TBD | TBD | TBD | D-100-08/IN-03 degraded-notice wording branch | — | — | unit | `CreatorStylePacketServiceTests` branch cases | ✓ add cases | ⬜ pending |
| TBD | TBD | TBD | D-100-08/IN-04 unavailable ≠ degraded | — | — | unit | same file, status assertion | ✓ add case | ⬜ pending |
| TBD | TBD | TBD | D-100-08/IN-08 exemplar dedup | — | — | unit | `CreatorDeckExemplarSelectorTests` | ✓ add case | ⬜ pending |
| TBD | TBD | TBD | POST form CSRF | T-100-04 CSRF | `[ValidateAntiForgeryToken]` + `@Html.AntiForgeryToken()` | unit | controller attribute assertion test | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-14 seed hydration | — | Seed loader idempotent, no prod DB dependency | unit | new `CreatorStyleSeedLoaderTests` (mirror `ContentKbSeedLoaderTests`) | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-14 CLI export command | — | — | unit | serialization tests in `DeckFlow.Core.Tests` (CLI has no test project — extract testable serialization to Core/Web per mixed-solution rule) | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

Note: sitemap-exclusion test dropped — sitemap/SEO wiring deferred post-merge (user decision 2026-07-19; `SeoPaths` absent on this branch; `SitemapController` must not be touched).

---

## Wave 0 Requirements

- [ ] `DeckFlow.Web.Tests/.../CreatorStyleControllerTests.cs` — GET empty form, POST success, POST error ladder (mirror `ManabaseController` guard pattern)
- [ ] `DeckFlow.Web.Tests/.../CreatorStyleSeedLoaderTests.cs` — mirrors `ContentKbSeedLoaderTests.cs`
- [ ] `DeckFlow.Web/e2e/creator-style.spec.ts` — mirrors `manabase.spec.ts` + `tool-toggles.spec.ts` flag assertion
- [ ] `FeatureFlagStoreSeedTests` — new seed row `tool.creator-style.enabled=false` (exact flag key per CONTEXT.md/plans)
- Framework install: none — all frameworks already present.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| README documents workflow | CS-31 | doc review | Read README section for new tool; confirm workflow steps + flag note |
| Empty-store + degraded-banner UX with flag ON | D-100-15/16 | needs live flag flip + seeded/empty store states | `scripts/run-web-test.sh`, flip flag via /Admin/Flags, verify banner wording branches |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
