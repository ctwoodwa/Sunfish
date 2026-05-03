#!/usr/bin/env node
/**
 * Sunfish CSS logical-properties auto-fixer.
 *
 * Applies the safe mechanical replacements that the audit (audit.mjs) flags.
 * Operates on a list of files passed as args. Conservative — refuses to touch
 * positional `left:`/`right:` (CSS-LP-008) and `inset` shorthand cases since
 * those need component-aware judgment.
 *
 * Usage: node tooling/css-logical-audit/fix.mjs <file...>
 */

import { readFileSync, writeFileSync } from 'node:fs';
import process from 'node:process';

/**
 * Safe transformations: each replaces the LHS pattern wherever it appears in
 * a CSS / Razor source line. Right-hand-side (the value) is preserved.
 *
 * The trailing `:` is anchored so substring matches (like `border-left-color`
 * accidentally matching `border-left`) don't fire — the longer rules sit
 * earlier so they win.
 */
const TRANSFORMS = [
  // Border-radius first (longest property names, prevent partial matches downstream).
  [/\bborder-top-left-radius\s*:/g, 'border-start-start-radius:'],
  [/\bborder-top-right-radius\s*:/g, 'border-start-end-radius:'],
  [/\bborder-bottom-left-radius\s*:/g, 'border-end-start-radius:'],
  [/\bborder-bottom-right-radius\s*:/g, 'border-end-end-radius:'],

  // Border colored / styled / width sub-properties.
  [/\bborder-left-color\s*:/g, 'border-inline-start-color:'],
  [/\bborder-right-color\s*:/g, 'border-inline-end-color:'],
  [/\bborder-left-style\s*:/g, 'border-inline-start-style:'],
  [/\bborder-right-style\s*:/g, 'border-inline-end-style:'],
  [/\bborder-left-width\s*:/g, 'border-inline-start-width:'],
  [/\bborder-right-width\s*:/g, 'border-inline-end-width:'],

  // Border shorthand.
  [/\bborder-left\s*:/g, 'border-inline-start:'],
  [/\bborder-right\s*:/g, 'border-inline-end:'],

  // Margin / padding.
  [/\bmargin-left\s*:/g, 'margin-inline-start:'],
  [/\bmargin-right\s*:/g, 'margin-inline-end:'],
  [/\bpadding-left\s*:/g, 'padding-inline-start:'],
  [/\bpadding-right\s*:/g, 'padding-inline-end:'],

  // text-align — narrow to specific tokens only (left/right; preserve center/justify/inherit/etc).
  [/\btext-align\s*:\s*left\b/gi, 'text-align: start'],
  [/\btext-align\s*:\s*right\b/gi, 'text-align: end'],

  // float — narrow to left/right.
  [/\bfloat\s*:\s*left\b/gi, 'float: inline-start'],
  [/\bfloat\s*:\s*right\b/gi, 'float: inline-end'],
];

const args = process.argv.slice(2);
if (args.length === 0) {
  console.error('Usage: node fix.mjs <file...>');
  process.exit(2);
}

let totalChanges = 0;

for (const file of args) {
  let content;
  try { content = readFileSync(file, 'utf8'); }
  catch (err) {
    console.error(`! ${file}: ${err.message}`);
    continue;
  }

  let updated = content;
  let fileChanges = 0;

  for (const [pattern, replacement] of TRANSFORMS) {
    const before = updated;
    updated = updated.replace(pattern, replacement);
    if (updated !== before) {
      // Count actual replacements by comparing length / re-running match counts.
      const beforeMatches = before.match(pattern);
      if (beforeMatches) fileChanges += beforeMatches.length;
    }
  }

  if (fileChanges > 0) {
    writeFileSync(file, updated, 'utf8');
    console.log(`${file}: ${fileChanges} replacement(s)`);
    totalChanges += fileChanges;
  } else {
    console.log(`${file}: 0 replacements (clean)`);
  }
}

console.log(`\nTotal: ${totalChanges} replacement(s) across ${args.length} file(s).`);
