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
