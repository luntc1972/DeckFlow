# DeckFlow

DeckFlow helps deck builders translate decks between Moxfield and Archidekt without manual editing. It also provides AI prompt-building workflows for single-deck analysis, cEDH meta-gap analysis, head-to-head deck comparison, and deck-primer generation; a deterministic mana-base analyzer and a local bracket classifier; Commander Spellbook combo lookup, Scryfall card and mechanic references, an Ask-a-Judge handoff flow, public feedback capture, and a cache-backed category suggestion engine.

## User help
End-user documentation is served by the running web app at `/help` (feature guides) and `/about` (version, source, credits). This README keeps the developer-facing material (build, publish, API, CLI, deployment).

**Repository description (≤350 characters):** DeckFlow unifies Moxfield/Archidekt decks and generates paste-ready AI prompts (analysis, deck primer, comparison, cEDH meta-gap), plus a mana-base analyzer, bracket check, deck diffs, card/mechanic lookup, Ask-a-Judge handoff, and a browsable MTG creator knowledge base. Live at deckflow.gg.

## User Feedback

A public **Feedback** form is linked in the site footer (`/feedback`). Submissions are stored through DeckFlow's relational storage provider. SQLite is the default and stores `feedback.db` at `$MTG_DATA_DIR/feedback.db` (falling back to `./artifacts/feedback.db` in development). Postgres can be enabled with the database environment variables below.

An admin page at `/Admin/Feedback` displays submissions with filters for status and type, and lets you mark items Read, Archive, or Delete them.

### Admin configuration

Set these environment variables (via the Render env var UI):

- `FEEDBACK_ADMIN_USER` — basic auth username for all `/Admin/*` pages.
- `FEEDBACK_ADMIN_PASSWORD` — basic auth password.
- `FEEDBACK_IP_SALT` (optional) — salt for hashing submitter IPs. If unset, a random 32-byte salt is generated on first run and persisted in the feedback metadata table.

Basic auth covers the whole admin shell: Dashboard (`/Admin`), Feedback, Flags, Harvest, Analytics, Content KB curation, and YouTube Export. If `FEEDBACK_ADMIN_USER` or `FEEDBACK_ADMIN_PASSWORD` are not set, `/Admin/*` returns **503 Service Unavailable**. The public `/feedback` form continues to accept submissions.

On `/Admin/Flags`, operators can narrow the table instantly in-browser with a starts-with key filter, namespace chips for `service.` and `analysis.`, and status chips (**All statuses / Enabled / Disabled**) that filter rows by their current on/off state; all three compose, and the current filter is kept in `sessionStorage` across admin page reloads within the session. Public-tool visibility flags (`tool.*`) are **not** listed here — they are administered on `/Admin/Tools` (which cascades to the home tile, nav, help, and route), so a tool flag is toggled in exactly one place.

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
- `Deck History` lets you version a deck into a JSON file you own, append labeled snapshots with notes, diff any two saved versions, and generate an AI prompt about how the list evolved; its evolution prompt now embeds Scryfall oracle text for every card seen in that history so AI models recognize newer cards.
- The category suggestion UI now shows one merged, ranked, paste-ready card category list across sources, while commander category results show `% of decks` and cap the first 25 visible rows with an expander for the remainder.
- `DeckFlow.CLI` exposes deck comparison, category harvesting, cache querying, and the local Content KB pipeline (source management, transcript harvest, LLM distillation, site-index export) in a console tool.

### Bracket classifier and balancer
- **Bracket Check (`/bracket`, flag `tool.bracket.enabled`, default OFF):** auto-classify a Commander deck into its official 1-5 bracket from Game Changers, two-card combos, and mass land denial — then optionally generate a balancer prompt to hit a target bracket. The classification is computed directly in DeckFlow (no AI needed); the AI prompt is only for the optional fair-swap balancer step. Paste or URL-import a deck, pick a target bracket (or leave it unset for classify-only), and DeckFlow shows the tier badge, why-this-bracket reasons (GC count, detected combos, MLD), floor violations (cards exceeding the target), and starter cuts. A copy-ready paste artifact builds the balancer prompt for ChatGPT or Claude. Tutors are not counted per the October 2025 rubric update. Extra-card-draw is informational only (covered by Game Changers). The tool is flag-gated (`tool.bracket.enabled`) — when off, the page is unreachable and all other pages stay byte-identical.

