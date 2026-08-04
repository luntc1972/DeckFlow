# SEO Ladder — P0 through P3

Branch: `feat/seo-ladder`. Worktree: `../deckflow-seo-ladder`.

Origin: an external SEO review of the live site (www.deckflow.gg). Roughly 60% of that review
asked for infrastructure this repo already ships (canonical, OG, Twitter, JSON-LD, sitemap,
robots.txt, per-page titles/descriptions). This plan covers only what the repo audit confirmed
is genuinely missing or broken, plus the defects the external review could not see.

## Ground truth (verified 2026-08-04, do not re-derive)

- `DeckFlow.Web/Seo/SeoPaths.cs` — `Indexable` is a static 20-path array; `Tools` is derived as
  `Indexable` minus `NonToolPages`; `Normalize` lowercases and strips trailing slash;
  `IsShareablePage` returns `/` or a member of `Tools`.
- Consumers of `SeoPaths` (complete census):
  | Consumer | Member used | Effect |
  |---|---|---|
  | `DeckFlow.Web/Controllers/SitemapController.cs:42` | `Indexable` | sitemap `<loc>` list |
  | `DeckFlow.Web/Seo/StructuredDataBuilder.cs:46` | `Tools` | tool-page JSON-LD graph |
  | `DeckFlow.Web/Seo/StructuredDataBuilder.cs:41` | `Normalize` | path matching |
  | `DeckFlow.Web/Views/Shared/_Layout.cshtml:87` | `IsShareablePage` | renders the share bar |
  | `DeckFlow.Web.Tests/SeoPathsTests.cs` | `Normalize`, `IsShareablePage` | unit tests |
- `StructuredDataBuilder.ForPath` branch order is `/` → `IsHelpDetail` → `Tools` → fallback.
  Help-detail therefore wins over `Tools` regardless of what `Indexable` contains.
- `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` short-circuits a flag-off action to
  HTTP 404. `SeoPaths.Indexable` has no flag awareness, so the sitemap can advertise 404s.
- Live prod check, 2026-08-04: `/cut-lab` → 404, `/content-kb` → 404, all other sitemap URLs → 200.
- `DeckFlow.Web/Services/Tools/ToolRegistry.cs` — each `ToolDefinition` carries `Route`, `FlagKey`,
  `Label`, `TileTitle`, `TileDescription`, `HelpSlug`. This is the route→flag join source.
- `DeckFlow.Web/Controllers/HelpController.cs:46` — the help content dependency is
  `IHelpContentService`. `VisibleTopics()` and `IsTopicVisible()` are **private** controller
  methods filtering topics by each topic's optional `RequiresFlag`; they are not reusable as-is.
  18 topic files live in `DeckFlow.Web/Help/*.md`.
- `_Layout.cshtml` — `defaultDescription` line 3, description resolution 69-70, title computation
  71-72, canonical construction 78, emission 99-101, og/twitter 102-111, JSON-LD 112.
- Views missing `ViewData["Description"]`: `Views/Deck/Bracket.cshtml`,
  `Views/Deck/DeckHistory.cshtml`, `Views/Deck/CutLab.cshtml`, `Views/ContentKb/Detail.cshtml`.

## Decisions taken

- **D-1 — Sitemap becomes flag-aware.** A page whose feature flag is off must not appear in
  `sitemap.xml`. This resolves `/cut-lab` without anyone flipping a production flag, and every
  future flag-gated tool inherits the behavior.
- **D-2 — `/content-kb` leaves SEO scope.** Content KB is being deprecated. Remove it from the
  indexable set so it stops being sitemapped and stops receiving tool JSON-LD. Its route, homepage
  tile, nav entry and help topic stay untouched — deprecation is a separate ticket. Redirect/410
  handling for the retired URLs also belongs to that ticket, not this one.
