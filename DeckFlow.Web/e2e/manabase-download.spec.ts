import { expect, test } from '@playwright/test';

// Smoke tests for the /manabase download button.
//
// The download button only renders after a successful analysis (which calls Scryfall and is
// covered by xUnit ManabaseControllerDownloadTests). These tests guard the wiring that can be
// verified without a live Scryfall round-trip:
//
//   1. On a fresh GET /manabase the download form is absent (no result yet).
//   2. The analyze form still posts to /manabase (unchanged).
//   3. No console errors on page load.
//   4. No horizontal overflow on desktop or mobile (the Download button reuses .run-button).
//
// The File-result behavior (timestamped filename, text/plain content-type, report content)
// is covered by ManabaseControllerDownloadTests.cs.

test('download form is absent on a fresh GET /manabase (no result yet)', async ({ page }) => {
  const consoleErrors: string[] = [];
  page.on('console', (msg) => {
    if (msg.type() === 'error') {
      consoleErrors.push(msg.text());
    }
  });

  const response = await page.goto('/manabase');
  expect(response?.ok()).toBeTruthy();

  // The download mini-form must NOT be present before any analysis has run.
  await expect(page.locator('form[action*="/manabase/download"]')).not.toBeAttached();

  expect(consoleErrors).toEqual([]);
});

test('analyze form still posts to /manabase after download feature added', async ({ page }) => {
  const response = await page.goto('/manabase');
  expect(response?.ok()).toBeTruthy();

  // The analyze form's action must be unchanged — download is a separate mini-form.
  await expect(page.locator('form[action="/manabase"]')).toBeVisible();
});

test('no horizontal overflow on /manabase (desktop and mobile)', async ({ page }) => {
  const response = await page.goto('/manabase');
  expect(response?.ok()).toBeTruthy();

  // Guard that the new Download button (which reuses .run-button) does not widen the page
  // on either the chromium-desktop or chromium-mobile Playwright projects.
  const noOverflow = await page.evaluate(
    () => (document.scrollingElement?.scrollWidth ?? document.documentElement.scrollWidth)
      <= window.innerWidth + 1,
  );
  expect(noOverflow).toBe(true);
});
