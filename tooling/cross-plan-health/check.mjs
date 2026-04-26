// Plan 5 Task 7 — cross-plan health gate (Wave 1 finding carry-forward).
//
// Reads waves/global-ux/status.md, parses each plan's verdict from the
// "Plans authored" section, and exits 1 if any plan is RED. Lets CI surface
// cross-plan health drift to the human owner per Plan 5 spec / Wave 1 finding.
//
// SECURITY: no shell interpolation; the markdown path is read via
// readFileSync only. The CLI argument is treated as a filesystem path.

export function parseStatusTable(markdown) {
  // Find the "Plans authored" table; parse rows for Plan name + verdict.
  const lines = markdown.split('\n');
  const verdicts = [];
  let inTable = false;
  for (const line of lines) {
    if (/^##\s+Plans authored/i.test(line)) { inTable = true; continue; }
    if (inTable && /^##\s/.test(line)) break; // end of section
    if (!inTable) continue;
    if (!line.startsWith('|')) continue;
    if (line.match(/^\|\s*Plan\s*\|/i) || line.match(/^\|\s*-+\s*\|/)) continue;
    // Row: | [Plan N](...) | scope | weeks | lines | Status... |
    const cells = line.split('|').map(c => c.trim());
    if (cells.length < 6) continue;
    const planMatch = cells[1].match(/\[?(Plan\s+\w+\b)/);
    if (!planMatch) continue;
    const statusCell = cells[5];
    let verdict = 'UNKNOWN';
    if (/RED/.test(statusCell)) verdict = 'RED';
    else if (/YELLOW/.test(statusCell)) verdict = 'YELLOW';
    else if (/GREEN/.test(statusCell)) verdict = 'GREEN';
    else if (/READY/.test(statusCell) || /COMPLETE/.test(statusCell)) verdict = 'GREEN';
    else if (/BLOCKED/.test(statusCell)) verdict = 'RED';
    verdicts.push({ plan: planMatch[1], verdict });
  }
  return verdicts;
}

export function evaluateHealth(verdicts) {
  const redPlans = verdicts.filter(v => v.verdict === 'RED').map(v => v.plan);
  return { healthy: redPlans.length === 0, redPlans, verdicts };
}

import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const path = process.argv[2] || 'waves/global-ux/status.md';
  let md;
  try { md = readFileSync(path, 'utf8'); }
  catch (e) { console.error(`Cannot read ${path}: ${e.message}`); process.exit(2); }
  const verdicts = parseStatusTable(md);
  const result = evaluateHealth(verdicts);
  console.log(`Cross-plan health: ${result.healthy ? 'GREEN' : 'RED'}`);
  for (const v of result.verdicts) console.log(`  ${v.plan}: ${v.verdict}`);
  if (!result.healthy) {
    console.error(`\nSUNFISH_PLAN_HEALTH: ${result.redPlans.length} RED plan(s): ${result.redPlans.join(', ')}`);
    console.error('Surface to human owner; consider re-prioritization gate per Plan 5 spec.');
    process.exit(1);
  }
}
