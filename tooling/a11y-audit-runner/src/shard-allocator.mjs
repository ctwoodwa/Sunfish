import { createHash } from 'node:crypto';

/**
 * Deterministically allocate stories to a single shard using SHA-256 hash mod N.
 *
 * Properties:
 *  - Deterministic: identical inputs → identical outputs across machines / runs.
 *  - Partitioning: union over all shardIndex in [0, totalShards) yields every input
 *    story exactly once (no duplicates, no drops).
 *  - Stable under reordering: input order does not affect which shard a story lands on.
 *
 * Used by the a11y audit CI gate (Plan 5) to fan story-driven Storybook tests across
 * 4 shards in parallel without coordination overhead.
 *
 * @param {string[]} stories     Storybook story IDs (e.g. "button--default")
 * @param {number}   shardIndex  0-based shard index, must be in [0, totalShards)
 * @param {number}   totalShards Total shard count, must be >= 1
 * @returns {string[]} subset of `stories` assigned to this shard
 */
export function allocateShard(stories, shardIndex, totalShards) {
  return stories.filter(id => {
    const hash = createHash('sha256').update(id).digest();
    const bucket = hash.readUInt32BE(0) % totalShards;
    return bucket === shardIndex;
  });
}
