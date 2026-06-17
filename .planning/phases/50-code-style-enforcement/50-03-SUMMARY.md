# 50-03 Summary

Status: `COMPLETE`

## Objective

Add the FMT-02 carve-out guard test: four xUnit 2.9.3 regression fixtures that run the same formatter mode as Plan 02 and fail loudly if a future `.editorconfig` change weakens a carve-out.

## Files

- `DeckFlow.Core.Tests/CarveOutGuardTests.cs`
- `.planning/phases/50-code-style-enforcement/50-03-SUMMARY.md`

## What changed

- Added `CarveOutGuardTests` in `DeckFlow.Core.Tests` with `[Trait("Category", "CarveOutGuard")]` at the class level for local exclusion only.
- Added four `[Fact]` fixtures covering:
  - `{ get; init; }`
  - raw-string literal indentation
  - own-line `[JsonPropertyName]`
  - switch expression shape
- Each test appends the final LF newline expected by `.editorconfig`, runs the shared helper, then compares UTF-8 bytes before and after formatting.
- The helper creates a tiny repo-local throwaway project under `artifacts/carveout-guard/<guid>/`, copies the reconciled repo `.editorconfig`, writes the fixture as `Fixture.cs`, runs full `dotnet format "<temp csproj>"`, reads the file back, and deletes the temp directory in `finally`.
- `Process.StartInfo.FileName` is plain `"dotnet"` for the authoritative CI execution path. No WSL-to-Windows bridge is hard-coded in the test.

## Formatter mode

- Mirrors Plan 02 exactly: full `dotnet format`, not `dotnet format whitespace --folder`.
- Source of truth: `50-02-SUMMARY.md` states the gate mode is full `dotnet format DeckFlow.sln --verify-no-changes ...`, and this test intentionally matches that stronger mode.

## xUnit contract

- Compiles against xUnit `2.9.3`.
- Uses only `[Fact]`, `[Trait("Category", "CarveOutGuard")]`, and `Assert.*`.
- Does **not** use `Assert.Skip`, `SkippableFact`, or any new test package.
- CI runs these tests through the existing unfiltered `dotnet test DeckFlow.sln`; the trait exists only so local WSL runs can exclude them with `--filter Category!=CarveOutGuard`.

## Verification

- Required local gate passed:
  - `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -c Release`
  - Result: `Build succeeded. 0 Warning(s), 0 Error(s).`
- Local `dotnet test` was not used as a completion gate because `CLAUDE.md` marks VSTest unreliable in WSL. CI remains authoritative for execution of the four tests.

## Follow-up gate

- Plan 04 must confirm that the four `CarveOutGuard` facts actually ran in CI via the existing unfiltered `dotnet test DeckFlow.sln` path and were not skipped.
