---
phase: 107
slug: cut-lab-tech-debt-cleanup
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-22
---

# Phase 107 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Quality/tech-debt phase — no REQUIREMENTS.md IDs. Acceptance is per cleanup item (each fixed OR
> closed-with-reason), mapped to the existing xUnit / Vitest / Playwright suites. No new frameworks
> or fixtures — existing Cut Lab test infrastructure (6 prior phases) covers every item.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`); Vitest (`DeckFlow.Web/ts-tests/`); Playwright (`DeckFlow.Web/e2e/`) |
| **Config file** | existing `.csproj` / `vitest.config.*` / `playwright.config.*` — none new |
| **Quick run command** | `dotnet build DeckFlow.sln && dotnet test DeckFlow.sln --filter "FullyQualifiedName~CutLab"` |
| **Full suite command** | `dotnet test DeckFlow.sln` + (`cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit && npx --no-install vitest run`) + (`scripts/run-web-test.sh` then `npx --no-install playwright test`) |
| **Estimated runtime** | ~180–300 seconds (full: dotnet + Vitest + Playwright e2e) |

Constraints: VSTest is unreliable in WSL — rely on `dotnet build` clean + `dotnet test` via the
project harness. UI testing must NEVER open a Windows-host browser: start via `scripts/run-web-test.sh`
(sets `DECKFLOW_DISABLE_AUTO_BROWSER=true`), drive Playwright headless in WSL. Compiled
`wwwroot/js/*.js` is gitignored — never stage it.

---

## Sampling Rate

- **After every task commit:** Run `dotnet test DeckFlow.sln --filter "FullyQualifiedName~CutLab"` (+ `npx --no-install vitest run` for TS-touching tasks).
- **After every plan wave:** Run the full suite command (dotnet + Vitest/TS + Playwright).
- **Before `/gsd:verify-work`:** Full suite green + a fresh theme×viewport screenshot pass for items 3/4.
- **Max feedback latency:** ~60 seconds (targeted CutLab filter) / ~300 seconds (full suite).

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Item / Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|--------------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 107-01-01 | 01 | 1 | Item 1 (CLEANUP-1) dead fields removed, guard re-scoped | — | N/A (internal refactor, no surface) | unit/build | `dotnet build DeckFlow.sln` | ✅ | ⬜ pending |
| 107-01-02 | 01 | 1 | Item 1 — 43 call sites reworked, analysis coverage preserved | — | N/A | unit | `dotnet test DeckFlow.sln --filter "FullyQualifiedName~CutLab"` | ✅ | ⬜ pending |
| 107-02-01 | 02 | 1 | Item 3 (CLEANUP-3) 9 dark themes delta AA tokens | — | N/A (CSS tokens) | grep/manual | `for f in abzan dimir esper golgari grixis jund planeswalker-dark rakdos sultai; do grep -c -- "--cutlab-delta-up:\|--cutlab-delta-down:" DeckFlow.Web/wwwroot/css/site-$f.css; done` | ✅ | ⬜ pending |
| 107-02-02 | 02 | 1 | Item 4 (CLEANUP-4) button-scoped pill rule + mobile label | — | N/A (CSS/attr) | build/grep | `dotnet build DeckFlow.sln && grep -c "button\.manabase-pill\.is-selected" DeckFlow.Web/wwwroot/css/site-common.css` | ✅ | ⬜ pending |
| 107-02-03 | 02 | 1 | Item 3/4 visual + Nyx-badge decisive outcome | — | N/A | manual/screenshot | theme×viewport Playwright screenshot pass (`scripts/run-web-test.sh` + `npx --no-install playwright test`) | ✅ | ⬜ pending |
| 107-03-01 | 03 | 2 | Item 2 (CLEANUP-2) commander-inclusive chip single-source | — | N/A (display string) | unit | `dotnet test DeckFlow.sln --filter "FullyQualifiedName~CutLabViewModel"` + `npx --no-install vitest run` | ✅ | ⬜ pending |
| 107-03-02 | 03 | 2 | Item 5 (CLEANUP-5) pluralizer + path-base form+fetch + cacheKey | — | Path-base-safe decide POST (same-origin route via Url.Content) | unit/grep | `dotnet test DeckFlow.sln --filter "FullyQualifiedName~CutLabViewModelWording"` + grep both hardcoded paths gone | ✅ | ⬜ pending |
| 107-04-01 | 04 | 3 | Item 6 (CLEANUP-6) serialize findings into decide response | T-107-06 | Server-computed output only; no new request field on same-origin-guarded endpoint | unit | `dotnet test DeckFlow.sln --filter "FullyQualifiedName~CutLabApiControllerTests"` | ✅ | ⬜ pending |
| 107-04-02 | 04 | 3 | Item 6 body-scoped client renderer (injection-safe) | T-107-06 | textContent/typed DOM only — no innerHTML with raw data | unit | `cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit && npx --no-install vitest run` | ✅ | ⬜ pending |
| 107-04-03 | 04 | 3 | Item 6 live-patch without reload | T-107-06 | N/A (client render of validated data) | e2e | `scripts/run-web-test.sh` + `npx --no-install playwright test e2e/cut-lab-structure.spec.ts` | ✅ | ⬜ pending |
| 107-04-04 | 04 | 3 | Phase gate — all six items fixed-or-closed, full suite green | — | No compiled js staged; no EOL churn | full | `dotnet test DeckFlow.sln && (cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit && npx --no-install vitest run)` + full Playwright + `git diff --check` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

Threat note: `security_enforcement` is enabled but this phase introduces NO new input surface
(RESEARCH Security Domain). The only threat reference (T-107-06) is the item-6 live-DOM injection
concern, mitigated by the textContent/typed-DOM requirement in 107-04 Task 2.

---

## Wave 0 Requirements

Existing infrastructure covers all phase items — no Wave 0 scaffolding needed. Every item extends an
existing test file:
- Item 1 → `DeckFlow.Web.Tests/CutLabPageServiceTests.cs`, `CutLabOriginalEntriesTests.cs` (rewrite in place).
- Item 2 → `DeckFlow.Web.Tests/CutLabViewModelWordingTests.cs` (new assertion) + `cut-lab-*.test.ts` (Vitest).
- Item 3/4 → visual-only (no automated contrast test in repo — theme×viewport screenshot pass per project convention).
- Item 5 → `CutLabViewModelWordingTests.cs` + grep gates for hardcoded paths.
- Item 6 → `DeckFlow.Web.Tests/CutLabApiControllerTests.cs` + `DeckFlow.Web/ts-tests/cut-lab-proposal.test.ts` + `DeckFlow.Web/e2e/cut-lab-structure.spec.ts` (extend all three).

---

## Manual-Only Verifications

| Behavior | Item | Why Manual | Test Instructions |
|----------|------|------------|-------------------|
| Dark-theme delta up/down colors legible at AA on `--panel` | Item 3 | No automated WCAG-contrast tooling in repo (project convention = visual review); contrast pre-verified by closed-form math in RESEARCH | Theme×viewport Playwright screenshots of the Compare panel deltas on a dark theme (sultai / planeswalker-dark). |
| Lock-all BUTTON pill selected-state distinct; radio-pill `:has()` selection has no double-highlight | Item 4 | Renders/interaction depend on real paint; radio-pill regression risk is visual | Screenshot the Lock-all button locked vs unlocked; click between Bracket/PlayExperience radios to confirm single highlight. |
| Nyx-mobile commander badge overlap | Item 4 | Overlap depends on font metrics / real layout; must end fixed-or-closed | Nyx mobile-viewport screenshot; if overlap → apply CSS fix around site-common.css:1254-1257 and re-shoot; else close-with-screenshot. |
| Mobile pool-row "Package" label not truncated | Item 4 | Real text-truncation depends on font/viewport | Mobile-viewport screenshot of the pool row. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or are visual-only with a documented screenshot pass (items 3/4)
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify (items 3/4 visual are bounded by build/grep gates on either side)
- [ ] Wave 0 covers all MISSING references (none — existing infra)
- [ ] No watch-mode flags (all `vitest run` / `playwright test`, non-watch)
- [ ] Feedback latency < 300s (full suite)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
