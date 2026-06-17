# Stack Research

**Domain:** Local harvest-and-publish studio tool for a .NET 10 codebase
**Researched:** 2026-06-13
**Confidence:** HIGH (existing codebase verified directly; external APIs verified against official docs)

---

## Scope

This research covers ONLY the NEW standalone local tool for v1.7. The existing deployed
stack (.NET 10, ASP.NET Core Razor MVC, RestSharp/Polly, Npgsql, SQLite) is pinned and
not re-researched here. All recommendations must be compatible with `<ProjectReference>`
to `DeckFlow.Core`.

---

## Existing YouTube Library (Already in DeckFlow.Core)

**YoutubeExplode 6.6.0** — already a dependency in `DeckFlow.Core.csproj`.

`YouTubeChannelVideoLister` and `YouTubeTranscriptFetcher` in
`DeckFlow.Core/Integration/` use it to:
- List recent channel uploads (`youtube.Channels.GetUploadsAsync`)
- Fetch video metadata by ID (`youtube.Videos.GetAsync`)
- Fetch closed captions (`youtube.Videos.ClosedCaptions`)

YoutubeExplode 6.x ALSO supports keyword search via
`youtube.Search.GetResultBatchesAsync("query")`, returning
`VideoSearchResult`, `ChannelSearchResult`, and `PlaylistSearchResult`.
This is the discovery vector v1.7 needs.

**YoutubeExplode does NOT use the YouTube Data API v3.** It reverse-engineers
YouTube's internal web endpoints. No API key required, no quota.

**Recommendation: reuse YoutubeExplode 6.6.0 for ALL YouTube operations** —
listing, search, and video-by-ID. Adding `Google.Apis.YouTube.v3` would be
additive cost and complexity for zero gain given YoutubeExplode already covers
every required operation. See "Alternatives Considered" for the full comparison.

---

## 1. UI Host for the New Standalone Local Tool

### Recommendation: ASP.NET Core Minimal-API + Blazor Server, run locally as `dotnet run`

**Rationale:**

The tool runs on localhost only (`http://localhost:<port>`). "Standalone" means
`dotnet run --project DeckFlow.Studio` opens a browser, not that it must be a
native .exe with embedded WebView. A self-hosted ASP.NET Core + Blazor Server
app satisfies all constraints:

- **Reuse of Core stores**: `<ProjectReference>` to `DeckFlow.Core` — same
  `ContentVideoStore`, `ContentSiteIndexStore`, `ContentSourceStore`, all stores
  compile in. No adapter layer.
- **Lowest new surface**: Blazor Server is already the in-org skill (DeckFlow.Web
  is ASP.NET Core MVC/Razor). Blazor's component model is familiar. No WPF
  XAML, no MAUI, no Photino native interop.
- **Windows-first, WSL-friendly**: `dotnet run` works in both WSL2 and Windows
  CMD/PowerShell. Chromium/Edge opens the localhost URL. No installer, no
  NativeAOT, no app store.
- **Rich async UI**: harvest + distill are long-running async pipelines. Blazor
  Server's SignalR connection handles real-time progress updates trivially via
  `StateHasChanged()`. WPF/WinForms would need Dispatcher marshaling.
- **No deployment needed**: this tool never ships to Render. Binaries stay local.
  Zero concern about the 512MB Render RAM cap.
- **Minimal friction**: `dotnet new blazorserver` in `DeckFlow.Studio/`, add
  `<ProjectReference>`, wire the existing stores into DI. First page renders in
  ~30 min.

**Project structure:**

```
DeckFlow.Studio/
  DeckFlow.Studio.csproj        ← net10.0, Sdk="Microsoft.NET.Sdk.Web"
  Program.cs                    ← AddDeckFlowStudio() DI, app.Run()
  Components/                   ← Blazor .razor components
  Pages/                        ← Blazor page routes
```

**Add to solution:** `dotnet sln add DeckFlow.Studio/DeckFlow.Studio.csproj`

