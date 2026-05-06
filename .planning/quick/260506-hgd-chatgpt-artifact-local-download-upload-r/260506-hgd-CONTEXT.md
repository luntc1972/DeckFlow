# Quick Task 260506-hgd: ChatGPT artifact local download/upload — replace server-side save and import - Context

**Gathered:** 2026-05-06
**Status:** Ready for planning

<domain>
## Task Boundary

Replace the server-side ChatGPT artifact persistence layer (currently disabled by stopgap commit `0021908`) with a local-only download/upload flow. Three pages affected: ChatGPT Analysis (`/chatgpt-packets`), ChatGPT Deck Comparison (`/chatgpt-deck-comparison`), ChatGPT cEDH Meta Gap (`/chatgpt-cedh-meta-gap`).

User experience target: a single round-trip artifact file the user can download to their machine, store wherever they want, and upload back later to resume any saved session. Zero shared server-side state.

</domain>

<decisions>
## Implementation Decisions

### Format
- **Single zip** for download and re-import. Both surfaces are paired: clicking Download produces a `.zip`; the re-import flow accepts the same `.zip` via a single `<input type="file" accept=".zip">` picker. Server unzips on upload, reads `40-deck-profile.json` + `51-set-upgrade-response.json` (or comparison/cedh equivalents) into the request, hydrates the form, and renders the downstream step panel.

### When download happens
- **Manual Download button on each results panel.** No auto-download, no opt-in checkbox at top of form. User decides what's worth saving and clicks explicitly. Place the button inside the relevant results panel (Step 3 results for analysis, Step 5 results for set upgrade — and equivalents on Comparison and Meta Gap pages).

### Existing /data/ChatGPT files cleanup
- **Leave as-is.** Stopgap commit `0021908` already disabled all read and write paths. No deletion code in this restructure. Cleanup is a manual operator step (Render shell + `rm -rf`) handled separately.

### Partial-data download policy
- **Everything available.** When the user clicks Download, the zip includes every artifact that has content: prompts (`31-analysis-prompt.txt`, `50-set-upgrade-prompt.txt`, etc.), summaries (`00-input-summary.txt`, `01-request-context.txt`), schemas (`41-deck-profile-schema.json`), and responses (`40-deck-profile.json`, `51-set-upgrade-response.json`). Re-import only consumes the response JSON files; the rest are reference/audit artifacts that ride along.

### Claude's Discretion
- **Backend implementation seam.** Reuse `ChatGptPacketArtifactStore.SaveAsync` logic but stream into an in-memory `ZipArchive` instead of disk. Same for the comparison and cedh services.
- **Server endpoint shape for download.** Pick whichever fits the existing `IActionResult` pattern: `FileContentResult` with `application/zip` and `Content-Disposition: attachment` is most idiomatic. Filename: `<commander-or-page>-<timestamp>.zip`.
- **Server endpoint shape for upload.** New `[HttpPost]` action per page that accepts `IFormFile`, unzips, validates filenames against an allow-list (no path traversal), reads the JSON files, returns the same view model the existing controllers produce after a successful import. Wire into the same form replacing the current "Import saved session from folder" details panel.
- **Antiforgery / SameOrigin.** Reuse existing patterns. Upload endpoint posts via the same form, gets `[ValidateAntiForgeryToken]` and the standard `SameOriginRequestValidator` flow.
- **Removing the now-dead path.** Delete `ChatGptArtifactsDirectory`, `ChatGptPacketArtifactStore` filesystem paths, the `/api/saved-sessions` endpoint (currently returns `[]`), `IChatGptArtifactsDirectory` DI registration. Delete or repurpose `SaveArtifactsAsync` callsites in all three services. Drop `ImportArtifactsPath` and `SaveArtifactsToDisk` from request models. Drop matching view fragments and TS handlers (`loadSavedSessionsAsync`, the saved-session dropdown wiring).
- **Comparison + Meta Gap page UX.** Mirror the Analysis page's Download/Upload buttons. Same one-zip pattern; per-page artifact lists differ but the wrapper UX is identical.
- **TypeScript wiring.** Hook `<input type="file">` change handler that POSTs as multipart to the upload endpoint, then either follows the redirect or replaces the form via the existing busy-indicator/post pattern. Download is a simple anchor with `href` pointing at the download endpoint plus current form state — or a button that POSTs the form to a download action that streams the zip back. Pick whichever avoids re-running expensive Scryfall/banlist work.
- **Memory cap.** Render Starter is 512MB. Build zip in `MemoryStream`; one zip per request, kept under a few MB given the textual artifacts. No streaming concerns.

</decisions>

<specifics>
## Specific Ideas

- The current artifact file inventory is the canonical reference for what goes into the zip. See `ChatGptPacketArtifactStore.SaveAsync` (`DeckFlow.Web/Services/ChatGptPacketArtifactStore.cs`) for the exact `(FileName, Label, Content)` tuple list. Equivalents in `ChatGptDeckComparisonService.SaveArtifactsAsync` and `ChatGptCedhMetaGapService.SaveArtifactsAsync`.
- The existing `LoadInto` logic in `ChatGptPacketArtifactStore` already handles partial imports gracefully (only `40-*` loaded → WorkflowStep=3; only `51-*` loaded → WorkflowStep=5; neither → throw). The new upload endpoint should preserve that behavior — same WorkflowStep transitions, same error message style.
- Stopgap commit `0021908` is the baseline for this work. After restructure, the README "Artifact saving (temporarily disabled)" sections should be rewritten to describe the new download/upload flow.

</specifics>

<canonical_refs>
## Canonical References

- Stopgap commit on `main`: `0021908` — `fix(chatgpt): disable server-side artifact save and import`
- Affected file inventory: `DeckFlow.Web/Services/ChatGpt{DeckPacketService,DeckComparisonService,CedhMetaGapService,ArtifactsDirectory,PacketArtifactStore}.cs`, `DeckFlow.Web/Controllers/DeckController.cs`, three view files under `DeckFlow.Web/Views/Deck/ChatGpt*.cshtml`, request models `DeckFlow.Web/Models/ChatGpt*Request.cs`, TS handlers in `DeckFlow.Web/wwwroot/ts/deck-sync.ts` (search `loadSavedSessionsAsync`, `data-saved-sessions-*`, `data-chatgpt-import-*`).
- README sections to rewrite: lines 272-273, 354-356, 423-425 of `README.md` ("Artifact saving (temporarily disabled)" → new local download/upload description).

</canonical_refs>
