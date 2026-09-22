import { afterEach, describe, expect, it, vi } from 'vitest';

afterEach(() => {
  document.body.innerHTML = '';
  vi.resetModules();
});

describe('deck-sync hidden URL type swap', () => {
  it('changes hidden URL inputs to text and restores their type without losing the value', async () => {
    document.body.innerHTML = `
      <select name="DeckInputSource"><option value="PublicUrl">URL</option><option value="PasteText" selected>Text</option></select>
      <div data-sync-panel="deck-modules-deck-url"><input type="url" name="DeckUrl" value="moxfield.com/decks/abc"></div>
      <div data-sync-panel="deck-modules-deck-text"><textarea name="DeckText"></textarea></div>`;

    await import('../wwwroot/ts/deck-sync');

    const source = document.querySelector<HTMLSelectElement>('select[name="DeckInputSource"]')!;
    const url = document.querySelector<HTMLInputElement>('input[name="DeckUrl"]')!;
    expect(url.type).toBe('text');
    expect(url.value).toBe('moxfield.com/decks/abc');

    source.value = 'PublicUrl';
    source.dispatchEvent(new Event('change', { bubbles: true }));

    expect(url.type).toBe('url');
    expect(url.value).toBe('moxfield.com/decks/abc');
  });
});
