---
phase: 98
slug: card-grounding-guard
status: planned
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-18
---

# Phase 98 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Guard decision logic (fuzzy-result classification, legality/color-identity/castability
> checks, whitelist assembly) is deterministic and MUST be fully unit-tested, including
> known-hallucination fixtures (CS-25). Scryfall HTTP is exercised only through the
> existing test seams (delegate-injection ctors / FakeResolver) — no live network in tests.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x (`DeckFlow.Core.Tests` + `DeckFlow.Web.Tests`) — .NET 10 |
| **Config file** | existing `*.Tests.csproj` (both projects present) |
| **Quick run command** | `dotnet.exe test DeckFlow.Web.Tests --filter FullyQualifiedName~Grounding` (plus `~CardGrounding` on Core.Tests for pure-Core checks) |
| **Full suite command** | `dotnet.exe test DeckFlow.Core.Tests && dotnet.exe test DeckFlow.Web.Tests` |
| **Estimated runtime** | ~60–120 seconds (both suites) |

> **WSL caveat (project rule):** VSTest is unreliable under WSL. Primary gate is
> `dotnet.exe build` clean + targeted `dotnet.exe test` from the Windows host, with
> push-and-watch CI as the authoritative backstop. Use the Windows `dotnet.exe`, not the
> WSL `dotnet`. This phase has **no user-facing UI surface** (reusable service consumed
> by Phase 99) — the web-page tests+themes+mobile rule does not apply.

---

## Sampling Rate

- **After every task commit:** Run quick command (Grounding-filtered tests)
- **After every plan wave:** Run both full suites
- **Before `/gsd:verify-work`:** Both suites green + no regression in existing Scryfall service tests (shared throttle/pipeline untouched or still green)
- **Max feedback latency:** ~120 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 98-01 T1 | 98-01 | 1 | CS-23 (contracts) | T-98-01 | Verdict/enum/deck-context contracts compile HTTP-free in Core | build | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CardGrounding` | ❌ new | ⬜ pending |
| 98-01 T2 | 98-01 | 1 | CS-23 | T-98-01, T-98-02 | Fail-closed legality (null->reject); basic-land singleton exemption; pip castability | unit (pure Core) | `dotnet test DeckFlow.Core.Tests --filter FullyQualifiedName~CardGroundingRules` | ❌ new | ⬜ pending |
| 98-02 T1 | 98-02 | 2 | CS-21 | T-98-10 | Legalities DTO field; ScryfallErrorResponse 404 discriminator; AddQueryParameter (no URL concat) | build | `dotnet build DeckFlow.Web -warnaserror` | ❌ new | ⬜ pending |
| 98-02 T2 | 98-02 | 2 | CS-21, CS-24 | T-98-11..T-98-15 | Strict accept returns canonical name; NotFound vs Ambiguous split; NotLegal fail-closed; UpstreamUnavailable not cached; resolution-only cache (no cross-deck contamination); distinct cache-key prefix | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CardGroundingGuard` | ❌ new | ⬜ pending |
| 98-03 T1 | 98-03 | 3 | CS-22 | T-98-20, T-98-21, T-98-22 | Corpus-only pool; frequency-ranked + capped; every candidate re-validated through guard; zero direct Scryfall | unit | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~CreatorWhitelistPool` | ❌ new | ⬜ pending |
| 98-03 T2 | 98-03 | 3 | CS-22 | T-98-20 | Builder DI-registered (or dependency gap noted for P99) | build | `dotnet build DeckFlow.Web -warnaserror` | ❌ new | ⬜ pending |
| 98-04 T1 | 98-04 | 3 | CS-25 | T-98-30 | Fake/banned/off-identity/duplicate/ambiguous inputs rejected with exact reasons; healed typo accepted | unit (fixtures) | `dotnet test DeckFlow.Web.Tests --filter FullyQualifiedName~Hallucination` | ❌ new | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements — both test projects, delegate-injection /
`FakeResolver` seams, and `[InternalsVisibleTo]` are already in place. No Wave 0 setup needed.
Every task listed above carries an `<automated>` command; the new test files are created within the
same tasks that produce the code under test (no missing test scaffolds).

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Scryfall fuzzy 404/ambiguous shape still matches research capture | CS-21 | External API contract — already live-verified 2026-07-18 (98-RESEARCH.md); re-probe only if Scryfall changes | `curl 'https://api.scryfall.com/cards/named?fuzzy=aust+com'` (ambiguous) and `fuzzy=zzzznotacardzzz` (not found) per 98-RESEARCH.md Pitfall 1 |
| Live Scryfall `legalities`/`color_identity` JSON shape | CS-23 | External API contract — live-verified 2026-07-18; re-probe only if Scryfall changes | `curl 'https://api.scryfall.com/cards/named?fuzzy=lightning+bolt'` → confirm lowercase `legalities.commander` + `color_identity` |
