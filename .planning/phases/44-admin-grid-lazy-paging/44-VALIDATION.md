---
phase: 44
slug: admin-grid-lazy-paging
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-06-13
---

# Phase 44 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.3 |
| **Config file** | none (default discovery) |
| **Quick run command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` |
| **Full suite command** | `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.sln` |
| **Estimated runtime** | ~60–120 seconds (full sln) |

> Note: VSTest is unreliable in WSL (per CLAUDE.md). Primary gate is `dotnet build` clean plus push-and-watch CI; the commands above are the intended local sampling commands when the runner is available.

---

## Sampling Rate

- **After every task commit:** Run quick run command (Core.Tests for Wave 0 repo/index changes; Web.Tests for controller/partial changes)
- **After every plan wave:** Run full suite command
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** ~120 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 44-W0-idx | 00 | 0 | GRID-02 SC3 | — | N/A | unit | Core.Tests quick run | ❌ W0 (update `CategoryKnowledgeRepositoryTests.cs:276-287`) | ⬜ pending |
| 44-W0-skel | 00 | 0 | GRID-01 SC1 | — | N/A | unit | Web.Tests quick run | ❌ W0 (new `Index_DoesNotCallCommanderCountOrPagedQuery` in `AdminHarvestControllerTests.cs`) | ⬜ pending |
| 44-idx-ddl | repo | 0 | GRID-02 SC3 | — | N/A | unit | Core.Tests quick run | ❌ W0 | ⬜ pending |
| 44-partial-200 | ctrl | 1 | GRID-01 SC2 | — | same-origin GET → 200 PartialView | unit | Web.Tests quick run | ❌ W1 | ⬜ pending |
| 44-partial-403 | ctrl | 1 | GRID-01 SC4 | T-44-csrf | cross-origin `Origin` → 403 | unit | Web.Tests quick run | ❌ W1 | ⬜ pending |
| 44-grid-ajax | ui | 1 | GRID-01 SC2 | — | grid populates post-load, pagination swaps section only | smoke (manual) | browser at `/Admin/Harvest` | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `DeckFlow.Core.Tests/CategoryKnowledgeRepositoryTests.cs` — update `EnsureSchemaAsync_CreatesDeckQueueIndexes` (lines 276–287) to assert the new single partial expression index exists and the two old index names (`ix_deck_queue_processed_commander`, `ix_deck_queue_processed_commander_lower`) do NOT. Covers GRID-02 SC3.
- [ ] `DeckFlow.Web.Tests/AdminHarvestControllerTests.cs` — add `Index_DoesNotCallCommanderCountOrPagedQuery`: assert `FakeCategoryKnowledgeStore` call-count for `GetDistinctProcessedCommanderCountAsync` and `GetPagedProcessedCommandersAsync` are both 0 after `Index()`. Covers GRID-01 SC1.

*Wave 0 establishes the failing tests that lock SC1 (skeleton runs no slow query) and SC3 (index consolidation) before implementation.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Distinct-count query uses the index, not a full scan | GRID-02 SC3 | Query plan inspection is not an xUnit assertion | Run `EXPLAIN QUERY PLAN` for the distinct-count query against a local SQLite KB DB; confirm it references `ix_deck_queue_commander_lower_processed`, not `SCAN deck_queue` |
| Grid populates after page load without full-page reload; pagination click swaps only the grid section | GRID-01 SC2 | Browser interaction / no-reload behavior | Navigate to `/Admin/Harvest`, observe empty placeholder then AJAX-populated grid; click a page number and confirm only the grid section re-renders (no full-page nav) |

---

## Validation Sign-Off

- [ ] All tasks have automated verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
