# 55-02 Summary

## Outcome

Implemented the single authoritative publish-state engine in `DeckFlow.Core.Content`:

- `PublishState` enum with the four locked states:
  - `NeverPublished`
  - `PushedHidden`
  - `Published`
  - `LocalNewer`
- `PublishStateExtensions.ToDisplayString()` centralizes the shared UI vocabulary:
  - `NeverPublished` -> `Never published`
  - `PushedHidden` -> `Pushed-hidden`
  - `Published` -> `Published`
  - `LocalNewer` -> `Local-newer`
- `PublishStateDeriver.Derive(DateTimeOffset? pushedToProdUtc, bool isVisible, DateTimeOffset localIndexedUtc)` is pure, synchronous, and store-free.

## Locked Derivation Order

First match wins:

1. `pushedToProdUtc is null` -> `NeverPublished`
2. `!isVisible` -> `PushedHidden`
3. `localIndexedUtc (UTC) > pushedToProdUtc (UTC)` -> `LocalNewer`
4. Otherwise -> `Published`

## Boundary Semantics

- Equal timestamps resolve to `Published`, not `LocalNewer`.
- Cross-offset comparisons normalize both values to UTC before comparing, so same-instant timestamps across different offsets resolve identically.

## Test Coverage Added

`PublishStateDeriverTests` covers:

- both null-push cases (`NeverPublished` wins over visibility)
- hidden pushed entries (`PushedHidden`)
- visible entries with local time before push (`Published`)
- visible entries with local time equal to push (`Published`)
- visible entries with local time strictly after push (`LocalNewer`)
- same-instant cross-offset case:
  - `2023-07-28T23:50:51+00:00`
  - `2023-07-28T16:50:51-07:00`
  - result: `Published`
- cross-offset strictly-later case resolving to `LocalNewer`
- exact display-string mapping

## Verification

- `dotnet test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -nologo --no-build` -> Passed (`455` passed, `0` failed)
- `dotnet build DeckFlow.Core/DeckFlow.Core.csproj -nologo` -> `0` errors
- `dotnet build DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj -nologo` -> `0` errors
- `grep -rln "Never published\|Pushed-hidden\|Local-newer" --include=*.cs . | grep -v obj` -> only `DeckFlow.Core/Content/PublishState.cs` and `DeckFlow.Core.Tests/Content/PublishStateDeriverTests.cs`