- **D-3 — No production flag flips in this branch.** Flag state is an operator action.
- **D-4 — Help topics get sitemap coverage without entering `Indexable`.** See T-2 for why.
- **D-5 — No landing page is created for a feature that does not independently exist.** Combo
  analysis, opening-hand analysis and deck categories are modes or lenses inside existing tools;
  a dedicated page for each would be thin. Only Set Upgrade Analysis and DeckFlow Bridge qualify.

## Wave P0 — defects

### T-1 Flag-aware sitemap, and `/content-kb` out of the indexable set

`SitemapController` currently projects `SeoPaths.Indexable` directly. It must instead emit only
paths that are currently reachable.

Behavior required:
- A path that maps to a `ToolDefinition` whose `FlagKey` is disabled in `IFeatureFlagCache` is
  omitted from `sitemap.xml`.
- `/help` is gated by `tool.help.enabled` and must obey the same rule.
- `/`, `/about`, `/feedback` have no flag and are always emitted.
- Ordering of the surviving entries is unchanged.
- `/content-kb` is removed from `SeoPaths.Indexable` outright (D-2). Note the knock-on: because
  `Tools` is derived from `Indexable`, `/content-kb` also stops getting tool JSON-LD and stops
  rendering the share bar. Both are intended.

Implementation notes: the route→flag mapping must come from `IToolRegistry`, not a second
hardcoded table — a duplicated map is the drift this codebase already avoids by making `SeoPaths`
a single source of truth. The join must match an indexable path against **both**
`ToolDefinition.Route` and `ToolDefinition.AdditionalRoutes`, because T-10 attaches
`/set-upgrade-analysis` to the deck-analysis definition via `AdditionalRoutes`. `SitemapController`
will need `IToolRegistry` and `IFeatureFlagCache` injected; both are registered through the service
extensions invoked at `Program.cs:113-114`.

`SitemapControllerTests.CreateController()` (`DeckFlow.Web.Tests/SitemapControllerTests.cs:97`) is
the repository's only direct construction of this controller and currently calls a parameterless
constructor. It must be updated to supply `IToolRegistry`, a controllable `IFeatureFlagCache`, and
the `IHelpContentService` that T-4 adds, or the test project stops compiling.

`robots.txt` output is unchanged.

### T-2 Meta descriptions and titles for the three uncovered views

Add a unique, non-default `ViewData["Description"]` to `Views/Deck/Bracket.cshtml`,
`Views/Deck/DeckHistory.cshtml` and `Views/Deck/CutLab.cshtml`. Retitle the first two, whose
current titles are bare feature names:

| View | Current title | New title |
|---|---|---|
| `Bracket.cshtml` | `Bracket Check` | `MTG Commander Bracket Checker` |
| `DeckHistory.cshtml` | `Deck History` | `MTG Commander Deck Version Tracker` |

`CutLab.cshtml` keeps its title; it needs only the description.

Do not add a description to `Views/ContentKb/Detail.cshtml` — out of scope per D-2.

The layout appends `" - DeckFlow"` (`_Layout.cshtml:72`). Titles above are written without the
suffix. Do not change the separator; the external review's `"| DeckFlow"` suggestion is cosmetic
and would churn every page title for no ranking benefit.

Descriptions must be distinct from each other, from the sitewide default, and from every existing
page description. Each should state what the tool does and what it returns.

### T-3 Repair `PageMetadataViewTests`

Two independent defects make this test vacuous:

1. Its `IndexableViews` list is a hardcoded 17 entries while `SeoPaths.Indexable` holds 20 paths.
   The three views from T-2 are simply not checked. The list must be **derived from
   `SeoPaths.Indexable`** via a path→view-file map so it cannot drift again; a path with no
   corresponding view file must fail the test loudly rather than be skipped silently.
2. Its `DefaultDescription` constant no longer matches `_Layout.cshtml:3`. The "no page reuses the
   sitewide default" assertion therefore compares against a string no page could emit, and passes
   for the wrong reason. The constant must be **read from `_Layout.cshtml` at test time**.

Prove the repaired test actually bites: with T-2 reverted, it must fail. State that result in the
task report — a green run alone is not evidence.

