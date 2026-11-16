(function () {
  'use strict';

  const allowedOriginsStorageKey = 'deckflowAllowedOrigins';
  const defaultAllowedOrigins = [
    'https://deckflow.gg',
    'https://www.deckflow.gg',
    'http://localhost',
    'https://localhost',
    'http://localhost:5173',
    'http://127.0.0.1',
    'https://127.0.0.1'
  ];
  const pageBridgeEnabled = document.body?.dataset.deckflowExtensionBridge === 'enabled'
    || document.documentElement?.dataset.deckflowExtensionBridge === 'enabled';

  if (!pageBridgeEnabled) {
    return;
  }

  window.addEventListener('message', async (event) => {
    if (event.source !== window) {
      return;
    }

    const message = event.data;
    if (!message || message.source !== 'deckflow-web' || typeof message.type !== 'string') {
      return;
    }

    if (message.type === 'deckflow-extension-ping') {
      const access = await getOriginAccess();
      postBridgeMessage('deckflow-extension-ping-response', message.requestId, access);
      return;
    }

    if (message.type !== 'deckflow-moxfield-import' || typeof message.deckUrl !== 'string') {
      return;
    }

    try {
      const access = await getOriginAccess();
      if (!access.allowed) {
        throw createOriginNotAllowedError(access.optionsUrl);
      }

      const deckId = getDeckIdFromUrl(message.deckUrl);
      if (!deckId) {
        throw new Error('The submitted URL is not a valid public Moxfield deck link.');
      }

      const response = await chrome.runtime.sendMessage({ type: 'moxfield-fetch-deck', deckId });
      if (!response?.ok || !response.payload) {
        throw new Error(response?.error ?? 'Unable to fetch deck data from Moxfield.');
      }

      const exportBundle = globalThis.DeckFlowMoxfieldParser.buildExportBundle(response.payload, deckId);
      postBridgeMessage('deckflow-moxfield-import-response', message.requestId, {
        ok: true,
        deckText: exportBundle.moxfieldText,
        cardCount: exportBundle.cardCount,
        deckName: exportBundle.deckName,
        sourceUrl: response.sourceUrl ?? null
      });
    } catch (error) {
      postBridgeMessage('deckflow-moxfield-import-response', message.requestId, {
        ok: false,
        error: error instanceof Error ? error.message : String(error),
        optionsUrl: error && typeof error === 'object' && 'optionsUrl' in error ? error.optionsUrl : undefined
      });
    }
  });

  async function getOriginAccess() {
    const allowedOrigins = await loadAllowedOrigins();
    return {
      allowed: allowedOrigins.includes(window.location.origin),
      optionsUrl: chrome.runtime.getURL(`options.html?origin=${encodeURIComponent(window.location.origin)}`)
    };
  }

  async function loadAllowedOrigins() {
    const result = await chrome.storage.sync.get(allowedOriginsStorageKey);
    const configured = Array.isArray(result[allowedOriginsStorageKey]) ? result[allowedOriginsStorageKey] : [];
    const normalized = configured
      .map(normalizeOrigin)
      .filter(Boolean);

    if (normalized.length > 0) {
      return Array.from(new Set(normalized));
    }

    return defaultAllowedOrigins;
  }

  function createOriginNotAllowedError(optionsUrl) {
    const error = new Error(`This DeckFlow origin is not allowed in the extension yet. Add ${window.location.origin} in the extension options and try again.`);
    error.optionsUrl = optionsUrl;
    return error;
  }

  function postBridgeMessage(type, requestId, payload) {
    window.postMessage({
      source: 'deckflow-extension',
      type,
      requestId,
      ...payload
    }, window.location.origin);
  }

  function normalizeOrigin(value) {
    try {
      return new URL(value).origin;
    } catch {
      return '';
    }
  }

  function getDeckIdFromUrl(url) {
    try {
      const parsed = new URL(url);
      if (!/moxfield\.com$/i.test(parsed.hostname)) {
        return null;
      }

      const match = parsed.pathname.match(/^\/decks\/([^/]+)/i);
      return match ? match[1] : null;
    } catch {
      return null;
    }
  }

})();
