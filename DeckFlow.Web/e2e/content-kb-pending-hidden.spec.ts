import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { expect, test } from '@playwright/test';

// D-04 / D-15 / Codex HIGH+LOW: a drifted visible-but-pending row must render NOWHERE on the public
// Content KB — neither in the browse list nor at its /content-kb/{id} detail route. This e2e seeds a
// visible-but-pending row (plus an approved+visible control) directly into the content-site-index
// SQLite DB the running server reads, then asserts the pending row is excluded from browse and 404s
// on direct navigation while the approved control renders.
//
// The DB path mirrors DeckFlowDatabaseConnectionFactory.ResolveArtifactsPath for Development
// (no MTG_DATA_DIR): {ContentRoot}/../artifacts/content-site-index.db, where ContentRoot is the
// DeckFlow.Web project dir the webServer command runs from.
const dbPath = resolve(__dirname, '..', '..', 'artifacts', 'content-site-index.db');

const suffix = `${Date.now()}`;
const pendingKey = `e2e-pending-${suffix}`;
const approvedKey = `e2e-approved-${suffix}`;
const pendingTitle = `E2E Pending Hidden ${suffix}`;
const approvedTitle = `E2E Approved Control ${suffix}`;

let pendingId = 0;
let approvedId = 0;
let seeded = false;

function sqlite(sql: string): string {
  // `.timeout` (via -cmd) SILENTLY sets the busy timeout to guard against the running server briefly
  // holding the SQLite file. Do NOT prepend `PRAGMA busy_timeout=...;` to the SQL — it emits its value
  // as a result row, corrupting Number(...) parsing of the id SELECT (turned every run into a skip).
  return execFileSync('sqlite3', ['-cmd', '.timeout 8000', dbPath, sql], {
    encoding: 'utf8',
  }).trim();
}

function seedRow(key: string, title: string, approval: string): number {
  sqlite(
    `INSERT INTO content_site_index
       (source, title, video_url, artifact_path, indexed_utc,
        archetype_tags, bracket_tags, card_category_tags,
        natural_key_type, natural_key_value, is_visible, approval_status)
     VALUES
       ('E2E Source', '${title}', 'https://youtu.be/${key}',
        'content-kb/e2e/${key}.md', '2026-07-06T00:00:00Z',
        '[]', '[]', '[]', 'youtube_channel', '${key}', 1, '${approval}');`,
  );
  return Number(
    sqlite(`SELECT id FROM content_site_index WHERE natural_key_value='${key}';`),
  );
}

test.beforeAll(async () => {
  if (!existsSync(dbPath)) {
    return; // Server not yet initialized against this DB; tests below self-skip.
  }

  try {
    pendingId = seedRow(pendingKey, pendingTitle, 'pending');
    approvedId = seedRow(approvedKey, approvedTitle, 'approved');
    seeded = pendingId > 0 && approvedId > 0;
  } catch {
    seeded = false; // sqlite3 unavailable or table not yet created — self-skip.
  }
});

test.afterAll(async () => {
  if (!existsSync(dbPath)) {
    return;
  }

  try {
    sqlite(
      `DELETE FROM content_site_index WHERE natural_key_value IN ('${pendingKey}','${approvedKey}');`,
    );
  } catch {
    // best-effort cleanup
  }
});

test('pending row is hidden from browse and 404s on detail; approved control renders', async ({
  page,
}) => {
  test.skip(!seeded, 'Content DB could not be seeded (KB DB absent or sqlite3 unavailable).');

  const browse = await page.goto('/content-kb');
  // Flag off → route 404s; nothing to assert about serve filtering, so skip rather than false-fail.
  test.skip(browse?.status() === 404, 'Knowledge Base flag disabled; serve-filter e2e not applicable.');
  expect(browse?.status()).toBe(200);

  // Browse list must include the approved control and exclude the pending row.
  await expect(page.locator('body')).toContainText(approvedTitle);
  await expect(page.locator('body')).not.toContainText(pendingTitle);

  // Direct navigation to the pending row's detail route must 404.
  const pendingDetail = await page.goto(`/content-kb/${pendingId}`);
  expect(pendingDetail?.status()).toBe(404);

  // The approved control's detail route is reachable (not a 404) — proves the filter is not over-broad.
  const approvedDetail = await page.goto(`/content-kb/${approvedId}`);
  expect(approvedDetail?.status()).not.toBe(404);
});
