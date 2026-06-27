import { expect, test } from '@playwright/test';

test('bootstrap hides a stuck busy overlay when a result marker is already present', async ({ page }) => {
  // This bug is a bootstrap-time race, so force the page into the bad pre-bootstrap state:
  // the shared overlay is visible and a rendered-result marker already exists before
  // deck-sync.ts handles DOMContentLoaded. Suppress the existing pageshow hide path so this
  // test proves the new bootstrap guard specifically instead of passing via the later fallback.
  await page.addInitScript(() => {
    const originalAddEventListener = window.addEventListener.bind(window);
    window.addEventListener = ((type: string, listener: EventListenerOrEventListenerObject | null, options?: boolean | AddEventListenerOptions) => {
      if (type === 'pageshow') {
        return;
      }

      originalAddEventListener(type, listener as EventListenerOrEventListenerObject, options);
    }) as typeof window.addEventListener;

    document.addEventListener('DOMContentLoaded', () => {
      const overlay = document.getElementById('busy-indicator');
      if (overlay) {
        overlay.classList.remove('hidden');
      }

      const resultMarker = document.createElement('section');
      resultMarker.className = 'result-panel';
      resultMarker.setAttribute('data-scroll-on-load', '');
      document.body.appendChild(resultMarker);
    });
  });

  const response = await page.goto('/manabase');
  expect(response?.ok()).toBeTruthy();

  const overlay = page.locator('#busy-indicator');
  await expect(overlay).toHaveClass(/hidden/);
  await expect(overlay).toBeHidden();
});
