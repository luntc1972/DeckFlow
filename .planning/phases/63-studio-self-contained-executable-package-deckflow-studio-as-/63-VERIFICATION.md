---
phase: 63-studio-self-contained-executable
verified: 2026-06-20T18:15:00Z
status: passed
score: 7/7 must-haves verified
overrides_applied: 0
operator_checkpoint: APPROVED-by-operator
---

# Phase 63: Studio Self-Contained Executable — Verification Report

**Phase Goal:** The operator can run DeckFlow.Studio on a clean Windows box (no .NET installed) by
launching a single self-contained `win-x64` executable produced by a repeatable, documented publish
step. Closes DIST-01.

**Verified:** 2026-06-20T18:15:00Z
**Status:** PASSED
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Selecting the win-x64-selfcontained publish profile produces a single DeckFlow.Studio.exe (>50 MB) with wwwroot/ beside it and no loose managed *.dll next to the exe | VERIFIED | `artifacts/studio-release/DeckFlow.Studio.exe` = 121,566,825 bytes (~115.9 MB); `wwwroot/` present; `find artifacts/studio-release -maxdepth 1 -name '*.dll'` returns empty; `e_sqlite3.dll` not loose (IncludeNativeLibrariesForSelfExtract embeds it) |
| 2 | A normal `dotnet build DeckFlow.Studio` (no profile) behaves exactly as before — no RID pin, no single-file, no self-contained applied by default | VERIFIED | `DeckFlow.Studio.csproj` has no `SelfContained`, `RuntimeIdentifier`, `PublishSingleFile`, or `PublishTrimmed` properties; grep returned no output. All publish properties are isolated to `win-x64-selfcontained.pubxml` only. |
| 3 | The publish script is re-runnable, cleans its output dir, and prints the artifact path + size | VERIFIED | `publish-studio.ps1` lines 34-38: `Remove-Item -Recurse -Force $AbsOutDir` then `New-Item -ItemType Directory`. `publish-studio.sh` lines 31-35: `rm -rf "$OUT_DIR"` then `mkdir -p`. Both scripts print exe MB + zip path/size at end. |
| 4 | STUDIO-SETUP.md documents publishing, launching, the wwwroot-beside-exe rule, the env-var secrets story, and the "basic flow needs no secrets" split | VERIFIED | Section "Run the standalone Windows executable" present (line 103+). Covers: ps1/sh scripts (lines 118-129), wwwroot-beside-exe rule (lines 150-168), http://localhost:5271 + auto-open browser (lines 169-183), logging/troubleshoot section (lines 185-205), "Basic flow needs no secrets" section (lines 207-219), env-var secrets table (lines 221-253), `_launch-studio.bat` example with not-committed warning (lines 239-253), ReadyToRun optional note (lines 261-269). |
| 5 | Serilog file sink + bootstrap logger present in Program.cs so packaged-exe crashes are written to `<data dir>/logs/` | VERIFIED | `Program.cs` lines 35-38: bootstrap `LoggerConfiguration().WriteTo.Console().WriteTo.File(logFilePath, ...).CreateBootstrapLogger()`; line 55: host-level `configuration.WriteTo.File(logFilePath, ...)`. `DeckFlow.Studio.csproj` line 22: `Serilog.Sinks.File Version="7.0.0"`. Commit `3bb054da`. |
| 6 | Auto-open default browser on startup (Production-only, suppressible via DECKFLOW_DISABLE_AUTO_BROWSER) | VERIFIED | `Program.cs` lines 167-191: `app.Lifetime.ApplicationStarted.Register(...)` guarded by `!app.Environment.IsDevelopment() && !disableAutoBrowser`; reads `IServerAddressesFeature` for the real bound URL; `Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true })`; exception-wrapped so launch failure cannot take down the host. Commit `dbe8f6b9`. |
| 7 | Operator clean-machine smoke — exe launches Studio UI on a no-.NET Windows box, http://localhost:5271, basic no-secrets flow works | VERIFIED (APPROVED-by-operator) | Task 5 operator checkpoint: operator approved the clean-machine smoke — standalone exe launched the Studio UI at http://localhost:5271 on a no-.NET Windows box and the basic no-secrets flow passed. Treated as approved per instruction. |

**Score: 7/7 truths verified**

---

### Required Artifacts

