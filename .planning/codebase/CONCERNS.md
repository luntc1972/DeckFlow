# Codebase Concerns

**Analysis Date:** 2026-05-29

## Tech Debt

**Documentation-Comment Backlog:**
- Issue: 1591/1573/1587 warnings suppressed in `DeckFlow.Web.csproj` via `<NoWarn>$(NoWarn);1591;1573;1587</NoWarn>` (`DeckFlow.Web/DeckFlow.Web.csproj:40`)
- Files: `DeckFlow.Web.csproj`, `DeckFlow.Web/Controllers/`, `DeckFlow.Web/Services/`
- Impact: Phase 23 hard-blocks on documenting ~38 remaining public types + new v1.4 surface before stripping NoWarn. Until then, missing doc comments remain hidden from compiler diagnostics.
- Fix approach: Phase 23-02 planned to complete backfill on all remaining types and strip NoWarn flag. Current status (Phase 27 shipped): 17-01 and 17-02 completed ~19 type-level declarations; Phase 23 must finish remaining ~38 before NoWarn removal.

**Large Service Files — Complexity Pressure:**
- Issue: Single-file service classes approaching or exceeding 1,500 lines of code
- Files: 
  - `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (1,625 lines)
  - `DeckFlow.Web/Controllers/DeckController.cs` (1,555 lines)
  - `DeckFlow.Web/Services/DeckComparisonService.cs` (1,304 lines)
  - `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` (1,263 lines)
  - `DeckFlow.Web/Services/MetaGapService.cs` (1,001 lines)
- Impact: Long methods, deep nesting, and scattered concerns increase cognitive load and test fragility. DeckAnalysisPacketService contains ~80 lines of prompt-building logic mixed with card-fetch orchestration.
- Fix approach: Extract prompt-building to dedicated helper interfaces (already done for `IAnalysisPromptVariant` + `GeminiAnalysisPromptVariant`, etc.); consider further modularization of deck-assembly logic into smaller, testable units.

**Gemini Paste-Limit Workaround Missing:**
- Issue: Full analysis/comparison/meta-gap/set-upgrade packets frequently exceed Gemini's ~30K character paste limit, truncating instructions and producing degraded output
- Files: `DeckFlow.Web/Configuration/AiPlatformOptions.cs:6-7`, `DeckFlow.Web/Program.cs:71-77`
- Current state: `DECKFLOW_GEMINI_ENABLED=true` (env var toggle, default FALSE) hides Gemini from UI radio selector, but server-side prompt builders still accept "Gemini" in requests
- Impact: Users who enable Gemini get silently degraded responses without visibility into cause; no automatic split-message or truncation warning in place
- Fix approach: v1.5 backlog — add split-message workaround or packet-size gating per platform. Until then, keep Gemini hidden by default and document the 30K limit in release notes.

**MDFC/DFC Card Handling Incomplete:**
- Issue: Double-faced and modal double-faced cards parsed and handled at parser level but NOT flagged in analysis prompts (e.g., DeckFlow does not note "Fabled Passage has two sides; consider split casting")
- Files: `DeckFlow.Core/Parsing/ArchidektParser.cs` (MDFC parsing logic exists), `DeckFlow.Web/Services/DeckAnalysisPacketService.cs` (card reference lookup)
- Current state: Scryfall REST API DTO (`ScryfallCard.cs` deserialization) is missing the `layout` field that would flag MDFC deterministically
- Impact: Moderate (8-10% prompt deduplication opportunity); users don't get flagged on MDFC utility (e.g., fetch-land that also ramps). Workaround: CLI harness can infer layout from card name substring matching, but front-end lookup has no fallback.
- Fix approach: Add conditional MDFC flag logic in `DeckAnalysisPacketService` using substring matching on common keywords ("//", "faces", modal syntax) until Scryfall DTO is updated. Low priority (affects only MDFC subsetof decks).

## Known Bugs

**Phase 20 Lister N+1 Query:**
- Symptoms: Channel lister in content harvest queries all videos per-channel one at a time instead of batch; scales poorly as channels grow
- Files: `DeckFlow.Core/Integration/YoutubeChannelLister.cs`, `DeckFlow.Core/Integration/YoutubeContentService.cs`
- Trigger: When harvest workflow processes a list of YouTube channels, each channel's videos are fetched individually via sequential `Videos.GetAsync` calls
- Current mitigation: Bounded by `--limit` flag (default ~100 videos per channel); revisit if limit grows or channel count increases
- Status: WR-02 follow-up added per-video `Videos.GetAsync` in channel lister; committed 2026-05-27. Uncommitted; commit after UAT approval.

**Phase 20 Harvest Source Isolation:**
- Symptoms: One dead/aborted uploads playlist causes entire batch to fail without isolating the error to that source
- Files: `DeckFlow.Core/Integration/YoutubeContentService.cs`, harvest runner orchestration
- Current fix: Added per-source `try/catch` + fallback URL (`@TheCommandZone` example); one dead channel no longer aborts sibling channels
- Status: Uncommitted; commit after UAT approval.

**Sol Ring Category Suggestion — Colorless/Staple Card Bug (CAT-01):**
- Symptoms: Category suggestion endpoint returns empty result set for Sol Ring (colorless artifact ramp staple)
- Files: `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs`, category filter logic
- Trigger: Card lookup matches by card name + colorless set, but filter logic excluded cards during the transition from server-harvested to local-KB model
- Current fix: Read-time `CategoryFilter` applied in lookup path; restored category results for colorless staple cards
- Status: SHIPPED 2026-05-25; live smoke-test passed (card + commander lookups post-schema-reset)

**Database Connection Timeout During Schema Validation:**
- Symptoms: Startup failure showing database connection timeout when schema validation runs on Render Starter (shared Basic-256mb Postgres)
- Files: `DeckFlow.Web/Program.cs:514` (`ValidateDbAtStartup` logic)
- Trigger: High concurrent load or slow Postgres response time causes `CreateConnection` to exceed the 30-second request timeout before schema queries complete
- Current mitigation: Database validation waits for up to 30 seconds; Render may throttle under load; no automatic retry on startup
- Fix approach: Increase timeout window or defer schema validation to background task post-startup; alternatively, make it optional (gated by env var) for Render deployments.

## Security Considerations

**Public Repository with Secrets in Render Dashboard:**
- Risk: Codebase is public at `luntc1972/DeckFlow`; all secrets (`OPENAI_API_KEY`, `FEEDBACK_ADMIN_PASSWORD`, `DECKFLOW_LLM_MONTHLY_CAP_USD`, etc.) are stored in Render dashboard with `sync: false` (not committed)
- Files: None in repo (by design); stored in Render service environment settings
- Current mitigation: `sync: false` prevents Render from publishing env vars to GitHub; `.gitignore` and `.env` checks prevent local accidental commits
- Recommendations: 
  1. Maintain `sync: false` indefinitely on all sensitive vars
  2. Document secret rotation procedures (Render dashboard → new value) in DEPLOYMENT.md
  3. If secrets are ever staged in local `.env` for testing, ensure `.gitignore` explicitly blocks `*.env` and `.env.*`
  4. Use Render's built-in secret auditing to verify no secrets leak into logs

**HTTP Basic-Auth Brute-Force Throttle (BUG-02 Fix):**
- Risk: Admin API at `/Admin/Feedback/*` is gated by Basic Auth with a fixed 15-minute window, 10-failure limit (BUG-02 / Phase 5)
- Files: `DeckFlow.Web/Infrastructure/BasicAuthMiddleware.cs:35`, `DeckFlow.Web/Services/AdminBruteForceTrackerStore.cs:10`
- Current mitigation: 
  1. Throttle runs BEFORE any password parsing (prevents timing attacks)
  2. Partition key uses `CF-Connecting-IP` (Cloudflare header) prioritized over `X-Forwarded-For` (Phase 5 BUG-02)
  3. 10-failure limit over 15 minutes is fixed; no exponential backoff
- Recommendations:
  1. Monitor logs for repeated 401 responses on `/Admin/Feedback` endpoints
  2. If brute-force attacks are observed, reduce the 10-failure threshold or increase window duration
  3. Consider adding IP-allowlist for admin endpoints if deployment is stable (known admin IPs)

**CSRF on API Endpoints:**
- Risk: SameOrigin validation checks Origin/Referer headers but relies on `UseForwardedHeaders()` middleware to read correct scheme from `X-Forwarded-Proto`
- Files: `DeckFlow.Web/Security/SameOriginRequestValidator.cs`, `DeckFlow.Web/Program.cs:181-198`
- Current mitigation: 
  1. `UseForwardedHeaders()` runs at position 194 in middleware chain — BEFORE `SameOriginRequestValidator` (position ~225-227)
  2. Default loopback trust list (127.0.0.1, ::1) is preserved for health checks; no `Clear()` called
- Recommendations:
  1. Verify `X-Forwarded-Proto` is present on all Render-ingress requests (test via curl -H X-Forwarded-Proto header)
  2. Monitor for 403 errors on `/api/*` endpoints to catch scheme-mismatch issues early

## Performance Bottlenecks

**IMemoryCache No Configurable Size Limit:**
- Problem: `AddMemoryCache()` in `DeckFlow.Web/Program.cs:69` uses default configuration with no explicit size limit
- Files: `DeckFlow.Web/Program.cs:69`
- Impact: On Render Starter with 512MB RAM cap, unbounded cache (TaggerSessionCache, search results, category knowledge) can exhaust memory. Default ASP.NET IMemoryCache expiration is 20% of process heap — but no size limit is enforced.
- Current usage: 
  - `TaggerSessionCache` (singleton, small ~270 bytes per session)
  - `FeedbackStore` search results cache (bounded by in-flight requests)
  - `CategoryKnowledgeStore` cache (bounded by card count in KB, ~0.5 MB per 1K cards)
- Scaling limit: If KB grows to 100K+ cards, in-memory index could exceed 50+ MB; risk of memory pressure on Render.
- Improvement path: Set explicit `MemoryCacheOptions.SizeLimit` in Program.cs (e.g., 100 MB) and tag entries with relative size; monitor production memory usage via Render logs.

**Scryfall Throttle is Static Semaphore (Global 5 req/sec):**
- Problem: `ScryfallThrottle.Gate` (static `SemaphoreSlim`) enforces global pacing across all concurrent requests
- Files: `DeckFlow.Web/Services/ScryfallThrottle.cs:35` (static field)
- Impact: In high-load scenarios (multiple deck builds in parallel), all Scryfall requests serialize through one gate. 200ms minimum interval means worst-case 5 requests/sec globally. During a "build multiple decks" batch workflow, second and third requests wait 200ms+ for first to complete, even if Cloudflare allows burst.
- Current design rationale: Conservative pacing (200ms vs Scryfall's 50-100ms suggestion) leaves headroom for Cloudflare's burst detection. Tradeoff: latency vs stability.
- Scaling path: Consider per-card-type throttle (e.g., collection vs search) or adaptive pacing based on Retry-After signals, but only if live metrics show 429s or timeouts.

**Archidekt Cache Job Service Background Refresh:**
- Problem: `ArchidektCacheJobService` runs on a background timer without advisory lock; multiple Render instances or pod restarts could trigger overlapping refreshes
- Files: `DeckFlow.Web/Services/ArchidektCacheJobService.cs:525`
- Impact: On Render Starter (single-instance), no issue. If multi-instance deployment is added, concurrent harvests will re-fetch and re-insert duplicate category rows, bloating the database. No advisory lock in place.
- Improvement path: Add database-level advisory lock (SQL `PRAGMA wal_checkpoint` + lockfile for SQLite, `pg_advisory_lock` for Postgres) OR change to single-instance constraint in deployment config.

**Prompt Variant Duplication (22% Overhead):**
- Problem: Five prompt variants (ChatGpt, Claude, Gemini, etc.) exist for Analysis, Comparison, MetaGap, SetUpgrade, FollowUp; ~22% of common template language is duplicated across implementations
- Files: `DeckFlow.Web/Services/PromptBuilders/Analysis/` (5 variant classes per prompt type), `DeckFlow.Web/Services/JsonTextFormatterService.cs`
- Impact: Moderate — maintenance burden when prompt logic changes; inconsistency risk if one variant is updated and others are missed
- Improvement path: v1.5 backlog — extract common scaffold template into base class or factory; override only platform-specific instruction text (e.g., GeminiJsonMandate vs ChatGpt default JSON output).

## Fragile Areas

**TaggerSessionCache TTL Invariant (HIGH-2):**
- Files: `DeckFlow.Web/Services/TaggerSessionCache.cs:54`, `DeckFlow.Web/Program.cs:109-111`
- Why fragile: Cache TTL (270s) MUST stay strictly 30 seconds BELOW `SocketsHttpHandler.PooledConnectionLifetime` (300s). If TTL is raised or handler lifetime is lowered without updating both, a stale session cookie could be replayed against a fresh handler, breaking Tagger CSRF flow.
- Safe modification: Any change to either constant requires updating the hardcoded 30-second margin comment AND adding a unit test that verifies `session_ttl < handler_lifetime - 30s`.
- Test coverage: `TaggerSessionCacheTests` (xUnit) verifies TTL, but does NOT verify the 30-second invariant across both constants. Add a static test in `TaggerSessionCacheTests` that reads both values and asserts the margin.

**Polly Resilience Pipeline Registration (D-05, B2 Checker Invariant):**
- Files: `DeckFlow.Web/Program.cs:163-165`, `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs`
- Why fragile: Pipelines are registered into `IResiliencePipelineRegistry<string>` but resolved via `ResiliencePipelineProvider<string>` with string keys (no keyed-services attributes). If a new HTTP caller is added and the pipeline name is misspelled, resolution succeeds at runtime but returns a no-op (not-registered) pipeline instead of failing fast.
- Safe modification: Every new HTTP service (`IDeckConvertService`, `INewLookupService`) must:
  1. Have a corresponding pipeline registered in `ResiliencePipelineFactory.AddDeckFlowResiliencePipelines()`
  2. Pass the exact matching string name to `ResiliencePipelineProvider<string>.GetPipeline<RestResponse>("name")`
  3. Add a smoke test in the test project that verifies the pipeline exists (e.g., `var p = provider.GetPipeline<RestResponse>("new-service"); Assert.NotNull(p);`)
- Test coverage: No existing test verifies pipeline existence at startup. Consider adding a `ResiliencePipelineFactoryTests` class that instantiates the factory and confirms all expected pipelines are registered.

**Forwarded Headers Trust Chain:**
- Files: `DeckFlow.Web/Program.cs:181-198`, `DeckFlow.Web/Security/SameOriginRequestValidator.cs`
- Why fragile: Middleware chain order is critical. If `app.UseForwardedHeaders()` is accidentally moved AFTER `app.UseRouting()` or security middleware, `request.Scheme` will be `http` (not `https` from proxy), and `SameOriginRequestValidator` will reject CORS-credentialed requests because Origin header is `https://` but scheme is `http://`.
- Safe modification: Any addition of middleware BEFORE `UseForwardedHeaders()` requires re-testing that request.Scheme reflects the proxy's X-Forwarded-Proto. Add a comment linking to line 181-198 rationale block to prevent accidental reordering.
- Test coverage: Integration test in `DeckFlow.Web.Tests` should verify `POST /api/suggestions` with `Origin: https://` header succeeds when `X-Forwarded-Proto: https` is present.

**EdhTop16Entry Deserialization (get-only init-property risk):**
- Files: `DeckFlow.Web/Models/EdhTop16Entry.cs:3-30` (all properties are `{ get; init; }`)
- Why fragile: `System.Text.Json` in .NET 9+ silently skips `get-only` properties during deserialization. If a property ever loses its setter or init keyword, the JSON field is ignored without warning. E.g., if line 25 is changed from `public IReadOnlyList<EdhTop16Card> MainDeck { get; init; }` to `public IReadOnlyList<EdhTop16Card> MainDeck { get; }`, JSON deserialization will fail silently (MainDeck remains empty array).
- Safe modification: CLAUDE.md explicitly forbids auto-formatting tools from converting `{ get; init; }` to `{ get; }`. Manual edits to this record MUST preserve init accessor. Add a reminder comment above the class definition.
- Test coverage: `EdhTop16ClientTests` should include a JSON roundtrip test that verifies all properties survive deserialization (spot-check a few key fields).

**ContentKbEnabled Display Gate (Phase 22 Dependency):**
- Files: Feature flag logic in `DeckFlow.Web/Views/` and controllers (content_kb_enabled, default OFF)
- Why fragile: Content KB site integration (Phase 22) adds a new feature flag to show/hide KB browse, filter, and upload UI. If the flag name is mismatched between controller, service, and view, the UI is silently hidden even when the KB is fully functional. No warning or error message surfaces the mismatch.
- Safe modification: Phase 22 must define the flag name as a constant in a shared location (e.g., `DeckFlow.Web/Configuration/FeatureFlagConstants.cs`) and use it everywhere. All test harnesses should verify the flag can be toggled via environment variable.
- Test coverage: Phase 22 success criteria must include a test that sets the flag to true and verifies the KB UI is displayed; set to false and verifies it is hidden.

## Scaling Limits

**RAM Allocation on Render Starter (512MB):**
- Current capacity: ~400 MB usable (after ASP.NET runtime overhead)
- Limit: If Content KB grows to 100K+ cards with full in-memory category index, plus concurrent request state (Scryfall collection batches, parsed decks), memory pressure occurs around 50-60 MB KB + 100 MB deck-request buffers = risk of OOM.
- Scaling path: 
  1. Monitor production memory usage via Render logs; set up alerting at 80% threshold
  2. Implement lazy-loading for KB index (load-on-first-access, cache only hot categories)
  3. Upgrade to Render Standard plan (1GB+) if KB grows beyond 50K cards
  4. Consider read-only Postgres copy of KB to offload memory pressure (Postgres can cache large indexes efficiently)

**Database Query Concurrency on Basic-256mb Postgres:**
- Current capacity: ~5-10 concurrent connections (connection pool default)
- Limit: If multiple Render instances or high-traffic weeks cause >10 concurrent queries, connection pool exhaustion occurs; new requests queue with 30-second timeout
- Scaling path:
  1. Monitor Render Postgres metrics (active connections, query time)
  2. Upgrade to Render Standard Postgres if >80% pool utilization is observed
  3. Add connection pooling middleware (PgBouncer) if multi-instance deployment is planned

**Whisper + LLM Monthly Spend Caps:**
- Current capacity: `DECKFLOW_WHISPER_MONTHLY_CAP_USD` $15 (default), `DECKFLOW_LLM_MONTHLY_CAP_USD` $15 (default)
- Limit: At ~$0.03 per Whisper minute + ~$0.01 per 1K tokens LLM, $15/month caps approximately:
  - Whisper: 500 minutes of transcription (~250 videos at 2 min avg)
  - LLM: 1.5M tokens of distillation (depending on prompt size)
- Scaling path: 
  1. Monitor actual spend via `llm_spend_ledger` + `whisper_spend_ledger` tables (Phase 21)
  2. Add spend alerts when 80% of cap is reached (gated by env var `DECKFLOW_LLM_SPEND_ALERT_THRESHOLD`)
  3. Adjust caps based on actual usage (e.g., if 250 videos/month is sustainable, raise to $25-30)
  4. Consider per-source spend caps (e.g., each YouTube channel limited to $5/month) for future phases

## Dependencies at Risk

**System.CommandLine 2.0.0-beta4 (Pre-Release):**
- Risk: Still in beta; future version bumps may have breaking API changes
- Files: `DeckFlow.CLI/Program.cs`, `DeckFlow.CLI/CommandRunners.cs`
- Impact: CLI is non-critical path (dev/admin use only); if beta version breaks, workaround is to pin current version or downgrade to 1.x
- Migration plan: Monitor NuGet for 2.0.0 RTM release; upgrade immediately when available (likely few breaking changes from beta to RTM)

**Markdig 0.38.0 (Stable):**
- Risk: Low; Markdown rendering is passive (no security parsing, safe subset only)
- Usage: `HelpContentService.cs` renders help topics from `DeckFlow.Web/Help/**/*.md`
- Migration plan: Standard NuGet package update cycle; no known blockers

**RestSharp 114.0.0 (Client Wrapper):**
- Risk: Medium; multiple HTTP callsites depend on RestSharp. API changes would require updates across ~8 service files.
- Files: Every `IDeckConvertService`, `ICardLookupService`, banlist, spellbook, Scryfall services
- Current pattern: RestSharp is wrapped by `IScryfallRestClientFactory` + `ResiliencePipelineProvider` (D-01); migration to `HttpClient` only would require retargeting this layer
- Migration plan: v1.5+ backlog — consider moving to naked `HttpClient` + `System.Net.Http` for new services (post-Phase 22); legacy services remain on RestSharp with plan to retire by v2.0

## Missing Critical Features

**Content KB v1.5 Backlog — No Split-Message for Gemini:**
- Problem: Gemini paste-limit (~30K char) frequently exceeded by full analysis/comparison packets. No automatic splitting or truncation strategy implemented.
- Blocks: Users cannot reliably use Gemini for large decks; manual packet splitting is not documented
- Workaround: Keep Gemini hidden by default (`DECKFLOW_GEMINI_ENABLED` default OFF); document in v1.4 release notes that Gemini supports decks <200 cards comfortably

**Advisory Lock for Multi-Instance Archidekt Cache:**
- Problem: `ArchidektCacheJobService` background refresh has no mutual exclusion; multiple Render instances would trigger overlapping cache jobs
- Blocks: Multi-instance deployment (horizontal scaling)
- Workaround: Deploy as single-instance only; document in `DEPLOYMENT.md` that Archidekt cache job must run on exactly one pod

**Per-Source Harvest Failure Retry:**
- Problem: Phase 20 added per-source `try/catch` to isolate failures, but no retry logic within each source; one transient failure per channel aborts that channel's processing
- Blocks: Resilience under flaky network conditions (e.g., Render egress IP throttling)
- Workaround: Manual re-run of harvest job; document retry steps in admin console help

## Test Coverage Gaps

**Tagger Session Cache 30-Second Invariant:**
- What's not tested: The 30-second margin between `TaggerSessionCache` TTL (270s) and `SocketsHttpHandler.SetHandlerLifetime` (300s) is a hard invariant, but no test verifies it
- Files: `DeckFlow.Web.Tests/Services/TaggerSessionCacheTests.cs`
- Risk: Future refactor accidentally changes one constant without updating the other, breaking Tagger flow silently
- Priority: HIGH — add a test in `TaggerSessionCacheTests` that reads both constants at runtime and asserts `(270 + 30) == 300`

**Pipeline Registration Smoke Tests:**
- What's not tested: Polly pipelines registered in `ResiliencePipelineFactory` are not verified to exist at service startup
- Files: Need new test class `DeckFlow.Web.Tests/Services/Http/ResiliencePipelineFactoryTests.cs`
- Risk: Misspelled pipeline name in a new service silently falls back to no-op, producing failed requests without clear error message
- Priority: MEDIUM — add a startup test that verifies all five named pipelines exist (banlist, spellbook, tagger, tagger-post, scryfall)

**ForwardedHeaders Trust Chain Integration:**
- What's not tested: `POST /api/suggestions` with `Origin: https://` succeeds when proxy sends `X-Forwarded-Proto: https` but fails when header is missing
- Files: Need integration test in `DeckFlow.Web.Tests/Controllers/Api/SuggestionsApiControllerTests.cs`
- Risk: Security regression if middleware order is accidentally changed
- Priority: HIGH — add HTTPS forwarding test to prevent CSRF validator regressions

**EdhTop16Entry JSON Deserialization Roundtrip:**
- What's not tested: JSON roundtrip of `EdhTop16Entry` with all init-only properties
- Files: `DeckFlow.Web.Tests/Services/EdhTop16ClientTests.cs`
- Risk: Silent deserialization failure if init accessor is accidentally removed
- Priority: MEDIUM — add a test that deserializes sample EDH Top 16 JSON and asserts all properties are populated

**Gemini Truncation / Paste-Limit Behavior:**
- What's not tested: When a prompt packet exceeds Gemini's 30K limit, behavior is undefined (silent truncation? error message? degraded output?)
- Files: No existing test; would require new `DeckFlow.Web.Tests/Services/PromptBuilders/GeminiPromptVariantTests.cs`
- Risk: Users don't know why Gemini responses are incomplete or malformed
- Priority: MEDIUM (backlog for v1.5) — add a test with a large deck (200+ cards) that measures packet size and warns if it exceeds 25K (safety margin below 30K)

---

*Concerns audit: 2026-05-29*
