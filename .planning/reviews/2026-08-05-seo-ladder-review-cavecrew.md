# SEO Ladder Branch Review (feat/seo-ladder)

**Date:** 2026-08-05  
**Scope:** `git diff main...HEAD` (17 commits)  
**Focus:** P2 commits (ec1e2327, 7f46fb11, 5fd22478) and newest (4171f61a, d6d00b16)

## Findings

No issues.

### What was checked

**SeoPaths as single source of truth:**
- SitemapController.SitemapXml() filters by SeoPaths.Indexable ✓
- StructuredDataBuilder.ForPath() checks SeoPaths.Tools ✓
- _Layout.cshtml uses SeoPaths.Normalize() for canonical URL ✓
- PageMetadataViewTests maps all indexable paths via the same coordinate system ✓
- No drift path exists; all four consumers derive from the same SeoPaths.Pages array

**SeoPaths refactoring (commit 7de2786d):**
- /content-kb moved from Indexable to (IsIndexable=false, IsTool=true) ✓
- Correct: tool retains WebPage JSON-LD + share bar, but is excluded from sitemap
- No tool whose JSON-LD needs to render can fall outside this design

**Unflagged indexable paths:**
- All 15 indexable tools (/sync, /convert, /card-lookup, /mechanic-lookup, /deck-analysis, /deck-comparison, /cedh-meta-gap, /deck-primer, /suggest-categories, /commander-categories, /judge-questions, /manabase, /bracket, /deck-history, /cut-lab) are registered in ToolRegistry ✓
- /set-upgrade-analysis: AdditionalRoute of deck-analysis (commit d6d00b16, ToolRegistry), gated by deck-analysis flag ✓
- /deckflow-bridge: ungated by design (correct — extension install page is always-reachable) ✓
- /help: special-cased in SitemapController line 86–88, gate on both Index and Topic actions ✓
- /, /about, /feedback: ungated landing pages (correct) ✓

**Cross-tool links (commit 7f46fb11 + d6d00b16):**
- Bracket.cshtml → deck-analysis: `FlagCache.IsEnabled("tool.deck-analysis.enabled")` ✓
- CardLookup.cshtml → mechanic-lookup: `FlagCache.IsEnabled("tool.mechanic-lookup.enabled")` ✓
- CedhMetaGap.cshtml → deck-comparison: `FlagCache.IsEnabled("tool.deck-comparison.enabled")` ✓
- DeckAnalysis.cshtml → deck-primer: `FlagCache.IsEnabled("tool.deck-primer.enabled")` ✓
- DeckComparison.cshtml → deck-history: `FlagCache.IsEnabled("tool.deck-history.enabled")` ✓
- Manabase.cshtml → deck-analysis: `FlagCache.IsEnabled("tool.deck-analysis.enabled")` ✓
- SetUpgradeAnalysis.cshtml: bracket, deck-history, manabase all conditionally linked with correct flags ✓

**Help kill-switch (commit 4171f61a):**
- HelpController.Index: `[FeatureFlagGate("tool.help.enabled")]` ✓
- HelpController.Topic: `[FeatureFlagGate("tool.help.enabled")]` ✓
- Both methods gate on the same flag key; kill-switch works ✓
- New reflection test (HelpControllerFlagGateTests) locks both in place ✓

**Casing and routing:**
- Extension legacy redirect: `context.Request.Path.Equals("/extension-install.html", StringComparison.OrdinalIgnoreCase)` ✓
- RouteOptions.LowercaseUrls = true: all hardcoded route strings in ToolRegistry are lowercase ✓
- SitemapController.IsReachable line 92: `definition.AdditionalRoutes.Contains(path, StringComparer.Ordinal)` — safe because paths are already normalized lowercase by ASP.NET before reaching this code ✓

**Page metadata:**
- Bridge: ViewData["Title"] and ViewData["Description"] set ✓
- SetUpgradeAnalysis: ViewData["Title"] and ViewData["Description"] set ✓
- Both pages added to PageMetadataViewTests.IndexableViewFiles ✓

**Tests:**
- SitemapControllerTests verifies /set-upgrade-analysis vanishes when deck-analysis flag is off ✓
- SetUpgradeAnalysisControllerTests verifies metadata, single h1, at-least-one related-tool link, and flag gates ✓
- BridgeControllerTests verifies metadata and legacy redirect ordering ✓

---

**totals:** 0 issues
