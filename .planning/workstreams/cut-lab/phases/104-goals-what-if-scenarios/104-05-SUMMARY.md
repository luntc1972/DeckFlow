# 104-05 Summary

## What changed

- Added the no-JS `/cut-lab/whatif` MVC fallback in `CutLabController`, wired to the shared `ICutLabWhatifPreviewService` for preview and the existing `Restore` + `Accept` composition for keep.
- Extended `CutLabViewModel` and `CutLab.cshtml` with a server-rendered what-if swap section: A/B selects, preview rows, keep/discard controls, and cut-pile / working-list candidate lists.
- Enhanced `cut-lab.ts` with `/api/cut-lab/whatif` preview and `/api/cut-lab/whatif/commit` keep handlers, plus a client-state writer fix that preserves persisted `decisions` and `baselineSnapshot` when rebuilding `CutLabStateJson` from the DOM.
- Added `CutLabControllerTests` coverage for MVC preview, keep, locked-card rejection, and the resolver-throws zero-Scryfall preview path.
- Added `ts-tests/cut-lab-whatif.test.ts` to assert the safety-critical invariant: preview does not sync/write hidden state, while keep does.
- Added what-if layout rules to `site-common.css` only.

## Commits

- `59273e77` — `feat(cut-lab): add no-js what-if swap flow`
- `84f161f0` — `feat(cut-lab): enhance what-if swap preview`

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabControllerTests" -p:OutDir=C:/tmp/df-10405-test/out/`
  Passed: `17` tests, `0` failed.
- `cd DeckFlow.Web && npx --no-install vitest run ts-tests/cut-lab-whatif.test.ts`
  Passed: `2` tests, `0` failed.
- `cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit`
  Exited `0`.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -p:OutDir=C:/tmp/df-10405-build-retry/out/`
  Succeeded with `0 Warning(s)` and `0 Error(s)`.
- `grep -n "cut-lab/whatif" DeckFlow.Web/Views/Deck/CutLab.cshtml`
  Matched the no-JS what-if form.
- `grep -n "public.*Whatif(" DeckFlow.Web/Controllers/CutLabController.cs`
  Matched the MVC `Whatif` action.
- `grep -n "/api/cut-lab/whatif" DeckFlow.Web/wwwroot/ts/cut-lab.ts`
  Matched both preview and commit endpoints.
- `git diff --name-only --cached`
  Empty: no staged leftovers.
- `git diff --name-only HEAD~2..HEAD -- DeckFlow.Web/wwwroot/css/site.css DeckFlow.Web/wwwroot/js`
  Empty: `site.css` untouched and no compiled JS changed by the task commits.

## Notes

- The first fresh standalone build attempt to `C:/tmp/df-10405-build/out/` failed with `CS2012` because `DeckFlow.Core\obj\Debug\net10.0\DeckFlow.Core.dll` was temporarily locked by `Microsoft Defender Antivirus Service (6580)`. A single retry to `C:/tmp/df-10405-build-retry/out/` succeeded cleanly without code changes.
