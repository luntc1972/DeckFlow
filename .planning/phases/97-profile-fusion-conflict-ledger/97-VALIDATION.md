---
phase: 97
slug: profile-fusion-conflict-ledger
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-12
---

# Phase 97 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Fusion + conflict logic is pure-Core and MUST be deterministic + fully unit-tested (CS-20).

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.x (`DeckFlow.Core.Tests`) — .NET 10 |
| **Config file** | `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj` (existing) |
| **Quick run command** | `dotnet.exe test DeckFlow.Core.Tests --filter FullyQualifiedName~Fusion` |
| **Full suite command** | `dotnet.exe test DeckFlow.Core.Tests` |
| **Estimated runtime** | ~30–60 seconds (Core suite) |

> **WSL caveat (project rule):** VSTest is unreliable under WSL. Primary gate is
> `dotnet.exe build` clean + targeted `dotnet.exe test` from the Windows host, with
> push-and-watch CI as the authoritative backstop. Use the Windows `dotnet.exe`, not the
> WSL `dotnet`. Studio Blazor page (D-11) is loopback-only — **no theme/mobile/public UI
> tests apply** (D-11 explicitly waives the web-page tests+themes+mobile rule).

---

## Sampling Rate

- **After every task commit:** Run quick command (Fusion-filtered Core tests)
- **After every plan wave:** Run full Core suite
- **Before `/gsd:verify-work`:** Full Core suite must be green + P94 round-trip tests still green (additive-only guard)
- **Max feedback latency:** ~60 seconds

---

## Per-Task Verification Map

*Planner fills one row per task. Every fusion/conflict/classifier/recency task MUST have a unit-test verify. The Studio page + CLI trigger tasks may use build-green + manual smoke.*

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 97-01-01 | 01 | 1 | CS-16 / CS-20 | — | N/A (pure compute) | unit | `dotnet.exe test DeckFlow.Core.Tests --filter FullyQualifiedName~Fusion` | ❌ W0 | ⬜ pending |

---

## Wave 0 Requirements

- [ ] Fusion test class(es) in `DeckFlow.Core.Tests/` — stubs for CS-16, CS-16a, CS-17, CS-18, CS-19, CS-20
- [ ] Golden fixtures grounded on `docs/research/p89-p90-prototype-snail.md` (land 37-42, ramp 7-12, draw 13-18, wipes 3-5, counters ≥8) to lock conflict-threshold + observable/philosophy verdicts
- [ ] Regression assertion that P94 `CreatorStyleProfileStoreTests` / `CreatorStyleProfileStorePostgresTests` round-trips stay green after additive `FusedTarget`/`FusedConflict` extension

*Existing xUnit infrastructure covers the framework — no install needed.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| D-03 harvest+distill confirmation run (P96 prompts reproduce ~27 prototype rules) | CS-16/CS-17 grounding | yt-dlp/ffmpeg/whisper NOT on PATH this environment (verified live) — cannot execute in-session | Documented operator step: on a machine with harvest tooling, run isolated `distill --db /tmp/p97-confirm.db` with `DECKFLOW_LLM_PROVIDER=claude`; compare emitted stated rules to prototype bands. Executor MUST NOT silently attempt this. |
| Studio ledger page renders say-vs-do rows read-only | CS-19 | Loopback Blazor UI; visual verdict-badge check | Launch Studio, open ledger page for Salubrious Snail slug, confirm each `(metric,condition)` row shows stated band · measured · resolved · verdict badge · clip link |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
