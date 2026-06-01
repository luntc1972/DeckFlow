# Phase 22: Content KB Site Integration - Context

**Gathered:** 2026-06-01
**Status:** Ready for planning

<domain>
## Phase Boundary

Surface the locally-distilled Content KB on the live site: materialize the slim index on Render Postgres, provide a **public** browse/filter surface + a **minimal admin** management view, serve each entry's prompt artifact for the ChatGPT-paste workflow, all gated behind `content_kb_enabled`. Artifacts + index reach Render via **commit-then-deploy** (no upload endpoint). KB-08 + KB-09.

**In scope:** Render Postgres index materialization (`EnsureSchemaAsync` + committed-seed load), public browse/filter page (behind flag), per-entry artifact detail page + copy-for-ChatGPT, a small admin management view (index status + flag toggle + reload-from-seed), CSRF on any mutating admin POST, 375px-responsive + zero theme bleed.
**Out of scope:** the local harvest/distill pipeline (Phases 19-21, done); codex backend (backlog); admin upload-to-/data path (rejected — see D-02); server-query pagination (deferred); Phase 23 NoWarn strip.
</domain>

<decisions>
## Implementation Decisions

### Audience + surfaces (D-01) — BOTH public browse + minimal admin manage
- **D-01a (public browse):** A **public** site page (e.g. route `/content-kb`) using the responsive **site** shell (`site-common.css` + v1.3 WDG-08 primitives — NOT `.admin-shell`). Gated by `content_kb_enabled` via `FeatureFlagGateAttribute` (default OFF → the route is hidden/404 when off). This is the primary deliverable (serves the user-paste core value).
- **D-01b (admin manage — keep MINIMAL):** A small surface under `.admin-shell` (BasicAuth): shows KB index status (row count, distinct sources, last-loaded timestamp) + the `content_kb_enabled` toggle (reuse the existing `AdminFlagsController` / flags surface where possible) + a "reload index from committed seed" action. Do NOT build a full CRUD admin — management is status + flag + reload only.

### Artifact serving + index→Render sync (D-02) — commit-then-deploy
- **D-02a (artifacts in repo):** Distilled artifacts are **committed to the repo** (like `prompt-templates/`) and served as content. The current `content-kb/` (and `artifacts/`) are gitignored — planner decides the **published** location (e.g. a tracked `content-kb/` publish dir or `wwwroot/`-served path) and whether served as static files or via a controller that reads the file. Only the published artifact files ship — NO transcripts/audio/spend.
- **D-02b (index seed):** Slim-index rows ship as a **committed seed/import file** (the local CLI exports `content_site_index` rows to a tracked JSON/SQL file; planner picks format + may need a new `content-index-export` CLI verb). On Render startup: `EnsureSchemaAsync` then **idempotent load** (upsert by natural key) of the seed if the table is empty / out of date. NO direct local→Render Postgres write, NO upload endpoint.
- **D-02c:** Because there is NO upload endpoint, the only mutating site POST is the admin "reload index from seed" action → it still carries `[ValidateAntiForgeryToken]` + `SameOriginRequestValidator` (SC4/P11 applies to any admin mutating POST, not just uploads).

### Entry → artifact presentation (D-03) — detail page + copy
- **D-03:** Per-entry **detail page** (shareable URL, e.g. `/content-kb/{naturalKey}` or `{id}`) rendering the artifact's summary + timestamped clips + tags (render markdown via the existing **Markdig** path used by `HelpContentService`), with a **"copy for ChatGPT"** button reusing the existing packet/comparison copy-to-clipboard TS/UX. Must render correctly at 375px.

### Browse/filter (D-04) — client-side faceted
- **D-04:** Server renders the full list from `content_site_index` (small index — tens-to-hundreds of rows); **client-side** faceted filter by source / archetype / bracket / card_category (chips or dropdowns) + a text search; **empty-state CTA** for zero-content first run (SC2). NO pagination yet (deferred until the index is large).

### Locked by Success Criteria (do not re-litigate)
- `content_kb_enabled` `IFeatureFlagStore` flag, **default OFF**, flipped only after first UAT verifies browse + artifact rendering (SC5).
- Any mutating site POST: `[ValidateAntiForgeryToken]` AND `SameOriginRequestValidator.IsValid(Request)`; CI grep gate returns empty for unguarded actions (SC4/P11).
- All new views render at 375px mobile; zero CSS bleed into the 22 guild themes (SC5).
- Slim index has NO transcript/audio/spend data (SC1).

