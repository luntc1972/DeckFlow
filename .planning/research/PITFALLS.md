# Domain Pitfalls — DeckFlow v1.4 Content Knowledge Base + Admin Mobile + Backlog

**Domain:** ASP.NET 10 / Razor / Postgres on Render — adding YouTube + Whisper + LLM ingestion to a live app
**Researched:** 2026-05-23
**Overall confidence:** HIGH for external-API quirks (verified against official docs), HIGH for Render/repo constraints (verified against codebase), MEDIUM for "recurring v1.3 patterns" (drawn from RETROSPECTIVE.md only)

Pitfalls ordered by **likelihood × impact** (highest first). Each is calibrated to **THIS** system, not a generic warning. Phase numbering uses placeholder "Phase 16/17/18/19/20" — exact numbering belongs to the roadmapper. Where v1.4 uses 999.x backlog phases for backlog cleanup, the assignment notes that.

---

## Pitfall 1: YouTube Data API `captions.download` returns 403 for videos you don't own (CATASTROPHIC for ingestion design)

### Failure Mode
The official `captions.download` endpoint returns **HTTP 403 Forbidden** for **virtually every video the DeckFlow admin does not own** — even for public videos with public captions. The MTG content creators DeckFlow wants to ingest (MTGGoldfish, The Command Zone, Game Knights, EDHRECast, Tolarian Community College, Playing With Power, etc.) are all third-party channels. **Zero coverage** via the official API.

### Root Cause
Google's caption-download docs explicitly state: *"The caption download method requires the user to have permission to edit the video."* Even with valid OAuth, captions on videos the requestor doesn't own return 403 with `forbidden` reason. This restriction has been in place for years and is not a quota or API-key fix — it's a policy.

### Prevention
- **Pivot the design before Phase 1.** Do NOT plan around `captions.download`. The viable paths are:
  1. **Scrape the timed-text endpoint directly** (the same URL the YouTube player consumes — `https://www.youtube.com/api/timedtext?v=...&lang=en`). No API key needed. Subject to IP blocking from cloud egress.
  2. **Port the `youtube-transcript-api` (Python) approach to C#** — fetches the watch-page HTML, extracts the player config JSON, then hits the timed-text URL. Same IP-blocking risk.
  3. **Use a paid third-party (Supadata, Apify YouTube Transcript Scraper, ScrapingBee)** — they pay for residential proxy pools so you don't have to.
- **Document the design decision** in the spec: "We are NOT using Google's official caption API; we are scraping the player timed-text endpoint" so future maintainers don't waste a sprint trying to make the official API work.
- **Build a single `IYouTubeTranscriptFetcher` interface** with at least two implementations from day 1: `PlayerTimedTextFetcher` (default) + `SupadataFetcher` (paid fallback). Toggle via env var `DECKFLOW_YOUTUBE_TRANSCRIPT_PROVIDER`.

### Detection
- **Test against 5 real MTG channels** (NOT a video Chris owns) before declaring transcript-fetch "done." Sample list: MTGGoldfish "Budget Magic" / The Command Zone / EDHRECast / Tolarian / Playing With Power. If `captions.download` 403s on 5/5, the design is fundamentally broken.
- **Log structured field `transcript_source: {player-timedtext|supadata|whisper-fallback}`** on every successful fetch so operator can see distribution.
- **CI smoke test in Phase 1:** stub the watch-page HTML and assert `PlayerTimedTextFetcher` extracts the timed-text URL from current YouTube markup. Re-run periodically — YouTube changes this surface annually.

### Phase Owner
**Phase 16 (Content KB Phase 1 — Ingestion Foundation)** — first plan after schema. Before any service code is written, prove `IYouTubeTranscriptFetcher` returns captions for a video Chris does NOT own. If it doesn't, no other Phase 16 work matters.

---

## Pitfall 2: YouTube IP blocking from Render egress (cloud provider blacklist)

### Failure Mode
The `PlayerTimedTextFetcher` (and any scraping path) works fine from Chris's WSL dev box, then **silently 429/blocks on Render**. YouTube has aggressively blacklisted egress IPs from AWS / GCP / Azure / Render / Fly. The transcript fetcher returns 429 or a CAPTCHA HTML page (parsed as garbage), causing Whisper-fallback to fire on **every video** — burning Whisper budget for free transcripts that should have been "free."

### Root Cause
YouTube's `youtube-transcript-api` GitHub issue tracker has explicit confirmation: cloud-provider IPs are flagged. Render's outbound IP pool is shared and well-known. Once one tenant abuses, the whole pool gets a soft-block.

### Prevention
- **Render does NOT serve transcript fetches.** Either:
  1. Route YouTube fetches through a residential-proxy service (Webshare, Bright Data, Smartproxy) on a per-request basis — add proxy support to `IHttpClientFactory` named client `youtube-timedtext`. Cost: ~$1-3/mo at low volume.
  2. Use a paid managed transcript API (Supadata $0/mo for low tier, Apify pay-per-run) that pays for the proxy infra. Returns clean JSON; no scraping in our codebase.
- **Treat IP block as a first-class state in the Whisper budget calculator.** If `transcript_source: whisper-fallback` jumps from <10% to >50% in a 24h window, that's an IP-block signal — alert, don't silently bleed Whisper budget.
- **Apply the v1.0 Phase 5 lesson** (`feedback_http_resilience_pattern.md`): when egressing from Render to anything that fingerprints clients, browser-shaped headers + `AutomaticDecompression` are required. Reuse the `ScryfallTaggerHttpClient` Cloudflare-BIC pattern.

### Detection
- **Daily metric:** `whisper_fallback_ratio = whisper_used / total_transcripts_fetched_24h`. Alert at >25%.
- **Structured log on YouTube 429/CAPTCHA HTML:** `Warning("youtube-timedtext-blocked", source: rendered_outbound_ip, video_id: ...)`. Single occurrence is noise; 10+ in an hour is a block.
- **Pre-ship UAT:** harvest 5 videos from **deployed Render env**, not from WSL. Inspect the harvest log for `transcript_source` distribution. If any unexpected `whisper-fallback`, debug before ship.

### Phase Owner
**Phase 16 (Content KB Phase 1)** — design the proxy abstraction into `IYouTubeTranscriptFetcher` from day 1, even if v1.4 ships with a single direct provider. Adding proxy support after the fact requires changing the HTTP-client factory wiring (touches `Program.cs` named-client registrations).

---

## Pitfall 3: Whisper monthly cost cap race condition (two admin clicks bypass the cap)

### Failure Mode
Admin opens two browser tabs of the harvest page, clicks "Run Harvest" on both within 1 second. Both reads of `whisper_spend_this_month` see the same pre-click value, both decide the cap is not yet hit, both kick off harvest jobs spending up to 2× the cap. **Monthly Whisper budget blown** on a single admin double-click.

