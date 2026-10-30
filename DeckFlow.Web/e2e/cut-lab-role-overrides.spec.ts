import { expect, test, type Page } from '@playwright/test';
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

const fillImportForm = async (page: Page): Promise<void> => {
  await page.locator('#cut-lab-input-source').selectOption('PasteText');
  await page.locator('#cut-lab-deck-text').fill(oversizedPool);
  await clickManabasePillRadio(page, 'Bracket', '4');
  await clickManabasePillRadio(page, 'PlayExperience', 'Focused');
};

const importPool = async (page: Page): Promise<void> => {
  await page.goto(`${baseUrl}/cut-lab`);
  await expect(page.locator('h1')).toHaveText('Cut Lab');
  await fillImportForm(page);
  await page.getByRole('button', { name: 'Import pool' }).click();

  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"]')).toHaveAttribute('data-cut-lab-commander', 'true');
};

const clearScenarioStorage = async (page: Page): Promise<void> => {
  if (page.isClosed()) {
    return;
  }

  try {
    if (!page.url().startsWith(baseUrl)) {
      await page.goto('/cut-lab');
    }

    await page.evaluate(() => {
      for (const key of Object.keys(window.localStorage)) {
        if (key === 'deckflow.cutlab.scenario-index' || key.startsWith('deckflow.cutlab.scenario.')) {
          window.localStorage.removeItem(key);
        }
      }
    });
  } catch {
    // Best-effort cleanup only.
  }
};

test.beforeEach(async ({ page }) => {
  heldLock = await acquireAdminLockForTest(page);
  await setToolEnabled(page, 'Cut Lab', true);
});

test.afterEach(async ({ page }) => {
  try {
    await clearScenarioStorage(page);
  } finally {
    await releaseAdminLockForTest(heldLock);
    heldLock = null;
  }
});


