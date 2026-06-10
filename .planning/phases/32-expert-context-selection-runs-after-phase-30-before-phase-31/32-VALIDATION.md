---
phase: 32
slug: expert-context-selection
status: validated
nyquist_compliant: partial
wave_0_complete: n/a
created: 2026-06-09
---

# Phase 32 — Validation Strategy

> Retroactive Nyquist audit (State B reconstruction). Phase 32 (SEL-01..06) layered
> manual expert-context selection — pin videos, follow creators, evergreen flag —
> over the auto relevance scoring from Phase 30, plus an `is_evergreen` schema
> migration (SQLite + Postgres), typeahead search endpoints, and prompt-variant
> injection. The store/relevance/packet/controller logic carries broad xUnit
> coverage; the admin chip/tray UI and live-score preview are browser-visual.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Web.Tests, DeckFlow.Core.Tests) |
| **Quick run** | `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "ContentKb|ContentSiteIndex|ExpertContext|Evergreen"` |
| **Full suite** | `dotnet test DeckFlow.sln` |
| **Closeout result** | Core 270/270, Web 608 pass / 5 PG-skip / 0 fail |
| **Estimated runtime** | ~30-60s |

---

## Per-Requirement Verification Map

| Requirement | Behavior | Test File(s) | Test Type | Status |
|-------------|----------|--------------|-----------|--------|
| SEL-01, SEL-03 | `is_evergreen` column + self-healing migration (ordinal 13); `SetEvergreenAsync`; seed preserves curation | `ContentSiteIndexStoreTests`, `FakeContentSiteIndexStore` | unit | ✅ green (round-trip + preserve tests) |
| SEL-02 | Pin/follow merge into relevance set; caps + budget trim | `ContentKbRelevanceServiceTests`, `ContentKbMergedClipsTests` | unit | ✅ green |
| SEL-04 | Clip parsing / excerpt shaping for merged clips | `ContentKbClipParserTests`, `ContentKbExcerptTests` | unit | ✅ green |
| SEL-05 | Expert-context injection into analysis prompt variants; cache-key fork on selection | `DeckAnalysisPacketServiceExpertContextTests`, `AnalysisPromptVariantExpertContextTests` | unit | ✅ green |
| SEL-05, SEL-06 | Typeahead search endpoints (visible-only, Take(10), SameOrigin); admin set-evergreen/visibility (CSRF + BasicAuth) | `ContentKbControllerTests`, `AdminContentKbControllerTests` | unit | ✅ green |
| SEL-02, SEL-06 (UI) | "What Experts Say" chip/tray UI, admin live-score preview, localStorage persistence | — | manual (browser) | ✅ visual-verified 2026-06-08 (3 UAT fix rounds) |

---

## Wave 0 Requirements

Existing xUnit infrastructure covered all automatable selection requirements; 12
content-kb test classes shipped in-phase (incl. new test doubles such as
`FakeContentSiteIndexStore`). No Wave 0 install needed. (Note: a verify-time
compile-blocker — `GetMergedClipsAsync` missing from two test fakes after the
interface grew — was fixed in-phase; see [[feedback_verify_builds_test_project]].)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| "What Experts Say" panel renders pinned/followed clips; chips add/remove; tray persists via localStorage | SEL-02, SEL-06 | Razor render + client interaction | Open a deck analysis, pin a video / follow a creator, confirm panel + persistence |
| Admin live-score preview reflects pin/follow/evergreen changes | SEL-06 | Live admin UI render | `/Admin/ContentKb` preview — adjust selection, observe score update |

---

## Validation Sign-Off

- [x] Per-requirement map built from 4 plan SUMMARYs
- [x] All store/relevance/packet/controller logic has xUnit coverage (Core 270 / Web 608)
- [x] Admin/analysis UI human-verified on prod-like run (2026-06-08, 3 UAT rounds)
- [ ] `nyquist_compliant: true` — **not fully achievable**: chip/tray UI + live-score preview render are browser-visual. Recorded `partial` (all logic automated; UI human-verified).

**Approval:** approved 2026-06-09 — PARTIAL (logic fully automated via xUnit; selection UI human-verified)

---

## Validation Audit 2026-06-09
| Metric | Count |
|--------|-------|
| Automatable requirements | covered by 12 test classes |
| Resolved (automated) | SEL-01..06 logic |
| Manual-only | expert panel + admin preview UI render |

Reconstructed from artifacts. Broad in-phase automated coverage already present
across store, relevance, packet, and controller layers; no gaps to fill. Remaining
manual items are inherent UI render and were human-verified at phase close.
