---
phase: 84
slug: theme-semantic-token-migration
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-04
---

# Phase 84 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Pure CSS custom-property migration — validation is computed-style assertions (Playwright)
> + build/format gates. No new server logic.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Playwright (e2e, WSL→Windows dotnet.exe headless) + xUnit (build guard) |
| **Config file** | `DeckFlow.Web/playwright.config.ts`; e2e specs in `DeckFlow.Web/e2e/` |
| **Quick run command** | `dotnet build DeckFlow.sln` (clean = no CSS syntax break) |
| **Full suite command** | `scripts/run-web-test.sh` then `npx --no-install playwright test theming` |
| **Estimated runtime** | build ~40s; theming e2e ~2-4 min across 27 themes |

---

## Sampling Rate

- **After every task commit:** `dotnet build DeckFlow.sln` (fast structural gate)
- **After every plan wave:** `npx --no-install playwright test theming` against headless server
- **Before `/gsd:verify-work`:** Full theming e2e green + visual spot-check screenshots captured
- **Max feedback latency:** ~40s (build); e2e reserved for wave boundaries

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 84-01-* | 01 | 1 | THEME-01 | — | N/A (static CSS) | build + computed-style | `dotnet build` / `playwright test theming` | ✅ existing | ⬜ pending |
| 84-02-* | 02 | 2 | THEME-02 | — | N/A | computed-style (danger ≠ link per red theme) | `playwright test theming` | ⚠️ extend theming.spec.ts | ⬜ pending |
| 84-03-* | 03 | 2 | THEME-03 | — | N/A | visual spot-check + diff | manual screenshots 27 themes × light/dark | manual | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Extend `DeckFlow.Web/e2e/theming.spec.ts` — add THEME-02 assertion: in each red guild
      theme, computed `--danger` color ≠ computed `--link` color, and `.feedback-error`
      resolves to `--danger`. (Extends existing Tier-1/Tier-2 pattern — do NOT build new tooling.)

*Existing theming e2e infrastructure covers THEME-01/03 structural checks.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| No non-error surface changed color | THEME-03 | Perceptual diff across 27 themes not fully automatable | Start headless server (`scripts/run-web-test.sh`); screenshot each theme light+dark at desktop (1280) + mobile (390); diff against pre-migration baseline; only the ~8 documented D1 shifts may differ |
| Error/danger visibly distinct from link in red themes | THEME-02 | Human color-perception confirmation | Load a page with an error surface in rakdos/boros/gruul/mardu/jund; confirm danger text is not the link color, desktop + mobile |

---

## Validation Sign-Off

- [x] All tasks have automated verify (build/computed-style) or documented manual step
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 extends theming.spec.ts for THEME-02 (assigned to Plan 84-02 Task 1)
- [x] No watch-mode flags (CI/headless only; DECKFLOW_DISABLE_AUTO_BROWSER=true)
- [x] Feedback latency < 45s for build gate
- [x] `nyquist_compliant: true` set once planner maps every task

**Approval:** approved 2026-07-04
