---
phase: 57
slug: admin-surface-distill-quality
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-18
---

# Phase 57 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 (DeckFlow.Web.Tests, DeckFlow.Core.Tests) |
| **Config file** | none — test SDK in `.csproj` |
| **Quick run command** | `dotnet test DeckFlow.Web.Tests/ --no-build` |
| **Full suite command** | `dotnet test --no-build` |
| **Estimated runtime** | ~60–120 seconds |

> Note: VSTest is unreliable in WSL (CLAUDE.md). Treat `dotnet build` clean + filtered
> test runs as the primary gate; if WSL VSTest hangs, fall back to push-and-watch CI.

---

## Sampling Rate

- **After every task commit:** Run `dotnet test DeckFlow.Web.Tests/ --no-build`
- **After every plan wave:** Run `dotnet test --no-build`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~120 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 57-01-* | 01 | 1 | SITE-01 | — | `Index()` maps `PushedToProdUtc` + `IndexedUtc` onto `KbEntryRow` | unit | `dotnet test DeckFlow.Web.Tests/ --no-build --filter "AdminContentKbController"` | ✅ `AdminContentKbControllerTests.cs` | ⬜ pending |
| 57-01-* | 01 | 1 | SITE-01 | — | `Index()` derives `PublishState.Published` for visible+pushed rows | unit | same filter | ✅ extend | ⬜ pending |
| 57-01-* | 01 | 1 | SITE-01 | — | `Index()` derives `PublishState.NeverPublished` when `PushedToProdUtc` is null | unit | same filter | ✅ extend | ⬜ pending |
| 57-01-* | 01 | 1 | SITE-01 | — | `Index()` derives `PublishState.LocalNewer` when `IndexedUtc > PushedToProdUtc` | unit | same filter | ✅ extend | ⬜ pending |
| 57-02-* | 02 | 1 | DIST-01 | — | Reworked prompts still emit JSON parseable by `DistillationValidation` (3–8 clips, ≤200-word summary, allowlisted tags); JSON schemas unchanged | unit (existing) + manual UAT (Phase 58) | `dotnet test DeckFlow.Core.Tests/ --no-build --filter "Distillation"` | ✅ existing schema/validation tests | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

*Exact task IDs assigned by the planner; rows above map by requirement and plan.*

---

## Wave 0 Requirements

*Existing infrastructure covers all phase requirements.* `AdminContentKbControllerTests.cs`
already exists with a `Build()` helper and `FakeContentSiteIndexStore`; SITE-01 test work is
extension only. DIST-01's observable contract (`SummarySchema`/`ClipsSchema` + `DistillationValidation`)
is unchanged, so existing Core distillation tests continue to cover the parse path.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Reworked distill prompt yields measurably better paste-ready KB entries (clearer summaries, on-topic clips, accurate tags) | DIST-01 | Prose-prompt quality is not unit-assertable; success criterion is an operator before/after comparison on real harvested content | Run a distill on the same real harvested video against prior vs reworked prompt; operator inspects summary/clips/tags side-by-side. Gate executes in Phase 58 dogfood (DOGFOOD-01). |
| Publish-state column renders the four states on `/Admin/ContentKb` with existing columns unchanged | SITE-01 | Visual render in the live admin grid | Load `/Admin/ContentKb`, confirm one new publish-state column matching Studio's four states; confirm no existing column shifted/removed and empty-row colspan correct. |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies (DIST-01 quality is manual UAT by design — Phase 58 gate)
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (none — existing infra)
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
