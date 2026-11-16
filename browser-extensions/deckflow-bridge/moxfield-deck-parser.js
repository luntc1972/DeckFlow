(function () {
  'use strict';

  function buildExportBundle(deckPayload, fallbackDeckId) {
    const deckName = (typeof deckPayload.name === 'string' && deckPayload.name.trim()) || `moxfield-${fallbackDeckId}`;
    const authorTags = deckPayload.authorTags || {};
    const cards = [];

    appendZone(cards, deckPayload, 'commanders', 'Commander', authorTags);
    appendZone(cards, deckPayload, 'mainboard', null, authorTags);
    appendZone(cards, deckPayload, 'sideboard', 'Sideboard', authorTags);
    appendZone(cards, deckPayload, 'maybeboard', 'Maybeboard', authorTags);

    if (cards.length === 0) {
      throw new Error('No commander, mainboard, sideboard, or maybeboard cards were found.');
    }

    return {
      deckName,
      cardCount: cards.length,
      moxfieldText: cards.map(formatMoxfieldLine).join('\n') + '\n'
    };
  }

  function getZone(payload, zoneName) {
    const cards = payload?.boards?.[zoneName]?.cards;
    return cards && typeof cards === 'object' ? cards : payload?.[zoneName];
  }

  function hasDeckZones(payload) {
    return ['commanders', 'mainboard', 'sideboard', 'maybeboard'].some((zoneName) => {
      const zone = getZone(payload, zoneName);
      return zone && typeof zone === 'object';
    });
  }

  function appendZone(target, payload, zoneName, boardTag, authorTags) {
    const zone = getZone(payload, zoneName);
    if (!zone || typeof zone !== 'object') {
      return;
    }

    const isV3 = zone === payload?.boards?.[zoneName]?.cards;

    for (const [fallbackName, rawEntry] of Object.entries(zone)) {
      if (!rawEntry || typeof rawEntry !== 'object') {
        continue;
      }

      if (isV3 && (typeof rawEntry.card?.name !== 'string' || !rawEntry.card.name.trim())) {
        continue;
      }

      const card = rawEntry.card && typeof rawEntry.card === 'object' ? rawEntry.card : rawEntry;
      const cardName = card.name || fallbackName;
      const tags = normalizeTags(authorTags[cardName], boardTag);
      target.push({
        quantity: Number(rawEntry.quantity) || 1,
        name: cardName,
        setCode: typeof card.set === 'string' ? card.set.toLowerCase() : '',
        collectorNumber: typeof card.cn === 'string' ? card.cn : '',
        tags
      });
    }
  }

  function normalizeTags(rawTags, boardTag) {
    const tags = [];

    if (boardTag) {
      tags.push(boardTag);
    }

    if (Array.isArray(rawTags)) {
      for (const rawTag of rawTags) {
        if (typeof rawTag !== 'string') {
          continue;
        }

        const trimmed = rawTag.trim();
        if (trimmed) {
          tags.push(trimmed);
        }
      }
    }

    return Array.from(new Set(tags.map((tag) => tag.trim()).filter(Boolean)));
  }

  function formatMoxfieldLine(entry) {
    const printing = formatPrinting(entry);
    const tags = entry.tags.length > 0
      ? ` ${entry.tags.map((tag) => `#${tag.replace(/\s+/g, '')}`).join(' ')}`
      : '';
    return `${entry.quantity} ${entry.name}${printing}${tags}`;
  }

  function formatPrinting(entry) {
    if (!entry.setCode || !entry.collectorNumber) {
      return '';
    }

    return ` (${entry.setCode}) ${entry.collectorNumber}`;
  }
  const api = { buildExportBundle, getZone, hasDeckZones };
  globalThis.DeckFlowMoxfieldParser = api;
  if (typeof module !== 'undefined') {
    module.exports = api;
  }
})();
