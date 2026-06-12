import { expect, test } from '@playwright/test';

const routes = [
  '/',
  '/sync',
  '/card-lookup',
  '/mechanic-lookup',
  '/convert',
  '/suggest-categories',
  '/judge-questions',
  '/deck-analysis',
  '/deck-comparison',
  '/cedh-meta-gap',
  '/deck-primer',
  '/commander-categories',
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