### Root Cause
The naive cap check pattern is read-decide-write across multiple statements (TOCTOU). Without a transaction or a serialized control gate, concurrent reads from `ContentHarvestRunStore` race. v1.0 had the analogous bug with the admin brute-force throttle (BUG-02) before the Postgres-backed lazy-expiry pattern was introduced.

### Prevention
- **Single-row Postgres advisory lock per month:** `pg_try_advisory_lock(hashtext('whisper-cap-' || to_char(now(), 'YYYY-MM')))`. Acquire at the start of every harvest dispatch; release at end. Concurrent dispatch returns "already running" instead of stacking.
- **Pre-flight transactional check:** wrap "compute current spend → compare to cap → insert harvest-run row" in a single `BEGIN ... COMMIT` with `SERIALIZABLE` isolation. Either both succeed-then-cap-hit (one rolls back), or one sees a stale value and conflicts on commit.
- **Estimate spend BEFORE Whisper call, not after.** Use audio duration metadata (from YouTube `videos.list` `contentDetails.duration` or podcast RSS `itunes:duration`) × $0.006/min to forecast spend. If forecast + ytd > cap, abort BEFORE the API call. Confirms post-call against actual billing.
- **Hard kill-switch env var:** `DECKFLOW_WHISPER_KILL_SWITCH=true` → no Whisper calls regardless of cap state. Always-on, evaluated as the very first check.
- **NEVER trust the admin UI's `?confirmed=true` query param alone.** Re-check server-side.
- **TZ ambiguity on monthly rollover:** pick ONE timezone for "month" boundary and document it. Render servers default to UTC; admin lives in MDT. A 6pm-MDT submission on the last day of the month is the 1st-UTC. Pick UTC (server-native) and display it as "UTC monthly cap" in the admin UI.

### Detection
- **Test:** spawn 5 concurrent `POST /Admin/Content/Harvest/Start` requests in xUnit (`Task.WhenAll` against a stub Whisper client that records its calls). Assert ≤ 1 harvest run row is created AND ≤ N seconds of audio are billed (where N = configured budget).
- **Log every Whisper API call** with `correlation_id`, `harvest_run_id`, `minutes_billed`, `running_total`. Audit log query: `SUM(minutes_billed) GROUP BY DATE_TRUNC('month', billed_at AT TIME ZONE 'UTC')` must equal admin-UI displayed spend exactly.
- **Pre-flight forecast vs. post-call actual divergence alert:** if `actual_minutes / forecast_minutes > 1.2`, surface in admin UI — duration metadata was wrong, recalibrate.

### Phase Owner
**Phase 16 (Content KB Phase 1 — Ingestion Foundation)** — kill switch + pre-flight forecast in initial dispatcher.
**Phase 17 (Content KB Phase 1 — Cost & Cap UI)** — advisory lock + serializable txn before any "Harvest Now" button ships.

---

## Pitfall 4: LLM JSON-mode parse failure on summarization (Anthropic + Gemini are NOT strict; OpenAI is)

### Failure Mode
LLM emits "Here's the summary:" prefix before the JSON block, or trailing commentary, or invalid Unicode in tags. `JsonSerializer.Deserialize<VideoSummary>(response)` throws `JsonReaderException`. The admin harvest job dies mid-batch, leaving partial transcripts but no summaries — but **the Whisper cost was already incurred.** Worse, the next "Resume Harvest" click re-Whispers everything because the harvest-run row says "failed" and there's no "transcripts complete, summaries pending" intermediate state.

### Root Cause
- **OpenAI Structured Outputs** (with `strict: true` JSON schema, not just `json_object` mode): <0.1% failure rate. Refusals are the dominant failure.
- **Anthropic Claude:** no schema-enforced JSON mode. "JSON mode" is a system-prompt convention. Empirical 5-10% malformed-JSON rate at production volume. Often emits prose preamble.
- **Gemini:** `responseSchema` field constrains output but still allows wrapper text in some model versions; failure rate similar to Anthropic's.

The DeckFlow team has already been bitten by this exact class of bug — v1.3 Phase 999.4 shipped a `JsonReaderException` → user-facing "wait for AI to finish generating" message for the deck-analysis paste-back path. Same pattern recurs server-side for harvest summarization.

### Prevention
- **Use OpenAI Structured Outputs** for summarization in v1.4. Free up Claude/Gemini for deck analysis where the user controls paste-back. Hard-pin via `Microsoft.Extensions.AI` or direct `openai` SDK calls with `response_format: { type: "json_schema", json_schema: {...}, strict: true }`.
- **Stage harvest pipeline with intermediate persistence:**
  1. Fetch transcript → persist `transcripts.status = 'fetched'`
  2. Whisper if needed → update `transcripts.status = 'transcribed'` (cost recorded here, atomic)
  3. Summarize → persist `summaries.status = 'summarized'`
  4. Tag → persist `content_tags.status = 'tagged'`
  Resume picks up from the first non-complete stage. **Whisper cost is NEVER re-incurred** on resume.
- **Validate response shape with FluentValidation or a manual `ValidateAndExtract` helper** before persisting. Reject summaries with `Title.Length > 200`, `Tags.Count > 20`, etc.
- **Single-retry with "JSON only, no preamble" re-prompt** on parse failure. Log the raw response on second failure, mark video summary as `'failed-json-parse'`, MOVE ON. Do not block the batch.
- **Hallucinated archetype/format tag detection:** post-process tags through a `KnownTagSet` allowlist (sourced from existing DeckFlow taxonomy `Models/Knowledge/`). Tags outside the set → discarded with a log entry. NEVER silently insert LLM-invented categories into the canonical taxonomy.

### Detection
- **Counter:** `llm_parse_failures_total` Serilog enrichment + alert at >1% of summarization calls.
- **Counter:** `llm_hallucinated_tags_discarded_total` — track LLM honesty. >10% = re-engineer the prompt.
- **Test:** unit test on `ParseAndValidateSummary` with 10 captured "weird real LLM responses" (preamble, trailing text, escaped Unicode, doubled quotes). All must produce either a clean `VideoSummary` or a clean `ParseFailureReason`, never a thrown exception escaping the service layer.

### Phase Owner
**Phase 17 (Content KB Phase 1 — LLM Summarization)** — staged-pipeline persistence + Structured Outputs + tag allowlist all land together. **Defer Claude/Gemini summarization to v1.5.**

---

## Pitfall 5: AiPlatform value-object regression — adding "GeminiDirect" as a string instead of an `AiPlatform` variant

### Failure Mode
Gemini paste-limit workaround adds a "Gemini Direct API" target. Implementer adds it as a string literal `"GeminiDirect"` in 5 places (form value, controller switch, prompt builder dispatch, view label, zip artifact field). The v1.3 Phase 15 `AiPlatform` sealed-record value object (`OCP 8/10`) is bypassed because the implementer is unfamiliar with the registry pattern. OCP regresses to 3/10. Future 5th AI surface (Mistral? Llama?) requires another N-place edit.

