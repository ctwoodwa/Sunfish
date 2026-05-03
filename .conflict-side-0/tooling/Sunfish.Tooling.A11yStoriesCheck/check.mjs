#!/usr/bin/env node
/**
 * Sunfish a11y-stories check (SUNFISH_A11Y_001).
 *
 * Walks `packages/ui-core/src/components/` and, for every Lit component file
 * (`sunfish-*.ts`), verifies that a sibling `sunfish-*.stories.ts` exists.
 * Components without a matching stories file cannot participate in the a11y
 * Storybook test-runner pipeline (Plan 4 Task 4.2 / Plan 5 Task 1's
 * `a11y-storybook` job), so missing stories are a CI-visible quality gate.
 *
 * **Why a Node tool, not a Roslyn analyzer?**
 *   The source is TypeScript, not C#. A Roslyn analyzer would either need a
 *   marker .cs file in ui-core (no .cs files exist there) or would fire on
 *   the wrong compilation. An MSBuild-style Node check matches the existing
 *   pattern in `tooling/locale-completeness-check/` and `tooling/css-logical-audit/`.
 *
 * **Scope (v1):** ui-core Lit components only. Razor (.razor) coverage and
 * React adapter coverage are deferred to v2 — documented in the README.
 *
 * Usage:
 *   node check.mjs                            # report only; exit 0 if clean
 *   node check.mjs --json                     # JSON output (CI-friendly)
 *   node check.mjs --fail-on-missing          # exit 1 if any component missing stories
 *   node check.mjs --root <dir>               # custom scan root (defaults to repo root)
 *   node check.mjs --components-dir <relPath> # custom components dir (defaults to packages/ui-core/src/components)
 */

import { readdir, stat } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import process from 'node:process';

const DIAGNOSTIC_ID = 'SUNFISH_A11Y_001';
const COMPONENT_PREFIX = 'sunfish-';
const COMPONENT_EXT = '.ts';
const STORIES_SUFFIX = '.stories.ts';

const args = process.argv.slice(2);
const json = args.includes('--json');
const failOnMissing = args.includes('--fail-on-missing');
const rootIdx = args.indexOf('--root');
const compIdx = args.indexOf('--components-dir');

const REPO_ROOT = rootIdx >= 0 && args[rootIdx + 1]
  ? args[rootIdx + 1]
  : join(import.meta.dirname, '..', '..');

const COMPONENTS_REL = compIdx >= 0 && args[compIdx + 1]
  ? args[compIdx + 1]
  : 'packages/ui-core/src/components';

const COMPONENTS_DIR = join(REPO_ROOT, COMPONENTS_REL);

/**
 * Walk one component directory and return list of component files (sunfish-*.ts
 * excluding *.stories.ts) plus a flag indicating whether each has a sibling
 * stories file.
 */
async function scanComponentDir(dir) {
  let entries;
  try { entries = await readdir(dir, { withFileTypes: true }); }
  catch { return []; }

  const tsFiles = entries
    .filter(e => e.isFile()
      && e.name.startsWith(COMPONENT_PREFIX)
      && e.name.endsWith(COMPONENT_EXT)
      && !e.name.endsWith(STORIES_SUFFIX))
    .map(e => e.name);

  const results = [];
  for (const file of tsFiles) {
    const baseName = file.slice(0, -COMPONENT_EXT.length); // strip .ts
    const storiesFile = `${baseName}${STORIES_SUFFIX}`;
    const hasStories = existsSync(join(dir, storiesFile));
    results.push({
      componentFile: file,
      storiesFile,
      hasStories,
      dir,
    });
  }
  return results;
}

export async function checkComponents(componentsDir = COMPONENTS_DIR, repoRoot = REPO_ROOT) {
  const findings = [];
  let topLevel;
  try { topLevel = await readdir(componentsDir, { withFileTypes: true }); }
  catch (err) {
    return { findings, error: `Cannot read ${componentsDir}: ${err.message}` };
  }

  for (const entry of topLevel) {
    if (!entry.isDirectory()) continue;
    const subResults = await scanComponentDir(join(componentsDir, entry.name));
    for (const r of subResults) {
      findings.push({
        diagnosticId: DIAGNOSTIC_ID,
        component: entry.name,
        componentFile: relative(repoRoot, join(r.dir, r.componentFile)).split(sep).join('/'),
        expectedStoriesFile: relative(repoRoot, join(r.dir, r.storiesFile)).split(sep).join('/'),
        hasStories: r.hasStories,
      });
    }
  }
  return { findings, error: null };
}

async function main() {
  const { findings, error } = await checkComponents();

  if (error) {
    console.error(`! ${error}`);
    process.exit(2);
  }

  const missing = findings.filter(f => !f.hasStories);

  if (json) {
    process.stdout.write(JSON.stringify({
      diagnosticId: DIAGNOSTIC_ID,
      checked: findings.length,
      missing: missing.length,
      findings,
    }, null, 2) + '\n');
  } else {
    if (findings.length === 0) {
      console.log(`${DIAGNOSTIC_ID}: no components found under ${COMPONENTS_REL}.`);
      console.log('Nothing to check yet (Plan 4 cascade scaffolds Lit components).');
    } else {
      console.log(`${DIAGNOSTIC_ID}: scanned ${findings.length} component(s); ${missing.length} missing sibling stories.\n`);
      for (const f of findings) {
        const marker = f.hasStories ? '  ' : '!!';
        console.log(`${marker} ${f.componentFile}  (expects: ${f.expectedStoriesFile})`);
      }
      if (missing.length > 0) {
        console.log(`\n${DIAGNOSTIC_ID} warning: ${missing.length} component(s) missing sibling .stories.ts file.`);
        console.log('Components without stories cannot participate in the a11y Storybook test-runner gate.');
      }
    }
  }

  if (failOnMissing && missing.length > 0) {
    process.exit(1);
  }
}

// Direct-invocation guard so tests can import without triggering main().
const isDirectInvocation = import.meta.url === `file://${process.argv[1].split(sep).join('/')}`
  || import.meta.url.endsWith(process.argv[1].split(sep).join('/'));
if (isDirectInvocation) {
  await main();
}
