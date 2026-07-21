---
phase: 105
slug: builder-compatible-export
status: mapped
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-21
---

# Phase 105 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source of truth: `105-RESEARCH.md` §"Reuse Map" + §"Security Domain" + `105-CONTEXT.md` D1–D3.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`), Vitest (jsdom, `DeckFlow.Web/ts-tests/**/*.test.ts`), Playwright (`DeckFlow.Web/e2e/*.spec.ts`) |
| **Config file** | `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`; `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`; `DeckFlow.Web/vitest.config.ts`; Playwright config alongside `e2e/` |
| **Quick run command** | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CutLabExportComposer"` + `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"` + `npx --no-install vitest run cut-lab` |
| **Full suite command** | `dotnet build` (clean) + `dotnet test DeckFlow.Core.Tests` + `dotnet test DeckFlow.Web.Tests` + `npx --no-install vitest run` + `npx --no-install playwright test e2e/cut-lab*.spec.ts` (via `scripts/run-web-test.sh`) |
| **Estimated runtime** | ~90 seconds (quick ~20s; full incl. e2e ~90s) |

---

## Sampling Rate

- **After every task commit:** Run the matching quick filter — Core composer/exporter/diff (`dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CutLabExportComposer|FullyQualifiedName~Exporter|FullyQualifiedName~DiffEngine"`) or Web (`dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"`) or TS (`npx --no-install vitest run cut-lab`).
- **After every plan wave:** Run the full suite (`dotnet build` clean + both xUnit projects + Vitest + Playwright `cut-lab*` specs).
- **Before `/gsd:verify-work`:** Full suite must be green including `cut-lab-export.spec.ts`.
- **Max feedback latency:** ~20 seconds (quick), ~90 seconds (full).

---

## Per-Task Verification Map

> Task IDs assigned by the planner; requirement → test-type mapping is fixed here.

| Requirement | Behavior | Test Type | Automated Command | File Exists |
|-------------|----------|-----------|-------------------|-------------|
| EXPORT-02 | `OriginalEntries` round-trips through the serializer; pre-105 blobs deserialize to empty list; field is Take-clamped (105-01 T1) | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CutLabStateSerializer` | ✅ file / ❌ new cases |
| EXPORT-02 | `OriginalEntries` captured once from full-fidelity intake, not overwritten by later posts, survives scenario reload (105-01 T2) | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CutLabOriginalEntries` | ❌ W0 (new file) |
| EXPORT-03 | `ScryfallCardData.ColorIdentity` populated by mapper from already-resolved card; null identity maps to null (105-02 T1) | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~ScryfallCardDataMapper` | ✅ file / ❌ new cases |
| EXPORT-03 | `CommanderIdentityCheck` returns Legal / Illegal / Unverified (null ≠ legal); colorless legal in any identity (105-03 T1) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CutLabExportComposer` | ❌ W0 (new file) |
| EXPORT-01 | Composer emits non-empty Moxfield AND Archidekt full-list text for a Cut-Lab-shaped final list (105-03 T2) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CutLabExportComposer` | ❌ W0 |
| EXPORT-01 | Archidekt-target full-list export from a Cut-Lab-shaped entry list (commander `[Commander]` suffix) (105-03 T2) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~Exporter` | ✅ file / ❌ new case |
| EXPORT-02 | Cut-only patch: final ⊂ original → CUT names all removals, ADD empty/noted, both dialects (105-03 T2) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CutLabExportComposer` | ❌ W0 |
| EXPORT-02 | Diff cut-only scenario: `ToAdd` empty, `OnlyInArchidekt` = the cuts (105-03 T2) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~DiffEngine` | ✅ file / ❌ new case |
| EXPORT-02 | Swap-introduced add appears in ADD section (105-03 T2) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CutLabExportComposer` | ❌ W0 |
| EXPORT-03 | Count gate: ==100 → HardBlock false; ≠100 → HardBlock true + OffCount reported (105-03 T2) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CutLabExportComposer` | ❌ W0 |
| EXPORT-03 | Illegal vs Unverified color-identity land in SEPARATE buckets (no overlap); banlist offender named; none set HardBlock (105-03 T2) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CutLabExportComposer` | ❌ W0 |
| EXPORT-01 | Board-normalized full list: kept sideboard AND maybeboard cards land in the 100-card mainboard export (Moxfield + Archidekt), none dropped/mis-placed (105-03 T2 composer + 105-04 T1 service) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CutLabExportComposer` + `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CutLabExportServiceTests` | ❌ W0 |
| EXPORT-02 | Quantity-decrease cut: original `10 Forest` / final `7 Forest` → `CUT 3 Forest` (CountMismatch), plus a normal single-card cut (105-03 T2) | unit | `dotnet test DeckFlow.Core.Tests --filter "FullyQualifiedName~CutLabExportComposer|FullyQualifiedName~DiffEngine"` | ❌ W0 |
| EXPORT-01 | Quantity-sum on export: duplicate-equivalent reconstructed entries consolidate so exported full list totals 100 (105-03 T2) | unit | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CutLabExportComposer` | ❌ W0 |
| EXPORT-01/02/03 | Service reconstructs final entries from `OriginalEntries` (fallback→mainboard + warning); banlist re-check surfaced non-blocking; fail-open on `HttpRequestException`; no redundant Scryfall resolution (105-04 T1) | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CutLabExportServiceTests` | ❌ W0 (new file) |
| EXPORT-01/02/03 | New `ICutLabExportService` ctor dep does not break controller tests (shared `CreateController` helper supplies a fake); `POST /cut-lab/export` action rehydrates state + attaches export VM; off-count POST returns panel not 4xx (105-04 T1) | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CutLabControllerTests` | ✅ file / ❌ helper update + new cases |
| EXPORT-01/02/03 | Export panel renders 4 copy blocks + 3-check summary; step-tab disabled affordance; Export tab is a server-POST submit activator (`type=submit form=cut-lab-export-form`, no client step-toggle handler); copy-to-clipboard handler; TS compiles (105-04 T2) | unit (Vitest) + tsc | `cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit && npx --no-install vitest run cut-lab` | ✅ file / ❌ snapshot update |
| EXPORT-01/02/03 | End-to-end: below-100 greyed/blocked; reach 100 → **activate Export tab (submit → server POST /cut-lab/export)** → Export panel + validation summary populate → both-dialect full list + CUT/ADD patch + summary; warnings named without disabling (105-05 T1) | e2e | `npx --no-install playwright test e2e/cut-lab-export.spec.ts` | ❌ W0 (new spec) |

