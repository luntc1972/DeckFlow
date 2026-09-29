import { expect, test, type Route } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
});

test.afterEach(async () => {
  if (heldLock) {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});

test('analytics range chips sit in the page header actions', async ({ page }) => {
  await page.goto('/Admin/Analytics');
  await expect(page.locator('h1')).toHaveCount(1);
  await expect(page.locator('h1')).toHaveText('Page-usage analytics');
  await expect(page.locator('.admin-page-header__lede')).toHaveText('Page-view and traffic analytics over time.');
  const chips = page.locator('.admin-page-header__actions nav[aria-label="Time range"] a.admin-filter-chips__chip');
  await expect(chips).toHaveCount(4);
  await expect(page.locator('.admin-page-header__actions nav[aria-label="Time range"] a.admin-filter-chips__chip[aria-current="true"]')).toHaveCount(1);
  await expect(page.locator('[data-admin-analytics]')).toHaveCount(1);
  await chips.getByText('Last 30 days').click();
  await expect(page).toHaveURL(/range=30d/);
  await expect(chips.getByText('Last 30 days')).toHaveAttribute('aria-current', 'true');
  expect((await chips.getByText('Last 30 days').boundingBox())!.height).toBeGreaterThanOrEqual(44);
  await page.setViewportSize({ width: 375, height: 800 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
});

test('feedback list and seeded detail use the shared components and delete confirms', async ({ page }) => {
  const marker = `p04-08 e2e feedback ${crypto.randomUUID().replaceAll('-', '').slice(0, 16)}`;
  let feedbackId: string | undefined;
  const feedbackPath = (url: URL) => url.pathname.toLowerCase() === '/feedback';
  await page.route(feedbackPath, async (route) => {
    if (route.request().method() !== 'POST') {
      await route.continue();
      return;
    }
    await route.continue({ headers: { ...route.request().headers(), 'cf-connecting-ip': '2001:db8:1:2:3:4' } });
  });
  try {
    await page.goto('/Feedback');
    await page.getByLabel('Message').fill(marker);
    await page.getByRole('button', { name: 'Send Feedback' }).click();
    await expect(page.locator('.feedback-banner--success[role=status]')).toBeVisible();
    await page.unroute(feedbackPath);
    await page.goto('/Admin/Feedback');
    const row = page.locator('tr', { hasText: marker });
    await expect(row).toHaveCount(1);
    const view = row.getByRole('link', { name: 'View' });
    feedbackId = new URL(await view.getAttribute('href')!, page.url()).pathname.match(/\/(\d+)$/)?.[1];
    expect(feedbackId).toBeTruthy();
    const archiveForm = row.locator('td[data-label="Actions"] form');
    await expect(archiveForm).toHaveClass(/admin-action-form/);
    const archiveDisplay = await archiveForm.evaluate((element) => getComputedStyle(element).display);
    expect(page.viewportSize()!.width > 768 ? archiveDisplay : ['flex', 'inline-flex']).toContain(page.viewportSize()!.width > 768 ? 'inline' : archiveDisplay);
    await expect(page.locator('h1')).toHaveText('Feedback');
    await expect(page.locator('.admin-page-header__lede')).toHaveText('Review and triage user-submitted feedback and bug reports.');
    await expect(page.locator('nav[aria-label="Status filter"] a.admin-filter-chips__chip')).toHaveCount(4);
    await view.click();
    await expect(page.locator('h1')).toHaveText(`Feedback #${feedbackId}`);
    await expect(page.locator('pre.admin-artifact')).toContainText(marker);
    await expect(page.locator('div.admin-card__actions form.admin-action-form')).toHaveCount(3);
    await expect(page.locator('div.admin-card__actions form.admin-action-form input[name="__RequestVerificationToken"]')).toHaveCount(3);
    const deleteForm = page.locator(`form[data-admin-confirm-delete][data-admin-feedback-id="${feedbackId}"]`);
    await expect(deleteForm.locator('button.admin-button--danger')).toBeVisible();
    let deletes = 0;
    await page.route((url) => url.pathname.toLowerCase() === `/admin/feedback/${feedbackId}/delete`, async (route) => {
      deletes++;
      await route.continue();
    });
    await deleteForm.getByRole('button', { name: 'Delete' }).click();
    await page.locator('[data-admin-modal-cancel]').click();
    expect(deletes).toBe(0);
    await deleteForm.getByRole('button', { name: 'Delete' }).click();
    await page.locator('[data-admin-modal-confirm]').click();
    await expect(page).toHaveURL(/\/admin\/feedback$/i);
    expect(deletes).toBe(1);
    await expect(page.locator('tr', { hasText: marker })).toHaveCount(0);
    feedbackId = undefined;
  } finally {
    if (feedbackId) {
      await page.goto(`/Admin/Feedback/${feedbackId}`);
      const form = page.locator('form[data-admin-confirm-delete]');
      if (await form.count()) {
        await Promise.all([page.waitForURL(/\/admin\/feedback$/i), form.evaluate((element: HTMLFormElement) => element.submit())]);
      }
    }
  }
});

test('dashboard and youtube export use shared cards', async ({ page }) => {
  await page.goto('/Admin');
  await expect(page.locator('h1')).toHaveText('Dashboard');
  await expect(page.locator('.admin-page-header__lede')).toHaveText('Quick access to admin functions.');
  await expect(page.locator('a.admin-card.admin-card--link')).toHaveCount(9);
  await expect(page.locator('.admin-hub-personal-tools a.admin-card.admin-card--link')).toHaveCount(2);
  await page.goto('/Admin/YoutubeExport');
  await expect(page.locator('h1')).toHaveText('YouTube Export');
  await expect(page.locator('.admin-page-header__lede')).toHaveText("Download a channel's video list with titles, views, and upload dates.");
  await expect(page.locator('form[data-yt-export-form]').getByLabel('Channel')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Download list' })).toHaveClass(/admin-button--primary/);
  await page.setViewportSize({ width: 375, height: 800 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
});

test('youtube export pending state recovers through the download cookie', async ({ page, context }) => {
  test.setTimeout(90_000);
  const bounded = async <T>(promise: Promise<T>, ms: number, message: string): Promise<T> => {
    let timer: ReturnType<typeof setTimeout> | undefined;
    try {
      return await Promise.race([promise, new Promise<T>((_, reject) => { timer = setTimeout(() => reject(new Error(message)), ms); })]);
    } finally { if (timer) clearTimeout(timer); }
  };
  let releaseExport!: () => void;
  const release = new Promise<void>(resolve => { releaseExport = resolve; });
  let postSeen!: () => void;
  const posted = new Promise<void>(resolve => { postSeen = resolve; });
  let posts = 0;
  let body = '';
  let cleaningUp = false;
  const predicate = (url: URL) => url.pathname.toLowerCase() === '/admin/youtubeexport/export';
  const handler = async (route: Route) => {
    if (route.request().method() !== 'POST') { await route.continue(); return; }
    posts++;
    body = route.request().postData() ?? '';
    postSeen();
    await release;
    try { await route.fulfill({ status: 200, contentType: 'text/plain; charset=utf-8', headers: { 'Content-Disposition': 'attachment; filename="p04-08-export.txt"' }, body: 'export' }); }
    catch (error) { if (!cleaningUp) throw error; }
  };
  await bounded(page.route(predicate, handler), 5_000, 'route setup timed out');
  await bounded(page.goto('/Admin/YoutubeExport'), 10_000, 'youtube export page did not load');
  await bounded(expect(page.locator('form[data-yt-export-form]')).toHaveAttribute('action', /\/admin\/youtubeexport\/export$/i), 5_000, 'export action mismatch');
  await bounded(page.locator('form[data-yt-export-form]').getByLabel('Channel').fill('@examplechannel'), 5_000, 'channel fill timed out');
  await bounded(page.evaluate(() => {
    document.addEventListener('submit', () => {
      const button = document.querySelector<HTMLButtonElement>('form[data-yt-export-form] button[type=submit]');
      (window as Window & { p0408Busy?: { disabled: boolean; text: string } }).p0408Busy = { disabled: button!.disabled, text: button!.textContent! };
    });
  }), 5_000, 'busy-state observer setup timed out');
  let clickDone: Promise<void> | undefined;
  try {
    const deadline = Date.now() + 30_000;
    const within = <T>(promise: Promise<T>, ms: number, message: string) => bounded(promise, Math.min(ms, Math.max(1, deadline - Date.now())), message);
    clickDone = page.getByRole('button', { name: 'Download list' }).click({ noWaitAfter: true });
    await within(Promise.race([posted, clickDone.then(() => new Promise<void>(() => {}))]), 10_000, 'export POST not seen within 10 s');
    expect(posts).toBe(1);
    const token = new URLSearchParams(body).get('downloadToken');
    expect(token).toMatch(/^[0-9a-f]{32}$/);
    expect(body).toContain('__RequestVerificationToken=');
    expect(body).toContain('channel=%40examplechannel');
    await within(context.addCookies([{ name: 'yt-export-done', value: token!, domain: new URL(page.url()).hostname, path: '/', httpOnly: false, sameSite: 'Strict' }]), 5_000, 'cookie setup timed out');
    releaseExport();
    await within(clickDone, 10_000, 'download click did not settle within 10 s');
    await within(expect.poll(() => page.evaluate(() => (window as Window & { p0408Busy?: { disabled: boolean; text: string } }).p0408Busy)).toEqual({ disabled: true, text: 'Fetching from YouTube… this can take a minute' }), 5_000, 'button did not become busy');
    await within(expect(page.getByRole('button', { name: 'Download list' })).toBeEnabled(), 10_000, 'button did not restore');
    await within(expect.poll(() => page.evaluate(() => document.cookie)).not.toContain(`yt-export-done=${token}`), 5_000, 'completion cookie did not expire');
    expect(posts).toBe(1);
  } finally {
    cleaningUp = true;
    releaseExport();
    if (clickDone) await bounded(clickDone.catch(() => undefined), 5_000, 'click cleanup timed out').catch(() => undefined);
    await bounded(page.unroute(predicate, handler), 5_000, 'route cleanup timed out').catch(() => undefined);
  }
});
