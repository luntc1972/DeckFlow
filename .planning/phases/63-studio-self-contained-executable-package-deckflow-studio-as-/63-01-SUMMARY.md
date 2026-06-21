---
phase: 63-studio-self-contained-executable
plan: 01
subsystem: infra
tags: [dotnet, publish, win-x64, self-contained, single-file, blazor, studio, packaging]

# Dependency graph
requires:
  - phase: 47-direct-prod-publish
    provides: DeckFlow.Studio project with DirectPush, SCP, user-secrets wiring
  - phase: 59-studio-automation-sync-polish
    provides: Auto-distill, harvest badges, one-click workflow in Studio
provides:
  - win-x64 self-contained publish profile for DeckFlow.Studio (DIST-01)
  - Re-runnable publish scripts (ps1 + sh) that invoke Windows dotnet.exe
  - Kestrel port pin at localhost:5271 in appsettings.json
  - STUDIO-SETUP.md standalone-exe section + README pointer
  - Machine-verified artifact shape (115.9 MB exe, wwwroot, no loose DLLs, zip 49.9 MB)
affects: [operator-workflow, studio-distribution, cycle10-close]

# Tech tracking
tech-stack:
  added: []  # No new NuGet packages; pure SDK publish properties
  patterns:
    - "Publish profile isolation: packaging properties in .pubxml only, not in .csproj, so default build/run/CI/Docker behavior is unchanged"
    - "WSL->Windows script pattern: bash script invokes Windows dotnet.exe via /mnt/c/Program Files/dotnet/dotnet.exe with wslpath for zip"

key-files:
  created:
    - DeckFlow.Studio/Properties/PublishProfiles/win-x64-selfcontained.pubxml
    - scripts/publish-studio.ps1
    - scripts/publish-studio.sh
  modified:
    - DeckFlow.Studio/appsettings.json
    - DeckFlow.Studio/STUDIO-SETUP.md
    - README.md

key-decisions:
  - "Publish config lives in win-x64-selfcontained.pubxml only (not .csproj) so default dotnet build/run/CI/Docker behavior is completely unchanged"
  - "Kestrel port pinned in appsettings.json to localhost:5271 (same as launchSettings dev port) so packaged exe serves at documented port with no operator action; ASPNETCORE_URLS still overrides it at runtime"
  - "Distribution artifact is exe + wwwroot/ + appsettings.json + endpoints manifest (zip); NOT a literal single file — wwwroot cannot be embedded by Blazor Server UseStaticFiles(); docs honest about this"
  - "ReadyToRun left OFF (not in profile); documented as optional toggle only"
  - "Bash script uses PowerShell Compress-Archive via wslpath for zip (zip(1) not installed in this WSL distro; PowerShell always present)"

patterns-established:
  - "DeckFlow.Studio publish: run scripts/publish-studio.ps1 (Windows) or scripts/publish-studio.sh (WSL) — both select win-x64-selfcontained profile, clean output, strip non-dist files, zip, print size"
  - "Secrets on clean machine: env vars with double-underscore separator (Studio__ProdConnectionString etc.); basic harvest->distill->review->approve needs zero secrets"

requirements-completed: [DIST-01]

# Metrics
duration: 55min
completed: 2026-06-20
---

# Phase 63 Plan 01: Studio Self-Contained Executable Summary

**win-x64 self-contained DeckFlow.Studio.exe (115.9 MB) produced by re-runnable publish scripts; artifact-shape machine-verified; operator clean-machine smoke pending**

## Performance

- **Duration:** ~55 min
- **Started:** 2026-06-20T16:10:00Z (approx)
- **Completed:** 2026-06-20T17:05:18Z
- **Tasks:** 4 of 5 completed (Task 5 is operator checkpoint — blocking)
- **Files modified:** 6

## Accomplishments

- Added `DeckFlow.Studio/Properties/PublishProfiles/win-x64-selfcontained.pubxml` with the DIST-01 locked publish shape (win-x64, SelfContained, PublishSingleFile, IncludeNativeLibrariesForSelfExtract, PublishTrimmed=false); properties are in the profile only so `dotnet build/run/CI/Docker` behavior is completely unchanged
- Pinned Kestrel to `http://localhost:5271` in `appsettings.json` so the packaged exe serves at the documented port with no operator env var
- Created `scripts/publish-studio.ps1` (Windows primary) and `scripts/publish-studio.sh` (WSL bash wrapper) — both clean output dir, invoke Windows `dotnet.exe` with the profile, strip `*.pdb/*.xml/web.config/appsettings.Development.json`, zip, and print exe size + zip path
- Documented the standalone-exe workflow in `DeckFlow.Studio/STUDIO-SETUP.md` (new "Run the standalone Windows executable" section) and added a brief pointer in `README.md`
- Machine-verified artifact shape: `DeckFlow.Studio.exe` = 115.9 MB (>50 MB), `wwwroot/` present, `appsettings.json` present, `DeckFlow.Studio.staticwebassets.endpoints.json` present, no loose `*.dll` at root, no loose `e_sqlite3.dll`, stripped files absent, zip produced at 49.9 MB

## Task Commits

1. **Task 1: Publish profile + Kestrel port pin** — `bec153f3` (chore)
2. **Task 2: Publish scripts (ps1 + sh)** — `f3fddebc` (chore)
3. **Task 3: STUDIO-SETUP.md + README docs** — `837186ab` (docs)
4. **Task 4: Publish run + artifact-shape fix (zip)** — `0817584c` (fix)

