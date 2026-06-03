# v1.4 Research Synthesis — Roadmapper Input

**Milestone:** DeckFlow v1.4 — Content Knowledge Base Foundation + Admin Mobile + v1.3 Backlog Cleanup
**Synthesized:** 2026-05-23
**Confidence:** HIGH for existing-pattern reuse + library version selection; MEDIUM for Whisper $/min figures (vendor-pricing volatility).

> **SCOPE UPDATE 2026-05-23:** Cluster D (Gemini paste-limit unblock — GEM-01/02) REMOVED from v1.4 by user decision. Deferred to v1.5. The Phase 3 entry in build order below (Gemini split-message) is informational only — REQUIREMENTS.md is authoritative; roadmapper should skip it. Critical path now: A → C → E (#5 → #6 → #7 → #8). v1.4 = 4 clusters (MODAL, DOC, AMOB, KB) / 16 REQ-IDs.

## 1. Stack Additions Quick-Ref

- **YoutubeExplode 6.6.0** — YouTube transcript + ASR caption fetch (no API key, no quota); the only viable path because `Google.Apis.YouTube.v3.captions.download` returns 403 for third-party videos (Issue Tracker 241669016 verified)
- **OpenAI 2.10.0** — single SDK for Whisper transcription (`AudioClient`) + chat summarization + Structured Outputs tagging; integrated via `HttpClientPipelineTransport(httpClient)` seam to stay inside `IHttpClientFactory` lifecycle
- **System.ServiceModel.Syndication 10.0.2** — Microsoft RSS/Atom for podcast feeds; iTunes namespace via `ElementExtensions` (~30 LOC helper)
- **(Gemini, no new package)** — hand-roll Gemini REST via existing RestSharp + named Polly pipeline if Path B chosen; reject `Google.GenAI` 1.7.0 + `Google_GenerativeAI` 3.6.6 (transitive `Microsoft.Extensions.AI` / Newtonsoft baggage)
- **(Spend ledger, no new package)** — new `whisper_spend_ledger` table via existing `IRelationalDialect` + `RelationalDatabaseConnection`; `numeric(10,4)` (PG) / TEXT (SQLite)
- **(Admin mobile + modal + doc-comments, no packages)** — CSS factoring + native `<dialog>` + csproj NoWarn strip; explicitly NO Bootstrap/Tailwind/StyleCop

**Five new named HttpClients:** `youtube-explode`, `podcast-rss`, `podcast-audio`, `openai`, `gemini-rest` (each with matching named Polly pipeline). **Five new env vars:** `OPENAI_API_KEY`, `DECKFLOW_GEMINI_API_KEY`, `DECKFLOW_WHISPER_MONTHLY_CAP_USD`, optional `DECKFLOW_WHISPER_CAP_THRESHOLD_PCT`, `DECKFLOW_WHISPER_KILL_SWITCH` — all `sync: false` on Render.

## 2. Feature Cluster Quick-Map

| Cluster | Features (FEATURES.md) | Complexity | Critical Dep | Risk |
|---------|------------------------|------------|--------------|------|
| **A. WDG-04 Focus-Trapped Modal** | Feature 6 | S (1-2 plans) | None — pre-req for C + E | LOW |
| **B. Doc-Comment NoWarn Backlog** | Feature 7 (~88 types) | M (2-4 plans, mechanical) | None; split into early + late halves | LOW (sequencing trap if NoWarn stripped before backfill) |
| **C. Admin Mobile Sweep** | Feature 4 | S-M (2-4 plans) | Cluster A modal lands first | MED — 22-guild-theme CSS bleed risk |
| **D. Gemini Paste-Limit Unblock** | Feature 5 | S-M (2-4 plans) | Independent; UAT-gated path choice | MED — split-message UX may fail UAT, forcing Path B Gemini API |
| **E. Content KB Phase 1** | Features 1+2+3 (ingestion, source CRUD, spend cap) | L (6-8 plans) | Schema before HTTP before orchestrator before UI; spend-ledger before adapters fire | MED-HIGH — 4 new upstream surfaces + Whisper budget exposure + Render IP-block risk |

## 3. Suggested Phase Sequence

| # | Phase | Why this order | Deps | Risk |
|---|-------|----------------|------|------|
| 1 | **WDG-04 Modal (Cluster A)** | Closes v1.3 carry-over. Tiny: 1 TS + 1 view + small CSS. Zero coupling. Lands first as proof | None | LOW |
| 2 | **Doc-Comment Backlog Part 1 — Controllers + Services (B subset)** | ~50 of 88 types. Mechanical. NoWarn stays until Part 2 | None | LOW |
| 3 | **Gemini Unblock — split-message (D Path 1)** | Closes v1.2 deferred flag. 5 PromptBuilder files + 3 views. Preserves Phase 999.2 D-08. Regression-tested before admin sweep | None | MED — UAT-gated |
| 4 | **Admin Mobile Sweep (C)** | AFTER WDG-04 so modal CSS doesn't need re-architecting mid-factoring. Splits `admin.css` → `admin-common.css` + `admin-mobile.css` + import shim | Cluster A | MED — full admin regression |
| 5 | **Content KB Stores + Schema (E foundation)** | First half. 8 new `content_*` tables. Zero UI; zero outbound HTTP. Validates schema before HTTP services depend on it | None within v1.4 | MED — F-PROD-CONTRACT test-isolation (999.6 lesson) |
| 6 | **Content KB Outbound HTTP Services (E ingestion)** | YouTube + Podcast + Whisper + LLM. IHttpClientFactory + RestSharp + Polly. WhisperSpendLedger cap-gate integrated. MockHttp tests | #5 | MED-HIGH — 4 new upstream surfaces |
| 7 | **Content KB Orchestrator + Harvest Runs (E coordination)** | `ContentHarvestOrchestrator` + `ContentHarvestRunStore`. Wires #5 + #6. Tests assert cap-abort, idempotent re-run, partial-success | #5, #6 | MED |
| 8 | **Content KB Admin UI (E UI)** | 3 admin controllers + 7 Razor views + sidebar additions. Inherits Cluster C's CSS. SameOriginRequestValidator on every POST | #4, #5-7 | LOW |
| 9 | **Doc-Comment Backlog Part 2 + strip `NoWarn` (B finish)** | Remaining ~38 types. LAST is csproj edit. Triggers warnings-as-future-gate. Lands last so v1.4 new types (D + E) are documented before gate flips | All prior | LOW |

**Critical path:** A → C → E (#5 → #6 → #7 → #8). Off-critical-path: B (parallelizable), D (independent).
**Total scope estimate:** 5-8 numbered phases / ~25-35 plans (~v1.1 + v1.2 combined).

## 4. Top Pitfalls That Drive Phase Design

Ranked by likelihood × impact:

1. **P1 — YouTube `captions.download` returns 403 for non-owned videos** → **Phase 6 (Outbound HTTP Services)** → Use YoutubeExplode (NOT Google.Apis.YouTube.v3); prove against 5 real cEDH channels before writing service code.
2. **P2 — YouTube IP-blocks Render egress (cloud IP blacklist)** → **Phase 6** → Design `IYouTubeTranscriptFetcher` with proxy-pluggable abstraction day 1; track `whisper_fallback_ratio` >25% as IP-block signal; harvest from deployed Render, NOT WSL, before ship.
3. **P3 — Whisper cap TOCTOU race (two admin tabs double-spend)** → **Phase 7 (Orchestrator)** + Phase 6 → Postgres `pg_try_advisory_lock` per month-key; SERIALIZABLE transaction wrapping check-and-insert; pre-flight estimate BEFORE Whisper call; UTC month boundary; hard kill-switch env var.
4. **P4 — LLM JSON parse failure mid-harvest after Whisper cost incurred** → **Phase 6 + Phase 7** → OpenAI Structured Outputs (`strict: true`, <0.1% failure); staged-pipeline persistence (transcript/summary/tags each own row + status); KnownTagSet allowlist; never re-Whisper on resume.
5. **P6 — Postgres pool starvation from connections held across `await`** → **Phase 6 + Phase 7** → NEVER hold connection across HTTP call; cap pool ~10-15 explicitly; single-worker hosted harvest; release-acquire pattern documented in plan-checker rule.

**Secondary critical:** P7 (Whisper 25MB / 10min timeout) needs Dockerfile `apt-get install ffmpeg` + client-side audio chunking. P11 (admin POST missing anti-forgery) — global `AutoValidateAntiforgeryTokenAttribute` filter recommended. P12 (schema name collision with v1.1 `harvest_runs`) — strict `content_*` prefix; `ContentHarvestRunStore` is parallel impl, NOT subclass.

## 5. Cross-Cutting Invariants (MUST/MUST NOT)

1. **MUST** route all outbound HTTP through `IHttpClientFactory` named clients + named Polly pipelines via `ResiliencePipelineProvider<string>`. **MUST NOT** migrate to `Microsoft.Extensions.Http.Resilience` standard handler. **MUST NOT** `new HttpClient()` anywhere.
2. **MUST NOT** touch the `AiPlatform` value object for server-side LLM summarization; admin ingestion is a single dedicated provider. AiPlatform variant added ONLY if Gemini Path B chosen.
3. **MUST NOT** widen the v1.1 `harvest_runs.kind` CHECK or extend `HarvestRunStore`; fork to a parallel `ContentHarvestRunStore` on `content_harvest_runs` table.
4. **MUST** namespace all new tables with `content_*` prefix (except `whisper_spend_ledger`); zero overlap with v1.1 names.
5. **MUST** call `IWhisperSpendLedger.WouldExceedCapAsync(estimate)` BEFORE every Whisper API invocation; cap-gate is correctness-critical. **MUST** record ledger row on success only.
6. **MUST** use env var `DECKFLOW_WHISPER_MONTHLY_CAP_USD` for the cap (typed decimal); **MUST NOT** route cap through `IFeatureFlagStore` (wrong tool for $-cap).
7. **MUST** put `SameOriginRequestValidator` on every `/api/*` POST AND `[ValidateAntiForgeryToken]` (or global filter) on every `/Admin/*` POST — two separate CSRF mechanisms.
8. **MUST** preserve `{ get; init; }` on every new record type (System.Text.Json silently skips get-only props in .NET 9+ — already broke `EdhTop16Client` once).
9. **MUST** preserve C# raw-string literals byte-for-byte in `SummaryPrompt.cs` and DDL constants; no auto-reformat passes.
10. **MUST** use native HTML `<dialog>` element with `showModal()`; **MUST NOT** add a focus-trap npm dependency.
11. **MUST** scope new admin CSS to `.admin-shell` parent class; **MUST NOT** add unscoped element selectors (`table`, `button`, `input`) — 22-guild-theme bleed risk. Use `@layer admin { ... }` for cascade discipline.
12. **MUST** put layout CSS in `site-common.css` / new `admin-common.css`, NOT in `site.css` or `admin.css` directly.
13. **MUST** isolate every store test (own SQLite file or `:memory:` per-fact scope) — F-PROD-CONTRACT lesson from 999.6.
14. **MUST** keep all API keys in Render env vars with `sync: false`; **MUST NOT** ever commit secrets to public repo. Pre-commit Gitleaks recommended.
15. **MUST** route every plan through Codex peer review (`/gsd-review`) before execute-phase dispatch; Codex codes, Claude reviews.

## 6. Recurring v1.3 Patterns (Process Pitfalls — DO NOT REPEAT)

- **R-1 STATE.md arithmetic drift** — auto-compute counters on phase close; CI gate `gsd-sdk verify-state` asserts `completed_phases ≤ total_phases`.
- **R-2 REQUIREMENTS.md checkbox drift** — auto-flip `[x]` from SUMMARY frontmatter `requirements-completed:`; reject SUMMARYs missing it at plan-check time.
- **R-3 Planning-time grep miscounts** — every SC grep MUST be anchored (`grep -cE '^[[:space:]]*\[HttpPost'`, not `grep -c HttpPost`); plan-checker validates anchoring.
- **R-4 Cross-AI plan review catches what Claude misses** — every v1.4 plan goes through `/gsd-review` with Codex; no exceptions for "small" plans.
- **R-5 `no-ship-failing-tests`** — Failed:0 mandatory before milestone PR. Roadmapper pre-allocates a `999.x` test-hardening backlog phase before ship.
- **R-6 Formatting paranoia** — no Format Document; no `{ get; init; }` → `{ get; }`; no inline `[Attribute]`; no raw-string re-indent; touch only lines that need touching. Codex reminded in every CONTEXT.md.
- **R-7 HANDOFF.json / origin staleness on resume** — every session resume `git fetch` + compare `HEAD` vs `origin/<branch>` BEFORE reading planning artifacts. Critical for v1.4's long-running harvest phases that span sessions.

## 7. Open Decisions Needed at Plan Time

| Decision | Blocks | Defer? |
|----------|--------|--------|
| **Gemini unblock path: split-message (Path A) vs direct API (Path B)** | Cluster D / Phase 3 | NO — `/gsd-discuss-phase` at Phase 3; recommend Path A (preserves Phase 999.2 D-08; no new key, no spend exposure) |
| **YouTube transcript provider: YoutubeExplode direct vs paid proxy** | Phase 6 | NO at design time — interface MUST support both day 1; default = YoutubeExplode direct; flip via `DECKFLOW_YOUTUBE_TRANSCRIPT_PROVIDER` env var post-Render UAT |
| **Admin table responsive strategy per-table: overflow-x vs card-stack** | Phase 4 | NO — decide per-table during plan; Analytics + HarvestRunStore → overflow-x; Feedback list + Sources list → card-stack candidates |
| **Content KB feature flag default** | Phase 5+ | YES — defer to first admin UAT; `content_kb_enabled=false` until verified end-to-end |
| **Tag inference vocabulary exact enum values** | Phase 6 (LLM tagging) | NO — derive at plan time; use `static class ContentTagVocabulary` (controlled vocab in code, not DB) |
| **Render Dockerfile ffmpeg install** | Phase 6 (Whisper chunking) | NO — verify at Phase 5/6 start; if missing AND podcasts >25MB → `RUN apt-get install -y ffmpeg` required |
| **Whisper monthly cap initial $ value** | Phase 5 + 7 | NO — set `DECKFLOW_WHISPER_MONTHLY_CAP_USD=15.00` per STACK.md cost model (expected ~$13.32 + 12% headroom) |
| **Razor `.cshtml`-generated CS1591 handling post-NoWarn-strip** | Phase 9 | NO — Phase 9 plan verifies via `dotnet build -warnaserror:CS1591` from clean obj/; scoped 1591 retention for generated Razor partials only |
| **Whisper-vs-LLM cap separation (single ledger or two)** | Phase 5 schema | NO — design `content_spend_ledger` with `provider` + `kind` columns day 1; per-provider caps are future drop-in |

## 8. Watch Out For (Top 3 Worst-Case Failures)

The roadmapper MUST price these into phase Success Criteria so they are not discovered mid-execution:

1. **P1 — Building Phase 5/6 around `Google.Apis.YouTube.v3` for captions.** Every cEDH channel returns 403. Pipeline is dead before Whisper fallback rate signals anything. **SC for Phase 6 MUST be:** "Successfully fetch captions for 5 real third-party MTG channels (MTGGoldfish + Command Zone + EDHRECast + Tolarian + Playing With Power) via `IYouTubeTranscriptFetcher` from deployed Render env." NOT "via YouTube Data API."

2. **P2 — YouTube IP-blocks the Render egress pool silently.** WSL dev sees green; production sees `whisper_fallback_ratio=100%`; $15 monthly cap blown on day 1 because every "free" caption fell back to paid Whisper. **SC for Phase 6 MUST include:** "Pre-ship UAT: harvest 5 videos from deployed Render env; inspect `transcript_source` distribution; if any unexpected `whisper-fallback`, debug before ship. Proxy abstraction in `IYouTubeTranscriptFetcher` interface from day 1."

3. **P3 — Admin double-click bypasses the Whisper cap.** Two browser tabs, two harvest dispatches, both pre-flight checks read same pre-spend value, both proceed; monthly Whisper budget 2× over. Cost is real money. **SC for Phase 7 MUST be:** "Concurrent test: 5 parallel `POST /Admin/ContentHarvest/Trigger` against stub Whisper client; assert ≤1 harvest run row created AND ≤N seconds billed (N = cap). Postgres `pg_try_advisory_lock` per `YYYY-MM` key acquired BEFORE any Whisper call. Hard `DECKFLOW_WHISPER_KILL_SWITCH=true` env var evaluated first."

## Confidence Assessment

| Area | Level | Notes |
|------|-------|-------|
| Reuse of existing DeckFlow patterns | HIGH | Verified at HEAD 65f2fe4; HarvestRunStore + IFeatureFlagCache + RestSharp+Polly directly mirrored |
| Stack additions (versions + integration seams) | HIGH | NuGet versions verified 2026-05; `HttpClientPipelineTransport` verified against OpenAI .NET docs |
| Feature scope boundaries (anti-features deferred to v1.5) | HIGH | PROJECT.md explicit; FEATURES.md anti-feature table aligned |
| YouTube transcript path (YoutubeExplode vs Google.Apis) | HIGH | Issue Tracker 241669016 + 403 limitation confirmed |
| Gemini unblock path choice | MEDIUM | UAT-dependent; both paths sound architecturally |
| Whisper $/min pricing | MEDIUM | $0.006/min verified vendor 2026-05 but pricing has shifted twice in 2025 |
| Render IP-block risk (P2) | MEDIUM | Documented in `youtube-transcript-api` Issue #511; not directly tested from current Render IP |
| Recurring v1.3 process patterns | HIGH | Drawn from RETROSPECTIVE.md observed-incidents |
