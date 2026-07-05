---
phase: 85
slug: chatgpt-naming-cleanup
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-05
---

# Phase 85 — Validation Strategy

> Per-phase validation contract for a byte-identical identifier rename (chatgpt→prompt /
> ChatGpt→Prompt). Validation is dominated by grep-clean gates + build + full test/e2e green,
> plus a byte-identical rendered-output proof. See `85-RESEARCH.md` "Validation Architecture".

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit (C# — DeckFlow.Core.Tests, DeckFlow.Web.Tests) + Playwright e2e (`DeckFlow.Web/e2e/*.spec.ts`) + `tsc` (TS compile) |
| **Config file** | `DeckFlow.sln`, `DeckFlow.Web/playwright.config.ts`, `DeckFlow.Web/tsconfig.json` |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -clp:ErrorsOnly` + grep gates (below) |
| **Full suite command** | Build + `cd DeckFlow.Web && npx --no-install playwright test` + `dotnet.exe test DeckFlow.sln` |
| **Estimated runtime** | build ~3s; e2e ~5s; xUnit suite ~minutes (WSL: use dotnet.exe) |

---

## Sampling Rate

- **After every task commit:** `dotnet.exe build DeckFlow.sln -clp:ErrorsOnly` + the wave's scoped grep gate.
- **After every plan wave:** Full e2e (`npx --no-install playwright test`) + affected xUnit tests.
- **Before `/gsd:verify-work`:** Full suite green + all grep-clean gates pass + byte-identical proof.
- **Max feedback latency:** ~10s for build+grep; full suite before verification.

---

## Core Acceptance Gates (requirement-mapped)

| Gate | Req | Command (success = zero / clean) |
|------|-----|----------------------------------|
| No kebab `chatgpt-*` in web assets | AICLEAN-01/02/03 | `grep -rIn 'chatgpt-' DeckFlow.Web/wwwroot/css DeckFlow.Web/wwwroot/ts DeckFlow.Web/Views` → 0 |
| No `ChatGpt`/`chatgpt` outside D3 keep-list | AICLEAN-02 | `grep -rIn 'ChatGpt\|chatgpt' <renamed-scope>` → only keep-list (model-trio + AiPlatform.ChatGpt + `*-chatgpt-prompt.txt` + user-visible copy) remains |
| No dead/duplicated selector | AICLEAN-02 | git diff shows only identifier tokens changed; no orphaned `.chatgpt-*`/`.prompt-*` pair |
| Byte-identical render | AICLEAN-01 | rendered-HTML/computed-style diff vs pre-rename baseline == identifier-only (Phase-84-style headless capture) |
| Build clean | all | `dotnet.exe build DeckFlow.sln` → 0 warnings, 0 errors |
| Full e2e green | AICLEAN-03 | `npx --no-install playwright test` unchanged/green |
| xUnit green | all | `dotnet.exe test DeckFlow.sln` all pass (C# rename touches tests in lockstep) |

---

## Wave 0 Requirements

- No new test scaffolding required — existing xUnit + Playwright suites already exercise the
  touched CSS/TS/views/C#. The rename must keep them green (updated in lockstep where a test
  references a renamed symbol/selector).
- Recommended (planner discretion): a pre-rename rendered-output/computed-style baseline
  (Phase 84 Task 0 pattern, headless `run-web-test.sh`, no new deps) committed BEFORE any edit,
  to make the byte-identical proof a concrete diff rather than executor discretion.

*If none: "Existing infrastructure covers all phase requirements."*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| D3 keep-list correctness (no genuine ChatGPT-model ref or user-visible copy renamed) | AICLEAN-02 | Semantic judgment — grep can't tell branding from model-variant | Review final diff: confirm the 7 `ChatGpt*PromptVariant` + Claude/Gemini siblings, `AiPlatform.ChatGpt`, `*-chatgpt-prompt.txt`, and all user-visible "ChatGPT" copy are UNCHANGED |
| Contract-value lockstep (D5) | AICLEAN-02 | Client↔server key desync isn't caught by render diff | Confirm each renamed `data-cache-key`/`data-sync-panel`/storage-key value changed on BOTH the emitting (C#/Razor) and consuming (TS) side |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify (grep gate / build / test) or reuse existing suites
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 (optional baseline) captured before first edit if byte-identical proof is diff-based
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s (build+grep)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
