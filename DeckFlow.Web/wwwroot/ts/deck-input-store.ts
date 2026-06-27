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
    [key: string]: unknown;
  };

  type DeckFlowWindow = Window & {
    DeckFlow?: DeckFlowNamespace;
  };

  const getDeckTextBytes = (value: string): number => new TextEncoder().encode(value).length;

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

  const restoreSplitFields = (
    stored: LastDeckState,
    inputSelect: HTMLSelectElement | null,
    urlInput: HTMLInputElement | null,
    textArea: HTMLTextAreaElement | null
  ): void => {
    const urlValue = urlInput?.value.trim() ?? '';
    const textValue = textArea?.value.trim() ?? '';

    // Why: POST-rendered values and user-entered text must win; only fill a fresh empty form.
    if (urlValue !== '' || textValue !== '') {
      return;
    }

    if (urlInput) {
      urlInput.value = stored.deckUrl;
    }

    if (textArea) {
      textArea.value = stored.deckText;
    }

    if (inputSelect) {
      inputSelect.value = stored.inputSource;
    }
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
      restoreSplitFields(stored, inputSelect, urlInput, textArea);
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
      combinedInput.value = stored.inputSource === 'PublicUrl'
        ? (stored.deckUrl || stored.deckText)
        : (stored.deckText || stored.deckUrl);
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

  document.addEventListener('DOMContentLoaded', bootstrapDeckInputStore);
  if (document.readyState !== 'loading') {
    bootstrapDeckInputStore();
  }
})();
