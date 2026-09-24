import { expect, test } from '@playwright/test';
import { withToolEnabled } from './support/admin-tools';

withToolEnabled('Bracket Check');

test('submits bracket paste text when hidden URL contains a scheme-less value', async ({ page }) => {
  await page.goto('/bracket');
  await page.locator('input[name="DeckUrl"]').fill('moxfield.com/decks/abc');
  await page.locator('#bracket-input-source').selectOption('PasteText');
  await page.locator('#bracket-deck-text').fill('1 Sol Ring');

  await page.route('**/bracket', route => route.request().method() === 'POST'
    ? route.fulfill({ status: 200, contentType: 'text/html', body: '<main>Submitted</main>' })
    : route.continue());
  const request = page.waitForRequest(candidate => candidate.url().includes('/bracket') && candidate.method() === 'POST');
  await page.getByRole('button', { name: 'Classify deck' }).click();

  expect((await request).postData()).toContain('DeckText=1+Sol+Ring');
});
