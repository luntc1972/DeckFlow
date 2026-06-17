# Pitfalls Research

**Domain:** Local harvest-and-publish studio — direct prod-DB write, secrets management, YouTube scraping, LLM spend, commit-deploy path, admin grid perf, Blazor Server local tool
**Researched:** 2026-06-13
**Confidence:** HIGH — grounded in the actual codebase; every pitfall traced to a specific file, SQL statement, or known incident

---

## Critical Pitfalls

### Pitfall 1: Direct Prod-DB Write Clobbers Admin-Curated Visibility and Pins

**What goes wrong:**

The Studio tool calls `ContentSiteIndexStore.UpsertRowAsync` (the non-preserving overload) against
the prod Render Postgres instance. That method's `ON CONFLICT DO UPDATE` clause overwrites every
column **including `is_visible`** and **`is_evergreen`** (see `UpsertSql` in
`ContentSiteIndexStore.cs:558-596`). Any row the admin curated as visible/evergreen in the
deployed app gets silently reset to `FALSE` the moment the Studio pushes an updated distillation
of the same video.

**Why it happens:**

There are two upsert overloads. `UpsertRowAsync` (used by `DistillVideoAsync` in
`ContentKbCommandRunners.cs:1150`) was designed for the local CLI where `is_visible` has no
meaning yet. `UpsertRowPreservingVisibilityAsync` (used by `ContentKbSeedLoader.cs:61`) was added
specifically to protect curation on load — but it hardcodes `@isVisible = false` and `@isEvergreen = false`
on INSERT and preserves nothing on UPDATE (its `DO UPDATE` clause does not touch visibility at all,
see line 627-636). Neither overload is correct for a direct prod write.

A correct direct-prod write must: on INSERT set `is_visible=FALSE, is_evergreen=FALSE`; on UPDATE
touch only content columns (`source, title, video_url, artifact_path, published_utc, indexed_utc,
archetype_tags, bracket_tags, card_category_tags`) and leave `is_visible` and `is_evergreen`
exactly as they are in the DB. No existing overload does this for UPDATE.

**How to avoid:**

Add a third SQL variant — `UpsertContentColumnsOnlyAsync` — whose `DO UPDATE` explicitly excludes
`is_visible`, `is_evergreen`, and the forthcoming `approval_status` from the SET clause. Use this
variant exclusively for prod writes from the Studio. Never call `UpsertRowAsync` against a prod
connection string. Add an integration test: upsert a row with `is_visible=TRUE`, call the new
method, assert `is_visible` remains `TRUE`.

**Warning signs:**

- Admin-published entries disappear from the deployed KB browse page after a Studio push.
- `is_visible` count drops in prod Postgres after a Studio publish event.

**Phase to address:**

The phase that implements direct prod-DB write. Must be a design requirement on that phase, not a
later fix. Constraint: no prod write path may be added without this safe overload existing first.

---

### Pitfall 2: Partial Write — DB Row Written but Markdown SCP Failed, Leaving a Dangling artifactPath

**What goes wrong:**

The direct prod-DB push path writes a `content_site_index` row with an `artifact_path` pointing to
a markdown file (e.g., `content-kb/artifacts/snail-commander/dQw4w9WgXcQ.md`). The SCP step to
copy that file to the Render `/data` disk then fails (SSH timeout, key not registered, disk full).
The deployed site's KB browse page calls `GetPublishedRowsAsync`, gets the row, tries to resolve
the `artifact_path` via `ContentKbArtifactPathResolver`, and either 404s or shows an empty detail
pane. The operator has no indication the row is broken unless they notice the browse-page failure.

This is cross-store atomicity: the two stores (Postgres row + `/data` filesystem file) have no
shared transaction. There is no existing reconciliation mechanism.

**Why it happens:**

The commit-then-deploy path avoids this entirely because both the JSON seed and the markdown files
travel together in the same git commit, and `ContentKbSeedLoader` runs on startup only after the
files are deployed. The direct path decouples these two writes.

**How to avoid:**

