---
phase: 50
slug: code-style-enforcement
status: approved
nyquist_compliant: partial
wave_0_complete: true
created: 2026-06-14
---

# Phase 50 — Validation Strategy

> Per-phase validation contract. Phase 50 is a config/CI/process phase: its primary
> "automation" is the **live `format-gate` CI job** (continuous enforcement on every
> push/PR — stronger than a one-shot unit test), plus one committed xUnit guard test
> for the carve-outs. Items not unit-testable without a new dependency (a shell-test
> framework) are recorded as CI-behavioral or manual assertions.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (`DeckFlow.Core.Tests`) + GitHub Actions CI (`format-gate` job) |
| **Config file** | `.github/workflows/ci.yml`, `.editorconfig`, `scripts/format-check-changed.sh` |
| **Quick run command** | `dotnet test DeckFlow.Core.Tests --filter Category=CarveOutGuard` |
| **Full suite command** | `dotnet test DeckFlow.sln` (CarveOutGuard runs unfiltered in CI) |
| **Estimated runtime** | guard test ~12s; format-gate CI job ~30s |

---

## Sampling Rate

- **After every task commit:** local `dotnet build` + (if staged `.cs`) `bash scripts/format-check-changed.sh staged`
- **After every plan wave:** `dotnet test DeckFlow.sln` (Core green; Web/PG auto-skip in WSL)
- **Before `/gsd:verify-work`:** CI run on `v1.7` must be green (`format-gate` + `build-and-test`)
- **Max feedback latency:** ~30s (CI format-gate) / ~12s (local guard test)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 50-03-01 | 03 | 2 | FMT-02 | — | Four carve-outs (`init` / raw-string / `[Attribute]` / switch-expr) byte-identical after `dotnet format` with reconciled `.editorconfig` | unit (xUnit) | `dotnet test DeckFlow.Core.Tests --filter Category=CarveOutGuard` | ✅ | ✅ green (4/4 ran+passed locally + in CI run 27512539496) |
| 50-02-01 | 02 | 2 | FMT-03 | T-50-01/02/03/06 | Mis-formatted **added** `.cs` line fails CI; legacy off-hunk line passes; no silent empty-diff pass | CI-behavioral | `format-gate` job (`bash scripts/format-check-changed.sh ci`) | ✅ | ✅ proven both directions in real CI (green 27511872066 / red 27511998394) |
| 50-02-02 | 02 | 2 | FMT-04 | T-50-01/05 | Pre-commit hook blocks a mis-formatted staged `.cs`, allows a clean one | CI/local-behavioral | `bash scripts/format-check-changed.sh staged` (via `.githooks/pre-commit`) | ✅ | ✅ block/allow proven locally |
| 50-01-01 | 01 | 1 | FMT-01 | — | 3 `resharper_*` keys adopted; RS `crlf` rejected; `[*] end_of_line` stays `lf`; carve-outs intact | config assertion | `grep -A2 '^\[\*\]$' .editorconfig \| grep -q 'end_of_line = lf'` + carve-out survival via FMT-02 | ✅ | ✅ asserted (commit 7020421) + FMT-02 guards carve-outs |
| 50-04-01 | 04 | 3 | FMT-05 | — | `CLAUDE.md` has no blanket "never reformat"; `.editorconfig` source-of-truth; 5 carve-out specifics retained | doc assertion | `! grep -q 'never reformat' CLAUDE.md` + carve-out specifics present | ✅ | ✅ asserted (commit 3857f3c) |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all automatable phase requirements. The only committed
automated test (CarveOutGuard) uses the existing xUnit framework + a repo-local
throwaway fixture project — no new framework, no new dependency.

---

## Manual-Only / CI-Behavioral Verifications

| Behavior | Requirement | Why Manual / CI-only | Test Instructions |
|----------|-------------|----------------------|-------------------|
| Changed-lines gate fails a mis-formatted **added** line; passes a legacy off-hunk edit | FMT-03 | Diff-intersect behavior depends on git base/staged context; not unit-testable without a shell-test framework (new dep, forbidden). The `format-gate` CI job IS the continuous automation. | On a PR/branch push: add a mis-formatted `.cs` line → `format-gate` red; one-line edit in a legacy file with unrelated quirks → `format-gate` green. |
| Pre-commit hook block/allow | FMT-04 | Requires staged-hunk git context + a configured `core.hooksPath`; same shell-context constraint. | `git config core.hooksPath .githooks`; stage a mis-formatted `.cs` → commit blocked; stage a clean change → commit succeeds. |
| `.editorconfig` reconciliation invariants | FMT-01 | One-time config merge; outcome is grep-assertable, carve-out survival is guarded by FMT-02. | `grep -A2 '^\[\*\]$' .editorconfig` shows `end_of_line = lf`; the 3 `resharper_*` keys present in `[*.cs]`; `50-RECONCILIATION.md` lists all 8 source keys. |
| `CLAUDE.md` source-of-truth rewrite | FMT-05 | Documentation change. | `CLAUDE.md` contains no blanket "never reformat" / "DO NOT run Format Document"; the 5 carve-out specifics are retained; `.editorconfig` named the enforced source of truth. |

---

## Validation Sign-Off

- [x] FMT-02 has committed `<automated>` xUnit verification (CarveOutGuard, green in CI)
- [x] FMT-03/FMT-04 enforced by the live `format-gate` CI job (continuous; both directions proven in real CI runs)
- [x] FMT-01/FMT-05 covered by config/doc assertions + FMT-02 carve-out guard
- [x] No new test framework or dependency introduced (constraint honored)
- [x] No watch-mode flags
- [x] CI feedback latency ~30s (format-gate) / ~12s (guard test)
- [~] `nyquist_compliant: partial` — one committed automated test (FMT-02); the remaining requirements are config/CI-behavioral/doc by nature, validated by the live gate and assertions rather than unit tests

**Approval:** approved 2026-06-14 (process phase — automation is the live CI gate + carve-out guard; remaining items are inherently non-unit-testable without a forbidden new dependency)
