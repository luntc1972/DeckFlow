# DeckFlow

DeckFlow helps deck builders translate decks between Moxfield and Archidekt without manual editing. It also provides AI prompt-building workflows for single-deck analysis, cEDH meta-gap analysis, and head-to-head deck comparison, a deterministic mana-base analyzer, Commander Spellbook combo lookup, Scryfall card and mechanic references, an Ask-a-Judge handoff flow, public feedback capture, and a cache-backed category suggestion engine.

## User help
End-user documentation is served by the running web app at `/help` (feature guides) and `/about` (version, source, credits). This README keeps the developer-facing material (build, publish, API, CLI, deployment).

**Repository description (≤350 characters):** DeckFlow unifies Moxfield/Archidekt decks and generates paste-ready AI prompts (analysis, deck primer, comparison, cEDH meta-gap), plus a mana-base analyzer, deck diffs, card/mechanic lookup, Ask-a-Judge handoff, and a browsable MTG content-creator knowledge base. Live at deckflow.gg.

## User Feedback

A public **Feedback** form is linked in the site footer (`/feedback`). Submissions are stored through DeckFlow's relational storage provider. SQLite is the default and stores `feedback.db` at `$MTG_DATA_DIR/feedback.db` (falling back to `./artifacts/feedback.db` in development). Postgres can be enabled with the database environment variables below.

An admin page at `/Admin/Feedback` displays submissions with filters for status and type, and lets you mark items Read, Archive, or Delete them.

### Admin configuration

Set these environment variables (via `fly secrets set ...` on Fly.io or the Render env var UI):

- `FEEDBACK_ADMIN_USER` — basic auth username for all `/Admin/*` pages.
- `FEEDBACK_ADMIN_PASSWORD` — basic auth password.
- `FEEDBACK_IP_SALT` (optional) — salt for hashing submitter IPs. If unset, a random 32-byte salt is generated on first run and persisted in the feedback metadata table.

Basic auth covers the whole admin shell: Dashboard (`/Admin`), Feedback, Flags, Harvest, Analytics, Content KB curation, and YouTube Export. If `FEEDBACK_ADMIN_USER` or `FEEDBACK_ADMIN_PASSWORD` are not set, `/Admin/*` returns **503 Service Unavailable**. The public `/feedback` form continues to accept submissions.

Public submissions are rate-limited to 5 per hour per IP.

### Feedback rate-limit identity (CF-Connecting-IP, Phase 5)

The feedback-submit rate-limit policy in `DeckFlow.Web/Program.cs` derives its
partition key from the `CF-Connecting-IP` request header (set by Cloudflare to
the originating client IP). The same helper, `Program.DeriveCloudflareClientIp`,
also drives the admin brute-force throttle — single source of truth for both
surfaces.

Spoofing `X-Forwarded-For` cannot rotate the partition key (the helper does not
read that header). The Phase 03 immediate-peer-IP shape (`peer:<RemoteIpAddress>`)
was rewritten in Phase 5 because Render's edge fans inbound traffic across
multiple proxy IPs, fragmenting per-client buckets — see Phase 5 Plan 05-02.

This trust-the-header model requires that the Render container origin be
reachable only via Cloudflare; otherwise `CF-Connecting-IP` is spoofable by a
direct-to-origin attacker. See "Admin throttle" below for the Render Inbound IP
Rules prerequisite — it covers both surfaces.