Implement the direct path as two explicit sequential steps in the UI with distinct confirmations:
**Step 1 — Push Artifacts** (SCP all markdown files for approved entries), then **Step 2 — Push DB
Rows** (Npgsql upsert). This ordering ensures the file exists before the row references it. If
Step 1 fails, Step 2 is blocked. Surface each step's success/failure in the UI before advancing.
Add a post-push verification query: `SELECT id, artifact_path FROM content_site_index WHERE
is_visible = TRUE AND natural_key_value = ANY(@pushed_ids)` and attempt a HEAD check on each
`artifact_path` against the Render service to confirm the file is reachable. Log the dangling
count explicitly.

For recovery: add a `GET /Admin/ContentKb/audit-artifacts` endpoint on the deployed site that
queries `is_visible=TRUE` rows and checks each `artifact_path` exists on disk, returning a count
of broken rows. The operator can call this after any publish event.

**Warning signs:**

- SCP exits non-zero but the UI does not block the DB push.
- KB browse detail pane shows blank/missing content after a publish.
- `artifact_path` values reference files that do not exist under `MTG_DATA_DIR`.

**Phase to address:**

Direct prod-DB push phase. The file-first ordering must be in the plan's success criteria, not
retrofitted. The recovery audit endpoint is a separate low-complexity phase item.

---

### Pitfall 3: Secret Leakage via appsettings, Logs, or Accidental Commit on a Public Repo

**What goes wrong (three sub-modes):**

**A. appsettings file committed.** Developer creates `DeckFlow.Studio/appsettings.Development.json`
or `appsettings.local.json` with the prod Postgres connection string to "test it quickly," commits
the file, pushes. The repo is public (`luntc1972/DeckFlow`). GitHub secret scanning may flag it,
but the string is already in git history. Rotation is the only fix.

**B. Connection string in Serilog structured logs.** `RelationalDatabaseConnection` is constructed
with the raw connection string. If an exception is thrown and the exception message includes the
connection or if structured logging logs the config section, the password appears in the rolling
log files under `logs/web-.log` and in Render's stdout stream (both are observable by anyone with
access to the Render dashboard).

**C. `user-secrets` GUID exposed without secrets.** The `<UserSecretsId>` GUID in
`DeckFlow.Studio.csproj` is public — this is acceptable. What is not acceptable is any
`secrets.json` analogue landing in the tracked tree. The risk is creating a `secrets.json` file
inside the Studio project directory (instead of the OS user-secrets path) and accidentally staging
it.

**Why it happens:**

`appsettings.Development.json` is already in the solution's `.gitignore` patterns for DeckFlow.Web,
but a new Studio project with a different name may not be covered. The project CLAUDE.md rule "no
secrets in commits ever" is a human gate, not an automated one. Serilog's `{ConnectionString}`
structured template would log the value literally if used.

**How to avoid:**

- Use `dotnet user-secrets` exclusively for the Studio project (see STACK.md). The `<UserSecretsId>`
  GUID is the only Studio secret-related content that may appear in any tracked file.
- Add `DeckFlow.Studio/appsettings*.local.json` and `DeckFlow.Studio/secrets.json` to `.gitignore`
  before writing any Studio config file.
- The prod connection string must never appear in any `ILogger` call. In the Studio's startup, log
  `"Prod connection: [configured]"` (boolean) not the string itself.
- `PostgresConnectionStringNormalizer.Normalize(connectionString)` (already in Core) normalizes
  `postgres://` URIs; use it, but never pass the result to a logger.
- Add a pre-commit `git diff --cached | grep -i "postgres\|password\|apikey\|secret"` script (or
  register it as a git hook) to catch accidental staging. This codebase has no pre-commit hook today
  — v1.7 is the right time to add one scoped to the Studio path.

**Warning signs:**

- `git status` shows `appsettings*.json` or `secrets.json` as staged.
- Serilog output includes a substring matching `postgres://` or `Host=`.
- `git log --all --full-history -- "**/secrets.json"` returns any commit.

**Phase to address:**

Phase that scaffolds the Studio project (earliest phase). Gitignore entries and the no-log-creds
rule must be in the plan, not in a later security review.

---

### Pitfall 4: Export-All-Rows Includes Unapproved Entries in the Seed Commit

**What goes wrong:**

