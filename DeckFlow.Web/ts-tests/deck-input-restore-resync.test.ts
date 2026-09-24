import { beforeAll, describe, expect, it, vi } from 'vitest';

document.body.innerHTML = `
  <form id="restore-form">
    <select name="DeckInputSource" data-df-select>
      <option value="PublicUrl" selected>Public URL</option>
      <option value="PasteText">Paste deck text</option>
    </select>
    <div data-sync-panel="manabase-deck-url"></div>
    <div data-sync-panel="manabase-deck-text" class="hidden"></div>
    <input name="DeckUrl" value="" />
    <textarea name="DeckText"></textarea>
  </form>
`;

window.sessionStorage.setItem('deckflow.last-deck', JSON.stringify({
  inputSource: 'PasteText',
  deckUrl: 'https://moxfield.com/decks/example',
  deckText: '1 Sol Ring',
}));

const restoredChanges = vi.fn();
document.addEventListener('change', restoredChanges);

await import('../wwwroot/ts/df-select');
await import('../wwwroot/ts/busy-indicator');
await import('../wwwroot/ts/moxfield-extension-bridge');
await import('../wwwroot/ts/deck-sync');
await import('../wwwroot/ts/deck-input-store');

beforeAll(() => {
  document.dispatchEvent(new Event('DOMContentLoaded'));
});

const inputSource = (): HTMLSelectElement =>
  document.querySelector('select[name="DeckInputSource"]') as HTMLSelectElement;

describe('deck input restore resync', () => {
  it('resyncs restored input mode once without persisting partial state', () => {
    expect(inputSource().value).toBe('PasteText');
    expect(document.querySelector('[data-sync-panel="manabase-deck-url"]')?.classList.contains('hidden')).toBe(true);
    expect(document.querySelector('[data-sync-panel="manabase-deck-text"]')?.classList.contains('hidden')).toBe(false);
    expect(document.querySelector<HTMLButtonElement>('.df-select__trigger')?.textContent).toContain('Paste deck text');
    expect(restoredChanges).toHaveBeenCalledTimes(1);
    expect(window.sessionStorage.getItem('deckflow.last-deck')).toContain('https://moxfield.com/decks/example');
  });

  it('does not dispatch change when the restored source is unchanged', async () => {
    document.body.innerHTML = `
      <form><select name="InputSource"><option value="PublicUrl">URL</option><option value="PasteText" selected>Text</option></select><input name="DeckUrl" /><textarea name="DeckText"></textarea></form>
    `;
    window.sessionStorage.setItem('deckflow.last-deck', JSON.stringify({ inputSource: 'PasteText', deckUrl: '', deckText: '1 Sol Ring' }));
    vi.resetModules();
    await import('../wwwroot/ts/deck-input-store');
    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(restoredChanges).toHaveBeenCalledTimes(1);
  });

  it('restores the DeckConvert InputSource fallback', async () => {
    document.body.innerHTML = `
      <form><select name="InputSource"><option value="PublicUrl" selected>URL</option><option value="PasteText">Text</option></select><input name="DeckUrl" /><textarea name="DeckText"></textarea></form>
    `;
    window.sessionStorage.setItem('deckflow.last-deck', JSON.stringify({ inputSource: 'PasteText', deckUrl: '', deckText: '1 Sol Ring' }));
    vi.resetModules();
    await import('../wwwroot/ts/deck-input-store');

    const fallback = document.querySelector<HTMLSelectElement>('select[name="InputSource"]') as HTMLSelectElement;
    document.dispatchEvent(new Event('DOMContentLoaded'));

    expect(fallback.value).toBe('PasteText');
    expect(restoredChanges).toHaveBeenCalledTimes(2);
  });

  it('resyncs an input source restored from a data-cache-key form', async () => {
    document.body.innerHTML = `
      <form data-cache-key="restore-resync"><select name="DeckInputSource"><option value="PublicUrl" selected>URL</option><option value="PasteText">Text</option></select><div data-sync-panel="manabase-deck-url"></div><div data-sync-panel="manabase-deck-text" class="hidden"></div><input name="DeckUrl" /><textarea name="DeckText"></textarea></form>
    `;
    window.sessionStorage.setItem('decksync-form-state-restore-resync', JSON.stringify({
      DeckInputSource: ['PasteText'],
      DeckUrl: ['https://moxfield.com/decks/example'],
      DeckText: ['1 Sol Ring'],
    }));
    vi.resetModules();
    await import('../wwwroot/ts/deck-sync');

    expect(document.querySelector<HTMLSelectElement>('select[name="DeckInputSource"]')?.value).toBe('PasteText');
    expect(document.querySelector('[data-sync-panel="manabase-deck-url"]')?.classList.contains('hidden')).toBe(true);
    expect(document.querySelector('[data-sync-panel="manabase-deck-text"]')?.classList.contains('hidden')).toBe(false);
    expect(restoredChanges).toHaveBeenCalledTimes(3);
  });
});