**Excluded from Dockerfile** — the Studio project is never published in the
container build. Add to `Dockerfile` restore COPY only for `DeckFlow.Core` and
`DeckFlow.Web`; ignore Studio.

### Alternatives Considered

| Option | Why Not |
|--------|---------|
| **WPF / WinForms** | Windows-only (acceptable), but XAML + Dispatcher async is higher friction than Blazor for a data-heavy tool. No code reuse with existing Razor knowledge. |
| **Blazor Hybrid (WPF host)** | Adds WPF dependency just to host a WebView. Blazor Server running in a browser tab is simpler and equivalent for a local tool. |
| **MAUI** | Cross-platform overhead, MAUI workload install, iOS/Android surface area. Overkill for a Windows-only CLI-owner tool. |
| **Avalonia** | Excellent cross-plat, but introduces a new XAML dialect (no existing Razor/MVC skill). Adds a new dependency with no existing project pattern. |
| **Photino** | Lightweight native-window host for Blazor. No .NET 10 prebuilt binaries verified at research time; small community. Riskier than plain Blazor Server. |
| **Separate SPA (React/Vue) + API** | TypeScript tooling already exists in DeckFlow.Web, but a full SPA adds build pipeline, npm deps, and cross-process IPC. More complexity than Blazor Server. |

---

## 2. YouTube Discovery

### Library: YoutubeExplode 6.6.0 (already in DeckFlow.Core — NO NEW DEP)

**Operations available and their equivalent quota cost:**

| Operation | YoutubeExplode method | YouTube Data API v3 equivalent | API v3 quota cost |
|-----------|----------------------|-------------------------------|-------------------|
| Channel video listing | `youtube.Channels.GetUploadsAsync(channelId)` | `playlistItems.list` | 1 unit/page |
| Video metadata by ID | `youtube.Videos.GetAsync(videoId)` | `videos.list` | 1 unit/page |
| Keyword search | `youtube.Search.GetResultBatchesAsync("query")` | `search.list` | **100 units/call** (separate bucket, 100 calls/day max) |
| Channel info by handle | `youtube.Channels.GetByHandleAsync(handle)` | `channels.list` | 1 unit/call |

**Key quota fact (YouTube Data API v3):** `search.list` has its own dedicated
quota bucket of 100 calls/day, with each call costing 100 units against the
general 10,000-unit/day pool. At 10 calls/day of search, you exhaust 1,000
general units AND burn 10% of the dedicated search bucket. For a local curation
tool doing daily discovery, this is tight.

**YoutubeExplode has no quota.** It scrapes YouTube's internal web API. This
means search, channel listing, and video metadata are unlimited for a
single-operator tool. The existing concurrency constraint (serialized to 1 due
to AngleSharp shared static state — see comment in `YouTubeChannelVideoLister.cs`)
applies only to the metadata lookup phase, not listing/search.

**Auth model for Google.Apis.YouTube.v3 (for reference only):**
- API key (server-side): sufficient for read-only search/listing; no user OAuth
  needed for public data.
- OAuth 2.0: required only if accessing private data or user's channel. Not
  needed here.

**Recommendation: do not add `Google.Apis.YouTube.v3`.** YoutubeExplode already
covers every v1.7 operation with no quota ceiling and no new dependency.

---

## 3. Git Automation for Commit-Then-Deploy Path

### Recommendation: Shell out to `git` (Process.Start)

**Rationale:**

The commit-then-deploy publish path produces one commit per publish event:
stage `content-kb/seed/index-seed.json` + markdown artifacts, commit, push to
`main`. This is a low-frequency, single-repo, single-author operation.

**Shell out is simpler and safer here:**

- No new NuGet package.
- `git` is guaranteed to be on PATH in WSL2 and Windows (this codebase already
  uses `git` CLI everywhere — Codex, GSD scripts, etc.).
- Authentication (SSH key or Windows Credential Manager) is already configured
  in the dev environment for pushing to GitHub. Shelling out inherits that
  config automatically. LibGit2Sharp requires re-implementing credential
  resolution (SSH agent, credential helper) in managed code.
