# 66-05 Summary

- Added `AdminToolsController` at `/Admin/Tools` with registry-driven GET listing and a POST toggle path that mirrors `AdminFlagsController`: same-origin guard, anti-forgery, known-tool validation, `SetEnabledAsync`, and `ReloadAsync`.
- Added `Views/AdminTools/_ViewStart.cshtml` to force `_AdminLayout`, plus `Views/AdminTools/Index.cshtml` with section-grouped tool toggles, friendly labels, core badges, success messaging, and an inline warn-not-block banner for disabled core Analyze tools.
- Added a `Tools` card to `Views/AdminLanding/Index.cshtml` so the new page is discoverable from the admin hub.
- Added `AdminToolsControllerTests` for GET grouping, cross-origin rejection, blank/unknown key rejection, valid toggle persistence with reload, and core-disable warning behavior.
- Extended `FakeFeatureFlagCache` to record `ReloadAsync` calls so the same-round-trip reload contract is asserted directly in tests.
- Added a scoped admin warning banner/core badge style in `admin-common.css` because the admin shell did not already have a warning variant.

## Verification

- `dotnet build DeckFlow.Web/DeckFlow.Web.csproj -c Debug`
- `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~AdminToolsControllerTests"`
- `dotnet test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "FullyQualifiedName~AdminFlagsControllerToggleTests"`
- `dotnet build`

## Notes

- Builds/tests still emit the pre-existing `NU1903` `SQLitePCLRaw.lib.e_sqlite3` vulnerability warnings already present in this worktree; no new package changes were introduced here.
