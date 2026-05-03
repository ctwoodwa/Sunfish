import { test } from 'node:test';
import { equal } from 'node:assert/strict';
import { scanResxComment } from '../check.mjs';

test('scanner flags unescaped < in comment', () => {
  const finding = scanResxComment('Common <script>alert(1)</script> verb');
  equal(finding.violation, true);
  equal(finding.character, '<');
});

test('scanner flags unescaped > in comment', () => {
  const finding = scanResxComment('Result -> next state');
  equal(finding.violation, true);
});

test('scanner flags unescaped & in comment', () => {
  const finding = scanResxComment('A & B operator');
  equal(finding.violation, true);
});

test('scanner accepts properly-escaped XML entities', () => {
  const finding = scanResxComment('Common &lt;script&gt; verb is fine');
  equal(finding.violation, false);
});

test('scanner accepts numeric entities', () => {
  const finding = scanResxComment('Use &#0026; for ampersand');
  equal(finding.violation, false);
});

test('scanner accepts plain ASCII text', () => {
  const finding = scanResxComment('Common verb on submit/commit buttons.');
  equal(finding.violation, false);
});
