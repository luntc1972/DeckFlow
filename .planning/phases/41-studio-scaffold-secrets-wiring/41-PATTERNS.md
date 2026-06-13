# Phase 41: Studio Scaffold + Secrets Wiring - Pattern Map

**Mapped:** 2026-06-13
**Files analyzed:** 6 new files to create + 2 existing files to modify
**Analogs found:** 6 / 6 (all from DeckFlow.Web)

---

## File Classification

| New / Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---------------------|------|-----------|----------------|---------------|
| `DeckFlow.Studio/DeckFlow.Studio.csproj` | config | — | `DeckFlow.Web/DeckFlow.Web.csproj` | exact (same SDK, same TFM, same Core ref pattern) |
| `DeckFlow.Studio/Program.cs` | config / composition-root | request-response | `DeckFlow.Web/Program.cs` (lines 1–63) | role-match (Blazor wiring differs; startup / Serilog console pattern identical) |
| `DeckFlow.Studio/Properties/launchSettings.json` | config | — | `DeckFlow.Web/Properties/launchSettings.json` | exact (same profile shape; different port) |
| `DeckFlow.Studio/Components/Pages/Home.razor` | component | request-response | none (first Blazor page in repo) | no analog — use Research pattern |
| `DeckFlow.Studio/appsettings.json` | config | — | `DeckFlow.Web/appsettings.json` | exact (logging-only config, same structure) |
| `DeckFlow.Studio/appsettings.Development.json` | config | — | `DeckFlow.Web/appsettings.Development.json` | exact (logging override only) |
| `.gitignore` (modified) | config | — | `.gitignore` (existing) | append-only |
| `Dockerfile` (comment-only) | config | — | `Dockerfile` lines 29 (existing) | comment insertion only |

---

## Pattern Assignments

### `DeckFlow.Studio/DeckFlow.Studio.csproj` (config)

**Analog:** `DeckFlow.Web/DeckFlow.Web.csproj`

**Required `<PropertyGroup>` block** — copy these four properties exactly, ignore all DeckFlow.Web extras
(Web.csproj lines 34–41):

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <UserSecretsId><!-- GUID inserted by: dotnet user-secrets init --project DeckFlow.Studio/DeckFlow.Studio.csproj --></UserSecretsId>
</PropertyGroup>
```

**CRITICAL PITFALL — template defaults to net6.0:** The `dotnet new blazorserver` template on this
machine generates `<TargetFramework>net6.0</TargetFramework>`. This must be changed to `net10.0`
before `dotnet sln add` or the first build. Failure to do so produces:
`error NU1202: Package DeckFlow.Core ... is not compatible with net6.0`

**Required `<ItemGroup>` block** — ProjectReference pattern (Web.csproj line 4):

```xml
<ItemGroup>
  <ProjectReference Include="..\DeckFlow.Core\DeckFlow.Core.csproj" />
</ItemGroup>
```

**What to strip from the template-generated csproj:**
- Any `<PackageReference>` blocks (no new packages; everything arrives transitively via Core)
- Any `<TargetFramework>net6.0</TargetFramework>` (replace with net10.0)
- Any `<TypeScriptCompileBlocked>` or `CompileTypeScriptAssets` targets (Studio has no TS)
- Any `ZipDeckFlowBridge` target (Web-only)
- Any `<GenerateDocumentationFile>` (not required for a local tool)

**Minimal final shape:**

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId><!-- GUID from dotnet user-secrets init --></UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\DeckFlow.Core\DeckFlow.Core.csproj" />
  </ItemGroup>

</Project>
```

---

### `DeckFlow.Studio/Program.cs` (composition root)

**Analog:** `DeckFlow.Web/Program.cs` lines 1–63

**Imports pattern** — minimal set for Phase 41 Studio startup. Do NOT copy the Web import block
(30+ using directives for MVC, Polly, RestSharp, etc. — none apply to Studio):

```csharp
using Serilog;
// DeckFlow.Core references added in later phases when stores are wired
```

**Top-level class pattern** — Web.csproj uses `public partial class Program` + `static async Task Main`.
Studio should match:

```csharp
namespace DeckFlow.Studio;

/// <summary>
/// Configures and starts the DeckFlow Studio local Blazor Server application.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Entry point for the Studio host.
    /// </summary>
    public static async Task Main(string[] args)
    {
        try
        {
            // ... startup ...
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "DeckFlow Studio host terminated during startup or run.");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
```

**Serilog pattern — console-only for Phase 41** (Web.csproj lines 50–63 use both console + file sink;
Studio uses console only per RESEARCH.md Open Question 2 recommendation):

