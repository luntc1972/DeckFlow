import { execFileSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { resolveE2EPort } from './e2e-port';

const fixtureParentDirectory = resolve(__dirname, '..', 'fixtures');
const contentSiteIndexDbPath = resolve(__dirname, '..', '..', '..', 'artifacts', 'content-site-index.db');

type ContentKbFixtureSeedEntry = {
  artifactPath: string;
  naturalKeyType: string;
  naturalKeyValue: string;
  source: string;
  title: string;
  videoUrl: string;
};

export function createContentKbE2eFixture(): string {
  const temporaryFixtureDirectory = join(getTemporaryDirectory(), `deckflow-content-kb-e2e-${resolveE2EPort()}`);
  mkdirSync(temporaryFixtureDirectory, { recursive: true });
  cpSync(fixtureParentDirectory, temporaryFixtureDirectory, { recursive: true });
  seedVisibleEntries(temporaryFixtureDirectory);
  return temporaryFixtureDirectory;
}

function seedVisibleEntries(contentBase: string): void {
  const seedPath = resolve(contentBase, 'content-kb', 'seed', 'index-seed.json');
  const entries = JSON.parse(readFileSync(seedPath, 'utf8')) as ContentKbFixtureSeedEntry[];
  const visibleEntries = entries.filter((entry) => entry.naturalKeyValue.startsWith('e2e-visible-'));
  if (visibleEntries.length < 2) {
    throw new Error(`Content KB E2E fixture seed at ${seedPath} must contain at least two visible entries.`);
  }

  const values = visibleEntries
    .map(
      (entry) => `('${entry.source}', '${entry.title}', '${entry.videoUrl}', '${entry.artifactPath}', ` +
        `'2026-09-27T00:00:00+00:00', '[]', '[]', '[]', '${entry.naturalKeyType}', ` +
        `'${entry.naturalKeyValue}', 1, 'approved', 1)`,
    )
    .join(', ');
  const sql = `CREATE TABLE IF NOT EXISTS content_site_index (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    source TEXT NOT NULL, title TEXT NOT NULL, video_url TEXT NOT NULL, artifact_path TEXT NOT NULL,
    published_utc TEXT NULL, pushed_to_prod_utc TEXT NULL,
    indexed_utc TEXT NOT NULL DEFAULT (datetime('now')), archetype_tags TEXT NOT NULL DEFAULT '[]',
    bracket_tags TEXT NOT NULL DEFAULT '[]', card_category_tags TEXT NOT NULL DEFAULT '[]',
    natural_key_type TEXT NOT NULL, natural_key_value TEXT NOT NULL, is_visible INTEGER NOT NULL DEFAULT 0,
    is_hidden INTEGER NOT NULL DEFAULT 0, is_evergreen INTEGER NOT NULL DEFAULT 0,
    approval_status TEXT NOT NULL DEFAULT 'pending', body_sha256 TEXT NULL, awaiting_confirm_utc TEXT NULL,
    seed_managed INTEGER NULL, UNIQUE (natural_key_type, natural_key_value)
  );
  INSERT INTO content_site_index
    (source, title, video_url, artifact_path, indexed_utc, archetype_tags, bracket_tags, card_category_tags,
     natural_key_type, natural_key_value, is_visible, approval_status, seed_managed)
  VALUES ${values}
  ON CONFLICT(natural_key_type, natural_key_value) DO UPDATE SET
    is_visible = 1, approval_status = 'approved', seed_managed = 1;`;

  execFileSync('sqlite3', ['-cmd', '.timeout 8000', contentSiteIndexDbPath, sql], { encoding: 'utf8' });
}

function getTemporaryDirectory(): string {
  if (!process.env.WSL_DISTRO_NAME || !existsSync('/mnt/c')) {
    return tmpdir();
  }

  const windowsTemporaryDirectory = execFileSync('cmd.exe', ['/d', '/s', '/c', 'echo %TEMP%'], {
    encoding: 'utf8',
  }).trim();
  return execFileSync('wslpath', ['-u', windowsTemporaryDirectory], { encoding: 'utf8' }).trim();
}
