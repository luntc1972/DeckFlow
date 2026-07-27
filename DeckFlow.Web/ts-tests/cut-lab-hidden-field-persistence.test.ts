import { afterEach, describe, expect, it, vi } from 'vitest';

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  document.body.innerHTML = '';
  window.localStorage.clear();
  window.sessionStorage.clear();
});

describe('cut lab hidden field persistence', () => {
  it('does not restore stale CutLabStateJson while still hydrating other fields', async () => {
    window.sessionStorage.setItem(
      'decksync-form-state-cut-lab',
      JSON.stringify({
        CutLabStateJson: ['{"stale":true}'],
        DeckUrl: ['https://archidekt.com/decks/stale'],
      }));
    window.sessionStorage.setItem(
      'decksync-form-state-cut-lab:savedAt',
      Date.now().toString());

    document.body.innerHTML = `
      <form data-cache-key="cut-lab">
        <input type="hidden" name="CutLabStateJson" value='{"fresh":true}' />
        <input name="DeckUrl" value="https://archidekt.com/decks/fresh" />
      </form>
    `;

    vi.resetModules();
    await import('../wwwroot/ts/deck-sync');

    expect(document.querySelector<HTMLInputElement>('input[name="CutLabStateJson"]')?.value)
      .toBe('{"fresh":true}');
    expect(document.querySelector<HTMLInputElement>('input[name="DeckUrl"]')?.value)
      .toBe('https://archidekt.com/decks/stale');
  });
});
