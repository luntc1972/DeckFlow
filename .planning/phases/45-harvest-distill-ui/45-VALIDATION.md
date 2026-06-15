---
phase: 45
slug: harvest-distill-ui
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-15
---

# Phase 45 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: 45-RESEARCH.md "Validation Architecture" section.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Core.Tests` + `DeckFlow.Web.Tests`). Studio has NO test project and none will be added (CLAUDE.md: no new frameworks). |
| **Config file** | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` |
| **Quick run command** | `dotnet build DeckFlow.sln` (zero errors, zero new warnings) |
| **Full suite command** | `dotnet test DeckFlow.Core.Tests/` |
| **Estimated runtime** | build ~30s; Core.Tests suite ~60–90s |

> **WSL caveat (CLAUDE.md):** VSTest is unreliable in WSL. Primary feedback signal is
> `dotnet build` clean. Core unit tests are the automated gate for the two Core-side
> changes (ledger interface, badge resolver). Razor/Blazor component behavior is
> manual-smoke only — no bUnit, no Studio test project.

---

## Sampling Rate

- **After every task commit:** Run `dotnet build DeckFlow.sln` — zero errors, zero new warnings.
- **After every plan wave:** Run `dotnet test DeckFlow.Core.Tests/` — full pass.
- **Before `/gsd:verify-work`:** Build clean + browser smoke of all 5 HARV success criteria.
- **Max feedback latency:** ~90 seconds (Core.Tests suite).

---

## Per-Task Verification Map

| Req ID | Behavior | Test Type | Automated Command | File Exists | Status |
|--------|----------|-----------|-------------------|-------------|--------|
| HARV-01 | Channel browse lists recent videos via `IYouTubeChannelVideoLister` | manual (Blazor component — browser smoke) | Start Studio, paste channel handle, verify table renders | N/A — component | ⬜ pending |
| HARV-02 | URL/bare-ID paste resolves via lister; invalid IDs surface user error; local-DB dupes flagged | unit (Core) + manual | `dotnet test DeckFlow.Core.Tests/ --filter "YouTubeChannelVideoLister"` | Existing — verify coverage | ⬜ pending |
| HARV-03 | Per-video status badge: blocked / distilled / harvested / not-harvested (resolve via `IContentSiteIndexStore.GetByNaturalKeyAsync` proxy) | unit (if resolver extracted to static/helper) else manual | extract `ResolveStatusAsync` → `dotnet test DeckFlow.Core.Tests/` | ❌ W0 (if extracted) | ⬜ pending |
| HARV-04 | Harvest live progress reaches UI without circuit freeze; CTS cancels on component `Dispose` | manual (browser smoke — start harvest, watch live log, close tab → op stops) | Browser verify | N/A — component | ⬜ pending |
| HARV-05 | Dry-run spend projection card; re-distill needs explicit secondary confirm; actual spend shown post-run; monthly cap enforced | unit (cap math, Core) + manual (flow) | `dotnet test DeckFlow.Core.Tests/ --filter "LlmSpendLedger"` + browser | ❌ W0 (new cap method) | ⬜ pending |
| D-02 | `ILlmSpendLedger.GetMonthlyCapUsd()` returns env/default cap (promote private `ReadMonthlyCapUsd`) | unit (Core) | `dotnet test DeckFlow.Core.Tests/ --filter "LlmSpendLedger"` | ❌ W0 | ⬜ pending |
| D-03 | `SessionCapOverride` raises effective cap for `WouldExceedCapAsync` | unit (Core) | `dotnet test DeckFlow.Core.Tests/ --filter "SpendLedger"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Unit test for `ILlmSpendLedger.GetMonthlyCapUsd()` new interface method — covers D-02
- [ ] Unit test for `SessionCapOverride` resolver affecting `WouldExceedCapAsync` — covers D-03
- [ ] (Optional) Unit test for extracted `ResolveStatusAsync` badge helper if pulled out of Razor — covers HARV-03
- [ ] Manual-smoke checklist doc for the 5 HARV criteria (Harvest.razor not unit-testable: no bUnit, VSTest WSL-unreliable, no Studio test project)

*No Wave 0 framework install needed — xUnit already present in Core.Tests and Web.Tests.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Channel browse renders recent-video table with status badges | HARV-01 | Blazor component render; no Studio test project | Start Studio (`DECKFLOW_DISABLE_AUTO_BROWSER=true`), paste channel handle/URL, confirm table lists videos each with a badge; already-harvested visually distinct |
| Live harvest progress without tab freeze + cancel-on-dispose | HARV-04 | Background-task + `InvokeAsync(StateHasChanged)` + CTS-on-Dispose only observable at runtime | Trigger harvest on ≥2 videos; confirm log lines stream live (tab responsive); close/navigate away → in-flight op stops (no orphan work) |
| Dry-run spend gate + re-distill double-confirm + actual spend | HARV-05 | Multi-step UI flow with secondary confirmation | Run dry-run → see projected spend card; attempt re-distill of already-distilled video → must check secondary confirm before `dryRun:false` enabled; complete live distill → actual spend shown; cap enforced |

---

## Validation Sign-Off

- [ ] All tasks have automated verify or Wave 0 dependency, OR are explicitly Manual-Only above
- [ ] Sampling continuity: no 3 consecutive tasks without a build/test signal
- [ ] Wave 0 covers all MISSING references (cap method, session override)
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
