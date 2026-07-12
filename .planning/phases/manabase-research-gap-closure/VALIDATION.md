---
phase: manabase-research-gap-closure
slug: manabase-research-gap-closure
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-12
---

# Phase manabase-research-gap-closure — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Derived from RESEARCH.md "## Validation Architecture" (line 938) — see that
> section for the full existing-test inventory (20 Core + 11 Web test files,
> 10 Playwright manabase specs).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`); Playwright (`DeckFlow.Web/e2e/*.spec.ts`) |
| **Config file** | `DeckFlow.sln`; Playwright config under `DeckFlow.Web/` |
| **Quick run command** | `dotnet build DeckFlow.sln` (0/0 gate) then `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~Manabase"` |
| **Full suite command** | `dotnet test DeckFlow.sln` + `scripts/run-web-test.sh` + `npx --no-install playwright test` |
| **Estimated runtime** | ~120s quick / ~10 min full incl. e2e |

---

## Sampling Rate

- **After every task commit:** `dotnet build DeckFlow.sln` + targeted `dotnet test --filter "FullyQualifiedName~Manabase"` for the touched area
- **After every plan wave:** Full `dotnet test DeckFlow.sln`; plus Playwright run for any wave touching `Manabase.cshtml` or flag wiring
- **Before `/gsd:verify-work`:** Full suite green + `cedh-land-calibrate` harness re-run with documented before/after delta (MBGAP-03)
- **Max feedback latency:** ~120 seconds (quick path)

---

## Per-Task Verification Map

MBGAP-keyed (no formal REQ-IDs — backlog phase). Task IDs filled by planner.

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | MBGAP-01 census weighting | — | N/A | unit | `dotnet test --filter FullyQualifiedName~ManabaseClassifierTests` | ✅ extend | ⬜ pending |
| TBD | TBD | TBD | MBGAP-01 flag-off byte-identical | — | N/A | unit | new `restricted-lands` parity test (mirror `ritual-burst-mana` pattern) | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MBGAP-01 disclosure marker renders | — | N/A | e2e | extend `manabase-ramp-disclosure.spec.ts` or new spec | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MBGAP-02 fast/slow/ELD per-trial | — | N/A | unit | new `ConditionalCountLandTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MBGAP-02 Verge/Vivid/MSH-cycle classification | — | N/A | unit | `dotnet test --filter FullyQualifiedName~ManabaseClassifierTests` | ✅ extend | ⬜ pending |
| TBD | TBD | TBD | MBGAP-02 accuracy-bundle-off parity | — | N/A | unit | existing accuracy parity tests | ✅ extend | ⬜ pending |
| TBD | TBD | TBD | MBGAP-02 oracle-regex canaries | — | N/A | unit | extend `ManabaseLiveOracleCanaryTests.cs` (one canary per new regex) | ✅ extend | ⬜ pending |
| TBD | TBD | TBD | MBGAP-03 ritual credit term | — | N/A | unit | extend `CedhLandTargetHybridTests.cs` or sibling `RitualLandCreditTests` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | MBGAP-03 flag seed/catalog | — | N/A | unit | `dotnet test --filter "FullyQualifiedName~FeatureFlagCatalogTests\|FeatureFlagStoreSeedTests"` | ✅ extend | ⬜ pending |
| TBD | TBD | TBD | MBGAP-05a-d verdict wording | — | N/A | unit | `dotnet test --filter FullyQualifiedName~ManabaseVerdictSynthesizerTests` | ✅ extend | ⬜ pending |
| TBD | TBD | TBD | MBGAP-05 page/txt/swap propagation | — | N/A | unit+e2e | `ManabaseReportTextBuilderTests` / `ManabaseSwapPromptBuilderTests` + `manabase-verdict.spec.ts` | ✅ extend | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] New unit test file for per-trial `ConditionalCountLand` sim primitive (fast/slow/ELD) — new `CardKind`, no existing coverage
- [ ] New/extended test for ritual land-target credit term in `KarstenManabase`
- [ ] New flag-parity test for `analysis.manabase.restricted-lands` (mirror `ritual-burst-mana` byte-identical pattern)
- [ ] New/extended Playwright spec for restricted-land disclosure marker
- [ ] Canary assertions in `ManabaseLiveOracleCanaryTests.cs` for every new oracle-text regex (H1 lesson: 2024 rewording rotted a predicate for ~a year with green tests)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Calibration sanity delta | MBGAP-03 | CLI harness over 1597-deck corpus, judgment call on under-flag% | `dotnet run --project DeckFlow.CLI -- cedh-land-calibrate ...`; compare to floor-22/blend-0.5 precedent bars |
| Karsten 2022 threshold decision doc | MBGAP-04 | Research spike, no code by default | Doc review; resolve manabase-math.md "[H, verbatim]" vs EF2 L14 "unconfirmed" |
| Help doc accuracy re-audit | MBGAP-11 | Markdown prose accuracy has no automated test | Line-by-line against `docs/manabase-analysis-rules.md` |
| Lens visual verify, 2 viewports | MBGAP-12 | Pixel/visual judgment beyond functional specs | Playwright screenshots desktop+mobile, tap-analyzer + mulligan lenses, review renders |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
