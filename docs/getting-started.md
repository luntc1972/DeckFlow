# Getting started

DeckFlow setup, development, and deployment guidance.

## Getting Started
1. Restore/build: `dotnet build DeckFlow.sln`
2. Run the web app: `dotnet run --project DeckFlow.Web`
3. Use the CLI to compare or harvest decks: `dotnet run --project DeckFlow.CLI -- --help`

### Helper scripts
- `scripts/run-web.sh` — bash wrapper that rebuilds `DeckFlow.Web` and launches it on `http://localhost:5173` with no browser auto-launch.
- `scripts/run-web.ps1` — PowerShell equivalent for Windows terminals.
- `scripts/publish-studio.ps1` — publishes `DeckFlow.Studio` as a self-contained win-x64 single-file executable (no .NET install required on the target machine). Run from Windows PowerShell; produces `artifacts/studio-release/` and `artifacts/DeckFlowStudio-<date>.zip`. See [DeckFlow.Studio/STUDIO-SETUP.md](../DeckFlow.Studio/STUDIO-SETUP.md) for full setup, launch, and secrets configuration steps. The git-backed flows (Publish, Direct Push, Pull from Prod) run git from the process working directory; to publish from a distributed exe that lives outside the repo, set `DECKFLOW_REPO_ROOT` to the repo working tree (otherwise launch Studio from inside the repo).
- `scripts/publish-studio.sh` — WSL bash wrapper that does the same publish via the Windows `dotnet.exe`.

### Releasing (version + tag)
- `scripts/release.sh 2026.07.6` or `.\scripts\release.ps1 2026.07.6` updates `DeckFlow.Web/DeckFlow.Web.csproj`, commits `chore(release): 2026.07.6`, and creates the matching lightweight git tag.
- The scripts require a clean tracked working tree, reject invalid CalVer values (`YYYY.MM` or `YYYY.MM.N`), and refuse to reuse an existing tag.
- They do not push. After the script succeeds, run `git push --follow-tags`. The About page will show the new version after the next deploy because production reads the tracked csproj version, not git metadata.

### Monthly cEDH land baseline

DeckFlow commits a monthly cEDH land-baseline snapshot under `DeckFlow.Web/Data/cedh-land-baseline/` — the per-commander land-count sample the `analysis.manabase.cedh-land-target` hybrid target reads (see the mana-base analyzer notes). The pipeline is:

1. Refresh `_calib` from EDHTop16 + Scryfall with `python3 scripts/cedh-baseline/fetch.py` (defaults to a **6-month** window; `--since` / `--supplement-since` override). The fetch pulls the size-tiered top-cut results, then runs a **commander-specific search** for each name in `SUPPLEMENT_COMMANDERS` (12-month lookback) so low-play commanders that never reach a usable sample through the tiered pull are still covered — deduped and appended automatically.
2. Generate the monthly report + JSON snapshot with `dotnet run --project DeckFlow.CLI -- cedh-land-baseline --data _calib --month YYYY-MM`
3. Commit the new dated `YYYY-MM.md` / `YYYY-MM.json` files plus the refreshed `latest.json`