### Root Cause
Phase 15 just shipped 5 days ago (2026-05-18). The pattern is new. v1.4 implementer (Codex) may not be deeply aware. Documentation lives in spec, not in self-explanatory call sites.

### Prevention
- **Codify in the Phase 19 (Gemini Unblock) CONTEXT.md** the explicit instruction: "Add to `AiPlatformRegistry.cs` and create a `GeminiDirectAnalysisBuilder.cs` variant. Do NOT add string literals." Reference the Phase 15 PR by commit SHA.
- **Code review gate:** plan-checker must `grep -r '"GeminiDirect"' DeckFlow.Web/` — any match outside `AiPlatform.cs` is a BLOCKER.
- **Existing test `AiPlatformExtensionTests` (7 facts, "4th-platform OCP proof")** is the regression guard. Add a `GeminiDirect_RegisteredAsValueObject` test case in the same fashion before adding any string.
- **`ResultContractTests`** (added in 999.6) already enforces variant-by-variant divergence. Extend, do not bypass.

### Detection
- **`grep -rE '"(Gemini|Claude|ChatGpt|GeminiDirect|Mistral)"' DeckFlow.Web/ DeckFlow.Core/`** in CI — only matches allowed: `AiPlatform.cs`, `AiPlatformRegistry.cs`, view markup with `@aiPlatform.DisplayName`, and a numbered allowlist in the test project. Anything else fails the build.
- Codex peer-review on the plan must verify the AiPlatform registry change is explicit in the PLAN.md tasks.

### Phase Owner
**Phase 19 (Gemini Paste-Limit Unblock)** — owned by whoever takes Gemini direct-API integration.

---

## Pitfall 6: Render Postgres connection-pool starvation on concurrent admin harvest action

### Failure Mode
Admin opens harvest page (1 connection for `ContentHarvestRunStore.ListAsync`). Clicks "Run Harvest" (acquires connection for run-insert, holds while the batch loops). Each per-video step (transcript persist, summary persist, tag persist) acquires a connection. Default Npgsql pool size = 100. Render's Basic-256MB Postgres tier caps connections far below that (Render's docs: connection limit depends on instance RAM; Basic-256MB is at the low end). At sustained 20+ concurrent connections, pool fills, pool exhausted, **other DeckFlow features (feedback form, category suggestions, brute-force tracker) start throwing `NpgsqlException: The connection pool has been exhausted`**.

### Root Cause
Long-running harvest worker holds connections across `await` points (network calls to YouTube + Whisper + LLM). Each `await` keeps the connection checked out from the pool. v1.3 Phase 999.6 (`F-PROD-CONTRACT IHarvestRunStore.GetByIdAsync` fix) was an analogous bug class — production-bug surfaced inside test cleanup.

### Prevention
- **NEVER hold a Postgres connection across a network call.** Pattern:
  ```csharp
  // BAD
  using var conn = ...;
  var run = await conn.QueryAsync(...);
  var transcript = await _youtube.FetchAsync(...); // conn still checked out
  await conn.ExecuteAsync(...);

  // GOOD
  HarvestRunRow run;
  using (var conn = ...) { run = await conn.QueryAsync(...); }
  var transcript = await _youtube.FetchAsync(...);
  using (var conn = ...) { await conn.ExecuteAsync(...); }
  ```