```csharp
// Source analog: DeckFlow.Web/Program.cs lines 50-63 (console portion only)
// Why: Phase 41 is scaffold-only; full Serilog file-sink wiring deferred to Phase 45
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});
```

**User-secrets + environment variable wiring pattern** (from RESEARCH.md Pattern 2 — no analog in repo
today; first use of AddUserSecrets in this codebase):

```csharp
// User-secrets for local dev; env vars override in CI (where user-secrets are unavailable)
builder.Configuration
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

// Read presence only — NEVER log or bind the connection string value directly
var prodConnStr = builder.Configuration["Studio:ProdConnectionString"];
var isProdConfigured = !string.IsNullOrEmpty(prodConnStr);

// STU-03: log presence flag, never the value
Log.Information("Studio prod connection: {Status}",
    isProdConfigured ? "configured" : "not configured");
```

**Startup log pattern** — match Web's `Log.Fatal` / `Log.CloseAndFlushAsync` wrapping exactly
(Web.csproj lines 484–490):

```csharp
// Source analog: DeckFlow.Web/Program.cs lines 484-490
catch (Exception exception)
{
    Log.Fatal(exception, "DeckFlow Studio host terminated during startup or run.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

**Blazor Server service registration** — the `dotnet new blazorserver` template generates:

```csharp
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
```

Keep both. Do NOT add MVC (`AddControllersWithViews`) — Studio is Blazor-only.

**Error handling note:** Web.csproj uses `app.UseExceptionHandler("/Deck")`. Studio should use
the default exception handler or a plain Blazor error boundary — no `UseExceptionHandler` route
needed in Phase 41.

---

### `DeckFlow.Studio/Properties/launchSettings.json` (config)

**Analog:** `DeckFlow.Web/Properties/launchSettings.json` (all lines)

**Copy pattern** — Web's file (lines 1–47) has four profiles: IIS Express, http, https, IIS.
Studio needs only one: `http`. No IIS profiles (local tool, not an IIS app). No https profile
(localhost-only tool; TLS adds no value):

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5271",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**Key differences from the Web analog:**
- `launchBrowser: false` — MUST remain false (MEMORY: "never auto-launch DeckFlow web; ask user to start" applies equally to Studio)
- Port `5271` — avoids collision with Web's `5173`/`7173`
- No `iisSettings` block — not needed for a local dotnet run tool
- No `https` profile — localhost-only; no TLS

---

### `DeckFlow.Studio/Components/Pages/Home.razor` (component)

**Analog:** None — this is the first Blazor page in the repo.

Use RESEARCH.md Open Question 3 pattern: a single stub page that confirms secrets wiring status.

```razor
@page "/"

<PageTitle>DeckFlow Studio</PageTitle>

<h1>DeckFlow Studio</h1>

<p>Studio is running.</p>
<p>Prod connection: <strong>@(_isProdConfigured ? "configured" : "not configured")</strong></p>

@code {
    [Inject] private StudioConfig Config { get; set; } = default!;

    private bool _isProdConfigured;

    protected override void OnInitialized()
    {
        _isProdConfigured = Config.IsProdConfigured;
    }
}
```

`StudioConfig` is a simple `record` registered as a singleton in `Program.cs`. It carries
`IsProdConfigured` (bool) — the value is derived from the presence of the secret, never the secret
value itself (STU-03).

---

### `.gitignore` (append-only modification)

**Analog:** Current `.gitignore` (all 26 lines, verified 2026-06-13)

**Gap confirmed:** The existing `.gitignore` has NO entry for `appsettings.Development.json`,
`appsettings*.local.json`, or `secrets.json` under any project path. The `blazorserver` template
generates `DeckFlow.Studio/appsettings.Development.json` unconditionally. If a developer adds a
connection string to that file to test, `git add .` will stage it.

**Entries to append** — add BEFORE the first `git add` of any Studio file:

```gitignore
# DeckFlow.Studio — local-tool secrets and generated artifacts
# appsettings.Development.json is committed (logging config only).
# These patterns cover files that could carry secrets.
DeckFlow.Studio/appsettings*.local.json
DeckFlow.Studio/appsettings.Local.json
DeckFlow.Studio/secrets.json
```

**What is NOT ignored** (must be committed):
- `DeckFlow.Studio/appsettings.json` — logging/Kestrel config, no secrets
- `DeckFlow.Studio/appsettings.Development.json` — logging override only, no secrets
- `DeckFlow.Studio/DeckFlow.Studio.csproj` — `<UserSecretsId>` GUID is public and safe

**Append location:** After line 26 (end of current file), in a new named section block.

---

### `Dockerfile` (comment-only modification)

**Analog:** `Dockerfile` line 29 (current):

```dockerfile
RUN dotnet restore DeckFlow.Web/DeckFlow.Web.csproj
```

**Required change:** Insert an inline comment on that line explaining the SC4 constraint. The
csproj COPY block (lines 24–26) only copies Core, Web, and CLI — not Studio. After `dotnet sln add`
adds Studio to `DeckFlow.sln`, the copied `.sln` will reference Studio but since no Studio
`.csproj` is COPY'd, the project-scoped restore still works. This is a trap for future editors.

```dockerfile
# Restore only Web (and its Core/CLI transitive deps) — Studio is local-only, not deployed.
# Do NOT change this to `dotnet restore DeckFlow.sln`; Studio packages would break the build stage.
RUN dotnet restore DeckFlow.Web/DeckFlow.Web.csproj
```

No other Dockerfile changes. SC4 requires this line remain unchanged in behavior.

---

## Shared Patterns

### Nullable + ImplicitUsings enforcement
**Source:** `DeckFlow.Web/DeckFlow.Web.csproj` lines 35–37
**Apply to:** `DeckFlow.Studio.csproj`

```xml
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

