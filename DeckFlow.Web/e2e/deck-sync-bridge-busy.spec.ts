import { expect, test } from '@playwright/test';

test('convert deck clears the busy overlay when bridge import aborts because the extension is not installed', async ({ context, page }) => {
  // Exercises the "extension not installed" bridge path in headless Chromium:
  // no DeckFlow Bridge extension is present, so submit interception alerts,
  // opens the install page, and must leave #busy-indicator hidden afterward.
  const dialogPromise = page.waitForEvent('dialog');
  const popupPromise = context.waitForEvent('page');

  const response = await page.goto('/convert');
  expect(response?.ok()).toBeTruthy();

  await page.locator('select[name="InputSource"]').selectOption('PublicUrl');
  await page.locator('input[name="DeckUrl"]').fill('https://www.moxfield.com/decks/test-deckflow-bridge');
  await page.getByRole('button', { name: 'Convert' }).click();

  const dialog = await dialogPromise;
  await dialog.dismiss();

  const popup = await popupPromise;
  await popup.close();

  await expect.poll(async () => {
    return await page.locator('#busy-indicator').evaluate(node => node.classList.contains('hidden'));
  }).toBe(true);
});
