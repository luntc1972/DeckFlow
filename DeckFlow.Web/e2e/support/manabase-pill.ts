import { expect, type Locator, type Page } from '@playwright/test';

type ManabasePillRoot = Locator | Page;

export async function clickManabasePillRadio(root: ManabasePillRoot, name: string, value: string): Promise<void> {
  const radioSelector = `input[name="${name}"][value="${value}"]`;

  // The hidden radio's flexbox static position lands beneath the label text, so click the enclosing label instead.
  await root.locator(`label.manabase-pill:has(${radioSelector})`).click();
  await expect(root.locator(radioSelector)).toBeChecked();
}
