# 107-03 Summary

## Outcome

- Added a fresh `CutLabViewModel.PoolStatusText` member and rendered the lock chip from that single server-side source.
- Reconciled the chip count basis with the Compare panel by making the pool count commander-inclusive on both the server render and the TypeScript twin.
- Reused `DeckFlow.Core.Manabase.ManabaseWording.Pluralize` for Cut Lab wording and removed the local `FormatCountLabel` helper.
- Made both decide paths path-base safe by reading `data-cut-lab-decide-action` and `data-cut-lab-decide-api` from the main Cut Lab form instead of hardcoding absolute `/cut-lab` paths.
- Kept the defensive `data-cache-key` fallback and documented why it remains intentional.

## Accepted Limitation

- The lock chip reflects the imported protected pool and locked total. It is not re-summed after Phase 106 quantity adjustments on the adjust path because `handleAdjustSubmit` does not call `updateLockedCountChip`; the sticky bar remains the live working-list total. This is accepted and documented as out of scope for 107-03.

## Verification

- `dotnet build DeckFlow.sln` passed clean with `0 Warning(s)` and `0 Error(s)`.
- `dotnet test DeckFlow.sln --filter "FullyQualifiedName~CutLabViewModel"` passed with `11` matching `DeckFlow.Web.Tests` tests green.
- `cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit` passed clean.
- `cd DeckFlow.Web && npx --no-install vitest run` did not pass because `ts-tests/cut-lab-lock-interactions.test.ts` still expects the old non-commander chip text (`"3 cards in pool · 3 locked"`). The implemented behavior now returns the planned commander-inclusive text (`"4 cards in pool · 3 locked"`). Updating that out-of-scope test fixture was not done under this plan's file restrictions.

## Scope / Safety Notes

- Only the scoped production/test files were edited for implementation:
  - `DeckFlow.Web/Models/CutLabViewModel.cs`
  - `DeckFlow.Web/Views/Deck/CutLab.cshtml`
  - `DeckFlow.Web/wwwroot/ts/cut-lab.ts`
  - `DeckFlow.Web.Tests/CutLabViewModelWordingTests.cs`
- All touched files preserved LF line endings.
- No compiled `wwwroot/js/*.js` files were staged or committed.
