---
phase: 37
slug: retire-clip-injection
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-10
---

# Phase 37 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `37-RESEARCH.md` § Validation Architecture.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (+ xunit.runner.visualstudio 3.1.4, MockHttp 7.0.0) |
| **Config file** | none — csproj-driven (`DeckFlow.Web.Tests`, `DeckFlow.Core.Tests`) |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug` |
| **Full suite command** | CI push-and-watch (VSTest unreliable in WSL — build-clean is primary gate per CLAUDE.md) |
| **Estimated runtime** | build ~60-90s; CI suite ~minutes |

---

## Sampling Rate

- **After every task commit:** `dotnet build DeckFlow.sln -c Debug` (must stay 0 warn / 0 err).
- **After every plan wave:** full build + CI xUnit run (push-and-watch).
- **Before `/gsd:verify-work`:** build 0/0 + CI green; plus manual smoke (below).
- **Max feedback latency:** build < ~90s.

---

## Per-Requirement Verification Map

| Req | Behavior | Test Type | Automated Command / Proof | File Exists |
|-----|----------|-----------|----------------------------|-------------|
| RET-01 | No `## Expert Context` block in any of 3 prompt variants | unit | assert variant `Build(...)` output lacks `## Expert Context` (replaces deleted expert-context asserts) | ❌ Wave 0 |
| RET-02 | Build 0 warn / 0 err, no dead refs | build | `dotnet build DeckFlow.sln -c Debug` → 0/0; grep proves removed types gone | ✅ baseline 0/0 captured |
| RET-03 | Browse + harvest/distill intact | smoke | manual: CLI harvest/distill runs; `/content-kb` (flag ON) renders entries | ✅ `ContentKbControllerTests` cover render |
| RET-04 | Markdig pipeline strips raw HTML (XSS) | unit | assert `Markdown.ToHtml("<script>…", Pipeline)` has no `<script>` (preserve `.DisableHtml()`) | ❌ Wave 0 |
| RET-05 | Pre-retire packet zip loads without throwing | unit | new test: load zip with legacy `ExpertSelectionJson`/`ExpertContextJson` → no exception | ❌ Wave 0 |
| RET-06 | Deck-analysis note links `/content-kb` copyable prompts | manual/view | visual: note + link present where accordion was (surface existing Detail-page copy affordance) | ❌ manual |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] RET-01 assertion: prompt-variant output omits `## Expert Context` (3 platforms).
- [ ] RET-04 XSS-regression test on the Markdig pipeline (`.DisableHtml()` behavior).
- [ ] RET-05 back-compat test: fixture zip with legacy expert-selection JSON entries loads cleanly.
- [ ] Delete 8 test files + edit 6 (see RESEARCH § Test Impact); ensure no orphaned `using`/helpers after deletion.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `/content-kb` browse reachable + renders distilled entries with encoded text | RET-03, RET-04 | flag-gated live HTML render; XSS visual confirm | flip `content.kb.enabled` ON, load `/content-kb`, confirm entries render, no expert pin/follow tray, harvested text encoded |
| Fresh deck-analysis packet has no Expert Context block + note links KB | RET-01, RET-06 | end-to-end prompt artifact | generate ChatGPT/Claude/Gemini analysis; confirm no `## Expert Context`; confirm KB pointer note + link present |
| Pre-retire packet zip loads | RET-05 | needs a real legacy zip | load a pre-retire packet zip; confirm no throw |

---

## Validation Sign-Off

- [ ] All RET reqs have automated verify or Wave 0 dependencies (RET-06 manual by nature)
- [ ] Sampling continuity: build runs every commit
- [ ] Wave 0 covers RET-01 / RET-04 / RET-05 new assertions + test deletions
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter (planner/executor sets after Wave 0 map complete)

**Approval:** pending
