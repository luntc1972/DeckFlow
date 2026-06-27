((): void => {
  'use strict';

  const LAST_DECK_KEY = 'deckflow.last-deck';
  // Why: cap stored deck text so oversized pastes do not trip sessionStorage quotas.
  const DECK_TEXT_MAX_BYTES = 100_000;

  type LastDeckState = {
    inputSource: string;
    deckUrl: string;
    deckText: string;
  };

  type DeckFlowNamespace = {
    getLastDeck?: () => LastDeckState | null;
    setLastDeck?: (state: LastDeckState) => void;
    clearLastDeck?: () => void;
    [key: string]: unknown;
  };

  type DeckFlowWindow = Window & {
    DeckFlow?: DeckFlowNamespace;
  };

  const getDeckTextBytes = (value: string): number => new TextEncoder().encode(value).length;

  const clearLastDeck = (): void => {
    try {
      window.sessionStorage.removeItem(LAST_DECK_KEY);
    } catch {
      // sessionStorage may be disabled or quota-limited; skip persistence silently.
    }
  };

  const setLastDeck = (state: LastDeckState): void => {
    try {
      const storedState: LastDeckState = {
        inputSource: state.inputSource,
        deckUrl: state.deckUrl,
        deckText: getDeckTextBytes(state.deckText) > DECK_TEXT_MAX_BYTES ? '' : state.deckText,
      };

      window.sessionStorage.setItem(LAST_DECK_KEY, JSON.stringify(storedState));
    } catch {
      // sessionStorage may be disabled or quota-limited; skip persistence silently.
    }
  };

  const getLastDeck = (): LastDeckState | null => {
    try {
      const raw = window.sessionStorage.getItem(LAST_DECK_KEY);
      if (!raw) {
        return null;
      }

      const parsed = JSON.parse(raw) as Partial<LastDeckState> | null;
      if (!parsed || typeof parsed !== 'object' || typeof parsed.inputSource !== 'string') {
        return null;
      }

      return {
        inputSource: parsed.inputSource,
        deckUrl: typeof parsed.deckUrl === 'string' ? parsed.deckUrl : '',
        deckText: typeof parsed.deckText === 'string' ? parsed.deckText : '',
      };
    } catch {
      return null;
    }
  };

  const removeRestoredNotice = (): void => {
    document.querySelector('.deck-restored-notice')?.remove();
  };

  const createRestoredNotice = (clearCurrentFields: () => void): HTMLDivElement => {
    const notice = document.createElement('div');
    notice.className = 'deck-restored-notice';
    notice.setAttribute('role', 'status');

    const message = document.createElement('span');
    message.className = 'deck-restored-notice__text';
    message.textContent = 'Restored your last deck.';

    const clearButton = document.createElement('button');
    clearButton.type = 'button';
    clearButton.className = 'deck-restored-notice__clear';
    clearButton.setAttribute('data-deck-restored-clear', '');
    clearButton.textContent = 'Clear';
    clearButton.addEventListener('click', () => {
      clearCurrentFields();
      clearLastDeck();
      notice.remove();
    });

    notice.append(message, clearButton);
    return notice;
  };

  const insertRestoredNotice = (anchor: HTMLElement | null, clearCurrentFields: () => void): void => {
    if (!anchor || !anchor.parentElement) {
      return;
    }

    removeRestoredNotice();
    anchor.parentElement.insertBefore(createRestoredNotice(clearCurrentFields), anchor);
  };

  const dispatchInputEvent = (element: HTMLInputElement | HTMLTextAreaElement): void => {
    element.dispatchEvent(new Event('input', { bubbles: true }));
  };

  const restoreSplitFields = (
    stored: LastDeckState,
    inputSelect: HTMLSelectElement | null,
    urlInput: HTMLInputElement | null,
    textArea: HTMLTextAreaElement | null
  ): boolean => {
    const urlValue = urlInput?.value.trim() ?? '';
    const textValue = textArea?.value.trim() ?? '';

    // Why: POST-rendered values and user-entered text must win; only fill a fresh empty form.
    if (urlValue !== '' || textValue !== '') {
      return false;
    }

    let restored = false;
    if (urlInput) {
      urlInput.value = stored.deckUrl;
      restored = restored || stored.deckUrl.trim() !== '';
    }

    if (textArea) {
      textArea.value = stored.deckText;
      restored = restored || stored.deckText.trim() !== '';
    }

    if (restored && inputSelect) {
      inputSelect.value = stored.inputSource;
    }

    return restored;
  };

  const attachSplitFields = (): boolean => {
    const inputSelect = document.querySelector<HTMLSelectElement>('select[name="DeckInputSource"]')
      ?? document.querySelector<HTMLSelectElement>('select[name="InputSource"]');
    const urlInput = document.querySelector<HTMLInputElement>('input[name="DeckUrl"]');
    const textArea = document.querySelector<HTMLTextAreaElement>('textarea[name="DeckText"]');

    if (!urlInput && !textArea) {
      return false;
    }

    const stored = getLastDeck();
    if (stored) {
      const restored = restoreSplitFields(stored, inputSelect, urlInput, textArea);
      if (restored) {
        const noticeAnchor = (urlInput?.closest('.field') as HTMLElement | null)
          ?? (textArea?.closest('.field') as HTMLElement | null);
        insertRestoredNotice(noticeAnchor, () => {
          if (urlInput) {
            urlInput.value = '';
            dispatchInputEvent(urlInput);
          }

          if (textArea) {
            textArea.value = '';
            dispatchInputEvent(textArea);
          }
        });
      }
    }

    const persist = (): void => {
      setLastDeck({
        inputSource: inputSelect?.value ?? 'PasteText',
        deckUrl: urlInput?.value ?? '',
        deckText: textArea?.value ?? '',
      });
    };

    urlInput?.addEventListener('input', persist);
    textArea?.addEventListener('input', persist);
    inputSelect?.addEventListener('change', persist);
    return true;
  };

  const attachCombinedField = (): void => {
    const combinedInput = document.querySelector<HTMLTextAreaElement>('textarea[name="DeckSource"]');
    if (!combinedInput) {
      return;
    }

    const stored = getLastDeck();
    if (stored && combinedInput.value.trim() === '') {
      const restoredValue = stored.inputSource === 'PublicUrl'
        ? (stored.deckUrl || stored.deckText)
        : (stored.deckText || stored.deckUrl);
      if (restoredValue.trim() !== '') {
        combinedInput.value = restoredValue;
        const noticeAnchor = combinedInput.closest('.field') as HTMLElement | null;
        insertRestoredNotice(noticeAnchor, () => {
          combinedInput.value = '';
          dispatchInputEvent(combinedInput);
        });
      }
    }

    combinedInput.addEventListener('input', () => {
      const value = combinedInput.value;
      const isUrl = /^https?:\/\//i.test(value.trim());

      setLastDeck(isUrl
        ? { inputSource: 'PublicUrl', deckUrl: value, deckText: '' }
        : { inputSource: 'PasteText', deckUrl: '', deckText: value });
    });
  };

  const bootstrapDeckInputStore = (): void => {
    // Why: this script must load before deck-sync.js so deck-sync sees the restored input mode on init.
    if (attachSplitFields()) {
      return;
    }

    attachCombinedField();
  };

  const win = window as DeckFlowWindow;
  win.DeckFlow = win.DeckFlow ?? {};
  win.DeckFlow.getLastDeck = getLastDeck;
  win.DeckFlow.setLastDeck = setLastDeck;
  win.DeckFlow.clearLastDeck = clearLastDeck;

  document.addEventListener('click', (event) => {
    const target = event.target;
    if (!(target instanceof Element) || !target.closest('[data-clear-cache]')) {
      return;
    }

    // Why: clear the carried deck and drop any restored-notice without relying on
    // deck-sync's navigation to remove it (keeps both clear paths consistent).
    clearLastDeck();
    removeRestoredNotice();
  });

  document.addEventListener('DOMContentLoaded', bootstrapDeckInputStore);
  if (document.readyState !== 'loading') {
    bootstrapDeckInputStore();
  }
})();
