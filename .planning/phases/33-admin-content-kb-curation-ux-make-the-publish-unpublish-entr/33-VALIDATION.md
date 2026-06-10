---
phase: 33
slug: admin-content-kb-curation-ux
status: validated
nyquist_compliant: partial
wave_0_complete: n/a
created: 2026-06-09
---

# Phase 33 — Validation Strategy

> Retroactive Nyquist audit (State B reconstruction). Phase 33 ships only
> client-side TypeScript DOM filtering (KBUX-01) and Razor/CSS readability +
> responsive rules (KBUX-02). No controller, model, or other server behavior
> changed — verified against both SUMMARYs (`key-files` modified are
> `Index.cshtml`, `content-kb-admin.ts`, `admin-common.css`, `admin-mobile.css`).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework (server)** | xUnit 2.9.3 (DeckFlow.Core.Tests, DeckFlow.Web.Tests) |
| **Framework (client)** | Vitest 3.x + jsdom (added 2026-06-09, user-authorized) — `DeckFlow.Web/package.json`, run via `cd DeckFlow.Web && npm test` |
| **Config file** | `DeckFlow.Web/vitest.config.ts` (jsdom env); browser compile still `DeckFlow.Web/tsconfig.json` (module:none) |
| **Build gate** | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -clp:ErrorsOnly` (compiles TS via MSBuild target) |
| **Estimated runtime** | vitest ~13s · build ~2-30s |

The two xUnit test projects exercise C# domain/service/controller code. Phase 33
touched no C# — its entire surface is browser DOM filtering and CSS. As of
2026-06-09 the **pure filter logic** (KBUX-01 match / live-count / empty-state
predicates) is extracted into the `DeckFlowKbFilter` seam and covered by Vitest
(`ts-tests/kb-entry-filter.test.ts`, 7 tests). The DOM wiring of `wireEntryFilter`
(script-order load, event binding) and all KBUX-02 CSS rendering remain
browser-visual / manual — Vitest+jsdom does not assert real CSS layout.

---

## Sampling Rate

- **Build gate:** `dotnet build DeckFlow.Web` must compile (TS + Razor) clean — confirmed `Build succeeded. 0 Warning(s), 0 Error(s).` in 33-02 SUMMARY.
- **Behavior verification:** browser visual-verify (manual). No automated client harness exists.

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Secure Behavior | Test Type | Automated Command | Status |
|---------|------|------|-------------|-----------------|-----------|-------------------|--------|
| 33-01-01 | 01 | 1 | KBUX-01 | filter input + `data-kb-search` row attrs + empty-state tbody (Razor auto-encoded) | manual (browser) | none — markup | ✅ visual-verified 2026-06-09 |
| 33-01-02 | 01 | 1 | KBUX-01 | filter match / live-count / empty-state **logic** (`DeckFlowKbFilter` seam) | unit (vitest) | `cd DeckFlow.Web && npm test` | ✅ 7/7 green 2026-06-09 |
| 33-01-02b | 01 | 1 | KBUX-01 | DOM wiring of `wireEntryFilter` (event bind, script-order global load) | manual (browser) | none — jsdom ≠ live page | ✅ visual-verified 2026-06-09 |
| 33-01-03 | 01 | 1 | KBUX-01 | filter-bar / count / empty-state CSS (admin-shell scoped) | manual (visual) | none — CSS render | ✅ visual-verified 2026-06-09 |
| 33-02-01 | 02 | 1 | KBUX-02 | zebra + hover/`:focus-within` + sticky header (`admin-common.css`) | manual (visual) | build compiles | ✅ visual-verified 2026-06-09 |
| 33-02-02 | 02 | 1 | KBUX-02 | mobile card-layout safety overrides (`admin-mobile.css`) | manual (responsive) | build compiles | ✅ visual-verified 2026-06-09 |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure does not cover client-side behavior, and no JS test
framework may be added (CLAUDE.md dependency rule). No Wave 0 server tests apply —
Phase 33 changed no C#. Build gate (TS compile + Razor) is the only automated check.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Typing in the filter input instantly narrows KB rows across title/source/tags; live count updates; zero matches shows in-table empty-state row | KBUX-01 | Client-side DOM filtering; no JS test runner in project, adding one forbidden | Open `/Admin/ContentKb`, type a known title fragment → only matching rows + correct count; type gibberish → empty-state row shows |
| Entries table readability: zebra striping, row hover/focus highlight, header sticky on scroll | KBUX-02 | CSS render — visual-only, not assertable from .NET tests | Scroll the KB table → header stays pinned; alternate rows shaded; hovering a row highlights it |
| Mobile card layout: rows stack as cards, per-card zebra, no broken sticky header | KBUX-02 | Responsive CSS render at narrow viewport | View `/Admin/ContentKb` at ≤mobile width → rows become cards, header not sticky, card-level focus highlight intact |

---

## Validation Sign-Off

- [x] Per-task map built from both SUMMARYs
- [x] Test infrastructure detected — no client test runner; server xUnit projects don't reach this surface
- [x] All 5 tasks visual-verified in browser 2026-06-09 (see project memory `project_phase33_shipped`)
- [x] Build gate green (33-02 SUMMARY: 0 warnings, 0 errors)
- [x] Automated coverage added 2026-06-09 — KBUX-01 filter logic under Vitest (7/7)
- [ ] `nyquist_compliant: true` — **not fully achievable**: KBUX-01 DOM wiring + KBUX-02 CSS render are visual/live-page only and cannot be machine-asserted. Recorded as `partial` (highest-value logic automated; remainder human-verified).

**Approval:** approved 2026-06-09 — PARTIAL (KBUX-01 logic automated via Vitest; DOM wiring + CSS human-verified)

---

## Validation Audit 2026-06-09 (initial)
| Metric | Count |
|--------|-------|
| Gaps found | 5 (all client-side) |
| Resolved (automated) | 0 |
| Escalated to manual-only | 5 |

Reconstructed from artifacts (no prior VALIDATION.md). At audit time no JS test
runner existed. All five tasks were browser visual-verified on 2026-06-09.

## Validation Audit 2026-06-09 (revised — runner added)
| Metric | Count |
|--------|-------|
| Gaps re-examined | 5 |
| Resolved (automated) | 1 (KBUX-01 filter logic → Vitest, 7 tests) |
| Remaining manual-only | 4 (Razor markup, DOM wiring, two CSS surfaces) |

User authorized adding a JS test runner (Vitest + jsdom). The KBUX-01 filter
predicates were extracted into the pure `DeckFlowKbFilter` seam and unit-tested
(`ts-tests/kb-entry-filter.test.ts`). Browser bundle still compiles under
`tsc` (module:none) and the .NET build is `0/0`. Remaining items are inherently
visual (CSS render) or require a live page (DOM wiring) and stay manual-only.
Status remains `partial`: automated coverage now exists for the highest-value
logic, but not all behavior is machine-assertable.