The CLI reclassifies each cached deck through `DeckFlow.Core/Manabase/` (the app's own classifier), applies the cEDH gate (avgMV ≤ 2.7, 95–101 cards), and emits the web-app JSON contract (`generated`, `sampleSize`, `overallMeanLands`, per-commander `n`/`landsMean`/`landsSd` for `n ≥ 3`) plus a human-readable monthly markdown report. The current 6-month snapshot is ~3,300 gated decks. Re-run the calibration harness after each refresh before flipping the flag on. See `scripts/cedh-baseline/README.md` for the operator runbook and how to add a commander to the supplement list.

### Commander role-floor baseline

DeckFlow commits a shipped commander-aware role-floor snapshot at `DeckFlow.Web/Data/role-floor-baseline/latest.json`. Generate it with `dotnet run --project DeckFlow.CLI -- role-floor-baseline --generated YYYY-MM-DD`. The command reads the committed Phase 2 findings artifact at `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.json` by default and writes the minified web-app snapshot under `DeckFlow.Web/Data/role-floor-baseline/`.

Options: `--findings` defaults to `.planning/workstreams/cycle21-cut-lab/phases/02-role-floor-divergence-research/RESEARCH-FINDINGS.json`; `--out` defaults to `DeckFlow.Web/Data/role-floor-baseline`; `--generated` is required and must be `YYYY-MM-DD`; `--thresholds` defaults to `scripts/role-floor-baseline/drift-thresholds.json`. The command refuses to write when the drift check fails or when any adopted role row uses a non-`postgres` source.

### Code formatting gate

DeckFlow's enforced formatting source of truth is the committed `.editorconfig`. Existing files are not mass-reflowed; the format gate checks changed C# lines only.

Install the versioned pre-commit hook once per clone:

WSL / Linux shell:
```bash
git config core.hooksPath .githooks
```

Windows Git-Bash:
```bash
git config core.hooksPath .githooks
```

After that opt-in, `.githooks/pre-commit` runs `bash scripts/format-check-changed.sh staged` on staged C# changes. A bad added line is blocked with a `file:line` failure; a clean staged change succeeds; a one-line edit in a legacy file passes when the violation is off-hunk.

CI is the authoritative enforcer. The `format-gate` job runs `bash scripts/format-check-changed.sh ci`, selects the PR/push diff base, and fails only when formatter-reported violations intersect added or modified C# lines. That means a PR with a mis-formatted added line fails, while a PR that makes a clean one-line edit in a legacy file with unrelated pre-existing quirks still passes the format gate.

### Local development TypeScript toolchain

Browser-side scripts under `DeckFlow.Web/wwwroot/ts/` compile to
`DeckFlow.Web/wwwroot/js/` via the `CompileTypeScriptAssets` MSBuild target
(BeforeTargets="Build") in `DeckFlow.Web.csproj`. The compiled `.js` files
are NOT tracked in git — `dotnet build` regenerates them every time.

First-time setup on a new dev machine:

```
cd DeckFlow.Web
npm install typescript
```

This populates `DeckFlow.Web/node_modules/typescript/` so the MSBuild target
can invoke `node ./node_modules/typescript/bin/tsc -p tsconfig.json`. The
Render production build does the equivalent in its Docker stage
(`RUN npm install typescript`), so deployments are unaffected.

If `dotnet build DeckFlow.Web` reports a missing `tsc`, run the
`npm install typescript` step above and rebuild.

### UI styling
- `DeckFlow.Web/wwwroot/css/site-common.css` contains shared shell and view-level styles that apply regardless of the selected color theme.
- `DeckFlow.Web/wwwroot/css/site*.css` files remain responsible for theme palettes and component styling.
- `DeckFlow.Web/wwwroot/css/site-mobile.css` loads after the active theme stylesheet to apply mobile-breakpoint overrides for selectors that themes redefine (e.g., `.back-to-top-button`, `.page-shell`, `.sync-column`); cascade-safe mobile rules continue to live in `site-common.css`.
- Tool pages plus the home page render a **Share DeckFlow** bar above the footer with Copy link, native device share (when supported), and Reddit / X / Bluesky actions.
- The shared footer on every page carries the Wizards of the Coast **Fan Content Policy** disclaimer (linked to the official policy), satisfying the policy's mandatory-notice condition for free fan tools.
- The theme picker now includes all ten two-color guild themes in addition to the existing wedges, shards, and specialty themes.
- Keep long-lived CSS out of Razor views; prefer shared stylesheets so caching and theme behavior stay predictable.

### Browser/API hardening
- Browser-facing JSON POST APIs now enforce same-origin `Origin`/`Referer` checks before processing deck sync, suggestion, mechanic lookup, and Archidekt cache-harvest requests.
- The old sessionStorage page-snapshot restore path was removed. DeckFlow no longer writes `main.content-shell.innerHTML` into storage or rehydrates raw HTML from storage on load.
- These checks are meant to reduce cross-site request abuse and avoid re-inserting stale or storage-poisoned markup into the DOM.

### Development-only endpoints
- `POST /api/analysis-prompt` builds the deck-analysis prompt headlessly (same `BuildAsync` pipeline as the `/deck-analysis` page) so prompts can be generated for A/B testing and automation without driving the Razor UI. It accepts a JSON body (`deckUrl` or `deckText`, plus optional `format`, `deckName`, `targetCommanderBracket`, `targetAiPlatform`, `selectedAnalysisQuestions`) and returns the generated prompt text and supporting artifacts.
- The endpoint is gated to the Development environment — it returns `404` in Production — and is same-origin guarded like the other JSON APIs.

### IIS publish
- Publish the web app with `dotnet publish DeckFlow.Web/DeckFlow.Web.csproj /p:PublishProfile=IIS-LocalFolder`
- The publish output goes to `DeckFlow.Web/bin/Release/net10.0/publish/iis-local/`
- The .NET SDK generates `web.config` during publish; there is no checked-in `web.config`
- In IIS, create an application such as `/deckflow` that points at that publish folder
- Install the ASP.NET Core Hosting Bundle on the IIS machine
- The checked-in views and scripts are path-base safe, so links and API calls stay under the IIS application path instead of jumping to `/`

### Deploying to cloud hosts (Render)
- A `Dockerfile` and `render.yaml` ship at the repo root for one-command builds on Render (the live production host).
- For durable feedback and category cache storage without a persistent disk, configure Postgres with `DECKFLOW_DATABASE_PROVIDER=Postgres` and `DECKFLOW_DATABASE_CONNECTION_STRING=<Postgres connection string>`.
- If you keep the default SQLite provider in a cloud host, set `MTG_DATA_DIR=/data` and mount a persistent volume there so `feedback.db` and `category-knowledge.db` survive deploys/restarts.
- AI session artifact folders are still filesystem-backed. Set `MTG_DATA_DIR=/data` and mount a persistent volume if saved AI sessions need to survive deploys/restarts.
- The Dockerfile's entrypoint resolves `$PORT` at container start so platforms that inject a dynamic port (Render) work without changes.
- **Moxfield URL caveat.** Moxfield's Cloudflare edge blocks requests from datacenter IP ranges with HTTP 403/5xx. When that happens, DeckFlow automatically falls back to Commander Spellbook's public `card-list-from-url` endpoint (which accepts the same Moxfield URL) and loads the deck from there instead. The UI surfaces a warning banner noting that card printings, set codes, collector numbers, author tags/categories, and sideboard/maybeboard entries are not available through the fallback. For full metadata, users should copy the Moxfield deck export text and paste it into the deck input directly — that path continues to work from anywhere.
- **Archidekt excluded-category caveat.** Archidekt lets a deck-builder park cards in categories that are switched off for the deck — scratch lists such as `Upgrade`, `To Buy` or `_Swap In`, which the site marks `includedInDeck: false`. DeckFlow honours that: a card in any switched-off category is imported to the maybeboard and is excluded from mana base, bracket and packet analysis, exactly as Archidekt's own card count excludes it. The built-in `Commander` category still wins over an excluded category, so a commander is never demoted. When pasting Archidekt text rather than submitting a URL, export **with categories included** — Archidekt writes the exclusion as a `{noDeck}` marker inside the `[...]` category list, and an export that omits categories carries no marker for DeckFlow to read.
- **Optional browser-extension path.** The web UI detects Moxfield deck URLs before submit on every deck tool that takes a public-URL import — Moxfield–Archidekt Deck Sync, Deck Analysis, Commander Mana Base Analyzer, Deck Primer, and Cut Lab (a Cut Lab bridge import also auto-enables the "Include sideboard" intake so the deck's sideboard overflow lands in the trimming pool). If the optional DeckFlow Bridge extension is installed and the current DeckFlow origin is allowed, the browser fetches the Moxfield deck directly and submits it through the existing form flow. As of extension v0.1.1 the default allow-list and host scope already include `deckflow.gg` (apex + `www`) and localhost, so the public site works with no manual setup; the extension also no longer injects on unrelated sites. If the extension is not installed, DeckFlow prompts the user with the included install page (`/deckflow-bridge`), which serves a downloadable ZIP from `/extensions/deckflow-bridge.zip`. Browsers do not allow the site to silently install the extension. Mobile browsers are left on the normal server/fallback path and are not prompted for the extension.
  The Moxfield URL fields in the web UI also include a collapsible in-app hint that links to the install page and explains the (now mostly automatic) allowed-origin setup.

### Browser extension install
- Extension folder: `browser-extensions/deckflow-bridge`
- Download/install page: `/deckflow-bridge` serves `/extensions/deckflow-bridge.zip` (the legacy `/extension-install.html` URL 301s here)
- Current install mode: download ZIP, unzip it locally, then load unpacked via `chrome://extensions` or `edge://extensions`
- Security default: the DeckFlow bridge only responds on origins on its allow list — `deckflow.gg` and localhost are pre-allowed (v0.1.1+); any other origin must be added in extension options. The content script is scoped to those hosts, so it no longer injects on every site.
- The extension contains:
  - `deckflow-bridge.js` for the optional DeckFlow web-app bridge
  - `options.html` / `options.js` for managing the allowed DeckFlow origin list
  - `background.js` for cross-origin Moxfield API requests

---
