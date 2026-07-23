import { expect, test } from '@playwright/test';
import { acquireAdminLockForTest, releaseAdminLockForTest } from './support/admin-lock';
import { setToolEnabled } from './support/admin-tools';

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

test('individual card pills lock cards and Lock All stays readable in Commander Table dark OS mode', async ({ page, baseURL }) => {
  const heldLock = await acquireAdminLockForTest(page);
  try {
    await setToolEnabled(page, 'Cut Lab', true);
    await page.emulateMedia({ colorScheme: 'dark' });
    await page.context().addCookies([{
      name: 'deckflow-theme',
      value: 'site-commander-table.css',
      url: baseURL ?? 'http://localhost:5173',
    }]);
    await page.goto('/cut-lab');

    const b4Label = page.locator('label.manabase-pill').filter({ hasText: 'B4 Optimized' });
    await b4Label.click();
    await expect(page.locator('input[name="Bracket"][value="4"]')).toBeChecked();

    await page.locator('#cut-lab-input-source').selectOption('PasteText');
    await page.locator('#cut-lab-deck-text').fill(oversizedPool);
    await page.locator('#cut-lab-primary-plan').fill('Protect the control shell.');
    await page.getByRole('button', { name: 'Import pool' }).click();
    await expect(page.getByRole('heading', { name: 'Lock your pool' })).toBeVisible({ timeout: 30_000 });

    const group = page.locator('details.cutlab-role-group').filter({ hasText: 'Lands' });
    await group.locator('summary').click();
    const lockAll = group.locator('[data-cut-lab-lock-role="lands"]');
    const commandTowerPill = group.locator('button[data-cut-lab-chip-card="Command Tower"]');
    const commandTowerCheckbox = page.locator(
      'tr[data-cut-lab-card="Command Tower"] input[data-cut-lab-lock-card]',
    );

    await commandTowerPill.click();
    await expect(commandTowerCheckbox).toBeChecked();
    await expect(commandTowerPill).toHaveAttribute('aria-pressed', 'true');

    const before = await lockAll.evaluate(element => {
      const style = getComputedStyle(element);
      const spanStyle = getComputedStyle(element.querySelector('span')!);
      return { color: style.color, spanColor: spanStyle.color, backgroundColor: style.backgroundColor, pointerEvents: style.pointerEvents };
    });
    expect(before).toEqual({
      color: 'rgb(26, 21, 16)',
      spanColor: 'rgb(26, 21, 16)',
      backgroundColor: 'rgb(250, 248, 243)',
      pointerEvents: 'auto',
    });

    await lockAll.click();
    const after = await lockAll.evaluate(element => {
      const style = getComputedStyle(element);
      const spanStyle = getComputedStyle(element.querySelector('span')!);
      return { color: style.color, spanColor: spanStyle.color, backgroundColor: style.backgroundColor, pointerEvents: style.pointerEvents };
    });
    expect(after).toEqual({
      color: 'rgb(255, 255, 255)',
      spanColor: 'rgb(255, 255, 255)',
      backgroundColor: 'rgb(45, 122, 79)',
      pointerEvents: 'auto',
    });
    await expect(lockAll).toHaveAttribute('aria-pressed', 'true');
  } finally {
    await releaseAdminLockForTest(heldLock);
  }
});