## Wave P1 — coverage and correctness

### T-4 Help topics in the sitemap

All 18 `/help/{slug}` topics are crawlable, already carry TechArticle JSON-LD, and are absent from
the sitemap. Add them.

**Do not add them to `SeoPaths.Indexable`.** `Tools` is derived from `Indexable`, and `Tools`
drives `IsShareablePage`, which `_Layout.cshtml:87` uses to render the share bar. Adding help
slugs to `Indexable` would put a share bar on all 18 help pages — a visible UI change from a
metadata-only task. JSON-LD itself would survive, because `ForPath` tests `IsHelpDetail` first.

Instead the sitemap composes its own URL set: the flag-filtered `Indexable` list from T-1, plus
help topic paths resolved from `IHelpContentService` and filtered by the same per-topic
`RequiresFlag` rule.

That rule currently lives in `HelpController.IsTopicVisible()`, which is private and therefore not
reusable. Extract it to a shared seam — a method on the help content service, or a small helper
both controllers depend on — so the visibility rule is stated once and cannot drift between the
help pages and the sitemap. Do not restate the predicate in `SitemapController`.

`/content-kb/{id}` detail pages are excluded (D-2).

### T-5 Canonical normalization

`_Layout.cshtml:78` builds the canonical from raw `Request.Path` and `Request.Host`, so
`/Manabase`, `/manabase/` and `/manabase` each self-canonicalize as distinct URLs.

Required behavior:
- The canonical path is normalized — lowercased, trailing slash stripped except at root. This is
  exactly what `SeoPaths.Normalize` already does; apply it rather than writing new logic.
- `og:url` must agree with the canonical, since both derive from the same value.
- Also enable lowercase URL generation so emitted links match.

Do **not** add an apex→www redirect in application code — that is Render/DNS configuration and
does not belong in the request pipeline.

Known related inconsistency, in scope to fix while here: the JSON-LD `WebSite` fallback node
hardcodes `https://www.deckflow.gg` (`StructuredDataBuilder.cs:60`) while the canonical uses the
request host, so the two can disagree on any other host. Make the fallback take the same base URL
the other branches already receive.

### T-6 Homepage introduction

`Views/Deck/Home.cshtml:21` currently reads:

> Personal Magic: The Gathering deck tooling — analysis, sync, reference, and categories.

"Personal" reads as private tooling and suppresses every commercial-intent term. Replace with copy
that names Magic: The Gathering, Commander, cEDH, and the concrete capabilities — mana base
analysis, bracket checking, deck comparison, deck primers, version tracking. Keep it to the
existing single-paragraph lede; do not keyword-stuff and do not restructure the hero.

The `<h1>` and the page title/description at `Home.cshtml:9-10` are already strong — leave them.

## Wave P2 — linking and the Bridge page

### T-7 Homepage tile labels

Tile anchor text comes from `ToolDefinition.TileTitle`; the nav dropdown uses `Label`
(`ToolRegistry.cs:12-27`). Several are bare feature names that carry no search intent.

| Tool key | Current `TileTitle` | New `TileTitle` |
|---|---|---|
| `manabase` | Mana Base | Commander Mana Base Analyzer |
| `bracket` | Bracket Check | Commander Bracket Checker |
| `deck-history` | Deck History | Deck Version Tracker |
| `deck-sync` | Deck Sync | Moxfield–Archidekt Deck Sync |
| `convert` | Convert Deck | MTG Decklist Converter |
| `suggest-categories` | Category Suggestions | Commander Deck Tag Suggestions |
| `commander-categories` | Category Reference | Commander Category Reference |

Leave `Label` alone — the nav dropdown is width-constrained and its short labels are correct there.

The literal `TileTitle` strings are asserted in `DeckFlow.Web.Tests/Tools/ToolRegistryTests.cs` at
lines 22, 25-26, 29-30 and 35-36; all seven changes break that test and it must be updated.
`HomeTilesViewTests` asserts no title strings — do not look for them there. Verify the longer
titles do not wrap badly in the tile grid at mobile width.

