#!/usr/bin/env node
/**
 * Sunfish locale completeness check (spec §4 + Plan 5 CI gate).
 *
 * For every component bundle (a `.resx` file with locale-tagged satellites alongside),
 * compute the per-locale completeness percentage = (keys present in locale) / (keys in
 * source). Compare against the `completenessFloor` declared in `i18n/locales.json` for
 * each locale. Exit 1 if any locale is below its floor (when --fail-on-incomplete).
 *
 * Bundle layout assumed (matches Plan 2 Task 3.1 scaffolder):
 *
 *   packages/<name>/Resources/SharedResource.resx          ← source (locales.json source)
 *   packages/<name>/Resources/SharedResource.<tag>.resx    ← locale satellite
 *
 * Usage:
 *   node tooling/locale-completeness-check/check.mjs                   # report only
 *   node tooling/locale-completeness-check/check.mjs --json            # JSON output
 *   node tooling/locale-completeness-check/check.mjs --fail-on-incomplete  # CI mode
 *   node tooling/locale-completeness-check/check.mjs --root <dir>      # custom scan root
 */

import { readFileSync } from 'node:fs';
import { readdir } from 'node:fs/promises';
import { join, basename, dirname, relative, sep } from 'node:path';
import process from 'node:process';

const args = process.argv.slice(2);
const json = args.includes('--json');
const failOnIncomplete = args.includes('--fail-on-incomplete');
const rootIdx = args.indexOf('--root');
const REPO_ROOT = rootIdx >= 0 && args[rootIdx + 1]
  ? args[rootIdx + 1]
  : join(import.meta.dirname, '..', '..');

const LOCALES_JSON = join(REPO_ROOT, 'i18n', 'locales.json');

/**
 * Parse a .resx file and extract the set of `<data name="...">` keys whose `<value>`
 * is non-empty. Empty `<value>` entries are treated as "not localized in this bundle"
 * — the spec's completeness metric is "number of keys translated", not "number of keys
 * declared". Translators leaving an entry empty pulls the locale percentage down.
 */
function readResxKeys(content) {
  const keys = new Set();
  // Lightweight extraction: <data name="X" ...> ... <value>Y</value>...</data>
  // Skip typed entries (binary blobs / ResXFileRef / numeric / etc.) — those aren't
  // translator-facing strings and don't count toward the localization metric.
  const dataPattern = /<data\s+([^>]*?)>([\s\S]*?)<\/data>/g;
  for (const match of content.matchAll(dataPattern)) {
    const attrs = match[1];
    const body = match[2];
    const nameMatch = attrs.match(/\bname\s*=\s*"([^"]+)"/);
    if (!nameMatch) continue;
    const typeMatch = attrs.match(/\btype\s*=\s*"([^"]+)"/);
    if (typeMatch && typeMatch[1].length > 0) continue;  // Typed entry; skip.

    const valueMatch = body.match(/<value>([\s\S]*?)<\/value>/);
    const value = valueMatch ? valueMatch[1].trim() : '';
    if (value.length > 0) keys.add(nameMatch[1]);
  }
  return keys;
}

/**
 * Recurse a directory and yield every `.resx` file path. Excludes node_modules + build
 * outputs by directory-name match (cheap exclude — same set as the css-logical audit).
 */
async function* walk(dir) {
  let entries;
  try { entries = await readdir(dir, { withFileTypes: true }); }
  catch { return; }
  for (const entry of entries) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      const rel = relative(REPO_ROOT, full).split(sep).join('/');
      if (rel.includes('node_modules') ||
          rel.endsWith('/bin') || rel.includes('/bin/') ||
          rel.endsWith('/obj') || rel.includes('/obj/') ||
          rel.endsWith('/dist') || rel.includes('/dist/') ||
          rel.includes('storybook-static') ||
          rel.includes('.claude') ||
          rel.includes('.wolf') ||
          rel.includes('/_site/')) continue;
      yield* walk(full);
    } else if (entry.isFile() && entry.name.endsWith('.resx')) {
      yield full;
    }
  }
}

