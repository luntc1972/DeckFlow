# Storage review fixes + CONCERNS.md triage

Scope: implement all 3 recommended actions from storage code review of commit 7b77a65, then address triaged concerns from `.planning/codebase/CONCERNS.md`.

---

## Part 1 — Storage review remediation (commit 7b77a65)

### A. BLOCKER — Postgres `SELECT EXISTS` bug
- [ ] `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — replace `SELECT EXISTS(...)` + `(long)cast` with `SELECT COUNT(1)` + `Convert.ToInt64`. Verify SQLite tests still pass.

### B. Postgres integration coverage
- [ ] Add Testcontainers.PostgreSql NuGet ref to `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`
- [ ] New `DeckFlow.Web.Tests/Integration/PostgresStorageTests.cs` — gated by env var (`DECKFLOW_POSTGRES_TESTS=1`) to keep CI/dev runs fast
- [ ] Cover: schema creation (both stores), feedback insert+select+update+delete, category-knowledge upsert+exists+migrations, deck queue enqueue+claim+mark
- [ ] Document in README how to run Postgres tests locally

### C. Cleanup
- [ ] Drop dead `IRelationalDialect.ParameterPrefix` property (both impls)
- [ ] Move `AddParameter` and `ExtractSqlitePath` helpers into `RelationalDatabaseConnection` (or new `DeckFlow.Core/Storage/DialectExtensions.cs`); delete duplicates in `CategoryKnowledgeRepository.cs` and `FeedbackStore.cs`
- [ ] Replace empty-string `_databasePath` sentinel with `string?` in `CategoryKnowledgeStore` and `CategoryKnowledgeRepository`; expose `DatabasePath` as nullable
- [ ] (Optional) capture `Dialect` once at `RelationalDatabaseConnection` construction instead of switch-per-access

---

## Part 2 — CONCERNS.md triage

Mode: classify each concern as **DO NOW**, **PHASE** (defer to GSD roadmap phase), or **NOTE-ONLY** (acknowledge, no action).

### Quick wins — DO NOW
- [ ] Tech-debt: Delete duplicate user/sln files (`MtgDeckStudio.Web.csproj.user`, `DeckSyncWorkbench.Web.csproj.user`, `MtgDeckStudio.sln.DotSettings.user`, `DeckSyncWorkbench.sln.DotSettings.user`)
- [ ] Tech-debt: Delete stray `moxfield_probe.json` and root `build.log`; add `*_probe.json` and `build.log` to `.gitignore`
- [ ] Tech-debt: Delete stray repo-root `logs/` directory if present; verify `.gitignore` already covers `*.log`
- [ ] Tech-debt: Rename `MTGDECKSTUDIO_DISABLE_AUTO_BROWSER` → `DECKFLOW_DISABLE_AUTO_BROWSER`; one-release fallback reads old name
- [ ] Deps: Pin Polly to a single explicit version in both `DeckFlow.Web.csproj` and `DeckFlow.Core.csproj` (drop wildcard)
- [ ] Security: Add `DisableHtml()` to Markdig pipeline in `HelpContentService.cs` (defense-in-depth)
- [ ] Security: Bind `AdminFeedbackController.Apply` op param to a typed enum; reject unknown values
- [ ] Security: Add `Warning`-level log on basic-auth failure with request IP in `BasicAuthMiddleware.cs`

### Defer to GSD roadmap phases (do not do now)
- DeckController split (god-class refactor) — large, needs phase
- ChatGPT services extraction (`PromptBuilder`, `ScryfallReferenceResolver`, etc.) — large, needs phase
- Move `NullHttpClientFactory`/`NullScryfallRestClientFactory` to test project + `[InternalsVisibleTo]` — small but touches DI patterns; phase with HTTP ctor cleanup
- Standardize on one ctor per service + named test-helper factory — coordinated refactor
- Generated `*.js` removed from git tracking — needs build-pipeline coordination
- Scryfall Tagger 404 bug — its own fix phase
- ScryfallThrottle → Polly rate limiter — already started under prior plan; finish in a phase
- Per-route Scryfall fairness queue — phase
- Disk-backed Scryfall set cache — phase
- Tagger refresh `IHostedService` — phase
- DB-backed Archidekt cache job persistence — phase
- ChatGPT artifact retention sweep — phase
- RestSharp abstraction (`IUpstreamHttpClient`) — phase
- Health/ready endpoint + correlation ID middleware — phase
- Structured API error envelope — phase
- DeckController test coverage uplift — phase
- Resilience pipeline behavior tests — phase
- Visual regression harness for 25 themes — phase
- Tighten forwarded-headers `KnownIPNetworks` once cloud CIDR known — phase
- Per-IP rate-limit on `/Admin/*` — phase
- Cloudflare upstream snapshot harness — phase

### Note-only (acknowledge, low ROI right now)
- Browser-extension test coverage gap — manifest-version protocol bumps already documented
- PWA / offline-first — UX nice-to-have, not blocking
- Path-base `~/...` view discipline — manual review acceptable for now

---

## QA gate
- [x] All commands routed through Codex MCP per global rule (no direct Edit/Write of code by Claude)
- [x] Each Codex prompt instructs two QA passes; rework on failure
- [x] After Part 1 + Quick Wins, run full `dotnet test`; all green before commit
- [x] Commits: one logical change per commit, README updated when behavior shifts, no Co-Authored-By trailer

---

## Review

### Part 1 — Storage review remediation
- **A. Postgres BLOCKER** — fixed in commit `9c92120` (SELECT EXISTS → COUNT(1) + Convert.ToInt64). 52/52 Core tests green.
- **C. Cleanup** — done in commit `9c9b80f` (consolidated AddParameter/ExtractSqlitePath, dropped dead `IRelationalDialect.ParameterPrefix`, made `DatabasePath` nullable).
- **B. Postgres integration coverage** — parked but landed: commit `aa07637` adds Testcontainers.PostgreSql + 3 integration test fixtures gated by `DECKFLOW_POSTGRES_TESTS=1`. Default test run skips them (3 SKIP). Docker-in-WSL2 prerequisite documented in README. Tests will run as soon as Docker Desktop WSL integration is enabled.

### Part 2 — Quick wins (8/8 done)
1. **Stale user/sln files** — removed locally (untracked, no commit needed).
2. **moxfield_probe.json + build.log** — commit `57b9467`. `*_probe.json` added to `.gitignore`.
3. **Repo-root logs/** — removed locally (untracked, `*.log` already in gitignore).
4. **Env var rename** — commit `5d7b163`. `DECKFLOW_DISABLE_AUTO_BROWSER` primary, legacy `MTGDECKSTUDIO_DISABLE_AUTO_BROWSER` read as one-release fallback.
5. **Polly pinned** — commit `5d54ddd`. Both `DeckFlow.Web` and `DeckFlow.Core` now pin Polly to `8.6.6`. Diamond eliminated (verified via `project.assets.json` showing single `polly/8.6.6` entry).
6. **Markdig DisableHtml** — commit `e4abec5`. Defense-in-depth XSS hardening on `HelpContentService` pipeline.
7. **AdminFeedbackController.Apply enum binding** — commit `ba6ce6f`. `string op` → `AdminFeedbackOp { MarkRead, Archive, Delete }`. Unknown values now rejected at the model-binding boundary via `ModelState.IsValid` check. Tests updated accordingly.
8. **Basic-auth failure logging** — commit `864aa82`. `BasicAuthMiddleware` now takes `ILogger<BasicAuthMiddleware>` and logs Warning + remote IP on every 401 challenge with a per-call reason string.

### Housekeeping
- Commit `e7bbc0b`: dropped tracked `.planning/PROJECT.md` left over from project wipe (S777).
- Stray `theme-normalization-report.md` in `wwwroot/css/` deleted locally (untracked).

### Final QA — 2026-04-29 2:57pm MDT
- `dotnet build` — 0 Warning(s), 0 Error(s).
- `dotnet test` — Core 52/52, Web 295/295 + 3 Postgres skipped (expected).
- 8 commits added to `main` since the storage fix.

### Follow-ups (not in scope, just noting)
- `.planning/codebase/*.md` and `tasks/todo.md` still reference `MTGDECKSTUDIO_DISABLE_AUTO_BROWSER` (mem 3106). Code is correct; docs lag. Refresh next time we touch planning artifacts.
- Postgres integration tests will run once Docker Desktop's WSL integration is enabled — no code work needed, just env setup.
