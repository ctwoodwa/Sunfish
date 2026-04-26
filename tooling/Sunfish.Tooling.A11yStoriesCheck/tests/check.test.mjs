/**
 * Tests for the a11y-stories-check tool. Synthetic-fixture-driven so the test
 * runs without depending on the real Sunfish ui-core layout (which may evolve
 * faster than this test).
 *
 * Pattern mirrors tooling/locale-completeness-check/tests/fixture-test.mjs:
 * each test sets up a temp directory mimicking
 * packages/ui-core/src/components/<component>/<files>, then invokes check.mjs
 * via --root + --components-dir.
 */

import { test } from 'node:test';
import { equal, ok } from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

const CHECK_SCRIPT = join(import.meta.dirname, '..', 'check.mjs');

function withTempRoot(testFn) {
  const dir = mkdtempSync(join(tmpdir(), 'sunfish-a11y-stories-test-'));
  try { return testFn(dir); }
  finally { rmSync(dir, { recursive: true, force: true }); }
}

function writeComponent(root, componentName, options = {}) {
  const { withStories = true } = options;
  const dir = join(root, 'packages/ui-core/src/components', componentName);
  mkdirSync(dir, { recursive: true });
  writeFileSync(
    join(dir, `sunfish-${componentName}.ts`),
    `// Lit component for ${componentName}\nexport class Sunfish${componentName} {}\n`,
    'utf8',
  );
  if (withStories) {
    writeFileSync(
      join(dir, `sunfish-${componentName}.stories.ts`),
      `// Storybook stories for sunfish-${componentName}\nexport default { title: '${componentName}' };\n`,
      'utf8',
    );
  }
}

function runCheck(root, extraArgs = []) {
  const result = spawnSync('node', [
    CHECK_SCRIPT,
    '--root', root,
    '--components-dir', 'packages/ui-core/src/components',
    '--json',
    ...extraArgs,
  ], { encoding: 'utf8' });
  return {
    stdout: result.stdout,
    stderr: result.stderr,
    code: result.status,
    parsed: result.stdout ? JSON.parse(result.stdout) : null,
  };
}

test('component WITH sibling stories file → no missing finding', () => {
  withTempRoot(root => {
    writeComponent(root, 'button', { withStories: true });
    const result = runCheck(root, ['--fail-on-missing']);
    equal(result.code, 0, `expected exit 0, got ${result.code}: ${result.stderr}`);
    equal(result.parsed.checked, 1);
    equal(result.parsed.missing, 0);
    equal(result.parsed.findings[0].hasStories, true);
    equal(result.parsed.findings[0].diagnosticId, 'SUNFISH_A11Y_001');
  });
});

test('component WITHOUT sibling stories file → emits finding + exits 1 with --fail-on-missing', () => {
  withTempRoot(root => {
    writeComponent(root, 'dialog', { withStories: false });
    const result = runCheck(root, ['--fail-on-missing']);
    equal(result.code, 1, `expected exit 1, got ${result.code}: ${result.stderr}`);
    equal(result.parsed.missing, 1);
    const finding = result.parsed.findings[0];
    equal(finding.hasStories, false);
    equal(finding.component, 'dialog');
    ok(finding.componentFile.endsWith('sunfish-dialog.ts'),
      `componentFile should end with sunfish-dialog.ts, got ${finding.componentFile}`);
    ok(finding.expectedStoriesFile.endsWith('sunfish-dialog.stories.ts'),
      `expectedStoriesFile should end with sunfish-dialog.stories.ts, got ${finding.expectedStoriesFile}`);
  });
});

test('mixed components → only the missing one is flagged; no --fail-on-missing → exit 0', () => {
  withTempRoot(root => {
    writeComponent(root, 'button', { withStories: true });
    writeComponent(root, 'dialog', { withStories: true });
    writeComponent(root, 'syncstate', { withStories: false });
    const result = runCheck(root); // no --fail-on-missing
    equal(result.code, 0, `expected exit 0 (report-only), got ${result.code}: ${result.stderr}`);
    equal(result.parsed.checked, 3);
    equal(result.parsed.missing, 1);
    const missing = result.parsed.findings.filter(f => !f.hasStories);
    equal(missing.length, 1);
    equal(missing[0].component, 'syncstate');
  });
});
