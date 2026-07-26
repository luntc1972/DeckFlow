import { afterEach, describe, expect, it, vi } from 'vitest';

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
  window.localStorage.clear();
  window.sessionStorage.clear();
});

describe('deck history hidden field persistence', () => {
  it('does not restore stale HistoryJson while still hydrating other fields', async () => {
    window.sessionStorage.setItem(
      'decksync-form-state-deck-history',
      JSON.stringify({
        HistoryJson: ['{"versions":[{"id":1}]}'],
        DeckUrl: ['https://www.moxfield.com/decks/stale'],
      }));
    window.sessionStorage.setItem(
      'decksync-form-state-deck-history:savedAt',
      Date.now().toString());

    document.body.innerHTML = `
      <form data-cache-key="deck-history">
        <input type="hidden" name="HistoryJson" value='{"versions":[{"id":1},{"id":2}]}' />
        <input name="DeckUrl" value="https://www.moxfield.com/decks/new" />
      </form>
    `;

    vi.resetModules();
    await import('../wwwroot/ts/deck-sync');

    expect(document.querySelector<HTMLInputElement>('input[name="HistoryJson"]')?.value)
      .toBe('{"versions":[{"id":1},{"id":2}]}');
    expect(document.querySelector<HTMLInputElement>('input[name="DeckUrl"]')?.value)
      .toBe('https://www.moxfield.com/decks/stale');
  });
});
