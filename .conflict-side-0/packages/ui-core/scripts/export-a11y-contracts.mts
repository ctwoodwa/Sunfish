/**
 * Plan 4 Task 1.6 — export Sunfish a11y contracts from Storybook stories to JSON.
 *
 * Glob all `src/**\/*.stories.ts`, dynamic-import each via tsx (which we run under),
 * extract `default.parameters.a11y.sunfish`, and emit `dist/a11y-contracts.json`
 * keyed by component tag name. The Blazor a11y bridge (Sunfish.UIAdapters.Blazor.A11y)
 * reads this JSON via ContractReader.Load to enforce the same contract on the .NET side
 * that the JS-side Storybook test-runner enforces.
 *
 * Usage: `pnpm --filter @sunfish/ui-core build:contracts`
 */

import { readdirSync, statSync, mkdirSync, writeFileSync } from 'node:fs';
import { join, relative, basename, dirname } from 'node:path';
import { pathToFileURL } from 'node:url';

const SRC_ROOT = join(import.meta.dirname, '..', 'src');
const DIST_ROOT = join(import.meta.dirname, '..', 'dist');
const OUTPUT_FILE = join(DIST_ROOT, 'a11y-contracts.json');

interface StoryMetaParameters {
  a11y?: {
    sunfish?: Record<string, unknown>;
  };
}

interface StoryMeta {
  component?: string;
  title?: string;
  parameters?: StoryMetaParameters;
}

function findStoryFiles(root: string, acc: string[] = []): string[] {
  for (const entry of readdirSync(root)) {
    const full = join(root, entry);
    const stat = statSync(full);
    if (stat.isDirectory()) {
      findStoryFiles(full, acc);
    } else if (entry.endsWith('.stories.ts') || entry.endsWith('.stories.tsx')) {
      acc.push(full);
    }
  }
  return acc;
}

async function main() {
  const stories = findStoryFiles(SRC_ROOT);
  if (stories.length === 0) {
    console.warn(`No .stories.ts files found under ${SRC_ROOT}.`);
    process.exit(0);
  }

  const contracts: Record<string, Record<string, unknown>> = {};
  const skipped: { file: string; reason: string }[] = [];

  for (const file of stories) {
    const fileUrl = pathToFileURL(file).href;
    let mod: { default?: StoryMeta };
    try {
      mod = await import(fileUrl);
    } catch (err) {
      skipped.push({
        file: relative(SRC_ROOT, file),
        reason: `import failed: ${(err as Error).message}`,
      });
      continue;
    }

    const meta = mod.default;
    if (!meta) {
      skipped.push({ file: relative(SRC_ROOT, file), reason: 'no default export' });
      continue;
    }

    const sunfish = meta.parameters?.a11y?.sunfish;
    if (!sunfish) {
      skipped.push({ file: relative(SRC_ROOT, file), reason: 'no parameters.a11y.sunfish' });
      continue;
    }

    const tag = meta.component ?? deriveTagFromFilename(file);
    if (!tag) {
      skipped.push({ file: relative(SRC_ROOT, file), reason: 'no component tag derivable' });
      continue;
    }

    if (contracts[tag]) {
      skipped.push({ file: relative(SRC_ROOT, file), reason: `duplicate tag '${tag}' (already from another story)` });
      continue;
    }

    contracts[tag] = sunfish;
  }

  mkdirSync(DIST_ROOT, { recursive: true });
  writeFileSync(OUTPUT_FILE, JSON.stringify(contracts, null, 2) + '\n', 'utf8');

  console.log(`Wrote ${Object.keys(contracts).length} contracts to ${relative(process.cwd(), OUTPUT_FILE)}`);
  if (skipped.length > 0) {
    console.warn(`Skipped ${skipped.length} story file(s):`);
    for (const s of skipped) console.warn(`  - ${s.file}: ${s.reason}`);
  }
}

function deriveTagFromFilename(file: string): string | null {
  // sunfish-button.stories.ts → sunfish-button
  const base = basename(file).replace(/\.stories\.tsx?$/, '');
  return base.includes('-') ? base : null;
}

await main();