- **Cap `Maximum Pool Size` explicitly in the connection string** to ~10-15 (well below Render Basic-256MB's ceiling) so DeckFlow fails fast and loudly in dev rather than silently consuming Render's whole budget in prod. Better to see `pool exhausted` in WSL than in prod.
- **Single hosted harvest worker, sequential video processing.** v1.4 spec is "manual admin-triggered harvest (no scheduler)" — keep the worker single-threaded per-batch. Concurrency yields no UX win and breaks the pool. Reuse the `ArchidektCacheJobService` singleton-hosted pattern.
- **Test the Whisper-stuck case:** if Whisper API hangs for 9 minutes (real failure mode — see Pitfall 7), don't hold a connection that whole time. Acquire-release around the API call boundary.

### Detection
- **Serilog enrichment:** log `npgsql_pool_active_connections` periodically via a `NpgsqlDataSource` event handler. Graph trend over a harvest run. Should stay flat near 1-3, not climb.
- **Smoke test in Phase 16:** start a harvest of 5 videos and concurrently hit `/feedback` 20 times. All `/feedback` POSTs must succeed (no pool exhaustion).
- **Stress test on Render staging (if one exists) before production deploy:** trigger a 50-video harvest and watch Render dashboard's "Connections" metric.

### Phase Owner
**Phase 16 (Content KB Phase 1 — Ingestion Foundation)** — connection-handling pattern established in the first harvest worker implementation. Plan-checker enforces "no `await` between connection acquire and release across HTTP boundary" rule.

---

## Pitfall 7: Whisper API timeout > 10min on long podcasts (silent partial transcription)

### Failure Mode
Admin queues a 90-minute MTG podcast (Command Zone full episodes are 120-180min). Whisper API call exceeds its server-side 10-minute processing window OR DeckFlow's `HttpClient.Timeout` (default 100s) fires first. Two failure modes:
1. **HttpClient timeout:** `TaskCanceledException` thrown server-side. Whisper job may still be running and **billed** on OpenAI's side. DeckFlow has no transcript and no record of the cost.
2. **Whisper returns truncated transcript with HTTP 200:** the audio was cut at 25MB upload limit, so the transcript only covers the first ~25min of audio but looks superficially valid. Summary downstream is misleading.

File-size limit: **25MB hard cap** (26,214,400 bytes), independent of duration. A 2-hour mono 64kbps MP3 = ~57MB → fails. Same podcast at 32kbps = ~28MB → still fails.

### Root Cause
Whisper-1 has no chunking API. The 25MB limit is enforced server-side. OpenAI explicitly does NOT support resumable uploads or chunked transcription as a single-call primitive.

### Prevention
- **Chunk audio client-side before upload.** Use a server-side ffmpeg invocation (already part of the `.NET 10 aspnet:10.0` Docker base? — **no, not included; add `RUN apt-get install -y ffmpeg` to Dockerfile**) to split podcasts into ≤10-minute, ≤20MB segments, transcribe each, concatenate transcripts with offset timestamps.
- **Set `HttpClient.Timeout = TimeSpan.FromMinutes(15)`** on the `whisper-api` named client. Higher than Whisper's processing window so we get a clean error, not our own timeout firing first. Wrap in Polly timeout strategy at 12 minutes.
- **Pre-flight size check:** download the audio file to /data tmp, measure size + duration via ffprobe, branch on `if (sizeBytes > 24_000_000) ChunkAndTranscribe() else SingleShot()`. NEVER attempt single-shot on a >24MB file.
- **Cost reconciliation:** persist Whisper API request metadata (file size, duration, model) and OpenAI's returned `usage` field if present. Reconcile weekly against OpenAI's billing dashboard export. Surprising delta → there's a hidden timeout-retry billing somewhere.
- **Render 512MB RAM cap consideration:** ffmpeg on a 180MB podcast download to /data is fine (disk-bound, low RAM). Re-encoding in-memory is NOT — must use ffmpeg `-i in.mp3 -c copy -ss ... -t ...` stream-copy, not transcode.
- **No native idempotency-key on Whisper transcriptions endpoint** (verified — OpenAI's Idempotency-Key support is documented for Agentic Commerce, not transcriptions). DeckFlow MUST track its own "in-flight" state in `transcripts.status = 'whisper-pending'` BEFORE calling, so a retry after a network blip can decide whether to call again or wait.

### Detection
- **Counter:** `whisper_request_duration_seconds` histogram. Alert at p99 > 600s.
- **Counter:** `whisper_chunked_files_total` vs `whisper_single_shot_total` — should match the file-size distribution of inputs.
- **Test:** xUnit fact `WhisperPipeline_LongFile_ChunksAndConcatenates` with a synthetic 30MB stub audio file. Assert chunk boundaries align with silence-detected breakpoints, transcript timestamps are offset correctly.
- **Render RAM watch:** monitor `/proc/self/status VmRSS` during harvest. If RAM climbs above 350MB (out of 512MB cap), abort harvest and alert — we're loading audio into memory somewhere we shouldn't.

### Phase Owner
**Phase 16 (Content KB Phase 1 — Ingestion Foundation)** — chunking architecture must land before Phase 17 invokes Whisper at scale. Dockerfile ffmpeg install is a Phase 16 dep (touches `Dockerfile` + render redeploy).

---

## Pitfall 8: Doc-comment NoWarn strip fails the build with ~88 warnings BEFORE backfill lands (sequencing bug)

### Failure Mode
Phase 18 (Doc-Comment Backlog) plan strips `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` from `DeckFlow.Web.csproj` as the first task. Build immediately fails with 88 CS1591 warnings (Web treats warnings as not-errors by default, but in CI/Release the gate is stricter). Codex executor sees red. Decides to revert the strip. Phase 18 churns for hours.

OR: implementer adds doc-comments to 30 of 88 types, NoWarn already stripped, Build emits 58 warnings, "looks like progress" mindset commits the partial state to main. Build is now noisy in CI for everyone for days.

### Root Cause
NoWarn was added specifically to suppress these 88 warnings as a v1.1 deferral (per Phase 14 CONTEXT "Deferred Ideas" — captured in v1.3 audit tech_debt). Stripping the NoWarn flag without first backfilling is order-of-operations wrong.

**Additional Razor-specific quirk:** `.cshtml` files generate compiler-emitted partial classes. CS1591 fires on the *generated* partial when `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is on, NOT on the user-authored partial. You cannot add a doc comment to a file you didn't write. The Roslyn issue tracker has a 9-year-old "we should not emit CS1591 on generated code" complaint that is still open (`dotnet/roslyn#12702`). **Razor views will continue to need pragma suppression even after the 88 user-authored types are documented.**

### Prevention
- **Plan sequencing is fixed:** Backfill ALL 88 doc-comments FIRST (with NoWarn still in place — warnings hidden but doc-comments still emitted to XML). Last commit of the phase strips NoWarn AND adds a more-targeted `<NoWarn>$(NoWarn);1591</NoWarn>` in a `Condition` that scopes only to generated Razor `obj/` files OR uses `<GeneratedCodeAttribute>`-aware suppression. Test build is clean.
- **Per-file partial-warnings handling pattern:** if a few legacy types are genuinely undocumentable (e.g., test-seam internal records that aren't supposed to surface as public XML), add file-scoped `#pragma warning disable CS1591` blocks, not project-wide suppression.
- **Auditable grep gate** before NoWarn-strip task: `grep -L '<summary>' $(find DeckFlow.Web -name '*.cs' -not -path 'obj/*' | xargs grep -l '^public ')` must return empty (every public-type file has a `<summary>`).
- **Razor-generated CS1591 specifically:** keep `1591` in NoWarn IF the Razor compile target emits it. Verify with `dotnet build -warnaserror:CS1591 -p:GenerateDocumentationFile=true` from a clean obj/. Test what fires.

### Detection
- **CI gate:** post-Phase 18, `dotnet build -warnaserror` (or at minimum `-warnaserror:CS1591;CS1573;CS1587`) is required to pass.
- **Pre-commit grep:** for every PR touching `*.cs` files, "public type without doc-comment in same file" check.
- **AuditBundle output review:** before Phase 18 close, count `<summary>` occurrences ≥ count `^public (sealed )?(class|record|interface)` occurrences in DeckFlow.Web.

### Phase Owner
**Phase 18 (Doc-Comment NoWarn Backlog)** — owned by Phase 18 itself. Phase 18 SC1 = "all 88 v1.1-era types documented before NoWarn touched." SC-final = "NoWarn stripped AND `dotnet build -warnaserror:CS1591` passes." Two SCs, not one.

---

## Pitfall 9: WDG-04 modal focus-trap hand-rolled vs `<dialog>` (accessibility + maintenance trap)

### Failure Mode
Implementer hand-rolls a focus-trap JS module: `keydown` listener, `tab` index walker, escape-key, focus-restore. Two months later: a Razor view nests another tabbable widget (`df-typeahead`, `df-select` combobox) inside the modal. The hand-rolled walker doesn't know about these custom-element focusable descendants. Tab leaks to the underlying page. Screen reader reads the page behind the modal. WCAG 2.4.3 fail.

Alternative failure: the hand-rolled focus trap clashes with the existing keyboard shortcuts in `wwwroot/ts/` (e.g., the WDG df-typeahead `Escape` handler) — Escape on the typeahead bubbles to the modal which closes; user loses the typeahead input mid-form.

### Root Cause
The native HTML `<dialog>` element (Baseline 2022; supported in all modern browsers) provides focus-trap + Escape + aria-modal + backdrop FOR FREE. Hand-rolling it is a 2026 anti-pattern.

### Prevention
- **Use `<dialog>` element with `showModal()`.** Style the `::backdrop` pseudo-element via `site-common.css` (per the v1.3-pinned layout-CSS rule). Native focus-trap, native Escape, native `aria-modal`.
- **Test inside a Razor view that contains a `df-typeahead` and a `df-select` combobox** — the actual `AdminFeedback/Detail.cshtml` already has the form; reproduce it. Verify Tab cycles through native + custom-element controls without leaking.
- **Escape key handling:** delegate to the dialog. If a child component (df-typeahead) calls `event.stopPropagation()` on Escape (to close its own popover, not the dialog), document that in `site-common.css` comments and TypeScript-component contracts. Verify both interactions.
- **`inert` attribute** on the rest of the page when the dialog opens — browsers respect this; assistive tech sees only the dialog. Polyfill not required for current browser-baseline.

### Detection
- **Manual a11y test in HUMAN-UAT.md for Phase 19/20 (WDG-04 modal):**
  1. Open Confirm modal with keyboard (Enter on trigger button).
  2. Tab through every focusable element — must cycle within modal only.
  3. Shift+Tab from first element — must cycle to last in modal.
  4. Escape — must close modal AND restore focus to trigger button.
  5. NVDA / VoiceOver test — modal content read; page behind silent.
- **Automated test in `wwwroot/ts/`:** Playwright (if added) or hand-rolled `document.activeElement` assertion after dispatch of Tab events.

### Phase Owner
**Phase 19 or 20 (WDG-04 Modal Replacement)** — pair with admin-mobile sweep so the same shell touches modal + admin a11y together.

---

## Pitfall 10: Admin mobile sweep regresses 22 guild themes (CSS bleed)

### Failure Mode
Implementer adds responsive rules to `wwwroot/css/admin.css` for "admin shell only" — but a selector like `table { display: block; overflow-x: auto; }` or `.btn { width: 100%; }` is too broad. The same `.btn` class is used across 22 guild-themed CSS files for site-wide buttons. Admin sweep ships, mobile users complain that the homepage CTA is now full-width and wrapping ugly.

OR: implementer adds the layout rule to `site-common.css` (the right file per project rules) but doesn't think about cascade order with 22 theme files. Theme files load AFTER site-common, override unexpected properties.

### Root Cause
DeckFlow's CSS architecture is unusual: 22 guild themes are FULL CSS FORKS (not overrides). `site-common.css` holds cross-cutting layout. `site.css` (legacy default) overrides per page. This invariant is project-pinned (CLAUDE.md) but easy to forget when adding new rules.

**Table-specific note** (from Adrian Roselli + Lullabot research): horizontal-scroll tables MUST have `tabindex="0"` on the scroll container so keyboard users can pan. Card-stack pattern loses cross-row comparison utility (worth it for sparse data, bad for the admin Analytics tables which are inherently comparative). Pick PER-TABLE, not project-wide.

### Prevention
- **Scope every new admin selector with `.admin-shell` parent class** (or `body[data-area="admin"]` if that's how admin pages distinguish themselves; verify in `_AdminLayout.cshtml`). Admin pages are gated by `/Admin/*` route + `BasicAuthMiddleware`, so the parent-class scoping is naturally aligned.
- **Pre-commit grep:** for every PR touching admin CSS, `grep -rE '^[^./.]' admin.css` for any unscoped element selectors (`table`, `button`, `input` without `.admin-shell` prefix) — those are BLOCKERs.
- **Visual regression:** page-screenshot the homepage in 3-4 guild themes (Rakdos, Azorius, Boros, Selesnya) at mobile breakpoint BEFORE the admin sweep, and AFTER. Diff. Any pixel change on non-admin pages is a leak.
- **Use CSS layer cascade:** `@layer admin { ... }` so admin rules are always lower-specificity than theme overrides on the public pages. Modern browsers support `@layer`; baseline since 2022.
- **Table-strategy decision matrix:** per admin table, decide horizontal-scroll vs card-stack and document the rationale in the view's comment header. Analytics table → horizontal-scroll (comparison matters). Feedback list → card-stack (scanning, not comparing).

### Detection
- **Manual check:** in Phase 20 HUMAN-UAT, open `/` and `/sync` in mobile viewport on **two non-default themes** (e.g., Rakdos, Gruul). Photo evidence.
- **Test:** add `AdminCssSelectorScopeTests` that parses `admin.css` (or `site-common.css` admin-related rules) with a simple CSS-AST and asserts every top-level selector starts with `.admin-shell` or `body[data-area="admin"]`. Reject unscoped element selectors.

### Phase Owner
**Phase 20 (Admin Mobile Responsive Sweep)** — owned by Phase 20.

---

## Pitfall 11: Admin POST endpoint forgets `[ValidateAntiForgeryToken]` (SameOriginRequestValidator covers /api/* only)

### Failure Mode
New `/Admin/Content/Sources/Create` POST handler ships without `[ValidateAntiForgeryToken]`. `SameOriginRequestValidator` is wired into API endpoints (verified: 2 hits in `AdminAnalyticsController.cs:86`, `AdminHarvestController.cs:100`), not into admin Razor controllers (those use the MVC anti-forgery filter). A CSRF attack from an attacker site can submit harvest configs as a logged-in admin.

### Root Cause
**Two separate CSRF mechanisms exist in DeckFlow:**
- `/api/*` endpoints → `SameOriginRequestValidator.IsValid(Request)` check inside the action body
- `/Admin/*` Razor POST endpoints → `[ValidateAntiForgeryToken]` filter attribute

It is easy to assume the Origin check covers admin too. It does NOT. Existing admin POSTs (verified) all carry `[ValidateAntiForgeryToken]` (`AdminFlagsController.cs:71`, `AdminFeedbackController.cs:69`, multiple in `AdminHarvestController.cs:128/143/171/254/271`) but the discipline is per-action, not enforced globally. A new controller missing the attribute is invisible to existing automation.

### Prevention
- **Global filter pattern:** in `Program.cs`, register `services.AddControllersWithViews(options => options.Filters.Add<AutoValidateAntiforgeryTokenAttribute>())`. This requires every POST to have a token automatically. Existing API controllers (which use SameOrigin) should be exempted via `[IgnoreAntiforgeryToken]` annotation, **explicitly opting OUT** per controller. Inverts the safety default.
- If global filter not adopted, **plan-checker grep gate:** `grep -L 'ValidateAntiForgeryToken' DeckFlow.Web/Controllers/Admin/*.cs` must return empty.
- **Razor `_AdminLayout.cshtml` ships an `@Html.AntiForgeryToken()` already?** Verify; if not, add. New forms inherit it.

### Detection
- **CI grep:** every file in `Controllers/Admin/` matching `^\s+\[HttpPost` must be within 3 lines of `[ValidateAntiForgeryToken]`.
- **Integration test (new):** POST to every admin endpoint WITHOUT a CSRF token; assert 400. Use route discovery, not a hardcoded list, so new endpoints are auto-covered.

### Phase Owner
**Phase 16 (Content KB Phase 1 — Ingestion Foundation)** — admin content-source CRUD ships in Phase 16. Anti-forgery discipline established at the same time. Reuse the existing admin controller pattern (verified working) rather than inventing new.

---

## Pitfall 12: New Postgres tables collide with v1.1 HarvestRunStore schema OR migration runs out of order

### Failure Mode
v1.4 implementer names a new table `harvest_runs` (because v1.1 already had one for Archidekt cache jobs — verified at `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs:437,456`). Schema collision. Worse: the new code's `EnsureSchemaAsync` runs BEFORE the v1.1 schema runs (DI order is non-deterministic for hosted services), creating a `harvest_runs` table that the v1.1 store then partially augments via `CREATE TABLE IF NOT EXISTS` (which silently does NOTHING if the table exists), leaving v1.1 columns missing.

### Root Cause
- DeckFlow uses `EnsureSchemaAsync` per-store, called on first use, NOT a centralized migration tool. There is no Migration Run Order Authority.
- `CREATE TABLE IF NOT EXISTS` is silently idempotent — first-create wins; subsequent creates with different columns are silently ignored. **No error surfaces.**
- The pattern works fine for orthogonal stores; it breaks when two stores think they own the same table name.

### Prevention
- **Namespace new tables explicitly:** `content_sources`, `content_videos`, `content_transcripts`, `content_summaries`, `content_clips`, `content_tags`, `content_harvest_runs`. The `content_` prefix is the namespace. Never reuse `harvest_runs`.
- **Create `ContentHarvestRunStore` as a separate class** from `HarvestRunStore`. Even if the schema feels duplicative, sharing it creates coupling between Archidekt harvest (v1.1) and content harvest (v1.4) lifecycles — different cadence, different cost model, different operator UI. Per the SRP, two stores.
- **Migration ordering:** centralize schema EnsureAsync calls in a `DatabaseStartupBootstrapper` hosted service that runs FIRST (`HostedService` order in DI), calls EnsureSchema on every store in a deterministic order, then unblocks the rest of the app. Reuse the `Program.cs:188-208` startup-DB-validation block as the hook.
- **Add a per-store schema version row** (`content_schema_version` table with `(store_name, version, applied_at)` rows). EnsureSchemaAsync compares its expected version against DB; fails fast on mismatch. Prevents the "CREATE TABLE IF NOT EXISTS silently noops" trap.
- **Dual-dialect compatibility:** every new schema MUST be tested on BOTH SQLite (local dev — `Microsoft.Data.Sqlite 10.0.0`) AND Postgres (prod — `Npgsql 10.0.0`). Postgres-specific features (advisory locks for Pitfall 3, JSONB columns, partial indexes) need a fallback path in the `SqliteRelationalDialect` implementation OR a dialect-gated codepath.

### Detection
- **Test:** `EnsureSchemaAsync_CalledTwiceWithDifferentVersions_FailsLoudly` — set up a v1 schema, attempt v2 EnsureSchema, assert exception or migration log.
- **Manual Postgres check:** before merging Phase 16, query Render Postgres `\dt content_*` — verify all expected tables exist with expected columns. (Render's `psql` shell works via `render psql`.)
- **Test against both SQLite (local dev) AND Postgres (CI integration):** the existing `[Trait("Category", "Postgres")]` test bucket pattern. Add `EnsureSchema_Sqlite` AND `EnsureSchema_Postgres` parametric tests for every new store.

### Phase Owner
**Phase 16 (Content KB Phase 1 — Ingestion Foundation)** — schema design happens once, tests are mandatory. Roadmapper should make SC explicit: "All v1.4 schema lives under `content_*` prefix; no overlap with v1.1 table names."

---

## Pitfall 13: Gemini paste-limit split-message UX is confusing (worse than Gemini-disabled)

### Failure Mode
Workaround for Gemini paste cap: split the prompt into N parts and tell the user "paste these N messages into Gemini one by one." User pastes part 1, Gemini responds "got it, send the next part." User pastes part 2. Gemini's context is reset because the user refreshed the tab, or Gemini hallucinates a response from part 1 alone. User gets garbage. Files a bug. The split-message UX is **worse than the v1.3 status quo of "Gemini hidden behind flag."**

### Root Cause
Gemini Web UI is a chat surface; it has no first-class "queue-multi-message-as-one-prompt" feature. State persistence between messages depends on the user not navigating away.

### Prevention
- **Skip the split-message approach.** Go direct: integrate **Gemini API** (AI Studio, free tier: 1,500 req/day for 2.5 Flash, 50/day for 2.5 Pro — sufficient for DeckFlow's volume; Pro has 5 RPM cap so plan for Flash by default).
- Gemini API integration **must use `AiPlatform.GeminiDirect` value-object variant** (see Pitfall 5).
- **Error envelope is different from existing AI platforms:** Gemini API returns `{"error": {"code": ..., "message": ..., "status": ...}}` structurally distinct from OpenAI/Anthropic. The existing `UpstreamErrorMessageBuilder` (verified at `CommanderController.cs:103-110`) must add a `BuildGeminiMessage(exception)` overload. Don't hand-write per-page error parsing.
- **Cost surprise:** AI Studio FREE tier is sufficient for DeckFlow's expected volume, but data is used for training (privacy concern noted, but our prompts contain only public deck info — acceptable for v1.4). Vertex AI is the paid alternative if data-privacy ever becomes a concern.
- **AI Studio vs Vertex AI surface differences:** different URLs, different auth (API key vs OAuth/service account), different rate limits. Pick AI Studio for v1.4 (simpler, free tier). If we ever need data-privacy + SLAs, Vertex requires a meaningful re-integration. Document this so v1.5+ planners know the migration scope.
- **Latency:** Gemini 2.5 Flash p50 ~1-2s; the existing AISEL pages await user paste-back so there is NO latency budget for v1.4 (user controls timing). Direct API only matters when DeckFlow programmatically calls Gemini (e.g., if/when summarization moves to Gemini — currently planned for OpenAI Structured Outputs).

### Detection
- **HUMAN-UAT on Phase 19:** real Gemini API key in Render env (`GEMINI_API_KEY` with `sync: false`). Real end-to-end paste-back test on `/deck-analysis`. Verify the AI response flows back through `<result>` extractor.
- **Counter:** `gemini_api_errors_total` by `error.status` — track 429 (rate limit), 400 (malformed prompt), 503 (Google outage). Alert if any sustains.

### Phase Owner
**Phase 19 (Gemini Paste-Limit Unblock)** — owned by Phase 19. Reuse the existing `AiPlatform` registry (Pitfall 5) AND `UpstreamErrorMessageBuilder` (per the v1.3-established service pattern).

---

## Pitfall 14: VSTest broken in WSL — false-confidence build green vs runtime regression

### Failure Mode
Implementer pushes code with a runtime null-ref or DI-resolution bug. `dotnet build` is clean. Pushes to main. CI runs the test suite; one test fails. Operator has to context-switch from local dev to GitHub Actions log review. Slow loop.

### Root Cause
`Testing: VSTest unreliable in WSL` is a project constraint (CLAUDE.md). Local dev cannot run `dotnet test`. Build-clean is necessary, not sufficient. Without CI, no test feedback.

### Prevention
- **Push-and-watch CI** is the established discipline (CLAUDE.md). Reinforce it in v1.4 phase plans: every plan SC must explicitly mention "push, wait for green, then close."
- **For high-confidence local validation:** instead of `dotnet test`, run `dotnet build -warnaserror -c Release` (catches more than debug build) + targeted manual harness scripts (`scripts/run-web.sh` + curl against running dev server).
- **`run + update tests after changes` rule** (memory `feedback_run_and_update_tests`) — build via `/mnt/c/Program Files/dotnet/dotnet.exe` + run test suite + triage failures + update drifted tests in-commit; grep counts alone aren't "done." Honor this through CI even when local-test is broken.

### Detection
- The CI test suite IS the detection. If a v1.4 PR ships without a green CI on the merge SHA, the merge SHA is unverified. `no-ship-failing-tests` rule (established 2026-05-22, applied by Phase 999.6) is the operating gate.

### Phase Owner
**Every phase.** Process discipline, not feature-owned.

---

## Pitfall 15: Public-repo secret leak (Whisper/OpenAI/Anthropic/Gemini keys in commits)

### Failure Mode
Implementer drops an API key into `appsettings.Development.json` "just for local testing." Forgets to revert. Pushes to a public repo. GitHub secret-scanning notifies OpenAI within minutes. OpenAI auto-revokes the key. Local dev is broken; cost was minimal. If the key wasn't auto-revoked, attacker drains the account.

### Root Cause
`luntc1972/DeckFlow` is a public repo. v1.4 introduces FOUR new secrets that don't exist today: `OPENAI_API_KEY` (Whisper + summarization), `GEMINI_API_KEY`, optional `ANTHROPIC_API_KEY` if Claude summarization is added later, and any YouTube transcript service key (Supadata/Webshare/etc.).

### Prevention
- **All keys live in Render env vars with `sync: false`** (matches existing pattern — verified for `FEEDBACK_ADMIN_PASSWORD`).
- **Add to `.gitignore`:** any `appsettings.*.local.json` pattern; any `secrets/` directory.
- **Pre-commit hook OR Gitleaks GitHub Action:** scan diffs for OpenAI key format (`sk-...`), Google key format (`AIza...`), Anthropic format (`sk-ant-...`). Block commit/push.
- **Document in CLAUDE.md `Constraints` section** the four new env vars and the `sync: false` requirement.
- **GitHub secret-scanning push protection** — enable in repo settings (free for public repos).

### Detection
- GitHub secret-scanning alerts → email Chris immediately.
- Gitleaks CI job on every PR.
- Manual audit pre-ship: `git log --all -p | grep -E 'sk-[a-zA-Z0-9]{20,}|AIza[a-zA-Z0-9]{35}'` — must return empty.

### Phase Owner
**Phase 16 (Content KB Phase 1 — Ingestion Foundation)** — first phase introducing API keys. Render env-var setup is a Phase 16 release-prep task. Pre-commit hook is added in Phase 16 too.

---

## Recurring Patterns from v1.3 (v1.4 MUST NOT REPEAT)

These are process pitfalls that bit v1.3 (per `.planning/RETROSPECTIVE.md` "What Was Inefficient" section). Calling them out separately so the v1.4 roadmap can install gates against each.

### R-1: STATE.md arithmetic drift
**v1.3 incident:** `completed_phases: 9 / total_phases: 11` (should be 11/11) and `completed_plans: 66 / total_plans: 46` (mathematically impossible). Shipped uncaught until milestone audit. Phase 999.7-01 + 4 closure commits to reconcile.
**Prevention for v1.4:** STATE.md update is automated on phase close (compute counters, do not trust hand-entered). At minimum: add a CI gate `gsd-sdk verify-state` that asserts `completed_phases ≤ total_phases` AND `completed_plans ≤ total_plans` on every push to a v1.4 branch. Fail loudly.
**Phase Owner:** all v1.4 phases. Roadmapper builds this into phase-close workflow.

### R-2: REQUIREMENTS.md checkbox drift
**v1.3 incident:** Phase 11 closed 2026-05-13. WDG-01..10 checkboxes shipped unchecked. 10 days of audit drift before Phase 999.7-02 flipped them.
**Prevention for v1.4:** at phase-close, auto-flip checkboxes for every REQ-ID listed in `requirements-completed:` SUMMARY frontmatter. If a SUMMARY lists a REQ-ID, the REQUIREMENTS.md checkbox flips to `[x]` programmatically. Reject SUMMARYs missing the frontmatter at plan-checker time.
**Phase Owner:** all v1.4 phases. Roadmapper enforces SUMMARY frontmatter requirement at plan-creation time.

### R-3: Planning-time grep miscounts
**v1.3 incident:** Phase 999.7-04 SC4 stated `grep -c D-11 returns 1` but HEAD had 3 instances. Verification gate format was too coarse. Audit F-01 evidence inventory similarly undercounted 3 → actual 5. Codex review caught both.
**Prevention for v1.4:** every SC that uses grep MUST specify the exact grep command including anchors. Use `grep -cE '^[[:space:]]*\[HttpPost'` not `grep -c HttpPost`. Plan-checker validates SC grep commands are anchored (no bare-word `grep -c X file` patterns).
**Phase Owner:** all v1.4 phases. plan-checker has explicit "anchored-grep validation" rule.

### R-4: Cross-AI plan review catches what Claude's plan-checker misses
**v1.3 incident:** Phase 999.7-01 had 2 BLOCKER issues caught by Codex peer review that Claude's `gsd-plan-checker` missed. Workflow `/gsd-plan-phase → /gsd-review → revise → /gsd-execute-phase` is the established pattern.
**Prevention for v1.4:** every v1.4 plan goes through `/gsd-review` with Codex as reviewer BEFORE execute-phase dispatch. No exceptions for "small" plans — the v1.3 caught-BLOCKER plan was tiny.
**Phase Owner:** all v1.4 plans. Workflow rule already in `~/.claude/CLAUDE.md`; reinforce in PROJECT.md v1.4 milestone section.

### R-5: `no-ship-failing-tests` discipline
**v1.3 incident:** Prior milestones shipped with deferred failures; v1.3 was the first to enforce Failed:0 before merge. Phase 999.6 created specifically to honor this.
**Prevention for v1.4:** rule remains in force. ANY failing test at any milestone-ship attempt blocks merge. The v1.4 milestone adds Whisper + LLM integration code — new failure surface. Roadmapper allocates a `999.x` test-hardening phase by default before milestone-ship, ready to absorb residual failures.
**Phase Owner:** ship gate. Roadmapper plans the test-hardening backlog phase upfront.

### R-6: Auto-format / formatting paranoia
**Project constraint not v1.3-incident, but bears repeating for v1.4** since Codex executor edits many files in a content-pipeline phase. CLAUDE.md is explicit: no Format Document, no `{ get; init; }` → `{ get; }` (System.Text.Json silently skips get-only properties in .NET 9+ — broke `EdhTop16Client` before), no inline `[Attribute]` on property line, no re-indent C# raw-string literals, preserve LF line endings, touch only lines that need touching.
**Prevention for v1.4:** plan-checker has a "diff sanity" rule — if a PR touches >20 files with formatting-only changes interleaved with substantive changes, BLOCK. Force the implementer to split formatting and feature diffs. Codex must be reminded in every CONTEXT.md.
**Phase Owner:** all v1.4 phases. Roadmapper includes formatting-discipline reminder in every CONTEXT.md template.

### R-7: HANDOFF.json / STATE.md vs origin staleness on resume
**v1.2 incident (cited in retrospective):** HANDOFF.json said Phase 10 was already done, but a prior session had already shipped 10-05 on a different branch. Cost: confused stash + unnecessary rebase + 30 minutes of reconstruction.
**Prevention for v1.4:** on every resume, `git fetch` + compare `HEAD` vs `origin/<branch>` BEFORE reading any planning artifact. v1.4's long-running harvest phases (Phase 16 + 17) likely span multiple sessions; this discipline matters more, not less.
**Phase Owner:** every session resume. Process discipline.

---

## Phase-Specific Warnings Quick Reference

| Phase Topic | Likely Pitfall(s) | Mitigation |
|-------------|-------------------|------------|
| **Phase 16: Content KB Foundation (sources + ingestion)** | P1, P2, P3, P6, P7, P11, P12, P15 | Verify YouTube transcript path on non-owned video; route through proxy or paid API; hosted single-worker; namespace tables `content_*`; anti-forgery on admin POSTs; Render env vars for keys |
| **Phase 17: Content KB Summarization + Cost UI** | P3, P4, P6 | Advisory-lock + serializable txn on cap check; OpenAI Structured Outputs only; staged-pipeline persistence; pool-friendly conn handling |
| **Phase 18: Doc-Comment NoWarn Backlog** | P8 | Backfill all 88 BEFORE NoWarn strip; separate SCs for "documented" vs "warning gate"; investigate Razor partial CS1591 |
| **Phase 19: Gemini Paste-Limit Unblock** | P5, P13 | AiPlatform value-object variant (NOT string); skip split-message UX; direct Gemini API; UpstreamErrorMessageBuilder.BuildGeminiMessage overload |
| **Phase 20: Admin Mobile Responsive + WDG-04 Modal** | P9, P10 | Use native `<dialog>`; CSS scoped to `.admin-shell`; `@layer admin`; visual-regression on 22 themes |
| **Every phase** | R-1, R-2, R-3, R-4, R-5, R-6, R-7 | STATE arithmetic auto-compute; REQ checkbox auto-flip; anchored grep; cross-AI plan review; no-ship-failing-tests; formatting paranoia; resume-discipline `git fetch` |

---

## Sources

- [YouTube Data API Quota Calculator](https://developers.google.com/youtube/v3/determine_quota_cost) — HIGH
- [YouTube Data API: Captions Download](https://developers.google.com/youtube/v3/docs/captions/download) — HIGH (download requires edit permission)
- [YouTube Data API Errors Reference](https://developers.google.com/youtube/v3/docs/errors) — HIGH
- [youtube-transcript-api IP-blocking GitHub issue](https://github.com/jdepoix/youtube-transcript-api/issues/511) — HIGH (cloud IP blocking confirmed)
- [Fixing YouTube Transcript API RequestBlocked Error](https://medium.com/@lhc1990/fixing-youtube-transcript-api-requestblocked-error-a-developers-guide-83c77c061e7b) — MEDIUM
- [OpenAI Whisper API Limits (file size 25MB, timeout)](https://www.transcribetube.com/blog/openai-whisper-api-limits) — MEDIUM
- [OpenAI Whisper file-size error 26214400 bytes](https://portkey.ai/error-library/content-size-limit-error-10534) — HIGH (explicit byte limit)
- [Whisper community thread on >25MB workaround](https://community.openai.com/t/whisper-api-how-to-upload-file-that-larger-than-25mb/693285) — MEDIUM
- [Whisper API pricing $0.006/min](https://tokenmix.ai/blog/whisper-api-pricing) — HIGH
- [OpenAI Structured Outputs introduction (<0.1% failure)](https://openai.com/index/introducing-structured-outputs-in-the-api/) — HIGH (official)
- [Structured Output Reliability in Production: "JSON Mode is not a contract"](https://tianpan.co/blog/2026-04-20-structured-output-reliability-production) — MEDIUM
- [LLM Structured Outputs: JSON Schema Enforcement](https://eastondev.com/blog/en/posts/ai/20260506-llm-structured-output/) — MEDIUM
- [Gemini API rate limits (official)](https://ai.google.dev/gemini-api/docs/rate-limits) — HIGH
- [Gemini API free-tier 2026 details](https://pecollective.com/tools/gemini-free-tier-guide/) — MEDIUM (free tier RPM/RPD)
- [Vertex AI vs AI Studio differences](https://www.cloudzero.com/blog/google-vertex-ai-pricing/) — MEDIUM
- [Npgsql connection pool exhaustion issues](https://github.com/npgsql/npgsql/issues/5156) — HIGH (official issue tracker)
- [Render Postgres connection limits](https://community.render.com/t/postgres-max-connections/1548) — HIGH (Render official community)
- [Render Connection Pooling docs](https://render.com/docs/postgresql-connection-pooling) — HIGH
- [CS1591 docs (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs1591) — HIGH
- [CS1591 generated-code suppression Roslyn issue (9-year-old open)](https://github.com/dotnet/roslyn/issues/12702) — HIGH
- [CS1591 + Razor xmlDoc enabled](https://github.com/aspnet/Mvc/issues/4653) — HIGH (Razor-specific)
- [Adrian Roselli: A Responsive Accessible Table](https://adrianroselli.com/2017/11/a-responsive-accessible-table.html) — HIGH (a11y authority)
- [UXPin: Accessible Modals with Focus Traps 2026](https://www.uxpin.com/studio/blog/how-to-build-accessible-modals-with-focus-traps/) — MEDIUM
- [The A11Y Collective: Mastering Accessible Modals with ARIA](https://www.a11y-collective.com/blog/modal-accessibility/) — HIGH
- [Cost Circuit Breaker AI Agents pattern](https://dev.to/sebastian_chedal/the-cost-circuit-breaker-how-we-prevent-runaway-spending-across-9-ai-agents-4i5k) — MEDIUM
- [OpenAI Agentic Commerce production idempotency guide](https://developers.openai.com/commerce/guides/production) — HIGH (idempotency-key NOT supported on transcriptions endpoint)
- DeckFlow `.planning/RETROSPECTIVE.md` v1.3 section — HIGH (project source of truth for recurring patterns)
- DeckFlow `.planning/milestones/v1.3-MILESTONE-AUDIT.md` — HIGH (project source for tech-debt deferrals)
- DeckFlow `CLAUDE.md` — HIGH (project constraints, formatting rules)
- DeckFlow `DeckFlow.Web/Services/Harvest/HarvestRunStore.cs` — HIGH (v1.1 schema pattern verified by direct grep)
- DeckFlow `DeckFlow.Web/Controllers/Admin/*.cs` — HIGH (anti-forgery pattern verified by direct grep)
