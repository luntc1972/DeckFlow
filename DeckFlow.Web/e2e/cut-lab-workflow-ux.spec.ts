import { expect, test, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';
import { expandCutLabSection } from './support/cut-lab-mobile-collapse';
import { clickManabasePillRadio } from './support/manabase-pill';
import { cutLabPool as oversizedPool } from './fixtures/cut-lab-pool';

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

const importPool = async (page: Page): Promise<void> => {
  await page.goto('/cut-lab');
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(oversizedPool);
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
  await page.getByRole('button', { name: 'Import pool' }).click();

  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await expandCutLabSection(page, 'cut-lab-section-competes');
  await page.locator('details.cutlab-role-group').filter({ hasText: 'Lands' }).locator(':scope > summary').click();
  await expect(page.locator('[data-cut-lab-lock-role="lands"]')).toBeVisible();
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"]')).toHaveAttribute('data-cut-lab-commander', 'true');
  const decideStep = page.locator('[role="tab"][aria-label="Decide"]');
  await decideStep.click();
  await expect(decideStep).toHaveAttribute('aria-selected', 'true');
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
  // The section must be expanded or the height assertion is vacuous.
  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
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
  // The Decide step no longer guarantees cut rounds is expanded; expand it through the UI before controls enter the accessibility tree.
  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
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
  // The Decide step no longer guarantees cut rounds is expanded; expand it through the UI before controls enter the accessibility tree.
  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
  // Expand Tune quantities purely to give the page enough scroll room for the sticky proposal to reach maximum travel; without it the overlap precondition is unreachable rather than false.
  await expandCutLabSection(page, 'cut-lab-section-tune');

  const details = page.locator('details[data-cut-lab-delta-expander]');
  const summary = details.locator(':scope > summary');
  await expect(summary, 'details[data-cut-lab-delta-expander] > summary is absent: proposal rendered without metric deltas and the guard has lost its target').toBeVisible();
  const wasOpen = await details.evaluate((element) => (element as HTMLDetailsElement).open);

  const measure = () => page.evaluate(() => {
    const pinned = document.querySelector<HTMLElement>('.cutlab-proposal--pinned');
    const summary = document.querySelector<HTMLElement>('details[data-cut-lab-delta-expander] > summary');
    if (!pinned || !summary) throw new Error('Pinned proposal or delta-expander summary was not rendered.');

    const pinnedRect = pinned.getBoundingClientRect();
    const summaryRect = summary.getBoundingClientRect();
    const x = summaryRect.x + summaryRect.width / 2;
    const y = summaryRect.y + summaryRect.height / 2;
    const hit = document.elementFromPoint(x, y);
    const capturingRects = [...pinned.querySelectorAll<HTMLElement>('.cutlab-proposal__pinned-actions, button')]
      .map((element) => element.getBoundingClientRect())
      .filter((rect) => rect.left <= x && x <= rect.right)
      .sort((left, right) => left.top - right.top);
    const gaps: Array<[number, number]> = [];
    let coveredBottom = pinnedRect.top;
    for (const rect of capturingRects) {
      const top = Math.max(rect.top, pinnedRect.top);
      const bottom = Math.min(rect.bottom, pinnedRect.bottom);
      if (bottom <= top) continue;
      if (top > coveredBottom) gaps.push([coveredBottom - pinnedRect.top, top - pinnedRect.top]);
      coveredBottom = Math.max(coveredBottom, bottom);
    }
    if (coveredBottom < pinnedRect.bottom) gaps.push([coveredBottom - pinnedRect.top, pinnedRect.bottom - pinnedRect.top]);
    const capturedByChild = capturingRects.some((rect) => y >= rect.top && y <= rect.bottom);
    return {
      pinnedRect: { left: pinnedRect.left, top: pinnedRect.top, right: pinnedRect.right, bottom: pinnedRect.bottom },
      summaryRect: { left: summaryRect.left, top: summaryRect.top, right: summaryRect.right, bottom: summaryRect.bottom },
      capturingRects: capturingRects.map((rect) => ({ left: rect.left, top: rect.top, right: rect.right, bottom: rect.bottom })),
      gaps,
      x,
      y,
      overlaps: x >= pinnedRect.left && x <= pinnedRect.right && y >= pinnedRect.top && y <= pinnedRect.bottom,
      capturedByChild,
      passesThrough: x >= pinnedRect.left && x <= pinnedRect.right && y >= pinnedRect.top && y <= pinnedRect.bottom && !capturedByChild,
      d: y - pinnedRect.top,
      isSummaryHit: hit === summary || Boolean(hit && summary.contains(hit)),
      hit: hit?.outerHTML ?? null,
      scrollY: window.scrollY,
      scrollHeight: document.documentElement.scrollHeight,
    };
  });

  // Why: d = summaryCentre - pinnedTop is monotone non-increasing in scrollY and is maximal at scroll top.
  // Measuring it anywhere else (mobile leaves the page saturated at scrollY 2008) reports the FLOOR, and the
  // reachability filter below then discards every band the summary could actually still descend through.
  await page.evaluate(() => window.scrollTo(0, 0));
  const initial = await measure();
  const targetGap = initial.gaps
    .filter(([top, bottom]) => (top + bottom) / 2 <= initial.d)
    .sort(([leftTop, leftBottom], [rightTop, rightBottom]) => (rightTop + rightBottom) - (leftTop + leftBottom))[0];
  if (!targetGap) throw new Error(`No reachable pinned pass-through gap; gaps=${JSON.stringify(initial.gaps)} d=${initial.d}`);

  const targetD = (targetGap[0] + targetGap[1]) / 2;
  let result = initial;
  let lo = 0;
  let hi = await page.evaluate(() => document.documentElement.scrollHeight - window.innerHeight);
  for (let attempt = 0; attempt < 30; attempt++) {
    const mid = (lo + hi) / 2;
    await page.evaluate((scrollY) => window.scrollTo(0, scrollY), mid);
    result = await measure();
    if (result.passesThrough) break;
    if (result.d > targetD) lo = mid;
    else hi = mid;
  }

  expect(result.passesThrough, `delta-expander summary centre must pass through pinned proposal; pinned=${JSON.stringify(result.pinnedRect)} summary=${JSON.stringify(result.summaryRect)} capturingRects=${JSON.stringify(result.capturingRects)} gaps=${JSON.stringify(result.gaps)} d=${result.d} targetD=${targetD} scrollY=${result.scrollY} scrollHeight=${result.scrollHeight}`).toBe(true);
  expect(result.isSummaryHit, `delta-expander summary centre must not be covered; elementFromPoint returned ${result.hit}`).toBe(true);
  await page.mouse.click(result.x, result.y);
  expect(await details.evaluate((element) => (element as HTMLDetailsElement).open), 'delta-expander open state flips after its centre receives a real click').toBe(!wasOpen);
});
