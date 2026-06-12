# 38-01 Summary

- Pre-split baseline git SHA: `2e2d5aa851a1b8d9d7655f689535cfc55225d933`
- Warning baseline (`"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj` then `grep -c ': warning '`): `0`

## Members moved

- `DeckController.Home()`
- `DeckController.Error()`
- `DeckController.GetSetOptions()`
- `DeckController.TryGetSetOptionsAsync()`

These members now live in `ShellController`.

## DeckController constructor

- Before split: `13` arguments
- After split: `12` arguments
- Removed dependency: `IScryfallSetService`

## Route and runtime notes

- `/Deck/Error` was preserved by moving `Error()` to `ShellController` and adding `[Route("Deck/Error")]`.
- `app.UseExceptionHandler("/Deck/Error")` in `DeckFlow.Web/Program.cs` was left unchanged.
- `DeckFlow.Web/Views/Deck/Error.cshtml` now links back to `ShellController.Home` via `Url.Action("Home", "Shell")`.
- `DeckViewLocationExpander` was registered in `Program.cs` with the fully-qualified type `new DeckFlow.Web.Controllers.DeckViewLocationExpander()`.

## Build verification

- Task 1 build result: `Build succeeded`, warnings `0`
- Task 2 build result: `Build succeeded`, warnings `0`
- Task 3 build result: `Build succeeded`, warnings `0`
- Task 4 build result: `Build succeeded`, warnings `0`