### Mana base analyzer
- **Deterministic mana-base check (`/manabase`, CLI `manabase`):** load an Archidekt/Moxfield deck (URL or pasted list) and DeckFlow scores it with Frank Karsten's source-count method — recommended land count vs. actual, per-color source supply vs. the toughest spell's requirement, and the weakest color — entirely in-app, **no AI round-trip needed for the verdict**. Cards resolve through Scryfall by exact printing (set + collector number) first, so alternate/flavor names still match; a small optional "copy for ChatGPT" block frames the deficits as a prompt only for the one thing the math can't do — naming specific land swaps. The scoring engine lives in `DeckFlow.Core/Manabase/` (pure, unit-tested); the web page and CLI command are thin surfaces over it. The CLI command takes `--mode casual|focused|cedh` (Focused keeps Casual land targeting/surfaces with an 85% color bar; cEDH lowers the land target), prints the **Reading your deck** plain-language verdict plus the non-cEDH ramp/draw slot-budget advisory by default, and appends the paste-ready ChatGPT swap prompt when given `--swap-prompt`. The verdict now rounds advisory shortfalls instead of always rounding them up, appends `…plus N more` when more than three issues were found, and labels the per-color add counts as heuristic guidance across the page, `.txt`, and swap prompt. The `/manabase` web page is gated behind the `tool.manabase.enabled` feature flag (default **ON**); an admin can hide the page and its nav link from `/Admin/Tools` without a redeploy (off → 503 maintenance page).
- **Conditional Mox accuracy:** the manabase classifier now models the five commander-legal conditional Moxen with a density-graded heuristic in the manabase path itself: Amber and Chrome are capped to commander colors, Amber/Opal keep full untapped fast-mana credit only in legend-/artifact-dense decks, Tantalite is delayed/tapped, and Diamond stays full-credit.
- **Commander detection + picker fallback:** pasted Archidekt/Moxfield exports now preserve the real command zone more reliably, including Moxfield **Copy for MTGO** / **Copy Plain Text** lists that end with a lone commander line after `SIDEBOARD`. After Scryfall resolution, each inferred commander is validated for Commander eligibility (Legendary Creature, Legendary Vehicle, qualifying "can be your commander" planeswalker, or a Legendary Enchantment Background), and partner/background pairs are kept together. If no valid commander can be identified, `/manabase` stops guessing and shows a commander picker instead: a dropdown of the deck's own commander-eligible cards plus a name-search backstop, then re-runs the same analysis with the deck text and options preserved.
- **Plain-language readout (flagged):** when the admin also enables `analysis.manabase.plain-language-verdict`, the `/manabase` result adds short metric glosses, a **Reading your deck** verdict block, and a Casual-only ramp/draw slot-budget advisory. This layer is advisory only: it never changes the land count, color counts, castability, or health band. The ramp/draw split is a community heuristic, not Karsten math, and its threshold is a single-point proxy from the commander mana value or the deck's 75th-percentile curve point. The per-color "add N more sources" language is also explicitly heuristic guidance rather than a new deterministic rule. In cEDH, DeckFlow can still show the glosses, but it suppresses the ramp/draw advisory and verdict block.
- **Casual / Focused / cEDH modes + commander importance:** the page has a **Deck type** selector — *Casual* (default; Karsten's full land target), *Focused* (same land target and surfaces as Casual, but an 85% color-support threshold for mid-power decks), or *cEDH* (lower land count, fast-mana heavy) — plus a **commander-importance** selector (*Central* / *Standard* / *Low*) that decides how hard the analyzer holds the commander's colors to their threshold (set *Central* for a must-cast-every-game commander like Brago). Both persist across the postback. The Focused tier is behind `analysis.manabase.focused-tier` (**default OFF**); when that flag is off, the page stays byte-identical to the historic two-mode UI and a hand-crafted Focused post falls back to Casual. A second dark-launch flag, `analysis.manabase.cedh-land-target` (**default OFF**), is cEDH-only: when enabled it replaces the flat 28-floor target with a curve-anchored target that can blend toward the commander's committed cEDH land baseline, surfaced on the page as a land range with its sample provenance, and names the commander in a persistent header; off stays byte-identical.
- **Per-card castability readout (Casual / Focused):** in Casual and Focused modes the report adds a **Castability** table — each real spell's estimated chance to be cast on its on-curve turn (drawing every turn, including Commander's first-turn draw), worst-first, with a semantic chip (low / ok / good), an **average delay** column (mean turns late it first becomes castable — *on curve*, else *+N.N turns*), and which factor is limiting it (*mana*, *color: X*, or *mana + color*). The commander is pinned and flagged; mana rocks/dorks and lands are counted in the math but never listed as rows. cEDH mode hides the table and shows a note instead.
- **Results page UX polish:** the results panel now keeps the verdict narrative together near the top, shows a persistent **Casual analysis / cEDH analysis** chip in the Result header, adds an **On this page** anchor list for the major sections, and caps the Casual castability table to the hardest rows by default with a no-JS expander for the rest. On mobile, long castability card names wrap instead of hard-clipping mid-word.
- **Mulligan-aware, four-tier verdict:** per-color source requirements are derived from the simulation (modeling Commander's free first mulligan) and clamped to Karsten's published table as a ceiling, so tight double-pips are neither flagged against an inflated count nor pushed past the math. The overall health reads on a graded **Excellent / Solid / Workable / Needs work** scale that combines source counts with the simulated castability headline: late expensive cards are treated as curve pressure, while *Needs work* is reserved for severe color shortages, two-plus color issues, broad color-access failures, or land shortages the simulation also corroborates. If the sim says the deck functions and only one soft color issue stacks with a paper land shortfall, the band stays *Workable* instead of red. The result panel leads with a **verdict summary** — health band, land count, and the single biggest fix — so the bottom line reads before the supporting lenses and tables.
- **Ramp surfaced (expandable):** the result shows how much acceleration the deck runs — the count of mana rocks / dorks (non-land mana sources) plus the ramp/draw pieces at ≤2 mana value — so it's clear what is lowering the recommended land count rather than that math being buried in the formula breakdown. The **Ramp** line is expandable: open it to see the actual mana rock/dork card names and the ≤2 MV ramp/draw card names the analyzer credited (a card that is both appears under both groups).
- **Reduced / alternative cost overrides:** some cards cost far less than their printed mana value — pitch/free spells (Force of Will), board-scaling self-reducers (Blasphemous Act `{8}{R}` usually cast for `{R}`), and evoke/suspend. DeckFlow **auto-detects** these and pre-fills a **"Reduced / alternative costs"** box (`Card Name: cost`, e.g. `Force of Will: 0`, `Blasphemous Act: {R}`); you can edit or clear any line. An applied override replaces the card's effective cost everywhere the math looks — the castability simulation, the on-curve turn, and the per-color source findings — so a free spell stops demanding its colors, and an overridden row is flagged with a `*`. The override is an effective mana *cost* (it can change colors, not just lower the number); `0` makes a card behave like a true 0-cost card. Once you edit the box your text is honored verbatim — including a box you deliberately empty to reject the suggestions (it only pre-fills until first touched, so a clear sticks instead of silently refilling). Lines the analyzer can't use — an unreadable cost, or a card name that matches nothing in the deck (a typo) — are surfaced under the box as **"N override line(s) not applied"** instead of being dropped silently.
- **"Show the work" formula panels:** two collapsible panels explain the verdict — *How the analysis works* (the Karsten regression + the Monte-Carlo castability model with London-mulligan, joint mana+color, ramp and fetch crediting, and commander weighting; always shown, credits Frank Karsten, flagged as an estimate cross-checked against community calculators including [Salubrious Snail](https://www.salubrioussnail.com/manabase-tool)) and *This deck's numbers* (the land target and per-color source tally for the entered deck, plus the simulation parameters).
- **MDFC land-backs are real lands (unconditional):** modal double-faced cards with a land back (Agadeem's Awakening, Shatterskull Smashing, and the rest of the Zendikar spell-side MDFC lands) count as **real lands** — toward the land total and the castability simulation — with their **full colored-source weight** and their tapped/pay-life state read from the land face (a pay-life back like Agadeem, the Undercrypt enters untapped; a plain tapped back enters tapped). The tapped timing is the only penalty; there is no separate color discount. This is now **always on**, not flag-gated: the earlier "partial non-land source + fractional Karsten land-target credit" legacy path (and its `analysis.manabase.accuracy` gating of MDFC behavior) has been removed, so an MDFC-heavy deck reads consistently whether or not the accuracy bundle is enabled. The `analysis.manabase.accuracy` flag still governs the other sim-accuracy knobs (mana quantity, ramp-credit, color-aware mulligan, land-ramp sim, health-band floor, pay-life shocklands, conditional-untapped lands).
- **Command zone castability callout (Phase 72, flagged):** behind `analysis.manabase.commander-castability`, the report can show a command-zone callout above the per-card Castability table with each commander, partner, and Background's estimated cast-on-curve chance. Those commander rows move out of the per-card table for display only; the health band and verdict logic are unchanged. When the flag is on, the downloaded `.txt` report now carries the same **Command-zone castability** block (commanders + companion +3-tax note), so the paste artifact matches the on-page callout and the swap prompt instead of dropping the command-zone story. The flag now defaults **ON**, so a fresh result shows the callout out of the box; an admin can still hide it from `/Admin/Flags`. The on-page "avg on-curve" figure, the verdict, and the health band all read one shared non-commander average, so they can never quote different numbers. For single-commander decks the callout also shows a small **Earlier turns** line — the cumulative chance the simulator's board could already pay for the commander on each turn before its on-curve turn (ramp/fast-mana powered early casts, e.g. `T2 12% · T3 48%`); zero-chance turns are omitted and the line disappears entirely when no early cast is possible. It is a pure observation on the existing 20,000-trial sim (commander rows only, no extra RNG), rendered ungated inside the callout.
- **Commander-aware community baseline (Increment 2):** when the community baseline flag is enabled, bracket 2-3 now blend in per-commander EDHREC land-count averages from the community `averages.csv` dump, using 3,179 commanders with at least 100 decks each. Refresh the bundled commander snapshot with `dotnet run --project DeckFlow.CLI -- edhrec-averages --csv <path>`. The commander-side data is from EDHREC under its non-commercial community license, and the `/manabase` UI shows the required attribution.
- **Companion detection and heuristic (Phase 72):** companion auto-detection comes from the Moxfield direct API only. Archidekt has no reliable Companion category, so Archidekt decks, pasted lists, and the Moxfield Commander Spellbook fallback path rely on the manual companion designator instead. When a companion is named, the analyzer models its "move to hand first" tax as a stated heuristic of +3 generic mana before casting; this is an approximation, not a rules-exact sequence.
- **"Analysis in beta" disclaimer (Phase 72):** the manabase results always show a short "manabase analysis is in beta - results may be inaccurate" banner at the top of the report. It is flag-independent (shown whether or not `analysis.manabase.commander-castability` is enabled) and is a standing reminder that the numbers are a heuristic guide, not a guarantee.
- **Tap analyzer (Phase 75, flagged):** behind `analysis.manabase.tap-analyzer` (default **ON**), the report and its paste artifact surface untapped-source quality — overall untapped-source frequency, a deck-level turn-1 untapped availability percentage, and (multi-color decks only) a per-color untapped breakdown. It is informational only and never changes the land count, color counts, castability, or health verdict. The flag now defaults **ON**, so a fresh result surfaces the untapped-source block; an admin can still hide it from `/Admin/Flags`.
- **Ritual land-target credit is a separate dark flag:** behind `analysis.manabase.ritual-land-credit` (default **OFF**), cEDH analyses can reduce the recommended land count for ritual-heavy lists by subtracting a capped `0.5` land per already-classified net-positive ritual from the enabled hybrid cEDH target, before the final safety clamp. This is intentionally separate from `analysis.manabase.ritual-burst-mana`: the land-target credit is a strategic deck-building heuristic, while the burst flag is a tactical castability sim credit, so a ritual can count in both places. On the current 3281-deck calibration harness, the extra credit moved the cEDH under-target rate from `21.8%` to `11.1%` and lowered the mean target from `25.4` to `24.7` lands (`-0.7` on average); flag-OFF stays byte-identical. The *This deck's numbers* breakdown now names the ritual land credit as its own line.
- **Cheap scry spells can add a small any-color source credit (new flag, seed ON):** behind `analysis.manabase.scry-credit`, each qualifying non-land spell with mana value ≤2 and a real `scry N` effect (reminder-text-only matches excluded; lands excluded) adds **+0.2 any-color effective sources per copy** to the Karsten per-color requirement lane only. It is analyzer-only: no `ManaSource` rows, no castability-sim change, and no land-target change. This is intentionally separate from the existing `≤2 MV` ramp/draw land-target credit, so a cheap draw+scry card can count in both places. Flag-OFF stays byte-identical, and the page plus `.txt` artifact now name the exact `count × 0.2` credit when present.
- **True colorless `{C}` and snow `{S}` now have their own source categories (new flag, seed ON):** behind `analysis.manabase.colorless-snow`, the parser keeps the legacy `ManaColor.Colorless` fold for raw pip maps but ALSO tracks dedicated true-`{C}` and `{S}` pip counts. The simulator then requires real colorless producers for `{C}` and snow permanents for `{S}` (bit `5` = produces colorless, bit `6` = snow), while the analyzer adds dedicated requirement rows and source counts for those categories on the page and in the `.txt` artifact. Flag-OFF keeps the old "drop colorless-folded pips" behavior byte-identical; the seed is ON, and flipping production remains an operator action.
- **Restricted lands are behind a dark flag with explicit disclosure:** behind `analysis.manabase.restricted-lands` (default **OFF**), `/manabase` applies the composition-gated approximation for **Cavern of Souls**, **Unclaimed Territory**, **Ancient Ziggurat**, and **Nykthos, Shrine to Nyx**. Flag-OFF stays byte-identical. When enabled, the report marks the affected land/source rows with a `†`, shows a matching footnote, and adds one unsupported-interactions panel entry naming the approximated lands so the discount is disclosed instead of hidden.
- **cEDH opening-hand keep shapes + casual/focused curve coverage (new dark flag, seed OFF):** behind `analysis.manabase.keep-shapes` (default **OFF**, flip after UAT), cEDH mode layers a plan-quality gate on top of the Karsten mana floor. The opening-hand block gains a **second headline** — *plan-keepable %* beside *mana-keepable %* — counting only mana-keepable openers that also pass one of three cEDH keep shapes: **explosive** (a payoff/tutor deployable by turn 3, crediting in-hand fast mana so a rock-fueled turn-3 payoff counts even when its printed cost would land later on lands alone), **early engine** (an engine online by turn 2), or **interaction bridge** (≥2 cheap interaction pieces — counterspells count despite being non-permanents — plus ≥2 development pieces). Plan-keepable is a subset of keepable, so it is always ≤ mana-keepable. Representative openers are **shape-labeled** (*explosive / engine / bridge keep*, or *no plan by turn 4 — mulligan*) and **turn-capped** so a turn-≥5 payoff is never shown as a workable line; a commander-central deck can surface its commander as the representative opener. In **Casual** and **Focused** mode the same flag instead adds a **curve-coverage** line — *plays a spell on ~N of first 5 turns*. The flag also gates plan-role classification, so flag-OFF (and non-cEDH with the flag off) stays byte-identical across the page, `.txt` artifact, and swap prompt.
- **Mana-source disclosure lists are behind a dark flag (new, seed OFF):** behind `analysis.manabase.source-list`, the **Untapped sources** lens can expand to show two nested disclosures: **Mana sources**, listing every physical land, rock, and dork in the deck with compact pip letters (`W U B R G C`), and **Tapped sources**, listing only the entries that cannot make mana the turn they are played. The Core report now always carries the slim display projection so analysis stays deterministic; the flag gates **page HTML only**. Flag-OFF is byte-identical.
- **Command-zone awareness in deck analysis (Phase 73, flagged):** behind `analysis.command-zone-awareness` (default **OFF**), the `/deck-analysis` prompts name the full command zone (commander, partner pair joined with `&`, or Background) plus the deck's companion as side metadata in the DECK CONTEXT / `<companion>` block — awareness only, the decklist text is never mutated, so output stays byte-identical until an admin enables it. When the flag is ON, Step 1 also shows an optional **companion designator** input: Moxfield decks auto-detect the companion, but Archidekt decks, pasted lists, and the Moxfield Commander Spellbook fallback have no reliable companion signal, so you name it here. The typed designator wins over auto-detection and is single-line-bounded and length-capped before it reaches any prompt.
- **Multi-axis deck score (Phase 77, flagged):** behind `analysis.multi-axis-score` (default **OFF**), `/deck-analysis` adds a four-axis **Power / Speed / Control / Consistency** score to the Step-3 results panel (above the Overview) and to all paste-artifact variants (ChatGPT and Claude in the UI; the Gemini variant exists in code behind `DECKFLOW_GEMINI_ENABLED`). Each axis shows a coarse **0-5** band (None .. Extreme) with a numeral, a 5-pip meter, a band pill, and a one-line rationale; a bracket cross-check note flags whether the power band agrees or diverges from the deck's computed bracket. The bands are deterministic heuristic estimates derived in `DeckFlow.Core` (tutors, fast mana, ramp/draw, counters, bracket, combo availability) — no AI round-trip for the numbers; they are framed in the prompt as a starting read the AI re-checks. Score-block band colors carry baked ink so they stay legible across every guild theme. The flag defaults OFF, so the page and all artifacts stay byte-identical until an admin enables it.
- **Deck Primer staleness banner (Phase 78, flagged):** behind `tool.primer.stale-flag` (default **OFF**), the Deck Primer surfaces a caution banner on Step 3 when the deck currently in Step 1 differs from the deck a saved primer was generated against — "Deck changed since this primer was generated — N cards differ. Regenerate to refresh the primer." with a **Regenerate primer** button. It activates on the resume-without-rebuild path: upload a previously downloaded primer `.zip` while Step 1 holds a different deck, and the old primer renders verbatim with the banner — **no auto-rebuild and no upstream re-fetch** (regeneration stays the explicit button press). Staleness is a card-name + quantity multiset hash (reordering cards or swapping printings does not flag stale; adding/removing a card or changing a quantity does). The flag defaults OFF, so Step 3 and the downloaded zips stay byte-identical until an admin enables it.

## Getting Started
1. Restore/build: `dotnet build DeckFlow.sln`
2. Run the web app: `dotnet run --project DeckFlow.Web`
3. Use the CLI to compare or harvest decks: `dotnet run --project DeckFlow.CLI -- --help`

### Helper scripts
- `scripts/run-web.sh` — bash wrapper that rebuilds `DeckFlow.Web` and launches it on `http://localhost:5173` with no browser auto-launch.
- `scripts/run-web.ps1` — PowerShell equivalent for Windows terminals.
- `scripts/publish-studio.ps1` — publishes `DeckFlow.Studio` as a self-contained win-x64 single-file executable (no .NET install required on the target machine). Run from Windows PowerShell; produces `artifacts/studio-release/` and `artifacts/DeckFlowStudio-<date>.zip`. See [DeckFlow.Studio/STUDIO-SETUP.md](DeckFlow.Studio/STUDIO-SETUP.md) for full setup, launch, and secrets configuration steps. The git-backed flows (Publish, Direct Push, Pull from Prod) run git from the process working directory; to publish from a distributed exe that lives outside the repo, set `DECKFLOW_REPO_ROOT` to the repo working tree (otherwise launch Studio from inside the repo).
- `scripts/publish-studio.sh` — WSL bash wrapper that does the same publish via the Windows `dotnet.exe`.

### Releasing (version + tag)
- `scripts/release.sh 2026.07.6` or `.\scripts\release.ps1 2026.07.6` updates `DeckFlow.Web/DeckFlow.Web.csproj`, commits `chore(release): 2026.07.6`, and creates the matching lightweight git tag.
- The scripts require a clean tracked working tree, reject invalid CalVer values (`YYYY.MM` or `YYYY.MM.N`), and refuse to reuse an existing tag.
- They do not push. After the script succeeds, run `git push --follow-tags`. The About page will show the new version after the next deploy because production reads the tracked csproj version, not git metadata.

### Monthly cEDH land baseline

DeckFlow commits a monthly cEDH land-baseline snapshot under `DeckFlow.Web/Data/cedh-land-baseline/` — the per-commander land-count sample the `analysis.manabase.cedh-land-target` hybrid target reads (see the mana-base analyzer notes). The pipeline is:

1. Refresh `_calib` from EDHTop16 + Scryfall with `python3 scripts/cedh-baseline/fetch.py` (defaults to a **6-month** window; `--since` / `--supplement-since` override). The fetch pulls the size-tiered top-cut results, then runs a **commander-specific search** for each name in `SUPPLEMENT_COMMANDERS` (12-month lookback) so low-play commanders that never reach a usable sample through the tiered pull are still covered — deduped and appended automatically.
2. Generate the monthly report + JSON snapshot with `dotnet run --project DeckFlow.CLI -- cedh-land-baseline --data _calib --month YYYY-MM`
3. Commit the new dated `YYYY-MM.md` / `YYYY-MM.json` files plus the refreshed `latest.json`

The CLI reclassifies each cached deck through `DeckFlow.Core/Manabase/` (the app's own classifier), applies the cEDH gate (avgMV ≤ 2.7, 95–101 cards), and emits the web-app JSON contract (`generated`, `sampleSize`, `overallMeanLands`, per-commander `n`/`landsMean`/`landsSd` for `n ≥ 3`) plus a human-readable monthly markdown report. The current 6-month snapshot is ~3,300 gated decks. Re-run the calibration harness after each refresh before flipping the flag on. See `scripts/cedh-baseline/README.md` for the operator runbook and how to add a commander to the supplement list.

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
- Tool pages plus the home page render a **Share DeckFlow** bar above the footer with Copy link, native device share (when supported), and Reddit / X / Bluesky actions.
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

### Deploying to cloud hosts (Render)
- A `Dockerfile` and `render.yaml` ship at the repo root for one-command builds on Render (the live production host).
- For durable feedback and category cache storage without a persistent disk, configure Postgres with `DECKFLOW_DATABASE_PROVIDER=Postgres` and `DECKFLOW_DATABASE_CONNECTION_STRING=<Postgres connection string>`.
- If you keep the default SQLite provider in a cloud host, set `MTG_DATA_DIR=/data` and mount a persistent volume there so `feedback.db` and `category-knowledge.db` survive deploys/restarts.
- AI session artifact folders are still filesystem-backed. Set `MTG_DATA_DIR=/data` and mount a persistent volume if saved AI sessions need to survive deploys/restarts.
- The Dockerfile's entrypoint resolves `$PORT` at container start so platforms that inject a dynamic port (Render) work without changes.
- **Moxfield URL caveat.** Moxfield's Cloudflare edge blocks requests from datacenter IP ranges with HTTP 403/5xx. When that happens, DeckFlow automatically falls back to Commander Spellbook's public `card-list-from-url` endpoint (which accepts the same Moxfield URL) and loads the deck from there instead. The UI surfaces a warning banner noting that card printings, set codes, collector numbers, author tags/categories, and sideboard/maybeboard entries are not available through the fallback. For full metadata, users should copy the Moxfield deck export text and paste it into the deck input directly — that path continues to work from anywhere.
- **Optional browser-extension path.** The web UI detects Moxfield deck URLs before submit on every deck tool that takes a public-URL import — Deck Sync, Deck Analysis, Mana Base, and Deck Primer. If the optional DeckFlow Bridge extension is installed and the current DeckFlow origin is allowed, the browser fetches the Moxfield deck directly and submits it through the existing form flow. As of extension v0.1.1 the default allow-list and host scope already include `deckflow.gg` (apex + `www`) and localhost, so the public site works with no manual setup; the extension also no longer injects on unrelated sites. If the extension is not installed, DeckFlow prompts the user with the included install page (`/extension-install.html`), which serves a downloadable ZIP from `/extensions/deckflow-bridge.zip`. Browsers do not allow the site to silently install the extension. Mobile browsers are left on the normal server/fallback path and are not prompted for the extension.
  The Moxfield URL fields in the web UI also include a collapsible in-app hint that links to the install page and explains the (now mostly automatic) allowed-origin setup.

### Browser extension install
- Extension folder: `browser-extensions/deckflow-bridge`
- Download/install page: `/extension-install.html` serves `/extensions/deckflow-bridge.zip`
- Current install mode: download ZIP, unzip it locally, then load unpacked via `chrome://extensions` or `edge://extensions`
- Security default: the DeckFlow bridge only responds on origins on its allow list — `deckflow.gg` and localhost are pre-allowed (v0.1.1+); any other origin must be added in extension options. The content script is scoped to those hosts, so it no longer injects on every site.
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
Choose an **Input method** — the page opens on **public deck URL** — and provide either a **Moxfield**/**Archidekt** deck URL or pasted deck export text. The chosen mode round-trips with the form so it survives refreshes and workflow-step navigation. The service:
- Falls back to treating leading quantity-1 entries as the commander when no Commander section header is present (Moxfield plain-text exports), then validates the inferred commander against Scryfall before continuing.
- Rejects inferred commanders that are not legal by the workflow rules: legendary creature, legendary Vehicle, or a planeswalker whose oracle text says it can be your commander.

#### Cross-tool single-deck carry-over
Within the same browser tab, DeckFlow now carries deck input between `/deck-analysis`, `/manabase`, `/cedh-meta-gap`, `/convert`, and `/deck-primer` via client-side `sessionStorage`. If you navigate from one of those single-deck tools to another, the target page prefills the saved deck URL or pasted list only when its deck field is still empty, so a value already rendered or typed there is never overwritten. When a prefill happens, the tool now shows a small inline "Restored your last deck." notice with a `Clear` action that empties the current deck field and removes the carried deck from storage. The existing `Start Over` / `Clear` controls on those tools also clear the carried deck now, so the deck does not reappear on the next navigation. The data is per-tab, clears when the tab closes, and is not stored on the server. Two-deck tools such as `/deck-comparison` and `/sync` are not covered yet.

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

The analysis, deck-comparison, follow-up, and set-upgrade prompts also ground the AI against fabrication: if it encounters a card name it does not recognize, it is told to treat the card as unknown and flag it rather than guess or invent its rules text. (Earlier wording asked the AI to look the card up on scryfall.com, which a pasted-in chat model cannot do and which invited made-up card text.)

### Print results to paper
The rendered results panels on **Deck Analysis** (Step 3 / Step 5), **Deck Comparison** (Step 3), **cEDH Meta Gap** (Step 3), and **Mana Base** each include a **Print results** button beside their Download button. It opens the browser print dialog (print or save-as-PDF) against a print stylesheet that strips all site chrome — header, nav, tool tabs, timing, layout picker, the input form, and the toolbar buttons themselves — leaving only the rendered analysis on the page. Page breaks are constrained so a content section, list item, score card, or combo block is not split across pages and a heading is not stranded at a page foot; color meters and status pills keep their ink. The print layout lives in `site-common.css` (`@media print`), so it applies across every guild theme.

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
For each deck, choose an **Input method** (public deck URL or paste text — each deck toggles independently) and provide a **Moxfield**/**Archidekt** deck URL or plain-text export, then select a Commander Bracket. Optionally name each deck — the service falls back to the commander name if left blank.

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
Choose an **Input method** (public deck URL or paste text) and provide a public **Moxfield**/**Archidekt** URL or deck export text. You can optionally override the commander name. The page then queries EDH Top 16 using:

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

The `% of decks` column is the share of that commander's harvested decks that run at least one card in the category — each deck is counted once, no matter how many of its cards carry the tag.

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

On a KB entry's detail page the browser shows the distilled notes as-authored (Summary, Key Clips, Tags), but the **Copy** button serves a standalone, paste-ready prompt — persona, task, and evidence rules around the notes — so pasting into ChatGPT/Claude/Gemini returns grounded, actionable advice with no extra typing. This is why the copied text is longer than what is rendered on the page. The framing (`ContentKbPromptWrapper`, in `DeckFlow.Core`) is now **baked at distill time** into a sibling `.prompt.md` next to the notes, so the exact shipped prompt is visible in the Studio review queue while approving and is served verbatim by the copy button. Older artifacts with no baked sibling fall back to reconstructing the identical prompt from the notes on the fly (`ContentKbPromptResolver`), so every existing and future entry copies a framed prompt without re-distilling.

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

- Each artifact is a markdown file under `content-kb/{source-slug}/{video-id}.md` with a ≤200-word summary, 3-8 key clips (each carrying an `[mm:ss]` timestamp when the transcript has a marker to support it, otherwise left untimed rather than guessed), and tags from a controlled vocabulary (archetype/strategy, format/bracket, card category). Distillation also writes a sibling `content-kb/{source-slug}/{video-id}.prompt.md` — the baked paste-ready prompt (persona + task + evidence rules wrapped around the notes) that the site copy button and the Studio review queue serve.
- The distill LLM backend is selected by `DECKFLOW_LLM_PROVIDER` (`openai` default with Structured Outputs, or `claude` to shell the Claude Code CLI at $0 subscription cost). Monthly spend caps: `DECKFLOW_LLM_MONTHLY_CAP_USD` and `DECKFLOW_WHISPER_MONTHLY_CAP_USD` (default $15; cap-gating applies to the OpenAI/Whisper paid paths).
- **`claude` provider on Windows — set `DECKFLOW_LLM_CLI_COMMAND`.** With `DECKFLOW_LLM_PROVIDER=claude`, the distiller shells the `claude` CLI. On Linux/macOS it runs bare `claude` (must be on `PATH`). On **Windows** the bare default is not used — set `DECKFLOW_LLM_CLI_COMMAND` to a JSON array invoking the CLI, with exactly one `{instruction}` placeholder. If your `claude` lives in WSL, call it via `wsl.exe` using the **full path** (wsl.exe uses a non-login shell, so `~/.local/bin` is not on `PATH` — bare `wsl.exe claude` fails):

  ```jsonc
  // PowerShell user env var, or _run-claude.bat `set` line, or dotnet user-secrets:
  DECKFLOW_LLM_CLI_COMMAND = ["wsl.exe","/home/<you>/.local/bin/claude","-p","{instruction}","--output-format","json","--allowedTools",""]
  ```

  A native Windows `claude` install instead uses `["cmd.exe","/c","claude.cmd","-p","{instruction}","--output-format","json","--allowedTools",""]`. Optional `DECKFLOW_LLM_CLI_TIMEOUT_SECONDS` bounds each call. If it is unset/invalid on Windows, distill aborts with a clear "Distiller CLI not configured" message (not silent per-video failures).
- The public browse/detail pages at `/content-kb` are gated behind the `tool.knowledge-base.enabled` feature flag (default OFF) and only show entries an admin published via `/Admin/ContentKb` (per-entry or per-source bulk curation; visibility survives seed reloads).
- `/Admin/YoutubeExport` downloads a channel's upload list (title, views, upload date, URL) as text or CSV — useful for picking `--video-ids` targets.
- **Content KB body hash mismatch (operator signal):** each entry's `content_site_index` row carries a `body_sha256` computed from the artifact body at distill time. On the detail page, the server recomputes that hash from the on-disk `.md` and logs a `Content KB body hash mismatch` warning if it differs from the stored value (or the stored value is null on a legacy pre-hash row) — this flags mojibake/stale-body drift for the operator. The entry still serves normally (fail-open this phase); a future phase may tighten this to refuse rendering once every row is backfilled.
- **Content KB body-hash backfill (startup, one-time):** every existing row whose `body_sha256` is still null gets hashed once at startup, on **both** hosts — the web app (after schema-ensure + seed load, covering prod and local-web) and Studio (after its local `content-kb.db` store is registered). Each host resolves its own `ArtifactPath` (git root / `/data` overlay for web; the local artifact root for Studio), reads the `.md`, and hashes via the same shared helper the render guard uses. A row whose `.md` can't be resolved is skipped with a warning, not thrown; the pass is idempotent (a row that already has a hash is never re-read or rewritten), so it quiets the legacy-row warning above as prior content gets backfilled.
- **Direct Push now re-exports the seed and can trigger a real redeploy (`sync.directpush-gitbody`):** Direct Push's git-durability stage re-exports `content-kb/seed/index-seed.json` through the same shared export factory Publish uses and stages it alongside the pushed bodies, so a fresh prod reseed can fully reconstruct a Direct-Push'd row instead of reverting it on the next deploy. When the web-DB feature flag `sync.directpush-gitbody` is OFF (its shipped default), the commit still carries `[skip render]` — behavior is byte-identical to before. When an operator turns the flag ON, the phrase is dropped so the push triggers a real Render redeploy (bodies are then served from git `/app` only, with the `/data` overlay fallback dropped from serving resolution). Studio reads the flag from the same production `feature_flags` table the web app uses, through a read-only accessor that fails closed (treats a missing row or a connection failure as OFF) — Studio never assumes a flag it cannot confirm is on.
- **Direct Push enforces expand→verify→contract; a Stage 5 confirms the deploy before anything goes visible (SYNC-09/D-06):** Direct Push is now a 5-stage flow — Stage 1 (diff), Stage 2 (SCP upload), Stage 3 (content-only DB write, sets a durable "awaiting confirm" marker — never stamps or makes anything visible), Stage 4 (git commit + push, unchanged), and a new **Stage 5 — Verify Deploy & Publish**: press it once the Render deploy triggered by Stage 4 shows healthy, and it polls the authenticated `deployed-body-hash` endpoint per pushed row; only rows whose deployed `/app` body hash matches get stamped `pushed_to_prod_utc` and flipped `is_visible=true`. A git failure or a not-yet-confirmed row simply stays hidden and awaiting-confirm — never a false-positive publish, never a silent deadlock. If a push is left mid-flight (e.g. the Studio session is reloaded before Stage 5 runs), the durable local marker survives the reload: an **Awaiting Confirm — Resume Interrupted Push** card appears automatically (on page load and after every diff) listing any marker-set rows and offering a **Resume** button that re-runs the same verify/publish step for exactly those rows — a content-upserted-but-unconfirmed row is never silently stranded as "Unchanged" on a later diff.
- **Reconcile — prod↔git↔seed drift detection plus a gated removal Apply in Studio (Phase 91, SYNC-11/SYNC-12/SYNC-17):** the **Reconcile** page in DeckFlow.Studio runs an operator-triggered dry-run that is **always available and strictly read-only**, independent of any feature flag. It reads the live prod `content_site_index` once, walks the operator's git `content-kb/**/*.md` tree, and reads `index-seed.json`, then classifies every row/file into one of four discrepancy classes: **published orphans** (visible/approved row, no git body), **file orphans** (`.md` file, no matching prod row), **seed drift** (a seed-managed row absent from the seed), and **body hash mismatches** (prod's stored hash differs from the computed git body hash). Results are persisted to a local, scope-tagged discrepancy store (idempotent upsert; a discrepancy no longer seen on a later run is marked resolved, never deleted) and written to a git-tracked `content-kb/reconcile-report.md`. If `index-seed.json` cannot be read or parsed, the page shows a **"seed unavailable — seed-drift/removal skipped"** notice in place of the seed-drift group instead of presenting an empty group as "no drift" — an unreadable seed always reads as a warning, never phantom drift or a false all-clear. Below the dry-run results, an **Apply removals** action lets an operator soft-hide (`is_visible = false`, never deleted) the **seed-drift rows only** — Published Orphans, File Orphans, and Body Hash Mismatches stay detection-only and are never applied. Apply is gated behind the web-DB feature flag `sync.reconcile` (seeded OFF; both a definitive off AND an indeterminate/unreachable read refuse — fail-safe, never fail-open), then **re-runs the diff fresh** and independently refuses (zero rows hidden) if the freshly-read seed is unavailable, *before* any other check — this refuse is on the raw seed-availability signal, not the discrepancy list, so a future classifier change can never reopen a mass-hide path through an unreadable seed. The fresh seed-drift set is then compared to what the operator reviewed; any drift since the dry-run (prod or the seed moved) is a stale-reject with zero writes, never a blind apply. Only rows the fresh prod read confirms `seed_managed = true` are ever hidden — a prod-owned row is structurally impossible to hide through this action.
- **Git Body Coverage — the `sync.directpush-gitbody` flip precondition (Studio):** a new **Git Body Coverage** page in DeckFlow.Studio runs an operator-triggered, **strictly read-only** audit: for every approved+visible production `content_site_index` row it checks that the row's body `.md` exists in the local git `content-kb` tree (what becomes `/app` after deploy) and lists any that are **missing**. It reports either "precondition satisfied" (0 missing) or a table of the missing rows (title, natural key, expected path); it never writes to prod or the local store, and failure paths stay sanitized (generic message, no connection-string/exception leak). This is the gate to run — and confirm **0 missing** — before flipping `sync.directpush-gitbody` ON (see `93-PREFLIP-CHECKLIST.md`), which switches body serving to git `/app`-only.
- **Pull from Prod — field-authoritative prod→local reconcile (Phase 92, SYNC-13/14/15):** Pull-from-Prod is strictly read-only toward production and now resolves each row's **body from the local git `content-kb` tree** — it does **not** SFTP-download prod bodies (prod `/data` is empty by design). Per-field authority governs an adopt: body and content follow the git tree, while the operator-owned DB fields (`is_visible`, `is_hidden`, `approval_status`) are read from prod and **preserved, never clobbered**. A **git-staleness guard** runs a bounded fetch and warns (or lets you proceed) when the local checkout is behind the remote — a stale tree would otherwise mis-report bodies as missing. Any **body-vs-index divergence** (the git body's hash disagrees with prod's `body_sha256`, or the body is absent/unreadable) is **surfaced per entry and excluded from the default adopt**, requiring an explicit per-entry opt-in rather than being silently adopted.

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

## Release Notes

Releases are tagged with CalVer (`YYYY.MM.PATCH`); the pre-CalVer `v1.x` tags are kept for history. Newest first.

### Unreleased
- **Set-upgrade prompt drops the duplicate notes file:** the Step 4 set-upgrade prompt used to ask the AI for a second fenced ```` ```text ```` block named `discussion_summary.txt` alongside the `set_upgrade_report` JSON. That standalone notes document duplicated the readable per-set analysis and was never parsed by the app, so it is gone from all three platform variants (ChatGPT, Claude, Gemini). The AI now returns the readable per-set analysis followed by the JSON report only; step 4/5 help copy updated to match.
- **Arena-format paste hardening:** pasted MTG Arena exports and Arena-shaped variants from MTGGoldfish / ManaBox / legacy `.dec` sideboard prefixes now route through the shared paste parser instead of failing or mis-parsing, and deck-tool help text now lists MTG Arena among the accepted export formats.
- **ChatGPT prompts now execute in one paste, even when ChatGPT files them:** every ChatGPT prompt artifact (Deck Analysis, Comparison, Primer, Meta Gap, Bracket, Follow-Up, Set Upgrade) now opens with an `EXECUTE NOW` directive. ChatGPT's web UI silently converts large pastes into an attached `.txt` and then asks "which task do you want me to run?" instead of running it; the leading directive makes the packet execute immediately either way. A new invariant test suite guarantees every current and future ChatGPT variant carries the header. Claude/Gemini prompts are unchanged.
- **ChatGPT analysis and meta-gap prompts validate DeckFlow heuristics before analyzing:** a new `HEURISTIC VALIDATION` section instructs the AI to verify every proposed combo, interaction/tutor/fast-mana count, and power/speed score against the actual card data — and to use the validated results downstream — instead of anchoring on a wrong heuristic first pass. In Deck Analysis the section appears only when the packet actually carries heuristic content (score block, interaction audit, win-con map, or combo references).
- **ChatGPT analysis prompt no longer accepts "output too long" refusals:** the OUTPUT FORMAT contract now mirrors the meta-gap anti-refusal clause — ChatGPT must deliver the full analysis (answers, Top Adds/Cuts, complete `deck_profile` JSON) in one reply, and if output genuinely nears its limit it must shorten answers symmetrically in prose and JSON (and cap adds/cuts at 5) rather than refuse, split, or restructure.

### 2026.07.4 — Manabase Gap-Closure & Result-Page Polish (2026-07-13)
A research-driven gap-closure pass on the `/manabase` analyzer (diffed against the full manabase research corpus) plus a round of Studio publish/pipeline hardening. Two new dark flags ship seeded **OFF**; everything else is on by default. Flag-off manabase output stays byte-identical.
- **Six untapped-land cycles are now resolved per trial in the simulation:** the analyzer detects all six conditional untapped-land cycles (fast lands, slow lands, check lands, Snarls, bond lands, and Castle/ELD-style lands) and scores each **tapped or untapped at the moment it is played**, based on the turn and board state, rather than the earlier "fast/slow are always tapped" approximation the 2026.07.3 notes described. This per-trial resolution lives under the `analysis.manabase.accuracy` bundle (default **ON**); detection is by oracle template so future printings are covered automatically.
- **Composition-gated restricted lands with explicit disclosure (new flag, seed OFF):** behind `analysis.manabase.restricted-lands`, `/manabase` applies a composition-gated approximation for **Cavern of Souls**, **Unclaimed Territory**, **Ancient Ziggurat**, and **Nykthos, Shrine to Nyx** (including the MSH true-Basic-census cycle). When enabled, affected land/source rows are marked with a `†`, a matching footnote is shown, and an unsupported-interactions panel entry names the approximated lands so the discount is disclosed, never hidden. Flag-off stays byte-identical.
- **cEDH ritual land-target credit (new flag, seed OFF):** behind `analysis.manabase.ritual-land-credit`, cEDH analyses can subtract a capped `0.5` land per net-positive ritual from the hybrid cEDH land target before the safety clamp — a strategic deck-building heuristic, kept separate from the tactical `analysis.manabase.ritual-burst-mana` sim credit (a ritual can count in both). On the 3281-deck calibration harness this moved the cEDH under-target rate `21.8% → 11.1%`. Flag-off stays byte-identical.
- **Cheap scry any-color source credit (new flag, seed ON):** behind `analysis.manabase.scry-credit`, each qualifying cheap non-land `scry N` spell adds `0.2` any-color effective sources per copy to the Karsten per-color requirement lane only — never as a `ManaSource`, never in castability, never in the land target. Reminder-text-only matches are excluded, lands are excluded, and draw+scry stacking with the existing `≤2 MV` ramp/draw land-target credit is intentional. Flag-off stays byte-identical.
- **True colorless/snow requirement categories (new flag, seed ON):** behind `analysis.manabase.colorless-snow`, `/manabase` treats true `{C}` and snow `{S}` as first-class source categories without changing the historic `ManaColor` fold. When enabled, the sim requires real colorless producers for `{C}` and snow permanents for `{S}`, and the analyzer surfaces dedicated Colorless / Snow requirement rows. Flag-off stays byte-identical, and the production row still flips only by operator action.
- **Result-page UX polish:** the results panel keeps the verdict narrative together near the top, shows a persistent **Casual analysis / cEDH analysis** mode chip, adds an **On this page** anchor list, and caps the Casual castability table to the hardest rows with a no-JS *Show all N* expander; long castability card names wrap on mobile instead of hard-clipping. Casual-mobile result height dropped ~53%. Verdict wording is drawn from one shared helper and is identical across the page, the `.txt` download, and the swap prompt.
- **Studio Publish is honest about what it does:** Publish now commits a **git seed only** — it no longer stamps a false "pushed to prod" timestamp — with copy that reflects the git-seed model, and the Publish commit message is editable. Content-KB seed-loaded rows now land **approved** so the git serve path actually surfaces them.
- **Studio harvest/distill hardening:** the Harvest job keeps running when you switch pages; **filtered** videos can be re-distilled from the pending list; the distill classifier was broadened to keep gameplay/strategy/philosophy content (not just card-name-bearing videos), badging dropped videos **Filtered**; and the summary/clips/tags distill was merged into a **single LLM call**. New per-page **help panels** and a **Workflow Guide** were added to Studio.
- **Deck-tool mobile fixes:** Deck Analysis, Deck Primer, and Deck Comparison no longer overflow horizontally on mobile; each generated-artifact box gained an **Expand** toggle. Deck Analysis's set picker is now a flat date-descending list instead of grouped.

### 2026.07.3 — Content-KB Sync Hardening + Manabase Deep-Eval (2026-07-06)
- **Content-KB Prod ↔ Git ↔ Studio sync hardening (Cycle 16):** the three content stores are reconciled so the git-committed `.md` bodies, the production `content_site_index`, and the Studio publish flow agree; a dry-run reconcile report surfaces drift, and the autodeploy seed path serves committed bodies without manual prod writes.
- **cEDH mana base now names your commander and shows a real-deck land range:** `/manabase` results display the commander by name in a persistent header in **both Casual and cEDH** modes (previously cEDH never named it — the command-zone castability line that carried the name is Casual-only). When a commander has enough committed cEDH tournament data (behind `analysis.manabase.cedh-land-target`, sample **N ≥ 10**), the land recommendation now also shows the **baseline range** the target was blended from — e.g. `cEDH meta range ~26–29 lands (33 cEDH tournament decks, 2026-07 sample; mean 27.5 ±1.6)` — so the number's provenance is visible instead of a bare point. The stale "competitive ~28–32 band, floored at 28" wording in the cEDH help paragraph and the *How the analysis works* explainer was corrected to reflect the flag-on behavior: curve target − 3.5, a **22-land safety floor**, then a **50% nudge toward the commander's cEDH tournament land mean**. Display-only — the land-target math and flag-off output are unchanged.
- **Meta-Gap no longer recommends cutting your own combo pieces:** the cEDH Meta Gap prompt keyed cut suggestions purely on reference-deck overlap, so a card unique to your list was flagged as a potential cut even when it is a combo piece or a deterministic win outlet (e.g. Sunscorched Desert paying off a Preston/Felidar infinite-blink loop, or Ghostly Flicker's Naru Meha combo being "replaced" by a non-combo blink). The prompt now instructs the AI to treat every infinite/near-infinite combo result as an active win engine, protect any card that appears in a combo line or pays one off, and never place such a card in `potential_cuts`, `top_10_cuts`, or the `replaces` field of an add. The same protection extends to **combo tutors** — including tutor abilities the decklist can't show, like cycling/typecycling (e.g. Step Through's wizardcycling fetching Naru Meha) — so the analysis no longer suggests cutting or replacing a card that fetches one of your combo pieces. Applied byte-identically across the ChatGPT, Claude, and Gemini prompt variants; the byte-identity goldens were rebased.
- **Back-to-top arrow visible in the Azorius theme:** the floating "back to top" button rendered as a blank white circle in Azorius — the theme flipped the button to a near-white background but left the chevron stroke white (white-on-white). The chevron now paints in the theme accent. A per-theme computed-style regression test asserts the chevron stroke clears a ≥3:1 contrast against its button background so this class of regression is caught automatically.
- **Response-split recovery tip on every prompt tool:** large packets can push an AI past its single-response output limit, so it splits the answer into parts. Deck Analysis, Deck Comparison, and cEDH Meta Gap now show an on-page note beside each generated prompt telling the user they can reply "Output only the `<object>` JSON in a single response" (`deck_profile`, `set_upgrade_report`, `deck_comparison`, or `meta_gap` as appropriate) — the JSON is the only part you paste back and it fits in one response on its own. This is an on-page note only; it does not change the generated prompt text (the separate Meta-Gap prompt hardening below does).
- **Meta-Gap prompt now tells the AI not to refuse or split:** on a full four-deck comparison ChatGPT would sometimes pre-emptively refuse — claiming the `meta_gap` JSON was "too long for one response" and offering to split it — even though a fully-populated object is only a few kilobytes. The cEDH Meta Gap OUTPUT CONTRACT now instructs the AI to return the complete JSON in a single response and not to refuse, split, ask to continue, or shrink the schema, with an explicit fallback (shorten justifications / cap each list to its top entries) instead of refusing. Added byte-identically across the ChatGPT, Claude, and Gemini variants; goldens rebased. The Help pages for all three prompt tools gained an "If your AI splits the answer or says it's too long" recovery section.
- **Mana base now models shocklands and MDFC land-backs correctly:** two accuracy fixes to the `/manabase` analyzer. **Shocklands and other "you may pay N life" lands** (Steam Vents, Godless Shrine, and pay-life MDFC backs) are now counted as entering **untapped** — the way they actually play — so they help turn-one casts instead of being scored like plain taplands. The detection is anchored to the "you may pay" wording so always-tapped lands with a life-payment *activated* ability (Boseiju Who Shelters All, Hall of the Bandit Lord, Untaidake) correctly stay tapped. **Modal double-faced cards with a land back** (Agadeem's Awakening, Shatterskull Smashing, and the rest of the Zendikar spell-side MDFC lands) now count as **real lands** toward the land total and the castability simulation, with their tapped/untapped state read from the land face; the previous partial-source land-target credit is dropped when they count as real lands, so there is no double-benefit. Both live under the existing `analysis.manabase.accuracy` bundle (default **ON**). As part of this, the six always-on manabase sim-accuracy flags (`source-mana-quantity`, `ramp-credit-v2`, `color-aware-mulligan`, `land-ramp-sim`, `health-band-headline-floor`, plus the new pay-life/MDFC knobs) were **consolidated into the single `analysis.manabase.accuracy` flag** — one toggle instead of six for the settled accuracy behavior. The UI/verdict flags (`commander-castability`, `tap-analyzer`, `mulligan-eval`, `plan-presence`, `plain-language-verdict`, `health-band-castability`) remain separate toggles.
- **Ritual burst mana is wired behind a dark flag:** `/manabase` can now thread the existing Core ritual-burst sim credit through the Web layer behind `analysis.manabase.ritual-burst-mana` (**default OFF**). When an admin enables it, **cEDH only** analyses may credit one-shot rituals such as Dark Ritual as temporary burst mana in early-turn castability; land count and color-source counts stay unchanged, and flag-OFF output remains byte-identical.
- **cEDH early interaction ships ON by default:** behind `analysis.manabase.cedh-interaction-lens` (**default ON**), **cEDH only** `/manabase` analyses add an **Early interaction** lens header, expose the full cEDH Castability table, and add the interaction blocks to both prompt artifacts. This is informational v1 only: it does **not** change land count, color counts, or the health verdict. Flag-OFF output is byte-identical (kill switch).
- **All manabase display/verdict reads now default ON:** the six per-block manabase flags — `commander-castability` (command-zone callout), `tap-analyzer` (untapped-sources block), `mulligan-eval` (opening-hand block), `plan-presence` ("with a plan" line), `plain-language-verdict` (plain-language verdict + ramp/draw budget), and `health-band-castability` (cast-rate can tip Solid→Workable) — now ship **ON by default**, so a fresh `/manabase` result shows the full read out of the box. An admin can still hide any one from `/Admin/Flags`. As part of this the opening-hand flag was renamed from `analysis.mulligan-eval` to `analysis.manabase.mulligan-eval` for namespace consistency; the store's idempotent rename migration carries any existing operator toggle state forward. Flipping the seed default does not retroactively change an existing database's stored rows — the live site's rows are flipped by an operator in `/Admin/Flags` (the rename aside, which preserves state).
- **Mana base now models conditional-untapped lands:** several lands that enter tapped *unless* a condition is met were being scored as always-tapped, under-rating well-built two-color manabases. Under the `analysis.manabase.accuracy` bundle (default **ON**), **bond lands** (Sea of Clouds, Training Center — "unless you have two or more opponents") are now counted **untapped** unconditionally, since a Commander game is always multiplayer; and **check lands** (Glacial Fortress cycle) plus **Snarls** (Frostboil Snarl cycle) are counted untapped when the deck runs at least six lands bearing a matching basic type (enough that the trigger is reliably available). Detection is by oracle template, so future printings of these cycles are covered automatically. **Fast lands and slow lands are deliberately left tapped** — in a long Commander game a fast land is usually tapped and a slow land only untaps late, so always-tapped is the fair approximation. Flag-off output is byte-identical.
- **Mana base reliably finds the commander from pasted exports:** `/manabase` now keeps the real command zone when a pasted Archidekt/Moxfield list encodes the commander outside the main deck, including Moxfield **Copy for MTGO** / **Copy Plain Text** exports that leave the commander as a lone trailing line after `SIDEBOARD` (previously it was dropped to the sideboard and a wrong mainboard card was guessed). After Scryfall resolution each inferred commander is eligibility-validated (Legendary Creature, Legendary Vehicle, a qualifying "can be your commander" planeswalker, or a Legendary Enchantment — Background), and partner/background pairs are preserved.
- **Commander picker instead of a silent wrong guess:** when import still can't identify a valid commander, `/manabase` stops guessing and shows a picker — a dropdown of the deck's own commander-eligible cards plus a name-search backstop — then re-runs the same analysis with the user's validated pick, preserving the pasted deck text and current options.

### 2026.07.2 — Cleanup, Refactor & Visual Polish (2026-07-05)
A behavior-neutral cleanup cycle — no new user-facing features, and paste artifacts stay byte-identical. Most of it is invisible internal work: a packet-service single-responsibility split, a theme semantic-token migration, an AI-agnostic `chatgpt-*` → `prompt-*` identifier rename (byte-identical render), and Studio creator-source model hardening. The one user-visible change is a round of **theme visual polish**, fixing pre-existing bugs the UI audit surfaced:
- **Readable active step-tab across all 24 themes:** the current workflow step (Deck Analysis / Comparison / Meta-Gap) is now a filled accent pill with contrast-checked text (WCAG ≥4.5:1), replacing the old low-contrast outline that was hard to read on several dark themes.
- **Per-theme accent, not a fixed blue:** the layout picker, layout-mode buttons, and clear-cache hover now use each theme's own accent color instead of a hardcoded blue that leaked into every theme.
- **Perceptible Layout picker:** on Deck Analysis, **Full / Compact / Advanced** now produce a visible layout change — Full gets a positive accent treatment, Advanced collapses the instructions panel.
- **Cleaner question-bucket toggles:** the expand/collapse control is now a labelled chevron (with an accessible name) instead of a stray bordered grey pill.
- **Studio pending-distill list fix:** the Harvest page's pending-distill list no longer shows videos that have already been distilled/approved/published. The query now excludes videos whose per-video `content_distill_status` is `distilled` (the sanctioned idempotency marker), while `failed` and `skipped_over_cap` stay listed because they are retriable.
- **Classifier keeps strategy/philosophy content + Filtered badge:** the distill classifier (`DistillationSchemas.ClassificationSystemPrompt`) now KEEPS substantial gameplay/strategy/philosophy content a Commander/cEDH player can apply — mulligan decisions, threat assessment, play-pattern and sequencing advice, table politics, meta/game-theory reasoning, and stated principles/heuristics — even when no specific card names are present (it still drops trivia, spoiler/news-only, promo/housekeeping, and guidance-free budget-pool reveals). Previously such videos were classified `filtered` and produced no summary. Relatedly, videos the classifier dropped (`content_distill_status = 'filtered'`) are no longer mislabelled "Harvested" in the pending-distill list: the loader surfaces the row's distill status and the Harvest page renders a distinct **Filtered** badge, so a rejected video is visibly distinct from a genuinely pending one. `filtered` rows stay listed (re-running distill re-classifies them under the current prompt); only `distilled` is excluded.

The 6-pillar UI audit re-scored 18 → 21/24. New per-theme visual-regression and interaction e2e tests were added so these render-level regressions are caught automatically.

### 2026.07.1 — Deeper Deck Evaluation (2026-07-03)
Three deeper deck-evaluation reads layered on the existing analysis engines with zero new dependencies. All three are flag-gated and ship **OFF** by default, so existing pages and paste artifacts stay byte-identical until an admin enables them.
- **Interaction & answers audit (Deck Analysis, Phase 79, flag `analysis.interaction-audit`):** `/deck-analysis` adds a card-backed count of the deck's interaction by bucket — targeted removal, board wipes, counterspells, protection/recursion, and stax/taxation — plus coverage-gap advisories for the buckets a deck runs thin on. Surfaced in the Step-3 results and the paste artifact; awareness-only — deck text is untouched, and with the flag off the artifact is byte-identical.
- **Win-condition & combo map (Deck Analysis, Phase 80, flag `analysis.wincon-map`):** `/deck-analysis` maps the deck's Commander Spellbook combos — both complete and one-card-away ("almost included") — into coarse early/mid/late assembly bands, ranked by the combos' own popularity and mana cost. Discloses "combo data unavailable" as distinct from "no win conditions," and reuses the single already-cached Spellbook lookup with no extra round-trip.
- **Opening-hand / mulligan evaluator (Mana Base, Phase 81, flag `analysis.mulligan-eval`):** the `/manabase` report and its paste artifact add an opening-hand read off the existing London-mulligan castability simulation — no second Monte-Carlo pass, so cast% stays byte-identical. It surfaces the keepable-hand band, the keep-size process (kept 7 / to 6 / to 5), colors/curve, and up to three representative openers that each name a tracked early play and whether it is castable on curve. A first-pass consistency signal, never a keep/mulligan recommendation.
- **Plan-presence "payoff on curve" (Mana Base, flag `analysis.manabase.plan-presence`, default OFF):** the opening-hand block and paste artifact gain a **"payoff on curve"** line — led by the share of keepable openers holding a **payoff** that is **castable on curve** (with its own high/medium/low band), followed by the composite "any win-directed card" percentage and a per-role breakdown (payoff / engine / tutor-combo / interaction). The payoff share leads because the composite saturates high on real decks (77–92%) and does not discriminate, whereas payoff coverage spreads cleanly (payoff-driven decks read high, combo/control read low — a correct profile signal, since a combo deck's closer is its tutor-combo line, shown in the breakdown). This is *role coverage*, a different axis from keepable-% (resources) and on-curve castability (velocity): a hand full of ramp and removal with no payoff is keepable but planless. Roles are classified in the Web layer (your Category Knowledge Store → Commander Spellbook combo pieces → an oracle-text heuristic, first-hit-wins; ramp / lands / filler draw never qualify) and passed as pure data into a dedicated deck-level Monte-Carlo pass that keeps Core I/O-free. **Permanent payoffs & interaction:** a **payoff** (a board threat) and **interaction** (removal / counters) count only when they are **permanents** castable on curve — a one-shot burn or extra-turn finisher, or a one-shot removal/counter, leaves nothing on the board to advance the win, so it earns no plan role (judged on the front face: an Adventure creature such as `Creature — Giant // Instant — Adventure` scores on its permanent creature front and still qualifies, whereas a spell/land MDFC such as `Instant // Land` scores on its instant front and does not). **Tutors and card draw still count even as instants/sorceries** — a sorcery tutor (Demonic Tutor) points at the permanent win, and card advantage furthers the plan — so those roles survive the gate on any card type. A pure counterspell, being a non-permanent, therefore no longer makes a hand "have a plan" on its own in either mode. The on-curve gate is load-bearing — a plan card drawn late or uncastable in color does not count. It reports the role components rather than one blended score, and stays a heuristic consistency signal, never keep/mulligan advice. When on, the opening-hand block's representative openers also prefer, at each mulligan depth (7 / 6 / 5), a hand that holds a castable-on-curve permanent plan card and name it — so you can see what a hand *with a plan* looks like, down to a mulligan to five. Turning the flag on adds a per-analysis category lookup and one Commander Spellbook fetch (both fail-open); OFF = byte-identical output and no extra calls.

### 2026.06.10 — Deck Evaluation & Creator Output (2026-06-10)
Four deck-evaluation and creator-output features. All four are flag-gated and ship **OFF** by default, so existing pages and paste artifacts stay byte-identical until an admin enables them. Each is detailed in its tool section above.
- **Bracket Check (`/bracket`, Phase 76, flag `tool.bracket.enabled`):** auto-classify a Commander deck into its official 1–5 bracket from Game Changers, two-card combos, and mass land denial — computed locally, no AI needed for the number — with an optional balancer prompt to hit a target bracket. See [Bracket classifier and balancer](#bracket-classifier-and-balancer).
- **Tap analyzer (Mana Base, Phase 75, flag `analysis.manabase.tap-analyzer`):** the `/manabase` report and its paste artifact surface untapped-source quality — overall untapped frequency, turn-1 untapped availability, and a per-color untapped breakdown for multi-color decks. Informational only; never changes the land count or verdict.
- **Multi-axis deck score (Deck Analysis, Phase 77, flag `analysis.multi-axis-score`):** `/deck-analysis` adds a four-axis **Power / Speed / Control / Consistency** 0–5 score to the Step-3 results and all three paste artifacts, with a bracket cross-check. Deterministic heuristic bands computed in `DeckFlow.Core` — no AI round-trip for the numbers.
- **Auto-refreshing Deck Primer staleness banner (Phase 78, flag `tool.primer.stale-flag`):** the Deck Primer flags on Step 3 when the deck in Step 1 differs from the deck a resumed primer was generated against ("Deck changed since this primer was generated — N cards differ") with a **Regenerate primer** button. Resume renders the saved primer verbatim — no auto-rebuild, no upstream re-fetch; regeneration is the explicit button press.

### 2026.06.9 — Manabase Accuracy, Command-Zone Awareness & Cross-Tool Persistence (2026-06-27)
- **Plain-language manabase verdict (Phase 71):** the Mana Base analyzer now leads with a plain-language advisory and metric glosses, so the headline reads in words ("functions, average on-curve cast rate is good") rather than only numbers — the underlying Karsten/simulation surfaces are unchanged. Flag-gated (`manabase.plain-language-verdict`).
- **Manabase command-zone + commander castability (Phase 72):** the analyzer is command-zone aware — it threads the full command zone (partners, commander+Background, companion) through deck loading and adds a commander-castability lens in the UI. Flag-gated (`manabase.commander-castability`).
- **Deck-analysis command-zone awareness (Phase 73):** the `/deck-analysis` prompt artifact now names the full command zone and an optional **companion** so your AI sees who sits in the command zone. A flag-gated Step-1 **Companion (optional)** field lets you name the companion for Archidekt/pasted decks (auto-detected from Moxfield). Awareness-only — deck text is untouched; with the flag off the prompt is byte-identical. Flag-gated (`analysis.command-zone-awareness`).
- **Cross-tool deck-input persistence (Phase 74):** paste a deck once and the single-deck tools carry it for you (sessionStorage, silent fill-if-empty), with a "Restored your last deck" notice and a **Start Over** that clears the carried deck.
- **Deck Primer output-style toggle:** the Deck Primer gains a style toggle — **Moxfield-rich** formatting and a **Full cEDH** competitive-depth style (visible at the cEDH bracket).
- **Feature-flag key namespacing:** operator feature-flag keys are namespaced (`tool.*` / `service.*` / `analysis.*` / `manabase.*`), existing rows are migrated with their toggle state preserved, and the Admin → Flags page adds an instant client-side key-prefix filter and per-`tool.*` descriptions.

#### Mana base accuracy — mana quantity, ramp credit & color-aware mulligan (Phase 70)
Four accuracy fixes are now **on by default** after a baseline across 8 real decks:
- **Per-source mana quantity:** burst sources now pay their real output (Sol Ring / Ancient Tomb = 2, Gilded Lotus = 3 of one color) on the affordability side, so expensive payoffs read correctly. The Karsten color counts are untouched.
- **Tighter ramp credit:** the land-target reduction for cheap ramp/draw is narrowed to **repeatable** ramp and true card draw — one-shot rituals and Treasure-makers no longer soften the land target.
- **Color-aware mulligan:** the castability simulation's London mulligan now ships hands that are land-count-fine but color-screwed (a 2+ color deck wants 2 colors in its opening lands), lifting cast% toward what real play achieves. Mono-color decks are unchanged.
- **Land-ramp in the simulation:** repeatable land-ramp (Cultivate, Rampant Growth) now puts its fetched land into the simulation as persistent colorless mana, so expensive payoffs in ramp decks read correctly instead of being under-rated. This is the only fix that can improve the overall verdict (never worsen it).

These are toggleable feature flags (`analysis.manabase.source-mana-quantity`, `analysis.manabase.ramp-credit-v2`, `analysis.manabase.color-aware-mulligan`, `analysis.manabase.land-ramp-sim`) for safe rollback.

Follow-up accuracy fixes (always on):

- **Board-scaling self cost reducers modeled:** cards that read *"costs {X} less to cast, where X is the greatest power among creatures you control"* (e.g. **The Skullspore Nexus**) are now resolved against the deck's greatest fixed creature power and **auto-applied** to the analysis, so a big-creature deck casts them at their real reduced cost instead of full price. The reduced cost also pre-fills the editable cost-override box, so you can dial in a different on-board assumption.
- **Actionable weakest color:** the flagged "weakest color" is now the color a new source would actually help (the one with the broadest color-limited shortfall), not whichever color happens to own a single expensive late-casting bomb. A curve-limited card no longer makes an over-supported color look like the problem.
- **Honest land advice:** when the deck is below the Karsten land count but the simulation shows every spell still casts fine (a ramp-saturated deck), the header now reads *"~N under the Karsten count, but ramp covers it"* and the "biggest fix" stops recommending lands you don't need.

#### Mana base accuracy — four-tier scale, curve-aware verdict & cast delay
- **Mulligan-aware source requirements:** the per-color "sources needed" figure now comes from the simulation itself (binary search for the smallest on-color count whose simulated cast % clears the bar) instead of the mulligan-blind hypergeometric. It models **Commander's free first mulligan**, so a tight turn-two `{W}{W}` no longer reads against an inflated requirement (e.g. a real Brago list dropped from a phantom "needs 30 white" to a sane "needs ~21"). The figure is **clamped to Karsten's published table** as a ceiling, so the simulation can only *lower* a requirement, never inflate a double-pip past what the math allows.
- **Four-tier health scale:** the verdict reads on a graded **Excellent / Solid / Workable / Needs work** scale that combines Karsten source checks with the simulated avg-on-curve headline. A high-mana-value bomb that casts late because it is expensive (a curve problem the base can't fix) no longer drags the verdict down. If the simulation says the deck functions, with a good average on-curve cast rate, no catastrophic color, no color short by more than about two Karsten sources, and no broad color-access shortfall, a land-count shortfall plus one soft color issue stays *Workable* instead of being forced to *Needs work*. Severe or broad color shortages, two-plus color issues, and land shortages the simulation corroborates still read as *Needs work*. Demanding cards are still surfaced by name.
- **Coherent "Biggest fix" callout:** the single most actionable fix is chosen so it never contradicts the land/health line — it points at the color that is genuinely short, else at the land count, else at trimming the top end, and never recommends a negative or "remove" source count.
- **Average cast delay:** the castability table adds an **Avg delay** column — the mean number of turns late each spell first becomes castable (*on curve* when it lands on time, else *+N.N turns*), capped at the grace horizon when it never resolves — as supporting context next to the on-curve %.
- **Deck-load review step:** a **Load deck & detect costs** action resolves the deck and surfaces the auto-detected reduced/alternative-cost suggestions for review/edit *before* you run the analysis.
- **Unsupported-interaction disclosure:** cards the analysis can't fully model — **X / variable costs** (skipped from the castability simulation) and **flexible split pips** (hybrid / Phyrexian / twobrid — no hard color requirement, per Karsten) — are listed by name in a collapsible note so a clean verdict never silently hides them.

### 2026.06.7 — Mana Base Modes & Castability (2026-06-21)
- **Casual / cEDH modes + commander importance:** the Mana Base analyzer now has a **Deck type** selector — *Casual* (Karsten's full land target) or *cEDH* (the competitive ~28–32 land band) — plus a **commander-importance** selector (*Central / Standard / Low*) that controls how hard it holds the commander's colors to threshold (without moving the land target). Both persist across the postback.
- **Per-card castability (Casual):** a worst-first table of each spell's estimated chance to be cast on its on-curve turn, from a Monte-Carlo simulation (London mulligan, joint mana+color, in-sim ramp, fetchlands credited to the colors they can fetch). The commander is pinned; rocks/dorks are counted but not listed. Cross-checked against the [Salubrious Snail](https://www.salubrioussnail.com/manabase-tool) calculator (mean ~3 pts).
- **Aggregate color findings:** each color reflects every card needing it (mean castability + under-supported count) while a single uncastable bomb still surfaces; the weakest color leads. A color is only flagged as *color-starved* (and only then does the verdict advise adding sources of it) when it is genuinely short of sources — so a color running a source **surplus** never reads "needs work" or "add lands" just because a cheap spell misses its turn-one colour window (a structural single-land-drop limit no extra sources would fix). Such a card is also dropped from the "hardest to cast" list (that list is meant to expose weak *support*, which it isn't), though it still counts in the honest under-supported tally — so nothing contradicts a color table that shows the colour over-supplied. Mana-limited curve bombs and genuinely source-short cards still surface there.
- **Opening-hand keep band:** the London-mulligan keep is the *sweet spot* — **3 lands** (2 only with a ramp piece), mulliganing **4–5 land floods**; a high-mana-curve deck keeps its wider band (up to 5). This drives the keepable-hand share and the representative openers.
- **"Show the work" formula panels:** two expandable panels — the methodology, and the Karsten regression evaluated term-by-term for your deck — so any verdict is auditable.

### 2026.06.6 — Studio Automation, Sync & Polish (2026-06-21)
- **One-click Harvest + Auto-distill in Studio (Phase 59, AUTO-01/AUTO-02):** the Studio Harvest page now has a default **"Harvest + Auto-distill"** action beside the original "Harvest Selected" button. On a **subscription ($0) provider** (`DECKFLOW_LLM_PROVIDER=claude`) one click harvests the selected videos, then distills exactly the *harvest-ready* ones (the videos that actually transcribed — skipped/no-caption/already-distilled picks are excluded) in the same action, with no separate Distill click. A per-video **outcome card** then reports harvested / distilled / auto-approved / left-in-review / dropped / failed (with failed ids) in one place. A small **Auto-approve panel** (on/off toggle + clip cutoff, **default ON at 5 clips**, persisted across Studio restarts) controls whether high-clip distills skip the review queue: a distill with clips ≥ cutoff is auto-flipped to `approval_status='approved'` (it only sets approval status — publishing to prod stays a separate operator-confirmed gate), while below-cutoff distills stay in the review queue. With auto-approve **off**, every distill enters the review queue. On a **metered provider** the one-click action does **not** live-distill (Core refuses unmetered classification on a metered provider) — it harvests, shows a "live distill requires a subscription provider" message, and points you to the manual **Distill** section, whose dry-run spend preview stays available. The original manual harvest/distill flow is kept intact as a fallback (and a completing subscription distill there auto-approves through the same shared step).
- **Pull from Prod — read-only prod→local reconcile in Studio (Phase 60, SYNC-01/02/03; live progress panel Phase 62, SUI-03):** a new **Pull from Prod** page in DeckFlow.Studio that is the read mirror of Direct Push and is **strictly read-only toward production**. Stage 1 reads the live prod `content_site_index` through a dedicated read-only reader (a plain `SELECT` only — no schema-ensure DDL, and the reader exposes no write method at all) and SCP-downloads the prod artifacts into an isolated `pull-staging/` directory (never the live `content-kb/`). A **live Pull Log panel** streams each stage transition (prepare staging → read production content_site_index → download artifacts → classify) and a per-artifact result line ("downloaded …" or "not downloaded: …") as the pull runs, so you can see progress without waiting for the final diff table. All progress copy is sanitized — no connection string, SSH target, absolute path, or raw exception ever appears. It then classifies each entry against your local store into one of four kinds — **prod-newer, missing-locally, local-only, diverged** — omitting anything already in sync. Stage 2 lets you resolve each differing entry **locally**: *adopt-prod* updates the local row's content columns and mirrors prod's `approval_status`, promoting the downloaded artifact into `content-kb/` (a partial pull whose artifact failed to download still updates the row, skipping only the file move); *keep-local* writes nothing. Production is never modified — adopting never auto-publishes, and the prod side has no write path. The prod connection string and SSH target live in user-secrets only and never enter the repo, logs, or any error message. *(Superseded in Cycle 16 / Phase 92 — see "Pull from Prod — field-authoritative prod→local reconcile" in the Content-KB sync section above: Pull now resolves bodies from the local git tree instead of SFTP-downloading them, preserves operator DB fields rather than mirroring prod, and surfaces body-vs-index divergence for per-entry opt-in.)*
- **Curated creators + harvest dropdown in Studio (Phase 61, SRC-01/SRC-02):** a new **Creators** page (`/creators`) lets the operator maintain a persisted list of curated creators/channels (add display name + channel URL/handle/ID, view, remove) stored in `content-kb.db` and surviving Studio restarts. The Harvest page's browse section then shows a **creator dropdown** populated from that list — pick a saved creator to fill the browse target instead of pasting a channel URL each time; the paste-URL/handle input remains as the one-off fallback when no creator is selected.
- **Unharvested-only browse + Skip in Studio (Phase 61, HSEL-01/HSEL-02/HSEL-03):** the Harvest browse list now **defaults to showing only not-yet-harvested videos**, with a **"Show all"** toggle to reveal harvested/distilled/approved/published rows. Each candidate also has a **Skip** action (lighter than Block) that hides it from selection without deleting any artifact or writing a harvest blocklist entry; skipped videos are excluded from selection in both views. A single canonical visible projection drives the rendered rows, Select-All, and the harvested set, so a row hidden by the filter or by skip can never be harvested. A **Skipped** page (`/skipped`) lists skipped videos and lets you **un-skip** one to bring it back (the parity partner to Block/Unblock).
- **Consistent status badges + Studio About link (Phase 62, SUI-01/SUI-06):** pipeline status (Not harvested / Harvested / Filtered / Distilled / Approved / Published / Blocked / Already in DB) now renders from a single shared `Shared/StatusBadge.razor` component on both the Harvest and Review pages — the inline `RenderBadge` switch in `Harvest.razor` is gone and Review's per-row status derives from the same `VideoStatusResolver.FromContentRow(approvalStatus, pushedToProdUtc, isVisible)` pure mapper that `ResolveStatusAsync` now routes through, so the Published/Approved/Distilled rule lives in exactly one place. The leftover Blazor-scaffold "About" link in the Studio layout now points to `https://www.deckflow.gg`.
- **Creator filter on Harvest browse and Review queue (Phase 62, SUI-05):** both the Harvest browse list and the Review queue now show a **"Filter by creator"** dropdown (default "All creators") whenever the current view contains rows from more than one creator. On the Harvest page the creator is derived from each video's `ChannelTitle`; on the Review page it is parsed from the stored `ArtifactPath` (`content-kb/<creator-slug>/…`). The filter composes with all existing filters — Harvest's unharvested-only default and skip exclusion still apply inside the filtered view, and the canonical visible projection (`GetVisibleChannelVideos`) enforces that a row hidden by the creator filter can never be harvested or selected even if it was checked before the filter changed. Publish is out of scope (no per-row list).
- **Tightened harvest→review→publish flow and grouped navigation (Phase 62, SUI-02/SUI-04):** the **Review queue** now shows a **"Go to Publish"** link/button (with an approved-entry count) whenever at least one entry has been approved, so you can jump straight from reviewing to publishing without navigating the sidebar. The Studio **sidebar navigation** is now grouped into a **Pipeline** section (Home → Harvest → Creators → Review → Publish → Direct Push → Pull from Prod) and a **Support** section (Skipped, Blocked) — every existing destination is preserved, and section headers make the flow direction obvious at a glance. The Harvest Select-All was already scoped to the visible/filtered rows (Phase 61 invariant) and is unchanged; per-row checkboxes remain the multi-select mechanism.
- **Studio help panels + Workflow Guide:** DeckFlow.Studio now adds a collapsible, default-collapsed **Help** panel to each major page so operators can see what the page is for, where it fits in the pipeline, and the main gotchas without changing any workflow behavior. A dedicated **Workflow Guide** page at `/guide` also walks the full path from dashboard and creator curation through harvest, distill, review, either publish path, and the follow-up maintenance pages.
- **Self-contained Studio executable (Phase 63, DIST-01):** `DeckFlow.Studio` can now be published as a single-file, self-contained **win-x64** executable (~116 MB) that the operator runs on a clean Windows box **with no .NET install**. A re-runnable publish script (`scripts/publish-studio.ps1` / `.sh`) produces `artifacts/studio-release/` + a dated zip; the executable pins its Kestrel port, writes a crash log, and auto-opens the browser on launch. See [DeckFlow.Studio/STUDIO-SETUP.md](DeckFlow.Studio/STUDIO-SETUP.md) for build/run/secrets steps.

### v1.7 — Local Harvest & Publish Studio + Visual Refresh (2026-06-17)
- **Visual refresh — 6-pillar UI audit remediation (Phase 48):** the deployed site was audited against six visual-design pillars and remediated from 16/24 to **20/24** — hub cards and section headers gained inline-SVG iconography and resting elevation, surfaces now lift off the page background, the smallest helper text was raised above the legibility floor, headings/labels got a real type hierarchy, and short tool pages (Card Lookup, Ask a Judge) close with an example panel instead of dead space. All changes are theme-token-scoped, so every guild theme (light, dark, and the Commander Table fork) inherits them; verified at mobile + desktop.
- **DeckFlow.Studio — local operator console (Phases 41/45/46/47):** a new standalone Blazor Server app (`DeckFlow.Studio`, run locally by the operator) to browse/paste YouTube videos → harvest captions → distill to Content-KB entries via an LLM (with a spend dry-run gate) → review/approve in a queue → publish to production two ways: a git commit-publish of the LF-normalized seed (→ Render deploy), or a direct prod push (SSH.NET SCP of artifacts to the Render disk + a safe content-columns-only Postgres upsert that preserves admin fields, then a git-durability **Stage 4** that commits **only** the pushed bodies — never the seed — and pushes the current branch with `[skip render]` so production is not redeployed; the push refuses if any commit ahead of origin is not one of its own durability commits, and fails closed if the branch's remote state can't be verified). The prod connection string lives in user-secrets only and never enters the repo or logs.
- **Under the hood:** harvest/distill/export logic moved out of the CLI into `DeckFlow.Core` as `IContentKbOrchestrator` (Phase 42); data access in the dual-provider stores moved to Dapper behind the existing dialect abstraction with Sqlite+Postgres parity preserved (Phase 49); `/Admin/Harvest` got AJAX lazy paging + a `LOWER(commander_name)` index (Phase 44); and a changed-lines `.editorconfig` format gate now runs as a pre-commit hook + CI job (Phase 50).

### v1.6 — Content KB Browse-Only Pivot (2026-06-12)
- **Content KB retrieval fix + value re-validation gate (Phases 34/35):** fixed the retriever (per-video clip-diversity cap, topical-fit scoring over tag breadth, prompt-injection sanitizer + Spike-001 regression test), then ran a **blind, multi-deck A/B value gate** on the AI answers. Verdict: **MARGINAL** — the KB clip-injection did not earn its place in the prompt.
- **Retire clip-injection; KB becomes browse-only (Phase 37):** per the recorded gate pivot, whole-channel clip-injection into deck-analysis prompts (the `## Expert Context` block, expert-selection widget, "What Experts Say" panel, retriever services) was **removed**. The Content KB is kept as an un-darked **browse-only reference** at `/content-kb`, and the deck-analysis page points users there for copyable prompts.
- **Rebuild KB corpus + admin block/hard-delete (Phases 37.5/37.6):** corpus reset + high-signal re-harvest under a quality-classifier filter (and a fix so clips carry real mid-video timestamps, not `[00:00]`); admins can block a YouTube video by id so the harvester never re-ingests it and hard-delete its rows.
- **Controller / CLI SRP split (Phase 38):** the `DeckController` god-class was decomposed into 8 focused feature controllers and `DeckFlow.CLI/CommandRunners` into deck-domain vs content-KB runners — **all routes and CLI commands preserved unchanged** (mechanically proven route-parity + a live render smoke).
- **Architecture-review refactor (Phase 39):** duplicated deck-loading + Scryfall card-resolution were extracted out of the four prompt-packet services into a shared `IDeckEntryLoader.LoadFromSourceAsync` + `IScryfallCardResolver` — behavior byte-identical, guarded by the existing packet-service test suites.

### v1.5 — Deck Primer + Content KB Integration (2026-06-10)
- **Deck Primer Generator (Phase 31):** a fourth paste-ready workflow at `/deck-primer` — paste/import a deck and DeckFlow builds a structured "explain this deck" prompt artifact (game plan, key interactions, mulligan/sequencing guidance) with the same download/upload artifact flow as the other generators. An output-style toggle offers a clean Standard primer or a Moxfield-style rich primer (table of contents, callout boxes, collapsible combo lines, tables, ASCII/markdown visuals); at the cEDH bracket a Full cEDH option adds full-section competitive depth.
- **Content KB integration (Phases 30/32/33):** distilled creator knowledge could be wired into the deck-analysis prompt (expert pin/follow/evergreen selection + a "What Experts Say" panel), shipped **dark** behind `tool.knowledge-base.enabled`. *(Note: this prompt-injection path was retired in v1.6 — see above; the KB is now a browse-only reference.)*
- **CLI `--video-ids`:** `harvest` and `distill` accept a comma-separated list of YouTube video ids (plus `--source-id` to disambiguate) to process exactly those videos instead of the most-recent walk.
- **Admin YouTube Export:** `/Admin/YoutubeExport` downloads a channel's upload list (title, view count, upload date, URL) as text or CSV, walking the uploads playlist up to 500 videos.
- **JS test runner + CI:** Vitest + jsdom for the browser TypeScript modules, plus the first GitHub Actions CI (build + xUnit + Vitest).

### v1.4 — Content Knowledge Base
- **Content Knowledge Base (Phases 19-22):** a local CLI pipeline harvests YouTube captions (Whisper fallback with monthly spend caps), distills each video into a markdown prompt artifact (≤200-word summary, 3-8 timestamped clips, controlled-vocabulary tags) via OpenAI **or** the `claude` CLI ($0 subscription path), and publishes a slim index to the site. The public `/content-kb` browse/detail pages are gated behind the `tool.knowledge-base.enabled` feature flag; admins curate which entries are visible per entry or per source. See the Content Knowledge Base section below.
- **Category cache rebuild (Phases 24/26/27):** integer-keyed star schema (hot commander aggregate went from a 69s timeout to 0.66ms), read-time `CategoryFilter` fix so colorless staples like Sol Ring always return categories, and content-hash dedup with a 5-day refresh on deck writes.
- **Admin mobile + tooling (Phases 16/18/25):** the admin shell is mobile-responsive (≥320px, ≥44px touch targets), the harvested-decks view is a server-side paged commander grid, and destructive admin actions use a native focus-trapped `<dialog>` confirm modal.
- **Doc-warning gate (Phases 17/23):** every public type and member in `DeckFlow.Web` carries XML doc-comments; the `NoWarn 1591;1573;1587` suppression was removed and the warning gate is live, scoped to `DeckFlow.Web/**`.
- **Removed:** the user-triggered "Run 5-Minute Archidekt Harvest" button on Category Suggestions — harvesting is driven by the background hosted service.

### v1.3 — AI-Agnostic Workflow & Hardening
- **AI-agnostic workflow URLs (Phase 12):** `/chatgpt-deck-analysis`, `/chatgpt-deck-comparison`, and `/chatgpt-cedh-meta-gap` now 301-redirect to `/deck-analysis`, `/deck-comparison`, and `/cedh-meta-gap`; page H1s, nav labels, hub labels, and artifact zip filenames use AI-agnostic wording.
- **Claude JSON wrapper cleanup (Phase 999.2):** Claude prompt variants no longer ask Claude to wrap JSON in `<result>...</result>` tags; ChatGPT and Gemini variants are unchanged, and legacy zips still parse through the backward-compatible `<result>` branch.
- **Packet download caching (Phase 999.3):** Deck Analysis, Deck Comparison, and cEDH Meta-Gap download endpoints reuse the Scryfall pipeline result built during preview, so a large Commander deck's download click completes in under 2 seconds instead of 2+ minutes. Cache is in-memory only (process-local, 5-minute TTL, 10MB cap); cache miss falls through silently to the full pipeline.
- **Truncated AI response inline errors (Phase 999.4):** Truncated JSON pasted into the response textarea on Deck Analysis, Deck Comparison, or cEDH Meta-Gap now renders the inline workflow message "The pasted response appears truncated — wait for the AI to finish generating before copying, then re-submit." instead of a generic error page with a raw stack trace.
- **Test hardening and semantic guards (Phase 999.5):** Four pre-existing test failures were fixed, `DeckComparisonService.ParseComparisonResponse` and `MetaGapService.ParseResponse` now reject valid JSON with no meaningful Deck Comparison or Meta-Gap content, and redundant ChatGPT `<result>` prompt directives were removed from five ChatGPT prompt variants.
- **Harvest job lookup fix (Phase 999.6):** `IHarvestRunStore.GetByIdAsync(Guid id, CancellationToken ct = default)` lets `ArchidektCacheJobService.GetJob(jobId)` return completed and terminal harvest job states using provider-specific Guid binding for SQLite and Postgres.

---

## License

DeckFlow is licensed under the [Apache License 2.0](LICENSE). Copyright 2026 Chris Lunt.

### Code vs. brand

The Apache 2.0 license covers the **source code only**. You are free to use,
modify, and self-host it — including commercially — provided you keep the
license and copyright notices and reproduce the [`NOTICE`](NOTICE) file in any
redistribution.

The license does **not** grant any right to the DeckFlow name, logo, or brand.
Under Apache 2.0 §6, trademarks are excluded from the grant. If you fork or
self-host, you must:

- not name your instance or derivative "DeckFlow";
- not use the DeckFlow logo or branding;
- not represent your deployment as the official DeckFlow, as originating from
  `deckflow.gg`, or as endorsed by or affiliated with DeckFlow.

"DeckFlow" is a trademark of Chris Lunt.
