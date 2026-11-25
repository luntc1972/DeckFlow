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

export function createContentKbE2eFixture(seedRows = true): string {
  if (seedRows) {
    const fixtureAvailability = getFixtureAvailability();
    if (fixtureAvailability) {
      throw new Error(fixtureAvailability);
    }
  }
  const temporaryFixtureDirectory = join(getTemporaryDirectory(), `deckflow-content-kb-e2e-${resolveE2EPort()}`);
  mkdirSync(temporaryFixtureDirectory, { recursive: true });
  cpSync(fixtureParentDirectory, temporaryFixtureDirectory, { recursive: true });
  if (seedRows) {
    seedVisibleEntries(temporaryFixtureDirectory);
  }
  return temporaryFixtureDirectory;
}

export function getContentKbE2eFixtureSkipReason(): string | undefined {
  return getFixtureAvailability();
}

function getFixtureAvailability(): string | undefined {
  if (!existsSync(contentSiteIndexDbPath)) {
    return `Content KB E2E specs skipped: artifacts database is missing at ${contentSiteIndexDbPath}.`;
  }

  try {
    execFileSync('sqlite3', ['--version'], { encoding: 'utf8' });
    return undefined;
  } catch {
    return 'Content KB E2E specs skipped: sqlite3 is not available on PATH to seed fixture rows.';
  }
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
    natural_key_type TEXT NOT NULL CHECK (natural_key_type IN ('youtube_channel','podcast_rss')), natural_key_value TEXT NOT NULL, is_visible INTEGER NOT NULL DEFAULT 0,
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

type TemporaryDirectoryEnvironment = Record<string, string | undefined>;
type TemporaryDirectoryExec = (file: string, args: readonly string[]) => string;
type TemporaryDirectoryExists = (path: string) => boolean;
type TemporaryDirectorySleep = (milliseconds: number) => void;

export function resolveTemporaryDirectory(
  env: TemporaryDirectoryEnvironment = process.env,
  exec: TemporaryDirectoryExec = (file, args) => execFileSync(file, args, { encoding: 'utf8' }),
  exists: TemporaryDirectoryExists = existsSync,
  sleep: TemporaryDirectorySleep = (milliseconds) => {
    Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, milliseconds);
  },
): string {
  if (!env.WSL_DISTRO_NAME || !exists('/mnt/c')) {
    return tmpdir();
  }
  const override = env.DECKFLOW_E2E_TEMP_DIR?.trim();
  if (override) {
    return override;
  }

  // Why: WSL interop intermittently fails cmd.exe instantly (UtilAcceptVsock accept4 failed 110),
  // and this runs once at Playwright config load, so one miss aborts the whole e2e run.
  let lastError: unknown = new Error('no attempts made');
  for (let attempt = 0; attempt < 3; attempt += 1) {
    try {
      const windowsTemporaryDirectory = exec('cmd.exe', ['/d', '/s', '/c', 'echo %TEMP%']).trim();
      if (!windowsTemporaryDirectory || windowsTemporaryDirectory.includes('%TEMP%')) {
        throw new Error('cmd.exe returned an empty or unexpanded TEMP path');
      }
      const temporaryDirectory = exec('wslpath', ['-u', windowsTemporaryDirectory]).trim();
      if (temporaryDirectory) {
        return temporaryDirectory;
      }
      throw new Error('wslpath returned an empty temporary path');
    } catch (error) {
      lastError = error;
      if (attempt < 2) {
        sleep(250 * (attempt + 1));
      }
    }
  }
  throw new Error(`Unable to resolve the Windows temp directory; set DECKFLOW_E2E_TEMP_DIR to override it: ${lastError instanceof Error ? lastError.message : String(lastError)}`);
}

function getTemporaryDirectory(): string {
  return resolveTemporaryDirectory();
}