test('edits card roles, preserves them through locks, and resets inferred roles', async ({ page }) => {
  await importPool(page);
  const solRing = page.locator('tr[data-cut-lab-card="Sol Ring"]');
  const roleCell = solRing.locator('[data-label="Type / role"]');
  const mainState = page.locator('form[data-cache-key="cut-lab"] input[name="CutLabStateJson"]');
  const editor = page.locator('#cutlab-card-modal');
  const openSolRing = async (): Promise<void> => { await solRing.locator('[data-cutlab-card-open="Sol Ring"]').click(); };
  const pickRole = async (label: string): Promise<void> => {
    await editor.locator('.df-select__trigger').click();
    await editor.getByRole('listbox').getByRole('option', { name: label, exact: true }).click();
  };

  await expect(roleCell).toContainText('Ramp');
  await expect(page.locator('tr[data-cut-lab-card="Zur the Enchanter"] [data-cut-lab-role-edit-card]')).toHaveCount(0);
  await openSolRing();
  await expect(editor.locator('[name="CardName"]')).toHaveValue('Sol Ring');
  await expect(editor.locator('[data-cut-lab-role-add]')).not.toHaveAttribute('open');
  await editor.getByText('Add a role', { exact: true }).click();
  await pickRole('Card draw');
  const marker = 'role-edit-navigation-marker';
  await page.evaluate(value => { (window as any).__roleEditMarker = value; }, marker);
  const scrollY = await page.evaluate(() => window.scrollY);
  await editor.getByRole('button', { name: 'Add', exact: true }).click();
  await expect(editor.locator('[data-cut-lab-role-status]')).toHaveText('Roles updated.');
  expect(await page.evaluate(() => (window as any).__roleEditMarker)).toBe(marker);
  expect(await page.evaluate(() => window.scrollY)).toBe(scrollY);
  await expect(editor).toHaveAttribute('open', '');
  await expect(editor.locator('[data-cut-lab-role-chips]')).toContainText('Card draw');
  await expect(editor.locator('[data-cut-lab-role-block]')).not.toHaveAttribute('aria-busy', 'true');
  await expect(page.locator('details[data-cut-lab-group-role="draw"] [data-cut-lab-chip-card="Sol Ring"]')).toHaveCount(1);
  await editor.getByRole('button', { name: 'Close', exact: true }).click();
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(roleCell).toContainText('Card draw');
  await expect(solRing.locator('.cutlab-role-edited-chip')).toHaveText('Edited');
  await expect(mainState).toHaveValue(/"roleOverrides":\[\{"name":"Sol Ring"/);

  await openSolRing();
  const removeRamp = editor.getByRole('button', { name: 'Remove Ramp role', exact: true });
  const box = await removeRamp.boundingBox();
  expect(box!.width).toBeGreaterThanOrEqual(44);
  expect(box!.height).toBeGreaterThanOrEqual(44);
  let removeRequests = 0;
  page.on('request', request => { if (request.url().endsWith('/api/cut-lab/role-override')) removeRequests++; });
  await removeRamp.evaluate(button => { (button as HTMLButtonElement).click(); (button as HTMLButtonElement).click(); });
  await expect(editor.locator('[data-cut-lab-role-status]')).toHaveText('Roles updated.');
  expect(removeRequests).toBe(1);
  await expect(editor.locator('[data-cut-lab-role-chips]')).not.toContainText('Ramp');
  await expect(roleCell).not.toContainText('Ramp');
  await editor.getByRole('button', { name: 'Close', exact: true }).click();
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(roleCell).not.toContainText('Ramp');
  await expect(roleCell).toContainText('Card draw');

  const lock = page.locator('[data-cut-lab-lock-card="Arcane Signet"]');
  await lock.setChecked(!(await lock.isChecked()));
  await expect(mainState).toHaveValue(/"roleOverrides":\[\{"name":"Sol Ring"/);
  for (const input of await page.locator('input[name="CutLabStateJson"]').all()) {
    await expect(input).toHaveValue(/"roleOverrides":\[\{"name":"Sol Ring"/);
  }

  await openSolRing();
  await editor.getByRole('button', { name: 'Reset card roles', exact: true }).click();
  await expect(editor.locator('[data-cut-lab-role-chips]')).toContainText('Ramp');
  await expect(editor.getByRole('button', { name: 'Reset card roles', exact: true })).toBeHidden();
  await expect(solRing.locator('.cutlab-role-edited-chip')).toHaveCount(0);
  await editor.getByRole('button', { name: 'Close', exact: true }).click();
  await expandCutLabSection(page, 'cut-lab-section-lock-pool');
  await expect(solRing.locator('.cutlab-role-edited-chip')).toHaveCount(0);
  await expect(roleCell).toContainText('Ramp');
  await expect(roleCell).not.toContainText('Card draw');
  await page.locator('tr[data-cut-lab-card="Plains"] [data-cutlab-card-open="Plains"]').click();
  await expect(editor.locator('[data-cut-lab-role-status]')).toBeEmpty();
  await expect(editor.locator('[data-cut-lab-role-chips]')).toContainText('Lands');
  await expect(editor.locator('[data-cut-lab-role-chips] button')).toHaveCount(0);
  await expect(editor.locator('[data-cut-lab-role-add]')).not.toHaveAttribute('open');
  await editor.getByText('Add a role', { exact: true }).click();
  await editor.locator('.df-select__trigger').click();
  const listbox = editor.getByRole('listbox');
  await expect(listbox).toBeVisible();
  await expect(listbox.getByRole('option', { name: 'Ramp', exact: true })).toHaveCount(0);
  await expect(listbox.getByRole('option', { name: 'Lands', exact: true })).toHaveCount(0);
  await editor.locator('.df-select__trigger').press('Escape');
  await editor.getByRole('button', { name: 'Close', exact: true }).click();
  // Exercise the same view-only opener used by Cuts made without changing the pool.
  await page.evaluate(() => {
    const opener = document.createElement('button');
    opener.dataset.cutlabCardOpen = 'Plains';
    opener.dataset.cutlabCardViewOnly = 'true';
    opener.id = 'role-test-view-only';
    opener.textContent = 'View Plains';
    document.body.append(opener);
  });
  await page.locator('#role-test-view-only').click();
  await expect(editor.locator('[data-cut-lab-role-chips]')).toContainText('Lands');
  await expect(editor.locator('[data-cut-lab-role-override-form]')).toBeHidden();
  await expect(editor.locator('[data-cut-lab-role-chips] button')).toHaveCount(0);
  await editor.getByRole('button', { name: 'Close', exact: true }).click();
  await page.locator('tr[data-cut-lab-card="Zur the Enchanter"] [data-cutlab-card-open]').click();
  await expect(editor.locator('[data-cut-lab-role-block]')).toBeVisible();
  await expect(editor.locator('[data-cut-lab-role-override-form]')).toBeHidden();
  await expect(editor.locator('[data-cut-lab-role-chips] li')).not.toHaveCount(0);
  await expect(editor.locator('[data-cut-lab-role-chips] button')).toHaveCount(0);
  await expect(page.locator('#cut-lab-role-editor')).toHaveCount(0);
  await expect(page.locator('[data-cut-lab-role-edit-card]')).toHaveCount(0);
});
