import { expect, test, type Browser, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { publishFirstUnpublishedEntry, setEntryVisibility } from './support/content-kb-publish';

type PublishedEntry = {
  id: number;
  title: string;
};

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;
let publishedEntry: PublishedEntry | null = null;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

function requirePublishedEntry(): PublishedEntry {
  if (!publishedEntry) {
    throw new Error('The published Content KB entry was not available from the preceding serial test.');
  }

  return publishedEntry;
}

async function openPublishedEntry(page: Page): Promise<void> {
  const entry = requirePublishedEntry();
  const browseResponse = await page.goto('/content-kb');
  expect(browseResponse?.ok()).toBeTruthy();

  const cardLink = page.locator(`[data-kb-entry] .hub-card__title a[href="/content-kb/${entry.id}"]`);
  await expect(cardLink).toHaveCount(1);

  const href = await cardLink.getAttribute('href');
  if (!href) {
    throw new Error(`The published Content KB entry ${entry.id} did not have a detail link.`);
  }

  const detailResponse = await page.goto(href);
  expect(detailResponse?.ok()).toBeTruthy();
}

test('published entry appears on the public browse page', async ({ page }) => {
  publishedEntry = await publishFirstUnpublishedEntry(page);

  const response = await page.goto('/content-kb');
  expect(response?.status()).toBe(200);
  expect(await page.locator('[data-kb-entry]').count()).toBeGreaterThanOrEqual(1);
  await expect(page.locator('body')).toContainText(publishedEntry.title);
});

test('published entry detail renders the artifact body and the ChatGPT copy affordance', async ({ page }) => {
  await openPublishedEntry(page);

  const artifactProse = page.locator('.kb-artifact-prose');
  await expect(artifactProse).toBeVisible();
  expect((await artifactProse.textContent())?.trim()).not.toBe('');

  const copyButton = page.locator('button[data-copy-target="kb-artifact-text"]');
  await expect(copyButton).toBeVisible();
  await expect(copyButton).toContainText(/ChatGPT/i);

  const hint = page.locator('.kb-artifact-hint');
  await expect(hint).toBeVisible();
  await expect(hint).toContainText(/ChatGPT|Claude/i);
});

test('copy affordance stays inside the viewport', async ({ page }) => {
  await openPublishedEntry(page);

  const copyButton = page.locator('button[data-copy-target="kb-artifact-text"]');
  await expect(copyButton).toBeVisible();

  // The longer label must not overflow the viewport or wrap to a giant button
  // on the 390px mobile project. Use viewport-relative rect coords.
  const rect = await copyButton.evaluate((el) => {
    const r = el.getBoundingClientRect();
    return { right: r.right, height: r.height, innerWidth: window.innerWidth };
  });
  expect(rect.right).toBeLessThanOrEqual(rect.innerWidth + 1);
  expect(rect.height).toBeLessThanOrEqual(80);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
});

test('unpublishing removes the entry from browse and 404s its detail route', async ({ page }) => {
  const entry = requirePublishedEntry();
  await setEntryVisibility(page, entry.id, false);

  const browseResponse = await page.goto('/content-kb');
  expect(browseResponse?.status()).toBe(200);
  await expect(page.locator('body')).not.toContainText(entry.title);

  const detailResponse = await page.goto(`/content-kb/${entry.id}`);
  expect(detailResponse?.status()).toBe(404);
});

test.afterAll(async ({ browser }) => {
  if (!publishedEntry) {
    return;
  }

  await restorePublishedEntry(browser, publishedEntry.id);
});

async function restorePublishedEntry(browser: Browser, id: number): Promise<void> {
  const context = await browser.newContext();
  const page = await context.newPage();
  let cleanupLock: LockHandle | null = null;

  try {
    cleanupLock = await acquireAdminLockForTest(page);
    await setEntryVisibility(page, id, false);
  } finally {
    await releaseAdminLockForTest(cleanupLock);
    await context.close();
  }
}
