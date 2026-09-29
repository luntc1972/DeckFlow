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
      await expect(grid.locator('p.admin-meta').first()).toBeVisible();
      await expect(grid.locator('.admin-banner--danger')).toHaveCount(0);
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

test('harvest-commanders sweep state rejects a failed grid load', async ({ page }) => {
  await page.route('**/Admin/Harvest/commanders**', async (route) => {
    await route.fulfill({ status: 500, contentType: 'text/html', body: 'forced failure' });
  });
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  const state = ADMIN_STATES.find((item) => item.slug === 'harvest-commanders')!;
  let outcome = 'passed';
  try {
    await state.drive(page);
  } catch {
    outcome = 'rejected';
  }
  const banner = page.locator('#commanders-grid-container .admin-banner--danger[role="alert"]');
  await expect(banner).toBeVisible();
  await expect(banner).toContainText('Could not load commanders.');
  expect(outcome).toBe('rejected');
});

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

test('filter chips render alike on all five chip pages', async ({ page }) => {
  test.setTimeout(90000);
  const pages = ['/Admin/Analytics', '/Admin/ContentKb', '/Admin/Feedback', '/Admin/Flags', '/Admin/Tools'];
  const fonts: string[] = [];
  for (const route of pages) {
    const response = await page.goto(route);
    expect(response?.ok()).toBeTruthy();
    const chip = page.locator('.admin-filter-chips__chip:not(.is-active)').first();
    await expect(chip).toBeVisible();
    const style = await chip.evaluate((element) => {
      const probe = document.createElement('span');
      probe.style.color = 'var(--muted)';
      element.closest('.admin-shell')!.append(probe);
      const muted = getComputedStyle(probe).color;
      probe.remove();
      const computed = getComputedStyle(element);
      return { paddingTop: computed.paddingTop, paddingLeft: computed.paddingLeft, paddingRight: computed.paddingRight, cursor: computed.cursor, decoration: computed.textDecorationLine, color: computed.color, font: `${computed.fontSize}|${computed.fontFamily}`, muted };
    });
    expect(style).toMatchObject({ paddingTop: '4px', paddingLeft: '16px', paddingRight: '16px', cursor: 'pointer', decoration: 'none', color: style.muted });
    fonts.push(style.font);
  }
  expect(new Set(fonts).size).toBe(1);

  for (const route of ['/Admin/Feedback', '/Admin/Tools']) {
    await page.goto(route);
    const activeChip = page.locator('.admin-filter-chips__chip.is-active').first();
    await expect(activeChip).toBeVisible();
    const colors = await activeChip.evaluate((element) => {
      const probe = document.createElement('span');
      probe.style.color = 'var(--text)';
      element.closest('.admin-shell')!.append(probe);
      const text = getComputedStyle(probe).color;
      probe.remove();
      return { chip: getComputedStyle(element).color, text };
    });
    expect(colors.chip).toBe(colors.text);
  }

  await page.goto('/Admin/Feedback');
  const realChip = page.locator('.admin-filter-chips__chip:not(.is-active)').first();
  await expect(realChip).toBeVisible();
  await page.evaluate(() => {
    const chips = document.querySelector('.admin-filter-chips')!;
    chips.insertAdjacentHTML('beforeend', '<button id="disabled-filter-chip" class="admin-filter-chips__chip" disabled>Disabled</button><a id="aria-disabled-filter-chip" class="admin-filter-chips__chip" href="#" aria-disabled="true">Aria disabled</a>');
  });
  try {
    for (const [selector, expectedDisabled] of [['.admin-filter-chips__chip:not(.is-active)', false], ['#disabled-filter-chip', true], ['#aria-disabled-filter-chip', true]] as const) {
      const chip = page.locator(selector).first();
      await chip.hover();
      const state = await chip.evaluate((element) => {
        const shell = element.closest('.admin-shell')!;
        const mutedProbe = document.createElement('span');
        mutedProbe.style.color = 'var(--muted)';
        const textProbe = document.createElement('span');
        textProbe.style.color = 'var(--text)';
        shell.append(mutedProbe, textProbe);
        const result = { hovered: element.matches(':hover'), color: getComputedStyle(element).color, cursor: getComputedStyle(element).cursor, decoration: getComputedStyle(element).textDecorationLine, muted: getComputedStyle(mutedProbe).color, text: getComputedStyle(textProbe).color };
        mutedProbe.remove();
        textProbe.remove();
        return result;
      });
      expect(state.hovered).toBeTruthy();
      expect(state.decoration).toBe('none');
      expect(state.color).toBe(expectedDisabled ? state.muted : state.text);
      if (expectedDisabled) expect(state.cursor).toBe('not-allowed');
    }
  } finally {
    await page.locator('#disabled-filter-chip, #aria-disabled-filter-chip').evaluateAll((elements) => elements.forEach((element) => element.remove()));
  }
});

