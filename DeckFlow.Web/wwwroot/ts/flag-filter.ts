// Pure, DOM-free filter logic for the Admin Flags prefix filter.
// Exposed as a global (module:"none" forbids export); browser loads this before
// admin-flags.js, and Vitest imports it for the globalThis side effect.
interface FlagFilterApi {
  keyMatches(key: string, query: string): boolean;
  statusMatches(enabled: boolean, status: string): boolean;
  formatCount(matched: number, total: number): string;
  emptyRowHidden(matched: number, total: number): boolean;
}

(function (root: { DeckFlowFlagFilter?: FlagFilterApi }): void {
  root.DeckFlowFlagFilter = {
    keyMatches(key: string, query: string): boolean {
      const normalizedKey = key.toLowerCase();
      const normalizedQuery = query.toLowerCase();
      return normalizedQuery === '' || normalizedKey.startsWith(normalizedQuery);
    },
    statusMatches(enabled: boolean, status: string): boolean {
      if (status === 'on') {
        return enabled;
      }

      if (status === 'off') {
        return !enabled;
      }

      return true;
    },
    formatCount(matched: number, total: number): string {
      return `${matched} of ${total} flags shown`;
    },
    emptyRowHidden(matched: number, total: number): boolean {
      return matched !== 0 || total === 0;
    },
  };
})(globalThis as { DeckFlowFlagFilter?: FlagFilterApi });
