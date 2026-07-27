TASK: Fix four Cut Lab card-classification bugs in the DeckFlow repo (already on branch feat/cutlab-fixes, worktree checked out at repo root). This is REVISION 2 of an implementation ticket — a prior read-only Codex review found 2 blocking gaps in v1, both are folded in below. Do not re-litigate the review; implement this ticket as written.

EXPECTED OUTCOME:
1. An MDFC (modal double-faced) card with a land back face (e.g. a Spell // Land MDFC) is counted in the "lands" role, not missed, and does not also double-count as "ramp".
2. Cut Lab's role classification reads front-face oracle text first everywhere it currently reads joined-all-faces text first — in BOTH `CutLabRoleAssigner.AssignRoles`'s local oracle variable AND `PlanRoleClassifier.FromHeuristic`'s local oracle variable (both currently have the same reversed `OracleText ?? FrontFaceOracleText` bug; PlanRoleClassifier.FromHeuristic is called by CutLabRoleAssigner via PlanRoleClassifier.Classify and separately by Manabase's own ManabaseAnalysisService, so fixing it there fixes both consumers). Real Manabase precedent for front-face-first: ManabaseClassifier.cs lines ~1097, ~1156, ~1841 use `FrontFaceOracleText ?? OracleText`.
3. Raugrin Triome (and every other cycling card) is NOT classified as "draw" just because its Cycling reminder text contains the literal phrase "Draw a card". The shared literal-draw regex must ignore text inside parentheses (reminder text). Note: ManabaseClassifier.cs already has an equivalent parenthetical-stripping regex around line 907 — reuse that same pattern/approach for consistency rather than inventing a new one.
4. A card that matches none of Cut Lab's 8 structural roles falls into a new "Other" display bucket in the "How Your Pool Competes" panel instead of silently vanishing from every role count — as a DISPLAY-ONLY bucket, not a floor/lockable role.
5. `dotnet build` is clean, the full DeckFlow.Core.Tests + DeckFlow.Web.Tests suites pass (including Manabase golden/byte-identity tests), and new regression tests exist for each of the 4 fixes.

