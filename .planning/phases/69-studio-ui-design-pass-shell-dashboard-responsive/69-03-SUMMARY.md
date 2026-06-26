---
phase: 69-studio-ui-design-pass-shell-dashboard-responsive
plan: 03
status: complete
commit: ee7ca904
executor: codex (gpt-5.4 medium)
requirements: [STUI-02]
tests: "HomePageTests 4/4 PASS (Windows runner)"
---

# 69-03 SUMMARY — Home pipeline dashboard + bUnit

**Executed by Codex (gpt-5.4 medium), Wave 2. Commit `ee7ca904`. Both builds clean (0W/0E). HomePageTests 4/4 PASS.**

## What shipped
- **Home.razor** (19-line placeholder → 178-line dashboard) — injects `IContentSiteIndexStore` + `PublishStateDeriver` + `StudioConfig`, `@implements IDisposable` with CTS. Load discipline copied EXACTLY from Review.razor (`Task.Run` → `EnsureSchemaAsync` → `GetAllRowsAsync`, `OperationCanceledException` guard, generic error copy, `finally` StateHasChanged). VideoStatus count cards (via `VideoStatusResolver.FromContentRow`), PublishState badge summary (via `PublishStateDeriver.Derive`, locked badge classes byte-identical to Review), quick links /harvest /review /publish, prod indicator, loading/error/empty states. `<h1 class="studio-page-title">` (Home-only adoption per recorded scope decision).
- **Home.razor.css** (NEW, 123 lines) — dashboard-card scoped CSS referencing 69-01 tokens (`--sp-*`, `--studio-surface`, `--studio-border`, `--fs-display`, `--studio-accent`); no hardcoded colors.
- **HomePageTests.cs** (NEW, 120 lines) — 4 bUnit facts: count-per-bucket, zero-bucket renders 0, quick-links present, store-failure shows generic copy + no exception leak. Reused existing `FakeContentSiteIndexStore.ReadFailureMessage` seam (no fake change needed).

## Constraints honored
- **Presentation-only over already-reachable data**: NO new store method or query. C# is read-only wiring only.
- **D-07 leak-safe**: `ex.Message` never assigned to display state; only the fixed generic copy. Asserted by `StoreFailure_ShowsGenericError_NoLeak` (T-69-01 mitigation).
- All Task 1/2 acceptance greps PASS; commit scoped to exactly the 3 in-scope files.