*Status legend: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `DeckFlow.Web.Tests/CutLabOriginalEntriesTests.cs` — EXPORT-02 capture-once + scenario-reload survival (flat path, mirrors existing `CutLab*Tests.cs`)
- [ ] `DeckFlow.Core.Tests/CutLabExportComposerTests.cs` — EXPORT-01/02/03 composer + `CommanderIdentityCheck` (also hosts the identity-check first cases)
- [ ] `DeckFlow.Web.Tests/CutLabExportServiceTests.cs` — EXPORT-01/02/03 Web orchestrator: reconstruction fallback warning, banlist re-check non-blocking, banlist fail-open, no redundant Scryfall resolution (flat path)
- [ ] `DeckFlow.Web/e2e/cut-lab-export.spec.ts` — end-to-end reach-100 → export round trip (mirrors `cut-lab-whatif.spec.ts`)
- [ ] Extend existing cases: `ScryfallCardDataMapperTests.cs` (color_identity), `CutLabStateSerializerTests.cs` (OriginalEntries), `ExporterTests.cs` (Archidekt Cut-Lab shape), `DiffEngineTests.cs` (cut-only + quantity-decrease), `CutLabControllerTests.cs` (CreateController helper supplies fake export service + `/cut-lab/export` action test), cut-lab Vitest snapshot (Export panel DOM)
- [ ] Framework install: **none** — xUnit / Vitest / Playwright all already present.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Export panel + Export step tab render correctly across guild themes (Classic/Nyx) on desktop and mobile | EXPORT-01/02/03, D1 | Visual/theme correctness not asserted by unit/e2e | Playwright screenshots at 2 viewports × ≥2 themes; cross-AI UI review per project rule |
| Copied Moxfield/Archidekt full list + CUT/ADD patch paste into the real builders with ZERO reformatting (the phase's core value) | EXPORT-01/02 | Real builder import parsers ≠ our assertions; only a live paste proves "zero reformatting" | 105-05 human-verify checkpoint: paste each blob into Moxfield/Archidekt import during UAT |
| Merely-unresolved card is labeled "could not verify," never "illegal" | EXPORT-03 | Copy nuance best confirmed by eye | Seed an unresolved + an off-identity card during UAT; confirm the two statuses read distinctly |

---

## Validation Sign-Off

- [x] All requirements have an automated verify or a Wave 0 dependency
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (`CutLabOriginalEntriesTests`, `CutLabExportComposerTests`, `CutLabExportServiceTests`, `cut-lab-export.spec.ts`)
- [x] No watch-mode flags
- [x] Feedback latency < 90s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** mapped 2026-07-21 (revision closing plan-checker BLOCKER 1 — Nyquist gate; every Wave-0 gap has a covering plan task; `wave_0_complete` flips true once those test files land during execution)
