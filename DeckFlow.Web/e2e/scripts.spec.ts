import { expect, test } from '@playwright/test';

const layoutScripts = [
  'site.js',
  'df-select.js',
  'df-typeahead.js',
];

const routeScripts: Array<{ route: string; scripts: string[] }> = [
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
  // Admin pages use _AdminLayout, which does NOT load the public layout trio
  // (site.js/df-select.js/df-typeahead.js) — only its own section scripts.
  { route: '/Admin/ContentKb?visibilityFilter=all', scripts: ['admin-modal.js', 'kb-entry-filter.js', 'content-kb-admin.js'] },
];

// Re-navigate on a transient non-2xx. Under fullyParallel CI both viewport
// projects hit the same admin pages at once; the shared SQLite store can throw a
// momentary "database is locked" (HTTP 500) on a single request while siblings
// succeed. A real outage still fails after the retries; only a one-off blip recovers.
async function gotoOk(page: import('@playwright/test').Page, route: string) {
  let response = await page.goto(route);
  for (let attempt = 0; attempt < 2 && !response?.ok(); attempt++) {
    response = await page.goto(route);
  }
  return response;
}

for (const { route, scripts } of routeScripts) {
  test(`script tags present for ${route}`, async ({ page }) => {
    const response = await gotoOk(page, route);

    expect(response?.ok()).toBeTruthy();

    for (const scriptName of scripts) {
      expect(await page.locator(`script[src*="${scriptName}"]`).count()).toBeGreaterThan(0);
    }
  });
}
