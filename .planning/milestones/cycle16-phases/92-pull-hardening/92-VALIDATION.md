---
phase: 92
slug: pull-hardening
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-10
---

# Phase 92 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (.NET 10) — `DeckFlow.Studio.Tests`, `DeckFlow.Core.Tests` |
| **Config file** | none — existing test projects cover this phase (no Wave 0 install) |
| **Quick run command** | `dotnet.exe test DeckFlow.Studio.Tests` (Studio coordinator/page tests) |
| **Full suite command** | `dotnet.exe build` (0/0) then `dotnet.exe test` across the solution |
| **Estimated runtime** | ~build 5–10s; Studio.Tests ~30–60s; full solution suite several min |

> WSL constraint: VSTest is unreliable under WSL — build clean is the primary gate; run targeted `DeckFlow.Studio.Tests` / `DeckFlow.Core.Tests` via `dotnet.exe` (Windows host) or push-and-watch CI. Never open a browser on the Windows host.

---

## Sampling Rate

- **After every task commit:** `dotnet.exe build` (0 warn / 0 err) + the touched test project
- **After every plan wave:** full-solution build + `DeckFlow.Core.Tests` + `DeckFlow.Studio.Tests`
- **Before verify:** full suite green; EOL clean (LF); changed-lines format gate clean
- **Max feedback latency:** ~60s (targeted project)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 92-01-* | 01 | 1 | SYNC-14 | — | `git fetch` failure (offline) warns, never blocks Pull; behind-count>0 warns; never SFTP/prod | unit | `dotnet.exe test DeckFlow.Core.Tests` | ✅ | ⬜ pending |
| 92-02-* | 02 | 2 | SYNC-15 | — | body-hash != prod body_sha256 ⇒ divergence stamped, EXCLUDED from default adopt; null body_sha256 ⇒ indeterminate ⇒ surfaced not skipped | unit | `dotnet.exe test DeckFlow.Studio.Tests` | ✅ | ⬜ pending |
| 92-02-* | 02 | 2 | SYNC-13 | — | adopt = body←git, index cols←prod, approval←prod-mirror, is_visible/is_hidden preserved-local; divergent-not-acked entry never adopted | unit | `dotnet.exe test DeckFlow.Studio.Tests` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky. Task IDs finalized by the planner; wave/plan split is indicative.*

---

## Wave 0 Requirements

*None — existing `DeckFlow.Core.Tests` + `DeckFlow.Studio.Tests` infrastructure covers all phase requirements. New `IGitRepository` behind-detection members get a `FakeGitRepository` extension in the existing double.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Live Studio `/pull` staleness warning + per-entry divergence opt-in against real Render prod | SYNC-14 / SYNC-15 | Requires the real Studio Blazor page + live prod-Postgres read + a genuinely-behind checkout; harness fakes the Postgres transport and does no Blazor render (P91 FU-3 precedent) | Pre-flip operator gate: run `/pull` once live, confirm the behind-warning fires on a stale checkout, confirm a divergent entry is excluded by default and adopts only on explicit opt-in, confirm no prod write |

---

## Validation Sign-Off

- [ ] All tasks have automated verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (none required)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