### T-8 Contextual cross-tool links

No tool page links to a related tool today. Add one short contextual link block per page, using
descriptive anchor text, on these pairs:

- Mana Base → Deck Analysis
- Deck Analysis → Set Upgrade Analysis (T-9), Deck Primer
- Deck Comparison → Deck History
- cEDH Meta Gap → Deck Comparison
- Bracket Check → Deck Analysis
- Card Lookup → Mechanic Lookup

Links must be real anchors, must not appear when the target tool's flag is off, and must not
disturb the existing page layout. Layout CSS goes in `site-common.css`, never `site.css`.

### T-9 DeckFlow Bridge page

`wwwroot/extension-install.html` exists as a static file and is in no sitemap. Give it a real route
at `/deckflow-bridge` that renders through the normal layout, so it inherits title, description,
canonical, OG and JSON-LD like every other page. Add it to the indexable set.

It has four **application** consumers, not two. All must be updated, and the legacy static URL
redirected to the new route rather than left to 404:

| Consumer | Location |
|---|---|
| Bridge hint partial | `Views/Shared/_DeckFlowBridgeHint.cshtml` |
| Help topic | `DeckFlow.Web/Help/browser-extension.md` |
| Layout, supplied to the extension bridge | `Views/Shared/_Layout.cshtml:118` |
| Hardcoded TS fallback | `wwwroot/ts/moxfield-extension-bridge.ts:58` |

The TypeScript change recompiles at build; never stage the emitted `wwwroot/js/*.js`.

`README.md` also references the legacy path at lines 278, 283 and 780. Update those to the
canonical route — documentation only, no runtime effect, since the redirect keeps the old URL
working.

The page explains what the Bridge extension is, what it solves, how to install it, and links to
the tools that benefit. Do not imply affiliation with Moxfield or Archidekt.

## Wave P3 — Set Upgrade Analysis landing page

### T-10 `/set-upgrade-analysis`

Set Upgrade Analysis is real — steps 4 and 5 of `/deck-analysis`, with dedicated prompt builders
under `DeckFlow.Web/Services/PromptBuilders/SetUpgrade/`. It has no URL of its own.

Create a server-rendered landing page at `/set-upgrade-analysis` that explains what the analysis
evaluates: a new Magic set against an existing Commander deck, producing suggested additions,
suggested cuts, strict upgrades, lateral moves, potential traps, and bracket/power-level
considerations. Explain who it is for, what input it takes, what it returns, and how it differs
from a generic "new set review" article.

Constraints:
- Roughly 500–1000 words of original copy. No filler to reach a count.
- **Duplicates no application logic** — it links into the existing `/deck-analysis` workflow.
- One `<h1>`, logical `<h2>`/`<h3>` beneath it.
- Unique title and description. Suggested title: `MTG Commander Set Upgrade Analysis`.
- Added to the indexable set, and gated on `tool.deck-analysis.enabled` via `[FeatureFlagGate]` —
  the page is worthless if the workflow it points at is dark.
- **Adding the path to `SeoPaths.Indexable` alone does not give T-1 a flag to check.** The
  route→flag join reads `ToolRegistry`, and the deck-analysis definition maps only
  `/deck-analysis`. Add `/set-upgrade-analysis` to that definition's `AdditionalRoutes` so the
  sitemap drops the page whenever the flag is off. Without this the new page reintroduces exactly
  the defect T-1 exists to fix.
