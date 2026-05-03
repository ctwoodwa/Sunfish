#!/usr/bin/env node
/**
 * Tests for the locale-completeness-check tool. Synthetic-fixture-driven so the test
 * runs without depending on real Sunfish .resx files (the cascade hasn't scaffolded
 * them yet). Each test sets up a temp directory mimicking the Sunfish layout
 * (i18n/locales.json + packages/<x>/Resources/<bundle>.resx + locale satellites)
 * and runs check.mjs against it via --root.
 */

import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

const CHECK_SCRIPT = join(import.meta.dirname, '..', 'check.mjs');

let passed = 0;
let failed = 0;
const results = [];

function assert(cond, message) {
  if (!cond) throw new Error(`assertion failed: ${message}`);
}

function withTempRoot(testFn) {
  const dir = mkdtempSync(join(tmpdir(), 'sunfish-loc-test-'));
  try { return testFn(dir); }
  finally { rmSync(dir, { recursive: true, force: true }); }
}

function writeLocales(root, locales) {
  mkdirSync(join(root, 'i18n'), { recursive: true });
  writeFileSync(
    join(root, 'i18n', 'locales.json'),
    JSON.stringify({ source: 'en-US', locales }),
    'utf8',
  );
}

function writeResx(root, packageName, suffix, entries) {
  const dir = join(root, 'packages', packageName, 'Resources');
  mkdirSync(dir, { recursive: true });
  const dataXml = entries.map(([k, v]) =>
    `  <data name="${k}" xml:space="preserve">\n    <value>${v}</value>\n  </data>`).join('\n');
  const fileName = suffix ? `SharedResource.${suffix}.resx` : 'SharedResource.resx';
  writeFileSync(
    join(dir, fileName),
    `<?xml version="1.0" encoding="utf-8"?>\n<root>\n${dataXml}\n</root>\n`,
    'utf8',
  );
}

function runCheck(root, extraArgs = []) {
  const result = spawnSync('node', [CHECK_SCRIPT, '--root', root, '--json', ...extraArgs], {
    encoding: 'utf8',
  });
  return {
    stdout: result.stdout,
    stderr: result.stderr,
    code: result.status,
    parsed: result.stdout ? JSON.parse(result.stdout) : null,
  };
}

function test(name, fn) {
  try {
    fn();
    passed++;
    results.push({ name, ok: true });
  } catch (err) {
    failed++;
    results.push({ name, ok: false, error: err.message });
  }
}

// ---- Tests ------------------------------------------------------------------

test('no resx files → no findings, no fail', () => {
  withTempRoot(root => {
    writeLocales(root, [
      { tag: 'en-US', status: 'complete', completenessFloor: 100 },
      { tag: 'ar-SA', status: 'complete', completenessFloor: 95 },
    ]);
    const result = runCheck(root, ['--fail-on-incomplete']);
    assert(result.code === 0, `expected exit 0, got ${result.code}: ${result.stderr}`);
    assert(result.parsed.bundles === 0, 'expected zero bundles');
    assert(result.parsed.findings.length === 0, 'expected zero findings');
  });
});

test('source-only resx → zero locale satellites; all locales 0%', () => {
  withTempRoot(root => {
    writeLocales(root, [
      { tag: 'en-US', status: 'complete', completenessFloor: 100 },
      { tag: 'ar-SA', status: 'complete', completenessFloor: 95 },
    ]);
    writeResx(root, 'foundation', null, [
      ['greeting', 'Hello'],
      ['farewell', 'Goodbye'],
    ]);
    const result = runCheck(root);
    assert(result.parsed.bundles === 1, `expected 1 bundle, got ${result.parsed.bundles}`);
    assert(result.parsed.findings.length === 1, 'one bundle × one target locale');
    const f = result.parsed.findings[0];
    assert(f.locale === 'ar-SA', 'expected ar-SA finding');
    assert(f.percentage === 0, `expected 0%, got ${f.percentage}`);
    assert(f.passing === false, 'should fail floor=95');
  });
});