| Artifact | Expected | Status | Evidence |
|----------|----------|--------|----------|
| `DeckFlow.Studio/Properties/PublishProfiles/win-x64-selfcontained.pubxml` | DIST-01 publish profile with locked properties | VERIFIED | Exists; contains `<RuntimeIdentifier>win-x64</RuntimeIdentifier>`, `<SelfContained>true</SelfContained>`, `<PublishSingleFile>true</PublishSingleFile>`, `<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>`, `<PublishTrimmed>false</PublishTrimmed>`; ReadyToRun intentionally absent (comment notes default-off); no PublishDir (script-controlled). Commit `bec153f3`. |
| `scripts/publish-studio.ps1` | Primary publish script, Windows dotnet.exe, profile, zip, print | VERIFIED | Exists (>15 lines, 75 lines). References `PublishProfile=win-x64-selfcontained`; invokes `C:\Program Files\dotnet\dotnet.exe`; strips `*.pdb/*.xml/web.config/appsettings.Development.json`; `Compress-Archive` zip; prints exe MB + zip path. Commit `f3fddebc`. |
| `scripts/publish-studio.sh` | WSL bash wrapper, Windows dotnet.exe, same profile, zip | VERIFIED | Exists (>8 lines, 79 lines). `#!/usr/bin/env bash` + `set -euo pipefail`; preflight `[[ -x "$DOTNET" ]]` guard; invokes `/mnt/c/Program Files/dotnet/dotnet.exe`; strips same files; uses PowerShell `Compress-Archive` via `wslpath` for zip (zip(1) not in WSL); `bash -n` syntax-check passes. Commit `f3fddebc` + fix `0817584c`. |
| `DeckFlow.Studio/appsettings.json` | Kestrel endpoint pinned to localhost:5271 | VERIFIED | Valid JSON; `Kestrel.Endpoints.Http.Url = "http://localhost:5271"`; no secrets. Commit `bec153f3`. |
| `DeckFlow.Studio/STUDIO-SETUP.md` | "Run the standalone Windows executable" section | VERIFIED | Section present at line 103. Contains "standalone" (line 107), script names, wwwroot-beside-exe rule, port 5271, env-var secret names, "basic flow needs no secrets" sub-section. Commits `837186ab`, `3bb054da`, `dbe8f6b9`. |
| `DeckFlow.Studio/Program.cs` | Serilog file sink + bootstrap logger + auto-browser-open | VERIFIED | Bootstrap logger lines 35-38; host-level file sink line 55; auto-browser-open block lines 167-191; port-in-use plain-language message lines 201-209. |
| `DeckFlow.Studio/DeckFlow.Studio.csproj` | Serilog.Sinks.File added; NO publish properties | VERIFIED | `Serilog.Sinks.File Version="7.0.0"` at line 22; zero occurrences of SelfContained/RuntimeIdentifier/PublishSingleFile/PublishTrimmed/ReadyToRun. Commit `3bb054da`. |
| `README.md` | Pointer to publish-studio scripts + STUDIO-SETUP.md | VERIFIED | Lines 180-181: `scripts/publish-studio.ps1 — publishes DeckFlow.Studio as a self-contained win-x64 single-file executable... See DeckFlow.Studio/STUDIO-SETUP.md for full setup`. Commit `837186ab`. |
| `artifacts/studio-release/DeckFlow.Studio.exe` | >50 MB, wwwroot beside it, no loose *.dll | VERIFIED | 121,566,825 bytes (~115.9 MB); `wwwroot/`, `appsettings.json`, `DeckFlow.Studio.staticwebassets.endpoints.json` all present; no loose `*.dll` at root; no loose `e_sqlite3.dll`; `*.pdb/*.xml/web.config/appsettings.Development.json` stripped. |
| `artifacts/DeckFlowStudio-2026.06.20.zip` | Distribution zip produced | VERIFIED | 52,308,799 bytes (~49.9 MB). |

---

### Key Link Verification

| From | To | Via | Status | Evidence |
|------|----|-----|--------|----------|
| `scripts/publish-studio.ps1` | `win-x64-selfcontained.pubxml` | `-p:PublishProfile=win-x64-selfcontained` | WIRED | `publish-studio.ps1` line 44: `& $DotNet publish $CsprojPath -p:PublishProfile=win-x64-selfcontained -o $AbsOutDir` |
| `scripts/publish-studio.sh` | `win-x64-selfcontained.pubxml` | `-p:PublishProfile=win-x64-selfcontained` | WIRED | `publish-studio.sh` line 41: `"$DOTNET" publish "$CSPROJ" -p:PublishProfile=win-x64-selfcontained -o "$OUT_DIR"` |
| `Program.cs` bootstrap logger | `<data dir>/logs/studio-<date>.log` | `WriteTo.File(logFilePath, ...)` | WIRED | Lines 35-38: `logFilePath = Path.Combine(logDirectory, "studio-.log")`; `CreateBootstrapLogger()`. |
| `Program.cs` auto-browser | `IServerAddressesFeature` + `Process.Start` | `ApplicationStarted.Register` | WIRED | Lines 171-191: reads real bound address, normalizes wildcards to localhost, `Process.Start` with `UseShellExecute = true`. |

