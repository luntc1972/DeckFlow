---
phase: 76
slug: bracket-classifier-balancer
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-06-28
---

# Phase 76 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (xunit.runner.visualstudio 3.1.4) |
| **Config file** | none |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~Bracket" --no-build` |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln --no-build` |
| **Estimated runtime** | ~60s (Bracket filter) / ~3-4 min (full suite) |

---

## Sampling Rate

- **After every task commit:** Run the quick run command (`~Bracket` filter)
- **After every plan wave:** Run the full suite command (`DeckFlow.sln`)
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~60 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 76-01-T1 | 01 | 0 | BRACKET-02 | — | N/A | build | `dotnet build DeckFlow.Core.csproj` | ✅ | ⬜ pending |
| 76-01-T2 | 01 | 0 | BRACKET-02 | T-76-03 (seed-file parse) | bracket-data.json parsed safely at startup | build | `dotnet build DeckFlow.Web.csproj && test -f .../bracket-data.json` | ❌ W0 | ⬜ pending |
| 76-01-T3 | 01 | 0 | BRACKET-01, BRACKET-03 | — | null combo → unavailable, not zero | unit | `dotnet test --filter BracketClassifierTests` | ❌ W0 | ⬜ pending |
| 76-02-T1 | 02 | 1 | BRACKET-02 | T-76-03 | JSON→IMemoryCache load | unit | `dotnet test --filter GameChangerCatalogServiceTests` | ❌ W0 | ⬜ pending |
| 76-02-T2 | 02 | 1 | BRACKET-02, BRACKET-05 | — | catalog migration byte-identity | unit | `dotnet test --filter ResultContractTests\|PrimerPromptVariantTests` | ✅ | ⬜ pending |
| 76-02-T3 | 02 | 1 | BRACKET-02, BRACKET-05 | — | flag seeded OFF | unit | `dotnet test --filter FeatureFlagCatalogTests\|FeatureFlagStoreSeedTests` | ❌ update | ⬜ pending |
| 76-03-T1 | 03 | 2 | BRACKET-04 | — | N/A | build | `dotnet build DeckFlow.Web.csproj` | ✅ | ⬜ pending |
| 76-03-T2 | 03 | 2 | BRACKET-03, BRACKET-04, BRACKET-05 | — | effective-date + combo-unavailable in all 3 variants | unit | `dotnet test --filter BracketPromptVariantParityTests` | ❌ W0 | ⬜ pending |
| 76-03-T3 | 03 | 2 | BRACKET-04 | — | parity: both blocks in all 3 variants | unit | `dotnet test --filter BracketPromptVariantParityTests` | ❌ W0 | ⬜ pending |
| 76-04-T1 | 04 | 3 | BRACKET-01, BRACKET-03 | — | N/A | build | `dotnet build DeckFlow.Web.csproj` | ✅ | ⬜ pending |
| 76-04-T2 | 04 | 3 | BRACKET-01, BRACKET-03 | T-76-01 (deck input) | null combo disclosure preserved | unit | `dotnet test --filter BracketClassificationServiceTests` | ❌ W0 | ⬜ pending |
| 76-05-T1 | 05 | 4 | BRACKET-01, BRACKET-03 | T-76-02 (URL import SSRF) | reuse Phase 64 host hardening | build | `dotnet build DeckFlow.Web.csproj` | ✅ | ⬜ pending |
| 76-05-T2 | 05 | 4 | BRACKET-05 | — | flag-gated route + registry tile | build | `dotnet build DeckFlow.Web.csproj && grep -c "Phase 76"` | ✅ | ⬜ pending |
| 76-05-T3 | 05 | 4 | BRACKET-05 | — | flag OFF → no bracket-badge markup | render | `dotnet test --filter BracketViewRenderTests` | ❌ W0 | ⬜ pending |
| 76-06-T1 | 06 | 5 | BRACKET-01, BRACKET-03 | — | live route + flag-OFF 404 | e2e | `cd DeckFlow.Web && DECKFLOW_DISABLE_AUTO_BROWSER=true npx --no-install playwright test e2e/bracket-smoke.spec.ts --reporter=line` | ❌ W0 | ⬜ pending |
| 76-06-T2 | 06 | 5 | BRACKET-01, BRACKET-03, BRACKET-05 | — | cross-theme/mobile visual sign-off | manual | checkpoint:human-verify | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `DeckFlow.Core.Tests/Bracket/BracketClassifierTests.cs` — BRACKET-01 classifier unit tests (GC threshold, MLD gate, combo gate, extra-turn informational-only, null-combo disclosure)
- [ ] `DeckFlow.Web.Tests/Bracket/BracketPromptVariantParityTests.cs` — BRACKET-04 parity (classification + balancer block in all 3 variants)
- [ ] `DeckFlow.Web.Tests/Bracket/BracketViewRenderTests.cs` — BRACKET-05 flag-OFF/ON view invariant (IRazorViewEngine, following ManabaseViewRenderTests.cs)
- [ ] Update `DeckFlow.Web.Tests/FeatureFlagCatalogTests.cs` — add `[InlineData("tool.bracket.enabled")]`
- [ ] Update `DeckFlow.Web.Tests/FeatureFlagStoreSeedTests.cs` — add `[InlineData("tool.bracket.enabled", false)]`
- [ ] `DeckFlow.Web/e2e/bracket-smoke.spec.ts` — BRACKET-01/03 live smoke (created in 76-06)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Bracket surface renders correctly across themes (Classic/Azorius/Nyx) + mobile | BRACKET-01, BRACKET-03, BRACKET-05 | Visual fidelity across guild-theme CSS forks + responsive layout cannot be asserted by render tests alone | Start server via `scripts/run-web-test.sh`; capture screenshots at desktop (1280px) + mobile (390px) per the two Playwright projects; operator confirms badge, reasons, balancer, and copy-textarea read correctly |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
