---
phase: 57-admin-surface-distill-quality
reviewed: 2026-06-18T00:00:00Z
depth: standard
files_reviewed: 8
files_reviewed_list:
  - DeckFlow.Web/Models/AdminContentKbViewModel.cs
  - DeckFlow.Web/Controllers/Admin/AdminContentKbController.cs
  - DeckFlow.Web/Program.cs
  - DeckFlow.Web/Views/AdminContentKb/Index.cshtml
  - DeckFlow.Web/wwwroot/css/admin-common.css
  - DeckFlow.Web.Tests/AdminContentKbControllerTests.cs
  - DeckFlow.Core/Knowledge/DistillationSchemas.cs
  - DeckFlow.Core.Tests/DistillationPromptRegressionTests.cs
findings:
  critical: 0
  warning: 1
  info: 3
  total: 4
status: issues_found
---

# Phase 57: Code Review Report

**Reviewed:** 2026-06-18
**Depth:** standard
**Files Reviewed:** 8
**Status:** issues_found

## Summary

Phase 57 (SITE-01 admin publish-state column + DIST-01 distill-prompt rework) was reviewed
adversarially against both plans and the DeckFlow carve-out rules. The implementation is
faithful, narrow, and clean. All carve-outs are respected, the DIST-01 JSON contract is
provably unchanged, and the publish-state derivation is correctly routed through the single
`PublishStateDeriver` source of truth with no hand-rolled if/else drift.

I verified the build and ran the targeted suites: `DeckFlow.Web.Tests --filter AdminContentKbController`
passes 20/20, and `DeckFlow.Core.Tests --filter "Distillation|CarveOutGuard"` passes 51/51,
including the untouched schema-contract fixture and the CarveOutGuard raw-string guards.

**Contract / carve-out verification (all PASS):**
- The four `*Schema` constants (`SummarySchema`/`ClassificationSchema`/`ClipsSchema`/`TagsSchema`,
  lines 11-45 of `DistillationSchemas.cs`) are byte-identical — the diff touches only prompt prose.
- `FormatAllowlist(...)` and the three `$"...{FormatAllowlist(...)}..."` interpolations are intact (3 occurrences).
- `ResponseFormatSchemas_MatchShippedPhase21Fixtures` is unmodified and green — proves schema contract unchanged.
- `DistillationValidation` is not touched; reworked prompts stay satisfiable (3-8 clips, ≤200-word summary, allowlisted tags).
- New `KbEntryRow` properties keep `{ get; init; }` (no `{ get; }` regression). XML doc present on all three.
- Raw-string `"""` delimiters not re-indented (CarveOutGuard green). LF line endings clean on all 8 files.
- `kb-status--local-newer` lives only in `admin-common.css`; not in `site.css`/`site-common.css`.
- `PublishStateDeriver` is registered as a singleton and is verified stateless/pure (no fields, no I/O) — singleton lifetime is correct.
- Publish-state derivation is single-sourced: the only call is `_deriver.Derive(...)`; the view switch maps state→CSS class only and gets display text from `ToDisplayString()` (no hardcoded vocabulary).

No Critical issues. Findings below are one Warning (a real test-coverage gap) and three Info items.

## Warnings

### WR-01: No test covers the `PushedHidden` publish state — the one derived state with a non-trivial precedence

**File:** `DeckFlow.Web.Tests/AdminContentKbControllerTests.cs:267-313` (the four new facts)
**Issue:** Plan 57-01 specifies the deriver has four states. The new tests cover `NeverPublished`,
`Published`, and `LocalNewer`, plus a field round-trip — but **not** `PushedHidden`
(`pushedToProdUtc != null && !isVisible`). `PushedHidden` is the only state whose precedence depends
on the `isVisible` branch firing *before* the timestamp comparison, so it is the state most likely to
regress silently if the deriver or the controller's `_deriver.Derive(r.PushedToProdUtc, r.IsVisible, r.IndexedUtc)`
argument order is ever reshuffled (e.g. passing `IsHidden` instead of `IsVisible`). A row that is
pushed-then-unpublished would currently mis-derive with no failing test. The view also renders this
state (`PublishState.PushedHidden => "kb-status--hidden"`, `Index.cshtml:173`), so it is live UI with zero coverage.
**Fix:** Add a fifth fact mirroring the existing pattern:
```csharp
[Fact]
public async Task Index_PublishStatePushedHidden_WhenPushedButNotVisible()
{
    var indexedUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    var pushedToProdUtc = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero);
    var store = new FakeContentSiteIndexStore();
    // visible:false so the row is the "unpublished" tri-state (IsHidden defaults false → survives default filter)
    store.Rows.Add(Row(1, visible: false, indexed: indexedUtc, pushedToProdUtc: pushedToProdUtc));
    var controller = Build(store, out _, crossOrigin: false);

    var result = await controller.Index(cancellationToken: default);

    var vm = Assert.IsType<ViewResult>(result).Model as AdminContentKbViewModel;
    Assert.NotNull(vm);
    Assert.Equal(PublishState.PushedHidden, vm.Entries[0].PublishState);
}
```
This also locks in that the controller passes `IsVisible` (not `IsHidden`) into the deriver's second slot.