---

### Data-Flow Trace (Level 4)

Not applicable — this phase delivers packaging artifacts and infrastructure code (scripts, publish profile, docs, logging), not user-facing data-rendering components.

---

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| pubxml has all 5 locked properties | `grep -q '<SelfContained>true\|<PublishSingleFile>true\|<IncludeNativeLibrariesForSelfExtract>true\|<PublishTrimmed>false' pubxml` | All present | PASS |
| csproj has no publish properties | `grep "SelfContained\|RuntimeIdentifier\|PublishSingleFile" DeckFlow.Studio.csproj` | No output | PASS |
| appsettings.json is valid JSON with Kestrel 5271 | `python3 -c "import json; json.load(open(...))"` | Valid | PASS |
| publish-studio.sh passes bash syntax check | `bash -n scripts/publish-studio.sh` | exit 0 | PASS |
| Artifact exe >50 MB | `stat -c%s artifacts/studio-release/DeckFlow.Studio.exe` | 121,566,825 bytes | PASS |
| No loose *.dll in artifact root | `find artifacts/studio-release -maxdepth 1 -name '*.dll'` | Empty | PASS |
| No loose e_sqlite3.dll | `find artifacts/studio-release -maxdepth 1 -iname 'e_sqlite3.dll'` | Empty | PASS |
| zip exists | `ls artifacts/DeckFlowStudio-*.zip` | 52,308,799 bytes | PASS |
| WriteTo.File (bootstrap) in Program.cs | `grep -n "CreateBootstrapLogger"` | Line 38 | PASS |
| Auto-browser-open in Program.cs | `grep -n "DECKFLOW_DISABLE_AUTO_BROWSER"` | Lines 165/168 | PASS |

---

### Probe Execution

No `probe-*.sh` scripts are defined for Phase 63. The equivalent live proof is the Task 4 publish
run (artifact-shape assert) which passed during execution, and the Task 5 operator clean-machine
smoke which is APPROVED-by-operator.

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| DIST-01 | 63-01-PLAN.md | Self-contained single-file win-x64 exe, clean Windows box, repeatable publish, documented | SATISFIED | Publish profile, scripts, appsettings pin, docs, artifact shape (115.9 MB exe + wwwroot + no loose DLLs), and operator clean-machine smoke all verified. `.planning/REQUIREMENTS.md` line 45. |

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `DeckFlow.Studio/STUDIO-SETUP.md` | 180 | `XXXX` | Info | Not a debt marker — context is a documented URL example (`ASPNETCORE_URLS=http://localhost:XXXX`) showing a placeholder port for operator reference. Not a TODO/FIXME/TBD. No action needed. |

No `TODO`, `FIXME`, `XXX`, or `TBD` markers found in any Phase 63 modified files. No stub returns, no placeholder UI, no hardcoded empty data. No Dockerfile or render.yaml edits. No secrets committed.

---

### Human Verification Required

None. The only human-in-the-loop item was the Task 5 operator clean-machine smoke, which has been
APPROVED-by-operator per the verification instruction. All automated checks pass. No additional
human verification items remain.

---

## Gaps Summary

No gaps. All 7 must-have truths verified. All required artifacts exist and are substantive and
wired. DIST-01 is satisfied.

**Two post-execution commits by the operator** (`3bb054da` and `dbe8f6b9`) added the Serilog file
sink/bootstrap logger and auto-browser-open respectively. Both are present in
`DeckFlow.Studio/Program.cs` and `DeckFlow.Studio.csproj` and have been verified against source.
These improve the operator experience of the packaged exe (crash diagnosability, desktop-app feel)
without altering the core DIST-01 deliverable shape.

---

## Commit Record (Phase 63 on cycle10 branch)

| Commit | Description |
|--------|-------------|
| `bec153f3` | chore(63): add win-x64-selfcontained publish profile and pin Kestrel port |
| `f3fddebc` | chore(63): add publish-studio scripts (ps1 primary + sh WSL wrapper) |
| `837186ab` | docs(63): add standalone-exe section to STUDIO-SETUP.md + README pointer |
| `0817584c` | fix(63): use PowerShell Compress-Archive in publish-studio.sh for zip step |
| `3bb054da` | fix(63): add Serilog file sink + bootstrap logger (post-execution operator fix) |
| `dbe8f6b9` | feat(63): auto-open default browser on Studio startup (post-execution operator fix) |

---

_Verified: 2026-06-20T18:15:00Z_
_Verifier: Claude (gsd-verifier)_
_Branch: cycle10 — worktree: deckflow-cycle10-run_
