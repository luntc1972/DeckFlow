import { expect, test } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

test('admin tokens resolve on the live admin shell', async ({ page }) => {
  const response = await page.goto('/Admin/Tools');
  expect(response?.ok()).toBeTruthy();

  await page.locator('details.admin-sidebar__disclosure').evaluate((details) => {
    details.open = true;
  });

  const tokens = await page.evaluate(() => {
    const styles = getComputedStyle(document.documentElement);
    return {
      spaceMd: styles.getPropertyValue('--space-md').trim(),
      statusDanger: styles.getPropertyValue('--status-danger').trim(),
      textBody: styles.getPropertyValue('--text-body').trim(),
    };
  });

  expect(tokens).toEqual({ spaceMd: '16px', statusDanger: '#ef4444', textBody: '15px' });
  await expect(page.locator('a.admin-sidebar__link[aria-current="page"]')).toHaveCount(1);
  await expect(page.locator('a.admin-sidebar__link[aria-current="page"]')).toHaveCSS(
    'background-color',
    'rgba(59, 130, 246, 0.08)');
});

test('admin shell marks the current page and renders one page header', async ({ page }) => {
  await page.goto('/Admin/Tools');
  await page.locator('details.admin-sidebar__disclosure').evaluate((details) => { details.open = true; });
  const current = page.locator('a.admin-sidebar__link[aria-current="page"]');
  await expect(page.locator('a.admin-sidebar__link')).toHaveCount(10);
  await expect(current).toHaveCount(1);
  await expect(current).toHaveAttribute('href', /\/Admin\/Tools$/);
  await expect(current).toHaveCSS('border-left-width', '4px');
  await expect(current).toHaveCSS('font-weight', '600');
  await expect(page.locator('h1.admin-page-header__title')).toHaveCount(1);
  await expect(page.locator('header.admin-topbar h1')).toHaveCount(0);
});

test('admin cards take their spacing from the parent gap', async ({ page }) => {
  await page.goto('/Admin');
  const result = await page.locator('main#admin-content').evaluate((main) => {
    const stack = document.createElement('div'); stack.className = 'admin-stack';
    for (let index = 0; index < 2; index++) { const card = document.createElement('section'); card.className = 'admin-card'; stack.append(card); }
    main.append(stack); const cards = stack.querySelectorAll<HTMLElement>('.admin-card');
    return { display: getComputedStyle(stack).display, gap: getComputedStyle(stack).rowGap, first: getComputedStyle(cards[0]).marginTop, second: getComputedStyle(cards[1]).marginBottom };
  });
  expect(result.display).toBe('flex'); expect(result.gap).not.toBe('0px'); expect(result.first).toBe('0px'); expect(result.second).toBe('0px');
});

test('admin filter chip wraps a long unbroken label at 375px', async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 800 }); await page.goto('/Admin/Tools');
  const result = await page.locator('main#admin-content').evaluate((main) => {
    const chip = document.createElement('button'); chip.className = 'admin-filter-chips__chip'; chip.textContent = `namespace.${'a'.repeat(110)}`;
    main.append(chip); return { overflow: chip.scrollWidth <= chip.clientWidth + 1, height: chip.getBoundingClientRect().height };
  });
  expect(result.overflow).toBeTruthy(); expect(result.height).toBeGreaterThan(44);
});
