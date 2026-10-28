import { expect, test, type Locator, type Page } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';
import { expandCutLabSection } from './support/cut-lab-mobile-collapse';
import { clickManabasePillRadio } from './support/manabase-pill';

import { resolveE2EPort } from './support/e2e-port';

const baseUrl = `http://localhost:${resolveE2EPort()}`;

const oversizedPool = [
  'Commander',
  '1 Zur the Enchanter',
  '',
  'Deck',
  '35 Plains',
  '35 Island',
  '20 Swamp',
  '1 Sol Ring #Ramp',
  '1 Arcane Signet #Ramp',
  '1 Fellwar Stone',
  '1 Mystic Remora',
  '1 Rhystic Study #Draw',
  '1 Swords to Plowshares',
  '1 Path to Exile',
  '1 Counterspell',
  '1 Dovin\'s Veto',
  '1 Demonic Tutor #Tutor',
  '1 Enlightened Tutor',
  '1 Command Tower',
  '1 Exotic Orchard',
].join('\n');

type LockHandle = Awaited<ReturnType<typeof acquireAdminLockForTest>>;

let heldLock: LockHandle | null = null;

test.describe.configure({ mode: 'serial' });

const importPool = async (page: Page): Promise<void> => {
  await page.goto(`${baseUrl}/cut-lab`);
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await fillImportForm(page);
  await page.getByRole('button', { name: 'Import pool' }).click();

  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"]')).toHaveAttribute('data-cut-lab-commander', 'true');
};

const fillImportForm = async (page: Page): Promise<void> => {
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(oversizedPool);
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
};

const waitForCutRounds = async (page: Page): Promise<void> => {
  await expandCutLabSection(page, 'cut-lab-section-cut-rounds');
  await expect(page.getByRole('heading', { name: 'Cut rounds' })).toBeVisible();
  await expect(page.locator('.cutlab-round-banner')).toBeVisible();
  await expect(page.locator('.cutlab-round-banner > p')).toHaveText(/.+/);
  await expect(page.locator('.cutlab-proposal')).toBeVisible();
};

const getExportTab = (page: Page): Locator => page.locator('#cut-lab-step-tab-5');

const getExportPanel = (page: Page): Locator => page.locator('#cut-lab-step-panel-5');

const cutToTarget = async (page: Page): Promise<void> => {
  // At exactly 100 the sticky remaining counter is removed and the proposal shows the
  // terminal "You're at 100 cards" heading, so drive off that single heading element.
  // Each accepted cut removes that card from the working list, so the proposal heading
  // always advances to a distinct card (or the terminal heading) — wait on that change
  // to sync each accept, avoiding races with the async re-render.
  const headingLocator = page.locator('.cutlab-proposal__heading');
  for (let guard = 0; guard < 12; guard += 1) {
    // Let the previous decision's async re-render fully settle before clicking, so a
    // click never races an in-flight decide response (which drops or double-fires it).
    await page.waitForLoadState('networkidle');
    const heading = (await headingLocator.textContent())?.trim() ?? '';
    if (heading.includes('at 100 cards')) {
      return;
    }

    const accept = page.locator('.cutlab-proposal .cutlab-decision-btn--accept');
    if (await accept.count() === 0) {
      throw new Error(`No accept button and not at target (heading: "${heading}").`);
    }

    await accept.first().click();
    // The first decide pays cold JIT + cold sim-cache cost; allow headroom on slow CI.
    await expect(headingLocator).not.toHaveText(heading, { timeout: 30_000 });
  }

  throw new Error('Expected to reach exactly 100 cards within 12 accepted cuts.');
};

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Cut Lab', true);
});

test.afterEach(async () => {
  await releaseAdminLockForTest(heldLock);
  heldLock = null;
});

const categorySection = (page: Page): Locator => page.locator('#cut-lab-section-by-category');
const group = (page: Page, category: string): Locator => categorySection(page).locator('details.cutlab-role-group').filter({ has: page.locator(`[data-cut-lab-category-locked="${category}"]`) });
const openCard = async (page: Page, category: string, name: string): Promise<void> => {
  await expandCutLabSection(page, 'cut-lab-section-by-category');
  const container = group(page, category);
  if (!(await container.evaluate(node => (node as HTMLDetailsElement).open))) await container.locator('summary').click();
  await container.locator(`[data-cutlab-card-open="${name}"]`).click();
  await expect(page.locator('#cutlab-card-modal')).toBeVisible();
};
const saveCategory = async (page: Page): Promise<void> => {
  await page.locator('[data-cut-lab-category-input]').fill('FastMana');
  await page.getByRole('button', { name: 'Save category', exact: true }).click();
  await expect(group(page, 'FastMana')).toHaveCount(1);
};
const assertState = async (page: Page): Promise<void> => {
  const states = await page.locator('input[name="CutLabStateJson"]').evaluateAll(inputs => inputs.map(input => JSON.parse((input as HTMLInputElement).value)));
  expect(states.length).toBeGreaterThan(1);
  for (const state of states) {
    expect(state.categoryOverrides).toEqual([{ name: 'Sol Ring', category: 'FastMana' }]);
    expect(state.originalEntries.length).toBeGreaterThan(0);
  }
};

