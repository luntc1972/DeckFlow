# 104-02 Summary

## What changed

- Projected `GoalRows` in `CutLabViewModel` so the page can render exactly three editable goal rows with stable field names, current probability, baseline trend, and the casual-only representative-line annotation.
- Added a new Goals section to `CutLab.cshtml` with a no-JS form posting to `/cut-lab/goals`, plus goal inputs that also participate in the JS state snapshot through `data-cut-lab-goal`.
- Extended `cut-lab.ts` to serialize goal turns into `CutLabStateJson`, clamp/truncate the DOM values client-side, and intercept the goals form submit so JS users stay on the existing recalculate flow.
- Added goal-row layout rules in `site-common.css` only; `site.css` stayed untouched.
- Added `GoalCommanderByTurn`, `GoalEngineByTurn`, and `GoalPlanByTurn` to `CutLabRequest`, then implemented `CutLabController.Goals` to bind, clamp, serialize, and recompute on the server for the no-JS path.
- Expanded the targeted xUnit coverage for both the view-model projection and the controller fallback path.

## Commits

- `c5549fcc` — `feat: project cut lab goal rows`
- `76d0ad59` — `feat: add cut lab goals editor`
- `375e68ec` — `feat: add cut lab goals fallback`

## Verification

- `dotnet build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj -p:OutDir=C:/tmp/df-10402-task3-build/out/` passed with `0 Warning(s)` and `0 Error(s)`.
- `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabViewModelWordingTests" -p:OutDir=C:/tmp/df-10402-task1-test/out/` passed: `8` tests.
- `dotnet test DeckFlow.Web.Tests --filter "FullyQualifiedName~CutLabControllerTests" -p:OutDir=C:/tmp/df-10402-task3-test/out/` passed: `13` tests.
- `cd DeckFlow.Web && npx --no-install tsc -p tsconfig.json --noEmit` passed.
- Grep gates passed for `CutLabGoalRowView`, `data-cut-lab-goal`, `cut-lab/goals`, controller `Goals(...)`, request `GoalCommanderByTurn`, and TS `goals` wiring.
- `git diff --stat` confirmed `DeckFlow.Web/wwwroot/css/site-common.css` changed and `DeckFlow.Web/wwwroot/css/site.css` remained untouched.

## Notes

- Local default `bin/Debug` outputs are still locked by a running `DeckFlow.Web` process in this workspace, so verification used external `OutDir` paths. That matches the prior phase's documented workaround and still produced clean build/test results.
