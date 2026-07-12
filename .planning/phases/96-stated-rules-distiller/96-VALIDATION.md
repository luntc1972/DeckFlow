---
phase: 96
slug: stated-rules-distiller
status: approved
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-12
---

# Phase 96 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Core.Tests`) |
| **Config file** | none — existing test project covers phase requirements |
| **Quick run command** | `dotnet build DeckFlow.Core/DeckFlow.Core.csproj` (build gate; VSTest unreliable in WSL — see CLAUDE.md) |
| **Full suite command** | Windows `dotnet.exe test DeckFlow.Core.Tests` (WSL VSTest unreliable — run via dotnet.exe or push-and-watch CI) |
| **Estimated runtime** | Core build ~30s; Core.Tests full ~1–2min |

---

## Sampling Rate

- **After every task commit:** `dotnet build DeckFlow.Core/DeckFlow.Core.csproj` clean (no new warnings)
- **After every plan wave:** Full `DeckFlow.Core.Tests` run (dotnet.exe) green
- **Before `/gsd:verify-work`:** Full suite must be green + byte-stable artifact gate intact
- **Max feedback latency:** ~120 seconds

---

## Per-Task Verification Map

> Planner fills concrete Task IDs / commands per plan. Every stated-rules extraction task
> (chunking, Select/Disambiguate/Decompose, Reduce/dedupe, schema validation, content_type
> heuristic, card-grounding seam, store round-trip) MUST have a pure-Core unit test.
> The Claimify multi-pass + reduce logic lives in `DeckFlow.Core.Knowledge.StatedRulesExtraction`
> (pure) and is fully unit-testable without LLM/HTTP. The golden regression (D-06/CS-15) uses
> the CLI process-runner-override test seam with canned responses (NOT a live subprocess).

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | — | — | CS-11..CS-15 | T-96-* | see threat_model | unit | `dotnet.exe test DeckFlow.Core.Tests` | ✅ existing | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- Existing xUnit infrastructure (`DeckFlow.Core.Tests`) covers all phase requirements — no new framework install.
- New test fixtures required (planner scopes): a real Salubrious Snail transcript fixture (D-06 golden),
  canned Select/Disambiguate/Decompose/Reduce LLM responses for the process-runner seam, and a
  `content_stated_rules` store round-trip fixture.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live multi-pass extraction against the real subscription/CLI LLM provider | CS-12 | Costs real tokens; non-deterministic model output | Operator runs a single real Snail re-distill and eyeballs the emitted `stated_rules:` block for sanity (deferred, D-05; not required to pass the phase) |

*All automated-testable behaviors have automated verification via canned-response seams; only the live-model round-trip is manual/deferred.*

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (plan-checker 8a–8d: all 16 tasks carry a verify command)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references (new fixtures scoped: Snail transcript, canned LLM responses, store round-trip)
- [x] No watch-mode flags
- [x] Feedback latency < 120s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** approved 2026-07-12 (plan-checker VERIFICATION PASSED)