If `CF-Connecting-IP` is missing on a request, the partition falls back to
`feedback:unknown` (or `admin:unknown` for /Admin/* requests) and a warning is
logged. All unidentifiable traffic shares one bucket, fail-closed.

### Admin throttle (Phase 5, BUG-02)

The `/Admin/*` routes (feedback console) are protected against basic-auth
brute-force by an application-layer throttle:

- **Lockout window:** 10 failed authentication attempts per client IP within a
  15-minute fixed window. The 11th attempt returns `429 Too Many Requests` with
  a `Retry-After` header value (seconds until window reset, in the range 1..900).
- **Persistence:** the throttle state is stored in Postgres
  (`admin_brute_force_buckets` table), so a deploy or container restart does NOT
  reset accumulated failure counts. There is no brute-force amnesty window on
  redeploy.
- **Client IP source:** the throttle partitions on the `CF-Connecting-IP`
  request header (same helper as the feedback rate-limit). Cloudflare always
  sets this to the originating client IP, so the partition key is stable per
  real client (not fragmented across the Render edge's multi-proxy IP fan-out).
- **Successful auth does NOT increment the bucket.** Only `Challenge`-emitted
  401s (missing/malformed/invalid credentials) count toward the throttle.

#### Spoof-prevention prerequisite (REQUIRED for production)

The `CF-Connecting-IP` header is trusted only because Cloudflare proxies all
inbound traffic. To prevent an attacker from reaching Render's container origin
directly and supplying a fake `CF-Connecting-IP` header, configure **Render Inbound IP Rules**
to allow only Cloudflare's published CIDR ranges:

- Render docs: https://render.com/docs/inbound-ip-rules
- Cloudflare IPv4 CIDRs: https://www.cloudflare.com/ips-v4/
- Cloudflare IPv6 CIDRs: https://www.cloudflare.com/ips-v6/

Render dashboard: deckflow service → Settings → Inbound IP Rules → add the full
Cloudflare list. Cloudflare publishes ~22 IPv4 + ~7 IPv6 CIDRs and announces
changes on the same pages. Refresh the Render allow-list manually if Cloudflare
publishes a CIDR change announcement.

Without this configuration, `CF-Connecting-IP` is spoofable by direct-to-origin
hits and the throttle can be evaded by rotating the header value per request.

#### Operational notes

- Both the admin throttle (`/Admin/*`) and the feedback-submit rate-limiter
  (`POST /feedback`) read from the same `CF-Connecting-IP`-derived partition
  function (`Program.DeriveCloudflareClientIp`), so the spoof-prevention
  requirement covers both surfaces.
- The throttle table grows lazily — one row per distinct partition key. Stale
  rows reset themselves on the next `RecordFailureAsync` after their 15-minute
  window has elapsed. No periodic cleanup job is required.

### Database storage

Feedback and category knowledge/cache storage can use either SQLite or Postgres.

SQLite is the zero-config default:

- unset `DECKFLOW_DATABASE_PROVIDER`, or set `DECKFLOW_DATABASE_PROVIDER=Sqlite`
- optional `DECKFLOW_DATABASE_CONNECTION_STRING`
- if no SQLite connection string is set, DeckFlow stores `feedback.db` and `category-knowledge.db` under `MTG_DATA_DIR`, falling back to `../artifacts`

Postgres is intended for hosted deployments where local files should not be the source of truth:

- `DECKFLOW_DATABASE_PROVIDER=Postgres`
- `DECKFLOW_DATABASE_CONNECTION_STRING=<Postgres connection string>`

DeckFlow creates its feedback and category/cache tables and indexes automatically on first use. You only need to provide the Postgres database, user, and connection string.

`DECKFLOW_DATABASE_CONNECTION_STRING` accepts either Npgsql key=value form (`Host=...;Username=...;Password=...;Database=...`) or a libpq URI (`postgresql://user:pass@host:port/db`, the default format Render and most managed Postgres providers hand out). URIs are normalized internally; URL-encoded passwords and `?sslmode=require` query params are honored.

### Postgres integration tests

By default, `dotnet test` skips Postgres integration tests because they require Docker.

To run them:

1. Ensure Docker (Desktop on Windows/macOS, daemon on Linux) is running and reachable from the test process. On WSL, enable Docker Desktop's WSL integration.
2. Set the env var: `DECKFLOW_POSTGRES_TESTS=1`
3. Run: `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~PostgresStorageTests"`

Testcontainers.PostgreSql will start a `postgres:16-alpine` container, run the tests against the live database, and dispose the container at the end.

## Highlights
- `DeckFlow.Core` contains parsers, diffing logic, exporters, and the Archidekt/Moxfield integrations.
- `DeckFlow.Core.Loading` centralizes deck input loading and Commander deck-size validation so the web app and CLI share the same parsing/import rules.
- `DeckFlow.Web` provides an ASP.NET Core MVC UI for running syncs, AI prompt building, deck-primer generation, cEDH meta-gap analysis, deck comparison prompt building, card lookup, commander category browsing, and category suggestions.
- `DeckFlow.CLI` exposes deck comparison, category harvesting, cache querying, and the local Content KB pipeline (source management, transcript harvest, LLM distillation, site-index export) in a console tool.

### What's new in v1.3
- **AI-agnostic workflow URLs (v1.3 / Phase 12):** `/chatgpt-deck-analysis`, `/chatgpt-deck-comparison`, and `/chatgpt-cedh-meta-gap` now 301-redirect to `/deck-analysis`, `/deck-comparison`, and `/cedh-meta-gap`; page H1s, nav labels, hub labels, and artifact zip filenames use AI-agnostic wording.
- **Claude JSON wrapper cleanup (v1.3 / Phase 999.2):** Claude prompt variants no longer ask Claude to wrap JSON in `<result>...</result>` tags; ChatGPT and Gemini variants are unchanged, and legacy zips still parse through the backward-compatible `<result>` branch.
- **Packet download caching (v1.3 / Phase 999.3):** Deck Analysis, Deck Comparison, and cEDH Meta-Gap download endpoints reuse the Scryfall pipeline result built during preview, so a large Commander deck's download click completes in under 2 seconds instead of 2+ minutes. Cache is in-memory only (process-local, 5-minute TTL, 10MB cap); cache miss falls through silently to the full pipeline.
- **Truncated AI response inline errors (v1.3 / Phase 999.4):** Truncated JSON pasted into the response textarea on Deck Analysis, Deck Comparison, or cEDH Meta-Gap now renders the inline workflow message "The pasted response appears truncated — wait for the AI to finish generating before copying, then re-submit." instead of a generic error page with a raw stack trace.
- **Test hardening and semantic guards (v1.3 / Phase 999.5):** Four pre-existing test failures were fixed, `DeckComparisonService.ParseComparisonResponse` and `MetaGapService.ParseResponse` now reject valid JSON with no meaningful Deck Comparison or Meta-Gap content, and redundant ChatGPT `<result>` prompt directives were removed from five ChatGPT prompt variants.
- **Harvest job lookup fix (v1.3 / Phase 999.6):** `IHarvestRunStore.GetByIdAsync(Guid id, CancellationToken ct = default)` lets `ArchidektCacheJobService.GetJob(jobId)` return completed and terminal harvest job states using provider-specific Guid binding for SQLite and Postgres.

### What's new in v1.4
- **Content Knowledge Base (Phases 19-22):** a local CLI pipeline harvests YouTube captions (Whisper fallback with monthly spend caps), distills each video into a markdown prompt artifact (≤200-word summary, 3-8 timestamped clips, controlled-vocabulary tags) via OpenAI **or** the `claude` CLI ($0 subscription path), and publishes a slim index to the site. The public `/content-kb` browse/detail pages are gated behind the `content.kb.enabled` feature flag; admins curate which entries are visible per entry or per source. See the Content Knowledge Base section below.
- **Category cache rebuild (Phases 24/26/27):** integer-keyed star schema (hot commander aggregate went from a 69s timeout to 0.66ms), read-time `CategoryFilter` fix so colorless staples like Sol Ring always return categories, and content-hash dedup with a 5-day refresh on deck writes.
- **Admin mobile + tooling (Phases 16/18/25):** the admin shell is mobile-responsive (≥320px, ≥44px touch targets), the harvested-decks view is a server-side paged commander grid, and destructive admin actions use a native focus-trapped `<dialog>` confirm modal.
- **Doc-warning gate (Phases 17/23):** every public type and member in `DeckFlow.Web` carries XML doc-comments; the `NoWarn 1591;1573;1587` suppression was removed and the warning gate is live, scoped to `DeckFlow.Web/**`.
- **Removed:** the user-triggered "Run 5-Minute Archidekt Harvest" button on Category Suggestions — harvesting is driven by the background hosted service.

### What's new in v1.5 (shipped 2026-06-10)
- **Deck Primer Generator (Phase 31):** a fourth paste-ready workflow at `/deck-primer` — paste/import a deck and DeckFlow builds a structured "explain this deck" prompt artifact (game plan, key interactions, mulligan/sequencing guidance) with the same download/upload artifact flow as the other generators.
- **Content KB integration (Phases 30/32/33):** distilled creator knowledge could be wired into the deck-analysis prompt (expert pin/follow/evergreen selection + a "What Experts Say" panel), shipped **dark** behind `content.kb.enabled`. *(Note: this prompt-injection path was retired in v1.6 — see below; the KB is now a browse-only reference.)*
- **CLI `--video-ids`:** `harvest` and `distill` accept a comma-separated list of YouTube video ids (plus `--source-id` to disambiguate) to process exactly those videos instead of the most-recent walk.
- **Admin YouTube Export:** `/Admin/YoutubeExport` downloads a channel's upload list (title, view count, upload date, URL) as text or CSV, walking the uploads playlist up to 500 videos.
- **JS test runner + CI:** Vitest + jsdom for the browser TypeScript modules, plus the first GitHub Actions CI (build + xUnit + Vitest).

### What's new in v1.6 (shipped 2026-06-12)
- **Content KB retrieval fix + value re-validation gate (Phases 34/35):** fixed the retriever (per-video clip-diversity cap, topical-fit scoring over tag breadth, prompt-injection sanitizer + Spike-001 regression test), then ran a **blind, multi-deck A/B value gate** on the AI answers. Verdict: **MARGINAL** — the KB clip-injection did not earn its place in the prompt.
- **Retire clip-injection; KB becomes browse-only (Phase 37):** per the recorded gate pivot, whole-channel clip-injection into deck-analysis prompts (the `## Expert Context` block, expert-selection widget, "What Experts Say" panel, retriever services) was **removed**. The Content KB is kept as an un-darked **browse-only reference** at `/content-kb`, and the deck-analysis page points users there for copyable prompts.
- **Rebuild KB corpus + admin block/hard-delete (Phases 37.5/37.6):** corpus reset + high-signal re-harvest under a quality-classifier filter (and a fix so clips carry real mid-video timestamps, not `[00:00]`); admins can block a YouTube video by id so the harvester never re-ingests it and hard-delete its rows.
- **Controller / CLI SRP split (Phase 38):** the `DeckController` god-class was decomposed into 8 focused feature controllers and `DeckFlow.CLI/CommandRunners` into deck-domain vs content-KB runners — **all routes and CLI commands preserved unchanged** (mechanically proven route-parity + a live render smoke).
- **Architecture-review refactor (Phase 39):** duplicated deck-loading + Scryfall card-resolution were extracted out of the four prompt-packet services into a shared `IDeckEntryLoader.LoadFromSourceAsync` + `IScryfallCardResolver` — behavior byte-identical, guarded by the existing packet-service test suites.

### What's new — Mana Base accuracy: mana quantity, ramp credit & color-aware mulligan (Phase 70)

Four accuracy fixes are now **on by default** after a baseline across 8 real decks:

- **Per-source mana quantity:** burst sources now pay their real output (Sol Ring / Ancient Tomb = 2, Gilded Lotus = 3 of one color) on the affordability side, so expensive payoffs read correctly. The Karsten color counts are untouched.
- **Tighter ramp credit:** the land-target reduction for cheap ramp/draw is narrowed to **repeatable** ramp and true card draw — one-shot rituals and Treasure-makers no longer soften the land target.
- **Color-aware mulligan:** the castability simulation's London mulligan now ships hands that are land-count-fine but color-screwed (a 2+ color deck wants 2 colors in its opening lands), lifting cast% toward what real play achieves. Mono-color decks are unchanged.
- **Land-ramp in the simulation:** repeatable land-ramp (Cultivate, Rampant Growth) now puts its fetched land into the simulation as persistent colorless mana, so expensive payoffs in ramp decks read correctly instead of being under-rated. This is the only fix that can improve the overall verdict (never worsen it).

These are toggleable feature flags (`manabase.source-mana-quantity`, `manabase.ramp-credit-v2`, `manabase.color-aware-mulligan`, `manabase.land-ramp-sim`) for safe rollback.

Follow-up accuracy fixes (always on):

- **Board-scaling self cost reducers modeled:** cards that read *"costs {X} less to cast, where X is the greatest power among creatures you control"* (e.g. **The Skullspore Nexus**) are now resolved against the deck's greatest fixed creature power and **auto-applied** to the analysis, so a big-creature deck casts them at their real reduced cost instead of full price. The reduced cost also pre-fills the editable cost-override box, so you can dial in a different on-board assumption.
- **Actionable weakest color:** the flagged "weakest color" is now the color a new source would actually help (the one with the broadest color-limited shortfall), not whichever color happens to own a single expensive late-casting bomb. A curve-limited card no longer makes an over-supported color look like the problem.
- **Honest land advice:** when the deck is below the Karsten land count but the simulation shows every spell still casts fine (a ramp-saturated deck), the header now reads *"~N under the Karsten count, but ramp covers it"* and the "biggest fix" stops recommending lands you don't need.

### What's new — Mana Base accuracy: four-tier scale, curve-aware verdict & cast delay
- **Mulligan-aware source requirements:** the per-color "sources needed" figure now comes from the simulation itself (binary search for the smallest on-color count whose simulated cast % clears the bar) instead of the mulligan-blind hypergeometric. It models **Commander's free first mulligan**, so a tight turn-two `{W}{W}` no longer reads against an inflated requirement (e.g. a real Brago list dropped from a phantom "needs 30 white" to a sane "needs ~21"). The figure is **clamped to Karsten's published table** as a ceiling, so the simulation can only *lower* a requirement, never inflate a double-pip past what the math allows.
- **Four-tier health scale:** the verdict reads on a graded **Excellent / Solid / Workable / Needs work** scale that measures the *mana base*, not the curve. A high-mana-value bomb that casts late because it is expensive (a curve problem the base can't fix) no longer drags the verdict down — only a genuine, fixable color or land shortage does. *Needs work* is reserved for a real, broad shortage (a color short by several sources, two-plus colors short, or lands 2+ short *and* the simulation corroborates the shortage) — a paper land deficit alone never reds the verdict, so a ramp-saturated deck whose cards all cast fine stays out of the red; a single contained color issue is *Workable*; minor notes are *Solid*; a clean base is *Excellent*. Demanding cards are still surfaced by name.
- **Coherent "Biggest fix" callout:** the single most actionable fix is chosen so it never contradicts the land/health line — it points at the color that is genuinely short, else at the land count, else at trimming the top end, and never recommends a negative or "remove" source count.
- **Average cast delay:** the castability table adds an **Avg delay** column — the mean number of turns late each spell first becomes castable (*on curve* when it lands on time, else *+N.N turns*), capped at the grace horizon when it never resolves — as supporting context next to the on-curve %.
- **Deck-load review step:** a **Load deck & detect costs** action resolves the deck and surfaces the auto-detected reduced/alternative-cost suggestions for review/edit *before* you run the analysis.
- **Unsupported-interaction disclosure:** cards the analysis can't fully model — **X / variable costs** (skipped from the castability simulation) and **flexible split pips** (hybrid / Phyrexian / twobrid — no hard color requirement, per Karsten) — are listed by name in a collapsible note so a clean verdict never silently hides them.

### What's new — Mana Base modes & castability (shipped 2026-06-21, `2026.06.7`)
- **Casual / cEDH modes + commander importance:** the Mana Base analyzer now has a **Deck type** selector — *Casual* (Karsten's full land target) or *cEDH* (the competitive ~28–32 land band) — plus a **commander-importance** selector (*Central / Standard / Low*) that controls how hard it holds the commander's colors to threshold (without moving the land target). Both persist across the postback.
- **Per-card castability (Casual):** a worst-first table of each spell's estimated chance to be cast on its on-curve turn, from a Monte-Carlo simulation (London mulligan, joint mana+color, in-sim ramp, fetchlands credited to the colors they can fetch). The commander is pinned; rocks/dorks are counted but not listed. Cross-checked against the [Salubrious Snail](https://www.salubrioussnail.com/manabase-tool) calculator (mean ~3 pts).
- **Aggregate color findings:** each color reflects every card needing it (mean castability + under-supported count) while a single uncastable bomb still surfaces; the weakest color leads.
- **"Show the work" formula panels:** two expandable panels — the methodology, and the Karsten regression evaluated term-by-term for your deck — so any verdict is auditable.

### What's new in Cycle 10 (shipped 2026-06-21, `2026.06.6`)
- **One-click Harvest + Auto-distill in Studio (Phase 59, AUTO-01/AUTO-02):** the Studio Harvest page now has a default **"Harvest + Auto-distill"** action beside the original "Harvest Selected" button. On a **subscription ($0) provider** (`DECKFLOW_LLM_PROVIDER=claude`) one click harvests the selected videos, then distills exactly the *harvest-ready* ones (the videos that actually transcribed — skipped/no-caption/already-distilled picks are excluded) in the same action, with no separate Distill click. A per-video **outcome card** then reports harvested / distilled / auto-approved / left-in-review / dropped / failed (with failed ids) in one place. A small **Auto-approve panel** (on/off toggle + clip cutoff, **default ON at 5 clips**, persisted across Studio restarts) controls whether high-clip distills skip the review queue: a distill with clips ≥ cutoff is auto-flipped to `approval_status='approved'` (it only sets approval status — publishing to prod stays a separate operator-confirmed gate), while below-cutoff distills stay in the review queue. With auto-approve **off**, every distill enters the review queue. On a **metered provider** the one-click action does **not** live-distill (Core refuses unmetered classification on a metered provider) — it harvests, shows a "live distill requires a subscription provider" message, and points you to the manual **Distill** section, whose dry-run spend preview stays available. The original manual harvest/distill flow is kept intact as a fallback (and a completing subscription distill there auto-approves through the same shared step).

- **Pull from Prod — read-only prod→local reconcile in Studio (Phase 60, SYNC-01/02/03; live progress panel Phase 62, SUI-03):** a new **Pull from Prod** page in DeckFlow.Studio that is the read mirror of Direct Push and is **strictly read-only toward production**. Stage 1 reads the live prod `content_site_index` through a dedicated read-only reader (a plain `SELECT` only — no schema-ensure DDL, and the reader exposes no write method at all) and SCP-downloads the prod artifacts into an isolated `pull-staging/` directory (never the live `content-kb/`). A **live Pull Log panel** streams each stage transition (prepare staging → read production content_site_index → download artifacts → classify) and a per-artifact result line ("downloaded …" or "not downloaded: …") as the pull runs, so you can see progress without waiting for the final diff table. All progress copy is sanitized — no connection string, SSH target, absolute path, or raw exception ever appears. It then classifies each entry against your local store into one of four kinds — **prod-newer, missing-locally, local-only, diverged** — omitting anything already in sync. Stage 2 lets you resolve each differing entry **locally**: *adopt-prod* updates the local row's content columns and mirrors prod's `approval_status`, promoting the downloaded artifact into `content-kb/` (a partial pull whose artifact failed to download still updates the row, skipping only the file move); *keep-local* writes nothing. Production is never modified — adopting never auto-publishes, and the prod side has no write path. The prod connection string and SSH target live in user-secrets only and never enter the repo, logs, or any error message.
- **Curated creators + harvest dropdown in Studio (Phase 61, SRC-01/SRC-02):** a new **Creators** page (`/creators`) lets the operator maintain a persisted list of curated creators/channels (add display name + channel URL/handle/ID, view, remove) stored in `content-kb.db` and surviving Studio restarts. The Harvest page's browse section then shows a **creator dropdown** populated from that list — pick a saved creator to fill the browse target instead of pasting a channel URL each time; the paste-URL/handle input remains as the one-off fallback when no creator is selected.
- **Unharvested-only browse + Skip in Studio (Phase 61, HSEL-01/HSEL-02/HSEL-03):** the Harvest browse list now **defaults to showing only not-yet-harvested videos**, with a **"Show all"** toggle to reveal harvested/distilled/approved/published rows. Each candidate also has a **Skip** action (lighter than Block) that hides it from selection without deleting any artifact or writing a harvest blocklist entry; skipped videos are excluded from selection in both views. A single canonical visible projection drives the rendered rows, Select-All, and the harvested set, so a row hidden by the filter or by skip can never be harvested. A **Skipped** page (`/skipped`) lists skipped videos and lets you **un-skip** one to bring it back (the parity partner to Block/Unblock).
- **Consistent status badges + Studio About link (Phase 62, SUI-01/SUI-06):** pipeline status (Not harvested / Harvested / Distilled / Approved / Published / Blocked / Already in DB) now renders from a single shared `Shared/StatusBadge.razor` component on both the Harvest and Review pages — the inline `RenderBadge` switch in `Harvest.razor` is gone and Review's per-row status derives from the same `VideoStatusResolver.FromContentRow(approvalStatus, pushedToProdUtc, isVisible)` pure mapper that `ResolveStatusAsync` now routes through, so the Published/Approved/Distilled rule lives in exactly one place. The leftover Blazor-scaffold "About" link in the Studio layout now points to `https://www.deckflow.gg`.
- **Creator filter on Harvest browse and Review queue (Phase 62, SUI-05):** both the Harvest browse list and the Review queue now show a **"Filter by creator"** dropdown (default "All creators") whenever the current view contains rows from more than one creator. On the Harvest page the creator is derived from each video's `ChannelTitle`; on the Review page it is parsed from the stored `ArtifactPath` (`content-kb/<creator-slug>/…`). The filter composes with all existing filters — Harvest's unharvested-only default and skip exclusion still apply inside the filtered view, and the canonical visible projection (`GetVisibleChannelVideos`) enforces that a row hidden by the creator filter can never be harvested or selected even if it was checked before the filter changed. Publish is out of scope (no per-row list).
- **Tightened harvest→review→publish flow and grouped navigation (Phase 62, SUI-02/SUI-04):** the **Review queue** now shows a **"Go to Publish"** link/button (with an approved-entry count) whenever at least one entry has been approved, so you can jump straight from reviewing to publishing without navigating the sidebar. The Studio **sidebar navigation** is now grouped into a **Pipeline** section (Home → Harvest → Creators → Review → Publish → Direct Push → Pull from Prod) and a **Support** section (Skipped, Blocked) — every existing destination is preserved, and section headers make the flow direction obvious at a glance. The Harvest Select-All was already scoped to the visible/filtered rows (Phase 61 invariant) and is unchanged; per-row checkboxes remain the multi-select mechanism.
- **Self-contained Studio executable (Phase 63, DIST-01):** `DeckFlow.Studio` can now be published as a single-file, self-contained **win-x64** executable (~116 MB) that the operator runs on a clean Windows box **with no .NET install**. A re-runnable publish script (`scripts/publish-studio.ps1` / `.sh`) produces `artifacts/studio-release/` + a dated zip; the executable pins its Kestrel port, writes a crash log, and auto-opens the browser on launch. See [DeckFlow.Studio/STUDIO-SETUP.md](DeckFlow.Studio/STUDIO-SETUP.md) for build/run/secrets steps.

### What's new in v1.7 (shipped 2026-06-17)
- **Visual refresh — 6-pillar UI audit remediation (Phase 48):** the deployed site was audited against six visual-design pillars and remediated from 16/24 to **20/24** — hub cards and section headers gained inline-SVG iconography and resting elevation, surfaces now lift off the page background, the smallest helper text was raised above the legibility floor, headings/labels got a real type hierarchy, and short tool pages (Card Lookup, Ask a Judge) close with an example panel instead of dead space. All changes are theme-token-scoped, so every guild theme (light, dark, and the Commander Table fork) inherits them; verified at mobile + desktop.
- **DeckFlow.Studio — local operator console (Phases 41/45/46/47):** a new standalone Blazor Server app (`DeckFlow.Studio`, run locally by the operator) to browse/paste YouTube videos → harvest captions → distill to Content-KB entries via an LLM (with a spend dry-run gate) → review/approve in a queue → publish to production two ways: a git commit-publish of the LF-normalized seed (→ Render deploy), or a direct prod push (SSH.NET SCP of artifacts to the Render disk + a safe content-columns-only Postgres upsert that preserves admin fields). The prod connection string lives in user-secrets only and never enters the repo or logs.
- **Under the hood:** harvest/distill/export logic moved out of the CLI into `DeckFlow.Core` as `IContentKbOrchestrator` (Phase 42); data access in the dual-provider stores moved to Dapper behind the existing dialect abstraction with Sqlite+Postgres parity preserved (Phase 49); `/Admin/Harvest` got AJAX lazy paging + a `LOWER(commander_name)` index (Phase 44); and a changed-lines `.editorconfig` format gate now runs as a pre-commit hook + CI job (Phase 50).

### Mana base analyzer
- **Deterministic mana-base check (`/manabase`, CLI `manabase`):** load an Archidekt/Moxfield deck (URL or pasted list) and DeckFlow scores it with Frank Karsten's source-count method — recommended land count vs. actual, per-color source supply vs. the toughest spell's requirement, and the weakest color — entirely in-app, **no AI round-trip needed for the verdict**. Cards resolve through Scryfall by exact printing (set + collector number) first, so alternate/flavor names still match; a small optional "copy for ChatGPT" block frames the deficits as a prompt only for the one thing the math can't do — naming specific land swaps. The scoring engine lives in `DeckFlow.Core/Manabase/` (pure, unit-tested); the web page and CLI command are thin surfaces over it. The `/manabase` web page is gated behind the `feature.manabase.enabled` feature flag (default **ON**); an admin can hide the page and its nav link from `/Admin/Flags` without a redeploy (off → 503 maintenance page).
- **Casual / cEDH modes + commander importance:** the page has a **Deck type** selector — *Casual* (default; Karsten's full land target) or *cEDH* (lower land count in the competitive ~28–32 band, fast-mana heavy) — plus a **commander-importance** selector (*Central* / *Standard* / *Low*) that decides how hard the analyzer holds the commander's colors to their threshold (set *Central* for a must-cast-every-game commander like Brago). Both persist across the postback.
- **Per-card castability readout (Casual):** in Casual mode the report adds a **Castability** table — each real spell's estimated chance to be cast on its on-curve turn (on the play), worst-first, with a semantic chip (low / ok / good), an **average delay** column (mean turns late it first becomes castable — *on curve*, else *+N.N turns*), and which factor is limiting it (*mana*, *color: X*, or *mana + color*). The commander is pinned and flagged; mana rocks/dorks and lands are counted in the math but never listed as rows. cEDH mode hides the table and shows a note instead.
- **Mulligan-aware, four-tier verdict:** per-color source requirements are derived from the simulation (modeling Commander's free first mulligan) and clamped to Karsten's published table as a ceiling, so tight double-pips are neither flagged against an inflated count nor pushed past the math. The overall health reads on a graded **Excellent / Solid / Workable / Needs work** scale that measures the mana base, not the curve: a card that casts late only because it is expensive (a curve problem) no longer fails the base, *Needs work* is reserved for a real broad shortage, *Workable* for a single contained color issue, and demanding cards are listed by name.
- **Ramp surfaced:** the result shows how much acceleration the deck runs — the count of mana rocks / dorks (non-land mana sources) plus the ramp/draw pieces at ≤2 mana value — so it's clear what is lowering the recommended land count rather than that math being buried in the formula breakdown.
- **Reduced / alternative cost overrides:** some cards cost far less than their printed mana value — pitch/free spells (Force of Will), board-scaling self-reducers (Blasphemous Act `{8}{R}` usually cast for `{R}`), and evoke/suspend. DeckFlow **auto-detects** these and pre-fills a **"Reduced / alternative costs"** box (`Card Name: cost`, e.g. `Force of Will: 0`, `Blasphemous Act: {R}`); you can edit or clear any line. An applied override replaces the card's effective cost everywhere the math looks — the castability simulation, the on-curve turn, and the per-color source findings — so a free spell stops demanding its colors, and an overridden row is flagged with a `*`. The override is an effective mana *cost* (it can change colors, not just lower the number); `0` makes a card behave like a true 0-cost card.
- **"Show the work" formula panels:** two collapsible panels explain the verdict — *How the analysis works* (the Karsten regression + the Monte-Carlo castability model with London-mulligan, joint mana+color, ramp and fetch crediting, and commander weighting; always shown, credits Frank Karsten, flagged as an estimate cross-checked against community calculators including [Salubrious Snail](https://www.salubrioussnail.com/manabase-tool)) and *This deck's numbers* (the land target and per-color source tally for the entered deck, plus the simulation parameters).

## Getting Started
1. Restore/build: `dotnet build DeckFlow.sln`
2. Run the web app: `dotnet run --project DeckFlow.Web`
3. Use the CLI to compare or harvest decks: `dotnet run --project DeckFlow.CLI -- --help`

### Helper scripts
- `scripts/run-web.sh` — bash wrapper that rebuilds `DeckFlow.Web` and launches it on `http://localhost:5173` with no browser auto-launch.
- `scripts/run-web.ps1` — PowerShell equivalent for Windows terminals.
- `scripts/publish-studio.ps1` — publishes `DeckFlow.Studio` as a self-contained win-x64 single-file executable (no .NET install required on the target machine). Run from Windows PowerShell; produces `artifacts/studio-release/` and `artifacts/DeckFlowStudio-<date>.zip`. See [DeckFlow.Studio/STUDIO-SETUP.md](DeckFlow.Studio/STUDIO-SETUP.md) for full setup, launch, and secrets configuration steps.
- `scripts/publish-studio.sh` — WSL bash wrapper that does the same publish via the Windows `dotnet.exe`.

### Code formatting gate

DeckFlow's enforced formatting source of truth is the committed `.editorconfig`. Existing files are not mass-reflowed; the format gate checks changed C# lines only.

Install the versioned pre-commit hook once per clone:

WSL / Linux shell:
```bash
git config core.hooksPath .githooks
```

Windows Git-Bash:
```bash
git config core.hooksPath .githooks
```

After that opt-in, `.githooks/pre-commit` runs `bash scripts/format-check-changed.sh staged` on staged C# changes. A bad added line is blocked with a `file:line` failure; a clean staged change succeeds; a one-line edit in a legacy file passes when the violation is off-hunk.

CI is the authoritative enforcer. The `format-gate` job runs `bash scripts/format-check-changed.sh ci`, selects the PR/push diff base, and fails only when formatter-reported violations intersect added or modified C# lines. That means a PR with a mis-formatted added line fails, while a PR that makes a clean one-line edit in a legacy file with unrelated pre-existing quirks still passes the format gate.

### Local development TypeScript toolchain

Browser-side scripts under `DeckFlow.Web/wwwroot/ts/` compile to
`DeckFlow.Web/wwwroot/js/` via the `CompileTypeScriptAssets` MSBuild target
(BeforeTargets="Build") in `DeckFlow.Web.csproj`. The compiled `.js` files
are NOT tracked in git — `dotnet build` regenerates them every time.

First-time setup on a new dev machine:

```
cd DeckFlow.Web
npm install typescript
```

This populates `DeckFlow.Web/node_modules/typescript/` so the MSBuild target
can invoke `node ./node_modules/typescript/bin/tsc -p tsconfig.json`. The
Render production build does the equivalent in its Docker stage
(`RUN npm install typescript`), so deployments are unaffected.

If `dotnet build DeckFlow.Web` reports a missing `tsc`, run the
`npm install typescript` step above and rebuild.

### UI styling
- `DeckFlow.Web/wwwroot/css/site-common.css` contains shared shell and view-level styles that apply regardless of the selected color theme.
- `DeckFlow.Web/wwwroot/css/site*.css` files remain responsible for theme palettes and component styling.
- `DeckFlow.Web/wwwroot/css/site-mobile.css` loads after the active theme stylesheet to apply mobile-breakpoint overrides for selectors that themes redefine (e.g., `.back-to-top-button`, `.page-shell`, `.sync-column`); cascade-safe mobile rules continue to live in `site-common.css`.
- The theme picker now includes all ten two-color guild themes in addition to the existing wedges, shards, and specialty themes.
- Keep long-lived CSS out of Razor views; prefer shared stylesheets so caching and theme behavior stay predictable.

### Browser/API hardening
- Browser-facing JSON POST APIs now enforce same-origin `Origin`/`Referer` checks before processing deck sync, suggestion, mechanic lookup, and Archidekt cache-harvest requests.
- The old sessionStorage page-snapshot restore path was removed. DeckFlow no longer writes `main.content-shell.innerHTML` into storage or rehydrates raw HTML from storage on load.
- These checks are meant to reduce cross-site request abuse and avoid re-inserting stale or storage-poisoned markup into the DOM.

### Development-only endpoints
- `POST /api/analysis-prompt` builds the deck-analysis prompt headlessly (same `BuildAsync` pipeline as the `/deck-analysis` page) so prompts can be generated for A/B testing and automation without driving the Razor UI. It accepts a JSON body (`deckUrl` or `deckText`, plus optional `format`, `deckName`, `targetCommanderBracket`, `targetAiPlatform`, `selectedAnalysisQuestions`) and returns the generated prompt text and supporting artifacts.
- The endpoint is gated to the Development environment — it returns `404` in Production — and is same-origin guarded like the other JSON APIs.

### IIS publish
- Publish the web app with `dotnet publish DeckFlow.Web/DeckFlow.Web.csproj /p:PublishProfile=IIS-LocalFolder`
- The publish output goes to `DeckFlow.Web/bin/Release/net10.0/publish/iis-local/`
- The .NET SDK generates `web.config` during publish; there is no checked-in `web.config`
- In IIS, create an application such as `/deckflow` that points at that publish folder
- Install the ASP.NET Core Hosting Bundle on the IIS machine
- The checked-in views and scripts are path-base safe, so links and API calls stay under the IIS application path instead of jumping to `/`

### Deploying to cloud hosts (Render, Fly, etc.)
- A `Dockerfile`, `fly.toml`, and `render.yaml` ship at the repo root for one-command builds on Fly.io or Render.
- For durable feedback and category cache storage without a persistent disk, configure Postgres with `DECKFLOW_DATABASE_PROVIDER=Postgres` and `DECKFLOW_DATABASE_CONNECTION_STRING=<Postgres connection string>`.
- If you keep the default SQLite provider in a cloud host, set `MTG_DATA_DIR=/data` and mount a persistent volume there so `feedback.db` and `category-knowledge.db` survive deploys/restarts.
- AI session artifact folders are still filesystem-backed. Set `MTG_DATA_DIR=/data` and mount a persistent volume if saved AI sessions need to survive deploys/restarts.
- The Dockerfile's entrypoint resolves `$PORT` at container start so platforms that inject a dynamic port (Render) work without changes.
- **Moxfield URL caveat.** Moxfield's Cloudflare edge blocks requests from datacenter IP ranges with HTTP 403/5xx. When that happens, DeckFlow automatically falls back to Commander Spellbook's public `card-list-from-url` endpoint (which accepts the same Moxfield URL) and loads the deck from there instead. The UI surfaces a warning banner noting that card printings, set codes, collector numbers, author tags/categories, and sideboard/maybeboard entries are not available through the fallback. For full metadata, users should copy the Moxfield deck export text and paste it into the deck input directly — that path continues to work from anywhere.
- **Optional browser-extension path.** The web UI now detects Moxfield deck URLs before submit. If the optional DeckFlow Bridge extension is installed and the current DeckFlow origin is allowed in extension settings, the browser fetches the Moxfield deck directly and submits it through the existing form flow. If the extension is not installed, DeckFlow can prompt the user with the included install page (`/extension-install.html`), which now serves a downloadable ZIP from `/extensions/deckflow-bridge.zip`. Browsers do not allow the site to silently install the extension. Mobile browsers are left on the normal server/fallback path and are not prompted for the extension.
  The Moxfield URL fields in the web UI also include a collapsible in-app hint that links to the install page and explains the allowed-origin setup.

### Browser extension install
- Extension folder: `browser-extensions/deckflow-bridge`
- Download/install page: `/extension-install.html` serves `/extensions/deckflow-bridge.zip`
- Current install mode: download ZIP, unzip it locally, then load unpacked via `chrome://extensions` or `edge://extensions`
- Security default: the DeckFlow bridge only responds on origins the user explicitly allows in extension options
- The extension contains:
  - `deckflow-bridge.js` for the optional DeckFlow web-app bridge
  - `options.html` / `options.js` for managing the allowed DeckFlow origin list
  - `background.js` for cross-origin Moxfield API requests

---

## Deck Analysis Workflow

The Deck Analysis page (`/deck-analysis`) guides you through a 5-step workflow. Step 2 generates the analysis prompt, Step 3 parses and renders the returned `deck_profile` JSON, Step 4 optionally generates a set-upgrade prompt using that parsed profile, and Step 5 parses and renders the returned `set_upgrade_report` JSON.

### Workflow layout modes
Three layouts are available via the toolbar: **Guided**, **Focused**, and **Expert**. They present the same underlying steps with different amounts of context and guidance text.

### Step 1 — Deck Setup
Choose an **Input method** (paste text or public deck URL) and provide either a **Moxfield**/**Archidekt** deck URL or pasted deck export text. The chosen mode round-trips with the form so it survives refreshes and workflow-step navigation. The service:
- Falls back to treating leading quantity-1 entries as the commander when no Commander section header is present (Moxfield plain-text exports), then validates the inferred commander against Scryfall before continuing.
- Rejects inferred commanders that are not legal by the workflow rules: legendary creature, legendary Vehicle, or a planeswalker whose oracle text says it can be your commander.

### Step 2 — Analysis
Configure the analysis:

| Setting | Purpose |
|---|---|
| **Target Commander Bracket** | Bracket 1–5. Your AI uses this when evaluating card quality, interaction density, and upgrade suggestions. |
| **Analysis questions** | Select one or more questions from the buckets below. |
| **Card name** | Required when card-specific questions are selected. |
| **Budget amount** | Required when the budget-upgrade question is selected. |
| **Decklist export format** | Moxfield or Archidekt — required when category questions are selected; optional for versioning questions. |
| **Include card versions** | When checked, the original deck's set code and collector number are sent so your AI can preserve the exact printing for retained cards. |
| **Preferred category names** | Shown when **Update categories** is selected. One name per line; your AI will prefer these over inventing new ones. |
| **Protected cards** | Cards that must appear in every generated deck version. |

Click **Generate Analysis Packet** to build the reference data and analysis prompt. The service:
- Resolves all deck cards via Scryfall (`POST /cards/collection` in batches of 75) to supply authoritative Oracle text.
- Fetches official mechanic rules text from the WOTC rules page for any keyword mechanics found on resolved cards.
- Fetches the Commander banned list.
- Queries the Commander Spellbook API if combo questions are selected.
- Fires the banned-list fetch, set-packet fetch, and Spellbook combo lookup concurrently to minimize wait time.
- Generates a suggested AI conversation title displayed in the UI with a copy button.

The generated prompt uses `##` section headings (TASK, EVIDENCE RULES, BRACKET GUIDANCE, ANALYSIS QUESTIONS, OUTPUT FORMAT, REFERENCE DATA, DECKLIST) to keep long prompts structured.

**Reference Oracle-text recency gate (optional, off by default).** By default every reference card carries its full Oracle text. Because well-known older cards are already in the target AI's training data, that text is mostly redundant tokens. The `analysis.reference.full-oracle-text` feature flag, when an operator **disables** it, drops Oracle text from cards released more than 12 months ago (keeping it for recent or undatable printings the model may not know yet) — roughly a 30% prompt-token reduction with no measured change to analysis verdicts in cEDH testing. The flag is fail-safe: its enabled state (the default, and the state assumed if the flag store is unreachable) always keeps the legacy full-Oracle output, so the gate only ever engages on an explicit operator opt-in.

### Step 3 — Analysis Results
Paste the fenced `deck_profile` JSON block or raw JSON payload returned from your AI. You can also paste a saved `deck_profile` JSON file here directly without filling out Steps 1 and 2 again. The page validates the payload, parses it into a strongly typed model, and renders a readable summary of:
- Format and commander
- Game plan, speed, primary axes, and synergy tags
- Strengths, weaknesses, deck needs, and weak slots
- Per-question answers with basis notes
- Full deck versions when versioning questions were requested

This step is local to the returned JSON. It does not regenerate the analysis packet or call upstream services again.

### Step 4 — Set Upgrade (optional)
Select one or more recent MTG sets, or paste a condensed set packet override. The page generates a set-upgrade prompt that references the parsed deck profile and asks your AI to evaluate new cards from each set as potential inclusions, with suggested cuts, bracket-fit notes, speculative tests, and traps called out per set. For Commander/precon-style sets (`commander`, `duel_deck`, `starter`), the packet is filtered to first-print cards only so reprints don't crowd out genuinely new candidates; standard expansions are unfiltered. The set dropdown loads asynchronously from `/api/set-options` so the page renders immediately. A deck in Step 1 is required; the parsed Step 3 deck profile is optional but strongly recommended — without it your AI gets an empty schema and produces generic recommendations.

### Step 5 — Set Upgrade Results (optional)
Paste the fenced `set_upgrade_report` JSON block or raw JSON payload returned from your AI. The page validates the payload, parses it into a strongly typed model, and renders a readable summary of:
- Per-set panels: top adds with suggested cuts and reasoning, traps, and speculative tests
- Final shortlist broken into must-test, optional, and skip columns

Each suggested card (top adds and shortlist must-test/optional entries) also shows the card's rules text inline so you can see what it does without a separate lookup. The text is the exact Scryfall oracle text pulled from the generated set packet when that packet is available for the session; otherwise it falls back to the card text echoed by your AI in the `card_text` field.

Like Step 3, this step is local to the returned JSON. You can paste a saved `set_upgrade_report` JSON file here directly without re-running the earlier steps — Step 5 runs standalone when no deck source is present.

### Prompt output-format rules
All AI prompts generated by this app (analysis, set-upgrade, deck comparison, meta-gap) explicitly instruct your AI to return JSON inside a fenced ```` ```json ```` code block. Raw JSON outside a code block is rejected by the wording.

### Artifact saving (local download / upload)
On the **Deck Analysis** page, the Step 3 and Step 5 result panels include a **Download session (.zip)** button. The zip contains every artifact for the current run: the input summary, request context, prompts, schemas, and response JSON blobs. Files are stored only on your machine; no copy is retained server-side.

To resume a saved run later, expand **Resume from a saved session (.zip)** at the top of the form, choose the previously downloaded zip, and the page rehydrates the response JSON into Step 3 or Step 5. The browser's busy indicator runs while the upload is processed.

Zip contents:
- **/deck-analysis**: `00-input-summary.txt`, `01-request-context.txt`, `30-reference.txt`, `31-analysis-prompt.txt`, `41-deck-profile-schema.json`, `50-set-upgrade-prompt.txt`, `40-deck-profile.json`, `51-set-upgrade-response.json`, `all-prompts.txt`, `all-responses.txt`

Re-import only consumes `40-deck-profile.json` and `51-set-upgrade-response.json`; the rest rides along for your records or future AI context.

---

## Analysis Question Buckets

Questions are grouped into collapsible buckets. Buckets with pre-selected questions open automatically on page load.

| Bucket | Notable questions |
|---|---|
| **Core Deck Analysis** | Strengths/weaknesses, win condition, consistency, power level, best meta |
| **Deck Construction & Balance** | Mana curve, lands and ramp, card draw, interaction count, underperformers |
| **Strategy & Synergy** | Key synergies, anti-synergies, commander support, protect-cards, game plan |
| **Optimization & Upgrades** | Cuts for strength, budget upgrades (requires amount), missing staples, faster/competitive, board-wipe resilience |
| **Meta & Matchups** | Performance vs. archetypes, pod weaknesses, tech options, hate pieces |
| **Play Pattern & Decision Making** | Ideal opening hand, tutor priorities, when to cast the commander, common misplays |
| **Specific Card-Level Questions** | Card worth including and better alternatives can each target multiple card names, and every `[card]` question is emitted once per card you add; also includes weakest card and too many high-CMC cards |
| **Advanced / Expert-Level** | Turn clock, disruption vulnerability, keepable hand percentage, redundancy, mana-base optimization |
| **Combo Analysis (Commander Spellbook)** | Combos already in the deck, combos one card away within the color identity — both use live Commander Spellbook API data injected into the prompt |
| **Deck Versioning & Upgrade Paths** | Bracket 2/3/4/5 version, 3 named upgrade paths, assign categories, update categories |

### Deck Versioning output format
When any versioning or category question is selected, the analysis prompt instructs your AI to:
- Output the **full, complete 100-card decklist** for each generated version — no truncation, no "fill with basics" shorthand.
- Count cards before responding to confirm the total reaches 100.
- Use the deck builder's inline format when an export format is chosen:
  - **Moxfield**: `1 CardName (SET) collectorNumber` — or with categories: `1 CardName (SET) collectorNumber #Category1 #Category2`
  - **Archidekt**: `1 CardName (SET) collectorNumber [Category1,Category2]` — commander line uses `[Commander]`
- Output a **Cards Added** and **Cards Cut** diff after each decklist, comparing against the original.
- Output a `deck_profile` JSON block for each generated deck version.
- When **Include card versions** is checked, preserve the original printing (set code + collector number) for every retained card.

### Category / tag questions
- **Assign categories** — Your AI assigns functional role categories to every card in the deck. Plain text export is not supported; Moxfield or Archidekt format is required.
- **Update categories** — Your AI updates or reassigns categories using the preferred category names you provide. Preferred names are injected into the prompt; your AI may add new categories only when none of the preferred names fit.
- Basic card types (Creature, Instant, Sorcery, Enchantment, Artifact, Planeswalker, Battle) are excluded as categories. Your AI is instructed to use functional role labels instead (Ramp, Card Draw, Removal, Wipe, Tutor, Win Condition, Protection, etc.).
- For category questions, the prompt explicitly requires the final decklist to be returned only inside a fenced `text` code block so it can be pasted directly into Moxfield or Archidekt bulk edit.

### Commander Spellbook combo lookup
When either combo question is selected, the service calls the Commander Spellbook `find-my-combos` API before building the prompt:
- Returns up to 20 **included combos** (all pieces are in the deck) and up to 15 **almost-included combos** (exactly one card missing, within the deck's color identity).
- Each combo entry lists the card names, results, and up to 300 characters of instructions.
- Results are injected as a reference block in the prompt. Your AI is told to treat this data as authoritative.
- Results are cached for 30 minutes keyed by the sorted deck card list.
- API failures degrade gracefully — the analysis continues without combo data rather than failing.

---

## Deck Comparison

The Deck Comparison page (`/deck-comparison`) generates structured AI prompts for comparing two Commander decklists side by side. It lives alongside the Deck Analysis page in the Deck Tools tabs.

### Step 1 — Deck Setup
Paste two decklists (Moxfield/Archidekt URL or plain-text export) and select a Commander Bracket for each deck. Optionally name each deck — the service falls back to the commander name if left blank.

### Step 2 — Generate Comparison Packet
The service:
- Parses both decklists, resolving cards via Scryfall `POST /cards/collection` in batches of 75.
- Falls back to per-card Scryfall search when a submitted name is an alternate-art or Universes Beyond printing that does not round-trip through the collection endpoint cleanly, then labels rendered decklists as `resolved name [printed as: submitted name]`.
- Queries Commander Spellbook for combos in each deck.
- Builds a comparison context document with bracket definitions, role counts (ramp, draw, interaction, wipes, recursion, closing power), mana curves, color identity, category overlap, and combo gaps.
- Generates a structured comparison prompt with `## TASK`, `## RULES`, `## COMPARISON AXES`, `## OUTPUT FORMAT`, deck sections, and comparison context. The prompt instructs your AI to produce both a human-readable comparison and a fenced `json` block matching a `deck_comparison` schema.
- Generates a follow-up prompt for iterative refinement of the comparison.

Comparison axes include: commander role and game plan, speed and setup tempo, ramp, draw, spot interaction, sweepers, recursion, closing power (including combos), resilience, consistency, mana stability, commander dependence, table fit, major overlap/differences, and five concrete cards or packages that best explain the gap.

### Step 3 — Review Results
Paste your AI's JSON response back into the form. The page parses the `deck_comparison` JSON and renders a formatted view with:
- Game plans and bracket labels for each deck
- Strengths and weaknesses per deck
- Key combos per deck
- Verdict panel: speed, resilience, interaction, mana consistency, closing power, and combo comparisons
- Shared themes and major differences
- Key gap cards or packages
- Recommended-for notes per deck
- Confidence notes (when your AI flags uncertainty)

If you continue asking follow-up questions in the same AI thread, use `32-comparison-follow-up-prompt.txt` to have your AI revise the readable comparison and regenerate the full `deck_comparison` JSON block.

### Artifact saving (local download / upload)
On the **Deck Comparison** page, the Step 3 result panel includes a **Download comparison session (.zip)** button. The zip contains every artifact for the current run: the input summary, both normalized decklists, combo summaries, context, prompts, schema, and response JSON. Files are stored only on your machine; no copy is retained server-side.

To resume a saved run later, expand **Resume from a saved session (.zip)** at the top of the form, choose the previously downloaded zip, and the page rehydrates the response JSON into Step 3. The browser's busy indicator runs while the upload is processed.

Zip contents:
- **/deck-comparison**: `00-comparison-input-summary.txt`, `10-deck-a-list.txt`, `11-deck-b-list.txt`, `12-deck-a-combos.txt`, `13-deck-b-combos.txt`, `20-comparison-context.txt`, `30-comparison-prompt.txt`, `31-comparison-schema.json`, `32-comparison-follow-up-prompt.txt`, `40-deck-comparison-response.json`

Re-import only consumes `40-deck-comparison-response.json`; the rest rides along for your records or future AI context.

### Prompt templates
The `prompt-templates/deck-comparison/` directory contains reference templates for compact and JSON-structured comparison prompts: all-in-one, competitive meta, matchup, quick verdict, JSON matchup, JSON strict return, and JSON tuning variants. See `docs/deck-comparison-prompt-cheat-sheet.md` for usage guidance.

---

## cEDH Meta Gap

The cEDH Meta Gap page (`/cedh-meta-gap`) generates a structured AI workflow for comparing your deck against recent EDH Top 16 lists for the same commander.

### Step 1 — Load Deck And Fetch References
Paste a public Moxfield or Archidekt URL, or paste deck export text directly. You can optionally override the commander name. The page then queries EDH Top 16 using:

- Time period
- Sort by (`TOP` or `NEW`)
- Minimum event size
- Maximum standing

The service parses the submitted deck, removes sideboard and maybeboard cards, resolves the commander, fetches matching EDH Top 16 entries, and sorts them newest-first before display.

### Step 2 — Generate Meta-Gap Prompt
Select 1 to 3 EDH Top 16 reference decks and generate the prompt. The service builds:

- `30-meta-gap-prompt.txt`
- `31-meta-gap-schema.json`

While building the prompt, the service also:

- Resolves submitted-deck and reference-deck card names through Scryfall so alternate print names and reskins are converted to canonical Oracle names where possible.
- Normalizes split and multi-face names to the base/front name for prompt display.
- Queries Commander Spellbook for your deck and for each selected reference deck, then injects combo summaries into the prompt.
- Ranks the injected combo reference by popularity (most-played first), breaking ties by lowest mana value needed to assemble, so the highest-impact combos lead the list; combos lacking ranking data keep their original API order.
- Caps the reference-deck count at 3 to keep the prompt size reasonable once decklists and combo references are included.

The prompt is structured with clear sections:

- `ROLE`
- `EVIDENCE PRIORITY`
- `RULES`
- `INPUT DATA`
- `ANALYSIS TASK`
- `OUTPUT CONTRACT`
- `JSON SHAPE`

Your AI is instructed to:

- Write a concise human-readable meta-gap summary first.
- Then return a fenced `json` block whose top-level object is `meta_gap`.
- Prefer the supplied Commander Spellbook combo evidence over weaker inferred combo reads when they conflict.
- Fill every field, using empty strings, zero values, `false`, or empty arrays when evidence is missing.

### Step 3 — Paste Returned JSON
Paste the raw JSON or fenced `json` block back into the page. The shared JSON extractor accepts fenced responses and ignores surrounding prose or extra trailing fence noise before parsing the payload. The page renders:

- Overview and readiness score
- Win lines
- Interaction
- Speed
- Mana efficiency
- Core convergence
- Missing staples
- Potential cuts
- Top 10 adds and cuts

### Artifact saving (local download / upload)
On the **cEDH Meta Gap** page, the Step 3 result panel includes a **Download meta-gap session (.zip)** button. The zip contains every artifact for the current run: the input summary, prompt, schema, and response JSON. Files are stored only on your machine; no copy is retained server-side.

To resume a saved run later, expand **Resume from a saved session (.zip)** at the top of the form, choose the previously downloaded zip, and the page rehydrates the response JSON into Step 3. The browser's busy indicator runs while the upload is processed.

Zip contents:
- **/cedh-meta-gap**: `00-input-summary.txt`, `30-meta-gap-prompt.txt`, `31-meta-gap-schema.json`, `40-meta-gap-response.json`

Re-import only consumes `40-meta-gap-response.json`; the rest rides along for your records or future AI context.

---

## Deck Sync

The Deck Sync page (`/sync`) compares two decks and generates the delta import needed to bring the target deck in line with the source.

Supported sync directions:

| Direction | Description |
|---|---|
| MoxfieldToArchidekt | Moxfield as source, Archidekt as target |
| ArchidektToMoxfield | Archidekt as source, Moxfield as target |
| MoxfieldToMoxfield | Compare two Moxfield decks |
| ArchidektToArchidekt | Compare two Archidekt decks |

For same-system comparisons, column labels update dynamically to reflect the source and target platform.

---

## Card Lookup

The Card Lookup page (`/card-lookup`) has two modes:

- **Single Card** (default; the only mode visible on mobile) — type a card name, get live Scryfall suggestions once you've entered 4+ characters, and picking a suggestion (or pressing Look Up) renders that card's Oracle text plus WOTC rulings inline via `GET /card-lookup/single`.
- **Card List** (desktop-only) — paste up to 100 card names and download the full Scryfall output as `.txt` (`POST /card-lookup/download`) or structured `.json` (`POST /card-lookup/download-json`). The inline line editor with per-row autocomplete is still available for editing before downloading.

Under the hood all modes use the same `ICardLookupService`: the card collection is fetched via `POST /cards/collection` in batches of 75, and rulings are fetched per-card via `GET /cards/{id}/rulings`.

The Single Card result panel also detects keyword mechanics and ability words on the resolved card, looks up the current official WOTC rules text for each detected term, and renders those entries in a separate **Keyword Rules** panel below the card text. This is intentionally limited to Single Card mode so large list downloads do not fan out into extra mechanic-rule lookups.

The Single Card result panel includes an "Ask a rules question about this card →" link that deep-links into `/judge-questions?card=<name>`.

---

## Mechanic Rules

The Mechanic Rules page (`/mechanic-lookup`) looks up the current official Wizards Comprehensive Rules text for a keyword mechanic or rules term.

Behavior:

- Exact rules sections such as `Prowess` return the matching numbered section and summary.
- Glossary terms such as `Battle` resolve through the glossary and, when the glossary points to a major rules section like `310`, the page now returns the full referenced section body rather than only the glossary sentence or section header.
- The Clear button clears the saved input, summary block, and rendered rules text together.

The service caches the parsed Wizards rules document in memory for 6 hours so repeated lookups do not keep re-downloading the full rules text file.

---

## Ask a Judge

The Ask a Judge page (`/judge-questions`) leads with a prominent link to the live community judge chat at [`chat.magicjudges.org/mtgrules`](https://chat.magicjudges.org/mtgrules/) — a 24/7 IRC channel (`#magicjudges-rules` on Libera.Chat) staffed by certified judges and rules experts. This is the authoritative path. When the page is opened with a `?card=<name>` query parameter (e.g. from Card Lookup), it pre-formats a `!CardName — ` opening message ready to copy into the chat.

A clearly labeled **secondary** ChatGPT prompt generator is provided below for casual play and quick second opinions. It carries a prominent disclaimer ("ChatGPT can be confidently wrong about MTG rules") and, if a reference card is supplied, fetches that card's Oracle text and rulings via `GET /card-lookup/single` and embeds them in the generated prompt. The prompt itself starts with the same warning so ChatGPT cannot bury it.

---

## Commander Categories

The Commander Categories page shows the Archidekt tags that appear most often on decks where a given card is listed as the commander. It reports what observers assigned, not what the app infers.

---

## Category Suggestions

The Category Suggestions page supports multiple lookup modes:

- `CachedData`
- `ReferenceDeck`
- `ScryfallTagger`
- `All`

Current behavior:

- `ReferenceDeck` reads exact categories from a supplied Archidekt deck URL or pasted Archidekt text.
- `CachedData` reads category hits from the existing local Archidekt-backed store.
- `ScryfallTagger` returns oracle-tag style suggestions from Scryfall Tagger.
- `All` combines the cached-store path and tagger path, with EDHREC as a fallback when no other source returns anything.

---

## Archidekt category cache
- Run `dotnet run --project DeckFlow.CLI -- archidekt-cache --minutes 5` to keep the local cache fed with the latest public decks.
- The CLI runs a dedicated cache session that respects rate limits via Polly, records skips for noisy decks, and persists card/category observations to `artifacts/category-knowledge.db`.
- The background hosted service reuses the same session logic to keep the cache fresh (the user-triggered harvest button was removed in v1.4).
- The cache session now stays alive for the requested harvest window even when the queue runs dry, and it retries transient recent-page fetch failures instead of ending the whole job early.
- Basic card type categories (Creature, Instant, Sorcery, Enchantment, Artifact, Planeswalker, Battle) are filtered out of cache suggestions.

---

## Content Knowledge Base

DeckFlow distills MTG content-creator videos into paste-ready prompt artifacts and a browsable site index. Heavy work (transcripts, audio, LLM calls, spend ledgers) runs **locally** via the CLI against `artifacts/content-kb.db`; only a slim index and the markdown artifacts ship to the site.

Local pipeline (run from the repo root):

```bash
# 1. Register a source (YouTube channel or podcast RSS)
dotnet run --project DeckFlow.CLI -- content-source-add \
  --url https://www.youtube.com/@salubrioussnail --name "Salubrious Snail"

# 2. Harvest transcripts (captions first; --enable-whisper opts into the Whisper audio fallback)
dotnet run --project DeckFlow.CLI -- harvest --limit 5
#    ...or pick exact videos instead of the most-recent walk (v1.5):
dotnet run --project DeckFlow.CLI -- harvest --video-ids "VLdny8IVXYE,IJYU_rzCcP8"

# 3. Distill into artifacts + index rows (--dry-run estimates spend first)
dotnet run --project DeckFlow.CLI -- distill --limit 5
dotnet run --project DeckFlow.CLI -- distill --video-ids "VLdny8IVXYE"

# 4. Export the index seed for commit-then-deploy
dotnet run --project DeckFlow.CLI -- content-index-export
```

- Each artifact is a markdown file under `content-kb/{source-slug}/{video-id}.md` with a ≤200-word summary, 3-8 timestamped clips, and tags from a controlled vocabulary (archetype/strategy, format/bracket, card category).
- The distill LLM backend is selected by `DECKFLOW_LLM_PROVIDER` (`openai` default with Structured Outputs, or `claude` to shell the Claude Code CLI at $0 subscription cost). Monthly spend caps: `DECKFLOW_LLM_MONTHLY_CAP_USD` and `DECKFLOW_WHISPER_MONTHLY_CAP_USD` (default $15; cap-gating applies to the OpenAI/Whisper paid paths).
- **`claude` provider on Windows — set `DECKFLOW_LLM_CLI_COMMAND`.** With `DECKFLOW_LLM_PROVIDER=claude`, the distiller shells the `claude` CLI. On Linux/macOS it runs bare `claude` (must be on `PATH`). On **Windows** the bare default is not used — set `DECKFLOW_LLM_CLI_COMMAND` to a JSON array invoking the CLI, with exactly one `{instruction}` placeholder. If your `claude` lives in WSL, call it via `wsl.exe` using the **full path** (wsl.exe uses a non-login shell, so `~/.local/bin` is not on `PATH` — bare `wsl.exe claude` fails):

  ```jsonc
  // PowerShell user env var, or _run-claude.bat `set` line, or dotnet user-secrets:
  DECKFLOW_LLM_CLI_COMMAND = ["wsl.exe","/home/<you>/.local/bin/claude","-p","{instruction}","--output-format","json","--allowedTools",""]
  ```

  A native Windows `claude` install instead uses `["cmd.exe","/c","claude.cmd","-p","{instruction}","--output-format","json","--allowedTools",""]`. Optional `DECKFLOW_LLM_CLI_TIMEOUT_SECONDS` bounds each call. If it is unset/invalid on Windows, distill aborts with a clear "Distiller CLI not configured" message (not silent per-video failures).
- The public browse/detail pages at `/content-kb` are gated behind the `content.kb.enabled` feature flag (default OFF) and only show entries an admin published via `/Admin/ContentKb` (per-entry or per-source bulk curation; visibility survives seed reloads).
- `/Admin/YoutubeExport` downloads a channel's upload list (title, views, upload date, URL) as text or CSV — useful for picking `--video-ids` targets.

---

## Web API
Swagger UI is available at `/swagger` when running in Development mode.

### Category suggestion
```
POST /api/suggestions/card
Content-Type: application/json

{
  "mode": "CachedData",
  "archidektInputSource": "PublicUrl",
  "archidektUrl": "",
  "archidektText": "",
  "cardName": "Guardian Project"
}
```

### Commander category lookup
```
POST /api/suggestions/commander
Content-Type: application/json

{
  "commanderName": "Bello, Bard of the Brambles"
}
```

### Archidekt cache background jobs
Start a background harvest:
```
POST /api/archidekt-cache-jobs
Content-Type: application/json

{
  "durationSeconds": 300
}
```

Poll a specific job:
```
GET /api/archidekt-cache-jobs/{jobId}
```

Get the currently active job, if any:
```
GET /api/archidekt-cache-jobs/active
```

### cURL examples
```bash
curl -X POST http://localhost:5000/api/suggestions/card \
  -H "Content-Type: application/json" \
  -d '{"mode":"CachedData","archidektInputSource":"PublicUrl","cardName":"Guardian Project"}'

curl -X POST http://localhost:5000/api/suggestions/commander \
  -H "Content-Type: application/json" \
  -d '{"commanderName":"Bello, Bard of the Brambles"}'
```

---

## Scryfall usage
- Scryfall is used for card-name autocomplete, commander autocomplete, the Card Lookup page, card reference resolution in the Deck Analysis workflow, and async set catalog loading.
- All Scryfall clients send a real `User-Agent`, an explicit `Accept` header, and use `https`.
- Card lookup uses `POST /cards/collection` in batches of 75 identifiers.
- The Card Lookup page is capped at 100 non-empty input lines per submission (at most two `cards/collection` requests plus one `cards/{id}/rulings` request per unique resolved card, all throttled).
- The AI workflow uses the same batch endpoint to resolve authoritative Oracle text for all deck cards.
- The set catalog is fetched via `GET /sets` and cached in memory for 6 hours; the web UI loads it asynchronously via `/api/set-options`.

### Rate limiting
- Scryfall enforces a soft cap of 10 requests per second at the Cloudflare edge (no proactive `X-RateLimit-*` headers on 200 responses; only `Retry-After` on 429).
- `DeckAnalysisPacketService` throttles all Scryfall calls to ~110ms apart (≈9 req/s) via a process-wide semaphore so batched collection lookups plus per-card fallback searches stay under the cap.
- On a 429 the wrapper reads `Retry-After` and retries once if the cooldown is ≤5 seconds; longer cooldowns surface as a friendly "Scryfall returned HTTP 429. Try again shortly." error instead of being misattributed to card/commander validation.
- The CLI ships a diagnostic `scryfall-probe` command that calls Scryfall and dumps status, headers, and body — useful for reproducing rate-limit responses. Example: `dotnet run --project DeckFlow.CLI -- scryfall-probe --endpoint random --repeat 25` (intentionally triggers 429).

---

## CLI usage examples
```bash
dotnet run --project DeckFlow.CLI -- compare \
  --moxfield my.deck --archidekt other.deck --out diff.txt

dotnet run --project DeckFlow.CLI -- archidekt-cache --minutes 10

dotnet run --project DeckFlow.CLI -- category-find \
  --card "Guardian Project" --cache-seconds 20
```

Content KB distill selects its LLM backend with `DECKFLOW_LLM_PROVIDER` (`openai` default, `claude` for the local CLI subscription backend). See [`docs/ops/content-kb-llm-cli-backends.md`](docs/ops/content-kb-llm-cli-backends.md) for exact WSL, Windows, and Windows `dotnet.exe` from WSL commands.

---

## Browser Extension

The **DeckFlow Bridge** Chrome/Edge extension lets DeckFlow fetch Moxfield decks through your logged-in browser session when direct server-side requests fail.

See [`browser-extensions/deckflow-bridge/README.md`](browser-extensions/deckflow-bridge/README.md) for load-unpacked installation instructions, or open `/extension-install.html` in the running app to download the current ZIP package.

---

## Architecture
- Core logic is isolated in `DeckFlow.Core` (diff engine, export helpers, parsers, integration clients, knowledge store).
- Web and CLI layers orchestrate requests and rely on DI to resolve shared services.
- Importers for Archidekt and Moxfield implement typed interfaces (`IMoxfieldDeckImporter`, `IArchidektDeckImporter`) for easy test substitution.
- `DeckAnalysisPacketService` parallelizes independent fetches (banned-list, set-packet, Commander Spellbook) using `Task.WhenAll` to reduce total build time.
- `DeckComparisonService` parses two decklists, resolves cards via Scryfall, queries Commander Spellbook for both decks, derives comparison context (role counts, mana curves, combo gaps), and generates structured AI prompts with a JSON output schema.
- `CommanderSpellbookService` caches results for 30 minutes and degrades gracefully on API failure.
- `CategoryKnowledgeStore` persists observations through the configured relational provider. SQLite stores `artifacts/category-knowledge.db` by default; Postgres can be selected with `DECKFLOW_DATABASE_PROVIDER=Postgres`.

---

## UI Notes
- The floating back-to-top control uses inline SVG in the shared layout, not the old `chevron-up.png` bitmap.
- The back-to-top button stays hidden while the page is already near the top and appears only after the user scrolls down.

### Visual themes
A persistent theme picker in the shared layout lets users switch between visual themes. The selection is stored in `localStorage` and applied on page load. The shared layout now enhances that native select with an ARIA combobox button/listbox while preserving the original form control for form posts and keyboard fallback. Available themes:
- **Default** — the base site stylesheet
- **Abzan (WBG)**, **Bant (GWU)**, **Esper (WUB)**, **Grixis (UBR)**, **Jeskai (URW)**, **Jund (BRG)**, **Mardu (RWB)**, **Naya (RGW)**, **Sultai (BGU)**, **Temur (GUR)** — color-shard/wedge-inspired palettes
- **Nyx** — enchantment-themed dark palette
- **Planeswalker Dark** — dark-mode palette
- **Commander Table** — warm tabletop-inspired palette

---

## License

DeckFlow is licensed under the [Apache License 2.0](LICENSE). Copyright 2026 Chris Lunt.