test('harvest tabs carry padding, pointer and a gap before the panel', async ({ page }) => {
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  const metrics = await page.evaluate(() => {
    const tab = document.querySelector<HTMLElement>('#harvest-tab-overview')!;
    const strip = document.querySelector<HTMLElement>('.admin-tabs')!;
    const panel = document.querySelector<HTMLElement>('#harvest-panel-overview')!;
    const tabStyle = getComputedStyle(tab);
    return { paddingTop: tabStyle.paddingTop, paddingLeft: tabStyle.paddingLeft, cursor: tabStyle.cursor, marginBottom: getComputedStyle(strip).marginBottom, gap: panel.getBoundingClientRect().top - strip.getBoundingClientRect().bottom };
  });
  expect(metrics).toMatchObject({ paddingTop: '8px', paddingLeft: '16px', cursor: 'pointer', marginBottom: '16px' });
  expect(metrics.gap).toBeGreaterThanOrEqual(15.5);
});

test('content kb status and publish badges stay on one line', async ({ page }) => {
  const response = await page.goto('/Admin/ContentKb');
  expect(response?.ok()).toBeTruthy();
  const rows = await page.locator('#kb-entries-table tbody tr').count();
  test.skip(rows === 0, 'no KB rows seeded');
  await expect(page.locator('td[data-label="Status"] .admin-badge:visible')).not.toHaveCount(0);
  await expect(page.locator('td[data-label="Publish State"] .admin-badge:visible')).not.toHaveCount(0);
  const result = await page.evaluate(() => {
    const badges = [...document.querySelectorAll<HTMLElement>('#kb-entries-table .admin-badge')]
      .filter((badge) => badge.checkVisibility())
      .map((badge) => {
        const box = badge.getBoundingClientRect();
        const cell = badge.closest<HTMLElement>('td')!.getBoundingClientRect();
        return { text: badge.textContent?.trim() ?? '', height: box.height, lineHeight: Number.parseFloat(getComputedStyle(badge).lineHeight), inside: box.left >= cell.left - .5 && box.right <= cell.right + .5 };
      });
    const wrapper = document.querySelector<HTMLElement>('.admin-table-scroll')!;
    return { badges, scrollWidth: wrapper.scrollWidth, clientWidth: wrapper.clientWidth };
  });
  for (const badge of result.badges) {
    expect(badge.lineHeight).toBeGreaterThan(0);
    expect(Number.isFinite(badge.lineHeight)).toBeTruthy();
    expect(badge.height / badge.lineHeight, `content kb badge renders on one line: ${badge.text}`).toBeLessThan(1.6);
    expect(badge.inside, `content kb badge stays inside its cell: ${badge.text}`).toBeTruthy();
  }
  expect(result.scrollWidth, 'content kb table does not scroll horizontally').toBeLessThanOrEqual(result.clientWidth + 1);
});

test('harvest backlog badge stays inside its stat tile', async ({ page }) => {
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  const result = await page.locator('#health-queued-decks').evaluate((queued) => {
    const tile = queued.closest<HTMLElement>('.admin-stat-tile')!;
    const badge = document.createElement('span');
    badge.className = 'admin-badge admin-badge--warning admin-badge--alert';
    badge.textContent = 'Backlog exceeds floor and growing';
    tile.append(badge);
    try {
      const style = getComputedStyle(tile); const rect = tile.getBoundingClientRect(); const box = badge.getBoundingClientRect();
      const left = rect.left + Number.parseFloat(style.borderLeftWidth) + Number.parseFloat(style.paddingLeft);
      const right = rect.right - Number.parseFloat(style.borderRightWidth) - Number.parseFloat(style.paddingRight);
      return { left, right, badgeLeft: box.left, badgeRight: box.right, scrollWidth: badge.scrollWidth, clientWidth: badge.clientWidth, documentWidth: document.documentElement.scrollWidth, innerWidth };
    } finally { badge.remove(); }
  });
  expect(result.badgeLeft, 'backlog badge fits its stat tile').toBeGreaterThanOrEqual(result.left - .5);
  expect(result.badgeRight, 'backlog badge fits its stat tile').toBeLessThanOrEqual(result.right + .5);
  expect(result.scrollWidth, 'backlog badge fits its stat tile').toBeLessThanOrEqual(result.clientWidth + 1);
  expect(result.documentWidth, 'backlog badge fits its stat tile').toBeLessThanOrEqual(result.innerWidth + 1);
});

test('wrapped harvest backlog badge uses 4px corners', async ({ page }) => {
  const response = await page.goto('/Admin/Harvest');
  expect(response?.ok()).toBeTruthy();
  const result = await page.locator('#health-queued-decks').evaluate((queued) => {
    const tile = queued.closest<HTMLElement>('.admin-stat-tile')!; const badge = document.createElement('span');
    badge.className = 'admin-badge admin-badge--warning admin-badge--alert'; badge.textContent = 'Backlog exceeds floor and growing'; tile.append(badge);
    try { const style = getComputedStyle(badge); return { height: badge.getBoundingClientRect().height, lineHeight: Number.parseFloat(style.lineHeight), topLeft: style.borderTopLeftRadius, bottomRight: style.borderBottomRightRadius }; }
    finally { badge.remove(); }
  });
  expect(result.lineHeight).toBeGreaterThan(0);
  expect(Number.isFinite(result.lineHeight)).toBeTruthy();
  expect(result.height / result.lineHeight, 'backlog badge wraps in its stat tile').toBeGreaterThanOrEqual(1.6);
  expect(result.topLeft, 'wrapped backlog badge uses 4px corners').toBe('4px');
  expect(result.bottomRight, 'wrapped backlog badge uses 4px corners').toBe('4px');
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