test('category edits survive locks and export with imported categories intact', async ({ page }) => {
  await importPool(page);
  await expandCutLabSection(page, 'cut-lab-section-by-category');
  const keys = await categorySection(page).locator('[data-cut-lab-category-locked]').evaluateAll(nodes => nodes.map(node => node.getAttribute('data-cut-lab-category-locked')));
  expect(keys).toEqual(['Draw', 'Ramp', 'Tutor', 'uncategorized']);
  await expect(group(page, 'Ramp').locator('[data-cutlab-card-open="Arcane Signet"]')).toHaveCount(1);
  for (const name of ['Sol Ring', 'Arcane Signet']) {
    await openCard(page, 'Ramp', name);
    await page.locator('[data-cutlab-modal-lock]').click();
    await page.locator('[data-cutlab-modal-close]').click();
  }
  await expect(group(page, 'Ramp').locator('[data-cut-lab-category-locked]')).toHaveText('2');
  await openCard(page, 'Ramp', 'Sol Ring');
  await expect(page.locator('[data-cut-lab-category-input]')).toHaveValue('Ramp');
  expect(await page.locator('#cutlab-category-options option').evaluateAll(nodes => nodes.map(node => (node as HTMLOptionElement).value))).toEqual(expect.arrayContaining(['Draw', 'Ramp', 'Tutor']));
  await saveCategory(page);
  await expect(group(page, 'FastMana').locator('[data-cutlab-card-open="Sol Ring"]')).toHaveAttribute('aria-pressed', 'true');
  await expect(group(page, 'Ramp').locator('[data-cutlab-card-open="Arcane Signet"]')).toHaveCount(1);
  await assertState(page);
  await openCard(page, 'FastMana', 'Sol Ring');
  await page.locator('[data-cutlab-modal-lock]').click();
  await assertState(page);
  await page.locator('[data-cutlab-modal-lock]').click();
  await page.locator('[data-cutlab-modal-close]').click();
  await waitForCutRounds(page);
  await cutToTarget(page);
  await getExportTab(page).click();
  const panel = getExportPanel(page);
  const printing = '(?: \\([^\\n)]+\\) \\S+)?';
  for (const format of ['moxfield', 'archidekt'] as const) {
    const full = await panel.locator(`#cut-lab-export-${format}-full`).inputValue();
    for (const [name, category] of [['Sol Ring', 'FastMana'], ['Arcane Signet', 'Ramp']]) {
      const tag = format === 'moxfield' ? `#${category}` : `\\[${category}\\]`;
      expect(full).toMatch(new RegExp(`^1 ${name}${printing} ${tag}$`, 'm'));
    }
    const patch = await panel.locator(`#cut-lab-export-${format}-patch`).inputValue();
    const oldTag = format === 'moxfield' ? '#Ramp' : '\\[Ramp\\]';
    const newTag = format === 'moxfield' ? '#FastMana' : '\\[FastMana\\]';
    const [cuts, adds] = patch.split('# ADD (add these)');
    expect(cuts).toMatch(new RegExp(`^1 Sol Ring${printing} ${oldTag}$`, 'm'));
    expect(adds).toMatch(new RegExp(`^1 Sol Ring${printing} ${newTag}$`, 'm'));
  }
});

test('Use imported category resets the edit', async ({ page }) => {
  await importPool(page);
  await openCard(page, 'Ramp', 'Sol Ring');
  await saveCategory(page);
  await openCard(page, 'FastMana', 'Sol Ring');
  const reset = page.getByRole('button', { name: 'Use imported category', exact: true });
  await expect(reset).toBeVisible();
  await reset.click();
  await expect(group(page, 'Ramp').locator('[data-cutlab-card-open="Sol Ring"]')).toHaveCount(1);
  const state = JSON.parse(await page.locator('form[data-cache-key="cut-lab"] input[name="CutLabStateJson"]').inputValue());
  expect(state).not.toHaveProperty('categoryOverrides');
});
