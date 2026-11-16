(function () {
  'use strict';

  const ZONES = [
    { name: 'commanders', tag: 'Commander' },
    { name: 'mainboard', tag: null },
    { name: 'sideboard', tag: 'Sideboard' },
    { name: 'maybeboard', tag: 'Maybeboard' }
  ];

  function isObject(value) {
    return value !== null && typeof value === 'object';
  }

  function buildExportBundle(deckPayload, fallbackDeckId) {
    const deckName = (typeof deckPayload.name === 'string' && deckPayload.name.trim()) || `moxfield-${fallbackDeckId}`;
    const authorTags = deckPayload.authorTags || {};
    const cards = [];

    for (const { name, tag } of ZONES) {
      appendZone(cards, deckPayload, name, tag, authorTags);
    }

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
    if (isObject(cards)) {
      return { cards, isV3: true };
    }

    const zone = payload?.[zoneName];
    return isObject(zone) ? { cards: zone, isV3: false } : null;
  }

  function hasDeckZones(payload) {
    return ZONES.some(({ name }) => getZone(payload, name) !== null);
  }

  function appendZone(target, payload, zoneName, boardTag, authorTags) {
    const zone = getZone(payload, zoneName);
    if (!zone) {
      return;
    }

    const { cards, isV3 } = zone;

    for (const [fallbackName, rawEntry] of Object.entries(cards)) {
      if (!isObject(rawEntry)) {
        continue;
      }

      if (isV3 && (typeof rawEntry.card?.name !== 'string' || !rawEntry.card.name.trim())) {
        continue;
      }

      const card = isObject(rawEntry.card) ? rawEntry.card : rawEntry;
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

    return Array.from(new Set(tags));
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
