# 37-02 Summary

## What Changed

- Un-darkened fresh installs of `/content-kb` by flipping the seeded `content.kb.enabled` default ON for both Postgres and SQLite.
- Added a RET-04 regression test that pins the Content KB Markdig render posture: raw `<script>` stays inert under `UseAdvancedExtensions().DisableHtml()`, while normal Markdown still renders.
- Added a RET-06 pointer note on the deck-analysis page to route users to `/content-kb` for copyable prompts, and rewrote stale home/nav copy so it no longer promises removed auto-injection behavior.
- Operator TODO: the live PROD `content.kb.enabled` row must still be flipped ON manually via `/Admin/Flags`; the seed change only affects fresh databases.

## Files Changed

- `DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs`
- `DeckFlow.Web.Tests/ContentKbMarkdigXssTests.cs`
- `DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml`
- `DeckFlow.Web/Views/Deck/Home.cshtml`
- `DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml`

## Verification

- Build:
  `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -c Debug`
  Result after Task 1: `Build succeeded.` / `0 Warning(s)` / `0 Error(s)`
  Result after Task 2: `Build succeeded.` / `0 Warning(s)` / `0 Error(s)`
  Result after Task 3: `Build succeeded.` / `0 Warning(s)` / `0 Error(s)`
- Acceptance checks:
  `grep -n "content.kb.enabled" DeckFlow.Web/Services/FeatureFlags/FeatureFlagStore.cs` showed `TRUE` and `1` only.
  `grep -n "DisableHtml" DeckFlow.Web/Controllers/ContentKbController.cs` still returned the Markdig pipeline line.
  `grep -rn "Html.Raw\|MarkupString\|WriteLiteral\|IHtmlContent" DeckFlow.Web/Views/ContentKb` returned nothing.
  `grep -n "/content-kb" DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml` returned the new pointer note.
  `grep -in "inject their advice\|inject.*deck analysis prompt" DeckFlow.Web/Views/Deck/Home.cshtml` returned nothing.
  `grep -rn "Html.Raw\|MarkupString" DeckFlow.Web/Views/Deck/DeckAnalysis.cshtml DeckFlow.Web/Views/Deck/Home.cshtml DeckFlow.Web/Views/Shared/_DeckToolTabs.cshtml` returned nothing.
  `git diff --stat` during each task stayed within the fenced files for this plan.

## Deviations

- The worktree was already dirty before execution because of unrelated pre-existing changes in `.planning/ROADMAP.md`, `.gstack/`, `.superpowers/`, and `SECURITY.md`; they were left untouched and unstaged.

## Requirement Coverage

- `RET-03`: preserved the KB reference path by keeping browse/detail intact and exposing `/content-kb` on fresh DBs via the default-on seed.
- `RET-04`: covered by the default-on seed plus `ContentKbMarkdigXssTests.cs`, with `.DisableHtml()` and zero raw HTML sinks on KB views re-verified.
- `RET-06`: covered by the new deck-analysis pointer note and the home/nav copy rewrite to direct users to browsable KB entries with copyable prompts instead of removed injection behavior.
