import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, rmSync } from 'node:fs';
import { resolve } from 'node:path';

export default function cleanupContentKbE2eFixture(): void {
  const contentBase = process.env.DECKFLOW_E2E_CONTENT_BASE;
  if (contentBase && existsSync(contentBase)) {
    removeFixtureRows(contentBase);
    rmSync(contentBase, { recursive: true, force: true });
  }
}

function removeFixtureRows(contentBase: string): void {
  const seedPath = resolve(contentBase, 'content-kb', 'seed', 'index-seed.json');
  const entries = JSON.parse(readFileSync(seedPath, 'utf8')) as Array<{ naturalKeyValue: string }>;
  const values = entries.map((entry) => `'${entry.naturalKeyValue}'`).join(', ');
  const dbPath = resolve(__dirname, '..', '..', '..', 'artifacts', 'content-site-index.db');
  execFileSync('sqlite3', ['-cmd', '.timeout 8000', dbPath, `DELETE FROM content_site_index WHERE natural_key_value IN (${values});`], {
    encoding: 'utf8',
  });
}
