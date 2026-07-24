# Personal Tools: Admin Reframe of Cycle 17 (Creator-Style Deck Intelligence)

*Design spec, 2026-07-24. Supersedes the public-launch surface designed in Phase 100.*

## Problem

Cycle 17 (phases 94–100, branch `plan/cycle-17-creator-style`, head `6da5eb42`) is code-complete
and verified, but has never shipped. Two things blocked it:

1. **Legal.** Crawling third-party creators' decklists to build a public product feature carries
   risk the project is not willing to take (see the 2026-07-19 advertising/legal review, which
   turned creator-crawl off).
2. **Drift.** The branch forked at `5709f37c` (2026-07-06). Cycles 18 and 19 have since landed on
   `main`, leaving it 777 commits behind and 18 days stale.

The decision taken 2026-07-24: **reframe the feature from a public tool to an admin-only personal
tool.** The output never reaches the public, which resolves the legal concern, and the same
treatment establishes a home for future owner-only features.

## Decisions

| # | Decision | Rationale |
|---|---|---|
| D-01 | Route moves to `/Admin/CreatorStyle`, gated by the existing BasicAuth branch | `Program.cs:238` already gates `/Admin/*`; no new auth primitive |
| D-02 | Establish a personal-tools section under the `/Admin` landing | Owner-only features get a home instead of ad-hoc placement |
| D-03 | Creator crawl stays in scope, admin-only | Output never leaves the owner's login |
| D-04 | Fresh branch `feat/personal-tools` off `main`; port code, skip planning docs | Code diff is `+34,514 / −895` across 291 files; planning-doc diff is `−57,732` of pure archival churn |
| D-05 | Deck Tendencies ports in the same effort | It is stacked on cycle-17 and is already admin-only |
| D-06 | Stated rules are hand-authored from the P89/P90 prototype, not re-distilled | `yt-dlp`/`ffmpeg`/`whisper` are absent from PATH; installing them is a new system dependency |
| D-07 | Definition of done is a working tool with real data, not an empty surface | Stated goal was "so I can use the features" |

### Rejected alternatives

- **Rebase the 363 commits onto main.** Every planning-doc commit conflicts against Cycle 18/19's
  archival of the same directories. History remains available at
  `origin/plan/cycle-17-creator-style`, so replaying it buys nothing.
- **Keep the public `/creator-style` route with an admin check bolted on.** Adds an auth path
  outside the `/Admin` branch and retains flag/registry/SEO plumbing that has no purpose once the
  tool is private.
- **Measured-only fusion.** `ProfileFusionEngine` iterates *stated* rules
  (`foreach … FuseActiveRule(rule, measuredByMetric)`), so zero stated input yields zero
  `FusedTarget[]` and an empty critique. Emitting measured-only passthrough targets would require
  net-new engine logic and would still produce no conflict ledger — the say-vs-do comparison is the
  entire point of the feature.
- **Install the distill toolchain and re-distill 85 videos.** New system dependencies plus an
  unbounded transcription run, when the rules already exist in a research doc.

## Architecture

### Surface

Only the outermost layer changes. The engine (P94–P98) is ported unmodified.

```
Controllers/CreatorStyleController.cs        →  Controllers/Admin/AdminCreatorStyleController.cs
  [HttpGet("/creator-style")]                →  [Route("Admin/CreatorStyle")]
  [FeatureFlagGate(...)]                     →  removed — BasicAuth branch is the gate
  : DeckToolControllerBase                   →  : Controller
Views/Deck/CreatorStyle.cshtml               →  Views/AdminCreatorStyle/Index.cshtml + _ViewStart
```

`DeckToolControllerBase` carries public deck-tool conventions (flag gating, tool nav, workflow step
tabs). Admin controllers in this repo deliberately do not inherit it — `AdminCreatorProfileController`
is a plain `Controller`. Match that.

### Dropped from Phase 100

All of it exists only to make a tool safely public:

| Dropped | Reason |
|---|---|
| `tool.creator-style.enabled` flag + 6 lockstep test suites | The `/Admin` branch is the gate |
| `ToolRegistry` entry, route/count tests, route-gate coverage | No public tile |
| `SeoPaths` / sitemap follow-up | Never indexed; the deferred post-merge item is closed by deletion |
| `Help/creator-style.md` + its `requires_flag` | Public help topic for a private tool |
| `PacketSessionCache` bypass-list entry | Single user; no session-cache contention |
| Public deck-input component, theme + mobile UI review | Admin views use the admin layout, exempt by existing precedent |

### Retained unchanged

Core engine (P94–P98), `CreatorStylePacketService`, `CreatorProfileDeckCrawler`,
`CardGroundingGuard`, `CreatorStyleRubricScorer`, `CreatorStyleSeedLoader`, and the CLI
`fuse-profile` / `creator-style-index-export` commands.

### Personal-tools section

`AdminLandingController` renders `Views/AdminLanding/Index.cshtml` with a section picker. Two
entries are added: Creator Style and Deck Tendencies. `AdminCreatorProfileController` (Deck
Tendencies) already declares `[Route("Admin/CreatorProfile")]` and ports as-is. No new gating
primitive; both inherit BasicAuth.

## Data path

`CreatorStyleProfileStore` binds to `content-kb.db`, a local SQLite file
(`DeckFlowDatabaseConnectionFactory.cs:72`) — **not** the production Postgres. Phase 100 solved
production hydration with git-shipped JSON seeds, and that code is built and tested. Seeds exist
today as `[]` placeholders:

- `content-kb/seed/creator-style-profiles.json`
- `content-kb/seed/creator-deck-cache.json`

`CreatorStyleSeedLoader` hydrates from them at startup. Production reads seeds only and never
crawls.

### New component: stated-rules importer

Hand-authored rules must reach `content_stated_rules` before fusion runs, and today the only writer
is the distill pipeline. Therefore:

- **`content-kb/seed/creator-stated-rules.json`** — version-controlled, hand-authored.
- **`creator-style-import-stated <file>`** — new CLI command loading that file into the local store
  via the existing `InsertStatedRuleAsync`.

Each rule carries `Provenance = "hand-authored"`. A future re-distill then supersedes it through the
existing `RecencyCollapser` rather than duplicating it.

### Source of the stated rules

`docs/research/p89-p90-prototype-snail.md` (Fable prototype, 2026-07-05) extracted these from 41
real artifacts, already in `StatedRuleCandidate` shape:

> lands 37–42 (28 for low-curve + aggressive-mull) · ramp 7–12 · draw 13–18 · removal 8–14
> (15–20 broad) · interaction ~20 slow / 5–8 proactive · board wipes 3–5 max · counterspells ≥8 in
> blue · tutors ~3 at Bracket 2 · copies-to-see-in-opener ≥10

The same document holds the P90 fusion table — six metrics with verdicts computed against the real
39-deck measured data, and the source the P97 goldens were built from. The flagship result ("board
wipes: agreement, not hypocrisy") is therefore reproducible with no distill tooling.

### Operator run (local, one time)

```
1. /Admin/CreatorProfile → crawl SalubriousSnail (39 decks)  → measured profile
2. CLI creator-style-import-stated content-kb/seed/creator-stated-rules.json
3. CLI fuse-profile salubrioussnail                          → FusedTarget[] + conflict ledger
4. CLI creator-style-index-export                            → writes the two seed JSONs
5. commit seeds → push → Render deploy → /Admin/CreatorStyle live
```

## Port plan

Fresh branch `feat/personal-tools` off `main`. Six commits, reframing during the port rather than
porting-then-deleting:

| # | Commit | Content |
|---|---|---|
| 1 | Core engine | P94–P98: profile records, store, measured extraction, stated extraction, fusion, grounding guard. Purely additive; no conflicts with main. |
| 2 | Web services | `Services/CreatorStyle/*`, seed loader, DI registration |
| 3 | Shared-infra reconciliation | `ScryfallCollectionResolver`, `ScryfallLimits`, `CachedNameResolution`, dedicated archidekt resilience pipeline |
| 4 | Admin surface | `AdminCreatorStyleController` + `Views/AdminCreatorStyle/`, minus flag/registry/SEO/help |
| 5 | Deck Tendencies | `AdminCreatorProfileController`, view, `DeckTendenciesReport` — from `feature/deck-tendencies` |
| 6 | CLI + seeds | `fuse-profile`, `creator-style-index-export`, new `creator-style-import-stated`, the authored `creator-stated-rules.json` plus the two existing `[]` seed placeholders, admin landing entries, README |

**Commit 3 is the only real conflict surface.** Cycle 17's `/simplify` rounds promoted a neutral
`ScryfallCollectionResolver` and deleted a third duplicate copy from `ManabaseAnalysisService`.
That is *main's* code improved on a stale branch, and Cut Lab edited the same files across Cycles 18
and 19. It must be re-derived against current `main` line by line, not applied wholesale.

Porting-then-deleting is rejected because it would import six flag-lockstep suites asserting exact
counts that Cut Lab has since changed. They would fail on arrival, costing a debugging cycle for
code slated for deletion.

## Testing

Port the existing suites (Core ~1433, Web ~1374 at branch head), less the deleted public-surface
tests: flag lockstep ×6, `ToolRegistryTests` counts, route-gate coverage, SEO/sitemap assertions.
`CreatorStyleViewRenderTests` is rewritten against the admin view.

Add:

- Admin-route BasicAuth coverage, matching `AdminCreatorProfileController`'s existing tests.
- Round-trip test for the stated-rules importer (JSON → `content_stated_rules` → read back).
- A fusion test asserting the hand-authored Snail rules reproduce the P90 verdict table.

Gates before merge, per project standing rules: `dotnet build` clean, full Core and Web suites
green, screenshots at two viewports for both admin views, `/simplify` run on the change, README
updated. Admin e2e follows the existing serialize/throttle convention; there is no public e2e spec
because there is no public route.

## Risks

| Risk | Mitigation |
|---|---|
| Commit 3 conflicts with Cut Lab's edits to the same shared files | Re-derive against `main` line by line; full manabase + Scryfall suites must stay green |
| Hand-authored rules drift from what the creator actually said | `Provenance = "hand-authored"` marks them; a later re-distill supersedes via `RecencyCollapser` |
| Crawl of 39 decks exceeds Render's 512MB / request timeout | Crawl runs locally only; production reads seeds |
| Stale server on `:5173` produces false e2e failures | Probe with `netstat` and kill before any e2e run (recurring hazard, hit 4× during Cycle 17) |

## Out of scope

- Installing the distill toolchain or re-distilling Snail's 85 videos.
- Any public launch of creator-style, including sitemap or SEO wiring.
- Postgres migration of the creator-style stores.
- Pet-card detection — superseded pending the EDHREC integration under consideration for Cycle 20.
