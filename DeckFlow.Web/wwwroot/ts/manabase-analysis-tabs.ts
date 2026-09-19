((): void => {
  'use strict';

  const desktopQuery = window.matchMedia('(min-width: 1024px)');

  const initialize = (): void => {
    const switchers = document.querySelectorAll<HTMLElement>('[data-manabase-analysis-switcher]');
    switchers.forEach((switcher): void => {
      const tabs = Array.from(switcher.querySelectorAll<HTMLButtonElement>('[role="tab"]'));
      const panels = tabs
        .map((tab): HTMLElement | null => document.getElementById(tab.getAttribute('aria-controls') ?? ''));

      if (tabs.length < 2 || panels.some((panel): boolean => panel === null)) {
        panels.forEach((panel): void => {
          if (panel !== null) {
            panel.hidden = false;
          }
        });
        return;
      }

      const indicator = switcher.querySelector<HTMLElement>('.manabase-analysis-tab-indicator');
      const updateIndicator = (tab: HTMLButtonElement): void => {
        if (indicator === null) {
          return;
        }

        indicator.style.transform = `translateX(${tab.offsetLeft}px)`;
        indicator.style.width = `${tab.offsetWidth}px`;
      };

      const selectTab = (selectedIndex: number, focus: boolean): void => {
        if (!desktopQuery.matches) {
          panels.forEach((panel): void => {
            if (panel !== null) {
              panel.hidden = false;
            }
          });
          return;
        }

        tabs.forEach((tab, index): void => {
          const selected = index === selectedIndex;
          tab.setAttribute('aria-selected', selected ? 'true' : 'false');
          tab.tabIndex = selected ? 0 : -1;
          const panel = panels[index];
          if (panel !== null) {
            panel.hidden = !selected;
          }
        });

        const selectedTab = tabs[selectedIndex];
        updateIndicator(selectedTab);
        if (focus) {
          selectedTab.focus();
        }
      };

      tabs.forEach((tab, index): void => {
        tab.addEventListener('click', (): void => selectTab(index, false));
        tab.addEventListener('keydown', (event: KeyboardEvent): void => {
          if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') {
            return;
          }

          event.preventDefault();
          const nextIndex = event.key === 'ArrowRight'
            ? (index + 1) % tabs.length
            : (index - 1 + tabs.length) % tabs.length;
          selectTab(nextIndex, true);
        });
      });

      const selectTabForHash = (hash: string): void => {
        const target = document.getElementById(hash.slice(1));
        const targetIndex = panels.findIndex((panel): boolean => panel === target || (panel !== null && target !== null && panel.contains(target)));
        if (targetIndex >= 0) {
          selectTab(targetIndex, false);
        }
      };

      document.querySelectorAll<HTMLAnchorElement>('a[href^="#"]').forEach((link): void => {
        link.addEventListener('click', (): void => selectTabForHash(link.hash));
      });
      window.addEventListener('hashchange', (): void => selectTabForHash(window.location.hash));

      const updateForBreakpoint = (): void => {
        if (!desktopQuery.matches) {
          panels.forEach((panel): void => {
            if (panel !== null) {
              panel.hidden = false;
            }
          });
          return;
        }

        const selectedIndex = Math.max(0, tabs.findIndex((tab): boolean => tab.getAttribute('aria-selected') === 'true'));
        selectTab(selectedIndex, false);
      };

      desktopQuery.addEventListener('change', updateForBreakpoint);
      window.addEventListener('resize', updateForBreakpoint);
      updateForBreakpoint();
      if (window.location.hash) {
        selectTabForHash(window.location.hash);
      }
    });
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialize, { once: true });
    return;
  }

  initialize();
})();
