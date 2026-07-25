## Build baseline

- Capture date: 2026-07-25
- Branch: `feat/personal-tools`
- HEAD commit: `1f1a33185038a95123df858da7972ff61fd7727e`
- Working tree preflight: no dirty tracked production files; tracked changes were limited to `.planning/ROADMAP.md` and `.planning/STATE.md`, with additional untracked `.planning/`, `.foreman/`, `.codex-audit/`, and `DeckFlow.Web/wwwroot/js/` noise.
- Build result: `Build succeeded.`
- Build warnings: `9`
- Build errors: `0`
- Warning delta vs RESEARCH baseline (`9`): no deviation

### Warning ID / file table

| Warning ID | Count | File |
| --- | ---: | --- |
| `CS8629` | 9 | `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs` |

### Warning instances

- `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs(52,26)` -> `CS8629`
- `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs(54,26)` -> `CS8629`
- `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs(56,25)` -> `CS8629`
- `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs(69,28)` -> `CS8629`
- `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs(123,26)` -> `CS8629`
- `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs(125,25)` -> `CS8629`
- `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs(137,26)` -> `CS8629`
- `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs(139,26)` -> `CS8629`
- `DeckFlow.Core.Tests/Manabase/ManabaseBaselineWeightingTests.cs(141,26)` -> `CS8629`

## Test baseline

### DeckFlow.Core.Tests

- Project: `DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj`
- Runner summary: `Passed!  - Failed:     0, Passed:  1613, Skipped:     0, Total:  1613, Duration: 1 m 51 s - DeckFlow.Core.Tests.dll (net10.0)`

### DeckFlow.Web.Tests

- Project: `DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj`
- Runner summary: `Passed!  - Failed:     0, Passed:  2013, Skipped:    16, Total:  2029, Duration: 2 m 28 s - DeckFlow.Web.Tests.dll (net10.0)`

### Pre-existing failures

None. Both test projects completed with zero failed tests on this 2026-07-25 baseline capture.

## Capture commands

```bash
git branch --show-current
git status --porcelain
env -u MTG_DATA_DIR "/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.sln 2>&1 | tee /tmp/deckflow-112-01/build.log
env -u MTG_DATA_DIR "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Core.Tests/DeckFlow.Core.Tests.csproj 2>&1 | tee /tmp/deckflow-112-01/core-test.log
env -u MTG_DATA_DIR "/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj 2>&1 | tee /tmp/deckflow-112-01/web-test.log
git rev-parse plan/cycle-17-creator-style
git rev-parse 5709f37c
git cat-file -e plan/cycle-17-creator-style:<each of the 102 allowlisted paths>
git log --oneline 8599cd3b..HEAD -- <each of the 15 M-file targets>
git show HEAD:DeckFlow.Web/Program.cs
git show HEAD:DeckFlow.Web/Services/Persistence/DeckFlowDatabaseConnectionFactory.cs
git show HEAD:DeckFlow.Core/Integration/ILlmDistillationService.cs
git show HEAD:DeckFlow.Core/Knowledge/DistillationResults.cs
git show HEAD:DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs
git show HEAD:DeckFlow.Core/Content/ContentKbPaths.cs
git show HEAD:DeckFlow.Web/Services/Content/WebSeedKeyMembershipSource.cs
git cat-file -e HEAD:content-kb/seed/creator-style-profiles.json
git cat-file -e HEAD:content-kb/seed/creator-deck-cache.json
```

## Drift preflight

### Check A — port source is readable

- `git rev-parse plan/cycle-17-creator-style` -> `6da5eb420b6403b68804bdbd3f2e51d7213ab33c`
- `git rev-parse 5709f37c` -> `5709f37ca02d400cb0ae35c1726297d8e1955bc1`
- Result: PASS

### Check B — every allowlisted source path exists on the port branch

- Commit 1 allowlist count verified from `112-02-PLAN.md`: `64`
- Commit 2 allowlist count verified from `112-04-PLAN.md`: `38`
- Total allowlisted paths verified with `git cat-file -e`: `102`
- Missing paths: none
- Result: PASS

### Check C — M-file drift since the research session

- Drift window checked: `8599cd3b..HEAD`
- Target set size: `15`

| M-file target | Changed after `8599cd3b`? | Result |
| --- | --- | --- |
| `DeckFlow.Core/Content/ContentKbPaths.cs` | No | PASS |
| `DeckFlow.Core/AssemblyInfo.cs` | No | PASS |
| `DeckFlow.Core/Integration/ILlmDistillationService.cs` | No | PASS |
| `DeckFlow.Core/Knowledge/DistillationResults.cs` | No | PASS |
| `DeckFlow.Core/Knowledge/DistillationValidation.cs` | No | PASS |
| `DeckFlow.Core/Knowledge/ContentTagVocabulary.cs` | No | PASS |
| `DeckFlow.Core/Loading/CommanderInference.cs` | No | PASS |
| `DeckFlow.Core/Knowledge/CategoryKnowledgeRepository.cs` | No | PASS |
| `DeckFlow.Core/Knowledge/CardCategoryRepository.cs` | No | PASS |
| `DeckFlow.Web/Services/Persistence/DeckFlowDatabaseConnectionFactory.cs` | No | PASS |
| `DeckFlow.Web/Services/Scryfall/ScryfallDtos.cs` | No | PASS |
| `DeckFlow.Web/Services/Scryfall/ScryfallCardResolver.cs` | No | PASS |
| `DeckFlow.Web/Services/PacketSessionCache.cs` | No | PASS |
| `DeckFlow.Web/Program.cs` | No | PASS |
| `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` | No | PASS |

- Main-only member `CreateManabaseBaselineConnection` is still present in `DeckFlow.Web/Services/Persistence/DeckFlowDatabaseConnectionFactory.cs`.
- Main-only member `ExtractCombinedAsync` is still present in `DeckFlow.Core/Integration/ILlmDistillationService.cs`.
- Main-only member `CombinedExtractionResult` is still present in `DeckFlow.Core/Knowledge/DistillationResults.cs`.
- `DeckFlow.Web/Program.cs` still contains `builder.Services.AddDeckFlowScryfallServices();`.
- `DeckFlow.Web/Program.cs` still contains the sequential pair `EnsureSchemaAsync()` then `GetRequiredService<IContentKbSeedLoader>().LoadIfPresentAsync()` at lines `278-279`.
- `DeckFlow.Web/Services/Http/ResiliencePipelineFactory.cs` still contains the five expected registrations: `banlist`, `spellbook`, `tagger`, `tagger-post`, `scryfall`.
- Result: PASS

### Check D — contamination set is still on main

- `DeckFlow.Core/Content/ContentKbPaths.cs` exists on `HEAD`.
- `grep -c 'CreatorStyleProfileSeedRelativePath' DeckFlow.Core/Content/ContentKbPaths.cs` -> `0`
- `DeckFlow.Web/Services/Content/WebSeedKeyMembershipSource.cs` exists on `HEAD`.
- `git cat-file -e HEAD:content-kb/seed/creator-style-profiles.json` -> missing
- `git cat-file -e HEAD:content-kb/seed/creator-deck-cache.json` -> missing
- Result: PASS

VERDICT: GO — manifest matches HEAD
