import { test } from 'node:test';
import { equal, deepEqual } from 'node:assert/strict';
import { parseStatusTable, evaluateHealth } from '../check.mjs';

test('parser extracts plan verdicts from markdown table', () => {
  const md = `## Plans authored
| Plan | Scope | Weeks | Lines | Status |
|---|---|---|---|---|
| [Plan 2](...) | Loc-Infra | 2-4 | 448 | **GREEN — COMPLETE** |
| [Plan 3](...) | Translator | 2-4 | 531 | **RED** — 1 of 19 |
`;
  const verdicts = parseStatusTable(md);
  equal(verdicts.length, 2);
  equal(verdicts[0].plan, 'Plan 2');
  equal(verdicts[0].verdict, 'GREEN');
  equal(verdicts[1].verdict, 'RED');
});

test('evaluateHealth flags RED plans', () => {
  const verdicts = [
    { plan: 'Plan 2', verdict: 'GREEN' },
    { plan: 'Plan 3', verdict: 'RED' },
  ];
  const result = evaluateHealth(verdicts);
  equal(result.healthy, false);
  equal(result.redPlans.length, 1);
  equal(result.redPlans[0], 'Plan 3');
});

test('evaluateHealth passes when all GREEN/YELLOW', () => {
  const verdicts = [
    { plan: 'Plan 2', verdict: 'GREEN' },
    { plan: 'Plan 4', verdict: 'YELLOW' },
  ];
  const result = evaluateHealth(verdicts);
  equal(result.healthy, true);
});
