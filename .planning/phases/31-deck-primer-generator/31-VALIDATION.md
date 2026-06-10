---
phase: 31
slug: deck-primer-generator
status: validated
nyquist_compliant: partial
wave_0_complete: n/a
created: 2026-06-09
---

# Phase 31 — Validation Strategy

> Retroactive Nyquist audit (State B reconstruction). Phase 31 shipped the Deck
> Primer Generator (PRM-01..12) — a fourth paste-ready workflow with a 31-section
> catalog, combo grounding, bracket routing, and three prompt-variant builders.
> The server/domain logic carries strong xUnit coverage; the Razor UI surface and
> paste-into-AI round-trip are browser-visual (manual), which is where the 3
> verify-found bugs were caught and fixed (9fd1c65 / 779affe / abbeedd).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Web.Tests, DeckFlow.Core.Tests) |
| **Quick run** | `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "Primer|EdhTop16|Archetype"` |
| **Full suite** | `dotnet test DeckFlow.sln` |
| **Closeout result** | Web.Tests 654 pass / 0 fail / 5 PG-skip |
| **Estimated runtime** | ~30-60s |

---

## Per-Requirement Verification Map

| Requirement | Behavior | Test File(s) | Test Type | Status |
|-------------|----------|--------------|-----------|--------|
| PRM-01 | Combo-data spike / prompt-size gating | `31-SPIKE` doc + size measurement | manual (spike) | ✅ verdicts recorded |
| PRM-02..04 | Section catalog + request binding + bracket presets | `PrimerSectionCatalogTests`, `DeckPrimerRequestTests` | unit | ✅ green |
| PRM-05..08 | Primer packet service (build, combo grounding, archetype) | `DeckPrimerPacketServiceTests`, `ContentKbArchetypeDeriverTests` | unit | ✅ green |
| PRM-09 | EDH Top16 top-archetype derivation + graceful degrade | `EdhTop16ClientTopArchetypesTests`, `EdhTop16ClientTests` | unit | ✅ green |
| PRM-10..12 | Three prompt variants + artifact store round-trip | `PrimerPromptVariantTests`, `PacketArtifactStorePrimerTests`, `DeckPrimerResultRoundTripTests` | unit | ✅ green |
| PRM-05..12 (UI) | Razor primer page render, collapsible groups, bracket gating, paste round-trip | — | manual (browser) | ✅ visual-verified desktop+mobile 2026-06-09 |

---

## Wave 0 Requirements

Existing xUnit infrastructure covered all automatable primer requirements; tests
shipped in-phase (9 primer-related test classes). No Wave 0 install needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Primer page renders; section groups collapse/expand; preset chips select per bracket | PRM-05..08, 12 | Razor render + client interaction | Open `/deck-primer`, paste a deck, toggle brackets/presets |
| Generated primer pastes into ChatGPT/Claude/Gemini and returns a useful answer in one round-trip (core value) | PRM-10..12 | Real LLM round-trip — not unit-assertable | Generate primer, paste into each target, confirm usable output (13KB primer w/ combo markers confirmed 2026-06-09) |
| `data-preset-ids` JSON-encoding, category-knowledge degrade, controller ctor | PRM-* | Found at visual-verify, fixed 9fd1c65/779affe/abbeedd; regression guard via DeckControllerTests ctor | Covered by build + the 3 fix commits |

---

## Validation Sign-Off

- [x] Per-requirement map built from 6 plan SUMMARYs
- [x] All domain/service logic has xUnit coverage (654/0/5)
- [x] UI + paste round-trip human-verified (3 bugs caught + fixed)
- [ ] `nyquist_compliant: true` — **not fully achievable**: Razor render + LLM paste round-trip are inherently manual. Recorded `partial` (all logic automated; UI/round-trip human-verified).

**Approval:** approved 2026-06-09 — PARTIAL (logic fully automated via xUnit; UI + AI round-trip human-verified)

---

## Validation Audit 2026-06-09
| Metric | Count |
|--------|-------|
| Automatable requirements | covered by 9 test classes |
| Resolved (automated) | PRM-02..12 logic |
| Manual-only | UI render + AI paste round-trip (PRM-01 spike) |

Reconstructed from artifacts. Strong in-phase automated coverage already present;
no gaps to fill. Remaining manual items are inherent (Razor render, real LLM
round-trip) and were human-verified at phase close.
