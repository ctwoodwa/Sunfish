import { test } from 'node:test';
import { equal, deepEqual } from 'node:assert/strict';
import { allocateShard } from '../src/shard-allocator.mjs';

test('allocator is deterministic — same story IDs always go to same shard', () => {
  const stories = ['button--default', 'dialog--open', 'syncstate--healthy'];
  const r1 = allocateShard(stories, 0, 4);
  const r2 = allocateShard(stories, 0, 4);
  deepEqual(r1, r2);
});

test('allocator partitions all stories across N shards (no duplicates, no drops)', () => {
  const stories = Array.from({length: 1000}, (_, i) => `story-${i}`);
  const all = [];
  for (let i = 0; i < 4; i++) all.push(...allocateShard(stories, i, 4));
  equal(all.length, 1000);
  equal(new Set(all).size, 1000);
});

test('allocator handles empty input', () => {
  deepEqual(allocateShard([], 0, 4), []);
});

test('allocator handles totalShards of 1 — all stories on shard 0', () => {
  const stories = ['a', 'b', 'c'];
  deepEqual(allocateShard(stories, 0, 1), stories);
  deepEqual(allocateShard(stories, 1, 1), []);
});
