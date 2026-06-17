---
phase: 48
slug: ui-audit-remediation
status: verified
threats_total: 6
threats_open: 0
threats_closed: 6
asvs_level: 1
created: 2026-06-17
---

# Phase 48 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.
>
> **Mode:** retroactive-STRIDE — no plan-time threat_model block exists for Phase 48.
> The threat register below was derived from STRIDE analysis of the implementation
> surface (CSS static assets, Razor partial, inline SVG markup) after execution.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Browser ← Razor render | Server renders _ShortFormFooter.cshtml; output is the only new HTML path | Static string model literals only; no user-controlled data |
| Browser ← Static assets | CSS files served as static files from wwwroot | Pure CSS; no user data; no server-side processing |
| Browser ← Inline SVG | SVG markup embedded in Home.cshtml at render time | Hardcoded paths only; no user data; no scripting |

---

## Threat Register

| Threat ID | Category | Component | Disposition | Mitigation | Status |
|-----------|----------|-----------|-------------|------------|--------|
| T-48-01 | Tampering / XSS | `_ShortFormFooter.cshtml` — `@hint` render | mitigate | Razor auto-encodes `@hint`; no `Html.Raw` present | closed |
| T-48-02 | Tampering / XSS | `_ShortFormFooter` call sites (CardLookup, JudgeQuestions) | mitigate | Both callers pass static string literals, not user-controlled data | closed |
| T-48-03 | Tampering / XSS | Inline SVG icons in `Home.cshtml` | mitigate | All 17 SVGs are hand-authored static markup; no `<script>`, no event handlers, no external refs; `aria-hidden="true" focusable="false"` present on all 17 | closed |
| T-48-04 | Information Disclosure / CSP bypass | CSS `url()` off-origin references in any of the 29 changed CSS files | mitigate | Zero `url(http…)` matches across all CSS files; `@import` resolves only to local `url('site.css')`; no `expression()` found | closed |
| T-48-05 | Information Disclosure / CSP bypass | Remote `@import` in theme CSS files | mitigate | All `@import` statements resolve to local `site.css` only; confirmed by grep across all 29 changed files | closed |
| T-48-06 | Elevation of Privilege / CSP weakening | New inline SVG may require loosening CSP | accept | Existing CSP: `default-src 'self'`; inline SVG embedded in Razor-rendered HTML is same-origin DOM content, not a separate resource fetch. No CSP directive change was required or made. `script-src 'self' <sha256>` unchanged. See Accepted Risks Log. | closed |

*Status: open · closed*
*Disposition: mitigate (implementation required) · accept (documented risk)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-48-01 | T-48-06 | Inline SVG in Razor views is same-origin HTML markup — the browser treats it as DOM content, not a fetched resource. No `script-src` or `img-src` change is needed. The existing `script-src 'self' <sha256>` and `style-src 'self' 'unsafe-inline'` are unchanged by Phase 48. The CSP SHA hash covers only the JSON-LD block in `_Layout.cshtml` (unrelated); Phase 48 adds no new inline scripts. Risk: none introduced. | operator (luntc1972) | 2026-06-17 |

---

## Verification Detail

### T-48-01: `@hint` auto-encoding in `_ShortFormFooter.cshtml`

- File: `DeckFlow.Web/Views/Shared/_ShortFormFooter.cshtml:17`
- Code: `<p class="short-form-footer__hint">@hint</p>`
- `Html.Raw` grep across all four changed view files: **0 matches**
- Razor `@expression` syntax HTML-encodes by default in ASP.NET Core MVC. No opt-out present.
- **CLOSED**

### T-48-02: Call-site data source for `_ShortFormFooter`

- `CardLookup.cshtml:118` — passes a hardcoded string literal `"Try pasting 'Sol Ring'…"` — no model binding, no query string, no user input.
- `JudgeQuestions.cshtml:117` — passes a hardcoded string literal `"For a rules question…"` — no model binding, no query string, no user input.
- Even if auto-encoding failed (it does not), there is no user-controlled path to inject data into these arguments.
- **CLOSED**

### T-48-03: Inline SVG safety in `Home.cshtml`

- SVG count: 17 (confirmed by `grep -c '<svg'` = 17)
- `aria-hidden="true"` count: 17 (matches SVG count exactly — all SVGs decorated)
- `currentColor` count: 17 (all colour references are inherited from CSS, no hard-coded hex or external colour source)
- Event-handler grep (`on[A-Za-z]+=`): 0 matches in SVG context; only `aria-pressed` and `data-*` attributes present in the file
- `<script>` in Home.cshtml: 0 matches
- `javascript:` or `xlink:href` or `http://` in Home.cshtml: 0 matches
- **CLOSED**

### T-48-04 + T-48-05: CSS off-origin references

- `url(http…)` grep across all wwwroot/css/: **0 matches**
- `expression(` grep across all wwwroot/css/: **0 matches**
- `@import` statements found: `@import url('site.css')` only — local relative reference in 11 guild theme files; no remote host
- Changed files confirmed by `git diff --name-only c5616bd..HEAD`: exactly 29 CSS/view files, matching the declared scope in SUMMARYs
- **CLOSED**

### T-48-06: CSP impact of inline SVG

- `SecurityHeadersApplicationBuilderExtensions.cs:11-20` — CSP is unchanged by Phase 48
- Inline SVG in a Razor view is emitted as part of the HTML document body. The browser parses it as DOM nodes (`<svg>`, `<circle>`, `<path>`, etc.), not as a fetched image resource. The `img-src` directive does not govern inline SVG; `default-src 'self'` applies to resource fetches only.
- No `script-src` change needed: SVG elements contain no scripts.
- **CLOSED** (accepted — no implementation gap)

---

## Unregistered Threat Flags

Both 48-01-SUMMARY.md and 48-02-SUMMARY.md declare `## Threat Flags: None`.
No threat flags were raised by the executor during implementation.

One code-review warning (48-REVIEW.md WR-01: `--fs-xs` collapses to equal `--fs-sm`) is a maintainability/UX finding with no security relevance. It is documented in 48-REVIEW.md and is not an unregistered threat flag.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-06-17 | 6 | 6 | 0 | Claude (gsd-security-auditor, sonnet-4-6, retroactive-STRIDE) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-06-17
