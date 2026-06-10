// Pure, DOM-free filter logic for the Admin Content KB entries table (KBUX-01).
// Exposed as a global (module:"none" forbids export); browser loads this before
// content-kb-admin.js, and Vitest imports it for the globalThis side effect.
interface KbEntryFilterApi {
  rowMatches(searchText: string, query: string): boolean;
  formatCount(matched: number, total: number): string;
  emptyRowHidden(matched: number, total: number): boolean;
}

(function (root: { DeckFlowKbFilter?: KbEntryFilterApi }): void {
  root.DeckFlowKbFilter = {
    rowMatches(searchText: string, query: string): boolean {
      return query === '' || searchText.includes(query);
    },
    formatCount(matched: number, total: number): string {
      return `${matched} of ${total} entries shown`;
    },
    emptyRowHidden(matched: number, total: number): boolean {
      return matched !== 0 || total === 0;
    },
  };
})(globalThis as { DeckFlowKbFilter?: KbEntryFilterApi });