/**
 * Group .resx files into bundles. A bundle is identified by (directory, baseName);
 * its members are the source file (no tag) plus zero or more locale-tagged satellites.
 *
 *   Resources/SharedResource.resx        → bundle ("Resources/SharedResource", source)
 *   Resources/SharedResource.ar-SA.resx  → bundle ("Resources/SharedResource", "ar-SA")
 */
function bundleKey(filePath) {
  const fileName = basename(filePath);
  const dir = dirname(filePath);
  // Strip .resx, then split off optional .<tag> suffix.
  const noExt = fileName.replace(/\.resx$/, '');
  const tagMatch = noExt.match(/^(.+?)\.([a-zA-Z]{2,3}(?:-[A-Za-z0-9]+)*)$/);
  if (tagMatch) {
    return { bundle: join(dir, tagMatch[1]), tag: tagMatch[2] };
  }
  return { bundle: join(dir, noExt), tag: null };
}

async function main() {
  let localesConfig;
  try {
    localesConfig = JSON.parse(readFileSync(LOCALES_JSON, 'utf8'));
  } catch (err) {
    console.error(`! Cannot read ${LOCALES_JSON}: ${err.message}`);
    process.exit(2);
  }

  const sourceTag = localesConfig.source ?? 'en-US';
  const targetLocales = localesConfig.locales.filter(l => l.tag !== sourceTag);

  // Collect files into bundles.
  const bundles = new Map(); // bundleKey → { source: filePath|null, tags: Map<tag, filePath> }
  for await (const file of walk(REPO_ROOT)) {
    const { bundle, tag } = bundleKey(file);
    if (!bundles.has(bundle)) bundles.set(bundle, { source: null, tags: new Map() });
    const entry = bundles.get(bundle);
    if (tag === null) entry.source = file;
    else entry.tags.set(tag, file);
  }

  const findings = [];
  for (const [bundle, entry] of bundles) {
    if (entry.source === null) continue;  // Locale-only file with no source; skip silently.

    const sourceContent = readFileSync(entry.source, 'utf8');
    const sourceKeys = readResxKeys(sourceContent);
    const sourceCount = sourceKeys.size;
    if (sourceCount === 0) continue;

    for (const locale of targetLocales) {
      const localeFile = entry.tags.get(locale.tag);
      let presentCount = 0;
      if (localeFile) {
        const localeKeys = readResxKeys(readFileSync(localeFile, 'utf8'));
        for (const k of sourceKeys) if (localeKeys.has(k)) presentCount++;
      }
      const pct = sourceCount === 0 ? 100 : (presentCount * 100) / sourceCount;
      const floor = locale.completenessFloor ?? 0;
      const passing = pct >= floor;
      findings.push({
        bundle: relative(REPO_ROOT, bundle).split(sep).join('/'),
        locale: locale.tag,
        tier: locale.status,
        floor,
        present: presentCount,
        total: sourceCount,
        percentage: Math.round(pct * 100) / 100,
        passing,
        coordinator: locale.coordinator,
      });
    }
  }

  if (json) {
    process.stdout.write(JSON.stringify({ findings, bundles: bundles.size }, null, 2) + '\n');
  } else {
    if (bundles.size === 0) {
      console.log('Locale completeness check: no .resx bundles found.');
      console.log('Nothing to check yet (Plan 2 cascade scaffolds Resources/ folders per package).');
    } else {
      const fails = findings.filter(f => !f.passing);
      console.log(`Locale completeness: ${bundles.size} bundle(s); ${findings.length} (bundle x locale) cell(s); ${fails.length} below floor.\n`);
      for (const f of findings.sort((a, b) => a.percentage - b.percentage)) {
        const marker = f.passing ? ' ' : '!!';
        console.log(`${marker} ${f.bundle.padEnd(60)} ${f.locale.padEnd(8)} ${f.tier.padEnd(10)} ${f.percentage.toString().padStart(6)}% (${f.present}/${f.total})  floor=${f.floor}%`);
      }
    }
  }

  if (failOnIncomplete) {
    const fails = findings.filter(f => !f.passing);
    if (fails.length > 0) {
      process.exit(1);
    }
  }
}

await main();
