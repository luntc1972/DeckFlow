((): void => {
  'use strict';

  const normalize = (value: string): string => value.trim().toLowerCase();
  const hasToken = (tokens: string, filter: string): boolean =>
    filter.length === 0 || tokens.split('|').some(token => normalize(token) === filter);

  const attachFilters = (): void => {
    const cards = Array.from(document.querySelectorAll<HTMLElement>('[data-kb-entry]'));
    if (cards.length === 0) {
      return;
    }

    // Card dataset values never change after render — normalize them once instead of per filter pass.
    const indexed = cards.map(card => ({
      card,
      search: normalize(card.dataset.search ?? ''),
      source: card.dataset.source ?? '',
      archetype: card.dataset.archetype ?? '',
      bracket: card.dataset.bracket ?? '',
      cardCategory: card.dataset.cardCategory ?? '',
    }));

    const search = document.querySelector<HTMLInputElement>('[data-kb-search]');
    const filters = Array.from(document.querySelectorAll<HTMLSelectElement>('[data-kb-filter]'));
    const noMatch = document.querySelector<HTMLElement>('[data-kb-empty-filter]');
    const count = document.querySelector<HTMLElement>('[data-kb-match-count]');
    let timer: number | null = null;

    const apply = (): void => {
      const query = normalize(search?.value ?? '');
      const selected = new Map(filters.map(filter => [filter.dataset.kbFilter ?? '', normalize(filter.value)]));
      let shown = 0;

      indexed.forEach(entry => {
        const visible = entry.search.includes(query)
          && hasToken(entry.source, selected.get('source') ?? '')
          && hasToken(entry.archetype, selected.get('archetype') ?? '')
          && hasToken(entry.bracket, selected.get('bracket') ?? '')
          && hasToken(entry.cardCategory, selected.get('card-category') ?? '');

        entry.card.style.display = visible ? '' : 'none';
        if (visible) {
          shown += 1;
        }
      });

      if (noMatch) {
        noMatch.hidden = shown > 0;
      }
      if (count) {
        count.textContent = `${shown} ${shown === 1 ? 'entry' : 'entries'} shown`;
      }
    };

    const schedule = (): void => {
      if (timer !== null) {
        window.clearTimeout(timer);
      }
      timer = window.setTimeout(apply, 200);
    };

    search?.addEventListener('input', schedule);
    search?.addEventListener('keydown', event => {
      if (event.key === 'Escape' && search.value.length > 0) {
        search.value = '';
        apply();
      }
    });
    filters.forEach(filter => filter.addEventListener('change', apply));
    document.querySelector<HTMLButtonElement>('[data-kb-clear-filters]')?.addEventListener('click', () => {
      if (search) {
        search.value = '';
      }
      filters.forEach(filter => {
        filter.value = '';
        window.DeckFlow?.refreshDfSelect?.(filter);
      });
      apply();
    });
    apply();
  };

  const attachCopyButtons = (): void => {
    document.querySelectorAll<HTMLButtonElement>('button[data-copy-target]').forEach(button => {
      button.addEventListener('click', async () => {
        const target = document.getElementById(button.dataset.copyTarget ?? '') as HTMLTextAreaElement | null;
        const original = button.dataset.copyOriginalText ?? button.textContent?.trim() ?? 'Copy for ChatGPT';
        button.dataset.copyOriginalText = original;

        try {
          if (!target) {
            throw new Error('Copy target not found.');
          }

          await navigator.clipboard.writeText(target.value);
          button.textContent = 'Copied';
          button.classList.add('is-copied');
        } catch {
          button.textContent = 'Copy failed';
          button.classList.add('is-copy-failed');
        }

        window.setTimeout(() => {
          button.textContent = original;
          button.classList.remove('is-copied', 'is-copy-failed');
        }, 1500);
      });
    });
  };

  document.addEventListener('DOMContentLoaded', () => {
    attachFilters();
    attachCopyButtons();
  });
})();
