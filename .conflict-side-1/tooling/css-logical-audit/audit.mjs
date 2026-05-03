#!/usr/bin/env node
/**
 * Sunfish CSS logical-properties audit.
 *
 * Scans the configured globs for use of physical CSS properties that should
 * be expressed in CSS logical equivalents (margin-inline-*, padding-inline-*,
 * border-inline-*, etc.) so RTL locales render correctly without per-locale
 * stylesheet hacks. Plan 4B §2 + spec §2.
 *
 * Usage:
 *   node tooling/css-logical-audit/audit.mjs                       # default scan
 *   node tooling/css-logical-audit/audit.mjs --json                # machine-readable
 *   node tooling/css-logical-audit/audit.mjs --fail-on-finding     # exit 1 on any finding (CI mode)
 */

import { readFileSync } from 'node:fs';
import { readdir } from 'node:fs/promises';
import { join, relative, sep } from 'node:path';
import process from 'node:process';

const REPO_ROOT = join(import.meta.dirname, '..', '..');

/**
 * Each rule: a regex against a single line, plus the suggested logical-property
 * replacement. Patterns intentionally narrow — they only flag the actual
 * physical-property usages, not innocuous occurrences (e.g., `left;` in JS,
 * variable names containing `right`).
 */
const RULES = [
  {
    id: 'CSS-LP-001',
    pattern: /\bmargin-(left|right)\s*:/i,
    suggest: 'margin-inline-start / margin-inline-end',
  },
  {
    id: 'CSS-LP-002',
    pattern: /\bpadding-(left|right)\s*:/i,
    suggest: 'padding-inline-start / padding-inline-end',
  },
  {
    id: 'CSS-LP-003',
    pattern: /\bborder-(left|right)(-(width|style|color))?\s*:/i,
    suggest: 'border-inline-start (with -width / -style / -color suffix)',
  },
  {
    id: 'CSS-LP-004',
    pattern: /\bborder-top-(left|right)-radius\s*:/i,
    suggest: 'border-start-start-radius / border-start-end-radius',
  },
  {
    id: 'CSS-LP-005',
    pattern: /\bborder-bottom-(left|right)-radius\s*:/i,
    suggest: 'border-end-start-radius / border-end-end-radius',
  },
  {
    id: 'CSS-LP-006',
    pattern: /\btext-align\s*:\s*(left|right)\b/i,
    suggest: 'text-align: start / text-align: end',
  },
  {
    id: 'CSS-LP-007',
    pattern: /\bfloat\s*:\s*(left|right)\b/i,
    suggest: 'flex / grid layout (no logical equivalent for float)',
  },
  {
    id: 'CSS-LP-008',
    pattern: /(^|[\s;{,])(left|right)\s*:\s*(-?\d+|0|auto|var\()/i,
    suggest: 'inset-inline-start / inset-inline-end',
  },
];

const args = process.argv.slice(2);
const json = args.includes('--json');
const failOnFinding = args.includes('--fail-on-finding');

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
          rel.includes('.wolf')) continue;
      yield* walk(full);
    } else if (entry.isFile()) {
      yield full;
    }
  }
}

function shouldIncludeFile(absolutePath) {
  const rel = relative(REPO_ROOT, absolutePath).split(sep).join('/');
  if (!(rel.startsWith('packages/') || rel.startsWith('apps/'))) return false;
  if (!/\.(css|ts|tsx|razor)$/.test(rel)) return false;
  if (rel.endsWith('.d.ts')) return false;
  if (rel.endsWith('.min.css')) return false;            // third-party / minified vendor CSS
  if (rel.includes('/tests/')) return false;
  if (rel.includes('/node_modules/')) return false;
  if (rel.includes('/bin/') || rel.includes('/obj/') || rel.includes('/dist/')) return false;
  if (rel.includes('/_site/')) return false;             // DocFX site-generation output
  if (rel.includes('/wwwroot/lib/')) return false;       // vendored static assets
  if (rel.includes('/wwwroot/css/vendor/')) return false;
  return true;
}

const findings = [];

for await (const file of walk(REPO_ROOT)) {
  if (!shouldIncludeFile(file)) continue;
  const content = (() => {
    try { return readFileSync(file, 'utf8'); }
    catch { return ''; }
  })();
  if (!content) continue;
  // Skip compiled / vendored CSS output. Hand-authored Sunfish CSS does not start
  // with @charset (compilers emit it; humans rarely do).
  if (/^@charset\b/.test(content)) continue;
  const lines = content.split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    for (const rule of RULES) {
      const match = line.match(rule.pattern);
      if (match) {
        findings.push({
          file: relative(REPO_ROOT, file).split(sep).join('/'),
          line: i + 1,
          column: (match.index ?? 0) + 1,
          rule: rule.id,
          matched: match[0].trim(),
          suggest: rule.suggest,
          context: line.trim(),
        });
      }
    }
  }
}

if (json) {
  process.stdout.write(JSON.stringify({ findings, count: findings.length }, null, 2) + '\n');
} else {
  if (findings.length === 0) {
    console.log('CSS logical-properties audit: clean. 0 physical-property findings.');
  } else {
    console.log(`CSS logical-properties audit: ${findings.length} finding(s).\n`);
    for (const f of findings) {
      console.log(`${f.file}:${f.line}:${f.column}  [${f.rule}]  ${f.matched}`);
      console.log(`    context: ${f.context}`);
      console.log(`    suggest: ${f.suggest}`);
      console.log();
    }
  }
}

if (failOnFinding && findings.length > 0) {
  process.exit(1);
}
