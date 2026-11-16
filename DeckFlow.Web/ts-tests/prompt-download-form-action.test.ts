import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 400 });
let originalFormActionDescriptor: PropertyDescriptor | undefined;

beforeAll(async () => {
  originalFormActionDescriptor = Object.getOwnPropertyDescriptor(HTMLButtonElement.prototype, 'formAction');
  // Why: jsdom lacks formAction; browsers return the document URL when the attribute is absent.
  Object.defineProperty(HTMLButtonElement.prototype, 'formAction', {
    configurable: true,
    get(this: HTMLButtonElement): string {
      const attr = this.getAttribute('formaction');
      return attr ? new URL(attr, document.baseURI).href : document.URL;
    }
  });
  window.history.replaceState({}, '', '/manabase');
  document.body.innerHTML = `
    <form action="/manabase/download" method="post">
      <button type="submit" data-prompt-download-submit id="manabase-download">Download analysis</button>
    </form>
    <form action="/deck" method="post">
      <button type="submit" data-prompt-download-submit formaction="/deck/download" id="deck-download">Download deck</button>
    </form>
  `;
  vi.stubGlobal('fetch', fetchMock);
  vi.spyOn(window, 'alert').mockImplementation(() => {});
  await import('../wwwroot/ts/busy-indicator');
  await import('../wwwroot/ts/moxfield-extension-bridge');
  await import('../wwwroot/ts/deck-sync');
  document.dispatchEvent(new Event('DOMContentLoaded'));
});

beforeEach(() => {
  fetchMock.mockClear();
});

afterAll(() => {
  if (originalFormActionDescriptor) {
    Object.defineProperty(HTMLButtonElement.prototype, 'formAction', originalFormActionDescriptor);
  } else {
    Reflect.deleteProperty(HTMLButtonElement.prototype, 'formAction');
  }
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('prompt download form action', () => {
  it('uses the form action when the button has no formaction attribute', () => {
    document.querySelector<HTMLButtonElement>('#manabase-download')!.click();

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/manabase\/download$/),
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('uses the button formaction when explicitly supplied', () => {
    document.querySelector<HTMLButtonElement>('#deck-download')!.click();

    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/deck\/download$/),
      expect.objectContaining({ method: 'POST' })
    );
  });
});
