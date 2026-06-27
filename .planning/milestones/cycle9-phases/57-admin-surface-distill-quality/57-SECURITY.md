# Phase 57 — Security Audit (Admin Surface + Distill Quality)

**Phase:** 57 — Admin Surface + Distill Quality (SITE-01 + DIST-01)
**Audit range:** `93dbde8..6aa4a5f` (branch `cycle9`)
**ASVS Level:** 1
**Block-on:** open
**Overall verdict:** SECURED — 8/8 threats resolved (5 mitigate/mitigate-verified, 3 accept-verified). 0 OPEN.

This audit verifies that every declared disposition in the two plan STRIDE registers
(57-01, 57-02) is honored by the committed code. Implementation files are read-only;
no patches were applied. Every verdict cites file:line or git-diff evidence.

---

## Threat Verification — Plan 57-01 (SITE-01: Admin Publish State Column)

| Threat ID | Category | Disposition | Verdict | Evidence |
|-----------|----------|-------------|---------|----------|
| T-57-01-01 | Information Disclosure | accept | ACCEPTED-VERIFIED | Column is derived **only** from existing operator-created DB fields. `AdminContentKbController.cs:79-81` maps `PushedToProdUtc = r.PushedToProdUtc`, `IndexedUtc = r.IndexedUtc`, `PublishState = _deriver.Derive(r.PushedToProdUtc, r.IsVisible, r.IndexedUtc)` — all sourced from the trusted `IContentSiteIndexStore` row, no user PII. **No new route added**: controller diff (`93dbde8..6aa4a5f`) adds zero `[HttpGet]`/`[HttpPost]` attributes — only the `_deriver` field + three mapping initializers. Page stays behind the `/Admin` BasicAuth branch (class `[Route("Admin/ContentKb")]`, `AdminContentKbController.cs:17`; gated by `BasicAuthMiddleware` per architecture). |
| T-57-01-02 | Tampering | accept | ACCEPTED-VERIFIED | New `KbEntryRow` props use `{ get; init; }` so System.Text.Json will not silently skip them (project carve-out honored): `PushedToProdUtc { get; init; }` (`AdminContentKbViewModel.cs:90`), `required DateTimeOffset IndexedUtc { get; init; }` (`:93`), `PublishState PublishState { get; init; } = PublishState.NeverPublished` (`:96`). All three are populated **server-side** from the trusted store row and the pure deriver (`AdminContentKbController.cs:79-81`); no user input flows into them — no model-binding surface (they are not request-bound DTO fields). No injection surface. |
| T-57-01-03 | Elevation of Privilege | mitigate | MITIGATED | `PublishStateDeriver` registered as a **singleton**: `Program.cs:97` `builder.Services.AddSingleton<DeckFlow.Core.Content.PublishStateDeriver>();`. The type is `sealed`, has **zero instance fields, no constructor, no I/O, and no mutable state** (`PublishStateDeriver.cs:6-37` — a single pure `Derive(...)` method). Cannot leak state across requests. Mirrors the Studio registration. |
| T-57-01-SC | Tampering (supply chain) | accept | ACCEPTED-VERIFIED | Zero dependency changes in range: `git diff --stat 93dbde8..6aa4a5f` over `*.csproj`/`*.props`/`package.json`/`*lock*` returns **empty**. No new NuGet/npm installs. |

### Razor output-encoding check (T-57-01-01 / -02 supporting)
The new cell renders `@entry.PublishState.ToDisplayString()` and a CSS class via a normal
Razor `@( ... switch ... )` interpolation (`Index.cshtml:169-177`). Both paths are
**auto-HTML-encoded** by Razor. `grep` for `Html.Raw`/`MarkupString` in `Index.cshtml`
returns **0** — no raw-output bypass, no injection surface introduced. The empty-filter
`colspan` was correctly bumped 5 → 6 (`Index.cshtml:251`); existing columns unshifted.

---

## Threat Verification — Plan 57-02 (DIST-01: Distill Prompt Rework)

| Threat ID | Category | Disposition | Verdict | Evidence |
|-----------|----------|-------------|---------|----------|
| T-57-02-01 | Tampering (prompt-injection via transcript) | accept | ACCEPTED-VERIFIED | Pre-existing, unchanged risk. The diff (`DistillationSchemas.cs`) changes **prose only** inside the four system-prompt strings — no new injection vector. Output remains bounded by `DistillationValidation` (unchanged in range): `SummaryMaxWords=200`, `MinClipCount=3`, `MaxClipCount=8`, `SanitizeClips`/`SanitizeTags` (`DistillationValidation.cs:17-19,77`). `git diff --stat` shows `DistillationValidation.cs` **not touched**. The allowlist gate is intact — the three `FormatAllowlist(ContentTagVocabulary.*)` interpolations remain in `TagsSystemPrompt` (`DistillationSchemas.cs` diff, unchanged interpolation lines). |
| T-57-02-02 | Tampering (schema/validation contract drift) | mitigate | MITIGATED | Zero schema drift: `git diff 93dbde8..6aa4a5f -- DistillationSchemas.cs` filtered for `SummarySchema`/`ClassificationSchema`/`ClipsSchema`/`TagsSchema`/`FormatAllowlist` returns **no added/removed lines** — only prompt prose changed. The contract guard `ResponseFormatSchemas_MatchShippedPhase21Fixtures` is **not in the test diff** (untouched, stays green), proving the schema contract is byte-identical. `DistillationValidation.cs` byte-identical (not in diff). |
| T-57-02-03 | Tampering (raw-string re-indentation) | mitigate | MITIGATED | Carve-out honored: the raw-string `"""` delimiters are **not re-indented** — the diff keeps the opening `= """` on the same line and the closing `"""` at its original column for all three raw-literal prompts (Summary/Classification/Clips). Only the text between delimiters changed. `TagsSystemPrompt` (a string concatenation, not a raw literal) edited as permitted. CarveOutGuard + format-gate enforce this per plan acceptance. |
| T-57-02-SC | Tampering (supply chain) | accept | ACCEPTED-VERIFIED | Zero dependency changes in range (same `git diff --stat` over project/lock files: empty). No new installs. |

### Test fixture refresh (intended, not drift)
`DistillationPromptRegressionTests.cs` diff is the intended fixture realignment:
prompt fixtures updated to match the reworked prose **plus** a new
`expectedClassificationPrompt` fixture + `Assert.Equal(expectedClassificationPrompt,
DistillationSchemas.ClassificationSystemPrompt)` — a **coverage increase** that newly
pins `ClassificationSystemPrompt`. The `ResponseFormatSchemas` schema-guard fixture is
deliberately **not** modified.

---

## Unregistered Flags

None. No SUMMARY `## Threat Flags` section declared new attack surface; the implemented
diff matches the planned `files_modified` exactly (4 prod files + 2 test files + planning
docs) — no out-of-plan endpoints, inputs, secrets, or network calls appeared.

---

## Deferred / Out-of-Scope (informational)

- **DOGFOOD-01 (Phase 58):** distill-quality before/after on real harvested content is an
  operator inspection, explicitly out of scope for this security audit and for automated
  verification. Not a security item.
- **Runtime confirmation of BasicAuth gate:** verified by code (route prefix + middleware
  branch) per architecture; the `/Admin` BasicAuth middleware itself is unchanged by Phase 57.

---

## Result

```
SECURED
Phase 57 — Admin Surface + Distill Quality
Threats Closed: 8/8 (T-57-01-01/02/03/SC, T-57-02-01/02/03/SC)
Open: 0
ASVS Level: 1
```