`RunContentIndexExportAsync` (CLI, line 354-382) calls `indexStore.GetAllRowsAsync()` — it exports
**every row** in `content_site_index` regardless of `is_visible`, `is_evergreen`, or the planned
`approval_status`. When the Studio UI calls this export to produce `index-seed.json` and commits
it, unapproved/rejected entries land in the seed file. On the next Render deploy,
`ContentKbSeedLoader.LoadIfPresentAsync` calls `UpsertRowPreservingVisibilityAsync` for each seed
row — which inserts them with `is_visible=FALSE`. They are invisible to users, but they are in
prod Postgres and in the public git repo (the seed JSON is committed to `content-kb/seed/`).

This is a data hygiene failure: rejected content (e.g., a creator video flagged as off-topic) ends
up committed to the public repo and loaded into the prod DB, even though the operator marked it
rejected.

**Why it happens:**

The CLI export was designed for the original local-only flow where everything in the local DB was
already curated. The Studio adds an explicit approval gate, but `GetAllRowsAsync` has no filter
parameter. The `ContentIndexExportRow.From` method (line 1333) does not include `is_visible` or any
approval field — the export format predates the approval concept.

**How to avoid:**

The Studio's publish-to-seed action must call a filtered export: only rows with
`approval_status = 'approved'` (or at minimum `is_visible = TRUE` as a proxy until the new column
lands). Do not expose a "export all" button in the Studio UI. Add a `GetApprovedRowsAsync` method
or extend the existing export with an `approvedOnly: bool` parameter, defaulting to `true` in
Studio calls. Add a `--approved-only` flag to the CLI export command for parity.

Also: add the `approval_status` column to `ContentIndexExportRow` so the seed JSON can record the
state and round-trip cleanly. The seed loader should ignore rows where `approval_status != 'approved'`
as a defense-in-depth check on load.

**Warning signs:**

- `index-seed.json` contains entries whose `approval_status` is `pending` or `rejected`.
- The seed row count exceeds the "approved" count visible in the Studio review queue.
- A video the operator intentionally rejected appears in the Render Postgres `content_site_index`.

**Phase to address:**

Phase that adds the `approval_status` column + review queue. The export must be gated before the
commit-path publish phase ships.

---

### Pitfall 5: Re-Distilling Already-Distilled Videos, Burning LLM Spend

**What goes wrong:**

The Studio UI shows a video with status `distilled`. The operator clicks "distill" again (e.g., to
regenerate tags after a vocabulary change). `RunDistillAsync` does check `distill_status ==
'distilled'` and skips (`logger.Information("already distilled")`, line 471-475 of
`ContentKbCommandRunners.cs`) — but only when iterating `ListVideosPendingDistillAsync`. If the
operator passes explicit `--video-ids`, the pending filter is bypassed (line 455-459): it takes the
provided IDs and proceeds regardless of current status.

For a subscription LLM provider (claude CLI, `isSubscriptionProvider = true`) the cost is $0. For
OpenAI (`isSubscriptionProvider = false`) this triggers 3 API calls per video. The spend guard
(`WouldExceedCapAsync`) only fires on the monthly cap, not on "was already distilled."

**Why it happens:**