Both must be present in Studio's csproj. The `blazorserver` template may or may not include
`ImplicitUsings`; verify and add if absent.

### Fatal startup catch + CloseAndFlushAsync
**Source:** `DeckFlow.Web/Program.cs` lines 484–490
**Apply to:** `DeckFlow.Studio/Program.cs`

Wrap the entire `Main` body in `try { ... } catch (Exception) { Log.Fatal(...) } finally { await Log.CloseAndFlushAsync() }`. Web uses this exact shape; copy it verbatim.

### `launchBrowser: false`
**Source:** `DeckFlow.Web/Properties/launchSettings.json` lines 6, 13, 21
**Apply to:** `DeckFlow.Studio/Properties/launchSettings.json`

Every profile in Web has `"launchBrowser": false`. Studio's single `http` profile must match.
This is a project-level rule, not just a DeckFlow.Web rule.

### STU-03 secret-safe logging
**Source:** RESEARCH.md Pattern 2 (no codebase analog yet)
**Apply to:** `DeckFlow.Studio/Program.cs`

Log `"configured"` / `"not configured"` only. Never pass the connection string, or any
`PostgresConnectionStringNormalizer.Normalize()` output, to any `ILogger` or `Log.*` method.
The connection string must not appear in any log line, UI text, or error message.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `DeckFlow.Studio/Components/Pages/Home.razor` | component | request-response | No Blazor pages exist in repo; closest MVC analog is Razor views but Blazor component syntax is sufficiently different that the RESEARCH.md stub is more reliable |
| User-secrets wiring (`AddUserSecrets<Program>()`) | config pattern | — | No `AddUserSecrets` call exists anywhere in the repo today; the pattern is from RESEARCH.md / Microsoft docs |

---

## Critical Pre-Commit Checklist (for planner to include as plan tasks)

These items must happen in this exact order to avoid the two confirmed pitfalls:

1. `dotnet new blazorserver --output DeckFlow.Studio --name DeckFlow.Studio`
2. **Immediately** edit `DeckFlow.Studio.csproj`: set `net10.0`, add `Nullable`/`ImplicitUsings`, add Core `<ProjectReference>`, strip demo `<PackageReference>` blocks
3. **Immediately** add `.gitignore` entries (before any `git add`)
4. `dotnet user-secrets init --project DeckFlow.Studio/DeckFlow.Studio.csproj` (writes `<UserSecretsId>` to csproj)
5. `dotnet sln DeckFlow.sln add DeckFlow.Studio/DeckFlow.Studio.csproj`
6. Delete template demo files: `Data/`, `Pages/Counter.razor`, `Pages/FetchData.razor`, `Shared/SurveyPrompt.razor`, `Pages/Index.razor`
7. Create `Components/Pages/Home.razor` stub
8. Wire `Program.cs` (Serilog console, `AddUserSecrets`, `StudioConfig` singleton)
9. Add Dockerfile comment (no behavior change)
10. `dotnet build DeckFlow.sln` — must be 0 errors, 0 new warnings

---

## Metadata

**Analog search scope:** `DeckFlow.Web/` (csproj, launchSettings.json, Program.cs), `.gitignore`, `Dockerfile`
**Files scanned:** 5
**Pattern extraction date:** 2026-06-13
