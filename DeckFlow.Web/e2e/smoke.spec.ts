import { expect, test } from '@playwright/test';

// Ungated (or default-on) routes only. /bracket, /cut-lab and /deck-history seed FALSE in
// FeatureFlagStore, so an unconditional GET here would 404; each has its own spec that toggles
// its flag through the admin console instead.
const routes = [
  '/',
  '/sync',
  '/card-lookup',
  '/mechanic-lookup',
  '/convert',
  '/suggest-categories',
  '/judge-questions',
  '/deck-analysis',
  '/set-upgrade-analysis',
  '/deck-comparison',
  '/cedh-meta-gap',
  '/manabase',
  '/deck-primer',
  '/commander-categories',
  '/deckflow-bridge',
  '/help',
  '/about',
  '/feedback',
];

for (const route of routes) {
  test(`smoke loads ${route}`, async ({ page }) => {
    const consoleErrors: string[] = [];
    const pageErrors: string[] = [];

    page.on('console', (message) => {
      if (message.type() === 'error') {
        consoleErrors.push(message.text());
      }
    });
    page.on('pageerror', (error) => {
      pageErrors.push(error.message);
    });

    const response = await page.goto(route);

    expect(response?.ok()).toBeTruthy();
    expect(consoleErrors).toEqual([]);
    expect(pageErrors).toEqual([]);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
  });
}
