# 31-05 SUMMARY — Primer Zip Round-Trip + Result JSON Guard

**Status:** COMPLETE (Codex impl / Claude review pending) — 2026-06-09
**Requirements:** PRM-09
**Wave:** 3 (`depends_on: ["31-02", "31-03"]`)

## What shipped

- **`BuildPrimerZip`** — added to `PacketArtifactStore` with the primer-specific artifact names, conditional per-platform prompt writes, and `all-primer-prompts.txt` built from only the prompt variants that are actually present in the packet.
- **`LoadPrimerFromZip`** — added to `PacketArtifactStore` using `ReadEntries(zipStream, PrimerAllowedNames)` so primer uploads stay on the existing allowlist + size-cap path and reject cross-workflow entries.
- **Primer request-context restore** — `LoadPrimerFromZip` restores `TargetCommanderBracket`, normalized `TargetAiPlatform`, and ordered `SelectedSectionIds` from `01-primer-request-context.txt`, while tolerating missing keys and 2-prompt zips.
- **`SuggestPrimerZipFileName`** — added alongside the existing filename helpers with the same safe-segment + UTC timestamp pattern.
- **`PacketArtifactStorePrimerTests`** — new in-memory zip tests cover request-context round-trip, skip-when-null Gemini omission, allowlisted entry names only, and non-primer entry rejection.
- **`DeckPrimerResultRoundTripTests`** — new `System.Text.Json` guard verifies `DeckPrimerPacketResult` preserves all properties, including the `PromptTextsByPlatform` dictionary, across serialize/deserialize round-trip.

## Deviations

- **No scope-fence deviations.** Only the allowed artifact-store file, the two allowed test files, and this required summary file were touched.
- **Pre-existing unrelated worktree changes remain untouched.** `.planning/spikes/001-kb-value-ab/baseline.txt` and `.planning/spikes/001-kb-value-ab/with-context.txt` were already modified before this plan execution and were excluded from commits.

## Verification

- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web/DeckFlow.Web.csproj -warnaserror:CS1591` → Build succeeded, 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" build DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj` → Build succeeded, 0 errors.
- `"/mnt/c/Program Files/dotnet/dotnet.exe" test DeckFlow.Web.Tests/DeckFlow.Web.Tests.csproj --filter "PacketArtifactStorePrimerTests|DeckPrimerResultRoundTripTests"` → **5/5 passed, 0 failed**.
- Acceptance greps passed for `BuildPrimerZip`, `LoadPrimerFromZip`, `SuggestPrimerZipFileName`, and `ReadEntries(zipStream, PrimerAllowedNames)`.

## Notes / next

- `BuildPrimerZip` writes one prompt entry per present platform variant, so Gemini-disabled packets naturally omit `30-primer-gemini-prompt.txt` and still reload cleanly.
- The request-context parser for primer uploads is intentionally tolerant and only restores the fields required by PRM-09; downstream section validation still happens in the primer service.