- `ProcessOutput` helper already exists in `DeckFlow.Core/Integration/ProcessOutput.cs`
  and `FfmpegAudioChunker.cs` demonstrates the process-spawn pattern. Reuse it.
- Error output from `git` is human-readable and already what the developer
  expects to see in the UI.

**LibGit2Sharp tradeoffs:**

LibGit2Sharp 0.31.0 (published 2024-12-03) targets net8.0+ and is compatible
with .NET 10. It supports stage/commit/push. However:

- Push with SSH requires native `libgit2` SSH bindings, which have had
  intermittent issues on WSL2/Windows depending on the build.
- Credential helper passthrough is not automatic — you must explicitly wire
  `CredentialsHandler` callbacks.
- For a UI tool where the user watches a log panel, shelling out gives the
  exact same feedback as a terminal would, which is what the developer expects.

**Use LibGit2Sharp if:** you need programmatic diff inspection, branch management,
or status queries without spawning processes. For v1.7's simple stage-commit-push
loop, shelling out wins on simplicity.

**Shell-out pattern (reuse existing `ProcessOutput`):**

```csharp
// Stage
await RunGitAsync("add", "content-kb/seed/index-seed.json", repoRoot);
await RunGitAsync("add", "content-kb/artifacts/", repoRoot);
// Commit
await RunGitAsync("commit", "-m", "content(kb): publish approved entries", repoRoot);
// Push
await RunGitAsync("push", "origin", "main", repoRoot);
```

---

## 4. Direct Prod-Write Path

### 4a. Database (Npgsql — already in DeckFlow.Core)

**Verdict: fully supported.** `RelationalDatabaseConnection` with
`RelationalDatabaseProvider.Postgres` already takes a connection string at
construction time. The Studio tool passes the prod Render Postgres internal
connection string, and all existing store methods (`ContentSiteIndexStore`,
`ContentVideoStore`, etc.) work against it unchanged.

The `RunCorpusResetAsync` command in `ContentKbCommandRunners.cs` (line 159)
already demonstrates this pattern — it accepts `--connection-string` and
constructs Postgres stores directly.

No new code needed for DB writes. Just pass the creds.

### 4b. Markdown Artifact Files on Render /data Disk

**Verdict: SCP/SFTP is the mechanism. It works on the Starter plan. It is NOT a
zero-effort path — it requires SSH key setup and a shell-out per publish.**

**Findings (from Render docs):**

- Persistent disks are available on Starter ($7/mo) and above. DeckFlow already
  runs on Starter, so this is available.
- After setting up an SSH public key in Render Account Settings, you can SCP
  files to the disk-backed service:
  ```
  scp -s ./content-kb/artifacts/source-slug/video-id.md \
      SERVICE_ID@ssh.REGION.render.com:/data/content-kb/artifacts/source-slug/video-id.md
  ```
  (The `-s` flag uses SFTP protocol over the SSH connection.)
- There is **no Render REST API for writing files to /data**. SCP is the only
  remote write mechanism.
- The disk is accessible only at runtime of the running service instance. You
  are SCP-ing into the running web service container, not a separate storage
  endpoint.
- SSH access requires the public key to be registered in the Render dashboard.

**Operational implication:** the direct-push path for markdown artifacts requires:
1. Render SSH key configured (one-time setup).
2. A shell-out to `scp` per artifact file OR a `tar` bundle + single SCP.
3. The Studio tool must know the Render service SSH address
   (`<service-id>.ssh.<region>.render.com`).

**Recommendation for v1.7:** make the commit-then-deploy path the **primary
publish path** and treat direct prod-DB + SCP as the **secondary/power-user
path**. The SCP path works but has more moving parts (SSH key registration,
service address configuration, no retry/resume built in). The seed commit path
is simpler and already proven.

For the direct path, the DB write is easy (existing Npgsql stores); the markdown
SCP is achievable but adds complexity. Consider implementing it as two separate
actions in the UI: "Push DB" (Npgsql, easy) and "Push Artifacts" (SCP,
requires SSH config).

---

## 5. Secret Handling for the Local Tool