The explicit video-IDs path is intentionally designed to force-re-distill (the comment says "exactly
these"). The design was correct for CLI power users who know what they are doing. A UI that shows a
"Re-distill" button without surfacing the cost projection makes it a one-click mistake.

**How to avoid:**

In the Studio UI: when a video already has `distill_status = 'distilled'`, label it
"Re-distill" with a cost warning and require an explicit secondary confirmation (not just the main
"Run" button). Always run `RunDistillAsync(dryRun: true)` first to project cost for the selected
set, show the result, and require confirmation before `dryRun: false`. The dry-run path is already
implemented — surface it in the UI's pre-distill confirmation step. Add a rule: if the monthly
cap is set and the projected cost equals $0 (subscription provider), still show the "N videos will
be re-distilled" count so the operator knows what is happening.

**Warning signs:**

- `LlmSpendLedger` monthly total rising unexpectedly after a Studio session.
- Distill run log showing `already distilled` skips at zero but spending non-zero (means IDs were
  passed explicitly, bypassing the status check).
- More LLM calls in `content_harvest_runs` than there are unprocessed videos.

**Phase to address:**

Phase that implements the distill UI step. The dry-run-then-confirm pattern must be a requirement,
not an enhancement.

---

## Moderate Pitfalls

### Pitfall 6: YoutubeExplode AngleSharp Concurrency Bug Parallelized in the Studio UI

**What goes wrong:**

The Studio shows a list of channels. The operator selects three and clicks "Browse all." The UI
fires three concurrent calls to `IYouTubeChannelVideoLister.ListRecentAsync`. AngleSharp's HTML
parser has a shared static state; concurrent calls corrupt each other's parse, producing partial
or garbled video lists. This was already hit in production harvest (MEMORY: `harvest_lister_concurrency_crash`
resolved 2026-06-08 by setting `concurrency = 1`).

**Why it happens:**

The fix was applied in the CLI harvest path by serializing the lister. Blazor Server components
are tempting to parallelize — `await Task.WhenAll(...)` patterns across channel browses would
reintroduce the bug.

**How to avoid:**

Enforce `SemaphoreSlim(1)` in the Studio's channel browse service, matching the CLI fix. The
`IYouTubeChannelVideoLister` interface should be documented as "not thread-safe, serialize all
calls." Never use `Task.WhenAll` across lister invocations in any Studio component. The STACK.md
already flags this as an architectural constraint to carry forward.

**Warning signs:**

- Channel browse returns fewer videos than expected, or duplicated/garbled titles.
- `AngleSharp`-related exceptions in Studio logs during multi-channel operations.
- Browse results differ between single-channel and multi-channel calls for the same channel.

**Phase to address:**

Channel browse UI phase. The semaphore must be in the plan's design notes.

---

### Pitfall 7: Blazor Server Long-Running Harvest/Distill Blocking the SignalR Circuit

**What goes wrong:**

Harvest for a 10-video batch takes ~2-5 minutes (transcript fetch + optional Whisper). Distill for
10 videos at ~30s each = 5 minutes. If this runs synchronously on the Blazor Server rendering
thread (or awaited directly in a component's `OnInitializedAsync`/button handler), the SignalR
circuit is held open with no UI updates. The browser appears frozen. After the default Blazor
circuit disconnect timeout (~3 minutes of no pings), the connection drops and the in-flight
operation is orphaned.

**Why it happens:**

Blazor Server's component lifecycle is single-threaded per circuit. `await RunHarvestAsync(...)` in
a button handler blocks the circuit for the entire operation. The developer may assume
`await` means "non-blocking," but it blocks the circuit's render loop from responding to
`StateHasChanged` events.

**How to avoid:**

Run harvest and distill as background `Task`s detached from the circuit, using
`_ = Task.Run(async () => { ... ; await InvokeAsync(StateHasChanged); })`. Progress updates must
go through `InvokeAsync(StateHasChanged)` from the background thread. Use a `CancellationTokenSource`
tied to `IDisposable` component teardown so orphaned operations cancel when the circuit closes.
This is the same pattern as the deployed `AdminHarvestController`'s polling approach — the
Studio adapts it for Blazor. Do not hold the cancellation token passed to `OnInitializedAsync`
for the long-running work; use a separate CTS.

For a single-operator tool, no queue or hub abstraction is needed — one in-flight operation at a
time is sufficient.

**Warning signs:**

- The browser tab appears frozen during harvest/distill with no incremental updates.
- Blazor circuit disconnect errors in Studio logs after long operations.
- An in-flight harvest continues after the browser tab is closed (no CTS teardown).

**Phase to address:**

Phase that wires harvest/distill into the Studio UI. Background-task pattern must be in the plan.

---

### Pitfall 8: git Push to Main from the Studio GUI

**What goes wrong:**

The commit-then-deploy publish path shells out to `git push origin main`. This is the operator
performing a deliberate publish action with their own credentials — it is not the AI pushing to
main autonomously. However, if the Studio commit path is invoked carelessly (e.g., a mis-click on
"Publish" before reviewing the diff), an irreversible push to main triggers a Render auto-deploy
immediately.

Additionally, the CLAUDE.md rule "AI must not push to main" applies to Claude and Codex agents, not
to the operator's own local tool. The Studio is operator-controlled. The risk is not a policy
violation — the risk is accidental publish before review.

**How to avoid:**

Add a mandatory "What will change" diff screen before `git push`. Show: rows being added, rows
being updated, rows being removed (from the seed JSON diff). Require a checkbox acknowledge ("I
have reviewed the diff above") before the Push button is enabled. Implement as a two-stage
action: Stage 1 = `git commit` (local, reversible via `git reset HEAD~1`); Stage 2 = `git push`
(irreversible for Render auto-deploy). Make Stage 2 a separate button, not automatic after Stage 1.

**Warning signs:**

- The Studio executes `git push` in the same function call as `git commit` with no intervening
  confirmation.
- No diff is shown between the current `index-seed.json` and the previous committed version.

**Phase to address:**

Phase that implements the commit-then-deploy path. Two-stage separation and diff display must be
in the plan's requirements.

---

### Pitfall 9: Schema Drift Between Local SQLite and Prod Postgres

**What goes wrong:**

The Studio adds the `approval_status` column to `content_site_index` via a self-healing ALTER
migration (the existing pattern from `is_evergreen` in v1.5). The migration runs on first-connect
in the local SQLite DB. The prod Postgres DB does not get the migration until `ContentKbSeedLoader`
runs at next Render deploy (which calls `EnsureSchemaAsync`). Between the local migration and the
next deploy, any direct prod-DB write from the Studio encounters a column-missing error.

**Why it happens:**

`EnsureSchemaAsync` is called on `ContentSiteIndexStore` construction (line 97 in the store, before
every operation). For local SQLite, this runs on first use. For the prod Postgres path in the
Studio, `EnsureSchemaAsync` is called when the Studio constructs its Postgres-backed store — so
the migration runs the first time the Studio connects to prod. This is actually safe: the schema
migration will fire before any data write on the same connection. The risk is if the migration is
added to the store but the Studio uses an older version of the store binary that predates the
migration.

**How to avoid:**

Always call `EnsureSchemaAsync` explicitly at Studio startup before any UI operation is enabled.
Log the schema version or migration actions performed. Never ship the Studio without verifying
`EnsureSchemaAsync` covers the `approval_status` column. Add a startup health check in the Studio:
`SELECT column_name FROM information_schema.columns WHERE table_name = 'content_site_index'` and
assert the expected column set before enabling publish actions.

**Warning signs:**

- Studio startup produces `column "approval_status" does not exist` Postgres errors.
- Local SQLite queries succeed but prod queries fail on the same code path.

**Phase to address:**

The `approval_status` column phase. Schema migration must be verified against both dialects before
the direct prod-write phase begins.

---

### Pitfall 10: CRLF Line Endings in index-seed.json Committed on Windows

**What goes wrong:**

The Studio shells out to `git commit` from Windows (or WSL running Windows git). The `index-seed.json`
file is written by `File.WriteAllTextAsync` on Windows, which uses `Environment.NewLine = "\r\n"`.
The git commit adds a CRLF file. The `.gitattributes` in this repo enforces LF (`text=auto`), which
causes git to show the entire file as changed on every commit (LF → CRLF normalization conflict)
or, worse, the file is committed with mixed line endings that cause the JSON parser to produce
trailing `\r` in string values.

**Why it happens:**

`File.WriteAllTextAsync` uses the platform default line ending. The existing `RunContentIndexExportAsync`
ends the file with `json + "\n"` (LF), but the internal `JsonSerializer.Serialize` with
`WriteIndented = true` uses `Environment.NewLine` for property separators, which is `\r\n` on
Windows.

**How to avoid:**

The Studio's export path must force LF: serialize to a `MemoryStream` or use
`JsonWriterOptions { Indented = true, NewLine = "\n" }` (available in .NET 8+). Alternatively,
normalize the output string with `.Replace("\r\n", "\n")` before writing. The `.gitattributes`
`text=auto` rule will handle conversion at commit time, but relying on that silently normalizes
the whole file on every write, inflating git diffs. Explicit LF in the write step is cleaner.

**Warning signs:**

- `git diff --check` reports whitespace errors on `index-seed.json` after a Studio publish.
- The entire seed file appears as changed (all lines) in `git diff` even when only a few rows
  were added.

**Phase to address:**

Phase that implements the seed export + commit path.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Use `UpsertRowAsync` (existing) for prod writes | No new code | Silently overwrites `is_visible`/`is_evergreen` on every push (Pitfall 1) | Never — a new safe overload is required |
| Export all rows, filter in the UI | Simpler export code | Rejected/unapproved content in public git repo (Pitfall 4) | Never — filter at query level |
| Await harvest/distill directly in Blazor component handler | Simpler code | Freezes the UI, drops circuit on long runs (Pitfall 7) | Never for operations >30s |
| Store prod creds in `appsettings.Development.json` | Quick to configure | Secret committed to public repo (Pitfall 3) | Never |
| Single-step `git commit + push` | Fewer UI states | No recourse before Render auto-deploy triggers (Pitfall 8) | Never |
| Skip dry-run before distill in UI | Fewer clicks | Surprise LLM spend on re-distill (Pitfall 5) | Never for non-subscription providers; optional for subscription ($0) |
| Parallelize YoutubeExplode calls for speed | Faster multi-channel browse | AngleSharp corruption (Pitfall 6) | Never |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Render Postgres (direct write) | Use `postgres://` URI as-is from Render dashboard | Run through `PostgresConnectionStringNormalizer.Normalize()` first — Render emits `postgres://` but Npgsql requires `postgresql://` or keyword-value format |
| Render /data SCP | SCP to the wrong path (`/data/artifacts/` vs `/data/content-kb/artifacts/`) | Match exactly `MTG_DATA_DIR` layout; test with a single file SCP before batch push |
| Render SSH | Assume SSH key is auto-registered | SSH public key must be registered manually in Render Account Settings; one-time setup gate that blocks the SCP path entirely if skipped |
| YoutubeExplode search | Call `GetResultBatchesAsync` and `GetByIdsAsync` concurrently across channels | Serialize all calls behind a `SemaphoreSlim(1)` — AngleSharp shared state |
| `LlmSpendLedger` monthly cap | Check cap once before a batch, not per-video | The cap is checked per-call in `RunDistillAsync` (lines 1014, 1043, 1071); replicating this at the batch level in the Studio is a duplicate gate, not a replacement |
| `ContentKbSeedLoader` upsert on deploy | Assume the seed loader preserves all columns | `UpsertRowPreservingVisibilityAsync` preserves `is_visible` and `is_evergreen` on UPDATE but hardcodes them to FALSE on INSERT — a new row seeded after a prod reset starts invisible even if it was previously published |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Admin grid count query (`GetDistinctProcessedCommanderCountAsync`) on every page load | `/Admin/Harvest` slow to render; query runs `COUNT(DISTINCT LOWER(commander_name)) FROM deck_queue WHERE processed=1` — full table scan with no index on `LOWER(commander_name)` | Add `CREATE INDEX CONCURRENTLY` on `LOWER(commander_name)` where `processed=1` (a partial expression index); or cache the count with a short TTL in `IMemoryCache` (existing cache already used for `StatusCacheKey`) | Noticeable at ~5,000+ processed decks; already slow at current corpus size per Phase 25 investigation |
| Admin grid page query (`GetPagedProcessedCommandersAsync`) running both count + page on initial load | Two separate queries each doing aggregation; `LIMIT/OFFSET` does not help the GROUP BY | The count and the page query are already separate (correct separation). The bottleneck is the GROUP BY + LOWER() aggregate, not the pagination itself. Fix is index on `LOWER(commander_name)` + `WHERE processed=1`, same as above | Already slow; the AJAX-paging fix in v1.7 defers the queries to on-demand but does not eliminate them |
| `EnsureSchemaAsync` called on every store operation | Acceptable for low-frequency CLI use; in a Blazor UI that renders the review queue (multiple reads per page render), it fires multiple `SELECT column_name` queries per request | Call `EnsureSchemaAsync` once at Studio startup in `Program.cs`, not per store method invocation | Any Studio page that creates store instances in a per-render lifecycle method |
| SCP of large markdown artifact sets (100+ files) as individual `scp` calls | Each SCP is a new SSH handshake; 100 files = 100 handshakes | Bundle artifacts into a `tar` archive and SCP the archive, then `ssh SERVICE 'tar xf /tmp/artifacts.tar -C /data'` — single handshake | >20 files in a single publish event |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Prod connection string in any tracked file | Public repo exposure; GitHub secret scan will flag it but history is permanent | `dotnet user-secrets` only; add `appsettings*.local.json` to `.gitignore` on Studio project creation |
| Logging the prod connection string at Studio startup | Leaks creds to Serilog file sink and Render stdout | Log presence (`"configured"` / `"not configured"`), never the value |
| Studio Blazor Server page exposed on a non-localhost bind address | Any LAN machine can trigger prod writes | `applicationUrl` in `launchSettings.json` must be `http://localhost:<port>` only; document this explicitly |
| `RunCorpusResetAsync` reachable from Studio UI without explicit confirmation | Deletes ALL `content_site_index` rows in prod (see line 323-328 of `ContentKbCommandRunners.cs` — `DeleteAllRowsAsync` + `DeleteAllVideosAsync`) | Do not expose corpus-reset in the Studio UI at all; it is a CLI-only emergency operation. If ever added to the UI, require typing the word "RESET" as confirmation |
| YouTube API key (if Data API v3 added) in user-secrets without a usage cap | Quota exhaustion from a bug in discovery code | Enforce a per-session call counter in the Studio's API key service; stop at a configurable daily max (e.g., 50 calls = 5,000 units) before the 10,000-unit daily limit is hit |

---

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| No "already harvested" badge on browse results | Operator re-queues videos already in the local DB, wastes time waiting for harvest to report "already harvested" | Batch-lookup `GetVideosByYoutubeIdsAsync` for all visible video IDs in the browse result; show a green "Harvested" / "Distilled" badge per row before the operator selects |
| No spend projection before distill | Operator clicks distill on 20 videos, spends unexpected $0.40 | Always show dry-run result as the "confirm" step; block distill button until dry-run has run |
| Publish button active even when 0 approved entries | Clicking publish produces an empty seed commit with no meaningful change | Disable the publish button when `approvedCount == 0`; show "Nothing to publish — approve entries first" |
| No separation between "approve for visibility" and "publish to prod" | Operator approves entries expecting them to go live immediately, but publish is a separate step | Make the state machine explicit: Pending → Approved → Published. Use distinct button labels: "Approve" vs "Publish Approved to Prod" |
| Admin grid renders full page on every pagination click (current behavior) | /Admin/Harvest takes 2-5s to load any page because count + page queries run synchronously on each request | AJAX numbered pages: initial load renders a skeleton; page clicks replace only the table body via `GET /Admin/Harvest/commanders?page=N` partial |

---

## "Looks Done But Isn't" Checklist

- [ ] **Direct prod-DB write:** Verify `is_visible` and `is_evergreen` are preserved on UPDATE — not silently reset. Write an integration test: set `is_visible=TRUE`, push an updated distillation, assert `is_visible` is still `TRUE`.
- [ ] **Export-to-seed:** Verify the exported JSON contains only `approval_status='approved'` rows. Check row count against the approved queue count in the Studio UI — they must match.
- [ ] **Secret hygiene:** Run `git diff --cached | grep -i "postgres\|password\|secret\|apikey"` before the first Studio commit is ever made. Verify `DeckFlow.Studio/` paths are covered by `.gitignore` for `appsettings*.local.json`.
- [ ] **SCP artifacts before DB push:** Confirm the file-first ordering is enforced in code — Step 2 (DB push) must be unreachable if Step 1 (SCP) has not succeeded.
- [ ] **Distill dry-run gate:** Verify the dry-run confirmation dialog is shown before any non-zero real distill call. Manually click "Distill" on an already-distilled video and confirm the UI shows a warning.
- [ ] **AngleSharp concurrency:** Verify `SemaphoreSlim(1)` wraps all `IYouTubeChannelVideoLister` calls in the Studio. Confirm no `Task.WhenAll` exists over lister invocations.
- [ ] **Blazor circuit teardown:** Verify harvest/distill background tasks cancel when the Blazor component is disposed (`IDisposable.Dispose` or `IAsyncDisposable.DisposeAsync` calls `_cts.Cancel()`).
- [ ] **CRLF in seed JSON:** After a Studio publish, run `file content-kb/seed/index-seed.json` on Linux — must report `ASCII text` not `ASCII text, with CRLF line terminators`.
- [ ] **Admin grid AJAX:** Confirm that navigating to `/Admin/Harvest` page 2 does not trigger a full-page reload of the initial heavy count+aggregate query.

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| is_visible/is_evergreen clobbered on prod | MEDIUM | Run `UPDATE content_site_index SET is_visible = TRUE WHERE natural_key_value IN (...)` directly on prod via the `prod-readonly-query` skill (write escalation needed); redeploy to re-run seed loader with preserving overload |
| Dangling artifactPath (DB row, no file) | LOW-MEDIUM | SCP the missing artifact file manually; or set `is_visible = FALSE` on the dangling row; run audit endpoint to confirm |
| Secret committed to git | HIGH | Immediately rotate the Render Postgres password in the Render dashboard; run `git filter-repo` to scrub history; force-push (requires explicit user authorization); notify GitHub to invalidate cached copies |
| Unapproved entries in seed commit | LOW | `git revert` the seed commit; re-export with approval filter; re-commit |
| Accidental corpus reset on prod | HIGH | Restore from Render Postgres backup (Render Basic plan has daily backups); re-run SCP for artifact files from local copy; re-run `ContentKbSeedLoader` via redeploy |
| Monthly LLM cap exceeded | LOW | Cap enforcement stops further distill calls automatically; review `llm_spend_ledger` for the month; no data loss |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| is_visible/is_evergreen clobber on prod write | Direct prod-DB write phase | Integration test: set `is_visible=TRUE`, push, assert unchanged |
| Dangling artifactPath (partial write) | Direct prod-DB write phase | File-first ordering enforced; post-push audit query returns 0 broken rows |
| Secret leakage | Studio scaffold phase (earliest) | `git log --all -- "**/secrets.json"` returns nothing; grep committed files for credential patterns |
| Export includes unapproved entries | approval_status column + review queue phase | Seed row count equals approved count in UI |
| Re-distill LLM spend surprise | Distill UI phase | Dry-run confirmation shown before every distill; re-distill of known-distilled video shows warning |
| AngleSharp concurrency in Studio | Channel browse phase | SemaphoreSlim(1) in lister service; no Task.WhenAll over lister calls |
| Blazor circuit blocking/orphan | Harvest/distill UI wiring phase | Background Task + InvokeAsync(StateHasChanged) pattern; CTS disposed on component teardown |
| git push to main without review | Commit-then-deploy phase | Two-stage commit/push with diff display; push button requires checkbox acknowledge |
| Schema drift local vs prod | approval_status migration phase | EnsureSchemaAsync verified on Postgres connection at Studio startup; column presence check logged |
| CRLF in seed JSON | Commit-then-deploy phase | `file index-seed.json` reports LF only after Studio publish on Windows |
| Admin grid count/aggregate bottleneck | Admin grid AJAX phase | `/Admin/Harvest` initial load does not fire count/aggregate queries synchronously; AJAX page click fires one query with LIMIT/OFFSET |

---

## Sources

- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` — `UpsertSql` (line 558), `UpsertPreservingVisibilitySql` (line 598): confirmed `is_visible`/`is_evergreen` behavior per overload
- `DeckFlow.CLI/ContentKbCommandRunners.cs` — `RunContentIndexExportAsync` (line 354): confirmed `GetAllRowsAsync()` with no approval filter; `RunDistillAsync` video-IDs bypass (line 455-459); spend-per-call recording order (line 1032-1041 comment)
- `DeckFlow.Web/Services/ContentKbSeedLoader.cs` — `UpsertRowPreservingVisibilityAsync` confirmed as the deploy-time upsert
- `DeckFlow.Web/Controllers/Admin/AdminHarvestController.cs` — `Index` (line 97-100): two separate synchronous queries (`GetDistinctProcessedCommanderCountAsync` + `GetPagedProcessedCommandersAsync`) on every page load
- `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` — `GetDistinctProcessedCommanderCountAsync` (line 396): `COUNT(DISTINCT LOWER(commander_name))` full table scan; `GetPagedProcessedCommandersAsync` (line 368): GROUP BY LOWER with LIMIT/OFFSET
- PROJECT.md — "public repo: no secrets in commits ever"; `CLAUDE.md` — "do not push to main/master" (AI rule); commit conventions
- MEMORY note `feedback_codex_codes_claude_reviews.md` + `harvest_lister_concurrency_crash.md` (resolved 2026-06-08)
- STACK.md — AngleSharp concurrency constraint, SCP path architecture, user-secrets recommendation
- FEATURES.md — approval_status Option B recommendation, AJAX grid paging Option 1 recommendation

---
*Pitfalls research for: DeckFlow v1.7 Local Harvest & Publish Studio*
*Researched: 2026-06-13*
