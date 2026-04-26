#!/usr/bin/env node
import { allocateShard } from '../src/shard-allocator.mjs';
import { execFileSync } from 'node:child_process';
import { readFileSync, existsSync } from 'node:fs';

const args = process.argv.slice(2);
const shardIdx = parseInt(args[args.indexOf('--shard') + 1], 10);
const totalShards = parseInt(args[args.indexOf('--total-shards') + 1], 10);

if (Number.isNaN(shardIdx) || Number.isNaN(totalShards)) {
  console.error('Usage: run.mjs --shard N --total-shards M');
  process.exit(2);
}

const indexPath = 'packages/ui-core/storybook-static/index.json';
if (!existsSync(indexPath)) {
  console.error(`Storybook index not found at ${indexPath}; run pnpm --filter @sunfish/ui-core build-storybook first.`);
  process.exit(1);
}

const storyIndex = JSON.parse(readFileSync(indexPath, 'utf8'));
const allStories = Object.keys(storyIndex.entries);
const myStories = allocateShard(allStories, shardIdx, totalShards);

console.log(`Shard ${shardIdx}/${totalShards}: ${myStories.length} stories of ${allStories.length} total`);

if (myStories.length === 0) {
  console.log('No stories assigned to this shard; exiting cleanly.');
  process.exit(0);
}

// IMPORTANT: execFileSync with array args — no shell interpolation, no command-injection vector.
// Story IDs originate from a JSON file on disk; treat them as untrusted-shaped data and never
// pass them through a shell. See Plan 5 Threat Model.
execFileSync('pnpm', ['test-storybook', '--include-tags', myStories.join(',')], { stdio: 'inherit' });