## Info

### IN-01: `Published` test asserts only the `==` boundary, but its name promises "AtOrAfter"

**File:** `DeckFlow.Web.Tests/AdminContentKbControllerTests.cs:299-313`
**Issue:** `Index_PublishStatePublished_WhenVisibleAndPushedToProdUtcIsAtOrAfterIndexedUtc` sets
`indexedUtc == pushedToProdUtc` (both `2026-06-01`). The deriver returns `Published` when
`localIndexedUtc <= pushedToProdUtc`, so the test only exercises the equality edge — the strictly-after
case (`pushed > indexed`) is never asserted despite the test name claiming "AtOrAfter". The
boundary `<=` vs `<` is exactly where a `LocalNewer`/`Published` off-by-one would hide.
**Fix:** Either rename the test to `...WhenPushedEqualsIndexed`, or add a `[Theory]`/second case with
`pushedToProdUtc = 2026-06-02` and `indexedUtc = 2026-06-01` (strictly-after) asserting `Published`,
so both sides of the `<=` boundary are pinned.

### IN-02: Redundant fully-qualified `PublishState` in the Razor switch despite the new `@using`

**File:** `DeckFlow.Web/Views/AdminContentKb/Index.cshtml:171-175` (with `@using DeckFlow.Core.Content` at line 2)
**Issue:** Line 2 adds `@using DeckFlow.Core.Content`, yet the switch arms still write the fully-qualified
`DeckFlow.Core.Content.PublishState.Published` etc. The `@using` was added specifically so
`ToDisplayString()` (line 176) resolves; the qualified names are now redundant noise. Not a bug —
purely a readability nit. The plan's wording allowed the qualified form *"unless a `@using` already exists"*,
and one now does in the same edit.
**Fix:** Simplify the switch arms to `PublishState.Published => ...`, `PublishState.PushedHidden => ...`,
`PublishState.LocalNewer => ...` now that the namespace is imported.

### IN-03: `kb-status--local-newer` badge contrast is below WCAG AA on the rgba(13,202,240,0.15) fill

**File:** `DeckFlow.Web/wwwroot/css/admin-common.css:614-619`
**Issue:** The new badge sets `color: #000` on a translucent `rgba(13, 202, 240, 0.15)` background composited
over the admin panel surface. Black text on a ~15%-opacity light-cyan tint generally clears AA, but the
other three `kb-status` badges in this file use their own foreground tokens, not raw `#000`; mixing a
hardcoded `#000` here is a minor consistency drift and the effective contrast depends on the underlying
panel color (untested at the two viewports). This is admin-only UI behind BasicAuth, so impact is low.
**Fix:** No code change required for ship. If desired, align with the sibling badges' color approach
(reuse the existing badge foreground token rather than a literal `#000`) and confirm contrast at the
operator visual-verify step (Phase 58). Left as Info because the badge is functional and admin-scoped.

---

## Resolution (2026-06-18)

- **WR-01 RESOLVED** — added `Index_PublishStatePushedHidden_WhenPushedButNotVisible` fact (commit 2f90ccf). Suite now 22/22.
- **IN-01 RESOLVED** — added `Index_PublishStatePublished_WhenPushedStrictlyAfterIndexedUtc` fact (commit 2f90ccf).
- **IN-02 / IN-03 NOT fixed** — cosmetic nits (redundant FQN in Razor switch; badge literal `#000` over translucent fill, admin-only behind BasicAuth). Left as documented backlog per user choice.

---

_Reviewed: 2026-06-18_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