**Context:** public repo (`luntc1972/DeckFlow`), Windows dev machine, the Studio
project holds prod Postgres creds + (optionally) a YouTube API key.

### Recommendation: `dotnet user-secrets` via `Microsoft.Extensions.Configuration.UserSecrets`

**Why:**

- `dotnet user-secrets` stores secrets in
  `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` on Windows —
  outside the repo tree, never committed.
- The `.csproj` gets a `<UserSecretsId>` GUID element only; no secret values
  appear in any tracked file.
- Works in non-ASP.NET projects: add
  `Microsoft.Extensions.Configuration.UserSecrets` (already transitively
  available in any .NET 10 app using `Microsoft.Extensions.Configuration`)
  and call `.AddUserSecrets<Program>()` on `ConfigurationBuilder`.
- Consistent with how the deployed app handles secrets (env vars in Render
  dashboard, `sync: false`). Local dev uses user-secrets; CI/Render uses
  environment variables.
- No new `appsettings.local.json` pattern that could accidentally be committed
  if `.gitignore` drifts.

**What NOT to do:**

- Do NOT create `appsettings.Production.json` or any local override file with
  secrets — already in the "Do Not Modify" list.
- Do NOT read from `.env` files — no existing pattern in this project.
- Do NOT hardcode the Render Postgres connection string in any source file.

**Setup (one-time per developer):**

```bash
cd DeckFlow.Studio
dotnet user-secrets init
dotnet user-secrets set "Studio:ProdConnectionString" "postgres://..."
dotnet user-secrets set "Studio:RenderSshAddress" "srv-xxx@ssh.oregon.render.com"
# YouTube API key only needed if Google.Apis.YouTube.v3 is ever added
# dotnet user-secrets set "Studio:YouTubeApiKey" "AIza..."
```

**In Program.cs:**

```csharp
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()   // allows CI/Render to override
    .Build();
```

---

## Recommended Stack Summary

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| ASP.NET Core + Blazor Server | 10.0 (in-solution) | UI host for DeckFlow.Studio | Reuses existing .NET/Razor skill; trivial async UI updates via SignalR; `<ProjectReference>` to Core; runs as `dotnet run` on localhost |
| YoutubeExplode | 6.6.0 (already in Core) | YouTube channel listing, video metadata, keyword search | Already present; covers all v1.7 discovery ops; no API key; no quota |
| Npgsql | 10.0.0 (already in Core) | Direct prod Postgres writes via existing stores | Zero new code; `RelationalDatabaseConnection` already accepts a connection string |
| git (CLI, shelled out) | system git | Commit-then-deploy seed export | Inherits SSH auth; zero new deps; `ProcessOutput` pattern already in Core |
| Microsoft.Extensions.Configuration.UserSecrets | 10.x (transitively available) | Local secret storage (prod creds) | Keeps secrets outside repo; consistent with Render env-var pattern |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Serilog.AspNetCore | 9.0.0 (already in Web) | Structured logging in Studio | Wire same Serilog config as DeckFlow.CLI for consistent log format |
| LibGit2Sharp | 0.31.0 | Programmatic git operations | Only if shell-out git proves insufficient (e.g., status queries for UI display) |
| SCP/SFTP (via `scp` CLI) | system scp | Upload markdown artifacts to Render /data | Only for "Push Artifacts" secondary path; shell out, same pattern as git |

### What NOT to Add

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `Google.Apis.YouTube.v3` | YoutubeExplode 6.6.0 already in Core covers every needed operation at zero quota cost; adds 5+ transitive Google deps | YoutubeExplode already in Core |
| `Microsoft.Extensions.Http.Resilience` standard handler | Project constraint: use existing RestSharp + Polly v8 direct pattern; standard handler has known incompatibility with existing pattern | RestSharp + Polly v8 (existing) |
| MAUI / WPF host for Blazor Hybrid | Unnecessary wrapper; Blazor Server in browser tab is simpler for a localhost tool | ASP.NET Core + Blazor Server |
| `.env` files | No existing project pattern; risk of accidental commit | `dotnet user-secrets` |
| Render REST API for /data writes | Does not exist — no API endpoint for persistent disk file writes | SCP via SSH (after SSH key setup) |

