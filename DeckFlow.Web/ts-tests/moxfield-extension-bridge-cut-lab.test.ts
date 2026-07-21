import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

declare global {
  interface Window {
    DeckInputSource?: {
      PasteText: string;
      PublicUrl: string;
    };
  }
}

const importedDeckText = '#Commander\n1 Tymna the Weaver\n#Sideboard\n1 Mana Crypt';

document.body.innerHTML = `
  <div id="busy-indicator" class="busy-indicator"></div>
  <form data-cache-key="cut-lab">
    <select id="cut-lab-input-source" name="DeckInputSource">
      <option value="PasteText">PasteText</option>
      <option value="PublicUrl" selected>PublicUrl</option>
    </select>
    <input id="cut-lab-deck-url" name="DeckUrl" value="https://www.moxfield.com/decks/abc123" />
    <textarea id="cut-lab-deck-text" name="DeckText"></textarea>
    <input type="checkbox" name="IncludeSideboard" value="true" />
  </form>
`;

window.DeckInputSource = {
  PasteText: 'PasteText',
  PublicUrl: 'PublicUrl',
};

await import('../wwwroot/ts/busy-indicator');
await import('../wwwroot/ts/moxfield-extension-bridge');
await import('../wwwroot/ts/deck-sync');

beforeAll(() => {
  document.dispatchEvent(new Event('DOMContentLoaded'));
});

beforeEach(() => {
  Object.defineProperty(window.navigator, 'userAgentData', {
    configurable: true,
    value: undefined,
  });
  document.querySelector<HTMLSelectElement>('#cut-lab-input-source')!.value = 'PublicUrl';
  document.querySelector<HTMLInputElement>('#cut-lab-deck-url')!.value = 'https://www.moxfield.com/decks/abc123';
  document.querySelector<HTMLTextAreaElement>('#cut-lab-deck-text')!.value = '';
  document.querySelector<HTMLInputElement>('input[name="IncludeSideboard"]')!.checked = false;
  delete document.querySelector<HTMLFormElement>('form[data-cache-key="cut-lab"]')!.dataset.extensionBridgeBypass;
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('moxfield extension bridge cut-lab submit flow', () => {
  it('imports via the extension submit path, switches to paste mode, and enables sideboard', async () => {
    const form = document.querySelector<HTMLFormElement>('form[data-cache-key="cut-lab"]')!;
    const requestSubmitSpy = vi
      .spyOn(HTMLFormElement.prototype, 'requestSubmit')
      .mockImplementation(() => {});
    const postMessageSpy = vi.spyOn(window, 'postMessage');
    const sideboardCheckbox = form.querySelector<HTMLInputElement>('input[name="IncludeSideboard"]')!;

    window.addEventListener('message', (event: MessageEvent) => {
      const message = event.data as { type?: string; requestId?: string };
      if (!message?.requestId) {
        return;
      }

      if (message.type === 'deckflow-extension-ping') {
        window.dispatchEvent(new MessageEvent('message', {
          source: window,
          data: {
            source: 'deckflow-extension',
            type: 'deckflow-extension-ping-response',
            requestId: message.requestId,
            allowed: true,
          },
        }));
      }

      if (message.type === 'deckflow-moxfield-import') {
        window.dispatchEvent(new MessageEvent('message', {
          source: window,
          data: {
            source: 'deckflow-extension',
            type: 'deckflow-moxfield-import-response',
            requestId: message.requestId,
            ok: true,
            deckText: importedDeckText,
          },
        }));
      }
    }, { once: false });

    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    await vi.waitFor(() => {
      expect(requestSubmitSpy).toHaveBeenCalledTimes(1);
      expect(document.querySelector<HTMLTextAreaElement>('#cut-lab-deck-text')!.value).toBe(importedDeckText);
      expect(document.querySelector<HTMLSelectElement>('#cut-lab-input-source')!.value).toBe('PasteText');
      expect(document.querySelector<HTMLInputElement>('#cut-lab-deck-url')!.value).toBe('');
      expect(sideboardCheckbox.checked).toBe(true);
    });

    expect(postMessageSpy).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'deckflow-extension-ping' }),
      window.location.origin,
    );
    expect(postMessageSpy).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'deckflow-moxfield-import' }),
      window.location.origin,
    );
  });

  it('skips the extension handshake when cut lab is already in paste mode', () => {
    const form = document.querySelector<HTMLFormElement>('form[data-cache-key="cut-lab"]')!;
    const postMessageSpy = vi.spyOn(window, 'postMessage');
    document.querySelector<HTMLSelectElement>('#cut-lab-input-source')!.value = 'PasteText';
    document.querySelector<HTMLTextAreaElement>('#cut-lab-deck-text')!.value = 'existing deck text';
    document.querySelector<HTMLInputElement>('#cut-lab-deck-url')!.value = 'https://www.moxfield.com/decks/abc123';

    const dispatchResult = form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    expect(dispatchResult).toBe(true);
    expect(postMessageSpy).not.toHaveBeenCalled();
    expect(document.querySelector<HTMLTextAreaElement>('#cut-lab-deck-text')!.value).toBe('existing deck text');
    expect(document.querySelector<HTMLInputElement>('#cut-lab-deck-url')!.value).toBe('https://www.moxfield.com/decks/abc123');
  });
});