test('full coverage on satellite → 100%, passes', () => {
  withTempRoot(root => {
    writeLocales(root, [
      { tag: 'en-US', status: 'complete', completenessFloor: 100 },
      { tag: 'ar-SA', status: 'complete', completenessFloor: 95 },
    ]);
    writeResx(root, 'foundation', null, [
      ['greeting', 'Hello'],
      ['farewell', 'Goodbye'],
    ]);
    writeResx(root, 'foundation', 'ar-SA', [
      ['greeting', 'مرحبا'],
      ['farewell', 'وداعا'],
    ]);
    const result = runCheck(root, ['--fail-on-incomplete']);
    assert(result.code === 0, `expected exit 0, got ${result.code}`);
    const f = result.parsed.findings[0];
    assert(f.percentage === 100, `expected 100%, got ${f.percentage}`);
    assert(f.passing === true, 'should pass');
  });
});

test('partial coverage → percentage proportional; floor pass/fail enforced', () => {
  withTempRoot(root => {
    writeLocales(root, [
      { tag: 'en-US', status: 'complete', completenessFloor: 100 },
      { tag: 'ja',    status: 'complete', completenessFloor: 95 },
      { tag: 'ko',    status: 'bake-in',  completenessFloor: 40 },
    ]);
    writeResx(root, 'blocks-test', null, [
      ['k1', 'v1'], ['k2', 'v2'], ['k3', 'v3'], ['k4', 'v4'], ['k5', 'v5'],
    ]);
    writeResx(root, 'blocks-test', 'ja', [['k1', 'val1'], ['k2', 'val2']]);  // 40%
    writeResx(root, 'blocks-test', 'ko', [['k1', 'val1'], ['k2', 'val2']]);  // 40%

    const result = runCheck(root, ['--fail-on-incomplete']);
    assert(result.code === 1, `expected exit 1 (ja below floor), got ${result.code}`);
    const ja = result.parsed.findings.find(f => f.locale === 'ja');
    const ko = result.parsed.findings.find(f => f.locale === 'ko');
    assert(ja.percentage === 40, `ja: expected 40%, got ${ja.percentage}`);
    assert(ja.passing === false, 'ja complete tier with 40% should fail floor=95');
    assert(ko.percentage === 40, `ko: expected 40%, got ${ko.percentage}`);
    assert(ko.passing === true, 'ko bake-in tier with 40% meets floor=40');
  });
});

test('empty <value> entries do not count toward presence', () => {
  withTempRoot(root => {
    writeLocales(root, [
      { tag: 'en-US', status: 'complete', completenessFloor: 100 },
      { tag: 'ja',    status: 'complete', completenessFloor: 95 },
    ]);
    writeResx(root, 'blocks-empty', null, [['k1', 'v1'], ['k2', 'v2']]);
    writeResx(root, 'blocks-empty', 'ja', [['k1', 'val1'], ['k2', '']]);
    const result = runCheck(root);
    const f = result.parsed.findings[0];
    assert(f.percentage === 50, `empty value should not count; expected 50%, got ${f.percentage}`);
  });
});

test('fail-on-incomplete only fails when at least one locale is below floor', () => {
  withTempRoot(root => {
    writeLocales(root, [
      { tag: 'en-US', status: 'complete', completenessFloor: 100 },
      { tag: 'ja',    status: 'complete', completenessFloor: 95 },
    ]);
    writeResx(root, 'blocks-good', null, [['k1', 'v1']]);
    writeResx(root, 'blocks-good', 'ja', [['k1', 'val1']]);  // 100%
    const result = runCheck(root, ['--fail-on-incomplete']);
    assert(result.code === 0, 'should pass when all locales meet floor');
  });
});

// ---- Report -----------------------------------------------------------------

console.log(`Tests: ${passed} passed, ${failed} failed`);
for (const r of results) {
  console.log(`${r.ok ? '✓' : '✗'} ${r.name}${r.error ? ` — ${r.error}` : ''}`);
}
if (failed > 0) process.exit(1);
