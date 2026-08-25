import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';
import { expandCutLabSection } from './support/cut-lab-mobile-collapse';
import { clickManabasePillRadio } from './support/manabase-pill';

const oversizedPool = [
  'Commander',
  '1 Zur the Enchanter',
  '',
  'Deck',
  '36 Plains',
  '36 Island',
  '20 Swamp',
  '1 Sol Ring',
  '1 Arcane Signet',
  '1 Fellwar Stone',
  '1 Mystic Remora',
  '1 Rhystic Study',
  '1 Swords to Plowshares',
  '1 Path to Exile',
  '1 Counterspell',
  '1 Dovin\'s Veto',
  '1 Demonic Tutor',
  '1 Enlightened Tutor',
  '1 Command Tower',
  '1 Exotic Orchard',
].join('\n');

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

const importPool = async (page: Page): Promise<void> => {
  await page.goto('/cut-lab');
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(oversizedPool);
  await page.locator('#cut-lab-primary-plan').fill('Protect the control shell, then trim to the cleanest Zur line.');
  await page.locator('#cut-lab-secondary-plan').fill('Keep the fast mana package intact.');
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
  await page.getByRole('button', { name: 'Import pool' }).click();

  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await expandCutLabSection(page, 'cut-lab-section-competes');
  await page.locator('details.cutlab-role-group').filter({ hasText: 'Lands' }).locator(':scope > summary').click();
  await expect(page.locator('[data-cut-lab-lock-role="lands"]')).toBeVisible();
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"]')).toHaveAttribute('data-cut-lab-commander', 'true');
};

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Cut Lab', true);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

test('G-1 preserves the five-slot wizard contract in document order', async ({ page }) => {
  await importPool(page);

  await expect(page.locator('[role="tabpanel"]').evaluateAll((panels) => panels.map((panel) => panel.id))).resolves.toEqual([
    'cut-lab-step-panel-1',
    'cut-lab-step-panel-2',
    'cut-lab-step-panel-3',
    'cut-lab-step-panel-4',
    'cut-lab-step-panel-5',
  ]);
});

test('G-2 activates exactly one panel through native tab dispatch', async ({ page }) => {
  await importPool(page);

  const tabs = page.locator('[role="tab"]:not([aria-disabled="true"])');
  const tabCount = await tabs.count();
  expect(tabCount).toBeGreaterThan(0);

  for (let index = 0; index < tabCount; index++) {
    await page.evaluate(() => window.scrollTo(0, 0));
    const tab = tabs.nth(index);
    const before = await page.locator('[role="tab"]').evaluateAll((buttons) => buttons.map((button) => button.getAttribute('aria-selected')));
    const beforePanels = await page.locator('[role="tabpanel"]').evaluateAll((panels) => panels.map((panel) => getComputedStyle(panel).display));
    await tab.evaluate((button) => {
      button.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
    });

    const after = await page.locator('[role="tab"]').evaluateAll((buttons) => buttons.map((button) => button.getAttribute('aria-selected')));
    const afterPanels = await page.locator('[role="tabpanel"]').evaluateAll((panels) => panels.map((panel) => getComputedStyle(panel).display));
    expect(after).not.toEqual(before);
    expect(afterPanels).not.toEqual(beforePanels);
    await expect(tab).toHaveAttribute('aria-selected', 'true');
    await expect(page.locator('[role="tabpanel"]').evaluateAll((panels) => panels.filter((panel) => getComputedStyle(panel).display !== 'none').length)).resolves.toBe(1);
    await expect(page.locator('[role="tab"].is-active')).toHaveCount(1);
  }
});

test('G-3 keeps Decide page bulk below the desktop and mobile headroom thresholds', async ({ page }) => {
  await importPool(page);

  const decideTab = page.locator('[role="tab"][aria-label="Decide"]');
  await expect(decideTab).toBeVisible();
  await expect(decideTab).toHaveAttribute('aria-disabled', 'false');
  await decideTab.click();
  await expect(decideTab).toHaveAttribute('aria-selected', 'true');
  const panelId = await decideTab.getAttribute('aria-controls');
  expect(panelId, 'Decide tab should reference a panel with aria-controls').toBeTruthy();
  const height = await page.locator(`#${panelId!}`).evaluate((panel) => panel.scrollHeight);
  const limit = page.viewportSize()?.width === 390 ? 4_000 : 3_000;
  expect(height, `Decide panel measured ${height}px, should be below ${limit}px`).toBeLessThan(limit);
});

test('G-4 collapses intake after import and exposes the commander summary', async ({ page }) => {
  await importPool(page);

  const deckTextarea = page.locator('#cut-lab-deck-text');
  await expect(deckTextarea).toHaveCount(1);
  const intakeIsCollapsed = await page.evaluate(() => {
    const textarea = document.querySelector('#cut-lab-deck-text');
    if (!textarea) return false;
    const disclosure = textarea.closest('details');
    return Boolean(disclosure && !disclosure.open);
  });
  expect(intakeIsCollapsed).toBe(true);
  const commanderSummary = page.locator('[data-cut-lab-intake-summary] > summary').filter({ hasText: 'Zur the Enchanter' });
  await expect(commanderSummary).toBeVisible();
  await commanderSummary.scrollIntoViewIfNeeded();
  await expect(commanderSummary).toBeInViewport();
});

