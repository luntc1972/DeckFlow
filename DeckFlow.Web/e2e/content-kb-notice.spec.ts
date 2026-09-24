import { expect, test } from '@playwright/test';
import { publishFirstUnpublishedEntry, setEntryVisibility } from './support/content-kb-publish';
import { configureAdminPageForTest, withKnowledgeBaseEnabled } from './support/knowledge-base-flag';

withKnowledgeBaseEnabled();

test('creator notice links a published detail page to a persisted removal request', async ({ page }) => {
  const entry = await publishFirstUnpublishedEntry(page);
  const detailPath = `/content-kb/${entry.id}`;
  const message = `E2E creator removal request ${Date.now()} ${test.info().project.name}`;

  try {
    const indexResponse = await page.goto('/content-kb');
    expect(indexResponse?.ok()).toBeTruthy();
    await expect(page.locator('.creator-content-notice')).toBeVisible();

    const detailResponse = await page.goto(detailPath);
    expect(detailResponse?.ok()).toBeTruthy();
    const notice = page.locator('.creator-content-notice');
    await expect(notice).toBeVisible();
    await expect(notice.locator('p').first()).toHaveText(/^This page summarizes a video by .+\.$/);

    await Promise.all([
      page.waitForURL(/\/feedback\?type=creator-removal&source=/),
      notice.getByRole('link', { name: 'Request removal' }).click(),
    ]);
    await expect(page.locator('select[name="Type"]')).toHaveValue('3');
    await expect(page.locator('input[name="SourcePageUrl"]')).toHaveValue(detailPath);

    await page.locator('textarea[name="Message"]').fill(message);
    await configureAdminPageForTest(page, true);
    await page.getByRole('button', { name: 'Send Feedback' }).click();
    await expect(page.locator('.feedback-banner--success')).toBeVisible();

    const inboxResponse = await page.goto('/Admin/Feedback');
    expect(inboxResponse?.ok()).toBeTruthy();
    const row = page.locator('tr').filter({ hasText: message });
    await expect(row).toHaveCount(1);
    await expect(row).toContainText('Creator removal request');
    await row.getByRole('link', { name: 'View' }).click();
    await expect(page.locator('dl')).toContainText('Creator removal request');
    await expect(page.locator('dl')).toContainText(detailPath);
  } finally {
    await setEntryVisibility(page, entry.id, false);
  }
});
