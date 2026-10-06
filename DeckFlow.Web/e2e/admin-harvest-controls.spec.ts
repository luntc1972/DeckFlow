import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

// This spec never submits run or schedule forms because a harvest would reach archidekt.com; @resume needs the gate's isolated, seeded data dir.
type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;
const rateLimitedMode = process.env.DECKFLOW_E2E_RATE_LIMITED;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

async function expectRateLimitedBanner(page: Page): Promise<void> {
  const banner = page.locator('#harvest-rate-limited-banner');
  if (rateLimitedMode === '1') {
    await expect(banner).toBeVisible();
    await expect(banner).toHaveClass(/admin-banner--warning/);
    await expect(banner).toHaveAttribute('role', 'alert');
    await expect(banner).toContainText('UTC');
    await expect(banner.locator('form[action$="/resume-schedules" i] button[type="submit"]')).toBeVisible();
  } else if (rateLimitedMode === '0') {
    await expect(banner).toHaveCount(0);
  }
}

test('admin harvest controls render update, schedule and rate cards', async ({ page }) => {
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  await expect(page.locator('#runKind option')).toHaveValues(['bulk', 'update']);
  await expect(page.locator('#harvest-bulk-schedule')).toBeVisible();
  await expect(page.locator('#harvest-update-schedule')).toBeVisible();
  await expect(page.locator('#harvest-update-schedule #updateIntervalMinutes option')).toHaveValues(['', '15', '30', '60', '120']);
  await expect(page.locator('#ratePerMinute option')).toHaveValues(['5', '10', '20']);
  await expect(page.locator('#ratePerMinute')).toHaveValue(/^(5|10|20)$/);
  await expectRateLimitedBanner(page);

  expect(await page.evaluate(() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1)).toBeTruthy();
  const banner = page.locator('#harvest-rate-limited-banner');
  if (await banner.count()) {
    const box = await banner.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.x + box!.width).toBeLessThanOrEqual(await page.evaluate(() => window.innerWidth) + 1);
  }
});

test('admin harvest rate card saves an allow-listed rate', async ({ page }) => {
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  const rate = page.locator('#ratePerMinute');
  const original = await rate.inputValue();
  const selected = ['10', '5', '20'].find((candidate) => candidate !== original);
  expect(selected).toBeDefined();

  const saveRate = async (value: string): Promise<void> => {
    await rate.selectOption(value);
    const [postResponse] = await Promise.all([
      page.waitForResponse((candidate) => candidate.request().method() === 'POST' && new URL(candidate.url()).pathname.toLowerCase() === '/admin/harvest/rate'),
      page.locator('#harvest-rate-card button[type="submit"]').click(),
    ]);
    expect(postResponse.status()).toBeLessThan(400);
  };

  try {
    await saveRate(selected!);
    await expect(page.locator('.admin-banner:not(#harvest-rate-limited-banner)')).toHaveText(`Archidekt rate set to ${selected} requests per minute.`);
    await expect(rate).toHaveValue(selected!);
    if (rateLimitedMode === '1') {
      await expect(page.locator('#harvest-rate-limited-banner')).toBeVisible();
    }
  } finally {
    await saveRate(original);
  }
});

test('@resume admin harvest resume schedules clears the rate-limited banner', async ({ page }) => {
  test.skip(rateLimitedMode !== '1', 'The 06-12 gate seeds the isolated data directory with a rate-limited marker.');
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  await expect(page.locator('#harvest-rate-limited-banner')).toBeVisible();
  const [postResponse] = await Promise.all([
    page.waitForResponse((candidate) => candidate.request().method() === 'POST' && new URL(candidate.url()).pathname.toLowerCase() === '/admin/harvest/resume-schedules'),
    page.locator('#harvest-rate-limited-banner button[type="submit"]').click(),
  ]);
  expect(postResponse.status()).toBeLessThan(400);
  await expect(page.locator('.admin-banner:not(#harvest-rate-limited-banner)')).toHaveText('Rate-limit pause cleared. Both schedules resumed.');
  await expect(page.locator('#harvest-rate-limited-banner')).toHaveCount(0);
});