**Task 5:** Operator blocking checkpoint — not yet reached (clean-machine smoke).

## Files Created/Modified

- `DeckFlow.Studio/Properties/PublishProfiles/win-x64-selfcontained.pubxml` — DIST-01 MSBuild publish profile (win-x64, self-contained, single-file, native-self-extract, trim-off)
- `DeckFlow.Studio/appsettings.json` — Added Kestrel:Endpoints:Http:Url = http://localhost:5271
- `scripts/publish-studio.ps1` — PowerShell publish script (primary, Windows)
- `scripts/publish-studio.sh` — Bash publish wrapper (WSL, invokes Windows dotnet.exe)
- `DeckFlow.Studio/STUDIO-SETUP.md` — Added "Run the standalone Windows executable" section
- `README.md` — Added brief publish-studio pointer in helper-scripts section

## Decisions Made

- Publish config goes in `.pubxml` only (not `.csproj`) — unconditional csproj properties would alter every build/run/CI/Docker invocation; the profile applies only when explicitly selected
- Port 5271 pinned via `appsettings.json` Kestrel config (intentional default URL change for packaged-exe and `--no-launch-profile` runs; overridable via `ASPNETCORE_URLS` because env vars are applied after appsettings in Program.cs)
- Bash script uses `PowerShell Compress-Archive` via `wslpath` for the zip step because `zip(1)` is not installed in this WSL distro; PowerShell is always present on a WSL+Windows machine
- `ReadyToRun` left OFF by default; documented as optional toggle only

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed missing `zip` command in publish-studio.sh**
- **Found during:** Task 4 (run the actual publish)
- **Issue:** `zip` is not installed in the WSL distro; `bash scripts/publish-studio.sh` failed at the zip step with `zip: command not found` (exit 127). The exe (115.9 MB) and all other artifacts were already present; only the zip step failed.
- **Fix:** Replaced `zip -r` with `PowerShell Compress-Archive` invoked via `wslpath`-converted paths. PowerShell is always available at `/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe` on any WSL+Windows machine.
- **Files modified:** `scripts/publish-studio.sh`
- **Verification:** Re-ran the full Task 4 verification; `ARTIFACT_SHAPE_OK` printed; zip at 49.9 MB confirmed.
- **Committed in:** `0817584c` (fix(63): use PowerShell Compress-Archive in publish-studio.sh for zip step)

---

**Total deviations:** 1 auto-fixed (Rule 1 — bug)
**Impact on plan:** Necessary for correctness; no scope creep. The script is now portable across WSL setups without requiring `zip` installation.

## Issues Encountered

- `zip(1)` not installed in WSL distro — resolved via Rule 1 auto-fix (PowerShell Compress-Archive fallback). No other issues.

## Known Stubs

None — no placeholder data or stub UI components introduced.

## Threat Flags

No new security-relevant surfaces introduced beyond what is already in the plan's threat model (T-63-01 through T-63-SC). All mitigations verified:
- `appsettings.json` contains only logging config + Kestrel port (no secrets)
- Publish scripts strip `appsettings.Development.json`, `*.pdb`, `*.xml`, `web.config`
- `_launch-studio.bat` documented as NOT committed/distributed
- Zero new NuGet packages

## Pending Operator Checkpoint

**Task 5 (blocking): Clean-machine smoke — operator must verify**

Machine-checkable asserts passed in Tasks 1-4. The one proof that cannot run from this environment (which has .NET installed) is the clean-no-.NET-box launch:

1. (Optional) Confirm default build is unchanged: `"C:\Program Files\dotnet\dotnet.exe" build DeckFlow.Studio\DeckFlow.Studio.csproj` produces a framework-dependent build with no RID/single-file applied.
2. Copy `artifacts/studio-release/` (or unzip `artifacts/DeckFlowStudio-2026.06.20.zip`) to a Windows machine with NO .NET runtime/SDK installed.
3. Double-click `DeckFlow.Studio.exe`.
4. Open `http://localhost:5271` — Studio UI loads, Blazor connected (interactive pages, no 404 on `/_framework/blazor.server.js`).
5. Confirm `content-kb.db` created in the local data dir.
6. Run a basic harvest → review action to confirm the no-secrets flow works.

**Resume signal:** Type "approved" once the exe launches at http://localhost:5271 on a no-.NET Windows box and the basic no-secrets flow works; or describe what failed.

## Self-Check

- [x] `DeckFlow.Studio/Properties/PublishProfiles/win-x64-selfcontained.pubxml` exists — FOUND
- [x] `scripts/publish-studio.ps1` exists — FOUND
- [x] `scripts/publish-studio.sh` exists — FOUND
- [x] Commits `bec153f3`, `f3fddebc`, `837186ab`, `0817584c` all exist — VERIFIED
- [x] Publish ran clean: exe 115.9 MB, wwwroot present, no loose DLLs, zip 49.9 MB — ARTIFACT_SHAPE_OK

## Self-Check: PASSED

All four task commits exist; artifact shape proof passed; no stubs; no new threat surfaces beyond plan's model.

## Next Phase Readiness

- DIST-01 is complete pending the operator clean-machine smoke (Task 5 checkpoint)
- Once Task 5 is approved, Phase 63 Plan 01 is fully closed and DIST-01 can be marked Met in REQUIREMENTS.md
- Operator needs: the zip at `artifacts/DeckFlowStudio-2026.06.20.zip` + a clean Windows VM/machine

---
*Phase: 63-studio-self-contained-executable*
*Completed: 2026-06-20 (pending Task 5 operator checkpoint)*
