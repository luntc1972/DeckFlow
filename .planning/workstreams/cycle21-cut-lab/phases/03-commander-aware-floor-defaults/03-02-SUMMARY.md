# 03-02 Summary

## Scope

Executed plan 03-02 end to end on `gsd/cycle21-cut-lab`. The actual current date was **Wednesday, July 29, 2026**; the generator was intentionally run with `--generated 2026-07-28` because the task explicitly required that past date label.

## Task 1

- Added `DeckFlow.CLI/RoleFloorBaselineCommandRunner.cs`.
- Registered `role-floor-baseline` in `DeckFlow.CLI/Program.cs`.
- Added `scripts/role-floor-baseline/drift-thresholds.json` with all 7 required keys.
- Verified `role-floor-baseline --help` lists `--findings`, `--out`, `--generated`, and `--thresholds`.
- Verified drift evaluation appears before any `Directory.CreateDirectory` or write in the runner, and the non-postgres source guard runs before `RoleFloorBaseline.Build`.

## Task 2

Bootstrap generation console output, verbatim:

```text
No committed snapshot at DeckFlow.Web\Data\role-floor-baseline\latest.json; skipping drift check (bootstrap run).
Wrote DeckFlow.Web\Data\role-floor-baseline\latest.json
Commanders=678, AdoptedPairs=1463, Bytes=51694
```

Verified snapshot counts:

- `commanders`: 678
- `adoptedPairs`: 1463
- `floors-sum`: 1463
- `sampleSize`: 841
- `byteSize`: 51694
- Top-level keys: `generated`, `sampleSize`, `adoptedPairs`, `commanders`
- No `lands`, `interaction-mass`, or `protection` key appears anywhere.
- Every emitted floor is an integer and `>= 1`.

Verified `DeckFlow.Web/DeckFlow.Web.csproj` gained exactly one new content entry:

```xml
<Content Update="Data\role-floor-baseline\*.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

No `PackageReference` line changed.

## Task 3

All failure-path scratch inputs lived under the Windows system temp directory so the required Windows `dotnet.exe` process could read them:

- `C:\Users\ChrisLunt\AppData\Local\Temp\role-floor-baseline-task3.LdaB3J`

Failure-path results:

1. Missing thresholds field
   - Exit code: 1
   - First stderr line: `Could not read drift inputs: JSON deserialization for type 'DeckFlow.Core.Research.RoleFloorDriftThresholds' was missing required properties including: 'maxOneSidedPct'.`
   - Confirmed committed snapshot unchanged afterwards with `git diff --quiet -- DeckFlow.Web/Data/role-floor-baseline/latest.json`.
2. Drift rejection
   - Exit code: 1
   - First stderr line: `Drift check FAILED with 1 finding(s); no files written.`
   - Confirmed the scratch `latest.json` was unchanged on disk after the run.
   - Confirmed committed snapshot unchanged afterwards with `git diff --quiet -- DeckFlow.Web/Data/role-floor-baseline/latest.json`.
   - Full stderr:

```text
Drift check FAILED with 1 finding(s); no files written.
  AdoptedPairCollapse: adopted pairs fell 70.7% (5000 -> 1463); limit is 25%.
If this reflects a genuine corpus shift, retune and commit scripts\role-floor-baseline\drift-thresholds.json, then re-run.
```

3. Non-postgres source guard
   - Exit code: 1
   - First stderr line: `Found 1 adopted-role row(s) with a non-postgres source; refusing to build.`
   - Mutated commander/role pair: `Y'shtola, Night's Blessed/draw`
   - Guard stderr named the pair as `Y'shtola, Night's Blessed/draw: source=edhrec`.
   - Confirmed committed snapshot unchanged afterwards with `git diff --quiet -- DeckFlow.Web/Data/role-floor-baseline/latest.json`.

README was updated with a `role-floor-baseline` entry matching the neighboring CLI documentation depth, including the defaults for `--findings`, `--out`, and `--thresholds`, the required `--generated` option, the read/write paths, and the two refusal conditions.

## Threshold Rationale

`minEstablishedN: 80` and `minPopulousN: 150` come directly from the planning-time commander-`n` distribution over the 678 adopted commanders: min 40 / median 86 / max 874, so 80 treats roughly the upper half as established and 150 roughly the upper quartile. `moverThresholdFloors: 2` is one whole card above the noise of a single-card shift in an integer floor.

## Deviation

No product/code deviation from the plan.

Implementation note: the plan required scratch files under a system temp path, and the final checks used the Windows system temp directory instead of WSL `/tmp` because the required Windows `dotnet.exe` process cannot read WSL-only temp paths directly.