- `AdditionalRoutes` consumers were enumerated during plan review. Navigation is unaffected —
  highlighting uses `ToolDefinition.Tab` and links use the primary `Route`
  (`Views/Shared/_DeckToolTabs.cshtml:22,35`). No production code reads `AdditionalRoutes` today;
  T-1's sitemap join becomes its first. Three test consumers need attention:
  - `DeckFlow.Web.Tests/Tools/ToolRegistryTests.cs:21` — the deck-analysis expectation supplies no
    additional route. Add `/set-upgrade-analysis`.
  - `DeckFlow.Web.Tests/Tools/ToolRegistryTests.cs:48` — combined primary+additional route count is
    fixed at 22. It becomes 23.
  - `DeckFlow.Web.Tests/Tools/ToolRouteGateCoverageTests.cs:79` — associates actions beneath
    additional routes with their tool's flag. Stays green **only because** this task requires
    `[FeatureFlagGate("tool.deck-analysis.enabled")]` on the new action. Do not drop that attribute.
- Links to Deck Analysis and at least one other related tool.

## Tests

Add to the existing xUnit projects and Playwright e2e suite; introduce no new framework or package.

- Sitemap omits a tool path when its flag is off, and includes it when on (T-1). Prove by
  toggling a fake flag cache, not by asserting the current live set.
- Sitemap contains no `/content-kb` path (T-1). Two existing assertions in
  `DeckFlow.Web.Tests/SitemapControllerTests.cs` contradict this plan and must be dealt with
  explicitly, not discovered at build time: line 54 requires `/content-kb` to be present, and line
  59 asserts exactly 20 URLs. Replace the first; the fixed count is brittle across T-1, T-4, T-9
  and T-10 and should be replaced by set-membership assertions rather than recalculated.
- Sitemap contains every visible help topic and omits a topic whose flag is off (T-4).
- Sitemap excludes admin, api and error routes (regression guard).
- `PageMetadataViewTests` derives its view list from `SeoPaths.Indexable` and its default string
  from `_Layout.cshtml`; a path with no mapped view fails (T-3).
- Every indexable view has a unique non-default description, now genuinely covering all of them.
- Canonical is normalized for a mixed-case and trailing-slash request, and `og:url` matches (T-5).
- Rendered-HTML assertions that canonical, `og:*`, `twitter:*` and the JSON-LD script tag are
  actually present in output — no such test exists today; all current coverage is regex over
  source files.
- New routes return 200 and each has exactly one `<h1>` (T-9, T-10).
- `robots.txt` still references the sitemap absolutely.
- JSON-LD on every new page parses as valid JSON.
- The skipped admin-noindex test at `SitemapControllerTests.cs:63` — either repair it or record
  in the task report why it must stay skipped.
- e2e smoke route list currently omits `/bracket`, `/cut-lab`, `/deck-history`, `/content-kb`. Add
  the ones that remain in scope.

## Constraints

- Preserve each touched file's existing line endings exactly. Detect them per file — some are LF,
  some CRLF. Do not convert, normalize or "fix" EOL, and do not assume a repo-wide style. Change
  only the lines whose content actually changes; leave every other line and its ending
  byte-for-byte identical.
- Never stage compiled `wwwroot/js/*.js` — gitignored, rebuilt at deploy.
- Layout CSS belongs in `site-common.css`, not `site.css`.
- No new packages. No new test framework.
- Do not claim DeckFlow is an official Wizards of the Coast product, and do not imply affiliation
  with Moxfield, Archidekt, EDHREC, Commander Spellbook or Wizards of the Coast. Preserve existing
  trademark and fan-content disclaimers.
- One logical change per commit, conventional commit messages, plain default author.
- Build clean and run the suite after each wave.

## Out of scope

- Flipping any production feature flag.
- Content KB deprecation beyond removing it from the indexable set.
- Landing pages for combo analysis, opening-hand analysis or deck categories (D-5).
- A `/knowledge-base` route — `/content-kb` already exists and is being retired.
- "Saved Analysis Sessions" — no such feature exists; only zip upload/resume on existing tools.
- Sitemap `<lastmod>` — no accurate per-page modification date is tracked, and a fabricated
  timestamp on every request is worse than omitting the element.
- Per-page OG images, `hreflang`, slug-based content-kb URLs.
- Submitting the sitemap in Google Search Console — an operator action, still outstanding.