---

## Installation

```bash
# Create the Studio project
dotnet new blazorserver -o DeckFlow.Studio --no-restore
dotnet sln add DeckFlow.Studio/DeckFlow.Studio.csproj

# Add Core reference (no new NuGet packages needed for Core reuse)
# In DeckFlow.Studio.csproj:
#   <ProjectReference Include="..\DeckFlow.Core\DeckFlow.Core.csproj" />

# Initialize user-secrets for local secret storage
cd DeckFlow.Studio
dotnet user-secrets init

# Set prod secrets (one-time per developer; never committed)
dotnet user-secrets set "Studio:ProdConnectionString" "<render-postgres-url>"
dotnet user-secrets set "Studio:RenderSshAddress" "<service-id>@ssh.oregon.render.com"
```

No new NuGet packages are required beyond what is already in the solution.
`YoutubeExplode`, `Npgsql`, `RestSharp`, `Polly`, `Serilog` all come in
via `DeckFlow.Core`'s existing deps.

---

## Version Compatibility

| Package | Version | .NET 10 status |
|---------|---------|----------------|
| YoutubeExplode | 6.6.0 | Compatible (targets netstandard2.0 / net5.0+) |
| LibGit2Sharp | 0.31.0 | Compatible (targets net8.0+, computed net10.0 support) |
| Npgsql | 10.0.0 | Native .NET 10 release |
| Microsoft.Data.Sqlite | 10.0.0 | Native .NET 10 release |

---

## Key Architectural Constraints Carried Forward

- **ScryfallThrottle**: static `SemaphoreSlim` in DeckFlow.Web — do not reference
  from Studio. Studio does not call Scryfall.
- **AngleSharp concurrency bug**: `YouTubeChannelVideoLister` must remain
  serialized (concurrency = 1). Do not parallelize the metadata lookup phase in
  the Studio UI.
- **Public repo invariant**: `DeckFlow.Studio/` must be excluded from `.gitignore`
  compiled-output dirs (`obj/`, `bin/`) and user-secrets must never surface in
  tracked files.
- **Render /data SCP path requires SSH key registered in Render dashboard**:
  this is a one-time setup gate. Document in a `DeckFlow.Studio/README-setup.md`
  (not committed to the public repo's root).

---

## Sources

- `DeckFlow.Core/DeckFlow.Core.csproj` — verified YoutubeExplode 6.6.0, Npgsql 10.0.0 as existing deps
- `DeckFlow.Core/Integration/YouTubeChannelVideoLister.cs` — verified YoutubeExplode search capability and concurrency constraint
- `DeckFlow.CLI/ContentKbCommandRunners.cs` — verified `--connection-string` Postgres pattern for direct DB writes
- `render.yaml` / `Dockerfile` — verified Starter plan disk, /data mount, no Render API for file writes
- [Render Persistent Disks docs](https://render.com/docs/disks) — confirmed SCP is the file upload mechanism; no REST API for /data; Starter plan supported
- [Render SSH docs](https://render.com/docs/ssh) — confirmed SSH available on paid plans (Starter+); SCP with `-s` flag is supported
- [YouTube Data API v3 quota cost table](https://developers.google.com/youtube/v3/determine_quota_cost) — confirmed `search.list` = 100 units/call, dedicated 100-calls/day bucket
- [NuGet LibGit2Sharp 0.31.0](https://www.nuget.org/packages/LibGit2Sharp/) — confirmed .NET 10 compatibility, published 2024-12-03
- [Microsoft Blazor Hybrid docs](https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/) — confirmed WPF/MAUI host options; verified Blazor Server simpler for localhost-only tool
- [Microsoft user-secrets docs](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) — confirmed works in non-ASP.NET projects via `ConfigurationBuilder.AddUserSecrets<Program>()`

---
*Stack research for: DeckFlow v1.7 Local Harvest & Publish Studio — new standalone local tool*
*Researched: 2026-06-13*