### Claude's Discretion
- Exact public route name (`/content-kb` vs `/knowledge` vs `/decks/insights`).
- Seed file format (JSON vs SQL) + whether a new CLI export verb or reuse of an existing one.
- Published artifact location + static-vs-controller serving.
- Whether the admin manage view is a new tab or folded into the existing flags/maintenance admin page.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Index + artifact contracts (Phase 19, built)
- `DeckFlow.Core/Content/ContentSiteIndexStore.cs` + `IContentSiteIndexStore.cs` — `content_site_index` schema, `EnsureSchemaAsync`, query + upsert (natural-key) methods to reuse for materialize + seed-load.
- `DeckFlow.Core/Knowledge/ContentArtifactSpec.cs` — artifact file format (frontmatter: source/title/url/video_id/tags{archetype,bracket,card_category}/generated_utc + ## Summary / ## Key Clips / ## Tags).
- `.planning/phases/19-content-kb-foundation-local-schema-contracts/19-CONTEXT.md` — slim-index schema contract + artifact spec decisions.

### Feature flag + security (locked patterns)
- `DeckFlow.Web/Infrastructure/FeatureFlagGateAttribute.cs` — the gate attribute to put on the public controller/action.
- `DeckFlow.Web/Extensions/FeatureFlagsServiceCollectionExtensions.cs` + `Controllers/Admin/AdminFlagsController.cs` — flag registration + admin toggle to reuse for `content_kb_enabled`.
- `DeckFlow.Web/Security/SameOriginRequestValidator.cs` + existing `[ValidateAntiForgeryToken]` usage — CSRF on admin mutating POST.

### Reusable UI / rendering
- `DeckFlow.Web/Services/HelpContentService.cs` — Markdig markdown render pattern for the artifact detail page.
- Existing copy-to-clipboard TS in `DeckFlow.Web/wwwroot/ts/*` (packet/comparison pages) — reuse for "copy for ChatGPT".
- `DeckFlow.Web/wwwroot/css/site-common.css` (public responsive primitives) + `admin-common.css` (admin shell).

### Persistence
- `DeckFlow.Web/Services/DeckFlowDatabaseConnectionFactory.cs` — Postgres provider selection (Render). The site reads `content_site_index` from Postgres in prod, SQLite locally.

### Roadmap / requirements
- `.planning/ROADMAP.md` §"Phase 22" (goal + 5 SCs) · `.planning/REQUIREMENTS.md` KB-08, KB-09.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ContentSiteIndexStore` (EnsureSchemaAsync + query + natural-key upsert) — materialize on Render + idempotent seed load.
- `FeatureFlagGateAttribute` + `content_kb_enabled` flag + `AdminFlagsController` — gate the public surface + admin toggle.
- Markdig (HelpContentService) — render artifact markdown.
- Copy-to-clipboard TS (packet/comparison) — copy-for-ChatGPT button.
- `SameOriginRequestValidator` + anti-forgery — CSRF on the admin reload POST.

### Established Patterns
- Public tools live under `Views/Deck/` + `DeckController`; admin under `Views/Admin/` + `Controllers/Admin/` behind BasicAuth — decide controller placement (likely a new `ContentKbController` public + an admin action).
- Dialect-pluggable persistence (SQLite local / Postgres Render) via `RelationalDatabaseConnection` — the index store already works both ways.
- Layout CSS belongs in `site-common.css` (public) / `admin-common.css` (admin), never `site.css` (CLAUDE.md constraint); token additions in each theme `:root`.

### Integration Points
- Render startup (Program.cs) — add EnsureSchemaAsync + seed-load for the content index (guard: only when provider=Postgres / content_kb present).
- New public route + nav entry (gated by flag).
</code_context>

<specifics>
## Specific Ideas
- "copy for ChatGPT" must yield clean pasteable text (summary + clips + tags), not the raw frontmatter — mirror the existing one-round-trip paste UX (project core value).
- Default-OFF flag means the public route + nav must be invisible/404 until the operator flips it post-UAT.
- The 10 artifacts already produced (Phase 21.2 UAT) are realistic seed data for building + UAT.
</specifics>

<deferred>
## Deferred Ideas
- Admin upload-to-/data artifact path (rejected D-02 in favor of commit-then-deploy — revisit only if repo-artifact growth becomes a problem).
- Server-query filter + pagination (D-04 — when the index outgrows client-side filtering).
- Full admin CRUD over the index/sources (out of scope — manage = status + flag + reload only).
- Deck-analysis integration of Content KB tags — v1.5 per scope decision.

None blocking — discussion stayed within phase scope.
</deferred>

---

*Phase: 22-Content KB Site Integration*
*Context gathered: 2026-06-01*
