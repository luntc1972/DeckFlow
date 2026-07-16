import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

beforeAll(async () => {
  document.body.innerHTML = `
    <div id="busy-indicator" class="busy-indicator"></div>
    <form data-cache-key="prompt-deck-comparison">
      <select name="DeckAInputSource">
        <option value="PasteText">PasteText</option>
        <option value="PublicUrl" selected>PublicUrl</option>
      </select>
      <input name="DeckAUrl" value="https://www.moxfield.com/decks/abc123" />
      <textarea name="DeckAText"></textarea>
      <select name="DeckBInputSource">
        <option value="PasteText">PasteText</option>
        <option value="PublicUrl" selected>PublicUrl</option>
      </select>
      <input name="DeckBUrl" value="https://www.moxfield.com/decks/def456" />
      <textarea name="DeckBText"></textarea>
    </form>
    <form data-cache-key="prompt-cedh-meta-gap">
      <select name="DeckInputSource">
        <option value="PasteText">PasteText</option>
        <option value="PublicUrl" selected>PublicUrl</option>
      </select>
      <input name="DeckUrl" value="https://www.moxfield.com/decks/ghi789" />
      <textarea name="DeckText"></textarea>
    </form>
  `;
  await import('../wwwroot/ts/busy-indicator');
  await import('../wwwroot/ts/moxfield-extension-bridge');
  await import('../wwwroot/ts/deck-sync');
});

beforeEach(() => {
  Object.defineProperty(window.navigator, 'userAgentData', {
    configurable: true,
    value: { mobile: true },
  });
  document.querySelector<HTMLSelectElement>('form[data-cache-key="prompt-deck-comparison"] select[name="DeckAInputSource"]')!.value = 'PublicUrl';
  document.querySelector<HTMLInputElement>('form[data-cache-key="prompt-deck-comparison"] input[name="DeckAUrl"]')!.value = 'https://www.moxfield.com/decks/abc123';
  document.querySelector<HTMLTextAreaElement>('form[data-cache-key="prompt-deck-comparison"] textarea[name="DeckAText"]')!.value = '';
  document.querySelector<HTMLSelectElement>('form[data-cache-key="prompt-deck-comparison"] select[name="DeckBInputSource"]')!.value = 'PublicUrl';
  document.querySelector<HTMLInputElement>('form[data-cache-key="prompt-deck-comparison"] input[name="DeckBUrl"]')!.value = 'https://www.moxfield.com/decks/def456';
  document.querySelector<HTMLTextAreaElement>('form[data-cache-key="prompt-deck-comparison"] textarea[name="DeckBText"]')!.value = '';
  document.querySelector<HTMLSelectElement>('form[data-cache-key="prompt-cedh-meta-gap"] select[name="DeckInputSource"]')!.value = 'PublicUrl';
  document.querySelector<HTMLInputElement>('form[data-cache-key="prompt-cedh-meta-gap"] input[name="DeckUrl"]')!.value = 'https://www.moxfield.com/decks/ghi789';
  document.querySelector<HTMLTextAreaElement>('form[data-cache-key="prompt-cedh-meta-gap"] textarea[name="DeckText"]')!.value = '';
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('moxfield extension bridge split-field prompt forms', () => {
  it('does not throw for prompt deck comparison split fields', () => {
    const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
    const form = document.querySelector<HTMLFormElement>('form[data-cache-key="prompt-deck-comparison"]')!;

    expect(() => {
      form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    }).not.toThrow();
    expect(alertSpy).toHaveBeenCalledTimes(1);
  });

  it('does not throw for prompt cEDH meta-gap split fields', () => {
    const alertSpy = vi.spyOn(window, 'alert').mockImplementation(() => {});
    const form = document.querySelector<HTMLFormElement>('form[data-cache-key="prompt-cedh-meta-gap"]')!;

    expect(() => {
      form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    }).not.toThrow();
    expect(alertSpy).toHaveBeenCalledTimes(1);
  });
});
