TASK: Fix four Cut Lab card-classification bugs in the DeckFlow repo (branch feat/cutlab-fixes, worktree already checked out at repo root).

EXPECTED OUTCOME:
1. An MDFC (modal double-faced) card with a land back face (e.g. a Spell // Land MDFC) is counted in the "lands" role, not missed.
2. Cut Lab's role classification reads front-face oracle text first (matching how Manabase/PlanRoleClassifier already do it), not joined-all-faces text first — so back-face text no longer bleeds into a card's role.
3. Raugrin Triome (and every other cycling card) is NOT classified as "draw" just because its Cycling reminder text contains the literal phrase "Draw a card". The shared literal-draw regex must ignore text inside parentheses (reminder text), since MTG reminder text is always parenthesized on Scryfall oracle_text.
4. A card that matches none of Cut Lab's 8 structural roles falls into a new "Other" display bucket in the "How Your Pool Competes" panel instead of silently vanishing from every role count — WITHOUT becoming a floor/lockable role (see MUST NOT below — this is a display-only bucket).
5. `dotnet build` is clean, the full DeckFlow.Core.Tests + DeckFlow.Web.Tests suites pass, and new regression tests exist for each of the 4 fixes above (including a literal Raugrin Triome cycling-text test case and an MDFC land test case).

CONTEXT (read these first, in this order):
- docs/decisions/0003-ramp-classifier-divergence.md — ADR governing this area. Ramp classifiers (DeckStatClassifier.IsRampCard vs Manabase's ramp predicates) are INTENTIONALLY divergent — do not touch/unify ramp. Draw (DeckStatClassifier.MatchesYouCardDraw) is the INTENTIONALLY SHARED literal-draw signal used by both Cut Lab and Manabase (ManabaseClassifier.IsYouCardDraw delegates to it) — fixing a genuine bug in it is in scope and desired, but the ADR explicitly warns changes here can shift Manabase's golden/byte-identity tests and land-target math and must be re-verified, never assumed unaffected.
- DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs — the role-assignment method (AssignRoles), lines ~63-131. This is where fixes 1, 2, 4 (assignment side) land.
- DeckFlow.Core/Analysis/DeckStatClassifier.cs — YouCardDrawRegex (~line 24-26) and MatchesYouCardDraw (~line 55) and IsDrawCard (~line 62-65). This is where fix 3 lands.
- DeckFlow.Core/Manabase/CardFact.cs — has `HasLandFace` (bool) and `FrontFaceOracleText` (string?) already correctly populated for MDFC/DFC cards by ScryfallCardFactMapper.cs (HasLandFace method ~line 84, and OracleText/FrontFaceOracleText assembly ~lines 39-42, 70-82). Do NOT change ScryfallCardFactMapper or CardFact — the data is already correct; Cut Lab just isn't reading it right.
- DeckFlow.Web/Services/Manabase/PlanRoleClassifier.cs line ~176 — the sibling classifier that ALREADY does `fact.FrontFaceOracleText ?? fact.OracleText ?? string.Empty` (front-face-first). Cut Lab's CutLabRoleAssigner.cs line ~73 currently has the operands REVERSED (`fact.OracleText ?? fact.FrontFaceOracleText`) — this is the "align with Manabase" bug. Also see ManabaseClassifier.cs lines ~1097, ~1156, ~1841 for the same front-face-first pattern used elsewhere in Manabase.
- DeckFlow.Web/Services/CutLab/CutLabLockRules.cs — `IsLand(typeLine)` (~line 102) checks only the front-face type line via `CardTypeLine.FrontFace`. Do not change this method itself (it's correctly front-face-only for its OTHER callers) — instead, CutLabRoleAssigner must OR it with `fact.HasLandFace` locally.
- DeckFlow.Web/Services/CutLab/CutLabFloorRules.cs — `RoleKeys` (the 8 canonical floor-eligible role keys) and `TryGetCanonicalRole`. **DO NOT add "other" to this list** (see MUST NOT).
- DeckFlow.Web/Services/CutLab/CutLabFloorDefaults.cs — `GetBracketBand(role, bracket)` (~line 101-125) has a switch over role name with `_ => throw new ArgumentOutOfRangeException(...)` as the default case. If "other" were ever added to `CutLabFloorRules.RoleKeys`, `ResolveDefaults`'s `foreach (string role in CutLabFloorRules.RoleKeys)` loop would call this and THROW on every single Cut Lab page load. This is exactly why "other" must stay out of `CutLabFloorRules.RoleKeys`.
- DeckFlow.Web/Models/CutLabViewModel.cs:
  - `RoleDisplayLabels` dict (~line 39-50) — add `["other"] = "Other"`.
  - `BuildRoleGroups` (~line 373-404) — currently does `CutLabFloorRules.RoleKeys.Select(roleKey => ...)`. Change it to iterate a NEW local display-order list that is `CutLabFloorRules.RoleKeys` followed by `"other"` — mirror the EXACT existing pattern used a few lines below for `TypeGroupOrder` (~line 52-64: a separate `private static readonly string[] TypeGroupOrder = [...​, "Other"]` array, decoupled from `CardTypeLine.PrimaryTypePriority`, with a comment noting it appends the fallback bucket). Do the same thing here: add e.g. `private static readonly string[] RoleGroupDisplayOrder = [.. CutLabFloorRules.RoleKeys, "other"];` (or a plain literal array duplicating the 8 keys + "other" — match whichever style keeps it simplest) and have `BuildRoleGroups` iterate that instead of `CutLabFloorRules.RoleKeys` directly. The `.Where(card => ... roles.Contains(roleKey, ...))` membership logic inside the select does not need to change — a card with `["other"]` in its assigned-roles list will naturally show up under roleKey `"other"`.
  - `BuildFloorRows` (~line 445+) and anywhere else that iterates `CutLabFloorRules.RoleKeys` directly for floor logic must be LEFT UNCHANGED — floors/locks remain exactly the original 8 roles.
- DeckFlow.Web.Tests/CutLabRoleAssignerTests.cs — existing test file/conventions to extend (uses a `Fact(...)` test helper — read the file to find its signature before writing new tests).
- DeckFlow.Core.Tests/DeckStatClassifierTests.cs — existing `IsDrawCard_TrueCases`/`IsDrawCard_FalseCases` theory tests (~lines 45-65) to extend with a cycling-reminder-text false-positive case.

CONSTRAINTS:
- C# 12 / .NET 10, existing project conventions (file-scoped namespaces, XML doc comments on public members, `sealed`/`static` as already used in these files).
- Preserve each touched file's existing line endings exactly (LF or CRLF, per-file — do not assume a repo-wide style; check each file before editing). Change only the lines whose content actually changes.
- xUnit conventions: `[Theory]`/`[InlineData]` where the file already uses them, `[Fact]` otherwise, matching each file's existing style exactly.
- Do NOT touch DeckStatClassifier.IsRampCard or any Manabase ramp predicate — ramp stays intentionally divergent per ADR 0003.
- Do NOT change ScryfallCardFactMapper.cs or CardFact.cs — MDFC/land-face data is already correct there.

MUST DO:
1. In `CutLabRoleAssigner.AssignRoles`: change `bool isLand = CutLabLockRules.IsLand(typeLine);` to also OR in `fact.HasLandFace` (e.g. `bool isLand = CutLabLockRules.IsLand(typeLine) || fact.HasLandFace;`), and change the ramp gate a few lines below (`if (!CutLabLockRules.IsLand(typeLine) && DeckStatClassifier.IsRampCard(typeLine, oracle))`) to reuse the local `isLand` variable instead of recomputing (`if (!isLand && DeckStatClassifier.IsRampCard(typeLine, oracle))`) — otherwise an MDFC land could double-count as both "lands" and "ramp".
2. In the same method, swap the oracle-text precedence: `string oracle = fact.OracleText ?? fact.FrontFaceOracleText ?? string.Empty;` → `string oracle = fact.FrontFaceOracleText ?? fact.OracleText ?? string.Empty;`.
3. In `DeckStatClassifier.cs`: add a private helper that strips parenthetical reminder text (regex `\([^)]*\)` → empty, or equivalent) and apply it inside `MatchesYouCardDraw` before running `YouCardDrawRegex` (e.g. `internal static bool MatchesYouCardDraw(string oracleText) => YouCardDrawRegex.IsMatch(StripReminderText(oracleText));`). Add an XML doc / inline comment explaining why (cycling's "(..., Discard this card: Draw a card.)" reminder text was a false positive for every cycling card, e.g. Raugrin Triome). Do not change `IsRampCard`.
4. Add the "other" fallback: in `CutLabRoleAssigner.AssignRoles`, after building the `assigned` list, if it is empty, add a new `OtherRole = "other"` constant to the assigned list before returning. Wire the display side exactly as described in CONTEXT above (RoleDisplayLabels + new display-order array in CutLabViewModel.cs) — do NOT touch CutLabFloorRules.RoleKeys.
5. Add regression tests:
   - CutLabRoleAssignerTests.cs: an MDFC Spell // Land card (e.g. a fictional "Test Spell // Test Land" fact with `HasLandFace = true` and front-face type line NOT containing "Land") asserts `roles` contains "lands" and does NOT contain "ramp". A card whose `OracleText` (joined) contains draw text only on a back face but whose `FrontFaceOracleText` does not, asserts "draw" is NOT in the result. A card matching none of the 8 roles (e.g. a vanilla noncreature/nonland with no matching oracle text) asserts `roles` equals exactly `["other"]`.
   - DeckStatClassifierTests.cs: a literal Raugrin-Triome-style oracle text (tri-land: "(Tapland) enters the battlefield tapped.\n{T}: Add {R}, {U}, or {W}.\nCycling {3} ({3}, Discard this card: Draw a card.)") asserts `DeckStatClassifier.IsDrawCard(...)` is `false`. Keep existing true-case coverage (e.g. a card whose non-reminder-text body literally says "Draw a card") passing unchanged.
6. Run `dotnet build` (repo root — check for the correct dotnet invocation; if WSL/Windows path issues arise, look for scripts/ helpers) and confirm 0 errors, 0 new warnings.
7. Run the full DeckFlow.Core.Tests and DeckFlow.Web.Tests suites (`dotnet test`) and confirm all pass, INCLUDING any Manabase golden/byte-identity tests (grep for "golden" or "ByteIdentity" in DeckFlow.Core.Tests/DeckFlow.Web.Tests if unsure which suite they're in) — the shared draw-regex change (fix #3) can in principle shift Manabase output per ADR 0003. If any Manabase test fails or a golden snapshot changes as a result of the reminder-text fix, STOP, do not modify/rebaseline the golden file yourself, and report it clearly under DONE_WITH_CONCERNS with the exact failing test name and diff — this needs human review, not silent rebaselining.
8. Grep the codebase for any other place that reads `CutLabFloorRules.RoleKeys` or the 8-key role list to confirm nothing else needs updating for the new "other" role (e.g. any TS/cshtml that hardcodes the 8 roles) and report what you found even if nothing needed changing.

MUST NOT:
- Do not add "other" to `CutLabFloorRules.RoleKeys` (breaks `CutLabFloorDefaults.GetBracketBand` — see CONTEXT).
- Do not touch ramp classification (DeckStatClassifier.IsRampCard, any ManabaseClassifier ramp predicate) — ADR 0003 forbids unifying/changing these in this task.
- Do not touch ScryfallCardFactMapper.cs or CardFact.cs.
- Do not rebaseline/regenerate any Manabase golden test snapshot files yourself if the draw-regex fix shifts them — flag it instead.
- Do not commit. Leave changes uncommitted in the working tree for Claude to review and commit.
- Do not spawn subagents.
- Do not run `git push`, `git reset --hard`, or any destructive git command.

OUTPUT FORMAT: First line is exactly one of DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT / BLOCKED. Then: list every file changed with a one-line description of the change, the exact build/test commands you ran and their pass/fail result (paste key output, not just "passed"), and a list of the new test names added. If step 7 surfaced any Manabase golden/test concern, describe it explicitly and in detail even if you otherwise report DONE_WITH_CONCERNS.

WRITE SET:
- DeckFlow.Web/Services/CutLab/CutLabRoleAssigner.cs
- DeckFlow.Core/Analysis/DeckStatClassifier.cs
- DeckFlow.Web/Models/CutLabViewModel.cs
- DeckFlow.Web.Tests/CutLabRoleAssignerTests.cs
- DeckFlow.Core.Tests/DeckStatClassifierTests.cs
(No other files may be created or modified. If you believe a file outside this set genuinely needs a change, STOP and report NEEDS_CONTEXT explaining why instead of editing it.)
