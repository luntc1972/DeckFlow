import { expect, test } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

const layoutScripts = [
  'site.js',
  'df-select.js',
  'df-typeahead.js',
];

const publicRouteScripts: Array<{ route: string; scripts: string[] }> = [
  { route: '/', scripts: [...layoutScripts] },
  { route: '/sync', scripts: [...layoutScripts, 'deck-sync.js'] },
  { route: '/card-lookup', scripts: [...layoutScripts, 'card-lookup.js', 'deck-sync.js'] },
  { route: '/mechanic-lookup', scripts: [...layoutScripts, 'deck-sync.js'] },
  { route: '/convert', scripts: [...layoutScripts, 'deck-sync.js'] },
  { route: '/suggest-categories', scripts: [...layoutScripts, 'card-search.js', 'category-suggestions.js', 'deck-sync.js'] },
  { route: '/judge-questions', scripts: [...layoutScripts, 'deck-sync.js', 'judge-questions.js'] },
  { route: '/deck-analysis', scripts: [...layoutScripts, 'card-lookup.js', 'deck-sync.js'] },
  { route: '/deck-comparison', scripts: [...layoutScripts, 'deck-sync.js'] },
  { route: '/cedh-meta-gap', scripts: [...layoutScripts, 'deck-sync.js'] },
  { route: '/manabase', scripts: [...layoutScripts, 'deck-sync.js'] },
  { route: '/deck-primer', scripts: [...layoutScripts, 'primer-selection.js', 'deck-sync.js'] },
  { route: '/commander-categories', scripts: [...layoutScripts] },
  { route: '/help', scripts: [...layoutScripts] },
  { route: '/about', scripts: [...layoutScripts] },
  { route: '/feedback', scripts: [...layoutScripts] },
];

const adminRouteScripts: Array<{ route: string; scripts: string[] }> = [
  // Admin pages use _AdminLayout, which does NOT load the public layout trio
  // (site.js/df-select.js/df-typeahead.js) — only its own section scripts.
  { route: '/Admin/ContentKb?visibilityFilter=all', scripts: ['admin-modal.js', 'kb-entry-filter.js', 'content-kb-admin.js'] },
];

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

test.describe.configure({ mode: 'serial' });

async function gotoOk(page: import('@playwright/test').Page, route: string) {
  let response = await page.goto(route);
  for (let attempt = 0; attempt < 2 && !response?.ok(); attempt++) {
    response = await page.goto(route);
  }
  return response;
}

for (const { route, scripts } of publicRouteScripts) {
  test(`script tags present for ${route}`, async ({ page }) => {
    const response = await gotoOk(page, route);

    expect(response?.ok()).toBeTruthy();

    for (const scriptName of scripts) {
      expect(await page.locator(`script[src*="${scriptName}"]`).count()).toBeGreaterThan(0);
    }
  });
}

test.describe('admin script coverage @admin', () => {
  let heldLock: LockHandle | null = null;

  test.beforeEach(async ({ page }) => {
    // Keep every /Admin/ e2e request globally serialized across all Playwright
    // workers and both viewport projects. These pages share a SQLite-backed
    // admin store that returns transient 429/500s when two tests arrive at once.
    heldLock = await acquireAdminLockForTest(page);
  });

  test.afterEach(async () => {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  });

  for (const { route, scripts } of adminRouteScripts) {
    test(`script tags present for ${route}`, async ({ page }) => {
      const response = await gotoOk(page, route);

      expect(response?.ok()).toBeTruthy();

      for (const scriptName of scripts) {
        expect(await page.locator(`script[src*="${scriptName}"]`).count()).toBeGreaterThan(0);
      }
    });
  }
});