CONTEXT (read in this order):
- docs/decisions/0003-ramp-classifier-divergence.md — governs this area. Ramp (DeckStatClassifier.IsRampCard vs Manabase's ramp predicates) is INTENTIONALLY divergent — do not touch/unify ramp. Draw (DeckStatClassifier.MatchesYouCardDraw) is the INTENTIONALLY SHARED literal-draw signal (ManabaseClassifier.IsYouCardDraw delegates to it) — a genuine bug fix here is in scope, but changes can shift Manabase's golden/byte-identity tests per the ADR; verify, don't assume unaffected.
- DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs (~lines 63-131, `AssignRoles`) — fixes 1, 2a, 4 (assignment side) land here.
- DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs (`FromHeuristic`, ~line 171-205, the buggy line is ~176) — fix 2b lands here. This file is used by BOTH Cut Lab (via CutLabRoleAssigner.cs line ~75, `PlanRoleClassifier.Classify(...)`) and Manabase directly (ManabaseAnalysisService.cs). Confirmed by prior review: there is no existing test in DeckFlow.Web.Tests/Manabase/PlanRoleClassifierTests.cs that sets `FrontFaceOracleText` differently from `OracleText`, so this precedence fix will not break any existing test there — verify this is still true before relying on it.
- DeckFlow.Core/Analysis/DeckStatClassifier.cs — `YouCardDrawRegex` (~line 24-26), `MatchesYouCardDraw` (~line 55), `IsDrawCard` (~line 62-65). Fix 3 lands here.
- DeckFlow.Core/Manabase/ManabaseClassifier.cs (~line 907) — existing parenthetical-reminder-text stripping pattern to mirror for fix 3, for consistency with how Manabase already handles this elsewhere.
- DeckFlow.Core/Manabase/CardFact.cs — `HasLandFace` (bool) and `FrontFaceOracleText` (string?) already correctly populated for MDFC/DFC by ScryfallCardFactMapper.cs (HasLandFace ~line 84, OracleText/FrontFaceOracleText assembly ~lines 39-42, 70-82). Do NOT change ScryfallCardFactMapper or CardFact.
- DeckFlow.Web/Services/CutLab/CutLabLockRules.cs — `IsLand(typeLine)` (~line 102) is front-face-only via `CardTypeLine.FrontFace`; correct for its OTHER callers, do not change it. CutLabRoleAssigner must OR its result with `fact.HasLandFace` locally instead.
- DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs — `RoleKeys` (8 canonical floor-eligible role keys) and `TryGetCanonicalRole`. **DO NOT add "other" here** (see MUST NOT / landmine below).
- DeckFlow.Web/Services/CutLab/CutLabFloorDefaults.cs — `GetBracketBand(role, bracket)` (~line 101-125) throws `ArgumentOutOfRangeException` for any role not lands/ramp/draw/interaction/protection/engines/payoffs/wincons. `ResolveDefaults` iterates `CutLabFloorRules.RoleKeys` (~line 74) and calls this. If "other" were added to `CutLabFloorRules.RoleKeys`, every Cut Lab page load would throw. This is exactly why "other" must stay display-only, out of that list.
- DeckFlow.Web/Models/CutLabViewModel.cs:
  - `RoleDisplayLabels` dict (~line 39-50) — add `["other"] = "Other"`.
  - `TypeGroupOrder` (~line 52-64) — study this as the EXACT pattern to mirror: it's a separate literal `string[]` (8 primary types + `"Other"` appended), decoupled from `CardTypeLine.PrimaryTypePriority`, with a comment explaining it appends the fallback bucket.
  - `BuildRoleGroups` (~line 373-404) — currently iterates `CutLabFloorRules.RoleKeys` directly. Add a new sibling array (e.g. `RoleGroupDisplayOrder`, mirroring `TypeGroupOrder`'s style — either a plain literal 9-entry array or `[.. CutLabFloorRules.RoleKeys, "other"]`) and have `BuildRoleGroups` iterate that instead. The membership/select logic inside does not otherwise need to change.
  - `BuildFloorRows` and any other direct iteration of `CutLabFloorRules.RoleKeys` for floor logic: LEAVE UNCHANGED.
- DeckFlow.Web/Services/CutLab/CutLabUiPatchBuilder.cs — has its OWN separate copy of the `RoleDisplayLabels` dict (~line 8-19, same 8 entries) used by `DisplayLabelFor`/`RoleLabelFor` (~line 371-386) to render `RoleLabel` for live-patch UI updates. Add `["other"] = "Other"` to THIS dict too, for consistency, since some patch paths feed real `CutLabRoleAssigner.AssignRoles` output through `RoleLabelFor`. Do NOT touch `BuildAdjustRoleAssignments` (~line 382-397) in this same file — it's a separate, simplified lands-only heuristic unrelated to these 4 bugs; out of scope.
- DeckFlow.Web.Tests/CutLabRoleAssignerTests.cs — existing conventions/`Fact(...)` helper to extend.
- DeckFlow.Core.Tests/DeckStatClassifierTests.cs — existing `IsDrawCard_TrueCases`/`IsDrawCard_FalseCases` theory tests (~lines 45-65) to extend.
- DeckFlow.Web.Tests/CutLabPageServiceTests.cs — around line 2015-2025 there is a test asserting `Assert.Equal(CutLabFloorRules.RoleKeys, model.RoleGroups.Select(group => group.RoleKey).ToArray());`. This line must be updated to expect the 9-entry list (8 roles + "other") once `BuildRoleGroups` changes. IMPORTANT — do NOT change the two adjacent assertions about `"Unresolved Card"` (`Assert.Equal(string.Empty, model.RoleListByCardName["Unresolved Card"])` and `Assert.Empty(result.RoleAssignmentsByCardName["Unresolved Card"])`): "Unresolved Card" is a card with no matching Scryfall data at all (see CutLabAnalysisContextBuilder.cs ~lines 205-215 — when `cardsByName.TryGetValue` fails, `CutLabRoleAssigner.AssignRoles` is never called and `roles` stays `[]` by design). The new "other" fallback only applies INSIDE `AssignRoles` when a resolved `CardFact` matches none of the 8 roles — it does not apply to unresolved cards, and those two assertions must remain exactly as they are. Also check `Assert.Equal(CutLabFloorRules.RoleKeys, result.ResolvedFloors.Select(floor => floor.Role).ToArray())` a couple lines above — that one is about floors, not display groups, and must NOT change.

CONSTRAINTS:
- C# 12 / .NET 10, existing project conventions (file-scoped namespaces, XML doc comments on public members, `sealed`/`static` as already used).
- Preserve each touched file's existing line endings exactly (LF or CRLF, per-file — check each file, don't assume repo-wide). Change only lines whose content actually changes.
- xUnit conventions matching each file's existing style (`[Theory]`/`[InlineData]` vs `[Fact]`).
- Do NOT touch DeckStatClassifier.IsRampCard or any Manabase ramp predicate.
- Do NOT change ScryfallCardFactMapper.cs or CardFact.cs.
- Do NOT refactor Cut Lab to call into Manabase's classifier as a base layer, or otherwise restructure the relationship between DeckStatClassifier/ManabaseClassifier/PlanRoleClassifier beyond the specific fixes listed here — that broader architecture question is explicitly out of scope for this task (deferred to a separate design discussion).

MUST DO:
1. In `CutLabRoleAssigner.AssignRoles`: `bool isLand = CutLabLockRules.IsLand(typeLine) || fact.HasLandFace;` and reuse that local `isLand` in the ramp gate a few lines below instead of recomputing `CutLabLockRules.IsLand(typeLine)` again (`if (!isLand && DeckStatClassifier.IsRampCard(typeLine, oracle))`) — prevents an MDFC land from double-counting as ramp.
2. In `CutLabRoleAssigner.AssignRoles`: swap `string oracle = fact.OracleText ?? fact.FrontFaceOracleText ?? string.Empty;` to `string oracle = fact.FrontFaceOracleText ?? fact.OracleText ?? string.Empty;`.
3. In `PlanRoleClassifier.FromHeuristic`: swap the equivalent reversed line (`string oracle = fact.OracleText ?? fact.FrontFaceOracleText ?? string.Empty;`) to front-face-first, same as #2.
4. In `DeckStatClassifier.cs`: add a private helper that strips parenthetical reminder text (mirror the existing approach in ManabaseClassifier.cs ~line 907) and apply it inside `MatchesYouCardDraw` before running `YouCardDrawRegex`. Add a short comment explaining why (cycling's "(..., Discard this card: Draw a card.)" reminder text was a false positive for every cycling card, e.g. Raugrin Triome). Do not change `IsRampCard`.
5. Add the "other" fallback: in `CutLabRoleAssigner.AssignRoles`, after building `assigned`, if it is empty, add a new role-key constant (e.g. `OtherRole = "other"`) to it before returning.
6. Wire display: `CutLabViewModel.cs` (`RoleDisplayLabels` + new display-order array used by `BuildRoleGroups`, per CONTEXT above) and `CutLabUiPatchBuilder.cs` (`RoleDisplayLabels` dict only). Do NOT touch `CutLabFloorRules.RoleKeys`.
7. Update `CutLabPageServiceTests.cs` per the CONTEXT note above — the one `RoleGroups`-keys assertion, nothing else in that block.
8. Add regression tests:
   - CutLabRoleAssignerTests.cs: (a) an MDFC Spell // Land fact (`HasLandFace = true`, front-face type line NOT containing "Land") → roles contains "lands", does not contain "ramp"; (b) a fact whose joined `OracleText` contains draw text only on a back face but whose `FrontFaceOracleText` does not → "draw" is NOT in the result; (c) a fact matching none of the 8 roles → roles equals exactly `["other"]`.
   - DeckStatClassifierTests.cs: a literal Raugrin-Triome-style oracle text (tri-land ETB-tapped + `{T}: Add {R}, {U}, or {W}.` + `Cycling {3} ({3}, Discard this card: Draw a card.)`) → `IsDrawCard(...)` is `false`. Confirm an existing/added true-case (non-reminder-text "Draw a card") still passes.
   - PlanRoleClassifierTests.cs: if you judge a new test is warranted for the front-face-first precedence fix (a fact with differing FrontFaceOracleText vs OracleText producing different PlanRole results before/after), add one; otherwise note in your report why the existing coverage is sufficient.
9. Run `dotnet build` and confirm 0 errors, 0 new warnings.
10. Run the full DeckFlow.Core.Tests and DeckFlow.Web.Tests suites (`dotnet test`) and confirm all pass, including Manabase golden/byte-identity tests (grep for "golden"/"ByteIdentical" if unsure which suite). If any Manabase test fails or a golden snapshot changes because of the draw-regex fix, STOP — do not modify/rebaseline the golden file yourself — report it under DONE_WITH_CONCERNS with the exact failing test name and diff for human review.
11. Grep for any other place reading `CutLabFloorRules.RoleKeys` or hardcoding the 8 role keys (including any `.cshtml`/TypeScript) to confirm nothing else needs updating; report what you found even if nothing needed changing.

MUST NOT:
- Do not add "other" to `CutLabFloorRules.RoleKeys`.
- Do not touch ramp classification (DeckStatClassifier.IsRampCard, any ManabaseClassifier ramp predicate).
- Do not touch ScryfallCardFactMapper.cs or CardFact.cs.
- Do not touch `CutLabUiPatchBuilder.BuildAdjustRoleAssignments`.
- Do not change the "Unresolved Card" assertions in CutLabPageServiceTests.cs, or the `ResolvedFloors`-keys assertion.
- Do not rebaseline/regenerate any Manabase golden test snapshot yourself if the draw-regex fix shifts them — flag it instead.
- Do not attempt the "Cut Lab wraps Manabase" architecture change — out of scope, explicitly deferred by the user.
- Do not commit. Leave changes uncommitted for Claude to review and commit.
- Do not spawn subagents. Do not run `git push`, `git reset --hard`, or any destructive git command.

OUTPUT FORMAT: First line exactly one of DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT / BLOCKED. Then: every file changed with a one-line description, the exact build/test commands run and their pass/fail result (paste key output), and the list of new test names added. If step 10 surfaced any Manabase golden/test concern, describe it in full detail regardless of overall status.

WRITE SET:
- DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs
- DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs
- DeckFlow.Core/Analysis/DeckStatClassifier.cs
- DeckFlow.Web/Models/CutLabViewModel.cs
- DeckFlow.Web/Services/CutLab/CutLabUiPatchBuilder.cs
- DeckFlow.Web.Tests/CutLabRoleAssignerTests.cs
- DeckFlow.Core.Tests/DeckStatClassifierTests.cs
- DeckFlow.Web.Tests/CutLabPageServiceTests.cs
- DeckFlow.Web.Tests/Manabase/PlanRoleClassifierTests.cs (only if you add the optional test from MUST DO #8)
(No other files may be created or modified. If you believe a file outside this set genuinely needs a change, STOP and report NEEDS_CONTEXT explaining why instead of editing it.)
