TASK: Continuation of the same Cut Lab classification fix (this is the SAME code you already edited in this worktree — your prior 9-file diff is still uncommitted and correct; do not redo it, only add the one missing piece below and then report).

You previously stopped with NEEDS_CONTEXT because `DeckFlow.Web/Views/Deck/CutLab.cshtml` (around line 528) unconditionally renders a "Lock all X" button (`data-cut-lab-lock-role="@group.RoleKey"`) for every entry in `Model.RoleGroups`, which would make the new "other" display bucket lockable — violating the "other must be display-only, not floor/lockable" requirement. You were correct to stop rather than exceed your write set.

MUST DO NOW:
1. In `DeckFlow.Web/Views/Deck/CutLab.cshtml`, wrap ONLY the "Lock all @lowerDisplayLabel" `<button ... data-cut-lab-lock-role="@group.RoleKey">` element (around line 536-541) in a condition that skips it when `group.RoleKey` is `"other"` (e.g. `@if (!string.Equals(group.RoleKey, "other", StringComparison.Ordinal)) { <button>...</button> }`). Do NOT change anything else in this file — not the `<details>`/`<summary>` header, not the members chip list, not the empty-hint paragraph, not the existing "interaction" help-text block a few lines below. The "Other" group should still show its member cards and the "no cards" hint when empty — it just gets no lock button.
2. Preserve this file's existing line endings exactly (check before editing — do not assume repo-wide LF or CRLF).
3. Re-verify: grep the rest of `CutLab.cshtml` and any Cut Lab TypeScript under `DeckFlow.Web/wwwroot/ts/` for other places keyed on the 8 role strings (e.g. any hardcoded role-key list or `data-cut-lab-lock-role` JS handler) that might also need to special-case "other" — if you find one, report it in your output even if you judge it doesn't need a code change (out of your current write set) rather than editing it silently.
4. Do NOT attempt to run `dotnet build`/`dotnet test` — your sandbox cannot invoke the .NET SDK here (already confirmed: `dotnet: command not found` and a WSL-interop vsock failure on the Windows dotnet.exe path). Claude will run the build/test suite independently after your report. Just make the file edit and report.

MUST NOT: do not touch any file outside `DeckFlow.Web/Views/Deck/CutLab.cshtml`. Do not commit. Do not spawn subagents.

OUTPUT FORMAT: First line exactly DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT / BLOCKED. Then the exact diff hunk for CutLab.cshtml, and the result of the grep sweep from step 3.

WRITE SET:
- DeckFlow.Web/Views/Deck/CutLab.cshtml
