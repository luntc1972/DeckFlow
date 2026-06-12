import { beforeAll, describe, expect, it } from 'vitest';

document.body.innerHTML = `
  <div id="busy-indicator" class="busy-indicator hidden">
    <div id="busy-indicator-title"></div>
    <div id="busy-indicator-message"></div>
    <div id="busy-indicator-progress"></div>
  </div>
`;

await import('../wwwroot/ts/deck-sync');

beforeAll(() => {
  document.dispatchEvent(new Event('DOMContentLoaded'));
});

describe('busy overlay pageshow handling', () => {
  it('hides the busy overlay when the page is shown again', () => {
    const overlay = document.getElementById('busy-indicator');

    expect(overlay).not.toBeNull();

    overlay!.classList.remove('hidden');
    window.dispatchEvent(new Event('pageshow'));

    expect(overlay!.classList.contains('hidden')).toBe(true);
  });
});
