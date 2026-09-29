import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

const ADMIN_PAGES = [
  { route: '/Admin', slug: 'admin' },
  { route: '/Admin/Analytics', slug: 'analytics' },
  { route: '/Admin/ContentKb', slug: 'content-kb' },
  { route: '/Admin/CreatorProfile', slug: 'creator-profile' },
  { route: '/Admin/CreatorStyle', slug: 'creator-style' },
  { route: '/Admin/Feedback', slug: 'feedback' },
  { route: '/Admin/Flags', slug: 'flags' },
  { route: '/Admin/Harvest', slug: 'harvest' },
  { route: '/Admin/Tools', slug: 'tools' },
  { route: '/Admin/YoutubeExport', slug: 'youtube-export' },
] as const;

const ADMIN_STATES = [
  {
    slug: 'harvest-commanders', route: '/Admin/Harvest', drive: async (page: Page) => {
      await page.locator('#harvest-tab-commanders').click();
      const grid = page.locator('#commanders-grid-container');
      await expect(grid).toHaveAttribute('aria-busy', 'false');
      await expect(grid.locator('p.admin-meta:visible, .admin-banner--danger:visible')).toBeVisible();
    },
  },
  {
    slug: 'tools-filter-empty', route: '/Admin/Tools', drive: async (page: Page) => {
      const search = page.locator('#tools-filter-search');
      await expect(search).toBeVisible();
      await search.fill('zz-04-10-no-match');
      await expect(page.locator('#tools-filter-empty')).toBeVisible();
    },
  },
  {
    slug: 'flags-filter-empty', route: '/Admin/Flags', drive: async (page: Page) => {
      const search = page.locator('#flag-filter-search');
      await expect(search).toBeVisible();
      await search.fill('zz-04-10-no-match');
      await expect(page.locator('#flag-filter-empty')).toBeVisible();
    },
  },
] as const;

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

async function assertVisualPass(page: Page): Promise<void> {
  await expect(page.locator('h1')).toHaveCount(1);
  await expect(page.locator('h1')).toHaveClass(/admin-page-header__title/);

  const tablesAreContained = await page.locator('table').evaluateAll((tables) =>
    tables.every((table) => table.classList.contains('admin-table--card') || table.closest('.admin-table-scroll') !== null));
  expect(tablesAreContained).toBeTruthy();

  const emptyMessagesAreMuted = await page.locator('.admin-shell').evaluate((shell) => {
    const probe = document.createElement('span');
    probe.style.color = 'var(--muted)';
    shell.append(probe);
    const muted = getComputedStyle(probe).color;
    const emptyElements = Array.from(shell.querySelectorAll<HTMLElement>('.admin-empty, .admin-filter__empty, .admin-filter__empty-row td'));
    const result = emptyElements.filter((element) => element.checkVisibility()).every((element) => getComputedStyle(element).color === muted);
    probe.remove();
    return result;
  });
  expect(emptyMessagesAreMuted).toBeTruthy();

  await page.setViewportSize({ width: 375, height: 800 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBeTruthy();
}

for (const adminPage of ADMIN_PAGES) {
  test(`visual pass: ${adminPage.route}`, async ({ page }) => {
    const response = await page.goto(adminPage.route);
    expect(response?.ok()).toBeTruthy();
    await assertVisualPass(page);
  });
}

for (const state of ADMIN_STATES) {
  test(`visual pass state: ${state.slug}`, async ({ page }) => {
    const response = await page.goto(state.route);
    expect(response?.ok()).toBeTruthy();
    await state.drive(page);
    await assertVisualPass(page);
  });
}

test('harvest stat tiles stack label, value and badge', async ({ page }) => {
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  const result = await page.locator('.admin-stat-grid').evaluate((grid) => {
    const tiles = [...grid.querySelectorAll<HTMLElement>('.admin-stat-tile')];
    const stacked = tiles.map((tile) => {
      const label = tile.querySelector<HTMLElement>('.admin-stat-tile__label')!;
      const value = tile.querySelector<HTMLElement>('.admin-stat-tile__value')!;
      const labelBox = label.getBoundingClientRect();
      const valueBox = value.getBoundingClientRect();
      return labelBox.bottom <= valueBox.top + .5 && Math.abs(labelBox.left - valueBox.left) <= 1;
    });
    const tile = document.createElement('div');
    tile.className = 'admin-stat-tile';
    tile.innerHTML = '<span class="admin-stat-tile__label">Queued</span><strong class="admin-stat-tile__value">1</strong><span class="admin-badge admin-badge--warning">Backlog</span>';
    grid.append(tile);
    const value = tile.querySelector<HTMLElement>('.admin-stat-tile__value')!;
    const badge = tile.querySelector<HTMLElement>('.admin-badge')!;
    const style = getComputedStyle(tile);
    const contentWidth = tile.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
    const badgeBox = badge.getBoundingClientRect();
    const valueBox = value.getBoundingClientRect();
    const ownRow = badgeBox.top >= valueBox.bottom - .5;
    const ownWidth = badgeBox.width <= contentWidth - 8;
    badge.style.justifySelf = 'stretch';
    const stretches = badge.getBoundingClientRect().width >= contentWidth - 1;
    tile.remove();
    return { count: tiles.length, stacked, ownRow, ownWidth, stretches };
  });
  expect(result.count).toBe(4);
  expect(result.stacked.every(Boolean)).toBeTruthy();
  expect(result.ownRow).toBeTruthy();
  expect(result.ownWidth).toBeTruthy();
  expect(result.stretches).toBeTruthy();
});

test('admin page screenshots', async ({ page }, testInfo) => {
  test.skip(!process.env.ADMIN_SCREENSHOT_DIR, 'ADMIN_SCREENSHOT_DIR is not set.');
  test.setTimeout(180000);
  const directory = process.env.ADMIN_SCREENSHOT_DIR!;

  for (const adminPage of ADMIN_PAGES) {
    const response = await page.goto(adminPage.route);
    expect(response?.ok()).toBeTruthy();
    await page.screenshot({ path: `${directory}/${testInfo.project.name}-${adminPage.slug}.png`, fullPage: true });
  }

  for (const state of ADMIN_STATES) {
    const response = await page.goto(state.route);
    expect(response?.ok()).toBeTruthy();
    await state.drive(page);
    await page.screenshot({ path: `${directory}/${testInfo.project.name}-${state.slug}.png`, fullPage: true });
  }
});