test('G-5 keeps the complete Accept button visible and hit-testable while Decide evidence scrolls', async ({ page }) => {
  await importPool(page);

  const decideTab = page.locator('[role="tab"][aria-label="Decide"]');
  await decideTab.click();
  await expect(decideTab).toHaveAttribute('aria-selected', 'true');
  const panelId = await decideTab.getAttribute('aria-controls');
  expect(panelId).toBeTruthy();
  await page.locator(`#${panelId!} .cutlab-proposal__body`).evaluate((body) => {
    const scrollDepth = document.createElement('div');
    scrollDepth.style.height = '1200px';
    scrollDepth.setAttribute('aria-hidden', 'true');
    body.appendChild(scrollDepth);
  });
  await page.locator(`#${panelId!}`).evaluate((panel) => panel.scrollIntoView({ block: 'end' }));
  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));

  const acceptButton = page.getByRole('button', { name: 'Accept cut' });
  await expect(acceptButton).toBeVisible();
  if (page.viewportSize()?.width === 390) {
    const headingLineCount = await page.locator('.cutlab-proposal__pinned-row .cutlab-proposal__heading').evaluate((heading) => {
      const cardButton = heading.querySelector('button');
      if (cardButton) cardButton.textContent = 'Sword of Feast and Famine';
      const range = document.createRange();
      range.selectNodeContents(heading);
      return new Set(Array.from(range.getClientRects(), rect => Math.round(rect.top))).size;
    });
    expect(headingLineCount, 'long proposed card name wraps within two heading lines').toBeLessThanOrEqual(2);
  }
  const result = await acceptButton.evaluate((button) => {
    const rect = button.getBoundingClientRect();
    const offset = Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--cutlab-pinned-offset')) || 0;
    const hit = document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2);
    return {
      y: rect.y,
      bottom: rect.y + rect.height,
      innerHeight: window.innerHeight,
      stickyOffset: offset,
      isHit: hit === button || Boolean(hit && button.contains(hit)),
    };
  });

  expect(result.y, 'Accept button starts below the stacked sticky offset').toBeGreaterThanOrEqual(result.stickyOffset);
  expect(result.bottom, 'Accept button is fully inside the viewport').toBeLessThanOrEqual(result.innerHeight);
  expect(result.isHit, 'Accept button centre is not covered by another sticky layer').toBe(true);
});

// R2-2/F-1: guard production-rendered markup; overlap precondition prevents a vacuous hit test.
test('G-6 lets a workflow control beneath the pinned proposal receive a click', async ({ page }) => {
  await importPool(page);

  const decideTab = page.locator('[role="tab"][aria-label="Decide"]');
  await decideTab.click();
  await expect(decideTab).toHaveAttribute('aria-selected', 'true');

  const details = page.locator('details[data-cut-lab-delta-expander]');
  const summary = details.locator(':scope > summary');
  await expect(summary, 'details[data-cut-lab-delta-expander] > summary is absent: proposal rendered without metric deltas and the guard has lost its target').toBeVisible();
  const wasOpen = await details.evaluate((element) => element.open);

  const measure = () => page.evaluate(() => {
    const pinned = document.querySelector<HTMLElement>('.cutlab-proposal--pinned');
    const summary = document.querySelector<HTMLElement>('details[data-cut-lab-delta-expander] > summary');
    if (!pinned || !summary) throw new Error('Pinned proposal or delta-expander summary was not rendered.');

    const pinnedRect = pinned.getBoundingClientRect();
    const summaryRect = summary.getBoundingClientRect();
    const x = summaryRect.x + summaryRect.width / 2;
    const y = summaryRect.y + summaryRect.height / 2;
    const hit = document.elementFromPoint(x, y);
    return {
      pinnedRect: { left: pinnedRect.left, top: pinnedRect.top, right: pinnedRect.right, bottom: pinnedRect.bottom },
      summaryRect: { left: summaryRect.left, top: summaryRect.top, right: summaryRect.right, bottom: summaryRect.bottom },
      x,
      y,
      overlaps: x >= pinnedRect.left && x <= pinnedRect.right && y >= pinnedRect.top && y <= pinnedRect.bottom,
      isSummaryHit: hit === summary || Boolean(hit && summary.contains(hit)),
      hit: hit?.outerHTML ?? null,
    };
  });

  let result = await measure();
  for (let attempt = 0; attempt < 12 && !result.overlaps; attempt++) {
    await page.evaluate(() => window.scrollBy(0, 200));
    result = await measure();
  }

  expect(result.overlaps, `delta-expander summary centre must overlap pinned proposal; pinned=${JSON.stringify(result.pinnedRect)} summary=${JSON.stringify(result.summaryRect)}`).toBe(true);
  expect(result.isSummaryHit, `delta-expander summary centre must not be covered; elementFromPoint returned ${result.hit}`).toBe(true);
  await page.mouse.click(result.x, result.y);
  expect(await details.evaluate((element) => element.open), 'delta-expander open state flips after its centre receives a real click').toBe(!wasOpen);
});
