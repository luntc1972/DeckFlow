---
phase: 70
slug: manabase-accuracy-mana-quantity
status: validated
nyquist_compliant: false
wave_0_complete: true
created: 2026-06-24
---

# Phase 70 — Validation Strategy

> Reconstructed from artifacts (no SUMMARY.md present; phase executed via commits
> `043a9157`→`8ea0dea3`). Per-requirement coverage audited against the implemented test suite.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (.NET 10) — `DeckFlow.Core.Tests`, `DeckFlow.Web.Tests`; Playwright (e2e, `DeckFlow.Web/e2e/`) |
| **Config file** | none — solution-level `dotnet test` |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj --filter "FullyQualifiedName~Manabase" -v q -nologo` |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~Manabase|FullyQualifiedName~FeatureFlagStoreSeed" -v q -nologo` |
| **Estimated runtime** | ~15s Core / ~20s Web |

> Note: `dotnet` is not on PATH in WSL — use the `dotnet.exe` absolute path. VSTest runs here (Core manabase 131 green, Web manabase+seed 81 green at audit time 2026-06-24).

---

## Sampling Rate

- **After every task commit:** Run the quick (Core manabase) command
- **After every plan wave:** Run the full (Web manabase + seed) command
- **Before `/gsd:verify-work`:** Both suites green + Playwright manabase specs
- **Max feedback latency:** ~20 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Requirement | Secure Behavior | Test Type | Automated Command (filter) | File Exists | Status |
|---------|------|-------------|-----------------|-----------|----------------------------|-------------|--------|
| MQ-01 | 70-01 | Commander not drawn into simulated library; kept in color supply | Commander mana source excluded from drawable 99, still counted in `EffectiveSources` | unit | `~Analyze_CommanderOnlyColorSource_NotDrawnIntoLibrary` ; `~Classify_Commander_IsFlaggedOnSpellRequirement` ; `~Classify_GranterWithCommander` | ✅ | ✅ green |
| MQ-02 | 70-02 | Per-source mana QUANTITY (Sol Ring=2, Lotus=3) on affordability only; Karsten color counts invariant | Multi-mana source pays N pips of ONE color; cannot split colors; color counts unchanged; flag-off byte-identical | unit | `~ManaQuantityTests` (11 incl `GildedLotus_CannotPayThreeDifferentColoredPips`, `ColorFindings_AreInvariant_ToManaQuantityFlag`) | ✅ | ✅ green |
| MQ-02 (flag plumbing) | 70-02 | `manabase.source-mana-quantity` read via `IsFlagOn`, fail-safe OFF, raises cast% ON | Service threads `useManaQuantity` into analyzer; absent cache → OFF | unit | `~AnalyzeAsync_SourceManaQuantityFlag` | ✅ (added 2026-06-24) | ✅ green |
| MQ-03 | 70-03 | Ramp-credit narrowed to repeatable ramp; one-shot rituals/Treasures dropped (flag `ramp-credit-v2`) | v2 keep-rule drops Instant/Sorcery "Add"; flag-off matches broad predicate | unit | `~ManaRampCreditTests` ; `~AnalyzeAsync_RampCreditV2Flag` | ✅ | ✅ green |
| MQ-03b | 70-03b | Model land-ramp in sim as colorless ramp source (flag `land-ramp-sim`); colorless-only, self-excluded | Land-ramp adds colorless source at MV deploy cost; color counts + land total invariant; flag-off byte-identical | unit | `~LandRampSimTests` (7) ; `~AnalyzeAsync_LandRampSimFlag` | ✅ | ✅ green |
| MQ-04 | (CONTEXT) | Disclose unsupported interactions (hybrid/Phyrexian/X) instead of silently absorbing | X/hybrid surfaced in `UnsupportedInteractions`; no hard pips created | unit | `~Analyze_UnsupportedInteractions_SurfacesXAndHybridCards` ; `~Parse_HybridAndX_DoNotCreateHardPips` | ✅ | ✅ green |
| MQ-05 | 70-05 | Color-aware London mulligan (flag `color-aware-mulligan`); additive to land-count band | Multi-color opener of one color is mulliganed; mono identical even flag-on; verdict math invariant | unit | `~ColorAwareMulliganTests` (10) ; `~AnalyzeAsync_ColorAwareMulliganFlag` | ✅ | ✅ green |
| Flag seed | 70-02/03/05/03b | All four manabase flags seeded (default ON post-baseline) in SQLite + Postgres | Seed contract holds across dialects | unit | `~FeatureFlagStoreSeedTests` | ✅ | ✅ green |
| 70-06 | 70-06 | Two-lens result header (Karsten source check + simulated cast rate) | `AvgOnCurve` mean (0 on empty, no div-by-zero); `KarstenMet` raw value + clamped deficit | unit | `~ManabaseDisplayTests` (`AvgOnCurve_*`, `KarstenMet_*`) | ✅ | ✅ green |
| 70-06 (render) | 70-06 | Two-lens band renders above lands/health; mobile stacks; no theme overflow | Live `.manabase-twolens` band on a real analysis | e2e/manual | — | ❌ | ⚠️ manual-only |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky/manual*

---

## Wave 0 Requirements

Existing infrastructure covers all automated phase requirements. One sibling-parity test
(`AnalyzeAsync_SourceManaQuantityFlag_RaisesAffordability_FailsSafeOff`) was added during this
audit (2026-06-24) to match the plumbing coverage the other three manabase flags already had.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Two-lens result band live render (desktop 2-up, mobile stack, no theme overflow) | 70-06 | Needs a live Scryfall analysis (form-POST → ~7.6s upstream fetch). The Playwright suite intentionally avoids Scryfall in CI; gstack daemon was unstable for the form-POST nav (documented in 70-06-PLAN OPEN). | Start `DeckFlow.Web` on :5173 with `DECKFLOW_DISABLE_AUTO_BROWSER=true`; `/manabase` → "Paste decklist" tab → fill `#manabase-deck-text` with a real decklist → click **Analyze Mana Base** → screenshot the `.manabase-twolens` band desktop + mobile + across a couple themes (set the `deckflow-theme` cookie). Confirm band sits above the lands/health line, left lens ✓/⚠ matches `ColorFindings`, mobile stacks, no horizontal overflow. |

---

## Validation Audit 2026-06-24

| Metric | Count |
|--------|-------|
| Requirements audited | 7 (MQ-01..05 + MQ-03b + 70-06) + flag-seed contract |
| Fully automated (COVERED) | 8 of 9 verification rows |
| Gaps found | 2 |
| Resolved (auto-filled) | 1 — MQ-02 web flag-plumbing test |
| Escalated to manual-only | 1 — 70-06 two-lens live render (Scryfall/CI constraint) |

---

## Validation Sign-Off

- [x] All requirements have automated verify (except the one documented manual-only render check)
- [x] Sampling continuity: no 3 consecutive requirements without automated verify
- [x] Wave 0 covers all MISSING references (one parity test added)
- [x] No watch-mode flags
- [x] Feedback latency < 25s
- [ ] `nyquist_compliant: true` — **PARTIAL**: one manual-only render verification remains (70-06)

**Approval:** validated (partial) 2026-06-24 — 8/9 automated, 1 manual-only.
