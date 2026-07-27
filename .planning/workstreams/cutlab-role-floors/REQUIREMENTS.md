# Requirements: Cycle 21 — Commander-Aware Role Floors

**Defined:** 2026-07-26
**Core Value:** Every supported workflow must produce output the user can paste into ChatGPT/Claude/Gemini and get back a useful answer in one round-trip, without the user reformatting anything.

## Cycle 21 Requirements

### Research Validation (RFLR)

- [ ] **RFLR-01**: Per-commander role classification is reproduced using the real production classifiers (`DeckStatClassifier`, `PlanRoleClassifier` in `DeckFlow.Core`) against the Postgres corpus — not a Python reimplementation — for a defensible commander sample (more than the 4 sampled ad hoc in the prior throwaway session).
- [ ] **RFLR-02**: An explicit statistical bar (minimum deck count per commander, effect-size/spread threshold) is defined and applied to separate real per-commander role-floor divergence from corpus noise.
- [ ] **RFLR-03**: A committed findings document reports per-commander role distributions, spread magnitude per role (interaction/protection/engines/payoffs/win conditions), and which roles (if any) clear the statistical bar.
- [ ] **RFLR-04**: The findings document ends with an explicit go/no-go recommendation on building commander-aware role-floor defaults, including which roles (if any) are in scope for Phase 2.

### Commander-Aware Floor Implementation (RFLR) — conditional on RFLR-04 = go

- [ ] **RFLR-05**: Role-floor defaults for roles where RFLR-01–04 found real signal are computed via a priority chain (commander-specific data → existing bracket+plan fallback), mirroring the pattern already used for lands/ramp/draw in `CutLabFloorDefaults.cs`.
- [ ] **RFLR-06**: Existing bracket+plan-only floor behavior is preserved unchanged for commanders without sufficient corpus data, and for any role that did not clear the statistical bar.
- [ ] **RFLR-07**: New or changed floor-computation logic has unit test coverage in `DeckFlow.Core.Tests`.
- [ ] **RFLR-08**: For each role floor with commander-specific data, the Cut Lab UI shows BOTH numbers side by side — the commander-derived value and the bracket+plan-derived value — clearly labeled which is which (not just the single active value with a source tag), so the user can see and compare both at a glance. Roles/commanders without commander-specific data continue to show only the bracket+plan value, per the existing pattern.

## Out of Scope

| Feature | Reason |
|---------|--------|
| Land/ramp/draw floor logic changes | Already commander-aware via the existing priority chain; not part of this milestone |
| Bracket+plan fallback behavior changes | Preserved as-is; this milestone only adds a commander-specific layer on top |
| Any public-facing surface outside Cut Lab | Out of scope |
| Cycle 20 (Personal Tools / creator-style port) work | Separate milestone, separate branch, not touched here |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| RFLR-01 | Phase 1 | Pending |
| RFLR-02 | Phase 1 | Pending |
| RFLR-03 | Phase 1 | Pending |
| RFLR-04 | Phase 1 | Pending |
| RFLR-05 | Phase 2 (conditional) | Pending |
| RFLR-06 | Phase 2 (conditional) | Pending |
| RFLR-07 | Phase 2 (conditional) | Pending |
| RFLR-08 | Phase 2 (conditional) | Pending |

**Coverage:**
- Cycle 21 requirements: 8 total
- Mapped to phases: 8
- Unmapped: 0

---
*Requirements defined: 2026-07-26*
*Last updated: 2026-07-26*
