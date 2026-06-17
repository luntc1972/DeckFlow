# ReSharper Export Reconciliation

This report records the D-02 reconciliation of `.planning/rs-export.editorconfig` into the committed `.editorconfig` and the D-03 deletion of the transient export after a successful merge. The committed `.editorconfig` remains the source of truth; carve-outs and the LF constraint override conflicting ReSharper export preferences.

| RS Key | RS Value | Committed Value | Resolution | Enforceable? | Reason |
| --- | --- | --- | --- | --- | --- |
| `root` | `true` | `true` | `KEEP-EXISTING` | `n/a` | Already matched the committed root setting; no change required. |
| `end_of_line` under `[*]` | `crlf` | `lf` | `REJECT (constraint-wins / KEEP-EXISTING-LF)` | `n/a` | Conflicted with committed `[*] end_of_line = lf` and `.gitattributes` `* text=auto eol=lf`, so LF stayed authoritative. |
| `insert_final_newline` under `[*]` | `true` | `true` | `KEEP-EXISTING` | `n/a` | Already matched the committed setting; no change required. |
| `resharper_align_multiline_binary_expressions_chain` | `false` | `(absent)` | `ADOPT-RS` | `JetBrains-only (resharper_*)` | Added additively in `[*.cs]`; Rider/ReSharper-only hint and invisible to `dotnet format`. |
| `resharper_indent_braces_inside_statement_conditions` | `false` | `(absent)` | `ADOPT-RS` | `JetBrains-only (resharper_*)` | Added additively in `[*.cs]`; Rider/ReSharper-only hint and invisible to `dotnet format`. |
| `resharper_stick_comment` | `false` | `(absent)` | `ADOPT-RS` | `JetBrains-only (resharper_*)` | Added additively in `[*.cs]`; Rider/ReSharper-only hint and invisible to `dotnet format`. |
| `CodeEditing/GenerateMemberBody/PlaceBackingFieldAboveProperty` | `true` | `n/a` | `IGNORE` | `n/a` | Export self-documents this as having no useful EditorConfig equivalent. |
| `CodeInspection/Highlighting/IdentifierHighlightingEnabled` | `true` | `n/a` | `IGNORE` | `n/a` | Export self-documents this as having no useful EditorConfig equivalent. |
| `CodeStyle/CodeCleanup/RecentlyUsedProfile` | `Built-in: Full Cleanup` | `n/a` | `IGNORE` | `n/a` | Export self-documents this as having no useful EditorConfig equivalent. |
| `CodeStyle/Naming/CSharpAutoNaming/IsNotificationDisabled` | `true` | `n/a` | `IGNORE` | `n/a` | Export self-documents this as having no useful EditorConfig equivalent. |

## Carve-out conflict review

No RS key collides with init / raw-string / attribute / switch / xmldoc — the only conflict is EOL (rejected on the LF constraint). The three adopted keys are all `resharper_*`, so they are JetBrains-only and invisible to `dotnet format`; they cannot cause the changed-lines gate to rewrite the carve-out fixtures.

## Line-ending constraint

The export's `[*] end_of_line = crlf` was rejected. The committed `[*] end_of_line = lf` stayed in place, and the existing `[*.{ps1,bat,cmd}] end_of_line = crlf` exception remained untouched.

## Carve-out integrity

All five existing carve-outs remain verbatim in `.editorconfig`: init accessors, switch expressions, attribute placement, xmldoc indent, and raw-string indent.

Gate impact: zero gate-enforceable (dotnet_*/csharp_*) keys added — the changed-lines gate enforces the existing committed style.

Per D-03, the transient `.planning/rs-export.editorconfig` input was deleted after the successful merge; this report is the permanent audit trail.
