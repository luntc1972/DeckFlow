import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

const commandersGrid = (page: Page) => page.locator('#commanders-grid-container');

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

test('admin harvest lazily loads commanders once after tab activation', async ({ page }) => {
  let commandersRequestCount = 0;
  page.on('request', (request) => {
    if (request.url().includes('/Admin/Harvest/commanders')) {
      commandersRequestCount++;
    }
  });

  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  await expect(page.locator('#harvest-panel-commanders')).toBeHidden();
  await expect(commandersGrid(page).locator('table')).toHaveCount(0);
  expect(commandersRequestCount).toBe(0);

  await page.locator('#harvest-tab-commanders').click();
  await expect(commandersGrid(page)).toHaveAttribute('aria-busy', 'false');
  expect(commandersRequestCount).toBe(1);

  await page.locator('#harvest-tab-overview').click();
  await page.locator('#harvest-tab-commanders').click();
  expect(commandersRequestCount).toBe(1);
  await expect(page.locator('#harvest-tab-commanders')).toHaveAttribute('aria-selected', 'true');
  await expect(page.locator('#harvest-panel-overview')).toBeHidden();
});

test('admin harvest health strip fits its viewport', async ({ page }) => {
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  const strip = page.locator('div.admin-harvest__health');
  await expect(strip).toBeVisible();
  for (const id of ['health-processed-decks', 'health-queued-decks', 'health-distinct-commanders', 'health-database-size']) {
    await expect(page.locator(`#${id}`)).toBeVisible();
  }
  expect(await strip.evaluate((element) => element.scrollWidth <= element.clientWidth)).toBeTruthy();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth)).toBeTruthy();
  const stripBox = await strip.boundingBox();
  expect(stripBox).not.toBeNull();
  for (const tile of await strip.locator('.admin-harvest__health-tile').all()) {
    const tileBox = await tile.boundingBox();
    expect(tileBox).not.toBeNull();
    expect(tileBox!.x + tileBox!.width).toBeLessThanOrEqual(stripBox!.x + stripBox!.width + 1);
  }
});

test('admin harvest shows zero-discovery status on overview load', async ({ page }) => {
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  await expect(page.locator('#harvest-panel-overview')).toBeVisible();
  await expect(page.locator('#harvest-zero-discovery')).toBeVisible();
});

test('admin harvest keeps single URL import collapsed until requested', async ({ page }) => {
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  const importPanel = page.locator('#harvest-import-panel');
  await expect(importPanel).toBeVisible();
  await expect(importPanel).not.toHaveAttribute('open', '');
  await expect(page.locator('#url')).toBeHidden();

  await importPanel.locator('summary').click();
  await expect(page.locator('#url')).toBeVisible();
});

test('admin harvest run log identifies its empty state', async ({ page }) => {
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  await expect(page.locator('#harvest-run-log-heading')).toBeVisible();
  await expect(page.locator('#harvest-run-log, p.admin-harvest__runs-empty')).toHaveCount(1);
});

test('admin harvest single URL form posts with antiforgery protection', async ({ page }) => {
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  await page.locator('#harvest-import-panel summary').click();
  await page.locator('#url').fill('https://example.com/');

  const [postResponse] = await Promise.all([
    page.waitForResponse((candidate) => candidate.request().method() === 'POST' && candidate.url().includes('/admin/harvest/url')),
    page.locator('#harvest-import-panel button[type="submit"]').click(),
  ]);

  expect(postResponse.status()).not.toBe(400);
  await expect(page.locator('.admin-banner')).toHaveText('URL must be an Archidekt deck URL.');
});
