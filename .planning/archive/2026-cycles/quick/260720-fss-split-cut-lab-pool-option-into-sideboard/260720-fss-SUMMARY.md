---
quick_id: 260720-fss
status: complete
date: 2026-07-20
branch: gsd/cycle18-cut-lab
---

# 260720-fss Summary

## Completed

- Split the Cut Lab combined intake option into independent `IncludeSideboard` and `IncludeMaybeboard` request + persisted intent flags.
- Replaced the static combined board-set switch with a per-request analyzed board set built from `{ mainboard, commander }` plus optional `sideboard` and `maybeboard`.
- Computed quantity-weighted board counts from the full imported load before filtering: Main = `mainboard + commander`, Sideboard = `sideboard`, Considering/Maybe = `maybeboard`.
- Carried those counts through `CutLabProcessResult` and `CutLabViewModel`, showed them in the Cut Lab view after import, and updated the too-many validator error to report the three counts.
- Preserved no-JS decision rehydrate by restoring both new flags from `state.Intent`.
- Added legacy intent back-compat so serialized state with `includeSideboardAndMaybeboard: true` maps to both new flags true, while absent flags stay false.
- Updated Cut Lab tests to cover sideboard-only, maybeboard-only, both, neither, board counts, validator breakdown messaging, and legacy intent JSON.
- Updated Cut Lab docs and README wording to describe the split toggles, Considering/Maybeboard terminology, and the board-count feedback.

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln -clp:ErrorsOnly`
  Result: passed with the known 9 pre-existing warnings only.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLab"`
  Result: `222` passed, `0` failed, `0` skipped.
- UAT server on `:5173`
  Result: bounced after the initial DLL lock, restarted with `scripts/run-web-test.sh`, and verified listening again (`HTTP 405` to `HEAD /`, confirming Kestrel is up).

## Post-implementation cleanup (/simplify batch)

- Shared the board-breakdown plumbing and stopped re-serializing the legacy combined flag (legacy `IncludeSideboardAndMaybeboard` made `private` + `[JsonInclude]`: deserialized for back-compat read, never re-emitted since it has no getter).
- Added `Serialize_FreshState_DoesNotWriteLegacyCombinedBoardFlag` asserting the legacy key is absent from fresh session JSON.

## Live UAT (2026-07-20, `:5173`)

Exercised via HTTP form POST against the running server (directly tests checkbox binding + rendered counts/error):

| Toggles | Pool | Result |
|---------|------|--------|
| none | 130 | in-range |
| considering only | 142 (130+12) | independent bind |
| sideboard only | 145 (130+15) | independent bind |
| both | 157 (130+15+12) | size error |

- Independent binding proven: considering-only=142 ≠ sideboard-only=145.
- Per-board counts render at the toggles and in the breakdown line (`Main 130 · Sideboard 15 · Considering/Maybe 12`).
- Size error names the counts: *"This pool has 157 non-commander cards — over Cut Lab's 150 max. Main 130 · Sideboard 15 · Considering/Maybe 12. Deselect the sideboard or considering list to fit."*
- Build clean (0 warn/0 err), CutLab tests `223` passed / 0 failed (+1 vs prior 222).

## Commits

- `ed1b0510` `feat(260720-fss): split cut lab sideboard/considering toggles`
- `a2d76963` `feat(260720-fss): show cut lab board counts + size error`
- `19dd79a8` `test(260720-fss): cover split cut lab board toggles`
- `07f737fc` `docs(260720-fss): update cut lab board toggle docs`
- `517e28e1` `refactor(260720-fss): share cut lab board breakdown plumbing`
- `6e78099d` `fix(260720-fss): stop serializing legacy cut lab board flag`
